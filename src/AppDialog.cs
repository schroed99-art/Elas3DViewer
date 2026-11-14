using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Elas3DView
{
    public enum DlgKind { Info, Success, Warn, Error, Question }

    /// <summary>
    /// Ein Stück des Meldungstexts, das sich anklicken lässt — etwa ein Ablageort,
    /// der sich im Explorer öffnet. Der Text muss wörtlich in der Meldung stehen.
    /// </summary>
    public class DlgLink
    {
        public readonly string Text;
        public readonly string Tip;
        public readonly Action Click;

        public DlgLink(string text, string tip, Action click)
        {
            Text = text;
            Tip = tip;
            Click = click;
        }
    }

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
                          string okText, string altText, string cancelText, bool danger,
                          DlgLink[] links)
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
            Fill(body, message, links);
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

        /// <summary>
        /// Setzt den Text und macht anklickbar, was als Link gemeldet ist — jeweils
        /// die erste Fundstelle. Das Fenster bleibt dabei offen: Ein Link zeigt
        /// etwas, entschieden wird weiter über die Schaltflächen. Die Links nehmen
        /// keinen Tastaturfokus; die Eingabetaste gehört der Hauptschaltfläche.
        /// </summary>
        private static void Fill(TextBlock body, string message, DlgLink[] links)
        {
            if (links == null || links.Length == 0) { body.Text = message; return; }

            bool[] used = new bool[links.Length];
            int pos = 0;
            while (pos < message.Length)
            {
                int at = -1, which = -1;
                for (int i = 0; i < links.Length; i++)
                {
                    if (used[i] || links[i] == null || string.IsNullOrEmpty(links[i].Text)) continue;
                    int found = message.IndexOf(links[i].Text, pos, StringComparison.Ordinal);
                    if (found >= 0 && (at < 0 || found < at)) { at = found; which = i; }
                }
                if (which < 0) break;

                if (at > pos)
                    body.Inlines.Add(new System.Windows.Documents.Run(message.Substring(pos, at - pos)));

                DlgLink link = links[which];
                used[which] = true;
                System.Windows.Documents.Hyperlink h = new System.Windows.Documents.Hyperlink(
                    new System.Windows.Documents.Run(link.Text));
                h.Foreground = Brush("GoldText");
                h.Focusable = false;
                if (link.Tip != null) h.ToolTip = link.Tip;
                h.Click += delegate
                {
                    try { link.Click(); }
                    catch (Exception ex) { h.ToolTip = "Das ließ sich nicht öffnen: " + ex.Message; }
                };
                body.Inlines.Add(h);
                pos = at + link.Text.Length;
            }
            if (pos < message.Length)
                body.Inlines.Add(new System.Windows.Documents.Run(message.Substring(pos)));
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

        /// <summary>Für die Prüfung ohne Bildschirm: zeichnet eine Meldung in eine Datei, ohne sie zu zeigen.</summary>
        internal static void SavePicture(string path, DlgKind kind, string title, string message,
                                         string okText, string cancelText, bool danger,
                                         DlgLink[] links = null)
        {
            AppDialog d = new AppDialog(null, kind, title, message, okText, null, cancelText, danger, links);
            DialogProbe.Save((FrameworkElement)d.Content, d.Width, path);
        }

        // -------------------------------------------------------- Aufrufhilfen

        private static DlgResult Run(Window owner, DlgKind kind, string title, string message,
                                     string ok, string alt, string cancel, bool danger,
                                     DlgLink[] links = null)
        {
            MainWindow mw = owner as MainWindow;
            if (mw != null) mw.SetScrim(true);
            try
            {
                AppDialog d = new AppDialog(owner, kind, title, message, ok, alt, cancel, danger, links);
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

        public static void Warn(Window owner, string title, string message, DlgLink[] links = null)
        {
            Run(owner, DlgKind.Warn, title, message, "OK", null, null, false, links);
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

        /// <summary>
        /// Rückfrage, bei der auch der Weg zurück einen eigenen Namen trägt — etwa
        /// „Behalten" statt „Abbrechen", wenn die eigentliche Arbeit schon getan ist.
        /// </summary>
        public static bool Ask(Window owner, DlgKind kind, string title, string message,
                               string okText, string cancelText, bool danger, DlgLink[] links = null)
        {
            return Run(owner, kind, title, message, okText, null, cancelText, danger, links) == DlgResult.Ok;
        }

        /// <summary>Rückfrage mit drei Wegen, etwa Überschreiben / Danebenlegen / Abbrechen.</summary>
        public static DlgResult Choose(Window owner, string title, string message,
                                       string okText, string altText)
        {
            return Run(owner, DlgKind.Question, title, message, okText, altText, "Abbrechen", false);
        }
    }
}
