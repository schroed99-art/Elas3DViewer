using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Elas3DView;

namespace Elas3DView.Tests
{
    public static class SelfTest
    {
        private static int _fail;
        private static string _dir;

        [STAThread]
        public static int Main(string[] args)
        {
            _dir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "Elas3DView-selftest");
            Directory.CreateDirectory(_dir);
            Console.WriteLine("Testverzeichnis: " + _dir);
            Console.WriteLine();

            GenerateFixtures();

            Console.WriteLine("--- Laden ---");
            TestCube(Path.Combine(_dir, "cube_bin.stl"), "STL (binaer)");
            TestCube(Path.Combine(_dir, "cube_ascii.stl"), "STL (ASCII)");
            TestCube(Path.Combine(_dir, "cube_simple.3mf"), "3MF");
            TestCube(Path.Combine(_dir, "cube_cm.3mf"), "3MF");          // 2 cm -> 20 mm
            TestComponents(Path.Combine(_dir, "components.3mf"));
            TestSphere(Path.Combine(_dir, "sphere.stl"));

            Console.WriteLine();
            Console.WriteLine("--- Datei nach dem Laden nicht gesperrt (Voraussetzung fürs Umbenennen) ---");
            TestNoLock(Path.Combine(_dir, "cube_bin.stl"));
            TestNoLock(Path.Combine(_dir, "cube_simple.3mf"));
            TestNoLock(Path.Combine(_dir, "components.3mf"));

            Console.WriteLine();
            Console.WriteLine("--- Offscreen-Rendering (JPG-Pfad) ---");
            TestRender(Path.Combine(_dir, "sphere.stl"), 1920);
            TestRender(Path.Combine(_dir, "components.3mf"), 1280);

            Console.WriteLine();
            Console.WriteLine("--- Löschen in den Papierkorb ---");
            TestRecycle();

            if (args.Length > 1)
            {
                Console.WriteLine();
                Console.WriteLine("--- Große Modelle aus dem Bestand ---");
                for (int i = 1; i < args.Length; i++)
                {
                    TestBig(args[i]);
                    TestRender(args[i], 1920);
                }
            }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "ALLE TESTS BESTANDEN" : (_fail + " TEST(S) FEHLGESCHLAGEN"));
            return _fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------ Pruefungen

        private static void Check(string what, bool ok, string detail)
        {
            Console.WriteLine((ok ? "  OK    " : "  FEHLER ") + what + (detail == null ? "" : "   " + detail));
            if (!ok) _fail++;
        }

        private static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }

        private static void TestCube(string path, string expectFormat)
        {
            string name = Path.GetFileName(path);
            try
            {
                LoadedModel m = MeshLoader.Load(path);
                Vector3D s = m.Size;
                bool ok = m.TriangleCount == 12
                          && m.Format == expectFormat
                          && Near(s.X, 20, 0.01) && Near(s.Y, 20, 0.01) && Near(s.Z, 20, 0.01)
                          && Near(m.VolumeMm3, 8000, 1.0)
                          && m.Mesh.Positions.Count == 36
                          && m.Mesh.IsFrozen;
                Check(name, ok, string.Format(CultureInfo.InvariantCulture,
                    "{0}, {1} Dreiecke, {2:F1}x{3:F1}x{4:F1} mm, {5:F0} mm3",
                    m.Format, m.TriangleCount, s.X, s.Y, s.Z, m.VolumeMm3));
            }
            catch (Exception ex) { Check(name, false, ex.Message); }
        }

        /// <summary>Zwei per Komponente eingebundene Wuerfel, zusaetzlich vom Build-Item verschoben.</summary>
        private static void TestComponents(string path)
        {
            string name = Path.GetFileName(path);
            try
            {
                LoadedModel m = MeshLoader.Load(path);
                Vector3D s = m.Size;
                // Wuerfel 20 mm bei x=0 und bei x=30 -> Gesamtbreite 50, dazu Item-Versatz +100/+5/0
                bool ok = m.TriangleCount == 24
                          && Near(s.X, 50, 0.01) && Near(s.Y, 20, 0.01) && Near(s.Z, 20, 0.01)
                          && Near(m.Min.X, 100, 0.01) && Near(m.Min.Y, 5, 0.01)
                          && Near(m.VolumeMm3, 16000, 1.0);
                Check(name, ok, string.Format(CultureInfo.InvariantCulture,
                    "{0} Dreiecke, min=({1:F1},{2:F1},{3:F1}), {4:F1}x{5:F1}x{6:F1} mm, {7:F0} mm3",
                    m.TriangleCount, m.Min.X, m.Min.Y, m.Min.Z, s.X, s.Y, s.Z, m.VolumeMm3));
            }
            catch (Exception ex) { Check(name, false, ex.Message); }
        }

        /// <summary>Nach dem Laden muss sich die Datei sofort umbenennen lassen.</summary>
        private static void TestNoLock(string path)
        {
            string name = Path.GetFileName(path);
            string moved = path + ".moved";
            try
            {
                MeshLoader.Load(path);
                File.Move(path, moved);
                File.Move(moved, path);
                Check(name, true, "umbenennen direkt nach dem Laden möglich");
            }
            catch (Exception ex)
            {
                if (File.Exists(moved) && !File.Exists(path)) File.Move(moved, path);
                Check(name, false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>Wandert eine Datei wirklich in den Papierkorb statt endgültig zu verschwinden?</summary>
        private static void TestRecycle()
        {
            string p = Path.Combine(_dir, "papierkorb-probe.txt");
            try
            {
                File.WriteAllText(p, "Probe");
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    p,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                    Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                Check("papierkorb-probe.txt", !File.Exists(p), "verschoben, nicht endgültig gelöscht");
            }
            catch (Exception ex)
            {
                Check("papierkorb-probe.txt", false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>Ladezeit, Speicher und Renderzeit an einem echten großen Modell.</summary>
        private static void TestBig(string path)
        {
            string name = Path.GetFileName(path);
            try
            {
                if (!File.Exists(path)) { Check(name, false, "nicht gefunden"); return; }
                long mb = new FileInfo(path).Length / 1048576;

                long before = GC.GetTotalMemory(true);
                DateTime t0 = DateTime.UtcNow;
                LoadedModel m = MeshLoader.Load(path);
                double loadMs = (DateTime.UtcNow - t0).TotalMilliseconds;
                long used = (GC.GetTotalMemory(false) - before) / 1048576;

                Vector3D s = m.Size;
                Check(name, m.TriangleCount > 0, string.Format(CultureInfo.InvariantCulture,
                    "{0} MB, {1:N0} Dreiecke, laden {2:F0} ms, ~{3} MB Speicher, {4:F0}x{5:F0}x{6:F0} mm",
                    mb, m.TriangleCount, loadMs, used, s.X, s.Y, s.Z));
            }
            catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
        }

        private static void TestSphere(string path)
        {
            string name = Path.GetFileName(path);
            try
            {
                DateTime t0 = DateTime.UtcNow;
                LoadedModel m = MeshLoader.Load(path);
                double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
                double expected = 4.0 / 3.0 * Math.PI * 25 * 25 * 25;
                Vector3D s = m.Size;
                bool ok = m.TriangleCount > 70000
                          && Near(s.X, 50, 0.5) && Near(s.Z, 50, 0.5)
                          && m.VolumeMm3 > expected * 0.97 && m.VolumeMm3 < expected * 1.01;
                Check(name, ok, string.Format(CultureInfo.InvariantCulture,
                    "{0} Dreiecke in {1:F0} ms, {2:F1} mm Durchmesser, {3:F0} mm3 (Kugel: {4:F0})",
                    m.TriangleCount, ms, s.X, m.VolumeMm3, expected));
            }
            catch (Exception ex) { Check(name, false, ex.Message); }
        }

        /// <summary>Rendert wie die Anwendung abseits des Bildschirms und prueft, dass wirklich etwas zu sehen ist.</summary>
        private static void TestRender(string path, int width)
        {
            string name = Path.GetFileName(path);
            try
            {
                LoadedModel m = MeshLoader.Load(path);
                int height = (int)(width * 3.0 / 4.0);

                double yaw = -55 * Math.PI / 180, pitch = 28 * Math.PI / 180;
                Vector3D dir = new Vector3D(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
                double fovH = 45 * Math.PI / 180;
                double aspect = (double)width / height;
                double fovV = 2 * Math.Atan(Math.Tan(fovH / 2) / aspect);
                double dist = Math.Max(m.Radius / Math.Sin(fovH / 2), m.Radius / Math.Sin(fovV / 2)) * 1.10;

                PerspectiveCamera cam = new PerspectiveCamera();
                cam.FieldOfView = 45;
                cam.Position = m.Center + dir * dist;
                cam.LookDirection = -dir;
                cam.UpDirection = new Vector3D(0, 0, 1);
                cam.NearPlaneDistance = Math.Max(0.01, dist * 0.02);
                cam.FarPlaneDistance = dist + m.Radius * 6 + 100;

                Model3DGroup scene = new Model3DGroup();
                scene.Children.Add(new AmbientLight(Color.FromRgb(0x4C, 0x4E, 0x52)));
                scene.Children.Add(new DirectionalLight(Color.FromRgb(0xBE, 0xBE, 0xBE), -dir));
                MaterialGroup mg = new MaterialGroup();
                mg.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xB6, 0xBC, 0xC4))));
                GeometryModel3D gm = new GeometryModel3D(m.Mesh, mg);
                gm.BackMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.DarkSlateGray));
                scene.Children.Add(gm);

                ModelVisual3D mv = new ModelVisual3D();
                mv.Content = scene;
                Viewport3D vp = new Viewport3D();
                vp.Camera = cam;
                vp.Children.Add(mv);

                Grid host = new Grid();
                host.Background = new SolidColorBrush(Colors.White);
                host.Children.Add(vp);
                Size sz = new Size(width, height);
                host.Measure(sz);
                host.Arrange(new Rect(sz));
                host.UpdateLayout();

                DateTime t0 = DateTime.UtcNow;
                RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(host);
                rtb.Freeze();
                double ms = (DateTime.UtcNow - t0).TotalMilliseconds;

                // Wie viele Pixel weichen vom weissen Hintergrund ab?
                int stride = width * 4;
                byte[] px = new byte[stride * height];
                rtb.CopyPixels(px, stride, 0);
                long drawn = 0;
                for (int i = 0; i < px.Length; i += 4)
                    if (px[i] < 235 || px[i + 1] < 235 || px[i + 2] < 235) drawn++;
                double share = 100.0 * drawn / (width * (double)height);

                string jpg = Path.Combine(_dir, Path.GetFileNameWithoutExtension(path) + ".jpg");
                JpegBitmapEncoder enc = new JpegBitmapEncoder();
                enc.QualityLevel = 92;
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (FileStream fs = new FileStream(jpg, FileMode.Create, FileAccess.Write)) enc.Save(fs);

                long jpgSize = new FileInfo(jpg).Length;
                bool ok = share > 5.0 && share < 95.0 && jpgSize > 3000;
                Check(name + " -> " + width + "px", ok, string.Format(CultureInfo.InvariantCulture,
                    "{0:F0} ms, {1:F1} % Modellflaeche, JPG {2:N0} Bytes", ms, share, jpgSize));
            }
            catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
        }

        // ------------------------------------------------------------ Testdaten

        private static void AddBox(List<double[]> tris, double x0, double y0, double z0, double x1, double y1, double z1)
        {
            double[][] v =
            {
                new double[]{x0,y0,z0}, new double[]{x1,y0,z0}, new double[]{x1,y1,z0}, new double[]{x0,y1,z0},
                new double[]{x0,y0,z1}, new double[]{x1,y0,z1}, new double[]{x1,y1,z1}, new double[]{x0,y1,z1}
            };
            int[][] faces =
            {
                new int[]{0,3,2}, new int[]{0,2,1},   // unten  (-Z)
                new int[]{4,5,6}, new int[]{4,6,7},   // oben   (+Z)
                new int[]{0,1,5}, new int[]{0,5,4},   // vorn   (-Y)
                new int[]{2,3,7}, new int[]{2,7,6},   // hinten (+Y)
                new int[]{1,2,6}, new int[]{1,6,5},   // rechts (+X)
                new int[]{3,0,4}, new int[]{3,4,7}    // links  (-X)
            };
            foreach (int[] f in faces)
                tris.Add(new double[] { v[f[0]][0], v[f[0]][1], v[f[0]][2],
                                        v[f[1]][0], v[f[1]][1], v[f[1]][2],
                                        v[f[2]][0], v[f[2]][1], v[f[2]][2] });
        }

        private static void WriteBinaryStl(string path, List<double[]> tris)
        {
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write(new byte[80]);
                w.Write((uint)tris.Count);
                foreach (double[] t in tris)
                {
                    w.Write(0f); w.Write(0f); w.Write(0f);
                    for (int i = 0; i < 9; i++) w.Write((float)t[i]);
                    w.Write((ushort)0);
                }
            }
        }

        private static void WriteAsciiStl(string path, List<double[]> tris)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("solid test");
            foreach (double[] t in tris)
            {
                sb.AppendLine("  facet normal 0 0 0");
                sb.AppendLine("    outer loop");
                for (int i = 0; i < 3; i++)
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      vertex {0} {1} {2}",
                        t[i * 3], t[i * 3 + 1], t[i * 3 + 2]));
                sb.AppendLine("    endloop");
                sb.AppendLine("  endfacet");
            }
            sb.AppendLine("endsolid test");
            File.WriteAllText(path, sb.ToString());
        }

        private static void Write3mf(string path, string modelXml)
        {
            if (File.Exists(path)) File.Delete(path);
            using (FileStream fs = File.Create(path))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddEntry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"model\" ContentType=\"application/vnd.ms-package.3dmanufacturing-3dmodel+xml\"/>" +
                    "</Types>");
                AddEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rel0\" Target=\"/3D/3dmodel.model\" " +
                    "Type=\"http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel\"/>" +
                    "</Relationships>");
                AddEntry(zip, "3D/3dmodel.model", modelXml);
            }
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (StreamWriter w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                w.Write(content);
        }

        private static string CubeMeshXml(double s)
        {
            StringBuilder v = new StringBuilder(), t = new StringBuilder();
            double[][] p =
            {
                new double[]{0,0,0}, new double[]{s,0,0}, new double[]{s,s,0}, new double[]{0,s,0},
                new double[]{0,0,s}, new double[]{s,0,s}, new double[]{s,s,s}, new double[]{0,s,s}
            };
            foreach (double[] q in p)
                v.AppendFormat(CultureInfo.InvariantCulture, "<vertex x=\"{0}\" y=\"{1}\" z=\"{2}\"/>", q[0], q[1], q[2]);
            int[][] faces =
            {
                new int[]{0,3,2}, new int[]{0,2,1}, new int[]{4,5,6}, new int[]{4,6,7},
                new int[]{0,1,5}, new int[]{0,5,4}, new int[]{2,3,7}, new int[]{2,7,6},
                new int[]{1,2,6}, new int[]{1,6,5}, new int[]{3,0,4}, new int[]{3,4,7}
            };
            foreach (int[] f in faces)
                t.AppendFormat("<triangle v1=\"{0}\" v2=\"{1}\" v3=\"{2}\"/>", f[0], f[1], f[2]);
            return "<mesh><vertices>" + v + "</vertices><triangles>" + t + "</triangles></mesh>";
        }

        private static void GenerateFixtures()
        {
            List<double[]> cube = new List<double[]>();
            AddBox(cube, 0, 0, 0, 20, 20, 20);
            WriteBinaryStl(Path.Combine(_dir, "cube_bin.stl"), cube);
            WriteAsciiStl(Path.Combine(_dir, "cube_ascii.stl"), cube);

            string head = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                          "<model unit=\"{0}\" xml:lang=\"en-US\" " +
                          "xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">";

            Write3mf(Path.Combine(_dir, "cube_simple.3mf"),
                string.Format(head, "millimeter") +
                "<resources><object id=\"1\" type=\"model\">" + CubeMeshXml(20) + "</object></resources>" +
                "<build><item objectid=\"1\"/></build></model>");

            // 2 cm Kantenlaenge muss als 20 mm ankommen
            Write3mf(Path.Combine(_dir, "cube_cm.3mf"),
                string.Format(head, "centimeter") +
                "<resources><object id=\"1\" type=\"model\">" + CubeMeshXml(2) + "</object></resources>" +
                "<build><item objectid=\"1\"/></build></model>");

            // Objekt 2 bindet Objekt 1 zweimal ein, das Build-Item verschiebt zusaetzlich
            Write3mf(Path.Combine(_dir, "components.3mf"),
                string.Format(head, "millimeter") +
                "<resources>" +
                "<object id=\"1\" type=\"model\">" + CubeMeshXml(20) + "</object>" +
                "<object id=\"2\" type=\"model\"><components>" +
                "<component objectid=\"1\"/>" +
                "<component objectid=\"1\" transform=\"1 0 0 0 1 0 0 0 1 30 0 0\"/>" +
                "</components></object>" +
                "</resources>" +
                "<build><item objectid=\"2\" transform=\"1 0 0 0 1 0 0 0 1 100 5 0\"/></build></model>");

            WriteSphere(Path.Combine(_dir, "sphere.stl"), 25.0, 240, 160);
        }

        private static void WriteSphere(string path, double r, int slices, int stacks)
        {
            List<double[]> tris = new List<double[]>(slices * stacks * 2);
            for (int i = 0; i < stacks; i++)
            {
                double p0 = Math.PI * i / stacks, p1 = Math.PI * (i + 1) / stacks;
                for (int j = 0; j < slices; j++)
                {
                    double t0 = 2 * Math.PI * j / slices, t1 = 2 * Math.PI * (j + 1) / slices;
                    double[] a = Sph(r, p0, t0), b = Sph(r, p1, t0), c = Sph(r, p1, t1), d = Sph(r, p0, t1);
                    tris.Add(new double[] { a[0], a[1], a[2], b[0], b[1], b[2], c[0], c[1], c[2] });
                    tris.Add(new double[] { a[0], a[1], a[2], c[0], c[1], c[2], d[0], d[1], d[2] });
                }
            }
            WriteBinaryStl(path, tris);
        }

        private static double[] Sph(double r, double phi, double theta)
        {
            return new double[]
            {
                r * Math.Sin(phi) * Math.Cos(theta),
                r * Math.Sin(phi) * Math.Sin(theta),
                r * Math.Cos(phi)
            };
        }
    }
}
