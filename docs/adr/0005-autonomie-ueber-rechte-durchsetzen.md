# ADR 0005: Autonomie wird über Azure-Rechte durchgesetzt; kein PIM für Managed Identities

- Status: angenommen
- Datum: 2026-11-21

## Kontext

Autonomiestufen sollen nicht nur vereinbart, sondern technisch garantiert sein – ein starkes
Argument gegenüber Sicherheitsverantwortlichen. Für Stufe 2 („setzt um nach Freigabe jedes
Schritts“) wäre Just-in-time-Zugriff über Microsoft Entra PIM naheliegend.

Laut Microsoft-Dokumentation lassen sich für Anwendungen, Service Principals und Managed
Identities **keine berechtigten (eligible) Rollenzuweisungen** anlegen, weil sie die Aktivierung
nicht selbst ausführen können:
https://learn.microsoft.com/azure/role-based-access-control/pim-integration

## Entscheidung

| Stufe | Durchsetzung | SRE-Agent-Modus |
| --- | --- | --- |
| 1 | Managed Identity nur mit Leserechten | Review |
| 2 | Freigabe jedes Schritts in der Zentrale (durch den Agenten, nicht durch Rechte) | Review |
| 3 | Schreibrechte auf die Ressourcen des Bereichs, begrenzt durch Azure Policy | Autonomous |
| 4 | wie 3; zusätzlich darf der Agent „soll“-Eigenschaften über die Zentrale ändern | Autonomous |

Wir starten ohne Just-in-time-Rechte.

## Konsequenzen

- Stufe 1 und 3 sind technisch garantiert, Stufe 2 nur durch das Verhalten des Agenten.
- Später denkbar: Die Zentrale vergibt bei Freigabe selbst eine Rolle und entzieht sie danach.
  Dafür bräuchte die Zentrale das Recht, Rollen zuzuweisen (z. B. „Role Based Access Control
  Administrator“ mit Bedingungen) – sie würde damit zu einem besonders schützenswerten Ziel.
- Im PoC zu prüfen: ob Freigaben des SRE Agent im Review-Modus über Agent Hooks in der Zentrale landen.

## Verworfene Alternativen

- **PIM-Eligible für Managed Identities**: von Azure nicht unterstützt.
- **Autonomie nur als Konfiguration ohne Rechte**: schwächeres Sicherheitsversprechen.
