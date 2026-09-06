using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Elas3DView
{
    /// <summary>Umbenennen der Modelldatei; die Endung bleibt unangetastet.</summary>
    public class RenameDialog : Window
    {
        private readonly TextBox _box;
        private readonly CheckBox _chk;
        private readonly TextBlock _hint;
        private readonly string _extension;

        public string NewBaseName;
        public bool RenameSidecars;

        public RenameDialog(string currentBaseName, string extension, int sidecarCount)
        {
            _extension = extension;
            NewBaseName = currentBaseName;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            SizeToContent = SizeToContent.Height;
            Width = 480;
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

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(22, 20, 22, 20);
            shell.Child = root;

            TextBlock title = new TextBlock();
            title.Text = "Datei umbenennen";
            title.FontSize = 16;
            title.FontWeight = FontWeights.SemiBold;
            title.Margin = new Thickness(0, 0, 0, 4);
            title.SetResourceReference(TextBlock.ForegroundProperty, "Text");
            root.Children.Add(title);

            TextBlock caption = new TextBlock();
            caption.Text = "Neuer Name ohne die Endung " + extension;
            caption.FontSize = 12;
            caption.Margin = new Thickness(0, 0, 0, 12);
            caption.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            root.Children.Add(caption);

            DockPanel row = new DockPanel();
            TextBlock ext = new TextBlock();
            ext.Text = extension;
            ext.VerticalAlignment = VerticalAlignment.Center;
            ext.Margin = new Thickness(9, 0, 2, 0);
            ext.SetResourceReference(TextBlock.ForegroundProperty, "TextWeak");
            DockPanel.SetDock(ext, Dock.Right);
            row.Children.Add(ext);

            _box = new TextBox();
            _box.Text = currentBaseName;
            _box.Style = (Style)Application.Current.Resources["Txt"];
            _box.TextChanged += delegate { Validate(); };
            _box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
                else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
            };
            row.Children.Add(_box);
            root.Children.Add(row);

            _hint = new TextBlock();
            _hint.Margin = new Thickness(2, 7, 0, 0);
            _hint.FontSize = 12;
            _hint.TextWrapping = TextWrapping.Wrap;
            _hint.SetResourceReference(TextBlock.ForegroundProperty, "Negative");
            root.Children.Add(_hint);

            _chk = new CheckBox();
            _chk.Content = sidecarCount == 1
                ? "Zugehöriges Bild mit umbenennen (1 gefunden)"
                : "Zugehörige Bilder mit umbenennen (" + sidecarCount + " gefunden)";
            _chk.Style = (Style)Application.Current.Resources["Chk"];
            _chk.IsChecked = true;
            _chk.Margin = new Thickness(0, 14, 0, 0);
            _chk.Visibility = sidecarCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            root.Children.Add(_chk);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 20, 0, 0);

            Button cancel = new Button();
            cancel.Content = "Abbrechen";
            cancel.Style = (Style)Application.Current.Resources["Btn"];
            cancel.IsCancel = true;
            cancel.MinWidth = 104;
            cancel.Padding = new Thickness(16, 8, 16, 8);
            buttons.Children.Add(cancel);

            Button ok = new Button();
            ok.Content = "Umbenennen";
            ok.Style = (Style)Application.Current.Resources["BtnPrimary"];
            ok.IsDefault = true;
            ok.MinWidth = 104;
            ok.Margin = new Thickness(0);
            ok.Padding = new Thickness(16, 8, 16, 8);
            ok.Click += delegate { Accept(); };
            buttons.Children.Add(ok);

            root.Children.Add(buttons);

            Loaded += delegate
            {
                _box.Focus();
                _box.SelectAll();
            };
        }

        private string Problem()
        {
            string t = _box.Text;
            if (string.IsNullOrEmpty(t) || t.Trim().Length == 0) return "Der Name darf nicht leer sein.";
            if (t != t.Trim()) return "Der Name darf nicht mit Leerzeichen beginnen oder enden.";
            if (t.EndsWith(".")) return "Der Name darf nicht mit einem Punkt enden.";
            foreach (char c in Path.GetInvalidFileNameChars())
                if (t.IndexOf(c) >= 0)
                    return "Unzulässiges Zeichen im Namen: " + (c < 32 ? "Steuerzeichen" : c.ToString());
            if (t.Length + _extension.Length > 200) return "Der Name ist zu lang.";
            return null;
        }

        private void Validate()
        {
            string p = Problem();
            _hint.Text = p ?? "";
        }

        private void Accept()
        {
            string p = Problem();
            if (p != null) { _hint.Text = p; return; }
            NewBaseName = _box.Text;
            RenameSidecars = _chk.IsChecked == true;
            DialogResult = true;
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Theme.Install();

            app.DispatcherUnhandledException += delegate(object s,
                System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                AppDialog.Error(app.MainWindow, "Unerwarteter Fehler", e.Exception.Message);
                e.Handled = true;
            };

            MainWindow w = new MainWindow();
            if (args != null && args.Length > 0)
            {
                try
                {
                    if (File.Exists(args[0]) && MeshLoader.IsSupported(args[0]))
                        w.QueueInitialFile(Path.GetFullPath(args[0]));
                }
                catch { }
            }
            app.Run(w);
        }
    }
}
