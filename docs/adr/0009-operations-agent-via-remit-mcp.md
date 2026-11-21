# ADR 0009: The operations agent works through Remit's MCP server; Azure SRE Agent is the first Adapter

- Status: proposed
- Date: 2026-11-21

## Context

ADR 0006 decided that agents read System properties and Policies from Remit and report back via
Remit's own MCP server. The first operations agent is the Azure SRE Agent. What it offers
(as of November 2026, https://learn.microsoft.com/azure/sre-agent/):

- Custom MCP servers are connected as **Streamable-HTTP connectors**. Authentication: bearer token,
  custom headers, or spec-compliant OAuth (interactive sign-in, public HTTPS). Managed identity is only
  available for stdio connectors that run inside the agent.
- Incidents (Azure Monitor alerts, PagerDuty, ServiceNow) are routed by **response plans** to a custom agent
  with run mode **Review** or **Autonomous**. In Review mode an SRE Agent Administrator approves Azure write
  actions.
- **Hooks** exist only for `Stop` and `PostToolUse` – there is no hook before a tool call. **Tool access
  policies** can deny tools globally (e.g. all Azure CLI write commands).

The SRE Agent knows resources, alerts and logs, but not the TPO's targets, budgets, ownership or which
conflicts it may not resolve itself (principle 8). Remit must supply exactly that, without becoming
an operations tool itself.

Existing systems usually already have resources (e.g. a Redis cache) that were not built through Remit.

## Decision

1. **Contract of the MCP server** (`/mcp`, tool-neutral, no SRE Agent terms):

   | Tool | Purpose |
   | --- | --- |
   | `get_rules` | Area, Policy (with what it allows the agent), active System properties, Components and open TPO questions for a resource or Area |
   | `check_change` | The agent describes an intended change (kind, extra monthly cost, expected effects, data location). Remit answers with the outcome of the escalation rules and feasibility checks of the core: may act / needs approval / only propose / must ask the TPO, and why |
   | `report_deviation` / `resolve_deviation` | A property is violated, at risk or drifted – and later fulfilled again |
   | `report_decision` | An AI Decision with rationale, linked to a Deviation; refused if the escalation rules do not allow it |
   | `ask_tpo` | A TPO question with Options, default option and deadline |
   | `get_question` | Status and answer of a TPO question, so the agent can continue |

   The rules stay in the core (escalation, feasibility); the MCP layer only translates.

2. **Authentication, interim:** because the SRE Agent's HTTP connectors cannot use managed identity, `/mcp`
   accepts a bearer token (`Authorization: Bearer <key>`). The key is generated at deployment, stored only
   as a Container Apps secret and in the SRE Agent connector – never in the repository. Without a configured
   key `/mcp` is closed, except in local development. This deviates from ADR 0008 (Entra tokens of the
   agent's managed identity) and is to be replaced by OAuth with Entra ID once the agent's OAuth flow works
   against Entra.

3. **Enforcement** stays as in ADR 0005: the agent's Azure permissions follow the autonomy level of the
   Area. On top: response plans of Areas with autonomy 1–2 run in Review mode, level 1 additionally denies
   Azure CLI write commands via a tool access policy, and a `Stop` hook rejects any response with a
   mitigation that was not reported to Remit via `report_decision` or `ask_tpo`.

4. **Existing resources are adopted as Components.** Setup (not the TPO) lists them with their resource
   reference; Remit records a TPO Decision "taken over" so that every Component stays traceable (principle 5).
   The TPO can then set System properties for the Area directly, without a Need.

## Consequences

- The SRE Agent can be connected with portal configuration only; Remit needs no SRE Agent-specific code.
  Another operations agent can use the same tools.
- Level 2 and the reporting duty rely on agent behavior and hooks, not on permissions (as in ADR 0005).
- A static key is weaker than managed identity: it must be rotated, and whoever has it can act as the
  operations agent. Mitigation: one key per agent, Container Apps secret, HTTPS only, Decisions are logged.
- Resource references reach Remit but are never shown to the TPO (ADR 0002).

## Rejected alternatives

- **Remit calls the SRE Agent's data plane** (threads, tasks) instead of the agent calling Remit: couples the
  core to one product and inverts the source of truth.
- **Rules only as free text in the SRE Agent's instructions**: not measurable, not traceable, and the
  agent could resolve "must" conflicts itself.
- **Stdio connector with managed identity**: would require shipping Remit's MCP server as a package inside
  the agent's container, with its own database access – a second instance of the truth.
