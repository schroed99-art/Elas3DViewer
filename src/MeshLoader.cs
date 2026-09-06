using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml;

namespace Elas3DView
{
    /// <summary>Ergebnis eines Ladevorgangs: fertige, eingefrorene Geometrie plus Kennzahlen.</summary>
    public class LoadedModel
    {
        public MeshGeometry3D Mesh;
        public int TriangleCount;
        public Point3D Min;
        public Point3D Max;
        public double VolumeMm3;
        public string Format = "";

        public Point3D Center
        {
            get { return new Point3D((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2); }
        }

        public Vector3D Size
        {
            get { return new Vector3D(Max.X - Min.X, Max.Y - Min.Y, Max.Z - Min.Z); }
        }

        public double Radius
        {
            get
            {
                Vector3D s = Size;
                double r = 0.5 * Math.Sqrt(s.X * s.X + s.Y * s.Y + s.Z * s.Z);
                return r > 1e-6 ? r : 1.0;
            }
        }
    }

    public static class MeshLoader
    {
        public static bool IsSupported(string path)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            return e == ".stl" || e == ".3mf";
        }

        public static LoadedModel Load(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".3mf") return Load3mf(path);
            if (ext == ".stl") return LoadStl(path);
            throw new NotSupportedException("Nicht unterstuetztes Format: " + ext);
        }

        // ---------------------------------------------------------------- STL

        private static LoadedModel LoadStl(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            {
                if (fs.Length < 15) throw new IOException("Datei ist zu klein bzw. leer.");

                byte[] head = new byte[Math.Min(512, fs.Length)];
                int read = fs.Read(head, 0, head.Length);

                bool binary = true;
                if (fs.Length >= 84 && read >= 84)
                {
                    uint count = BitConverter.ToUInt32(head, 80);
                    if (84L + 50L * count != fs.Length)
                        binary = !LooksLikeAscii(head, read);   // Groesse passt nicht -> evtl. ASCII
                }
                else binary = false;

                fs.Position = 0;
                LoadedModel m = binary ? ParseBinaryStl(fs) : ParseAsciiStl(fs);
                m.Format = binary ? "STL (binaer)" : "STL (ASCII)";
                return m;
            }
        }

        private static bool LooksLikeAscii(byte[] head, int len)
        {
            string s = Encoding.ASCII.GetString(head, 0, len).TrimStart();
            return s.StartsWith("solid", StringComparison.OrdinalIgnoreCase) &&
                   s.IndexOf("facet", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Liest satzweise aus dem Datenstrom unmittelbar in die Zielsammlung. Weder
        /// die Datei noch ein Zwischenfeld liegen dabei vollstaendig im Speicher —
        /// bei einer Million Dreiecken sind das rund 200 MB Unterschied.
        /// </summary>
        private static LoadedModel ParseBinaryStl(FileStream fs)
        {
            byte[] head = new byte[84];
            if (fs.Read(head, 0, 84) < 84) throw new IOException("Ungueltige STL-Datei (Kopf unvollstaendig).");

            long count = BitConverter.ToUInt32(head, 80);
            long fits = (fs.Length - 84L) / 50L;
            if (count > fits) count = fits;           // defekte Zaehler tolerieren
            if (count <= 0) throw new IOException("STL enthaelt keine Dreiecke.");

            int n = (int)count;
            Point3DCollection pos = new Point3DCollection(n * 3);
            Stats st = Stats.Fresh();

            const int Batch = 512;
            byte[] buf = new byte[50 * Batch];
            int done = 0;
            while (done < n)
            {
                int want = Math.Min(Batch, n - done);
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
                    int o = i * 50 + 12;              // Facetten-Normale ueberspringen
                    Point3D p0 = new Point3D(BitConverter.ToSingle(buf, o),
                                             BitConverter.ToSingle(buf, o + 4),
                                             BitConverter.ToSingle(buf, o + 8));
                    Point3D p1 = new Point3D(BitConverter.ToSingle(buf, o + 12),
                                             BitConverter.ToSingle(buf, o + 16),
                                             BitConverter.ToSingle(buf, o + 20));
                    Point3D p2 = new Point3D(BitConverter.ToSingle(buf, o + 24),
                                             BitConverter.ToSingle(buf, o + 28),
                                             BitConverter.ToSingle(buf, o + 32));
                    pos.Add(p0); pos.Add(p1); pos.Add(p2);
                    st.Add(p0, p1, p2);
                }
                done += want;
            }
            if (pos.Count < 3) throw new IOException("STL enthaelt keine Dreiecke.");
            return Finish(pos, st);
        }

        private static LoadedModel ParseAsciiStl(FileStream fs)
        {
            List<Point3D> pts = new List<Point3D>(1024);
            char[] sep = new char[] { ' ', '\t', '\r', '\n' };
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, true, 1 << 16, true))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    int idx = line.IndexOf("vertex", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    string[] t = line.Substring(idx + 6).Split(sep, StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length < 3) continue;
                    double x, y, z;
                    if (double.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                        double.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
                        double.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                        pts.Add(new Point3D(x, y, z));
                }
            }
            if (pts.Count < 3) throw new IOException("STL enthaelt keine lesbaren Dreiecke.");
            return Build(pts);
        }

        // ---------------------------------------------------------------- 3MF

        private class Comp
        {
            public string Path;                       // null = gleiche Modell-Datei
            public int ObjectId;
            public Matrix3D Transform;
        }

        private class ObjDef
        {
            public List<Point3D> Vertices;
            public List<int> Tris;
            public List<Comp> Components;
        }

        private class Item
        {
            public string Path;
            public int ObjectId;
            public Matrix3D Transform;
        }

        private class Mf3Context
        {
            public ZipArchive Zip;
            public Dictionary<string, Dictionary<int, ObjDef>> Parts =
                new Dictionary<string, Dictionary<int, ObjDef>>(StringComparer.OrdinalIgnoreCase);
            public List<Item> Items = new List<Item>();
            public double UnitScale = 1.0;            // Faktor nach Millimeter
        }

        private static LoadedModel Load3mf(string path)
        {
            List<Point3D> pts = new List<Point3D>(4096);
            using (FileStream fs = File.OpenRead(path))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                Mf3Context ctx = new Mf3Context();
                ctx.Zip = zip;

                string root = FindRootModelPart(zip);
                if (root == null) throw new IOException("In der 3MF-Datei wurde kein 3D-Modell gefunden.");

                ParsePart(ctx, root, true);

                foreach (Item it in ctx.Items)
                    Emit(ctx, it.Path ?? root, it.ObjectId, it.Transform, 0, pts);

                // Rueckfallebene 1: kein <build> vorhanden -> alle bereits gelesenen Netze zeichnen
                if (pts.Count == 0)
                {
                    foreach (KeyValuePair<string, Dictionary<int, ObjDef>> part in ctx.Parts)
                        foreach (KeyValuePair<int, ObjDef> kv in part.Value)
                            if (kv.Value.Vertices != null)
                                Emit(ctx, part.Key, kv.Key, Matrix3D.Identity, 0, pts);
                }

                // Rueckfallebene 2: jede weitere .model-Datei im Archiv durchsuchen
                if (pts.Count == 0)
                {
                    List<string> more = new List<string>();
                    foreach (ZipArchiveEntry e in zip.Entries)
                        if (e.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase))
                            more.Add(Normalize(e.FullName));

                    foreach (string p in more)
                    {
                        if (ctx.Parts.ContainsKey(p)) continue;
                        ParsePart(ctx, p, false);
                        foreach (KeyValuePair<int, ObjDef> kv in ctx.Parts[p])
                            if (kv.Value.Vertices != null)
                                Emit(ctx, p, kv.Key, Matrix3D.Identity, 0, pts);
                    }
                }

                if (pts.Count < 3) throw new IOException("Die 3MF-Datei enthaelt keine Dreiecksdaten.");

                if (Math.Abs(ctx.UnitScale - 1.0) > 1e-9)
                {
                    double s = ctx.UnitScale;
                    for (int i = 0; i < pts.Count; i++)
                        pts[i] = new Point3D(pts[i].X * s, pts[i].Y * s, pts[i].Z * s);
                }
            }

            LoadedModel m = Build(pts);
            m.Format = "3MF";
            return m;
        }

        private static string Normalize(string p)
        {
            if (p == null) return null;
            p = p.Replace('\\', '/');
            while (p.StartsWith("/")) p = p.Substring(1);
            return p;
        }

        private static ZipArchiveEntry FindEntry(ZipArchive zip, string name)
        {
            name = Normalize(name);
            foreach (ZipArchiveEntry e in zip.Entries)
                if (string.Equals(Normalize(e.FullName), name, StringComparison.OrdinalIgnoreCase))
                    return e;
            return null;
        }

        private static string FindRootModelPart(ZipArchive zip)
        {
            ZipArchiveEntry rels = FindEntry(zip, "_rels/.rels");
            if (rels != null)
            {
                try
                {
                    using (Stream s = rels.Open())
                    using (XmlReader r = XmlReader.Create(s, SafeXmlSettings()))
                    {
                        while (r.Read())
                        {
                            if (r.NodeType != XmlNodeType.Element || r.LocalName != "Relationship") continue;
                            string type = Attr(r, "Type");
                            string target = Attr(r, "Target");
                            if (type != null && target != null &&
                                type.IndexOf("3dmodel", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string t = Normalize(target);
                                if (FindEntry(zip, t) != null) return t;
                            }
                        }
                    }
                }
                catch { }
            }
            if (FindEntry(zip, "3D/3dmodel.model") != null) return "3D/3dmodel.model";
            foreach (ZipArchiveEntry e in zip.Entries)
                if (e.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase))
                    return Normalize(e.FullName);
            return null;
        }

        private static XmlReaderSettings SafeXmlSettings()
        {
            XmlReaderSettings st = new XmlReaderSettings();
            st.DtdProcessing = DtdProcessing.Prohibit;
            st.XmlResolver = null;
            st.IgnoreComments = true;
            st.IgnoreWhitespace = true;
            return st;
        }

        /// <summary>Attribut ueber den lokalen Namen holen, unabhaengig vom Namensraum-Praefix.</summary>
        private static string Attr(XmlReader r, string localName)
        {
            if (!r.HasAttributes) return null;
            string val = null;
            if (r.MoveToFirstAttribute())
            {
                do
                {
                    if (string.Equals(r.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                    {
                        val = r.Value;
                        break;
                    }
                } while (r.MoveToNextAttribute());
            }
            r.MoveToElement();
            return val;
        }

        private static double D(string s, double fallback)
        {
            double v;
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        private static int I(string s, int fallback)
        {
            int v;
            if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        private static Matrix3D ParseTransform(string s)
        {
            if (string.IsNullOrEmpty(s)) return Matrix3D.Identity;
            char[] sep = new char[] { ' ', '\t', '\r', '\n', ',' };
            string[] t = s.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 12) return Matrix3D.Identity;
            double[] v = new double[12];
            for (int i = 0; i < 12; i++) v[i] = D(t[i], 0);
            // 3MF nutzt Zeilenvektoren, die letzten drei Werte sind die Verschiebung.
            return new Matrix3D(
                v[0], v[1], v[2], 0,
                v[3], v[4], v[5], 0,
                v[6], v[7], v[8], 0,
                v[9], v[10], v[11], 1);
        }

        private static void ParsePart(Mf3Context ctx, string partPath, bool isRoot)
        {
            partPath = Normalize(partPath);
            if (ctx.Parts.ContainsKey(partPath)) return;
            Dictionary<int, ObjDef> table = new Dictionary<int, ObjDef>();
            ctx.Parts[partPath] = table;

            ZipArchiveEntry entry = FindEntry(ctx.Zip, partPath);
            if (entry == null) return;

            using (Stream s = entry.Open())
            using (XmlReader r = XmlReader.Create(s, SafeXmlSettings()))
            {
                ObjDef cur = null;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.EndElement)
                    {
                        if (r.LocalName == "object") cur = null;
                        continue;
                    }
                    if (r.NodeType != XmlNodeType.Element) continue;

                    switch (r.LocalName)
                    {
                        case "model":
                            if (isRoot) ctx.UnitScale = UnitToMm(Attr(r, "unit"));
                            break;

                        case "object":
                            {
                                int id = I(Attr(r, "id"), -1);
                                cur = new ObjDef();
                                if (id >= 0) table[id] = cur;
                                break;
                            }

                        case "vertex":
                            if (cur != null)
                            {
                                if (cur.Vertices == null) cur.Vertices = new List<Point3D>();
                                cur.Vertices.Add(new Point3D(D(Attr(r, "x"), 0), D(Attr(r, "y"), 0), D(Attr(r, "z"), 0)));
                            }
                            break;

                        case "triangle":
                            if (cur != null)
                            {
                                if (cur.Tris == null) cur.Tris = new List<int>();
                                cur.Tris.Add(I(Attr(r, "v1"), 0));
                                cur.Tris.Add(I(Attr(r, "v2"), 0));
                                cur.Tris.Add(I(Attr(r, "v3"), 0));
                            }
                            break;

                        case "component":
                            if (cur != null)
                            {
                                if (cur.Components == null) cur.Components = new List<Comp>();
                                Comp c = new Comp();
                                c.ObjectId = I(Attr(r, "objectid"), -1);
                                c.Path = Normalize(Attr(r, "path"));   // Production-Extension
                                c.Transform = ParseTransform(Attr(r, "transform"));
                                if (c.ObjectId >= 0) cur.Components.Add(c);
                            }
                            break;

                        case "item":
                            {
                                Item it = new Item();
                                it.ObjectId = I(Attr(r, "objectid"), -1);
                                it.Path = Normalize(Attr(r, "path"));
                                it.Transform = ParseTransform(Attr(r, "transform"));
                                if (it.ObjectId >= 0) ctx.Items.Add(it);
                                break;
                            }
                    }
                }
            }
        }

        private static double UnitToMm(string unit)
        {
            if (unit == null) return 1.0;
            switch (unit.Trim().ToLowerInvariant())
            {
                case "micron": return 0.001;
                case "millimeter": return 1.0;
                case "centimeter": return 10.0;
                case "inch": return 25.4;
                case "foot": return 304.8;
                case "meter": return 1000.0;
                default: return 1.0;
            }
        }

        private static void Emit(Mf3Context ctx, string partPath, int objectId, Matrix3D xf, int depth, List<Point3D> outPts)
        {
            if (depth > 24) return;
            partPath = Normalize(partPath);
            if (!ctx.Parts.ContainsKey(partPath)) ParsePart(ctx, partPath, false);

            Dictionary<int, ObjDef> table;
            if (!ctx.Parts.TryGetValue(partPath, out table)) return;
            ObjDef o;
            if (!table.TryGetValue(objectId, out o)) return;

            if (o.Vertices != null && o.Tris != null)
            {
                List<Point3D> v = o.Vertices;
                List<int> tr = o.Tris;
                for (int i = 0; i + 2 < tr.Count; i += 3)
                {
                    int a = tr[i], b = tr[i + 1], c = tr[i + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= v.Count || b >= v.Count || c >= v.Count) continue;
                    outPts.Add(xf.Transform(v[a]));
                    outPts.Add(xf.Transform(v[b]));
                    outPts.Add(xf.Transform(v[c]));
                }
            }

            if (o.Components != null)
                foreach (Comp c in o.Components)
                    Emit(ctx, c.Path ?? partPath, c.ObjectId, Matrix3D.Multiply(c.Transform, xf), depth + 1, outPts);
        }

        // ------------------------------------------------------- Geometrieaufbau

        /// <summary>Sammelt Huelle und Volumen waehrend des Einlesens.</summary>
        private struct Stats
        {
            public double MinX, MinY, MinZ, MaxX, MaxY, MaxZ, Vol6;

            public static Stats Fresh()
            {
                Stats s = new Stats();
                s.MinX = s.MinY = s.MinZ = double.MaxValue;
                s.MaxX = s.MaxY = s.MaxZ = double.MinValue;
                return s;
            }

            public void Add(Point3D p0, Point3D p1, Point3D p2)
            {
                // Signiertes Tetraeder-Volumen gegen den Ursprung
                Vol6 += p0.X * (p1.Y * p2.Z - p2.Y * p1.Z)
                      - p1.X * (p0.Y * p2.Z - p2.Y * p0.Z)
                      + p2.X * (p0.Y * p1.Z - p1.Y * p0.Z);
                Grow(p0); Grow(p1); Grow(p2);
            }

            private void Grow(Point3D p)
            {
                if (p.X < MinX) MinX = p.X;
                if (p.X > MaxX) MaxX = p.X;
                if (p.Y < MinY) MinY = p.Y;
                if (p.Y > MaxY) MaxY = p.Y;
                if (p.Z < MinZ) MinZ = p.Z;
                if (p.Z > MaxZ) MaxZ = p.Z;
            }
        }

        /// <summary>
        /// Friert die Geometrie ein — bewusst nur mit Positionen.
        ///
        /// Ohne TriangleIndices liest WPF die Positionen als fortlaufende Dreiecksliste,
        /// ohne Normals rechnet es sie selbst aus. Da jedes Dreieck eigene Eckpunkte hat,
        /// ergibt das dieselben flaechigen Normalen wie zuvor von Hand — nachgemessen an
        /// einem Modell mit 1,68 Mio. Dreiecken: Bild fuer Bild identisch, dabei 126 statt
        /// 449 MB. Die beiden Listen waeren zusammen groesser als die Positionen selbst.
        /// </summary>
        private static LoadedModel Finish(Point3DCollection positions, Stats st)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            mesh.Positions = positions;
            mesh.Freeze();

            LoadedModel m = new LoadedModel();
            m.Mesh = mesh;
            m.TriangleCount = positions.Count / 3;
            m.Min = new Point3D(st.MinX, st.MinY, st.MinZ);
            m.Max = new Point3D(st.MaxX, st.MaxY, st.MaxZ);
            m.VolumeMm3 = Math.Abs(st.Vol6) / 6.0;
            return m;
        }

        /// <summary>Baut aus einer flachen Punktliste (je 3 Punkte = ein Dreieck) Geometrie.</summary>
        private static LoadedModel Build(List<Point3D> pts)
        {
            int usable = (pts.Count / 3) * 3;
            if (usable < 3) throw new IOException("Keine Dreiecke gefunden.");

            Point3DCollection positions = new Point3DCollection(usable);
            Stats st = Stats.Fresh();

            for (int i = 0; i < usable; i += 3)
            {
                Point3D p0 = pts[i], p1 = pts[i + 1], p2 = pts[i + 2];
                positions.Add(p0); positions.Add(p1); positions.Add(p2);
                st.Add(p0, p1, p2);
            }
            return Finish(positions, st);
        }
    }
}
