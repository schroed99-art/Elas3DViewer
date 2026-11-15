using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Elas3DView
{
    /// <summary>
    /// Eine Zeile der Dateiliste. Eigenschaften statt Felder, weil die Zeile seit
    /// dem 14.11.2026 ein Häkchen trägt und die Liste es über eine Bindung liest.
    /// </summary>
    public class FileEntry : System.ComponentModel.INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsImage { get; set; }

        private bool _marked;

        /// <summary>
        /// Angehakt: Diese Datei geht bei „Ins PrintFlow …" MIT in die Akte des
        /// geöffneten Modells — als weitere Quelldatei, nicht als eigenes Modell.
        /// </summary>
        public bool Marked
        {
            get { return _marked; }
            set
            {
                if (_marked == value) return;
                _marked = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("Marked"));
                if (MarkedChanged != null) MarkedChanged(this);
            }
        }

        /// <summary>Bilder haben kein Häkchen: Zur Akte gehören nur STL und 3MF.</summary>
        public Visibility MarkVisibility
        {
            get { return IsImage ? Visibility.Collapsed : Visibility.Visible; }
        }

        /// <summary>Das Fenster hört hier mit, um die Auswahl über ein Neuladen der Liste zu halten.</summary>
        public Action<FileEntry> MarkedChanged;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public override string ToString() { return Name; }
    }

    public class MainWindow : Window
    {
        // ---- Szene -----------------------------------------------------
        private Viewport3D _viewport;
        private Grid _viewHost;
        private Border _viewFrame;
        private Model3DGroup _liveScene;
        private GeometryModel3D _liveMesh;
        private AmbientLight _ambient;
        private DirectionalLight _key, _fill, _rim;
        private Material _material, _backMaterial;

        private LoadedModel _model;
        private string _currentFile;     // ausgewählte Datei - Modell oder Bild
        private string _modelFile;       // zuletzt geladenes 3D-Modell
        private string _currentFolder;
        private bool _showingImage;
        private Image _imageView;
        private int _kind;               // 0 = Modelle, 1 = Bilder, 2 = alles
        private Button[] _kindBtns;
        private Button _btnImageToggle;

        // ---- Kamera ----------------------------------------------------
        private const double FovDeg = 45.0;
        private double _yaw = -55, _pitch = 28, _distance = 200, _fitDistance = 200;
        private Point3D _target = new Point3D(0, 0, 0);
        private bool _ortho;

        private Point _dragStart;
        private int _dragMode;            // 0 = keine, 1 = drehen, 2 = verschieben
        private bool _rightDragged;

        // ---- Oberfläche ------------------------------------------------
        private ListBox _fileList;
        private TextBlock _folderLabel, _countLabel, _statusFile, _statusInfo, _emptyHint;
        private ComboBox _cbBackground, _cbFinish, _cbWidth, _cbJpgBg, _cbLogo;
        private Slider _sldLogo;
        private TextBlock _lblLogoSize;
        private PositionPicker _logoPos;
        private Image _logoImage;
        private string _logoFile;
        private double _logoPercent = 16;
        private bool _suppressLogo;
        private bool _suppressBg;
        private bool _contrastAccepted;
        // Für welchen Grund die Zusage gilt: das JPG (Bildgrund) oder das Titelbild (Ansicht).
        private bool _contrastAcceptedLikeView;
        private int _bgPreset;
        private string _bgImage;
        private ColorPickerButton _colorBtn;
        private CheckBox _chkOrtho;
        private Button _themeBtn;
        private Border _busy, _scrim, _toast;
        private TextBlock _busyText, _toastText;
        private Button _busyCancel;
        private ContextMenu _fileMenu, _viewMenu;
        private DispatcherTimer _toastTimer;

        private bool _suppressSelection;
        private bool _busyActive;
        private bool _cancelRequested;
        private string _pendingFile;

        // ---- ElasPrintFlow ---------------------------------------------
        private PrintFlowClient _pf;
        private string _pfUrl = PrintFlowClient.DefaultUrl;
        private string _pfUser = "";
        private string _pfSession = "";      // nur die Sitzung, nie das Passwort
        private string _pfCategory = "";
        private System.Threading.CancellationTokenSource _pfCancel;

        private Color _modelColor = Color.FromRgb(0xB6, 0xBC, 0xC4);
        private readonly List<FileEntry> _entries = new List<FileEntry>();

        private static readonly string[] BackgroundNames =
            { "Wie Design", "Hell", "Dunkel", "Weiß", "Blau", "Grau" };
        private static readonly string[] FinishNames =
            { "Matt", "Seidenmatt", "Glänzend", "Metallic" };
        private static readonly string[] JpgBgNames =
            { "Weiß", "Wie Ansicht", "Hell", "Dunkel", "Grau" };
        private static readonly int[] WidthValues = { 1280, 1600, 1920, 2560, 3840 };

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string a, string b);

        public MainWindow()
        {
            Title = "Elas3DView";
            Width = 1340;
            Height = 860;
            MinWidth = 980;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            AllowDrop = true;
            SetResourceReference(BackgroundProperty, "Bg");

            BuildUi();
            HookCommands();

            Drop += OnDrop;
            DragOver += OnDragOver;
            Loaded += OnLoaded;
            Activated += OnWindowActivated;
            SourceInitialized += delegate { Theme.ApplyTitleBar(this); };
            Theme.Changed += delegate { OnThemeChanged(); };
            Closing += delegate { SaveSettings(); };
        }

        public void QueueInitialFile(string path) { _pendingFile = path; }

        // =================================================================
        //  Aufbau der Oberfläche
        // =================================================================

        private void BuildUi()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            root.Children.Add(BuildHeader());

            Grid main = new Grid();
            main.Margin = new Thickness(10, 0, 10, 0);
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(268) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(main, 1);
            root.Children.Add(main);

            main.Children.Add(BuildFilePanel());

            GridSplitter split = new GridSplitter();
            split.Width = 8;
            split.HorizontalAlignment = HorizontalAlignment.Stretch;
            split.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetColumn(split, 1);
            main.Children.Add(split);

            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(right, 2);
            main.Children.Add(right);

            right.Children.Add(BuildViewport());

            UIElement bar = BuildViewBar();
            Grid.SetRow(bar, 1);
            right.Children.Add(bar);

            UIElement status = BuildStatusBar();
            Grid.SetRow(status, 2);
            root.Children.Add(status);

            // Überlagerungen über alles
            _scrim = new Border();
            _scrim.Visibility = Visibility.Collapsed;
            _scrim.SetResourceReference(Border.BackgroundProperty, "ScrimBrush");
            Grid.SetRowSpan(_scrim, 3);
            root.Children.Add(_scrim);
        }

        private UIElement BuildHeader()
        {
            StackPanel top = new StackPanel();
            top.Children.Add(BuildBrandBand());

            Border shell = Card();
            shell.Margin = new Thickness(10, 10, 10, 8);
            shell.Padding = new Thickness(14, 9, 14, 9);
            top.Children.Add(shell);

            DockPanel dp = new DockPanel();
            shell.Child = dp;

            WrapPanel actions = new WrapPanel();
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(Primary("Öffnen …", "Modell öffnen (Strg+O)", OnOpenFile));
            actions.Children.Add(Action("Ordner …", "Ordner öffnen (Strg+Umschalt+O)", OnOpenFolder));
            actions.Children.Add(Gap());
            actions.Children.Add(Action("JPG speichern", "Aktuelle Ansicht als JPG neben das Modell legen (Strg+S)", OnSaveJpg));
            actions.Children.Add(Action("Alle als JPG …", "Für jede Datei im Ordner ein JPG erzeugen", OnBatchJpg));
            actions.Children.Add(Gap());
            actions.Children.Add(Action("Ins PrintFlow …",
                "Modell mit Titelbild in ElasPrintFlow anlegen, danach hier aufräumen (Strg+P)",
                OnSendToPrintFlow));
            actions.Children.Add(Gap());
            actions.Children.Add(Action("Umbenennen", "Modelldatei umbenennen (F2)", OnRename));
            Button del = Action("Löschen", "Modell und zugehöriges Bild in den Papierkorb (Entf)", OnDelete);
            del.Style = (Style)Application.Current.Resources["BtnDanger"];
            actions.Children.Add(del);
            dp.Children.Add(actions);

            return top;
        }

        /// <summary>
        /// Das Kopfband, wie ElasPrintFlow es trägt (dort `Titelleiste.tsx`, Handoff
        /// „Kopfbereich", Variante 1c) — Tristan, 14.11.2026: „übernimm die Ansicht
        /// aus ElasPrintFlow, also den Header, mit dem Namen Elas3DView geschrieben
        /// wie ElasPrintFlow."
        ///
        /// Die Maße stehen wörtlich drüben: 90 px hoch, 30 px Rand, die Bildmarke
        /// 50 px, der Name 26 px in zwei Gewichten — „Elas" leicht, der Rest fett,
        /// ohne Lücke, sonst liest es sich als zwei Namen. Darunter der Leitsatz in
        /// der Kursiven, rechts die Pillen, unten eine Goldlinie, die an beiden
        /// Enden ausläuft.
        ///
        /// Das Band bleibt dunkel, auch im hellen Design: Die Bildmarke ist die
        /// helle Fassung, und drüben ist das Band ebenfalls nicht Teil des Themas.
        /// Deshalb stehen hier feste Farben und keine Verweise auf die Palette.
        ///
        /// Manrope und Cormorant Garamond sind in die exe eingebettet (BrandFonts.cs);
        /// lassen sie sich nicht laden, greifen Segoe UI und Constantia.
        /// </summary>
        private UIElement BuildBrandBand()
        {
            Grid band = new Grid();
            band.Height = 90;
            band.ClipToBounds = true;
            band.SnapsToDevicePixels = true;

            // linear-gradient(100deg, #1E1826 0%, #171320 55%, #15121A 100%)
            LinearGradientBrush ground = new LinearGradientBrush();
            ground.StartPoint = new Point(0, -0.8);
            ground.EndPoint = new Point(1, 1.8);
            ground.GradientStops.Add(new GradientStop(Color.FromRgb(0x1E, 0x18, 0x26), 0));
            ground.GradientStops.Add(new GradientStop(Color.FromRgb(0x17, 0x13, 0x20), 0.55));
            ground.GradientStops.Add(new GradientStop(Color.FromRgb(0x15, 0x12, 0x1A), 1));
            ground.Freeze();
            band.Background = ground;

            // Der Lichtschein liegt im Band, nicht darüber — das Band schneidet ihn ab.
            Canvas lights = new Canvas();
            lights.IsHitTestVisible = false;
            System.Windows.Shapes.Ellipse glow = new System.Windows.Shapes.Ellipse();
            glow.Width = 420;
            glow.Height = 220;
            RadialGradientBrush shine = new RadialGradientBrush();
            shine.MappingMode = BrushMappingMode.Absolute;
            shine.Center = new Point(210, 110);
            shine.GradientOrigin = new Point(210, 110);
            shine.RadiusX = 237;
            shine.RadiusY = 237;
            shine.GradientStops.Add(new GradientStop(Color.FromArgb(41, 0xD3, 0x9A, 0x39), 0));
            shine.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xD3, 0x9A, 0x39), 0.68));
            shine.Freeze();
            glow.Fill = shine;
            Canvas.SetLeft(glow, 120);
            Canvas.SetTop(glow, -90);
            lights.Children.Add(glow);
            band.Children.Add(lights);

            DockPanel row = new DockPanel();
            row.LastChildFill = false;
            row.Margin = new Thickness(30, 0, 30, 0);
            band.Children.Add(row);

            BitmapSource markSource = BrandMark();
            if (markSource != null)
            {
                Image mark = new Image();
                mark.Source = markSource;
                mark.Height = 50;
                mark.Stretch = Stretch.Uniform;
                mark.VerticalAlignment = VerticalAlignment.Center;
                mark.Margin = new Thickness(0, 0, 20, 0);
                RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
                DockPanel.SetDock(mark, Dock.Left);
                row.Children.Add(mark);
            }

            StackPanel words = new StackPanel();
            words.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(words, Dock.Left);
            row.Children.Add(words);

            // Der Name wörtlich wie drüben: Manrope 26 px in zwei Gewichten,
            // Laufweite −0,025 Geviert, Zeilenhöhe 1,1. Die Schriften sind
            // eingebettet (BrandFonts.cs). Stehen sie nicht bereit, greifen Segoe UI
            // und Constantia — dann ohne die enge Laufweite und mit kleinerem
            // Leitsatz, denn beide laufen breiter als die Hausschriften.
            bool brand = BrandFonts.Ready;
            FontFamily sans = BrandFonts.Sans();
            TrackedWord name = new TrackedWord(26, brand ? -0.025 : 0, 28.6, FixedBrush(0xFB, 0xF4, 0xEE));
            name.Add("Elas", sans, FontWeights.Light);
            name.Add("3DView", sans, FontWeights.Bold);
            name.HorizontalAlignment = HorizontalAlignment.Left;
            words.Children.Add(name);

            TextBlock motto = new TextBlock();
            motto.Text = "Vom Modell zum Vorschaubild";
            motto.FontFamily = BrandFonts.Serif();
            motto.FontStyle = FontStyles.Italic;
            motto.FontSize = brand ? 17 : 15.5;
            if (brand)
            {
                motto.LineHeight = 22;      // 17 px × 1,3
                motto.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            }
            motto.Margin = new Thickness(brand ? 0 : 1, 4, 0, 0);
            motto.Foreground = FixedBrush(0xC9, 0xA9, 0x8C);
            words.Children.Add(motto);

            _themeBtn = Styled("BtnBand", "Helles Design", "Zwischen hellem und dunklem Design wechseln", OnToggleTheme);
            _themeBtn.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(_themeBtn, Dock.Right);
            row.Children.Add(_themeBtn);

            // Die Fassung steht oben, wo man sie nicht suchen muss — nach einem
            // Setup zeigt ein Blick, ob wirklich das Neue läuft.
            Button version = Styled("BtnBand", "v" + AppVersion.Current,
                "Version " + AppVersion.Current + " — zeigen, was sich geändert hat", OnShowNotes);
            version.VerticalAlignment = VerticalAlignment.Center;
            version.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(version, Dock.Right);
            row.Children.Add(version);

            // Die Abschlusslinie läuft an beiden Enden aus — eine durchgezogene
            // Kante schnitte das Band vom Rest ab, statt es abzuschließen.
            Border line = new Border();
            line.Height = 1;
            line.VerticalAlignment = VerticalAlignment.Bottom;
            line.IsHitTestVisible = false;
            LinearGradientBrush gold = new LinearGradientBrush();
            gold.StartPoint = new Point(0, 0);
            gold.EndPoint = new Point(1, 0);
            gold.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xD3, 0x9A, 0x39), 0));
            gold.GradientStops.Add(new GradientStop(Color.FromArgb(128, 0xD3, 0x9A, 0x39), 0.22));
            gold.GradientStops.Add(new GradientStop(Color.FromArgb(128, 0xD3, 0x9A, 0x39), 0.78));
            gold.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xD3, 0x9A, 0x39), 1));
            gold.Freeze();
            line.Background = gold;
            band.Children.Add(line);

            return band;
        }

        private void OnShowNotes(object sender, RoutedEventArgs e)
        {
            new ReleaseNotesDialog().ShowOver(this);
        }

        private static SolidColorBrush FixedBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Die Bildmarke des Kopfbands — dieselbe wie in ElasPrintFlow
        /// (`public\marke\logo-marke-hell.png`), verkleinert in die exe eingebettet.
        /// Sie ist kein Wasserzeichen und steht deshalb nicht in der Logo-Auswahl.
        /// </summary>
        private static BitmapSource BrandMark()
        {
            try
            {
                using (Stream s = System.Reflection.Assembly.GetExecutingAssembly()
                                        .GetManifestResourceStream("Kopfband-Marke.png"))
                {
                    if (s == null) return null;
                    BitmapImage img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = s;
                    img.EndInit();
                    img.Freeze();
                    return img;
                }
            }
            catch { return null; }
        }

        private UIElement BuildFilePanel()
        {
            Border shell = Card();
            shell.Margin = new Thickness(0, 0, 0, 0);
            shell.Padding = new Thickness(10, 10, 6, 10);

            DockPanel dp = new DockPanel();
            shell.Child = dp;

            // Überschrift und, rechts daneben, „Aktualisieren" (Tristan, 14.11.2026:
            // „wenn während der Arbeit neue Dateien hinzugekommen sind, dass die
            // Anzeige erneuert wird"). F5 tat das schon immer — nur stand es
            // nirgends, und es sagte nicht, ob sich etwas getan hat.
            DockPanel headRow = new DockPanel();
            headRow.Margin = new Thickness(4, 0, 4, 6);
            DockPanel.SetDock(headRow, Dock.Top);
            dp.Children.Add(headRow);

            Button refresh = Chip("Aktualisieren",
                "Ordner neu einlesen: zeigt, was inzwischen dazugekommen oder verschwunden ist (F5)", OnRefresh);
            refresh.Margin = new Thickness(0);
            DockPanel.SetDock(refresh, Dock.Right);
            headRow.Children.Add(refresh);

            TextBlock head = new TextBlock();
            head.Text = "DATEIEN";
            head.FontSize = 10.5;
            head.FontWeight = FontWeights.SemiBold;
            head.VerticalAlignment = VerticalAlignment.Center;
            head.SetResourceReference(TextBlock.ForegroundProperty, "GoldText");
            headRow.Children.Add(head);

            _folderLabel = new TextBlock();
            _folderLabel.Text = "Kein Ordner gewählt";
            _folderLabel.FontSize = 11.5;
            _folderLabel.TextTrimming = TextTrimming.CharacterEllipsis;
            _folderLabel.Margin = new Thickness(4, 0, 4, 9);
            _folderLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            DockPanel.SetDock(_folderLabel, Dock.Top);
            dp.Children.Add(_folderLabel);

            // Umschalter: zeigt die Liste Modelle, Bilder oder beides?
            WrapPanel kinds = new WrapPanel();
            kinds.Margin = new Thickness(2, 0, 0, 8);
            DockPanel.SetDock(kinds, Dock.Top);
            string[] kindNames = { "Modelle", "Bilder", "Alle" };
            string[] kindTips =
            {
                "Nur STL- und 3MF-Dateien zeigen",
                "Nur Bilddateien des Ordners zeigen",
                "Modelle und Bilder gemeinsam zeigen"
            };
            _kindBtns = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                Button b = Chip(kindNames[i], kindTips[i], delegate { SetKind(captured); });
                b.Margin = new Thickness(0, 0, 4, 0);
                _kindBtns[i] = b;
                kinds.Children.Add(b);
            }
            dp.Children.Add(kinds);

            _countLabel = new TextBlock();
            _countLabel.FontSize = 11;
            _countLabel.Margin = new Thickness(4, 8, 0, 0);
            _countLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            DockPanel.SetDock(_countLabel, Dock.Bottom);
            dp.Children.Add(_countLabel);

            _fileList = new ListBox();
            _fileList.Style = (Style)Application.Current.Resources["List"];
            _fileList.ItemContainerStyle = (Style)Application.Current.Resources["ListItem"];
            _fileList.ItemTemplate = FileRowTemplate();
            _fileList.SelectionChanged += OnFileSelected;
            _fileList.PreviewMouseRightButtonDown += OnListRightDown;
            _fileMenu = BuildFileMenu();
            _fileList.ContextMenu = _fileMenu;
            dp.Children.Add(_fileList);

            return shell;
        }

        /// <summary>
        /// Eine Zeile der Dateiliste: der Name, rechts ein Häkchen (Tristan,
        /// 14.11.2026: „ich wähle ein Modell und markiere weitere Modelle, die
        /// unter dieser Modellakte mit abgelegt werden").
        ///
        /// DAS HÄKCHEN WÄHLT NICHT AUS. Ein Klick darauf lädt das Modell nicht —
        /// sonst wechselte mit jedem Anhaken die Hauptdatei. Geöffnet wird über
        /// den Namen, angehakt über das Kästchen; das eine ist „dieses Modell",
        /// das andere „gehört dazu".
        /// </summary>
        private static DataTemplate FileRowTemplate()
        {
            const string xaml = @"
<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
  <DockPanel LastChildFill='True'>
    <CheckBox DockPanel.Dock='Right' Style='{DynamicResource Chk}' Margin='8,0,0,0'
              Focusable='False' VerticalAlignment='Center'
              IsChecked='{Binding Marked, Mode=TwoWay}'
              Visibility='{Binding MarkVisibility}'
              ToolTip='Gehört mit zur Modellakte: Bei „Ins PrintFlow …“ wird diese Datei im Ordner des geöffneten Modells abgelegt, nicht als eigenes Modell.'/>
    <TextBlock Text='{Binding Name}' TextTrimming='CharacterEllipsis' VerticalAlignment='Center'/>
  </DockPanel>
</DataTemplate>";
            return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        /// <summary>
        /// Die angehakten Dateien — als Pfade, nicht als Zeilen: Die Liste wird bei
        /// jedem Umschalten (Modelle/Bilder/Alle) und nach jedem Löschen neu gebaut,
        /// die Auswahl soll das überstehen.
        /// </summary>
        private readonly HashSet<string> _marked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void OnEntryMarked(FileEntry fe)
        {
            if (fe.Marked) _marked.Add(fe.FullPath);
            else _marked.Remove(fe.FullPath);
            UpdateCount();
        }

        private void UpdateCount()
        {
            string text = _entries.Count == 1 ? "1 Datei" : _entries.Count + " Dateien";
            if (_marked.Count > 0) text += "   ·   " + _marked.Count + " für die Akte angehakt";
            _countLabel.Text = text;
        }

        private void OnClearMarks(object sender, RoutedEventArgs e)
        {
            // Über die Zeilen, damit die Kästchen mitgehen; der Rest fällt aus dem Satz.
            foreach (FileEntry fe in _entries) fe.Marked = false;
            _marked.Clear();
            UpdateCount();
        }

        /// <summary>Die angehakten Modelldateien außer der genannten, in der Reihenfolge der Liste.</summary>
        private List<string> MarkedExtras(string mainFile)
        {
            List<string> extras = new List<string>();
            foreach (FileEntry fe in _entries)
            {
                if (fe.IsImage || !fe.Marked) continue;
                if (string.Equals(fe.FullPath, mainFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(fe.FullPath)) extras.Add(fe.FullPath);
            }
            return extras;
        }

        private UIElement BuildViewport()
        {
            _viewFrame = Card();
            _viewFrame.Margin = new Thickness(0, 0, 0, 8);
            _viewFrame.Padding = new Thickness(0);

            _viewHost = new Grid();
            _viewHost.Focusable = true;
            _viewFrame.Child = _viewHost;

            // Ecken des 3D-Bereichs wirklich abrunden
            _viewHost.SizeChanged += delegate
            {
                _viewHost.Clip = new RectangleGeometry(
                    new Rect(0, 0, _viewHost.ActualWidth, _viewHost.ActualHeight), 13, 13);
                UpdateCamera();
                if (_logoImage != null && _logoImage.Visibility == Visibility.Visible)
                    LayoutLogo(_logoImage, _viewHost.ActualWidth);
            };

            _viewport = new Viewport3D();
            _viewport.IsHitTestVisible = false;   // sonst wird die Maus bei großen Netzen zäh
            _viewHost.Children.Add(_viewport);

            _emptyHint = new TextBlock();
            _emptyHint.Text = "Modell öffnen  ·  oder STL-/3MF-Datei hierher ziehen";
            _emptyHint.HorizontalAlignment = HorizontalAlignment.Center;
            _emptyHint.VerticalAlignment = VerticalAlignment.Center;
            _emptyHint.FontSize = 14;
            _emptyHint.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            _viewHost.Children.Add(_emptyHint);

            // Bildansicht statt 3D — liegt an derselben Stelle, nur eines ist sichtbar.
            _imageView = new Image();
            _imageView.Stretch = Stretch.Uniform;
            _imageView.StretchDirection = StretchDirection.Both;
            _imageView.Margin = new Thickness(14);
            _imageView.IsHitTestVisible = false;
            _imageView.Visibility = Visibility.Collapsed;
            RenderOptions.SetBitmapScalingMode(_imageView, BitmapScalingMode.HighQuality);
            _viewHost.Children.Add(_imageView);

            // Liegt über dem Modell, aber unter Hinweis und Meldung.
            _logoImage = new Image();
            _logoImage.IsHitTestVisible = false;
            _logoImage.Visibility = Visibility.Collapsed;
            RenderOptions.SetBitmapScalingMode(_logoImage, BitmapScalingMode.HighQuality);
            _viewHost.Children.Add(_logoImage);

            BuildBusyOverlay();
            BuildToast();

            _viewMenu = BuildViewMenu();

            _viewHost.MouseLeftButtonDown += OnViewMouseDown;
            _viewHost.MouseRightButtonDown += OnViewMouseDown;
            _viewHost.MouseDown += OnViewMouseDown;
            _viewHost.MouseMove += OnViewMouseMove;
            _viewHost.MouseUp += OnViewMouseUp;
            _viewHost.MouseWheel += OnViewMouseWheel;

            InitScene();
            return _viewFrame;
        }

        private UIElement BuildViewBar()
        {
            Border shell = Card();
            shell.Margin = new Thickness(0, 0, 0, 8);
            shell.Padding = new Thickness(10, 8, 10, 8);

            StackPanel rows = new StackPanel();
            shell.Child = rows;

            WrapPanel wp = new WrapPanel();
            rows.Children.Add(wp);

            wp.Children.Add(Chip("Iso", "Isometrische Ansicht (1)", delegate { SetView(-55, 28); }));
            wp.Children.Add(Chip("Vorne", "Vorderansicht (2)", delegate { SetView(-90, 0); }));
            wp.Children.Add(Chip("Hinten", "Rückansicht (3)", delegate { SetView(90, 0); }));
            wp.Children.Add(Chip("Links", "Ansicht von links (4)", delegate { SetView(180, 0); }));
            wp.Children.Add(Chip("Rechts", "Ansicht von rechts (5)", delegate { SetView(0, 0); }));
            wp.Children.Add(Chip("Oben", "Draufsicht (6)", delegate { SetView(-90, 89.5); }));
            wp.Children.Add(Chip("Unten", "Untersicht (7)", delegate { SetView(-90, -89.5); }));
            wp.Children.Add(Chip("Einpassen", "Modell einpassen (Leertaste)", delegate { FitToModel(true); UpdateCamera(); }));
            wp.Children.Add(Gap());

            _btnImageToggle = Chip("Bild zeigen",
                "Zwischen dem Modell und seinem Vorschaubild wechseln (B)", ToggleImage);
            wp.Children.Add(_btnImageToggle);
            wp.Children.Add(Gap());

            _chkOrtho = new CheckBox();
            _chkOrtho.Content = "Orthogonal";
            _chkOrtho.Style = (Style)Application.Current.Resources["Chk"];
            _chkOrtho.Margin = new Thickness(2, 0, 12, 0);
            _chkOrtho.ToolTip = "Parallelprojektion statt Perspektive";
            _chkOrtho.Checked += delegate { SetProjection(true); };
            _chkOrtho.Unchecked += delegate { SetProjection(false); };
            wp.Children.Add(_chkOrtho);

            wp.Children.Add(Gap());
            _colorBtn = new ColorPickerButton("Modellfarbe");
            _colorBtn.SelectedColor = _modelColor;
            _colorBtn.ToolTip = "Farbe des Modells wählen";
            _colorBtn.ColorChanged += delegate
            {
                _modelColor = _colorBtn.SelectedColor;
                ApplyMaterial();
            };
            wp.Children.Add(Pair("Farbe", _colorBtn));

            _cbFinish = Combo(FinishNames, 1, 116, delegate { ApplyMaterial(); });
            wp.Children.Add(Pair("Oberfläche", _cbFinish));

            _cbBackground = Combo(BackgroundNames, 0, 136, delegate
            {
                if (_suppressBg) return;
                ImageEntry sel = _cbBackground.SelectedItem as ImageEntry;
                _bgImage = sel == null ? null : Path.GetFileName(sel.FullPath);
                if (sel == null) _bgPreset = _cbBackground.SelectedIndex;
                _contrastAccepted = false;
                ApplyBackground();
            });
            _cbBackground.ToolTip = "Fläche hinter dem Modell — Voreinstellung oder eigenes Bild";
            wp.Children.Add(Pair("Hintergrund", _cbBackground));
            wp.Children.Add(Chip("Hintergründe", "Ordner mit den Hintergrundbildern öffnen",
                delegate { OpenFolder(ImageStore.Backgrounds); }));

            // ---- zweite Reihe: alles, was das erzeugte Bild betrifft ----
            WrapPanel wp2 = new WrapPanel();
            wp2.Margin = new Thickness(0, 8, 0, 0);
            rows.Children.Add(wp2);

            string[] widthLabels = new string[WidthValues.Length];
            for (int i = 0; i < WidthValues.Length; i++)
                widthLabels[i] = WidthValues[i].ToString(CultureInfo.InvariantCulture) + " px";
            _cbWidth = Combo(widthLabels, 2, 100, null);
            _cbWidth.ToolTip = "Breite des erzeugten Bildes";
            wp2.Children.Add(Pair("JPG", _cbWidth));

            _cbJpgBg = Combo(JpgBgNames, 0, 112, delegate { _contrastAccepted = false; });
            _cbJpgBg.ToolTip = "Hintergrund im gespeicherten JPG. Weiß ist das, was der Shop erwartet.";
            wp2.Children.Add(Pair("Bildgrund", _cbJpgBg));

            wp2.Children.Add(Gap());

            _cbLogo = new ComboBox();
            _cbLogo.Style = (Style)Application.Current.Resources["Combo"];
            _cbLogo.ItemContainerStyle = (Style)Application.Current.Resources["ComboItem"];
            _cbLogo.Width = 168;
            _cbLogo.Margin = new Thickness(0, 0, 6, 0);
            _cbLogo.ToolTip = "Logo, das im Bild und in der Ansicht liegt";
            _cbLogo.SelectionChanged += delegate { _contrastAccepted = false; UpdateLogo(); };
            wp2.Children.Add(Pair("Logo", _cbLogo));

            _sldLogo = new Slider();
            _sldLogo.Style = (Style)Application.Current.Resources["Sld"];
            _sldLogo.Minimum = 4;
            _sldLogo.Maximum = 45;
            _sldLogo.Value = _logoPercent;
            _sldLogo.Width = 116;
            _sldLogo.Margin = new Thickness(0, 0, 6, 0);
            _sldLogo.ToolTip = "Breite des Logos, gemessen an der Bildbreite";
            _sldLogo.ValueChanged += delegate
            {
                _logoPercent = Math.Round(_sldLogo.Value);
                _contrastAccepted = false;
                UpdateLogo();
            };

            _lblLogoSize = new TextBlock();
            _lblLogoSize.VerticalAlignment = VerticalAlignment.Center;
            _lblLogoSize.Margin = new Thickness(0, 0, 6, 0);
            _lblLogoSize.MinWidth = 34;
            _lblLogoSize.FontFamily = new FontFamily(Theme.NumFont);
            _lblLogoSize.FontSize = 13;
            _lblLogoSize.SetResourceReference(TextBlock.ForegroundProperty, "GoldText");

            StackPanel sizeGroup = new StackPanel();
            sizeGroup.Orientation = Orientation.Horizontal;
            sizeGroup.Margin = new Thickness(0, 0, 4, 0);
            sizeGroup.Children.Add(Label("Größe"));
            sizeGroup.Children.Add(_sldLogo);
            sizeGroup.Children.Add(_lblLogoSize);
            wp2.Children.Add(sizeGroup);

            _logoPos = new PositionPicker();
            _logoPos.Changed += delegate { _contrastAccepted = false; UpdateLogo(); };
            wp2.Children.Add(Pair("Position", _logoPos));

            wp2.Children.Add(Chip("Logos", "Ordner mit den Logos im Explorer öffnen",
                delegate { OpenFolder(ImageStore.Logos); }));

            return shell;
        }


        private void OpenFolder(ImageStore store)
        {
            try
            {
                store.EnsureFolder();
                System.Diagnostics.Process.Start("explorer.exe", "\"" + store.FolderPath() + "\"");
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Ordner", ex.Message);
            }
        }

        /// <summary>
        /// Liest beide Bildordner neu ein und behält die bisherige Auswahl bei — auch
        /// dann, wenn eine Datei dazugekommen ist und sich die Reihenfolge verschiebt.
        /// </summary>
        private void RefreshImageFolders()
        {
            ImageStore.Logos.EnsureFolder();
            ImageStore.Backgrounds.EnsureFolder();

            _suppressLogo = true;
            _cbLogo.Items.Clear();
            _cbLogo.Items.Add("Kein Logo");
            int select = 0;
            List<ImageEntry> logos = ImageStore.Logos.List();
            for (int i = 0; i < logos.Count; i++)
            {
                _cbLogo.Items.Add(logos[i]);
                if (Same(logos[i].FullPath, _logoFile)) select = i + 1;
            }
            _cbLogo.SelectedIndex = select;
            _suppressLogo = false;

            _suppressBg = true;
            _cbBackground.Items.Clear();
            foreach (string s in BackgroundNames) _cbBackground.Items.Add(s);
            int bgSelect = _bgPreset >= 0 && _bgPreset < BackgroundNames.Length ? _bgPreset : 0;
            List<ImageEntry> backs = ImageStore.Backgrounds.List();
            for (int i = 0; i < backs.Count; i++)
            {
                _cbBackground.Items.Add(backs[i]);
                if (Same(backs[i].FullPath, _bgImage)) bgSelect = BackgroundNames.Length + i;
            }
            _cbBackground.SelectedIndex = bgSelect;
            if (_cbBackground.SelectedItem as ImageEntry == null) _bgImage = null;
            _suppressBg = false;

            UpdateLogo();
            ApplyBackground();
        }

        private static bool Same(string fullPath, string fileName)
        {
            return fileName != null &&
                   string.Equals(Path.GetFileName(fullPath), fileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Legt das Logo über die Ansicht. Größe und Rand hängen an der Breite der
        /// Fläche, damit Bildschirm und erzeugtes JPG dasselbe zeigen.
        /// </summary>
        private void UpdateLogo()
        {
            if (_suppressLogo || _logoImage == null) return;

            ImageEntry sel = _cbLogo == null ? null : _cbLogo.SelectedItem as ImageEntry;
            _logoFile = sel == null ? null : Path.GetFileName(sel.FullPath);
            if (_lblLogoSize != null)
                _lblLogoSize.Text = ((int)_logoPercent).ToString(CultureInfo.CurrentCulture) + " %";

            BitmapSource src = sel == null ? null : ImageStore.Load(sel.FullPath);
            if (src == null)
            {
                _logoImage.Visibility = Visibility.Collapsed;
                return;
            }
            _logoImage.Source = src;
            _logoImage.Visibility = Visibility.Visible;
            LayoutLogo(_logoImage, _viewHost.ActualWidth);
        }

        /// <summary>
        /// Wo das Logo sitzt, in Anteilen der Fläche (0 bis 1) — dieselbe Rechnung wie
        /// in LayoutLogo, nur ohne Bezug auf eine konkrete Größe.
        /// </summary>
        private Rect LogoRegion()
        {
            BitmapSource src = _logoImage == null ? null : _logoImage.Source as BitmapSource;
            if (src == null || src.PixelWidth <= 0) return new Rect(0, 0, 1, 1);

            double aspect = ViewAspect();
            double lw = _logoPercent / 100.0;
            double lh = lw * ((double)src.PixelHeight / src.PixelWidth) * aspect;
            if (lh > 1) lh = 1;

            const double padX = 0.028;
            double padY = padX * aspect;

            int p = _logoPos == null ? 8 : _logoPos.Index;
            int col = p % 3, row = p / 3;
            double x = col == 0 ? padX : col == 1 ? (1 - lw) / 2 : 1 - padX - lw;
            double y = row == 0 ? padY : row == 1 ? (1 - lh) / 2 : 1 - padY - lh;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            return new Rect(x, y, Math.Min(lw, 1 - x), Math.Min(lh, 1 - y));
        }

        /// <summary>
        /// Wäre das Logo im gespeicherten Bild überhaupt zu sehen? Gemessen wird der
        /// Grund genau an der Stelle, an der das Logo liegt.
        /// </summary>
        /// <param name="likeView">
        /// Wahr für das Titelbild der Übernahme: Dort liegt hinter dem Logo der
        /// Hintergrund der Ansicht, nicht der Bildgrund des JPG.
        /// </param>
        private bool LogoWouldVanish(bool likeView, out string detail)
        {
            detail = null;
            if (_logoImage == null || _logoImage.Visibility != Visibility.Visible) return false;
            BitmapSource src = _logoImage.Source as BitmapSource;
            if (src == null) return false;

            Contrast.Stats logo = Contrast.OfImage(src);
            Brush behind = likeView ? ViewBackground(1.0) : ExportBackground(1.0);
            Contrast.Stats ground = Contrast.OfBrush(behind, ViewAspect(), LogoRegion());
            if (!Contrast.WouldVanish(logo, ground)) return false;

            detail = "Helligkeit des Logos " + Contrast.Percent(logo.Mean) +
                     (likeView ? ", des Hintergrunds an dieser Stelle " : ", des Bildgrunds an dieser Stelle ") +
                     Contrast.Percent(ground.Mean) + ".";
            return true;
        }

        /// <summary>Fragt einmal nach, solange sich an Logo und Grund nichts ändert.</summary>
        private bool ContrastOkay() { return ContrastOkay("Trotzdem speichern", false); }

        private bool ContrastOkay(string goText, bool likeView)
        {
            // Eine Zusage für das JPG gilt nicht für das Titelbild und umgekehrt:
            // hinter dem Logo liegt jeweils ein anderer Grund.
            if (_contrastAccepted && _contrastAcceptedLikeView == likeView) return true;
            string detail;
            if (!LogoWouldVanish(likeView, out detail)) return true;

            string groundName = likeView ? "Hintergrund" : "Bildgrund";
            bool go = AppDialog.Confirm(this, "Das Logo wäre nicht zu sehen",
                "Das gewählte Logo ist nahezu einfarbig und hebt sich vom " + groundName + " kaum ab.\n" +
                detail + "\n\n" +
                "Auf hellem Grund braucht es ein dunkles Logo, auf dunklem ein helles — " +
                "oder den " + groundName + " wechseln.",
                goText, false);
            if (go) { _contrastAccepted = true; _contrastAcceptedLikeView = likeView; }
            return go;
        }

        /// <summary>Größe, Ausrichtung und Rand für eine gegebene Flächenbreite setzen.</summary>
        private void LayoutLogo(Image img, double hostWidth)
        {
            if (hostWidth < 8) hostWidth = 8;
            img.Width = hostWidth * _logoPercent / 100.0;
            img.Stretch = Stretch.Uniform;

            int p = _logoPos == null ? 8 : _logoPos.Index;
            int col = p % 3, row = p / 3;
            img.HorizontalAlignment = col == 0 ? HorizontalAlignment.Left
                                    : col == 1 ? HorizontalAlignment.Center
                                               : HorizontalAlignment.Right;
            img.VerticalAlignment = row == 0 ? VerticalAlignment.Top
                                  : row == 1 ? VerticalAlignment.Center
                                             : VerticalAlignment.Bottom;

            double pad = hostWidth * 0.028;
            img.Margin = new Thickness(
                col == 0 ? pad : 0, row == 0 ? pad : 0,
                col == 2 ? pad : 0, row == 2 ? pad : 0);
        }

        private UIElement BuildStatusBar()
        {
            Border shell = new Border();
            shell.Padding = new Thickness(22, 6, 22, 10);

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            shell.Child = sp;

            _statusFile = new TextBlock();
            _statusFile.FontSize = 12;
            _statusFile.FontWeight = FontWeights.SemiBold;
            _statusFile.SetResourceReference(TextBlock.ForegroundProperty, "Text");
            sp.Children.Add(_statusFile);

            _statusInfo = new TextBlock();
            _statusInfo.FontSize = 12;
            _statusInfo.Margin = new Thickness(14, 0, 0, 0);
            _statusInfo.FontFamily = new FontFamily(Theme.NumFont);
            _statusInfo.FontSize = 13;
            _statusInfo.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            sp.Children.Add(_statusInfo);

            return shell;
        }

        private void BuildBusyOverlay()
        {
            _busy = new Border();
            _busy.CornerRadius = new CornerRadius(13);
            _busy.Padding = new Thickness(24, 18, 24, 18);
            _busy.HorizontalAlignment = HorizontalAlignment.Center;
            _busy.VerticalAlignment = VerticalAlignment.Center;
            _busy.Visibility = Visibility.Collapsed;
            _busy.SetResourceReference(Border.BackgroundProperty, "Elevated");
            _busy.SetResourceReference(Border.BorderBrushProperty, "Border");
            _busy.BorderThickness = new Thickness(1);

            StackPanel sp = new StackPanel();
            _busyText = new TextBlock();
            _busyText.FontSize = 13.5;
            _busyText.HorizontalAlignment = HorizontalAlignment.Center;
            _busyText.SetResourceReference(TextBlock.ForegroundProperty, "Text");
            sp.Children.Add(_busyText);

            _busyCancel = new Button();
            _busyCancel.Content = "Abbrechen";
            _busyCancel.Style = (Style)Application.Current.Resources["Btn"];
            _busyCancel.Margin = new Thickness(0, 13, 0, 0);
            _busyCancel.HorizontalAlignment = HorizontalAlignment.Center;
            _busyCancel.Visibility = Visibility.Collapsed;
            _busyCancel.Click += delegate
            {
                _cancelRequested = true;
                if (_pfCancel != null) _pfCancel.Cancel();
            };
            sp.Children.Add(_busyCancel);

            _busy.Child = sp;
            _viewHost.Children.Add(_busy);
        }

        /// <summary>Kurz eingeblendetes Popup für Erfolgsmeldungen — sichtbar, aber nicht blockierend.</summary>
        private void BuildToast()
        {
            _toast = new Border();
            _toast.CornerRadius = new CornerRadius(11);
            _toast.Padding = new Thickness(18, 11, 18, 11);
            _toast.Margin = new Thickness(0, 0, 0, 22);
            _toast.HorizontalAlignment = HorizontalAlignment.Center;
            _toast.VerticalAlignment = VerticalAlignment.Bottom;
            _toast.Visibility = Visibility.Collapsed;
            _toast.SetResourceReference(Border.BackgroundProperty, "Elevated");
            _toast.SetResourceReference(Border.BorderBrushProperty, "Border");
            _toast.BorderThickness = new Thickness(1);

            _toastText = new TextBlock();
            _toastText.FontSize = 12.5;
            _toast.Child = _toastText;
            _viewHost.Children.Add(_toast);

            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromMilliseconds(2200);
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280));
                fade.Completed += delegate { _toast.Visibility = Visibility.Collapsed; };
                _toast.BeginAnimation(OpacityProperty, fade);
            };
        }

        private void Toast(string text, bool good)
        {
            _toastText.Text = (good ? "✔  " : "⚠  ") + text;
            _toastText.SetResourceReference(TextBlock.ForegroundProperty, good ? "Positive" : "Negative");
            _toast.BeginAnimation(OpacityProperty, null);
            _toast.Opacity = 1;
            _toast.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        public void SetScrim(bool on)
        {
            if (_scrim != null) _scrim.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        // -------------------------------------------------- kleine Bausteine

        private static Border Card()
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(14);
            b.BorderThickness = new Thickness(1);
            b.SetResourceReference(Border.BackgroundProperty, "Panel");
            b.SetResourceReference(Border.BorderBrushProperty, "Border");
            return b;
        }

        private static Button Styled(string key, string text, string tip, RoutedEventHandler h)
        {
            Button b = new Button();
            b.Content = text;
            b.ToolTip = tip;
            b.Style = (Style)Application.Current.Resources[key];
            b.Click += h;
            return b;
        }

        private static Button Action(string t, string tip, RoutedEventHandler h) { return Styled("Btn", t, tip, h); }
        private static Button Primary(string t, string tip, RoutedEventHandler h) { return Styled("BtnPrimary", t, tip, h); }
        private static Button Chip(string t, string tip, RoutedEventHandler h) { return Styled("BtnChip", t, tip, h); }

        private static UIElement Gap()
        {
            Border b = new Border();
            b.Width = 1;
            b.Height = 18;
            b.Margin = new Thickness(5, 0, 11, 0);
            b.VerticalAlignment = VerticalAlignment.Center;
            b.SetResourceReference(Border.BackgroundProperty, "Border");
            return b;
        }

        /// <summary>Beschriftung und Bedienelement bleiben beim Umbruch beieinander.</summary>
        private static UIElement Pair(string caption, UIElement control)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(0, 0, 4, 0);
            sp.Children.Add(Label(caption));
            sp.Children.Add(control);
            return sp;
        }

        private static TextBlock Label(string text)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = 11.5;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(4, 0, 6, 0);
            t.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            return t;
        }

        private static ComboBox Combo(string[] items, int selected, double width, SelectionChangedEventHandler h)
        {
            ComboBox cb = new ComboBox();
            cb.Style = (Style)Application.Current.Resources["Combo"];
            cb.ItemContainerStyle = (Style)Application.Current.Resources["ComboItem"];
            cb.Width = width;
            cb.Margin = new Thickness(0, 0, 6, 0);
            foreach (string s in items) cb.Items.Add(s);
            cb.SelectedIndex = selected;
            if (h != null) cb.SelectionChanged += h;
            return cb;
        }

        // =================================================================
        //  Kontextmenüs
        // =================================================================

        private MenuItem Item(string header, string gesture, RoutedEventHandler h)
        {
            MenuItem mi = new MenuItem();
            mi.Header = header;
            if (gesture != null) mi.InputGestureText = gesture;
            mi.Click += h;
            return mi;
        }

        private ContextMenu BuildFileMenu()
        {
            ContextMenu cm = new ContextMenu();
            cm.Items.Add(Item("JPG speichern", "Strg+S", OnSaveJpg));
            cm.Items.Add(Item("Ins PrintFlow …", "Strg+P", OnSendToPrintFlow));
            cm.Items.Add(Item("Bild / Modell", "B", ToggleImage));
            cm.Items.Add(Item("Umbenennen", "F2", OnRename));
            cm.Items.Add(new Separator());
            cm.Items.Add(Item("Im Explorer zeigen", null, OnShowInExplorer));
            cm.Items.Add(Item("Aktualisieren", "F5", OnRefresh));
            cm.Items.Add(Item("Häkchen aufheben", null, OnClearMarks));
            cm.Items.Add(new Separator());
            cm.Items.Add(Item("Löschen", "Entf", OnDelete));
            return cm;
        }

        private ContextMenu BuildViewMenu()
        {
            ContextMenu cm = new ContextMenu();
            cm.Items.Add(Item("JPG speichern", "Strg+S", OnSaveJpg));
            cm.Items.Add(Item("Ins PrintFlow …", "Strg+P", OnSendToPrintFlow));
            cm.Items.Add(Item("Umbenennen", "F2", OnRename));
            cm.Items.Add(new Separator());
            cm.Items.Add(Item("Bild / Modell", "B", ToggleImage));
            cm.Items.Add(Item("Einpassen", "Leertaste", delegate { FitToModel(true); UpdateCamera(); }));
            cm.Items.Add(Item("Im Explorer zeigen", null, OnShowInExplorer));
            cm.Items.Add(new Separator());
            cm.Items.Add(Item("Löschen", "Entf", OnDelete));
            return cm;
        }

        /// <summary>Rechtsklick in der Liste wählt zuerst den getroffenen Eintrag aus.</summary>
        private void OnListRightDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject d = e.OriginalSource as DependencyObject;
            while (d != null && !(d is ListBoxItem) && !(d is ListBox))
                d = VisualTreeHelper.GetParent(d);
            ListBoxItem item = d as ListBoxItem;
            if (item != null) item.IsSelected = true;
        }

        private void OnShowInExplorer(object sender, RoutedEventArgs e)
        {
            string path = _currentFile;
            try
            {
                if (path != null && File.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (_currentFolder != null && Directory.Exists(_currentFolder))
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + _currentFolder + "\"");
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Explorer", "Der Explorer ließ sich nicht öffnen:\n\n" + ex.Message);
            }
        }

        private void HookCommands()
        {
            AddBinding(Key.O, ModifierKeys.Control, OnOpenFile);
            AddBinding(Key.O, ModifierKeys.Control | ModifierKeys.Shift, OnOpenFolder);
            AddBinding(Key.S, ModifierKeys.Control, OnSaveJpg);
            AddBinding(Key.P, ModifierKeys.Control, OnSendToPrintFlow);
            AddBinding(Key.F2, ModifierKeys.None, OnRename);
            AddBinding(Key.Delete, ModifierKeys.None, OnDelete);
            AddBinding(Key.F5, ModifierKeys.None, OnRefresh);
            AddBinding(Key.B, ModifierKeys.None, ToggleImage);
            AddBinding(Key.Space, ModifierKeys.None, delegate { FitToModel(true); UpdateCamera(); });
            AddBinding(Key.D1, ModifierKeys.None, delegate { SetView(-55, 28); });
            AddBinding(Key.D2, ModifierKeys.None, delegate { SetView(-90, 0); });
            AddBinding(Key.D3, ModifierKeys.None, delegate { SetView(90, 0); });
            AddBinding(Key.D4, ModifierKeys.None, delegate { SetView(180, 0); });
            AddBinding(Key.D5, ModifierKeys.None, delegate { SetView(0, 0); });
            AddBinding(Key.D6, ModifierKeys.None, delegate { SetView(-90, 89.5); });
            AddBinding(Key.D7, ModifierKeys.None, delegate { SetView(-90, -89.5); });
            AddBinding(Key.PageDown, ModifierKeys.None, delegate { StepFile(1); });
            AddBinding(Key.PageUp, ModifierKeys.None, delegate { StepFile(-1); });
        }

        private void AddBinding(Key key, ModifierKeys mods, RoutedEventHandler action)
        {
            RoutedCommand cmd = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(cmd, delegate(object s, ExecutedRoutedEventArgs e)
            {
                action(this, new RoutedEventArgs());
                e.Handled = true;
            }));

            // Key und Modifiers einzeln setzen: der KeyBinding-Konstruktor wuerde
            // Gesten ohne Modifikator (etwa "1" oder Leertaste) ablehnen.
            KeyBinding kb = new KeyBinding();
            kb.Command = cmd;
            kb.Key = key;
            kb.Modifiers = mods;
            InputBindings.Add(kb);
        }

        // =================================================================
        //  Design
        // =================================================================

        private void OnToggleTheme(object sender, RoutedEventArgs e)
        {
            Theme.Apply(!Theme.IsDark);
        }

        private void OnThemeChanged()
        {
            // Beschriftet ist, wohin der Klick führt, nicht wo man gerade steht.
            _themeBtn.Content = Theme.IsDark ? "Helles Design" : "Dunkles Design";
            Theme.ApplyTitleBar(this);
            ApplyBackground();
        }

        // =================================================================
        //  Szene und Kamera
        // =================================================================

        private void InitScene()
        {
            _liveScene = new Model3DGroup();

            _ambient = new AmbientLight(Color.FromRgb(0x36, 0x38, 0x3C));
            _key = new DirectionalLight(Color.FromRgb(0xD4, 0xD4, 0xD2), new Vector3D(0, 0, -1));
            _fill = new DirectionalLight(Color.FromRgb(0x51, 0x56, 0x5E), new Vector3D(0, 0, -1));
            _rim = new DirectionalLight(Color.FromRgb(0x2A, 0x2C, 0x31), new Vector3D(0, 0, 1));

            _liveScene.Children.Add(_ambient);
            _liveScene.Children.Add(_key);
            _liveScene.Children.Add(_fill);
            _liveScene.Children.Add(_rim);

            ModelVisual3D visual = new ModelVisual3D();
            visual.Content = _liveScene;
            _viewport.Children.Add(visual);

            ApplyMaterialObjects();
            RebuildCamera();
        }

        private void ApplyMaterialObjects()
        {
            Color c = _modelColor;
            int finish = _cbFinish == null ? 1 : Math.Max(0, _cbFinish.SelectedIndex);

            byte specLevel;
            double power;
            double diffuseScale = 1.0;
            switch (finish)
            {
                case 0: specLevel = 0x10; power = 6; break;             // Matt
                case 2: specLevel = 0x8C; power = 52; break;            // Glänzend
                case 3: specLevel = 0xB4; power = 34; diffuseScale = 0.68; break;  // Metallic
                default: specLevel = 0x40; power = 20; break;           // Seidenmatt
            }

            SolidColorBrush diffuse = new SolidColorBrush(Scale(c, diffuseScale));
            diffuse.Freeze();
            SolidColorBrush spec = new SolidColorBrush(Color.FromRgb(specLevel, specLevel, specLevel));
            spec.Freeze();
            SolidColorBrush back = new SolidColorBrush(Scale(c, 0.42));
            back.Freeze();

            MaterialGroup mg = new MaterialGroup();
            mg.Children.Add(new DiffuseMaterial(diffuse));
            if (specLevel > 0x18) mg.Children.Add(new SpecularMaterial(spec, power));
            mg.Freeze();
            _material = mg;

            DiffuseMaterial bm = new DiffuseMaterial(back);
            bm.Freeze();
            _backMaterial = bm;
        }

        private static Color Scale(Color c, double f)
        {
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, c.R * f)),
                (byte)Math.Max(0, Math.Min(255, c.G * f)),
                (byte)Math.Max(0, Math.Min(255, c.B * f)));
        }

        private void ApplyMaterial()
        {
            ApplyMaterialObjects();
            if (_liveMesh != null && _model != null)
            {
                _liveScene.Children.Remove(_liveMesh);
                _liveMesh = NewMeshModel();
                _liveScene.Children.Add(_liveMesh);
            }
        }

        private GeometryModel3D NewMeshModel()
        {
            GeometryModel3D g = new GeometryModel3D(_model.Mesh, _material);
            g.BackMaterial = _backMaterial;
            if (g.CanFreeze) g.Freeze();
            return g;
        }

        /// <summary>Blickrichtung wechseln und dabei den eingestellten Zoomgrad beibehalten.</summary>
        private void SetView(double yaw, double pitch)
        {
            double ratio = ZoomRatio();
            _yaw = yaw;
            _pitch = pitch;
            if (_model != null)
            {
                FitToModel(false);
                _distance = _fitDistance * ratio;
            }
            UpdateCamera();
        }

        private void SetProjection(bool ortho)
        {
            double ratio = ZoomRatio();
            _ortho = ortho;
            if (_model != null)
            {
                FitToModel(false);
                _distance = _fitDistance * ratio;
            }
            RebuildCamera();
        }

        private double ZoomRatio()
        {
            if (_fitDistance < 1e-9) return 1.0;
            double r = _distance / _fitDistance;
            return r < 0.02 ? 0.02 : (r > 50 ? 50 : r);
        }

        private Vector3D CameraDirection()
        {
            double y = _yaw * Math.PI / 180.0;
            double p = _pitch * Math.PI / 180.0;
            return new Vector3D(Math.Cos(p) * Math.Cos(y), Math.Cos(p) * Math.Sin(y), Math.Sin(p));
        }

        private void RebuildCamera()
        {
            if (_ortho) _viewport.Camera = new OrthographicCamera();
            else
            {
                PerspectiveCamera pc = new PerspectiveCamera();
                pc.FieldOfView = FovDeg;
                _viewport.Camera = pc;
            }
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (_viewport == null || _viewport.Camera == null) return;
            if (_pitch > 89.5) _pitch = 89.5;
            if (_pitch < -89.5) _pitch = -89.5;

            Vector3D dir = CameraDirection();
            Point3D pos = _target + dir * _distance;
            Vector3D look = -dir;
            Vector3D up = new Vector3D(0, 0, 1);

            double radius = _model != null ? _model.Radius : 100.0;
            double near = Math.Max(0.01, Math.Min(_distance * 0.02, radius * 0.05));
            double far = _distance + radius * 6.0 + 100.0;

            PerspectiveCamera pc = _viewport.Camera as PerspectiveCamera;
            if (pc != null)
            {
                pc.Position = pos;
                pc.LookDirection = look;
                pc.UpDirection = up;
                pc.NearPlaneDistance = near;
                pc.FarPlaneDistance = far;
            }
            OrthographicCamera oc = _viewport.Camera as OrthographicCamera;
            if (oc != null)
            {
                oc.Position = pos;
                oc.LookDirection = look;
                oc.UpDirection = up;
                oc.Width = OrthoWidth();
                oc.NearPlaneDistance = -radius * 4 - 10;
                oc.FarPlaneDistance = far;
            }

            UpdateLights(look, up);
        }

        private double OrthoWidth()
        {
            return 2.0 * _distance * Math.Tan(FovDeg * Math.PI / 360.0);
        }

        private void UpdateLights(Vector3D look, Vector3D worldUp)
        {
            Vector3D right = Vector3D.CrossProduct(look, worldUp);
            if (right.Length < 1e-6) right = new Vector3D(1, 0, 0);
            right.Normalize();
            Vector3D up = Vector3D.CrossProduct(right, look);
            up.Normalize();

            _key.Direction = Norm(look + right * 0.42 - up * 0.55);
            _fill.Direction = Norm(look - right * 0.75 + up * 0.25);
            _rim.Direction = Norm(-look - up * 0.3);
        }

        private static Vector3D Norm(Vector3D v)
        {
            double l = v.Length;
            if (l < 1e-9) return new Vector3D(0, 0, -1);
            return v / l;
        }

        /// <summary>
        /// Passt das Modell formatfüllend ein. Gerechnet wird über die acht Ecken der
        /// Bounding-Box, nicht über eine Hüllkugel – flache oder längliche Teile füllen
        /// den Rahmen damit deutlich besser aus.
        /// </summary>
        private void FitToModel(bool resetTarget)
        {
            if (_model == null) return;
            if (resetTarget) _target = _model.Center;

            double aspect = ViewAspect();
            double tanH = Math.Tan(FovDeg * Math.PI / 360.0);
            double tanV = tanH / Math.Max(0.05, aspect);

            Vector3D dir = CameraDirection();
            Vector3D look = -dir;
            Vector3D right = Vector3D.CrossProduct(look, new Vector3D(0, 0, 1));
            if (right.Length < 1e-6) right = new Vector3D(1, 0, 0);
            right.Normalize();
            Vector3D up = Vector3D.CrossProduct(right, look);
            up.Normalize();

            Point3D mn = _model.Min, mx = _model.Max;
            double need = 0, maxU = 0, maxV = 0;
            for (int i = 0; i < 8; i++)
            {
                Point3D corner = new Point3D(
                    (i & 1) == 0 ? mn.X : mx.X,
                    (i & 2) == 0 ? mn.Y : mx.Y,
                    (i & 4) == 0 ? mn.Z : mx.Z);
                Vector3D rel = corner - _target;
                double u = Math.Abs(Vector3D.DotProduct(rel, right));
                double v = Math.Abs(Vector3D.DotProduct(rel, up));
                double w = Vector3D.DotProduct(rel, dir);
                if (u > maxU) maxU = u;
                if (v > maxV) maxV = v;
                need = Math.Max(need, Math.Max(u / tanH, v / tanV) + w);
            }

            if (_ortho)
            {
                double widthNeeded = 2.0 * Math.Max(maxU, maxV * Math.Max(0.05, aspect)) * 1.06;
                _distance = widthNeeded / (2.0 * tanH);
            }
            else _distance = need * 1.06;

            if (_distance < _model.Radius * 0.02) _distance = _model.Radius * 0.02;
            _fitDistance = _distance;
        }

        private double ViewAspect()
        {
            double w = _viewHost.ActualWidth, h = _viewHost.ActualHeight;
            if (w < 4 || h < 4) return 4.0 / 3.0;
            return w / h;
        }

        // =================================================================
        //  Maussteuerung
        // =================================================================

        private void OnViewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_busyActive) return;
            int mode = 0;
            if (e.ChangedButton == MouseButton.Left) mode = 1;
            else if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle) mode = 2;
            if (mode == 0) return;

            if (e.ChangedButton == MouseButton.Right) _rightDragged = false;
            _dragMode = mode;
            _dragStart = e.GetPosition(_viewHost);
            _viewHost.CaptureMouse();
            _viewHost.Focus();
            e.Handled = true;
        }

        private void OnViewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragMode == 0 || _model == null) return;
            Point p = e.GetPosition(_viewHost);
            double dx = p.X - _dragStart.X;
            double dy = p.Y - _dragStart.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 3) _rightDragged = true;
            _dragStart = p;

            if (_dragMode == 1)
            {
                _yaw -= dx * 0.35;
                _pitch += dy * 0.35;
            }
            else
            {
                Vector3D dir = CameraDirection();
                Vector3D look = -dir;
                Vector3D right = Vector3D.CrossProduct(look, new Vector3D(0, 0, 1));
                if (right.Length < 1e-6) right = new Vector3D(1, 0, 0);
                right.Normalize();
                Vector3D up = Vector3D.CrossProduct(right, look);
                up.Normalize();

                double h = Math.Max(4.0, _viewHost.ActualHeight);
                double worldPerPixel = _ortho
                    ? OrthoWidth() / Math.Max(4.0, _viewHost.ActualWidth)
                    : 2.0 * _distance * Math.Tan(FovDeg * Math.PI / 360.0) / h;
                _target += right * (-dx * worldPerPixel) + up * (dy * worldPerPixel);
            }
            UpdateCamera();
        }

        private void OnViewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragMode == 0) return;
            _dragMode = 0;
            _viewHost.ReleaseMouseCapture();

            // Rechtsklick ohne Ziehen öffnet das Kontextmenü statt zu verschieben.
            if (e.ChangedButton == MouseButton.Right && !_rightDragged && !_busyActive)
            {
                _viewMenu.PlacementTarget = _viewHost;
                _viewMenu.Placement = PlacementMode.MousePoint;
                _viewMenu.IsOpen = true;
            }
            e.Handled = true;
        }

        private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_model == null) return;
            double factor = Math.Pow(0.88, e.Delta / 120.0);
            double r = _model.Radius;
            _distance = Math.Max(r * 0.05, Math.Min(r * 200.0, _distance * factor));
            UpdateCamera();
            e.Handled = true;
        }

        // =================================================================
        //  Hintergrund
        // =================================================================

        /// <param name="scale">
        /// Verhältnis von Bildbreite zu Ansichtsbreite. Nur die Schraffur hängt daran:
        /// sie ist fest gekachelt und muss beim Rendern in ein großes Bild mitwachsen,
        /// sonst käme sie dort viel feiner heraus als auf dem Bildschirm.
        /// </param>
        private Brush ViewBackground(double scale)
        {
            ImageEntry img = _cbBackground == null ? null : _cbBackground.SelectedItem as ImageEntry;
            if (img != null)
            {
                BitmapSource src = ImageStore.Load(img.FullPath);
                if (src != null)
                {
                    ImageBrush ib = new ImageBrush(src);
                    ib.Stretch = Stretch.UniformToFill;   // füllt den Rahmen, ohne zu verzerren
                    ib.Freeze();
                    return ib;
                }
            }
            return PresetBrush(_cbBackground == null ? 0 : _cbBackground.SelectedIndex, scale);
        }

        /// <summary>
        /// Grund für das erzeugte JPG. Voreingestellt Weiß: „Weiß ist das, was ein
        /// Produktbild braucht — der Shop zeigt es auf hellen Flächen, Marktplätze
        /// erwarten es so" (elasprintflow-oberflaeche-medien). „Wie Ansicht" nimmt
        /// stattdessen genau das mit, was hinter dem Modell zu sehen ist.
        /// </summary>
        private Brush ExportBackground(double scale)
        {
            switch (_cbJpgBg == null ? 0 : _cbJpgBg.SelectedIndex)
            {
                case 1: return ViewBackground(scale);
                case 2: return PresetBrush(1, scale);
                case 3: return PresetBrush(2, scale);
                case 4: return PresetBrush(5, scale);
                default:
                    SolidColorBrush w = new SolidColorBrush(Colors.White);
                    w.Freeze();
                    return w;
            }
        }

        private Brush PresetBrush(int index, double scale)
        {
            Brush b;
            switch (index)
            {
                case 1: b = VerticalGradient(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xD5, 0xDD, 0xE6)); break;
                case 2: b = VerticalGradient(Color.FromRgb(0x3B, 0x42, 0x4C), Color.FromRgb(0x14, 0x17, 0x1C)); break;
                case 3: b = new SolidColorBrush(Colors.White); break;
                case 4: b = VerticalGradient(Color.FromRgb(0x33, 0x66, 0x8E), Color.FromRgb(0x0F, 0x1D, 0x2B)); break;
                case 5: b = new SolidColorBrush(Color.FromRgb(0xBF, 0xC4, 0xCA)); break;
                default: return Theme.PreviewBrush(scale);
            }
            b.Freeze();
            return b;
        }

        private static LinearGradientBrush VerticalGradient(Color top, Color bottom)
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(0, 1);
            g.GradientStops.Add(new GradientStop(top, 0));
            g.GradientStops.Add(new GradientStop(bottom, 1));
            return g;
        }

        private void ApplyBackground()
        {
            if (_viewHost != null) _viewHost.Background = ViewBackground(1.0);
        }

        // =================================================================
        //  Laden
        // =================================================================

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            OnThemeChanged();
            ApplyMaterial();
            RefreshImageFolders();
            SetKind(_kind);
            if (_pendingFile != null && File.Exists(_pendingFile))
            {
                string f = _pendingFile;
                _pendingFile = null;
                RefreshFolder(f);
                await LoadFile(f);
            }
            else if (_currentFolder != null && Directory.Exists(_currentFolder))
            {
                RefreshFolder(null);
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            string p = paths[0];
            if (Directory.Exists(p))
            {
                _currentFolder = p;
                RefreshFolder(null);
                return;
            }
            if (!MeshLoader.IsSupported(p))
            {
                AppDialog.Warn(this, "Format nicht unterstützt",
                    "Diese Datei kann nicht angezeigt werden:\n\n" + Path.GetFileName(p) +
                    "\n\nMöglich sind STL- und 3MF-Dateien.");
                return;
            }
            RefreshFolder(p);
            await LoadFile(p);
        }

        private async void OnOpenFile(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = "3D-Modell öffnen";
            dlg.Filter = "3D-Modelle (*.stl;*.3mf)|*.stl;*.3mf|STL-Dateien (*.stl)|*.stl|3MF-Dateien (*.3mf)|*.3mf|Alle Dateien (*.*)|*.*";
            if (_currentFolder != null && Directory.Exists(_currentFolder)) dlg.InitialDirectory = _currentFolder;
            if (dlg.ShowDialog(this) != true) return;
            RefreshFolder(dlg.FileName);
            await LoadFile(dlg.FileName);
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Ordner mit STL-/3MF-Dateien wählen";
            dlg.ShowNewFolderButton = false;
            if (_currentFolder != null && Directory.Exists(_currentFolder)) dlg.SelectedPath = _currentFolder;
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            _currentFolder = dlg.SelectedPath;
            RefreshFolder(null);
        }

        private void RefreshFolder(string selectPath)
        {
            if (selectPath != null) _currentFolder = Path.GetDirectoryName(selectPath);
            if (_currentFolder == null || !Directory.Exists(_currentFolder)) return;

            List<string> files = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(_currentFolder))
                {
                    bool model = MeshLoader.IsSupported(f);
                    bool image = IsImageFile(f);
                    if (!model && !image) continue;
                    if (_kind == 0 && !model) continue;
                    if (_kind == 1 && !image) continue;
                    files.Add(f);
                }
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Ordner nicht lesbar", ex.Message);
                return;
            }

            files.Sort(delegate(string a, string b)
            {
                try { return StrCmpLogicalW(Path.GetFileName(a), Path.GetFileName(b)); }
                catch { return string.Compare(a, b, StringComparison.OrdinalIgnoreCase); }
            });

            // Häkchen gelten für den Ordner, in dem sie gesetzt wurden. Wer den Ordner
            // wechselt, nimmt sie nicht mit — eine Akte aus zwei Ordnern wäre ein Versehen.
            _marked.RemoveWhere(delegate(string p)
            {
                return !string.Equals(Path.GetDirectoryName(p), _currentFolder, StringComparison.OrdinalIgnoreCase) ||
                       !File.Exists(p);
            });

            _entries.Clear();
            foreach (string f in files)
            {
                FileEntry fe = new FileEntry();
                fe.FullPath = f;
                fe.Name = Path.GetFileName(f);
                fe.IsImage = IsImageFile(f);
                fe.Marked = !fe.IsImage && _marked.Contains(f);
                fe.MarkedChanged = OnEntryMarked;       // erst jetzt: das Setzen oben ist kein Klick
                _entries.Add(fe);
            }

            _suppressSelection = true;
            _fileList.Items.Clear();
            int selectIndex = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                _fileList.Items.Add(_entries[i]);
                if (selectPath != null && string.Equals(_entries[i].FullPath, selectPath, StringComparison.OrdinalIgnoreCase))
                    selectIndex = i;
                else if (selectPath == null && _currentFile != null &&
                         string.Equals(_entries[i].FullPath, _currentFile, StringComparison.OrdinalIgnoreCase))
                    selectIndex = i;
            }
            if (selectIndex >= 0)
            {
                _fileList.SelectedIndex = selectIndex;
                _fileList.ScrollIntoView(_fileList.SelectedItem);
            }
            _suppressSelection = false;

            _folderLabel.Text = _currentFolder;
            _folderLabel.ToolTip = _currentFolder;
            UpdateCount();
        }

        // -------------------------------------------------- Ordner neu einlesen

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            RefreshList(true);
            RefreshImageFolders();      // Logos und Hintergründe, wie F5 es schon immer tat
        }

        /// <summary>
        /// Was die Liste zeigte, läse man den Ordner jetzt — dieselbe Auswahl wie
        /// in RefreshFolder, aber ohne etwas anzufassen. Null, wenn der Ordner sich
        /// nicht lesen lässt.
        /// </summary>
        private List<string> FolderSnapshot()
        {
            List<string> files = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(_currentFolder))
                {
                    bool model = MeshLoader.IsSupported(f);
                    bool image = IsImageFile(f);
                    if (!model && !image) continue;
                    if (_kind == 0 && !model) continue;
                    if (_kind == 1 && !image) continue;
                    files.Add(f);
                }
            }
            catch { return null; }
            return files;
        }

        private DateTime _lastFolderCheck = DateTime.MinValue;

        /// <summary>
        /// Liest den Ordner neu ein, wenn sich darin etwas getan hat, und sagt, was.
        ///
        /// ERST VERGLEICHEN, DANN NEU BAUEN. Die Liste neu aufzubauen rückt sie zur
        /// Auswahl zurück; wer gerade weiter unten liest, soll das nur erleben,
        /// wenn es einen Grund gibt. Auswahl und Häkchen bleiben in jedem Fall.
        /// </summary>
        /// <param name="asked">
        /// Wahr beim Knopf und bei F5: Dann gibt es immer eine Antwort, auch „der
        /// Ordner ist unverändert". Falsch, wenn das Fenster von selbst nachsieht —
        /// dann meldet es sich nur, wenn sich etwas getan hat.
        /// </param>
        private void RefreshList(bool asked)
        {
            if (_busyActive) return;
            if (_currentFolder == null || !Directory.Exists(_currentFolder))
            {
                if (asked) Toast("Kein Ordner geöffnet", false);
                return;
            }
            _lastFolderCheck = DateTime.UtcNow;

            List<string> now = FolderSnapshot();
            if (now == null)
            {
                if (asked) RefreshFolder(null);     // zeigt die Fehlermeldung
                return;
            }

            HashSet<string> before = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry fe in _entries) before.Add(fe.FullPath);
            int added = 0;
            foreach (string f in now) if (!before.Remove(f)) added++;
            int gone = before.Count;                // was vorher da war und jetzt fehlt

            if (added == 0 && gone == 0)
            {
                if (asked) Toast("Der Ordner ist unverändert", true);
                return;
            }

            RefreshFolder(null);

            // Die geöffnete Datei gibt es nicht mehr: Dann zeigt die Ansicht nichts
            // mehr, was sich speichern oder übernehmen ließe.
            if (_modelFile != null && !File.Exists(_modelFile)) ClearModel();
            else if (_showingImage && (_currentFile == null || !File.Exists(_currentFile))) ShowModelView();
            UpdateImageToggle();

            string what = "";
            if (added > 0) what = added == 1 ? "1 neue Datei" : added + " neue Dateien";
            if (gone > 0)
                what += (what.Length > 0 ? "  ·  " : "") + (gone == 1 ? "1 Datei fehlt jetzt" : gone + " Dateien fehlen jetzt");
            Toast(what, true);
        }

        /// <summary>
        /// Kommt man aus einem anderen Programm zurück — dem Browser, dem Slicer,
        /// dem Explorer —, sieht das Fenster von selbst nach. Dort entstehen die
        /// neuen Dateien, und genau dann will man sie sehen. Höchstens alle zwei
        /// Sekunden: Auch ein eigener Dialog, der sich schließt, gibt das Fenster
        /// „zurück", und ein Netzordner antwortet nicht umsonst.
        /// </summary>
        private void OnWindowActivated(object sender, EventArgs e)
        {
            if ((DateTime.UtcNow - _lastFolderCheck).TotalSeconds < 2) return;
            RefreshList(false);
        }

        private static readonly string[] ImageExtensions =
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        private static bool IsImageFile(string path)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            foreach (string x in ImageExtensions) if (e == x) return true;
            return false;
        }

        private void SetKind(int kind)
        {
            _kind = kind;
            for (int i = 0; i < _kindBtns.Length; i++)
                _kindBtns[i].Style = (Style)Application.Current.Resources[i == kind ? "BtnPrimary" : "BtnChip"];
            RefreshFolder(null);
        }

        private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection) return;
            FileEntry fe = _fileList.SelectedItem as FileEntry;
            if (fe == null) return;
            if (string.Equals(fe.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase)) return;
            if (fe.IsImage) ShowImage(fe.FullPath);
            else await LoadFile(fe.FullPath);
        }

        // =================================================================
        //  Bildansicht
        // =================================================================

        /// <summary>Zeigt eine Bilddatei anstelle der 3D-Ansicht.</summary>
        private void ShowImage(string path)
        {
            BitmapSource src = ImageStore.Load(path);
            if (src == null)
            {
                AppDialog.Error(this, "Bild konnte nicht geöffnet werden", Path.GetFileName(path));
                return;
            }
            _imageView.Source = src;
            _imageView.Visibility = Visibility.Visible;
            _viewport.Visibility = Visibility.Collapsed;
            _logoImage.Visibility = Visibility.Collapsed;
            _emptyHint.Visibility = Visibility.Collapsed;
            _showingImage = true;
            _currentFile = path;
            UpdateStatus();
            UpdateImageToggle();
            Title = Path.GetFileName(path) + " – Elas3DView";
        }

        /// <summary>Zurück zur 3D-Ansicht des geladenen Modells.</summary>
        private void ShowModelView()
        {
            _showingImage = false;
            _imageView.Visibility = Visibility.Collapsed;
            _imageView.Source = null;
            _viewport.Visibility = Visibility.Visible;
            _emptyHint.Visibility = _model == null ? Visibility.Visible : Visibility.Collapsed;
            UpdateLogo();
            if (_modelFile != null) _currentFile = _modelFile;
            UpdateStatus();
            UpdateImageToggle();
            Title = (_modelFile == null ? "" : Path.GetFileName(_modelFile) + " – ") + "Elas3DView";
        }

        /// <summary>Erstes Bild, das denselben Namen trägt wie das geladene Modell.</summary>
        private string ModelImage()
        {
            if (_modelFile == null) return null;
            List<string> found = Sidecars(_modelFile);
            return found.Count > 0 ? found[0] : null;
        }

        private void UpdateImageToggle()
        {
            if (_btnImageToggle == null) return;
            if (_showingImage)
            {
                _btnImageToggle.Content = "Modell zeigen";
                _btnImageToggle.IsEnabled = _model != null;
                return;
            }
            _btnImageToggle.Content = "Bild zeigen";
            _btnImageToggle.IsEnabled = ModelImage() != null;
        }

        /// <summary>Wechselt zwischen dem Modell und seinem Vorschaubild.</summary>
        private void ToggleImage(object sender, RoutedEventArgs e)
        {
            if (_showingImage) { ShowModelView(); SelectInList(_modelFile); return; }
            string img = ModelImage();
            if (img == null)
            {
                AppDialog.Info(this, "Kein Bild vorhanden",
                    _modelFile == null
                        ? "Bitte zuerst ein Modell öffnen."
                        : "Zu " + Path.GetFileName(_modelFile) + " liegt noch kein Bild im Ordner.\n\n" +
                          "Mit „JPG speichern\" lässt sich eines erzeugen.");
                return;
            }
            ShowImage(img);
            SelectInList(img);
        }

        /// <summary>Hebt eine Datei in der Liste hervor, ohne ein Laden auszulösen.</summary>
        private void SelectInList(string path)
        {
            if (path == null) return;
            _suppressSelection = true;
            for (int i = 0; i < _fileList.Items.Count; i++)
            {
                FileEntry fe = _fileList.Items[i] as FileEntry;
                if (fe != null && string.Equals(fe.FullPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    _fileList.SelectedIndex = i;
                    _fileList.ScrollIntoView(_fileList.SelectedItem);
                    break;
                }
            }
            _suppressSelection = false;
        }

        private void StepFile(int delta)
        {
            if (_fileList.Items.Count == 0) return;
            int i = _fileList.SelectedIndex + delta;
            if (i < 0) i = 0;
            if (i >= _fileList.Items.Count) i = _fileList.Items.Count - 1;
            _fileList.SelectedIndex = i;
            _fileList.ScrollIntoView(_fileList.SelectedItem);
        }

        private async Task LoadFile(string path)
        {
            if (_busyActive) return;
            ShowBusy("Lade " + Path.GetFileName(path) + " …", false);
            await Task.Delay(1);          // Hinweis zeichnen lassen, bevor aufgeräumt wird

            // Das alte Netz vorher loslassen, sonst liegen beim Wechsel kurz zwei
            // große Modelle nebeneinander im Speicher. Die Ansicht verdeckt in
            // diesem Moment ohnehin der Ladehinweis.
            if (_liveMesh != null)
            {
                _liveScene.Children.Remove(_liveMesh);
                _liveMesh = null;
            }
            _model = null;
            TrimMemory();

            try
            {
                LoadedModel m = await Task.Run<LoadedModel>(delegate { return MeshLoader.Load(path); });
                SetModel(m, path);
            }
            catch (Exception ex)
            {
                ClearModel();             // das alte Netz ist bereits fort
                HideBusy();
                AppDialog.Error(this, "Datei konnte nicht geladen werden",
                    Path.GetFileName(path) + "\n\n" + ex.Message);
                return;
            }
            HideBusy();
        }

        private void SetModel(LoadedModel m, string path)
        {
            _model = m;
            _currentFile = path;
            _modelFile = path;

            if (_liveMesh != null) _liveScene.Children.Remove(_liveMesh);
            _liveMesh = NewMeshModel();
            _liveScene.Children.Add(_liveMesh);

            _showingImage = false;
            _imageView.Visibility = Visibility.Collapsed;
            _imageView.Source = null;
            _viewport.Visibility = Visibility.Visible;
            _emptyHint.Visibility = Visibility.Collapsed;
            UpdateLogo();

            FitToModel(true);
            UpdateCamera();
            UpdateStatus();
            UpdateImageToggle();
            Title = Path.GetFileName(path) + " – Elas3DView";
            TrimMemory();
        }

        /// <summary>
        /// Gibt den Speicher abgelegter Modelle an das Betriebssystem zurück.
        ///
        /// Ein fallengelassenes Netz liegt im Bereich für große Felder, und den räumt
        /// die Laufzeit von sich aus selten auf: beim Durchblättern von fünf großen
        /// Modellen wuchs der Verbrauch gemessen auf 1,4 GB, obwohl immer nur eines
        /// gebraucht wurde — nach einem verdichtenden Durchlauf blieben 9 MB übrig.
        /// Unterhalb der Schwelle wird nichts getan; bei kleinen Teilen kostet das
        /// nur Zeit.
        /// </summary>
        private static void TrimMemory()
        {
            try
            {
                Process p = Process.GetCurrentProcess();
                p.Refresh();
                if (p.PrivateMemorySize64 < 400L * 1048576L) return;

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch { }
        }

        private void ClearModel()
        {
            if (_liveMesh != null) _liveScene.Children.Remove(_liveMesh);
            _liveMesh = null;
            _model = null;
            _currentFile = null;
            _modelFile = null;
            _showingImage = false;
            _imageView.Visibility = Visibility.Collapsed;
            _imageView.Source = null;
            _viewport.Visibility = Visibility.Visible;
            _emptyHint.Visibility = Visibility.Visible;
            UpdateStatus();
            UpdateImageToggle();
            Title = "Elas3DView";
            TrimMemory();
        }

        private void UpdateStatus()
        {
            if (_currentFile == null)
            {
                _statusFile.Text = "";
                _statusInfo.Text = "";
                return;
            }

            long bytes = 0;
            try { bytes = new FileInfo(_currentFile).Length; }
            catch { }
            _statusFile.Text = Path.GetFileName(_currentFile);

            if (_showingImage)
            {
                BitmapSource b = _imageView.Source as BitmapSource;
                _statusInfo.Text = b == null
                    ? "Bild  ·  " + FormatBytes(bytes)
                    : string.Format(CultureInfo.CurrentCulture, "Bild  ·  {0:N0} × {1:N0} px  ·  {2}",
                        b.PixelWidth, b.PixelHeight, FormatBytes(bytes));
                return;
            }

            if (_model == null) { _statusInfo.Text = ""; return; }
            Vector3D s = _model.Size;
            _statusInfo.Text = string.Format(CultureInfo.CurrentCulture,
                "{0}  ·  {1:N0} Dreiecke  ·  {2:N1} × {3:N1} × {4:N1} mm  ·  {5:N1} cm³  ·  {6}",
                _model.Format, _model.TriangleCount, s.X, s.Y, s.Z, _model.VolumeMm3 / 1000.0, FormatBytes(bytes));
        }

        private static string FormatBytes(long b)
        {
            if (b >= 1048576) return (b / 1048576.0).ToString("N1", CultureInfo.CurrentCulture) + " MB";
            if (b >= 1024) return (b / 1024.0).ToString("N0", CultureInfo.CurrentCulture) + " kB";
            return b + " B";
        }

        private void ShowBusy(string text, bool cancellable)
        {
            _busyActive = true;
            _cancelRequested = false;
            _busyText.Text = text;
            _busyCancel.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
            _busy.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        private void BusyText(string text) { _busyText.Text = text; }

        private void HideBusy()
        {
            _busyActive = false;
            _busy.Visibility = Visibility.Collapsed;
            Mouse.OverrideCursor = null;
        }

        // =================================================================
        //  JPG-Export
        // =================================================================

        private int TargetWidth()
        {
            int i = _cbWidth == null ? 2 : _cbWidth.SelectedIndex;
            if (i < 0 || i >= WidthValues.Length) i = 2;
            return WidthValues[i];
        }

        private async void OnSaveJpg(object sender, RoutedEventArgs e)
        {
            if (_busyActive) return;
            if (_model == null || _modelFile == null)
            {
                AppDialog.Info(this, "Kein Modell geladen",
                    "Bitte zuerst eine STL- oder 3MF-Datei öffnen.");
                return;
            }
            if (_showingImage) ShowModelView();
            if (!ContrastOkay()) return;

            string target = Path.Combine(Path.GetDirectoryName(_modelFile),
                                         Path.GetFileNameWithoutExtension(_modelFile) + ".jpg");

            if (File.Exists(target))
            {
                DlgResult r = AppDialog.Choose(this, "Bild existiert bereits",
                    "Im Ordner liegt schon " + Path.GetFileName(target) + ".\n\nWas soll damit geschehen?",
                    "Überschreiben", "Danebenlegen");
                if (r == DlgResult.Cancel) return;
                if (r == DlgResult.Alt) target = UniquePath(target);
            }

            // Bei einer Million Dreiecken rechnet der Weichzeichner mehrere Sekunden.
            // Erst anzeigen, einen Zeichendurchlauf abwarten, dann rechnen.
            ShowBusy("Bild wird erzeugt …", false);
            await Task.Delay(1);
            try
            {
                SaveSnapshot(target);
                HideBusy();
                RefreshFolder(null);
                UpdateImageToggle();
                Toast("Gespeichert: " + Path.GetFileName(target), true);
            }
            catch (Exception ex)
            {
                HideBusy();
                AppDialog.Error(this, "Bild konnte nicht gespeichert werden", ex.Message);
            }
        }

        private static string UniquePath(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < 10000; i++)
            {
                string p = Path.Combine(dir, baseName + "_" + i + ext);
                if (!File.Exists(p)) return p;
            }
            return path;
        }

        private void SaveSnapshot(string targetPath)
        {
            int w = TargetWidth();
            int h = (int)Math.Round(w / Math.Max(0.2, ViewAspect()));
            if (h < 16) h = 16;

            BitmapSource img = RenderOffscreen(w, h);

            JpegBitmapEncoder enc = new JpegBitmapEncoder();
            enc.QualityLevel = 92;
            enc.Frames.Add(BitmapFrame.Create(img));
            using (FileStream fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                enc.Save(fs);
        }

        /// <summary>Rendert die aktuelle Ansicht abseits des Bildschirms, mit 2× Supersampling.</summary>
        private BitmapSource RenderOffscreen(int width, int height)
        {
            return RenderOffscreen(width, height, false);
        }

        /// <param name="likeView">
        /// Falsch: der Grund aus „Bildgrund" (das JPG, voreingestellt Weiß).
        /// Wahr: genau der Hintergrund der Ansicht — für das Titelbild der Übernahme.
        /// </param>
        private BitmapSource RenderOffscreen(int width, int height, bool likeView)
        {
            return RenderOffscreen(width, height, likeView, true);
        }

        /// <param name="withLogo">Falsch für die Bilder der Variantenliste: dort stört das Wasserzeichen nur.</param>
        private BitmapSource RenderOffscreen(int width, int height, bool likeView, bool withLogo)
        {
            int ss = 2;
            if ((long)width * ss * height * ss > 60000000L) ss = 1;
            int rw = width * ss, rh = height * ss;

            Model3DGroup scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(_ambient.Color));
            scene.Children.Add(new DirectionalLight(_key.Color, _key.Direction));
            scene.Children.Add(new DirectionalLight(_fill.Color, _fill.Direction));
            scene.Children.Add(new DirectionalLight(_rim.Color, _rim.Direction));
            scene.Children.Add(NewMeshModel());

            ModelVisual3D mv = new ModelVisual3D();
            mv.Content = scene;

            Viewport3D vp = new Viewport3D();
            vp.Camera = (Camera)_viewport.Camera.Clone();
            vp.Children.Add(mv);

            Grid host = new Grid();
            // Ohne Fenster (Schalter /dialogbilder) hat die Ansicht keine Breite;
            // dann gilt die Bildbreite, sonst geriete die Schraffur riesig.
            double viewWidth = _viewHost.ActualWidth >= 4 ? _viewHost.ActualWidth : width;
            double bgScale = rw / viewWidth;
            host.Background = likeView ? ViewBackground(bgScale) : ExportBackground(bgScale);
            host.Children.Add(vp);

            // Dasselbe Logo wie in der Ansicht, auf die Breite des Bildes gerechnet.
            if (withLogo && _logoImage != null && _logoImage.Visibility == Visibility.Visible && _logoImage.Source != null)
            {
                Image mark = new Image();
                mark.Source = _logoImage.Source;
                mark.Stretch = Stretch.Uniform;
                RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
                LayoutLogo(mark, rw);
                host.Children.Add(mark);
            }

            Size size = new Size(rw, rh);
            host.Measure(size);
            host.Arrange(new Rect(size));
            host.UpdateLayout();

            RenderTargetBitmap rtb = new RenderTargetBitmap(rw, rh, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(host);
            rtb.Freeze();
            if (ss == 1) return rtb;

            DrawingVisual dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
            using (DrawingContext dc = dv.RenderOpen())
                dc.DrawImage(rtb, new Rect(0, 0, width, height));

            RenderTargetBitmap final = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            final.Render(dv);
            final.Freeze();
            return final;
        }

        private async void OnBatchJpg(object sender, RoutedEventArgs e)
        {
            if (_busyActive) return;
            // Nur Modelle, auch wenn die Liste gerade Bilder mit anzeigt.
            List<FileEntry> list = new List<FileEntry>();
            foreach (FileEntry entry in _entries) if (!entry.IsImage) list.Add(entry);

            if (list.Count == 0)
            {
                AppDialog.Info(this, "Keine Modelle", "In diesem Ordner liegen keine STL- oder 3MF-Dateien.");
                return;
            }
            if (!AppDialog.Confirm(this, "Alle als JPG speichern",
                    "Für alle " + list.Count + " Modelle in\n" + _currentFolder +
                    "\nein JPG in der aktuellen Ausrichtung erzeugen?\n\nVorhandene Bilder werden überschrieben.",
                    "Erzeugen", false))
                return;

            if (_showingImage) ShowModelView();
            if (!ContrastOkay()) return;
            string keepFile = _modelFile;
            int done = 0, failed = 0;

            ShowBusy("Erzeuge Vorschaubilder …", true);
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (_cancelRequested) break;
                    FileEntry fe = list[i];
                    BusyText(string.Format("{0} / {1}   {2}", i + 1, list.Count, fe.Name));
                    await Task.Delay(1);

                    try
                    {
                        string p = fe.FullPath;
                        LoadedModel m = await Task.Run<LoadedModel>(delegate { return MeshLoader.Load(p); });
                        _model = m;
                        _currentFile = p;
                        _modelFile = p;
                        if (_liveMesh != null) _liveScene.Children.Remove(_liveMesh);
                        _liveMesh = NewMeshModel();
                        _liveScene.Children.Add(_liveMesh);
                        FitToModel(true);
                        UpdateCamera();
                        TrimMemory();

                        SaveSnapshot(Path.Combine(Path.GetDirectoryName(p),
                                                  Path.GetFileNameWithoutExtension(p) + ".jpg"));
                        done++;
                    }
                    catch { failed++; }
                }
            }
            finally { HideBusy(); }

            if (keepFile != null && File.Exists(keepFile))
            {
                _currentFile = null;
                await LoadFile(keepFile);
            }
            UpdateStatus();

            string msg = done + (done == 1 ? " Bild wurde erzeugt." : " Bilder wurden erzeugt.");
            if (failed > 0) msg += "\n" + failed + (failed == 1 ? " Datei" : " Dateien") + " konnten nicht gelesen werden.";
            if (_cancelRequested) msg += "\nDer Vorgang wurde abgebrochen.";
            if (failed > 0) AppDialog.Warn(this, "Vorschaubilder", msg);
            else AppDialog.Success(this, "Vorschaubilder", msg);
        }

        // =================================================================
        //  Umbenennen
        // =================================================================

        private List<string> Sidecars(string modelPath)
        {
            List<string> found = new List<string>();
            string dir = Path.GetDirectoryName(modelPath);
            string b = Path.GetFileNameWithoutExtension(modelPath);
            string[] exts = { ".jpg", ".jpeg", ".png" };
            foreach (string se in exts)
            {
                string p = Path.Combine(dir, b + se);
                if (File.Exists(p)) found.Add(p);
            }
            return found;
        }

        private void OnRename(object sender, RoutedEventArgs e)
        {
            if (_busyActive) return;      // etwa während eine Datei hochgeladen wird
            if (_currentFile == null || !File.Exists(_currentFile))
            {
                AppDialog.Info(this, "Kein Modell geladen", "Bitte zuerst eine Datei öffnen.");
                return;
            }

            string oldPath = _currentFile;
            string dir = Path.GetDirectoryName(oldPath);
            string ext = Path.GetExtension(oldPath);
            string oldBase = Path.GetFileNameWithoutExtension(oldPath);

            // Nur ein Modell zieht seine Bilder mit; ein Bild wird für sich umbenannt.
            List<string> sidecars = MeshLoader.IsSupported(oldPath)
                ? Sidecars(oldPath) : new List<string>();

            RenameDialog dlg = new RenameDialog(oldBase, ext, sidecars.Count);
            dlg.Owner = this;
            SetScrim(true);
            bool ok;
            try { ok = dlg.ShowDialog() == true; }
            finally { SetScrim(false); }
            if (!ok) return;

            string newBase = dlg.NewBaseName;
            if (string.Equals(newBase, oldBase, StringComparison.Ordinal)) return;

            string newPath = Path.Combine(dir, newBase + ext);
            if (File.Exists(newPath))
            {
                AppDialog.Warn(this, "Name schon vergeben",
                    "Es gibt bereits eine Datei mit diesem Namen:\n\n" + Path.GetFileName(newPath));
                return;
            }

            try { File.Move(oldPath, newPath); }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Umbenennen fehlgeschlagen", ex.Message);
                return;
            }

            int extra = 0;
            if (dlg.RenameSidecars)
            {
                foreach (string p in sidecars)
                {
                    string t = Path.Combine(dir, newBase + Path.GetExtension(p));
                    try { if (!File.Exists(t)) { File.Move(p, t); extra++; } }
                    catch { }
                }
            }

            if (string.Equals(_modelFile, oldPath, StringComparison.OrdinalIgnoreCase)) _modelFile = newPath;
            _currentFile = newPath;
            Title = Path.GetFileName(newPath) + " – Elas3DView";
            RefreshFolder(newPath);
            UpdateStatus();
            UpdateImageToggle();
            Toast("Umbenannt in " + Path.GetFileName(newPath) +
                  (extra > 0 ? "  (+ " + extra + " Bild" + (extra == 1 ? "" : "er") + ")" : ""), true);
        }

        // =================================================================
        //  Löschen
        // =================================================================

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (_busyActive) return;
            if (_currentFile == null || !File.Exists(_currentFile))
            {
                AppDialog.Info(this, "Kein Modell geladen", "Bitte zuerst eine Datei öffnen.");
                return;
            }

            List<string> sidecars = MeshLoader.IsSupported(_currentFile)
                ? Sidecars(_currentFile) : new List<string>();
            string list = "•  " + Path.GetFileName(_currentFile);
            foreach (string p in sidecars) list += "\n•  " + Path.GetFileName(p);

            string tail = sidecars.Count == 0
                ? "\n\nDie Datei wandert in den Papierkorb und kann von dort zurückgeholt werden."
                : "\n\nDas zugehörige Bild wird mit weggeräumt. Alles wandert in den Papierkorb und kann von dort zurückgeholt werden.";

            if (!AppDialog.Confirm(this, "Wirklich löschen?", list + tail, "In den Papierkorb", true))
                return;

            int index = _fileList.SelectedIndex;
            List<string> victims = new List<string>();
            victims.Add(_currentFile);
            victims.AddRange(sidecars);

            int removed = 0;
            foreach (string p in victims)
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        p,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                        Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                    removed++;
                }
                catch (Exception ex)
                {
                    AppDialog.Error(this, "Löschen fehlgeschlagen",
                        Path.GetFileName(p) + "\n\n" + ex.Message);
                    break;
                }
            }

            string gone = Path.GetFileName(_currentFile);
            bool wasModel = string.Equals(_modelFile, _currentFile, StringComparison.OrdinalIgnoreCase);
            if (wasModel) ClearModel();
            else
            {
                _currentFile = _modelFile;
                if (_showingImage) ShowModelView();
            }
            RefreshFolder(null);
            UpdateImageToggle();

            // Nachbarn auswählen, damit man zügig weiterarbeiten kann
            if (_fileList.Items.Count > 0)
            {
                if (index >= _fileList.Items.Count) index = _fileList.Items.Count - 1;
                if (index < 0) index = 0;
                _fileList.SelectedIndex = index;
                _fileList.ScrollIntoView(_fileList.SelectedItem);
            }

            Toast(gone + " in den Papierkorb verschoben" +
                  (removed > 1 ? " (mit " + (removed - 1) + " Bild" + (removed == 2 ? "" : "ern") + ")" : ""), true);
        }

        // =================================================================
        //  Übernahme nach ElasPrintFlow
        // =================================================================

        /// <summary>
        /// Ein Knopf für den ganzen Weg: Modell mit Titelbild in ElasPrintFlow
        /// anlegen, die Ablage dort prüfen und dann — nach Rückfrage — hier
        /// aufräumen. Gelöscht wird nur, was drüben Byte für Byte nachgewiesen ist;
        /// bei jedem Fehler davor bleibt auf diesem Rechner alles, wie es war.
        /// </summary>
        private async void OnSendToPrintFlow(object sender, RoutedEventArgs e)
        {
            if (_busyActive) return;
            if (_model == null || _modelFile == null || !File.Exists(_modelFile))
            {
                AppDialog.Info(this, "Kein Modell geladen", "Bitte zuerst eine STL- oder 3MF-Datei öffnen.");
                return;
            }
            if (_showingImage) ShowModelView();

            string file = _modelFile;
            long bytes = new FileInfo(file).Length;
            if (bytes > PrintFlowClient.MaxUpload)
            {
                AppDialog.Warn(this, "Datei zu groß",
                    Path.GetFileName(file) + " hat " + FormatBytes(bytes) + ".\n\n" +
                    "ElasPrintFlow nimmt in einem Stück höchstens " +
                    FormatBytes(PrintFlowClient.MaxUpload) + " an.");
                return;
            }

            // ---- Was angehakt ist, geht MIT in die Akte ----
            // Das geöffnete Modell ist die Hauptdatei; die angehakten liegen danach
            // als weitere Quelldateien in seinem Ordner. Die Grenze gilt je Datei:
            // Jede reist als eigene Anfrage.
            List<string> extraPaths = MarkedExtras(file);
            long allBytes = bytes;
            List<string[]> fileRows = new List<string[]>();
            fileRows.Add(new string[] { Path.GetFileName(file), FormatBytes(bytes) });
            foreach (string extra in extraPaths)
            {
                long size = new FileInfo(extra).Length;
                if (size > PrintFlowClient.MaxUpload)
                {
                    AppDialog.Warn(this, "Datei zu groß",
                        Path.GetFileName(extra) + " hat " + FormatBytes(size) + ".\n\n" +
                        "ElasPrintFlow nimmt in einem Stück höchstens " +
                        FormatBytes(PrintFlowClient.MaxUpload) + " an. Bitte das Häkchen dort entfernen.");
                    return;
                }
                allBytes += size;
                fileRows.Add(new string[] { Path.GetFileName(extra), FormatBytes(size) });
            }

            List<string> categories = await ConnectPrintFlow(false, null);
            if (categories == null) return;

            // Bei einer Million Dreiecken dauert das Bild Sekunden — erst anzeigen, dann rechnen.
            BitmapSource picture;
            ShowBusy("Titelbild wird erzeugt …", false);
            await Task.Delay(1);
            try { picture = RenderTitle(); }
            catch (Exception ex)
            {
                HideBusy();
                AppDialog.Error(this, "Titelbild konnte nicht erzeugt werden", ex.Message);
                return;
            }
            HideBusy();

            List<string> marks = new List<string>();
            marks.Add("Ohne Wasserzeichen");
            for (int i = 1; i < _cbLogo.Items.Count; i++) marks.Add(Convert.ToString(_cbLogo.Items[i]));

            string name = Path.GetFileNameWithoutExtension(file);
            string category = _pfCategory;
            PrintFlowHit attachTo = null;       // eine BESTEHENDE Akte statt eines neuen Modells

            while (true)
            {
                string what = extraPaths.Count == 0
                    ? Path.GetFileName(file)
                    : Path.GetFileName(file) + " und " + extraPaths.Count +
                      (extraPaths.Count == 1 ? " weitere Datei" : " weitere Dateien");
                PrintFlowDialog dlg = new PrintFlowDialog(
                    what + "  ·  " + FormatBytes(allBytes) + "   →   " + _pfUrl,
                    _pfUrl + (_pfUser.Length > 0 ? "  ·  angemeldet als " + _pfUser : ""),
                    name, categories, category, marks, Math.Max(0, _cbLogo.SelectedIndex), picture,
                    delegate(int mark)
                    {
                        if (mark >= 0 && mark < _cbLogo.Items.Count) _cbLogo.SelectedIndex = mark;
                        return RenderTitle();
                    },
                    extraPaths.Count == 0 ? null : fileRows);
                bool accepted = dlg.ShowOver(this);
                name = dlg.ModelName;
                category = dlg.Category;
                picture = dlg.Picture;

                if (dlg.ChangeConnection)
                {
                    categories = await ConnectPrintFlow(true, null);
                    if (categories == null) return;
                    continue;
                }
                if (dlg.SearchExisting)
                {
                    // Kein neues Modell: in den vorhandenen suchen und dort anhängen.
                    attachTo = PickExisting(what + "  ·  " + FormatBytes(allBytes), name, picture,
                                            Path.GetFileName(file), null);
                    if (attachTo == null) continue;     // „Zurück"
                    break;
                }
                if (!accepted) return;
                if (!ContrastOkay("Trotzdem übernehmen", true)) continue;

                // Die Hauptdatei heißt drüben wie das Modell. Trägt eine der
                // weiteren Dateien schon genau diesen Namen, lägen zwei gleiche im
                // Ordner — ElasPrintFlow lehnt das ab, aber erst nach dem Hochladen.
                string stored = PrintFlowClient.CleanName(name) + Path.GetExtension(file);
                string clash = null;
                foreach (string extra in extraPaths)
                    if (string.Equals(Path.GetFileName(extra), stored, StringComparison.OrdinalIgnoreCase))
                        clash = Path.GetFileName(extra);
                if (clash != null)
                {
                    AppDialog.Warn(this, "Zwei Dateien hießen gleich",
                        "Die Hauptdatei wird in ElasPrintFlow zu „" + stored + "“ — so heißt aber schon die " +
                        "angehakte Datei „" + clash + "“.\n\n" +
                        "Bitte dem Modell einen anderen Namen geben oder das Häkchen dort entfernen.");
                    continue;
                }

                // Vorab fragen, ob es Name und Kategorie schon gibt — sonst reist
                // die Datei erst hinüber und wird dort abgelehnt.
                ShowBusy("Name wird geprüft …", false);
                PrintFlowHit taken = null;
                PrintFlowException problem = null;
                try { taken = await _pf.FindExisting(name, category, System.Threading.CancellationToken.None); }
                catch (Exception ex) { problem = ex as PrintFlowException ?? new PrintFlowException(ex.Message); }
                HideBusy();

                if (problem != null && problem.NeedLogin)
                {
                    categories = await ConnectPrintFlow(true, "Die Anmeldung ist abgelaufen. Bitte neu anmelden.");
                    if (categories == null) return;
                    continue;
                }
                if (problem != null)
                {
                    AppDialog.Error(this, "ElasPrintFlow", problem.Message);
                    return;
                }
                if (taken != null)
                {
                    // Den Namen gibt es dort schon (Tristan, 15.11.2026): an die
                    // bestehende Akte anhängen — oder zurück und dem neuen Modell
                    // einen anderen Namen geben. Die Akte steht mit ihrem Bild da;
                    // ob es dasselbe Modell ist, sieht man, bevor man entscheidet.
                    attachTo = PickExisting(what, taken.Name, picture, Path.GetFileName(file), taken);
                    if (attachTo == null) continue;     // „Anderer Name"
                    break;
                }
                break;
            }

            if (attachTo != null)
            {
                await AttachToExisting(file, extraPaths, attachTo);
                return;
            }
            _pfCategory = category;

            // ---- hinüber ----
            byte[] png;
            try { png = EncodePng(picture); }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Titelbild konnte nicht erzeugt werden", ex.Message);
                return;
            }

            // ---- Bilder für die Variantenliste ----
            // Jede weitere Datei bekommt ihr eigenes Bild, in derselben Blickrichtung:
            // Die Namen unterscheiden sich oft nur um eine Ziffer, ein Bild
            // unterscheidet sie auf einen Blick. Dazu wird jede einmal geladen —
            // wie bei „Alle als JPG"; danach steht wieder die Hauptdatei da.
            List<PrintFlowFile> extras = new List<PrintFlowFile>();
            byte[] mainPicture = null;
            if (extraPaths.Count > 0)
            {
                PictogramRun run = await RenderPictograms(file, extraPaths);
                if (run.Stopped)
                {
                    Toast("Übernahme abgebrochen — nichts angelegt, nichts gelöscht", false);
                    return;
                }
                extras = run.Extras;
                mainPicture = run.Main;
            }

            PrintFlowModel model = await RunTransfer(
                "Datei wird hochgeladen …", "Übernahme abgebrochen — nichts angelegt, nichts gelöscht",
                delegate(IProgress<PrintFlowProgress> progress, System.Threading.CancellationToken ct)
                {
                    return _pf.Import(file, name, category, png, extras, mainPicture, progress, ct);
                });
            if (model == null) return;

            FinishTransfer(file, extraPaths, model);
        }

        /// <summary>
        /// Zeigt die Suche in den Modellen von ElasPrintFlow — oder, wenn der Name
        /// schon vergeben ist, gleich die Akte dieses Namens.
        /// </summary>
        /// <param name="preset">Die Akte mit dem vergebenen Namen; null bei freier Suche.</param>
        /// <returns>Die Akte, an die angehängt werden soll; null, wenn der Nutzer zurück will.</returns>
        private PrintFlowHit PickExisting(string what, string query, BitmapSource mine, string mineInfo,
                                          PrintFlowHit preset)
        {
            string caption = preset == null
                ? what + "   —   in ElasPrintFlow nachsehen, ob es das Modell schon gibt, und dort anhängen."
                : "„" + preset.Name + "“ gibt es in der Kategorie „" + preset.Category + "“ schon. " +
                  "An diese Akte anhängen — oder zurück und dem neuen Modell einen anderen Namen geben.";

            PrintFlowSearchDialog dlg = new PrintFlowSearchDialog(
                caption, query, mine, mineInfo, preset == null ? "Zurück" : "Anderer Name", preset,
                delegate(string q, System.Threading.CancellationToken ct) { return _pf.Search(q, ct); },
                delegate(PrintFlowHit h, System.Threading.CancellationToken ct) { return _pf.Picture(h.PreviewPath, ct); });
            return dlg.ShowOver(this) ? dlg.Chosen : null;
        }

        /// <summary>
        /// Hängt das geöffnete Modell samt den angehakten Dateien an eine Akte, die
        /// ElasPrintFlow schon führt — derselbe Vorgang wie bei den Häkchen, nur
        /// dass die Akte nicht neu entsteht: Jede Datei bekommt ihr Vorschaubild,
        /// keine eine Artikelnummer, danach Vergleich und Löschfrage wie immer.
        /// </summary>
        private async Task AttachToExisting(string file, List<string> extraPaths, PrintFlowHit target)
        {
            // Liegt dort schon etwas gleichen Namens? Vorher sagen, was dann geschieht.
            ShowBusy("Akte wird gelesen …", false);
            List<PrintFlowStored> stored = null;
            PrintFlowException problem = null;
            try { stored = await _pf.StoredFiles(target.Id, System.Threading.CancellationToken.None); }
            catch (Exception ex) { problem = ex as PrintFlowException ?? new PrintFlowException(ex.Message); }
            HideBusy();
            if (problem != null)
            {
                if (problem.NeedLogin) _pfSession = "";
                AppDialog.Error(this, "ElasPrintFlow", problem.Message + "\n\nAuf diesem Rechner ist nichts verändert.");
                return;
            }

            List<string> all = new List<string>();
            all.Add(file);
            all.AddRange(extraPaths);
            string same = "";
            foreach (string p in all)
                foreach (PrintFlowStored s in stored)
                    if (string.Equals(s.Name, Path.GetFileName(p), StringComparison.OrdinalIgnoreCase))
                        same += "\n•  " + s.Name;
            if (same.Length > 0 && !AppDialog.Ask(this, DlgKind.Question,
                    "In der Akte liegt schon eine Datei dieses Namens",
                    "In „" + target.Name + "“ liegt bereits:" + same + "\n\n" +
                    "Ist die Datei dort dieselbe, geht sie nicht noch einmal hinüber. " +
                    "Ist sie eine andere, kommt die neue mit dem Zusatz „(2)“ dazu. " +
                    "Überschrieben wird nichts.",
                    "Fortfahren", "Abbrechen", false))
                return;

            // Jede Datei bekommt ihr Bild für die Variantenliste — auch die geöffnete:
            // Sie ist in der fremden Akte eine Datei unter mehreren.
            PictogramRun run = await RenderPictograms(file, extraPaths);
            if (run.Stopped)
            {
                Toast("Übernahme abgebrochen — nichts angehängt, nichts gelöscht", false);
                return;
            }
            List<PrintFlowFile> files = new List<PrintFlowFile>();
            PrintFlowFile head = new PrintFlowFile();
            head.Path = file;
            head.PicturePng = run.Main;
            files.Add(head);
            files.AddRange(run.Extras);

            PrintFlowModel model = await RunTransfer(
                "Akte wird gelesen …", "Übernahme abgebrochen — nichts angehängt, nichts gelöscht",
                delegate(IProgress<PrintFlowProgress> progress, System.Threading.CancellationToken ct)
                {
                    return _pf.Attach(target, files, progress, ct);
                });
            if (model == null) return;

            FinishTransfer(file, extraPaths, model);
        }

        private sealed class PictogramRun
        {
            public byte[] Main;
            public List<PrintFlowFile> Extras = new List<PrintFlowFile>();
            public bool Stopped;
        }

        /// <summary>
        /// Die Bilder für die Variantenliste: das der geöffneten Datei und eines je
        /// weiterer Datei, in der Blickrichtung, die steht. Dazu wird jede weitere
        /// einmal geladen — wie bei „Alle als JPG"; danach steht wieder die
        /// geöffnete da.
        /// </summary>
        private async Task<PictogramRun> RenderPictograms(string file, List<string> extraPaths)
        {
            PictogramRun run = new PictogramRun();
            ShowBusy("Vorschaubilder werden erzeugt …", extraPaths.Count > 0);
            await Task.Delay(1);
            try
            {
                try { run.Main = EncodePng(RenderPictogram()); } catch { run.Main = null; }

                for (int i = 0; i < extraPaths.Count; i++)
                {
                    if (_cancelRequested) { run.Stopped = true; break; }
                    string p = extraPaths[i];
                    BusyText(string.Format("Vorschaubild {0} / {1}   {2}", i + 1, extraPaths.Count, Path.GetFileName(p)));
                    await Task.Delay(1);

                    PrintFlowFile one = new PrintFlowFile();
                    one.Path = p;
                    try
                    {
                        LoadedModel m = await Task.Run<LoadedModel>(delegate { return MeshLoader.Load(p); });
                        one.PicturePng = PictogramOf(m);
                    }
                    catch { one.PicturePng = null; }    // die Datei geht trotzdem mit, nur ohne Bild
                    run.Extras.Add(one);
                }
                if (_cancelRequested) run.Stopped = true;
            }
            finally { HideBusy(); }

            // Zurück zur geöffneten Datei — sie ist das Modell, um das es geht.
            if (extraPaths.Count > 0)
            {
                _currentFile = null;
                await LoadFile(file);
            }
            return run;
        }

        /// <summary>
        /// Der Rahmen um jede Übergabe: Hinweis mit Fortschritt und Abbrechen,
        /// danach die Meldung, falls abgebrochen wurde oder es scheiterte.
        /// </summary>
        /// <returns>Das Ergebnis; null, wenn es keines gibt — dann ist schon gesagt, warum.</returns>
        private async Task<PrintFlowModel> RunTransfer(string firstText, string canceledText,
            Func<IProgress<PrintFlowProgress>, System.Threading.CancellationToken, Task<PrintFlowModel>> work)
        {
            ShowBusy(firstText, true);
            _pfCancel = new System.Threading.CancellationTokenSource();
            bool live = true;
            Progress<PrintFlowProgress> progress = new Progress<PrintFlowProgress>(delegate(PrintFlowProgress p)
            {
                if (!live) return;      // Meldungen kommen nachgereicht; eine späte darf nichts mehr umschreiben
                BusyText(p.Text);
                _busyCancel.Visibility = p.CanCancel ? Visibility.Visible : Visibility.Collapsed;
            });

            PrintFlowModel model = null;
            Exception failure = null;
            bool canceled = false;
            try { model = await work(progress, _pfCancel.Token); }
            catch (OperationCanceledException) { canceled = true; }
            catch (Exception ex) { failure = ex; }

            live = false;
            _pfCancel.Dispose();
            _pfCancel = null;
            HideBusy();
            SelectInList(_currentFile);

            if (canceled)
            {
                Toast(canceledText, false);
                return null;
            }
            if (failure != null)
            {
                PrintFlowException pfx = failure as PrintFlowException;
                if (pfx != null && pfx.NeedLogin) _pfSession = "";
                AppDialog.Error(this, "Übernahme fehlgeschlagen",
                    failure.Message + "\n\nAuf diesem Rechner ist nichts verändert.");
                return null;
            }
            return model;
        }

        /// <summary>
        /// Das Bild einer Datei für die Variantenliste in ElasPrintFlow: die
        /// Ansicht, wie sie steht, samt Hintergrund — aber ohne Logo und kleiner
        /// als das Titelbild. Es soll Dateien unterscheidbar machen, nicht werben.
        /// </summary>
        private BitmapSource RenderPictogram()
        {
            int w = 960;
            int h = (int)Math.Round(w / Math.Max(0.2, ViewAspect()));
            if (h < 16) h = 16;
            return RenderOffscreen(w, h, true, false);
        }

        /// <summary>
        /// Stellt ein geladenes Modell NUR FÜR SEIN BILD in die Ansicht — in der
        /// Blickrichtung, die gerade steht, eingepasst wie bei „Alle als JPG".
        /// Die geöffnete Datei bleibt, wer sie war: Der Aufrufer lädt sie danach
        /// wieder, sonst zeigte die Ansicht ein Modell und die Liste ein anderes.
        /// </summary>
        private byte[] PictogramOf(LoadedModel m)
        {
            _model = m;
            if (_liveMesh != null) _liveScene.Children.Remove(_liveMesh);
            _liveMesh = NewMeshModel();
            _liveScene.Children.Add(_liveMesh);
            FitToModel(true);
            UpdateCamera();
            TrimMemory();
            return EncodePng(RenderPictogram());
        }

        /// <summary>
        /// Für die Prüfung ohne Bildschirm (Schalter /dialogbilder): die Bilder,
        /// die bei einer Übernahme für die angehakten Dateien entstünden, und das
        /// der Hauptdatei — danach steht wieder die Hauptdatei in der Ansicht.
        /// </summary>
        internal void ProbePictograms(string dir)
        {
            string main = _modelFile;
            if (main == null) return;
            ProbeLayout();
            File.WriteAllBytes(Path.Combine(dir, "8-vorschau-hauptdatei.png"), EncodePng(RenderPictogram()));
            int n = 0;
            foreach (string extra in MarkedExtras(main))
            {
                ProbeLayout();
                File.WriteAllBytes(Path.Combine(dir, "8-vorschau-dazu-" + (++n) + ".png"),
                                   PictogramOf(MeshLoader.Load(extra)));
            }
            SetModel(MeshLoader.Load(main), main);
        }

        /// <summary>
        /// Für die Prüfung ohne Bildschirm (Schalter /dialogbilder): „Aktualisieren"
        /// an einem Ordner, in dem nacheinander eine Datei dazukommt, verschwindet
        /// und nichts geschieht. Gearbeitet wird im ZIELORDNER der Probe — der
        /// gehört ihr; der Ordner des Modells bleibt unberührt.
        /// </summary>
        /// <returns>Je Schritt: Zahl der Zeilen und die Meldung.</returns>
        internal string ProbeRefresh(string dir, string modelPath)
        {
            _currentFolder = dir;
            RefreshFolder(null);
            string report = "leer:        " + _entries.Count + " Zeilen\r\n";

            string copy = Path.Combine(dir, "probe-neu" + Path.GetExtension(modelPath));
            File.Copy(modelPath, copy, true);
            RefreshList(true);
            report += "dazugelegt:  " + _entries.Count + " Zeilen  |  " + _toastText.Text + "\r\n";

            RefreshList(true);
            report += "nochmal:     " + _entries.Count + " Zeilen  |  " + _toastText.Text + "\r\n";

            File.Delete(copy);
            RefreshList(true);
            report += "weggenommen: " + _entries.Count + " Zeilen  |  " + _toastText.Text + "\r\n";
            return report;
        }

        /// <summary>
        /// Nur für die Prüfung ohne Bildschirm: Ein nie gezeigtes Fenster verliert
        /// nach jedem Zeichnen seine Maße (siehe DialogProbe), und die Bilder
        /// rechnen mit dem Seitenverhältnis der Ansicht. Im echten Fenster steht
        /// das fest; hier wird es vor jedem Bild neu gesetzt.
        /// </summary>
        private void ProbeLayout()
        {
            FrameworkElement root = (FrameworkElement)Content;
            root.Measure(new Size(1340, 820));
            root.Arrange(new Rect(0, 0, 1340, 820));
            root.UpdateLayout();
        }

        /// <summary>
        /// Stellt die Verbindung her — mit der gemerkten Sitzung, sonst über die
        /// Anmeldung. Die Kategorienliste ist zugleich die Probe, ob die Sitzung gilt.
        /// </summary>
        /// <returns>Die Kategorien; null, wenn der Nutzer abbricht.</returns>
        private async Task<List<string>> ConnectPrintFlow(bool askFirst, string reason)
        {
            while (true)
            {
                if (!askFirst && _pfSession.Length > 0)
                {
                    if (_pf == null)
                    {
                        try { _pf = new PrintFlowClient(_pfUrl, _pfSession); }
                        catch (PrintFlowException) { _pf = null; }
                    }
                    if (_pf != null)
                    {
                        ShowBusy("Verbinde mit ElasPrintFlow …", false);
                        List<string> found = null;
                        PrintFlowException problem = null;
                        try { found = await _pf.Categories(System.Threading.CancellationToken.None); }
                        catch (Exception ex) { problem = ex as PrintFlowException ?? new PrintFlowException(ex.Message); }
                        HideBusy();
                        if (found != null) return found;

                        if (problem.NeedLogin)
                        {
                            _pfSession = "";
                            reason = "Die Anmeldung ist abgelaufen. Bitte neu anmelden.";
                        }
                        else
                        {
                            DlgResult r = AppDialog.Choose(this, "ElasPrintFlow antwortet nicht",
                                problem.Message + "\n\nLäuft der Server, und stimmt die Adresse?",
                                "Erneut versuchen", "Verbindung ändern");
                            if (r == DlgResult.Cancel) return null;
                            if (r == DlgResult.Ok) continue;
                            reason = null;
                        }
                    }
                }
                askFirst = false;

                PrintFlowLoginDialog dlg = new PrintFlowLoginDialog(_pfUrl, _pfUser, reason);
                if (!dlg.ShowOver(this)) return null;

                if (_pf != null) _pf.Dispose();
                _pf = dlg.Client;
                _pfUrl = _pf.BaseUrl;
                _pfUser = dlg.User;
                _pfSession = _pf.Session;
                SaveSettings();         // die Sitzung soll einen Absturz überleben
                reason = null;
            }
        }

        /// <summary>
        /// Das Titelbild: dieselbe Rechnung wie beim JPG, aber höchstens 1920 px
        /// breit — es reist als Text in der Anfrage mit und liegt danach als
        /// Vorschau im Modellordner, nicht als Druckvorlage.
        ///
        /// Der Grund ist der HINTERGRUND DER ANSICHT, nicht „Bildgrund" (Tristan,
        /// 14.11.2026). Bis dahin nahm das Titelbild den Bildgrund des JPG mit —
        /// voreingestellt Weiß. Wer sich einen dunklen Grund mit dem weißen Logo
        /// eingerichtet hatte, bekam ein weißes Bild, auf dem das Logo
        /// verschwand, während der Dialog „so, wie die Ansicht gerade steht"
        /// versprach. „Bildgrund" gilt weiter für „JPG speichern".
        /// </summary>
        private BitmapSource RenderTitle()
        {
            int w = Math.Min(TargetWidth(), 1920);
            int h = (int)Math.Round(w / Math.Max(0.2, ViewAspect()));
            if (h < 16) h = 16;
            return RenderOffscreen(w, h, true);
        }

        /// <summary>PNG, weil ElasPrintFlow das Titelbild unter Name.png ablegt, was immer ankommt.</summary>
        private static byte[] EncodePng(BitmapSource img)
        {
            // Der Grund ist immer deckend; ohne Alphakanal wird die Datei kleiner.
            FormatConvertedBitmap flat = new FormatConvertedBitmap(img, PixelFormats.Bgr24, null, 0);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(flat));
            using (MemoryStream ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        /// <summary>Ordner des Modells in Windows-Schreibweise; ElasPrintFlow führt ihn mit Schrägstrichen.</summary>
        private static string NasFolder(string nasPath)
        {
            if (string.IsNullOrEmpty(nasPath)) return null;
            string p = nasPath.Replace('/', '\\');
            int cut = p.LastIndexOf('\\');
            return cut > 2 ? p.Substring(0, cut) : p;
        }

        internal const string DeleteTitle = "Übernommen — hier löschen?";
        internal const string DeleteYes = "Endgültig löschen";
        internal const string DeleteNo = "Behalten";

        /// <summary>Was angelegt wurde: Name, Artikelnummer, Kategorie und der Ordner auf der NAS.</summary>
        internal static string TransferSummary(PrintFlowModel model)
        {
            if (model.Attached) return AttachSummary(model);

            string head = "„" + model.Name + "“ liegt jetzt in ElasPrintFlow.\n\n" +
                          "Artikelnummer:  " + (string.IsNullOrEmpty(model.ArticleNumber) ? "–" : model.ArticleNumber) + "\n" +
                          "Kategorie:  " + model.Category;
            // Mehrere Dateien, EINE Akte — die weiteren stehen dort unter
            // „Quelldateien & Varianten", nicht als eigene Modelle.
            int more = model.ExtraNames == null ? 0 : model.ExtraNames.Count;
            if (more > 0)
                head += "\nDateien:  " + (more + 1) + " — die Hauptdatei und " + more + " weitere im selben Ordner";
            if (model.PicturesFailed > 0)
                head += "\n" + model.PicturesFailed + (model.PicturesFailed == 1
                    ? " Vorschaubild ließ sich nicht ablegen"
                    : " Vorschaubilder ließen sich nicht ablegen") + " — in der Modellakte nachholbar";
            string folder = NasFolder(model.NasPath);
            return folder == null ? head : head + "\n" + folder;
        }

        internal static string DeleteQuestion(PrintFlowModel model, List<string> victims)
        {
            string list = "";
            foreach (string p in victims) list += "\n•  " + Path.GetFileName(p);
            int files = model.Attached
                ? model.ExtraNames.Count + model.AlreadyThere.Count
                : 1 + (model.ExtraNames == null ? 0 : model.ExtraNames.Count);

            // Lag beim Anhängen schon alles dort, steht der Nachweis bereits oben.
            string proof = model.Attached && model.ExtraNames.Count == 0 ? ""
                : (files == 1
                       ? "Die Datei auf der NAS stimmt Byte für Byte mit der lokalen überein."
                       : "Alle " + files + " Dateien auf der NAS stimmen Byte für Byte mit den lokalen überein.") + "\n\n";

            return TransferSummary(model) + "\n\n" + proof +
                   "Von diesem Rechner endgültig löschen:" + list + "\n\n" +
                   "Das geht am Papierkorb vorbei und lässt sich nicht zurückholen.";
        }

        internal const string AttachedTitle = "Angehängt — hier löschen?";
        internal const string AlreadyThereTitle = "Liegt dort schon — hier löschen?";

        /// <summary>
        /// Was beim Anhängen an eine bestehende Akte geschehen ist: was dazukam —
        /// unter dem Namen, den es dort trägt —, was schon gleich dalag, und wo.
        /// </summary>
        private static string AttachSummary(PrintFlowModel model)
        {
            int added = model.ExtraNames.Count, there = model.AlreadyThere.Count;
            string head;
            if (added == 0)
            {
                head = (there == 1 ? "Die Datei lag" : "Alle " + there + " Dateien lagen") +
                       " in der Akte „" + model.Name + "“ schon — Byte für Byte gleich. " +
                       "Hochgeladen wurde nichts.";
            }
            else
            {
                head = "„" + model.Name + "“ hat in ElasPrintFlow jetzt " +
                       (added == 1 ? "eine weitere Datei:" : added + " weitere Dateien:");
                foreach (string n in model.ExtraNames) head += "\n•  " + n;
                if (there > 0)
                    head += "\n" + (there == 1 ? "Eine Datei lag" : there + " Dateien lagen") +
                            " dort schon, Byte für Byte gleich.";
            }

            head += "\n\nArtikelnummer:  " + (string.IsNullOrEmpty(model.ArticleNumber) ? "–" : model.ArticleNumber) +
                    "\nKategorie:  " + model.Category;
            if (model.PicturesFailed > 0)
                head += "\n" + model.PicturesFailed + (model.PicturesFailed == 1
                    ? " Vorschaubild ließ sich nicht ablegen"
                    : " Vorschaubilder ließen sich nicht ablegen") + " — in der Modellakte nachholbar";
            string folder = NasFolder(model.NasPath);
            return folder == null ? head : head + "\n" + folder;
        }

        /// <summary>
        /// Was sich in der Zusammenfassung anklicken lässt (Tristan, 14.11.2026:
        /// „einen Link, um direkt dorthin zu springen"): Der Ablageort öffnet den
        /// Ordner auf der NAS im Explorer, die Datei darin markiert; die
        /// Artikelnummer öffnet das Modell in ElasPrintFlow.
        /// </summary>
        internal static DlgLink[] TransferLinks(PrintFlowModel model, string baseUrl)
        {
            List<DlgLink> links = new List<DlgLink>();

            if (model.Id > 0 && !string.IsNullOrEmpty(model.ArticleNumber) && !string.IsNullOrEmpty(baseUrl))
            {
                string page = baseUrl.TrimEnd('/') + "/model-manager/inventory/" + model.Id;
                links.Add(new DlgLink(model.ArticleNumber, "Modell in ElasPrintFlow öffnen: " + page,
                    delegate { Process.Start(page); }));
            }

            string folder = NasFolder(model.NasPath);
            if (folder != null)
            {
                string file = model.NasPath.Replace('/', Path.DirectorySeparatorChar);
                links.Add(new DlgLink(folder, "Ordner auf der NAS im Explorer öffnen",
                    delegate
                    {
                        if (File.Exists(file)) Process.Start("explorer.exe", "/select," + Quoted(file));
                        else Process.Start("explorer.exe", Quoted(folder));
                    }));
            }
            return links.ToArray();
        }

        private static string Quoted(string path) { return (char)34 + path + (char)34; }

        /// <summary>
        /// Für die Prüfung ohne Bildschirm (Schalter /dialogbilder): lädt ein Modell
        /// und liefert das Titelbild so, wie es an ElasPrintFlow ginge — mit dem
        /// ersten Logo als Wasserzeichen. Dieselbe Rechnung wie bei der Übernahme.
        /// </summary>
        internal BitmapSource ProbeTitle(string modelPath, string pngPath, List<string> marks)
        {
            RefreshImageFolders();
            if (_cbLogo.Items.Count > 1) _cbLogo.SelectedIndex = 1;
            SetModel(MeshLoader.Load(modelPath), modelPath);

            // Die Dateiliste mit dem Ordner des Modells füllen und die übrigen
            // Modelle anhaken — damit das Fensterbild die Häkchen zeigt.
            RefreshFolder(modelPath);
            int ticked = 0;
            foreach (FileEntry fe in _entries)
            {
                if (fe.IsImage || string.Equals(fe.FullPath, modelPath, StringComparison.OrdinalIgnoreCase)) continue;
                fe.Marked = true;
                if (++ticked == 3) break;
            }

            marks.Add("Ohne Wasserzeichen");
            for (int i = 1; i < _cbLogo.Items.Count; i++) marks.Add(Convert.ToString(_cbLogo.Items[i]));

            BitmapSource picture = RenderTitle();
            File.WriteAllBytes(pngPath, EncodePng(picture));
            return picture;
        }

        /// <summary>
        /// Für die Prüfung ohne Bildschirm (Schalter /dialogbilder): das ganze
        /// Fenster als Bild, ohne es zu zeigen — Kopfband, Leisten und Ansicht.
        /// </summary>
        internal void ProbeWindow(string pngPath)
        {
            DialogProbe.Save((FrameworkElement)Content, 1340, 820, pngPath);
        }

        /// <summary>Nach dem Anlegen: berichten, nachfragen, und erst dann lokal löschen.</summary>
        /// <param name="extraPaths">Die weiteren Dateien derselben Akte; leer, wenn es nur die eine gab.</param>
        private void FinishTransfer(string file, List<string> extraPaths, PrintFlowModel model)
        {
            string number = string.IsNullOrEmpty(model.ArticleNumber) ? "–" : model.ArticleNumber;
            DlgLink[] links = TransferLinks(model, _pfUrl);

            // Die Häkchen haben ihren Dienst getan: Die Dateien liegen in der Akte,
            // ob sie hier bleiben oder nicht. Stünden sie weiter da, gingen sie bei
            // der nächsten Übernahme ein zweites Mal mit — in eine fremde Akte.
            foreach (FileEntry fe in _entries) if (fe.Marked) fe.Marked = false;
            _marked.Clear();
            UpdateCount();

            // Angelegt oder angehängt — die Meldungen sagen, was von beidem geschah.
            bool nothingNew = model.Attached && model.ExtraNames.Count == 0;    // lag alles schon gleich dort
            string done = nothingNew ? "Lag in " + number + " schon"
                        : model.Attached ? "An " + number + " angehängt"
                                         : "Übernommen als " + number;

            if (!model.Verified)
            {
                AppDialog.Warn(this, model.Attached ? "Angehängt, aber nicht geprüft" : "Angelegt, aber nicht geprüft",
                    TransferSummary(model) + "\n\n" + model.VerifyProblem + "\n\n" +
                    "Die lokalen Dateien bleiben deshalb liegen.", links);
                return;
            }

            List<string> victims = new List<string>();
            if (File.Exists(file)) victims.Add(file);
            victims.AddRange(Sidecars(file));
            foreach (string extra in extraPaths)
            {
                if (File.Exists(extra)) victims.Add(extra);
                victims.AddRange(Sidecars(extra));
            }

            bool delete = victims.Count > 0 && AppDialog.Ask(this, DlgKind.Success,
                nothingNew ? AlreadyThereTitle : model.Attached ? AttachedTitle : DeleteTitle,
                DeleteQuestion(model, victims),
                DeleteYes, DeleteNo, true, links);
            if (!delete)
            {
                Toast(done + " — lokal behalten", true);
                return;
            }

            int index = _fileList.SelectedIndex;
            int removed = 0;
            string stuck = null;
            foreach (string p in victims)
            {
                try
                {
                    File.Delete(p);
                    removed++;
                }
                catch (Exception ex)
                {
                    stuck = Path.GetFileName(p) + "\n\n" + ex.Message;
                    break;
                }
            }

            bool gone = !File.Exists(file);
            if (gone && string.Equals(_modelFile, file, StringComparison.OrdinalIgnoreCase)) ClearModel();
            else if (_showingImage && (_currentFile == null || !File.Exists(_currentFile))) ShowModelView();
            RefreshFolder(null);
            UpdateImageToggle();

            // Nachbarn auswählen — der nächste Kandidat steht damit schon in der Ansicht.
            if (gone && _fileList.Items.Count > 0)
            {
                if (index >= _fileList.Items.Count) index = _fileList.Items.Count - 1;
                if (index < 0) index = 0;
                _fileList.SelectedIndex = index;
                _fileList.ScrollIntoView(_fileList.SelectedItem);
            }

            if (stuck != null)
            {
                AppDialog.Error(this, "Löschen fehlgeschlagen",
                    stuck + "\n\nDas Modell liegt trotzdem vollständig in ElasPrintFlow (" + number + ").");
                return;
            }
            Toast(done + " — hier gelöscht (" + removed +
                  (removed == 1 ? " Datei)" : " Dateien)"), true);
        }

        // =================================================================
        //  Einstellungen
        // =================================================================

        private static string SettingsPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elas3DView");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.txt");
        }

        private void SaveSettings()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("folder=" + (_currentFolder ?? ""));
                lines.Add("theme=" + (Theme.IsDark ? "dark" : "light"));
                lines.Add("background=" + _bgPreset);
                lines.Add("bgimage=" + (_bgImage ?? ""));
                lines.Add("finish=" + _cbFinish.SelectedIndex);
                lines.Add("color=" + ColorPickerButton.HexOf(_modelColor));
                lines.Add("width=" + _cbWidth.SelectedIndex);
                lines.Add("jpgbg=" + _cbJpgBg.SelectedIndex);
                lines.Add("kind=" + _kind);
                lines.Add("logo=" + (_logoFile ?? ""));
                lines.Add("logopos=" + _logoPos.Index);
                lines.Add("logosize=" + ((int)_logoPercent));
                lines.Add("ortho=" + (_ortho ? "1" : "0"));
                lines.Add("yaw=" + _yaw.ToString("R", CultureInfo.InvariantCulture));
                lines.Add("pitch=" + _pitch.ToString("R", CultureInfo.InvariantCulture));
                lines.Add("pfurl=" + _pfUrl);
                lines.Add("pfuser=" + _pfUser);
                lines.Add("pfsession=" + PrintFlowClient.Protect(_pfSession));
                lines.Add("pfcat=" + _pfCategory);

                string recent = "";
                foreach (Color c in ColorPickerButton.Recent)
                    recent += (recent.Length > 0 ? "," : "") + ColorPickerButton.HexOf(c);
                lines.Add("recent=" + recent);

                File.WriteAllLines(SettingsPath(), lines.ToArray());
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) return;
                foreach (string line in File.ReadAllLines(p))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq);
                    string v = line.Substring(eq + 1);
                    int iv;
                    double dv;
                    switch (k)
                    {
                        case "folder": if (Directory.Exists(v)) _currentFolder = v; break;
                        case "theme": if (v == "light" && Theme.IsDark) Theme.Apply(false); break;
                        case "background":
                            if (int.TryParse(v, out iv) && iv >= 0 && iv < BackgroundNames.Length)
                                _bgPreset = iv;
                            break;
                        case "bgimage": _bgImage = v.Length > 0 ? v : null; break;
                        case "finish":
                            if (int.TryParse(v, out iv) && iv >= 0 && iv < FinishNames.Length)
                                _cbFinish.SelectedIndex = iv;
                            break;
                        case "color":
                            _modelColor = ColorPickerButton.ParseHex(v);
                            _colorBtn.SelectedColor = _modelColor;
                            break;
                        case "width":
                            if (int.TryParse(v, out iv) && iv >= 0 && iv < WidthValues.Length)
                                _cbWidth.SelectedIndex = iv;
                            break;
                        case "jpgbg":
                            if (int.TryParse(v, out iv) && iv >= 0 && iv < JpgBgNames.Length)
                                _cbJpgBg.SelectedIndex = iv;
                            break;
                        case "kind":
                            if (int.TryParse(v, out iv) && iv >= 0 && iv <= 2) _kind = iv;
                            break;
                        case "logo": _logoFile = v.Length > 0 ? v : null; break;
                        case "logopos":
                            if (int.TryParse(v, out iv)) _logoPos.Index = iv;
                            break;
                        case "logosize":
                            if (int.TryParse(v, out iv) && iv >= 4 && iv <= 45)
                            {
                                _logoPercent = iv;
                                _sldLogo.Value = iv;
                            }
                            break;
                        case "ortho": _chkOrtho.IsChecked = v == "1"; break;
                        case "yaw":
                            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) _yaw = dv;
                            break;
                        case "pitch":
                            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) _pitch = dv;
                            break;
                        case "pfurl": if (v.Length > 0) _pfUrl = v; break;
                        case "pfuser": _pfUser = v; break;
                        case "pfsession": _pfSession = PrintFlowClient.Unprotect(v); break;
                        case "pfcat": _pfCategory = v; break;
                        case "recent":
                            ColorPickerButton.Recent.Clear();
                            foreach (string hex in v.Split(','))
                                if (hex.Length >= 6) ColorPickerButton.Recent.Add(ColorPickerButton.ParseHex(hex));
                            break;
                    }
                }
            }
            catch { }
        }
    }
}
