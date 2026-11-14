using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Elas3DView
{
    /// <summary>Was ElasPrintFlow abgelehnt hat — in Worten, die man dem Nutzer zeigen kann.</summary>
    public class PrintFlowException : Exception
    {
        /// <summary>Die Sitzung fehlt oder ist abgelaufen; eine neue Anmeldung hilft.</summary>
        public bool NeedLogin;

        /// <summary>Name und Kategorie sind dort schon vergeben.</summary>
        public bool Duplicate;

        public PrintFlowException(string message) : base(message) { }
    }

    /// <summary>Das angelegte Modell, so wie ElasPrintFlow es führt.</summary>
    public class PrintFlowModel
    {
        public int Id;
        public string Name;
        public string Category;
        public string ArticleNumber;
        public string NasPath;

        /// <summary>Jede Datei auf der NAS wurde Byte für Byte mit der lokalen verglichen.</summary>
        public bool Verified;

        /// <summary>Warum der Vergleich nicht gelang oder nicht stimmte.</summary>
        public string VerifyProblem;

        /// <summary>
        /// Weitere Dateien derselben Akte, wie sie im Modellordner heißen — in
        /// ElasPrintFlow stehen sie unter „Quelldateien &amp; Varianten".
        /// </summary>
        public List<string> ExtraNames = new List<string>();

        /// <summary>Vorschaubilder, die sich nicht ablegen ließen. Die Dateien selbst liegen trotzdem da.</summary>
        public int PicturesFailed;
    }

    /// <summary>
    /// Eine Datei, die MIT in die Akte geht (Tristan, 14.11.2026): keine eigene
    /// Modellakte, keine eigene Artikelnummer — sie liegt im Ordner des Modells
    /// neben der Hauptdatei.
    /// </summary>
    public class PrintFlowFile
    {
        public string Path;

        /// <summary>
        /// Das Bild für die Variantenliste (dort Name.vorschau.png). Null, wenn
        /// keines erzeugt wurde — die Datei geht dann ohne Bild hinüber.
        /// </summary>
        public byte[] PicturePng;
    }

    public class PrintFlowProgress
    {
        public string Text;

        /// <summary>Nur während des Hochladens: danach wäre ein Abbruch ein halbes Modell.</summary>
        public bool CanCancel;
    }

    /// <summary>
    /// Übergabe an ElasPrintFlow über dessen eigene Schnittstelle — derselbe Weg,
    /// den die Weboberfläche und der ElasSTLsortierer gehen:
    ///
    ///   1. Datei hochladen        POST /api/models/hochladen-lokal   (landet unter „Vom Rechner")
    ///   2. Ordner erfassen        POST /api/models/scan-legacy
    ///   3. Modell anlegen         POST /api/models/execute-batch-import
    ///
    /// Schritt 3 verschiebt die Datei nach Kategorie\Name\ auf der NAS, schreibt
    /// das Titelbild und vergibt die Artikelnummer. Die Nummer entsteht aus der
    /// laufenden Modell-ID; deshalb kann nur ElasPrintFlow sie vergeben, und
    /// deshalb gibt es hier keinen zweiten Weg an ihm vorbei auf die Freigabe.
    ///
    /// Gespeichert wird nie das Passwort, nur die Sitzung.
    /// </summary>
    public sealed class PrintFlowClient : IDisposable
    {
        /// <summary>FUHRE_MAX in hochladen-lokal: mehr nimmt eine Anfrage nicht an.</summary>
        public const long MaxUpload = 300L * 1024 * 1024;

        public const string DefaultUrl = "http://192.168.178.82:3000";

        private static readonly JavaScriptSerializer Json = NewSerializer();

        private readonly CookieContainer _cookies = new CookieContainer();
        private readonly HttpClient _http;
        private readonly Uri _base;

        public string BaseUrl { get; private set; }

        static PrintFlowClient()
        {
            // Ohne Zielangabe im Kompilat gilt sonst der Stand von .NET 4.0: kein TLS 1.2.
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            try { ServicePointManager.Expect100Continue = false; } catch { }
        }

        private static JavaScriptSerializer NewSerializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;     // das Titelbild reist als Text mit
            return s;
        }

        public PrintFlowClient(string baseUrl, string session)
        {
            BaseUrl = NormalizeUrl(baseUrl);
            _base = new Uri(BaseUrl + "/");

            HttpClientHandler h = new HttpClientHandler();
            h.CookieContainer = _cookies;
            h.UseCookies = true;
            h.AllowAutoRedirect = false;        // eine Umleitung heißt hier: zur Anmeldeseite
            _http = new HttpClient(h);
            _http.Timeout = Timeout.InfiniteTimeSpan;   // die Frist setzt jede Anfrage selbst

            if (!string.IsNullOrEmpty(session))
            {
                try { _cookies.Add(_base, new Cookie("session", session, "/")); }
                catch { }
            }
        }

        public void Dispose() { _http.Dispose(); }

        /// <summary>Wert des Sitzungs-Cookies; leer, solange niemand angemeldet ist.</summary>
        public string Session
        {
            get
            {
                Cookie c = _cookies.GetCookies(_base)["session"];
                return c == null ? "" : c.Value;
            }
        }

        public static string NormalizeUrl(string url)
        {
            string u = (url ?? "").Trim().TrimEnd('/');
            if (u.Length == 0) throw new PrintFlowException("Bitte die Adresse von ElasPrintFlow angeben.");
            if (u.IndexOf("://", StringComparison.Ordinal) < 0) u = "http://" + u;
            Uri parsed;
            if (!Uri.TryCreate(u, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != "http" && parsed.Scheme != "https"))
                throw new PrintFlowException("Die Adresse muss mit http:// oder https:// beginnen.");
            return u;
        }

        /// <summary>
        /// Dieselbe Regel wie dateinameFuerModell() in ElasPrintFlow: was auf der
        /// Freigabe kein Ordnername sein kann, wird zum Unterstrich.
        /// </summary>
        public static string CleanName(string name)
        {
            return Regex.Replace(name ?? "", "[<>:\"/\\\\|?*\\x00-\\x1F]", "_").Trim();
        }

        // ------------------------------------------------------------ Anfragen

        private sealed class Reply
        {
            public int Status;
            public object Data;

            public bool Ok { get { return Status >= 200 && Status < 300; } }

            public Dictionary<string, object> Object { get { return Data as Dictionary<string, object>; } }

            public string Error(string fallback)
            {
                string e = Str(Object, "error");
                return string.IsNullOrEmpty(e) ? fallback : e;
            }
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return null;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static IList List(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v)) return null;
            return v as IList;
        }

        private static HttpContent JsonBody(object value)
        {
            return new StringContent(Json.Serialize(value), Encoding.UTF8, "application/json");
        }

        private static Dictionary<string, object> Map(params object[] pairs)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) d[(string)pairs[i]] = pairs[i + 1];
            return d;
        }

        /// <param name="seconds">Frist für die ganze Anfrage; 0 = keine (Hochladen).</param>
        /// <param name="login">Bei der Anmeldung heißt 401 „falsches Passwort", nicht „abgelaufen".</param>
        private async Task<Reply> Send(HttpMethod method, string path, HttpContent content,
                                       int seconds, CancellationToken ct, bool login)
        {
            bool unreachable = false;
            HttpResponseMessage res = null;

            using (HttpRequestMessage req = new HttpRequestMessage(method, new Uri(_base, path)))
            using (CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                req.Content = content;
                if (seconds > 0) limit.CancelAfter(TimeSpan.FromSeconds(seconds));
                try
                {
                    res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, limit.Token)
                                     .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    if (!(ex is OperationCanceledException) && !(ex is HttpRequestException) &&
                        !(ex is IOException) && !(ex is WebException))
                        throw;
                    unreachable = true;
                }
            }

            if (unreachable || res == null)
                throw new PrintFlowException("ElasPrintFlow ist unter " + BaseUrl + " nicht erreichbar.");

            using (res)
            {
                int status = (int)res.StatusCode;
                if (!login && (status == 401 || (status >= 300 && status < 400)))
                {
                    PrintFlowException expired =
                        new PrintFlowException("Die Anmeldung bei ElasPrintFlow ist abgelaufen.");
                    expired.NeedLogin = true;
                    throw expired;
                }

                string text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                Reply r = new Reply();
                r.Status = status;
                try { r.Data = Json.DeserializeObject(text); }
                catch { }                           // keine JSON-Antwort
                return r;
            }
        }

        // ------------------------------------------------------------ Anmeldung

        public async Task Login(string user, string password, CancellationToken ct)
        {
            Reply r = await Send(HttpMethod.Post, "api/auth/login",
                                 JsonBody(Map("username", user, "password", password)), 25, ct, true)
                            .ConfigureAwait(false);
            if (r.Ok && Session.Length > 0) return;

            if (r.Object == null && r.Status != 401 && r.Status != 429)
                throw new PrintFlowException("Unter " + BaseUrl + " antwortet kein ElasPrintFlow.");
            throw new PrintFlowException(r.Error("Anmeldung abgelehnt."));
        }

        /// <summary>Zugleich die Probe, ob die Sitzung noch gilt.</summary>
        public async Task<List<string>> Categories(CancellationToken ct)
        {
            Reply r = await Send(HttpMethod.Get, "api/models/categories", null, 25, ct, false)
                            .ConfigureAwait(false);
            IList list = r.Data as IList;
            if (!r.Ok || list == null)
                throw new PrintFlowException(r.Error("Unerwartete Antwort von ElasPrintFlow."));

            List<string> names = new List<string>();
            bool unsorted = false;
            foreach (object o in list)
            {
                string n = o as string ?? Str(o as Dictionary<string, object>, "name");
                if (string.IsNullOrEmpty(n)) continue;
                names.Add(n);
                if (string.Equals(n, "Unsortiert", StringComparison.OrdinalIgnoreCase)) unsorted = true;
            }
            if (!unsorted) names.Add("Unsortiert");
            return names;
        }

        /// <summary>
        /// Gibt es Name und Kategorie dort schon? Die harte Prüfung macht
        /// ElasPrintFlow beim Anlegen selbst; diese hier spart nur das Hochladen
        /// einer Datei, die danach doch abgelehnt würde. Groß- und Kleinschreibung
        /// zählen nicht — auf der Freigabe wäre es derselbe Ordner.
        /// </summary>
        /// <returns>Artikelnummer des vorhandenen Modells, sonst null.</returns>
        public async Task<string> FindDuplicate(string name, string category, CancellationToken ct)
        {
            string clean = CleanName(name);
            string cat = CleanName(category);
            string query = SearchTerm(clean);
            if (query.Length < 2) return null;

            Reply r = await Send(HttpMethod.Get,
                                 "api/models/suche?alles=1&q=" + Uri.EscapeDataString(query),
                                 null, 25, ct, false).ConfigureAwait(false);
            IList list = List(r.Object, "modelle");
            if (!r.Ok || list == null) return null;

            foreach (object o in list)
            {
                Dictionary<string, object> m = o as Dictionary<string, object>;
                if (string.Equals(Str(m, "name"), clean, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Str(m, "category"), cat, StringComparison.OrdinalIgnoreCase))
                    return Str(m, "article_number") ?? "";
            }
            return null;
        }

        /// <summary>
        /// Die Suche dort vergleicht nur A bis Z ohne Rücksicht auf Groß und Klein;
        /// „ÜBERTOPF" fände „Übertopf" nicht. Gesucht wird deshalb mit dem längsten
        /// Stück des Namens ohne Sonderzeichen, verglichen wird danach hier.
        /// </summary>
        private static string SearchTerm(string name)
        {
            string best = "";
            int start = 0;
            for (int i = 0; i <= name.Length; i++)
            {
                if (i < name.Length && name[i] < 128) continue;
                string run = name.Substring(start, i - start).Trim();
                if (run.Length > best.Length) best = run;
                start = i + 1;
            }
            return best.Length >= 2 ? best : name;
        }

        // ------------------------------------------------------------ Übernahme

        /// <summary>
        /// Legt ein Modell vollständig an. Wirft, solange nichts angelegt ist; ist
        /// es angelegt, kommt es zurück — auch wenn der Vergleich danach scheitert.
        /// </summary>
        /// <param name="extras">
        /// Weitere Dateien für dieselbe Akte; null oder leer, wenn das Modell aus
        /// einer Datei besteht. Sie behalten ihren Namen und liegen danach neben
        /// der Hauptdatei — ElasPrintFlow legt für sie kein eigenes Modell an.
        /// </param>
        /// <param name="mainPicturePng">
        /// Das Bild der Hauptdatei für die Variantenliste. Nur von Belang, wenn es
        /// weitere Dateien gibt: Dann soll jede Zeile dort ihr Bild haben.
        /// </param>
        public async Task<PrintFlowModel> Import(string filePath, string name, string category, byte[] titlePng,
                                                 IList<PrintFlowFile> extras, byte[] mainPicturePng,
                                                 IProgress<PrintFlowProgress> progress, CancellationToken ct)
        {
            // Die Hauptdatei steht vorn — an dieser Stelle hängt in jedem Schritt, welche es ist.
            List<string> locals = new List<string>();
            locals.Add(filePath);
            if (extras != null) foreach (PrintFlowFile x in extras) locals.Add(x.Path);
            int total = locals.Count;

            string folder = "Elas3DView " + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                            " " + Guid.NewGuid().ToString("N").Substring(0, 6);

            // 1. Hochladen — jede Datei als eigene Anfrage in denselben Ordner.
            //    Die Grenze von 300 MB gilt drüben je Anfrage, nicht je Akte.
            string remoteFolder = null;
            List<string> uploaded = new List<string>();
            Exception uploadFailure = null;
            try
            {
                for (int i = 0; i < total; i++)
                {
                    string fileName = Path.GetFileName(locals[i]);
                    string label = total == 1
                        ? "Datei wird hochgeladen …"
                        : "Datei " + (i + 1) + " von " + total + " wird hochgeladen …";

                    Reply up;
                    using (UploadContent body = new UploadContent(locals[i], fileName, folder, label, progress, ct))
                        up = await Send(HttpMethod.Post, "api/models/hochladen-lokal", body, 0, ct, false)
                                   .ConfigureAwait(false);

                    object okFlag;
                    bool written = up.Ok && up.Object != null && up.Object.TryGetValue("ok", out okFlag) &&
                                   okFlag is bool && (bool)okFlag;
                    if (!written)
                    {
                        string why = Str(up.Object, "error");
                        IList skipped = List(up.Object, "uebergangen");
                        if (string.IsNullOrEmpty(why) && skipped != null && skipped.Count > 0)
                            why = Convert.ToString(skipped[0], CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(why)) why = "Das Hochladen ist fehlgeschlagen.";
                        throw new PrintFlowException(total == 1 ? why : fileName + ": " + why);
                    }
                    remoteFolder = Str(up.Object, "ordner");
                    uploaded.Add(remoteFolder + "\\" + fileName);
                }
            }
            catch (Exception ex) { uploadFailure = ex; }

            if (uploadFailure != null)
            {
                // Bricht es bei der dritten Datei ab, liegen zwei schon drüben —
                // herrenlos, denn ein Modell gibt es noch nicht. Sie gehen wieder weg.
                foreach (string orphan in uploaded) await Discard(orphan).ConfigureAwait(false);
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(uploadFailure).Throw();
            }

            // Ab hier liegen die Dateien drüben. Ein Abbruch ließe sie herrenlos zurück.
            ct = CancellationToken.None;

            // 2. Erfassen
            Report(progress, total == 1 ? "Datei wird erfasst …" : "Dateien werden erfasst …");
            Reply scan = await Send(HttpMethod.Post, "api/models/scan-legacy",
                                    JsonBody(Map("folderPath", remoteFolder)), 120, ct, false)
                               .ConfigureAwait(false);
            if (!scan.Ok || scan.Object == null)
                throw new PrintFlowException(scan.Error("ElasPrintFlow konnte die Datei nicht erfassen.") +
                                             Where(remoteFolder));

            // „models" lässt aus, was dem Dateinamen nach schon bekannt ist; das
            // steht dann unter „bereitsErfasst". Der Ordner gehört allein dieser
            // Übernahme, also ist beides die eigene Datei. Ob es das Modell
            // wirklich schon gibt, entscheidet der gewählte Name — in Schritt 3.
            //
            // WIE DER SCAN DIE DATEIEN GRUPPIERT, ZÄHLT HIER NICHT. Er macht aus
            // ähnlichen Namen ein Modell und aus verschiedenen mehrere; welche
            // Dateien zusammengehören, hat der Nutzer angehakt. Gelesen werden
            // nur die Pfade, unter denen ElasPrintFlow die Dateien führt.
            List<Dictionary<string, object>> seen = new List<Dictionary<string, object>>();
            Collect(seen, List(scan.Object, "models"));
            Collect(seen, List(scan.Object, "bereitsErfasst"));
            if (seen.Count != total)
                throw new PrintFlowException("ElasPrintFlow hat in der Ablage " + seen.Count + " statt " +
                                             (total == 1 ? "einer Datei" : total + " Dateien") + " gefunden." +
                                             Where(remoteFolder));

            Dictionary<string, Dictionary<string, object>> byName =
                new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, object> d in seen) byName[Str(d, "name") ?? ""] = d;

            // 3. Anlegen — ein Modell, die erste Datei ist die Hauptdatei
            Report(progress, "Modell wird angelegt …");
            ArrayList files = new ArrayList();
            List<string> remotePaths = new List<string>();
            for (int i = 0; i < total; i++)
            {
                Dictionary<string, object> d;
                if (!byName.TryGetValue(Path.GetFileName(locals[i]), out d))
                    throw new PrintFlowException("ElasPrintFlow führt „" + Path.GetFileName(locals[i]) +
                                                 "“ in der Ablage nicht." + Where(remoteFolder));
                files.Add(Map("name", Str(d, "name"), "path", Str(d, "path"), "isMain", i == 0));
                remotePaths.Add(Str(d, "path"));
            }
            string picture = titlePng == null ? null : "data:image/png;base64," + Convert.ToBase64String(titlePng);

            Reply done = await Send(HttpMethod.Post, "api/models/execute-batch-import",
                JsonBody(Map("modelData", Map("name", name, "category", category, "files", files),
                             "previewDataUrl", picture)),
                600, ct, false).ConfigureAwait(false);

            if (done.Status == 409)
            {
                foreach (string orphan in remotePaths) await Discard(orphan).ConfigureAwait(false);

                // Zwei Gründe für 409: Den Namen gibt es in der Kategorie schon —
                // oder im Zielordner liegt bereits eine Datei, die überschrieben
                // würde (`zielBelegt`). Beim zweiten sagt die Antwort, welche.
                object blocked;
                bool targetTaken = done.Object != null && done.Object.TryGetValue("zielBelegt", out blocked) &&
                                   blocked is bool && (bool)blocked;
                PrintFlowException dup = new PrintFlowException(targetTaken
                    ? done.Error("Im Zielordner liegt bereits eine Datei dieses Namens.")
                    : "„" + name + "“ gibt es in der Kategorie „" + category + "“ schon.");
                dup.Duplicate = !targetTaken;
                throw dup;
            }
            object success;
            bool created = done.Ok && done.Object != null && done.Object.TryGetValue("success", out success) &&
                           success is bool && (bool)success;
            if (!created)
                throw new PrintFlowException(done.Error("Das Anlegen des Modells ist fehlgeschlagen.") +
                                             Where(remoteFolder));

            PrintFlowModel model = new PrintFlowModel();
            model.Id = Convert.ToInt32(done.Object["modelId"], CultureInfo.InvariantCulture);
            model.Name = CleanName(name);
            model.Category = CleanName(category);

            // 4. Nachlesen, was ElasPrintFlow daraus gemacht hat
            try
            {
                Reply single = await Send(HttpMethod.Get, "api/models/single?id=" + model.Id, null, 60, ct, false)
                                     .ConfigureAwait(false);
                if (single.Ok && single.Object != null)
                {
                    model.Name = Str(single.Object, "name") ?? model.Name;
                    model.Category = Str(single.Object, "category") ?? model.Category;
                    model.ArticleNumber = Str(single.Object, "article_number");
                    model.NasPath = Str(single.Object, "nas_path");
                }
            }
            catch (PrintFlowException) { }

            // Der Ordner des Modells, wie ElasPrintFlow ihn schreibt. Die Hauptdatei
            // heißt dort wie das Modell, die weiteren behalten ihren Namen.
            string remoteDir = null, mainStored = null;
            if (!string.IsNullOrEmpty(model.NasPath))
            {
                int cut = model.NasPath.LastIndexOfAny(new char[] { '/', '\\' });
                if (cut > 0)
                {
                    remoteDir = model.NasPath.Substring(0, cut);
                    mainStored = model.NasPath.Substring(cut + 1);
                }
            }
            for (int i = 1; i < total; i++) model.ExtraNames.Add(Path.GetFileName(locals[i]));

            // 5. Die Bilder für die Variantenliste. Sie sind eine Zugabe: Scheitert
            //    eines, liegt die Datei trotzdem da, und das Bild lässt sich in der
            //    Modellakte nachholen.
            if (total > 1)
            {
                Report(progress, "Vorschaubilder werden abgelegt …");
                if (mainPicturePng != null && mainStored != null &&
                    !await SetVariantPicture(model.Id, mainStored, mainPicturePng).ConfigureAwait(false))
                    model.PicturesFailed++;
                foreach (PrintFlowFile x in extras)
                {
                    if (x.PicturePng == null) continue;
                    if (!await SetVariantPicture(model.Id, Path.GetFileName(x.Path), x.PicturePng).ConfigureAwait(false))
                        model.PicturesFailed++;
                }
            }

            // 6. Jede abgelegte Datei gegen die lokale halten. Die Hauptdatei
            //    liefert ElasPrintFlow über die Nummer des Modells, die weiteren
            //    über ihren Pfad im Modellordner.
            for (int i = 0; i < total; i++)
            {
                string label = total == 1
                    ? "Ablage auf der NAS wird geprüft …"
                    : "Ablage auf der NAS wird geprüft (" + (i + 1) + " von " + total + ") …";
                Report(progress, label);

                string problem;
                if (i == 0)
                    problem = await Compare(new Uri(_base, "api/models/file?id=" + model.Id),
                                            locals[i], label, progress).ConfigureAwait(false);
                else if (remoteDir == null)
                    problem = "Der Ablageort ließ sich nicht nachlesen.";
                else
                    problem = await Compare(
                        new Uri(_base, "api/models/variant-file?path=" +
                                       Uri.EscapeDataString(remoteDir + "/" + Path.GetFileName(locals[i]))),
                        locals[i], label, progress).ConfigureAwait(false);

                if (problem != null)
                {
                    model.VerifyProblem = total == 1 ? problem : Path.GetFileName(locals[i]) + ": " + problem;
                    break;
                }
            }
            model.Verified = model.VerifyProblem == null;
            return model;
        }

        /// <summary>Legt das Bild einer Quelldatei neben sie (dort Name.vorschau.png).</summary>
        private async Task<bool> SetVariantPicture(int id, string storedName, byte[] png)
        {
            try
            {
                Reply r = await Send(HttpMethod.Post, "api/models/variants/bild",
                    JsonBody(Map("modelId", id, "dateiname", storedName,
                                 "bild", "data:image/png;base64," + Convert.ToBase64String(png))),
                    120, CancellationToken.None, false).ConfigureAwait(false);
                return r.Ok;
            }
            catch (PrintFlowException) { return false; }
        }

        private static void Collect(List<Dictionary<string, object>> into, IList models)
        {
            if (models == null) return;
            foreach (object m in models)
            {
                IList files = List(m as Dictionary<string, object>, "files");
                if (files == null) continue;
                foreach (object f in files)
                {
                    Dictionary<string, object> d = f as Dictionary<string, object>;
                    if (d != null && Str(d, "path") != null) into.Add(d);
                }
            }
        }

        private static string Where(string remoteFolder)
        {
            return string.IsNullOrEmpty(remoteFolder)
                ? ""
                : "\n\nDie hochgeladene Datei liegt in ElasPrintFlow unter\n" + remoteFolder;
        }

        private static void Report(IProgress<PrintFlowProgress> progress, string text)
        {
            if (progress == null) return;
            PrintFlowProgress p = new PrintFlowProgress();
            p.Text = text;
            progress.Report(p);
        }

        /// <summary>Räumt die eben hochgeladene Datei wieder weg. Gelingt es nicht, bleibt sie liegen.</summary>
        private async Task Discard(string remotePath)
        {
            if (string.IsNullOrEmpty(remotePath)) return;
            try
            {
                await Send(HttpMethod.Post, "api/models/quelldatei-loeschen",
                           JsonBody(Map("pfad", remotePath)), 30, CancellationToken.None, false)
                      .ConfigureAwait(false);
            }
            catch (PrintFlowException) { }
        }

        /// <summary>
        /// Holt die Datei so zurück, wie ElasPrintFlow sie unter dem Modell führt,
        /// und vergleicht sie Byte für Byte mit der lokalen. Erst danach darf die
        /// lokale verschwinden: geprüft wird nicht, dass etwas ankam, sondern dass
        /// genau das ankam.
        /// </summary>
        /// <returns>null, wenn beide gleich sind; sonst der Grund.</returns>
        private async Task<string> Compare(Uri source, string localPath, string label,
                                           IProgress<PrintFlowProgress> progress)
        {
            try
            {
                using (CancellationTokenSource limit = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
                using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, source))
                using (HttpResponseMessage res = await _http.SendAsync(
                           req, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false))
                {
                    if (!res.IsSuccessStatusCode)
                        return "ElasPrintFlow findet die abgelegte Datei nicht (Antwort " + (int)res.StatusCode + ").";

                    using (Stream remote = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream local = new FileStream(localPath, FileMode.Open, FileAccess.Read,
                                                             FileShare.Read, 1 << 16, true))
                    {
                        long total = local.Length, done = 0;
                        byte[] a = new byte[1 << 16], b = new byte[1 << 16];
                        int shown = -1;
                        while (true)
                        {
                            int n = await remote.ReadAsync(a, 0, a.Length, limit.Token).ConfigureAwait(false);
                            if (n == 0) break;

                            int got = 0;
                            while (got < n)
                            {
                                int m = await local.ReadAsync(b, got, n - got, limit.Token).ConfigureAwait(false);
                                if (m == 0) break;
                                got += m;
                            }
                            if (got < n) return "Die Datei auf der NAS ist größer als die lokale.";
                            for (int i = 0; i < n; i++)
                                if (a[i] != b[i]) return "Die Datei auf der NAS weicht von der lokalen ab.";

                            done += n;
                            int pct = total <= 0 ? 100 : (int)(done * 100 / total);
                            if (pct != shown)
                            {
                                shown = pct;
                                Report(progress, label + "  " + pct + " %");
                            }
                        }
                        if (done != total)
                            return "Die Datei auf der NAS ist kleiner als die lokale (" +
                                   done.ToString("N0", CultureInfo.CurrentCulture) + " von " +
                                   total.ToString("N0", CultureInfo.CurrentCulture) + " Bytes).";
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                return "Die Ablage ließ sich nicht prüfen: " + ex.Message;
            }
        }

        // ------------------------------------------------------------ Sitzung auf der Platte

        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("Elas3DView.PrintFlow.Sitzung");

        /// <summary>
        /// Die Sitzung liegt verschlüsselt in den Einstellungen — lesbar nur für
        /// dieses Windows-Konto. Wer die Datei kopiert, hat keinen Zugang kopiert.
        /// </summary>
        public static string Protect(string session)
        {
            if (string.IsNullOrEmpty(session)) return "";
            try
            {
                return Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(session), Salt, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(stored), Salt, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------ Hochladen

        /// <summary>
        /// Ein multipart/form-data-Körper von Hand: die Datei läuft als Strom durch
        /// (nicht erst ganz in den Speicher), der Fortschritt lässt sich melden,
        /// und der Dateiname geht als UTF-8 hinaus, so wie ein Browser ihn schickt.
        /// Der Baustein aus .NET würde Umlaute im Namen anders verpacken, als der
        /// Empfänger sie liest.
        /// </summary>
        private sealed class UploadContent : HttpContent
        {
            private readonly FileStream _file;
            private readonly byte[] _head, _tail;
            private readonly IProgress<PrintFlowProgress> _progress;
            private readonly CancellationToken _ct;
            private readonly string _label;

            /// <param name="label">Was der Nutzer liest; die Prozentzahl kommt dahinter.</param>
            public UploadContent(string path, string fileName, string folder, string label,
                                 IProgress<PrintFlowProgress> progress, CancellationToken ct)
            {
                _progress = progress;
                _ct = ct;
                _label = label;
                _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, true);

                string boundary = "----Elas3DView" + Guid.NewGuid().ToString("N");
                string safeName = fileName.Replace("\"", "%22").Replace("\r", "%0D").Replace("\n", "%0A");

                StringBuilder head = new StringBuilder();
                head.Append("--").Append(boundary).Append("\r\n");
                head.Append("Content-Disposition: form-data; name=\"ordner\"\r\n\r\n");
                head.Append(folder).Append("\r\n");
                head.Append("--").Append(boundary).Append("\r\n");
                head.Append("Content-Disposition: form-data; name=\"dateien\"; filename=\"")
                    .Append(safeName).Append("\"\r\n");
                head.Append("Content-Type: application/octet-stream\r\n\r\n");

                _head = Encoding.UTF8.GetBytes(head.ToString());
                _tail = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
                Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=" + boundary);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _head.Length + _file.Length + _tail.Length;
                return true;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                await stream.WriteAsync(_head, 0, _head.Length, _ct).ConfigureAwait(false);

                long total = _file.Length, sent = 0;
                byte[] buffer = new byte[1 << 16];
                int shown = -1;
                _file.Position = 0;
                while (true)
                {
                    int n = await _file.ReadAsync(buffer, 0, buffer.Length, _ct).ConfigureAwait(false);
                    if (n == 0) break;
                    await stream.WriteAsync(buffer, 0, n, _ct).ConfigureAwait(false);
                    sent += n;

                    int pct = total <= 0 ? 100 : (int)(sent * 100 / total);
                    if (pct != shown && _progress != null)
                    {
                        shown = pct;
                        PrintFlowProgress p = new PrintFlowProgress();
                        p.Text = _label + "  " + pct + " %";
                        p.CanCancel = true;
                        _progress.Report(p);
                    }
                }

                await stream.WriteAsync(_tail, 0, _tail.Length, _ct).ConfigureAwait(false);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _file.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
