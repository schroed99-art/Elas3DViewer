using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Elas3DView;

namespace Elas3DView.Tests
{
    /// <summary>
    /// Misst, wo der Speicher bei großen Modellen bleibt und ob er sich über
    /// mehrere geladene Modelle hinweg summiert.
    /// </summary>
    public static class MemProbe
    {
        private static string _outDir;

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Aufruf: MemProbe varianten <modell.stl>");
                Console.WriteLine("        MemProbe folge <modell1> <modell2> ...");
                return 1;
            }
            _outDir = Path.Combine(Path.GetTempPath(), "elas3dview-mem");
            Directory.CreateDirectory(_outDir);

            if (args[0] == "varianten") return Variants(args[1]);
            if (args[0] == "folge") return Sequence(args, 1);
            Console.WriteLine("Unbekannter Modus: " + args[0]);
            return 1;
        }

        // =============================================================== Varianten

        private static int Variants(string path)
        {
            Console.WriteLine("Datei: " + Path.GetFileName(path) +
                              "  (" + new FileInfo(path).Length / 1048576 + " MB)");
            Console.WriteLine();
            Console.WriteLine("Variante                              laden   verwaltet  Prozess   1. Bild  2. Bild");
            Console.WriteLine("--------------------------------------------------------------------------------------");

            Measure("A  heute: alles im Speicher", "a", delegate { return MeshLoader.Load(path).Mesh; });
            Measure("B  Datenstrom", "b", delegate { return LoadDirect(path, true, true); });
            Measure("C  Datenstrom, ohne Index", "c", delegate { return LoadDirect(path, false, true); });
            Measure("D  Datenstrom, ohne Normalen", "d", delegate { return LoadDirect(path, true, false); });
            Measure("E  Datenstrom, ohne beides", "e", delegate { return LoadDirect(path, false, false); });

            Console.WriteLine();
            Compare("a", "d");
            Compare("a", "e");
            return 0;
        }

        private delegate MeshGeometry3D Loader();

        private static void Measure(string name, string tag, Loader load)
        {
            Collect();
            long m0 = GC.GetTotalMemory(true);
            long w0 = Working();

            Stopwatch sw = Stopwatch.StartNew();
            MeshGeometry3D mesh = load();
            sw.Stop();
            long mPeak = GC.GetTotalMemory(false);

            Stopwatch r1 = Stopwatch.StartNew();
            Render(mesh, Path.Combine(_outDir, tag + ".png"));
            r1.Stop();
            Stopwatch r2 = Stopwatch.StartNew();
            Render(mesh, null);
            r2.Stop();

            long wDraw = Working();

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-36} {1,5:N0} ms {2,7:N0} MB {3,7:N0} MB {4,6:N0} ms {5,6:N0} ms",
                name, sw.ElapsedMilliseconds, (mPeak - m0) / 1048576, (wDraw - w0) / 1048576,
                r1.ElapsedMilliseconds, r2.ElapsedMilliseconds));

            GC.KeepAlive(mesh);
        }

        /// <summary>Vergleicht zwei gerenderte Bilder Bildpunkt für Bildpunkt.</summary>
        private static void Compare(string a, string b)
        {
            try
            {
                BitmapSource ia = Load(Path.Combine(_outDir, a + ".png"));
                BitmapSource ib = Load(Path.Combine(_outDir, b + ".png"));
                if (ia.PixelWidth != ib.PixelWidth || ia.PixelHeight != ib.PixelHeight)
                {
                    Console.WriteLine("Vergleich " + a + "/" + b + ": unterschiedliche Größe");
                    return;
                }
                int stride = ia.PixelWidth * 4;
                byte[] pa = new byte[stride * ia.PixelHeight];
                byte[] pb = new byte[stride * ib.PixelHeight];
                ia.CopyPixels(pa, stride, 0);
                ib.CopyPixels(pb, stride, 0);

                long diff = 0, maxDelta = 0;
                for (int i = 0; i < pa.Length; i++)
                {
                    int d = Math.Abs(pa[i] - pb[i]);
                    if (d > 2) diff++;
                    if (d > maxDelta) maxDelta = d;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Bildvergleich {0} gegen {1}: {2:N0} abweichende Kanäle von {3:N0}  ({4:F3} %), größter Unterschied {5}",
                    a, b, diff, pa.Length, 100.0 * diff / pa.Length, maxDelta));
            }
            catch (Exception ex) { Console.WriteLine("Vergleich fehlgeschlagen: " + ex.Message); }
        }

        private static BitmapSource Load(string p)
        {
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(p);
            bi.EndInit();
            return bi;
        }

        // ================================================================== Folge

        private static int Sequence(string[] args, int first)
        {
            Console.WriteLine("Mehrere Modelle nacheinander laden — bleibt der Speicher stehen?");
            Console.WriteLine();
            long start = Working();

            Console.WriteLine("ohne Aufräumen zwischendurch:");
            MeshGeometry3D keep = null;
            for (int i = first; i < args.Length; i++)
            {
                keep = MeshLoader.Load(args[i]).Mesh;      // altes Netz wird fallengelassen
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-42} {1,7:N0} MB", Short(args[i]), (Working() - start) / 1048576));
            }
            GC.KeepAlive(keep);

            keep = null;
            Compact();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "   nach Aufräumen und Verdichten            {0,7:N0} MB", (Working() - start) / 1048576));

            Console.WriteLine();
            Console.WriteLine("mit Aufräumen nach jedem Wechsel:");
            long start2 = Working();
            for (int i = first; i < args.Length; i++)
            {
                keep = null;
                Compact();
                keep = MeshLoader.Load(args[i]).Mesh;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-42} {1,7:N0} MB", Short(args[i]), (Working() - start2) / 1048576));
            }
            GC.KeepAlive(keep);
            return 0;
        }

        private static string Short(string p)
        {
            string n = Path.GetFileName(p);
            return n.Length > 40 ? n.Substring(0, 39) + "…" : n;
        }

        // ================================================================ Werkzeug

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>Aufräumen samt Verdichten des Bereichs für große Felder.</summary>
        private static void Compact()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            Collect();
        }

        private static long Working()
        {
            Process p = Process.GetCurrentProcess();
            p.Refresh();
            return p.PrivateMemorySize64;
        }

        private static void Render(MeshGeometry3D mesh, string savePath)
        {
            int w = 1280, h = 960;
            Rect3D b = mesh.Bounds;
            Point3D center = new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
            double radius = 0.5 * Math.Sqrt(b.SizeX * b.SizeX + b.SizeY * b.SizeY + b.SizeZ * b.SizeZ);
            if (radius < 1e-6) radius = 1;

            double yaw = -55 * Math.PI / 180, pitch = 28 * Math.PI / 180;
            Vector3D dir = new Vector3D(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
            double dist = radius / Math.Sin(22.5 * Math.PI / 180) * 1.1;

            PerspectiveCamera cam = new PerspectiveCamera();
            cam.FieldOfView = 45;
            cam.Position = center + dir * dist;
            cam.LookDirection = -dir;
            cam.UpDirection = new Vector3D(0, 0, 1);
            cam.NearPlaneDistance = Math.Max(0.01, dist * 0.02);
            cam.FarPlaneDistance = dist + radius * 6 + 100;

            Model3DGroup scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(Color.FromRgb(0x36, 0x38, 0x3C)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(0xD4, 0xD4, 0xD2), Norm(-dir + Right(dir) * 0.42 - Up(dir) * 0.55)));
            scene.Children.Add(new GeometryModel3D(mesh,
                new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xB6, 0xBC, 0xC4)))));

            ModelVisual3D mv = new ModelVisual3D();
            mv.Content = scene;
            Viewport3D vp = new Viewport3D();
            vp.Camera = cam;
            vp.Children.Add(mv);

            Grid host = new Grid();
            host.Background = Brushes.White;
            host.Children.Add(vp);
            host.Measure(new Size(w, h));
            host.Arrange(new Rect(0, 0, w, h));
            host.UpdateLayout();

            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(host);
            if (savePath == null) return;

            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(savePath)) enc.Save(fs);
        }

        private static Vector3D Right(Vector3D dir)
        {
            Vector3D r = Vector3D.CrossProduct(-dir, new Vector3D(0, 0, 1));
            if (r.Length < 1e-6) r = new Vector3D(1, 0, 0);
            r.Normalize();
            return r;
        }

        private static Vector3D Up(Vector3D dir)
        {
            Vector3D u = Vector3D.CrossProduct(Right(dir), -dir);
            u.Normalize();
            return u;
        }

        private static Vector3D Norm(Vector3D v)
        {
            double l = v.Length;
            return l < 1e-9 ? new Vector3D(0, 0, -1) : v / l;
        }

        /// <summary>Binäres STL unmittelbar in die Zielsammlungen lesen.</summary>
        private static MeshGeometry3D LoadDirect(string path, bool withIndices, bool withNormals)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            {
                byte[] head = new byte[84];
                if (fs.Read(head, 0, 84) < 84) throw new IOException("Kopf unvollständig.");
                long count = BitConverter.ToUInt32(head, 80);
                long fits = (fs.Length - 84L) / 50L;
                if (count > fits) count = fits;
                int n = (int)count;

                Point3DCollection pos = new Point3DCollection(n * 3);
                Vector3DCollection nor = withNormals ? new Vector3DCollection(n * 3) : null;
                Int32Collection idx = withIndices ? new Int32Collection(n * 3) : null;

                byte[] buf = new byte[50 * 512];
                int done = 0;
                while (done < n)
                {
                    int want = Math.Min(512, n - done);
                    int need = want * 50, got = 0;
                    while (got < need)
                    {
                        int r = fs.Read(buf, got, need - got);
                        if (r <= 0) break;
                        got += r;
                    }
                    if (got < need) want = got / 50;
                    if (want <= 0) break;

                    for (int i = 0; i < want; i++)
                    {
                        int o = i * 50 + 12;
                        Point3D p0 = new Point3D(BitConverter.ToSingle(buf, o), BitConverter.ToSingle(buf, o + 4), BitConverter.ToSingle(buf, o + 8));
                        Point3D p1 = new Point3D(BitConverter.ToSingle(buf, o + 12), BitConverter.ToSingle(buf, o + 16), BitConverter.ToSingle(buf, o + 20));
                        Point3D p2 = new Point3D(BitConverter.ToSingle(buf, o + 24), BitConverter.ToSingle(buf, o + 28), BitConverter.ToSingle(buf, o + 32));
                        pos.Add(p0); pos.Add(p1); pos.Add(p2);
                        if (nor != null)
                        {
                            Vector3D v = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                            double len = v.Length;
                            if (len > 1e-12) v /= len; else v = new Vector3D(0, 0, 1);
                            nor.Add(v); nor.Add(v); nor.Add(v);
                        }
                        if (idx != null)
                        {
                            int b = (done + i) * 3;
                            idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                        }
                    }
                    done += want;
                }

                MeshGeometry3D mesh = new MeshGeometry3D();
                mesh.Positions = pos;
                if (nor != null) mesh.Normals = nor;
                if (idx != null) mesh.TriangleIndices = idx;
                mesh.Freeze();
                return mesh;
            }
        }
    }
}
