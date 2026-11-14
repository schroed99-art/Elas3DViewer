using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace Elas3DView
{
    /// <summary>
    /// Die beiden Hausschriften von elas3dstudio — Manrope und Cormorant Garamond —,
    /// in die exe eingebettet (Tristan, 14.11.2026: „bette Manrope und Cormorant
    /// Garamond in Elas3DView ein").
    ///
    /// WARUM EIN ORDNER AUF DER PLATTE. WPF lädt eine Schrift nur aus einer Datei
    /// oder aus einer XAML-Ressource; diese Anwendung entsteht aber mit csc allein,
    /// ohne den XAML-Bau. Die Dateien liegen deshalb als gewöhnliche Ressourcen in
    /// der exe und werden beim ersten Gebrauch nach %TEMP%\Elas3DView\schriften
    /// geschrieben. Räumt Windows den Ordner weg, entsteht er beim nächsten Start
    /// neu — geprüft wird jede Datei an ihrer Länge.
    ///
    /// GEHT DAS SCHIEF, BLEIBT ES BEI SEGOE UI UND CONSTANTIA — den Vertretern,
    /// die bis dahin überall standen (siehe Theme.cs). Eine fehlende Schrift ist
    /// kein Grund, nicht zu starten.
    ///
    /// Die Schnitte sind dieselben, die ElasPrintFlow von Google Fonts lädt:
    /// Manrope 300 und 700, Cormorant Garamond 400 kursiv. Beide stehen unter der
    /// SIL Open Font License; die Texte liegen neben den Dateien in
    /// assets\schriften.
    /// </summary>
    internal static class BrandFonts
    {
        private static readonly string[] Files =
            { "Manrope-Light.ttf", "Manrope-Bold.ttf", "CormorantGaramond-Italic.ttf" };

        private const string SansFallback = "Segoe UI Variable Display, Segoe UI";
        private const string SerifFallback = "Constantia, Georgia";

        private static bool _tried;
        private static bool _ready;
        private static Uri _folder;

        /// <summary>Stehen die eingebetteten Schriften bereit?</summary>
        public static bool Ready
        {
            get { Ensure(); return _ready; }
        }

        /// <summary>Manrope — leicht und fett; sonst Segoe UI.</summary>
        public static FontFamily Sans()
        {
            Ensure();
            return _ready ? new FontFamily(_folder, "./#Manrope, " + SansFallback)
                          : new FontFamily(SansFallback);
        }

        /// <summary>Cormorant Garamond — kursiv; sonst Constantia.</summary>
        public static FontFamily Serif()
        {
            Ensure();
            return _ready ? new FontFamily(_folder, "./#Cormorant Garamond, " + SerifFallback)
                          : new FontFamily(SerifFallback);
        }

        private static void Ensure()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "Elas3DView", "schriften");
                Directory.CreateDirectory(dir);
                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string name in Files)
                {
                    using (Stream s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) return;      // ohne Ressourcen gebaut (Testläufe)
                        string target = Path.Combine(dir, name);
                        FileInfo there = new FileInfo(target);
                        if (there.Exists && there.Length == s.Length) continue;
                        try
                        {
                            using (FileStream fs = File.Create(target)) s.CopyTo(fs);
                        }
                        catch (IOException)
                        {
                            // Eine zweite Instanz hält die Datei offen. Liegt sie
                            // vollständig da, genügt das; sonst ohne die Schriften.
                            there.Refresh();
                            if (!there.Exists || there.Length != s.Length) return;
                        }
                    }
                }
                // Der Schrägstrich am Ende gehört dazu: WPF liest die Adresse als Ordner.
                _folder = new Uri(dir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
                _ready = true;
            }
            catch { _ready = false; }
        }
    }

    /// <summary>
    /// Ein Wort in mehreren Gewichten, mit Laufweite gesetzt — was CSS mit
    /// `letter-spacing` tut und ein WPF-TextBlock nicht kann.
    ///
    /// Gebraucht für den Namen im Kopfband: ElasPrintFlow setzt ihn mit
    /// `letter-spacing: -.025em`, und ohne das stünde er bei gleicher Schrift
    /// sichtbar breiter da als drüben.
    ///
    /// JEDES ZEICHEN WIRD EINZELN GEZEICHNET, ABER NICHT EINZELN VERMESSEN. Seine
    /// Stelle ist die Breite des Wortanfangs bis einschließlich dieses Zeichens
    /// minus seine eigene Breite — so bleibt der Ausgleich zwischen Zeichenpaaren
    /// (das „Vi" in „View") erhalten, den die Schrift mitbringt. Zwischen zwei
    /// Gewichten gibt es keinen; das hält der Browser genauso.
    /// </summary>
    internal sealed class TrackedWord : FrameworkElement
    {
        private sealed class Part
        {
            public string Text;
            public Typeface Face;
        }

        private sealed class Glyph
        {
            public FormattedText Text;
            public double X;
        }

        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Glyph> _glyphs = new List<Glyph>();
        private readonly double _size;
        private readonly double _tracking;
        private readonly double _lineHeight;
        private readonly Brush _brush;
        private double _top;

        /// <param name="size">Schriftgröße in Pixeln.</param>
        /// <param name="trackingEm">Laufweite in Geviert, wie `letter-spacing` in em.</param>
        /// <param name="lineHeight">Zeilenhöhe in Pixeln; der Text steht mittig darin.</param>
        public TrackedWord(double size, double trackingEm, double lineHeight, Brush brush)
        {
            _size = size;
            _tracking = trackingEm * size;
            _lineHeight = lineHeight;
            _brush = brush;
            IsHitTestVisible = false;
        }

        public void Add(string text, FontFamily family, FontWeight weight)
        {
            Part p = new Part();
            p.Text = text;
            p.Face = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
            _parts.Add(p);
            InvalidateMeasure();
        }

        private FormattedText Shape(string text, Typeface face)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                     face, _size, _brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _glyphs.Clear();
            double x = 0, height = 0;
            int count = 0;
            foreach (Part p in _parts)
            {
                for (int i = 0; i < p.Text.Length; i++)
                {
                    FormattedText one = Shape(p.Text.Substring(i, 1), p.Face);
                    double upTo = Shape(p.Text.Substring(0, i + 1), p.Face).WidthIncludingTrailingWhitespace;
                    Glyph g = new Glyph();
                    g.Text = one;
                    g.X = x + upTo - one.WidthIncludingTrailingWhitespace + count * _tracking;
                    _glyphs.Add(g);
                    if (one.Height > height) height = one.Height;
                    count++;
                }
                x += Shape(p.Text, p.Face).WidthIncludingTrailingWhitespace;
            }
            _top = (_lineHeight - height) / 2;
            return new Size(Math.Max(0, x + count * _tracking), _lineHeight);
        }

        protected override void OnRender(DrawingContext dc)
        {
            foreach (Glyph g in _glyphs) dc.DrawText(g.Text, new Point(g.X, _top));
        }
    }
}
