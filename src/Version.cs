using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Dieselbe Zahl steht in den Dateieigenschaften der exe.
[assembly: AssemblyTitle("Elas3DView")]
[assembly: AssemblyProduct("Elas3DView")]
[assembly: AssemblyCompany("Elas3DStudio")]
[assembly: AssemblyVersion(Elas3DView.AppVersion.Current + ".0")]
[assembly: AssemblyFileVersion(Elas3DView.AppVersion.Current + ".0")]

namespace Elas3DView
{
    /// <summary>Eine Fassung und was sie gebracht hat.</summary>
    public class ReleaseNote
    {
        public readonly string Version;
        public readonly string Date;
        public readonly string[] Points;

        public ReleaseNote(string version, string date, params string[] points)
        {
            Version = version;
            Date = date;
            Points = points;
        }
    }

    /// <summary>
    /// Die Fassung der Anwendung — eine Zahl, eine Quelle, wie `src/lib/version.ts`
    /// in ElasPrintFlow. Sie steht oben im Kopfband, in den Dateieigenschaften der
    /// exe und im Setup (`setup.ps1` liest sie hier heraus).
    ///
    /// WOZU. Wer sie im Kopfband liest, sieht sofort, ob das neue Setup schon
    /// eingespielt ist. Steht dieselbe Zahl wie vorher, läuft noch die alte exe —
    /// dann hat es keinen Sinn, das Neue zu suchen.
    ///
    /// WIE GEZÄHLT WIRD. Wie drüben, nach Größe der Änderung:
    ///
    ///   MAJOR  2.x.x  Umbauten, die den Umgang mit der Anwendung ändern
    ///   MINOR  x.1.x  neue Fähigkeiten — ein Knopf, ein Ablauf, ein Format
    ///   PATCH  x.x.1  Behebungen und Gestaltung
    ///
    /// SIE WIRD BEI JEDER ÄNDERUNG HOCHGEZÄHLT, und jede Änderung bekommt oben in
    /// der Liste ihren Eintrag. Die Liste ist für den geschrieben, der das
    /// Programm benutzt: was er jetzt kann, nicht was im Quelltext geschah.
    /// </summary>
    public static class AppVersion
    {
        public const string Current = "1.4.0";

        /// <summary>Neueste zuerst. Der erste Eintrag gehört zu <see cref="Current"/>.</summary>
        public static readonly ReleaseNote[] Notes =
        {
            new ReleaseNote("1.4.0", "14.11.2026",
                "Neu: mehrere Dateien für EINE Modellakte. In der Dateiliste rechts anhaken, was " +
                    "zum geöffneten Modell gehört — bei „Ins PrintFlow …“ gehen diese STL und 3MF " +
                    "mit in seinen Ordner, ohne eigene Artikelnummer.",
                "Das geöffnete Modell ist die Hauptdatei. Das Übernahme-Fenster zeigt, welche " +
                    "Dateien in die Akte gehen.",
                "Jede weitere Datei bekommt ihr eigenes Vorschaubild. In ElasPrintFlow stehen sie " +
                    "unter „Quelldateien & Varianten“.",
                "Der Vergleich Byte für Byte und die Löschfrage gelten für alle Dateien der Akte."),

            new ReleaseNote("1.3.0", "14.11.2026",
                "Die Version steht oben im Kopfband. Ein Klick darauf zeigt, was sich geändert hat.",
                "Nach der Übernahme sind Artikelnummer und Ablageort Links: zum Modell in " +
                    "ElasPrintFlow und in den Ordner auf der NAS.",
                "Das Kopfband trägt die Hausschriften Manrope und Cormorant Garamond. Sie stecken " +
                    "in der exe und müssen nicht installiert sein.",
                "Setup und Windows-Einstellungen nennen dieselbe Versionsnummer."),

            new ReleaseNote("1.2.0", "14.11.2026",
                "Neu: „Ins PrintFlow …“ (Strg+P) legt das Modell in ElasPrintFlow an — mit Titelbild, " +
                    "Kategorie und Artikelnummer.",
                "Das Wasserzeichen im Titelbild ist bei jeder Übernahme wählbar. Das Titelbild zeigt " +
                    "die Ansicht samt ihrem Hintergrund.",
                "Die Datei auf der NAS wird Byte für Byte mit der lokalen verglichen. Erst danach " +
                    "kommt die Frage, ob sie hier endgültig gelöscht werden soll.",
                "Angemeldet wird einmal; gemerkt wird nur die Sitzung, nie das Passwort.",
                "Kopfband wie in ElasPrintFlow."),

            new ReleaseNote("1.1.0", "14.11.2026",
                "Setup: eine einzelne Datei zum Installieren, Aktualisieren und Entfernen, ohne Adminrechte.",
                "Verknüpfungen auf dem Desktop und im Startmenü; STL und 3MF unter „Öffnen mit“."),

            new ReleaseNote("1.0.1", "06.09.2026",
                "Die Elas-Logos sind eingebaut und stehen beim ersten Start in der Auswahl."),

            new ReleaseNote("1.0.0", "06.09.2026",
                "STL- und 3MF-Betrachter mit Vorschaubild als JPG, einzeln oder für den ganzen Ordner.",
                "Logo, Hintergründe, Modellfarbe und Oberfläche; helles und dunkles Design.",
                "Umbenennen und Löschen nehmen das zugehörige Bild mit.")
        };
    }

    /// <summary>Was sich geändert hat — die Liste aus <see cref="AppVersion.Notes"/>, neueste oben.</summary>
    public class ReleaseNotesDialog : PanelDialog
    {
        public ReleaseNotesDialog()
            : base("Neuerungen", "Elas3DView  ·  Version " + AppVersion.Current, 560)
        {
            StackPanel list = new StackPanel();
            list.Margin = new Thickness(0, 0, 10, 0);

            for (int i = 0; i < AppVersion.Notes.Length; i++)
            {
                ReleaseNote note = AppVersion.Notes[i];

                if (i > 0)
                {
                    Border line = new Border();
                    line.Height = 1;
                    line.Margin = new Thickness(0, 14, 0, 14);
                    line.SetResourceReference(Border.BackgroundProperty, "Divider");
                    list.Children.Add(line);
                }

                StackPanel head = new StackPanel();
                head.Orientation = Orientation.Horizontal;
                head.Margin = new Thickness(0, 0, 0, 7);

                TextBlock number = new TextBlock();
                number.Text = note.Version;
                number.FontFamily = new FontFamily(Theme.NumFont);
                number.FontSize = 17;
                number.VerticalAlignment = VerticalAlignment.Bottom;
                number.SetResourceReference(TextBlock.ForegroundProperty, i == 0 ? "GoldText" : "TextDim");
                head.Children.Add(number);

                TextBlock date = new TextBlock();
                date.Text = note.Date + (i == 0 ? "  ·  diese Fassung" : "");
                date.FontSize = 11.5;
                date.Margin = new Thickness(11, 0, 0, 2);
                date.VerticalAlignment = VerticalAlignment.Bottom;
                date.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                head.Children.Add(date);
                list.Children.Add(head);

                foreach (string point in note.Points)
                {
                    DockPanel row = new DockPanel();
                    row.Margin = new Thickness(0, 0, 0, 5);

                    TextBlock dot = new TextBlock();
                    dot.Text = "•";
                    dot.Width = 16;
                    dot.FontSize = 12.5;
                    dot.SetResourceReference(TextBlock.ForegroundProperty, "Gold");
                    DockPanel.SetDock(dot, Dock.Left);
                    row.Children.Add(dot);

                    TextBlock text = new TextBlock();
                    text.Text = point;
                    text.FontSize = 12.5;
                    text.LineHeight = 18.5;
                    text.TextWrapping = TextWrapping.Wrap;
                    text.SetResourceReference(TextBlock.ForegroundProperty, "TextDim");
                    row.Children.Add(text);

                    list.Children.Add(row);
                }
            }

            ScrollViewer scroll = new ScrollViewer();
            scroll.MaxHeight = 430;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Focusable = false;
            scroll.Content = list;
            Root.Children.Add(scroll);

            Button close = MakeButton("Schließen", true);
            close.IsDefault = true;
            close.IsCancel = true;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 18, 0, 0);
            Root.Children.Add(close);
        }
    }
}
