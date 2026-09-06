using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Elas3DView;

namespace Elas3DView.Tests
{
    /// <summary>
    /// Startet das echte Hauptfenster und arbeitet eine Reihe von Schritten ab:
    /// Screenshot, JPG-Schalter, Logo einschalten, Bildansicht, helles Design.
    /// </summary>
    public static class UiShot
    {
        private static Window _w;
        private static string _dir;
        private static string _base;
        private static string _gear;
        private static readonly List<Action> Steps = new List<Action>();
        private static int _step;

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "folge") return Sequence(args[1]);
            return Shots(args);
        }

        /// <summary>
        /// Blättert im echten Fenster durch alle Modelle eines Ordners und meldet
        /// nach jedem, wie viel Speicher der Prozess belegt.
        /// </summary>
        private static int Sequence(string folder)
        {
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Theme.Install();

            string[] all = Directory.GetFiles(folder);
            List<string> models = new List<string>();
            foreach (string f in all)
            {
                string e = Path.GetExtension(f).ToLowerInvariant();
                if (e == ".stl" || e == ".3mf") models.Add(f);
            }
            models.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (models.Count == 0) { Console.WriteLine("Keine Modelle in " + folder); return 1; }

            MainWindow mw = new MainWindow();
            mw.QueueInitialFile(models[0]);
            _w = mw;
            _w.Width = 1200;
            _w.Height = 800;
            _w.Show();

            Console.WriteLine("Ordner: " + folder);
            Console.WriteLine();
            long start = Mem();
            int index = 0, idle = 0;

            // Getaktet statt wartend: die Nachrichtenschleife muss laufen, sonst
            // kehrt die Fortsetzung nach dem await nicht auf den Oberflächen-Thread zurück.
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(200);
            t.Tick += delegate
            {
                string want = Path.GetFileName(models[index]);
                bool ready = _w.Title != null && _w.Title.StartsWith(want, StringComparison.OrdinalIgnoreCase);
                if (!ready)
                {
                    if (++idle < 400) return;               // bis 80 Sekunden je Modell
                    Console.WriteLine("   " + Trim(want, 42) + ": Laden dauerte zu lange");
                }
                else
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "   {0,-44} {1,7:N0} MB", Trim(want, 42), (Mem() - start) / 1048576));
                }
                idle = 0;
                index++;
                if (index >= models.Count)
                {
                    t.Stop();
                    Console.WriteLine();
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "   Spitzenwert des Prozesses               {0,7:N0} MB",
                        Process.GetCurrentProcess().PeakWorkingSet64 / 1048576));
                    app.Shutdown();
                    return;
                }
                PickInList(Path.GetFileName(models[index]));
            };
            t.Start();

            app.Run();
            return 0;
        }

        private static void PickInList(string fileName)
        {
            ListBox lb = FindChild<ListBox>(_w);
            if (lb == null) { Console.WriteLine("FEHLER: Liste nicht gefunden"); return; }
            for (int k = 0; k < lb.Items.Count; k++)
            {
                FileEntry fe = lb.Items[k] as FileEntry;
                if (fe != null && string.Equals(Path.GetFileName(fe.FullPath), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    lb.SelectedIndex = k;
                    return;
                }
            }
            Console.WriteLine("   " + fileName + ": nicht in der Liste");
        }

        private static string Trim(string s, int n)
        {
            return s.Length > n ? s.Substring(0, n - 1) + "…" : s;
        }

        private static long Mem()
        {
            Process p = Process.GetCurrentProcess();
            p.Refresh();
            return p.PrivateMemorySize64;
        }

        private static int Shots(string[] args)
        {
            _dir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "elas3dview-tests");
            Directory.CreateDirectory(_dir);
            string shot = args.Length > 1 ? args[1] : Path.Combine(_dir, "ui.png");
            _base = Path.Combine(Path.GetDirectoryName(shot), Path.GetFileNameWithoutExtension(shot));

            _gear = Path.Combine(_dir, "Zahnrad.stl");
            WriteGear(_gear, 20, 26.0, 32.0, 8.0, 9.0);

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Theme.Install();

            MainWindow mw = new MainWindow();
            mw.QueueInitialFile(_gear);
            _w = mw;
            _w.Width = 1340;
            _w.Height = 860;
            _w.Show();

            BuildSteps();
            WatchPopups();

            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(2600);
            t.Tick += delegate
            {
                try
                {
                    if (_step < Steps.Count) Steps[_step]();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FEHLER in Schritt " + _step + ": " + ex.GetType().Name + ": " + ex.Message);
                }
                _step++;
                t.Interval = TimeSpan.FromMilliseconds(800);
                if (_step >= Steps.Count) { t.Stop(); app.Shutdown(); }
            };
            t.Start();

            app.Run();
            return 0;
        }

        private static void BuildSteps()
        {
            Steps.Add(delegate { Capture(_base + ".png"); });
            Steps.Add(delegate { TestJpgButton(_gear, "ohne-Logo"); });
            Steps.Add(SetLogo);
            Steps.Add(delegate { Capture(_base + "-logo.png"); });
            Steps.Add(delegate { TestJpgButton(_gear, "mit-Logo"); });
            Steps.Add(delegate { Click("Bild zeigen"); });
            Steps.Add(delegate { Capture(_base + "-bildansicht.png"); });
            Steps.Add(delegate { Click("Modell zeigen"); PickCombo("Hintergrund im gespeicherten JPG", "Wie Ansicht"); });
            Steps.Add(delegate { Capture(_base + "-schraffur.png"); });
            Steps.Add(delegate { TestJpgButton(_gear, "Schraffur"); });
            Steps.Add(delegate { PickCombo("Fläche hinter dem Modell", "Studio warm"); });
            Steps.Add(delegate { Capture(_base + "-hintergrundbild.png"); });
            Steps.Add(delegate { TestJpgButton(_gear, "Hintergrundbild"); });
            // Das weiße Negativ-Logo einmal auf dunklem, einmal auf weißem Grund
            Steps.Add(delegate { PickCombo("Logo, das im Bild", "Elas schwarz-weiss"); });
            Steps.Add(delegate { Capture(_base + "-sw-dunkel.png"); });
            Steps.Add(delegate { TestJpgButton(_gear, "sw-auf-dunkel"); });
            Steps.Add(delegate { ExpectPopup("nicht zu sehen", false); });
            Steps.Add(delegate { PickCombo("Hintergrund im gespeicherten JPG", "Weiß"); });
            Steps.Add(delegate { TestJpgButton(_gear, "sw-auf-weiss"); });
            Steps.Add(delegate { ExpectPopup("nicht zu sehen", true); });
            // Buntes Logo auf demselben weißen Grund darf nicht meckern
            Steps.Add(delegate { PickCombo("Logo, das im Bild", "Elas bunt"); });
            Steps.Add(delegate { TestJpgButton(_gear, "bunt-auf-weiss"); });
            Steps.Add(delegate { ExpectPopup("nicht zu sehen", false); });
            // Gegenrichtung: dunkles Logo auf dunklem Grund
            Steps.Add(delegate { PickCombo("Logo, das im Bild", "Testlogo dunkel"); });
            Steps.Add(delegate { PickCombo("Hintergrund im gespeicherten JPG", "Dunkel"); });
            Steps.Add(delegate { TestJpgButton(_gear, "dunkel-auf-dunkel"); });
            Steps.Add(delegate { ExpectPopup("nicht zu sehen", true); });
            Steps.Add(delegate { PickCombo("Hintergrund im gespeicherten JPG", "Weiß"); });
            Steps.Add(delegate { TestJpgButton(_gear, "dunkel-auf-weiss"); });
            Steps.Add(delegate { ExpectPopup("nicht zu sehen", false); });
            Steps.Add(delegate { Theme.Apply(false); });
            Steps.Add(delegate { Capture(_base + "-hell.png"); Theme.Apply(true); });
        }

        /// <summary>Logo einschalten, unten rechts, 18 Prozent der Bildbreite.</summary>
        private static void SetLogo()
        {
            ComboBox cb = FindByTip<ComboBox>(_w, "Logo, das im Bild");
            if (cb == null) { Console.WriteLine("FEHLER: Logo-Auswahl nicht gefunden"); return; }
            if (cb.Items.Count < 2)
            {
                Console.WriteLine("FEHLER: keine Logos im Ordner " + ImageStore.Logos.FolderPath());
                return;
            }

            PositionPicker pp = FindChild<PositionPicker>(_w);
            if (pp != null) pp.Index = 8;
            Slider s = FindByTip<Slider>(_w, "Breite des Logos");
            if (s != null) s.Value = 18;
            cb.SelectedIndex = 1;

            Console.WriteLine("Logo gewählt: " + cb.SelectedItem + "   (" + (cb.Items.Count - 1) + " im Ordner)");
        }

        private static readonly List<string> Popups = new List<string>();

        /// <summary>
        /// Beantwortet auftauchende Popups selbsttätig und schreibt mit, welche kamen.
        /// Ein eigener Takt ist nötig, weil ein Dialog eine eigene Schleife dreht.
        /// </summary>
        private static void WatchPopups()
        {
            DispatcherTimer w = new DispatcherTimer();
            w.Interval = TimeSpan.FromMilliseconds(120);
            w.Tick += delegate
            {
                foreach (Window win in Application.Current.Windows)
                {
                    AppDialog d = win as AppDialog;
                    if (d == null || !d.IsVisible) continue;
                    Popups.Add(d.Title);
                    Console.WriteLine("   Popup: „" + d.Title + "\"  -> Trotzdem speichern");
                    Button b = FindButton(d, "Trotzdem speichern");
                    if (b == null) b = FindButton(d, "OK");
                    if (b == null) b = FindButton(d, "Abbrechen");
                    if (b != null) b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    return;
                }
            };
            w.Start();
        }

        private static void ExpectPopup(string titlePart, bool expected)
        {
            bool seen = false;
            foreach (string s in Popups)
                if (s != null && s.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0) seen = true;
            Console.WriteLine((seen == expected ? "  OK     " : "  FEHLER ") +
                (expected ? "Warnung kam" : "keine Warnung") + " — „" + titlePart + "\"");
            Popups.Clear();
        }

        /// <summary>Wählt in dem über den Hinweistext gefundenen Auswahlfeld einen Eintrag.</summary>
        private static void PickCombo(string tipPart, string itemText)
        {
            ComboBox cb = FindByTip<ComboBox>(_w, tipPart);
            if (cb == null) { Console.WriteLine("FEHLER: Auswahlfeld '" + tipPart + "' nicht gefunden"); return; }
            for (int i = 0; i < cb.Items.Count; i++)
            {
                object o = cb.Items[i];
                string text = o as string;
                if (text == null && o != null) text = o.ToString();
                if (string.Equals(text, itemText, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedIndex = i;
                    Console.WriteLine("gewählt: " + tipPart.Substring(0, Math.Min(22, tipPart.Length)) + "… -> " + itemText);
                    return;
                }
            }
            Console.WriteLine("FEHLER: Eintrag '" + itemText + "' fehlt (" + cb.Items.Count + " Einträge)");
        }

        private static void Click(string content)
        {
            Button b = FindButton(_w, content);
            if (b == null) { Console.WriteLine("FEHLER: Schalter '" + content + "' nicht gefunden"); return; }
            if (!b.IsEnabled) { Console.WriteLine("FEHLER: Schalter '" + content + "' ist gesperrt"); return; }
            b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Console.WriteLine("geklickt: " + content);
        }

        private static void TestJpgButton(string modelPath, string label)
        {
            string jpg = Path.Combine(Path.GetDirectoryName(modelPath),
                                      Path.GetFileNameWithoutExtension(modelPath) + ".jpg");
            if (File.Exists(jpg)) File.Delete(jpg);

            Button btn = FindButton(_w, "JPG speichern");
            if (btn == null) { Console.WriteLine("FEHLER: Schalter 'JPG speichern' nicht gefunden"); return; }
            btn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            while (!File.Exists(jpg) && DateTime.UtcNow < deadline)
            {
                Dispatcher.CurrentDispatcher.Invoke(new Action(delegate { }), DispatcherPriority.ContextIdle);
                System.Threading.Thread.Sleep(25);
            }
            if (!File.Exists(jpg)) { Console.WriteLine("FEHLER: kein JPG erzeugt (" + label + ")"); return; }

            BitmapFrame f = BitmapFrame.Create(new Uri(jpg), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            string copy = Path.Combine(_dir, "export-" + label + ".jpg");
            File.Copy(jpg, copy, true);
            Console.WriteLine(string.Format("JPG {0}: {1}x{2} px, {3:N0} Bytes",
                label, f.PixelWidth, f.PixelHeight, new FileInfo(jpg).Length));
        }

        private static void Capture(string path)
        {
            _w.UpdateLayout();
            int pw = (int)Math.Round(_w.ActualWidth);
            int ph = (int)Math.Round(_w.ActualHeight);
            RenderTargetBitmap rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(_w);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
            Console.WriteLine("Screenshot: " + Path.GetFileName(path));
        }

        // -------------------------------------------------- Baumsuche

        private static Button FindButton(DependencyObject root, string content)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                Button b = c as Button;
                if (b != null && content.Equals(b.Content as string)) return b;
                Button deeper = FindButton(c, content);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static T FindChild<T>(DependencyObject root) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                T hit = c as T;
                if (hit != null) return hit;
                T deeper = FindChild<T>(c);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static T FindByTip<T>(DependencyObject root, string tipPart) where T : FrameworkElement
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                T hit = c as T;
                if (hit != null)
                {
                    string tip = hit.ToolTip as string;
                    if (tip != null && tip.IndexOf(tipPart, StringComparison.OrdinalIgnoreCase) >= 0) return hit;
                }
                T deeper = FindByTip<T>(c, tipPart);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // -------------------------------------------------- Zahnrad als Testteil

        private static double Profile(double angle, int teeth, double rootR, double tipR)
        {
            double toothAngle = 2 * Math.PI / teeth;
            double p = (angle % toothAngle) / toothAngle;
            if (p < 0) p += 1;
            double f;
            if (p < 0.22) f = 0;
            else if (p < 0.36) f = Smooth((p - 0.22) / 0.14);
            else if (p < 0.64) f = 1;
            else if (p < 0.78) f = 1 - Smooth((p - 0.64) / 0.14);
            else f = 0;
            return rootR + (tipR - rootR) * f;
        }

        private static double Smooth(double x)
        {
            if (x < 0) x = 0;
            if (x > 1) x = 1;
            return x * x * (3 - 2 * x);
        }

        private static void WriteGear(string path, int teeth, double rootR, double tipR, double thickness, double holeR)
        {
            int m = teeth * 12;
            double[][] outer = new double[m][];
            double[][] hole = new double[m][];
            for (int i = 0; i < m; i++)
            {
                double a = 2 * Math.PI * i / m;
                double r = Profile(a, teeth, rootR, tipR);
                outer[i] = new double[] { r * Math.Cos(a), r * Math.Sin(a) };
                hole[i] = new double[] { holeR * Math.Cos(a), holeR * Math.Sin(a) };
            }

            List<double[]> tris = new List<double[]>(m * 8);
            double z0 = 0, z1 = thickness;
            for (int i = 0; i < m; i++)
            {
                int j = (i + 1) % m;
                double[] o0 = outer[i], o1 = outer[j], h0 = hole[i], h1 = hole[j];
                Tri(tris, o0[0], o0[1], z1, o1[0], o1[1], z1, h1[0], h1[1], z1);
                Tri(tris, o0[0], o0[1], z1, h1[0], h1[1], z1, h0[0], h0[1], z1);
                Tri(tris, o0[0], o0[1], z0, h0[0], h0[1], z0, h1[0], h1[1], z0);
                Tri(tris, o0[0], o0[1], z0, h1[0], h1[1], z0, o1[0], o1[1], z0);
                Tri(tris, o0[0], o0[1], z0, o1[0], o1[1], z0, o1[0], o1[1], z1);
                Tri(tris, o0[0], o0[1], z0, o1[0], o1[1], z1, o0[0], o0[1], z1);
                Tri(tris, h0[0], h0[1], z0, h0[0], h0[1], z1, h1[0], h1[1], z1);
                Tri(tris, h0[0], h0[1], z0, h1[0], h1[1], z1, h1[0], h1[1], z0);
            }

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
            Console.WriteLine("Testmodell: " + Path.GetFileName(path) + "  (" + tris.Count + " Dreiecke)");
        }

        private static void Tri(List<double[]> l,
            double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz)
        {
            l.Add(new double[] { ax, ay, az, bx, by, bz, cx, cy, cz });
        }
    }
}
