# CLAUDE.md – Remit

Project context for Claude Code. Read this file completely before you change anything.
Working language for docs, ADRs, UI texts, code, identifiers and commit messages: **English**.

## What this is about

Remit is a web application in which a **Technical Product Owner (TPO)**
describes *what* an IT system should deliver. AI agents decide *how* it is built,
build it and operate it permanently – within limits that the TPO sets.

The TPO understands the overall system but does not need to understand the individual Components
(the "black box"). They **never** see code, Git or resource IDs – only Needs,
System properties, Decisions and results.

In detail: [`docs/vision.md`](docs/vision.md).

## Vocabulary (binding, use exactly like this in code and UI)

| Term | Meaning | Code name |
| --- | --- | --- |
| Need | Need (Bedarf): what someone needs from the system, in domain language, without a solution | `Need` |
| Proposal / Option | Proposal / Option (Vorschlag / Option): AI analysis with solution paths, effect, cost, risks | `Proposal` / `Option` |
| System property | System property (Systemeigenschaft): measurable rule that applies permanently (target, limit, budget …) | `SystemProperty` |
| Decision | Decision (Entscheidung): every change to the system, by AI or TPO, with rationale | `Decision` |
| TPO question | TPO question (Rückfrage): a Decision the AI is not allowed to make itself | `Decision` with `kind = TpoQuestion` |
| Area | Area (Bereich): part of the system to which a Policy applies | `Area` |
| Component | Component (Komponente): technical building block, created by the AI | `Component` |
| Policy | Policy (Richtlinie): autonomy and transparency level of an Area | `Policy` |
| Measurement / Deviation | Measurement / Deviation (Messung / Abweichung): actual value of a property / violation, forecast or drift | `Measurement` / `Deviation` |
| Role | Role (Rolle): abstract capability (ticket system, build agent …), connected via an Adapter | `Role` / `Adapter` |

Complete model: [`docs/data-model.md`](docs/data-model.md).

## Core principles (do not violate)

1. **Intent instead of implementation.** The model describes what should apply, never how it is built.
2. **Ownership stays with the TPO.** The AI never sets targets or limits itself (exception: autonomy level 4, "should" properties only).
3. **Everything measurable.** A System property without a measurement spec is invalid.
4. **Autonomy by risk**, not by technical effort – per Area.
5. **Fully traceable.** Component → Decision → Need or Deviation.
6. **Tool-neutral.** The core only knows Roles. Jira, GitHub, SRE Agent etc. are interchangeable Adapters.
7. **Remit is the source of truth.** Connected tools only receive copies.
8. **Two conflicting "must" properties are never resolved by the AI** – they always become a TPO question.

## Architecture decisions

All decisions are recorded as ADRs in [`docs/adr/`](docs/adr/). Summary:

| ADR | Decision | Status |
| --- | --- | --- |
| 0001 | Build the core ourselves, connect existing tools via Adapters | accepted |
| 0002 | Git and code are invisible to the TPO | accepted |
| 0003 | Delivery into the customer's Azure tenant, one instance per customer | accepted |
| 0004 | History as an event store, current state as derived tables | accepted |
| 0005 | Autonomy is enforced via Azure permissions; no PIM for managed identities | accepted |
| 0006 | Roles are connected via MCP; Remit offers its own MCP server | accepted |
| 0007 | PostgreSQL for event store and tables | proposed |
| 0008 | .NET 10 + Vue 3, one container on Azure Container Apps, MCP server in the backend, xUnit v3 / Vitest | accepted |
| 0009 | Operations agent works through Remit's MCP server; Azure SRE Agent as first Adapter; existing resources adopted | proposed |
| 0010 | Monitoring alerts are derived from System properties | proposed |

Always record new fundamental decisions as a new ADR (template: `docs/adr/0000-template.md`),
never make them silently in code.

## Current state and next step

- Concept, data model and clickable mockup are done.
- PoC with the cache scenario – see [`docs/poc-plan.md`](docs/poc-plan.md).
- The tech stack is decided (ADR 0008). Build and tests: `dotnet test` in the repository root (.NET 10 SDK,
  Microsoft Testing Platform via `global.json`).
- **PoC step 1 (domain core) – first slice done:** event-sourced aggregates (Area, SystemProperty, Need,
  Proposal, Decision, Component, Deviation), escalation rules, contradiction detection and conflict
  resolution between properties, property state evaluation – with tests in `tests/Remit.Domain.Tests`.
- **Version 0.0.1 – clickable preview:** `src/Remit.App` (ASP.NET Core Minimal API, in-memory event store,
  simulated analysis and build agents, read models derived from the streams) and `src/Remit.Web` (Vue 3 app with
  the screens Needs/Proposal, Set System properties, Operation). Tests in `tests/Remit.App.Tests` and Vitest.
  Start with `./start.sh`. The simulated agents and the in-memory store are placeholders, not decisions.
- **Version 0.0.2 – operations agent:** MCP server `/mcp` (`src/Remit.App/Mcp`, ADR 0009) with bearer-key access,
  existing building blocks taken over as Components, Deviations, alerts derived from properties (ADR 0010),
  Bicep for Container Apps with Entra ID sign-in (`infra/`). Guide for the Azure SRE Agent: `docs/sre-agent.md`.
- **Next:** PostgreSQL event store (ADR 0007) behind the same store contract – without it every restart loses the
  properties; then measured values (step 6) and the build agent (step 5).

## Way of working

- Small, verifiable steps. After each step, briefly summarize what changed.
- Everything that runs in Azure as Infrastructure as Code (Bicep) under `infra/`. Nothing manually in the portal.
- No secrets in the repository. Managed identities instead of keys wherever possible.
- UI texts in the TPO's domain language (see vocabulary), no technical terms in the UI.
- Domain logic (rules for autonomy, escalation, conflicts) belongs in the core, not in Adapters.
- Tests for domain rules first, especially: escalation rules and conflict detection between properties.

## What you must NOT do

- Do not build multi-tenancy – each instance belongs to exactly one customer (ADR 0003).
- Do not store System properties, Policies or Decisions in Confluence, Jira or GitHub as the truth (ADR 0001).
- No event store for measured values – they stay in Azure Monitor and are only referenced (ADR 0004).
- No product-specific terms (Jira, SRE Agent …) in the domain model.

## Further documents

- [`docs/vision.md`](docs/vision.md) – idea, roles, flow, business model
- [`docs/data-model.md`](docs/data-model.md) – entities, fields, levels, cache example
- [`docs/poc-plan.md`](docs/poc-plan.md) – scope, steps, acceptance criteria of the PoC
- [`docs/mockup.md`](docs/mockup.md) – clickable mockup of the three screens
