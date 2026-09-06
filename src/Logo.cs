using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Elas3DView
{
    public class ImageEntry
    {
        public string Name;
        public string FullPath;
        public override string ToString() { return Name; }
    }

    /// <summary>
    /// Ein Ordner voller Bilder neben der Anwendung — einmal für die Logos, einmal
    /// für die Hintergründe. Beim ersten Start wird er angelegt und, wo vorhanden,
    /// mit den mitgelieferten Dateien gefüllt; danach gehört er dem Nutzer: wer
    /// etwas hineinlegt, findet es in der Auswahl, wer etwas löscht, ist es los.
    /// </summary>
    public class ImageStore
    {
        public static readonly ImageStore Logos =
            new ImageStore("logos", new string[] { "Elas bunt.png", "Elas schwarz-weiss.png" }, null);

        public static readonly ImageStore Backgrounds =
            new ImageStore("hintergruende", new string[0],
                "Hier Bilddateien ablegen — PNG, JPG, BMP, GIF oder TIF.\r\n\r\n" +
                "Sie erscheinen dann in der Auswahl „Hintergrund\" und liegen hinter dem\r\n" +
                "Modell. Das Bild füllt die Fläche, ohne verzerrt zu werden; was seitlich\r\n" +
                "übersteht, wird beschnitten. Am günstigsten ist ein querformatiges Bild,\r\n" +
                "mindestens so groß wie das erzeugte JPG.\r\n\r\n" +
                "Damit der Hintergrund auch im gespeicherten Bild landet: Bildgrund auf\r\n" +
                "„Wie Ansicht\" stellen. Für den Shop bleibt Weiß die bessere Wahl.\r\n");

        private static readonly Dictionary<string, BitmapSource> Cache =
            new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] Extensions =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

        private readonly string _folder;
        private readonly string[] _defaults;
        private readonly string _hint;

        private ImageStore(string folder, string[] defaults, string hint)
        {
            _folder = folder;
            _defaults = defaults;
            _hint = hint;
        }

        public string FolderPath()
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            string dir = Path.GetDirectoryName(exe);
            if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
            return Path.Combine(dir, _folder);
        }

        /// <summary>
        /// Geprüft wird der Ordner, nicht die einzelne Datei — sonst käme ein
        /// absichtlich gelöschtes Logo beim nächsten Start zurück.
        /// </summary>
        public void EnsureFolder()
        {
            try
            {
                string dir = FolderPath();
                if (Directory.Exists(dir)) return;
                Directory.CreateDirectory(dir);

                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string name in _defaults)
                {
                    using (Stream s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) continue;
                        using (FileStream fs = File.Create(Path.Combine(dir, name)))
                            s.CopyTo(fs);
                    }
                }

                // Ein leerer Ordner sagt nicht, wofür er da ist.
                if (_hint != null)
                    File.WriteAllText(Path.Combine(dir, "Hinweis.txt"), _hint,
                                      new System.Text.UTF8Encoding(true));
            }
            catch { }
        }

        public List<ImageEntry> List()
        {
            List<ImageEntry> found = new List<ImageEntry>();
            try
            {
                string dir = FolderPath();
                if (!Directory.Exists(dir)) return found;
                List<string> files = new List<string>();
                foreach (string f in Directory.GetFiles(dir))
                    if (IsImage(f)) files.Add(f);
                files.Sort(StringComparer.CurrentCultureIgnoreCase);
                foreach (string f in files)
                {
                    ImageEntry e = new ImageEntry();
                    e.FullPath = f;
                    e.Name = Path.GetFileNameWithoutExtension(f);
                    found.Add(e);
                }
            }
            catch { }
            return found;
        }

        public static bool IsImage(string path)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            foreach (string x in Extensions) if (e == x) return true;
            return false;
        }

        /// <summary>Lädt vollständig in den Speicher, damit die Datei nicht gesperrt bleibt.</summary>
        public static BitmapSource Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            BitmapSource hit;
            if (Cache.TryGetValue(path, out hit)) return hit;
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();
                Cache[path] = bi;
                return bi;
            }
            catch
            {
                Cache[path] = null;
                return null;
            }
        }
    }

    /// <summary>
    /// Neun Felder im Rechteck — welches leuchtet, dort sitzt das Logo.
    /// Schneller zu treffen als eine Liste mit „oben links … unten rechts".
    /// </summary>
    public class PositionPicker : Border
    {
        private readonly Border[] _cells = new Border[9];
        private int _index = 8;

        public event EventHandler Changed;

        public int Index
        {
            get { return _index; }
            set
            {
                if (value < 0 || value > 8) return;
                _index = value;
                Refresh();
            }
        }

        public PositionPicker()
        {
            CornerRadius = new CornerRadius(7);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(3);
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(0, 0, 6, 0);
            SetResourceReference(BackgroundProperty, "Input");
            SetResourceReference(BorderBrushProperty, "Border");
            ToolTip = "Ecke oder Kante, an der das Logo sitzt";

            Grid grid = new Grid();
            for (int i = 0; i < 3; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            Child = grid;

            for (int i = 0; i < 9; i++)
            {
                Border cell = new Border();
                cell.Width = 15;
                cell.Height = 11;
                cell.Margin = new Thickness(1);
                cell.CornerRadius = new CornerRadius(2);
                cell.Cursor = Cursors.Hand;
                cell.Background = Brushes.Transparent;
                Grid.SetColumn(cell, i % 3);
                Grid.SetRow(cell, i / 3);
                int captured = i;
                cell.MouseLeftButtonUp += delegate
                {
                    _index = captured;
                    Refresh();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                };
                _cells[i] = cell;
                grid.Children.Add(cell);
            }
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < 9; i++)
            {
                if (i == _index) _cells[i].SetResourceReference(BackgroundProperty, "Gold");
                else _cells[i].SetResourceReference(BackgroundProperty, "Divider");
            }
        }
    }
}
