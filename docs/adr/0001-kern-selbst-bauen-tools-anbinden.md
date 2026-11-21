# ADR 0001: Kern selbst bauen, vorhandene Tools über Adapter anbinden

- Status: angenommen
- Datum: 2026-11-20

## Kontext

Zwei Wege standen zur Wahl: alles selbst entwickeln oder vorhandene Tools (Jira, GitHub,
Azure SRE Agent, Confluence) verbinden. Kunden haben diese Tools bereits und werden sie nicht
ersetzen. Der eigentliche Mehrwert liegt in der Schicht darüber.

## Entscheidung

Wir bauen selbst, was uns unterscheidet:

- das Datenmodell (Bedarf, Systemeigenschaft, Richtlinie, Entscheidung …)
- die Logik, die Eigenschaften prüft und über Autonomie und Eskalation entscheidet
- die Oberfläche für den TPO

Alles andere wird angebunden. Wo ein Tool nicht passt (z. B. der SRE Agent für einen Teil des
Betriebs), entwickeln wir diesen Teil selbst – hinter derselben Rollen-Schnittstelle.

Die Wahrheit über Absicht und Eigenschaften liegt in der Zentrale. Angebundene Tools erhalten Kopien.

## Konsequenzen

- Schneller zu einem vorzeigbaren Ergebnis; wir profitieren von Verbesserungen der Hersteller.
- Abhängigkeit von fremden Schnittstellen; jede Rolle braucht einen austauschbaren Adapter.
- Systemeigenschaften dürfen nie nur in Confluence oder Jira stehen – sonst ist die Zentrale nur eine Oberfläche.

## Verworfene Alternativen

- **Alles selbst bauen**: enormer Aufwand, Konkurrenz zu Microsoft und Atlassian, schwer verkäuflich.
- **Nur verbinden**: kein eigener Kern, kleinster gemeinsamer Nenner, kein Alleinstellungsmerkmal.
