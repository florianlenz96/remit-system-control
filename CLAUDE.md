# CLAUDE.md – Kommandozentrale

Projektkontext für Claude Code. Lies diese Datei vollständig, bevor du etwas änderst.
Arbeitssprache in Doku, ADRs und UI-Texten: **Deutsch**. Code, Bezeichner und Commit-Messages: Englisch.

## Worum es geht

Die Kommandozentrale ist eine Web-Anwendung, in der ein **Technical Product Owner (TPO)**
beschreibt, *was* ein IT-System leisten soll. KI-Agents entscheiden, *wie* es gebaut wird,
bauen es und betreiben es dauerhaft – innerhalb von Grenzen, die der TPO festlegt.

Der TPO versteht das Gesamtsystem, muss aber die einzelnen Komponenten (die „Black Box“)
nicht verstehen. Er sieht **nie** Code, Git oder Ressourcen-IDs – nur Bedarfe,
Systemeigenschaften, Entscheidungen und Ergebnisse.

Ausführlich: [`docs/vision.md`](docs/vision.md).

## Vokabular (verbindlich, in Code und UI genau so verwenden)

| Begriff | Bedeutung | Code-Name |
| --- | --- | --- |
| Bedarf | Was jemand vom System braucht, in Fachsprache, ohne Lösung | `Need` |
| Vorschlag / Option | KI-Analyse mit Lösungswegen, Wirkung, Kosten, Risiken | `Proposal` / `Option` |
| Systemeigenschaft | Messbare Regel, die dauerhaft gilt (Ziel, Grenze, Budget …) | `SystemProperty` |
| Entscheidung | Jede Änderung am System, durch KI oder TPO, mit Begründung | `Decision` |
| Rückfrage | Entscheidung, die die KI nicht selbst treffen darf | `Decision` mit `kind = TpoQuestion` |
| Bereich | Teil des Systems, für den eine Richtlinie gilt | `Area` |
| Komponente | Technischer Baustein, von der KI angelegt | `Component` |
| Richtlinie | Autonomie- und Transparenzstufe eines Bereichs | `Policy` |
| Messung / Abweichung | Ist-Wert einer Eigenschaft / Verletzung, Prognose oder Drift | `Measurement` / `Deviation` |
| Rolle | Abstrakte Fähigkeit (Ticketsystem, Bau-Agent …), über Adapter angebunden | `Role` / `Adapter` |

Vollständiges Modell: [`docs/datenmodell.md`](docs/datenmodell.md).

## Grundprinzipien (nicht verletzen)

1. **Absicht statt Implementierung.** Das Modell beschreibt, was gelten soll, nie wie es gebaut ist.
2. **Ownership beim TPO.** Die KI legt nie selbst Ziele oder Grenzen fest (Ausnahme: Autonomiestufe 4, nur „soll“-Eigenschaften).
3. **Alles messbar.** Eine Systemeigenschaft ohne Messvorschrift ist ungültig.
4. **Autonomie nach Risiko**, nicht nach technischem Aufwand – pro Bereich.
5. **Lückenlos nachvollziehbar.** Komponente → Entscheidung → Bedarf oder Abweichung.
6. **Tool-neutral.** Der Kern kennt nur Rollen. Jira, GitHub, SRE Agent usw. sind austauschbare Adapter.
7. **Quelle der Wahrheit ist die Zentrale.** Angebundene Tools bekommen nur Kopien.
8. **Zwei kollidierende „muss“-Eigenschaften löst nie die KI auf** – daraus wird immer eine Rückfrage.

## Architekturentscheidungen

Alle Entscheidungen stehen als ADRs in [`docs/adr/`](docs/adr/). Kurzfassung:

| ADR | Entscheidung | Status |
| --- | --- | --- |
| 0001 | Kern selbst bauen, vorhandene Tools über Adapter anbinden | angenommen |
| 0002 | Git und Code sind für den TPO unsichtbar | angenommen |
| 0003 | Auslieferung in den Azure-Tenant des Kunden, eine Instanz pro Kunde | angenommen |
| 0004 | Verlauf als Event Store, aktueller Stand als abgeleitete Tabellen | angenommen |
| 0005 | Autonomie wird über Azure-Rechte durchgesetzt; kein PIM für Managed Identities | angenommen |
| 0006 | Rollen werden über MCP angebunden; die Zentrale bietet selbst einen MCP-Server an | angenommen |
| 0007 | PostgreSQL für Event Store und Tabellen | vorgeschlagen |
| 0008 | Tech-Stack der Zentrale | **offen** |

Neue grundlegende Entscheidungen immer als neues ADR anlegen (Vorlage: `docs/adr/0000-vorlage.md`),
nie stillschweigend im Code treffen.

## Aktueller Stand und nächster Schritt

- Konzept, Datenmodell und Klick-Entwurf sind fertig.
- **Als Nächstes: PoC mit dem Cache-Szenario** – siehe [`docs/poc-plan.md`](docs/poc-plan.md).
- **Vor der ersten Zeile Code:** ADR 0008 (Tech-Stack) mit dem Nutzer klären. Nicht selbst entscheiden.

## Arbeitsweise

- Kleine, überprüfbare Schritte. Nach jedem Schritt kurz zusammenfassen, was sich geändert hat.
- Alles, was in Azure läuft, als Infrastructure as Code (Bicep) unter `infra/`. Nichts manuell im Portal.
- Keine Secrets im Repository. Managed Identities statt Schlüssel, wo immer möglich.
- UI-Texte in Fachsprache des TPO (siehe Vokabular), keine technischen Begriffe in der Oberfläche.
- Domänenlogik (Regeln zu Autonomie, Eskalation, Konflikten) gehört in den Kern, nicht in Adapter.
- Tests für Domänenregeln zuerst, besonders: Eskalationsregeln und Konflikterkennung zwischen Eigenschaften.

## Was du NICHT tun sollst

- Keine Mandantentrennung bauen – jede Instanz gehört genau einem Kunden (ADR 0003).
- Keine Systemeigenschaften, Richtlinien oder Entscheidungen in Confluence, Jira oder GitHub als Wahrheit ablegen (ADR 0001).
- Kein Event Store für Messwerte – die bleiben in Azure Monitor und werden nur referenziert (ADR 0004).
- Keine produktspezifischen Begriffe (Jira, SRE Agent …) im Domänenmodell.

## Weitere Dokumente

- [`docs/vision.md`](docs/vision.md) – Idee, Rollen, Ablauf, Geschäftsmodell
- [`docs/datenmodell.md`](docs/datenmodell.md) – Entitäten, Felder, Stufen, Cache-Beispiel
- [`docs/poc-plan.md`](docs/poc-plan.md) – Umfang, Schritte, Abnahmekriterien des PoC
- [`docs/mockup.md`](docs/mockup.md) – Klick-Entwurf der drei Screens
