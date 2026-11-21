# Copilot instructions – Remit

Working language for docs, ADRs, UI texts, code, identifiers and commit messages: **English**.
The full project context lives in [`CLAUDE.md`](../CLAUDE.md) and [`docs/`](../docs/); this file is the short version.

## What Remit is

A web application in which a **Technical Product Owner (TPO)** describes *what* an IT system should deliver.
AI agents decide *how* it is built, build it and operate it – within limits the TPO sets per Area.
The TPO **never** sees code, Git or resource IDs – only Needs, System properties, Decisions and results.

## Vocabulary (binding in code and UI)

| Term | Code name |
| --- | --- |
| Need – what someone needs, in domain language, without a solution | `Need` |
| Proposal / Option – AI analysis with solution paths, effect, cost, risks | `Proposal` / `Option` |
| System property – measurable rule that applies permanently | `SystemProperty` |
| Decision – every change to the system, with rationale | `Decision` |
| TPO question – a Decision the AI may not make itself | `Decision` with `kind = TpoQuestion` |
| Area – part of the system a Policy applies to | `Area` |
| Component – technical building block created by the AI | `Component` |
| Policy – autonomy and transparency level of an Area | `Policy` |
| Measurement / Deviation – actual value / violation, forecast or drift | `Measurement` / `Deviation` |
| Role / Adapter – abstract capability and its connection to a tool | `Role` / `Adapter` |

Do not invent synonyms. Complete model: [`docs/data-model.md`](../docs/data-model.md).

## Core principles (never violate)

1. Intent instead of implementation – the model describes what should apply, never how it is built.
2. Ownership stays with the TPO – the AI never sets targets or limits itself (exception: autonomy level 4, "should" properties only).
3. Everything measurable – a System property without a measurement spec is invalid.
4. Autonomy by risk, per Area – not by technical effort.
5. Fully traceable – Component → Decision → Need or Deviation.
6. Tool-neutral – the core only knows Roles; Jira, GitHub, SRE Agent etc. are interchangeable Adapters.
7. Remit is the source of truth – connected tools only receive copies.
8. Two conflicting "must" properties are never resolved by the AI – they always become a TPO question.

## Tech stack (ADR 0008)

- Backend: .NET 10, ASP.NET Core Minimal APIs; MCP server in the backend at `/mcp` (`ModelContextProtocol.AspNetCore`).
- Frontend: Vue 3 + TypeScript + Vite, served by the backend as static files (one container, one origin).
- Sign-in: backend-for-frontend with Entra ID (cookie for the UI, bearer token only for `/mcp`); no tokens in the browser.
- Persistence: own lean event store on PostgreSQL via Npgsql (ADR 0004, 0007) – no event sourcing framework such as Marten.
- Hosting: Azure Container Apps in the customer's tenant; infrastructure as Bicep under `infra/`.
- Tests: xUnit v3 with plain `Assert` (no extra assertion library), Vitest for the frontend.

## Code layout and conventions

```
src/Remit.Domain          domain core without dependencies (aggregates, events, rules)
tests/Remit.Domain.Tests  tests for domain rules
```

- Build and test: `dotnet test` in the repository root (Microsoft Testing Platform via `global.json`).
- `Directory.Build.props` enables nullable reference types and treats warnings as errors – keep the build warning-free.
- Aggregates are event-sourced: state changes only by applying events; history in the event store, current state in derived tables.
- Domain logic (autonomy, escalation, conflict rules) belongs in `Remit.Domain`, never in Adapters or the API layer.
- Write tests for domain rules first, especially escalation rules and conflict detection between properties.
- UI texts use the TPO's domain language from the vocabulary above – no technical terms in the UI.

## Do not

- Build multi-tenancy – each instance belongs to exactly one customer (ADR 0003).
- Store System properties, Policies or Decisions in Confluence, Jira or GitHub as the truth (ADR 0001).
- Put measured values into the event store – they stay in Azure Monitor and are only referenced (ADR 0004).
- Use product-specific terms (Jira, SRE Agent …) in the domain model.
- Commit secrets – use managed identities instead of keys wherever possible.
- Create Azure resources manually – everything as Bicep under `infra/`.
- Make fundamental decisions silently in code – record them as a new ADR in `docs/adr/` (template `0000-template.md`).
