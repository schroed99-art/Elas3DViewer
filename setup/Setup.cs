// Das Setup für Elas3DView: eine einzelne exe, in der die Anwendung steckt.
//
// Installiert wird für den angemeldeten Benutzer nach
// %LOCALAPPDATA%\Programs\Elas3DView — ohne Administratorrechte. Das ist kein
// Notbehelf: Die Anwendung legt neben sich die Ordner logos\ und
// hintergruende\ an und schreibt hinein. Unter „C:\Programme" dürfte sie das
// nicht.
//
// Aufrufe:
//   Elas3DView-Setup.exe                     Fenster, installieren
//   Elas3DView-Setup.exe /silent             still installieren
//   Deinstallieren.exe /uninstall [/silent]  entfernen
//
// Gebaut wird es von setup.ps1 mit demselben C#-Compiler wie die Anwendung
// (C# 5 — also keine $"..."-Zeichenketten, kein ?., kein nameof).

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Elas3DSetup
{
    internal static class Programm
    {
        internal const string AppName = "Elas3DView";
        internal const string ExeName = "Elas3DView.exe";
        internal const string UninstName = "Deinstallieren.exe";
        internal const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Elas3DView";
        internal const string Classes = @"Software\Classes\";
        internal const string ProgId = "Elas3DView.Modell";
        internal const int Abgebrochen = 1602;   // der übliche Code für „vom Benutzer abgebrochen"

        internal static readonly string[] Endungen = { ".stl", ".3mf" };
        internal static readonly string[] StandardLogos = { "Elas bunt.png", "Elas schwarz-weiss.png" };

        [STAThread]
        private static int Main(string[] args)
        {
            bool still = Hat(args, "/silent");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (Hat(args, "/uninstall")) return Deinstallation.Start(args, still);

                if (still)
                    return Installation.Ausfuehren(Optionen.Gemerkt(), null, true) ? 0 : Abgebrochen;

                using (InstallFenster f = new InstallFenster())
                {
                    string vorschau = Wert(args, "/vorschau");
                    if (vorschau != null) { f.SpeichereVorschau(vorschau); return 0; }
                    Application.Run(f);
                    return f.Ergebnis;
                }
            }
            catch (Exception ex)
            {
                if (!still)
                    MessageBox.Show("Das hat nicht geklappt:\n\n" + ex.Message, AppName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        internal static bool Hat(string[] args, string schalter)
        {
            foreach (string a in args)
                if (string.Equals(a, schalter, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static string Wert(string[] args, string schalter)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], schalter, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }

    // =====================================================================
    //  Orte
    // =====================================================================

    internal static class Ort
    {
        public static string Ziel
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "Programs", Programm.AppName);
            }
        }

        public static string Exe { get { return Path.Combine(Ziel, Programm.ExeName); } }
        public static string Deinstaller { get { return Path.Combine(Ziel, Programm.UninstName); } }

        public static string DesktopLnk
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Elas3DView.lnk"); }
        }

        public static string StartmenueLnk
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Elas3DView.lnk"); }
        }

        public static bool InZiel(string pfad)
        {
            if (string.IsNullOrEmpty(pfad)) return false;
            try
            {
                string z = Path.GetFullPath(Ziel).TrimEnd('\\') + "\\";
                return Path.GetFullPath(pfad).StartsWith(z, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static bool Gleich(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class Optionen
    {
        public bool Desktop;
        public bool OeffnenMit;

        public Optionen(bool desktop, bool oeffnenMit)
        {
            Desktop = desktop;
            OeffnenMit = oeffnenMit;
        }

        /// <summary>Bei einer Aktualisierung gelten die Haken vom letzten Mal.</summary>
        public static Optionen Gemerkt()
        {
            Optionen o = new Optionen(true, true);
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Programm.UninstKey))
            {
                if (k == null) return o;
                o.Desktop = Convert.ToInt32(k.GetValue("ElasDesktop", 1)) != 0;
                o.OeffnenMit = Convert.ToInt32(k.GetValue("ElasOeffnenMit", 1)) != 0;
            }
            return o;
        }
    }

    // =====================================================================
    //  Installieren
    // =====================================================================

    internal static class Installation
    {
        internal const string Nutzlast = "payload/";

        /// <returns>false, wenn der Benutzer abgebrochen hat.</returns>
        public static bool Ausfuehren(Optionen o, IWin32Window fenster, bool still)
        {
            string ziel = Ort.Ziel;
            if (!Prozesse.WarteBisGeschlossen(fenster, still)) return false;

            Directory.CreateDirectory(ziel);
            Assembly asm = Assembly.GetExecutingAssembly();
            long bytes = 0;
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (!name.StartsWith(Nutzlast, StringComparison.Ordinal)) continue;
                string datei = Path.Combine(ziel, name.Substring(Nutzlast.Length).Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(datei));
                try
                {
                    using (Stream s = asm.GetManifestResourceStream(name))
                    using (FileStream fs = File.Create(datei))
                    {
                        s.CopyTo(fs);
                        bytes += s.Length;
                    }
                }
                catch (IOException ex)
                {
                    throw new IOException(Path.GetFileName(datei) + " ist gesperrt — läuft Elas3DView noch?", ex);
                }
            }

            // Das Setup legt sich selbst als Deinstallationsprogramm daneben.
            string selbst = Application.ExecutablePath;
            string deinst = Ort.Deinstaller;
            if (!Ort.Gleich(selbst, deinst)) File.Copy(selbst, deinst, true);
            bytes += new FileInfo(deinst).Length;

            string exe = Ort.Exe;
            Verknuepfung.Anlegen(Ort.StartmenueLnk, exe);
            if (o.Desktop) Verknuepfung.Anlegen(Ort.DesktopLnk, exe);
            else Verknuepfung.EntfernenWennUnser(Ort.DesktopLnk);

            if (o.OeffnenMit) Zuordnung.Eintragen(exe);
            else Zuordnung.Austragen();

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Programm.UninstKey))
            {
                k.SetValue("DisplayName", Programm.AppName);
                k.SetValue("DisplayVersion", Stand.Fassung);
                k.SetValue("ElasStand", Stand.Anzeige);      // welcher Commit genau
                k.SetValue("Publisher", "Elas3DStudio");
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", ziel);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("UninstallString", "\"" + deinst + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + deinst + "\" /uninstall /silent");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
                k.SetValue("ElasDesktop", o.Desktop ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("ElasOeffnenMit", o.OeffnenMit ? 1 : 0, RegistryValueKind.DWord);
            }

            Shell.Benachrichtigen();
            return true;
        }
    }

    // =====================================================================
    //  Entfernen
    // =====================================================================

    internal static class Deinstallation
    {
        public static int Start(string[] args, bool still)
        {
            string selbst = Application.ExecutablePath;

            // Aus dem eigenen Ordner heraus lässt sich der Ordner nicht leeren —
            // also tut eine Kopie im Temp-Ordner die Arbeit, und diese hier geht.
            if (Ort.InZiel(selbst))
            {
                string kopie = Path.Combine(Path.GetTempPath(),
                    "Elas3DView-Deinstallieren-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
                File.Copy(selbst, kopie, true);
                string a = "/uninstall /warte " + Process.GetCurrentProcess().Id + (still ? " /silent" : "");
                Process.Start(new ProcessStartInfo(kopie, a) { UseShellExecute = false });
                return 0;
            }

            string warte = Programm.Wert(args, "/warte");
            if (warte != null)
            {
                try
                {
                    using (Process p = Process.GetProcessById(int.Parse(warte))) p.WaitForExit(10000);
                }
                catch { }
            }

            try
            {
                return Ausfuehren(still);
            }
            finally
            {
                if (warte != null) SelbstLoeschen(selbst);
            }
        }

        private static int Ausfuehren(bool still)
        {
            string ziel = Ort.Ziel;
            const string titel = "Elas3DView entfernen";

            if (!still && MessageBox.Show("Elas3DView von diesem Rechner entfernen?\n\nDie Einstellungen bleiben erhalten.",
                    titel, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return Programm.Abgebrochen;

            if (!Prozesse.WarteBisGeschlossen(null, still)) return Programm.Abgebrochen;

            // logos\ und hintergruende\ legt die Anwendung an, danach gehören sie
            // dem Benutzer. Liegt dort nur, was sie selbst hineingelegt hat, gehen
            // sie still mit; sonst wird gefragt — und im Zweifel bleiben sie.
            bool bilderMit = true;
            if (EigeneBilder(ziel))
            {
                bilderMit = !still && MessageBox.Show(
                    "In den Ordnern „logos“ und „hintergruende“ liegen eigene Bilder.\n\nSollen sie mit gelöscht werden?",
                    titel, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            }

            Verknuepfung.EntfernenWennUnser(Ort.DesktopLnk);
            Verknuepfung.EntfernenWennUnser(Ort.StartmenueLnk);
            Zuordnung.Austragen();
            Registry.CurrentUser.DeleteSubKeyTree(Programm.UninstKey, false);

            if (Directory.Exists(ziel))
            {
                foreach (string n in new string[] { Programm.ExeName, "Elas3DView.ico", Programm.UninstName })
                    Loesche(Path.Combine(ziel, n));
                foreach (string alt in Directory.GetFiles(ziel, "Elas3DView.alt-*.exe"))
                    Loesche(alt);
                if (bilderMit)
                    foreach (string d in new string[] { "logos", "hintergruende" })
                    {
                        string p = Path.Combine(ziel, d);
                        if (Directory.Exists(p)) Directory.Delete(p, true);
                    }
                if (Directory.GetFileSystemEntries(ziel).Length == 0) Directory.Delete(ziel);
            }

            Shell.Benachrichtigen();

            if (!still)
                MessageBox.Show(Directory.Exists(ziel)
                        ? "Elas3DView ist entfernt.\n\nDeine Bilder liegen weiter in\n" + ziel
                        : "Elas3DView ist entfernt.",
                    titel, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private static bool EigeneBilder(string ziel)
        {
            string logos = Path.Combine(ziel, "logos");
            if (Directory.Exists(logos))
                foreach (string f in Directory.GetFiles(logos))
                    if (Array.IndexOf(Programm.StandardLogos, Path.GetFileName(f)) < 0) return true;

            string hg = Path.Combine(ziel, "hintergruende");
            if (Directory.Exists(hg))
                foreach (string f in Directory.GetFiles(hg))
                    if (!string.Equals(Path.GetFileName(f), "Hinweis.txt", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        /// <summary>Der aufrufende Deinstaller braucht einen Moment, bis er seine Datei freigibt.</summary>
        private static void Loesche(string datei)
        {
            for (int i = 0; i < 40; i++)
            {
                try
                {
                    if (File.Exists(datei)) File.Delete(datei);
                    return;
                }
                catch (IOException) { Thread.Sleep(250); }
                catch (UnauthorizedAccessException) { Thread.Sleep(250); }
            }
            if (File.Exists(datei)) File.Delete(datei);
        }

        private static void SelbstLoeschen(string datei)
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe",
                    "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + datei + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
        }
    }

    // =====================================================================
    //  Hilfen
    // =====================================================================

    internal static class Prozesse
    {
        private static bool LaeuftAusZiel()
        {
            foreach (Process p in Process.GetProcessesByName("Elas3DView"))
            {
                try { if (Ort.InZiel(p.MainModule.FileName)) return true; }
                catch { }
                finally { p.Dispose(); }
            }
            return false;
        }

        /// <returns>false, wenn der Benutzer lieber abbricht.</returns>
        public static bool WarteBisGeschlossen(IWin32Window fenster, bool still)
        {
            while (LaeuftAusZiel())
            {
                if (still) throw new InvalidOperationException("Elas3DView läuft noch.");
                DialogResult r = MessageBox.Show(fenster,
                    "Elas3DView ist gerade geöffnet.\n\nBitte schließen und dann „Wiederholen“ wählen.",
                    Programm.AppName, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (r != DialogResult.Retry) return false;
            }
            return true;
        }
    }

    internal static class Verknuepfung
    {
        public static void Anlegen(string lnk, string exe)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk));
            object shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                link = Rufe(shell, "CreateShortcut", lnk);
                Setze(link, "TargetPath", exe);
                Setze(link, "WorkingDirectory", Path.GetDirectoryName(exe));
                Setze(link, "IconLocation", exe + ",0");
                Setze(link, "Description", "STL- und 3MF-Modelle ansehen");
                Rufe(link, "Save");
            }
            finally
            {
                Freigeben(link);
                Freigeben(shell);
            }
        }

        /// <summary>
        /// Nur eine Verknüpfung, die in den Installationsordner zeigt, ist unsere.
        /// Eine gleichnamige auf eine andere Elas3DView.exe bleibt stehen.
        /// </summary>
        public static void EntfernenWennUnser(string lnk)
        {
            if (!File.Exists(lnk)) return;
            object shell = null, link = null;
            string ziel = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                link = Rufe(shell, "CreateShortcut", lnk);
                ziel = link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null) as string;
            }
            catch { }
            finally
            {
                Freigeben(link);
                Freigeben(shell);
            }
            if (Ort.InZiel(ziel)) File.Delete(lnk);
        }

        private static object Rufe(object o, string name, params object[] args)
        {
            return o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args);
        }

        private static void Setze(object o, string name, object wert)
        {
            o.GetType().InvokeMember(name, BindingFlags.SetProperty, null, o, new object[] { wert });
        }

        private static void Freigeben(object o)
        {
            if (o != null && Marshal.IsComObject(o)) Marshal.FinalReleaseComObject(o);
        }
    }

    /// <summary>
    /// „Öffnen mit" für STL und 3MF. Die Standard-App für diese Endungen bleibt
    /// unangetastet — die setzt Windows nur auf Wunsch des Benutzers.
    /// </summary>
    internal static class Zuordnung
    {
        private const string AppKey = Programm.Classes + @"Applications\" + Programm.ExeName;

        public static void Eintragen(string exe)
        {
            string befehl = "\"" + exe + "\" \"%1\"";
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Programm.Classes + Programm.ProgId))
            {
                k.SetValue("", "3D-Modell");
                using (RegistryKey i = k.CreateSubKey("DefaultIcon")) i.SetValue("", "\"" + exe + "\",0");
                using (RegistryKey c = k.CreateSubKey(@"shell\open\command")) c.SetValue("", befehl);
            }
            foreach (string e in Programm.Endungen)
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Programm.Classes + e + @"\OpenWithProgids"))
                    k.SetValue(Programm.ProgId, new byte[0], RegistryValueKind.None);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(AppKey))
            {
                k.SetValue("FriendlyAppName", Programm.AppName);
                using (RegistryKey c = k.CreateSubKey(@"shell\open\command")) c.SetValue("", befehl);
                using (RegistryKey t = k.CreateSubKey("SupportedTypes"))
                    foreach (string e in Programm.Endungen) t.SetValue(e, "");
            }
        }

        public static void Austragen()
        {
            Registry.CurrentUser.DeleteSubKeyTree(Programm.Classes + Programm.ProgId, false);
            foreach (string e in Programm.Endungen)
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Programm.Classes + e + @"\OpenWithProgids", true))
                    if (k != null) k.DeleteValue(Programm.ProgId, false);
                // Leer gewordene Schlüssel, die das Setup selbst angelegt hat, gehen mit.
                LoescheWennLeer(Programm.Classes + e + @"\OpenWithProgids");
                LoescheWennLeer(Programm.Classes + e);
            }

            // Diesen Schlüssel legt Windows auch selbst an, wenn jemand über
            // „Öffnen mit → Durchsuchen" eine andere Elas3DView.exe wählt. Der bleibt.
            string befehl = null;
            using (RegistryKey c = Registry.CurrentUser.OpenSubKey(AppKey + @"\shell\open\command"))
                if (c != null) befehl = c.GetValue("") as string;
            if (befehl != null && befehl.IndexOf(Ort.Ziel, StringComparison.OrdinalIgnoreCase) >= 0)
                Registry.CurrentUser.DeleteSubKeyTree(AppKey, false);
        }

        private static void LoescheWennLeer(string pfad)
        {
            bool leer;
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(pfad))
            {
                if (k == null) return;
                leer = k.ValueCount == 0 && k.SubKeyCount == 0;
            }
            if (leer) Registry.CurrentUser.DeleteSubKey(pfad, false);
        }
    }

    internal static class Shell
    {
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        /// <summary>Damit Explorer und „Öffnen mit" den neuen Stand ohne Neuanmeldung sehen.</summary>
        public static void Benachrichtigen()
        {
            try { SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero); }
            catch { }
        }
    }

    // =====================================================================
    //  Das Fenster
    // =====================================================================

    internal sealed class InstallFenster : Form
    {
        private static readonly Color Grund = Color.FromArgb(28, 24, 34);
        private static readonly Color Schrift = Color.FromArgb(236, 230, 240);
        private static readonly Color Leise = Color.FromArgb(158, 148, 170);
        private static readonly Color Gold = Color.FromArgb(211, 154, 57);
        private static readonly Color GoldHell = Color.FromArgb(232, 178, 82);
        private static readonly Color KnopfGrund = Color.FromArgb(48, 42, 58);

        private readonly PictureBox _bild;
        private readonly CheckBox _desktop, _oeffnenMit, _starten;
        private readonly Button _los, _abbrechen;
        public int Ergebnis = Programm.Abgebrochen;

        public InstallFenster()
        {
            bool update = File.Exists(Ort.Exe);
            Optionen o = Optionen.Gemerkt();

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.75F);
            Text = update ? "Elas3DView aktualisieren" : "Elas3DView installieren";
            BackColor = Grund;
            ForeColor = Schrift;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 306);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            _bild = new PictureBox { Location = new Point(24, 22), Size = new Size(52, 52), SizeMode = PictureBoxSizeMode.Zoom };
            Label titel = new Label
            {
                Text = Programm.AppName,
                Font = new Font("Segoe UI Semibold", 17F),
                AutoSize = true,
                Location = new Point(88, 18)
            };
            Label unter = new Label
            {
                Text = "STL- und 3MF-Betrachter  ·  Version " + Stand.Fassung + " vom " + Stand.Datum,
                ForeColor = Leise,
                AutoSize = true,
                Location = new Point(91, 54)
            };
            Label ortTitel = new Label
            {
                Text = update ? "Bereits installiert — wird auf diesen Stand gebracht:" : "Wird installiert nach:",
                ForeColor = Leise,
                AutoSize = true,
                Location = new Point(24, 98)
            };
            Label ortPfad = new Label
            {
                Text = Ort.Ziel,
                AutoEllipsis = true,
                Location = new Point(24, 120),
                Size = new Size(472, 22)
            };

            _desktop = Haken("Verknüpfung auf dem Desktop", 158, o.Desktop);
            _oeffnenMit = Haken("STL und 3MF unter „Öffnen mit“ anbieten", 184, o.OeffnenMit);
            _starten = Haken("Danach starten", 210, true);

            _abbrechen = Knopf("Abbrechen", new Point(292, 252), 92, false);
            _los = Knopf(update ? "Aktualisieren" : "Installieren", new Point(396, 252), 100, true);
            _abbrechen.Click += delegate { Close(); };
            _los.Click += Installieren;
            AcceptButton = _los;
            CancelButton = _abbrechen;

            Controls.AddRange(new Control[] { _bild, titel, unter, ortTitel, ortPfad, _desktop, _oeffnenMit, _starten, _abbrechen, _los });
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Erst nach der Skalierung steht fest, wie groß das Bild wirklich wird.
            _bild.Image = Symbolbild(_bild.Height);
        }

        private CheckBox Haken(string text, int y, bool an)
        {
            return new CheckBox
            {
                Text = text,
                Checked = an,
                AutoSize = true,
                Location = new Point(24, y),
                ForeColor = Schrift,
                Cursor = Cursors.Hand
            };
        }

        private Button Knopf(string text, Point ort, int breite, bool haupt)
        {
            Button k = new Button
            {
                Text = text,
                Location = ort,
                Size = new Size(breite, 34),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                BackColor = haupt ? Gold : KnopfGrund,
                ForeColor = haupt ? Color.FromArgb(24, 19, 28) : Schrift
            };
            k.FlatAppearance.BorderSize = haupt ? 0 : 1;
            k.FlatAppearance.BorderColor = Color.FromArgb(78, 70, 90);
            k.FlatAppearance.MouseOverBackColor = haupt ? GoldHell : Color.FromArgb(60, 53, 72);
            k.FlatAppearance.MouseDownBackColor = haupt ? Gold : Color.FromArgb(40, 35, 48);
            if (haupt) k.Font = new Font("Segoe UI Semibold", 9.75F);
            return k;
        }

        private void Installieren(object sender, EventArgs e)
        {
            _los.Enabled = _abbrechen.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                if (!Installation.Ausfuehren(new Optionen(_desktop.Checked, _oeffnenMit.Checked), this, false)) return;
                Ergebnis = 0;
                if (_starten.Checked)
                    Process.Start(new ProcessStartInfo(Ort.Exe) { WorkingDirectory = Ort.Ziel, UseShellExecute = true });
                else
                    MessageBox.Show(this, "Elas3DView ist installiert.\n\nZu finden im Startmenü" +
                                          (_desktop.Checked ? " und auf dem Desktop." : "."),
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Die Installation ist nicht gelungen:\n\n" + ex.Message,
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _los.Enabled = _abbrechen.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Das Symbol aus der mitgelieferten .ico. Ihre Einträge sind PNG-Daten
        /// (so schreibt sie build.ps1), also wird der passende direkt gelesen.
        /// </summary>
        private static Image Symbolbild(int wunsch)
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(Installation.Nutzlast + "Elas3DView.ico"))
                {
                    if (s == null) return null;
                    byte[] ico = new byte[s.Length];
                    s.Read(ico, 0, ico.Length);
                    int anzahl = BitConverter.ToUInt16(ico, 4);
                    int besteGroesse = int.MaxValue, besteLaenge = 0, besterOffset = 0;
                    for (int i = 0; i < anzahl; i++)
                    {
                        int p = 6 + 16 * i;
                        int groesse = ico[p] == 0 ? 256 : ico[p];
                        if (groesse >= wunsch && groesse < besteGroesse)
                        {
                            besteGroesse = groesse;
                            besteLaenge = (int)BitConverter.ToUInt32(ico, p + 8);
                            besterOffset = (int)BitConverter.ToUInt32(ico, p + 12);
                        }
                    }
                    if (besteLaenge == 0) return null;
                    using (MemoryStream ms = new MemoryStream(ico, besterOffset, besteLaenge))
                        return new Bitmap(Image.FromStream(ms));
                }
            }
            catch { return null; }
        }

        /// <summary>Für die Prüfung beim Bauen: das Fenster als PNG, ohne es zu zeigen.</summary>
        public void SpeichereVorschau(string pfad)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            ShowInTaskbar = false;
            Show();
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(Width, Height))
            {
                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                bmp.Save(pfad, ImageFormat.Png);
            }
            Close();
        }
    }
}
