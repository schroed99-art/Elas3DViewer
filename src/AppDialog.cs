using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Elas3DView
{
    public enum DlgKind { Info, Success, Warn, Error, Question }

    /// <summary>Mittlere Schaltfläche eines dreiteiligen Dialogs.</summary>
    public enum DlgResult { Ok, Alt, Cancel }

    /// <summary>
    /// Einheitliches Popup für Meldungen und Rückfragen — die projektweite
    /// Vorgabe aus PROJECT_UI_INSTRUCTIONS.md: nichts als stiller Statustext,
    /// alles als sichtbares, mit Escape schließbares Popup.
    /// </summary>
    public class AppDialog : Window
    {
        private DlgResult _result = DlgResult.Cancel;

        private AppDialog(Window owner, DlgKind kind, string title, string message,
                          string okText, string altText, string cancelText, bool danger)
        {
            Title = title;                 // ohne Rahmen nicht sichtbar, aber identifizierbar
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Height;
            Width = 440;
            ResizeMode = ResizeMode.NoResize;
            Owner = owner;
            WindowStartupLocation = owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;

            // Ohne Schatten und ohne Glas — Erhebung entsteht laut Handoff
            // durch Kante plus Flächenwechsel.
            Border shell = new Border();
            shell.CornerRadius = new CornerRadius(12);
            shell.Background = Brush("Panel");
            shell.BorderThickness = new Thickness(1);
            shell.BorderBrush = Brush("Gold");
            shell.Margin = new Thickness(10);
            Content = shell;

            StackPanel root = new StackPanel();
            shell.Child = root;

            FrameworkElement icon = (FrameworkElement)IconFor(kind);
            icon.Margin = new Thickness(0, 24, 0, 12);
            root.Children.Add(icon);

            TextBlock head = new TextBlock();
            head.Text = title;
            head.FontSize = 17;
            head.FontWeight = FontWeights.Bold;
            head.TextAlignment = TextAlignment.Center;
            head.TextWrapping = TextWrapping.Wrap;
            head.Margin = new Thickness(28, 0, 28, 8);
            head.Foreground = Brush("Text");
            root.Children.Add(head);

            TextBlock body = new TextBlock();
            body.Text = message;
            body.FontSize = 13;
            body.LineHeight = 20;
            body.TextAlignment = TextAlignment.Center;
            body.TextWrapping = TextWrapping.Wrap;
            body.Margin = new Thickness(28, 0, 28, 22);
            body.Foreground = Brush("TextMuted");
            root.Children.Add(body);

            StackPanel bar = new StackPanel();
            bar.Orientation = Orientation.Horizontal;
            bar.HorizontalAlignment = HorizontalAlignment.Center;
            bar.Margin = new Thickness(0, 0, 0, 24);
            root.Children.Add(bar);

            if (cancelText != null) bar.Children.Add(MakeButton(cancelText, DlgResult.Cancel, false, false));
            if (altText != null) bar.Children.Add(MakeButton(altText, DlgResult.Alt, false, false));
            bar.Children.Add(MakeButton(okText, DlgResult.Ok, true, danger));

            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { _result = DlgResult.Cancel; Close(); e.Handled = true; }
            };
        }

        private Button MakeButton(string text, DlgResult result, bool primary, bool danger)
        {
            Button b = new Button();
            b.Content = text;
            b.MinWidth = 108;
            b.Margin = new Thickness(5, 0, 5, 0);
            b.Padding = new Thickness(16, 8, 16, 8);
            b.Style = (Style)Application.Current.Resources[primary ? "BtnPrimary" : "Btn"];
            if (danger && primary)
            {
                b.Foreground = Brush("Negative");
                b.BorderBrush = Brush("Negative");
            }
            if (primary) b.IsDefault = true;
            if (result == DlgResult.Cancel) b.IsCancel = true;
            b.Click += delegate { _result = result; Close(); };
            return b;
        }

        private static Brush Brush(string key)
        {
            object o = Application.Current.Resources[key];
            return o as Brush ?? Brushes.Gray;
        }

        /// <summary>
        /// Gezeichnetes Zeichen statt Symbolschrift — der Entwurf arbeitet ohne
        /// bunte Emoji, und die Farbe trägt hier die Bedeutung.
        /// </summary>
        private static UIElement IconFor(DlgKind k)
        {
            string glyph;
            string colorKey;
            switch (k)
            {
                case DlgKind.Success: glyph = "✓"; colorKey = "Positive"; break;
                case DlgKind.Warn: glyph = "!"; colorKey = "Negative"; break;
                case DlgKind.Error: glyph = "×"; colorKey = "Negative"; break;
                case DlgKind.Question: glyph = "?"; colorKey = "Gold"; break;
                default: glyph = "i"; colorKey = "Neutral"; break;
            }

            Border ring = new Border();
            ring.Width = 42;
            ring.Height = 42;
            ring.CornerRadius = new CornerRadius(999);
            ring.BorderThickness = new Thickness(1.4);
            ring.BorderBrush = Brush(colorKey);
            ring.HorizontalAlignment = HorizontalAlignment.Center;

            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontSize = 22;
            t.FontFamily = new FontFamily(Theme.NumFont);
            t.Foreground = Brush(colorKey);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(0, -2, 0, 0);
            ring.Child = t;
            return ring;
        }

        // -------------------------------------------------------- Aufrufhilfen

        private static DlgResult Run(Window owner, DlgKind kind, string title, string message,
                                     string ok, string alt, string cancel, bool danger)
        {
            MainWindow mw = owner as MainWindow;
            if (mw != null) mw.SetScrim(true);
            try
            {
                AppDialog d = new AppDialog(owner, kind, title, message, ok, alt, cancel, danger);
                d.ShowDialog();
                return d._result;
            }
            finally
            {
                if (mw != null) mw.SetScrim(false);
            }
        }

        public static void Info(Window owner, string title, string message)
        {
            Run(owner, DlgKind.Info, title, message, "OK", null, null, false);
        }

        public static void Success(Window owner, string title, string message)
        {
            Run(owner, DlgKind.Success, title, message, "OK", null, null, false);
        }

        public static void Warn(Window owner, string title, string message)
        {
            Run(owner, DlgKind.Warn, title, message, "OK", null, null, false);
        }

        public static void Error(Window owner, string title, string message)
        {
            Run(owner, DlgKind.Error, title, message, "OK", null, null, false);
        }

        /// <summary>Rückfrage mit Bestätigen und Abbrechen.</summary>
        public static bool Confirm(Window owner, string title, string message, string okText, bool danger)
        {
            return Run(owner, danger ? DlgKind.Warn : DlgKind.Question, title, message,
                       okText, null, "Abbrechen", danger) == DlgResult.Ok;
        }

        /// <summary>Rückfrage mit drei Wegen, etwa Überschreiben / Danebenlegen / Abbrechen.</summary>
        public static DlgResult Choose(Window owner, string title, string message,
                                       string okText, string altText)
        {
            return Run(owner, DlgKind.Question, title, message, okText, altText, "Abbrechen", false);
        }
    }
}
