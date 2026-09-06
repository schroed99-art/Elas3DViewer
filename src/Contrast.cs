using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Elas3DView
{
    /// <summary>
    /// Prüft, ob ein Logo auf dem gewählten Grund überhaupt zu sehen wäre.
    ///
    /// Anlass: Das Negativ-Logo von Elas ist reines Weiß auf durchsichtig. Auf
    /// dunklem Grund sieht es hervorragend aus, auf dem weißen Produktbildgrund
    /// verschwindet es spurlos — nachgemessen null von 46.464 Bildpunkten, die
    /// sich abheben. Ohne Hinweis merkt das niemand, bis das Bild im Laden hängt.
    /// </summary>
    public static class Contrast
    {
        /// <summary>Mittlere Helligkeit und Streuung der sichtbaren Bildpunkte, 0 bis 1.</summary>
        public struct Stats
        {
            public double Mean;
            public double Spread;
            public bool Known;
        }

        private static readonly Dictionary<BitmapSource, Stats> Cache =
            new Dictionary<BitmapSource, Stats>();

        public static Stats OfImage(BitmapSource src)
        {
            Stats hit;
            if (src == null) return new Stats();
            if (Cache.TryGetValue(src, out hit)) return hit;

            Stats st = new Stats();
            try
            {
                FormatConvertedBitmap bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                int w = bgra.PixelWidth, h = bgra.PixelHeight;
                int stride = w * 4;
                byte[] px = new byte[stride * h];
                bgra.CopyPixels(px, stride, 0);

                // Jeder vierte Punkt genügt; das Ergebnis ändert sich dadurch nicht.
                int step = 4;
                double sum = 0, sumSq = 0;
                long n = 0;
                for (int y = 0; y < h; y += step)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x += step)
                    {
                        int o = row + x * 4;
                        if (px[o + 3] < 40) continue;          // durchsichtig zählt nicht
                        double l = (0.114 * px[o] + 0.587 * px[o + 1] + 0.299 * px[o + 2]) / 255.0;
                        sum += l;
                        sumSq += l * l;
                        n++;
                    }
                }
                if (n > 0)
                {
                    st.Mean = sum / n;
                    double var = sumSq / n - st.Mean * st.Mean;
                    st.Spread = var > 0 ? Math.Sqrt(var) : 0;
                    st.Known = true;
                }
            }
            catch { }

            Cache[src] = st;
            return st;
        }

        /// <summary>
        /// Mittlere Helligkeit des Grundes in einem Ausschnitt, angegeben in Anteilen
        /// der Fläche. Gerechnet wird, indem der Grund allein klein gerendert wird —
        /// so gilt dasselbe Verfahren für Farbe, Verlauf, Schraffur und Bild.
        /// </summary>
        public static Stats OfBrush(Brush brush, double aspect, Rect region)
        {
            Stats st = new Stats();
            if (brush == null) return st;
            try
            {
                if (aspect < 0.05 || double.IsNaN(aspect)) aspect = 4.0 / 3.0;
                int w = 200;
                int h = Math.Max(1, (int)Math.Round(200 / aspect));

                DrawingVisual dv = new DrawingVisual();
                using (DrawingContext dc = dv.RenderOpen())
                    dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));

                RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);

                int stride = w * 4;
                byte[] px = new byte[stride * h];
                rtb.CopyPixels(px, stride, 0);

                int x0 = Clamp((int)Math.Floor(region.X * w), 0, w - 1);
                int x1 = Clamp((int)Math.Ceiling(region.Right * w), x0 + 1, w);
                int y0 = Clamp((int)Math.Floor(region.Y * h), 0, h - 1);
                int y1 = Clamp((int)Math.Ceiling(region.Bottom * h), y0 + 1, h);

                double sum = 0, sumSq = 0;
                long n = 0;
                for (int y = y0; y < y1; y++)
                {
                    int row = y * stride;
                    for (int x = x0; x < x1; x++)
                    {
                        int o = row + x * 4;
                        double l = (0.114 * px[o] + 0.587 * px[o + 1] + 0.299 * px[o + 2]) / 255.0;
                        sum += l;
                        sumSq += l * l;
                        n++;
                    }
                }
                if (n > 0)
                {
                    st.Mean = sum / n;
                    double var = sumSq / n - st.Mean * st.Mean;
                    st.Spread = var > 0 ? Math.Sqrt(var) : 0;
                    st.Known = true;
                }
            }
            catch { }
            return st;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        /// <summary>
        /// Verschwindet das Logo? Nur wenn es selbst kaum eigenen Kontrast mitbringt
        /// — ein buntes Logo trägt seine Lesbarkeit in sich und wird nie gemeldet.
        /// </summary>
        public static bool WouldVanish(Stats logo, Stats ground)
        {
            if (!logo.Known || !ground.Known) return false;
            if (logo.Spread > 0.18) return false;            // genug eigene Zeichnung
            if (ground.Spread > 0.22) return false;          // unruhiger Grund, irgendwo hebt es sich ab
            return Math.Abs(logo.Mean - ground.Mean) < 0.18;
        }

        public static string Percent(double v)
        {
            return Math.Round(v * 100).ToString(System.Globalization.CultureInfo.CurrentCulture) + " %";
        }
    }
}
