# ADR 0008: Tech-Stack der Zentrale

- Status: **offen** – vor Beginn der Implementierung mit dem Nutzer entscheiden
- Datum: 2026-11-21

## Kontext

Festgelegt sind bereits: Web-Anwendung, Betrieb auf Azure im Kundentenant (ADR 0003),
PostgreSQL (ADR 0007, vorgeschlagen), eigener MCP-Server (ADR 0006), Infrastruktur als Bicep.
Offen sind Sprache und Frameworks für Backend und Frontend.

## Zu entscheiden

- Backend-Sprache und Framework (z. B. .NET / ASP.NET Core oder TypeScript / Node.js)
- Frontend (z. B. Blazor, React, oder serverseitig gerendert)
- Hosting (Vorschlag: Azure Container Apps)
- MCP-Server: Teil des Backends oder eigener Dienst
- Test-Framework für Domänenregeln

## Kriterien

- Erfahrung des Teams
- Gute SDKs für Azure, Entra ID und MCP
- Einfach im Kundentenant auszurollen und zu aktualisieren
- Event Sourcing ohne schwergewichtiges Framework umsetzbar
