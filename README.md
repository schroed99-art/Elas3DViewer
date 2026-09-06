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

### Design

Rechts oben schaltet **Helles Design / Dunkles Design** um. Die Wahl wird
gemerkt, die Fensterleiste zieht mit.

### Tastenkürzel

| Taste | Funktion |
|---|---|
| Strg+O / Strg+Umschalt+O | Datei / Ordner öffnen |
| Strg+S | JPG speichern |
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

**Abweichung:** Manrope und Cormorant Garamond sind auf diesem Rechner nicht
vorhanden und lassen sich ohne Schriftdateien nicht laden. An ihrer Stelle
stehen **Segoe UI** und — für Kennzahlen, wie dort Cormorant — **Constantia**,
die einzige mitgelieferte Serifenschrift mit Mediävalziffern. Liegen die
Schriftdateien einmal vor, sind es zwei Zeilen in `src/Theme.cs`.

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
tests\SelfTest.cs    Testfälle samt Erzeugung der Testmodelle
tests\UiShot.cs      startet das echte Fenster und prüft Design und JPG-Schalter
build.ps1            Symbol erzeugen und kompilieren
```

Einstellungen (Ordner, Listenart, Design, Farbe, Oberfläche, Hintergrund samt
eigenem Bild, Bildgrund, JPG-Breite, Logo mit Größe und Position, Blickrichtung,
zuletzt benutzte Farben) liegen in `%APPDATA%\Elas3DView\settings.txt`.

Die mitgelieferten Logos liegen als Ressource in der exe und stammen aus
`assets\`; `build.ps1` bettet jede Datei dieses Ordners automatisch ein.
