# Elas3DView

Windows-Anwendung, um STL- und 3MF-Modelle anzusehen, daraus Vorschaubilder zu
erzeugen und die Dateien im Ordner in Ordnung zu halten — der Schreibtisch-Teil
neben der Web-Werkbank [ElasPrintFlow](#zusammenspiel-mit-elasprintflow).

**Programm:** `bin\Elas3DView.exe` — eine einzelne Datei, rund 334 kB (mit den
eingebetteten Logos), keine Installation, nichts nachzuinstallieren. Sie läuft
auf dem .NET Framework, das in Windows enthalten ist.

> **Frisch geklont?** Dann fehlt `bin\` — es steht in der `.gitignore`, weil es
> Bauergebnis ist. Einmal **`build.cmd`** doppelklicken; das dauert ein paar
> Sekunden und braucht nichts weiter als Windows selbst. Gebaut wird mit dem
> C#-Compiler, der zum .NET Framework gehört und auf jedem Windows schon liegt —
> kein SDK, kein Visual Studio, kein Download.

---

## Starten

Doppelklick auf `bin\Elas3DView.exe`.

Einmalig `Verknuepfung-anlegen.cmd` ausführen legt eine Verknüpfung auf dem
Desktop und im Startmenü an. Der Ordner lässt sich als Ganzes verschieben oder
auf einen anderen Rechner kopieren.

### Setup für andere Rechner

**`setup.cmd`** baut `dist\Elas3DView-Setup.exe` — eine einzelne Datei, rund
600 kB, die Anwendung steckt darin. Vorher wird die Anwendung frisch gebaut.

- Installiert für den angemeldeten Benutzer nach
  `%LOCALAPPDATA%\Programs\Elas3DView`, **ohne Administratorrechte**. Unter
  `C:\Programme` ginge es nicht: Die Anwendung schreibt neben sich in
  `logos\` und `hintergruende\`.
- Legt Startmenü- und (wahlweise) Desktop-Verknüpfung an und bietet STL und
  3MF unter „Öffnen mit" an. Die Standard-App für die Endungen bleibt, wie sie ist.
- Steht danach unter *Einstellungen → Apps* und lässt sich dort entfernen.
  Eigene Bilder in `logos\`/`hintergruende\` werden dabei nur auf Nachfrage
  gelöscht; die Einstellungen in `%APPDATA%\Elas3DView` bleiben.
- Ein zweites Setup über ein vorhandenes heißt „Aktualisieren" und behält die
  Haken vom letzten Mal.
- Still: `Elas3DView-Setup.exe /silent` bzw. `Deinstallieren.exe /uninstall /silent`.

Das Setup ist nicht signiert. Beim ersten Start aus einem Download meldet
Windows SmartScreen „Unbekannter Herausgeber" → *Weitere Informationen* →
*Trotzdem ausführen*.

---

## Bedienung

### Öffnen

| Weg | Beschreibung |
|---|---|
| **Öffnen …** | Einzelne Datei wählen (Strg+O) |
| **Ordner …** | Ganzen Ordner öffnen (Strg+Umschalt+O) |
| Ziehen & Ablegen | Datei *oder* Ordner ins Fenster ziehen |
| Kommandozeile | `Elas3DView.exe "C:\Pfad\teil.stl"` |

Links stehen die Dateien des Ordners. Ein Klick lädt das Modell, `Bild auf` /
`Bild ab` blättern durch die Liste.

### Modelle, Bilder oder beides

Über der Liste stehen drei Schalter: **Modelle**, **Bilder**, **Alle**. Sie
entscheiden, was die Liste zeigt — nur STL und 3MF, nur die Bilddateien des
Ordners (JPG, JPEG, PNG, BMP, GIF, TIF), oder alles zusammen. Ein Klick auf eine
Bilddatei zeigt sie an Stelle der 3D-Ansicht; die Statuszeile nennt dann
Bildgröße und Dateigröße.

**Bild zeigen** in der Ansichtsleiste (oder die Taste `B`) springt direkt vom
geladenen Modell zu seinem gleichnamigen Vorschaubild und wieder zurück. Der
Schalter ist gesperrt, solange kein Bild danebenliegt.

### Rechte Maustaste

Ein Rechtsklick — **in der Dateiliste oder mitten in der 3D-Ansicht** — öffnet
das Kontextmenü mit *JPG speichern*, *Umbenennen*, *Im Explorer zeigen* und
*Löschen*. In der Ansicht öffnet es sich nur, wenn nicht gezogen wurde; ein
Rechts-Ziehen verschiebt weiterhin das Modell.

### Ansicht

* **Linke Maustaste ziehen** – drehen
* **Rechte oder mittlere Maustaste ziehen** – verschieben
* **Mausrad** – zoomen
* Schaltflächen `Iso` bis `Unten`, auch über die Tasten `1`–`7`
* `Einpassen` bzw. Leertaste – Modell formatfüllend zeigen
* `Orthogonal` – Parallelprojektion statt Perspektive

### Farbe und Oberfläche

**Modellfarbe** öffnet eine Palette mit 42 Farbtönen — obenauf die sechs Farben
der Elas-Blüte, darunter Neutraltöne, Filamentfarben und Metallictöne. Darunter
liegen die zuletzt benutzten Farben und **Eigene Farbe …** für den vollen
Farbwähler. Die Auswahl wird gemerkt.

**Oberfläche** wechselt zwischen *Matt*, *Seidenmatt*, *Glänzend* und
*Metallic* — unterschiedlicher Glanz, bei Metallic zusätzlich ein
zurückgenommener Grundton.

### Hintergrund

**Hintergrund** wählt die Fläche hinter dem Modell: die schraffierte Vorschau des
Designs, einfarbige und verlaufende Voreinstellungen — oder ein **eigenes Bild**.
Bilder kommen aus dem Ordner **`bin\hintergruende`**, den der Schalter
*Hintergründe* öffnet; beim ersten Start legt die Anwendung ihn samt kurzem
Hinweis an. Was dort liegt (PNG, JPG, BMP, GIF, TIF), steht nach `F5` oder dem
nächsten Start in der Auswahl. Das Bild füllt die Fläche, ohne verzerrt zu
werden; was seitlich übersteht, wird beschnitten.

Damit der Hintergrund **im gespeicherten JPG landet**, muss *Bildgrund* auf
*Wie Ansicht* stehen — sonst bleibt es beim weißen Produktbildgrund. Die
Schraffur wird dabei passend mitskaliert, damit das Bild aussieht wie der
Bildschirm und nicht feiner gerastert.

### Logo aufs Bild setzen

**Logo** wählt aus, was auf dem Bild liegt — mitgeliefert sind das bunte Elas-Logo
und eine Graustufen-Fassung, `Kein Logo` lässt es weg. **Größe** stellt die Breite
von 4 bis 45 Prozent der Bildbreite ein, das Neun-Felder-Raster daneben die Ecke
oder Kante. Was in der Ansicht liegt, liegt genauso im gespeicherten JPG — beides
rechnet mit demselben Prozentsatz.

Vor dem Speichern prüft die Anwendung, **ob das Logo auf dem Bildgrund
überhaupt zu sehen wäre**. Gemessen wird der Grund genau an der Stelle, an der
das Logo sitzt — dafür wird er allein klein gerendert, was für Farbe, Verlauf,
Schraffur und Hintergrundbild gleichermaßen funktioniert. Ist das Logo nahezu
einfarbig und hebt es sich kaum ab, kommt eine Rückfrage mit den gemessenen
Helligkeiten; man kann trotzdem speichern. Ein buntes Logo bringt genug eigenen
Kontrast mit und wird nie gemeldet.

> Anlass war das Negativ-Logo von Elas: reines Weiß auf durchsichtig. Auf
> dunklem Grund sieht es hervorragend aus, auf dem weißen Produktbildgrund
> verschwindet es spurlos — das JPG war auf das Byte genau so groß wie eines
> ganz ohne Logo.

Die Logos sind Dateien im Ordner **`bin\logos`**; der Schalter *Logos*
öffnet ihn. Wer dort ein weiteres PNG ablegt, findet es nach `F5` oder dem
nächsten Start in der Auswahl; wer eines löscht, ist es los. Beim allerersten
Start legt die Anwendung den Ordner an und schreibt die beiden mitgelieferten
Logos hinein — danach fasst sie ihn nicht mehr an.

### Vorschaubild erzeugen

**JPG speichern** (Strg+S) legt die aktuelle Ansicht als `<Modellname>.jpg`
**im selben Ordner wie das Modell** ab — genau dort, wo ElasPrintFlow das
Vorschaubild erwartet.

* **JPG** wählt die Breite (1280 – 3840 px); die Höhe folgt dem Seitenverhältnis.
* **Bildgrund** ist auf **Weiß** voreingestellt. Das ist bewusst so: „Weiß ist
  das, was ein Produktbild braucht — der Shop zeigt es auf hellen Flächen,
  Marktplätze erwarten es so." Auf dem Bildschirm bleibt die gewählte
  Hintergrundfläche, im Bild landet Weiß. *Wie Ansicht* nimmt stattdessen genau
  das mit, was hinter dem Modell zu sehen ist — Schraffur, Verlauf oder eigenes
  Hintergrundbild.
* Gerendert wird mit doppelter Auflösung und anschließender Verkleinerung —
  saubere Kanten ohne Treppchen.
* Existiert die Datei schon, wird gefragt: überschreiben, danebenlegen
  (`name_1.jpg`) oder abbrechen.

**Alle als JPG …** erzeugt in einem Durchlauf für jede Datei des Ordners ein
Bild in der aktuellen Ausrichtung. Der Vorgang zeigt den Fortschritt und lässt
sich abbrechen.

### Umbenennen

**Umbenennen** (F2). Die Endung bleibt erhalten, unzulässige Zeichen und schon
vergebene Namen werden abgefangen. Liegt ein gleichnamiges Bild daneben
(`.jpg`, `.jpeg`, `.png`), wird es auf Wunsch mit umbenannt — Modell und Bild
bleiben zusammen.

### Löschen

**Löschen** (Entf) räumt das Modell **und das zugehörige Bild** weg. Vorher
zeigt eine Rückfrage genau auf, was verschwindet. Alles wandert in den
**Papierkorb** und lässt sich von dort zurückholen — nichts wird endgültig
gelöscht. Danach rückt der nächste Eintrag der Liste nach.

### Ins PrintFlow übernehmen

**Ins PrintFlow …** (Strg+P) legt das geladene Modell in ElasPrintFlow an und
räumt es danach von diesem Rechner. Ein Knopf für den ganzen Weg:

1. **Anmelden** — nur beim ersten Mal und wenn die Sitzung abgelaufen ist
   (ElasPrintFlow hält sie sieben Tage). Gemerkt werden Adresse, Benutzername
   und die Sitzung, **nie das Passwort**. Die Sitzung liegt verschlüsselt in den
   Einstellungen und ist nur für dieses Windows-Konto lesbar.
2. **Angaben** — Name (vorbelegt aus dem Dateinamen), Kategorie aus der Liste
   von ElasPrintFlow und das **Wasserzeichen**: eines der Logos oder keines. Der
   Dialog zeigt das Titelbild genau so, wie es hinausgeht — die Ansicht, wie sie
   gerade steht, **mit ihrem Hintergrund**, höchstens 1920 px breit. *Bildgrund*
   gilt hier nicht: Der steht für *JPG speichern*, das Titelbild nimmt immer,
   was hinter dem Modell zu sehen ist. Die Rückfrage, ob das Logo zu sehen
   wäre, misst deshalb ebenfalls den Hintergrund der Ansicht.
3. **Übernahme** — die Datei wird hochgeladen; ElasPrintFlow verschiebt sie nach
   `<Kategorie>\<Name>\` auf der NAS, legt das Titelbild daneben und vergibt die
   Artikelnummer.
4. **Prüfung** — die abgelegte Datei wird zurückgelesen und **Byte für Byte**
   mit der lokalen verglichen.
5. **Rückfrage** — erst jetzt, mit Artikelnummer und Ablageort: *Endgültig
   löschen* oder *Behalten*. Gelöscht werden die Modelldatei und ein
   gleichnamiges Bild daneben, **am Papierkorb vorbei** — der Sinn ist, dass die
   Platte frei wird und das Modell nur noch auf der NAS liegt. Der Ordner bleibt.
   In diesem Fenster sind **Artikelnummer und Ablageort Links**: Die Nummer
   öffnet das Modell in ElasPrintFlow, der Pfad den Ordner auf der NAS im
   Explorer. Das Fenster bleibt dabei offen.

Was dabei nicht passieren kann: Scheitert irgendein Schritt, oder stimmt der
Vergleich nicht, kommt die Löschfrage gar nicht erst. Auf dem Rechner bleibt
dann alles, wie es war.

#### Mehrere Dateien für eine Modellakte

Ein Modell besteht oft aus mehr als einer Datei — drei Entwürfe derselben
Lichtbox, ein Deckel zum Topf. Solche Dateien gehören **in eine Akte**, nicht in
drei: eine Artikelnummer, ein Ordner, die übrigen Dateien daneben.

* **Anhaken.** Jede Modelldatei der Liste trägt rechts ein Kästchen. Das
  **geöffnete** Modell ist die Hauptdatei; angehakt wird, was dazugehört. Das
  Kästchen wählt nicht aus — ein Klick darauf lädt das Modell nicht, sonst
  wechselte mit jedem Haken die Hauptdatei. Unter der Liste steht, wie viele
  angehakt sind; *Häkchen aufheben* im Kontextmenü nimmt alle zurück. Die Haken
  gelten für den Ordner, in dem sie gesetzt wurden.
* **Übernehmen.** *Ins PrintFlow …* zeigt unter dem Titelbild die **Dateien
  dieser Akte**: die Hauptdatei und, mit „dazu", die angehakten. Die Hauptdatei
  heißt drüben wie das Modell, die übrigen behalten ihren Namen. Trüge eine von
  ihnen schon genau diesen Namen, wird vor dem Hochladen nachgefragt.
* **Bilder.** Jede weitere Datei bekommt ihr eigenes Vorschaubild, in der
  Blickrichtung, die gerade steht, ohne Logo. Dafür wird jede einmal geladen —
  wie bei *Alle als JPG*; danach steht wieder die Hauptdatei in der Ansicht. In
  ElasPrintFlow stehen die Dateien unter **Quelldateien & Varianten**, dort
  liegt das Bild als `<Datei>.vorschau.png` daneben.
* **Prüfung und Aufräumen** gelten für **alle** Dateien: Jede wird Byte für Byte
  verglichen, und die Löschfrage nennt jede einzeln samt gleichnamigem Bild.
  Stimmt eine nicht, bleibt alles auf diesem Rechner.

Kein eigener Weg in ElasPrintFlow: Die Dateien reisen einzeln nach „Vom Rechner"
(jede bis 300 MB) und werden mit **einem** Aufruf als ein Modell angelegt — der
Import dort nimmt seit jeher mehrere Dateien mit einer Hauptdatei an.

Grenzen:

* Nur beim **Anlegen**. An eine Akte, die es in ElasPrintFlow schon gibt, hängt
  Elas3DView nichts an — das geht dort unter „Quelldateien & Varianten".
* Die Hauptdatei ist immer das geöffnete Modell. Soll es eine andere sein:
  dieses Modell öffnen, das bisherige anhaken. Nachträglich lässt sie sich in
  der Modellakte wechseln.
* Die weiteren Dateien sind **Ablage**, keine Ausführungen für den Laden: kein
  eigener Preis, keine eigene Größe.
* Höchstens 300 MB je Datei — mehr nimmt ElasPrintFlow in einem Stück nicht an.
* Ein schon vorhandenes JPG neben dem Modell wird nicht mit hochgeladen; das
  Titelbild entsteht bei der Übernahme neu. Gelöscht wird das JPG trotzdem mit.
* Gibt es den Namen in der Kategorie schon, wird vor dem Hochladen abgelehnt.
  Groß- und Kleinschreibung zählen dabei nicht: auf der Freigabe wäre es
  derselbe Ordner.

### Design

Oben liegt das **Kopfband**, wie ElasPrintFlow es trägt: Bildmarke, der Name in
zwei Gewichten — „Elas" leicht, „3DView" fett, ohne Lücke —, darunter der
Leitsatz, unten eine Goldlinie, die an beiden Enden ausläuft. Maße und Farben
stehen wörtlich in `Titelleiste.tsx` von ElasPrintFlow. Das Band bleibt dunkel,
auch im hellen Design; die Bildmarke liegt als `assets\Kopfband-Marke.png` in
der exe und ist kein Wasserzeichen.

Rechts im Kopfband schaltet **Helles Design / Dunkles Design** um. Die Wahl wird
gemerkt, die Fensterleiste zieht mit.

### Version und Neuerungen

Links daneben steht die **Version** (etwa `v1.3.0`). Nach einem Setup zeigt ein
Blick, ob wirklich das Neue läuft. Ein Klick darauf öffnet die **Neuerungen**:
jede Fassung mit Datum und dem, was sie für die Bedienung gebracht hat, die
neueste oben.

Zahl und Liste stehen an einer Stelle, `src\Version.cs`. Dieselbe Zahl tragen
die Dateieigenschaften der exe, das Setup und der Eintrag unter *Installierte
Apps*; `setup.ps1` liest sie dort heraus. **Bei jeder Änderung** wird sie
hochgezählt (gezählt wie in ElasPrintFlow: MAJOR Umbau, MINOR neue Fähigkeit,
PATCH Behebung oder Gestaltung) und bekommt oben in der Liste ihren Eintrag —
geschrieben für den, der das Programm benutzt.

### Tastenkürzel

| Taste | Funktion |
|---|---|
| Strg+O / Strg+Umschalt+O | Datei / Ordner öffnen |
| Strg+S | JPG speichern |
| Strg+P | Ins PrintFlow übernehmen |
| B | zwischen Modell und Vorschaubild wechseln |
| F2 | Umbenennen |
| Entf | Löschen (Papierkorb) |
| F5 | Datei- und Logoliste aktualisieren |
| Leertaste | Einpassen |
| 1 – 7 | Iso, Vorne, Hinten, Links, Rechts, Oben, Unten |
| Bild auf / Bild ab | vorheriges / nächstes Modell |

---

## Zusammenspiel mit ElasPrintFlow

Das Werkzeug ist auf die Konventionen der Werkbank abgestimmt:

* **Das Vorschaubild liegt im Modellordner**, nicht in einem zentralen
  Bildordner. In ElasPrintFlow gibt es keine Bildtabelle — was im Ordner der
  STL liegt, *ist* die Galerie des Modells. Ein hier erzeugtes JPG taucht dort
  also ohne weiteres Zutun als Kachel auf.
* **Produktbilder auf Weiß**, siehe oben.
* **Die Übernahme geht durch ElasPrintFlow, nicht an ihm vorbei.** Dieselben
  drei Aufrufe wie die Weboberfläche und der ElasSTLsortierer
  (`hochladen-lokal`, `scan-legacy`, `execute-batch-import`). Die Artikelnummer
  entsteht aus der laufenden Modell-ID; nur ElasPrintFlow kann sie vergeben.
  Direkt auf die Freigabe zu schreiben ergäbe Dateien, die dort niemand kennt.
* **Löschen greift nur die zugeordneten Dateien an**, nie den Ordner. In
  Sammelordnern wie `…\Etsy\Bestellung Nr. …\` liegen hunderte Dateien
  nebeneinander; ein Ordnerlöschen hätte dort ganze Bestellungen abgeräumt.
* **Alle Meldungen sind Popups**, keine stillen Statuszeilen — die projektweite
  Vorgabe aus `PROJECT_UI_INSTRUCTIONS.md`. Erfolgsmeldungen erscheinen als
  kurz eingeblendetes Popup, Rückfragen und Fehler als Dialog mit klarem
  Bestätigen/Abbrechen und Escape zum Schließen.

### Gestaltung

Farben und Formen folgen dem Design-Handoff, den ElasPrintFlow seit dem
12.08.2026 trägt: warmes Dunkelbraun, Gold als Akzent, Anwendungsgrund
`#17141C`, Panel `#1C1822`, Gold `#D39A39`. Übernommen sind auch die drei
Regeln des Handoffs:

* keine Schatten und kein Glas — Erhebung entsteht durch Kante plus Flächenwechsel
* Schaltflächen sind nie gefüllt; primär und sekundär trennen nur die goldene
  Kante und das Schriftgewicht
* Radien 8–12 px, Pillen 999 px

Der aktive Eintrag der Dateiliste trägt die 2 px starke Goldkante links, wie die
Seitenleiste der Werkbank. Die Vorschaufläche ist schraffiert.

**Die Hausschriften sind eingebettet — bisher nur für das Kopfband.** Manrope
(300 und 700) und Cormorant Garamond (400 kursiv) liegen unter
`assets\schriften` und wandern beim Bau als Ressource in die exe; es sind
dieselben Schnitte, die ElasPrintFlow von Google Fonts lädt. WPF kann eine
Schrift nur aus einer Datei laden, deshalb schreibt `src\BrandFonts.cs` sie
beim ersten Gebrauch nach `%TEMP%\Elas3DView\schriften`. Der Name im Kopfband
steht damit in Manrope, 26 px in zwei Gewichten und mit der Laufweite von drüben
(`TrackedWord` — ein WPF-`TextBlock` kennt keine), der Leitsatz in Cormorant
Garamond. Beide Schriften stehen unter der SIL Open Font License; die Texte
liegen neben den Dateien.

**Abweichung, die bleibt:** Die übrige Oberfläche steht weiter in **Segoe UI**
und — für Kennzahlen, wie dort Cormorant — in **Constantia**, der einzigen
mitgelieferten Serifenschrift mit Mediävalziffern. Dafür fehlen die Schnitte
400 und 600 von Manrope und der aufrechte von Cormorant Garamond; lägen sie
unter `assets\schriften`, sind es zwei Zeilen in `src/Theme.cs`. Lassen sich
die eingebetteten Schriften nicht laden, fällt auch das Kopfband auf Segoe UI
und Constantia zurück.

---

## Unterstützte Formate

* **STL** – binär und ASCII. Fehlerhafte Dreieckszähler im Kopf werden toleriert.
* **3MF** – Kernformat mit `<build>`-Elementen, verschachtelten Komponenten samt
  Transformationsmatrizen und allen Einheiten (µm bis m, umgerechnet auf
  Millimeter). Auch 3MF-Dateien, deren Objekte in eigene `.model`-Teildateien
  ausgelagert sind (Production-Extension, üblich bei Bambu Studio und
  OrcaSlicer), werden gelesen.

## Große Modelle

Gemessen am Bestand: „Deko Ei Floral Ornamente 1.stl", 80 MB mit 1,68 Mio.
Dreiecken, lädt in **181 ms** und belegt **115 MB**. Ein JPG mit 1920 px Breite
daraus dauert rund 4,6 Sekunden — währenddessen zeigt die Anwendung einen
Hinweis, statt einfach stehenzubleiben.

Zwei Dinge waren dafür nötig; beide ändern **nichts** am erzeugten Bild
(nachgemessen: Pixel für Pixel identisch, das JPG sogar Byte für Byte gleich groß):

**Die Geometrie trägt nur noch Positionen.** Vorher legte der Lader zusätzlich
eine Normalen- und eine Indexliste an — zusammen mehr Speicher als die
Positionen selbst. Ohne Indexliste liest WPF die Positionen als fortlaufende
Dreiecksliste, ohne Normalen rechnet es sie selbst aus; da jedes Dreieck eigene
Eckpunkte hat, kommen dieselben flächigen Normalen heraus. Dazu liest der Lader
binäre STL satzweise aus dem Datenstrom statt die Datei erst vollständig in den
Speicher zu holen.

**Abgelegte Modelle werden wirklich freigegeben.** Ein fallengelassenes Netz
liegt im Bereich für große Felder, und den räumt die Laufzeit von sich aus
selten auf. Beim Durchblättern mehrerer großer Modelle wuchs der Verbrauch
deshalb immer weiter. Jetzt wird das alte Netz vor dem Laden losgelassen und
oberhalb von 400 MB verdichtend aufgeräumt.

| | vorher | jetzt |
| --- | --- | --- |
| ein Modell mit 1,68 Mio. Dreiecken | 744 MB | **516 MB** |
| vier große Modelle nacheinander, Spitze | 1.131 MB | **547 MB** |
| Ladezeit desselben Modells | 368 ms | **181 ms** |

Das Durchblättern wuchs vorher von 447 über 1.034 auf 942 MB und weiter; jetzt
bleibt es bei dem, was ein Modell braucht.

---

## Als Standardprogramm hinterlegen (optional)

Rechtsklick auf eine `.stl`-Datei → *Öffnen mit* → *Andere App auswählen* →
*Auf dem PC nach einer anderen App suchen* → `bin\Elas3DView.exe` wählen und
*Immer diese App verwenden* ankreuzen. Dasselbe für `.3mf`.

---

## Neu bauen

`build.cmd` ausführen. Das Skript kompiliert alle Dateien aus `src\` mit
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, erzeugt dabei das
Programmsymbol und schreibt `bin\Elas3DView.exe`. Die Anwendung darf dabei nicht
laufen.

Tests: `powershell -ExecutionPolicy Bypass -File run-tests.ps1`. Geprüft werden
Dreiecksanzahl, Abmessungen und Volumen gegen Sollwerte (Würfel als binäres und
ASCII-STL, 3MF in Millimeter und Zentimeter, 3MF mit Komponenten und
Transformationen, Kugel mit 76 800 Dreiecken), dass geladene Dateien nicht
gesperrt bleiben, dass Löschen im Papierkorb landet, und der Renderpfad des
JPG-Exports. Echte Modelle lassen sich mitgeben:

```bash
powershell -ExecutionPolicy Bypass -File run-tests.ps1 "C:\Pfad\grosses-modell.stl"
```

Die Dialoge der Übernahme lassen sich ohne Bildschirm prüfen. Der versteckte
Schalter lädt ein Modell, zeichnet das Titelbild so, wie es an ElasPrintFlow
ginge, und legt es mit den Dialogen als PNG ab — nichts erscheint, nichts geht
ins Netz. Aus PowerShell aufrufen; die Git-Bash verbiegt `/dialogbilder` zu
einem Pfad, und dann startet das Programm ganz normal.

```powershell
bin\Elas3DView.exe /dialogbilder "C:\Pfad\modell.stl" "C:\Pfad\zielordner"
```

---

## Aufbau

```
src\MeshLoader.cs    STL- und 3MF-Leser, Geometrie, Volumen und Maße
src\Logo.cs          Bildordner für Logos und Hintergründe, Neun-Felder-Raster
src\Contrast.cs      Helligkeitsmessung: würde das Logo auf dem Grund verschwinden?
src\Theme.cs         Farbtokens, Steuerelement-Stile, helles/dunkles Design
src\AppDialog.cs     Popups für Meldungen und Rückfragen
src\ColorPicker.cs   Farbpalette mit freier Farbwahl
src\MainWindow.cs    Oberfläche, Kamera, Beleuchtung, JPG, Umbenennen, Löschen
src\Dialogs.cs       Umbenennen-Dialog und Programmeinstieg
src\PrintFlow.cs     Übergabe an ElasPrintFlow: Anmeldung, Hochladen, Anlegen, Byte-Vergleich
src\PrintFlowDialogs.cs  Anmeldung und Übernahme-Dialog, Prüfung ohne Bildschirm
src\Version.cs       die Version, die Liste der Neuerungen und ihr Fenster
tests\SelfTest.cs    Testfälle samt Erzeugung der Testmodelle
tests\UiShot.cs      startet das echte Fenster und prüft Design und JPG-Schalter
build.ps1            Symbol erzeugen und kompilieren
```

Einstellungen (Ordner, Listenart, Design, Farbe, Oberfläche, Hintergrund samt
eigenem Bild, Bildgrund, JPG-Breite, Logo mit Größe und Position, Blickrichtung,
zuletzt benutzte Farben, dazu Adresse, Benutzername, verschlüsselte Sitzung und
zuletzt gewählte Kategorie für ElasPrintFlow) liegen in
`%APPDATA%\Elas3DView\settings.txt`.

Die mitgelieferten Logos liegen als Ressource in der exe und stammen aus
`assets\`; `build.ps1` bettet jede Datei dieses Ordners automatisch ein.
