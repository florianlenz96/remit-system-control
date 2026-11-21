# ADR 0008: Tech-Stack der Zentrale

- Status: angenommen
- Datum: 2026-11-21

## Kontext

Festgelegt sind bereits: Web-Anwendung, Betrieb auf Azure im Kundentenant (ADR 0003),
PostgreSQL (ADR 0007, vorgeschlagen), eigener MCP-Server (ADR 0006), Infrastruktur als Bicep.
Offen waren Sprache und Frameworks für Backend und Frontend.

Kriterien: Erfahrung des Teams; gute SDKs für Azure, Entra ID und MCP; einfach im Kundentenant
auszurollen und zu aktualisieren; Event Sourcing ohne schwergewichtiges Framework umsetzbar.

## Entscheidung

| Thema | Entscheidung |
| --- | --- |
| Backend | **.NET 10 (LTS)**, ASP.NET Core mit Minimal APIs |
| Frontend | **Vue 3** mit TypeScript und Vite, als Single-Page-App |
| Auslieferung Frontend | Gebaute Vue-App wird vom ASP.NET-Core-Backend als statische Dateien ausgeliefert – ein Container, ein Origin |
| Anmeldung | Backend-for-Frontend: ASP.NET Core meldet über Entra ID (OpenID Connect, Microsoft.Identity.Web) an und hält die Sitzung als Cookie; im Browser liegen keine Tokens |
| Hosting | **Azure Container Apps** |
| MCP-Server | **Teil des Backends** (gleicher Host, Endpunkt `/mcp`), umgesetzt mit dem offiziellen C#-SDK `ModelContextProtocol.AspNetCore`; Agents authentifizieren sich mit Entra-ID-Tokens ihrer Managed Identity (eigenes Authentifizierungsschema neben dem Cookie) |
| Event Store | Eigene, schlanke Umsetzung auf PostgreSQL über Npgsql; kein Event-Sourcing-Framework |
| Tests Backend | **xUnit v3**, Assertions ohne Zusatzbibliothek |
| Tests Frontend | **Vitest** |

Aufbau des Codes:

```
src/Kommandozentrale.Domain   Domänenkern ohne Abhängigkeiten (Entitäten, Events, Regeln)
src/…                          weitere Projekte (API/MCP, Persistenz, Adapter, Vue-App) folgen schrittweise
tests/Kommandozentrale.Domain.Tests
```

## Konsequenzen

- Gute Azure-, Entra-ID- und MCP-Unterstützung aus einer Hand (Microsoft-SDKs).
- Ein Container für UI, API und MCP-Server: einfach auszurollen und zu aktualisieren (ADR 0003).
  Wird der MCP-Server später stärker belastet oder anders abgesichert, lässt er sich als eigener
  Container abtrennen – er ist nur eine dünne Schicht über dem Domänenkern.
- Der MCP-Endpunkt und die Oberfläche teilen sich einen Prozess; die Authentifizierungsschemata
  müssen sauber getrennt sein (Cookie nur für die Oberfläche, Bearer-Token nur für `/mcp`).
- Event Store selbst zu bauen heißt: Versionierung, optimistische Nebenläufigkeit und
  Projektionen liegen in unserer Verantwortung – dafür kein Framework-Lock-in.

## Verworfene Alternativen

- **TypeScript / Node.js im Backend**: gutes MCP-SDK, aber schwächere Passung zur Teamerfahrung
  und zum Microsoft-Ökosystem.
- **Blazor oder React im Frontend**: Entscheidung des Nutzers für Vue.
- **MCP-Server als eigener Dienst**: zweiter Container, zweite Identität und doppelte Anbindung
  an Datenbank und Domäne – im PoC ohne Nutzen.
- **Marten als Event Store**: ausgereift, aber ein schwergewichtiges Framework, das
  Datenbankschema und Programmiermodell vorgibt.
- **MSAL im Browser (Tokens in der SPA)**: mehr Angriffsfläche als ein Backend-for-Frontend.
