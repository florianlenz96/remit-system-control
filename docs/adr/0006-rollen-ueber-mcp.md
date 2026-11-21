# ADR 0006: Rollen werden über MCP angebunden; die Zentrale bietet selbst einen MCP-Server an

- Status: angenommen
- Datum: 2026-11-20

## Kontext

Kunden nutzen unterschiedliche Tools (Jira oder Azure Boards, GitHub oder Azure Repos).
Für viele gibt es bereits MCP-Server. Der Azure SRE Agent kann selbst MCP-Server anbinden und ist
über das Azure MCP Server von außen steuerbar
(https://learn.microsoft.com/azure/sre-agent/mcp-server).

## Entscheidung

- Der Kern kennt nur Rollen: Ticketsystem, Repository, Bau-Agent, Betriebs-Agent, Messquelle, Dokumentation.
- Konkrete Tools werden als Adapter angebunden, bevorzugt über MCP.
- Die Zentrale bietet einen eigenen MCP-Server an. Agents lesen darüber Systemeigenschaften und
  Richtlinien und melden Entscheidungen, Abweichungen und Rückfragen.

## Konsequenzen

- Ein Tool zu wechseln heißt, einen Adapter zu tauschen, nicht den Kern zu ändern.
- Agents fragen die Wahrheit direkt bei der Zentrale ab statt aus Kopien.
- Der MCP-Server der Zentrale ist eine sicherheitskritische Schnittstelle (Authentifizierung über Managed Identity / Entra ID).

## Verworfene Alternativen

- **Punkt-zu-Punkt-Integrationen pro Tool**: hoher Wartungsaufwand, schlecht austauschbar.
