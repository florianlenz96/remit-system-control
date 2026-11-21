# ADR 0004: Verlauf als Event Store, aktueller Stand als abgeleitete Tabellen

- Status: angenommen
- Datum: 2026-11-21

## Kontext

Die Zentrale muss nachweisen können, wer wann was entschieden hat und welcher Stand von
Eigenschaften und Richtlinien zu einem Zeitpunkt galt (Audit, DSGVO-Rechenschaftspflicht,
Auswertung für verdiente Autonomie). Gleichzeitig braucht die Oberfläche schnelle Abfragen
auf den aktuellen Stand.

## Entscheidung

- **Event Store (append-only)** für den Verlauf: Entscheidungen, Änderungen an Systemeigenschaften
  und Richtlinien, Statuswechsel von Bedarfen und Vorschlägen.
- **Tabellen** als Projektionen für den aktuellen Stand.
- **Rohmesswerte bleiben in Azure Monitor**; die Zentrale speichert nur Verweise und, wo nötig, Aggregate.

## Konsequenzen

- Versionierung ist eingebaut: jeder frühere Stand ist rekonstruierbar.
- Projektionen müssen neu aufgebaut werden können; Events sind versioniert und dürfen nie geändert werden.
- Mehr Komplexität als reines CRUD – bewusst nur für den Verlauf, nicht für alles.

## Verworfene Alternativen

- **Nur Tabellen mit Änderungshistorie**: Historie wird nachträglich angebaut und lückenhaft.
- **Messwerte im Event Store**: zu viele Daten, Azure Monitor ist dafür gemacht.
