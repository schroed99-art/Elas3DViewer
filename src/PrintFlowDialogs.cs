using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Elas3DView
{
    /// <summary>
    /// Gehäuse der Dialoge mit Eingabefeldern — dieselbe Fläche mit Goldkante wie
    /// beim Umbenennen, nur einmal gebaut statt in jedem Dialog neu.
    /// </summary>
    public abstract class PanelDialog : Window
    {
        protected readonly StackPanel Root;
        protected readonly TextBlock Hint;

        protected PanelDialog(string title, string caption, double width)
        {
            Title = title;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            SizeToContent = SizeToContent.Height;
            Width = width;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily(Theme.UiFont);

            Border shell = new Border();
            shell.CornerRadius = new CornerRadius(12);
            shell.BorderThickness = new Thickness(1);
            shell.Margin = new Thickness(10);
            shell.SetResourceReference(Border.BackgroundProperty, "Panel");
            shell.SetResourceReference(Border.BorderBrushProperty, "Gold");
            Content = shell;

            Root = new StackPanel();
            Root.Margin = new Thickness(22, 20, 22, 20);
            shell.Child = Root;

            TextBlock head = new TextBlock();
            head.Text = title;
            head.FontSize = 16;
            head.FontWeight = FontWeights.SemiBold;
            head.Margin = new Thickness(0, 0, 0, 4);
            head.SetResourceReference(TextBlock.ForegroundProperty, "Text");
            Root.Children.Add(head);

            TextBlock sub = new TextBlock();
            sub.Text = caption ?? "";
            sub.FontSize = 12;
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(0, 0, 0, 14);
            sub.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            Root.Children.Add(sub);

            Hint = new TextBlock();
            Hint.FontSize = 12;
            Hint.TextWrapping = TextWrapping.Wrap;
            Hint.Margin = new Thickness(2, 2, 0, 0);
            Hint.SetResourceReference(TextBlock.ForegroundProperty, "Negative");
        }

        /// <summary>Beschriftung links, Bedienelement rechts — in allen Zeilen gleich breit.</summary>
        protected void AddRow(string label, FrameworkElement control)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 0, 0, 9);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock t = new TextBlock();
            t.Text = label;
            t.FontSize = 12.5;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            g.Children.Add(t);

            Grid.SetColumn(control, 1);
            g.Children.Add(control);
            Root.Children.Add(g);
        }

        protected static Button MakeButton(string text, bool primary)
        {
            Button b = new Button();
            b.Content = text;
            b.Style = (Style)Application.Current.Resources[primary ? "BtnPrimary" : "Btn"];
            b.MinWidth = 104;
            b.Padding = new Thickness(16, 8, 16, 8);
            if (primary) b.Margin = new Thickness(0);
            return b;
        }

        /// <summary>
        /// Eingabefeld in der Höhe der Auswahlfelder. Der Stil rückt den Text
        /// zweimal ein (Vorlage und Feld); hier steht er bündig mit den Auswahlen.
        /// </summary>
        protected static void Fit(Control field)
        {
            field.Height = 34;
            field.Padding = new Thickness(4, 0, 4, 0);
            field.VerticalContentAlignment = VerticalAlignment.Center;
            field.FontSize = 13.5;
        }

        protected static ComboBox MakeCombo()
        {
            ComboBox cb = new ComboBox();
            cb.Style = (Style)Application.Current.Resources["Combo"];
            cb.ItemContainerStyle = (Style)Application.Current.Resources["ComboItem"];
            cb.Height = 34;
            cb.FontSize = 13.5;
            return cb;
        }

        /// <summary>Für die Prüfung ohne Bildschirm: zeichnet den Dialog in eine Datei, ohne ihn zu zeigen.</summary>
        internal void SavePicture(string path, string hint)
        {
            if (hint != null) Hint.Text = hint;
            DialogProbe.Save((FrameworkElement)Content, Width, path);
        }

        /// <summary>Zeigt den Dialog über dem abgedunkelten Hauptfenster.</summary>
        public bool ShowOver(MainWindow owner)
        {
            Owner = owner;
            owner.SetScrim(true);
            try { return ShowDialog() == true; }
            finally { owner.SetScrim(false); }
        }
    }

    /// <summary>
    /// Einmalige Anmeldung bei ElasPrintFlow. Angemeldet wird im Dialog selbst,
    /// damit ein falsches Passwort dort steht, wo man es korrigiert. Heraus kommt
    /// ein angemeldeter Client; das Passwort verlässt dieses Fenster nicht.
    /// </summary>
    public class PrintFlowLoginDialog : PanelDialog
    {
        private readonly TextBox _url, _user;
        private readonly PasswordBox _pass;
        private readonly Button _ok;
        private CancellationTokenSource _cts;
        private bool _closed;

        public PrintFlowClient Client;
        public string User;

        public PrintFlowLoginDialog(string url, string user, string reason)
            : base("Mit ElasPrintFlow verbinden",
                   reason ?? "Einmal anmelden. Gemerkt wird nur die Sitzung, nie das Passwort.", 480)
        {
            _url = new TextBox();
            _url.Text = string.IsNullOrEmpty(url) ? PrintFlowClient.DefaultUrl : url;
            _url.Style = (Style)Application.Current.Resources["Txt"];
            Fit(_url);
            AddRow("Adresse", _url);

            _user = new TextBox();
            _user.Text = user ?? "";
            _user.Style = (Style)Application.Current.Resources["Txt"];
            Fit(_user);
            AddRow("Benutzername", _user);

            _pass = new PasswordBox();
            _pass.Style = (Style)Application.Current.Resources["Pwd"];
            Fit(_pass);
            AddRow("Passwort", _pass);

            Root.Children.Add(Hint);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);

            Button cancel = MakeButton("Abbrechen", false);
            cancel.IsCancel = true;
            buttons.Children.Add(cancel);

            _ok = MakeButton("Anmelden", true);
            _ok.IsDefault = true;
            _ok.Click += delegate { Accept(); };
            buttons.Children.Add(_ok);
            Root.Children.Add(buttons);

            Loaded += delegate
            {
                if (_user.Text.Length == 0) _user.Focus();
                else _pass.Focus();
            };
            Closed += delegate
            {
                _closed = true;
                if (_cts != null) _cts.Cancel();
            };
        }

        private void Problem(string text, Control focus)
        {
            Hint.Text = text;
            if (focus != null) focus.Focus();
        }

        private async void Accept()
        {
            if (_cts != null) return;

            PrintFlowClient client;
            try { client = new PrintFlowClient(_url.Text, ""); }
            catch (PrintFlowException ex) { Problem(ex.Message, _url); return; }

            string user = _user.Text.Trim();
            string pass = _pass.Password;
            if (user.Length == 0) { client.Dispose(); Problem("Bitte den Benutzernamen angeben.", _user); return; }
            if (pass.Length == 0) { client.Dispose(); Problem("Bitte das Passwort angeben.", _pass); return; }

            Hint.Text = "";
            _ok.IsEnabled = false;
            _ok.Content = "Anmelden …";
            _cts = new CancellationTokenSource();

            string problem = null;
            try { await client.Login(user, pass, _cts.Token); }
            catch (OperationCanceledException) { problem = ""; }
            catch (Exception ex) { problem = ex.Message; }

            _cts.Dispose();
            _cts = null;
            if (_closed) { client.Dispose(); return; }

            _ok.IsEnabled = true;
            _ok.Content = "Anmelden";
            if (problem != null)
            {
                client.Dispose();
                _pass.Clear();
                Problem(problem, _pass);
                return;
            }

            _pass.Clear();
            Client = client;
            User = user;
            DialogResult = true;
        }
    }

    /// <summary>
    /// Die Übernahme eines Modells: Name, Kategorie und ob das Titelbild ein
    /// Wasserzeichen trägt. Das Bild im Dialog ist genau das, was hinausgeht.
    /// </summary>
    public class PrintFlowDialog : PanelDialog
    {
        private readonly TextBox _name;
        private readonly ComboBox _category, _mark;
        private readonly Image _image;
        private readonly TextBlock _imageInfo;
        private readonly Func<int, BitmapSource> _render;

        public string ModelName;
        public string Category;
        public BitmapSource Picture;

        /// <summary>Der Nutzer will Server oder Benutzer wechseln, statt zu übernehmen.</summary>
        public bool ChangeConnection;

        /// <summary>
        /// Der Nutzer will kein neues Modell anlegen, sondern in den vorhandenen
        /// suchen und die Datei an eine bestehende Akte anhängen.
        /// </summary>
        public bool SearchExisting;

        /// <param name="marks">Auswahl der Wasserzeichen; Eintrag 0 heißt „ohne".</param>
        /// <param name="render">Zeichnet das Titelbild mit dem Wasserzeichen dieses Eintrags neu.</param>
        /// <param name="fileRows">
        /// Nur wenn mehrere Dateien in die Akte gehen: je Zeile Name und Größe,
        /// die Hauptdatei zuerst. Null, wenn das Modell aus einer Datei besteht.
        /// </param>
        public PrintFlowDialog(string caption, string connection, string name,
                               List<string> categories, string category,
                               List<string> marks, int mark,
                               BitmapSource picture, Func<int, BitmapSource> render,
                               IList<string[]> fileRows = null)
            : base("Ins PrintFlow übernehmen", caption, 580)
        {
            _render = render;
            ModelName = name;
            Category = category;
            Picture = picture;

            // ---- Titelbild ----
            Border frame = new Border();
            frame.CornerRadius = new CornerRadius(9);
            frame.BorderThickness = new Thickness(1);
            frame.Height = 250;
            frame.Padding = new Thickness(6);
            frame.SetResourceReference(Border.BackgroundProperty, "Bg");
            frame.SetResourceReference(Border.BorderBrushProperty, "Border");

            _image = new Image();
            _image.Stretch = Stretch.Uniform;
            _image.Source = picture;
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
            frame.Child = _image;
            Root.Children.Add(frame);

            _imageInfo = new TextBlock();
            _imageInfo.FontSize = 11.5;
            _imageInfo.Margin = new Thickness(2, 6, 0, 14);
            _imageInfo.SetResourceReference(TextBlock.ForegroundProperty, "TextWeak");
            Root.Children.Add(_imageInfo);
            ShowImageInfo();

            // ---- Dateien dieser Akte ----
            if (fileRows != null && fileRows.Count > 1) AddFileList(fileRows);

            // ---- Angaben ----
            _name = new TextBox();
            _name.Text = name;
            _name.Style = (Style)Application.Current.Resources["Txt"];
            _name.TextChanged += delegate { Hint.Text = Problem() ?? ""; };
            Fit(_name);
            AddRow("Name", _name);

            _category = MakeCombo();
            int select = -1, unsorted = -1;
            for (int i = 0; i < categories.Count; i++)
            {
                _category.Items.Add(categories[i]);
                if (string.Equals(categories[i], category, StringComparison.OrdinalIgnoreCase)) select = i;
                if (string.Equals(categories[i], "Unsortiert", StringComparison.OrdinalIgnoreCase)) unsorted = i;
            }
            _category.SelectedIndex = select >= 0 ? select : (unsorted >= 0 ? unsorted : 0);
            AddRow("Kategorie", _category);

            _mark = MakeCombo();
            foreach (string m in marks) _mark.Items.Add(m);
            _mark.SelectedIndex = mark >= 0 && mark < marks.Count ? mark : 0;
            _mark.ToolTip = "Logo, das als Wasserzeichen im Titelbild liegt";
            _mark.SelectionChanged += delegate { Redraw(); };
            AddRow("Wasserzeichen", _mark);

            Root.Children.Add(Hint);

            // ---- Fußzeile ----
            DockPanel foot = new DockPanel();
            foot.Margin = new Thickness(0, 18, 0, 0);
            foot.LastChildFill = false;

            Button conn = new Button();
            conn.Content = "Verbindung …";
            conn.Style = (Style)Application.Current.Resources["BtnChip"];
            conn.VerticalAlignment = VerticalAlignment.Center;
            conn.ToolTip = connection + "\nServer oder Benutzer wechseln";
            conn.Click += delegate { Keep(); ChangeConnection = true; DialogResult = false; };
            DockPanel.SetDock(conn, Dock.Left);
            foot.Children.Add(conn);

            // Gibt es das Modell dort vielleicht schon — unter anderem Namen? Dann
            // gehört die Datei in seine Akte, nicht in eine neue.
            Button existing = new Button();
            existing.Content = "An bestehende Akte …";
            existing.Style = (Style)Application.Current.Resources["BtnChip"];
            existing.VerticalAlignment = VerticalAlignment.Center;
            existing.ToolTip = "In den Modellen von ElasPrintFlow suchen, mit Bild vergleichen und\n" +
                               "die Datei an eine vorhandene Akte anhängen — statt ein neues Modell anzulegen";
            existing.Click += delegate { Keep(); SearchExisting = true; DialogResult = false; };
            DockPanel.SetDock(existing, Dock.Left);
            foot.Children.Add(existing);

            Button ok = MakeButton("Übernehmen", true);
            ok.IsDefault = true;
            ok.Click += delegate { Accept(); };
            DockPanel.SetDock(ok, Dock.Right);
            foot.Children.Add(ok);

            Button cancel = MakeButton("Abbrechen", false);
            cancel.IsCancel = true;
            DockPanel.SetDock(cancel, Dock.Right);
            foot.Children.Add(cancel);

            Root.Children.Add(foot);

            Loaded += delegate
            {
                _name.Focus();
                _name.SelectAll();
            };
        }

        /// <summary>
        /// Was alles in diese EINE Akte geht (Tristan, 14.11.2026): die Hauptdatei
        /// — das geöffnete Modell — und die in der Dateiliste angehakten. Nur zum
        /// Lesen: Geändert wird die Auswahl dort, wo sie getroffen wurde, an den
        /// Häkchen. Die Hauptdatei lässt sich später in der Modellakte wechseln.
        /// </summary>
        private void AddFileList(IList<string[]> rows)
        {
            TextBlock head = new TextBlock();
            head.Text = "DATEIEN DIESER AKTE  ·  " + rows.Count;
            head.FontSize = 10.5;
            head.FontWeight = FontWeights.SemiBold;
            head.Margin = new Thickness(2, 0, 0, 6);
            head.SetResourceReference(TextBlock.ForegroundProperty, "GoldText");
            Root.Children.Add(head);

            StackPanel list = new StackPanel();
            for (int i = 0; i < rows.Count; i++)
            {
                DockPanel row = new DockPanel();
                row.Margin = new Thickness(0, i == 0 ? 0 : 5, 0, 0);

                TextBlock size = new TextBlock();
                size.Text = rows[i].Length > 1 ? rows[i][1] : "";
                size.FontSize = 11.5;
                size.Margin = new Thickness(12, 0, 0, 0);
                size.VerticalAlignment = VerticalAlignment.Center;
                size.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                DockPanel.SetDock(size, Dock.Right);
                row.Children.Add(size);

                TextBlock role = new TextBlock();
                role.Text = i == 0 ? "Hauptdatei" : "dazu";
                role.Width = 76;
                role.FontSize = 11.5;
                role.VerticalAlignment = VerticalAlignment.Center;
                role.SetResourceReference(TextBlock.ForegroundProperty, i == 0 ? "GoldText" : "TextWeak");
                DockPanel.SetDock(role, Dock.Left);
                row.Children.Add(role);

                TextBlock name = new TextBlock();
                name.Text = rows[i][0];
                name.FontSize = 12.5;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.VerticalAlignment = VerticalAlignment.Center;
                name.ToolTip = rows[i][0];
                name.SetResourceReference(TextBlock.ForegroundProperty, "TextDim");
                row.Children.Add(name);

                list.Children.Add(row);
            }

            ScrollViewer scroll = new ScrollViewer();
            scroll.MaxHeight = 112;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Focusable = false;
            scroll.Content = list;

            Border box = new Border();
            box.CornerRadius = new CornerRadius(9);
            box.BorderThickness = new Thickness(1);
            box.Padding = new Thickness(11, 9, 11, 9);
            box.SetResourceReference(Border.BackgroundProperty, "Bg");
            box.SetResourceReference(Border.BorderBrushProperty, "Border");
            box.Child = scroll;
            Root.Children.Add(box);

            TextBlock note = new TextBlock();
            note.Text = "Die weiteren Dateien liegen danach im Ordner dieses Modells — ohne eigene Artikelnummer. " +
                        "Jede bekommt ein Vorschaubild; in ElasPrintFlow stehen sie unter „Quelldateien & Varianten“.";
            note.FontSize = 11.5;
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(2, 6, 0, 14);
            note.SetResourceReference(TextBlock.ForegroundProperty, "TextWeak");
            Root.Children.Add(note);
        }

        private void ShowImageInfo()
        {
            _imageInfo.Text = Picture == null
                ? "Kein Titelbild"
                : "Titelbild  ·  " + Picture.PixelWidth + " × " + Picture.PixelHeight +
                  " px  ·  so, wie die Ansicht gerade steht";
        }

        private void Redraw()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                Picture = _render(_mark.SelectedIndex);
                _image.Source = Picture;
                ShowImageInfo();
            }
            catch (Exception ex)
            {
                Hint.Text = "Das Titelbild ließ sich nicht neu zeichnen: " + ex.Message;
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private string Problem()
        {
            string t = _name.Text.Trim();
            if (t.Length == 0) return "Der Name darf nicht leer sein.";
            if (t.EndsWith(".")) return "Der Name darf nicht mit einem Punkt enden.";
            foreach (char c in Path.GetInvalidFileNameChars())
                if (t.IndexOf(c) >= 0)
                    return "Unzulässiges Zeichen im Namen: " + (c < 32 ? "Steuerzeichen" : c.ToString()) +
                           "  — aus dem Namen wird auf der NAS ein Ordner.";
            if (t.Length > 120) return "Der Name ist zu lang.";
            return null;
        }

        /// <summary>Hält fest, was eingetragen ist — auch wenn der Dialog nur kurz weicht.</summary>
        private void Keep()
        {
            ModelName = _name.Text.Trim();
            Category = _category.SelectedItem as string ?? Category;
        }

        private void Accept()
        {
            string p = Problem();
            if (p != null) { Hint.Text = p; _name.Focus(); return; }
            Keep();
            DialogResult = true;
        }
    }

    /// <summary>
    /// In den Modellen von ElasPrintFlow suchen und eines wählen, an dessen Akte
    /// die Datei geht (Tristan, 15.11.2026: „in den Modellen suchen, dort ein Bild
    /// anzeigen und dann die Chance haben, das Modell dem gewählten hinzuzufügen").
    ///
    /// Oben stehen zwei Bilder nebeneinander — links das eigene, rechts das der
    /// gewählten Akte. Ob es dasselbe Modell ist, sagt kein Name; man sieht es.
    ///
    /// Derselbe Dialog fängt auch den vergebenen Namen ab: Dann steht die Akte
    /// dieses Namens schon gewählt da, und der Weg zurück heißt „Anderer Name".
    ///
    /// Die Eingabetaste sucht. Angehängt wird nur mit dem Knopf oder einem
    /// Doppelklick auf den Treffer — nie nebenbei.
    /// </summary>
    public class PrintFlowSearchDialog : PanelDialog
    {
        private readonly Func<string, CancellationToken, Task<PrintFlowSearch>> _search;
        private readonly Func<PrintFlowHit, CancellationToken, Task<byte[]>> _picture;
        private readonly PrintFlowHit _preset;

        private readonly TextBox _query;
        private readonly ListBox _list;
        private readonly Image _theirs;
        private readonly TextBlock _theirsInfo, _theirsEmpty, _state;
        private readonly Button _ok;

        private readonly Dictionary<int, BitmapSource> _pictures = new Dictionary<int, BitmapSource>();
        private readonly Dictionary<int, Image> _thumbs = new Dictionary<int, Image>();
        private CancellationTokenSource _cts;
        private bool _closed;

        /// <summary>Die Akte, an die angehängt werden soll; null, solange der Nutzer zurück will.</summary>
        public PrintFlowHit Chosen;

        /// <param name="mine">Das eigene Modell, so wie es gerade in der Ansicht steht.</param>
        /// <param name="backText">Wie der Weg zurück heißt: „Zurück" oder „Anderer Name".</param>
        /// <param name="preset">Eine Akte, die schon feststeht (der vergebene Name); sonst null.</param>
        public PrintFlowSearchDialog(string caption, string query, BitmapSource mine, string mineInfo,
                                     string backText, PrintFlowHit preset,
                                     Func<string, CancellationToken, Task<PrintFlowSearch>> search,
                                     Func<PrintFlowHit, CancellationToken, Task<byte[]>> picture)
            : base("An bestehende Akte anhängen", caption, 800)
        {
            _search = search;
            _picture = picture;
            _preset = preset;

            // ---- hier und dort ----
            Grid pair = new Grid();
            pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Image own;
            TextBlock ownInfo, ownEmpty;
            pair.Children.Add(Side("HIER", out own, out ownInfo, out ownEmpty));
            own.Source = mine;
            ownInfo.Text = mineInfo ?? "";

            FrameworkElement right = Side("IN ELASPRINTFLOW", out _theirs, out _theirsInfo, out _theirsEmpty);
            Grid.SetColumn(right, 2);
            pair.Children.Add(right);
            Root.Children.Add(pair);

            // ---- Suche ----
            DockPanel ask = new DockPanel();
            ask.Margin = new Thickness(0, 14, 0, 6);

            Button go = MakeButton("Suchen", false);
            go.MinWidth = 92;
            go.Margin = new Thickness(8, 0, 0, 0);
            go.Click += delegate { RunSearch(false); };
            DockPanel.SetDock(go, Dock.Right);
            ask.Children.Add(go);

            _query = new TextBox();
            _query.Text = query ?? "";
            _query.Style = (Style)Application.Current.Resources["Txt"];
            _query.ToolTip = "Name oder Artikelnummer — ein Teil genügt";
            Fit(_query);
            _query.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                RunSearch(false);
                e.Handled = true;
            };
            ask.Children.Add(_query);
            Root.Children.Add(ask);

            _state = new TextBlock();
            _state.FontSize = 11.5;
            _state.Margin = new Thickness(2, 0, 0, 6);
            _state.SetResourceReference(TextBlock.ForegroundProperty, "TextWeak");
            Root.Children.Add(_state);

            // ---- Treffer ----
            _list = new ListBox();
            _list.Style = (Style)Application.Current.Resources["List"];
            _list.Height = 250;         // vier Treffer ohne Blättern
            _list.SelectionChanged += delegate { ShowSelected(); };
            _list.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                // Nur der Doppelklick AUF einen Treffer — nicht der auf die Bildlaufleiste.
                DependencyObject d = e.OriginalSource as DependencyObject;
                while (d is Visual && !(d is ListBoxItem) && !(d is ListBox))
                    d = VisualTreeHelper.GetParent(d);
                if (d is ListBoxItem) Accept();
            };

            Border box = new Border();
            box.CornerRadius = new CornerRadius(9);
            box.BorderThickness = new Thickness(1);
            box.Padding = new Thickness(4);
            box.SetResourceReference(Border.BackgroundProperty, "Bg");
            box.SetResourceReference(Border.BorderBrushProperty, "Border");
            box.Child = _list;
            Root.Children.Add(box);

            Root.Children.Add(Hint);

            // ---- Fußzeile ----
            DockPanel foot = new DockPanel();
            foot.Margin = new Thickness(0, 16, 0, 0);
            foot.LastChildFill = false;

            TextBlock note = new TextBlock();
            note.Text = "Die Datei liegt danach im Ordner der Akte — ohne eigene Artikelnummer.\n" +
                        "Titelbild, Name und Hauptdatei der Akte bleiben, wie sie sind.";
            note.FontSize = 11.5;
            note.VerticalAlignment = VerticalAlignment.Center;
            note.SetResourceReference(TextBlock.ForegroundProperty, "TextWeak");
            DockPanel.SetDock(note, Dock.Left);
            foot.Children.Add(note);

            _ok = MakeButton("Diesem Modell hinzufügen", true);
            _ok.IsEnabled = false;
            _ok.Click += delegate { Accept(); };
            DockPanel.SetDock(_ok, Dock.Right);
            foot.Children.Add(_ok);

            Button back = MakeButton(backText, false);
            back.IsCancel = true;
            DockPanel.SetDock(back, Dock.Right);
            foot.Children.Add(back);
            Root.Children.Add(foot);

            ShowSelected();

            Loaded += delegate
            {
                _query.Focus();
                _query.SelectAll();
                if (_preset != null) ShowPreset();
                else RunSearch(true);
            };
            Closed += delegate
            {
                _closed = true;
                if (_cts != null) _cts.Cancel();
            };
        }

        /// <summary>Eine Bildfläche mit Überschrift und einer Zeile darunter.</summary>
        private static FrameworkElement Side(string label, out Image image, out TextBlock info, out TextBlock empty)
        {
            StackPanel side = new StackPanel();

            TextBlock head = new TextBlock();
            head.Text = label;
            head.FontSize = 10.5;
            head.FontWeight = FontWeights.SemiBold;
            head.Margin = new Thickness(2, 0, 0, 6);
            head.SetResourceReference(TextBlock.ForegroundProperty, "GoldText");
            side.Children.Add(head);

            Border frame = new Border();
            frame.CornerRadius = new CornerRadius(9);
            frame.BorderThickness = new Thickness(1);
            frame.Height = 186;
            frame.Padding = new Thickness(6);
            frame.SetResourceReference(Border.BackgroundProperty, "Bg");
            frame.SetResourceReference(Border.BorderBrushProperty, "Border");

            Grid inner = new Grid();
            image = new Image();
            image.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            inner.Children.Add(image);

            empty = new TextBlock();
            empty.FontSize = 12;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SetResourceReference(TextBlock.ForegroundProperty, "TextWeak");
            inner.Children.Add(empty);

            frame.Child = inner;
            side.Children.Add(frame);

            info = new TextBlock();
            info.FontSize = 12;
            info.Margin = new Thickness(2, 6, 0, 0);
            info.TextTrimming = TextTrimming.CharacterEllipsis;
            info.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            side.Children.Add(info);
            return side;
        }

        private PrintFlowHit Selected()
        {
            ListBoxItem item = _list.SelectedItem as ListBoxItem;
            return item == null ? null : item.Tag as PrintFlowHit;
        }

        /// <summary>Zeigt rechts oben, was in der Liste gewählt ist.</summary>
        private void ShowSelected()
        {
            PrintFlowHit hit = Selected();
            _ok.IsEnabled = hit != null;
            if (hit == null)
            {
                _theirs.Source = null;
                _theirsInfo.Text = "";
                _theirsEmpty.Text = "Einen Treffer wählen";
                return;
            }

            BitmapSource bmp;
            _pictures.TryGetValue(hit.Id, out bmp);
            _theirs.Source = bmp;
            _theirsEmpty.Text = bmp != null ? ""
                              : hit.PreviewPath == null ? "Diese Akte hat kein Titelbild"
                                                        : "Bild wird geladen …";
            _theirsInfo.Text = hit.Name + "   ·   " + Number(hit) + "   ·   " + hit.Category;
        }

        private static string Number(PrintFlowHit hit)
        {
            return string.IsNullOrEmpty(hit.ArticleNumber) ? "ohne Artikelnummer" : hit.ArticleNumber;
        }

        private ListBoxItem RowFor(PrintFlowHit hit)
        {
            DockPanel row = new DockPanel();

            Border frame = new Border();
            frame.Width = 62;
            frame.Height = 46;
            frame.CornerRadius = new CornerRadius(6);
            frame.Margin = new Thickness(0, 0, 12, 0);
            frame.SetResourceReference(Border.BackgroundProperty, "Panel");
            Image thumb = new Image();
            thumb.Stretch = Stretch.Uniform;
            thumb.Margin = new Thickness(2);
            RenderOptions.SetBitmapScalingMode(thumb, BitmapScalingMode.HighQuality);
            BitmapSource known;
            if (_pictures.TryGetValue(hit.Id, out known)) thumb.Source = known;
            frame.Child = thumb;
            _thumbs[hit.Id] = thumb;
            DockPanel.SetDock(frame, Dock.Left);
            row.Children.Add(frame);

            StackPanel words = new StackPanel();
            words.VerticalAlignment = VerticalAlignment.Center;

            TextBlock name = new TextBlock();       // ohne eigene Farbe: Die Zeile färbt, was gewählt ist
            name.Text = hit.Name;
            name.FontSize = 13.5;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            words.Children.Add(name);

            TextBlock sub = new TextBlock();
            sub.Text = Number(hit) + "   ·   " + hit.Category;
            sub.FontSize = 11.5;
            sub.FontWeight = FontWeights.Normal;
            sub.Margin = new Thickness(0, 2, 0, 0);
            sub.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            words.Children.Add(sub);
            row.Children.Add(words);

            ListBoxItem item = new ListBoxItem();
            item.Style = (Style)Application.Current.Resources["ListItem"];
            item.Content = row;
            item.Tag = hit;
            item.ToolTip = hit.Name;
            return item;
        }

        private void Fill(List<PrintFlowHit> hits, bool more)
        {
            _list.Items.Clear();
            _thumbs.Clear();
            foreach (PrintFlowHit h in hits) _list.Items.Add(RowFor(h));

            _state.Text = hits.Count == 0
                ? "Nichts gefunden."
                : (hits.Count == 1 ? "1 Treffer" : hits.Count + " Treffer") +
                  (more ? "   ·   ElasPrintFlow zeigt höchstens " + PrintFlowClient.SearchLimit +
                          " — das Suchwort genauer fassen" : "");
            if (hits.Count > 0) _list.SelectedIndex = 0;
            ShowSelected();
        }

        /// <summary>Der vergebene Name: Die Akte steht fest und ist schon gewählt.</summary>
        private async void ShowPreset()
        {
            List<PrintFlowHit> one = new List<PrintFlowHit>();
            one.Add(_preset);
            Fill(one, false);
            _state.Text = "Die Akte mit diesem Namen — oder nach einer anderen suchen";

            CancellationTokenSource mine = new CancellationTokenSource();
            _cts = mine;
            await LoadPictures(one, mine);
        }

        private async void RunSearch(bool first)
        {
            string q = _query.Text.Trim();
            Hint.Text = "";
            if (q.Length < 2)
            {
                _state.Text = "Bitte mindestens zwei Zeichen eingeben.";
                return;
            }

            if (_cts != null) _cts.Cancel();
            CancellationTokenSource mine = new CancellationTokenSource();
            _cts = mine;
            _state.Text = "Suche …";

            PrintFlowSearch found = null;
            string problem = null, shorter = null;
            try
            {
                found = await _search(q, mine.Token);

                // Der ganze Dateiname trifft selten einen Modellnamen. Beim ersten
                // Öffnen deshalb noch ein Versuch mit dem ersten Wort — der Nutzer
                // soll Kandidaten sehen, nicht erst tippen müssen.
                if (first && found.Hits.Count == 0)
                {
                    shorter = FirstWord(q);
                    if (shorter != null)
                    {
                        PrintFlowSearch wider = await _search(shorter, mine.Token);
                        if (wider.Hits.Count > 0) found = wider;
                        else shorter = null;
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { problem = ex.Message; }

            if (_closed || _cts != mine) return;
            if (problem != null)
            {
                _state.Text = "";
                Hint.Text = problem;
                return;
            }
            if (shorter != null) _query.Text = shorter;

            Fill(found.Hits, found.More);
            await LoadPictures(found.Hits, mine);
        }

        /// <summary>Das erste Wort eines Namens, wenn es zum Suchen taugt und nicht schon alles ist.</summary>
        private static string FirstWord(string name)
        {
            string[] parts = name.Split(new char[] { ' ', '_', '-', '.', '(' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts[0].Length < 3) return null;
            return parts[0];
        }

        /// <summary>
        /// Holt die Titelbilder der Treffer nach, eines nach dem anderen. Die Liste
        /// steht schon da; ein Bild, das nicht kommt, fehlt eben.
        /// </summary>
        private async Task LoadPictures(List<PrintFlowHit> hits, CancellationTokenSource mine)
        {
            foreach (PrintFlowHit hit in hits)
            {
                if (hit.PreviewPath == null || _pictures.ContainsKey(hit.Id)) continue;

                byte[] data = null;
                try { data = await _picture(hit, mine.Token); }
                catch (OperationCanceledException) { return; }
                catch (Exception) { data = null; }
                if (_closed || _cts != mine) return;

                BitmapSource bmp = Decode(data);
                if (bmp == null)
                {
                    // Kein Bild zu bekommen: nicht ewig „wird geladen" stehen lassen.
                    hit.PreviewPath = null;
                    if (Selected() == hit) ShowSelected();
                    continue;
                }
                _pictures[hit.Id] = bmp;
                Image thumb;
                if (_thumbs.TryGetValue(hit.Id, out thumb)) thumb.Source = bmp;
                if (Selected() == hit) ShowSelected();
            }
        }

        private static BitmapSource Decode(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.DecodePixelWidth = 760;         // Titelbilder sind bis 1920 px breit; hier reicht die Fläche
                img.StreamSource = new MemoryStream(data);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        private void Accept()
        {
            PrintFlowHit hit = Selected();
            if (hit == null) return;
            Chosen = hit;
            DialogResult = true;
        }

        /// <summary>Für die Prüfung ohne Bildschirm: Treffer und Bilder, als hätte ElasPrintFlow geantwortet.</summary>
        internal void ProbeFill(List<PrintFlowHit> hits, BitmapSource sample, bool more)
        {
            foreach (PrintFlowHit h in hits)
                if (h.PreviewPath != null) _pictures[h.Id] = sample;
            Fill(hits, more);
        }
    }

    /// <summary>
    /// Prüfung ohne Bildschirm, wie /vorschau im Setup:
    ///
    ///     Elas3DView.exe /dialogbilder Modelldatei Zielordner
    ///
    /// lädt das Modell, zeichnet das Titelbild genau so, wie es an ElasPrintFlow
    /// ginge, und legt es mit den Dialogen der Übernahme als PNG in den
    /// Zielordner. Nichts erscheint auf dem Bildschirm, nichts geht ins Netz.
    /// </summary>
    internal static class DialogProbe
    {
        public static void Run(string modelPath, string dir)
        {
            Directory.CreateDirectory(dir);
            try
            {
                MainWindow w = new MainWindow();
                List<string> marks = new List<string>();
                BitmapSource picture = w.ProbeTitle(modelPath, Path.Combine(dir, "0-titelbild.png"), marks);

                new PrintFlowLoginDialog(null, "", null)
                    .SavePicture(Path.Combine(dir, "1-anmeldung.png"), null);
                new PrintFlowLoginDialog(PrintFlowClient.DefaultUrl, "tristan",
                                         "Die Anmeldung ist abgelaufen. Bitte neu anmelden.")
                    .SavePicture(Path.Combine(dir, "2-anmeldung-abgewiesen.png"), "Ungültige Anmeldedaten.");

                List<string> categories = new List<string>(new string[]
                    { "Dekoration", "Figuren", "Vasen", "Werkzeuge", "Unsortiert" });
                string file = Path.GetFileName(modelPath);
                new PrintFlowDialog(file + "  ·  1,7 MB   →   " + PrintFlowClient.DefaultUrl, "",
                                    Path.GetFileNameWithoutExtension(modelPath), categories, "Vasen",
                                    marks, marks.Count > 1 ? 1 : 0, picture, null)
                    .SavePicture(Path.Combine(dir, "3-uebernahme.png"), null);

                // Dieselbe Übernahme mit weiteren Dateien für die Akte.
                string stem = Path.GetFileNameWithoutExtension(modelPath);
                List<string[]> rows = new List<string[]>();
                rows.Add(new string[] { file, "1,7 MB" });
                rows.Add(new string[] { stem + " (1).3mf", "2,3 MB" });
                rows.Add(new string[] { stem + " (2).3mf", "2,1 MB" });
                rows.Add(new string[] { stem + " ohne Boden.stl", "940 kB" });
                new PrintFlowDialog(file + " und 3 weitere Dateien  ·  7,0 MB   →   " + PrintFlowClient.DefaultUrl, "",
                                    stem, categories, "Vasen",
                                    marks, marks.Count > 1 ? 1 : 0, picture, null, rows)
                    .SavePicture(Path.Combine(dir, "3b-uebernahme-mehrere.png"), null);

                PrintFlowModel model = new PrintFlowModel();
                model.Id = 2743;
                model.Name = Path.GetFileNameWithoutExtension(modelPath);
                model.Category = "Vasen";
                model.ArticleNumber = "VA-0000024";
                model.NasPath = "//192.168.178.95/datenaustausch/1.Elas3DStudio/Modelle/Vasen/" +
                                model.Name + "/" + model.Name + ".stl";
                List<string> victims = new List<string>();
                victims.Add(file);
                victims.Add(model.Name + ".jpg");
                AppDialog.SavePicture(Path.Combine(dir, "4-loeschfrage.png"), DlgKind.Success,
                    MainWindow.DeleteTitle, MainWindow.DeleteQuestion(model, victims),
                    MainWindow.DeleteYes, MainWindow.DeleteNo, true,
                    MainWindow.TransferLinks(model, PrintFlowClient.DefaultUrl));

                // Die Löschfrage, wenn mehrere Dateien in die Akte gegangen sind.
                model.ExtraNames.Add(stem + " (1).3mf");
                model.ExtraNames.Add(stem + " (2).3mf");
                victims.Add(stem + " (1).3mf");
                victims.Add(stem + " (2).3mf");
                victims.Add(stem + " (2).jpg");
                AppDialog.SavePicture(Path.Combine(dir, "4b-loeschfrage-mehrere.png"), DlgKind.Success,
                    MainWindow.DeleteTitle, MainWindow.DeleteQuestion(model, victims),
                    MainWindow.DeleteYes, MainWindow.DeleteNo, true,
                    MainWindow.TransferLinks(model, PrintFlowClient.DefaultUrl));

                // An eine bestehende Akte anhängen: die Suche mit Treffern und Bild,
                // der vergebene Name, und die Rückfrage danach.
                List<PrintFlowHit> hits = new List<PrintFlowHit>();
                string[][] sample =
                {
                    new string[] { stem, "Vasen", "VA-0000024", "bild" },
                    new string[] { stem + " klein", "Vasen", "VA-0000031", "bild" },
                    new string[] { stem + " mit Deckel", "Dekoration", "DE-0000112", null },
                    new string[] { "Übertopf " + stem, "Vasen", "VA-0000007", "bild" },
                    new string[] { stem + " Set", "Sonstiges", "", "bild" }
                };
                for (int i = 0; i < sample.Length; i++)
                {
                    PrintFlowHit h = new PrintFlowHit();
                    h.Id = 9000 + i;
                    h.Name = sample[i][0];
                    h.Category = sample[i][1];
                    h.ArticleNumber = sample[i][2];
                    h.PreviewPath = sample[i][3];
                    hits.Add(h);
                }
                PrintFlowSearchDialog find = new PrintFlowSearchDialog(
                    file + "  ·  1,7 MB   —   in ElasPrintFlow nachsehen, ob es das Modell schon gibt, und dort anhängen.",
                    stem, picture, file, "Zurück", null, null, null);
                find.ProbeFill(hits, picture, false);
                find.SavePicture(Path.Combine(dir, "3c-anhaengen-suche.png"), null);

                PrintFlowSearchDialog taken = new PrintFlowSearchDialog(
                    "„" + stem + "“ gibt es in der Kategorie „Vasen“ schon. An diese Akte anhängen — oder zurück " +
                    "und dem neuen Modell einen anderen Namen geben.",
                    stem, picture, file, "Anderer Name", hits[0], null, null);
                taken.ProbeFill(hits.GetRange(0, 1), picture, false);
                taken.SavePicture(Path.Combine(dir, "3d-anhaengen-name-vergeben.png"), null);

                PrintFlowModel joined = new PrintFlowModel();
                joined.Attached = true;
                joined.Id = 2743;
                joined.Name = stem;
                joined.Category = "Vasen";
                joined.ArticleNumber = "VA-0000024";
                joined.NasPath = model.NasPath;
                joined.ExtraNames.Add(stem + " (2).stl");
                joined.ExtraNames.Add(stem + " ohne Boden.stl");
                joined.AlreadyThere.Add(stem + " (1).3mf");
                List<string> gone = new List<string>();
                gone.Add(file);
                gone.Add(stem + ".jpg");
                gone.Add(stem + " ohne Boden.stl");
                gone.Add(stem + " (1).3mf");
                AppDialog.SavePicture(Path.Combine(dir, "4c-loeschfrage-angehaengt.png"), DlgKind.Success,
                    MainWindow.AttachedTitle, MainWindow.DeleteQuestion(joined, gone),
                    MainWindow.DeleteYes, MainWindow.DeleteNo, true,
                    MainWindow.TransferLinks(joined, PrintFlowClient.DefaultUrl));

                joined.ExtraNames.Clear();
                AppDialog.SavePicture(Path.Combine(dir, "4d-loeschfrage-lag-schon-dort.png"), DlgKind.Success,
                    MainWindow.AlreadyThereTitle, MainWindow.DeleteQuestion(joined, gone.GetRange(3, 1)),
                    MainWindow.DeleteYes, MainWindow.DeleteNo, true,
                    MainWindow.TransferLinks(joined, PrintFlowClient.DefaultUrl));

                new ReleaseNotesDialog().SavePicture(Path.Combine(dir, "7-neuerungen.png"), null);

                // Das Hauptfenster selbst, in beiden Designs — für Kopfband und Leisten.
                // Für das helle ein ZWEITES Fenster: Ein nie gezeigtes Fenster lässt
                // sich nur einmal verlässlich vermessen. Nach dem Designwechsel
                // rechnet es seinen Inhalt mit eigenen Maßen neu, und das Bild
                // käme breiter und flacher heraus, als das Fenster ist.
                w.ProbeWindow(Path.Combine(dir, "5-fenster.png"));

                // Die Bilder der angehakten Dateien — nach dem Fensterbild, weil
                // dafür jede einmal in die Ansicht gestellt wird.
                w.ProbePictograms(dir);

                MainWindow light = new MainWindow();
                Theme.Apply(false);
                light.ProbeWindow(Path.Combine(dir, "6-fenster-hell.png"));
                Theme.Apply(true);

                // „Aktualisieren": eine Datei kommt dazu, nichts geschieht, sie verschwindet.
                File.WriteAllText(Path.Combine(dir, "9-aktualisieren.txt"),
                                  w.ProbeRefresh(dir, modelPath), new System.Text.UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(dir, "fehler.txt"), ex.ToString());
            }
        }

        public static void Save(FrameworkElement content, double width, string path)
        {
            Save(content, width, 0, path);
        }

        /// <param name="fixedHeight">
        /// Größer null: genau diese Höhe — für das Hauptfenster, dessen Inhalt
        /// sich dehnt und keine eigene Höhe hat. Sonst so hoch wie der Inhalt.
        /// </param>
        public static void Save(FrameworkElement content, double width, double fixedHeight, string path)
        {
            content.Measure(new Size(width, fixedHeight > 0 ? fixedHeight : double.PositiveInfinity));
            double height = fixedHeight > 0 ? fixedHeight : Math.Ceiling(content.DesiredSize.Height);
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                Rect all = new Rect(0, 0, width, height);
                dc.DrawRectangle((Brush)Application.Current.Resources["Bg"], null, all);
                VisualBrush picture = new VisualBrush(content);
                if (fixedHeight > 0)
                {
                    // Ohne festen Ausschnitt dehnt der Pinsel den Umriss ALLER
                    // Nachfahren auf die Fläche — ragt etwas über den Rand (das
                    // Logo in der Ansicht), gerät das ganze Bild verzerrt.
                    picture.ViewboxUnits = BrushMappingMode.Absolute;
                    picture.Viewbox = all;
                    picture.Stretch = Stretch.None;
                    picture.AlignmentX = AlignmentX.Left;
                    picture.AlignmentY = AlignmentY.Top;
                }
                dc.DrawRectangle(picture, null, all);
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }
    }
}
