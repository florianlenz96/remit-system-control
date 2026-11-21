# Klick-Entwurf

Interaktiver Entwurf: [Kommandozentrale – Konzept-Mockup](https://claude.ai/artifact/RN25QLDujYidGBDKAJ24so)
(alle Zahlen sind Beispieldaten).

## Screen 1 – Bedarf und KI-Vorschlag

- Kopf: Bedarf B-042 mit Herkunft (Jira KUNDE-318), Beschreibung in Fachsprache, Ist-Werte
  (p95 820 ms, abends 1.400 ms), Status „Wartet auf TPO-Entscheidung“.
- KI-Analyse mit Ursache und drei Optionen; die empfohlene Option zeigt Wirkung, Kosten, Risiko
  und welche Eigenschaften der TPO festlegen muss.
- Aktionen: „Annehmen und Eigenschaften festlegen“, „Rückfrage stellen“.
- Seitenleiste: Systemkarte mit Komponenten und Zustand (neue Komponente als „vorgeschlagen“),
  weitere Bedarfe.

## Screen 2 – Systemeigenschaften festlegen

- Leitsatz: „Du legst fest, was gelten muss. Die KI entscheidet, wie.“
- Gruppen: Ziel (p95, Verfügbarkeit), Aktualität (TTL Preise, Texte; Lagerbestand nie cachen),
  Ausfall und Backup (Verhalten, Backup, RTO), Budget und Rahmen (Budget, Datenstandort),
  Autonomie (Stufe 1–4).
- Seitenleiste: „Was die KI daraus macht“ – entscheidet selbst / fragt dich; Prüfung vor Freigabe.
- Aktion: „Freigeben – KI setzt um und betreibt“.

## Screen 3 – Betrieb

- Soll-Ist-Kacheln je Eigenschaft (erfüllt / gefährdet / verletzt).
- Hervorgehobene Rückfrage: „Black Friday: Ziel und Budget sind nicht beide haltbar“ mit drei Optionen.
- Entscheidungsprotokoll der KI mit Datum und Begründung.
- Vorher-nachher-Vergleich der Antwortzeit; Hinweis auf Spiegelung nach Jira und Confluence.

## Noch nicht entworfen

- Systemgraph als Gesamtübersicht (Einstieg)
- Datenfluss-Ansicht für Bereiche mit Transparenzstufe 3
