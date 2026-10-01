namespace tcmatch.Core
{
    public static class TranslationDE
    {
        public static readonly string languageNameEnglish = "German";
        public static readonly string languageNameLocal = "Deutsch";
        public static readonly string languageIsoCode = "de";
        public static readonly string readmeVersion = "2026-10-01"; // #RELEASE

        public static readonly string rawData = @"
001_001_001=Konfiguration

001_002_001=Standardwerte
001_002_002=Abbrechen
001_002_003=Speichern & Schließen
001_002_004=Standardwerte laden?
001_002_005=Möchtest du wirklich alle Einstellungen dieses Fensters auf die Standardwerte zurücksetzen? Ungespeicherte Änderungen gehen dabei verloren.
001_002_006=Änderungen verwerfen?
001_002_007=Es gibt ungespeicherte Änderungen. Möchtest du diese Änderungen verwerfen?
001_002_008=Ja
001_002_009=Nein
001_002_100=Zeichen für globale ODER-Verknüpfung
001_002_101=Zeichen für UND-Verknüpfung
001_002_102=Zeichen für Negation
001_002_103=Zeichen zum Umschalten der Groß-/Kleinschreibungs-Beachtung
001_002_104=Zeichen um Treffer am Textanfang zu erzwingen
001_002_105=Zeichen um Treffer am Textende zu erzwingen
001_002_106=Zeichen für lokale ODER-Verknüpfung
001_002_107=Zeichen für Metadaten
001_002_108=Zeichen zur Maskierung
001_002_109=Dezimaltrennzeichen
001_002_110=Zeichen für die Standard-Suche
001_002_111=Zeichen für die Regex-Suche
001_002_112=Zeichen für die Fuzzy-Suche
001_002_113=Zeichen für die Sequenz-Suche
001_002_114=Zeichen für Textblöcke
001_002_115=Zeichen für die Mustersuche
001_002_200=Metadaten: GUI
001_002_201=Metadaten: Name
001_002_202=Metadaten: Erweiterung
001_002_203=Metadaten: Ordner
001_002_204=Metadaten: Pfad
001_002_205=Metadaten: Beschreibung
001_002_206=Metadaten: Inhalt
001_002_207=Metadaten: Alter
001_002_208=Metadaten: Größe
001_002_209=Metadaten: Attribute
001_002_290=Metadaten: WDX - {0} - {1}
001_002_303=Suchassistent-Fenster - Größe: Die Fensterbreite ist zu gering (Minimum {0}px).
001_002_304=Suchassistent-Fenster - Größe: Die Fensterhöhe ist zu gering (Minimum {0}px).
001_002_305=Sonderzeichen: Das Dezimaltrennzeichen darf nicht leer sein.
001_002_306=Sonderzeichen: Zeichen doppelt vergeben: ""{0}"" bei ({1})
001_002_307=Metadaten: Das exklusive Feld ""{0}"" darf nicht mit anderen Feldern dem Kürzel ""{1}"" zugeordnet werden.
001_002_308=Metadaten: Das Kürzel ""{0}"" ({1}) enthält das aktive Steuerzeichen ""{2}"" ({3}).
001_002_309=Suchverhalten: Bei der Fuzzy-Suche muss der Schwellenwert {0} mindestens {1} sein.
001_002_310=Suchverhalten: Bei der Fuzzy-Suche muss der Schwellenwert {0} größer oder gleich Schwellenwert {1} sein.
001_002_311=Textersetzungen: Der Suchbegriff darf nicht leer sein.
001_002_312=Textersetzungen: Der Suchbegriff ""{0}"" ist mehrfach in miteinander im Konflikt stehenden Bereichen definiert.
001_002_313=Textersetzungen: Bei einer 1:1 Zuordnung (Bereich 4) muss der Suchbegriff ""{0}"" genau ein Zeichen lang sein.
001_002_314=Textersetzungen: Bei einer 1:1 Zuordnung (Bereich 4) darf der Ersetzungstext für den Suchbegriff ""{0}"" nicht leer sein.
001_002_315=Leistung, Caching & Diagnose: Die Obergrenze für Cache-Einträge muss mindestens {0} betragen.
001_002_316=Leistung, Caching & Diagnose: Die maximale Dateigröße für das Einlesen von ""@content"" muss mindestens {0} MB betragen.
001_002_317=Leistung, Caching & Diagnose: Der Arbeitsspeicher für den ""@content""-Cache muss mindestens {0} MB betragen.
001_002_318=Metadaten: Das Standard-Kürzel ""{0}"" für den Bereich ""{1}"" ist kein gültiges oder aktives Metadaten-Kürzel.
001_002_400=Suchassistent-Fenster - Position: Aufgrund der gewählten Kombination aus Ankerpunkt, Fenstergröße und Abstand könnte das Fenster teilweise oder vollständig außerhalb des sichtbaren Bildschirmbereichs liegen.
001_002_401=Leistung, Caching & Diagnose: Höhere Log-Level verlangsamen die Suche drastisch, da umfangreiche Protokolldateien geschrieben werden müssen.
001_002_402=Leistung, Caching & Diagnose: Das Deaktivieren des Caches reduziert die Suchgeschwindigkeit erheblich, da alle Datei- und Ordnerinformationen bei jeder Änderung des Suchtexts neu eingelesen werden.
001_002_403=32-Bit
001_002_404=64-Bit
001_002_405=WDX-Plugin {0}: Für dieses Plugin werden unterschiedliche INI-Dateien verwendet. Es sollte pro Plugin nur eine einzige INI-Datei definiert sein.
001_002_406=WDX-Plugin {0}: Das konfigurierte Feld ""{1}"" existiert nicht im Plugin.
001_002_407=WDX-Plugin {0} - Fehler: Datei nicht gefunden: pluginPath = ""{1}""
001_002_408=WDX-Plugin {0} - Fehler: LoadLibrary(pluginPath = ""{1}"") => fehlgeschlagen - Fehlercode: {2}
001_002_409=WDX-Plugin {0} - Fehler: Funktion ""ContentGetSupportedField"" nicht gefunden: pluginPath = ""{1}""
001_002_410=WDX-Plugin {0} - Fehler: Funktion ""ContentGetValueW"" (oder ""ContentGetValue"") nicht gefunden: pluginPath = ""{1}""
001_002_411=WDX-Plugin {0} - Fehler: CreateDirectory(""{1}"") (für ""{2}"") => fehlgeschlagen - {3}
001_002_412=WDX-Plugin {0} - Fehler: ContentSetDefaultParams(pluginInterfaceVersion = {1}.{2}, defaultIniName = {3}) => fehlgeschlagen - {4}
001_002_413=WDX-Plugin {0} - Fehler: ContentGetSupportedField(fieldIndex = {1}, fieldName = {2}, units = {3}, maxLen = {4}) => fehlgeschlagen - Ergebnis: {5} - {6}
001_002_414={0}-Plugin kann nicht in {1}-QuickSearch eXtended 2 geladen werden
001_002_415=Plugin wurde aus Performancegründen nicht geladen, da es deaktiviert ist

002_001_001=Einstellungen

002_002_001=Sprache
002_002_002=Sprache der Oberfläche:
002_002_003=Eigene Textanpassungen (Bearbeitung im externen Editor empfohlen):
002_002_004=Vorlage aus aktueller Sprache laden
002_002_005=Hier kannst du gezielt einzelne im Programm verwendete Texte im Format ""ID=Text"" ändern oder eine komplett neue Sprache erstellen. Reiche fertige Übersetzungen gerne über das GitHub-Projekt ein!
002_002_006=Änderungen verwerfen?
002_002_007=Sollen die vorhandenen Einträge im Feld ""Eigene Textanpassungen"" verworfen und die Standardvorlage der aktuellen Sprache neu geladen werden?
002_002_008=# Kopiere die Vorlage mit folgendem Befehl in eine KI (z.B. ChatGPT): ""Übersetze diese Texte in die [Zielsprache]. Lass die IDs (Zahlen vor dem =) exakt gleich und übersetze nur den Text rechts davon. Antworte ausschließlich mit dem fertigen Ergebnis ohne zusätzlichen Text:""
002_002_100=Erscheinungsbild
002_002_101=Theme der Oberfläche:
002_002_102=Eigene Farbanpassungen (Bearbeitung im externen Editor empfohlen):
002_002_103=Vorlage aus aktuellem Theme laden
002_002_104=Sollen die vorhandenen Einträge im Feld ""Eigene Farbanpassungen"" verworfen und die Standardvorlage des aktuellen Themes neu geladen werden?
002_002_105=Experteneinstellungen in verschiedenen Bereichen anzeigen:
002_002_201=Hell
002_002_202=Dunkel

002_003_001=Suchassistent-Fenster
002_003_002=Suchassistent beim Öffnen des Total Commander Schnellfilter-Dialogs (Strg+S) einblenden:
002_003_003=Wird der Suchassistent deaktiviert, kann er nur durch die Eingabe von ""@gui"" im Total Commander Schnellfilter-Dialog wieder aufgerufen werden.
002_003_004=Suchassistent nach dem Schließen des Total Commander Schnellfilter-Dialogs geöffnet lassen:
002_003_005=Ankerpunkt für die Fensterposition:
002_003_006=Bildschirm
002_003_007=Total Commander Hauptfenster
002_003_008=Total Commander Schnellfilter-Dialog
002_003_009=Obere linke Ecke
002_003_010=Obere rechte Ecke
002_003_011=Untere linke Ecke
002_003_012=Untere rechte Ecke
002_003_013=Pixel-Abstand zum Ankerpunkt (X / Y):
002_003_014=Negative Werte verschieben das Fenster nach links/oben, positive Werte nach rechts/unten.
002_003_015=Fenstergröße (Breite / Höhe):

002_004_001=Inhalt und Reihenfolge des Suchassistenten
002_004_002=Lege fest, welche Rubriken im Suchassistenten angezeigt werden und ob diese direkt aufgeklappt oder zugeklappt sind. Die Reihenfolge lässt sich über die Schaltflächen anpassen. Das Suchassistent-Fenster lässt sich bei Bedarf mit dem Mausrad scrollen. Fahre mit der Maus über Schaltflächen für hilfreiche Tooltipps.
002_004_010=Fehler (z. B. fehlerhafte Regex-Ausdrücke im Suchtext)
002_004_011=Warnungen (Performance-Hinweise bei verlangsamter Suche)
002_004_012=Informationen (allgemeine Performance-Messwerte)
002_004_013=Such-Struktur (visuelle Anzeige der Suchlogik)
002_004_014=Erkannte Such-Sonderzeichen (im aktuellen Suchtext)
002_004_015=Eingabehinweise (dynamische Vorschläge passend zum Suchtext)
002_004_016=Verfügbare Steuerzeichen (Übersicht aller aktiven Zeichen)
002_004_017=Text-Ersetzungen (Übersicht aktiver Regeln ab 2 Suchzeichen, die den Filtertext betreffen)
002_004_018=Schnellzugriff (Verlauf letzter Suchen und direkte Suchoptionen)
002_004_030=Rubrik
002_004_031=Anzeigestatus
002_004_032=Nach oben
002_004_033=Nach unten
002_004_040=Deaktiviert
002_004_041=Zugeklappt
002_004_042=Aufgeklappt
002_004_050=Vollständige Such-Struktur verwenden (zeigt auch Standardwerte für Suchmodus und Metadaten-Kürzel an, falls die Rubrik ""Such-Struktur"" aktiv ist)

002_005_001=Sonderzeichen zur Suchsteuerung
002_005_002=Deaktivierte Sonderzeichen verlieren ihre Steuerfunktion. Eine Suche kann diese Zeichen dann direkt als normalen Text verwenden, ohne sie mit einem Backslash (\\) zu maskieren.
002_005_003=Zeichen für globale ODER-Verknüpfung (|):
002_005_004=Zeichen für UND-Verknüpfung (Leerzeichen):
002_005_005=Zeichen für Negation (!):
002_005_006=Zeichen zum Umschalten der Groß-/Kleinschreibungs-Beachtung (~):
002_005_007=Zeichen um Treffer am Textanfang zu erzwingen (^):
002_005_008=Zeichen um Treffer am Textende zu erzwingen ($):
002_005_009=Zeichen für lokale ODER-Verknüpfung (/):
002_005_010=Zeichen für Metadaten (@):
002_005_011=Zeichen zur Maskierung (\\):
002_005_012=Dezimaltrennzeichen (für ""@age"" und ""@size""):
002_005_013=Zeichen für Textblöcke (""):

002_006_001=Sonderzeichen für den Suchmodus
002_006_002=Zeichen für die Standard-Suche (=):
002_006_003=Zeichen für die Regex-Suche (Regulärer Ausdruck) (?):
002_006_004=Zeichen für die Fuzzy-Suche (Unscharfe Suche) (<):
002_006_005=Zeichen für die Sequenz-Suche (*):
002_006_006=Zeichen für die Mustersuche (%):

002_007_001=Suchverhalten
002_007_002=Standard-Suchmodus:
002_007_003=Standard-Suche (=)
002_007_004=Regex-Suche (Regulärer Ausdruck) (?)
002_007_005=Fuzzy-Suche (Unscharfe Suche) (<)
002_007_006=Sequenz-Suche (*)
002_007_007=Groß-/Kleinschreibung standardmäßig beachten:
002_007_008=Fuzzy-Suche – 1 Tippfehler erlaubt ab dieser Textlänge:
002_007_009=Fuzzy-Suche – 2 Tippfehler erlaubt ab dieser Textlänge:
002_007_010=Fuzzy-Suche – 3 Tippfehler erlaubt ab dieser Textlänge:
002_007_011=Suche abbrechen mit Taste:
002_007_012=Deaktiviert
002_007_013=ESC-Taste (Schnellsuche wird geschlossen)
002_007_014=PAUSE-Taste (Schnellsuche bleibt offen)
002_007_015=ESC- und PAUSE-Taste (beide aktiv)
002_007_016=Wenn man eine langlaufende Inhaltssuche stoppen will, muss die gewählte Taste länger gedrückt werden. Die PAUSE-Taste hat den Vorteil, dass die Schnellsuche mit dem Suchtext offen bleibt.
002_007_017=Zwingt Total Commander, den Suchfilter weiter zu übermitteln, selbst wenn Zwischenzustände (wie ein unvollständiger regulärer Ausdruck) temporär zu 0 Treffern führen:
002_007_018=Deaktivieren Sie diese Option nur, wenn Sie genau wissen, was Sie tun! Kann zu unerwarteten Nebeneffekten führen. Erfordert einen Neustart von Total Commander.
002_007_019=Beim ersten Suchterm standardmäßig einen Treffer am Text- oder Wortanfang (^) erzwingen:
002_007_020=Ist diese Option aktiv, muss der allererste Suchterm im ersten Suchzweig direkt am Anfang des Elementnamens stehen (so als ob ein ""^"" vorangestellt wäre). Ein explizites ""^"" kehrt das Verhalten für diesen Term um.
002_007_021=Mustersuche (%)
002_007_022=Die Anker ^ und $ matchen nicht nur am Anfang/Ende des Dateinamens, sondern zusätzlich an Wortgrenzen (nachfolgende Trennzeichen begrenzen ein Wort):

002_008_001=Metadaten
002_008_002=Hier können eigene Kürzel definiert werden (z. B. ""@größe"" statt ""@size"" oder kurz ""@c"" statt ""@content""), um die Suche an die eigene Sprache oder an persönliche Vorlieben anzupassen. Bleibt das Feld leer, wird das Kürzel deaktiviert.
002_008_003=Eigenes Kürzel für ""@gui"":
002_008_004=Eigenes Kürzel für ""@name"":
002_008_005=Eigenes Kürzel für ""@ext"":
002_008_006=Eigenes Kürzel für ""@folder"":
002_008_007=Eigenes Kürzel für ""@path"":
002_008_008=Eigenes Kürzel für ""@desc"":
002_008_009=Eigenes Kürzel für ""@content"":
002_008_010=Eigenes Kürzel für ""@age"":
002_008_011=Eigenes Kürzel für ""@size"":
002_008_012=Eigenes Kürzel für ""@attr"":
002_008_101=Weitere Metadaten über Inhalts-Plugins (WDX) definieren
002_008_102=Hier lassen sich Felder aus externen Total Commander Plugins als eigene Metadaten-Kürzel registrieren. Wird beispielsweise dem Feld ""Title"" aus dem Plugin ""ShellDetails.wdx"" das Kürzel ""mp3"" zugewiesen, kann in der Suche mittels ""@mp3 Suchbegriff"" danach gefiltert werden. Werden mehrere Felder (z. B. auch der ""Interpret"") demselben Kürzel ""mp3"" zugeordnet, durchsucht die Engine alle diese Textfelder gleichzeitig.
002_008_103=Aktiv
002_008_104=Metadaten-Kürzel
002_008_105=Architektur
002_008_106=Plugin-Pfad
002_008_107=INI-Pfad
002_008_108=Feldname & Einheit
002_008_109=Nach oben
002_008_110=Nach unten
002_008_111=Definition hinzufügen
002_008_112=Definition entfernen
002_008_200=Standard-Metadaten-Kürzel je Suchbereich
002_008_201=Üblicherweise filtert man zunächst nach dem Dateinamen. Beim Durchsuchen der Historie oder der Tab-Pfade kann es jedoch hilfreich sein, direkt den gesamten Pfad zu durchsuchen. Hier lässt sich das Metadaten-Kürzel festlegen, das im jeweiligen Bereich standardmäßig verwendet wird. Natürlich kann das aktive Kürzel im Suchstring jederzeit durch ""@name"", ""@path"" oder andere Kürzel angepasst werden.
002_008_202=Dateifenster (Dateifenster fokussieren → Strg+S):
002_008_203=Suchergebnisse (Dateien suchen → Anwenden → Strg+S):
002_008_204=Verzeichnisse synchronisieren (Befehle → Verzeichnisse synchronisieren → Tippen starten):
002_008_205=Im Fenster ""Verzeichnisse synchronisieren"" stellt Total Commander nur eine reine Suchfunktion bereit. Unpassende Einträge werden nicht ausgeblendet, sondern die Treffer können direkt mit den Pfeiltasten (Oben/Unten) angesprungen werden.
002_008_206=Verzeichnishistorie (Alt+Pfeil unten → Strg+S):
002_008_207=Tab-Titel (Strg+Umschalt+A → Tippen starten):
002_008_208=Tab-Pfade (Strg+Umschalt+A → ""*"" eingeben):

002_009_001=Textersetzungen
002_009_002=PinYin-Suche aktivieren (Chinesisch, z.B. ""ys"" findet ""耶稣""):
002_009_003=Hangul-Suche aktivieren (Koreanisch, z.B. ""ㅇㅅ"" findet ""예수""):
002_009_004=Details zu den 4 Anwendungsbereichen befinden sich in der Hilfe. Erweiterte Massen-Ersetzungsregeln können zudem über die Datei ""{0}"" definiert werden.
002_009_005=Anwendungsbereich
002_009_006=Suchen nach
002_009_007=Ersetzen durch
002_009_008=Nach oben
002_009_009=Nach unten
002_009_010=Regel hinzufügen
002_009_011=Regel entfernen
002_009_012=1 - nur Suchtext
002_009_013=2 - nur Elementname (Datei/Ordner)
002_009_014=3 - Beide (Suchtext & Elementname)
002_009_015=4 - 1:1 Zuordnung
002_009_016=Umlaute und Akzente ignorieren (z. B. ""a"" findet ""ä"", ""á"", ""à"", ""â""):
002_009_017=Bei der Regex- und Mustersuche stehen die meisten Textersetzungen (PinYin-Suche, Hangul-Suche, Umlaute/Akzente ignorieren sowie 1:1-Zuordnungen) nicht zur Verfügung.

002_010_001=Leistung, Caching & Diagnose
002_010_002=Detailgrad der internen Protokollierung (Log-Level):
002_010_003=Level 0 - Keine Protokollierung
002_010_004=Level 1 - Nur Fehler
002_010_005=Level 2 - Plugin-Initialisierung
002_010_006=Level 3 - Technische Such-Ausführungspläne
002_010_007=Level 4 - Einzelne Datei-Scans
002_010_008=Level 5 - Tiefen-Diagnose der Engine
002_010_010=Metadaten-Cache (Datei- und Ordnerinformationen):
002_010_011=Deaktiviert (Daten bei jedem Durchlauf neu einlesen)
002_010_012=Nur Informationen des zuletzt gescannten Verzeichnisses cachen (braucht weniger Speicher)
002_010_013=Feste Obergrenze für Cache-Einträge verwenden (bessere Performance)
002_010_015=Metadaten-Cache - Obergrenze für Cache-Einträge:
002_010_016=Maximale Dateigröße für das Einlesen von ""@content"" - größere Dateien werden ignoriert (MB):
002_010_017=Gesamter Arbeitsspeicher für den ""@content""-Cache - ältere Inhalte werden beim Erreichen des Limits verworfen (MB):

003_001_001=Dokumentation

003_002_001=Version ""{0} {1}""
003_002_002=Version ""{0} {1}"" anzeigen
003_002_003=# ❌ Dokumentation fehlt\n\n**Datei nicht gefunden:**\n\n```{0}```
003_002_004=# ❌ Dokumentation fehlt\n\n**Fehler beim Einlesen der Datei:**\n\n```{0}```\n\n**Fehlermeldung:**\n\n```\n{1}\n```

004_001_001=Logdatei

004_002_001=Logdatei vom {0} ({1})
004_002_002=Logdatei löschen
004_002_003=Keine Logdatei vorhanden
004_002_004=[... Ältere Log-Einträge wurden aus Performancegründen übersprungen ...]\n\n
004_002_005=❌ Logdatei fehlt\nDatei nicht gefunden: {0}
004_002_006=❌ Fehler beim Einlesen der Logdatei\nDatei: {0}\n\nFehlermeldung:\n{1}

005_001_001=Persönliches

005_002_001={0}
005_002_002=Zu ""{0}"" wechseln

100_001_001=Einstellungen öffnen
100_001_002=Fehler (z. B. fehlerhafte Regex-Ausdrücke im Suchtext) – Klicken zum Ein-/Ausblenden
100_001_003=Warnungen (Performance-Hinweise bei verlangsamter Suche) – Klicken zum Ein-/Ausblenden
100_001_004=Informationen (allgemeine Performance-Messwerte) – Klicken zum Ein-/Ausblenden
100_001_005=Such-Struktur (visuelle Anzeige der Suchlogik) – Klicken zum Ein-/Ausblenden
100_001_006=Erkannte Such-Sonderzeichen (im aktuellen Suchtext) – Klicken zum Ein-/Ausblenden
100_001_007=Eingabehinweise (dynamische Vorschläge passend zum Suchtext) – Klicken zum Ein-/Ausblenden
100_001_008=Verfügbare Steuerzeichen (Übersicht aller aktiven Zeichen) – Klicken zum Ein-/Ausblenden
100_001_009=Text-Ersetzungen (Übersicht aktiver Regeln ab 2 Suchzeichen, die den Filtertext betreffen) – Klicken zum Ein-/Ausblenden
100_001_010=Schnellzugriff (Verlauf letzter Suchen und direkte Suchoptionen) – Klicken zum Ein-/Ausblenden

100_002_001=Zeichen für globale ODER-Verknüpfung: ""{0}"" – Trennt den Suchstring in unabhängige Zweige. Wenn mindestens ein Zweig passt, wird ein Element gefunden.
100_002_002=Zeichen für UND-Verknüpfung: ""{0}"" – Verknüpft Bedingungen innerhalb eines Suchzweigs. Alle Bedingungen müssen erfüllt sein, damit der Zweig passt.
100_002_003=Zeichen für lokale ODER-Verknüpfung: ""{0}"" – Erlaubt alternative Terme innerhalb einer einzelnen Bedingung. Mindestens ein Term muss passen, damit die Bedingung passt.
100_002_004=Zeichen zur Maskierung: ""{0}"" – Hebt die Sonderfunktion des nachfolgenden Steuerzeichens auf und sucht es als normalen Text.
100_002_005=Zeichen für Negation: ""{0}"" – Kehrt die Bedingung um. Damit die Bedingung erfüllt ist, dürfen die entsprechenden Begriffe nicht enthalten sein.
100_002_006=Zeichen zum Umschalten der Groß-/Kleinschreibungs-Beachtung: ""{0}"" – Obwohl standardmäßig aktiv, wird die Groß-/Kleinschreibung in dieser Bedingung durch das Umschaltzeichen ignoriert.
100_002_007=Zeichen zum Umschalten der Groß-/Kleinschreibungs-Beachtung: ""{0}"" – Obwohl standardmäßig ignoriert, wird die Groß-/Kleinschreibung in dieser Bedingung durch das Umschaltzeichen beachtet.
100_002_008=Zeichen um Treffer am Textanfang zu erzwingen: ""{0}"" – Der Suchtext dieser Bedingung muss direkt am Anfang des Elementnamens stehen.
100_002_009=Zeichen um Treffer am Textende zu erzwingen: ""{0}"" – Der Suchtext dieser Bedingung muss direkt am Ende des Elementnamens stehen.
100_002_010=Zeichen für die Standard-Suche: ""{0}"" – Sucht den exakten Text an beliebiger Stelle im Elementnamen.
100_002_011=Zeichen für die Regex-Suche: ""{0}"" – Interpretiert den Text als regulären Ausdruck und sucht damit.
100_002_012=Zeichen für die Fuzzy-Suche: ""{0}"" – Toleriert Tippfehler über eine automatische Levenshtein-Distanz.
100_002_013=Zeichen für die Sequenz-Suche: ""{0}"" – Sucht Zeichen in der angegebenen Reihenfolge, auch mit Lücken dazwischen.
100_002_014=Zeichen für Textblöcke: ""{0}"" – Hebt die Sonderfunktion aller Steuerzeichen im eingeschlossenen Text auf und sucht ihn als normalen Text.
100_002_015=Zeichen um Treffer am Textanfang zu erzwingen: ""{0}"" – Der Suchtext dieser Bedingung muss direkt am Anfang des Elementnamens stehen. Die globale Einstellung fügt diesen Modifikator automatisch für den ersten Suchterm hinzu.
100_002_016=Zeichen um Treffer am Textanfang aufzuheben: ""{0}"" – Obwohl die globale Einstellung diesen Modifikator automatisch für den ersten Suchterm hinzufügt, wird er durch dieses Zeichen aufgehoben.
100_002_017=Zeichen für die Mustersuche: ""{0}"" – Interpretiert den Text als vereinfachtes Muster und sucht damit.
100_002_100=Im Rahmen des Metadaten-Kürzels ""{0}"" werden gleichzeitig folgende Felder durchsucht: {1}.
100_002_102=Im Rahmen des Metadaten-Kürzels ""{0}"" wird folgendes Feld durchsucht: {1}.
100_002_103=Das Metadaten-Kürzel ""{0}"" blendet den Suchassistent ein.
100_002_104=Zeichen für Metadaten: ""{0}"": Schaltet den Suchkontext auf das angegebene Metadaten-Kürzel und dessen Felder um.
100_002_110=GUI-Steuerung (Suchassistent einblenden)
100_002_111=Name (z. B. ""Vertrag.docx"")
100_002_112=Erweiterung (z. B. ""docx"")
100_002_113=Name des Elternordners (z. B. ""Dokumente"")
100_002_114=Vollständiger Pfad (z. B. ""C:\\Daten\\Dokumente\\Vertrag.docx"")
100_002_115=Beschreibung (Dateikommentare aus ""descript.ion""-Dateien)
100_002_116=Inhalt (Volltextsuche in Dateien)
100_002_117=Änderungsalter (Beispieldaten: "">14t"", ""<2st"", "">2{0}5j"", ""=3t"")
100_002_118=Dateigröße (Beispieldaten: "">700"", ""<50M"", "">2{0}2G"", ""=7M"")
100_002_119=WDX: {0} - {1}
100_002_120=Dateiattribute (z. B. ""r""=schreibgeschützt, ""h""=versteckt usw.)
100_002_200=Standard-Suche: Sucht nach dem Text ""{0}"".
100_002_201=Fuzzy-Suche: Toleriert Tippfehler im Suchtext ""{0}"" über eine automatische Levenshtein-Distanz.
100_002_202=Sequenz-Suche: Sucht die Zeichen aus ""{0}"" in der angegebenen Reihenfolge mit beliebigen Lücken dazwischen.
100_002_203=Regex-Suche: Interpretiert ""{0}"" als regulären Ausdruck und sucht damit.
100_002_204=Regex-Suche: Fehler im regulären Ausdruck ""{0}"" ({1}).
100_002_205=Regex: {0}
100_002_206=Mustersuche: Interpretiert ""{0}"" als Mustersuche (Regex: ""{1}"").
100_002_207=Mustersuche: Muster ""{0}"" wurde in den regulären Ausdruck ""{1}"" umgewandelt, erzeugte jedoch einen Fehler: {2}
100_002_208=Muster: {0}
100_002_210=ab {0}
100_002_211=bis {0}
100_002_212={0} bis {1}
100_002_213== {0}
100_002_214=≈ {0}
100_002_215=Ungültiger Bereich: {0} bis {1}
100_002_216=Dateigröße: Mindestens {0} ({1} Bytes).
100_002_217=Dateigröße: Höchstens {0} ({1} Bytes).
100_002_218=Dateigröße: Zwischen {0} ({2} Bytes) und {1} ({3} Bytes).
100_002_219=Dateigröße: Genau {0} ({1} Bytes).
100_002_220=Dateigröße: Ungefähr {0} ({1} bis {2} Bytes).
100_002_221=Dateigröße: Ungültiger Bereich! Die Untergrenze {0} ({2} Bytes) ist größer als die Obergrenze {1} ({3} Bytes).
100_002_222=Dateigröße: Ungültiger Ausdruck ""{0}"". Beispieldaten: "">700"", ""<50M"", "">2{1}2G"", ""=7M""
100_002_223=Dateigröße: {0}
100_002_224=Dateigrößen-Filter: Schränkt die Suche auf Basis der Dateigröße ein. Beispieldaten: "">700"", ""<50M"", "">2{0}2G"", ""=7M""
100_002_230=Einheiten für das Änderungsalter (Sekunden, Minuten, Stunden, Tage, Wochen, Monate, Jahre)
100_002_231=S
100_002_232=M
100_002_233=ST
100_002_234=T
100_002_235=W
100_002_236=MO
100_002_237=J
100_002_240={0} Sekunden
100_002_241={0} Minuten
100_002_242={0} Stunden
100_002_243={0} Tage
100_002_244={0} Monate
100_002_245={0} Jahre
100_002_246=Jetzt
100_002_250=ab {0}
100_002_251=bis {0}
100_002_252={0} bis {1}
100_002_253== {0}
100_002_254=≈ {0}
100_002_255=Ungültiger Bereich: {0} bis {1}
100_002_256=Änderungsalter: Mindestens {0}.
100_002_257=Änderungsalter: Höchstens {0}.
100_002_258=Änderungsalter: Zwischen {0} und {1}.
100_002_259=Änderungsalter: Genau {0}.
100_002_260=Änderungsalter: Ungefähr {0}.
100_002_261=Änderungsalter: Ungültiger Bereich! Die Untergrenze {0} ist größer als die Obergrenze {1}.
100_002_262=Änderungsalter: Ungültiger Ausdruck ""{0}"". Beispieldaten: "">14t"", ""<2st"", "">2{1}5j"", ""=3t""
100_002_263=Änderungsalter: {0}
100_002_264=Änderungsalter-Filter: Schränkt die Suche auf Basis des Änderungsdatums ein. Beispieldaten: "">14t"", ""<2st"", "">2{0}5j"", ""=3t""

100_003_001={0} Einträge/s
100_003_002={0} Einträge/ms
100_003_003=Schnelle Filter-Auswertung (Werte vom vorherigen Suchdurchgang): {0} Einträge in {1} ms ausgewertet ({2} ms/Eintrag). Plugin-Startdauer: {3} ms. Die Auswertung läuft flüssig.
100_003_004=Langsame Filter-Auswertung (Werte vom vorherigen Suchdurchgang)! {0} Einträge in {1} ms ausgewertet ({2} ms/Eintrag). Plugin-Startdauer: {3} ms. Mögliche Ursachen: Umfangreiches Logging, Suche in Dateiinhalten/WDX-Feldern oder deaktivierter Cache.
100_003_010=Logging aktiv
100_003_011=Umfangreiches Logging ist aktiviert. Dies kann die Suchgeschwindigkeit spürbar verringern. Bitte in den Einstellungen deaktivieren oder reduzieren, wenn es nicht benötigt wird.

100_004_001=Maskierung aktiv
100_004_002=Textblock aktiv
100_004_003=Das Zeichen ""{0}"" hat die Maskierung aktiviert – Die Sonderfunktion des nächsten Zeichens ist aufgehoben, es wird als normaler Text gesucht.
100_004_004=Das Zeichen ""{0}"" hat einen Textblock gestartet – Die Sonderfunktion aller Steuerzeichen im eingeschlossenen Text ist aufgehoben, er wird als normaler Text gesucht. Schließe den Textblock mit {1}.
100_004_010=Suchtext
100_004_011=Gib deinen Suchtext ein.
100_004_020=Gleichheitsoperator: ""="" – Filtert nach dem Wert. Wird kein Operator angegeben, wird automatisch auf Gleichheit geprüft.
100_004_021=Größer-als-Operator: "">"" – Filtert nach Werten, die größer als der angegebene Wert sind.
100_004_022=Kleiner-als-Operator: ""<"" – Filtert nach Werten, die kleiner als der angegebene Wert sind.
100_004_023=Gib einen numerischen Wert ein. Verwende ""{0}"" als Dezimaltrenner.
100_004_030=Einheit: Byte – Misst die Größe in Bytes. Das Kürzel ""B"" ist optional.
100_004_031=Einheit: Kilobyte – Misst die Größe in Kilobytes. Gültige Kürzel: ""K"" oder ""KB"".
100_004_032=Einheit: Megabyte – Misst die Größe in Megabytes. Gültige Kürzel: ""M"" oder ""MB"".
100_004_033=Einheit: Gigabyte – Misst die Größe in Gigabytes. Gültige Kürzel: ""G"" oder ""GB"".
100_004_034=Einheit: Terabyte – Misst die Größe in Terabytes. Gültige Kürzel: ""T"" oder ""TB"".
100_004_035=Einheit: Petabyte – Misst die Größe in Petabytes. Gültige Kürzel: ""P"" oder ""PB"".
100_004_036=Einheit: Exabyte – Misst die Größe in Exabytes. Gültige Kürzel: ""E"" oder ""EB"".
100_004_040=Einheit: Sekunden – Misst das Alter in Sekunden. Kürzel: ""{0}"".
100_004_041=Einheit: Minuten – Misst das Alter in Minuten. Kürzel: ""{0}"".
100_004_042=Einheit: Stunden – Misst das Alter in Stunden. Kürzel: ""{0}"".
100_004_043=Einheit: Tage – Misst das Alter in Tagen. Das Kürzel ""{0}"" ist optional.
100_004_044=Einheit: Wochen – Misst das Alter in Wochen. Kürzel: ""{0}"".
100_004_045=Einheit: Monate – Misst das Alter in Monaten. Kürzel: ""{0}"".
100_004_046=Einheit: Jahre – Misst das Alter in Jahren. Kürzel: ""{0}"".

100_005_001=Tooltip in Zwischenablage kopieren
100_005_002=""{0}"" einfügen (Klick)
100_005_003=""{0}"" vervollständigen (Klick)

100_006_001=Text-Ersetzung: Sucht nach ""{0}"" und ersetzt es durch ""{1}"".
100_006_002=Suchtext durch ""{0}"" ersetzen (Strg+Klick)
100_006_003=Langtext einfügen (""{0}"")

100_007_001=Suchverlauf anzeigen
100_007_002=(Kein Suchverlauf vorhanden)
100_007_010=Zwischenablage einfügen
100_007_011=Zwischenablage einfügen (Klick)
100_007_012=Suchtext durch Zwischenablage ersetzen (Strg+Klick)
100_007_020=Suchfeld leeren
100_007_030=Groß-/Kleinschreibung standardmäßig beachten (Zum Umschalten bitte klicken.)
100_007_031=Groß-/Kleinschreibung standardmäßig nicht beachten (Zum Umschalten bitte klicken.)
100_007_040=Der allererste Suchterm im ersten Suchzweig muss direkt am Anfang des Elementnamens stehen. (Zum Umschalten bitte klicken.)
100_007_041=Der allererste Suchterm im ersten Suchzweig muss nicht direkt am Anfang des Elementnamens stehen. (Zum Umschalten bitte klicken.)
100_007_050=Standard-Suche als Standard-Suchmodus festlegen
100_007_051=Regex-Suche als Standard-Suchmodus festlegen
100_007_052=Mustersuche als Standard-Suchmodus festlegen
100_007_053=Fuzzy-Suche als Standard-Suchmodus festlegen
100_007_054=Sequenz-Suche als Standard-Suchmodus festlegen
";

        public static readonly string personalNote = @"# ✍️ Persönliches

## ❤️ Ich liebe Programmieren…

Mein Name ist **Samuel Plentz** und seit meiner Schulzeit programmiere ich mit großer Leidenschaft und Freude. Im Sommer 2026 entstand dieses Projekt als mein erstes **Vibe-Coding-Experiment**: Die KI generiert den Code, den ich anschließend manuell prüfe, verfeinere und zusammensetze.

Es ist mein persönlicher Beitrag und ein Tribut an den **Total Commander** — einen großartigen Begleiter im PC-Alltag.

Dieses Total Commander Plugin steht allen Nutzern **frei und kostenlos** zur Verfügung.

## ❤️ Ich liebe meine Familie…

Ein besonderer Dank gilt **meiner Frau**. Sie steht treu an meiner Seite, unterstützt mich vielfältig und ist mir in ihrem Engagement für unsere Familie und für andere ein großes Vorbild. Danke, dass du mein Leben so reich machst!

Meine **drei Söhne** sind eine riesige Bereicherung: Der Älteste fasziniert mich als strukturierter Denker mit großem Herz, der Mittlere begeistert durch seine Kreativität, Hilfsbereitschaft und Empathie, und der Jüngste ist in seiner unbeschwerten Art eine tägliche Quelle der Freude und guten Laune.

Auch **meinen Eltern** bin ich zutiefst dankbar: Sie sind schlichtweg klasse und haben auf meinem Lebensweg so vieles genau richtig gemacht.

## ❤️ Ich liebe Jesus…

**Leben ist mehr** als das, was wir sehen, erreichen oder besitzen können. **Jesus Christus** macht den entscheidenden Unterschied, er schenkt Sinn, Hoffnung und Halt.

Die zentrale Frage lautet: **Ist Jesus wirklich, wer er behauptet zu sein?**

Ich bin von ganzem Herzen überzeugt: Ja, er ist es. **Er ist der Sohn Gottes**, der auf diese Welt kam, um die Schuld der Menschheit zu tragen. Seine Kreuzigung und Auferstehung geschahen aus Liebe. Jeder, der an ihn und seine Erlösung glaubt, empfängt Vergebung und das **ewige Leben**.

Wer das **Neue Testament** unvoreingenommen liest, kann erkennen, dass Jesus nicht nur ein außergewöhnlicher Mensch, sondern tatsächlich **Sohn Gottes** ist.

Ich ermutige dich, selbst nachzulesen und zu prüfen: **Ist das echt?**";
    }
}
