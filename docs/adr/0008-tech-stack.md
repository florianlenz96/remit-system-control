# ADR 0008: Tech stack of Remit

- Status: accepted
- Date: 2026-11-21

## Context

Already decided: web application, operation on Azure in the customer's tenant (ADR 0003),
PostgreSQL (ADR 0007, proposed), own MCP server (ADR 0006), infrastructure as Bicep.
Still open were the language and frameworks for backend and frontend.

Criteria: the team's experience; good SDKs for Azure, Entra ID and MCP; easy to deploy and update
in the customer's tenant; event sourcing implementable without a heavyweight framework.

## Decision

| Topic | Decision |
| --- | --- |
| Backend | **.NET 10 (LTS)**, ASP.NET Core with Minimal APIs |
| Frontend | **Vue 3** with TypeScript and Vite, as a single-page app |
| Frontend delivery | The built Vue app is served by the ASP.NET Core backend as static files – one container, one origin |
| Sign-in | Backend-for-frontend: ASP.NET Core signs in via Entra ID (OpenID Connect, Microsoft.Identity.Web) and keeps the session as a cookie; no tokens in the browser |
| Hosting | **Azure Container Apps** |
| MCP server | **Part of the backend** (same host, endpoint `/mcp`), implemented with the official C# SDK `ModelContextProtocol.AspNetCore`; agents authenticate with Entra ID tokens of their managed identity (a separate authentication scheme alongside the cookie) |
| Event store | Own lean implementation on PostgreSQL via Npgsql; no event sourcing framework |
| Backend tests | **xUnit v3**, assertions without an extra library |
| Frontend tests | **Vitest** |

Code layout:

```
src/Remit.Domain   domain core without dependencies (entities, events, rules)
src/…                          further projects (API/MCP, persistence, Adapters, Vue app) follow step by step
tests/Remit.Domain.Tests
```

## Consequences

- Good Azure, Entra ID and MCP support from a single source (Microsoft SDKs).
- One container for UI, API and MCP server: easy to deploy and update (ADR 0003).
  If the MCP server later comes under heavier load or needs different security, it can be split off
  into its own container – it is only a thin layer over the domain core.
- The MCP endpoint and the UI share one process; the authentication schemes
  must be cleanly separated (cookie only for the UI, bearer token only for `/mcp`).
- Building the event store ourselves means: versioning, optimistic concurrency and
  projections are our responsibility – in return, no framework lock-in.

## Rejected alternatives

- **TypeScript / Node.js in the backend**: good MCP SDK, but a weaker fit with the team's experience
  and the Microsoft ecosystem.
- **Blazor or React in the frontend**: the user decided on Vue.
- **MCP server as a separate service**: a second container, a second identity and duplicate connections
  to database and domain – no benefit in the PoC.
- **Marten as event store**: mature, but a heavyweight framework that
  dictates the database schema and programming model.
- **MSAL in the browser (tokens in the SPA)**: larger attack surface than a backend-for-frontend.
