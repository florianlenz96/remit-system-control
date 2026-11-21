# Connecting the Azure SRE Agent

The Azure SRE Agent takes the Role **operations agent** (ADR 0009). It keeps doing what it is good at –
investigating incidents and mitigating them in Azure – and asks Remit what applies and who decides.
Remit stays the source of truth for targets, budgets and Policies.

```
 Azure Monitor alert (derived from a System property, ADR 0010)
        │
        ▼
 SRE Agent response plan ──► custom agent "remit-operations"
        │                         │  get_rules / check_change / report_* / ask_tpo   (MCP)
        │                         ▼
        │                      Remit  ──► TPO sees deviation, question, decision log
        ▼
 Mitigation in Azure (only if Remit's outcome is "act", or the TPO answered)
```

Steps 1–4 happen once. Everything except the SRE Agent's own settings is scripted.

## Prerequisites

- An Azure subscription, Azure CLI signed in (`az login`), `jq` and `openssl`.
- An Azure SRE Agent (https://sre.azure.com) whose managed resource groups include the system to operate,
  with Azure Monitor as incident platform.
- The system to operate, e.g. an existing Azure Cache for Redis and the Application Insights of the shop.

## 1. Deploy Remit

List the building blocks Remit should take over (they appear on the system map as "existing"):

```json
[
  { "name": "Catalog cache", "kind": "Cache",
    "resourceId": "/subscriptions/<sub>/resourceGroups/<shop-rg>/providers/Microsoft.Cache/Redis/<name>" }
]
```

```bash
infra/deploy.sh rg-remit westeurope existing-components.json
```

The script creates the registry, builds the image, creates the Entra ID app registration for signing in,
deploys the container app and prints the **MCP endpoint** and the **bearer token** for the agent.
Keep the token only in the agent's connector.

> Version 0.0.x keeps its data in memory. A restart or redeployment of Remit loses the System properties,
> questions and decisions; the taken-over building blocks come back from the configuration. PostgreSQL
> (ADR 0007) is the next step.

## 2. Set the System properties (as TPO)

Open Remit, sign in, go to **Operation → Set a system property**, e.g.

- Response time: at most 250 ms, must
- Budget: at most 250 EUR/month, must

and choose the autonomy of the Area (Policy).

## 3. Create the alerts from the properties

While signed in, open `https://<remit>/api/setup/alert-rules` to see what will be monitored, then save
`https://<remit>/api/setup/alert-rules.parameters.json` as `alerts.json` and deploy it into the shop's
resource group:

```bash
az deployment group create --resource-group <shop-rg> \
  --template-file infra/modules/property-alerts.bicep \
  --parameters @alerts.json \
  --parameters appInsightsId="$(az monitor app-insights component show --app <shop-appinsights> --resource-group <shop-rg> --query id -o tsv)"
```

Each alert is called `remit-<property>-<kind>` and carries the property id in its description.
Repeat this step whenever the TPO changes a property.

## 4. Configure the SRE Agent

All in https://sre.azure.com → your agent.

**Connector.** *Build + setup → Extensions → Connectors → Add connector → MCP server*

| Field | Value |
| --- | --- |
| Name | `remit` |
| Connection type | Streamable-HTTP |
| URL | `https://<remit>/mcp` |
| Authentication | Bearer token – the token from step 1 |

The status must change to **Connected**; the tools `get_rules`, `check_change`, `report_deviation`,
`resolve_deviation`, `report_decision`, `ask_tpo` and `get_question` then appear.

**Custom agent.** *Agent Canvas → create custom agent* `remit-operations`, tools: `remit/*` plus the Azure
tools it needs. Instructions:

```text
You operate the online shop on behalf of Remit. Remit is the source of truth for targets, limits and who
decides; never take targets or limits from anywhere else.

For every incident:
1. Call remit get_rules with the affected resource id. The alert description names the Remit property.
2. Investigate as usual. If a property is violated or at risk, call report_deviation with the measured value.
3. Before ANY change, call check_change and describe its consequences: kind, extra monthly cost, current
   monthly cost of the area if you know it (Cost Management), and the expected values after the change
   using the metric and unit of the properties.
4. Follow the outcome:
   - act: make the change, then call report_decision (with deviationId, rationale and how to undo it).
   - ask_approval or ask_tpo: do NOT change anything. Call ask_tpo in plain domain language without
     resource ids or technical terms, with at least two options, a default option and a deadline.
     Poll get_question; act only on the chosen option and pass its questionId to report_decision.
   - propose_only: do not change anything; describe your proposal with ask_tpo.
5. Never resolve two conflicting "must" properties yourself.
6. When the property is fulfilled again, call resolve_deviation with the measured value.
```

**Response plan.** *Incidents → Triggers & response plans → Create a response plan*

| Field | Value |
| --- | --- |
| Severity | Sev1 (must properties) and Sev3 (should properties) |
| Title contains | `remit-` |
| Response subagent | `remit-operations` |
| Agent autonomy level | **Autonomous** for autonomy 3–4 in Remit, **Review** for 1–2 (ADR 0005) |

**Stop hook** (*Build + setup → Extensions → Hooks → Create hook*, event Stop, type prompt):

```text
Did the agent change anything in Azure in this thread? If yes, did it call remit report_decision or
remit ask_tpo for that change? If it changed something without reporting it to Remit, reject with the
reason "Report the change to Remit with report_decision, or ask the TPO with ask_tpo." Otherwise approve.
$ARGUMENTS
```

**Permissions** of the agent's managed identity follow the autonomy level (ADR 0005): Reader on the
shop's resource group for levels 1–2, write access (e.g. *Redis Cache Contributor*) on the resources of the
Area for levels 3–4. For level 1 also add a global tool access policy **deny** `RunAzCliWriteCommands`.

## 5. Try it

- In the SRE Agent chat: *"What does Remit say applies to the catalog cache?"* – the agent calls `get_rules`.
- Make the response-time alert fire (e.g. a short load test, or temporarily set the property to 1 ms in Remit
  and redeploy the alerts). The agent reports a deviation; Remit shows it on **Operation**.
- If the agent wants to scale the cache beyond the budget, `check_change` answers `ask_tpo`. The question
  appears in Remit; after you answer, the agent continues and the decision appears in the log.

## Limits of this slice

- The run mode in the SRE Agent is not synchronized with the autonomy in Remit; change both together.
  `check_change` still applies Remit's current Policy.
- The bearer token is an interim solution (ADR 0009); rotate it by redeploying without `MCP_API_KEY`
  and updating the connector.
- Alerts are deployed by a person (step 3); later the build agent does it.
- Data is kept in memory (see step 1).
