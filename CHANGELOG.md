# Changelog

## 0.0.2 – 2026-11-21

Remit can now be connected to a real operations agent, e.g. the Azure SRE Agent (ADR 0009, ADR 0010).

- MCP server at `/mcp` with the tools `get_rules`, `check_change`, `report_deviation`, `resolve_deviation`,
  `report_decision`, `ask_tpo` and `get_question`. `check_change` applies the escalation rules and feasibility
  checks of the domain core: within the properties the agent may act; a budget overrun, a missed "must"
  target or a change of the data location always becomes a TPO question. `report_decision` refuses changes
  the agent was not allowed to make on its own and that no answered TPO question backs.
- Access to `/mcp` with a bearer key from configuration; closed without one outside local development.
- Existing building blocks (e.g. an existing Redis cache) are taken over as Components from configuration.
- Operation: set System properties for the Area directly, Deviations with their state on the property tiles,
  questions from the operations agent, resolved deviations.
- Alerts from properties: `GET /api/setup/alert-rules(.parameters.json)` and `infra/modules/property-alerts.bicep`
  (response time p95, availability, budget).
- Azure: `infra/main.bicep` and `infra/deploy.sh` (Container Apps, registry, managed identity, Entra ID sign-in).
- Guide: [`docs/sre-agent.md`](docs/sre-agent.md).
- Domain: `Component.Adopt`, `Deviation.Resolve`, `ChangeFeasibility` for described changes,
  `AlwaysAskTrigger.MustPropertyViolated`.

## 0.0.1 – 2026-11-21

First preview that can be clicked through end to end.

- Needs: list, enter a new Need in domain language, reject a Need.
- Proposal: simulated AI analysis with two or three Options (effect, cost, risks, required properties) and a
  recommendation. Recognises response-time and availability needs; everything else gets general Options.
- Set System properties: Remit asks exactly for the `requiredProperties` of the chosen Option (more can be
  added), each with "must"/"should" and how it is checked.
- Building: the escalation rules of the domain core decide. Autonomy 3–4: the simulated build agent decides,
  creates the Component and logs the Decision with rationale. Autonomy 2: it asks for approval. Autonomy 1: it
  only proposes and waits. Budgets apply to the whole Area: if the running cost plus the upper end of the Option's
  cost exceeds a budget, it is always a TPO question with Options, deadline and default option.
- An Option whose expected effect misses a "must" target of the Area is refused when accepting; a missed "should"
  target is noted in the AI's rationale.
- Operation: open TPO questions, System properties with target and expected effect, decision log, system map,
  Policy of the Area (autonomy and transparency) changeable with a rationale. Policy changes and rejected Needs
  are Decisions in the log; answers after the deadline are refused because the default option already applies.
- Technical: `Remit.App` (ASP.NET Core Minimal API, in-memory event store with optimistic concurrency, read models
  derived from the event streams), `Remit.Web` (Vue 3), one container via `Dockerfile`.

Not yet included: real agents (MCP), PostgreSQL, sign-in, measured values, Azure deployment.
