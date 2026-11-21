# ADR 0002: Git und Code sind für den TPO unsichtbar

- Status: angenommen
- Datum: 2026-11-20

## Kontext

Ein früher Gedanke war „GitOps für Systeme“ mit Git als Oberfläche für den Soll-Zustand.
Der TPO soll sich aber ausschließlich mit Bedarfen, Eigenschaften und Entscheidungen beschäftigen.

## Entscheidung

Der TPO arbeitet nur in der Kommandozentrale. Git, Code, Pipelines und Ressourcen-IDs tauchen in
seiner Oberfläche nicht auf. Technisch dürfen Repositories darunter weiter existieren
(z. B. für IaC und Anwendungscode der Agents), erscheinen aber höchstens als Verweis im Protokoll.

## Konsequenzen

- Die Zentrale braucht eine eigene Persistenz für Absicht und Verlauf (siehe ADR 0004).
- Der Bau-Agent arbeitet mit Repositories; die Zentrale übersetzt Ergebnisse in Fachsprache.
- Für Entwickler, die doch „unter die Haube“ schauen wollen, ist später eine Ergänzung denkbar (z. B. VS-Code-Erweiterung).

## Verworfene Alternativen

- **Git als Quelle der Wahrheit für den TPO**: passt zu Entwicklern, nicht zur Rolle des TPO.
