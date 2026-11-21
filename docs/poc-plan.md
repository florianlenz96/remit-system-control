# PoC Plan: Cache Scenario

## Goal

Run one scenario for real and end to end – from Need to operation, on real Azure.
Behind the scenes some things may be semi-manual at first; the flow for the TPO must be real.

**Scenario:** "Faster response times in the online shop" → cache for catalog and search
(details: [`data-model.md`](data-model.md#example-cache-for-the-online-shop)).

## Scope

In the PoC:

- One instance in our own Azure subscription, one user (the TPO)
- One example system "Online shop" (minimal demo app with catalog API and database)
- One Area "Catalog and search", autonomy 3, transparency 1
- Needs are created directly in Remit (no Jira)
- The three screens from the clickable mockup: Need, properties, operation

Not in the PoC:

- Jira and Confluence integration
- Multiple Areas, transparency level 3 (data flow view)
- Earned autonomy
- Delivery as a Managed Application

## Steps

0. **Decide the tech stack** (ADR 0008) – together with the user, not alone.
1. **Domain core**: entities, events, projections; rules for escalation and conflicts with tests.
2. **Remit**: web app with the three screens based on the clickable mockup; sign-in via Entra ID.
3. **MCP server of Remit**: read properties and Policies, report Decisions and TPO questions.
4. **Infrastructure**: Bicep for Remit (Container Apps, PostgreSQL, managed identity) and the demo shop.
5. **Build agent**: accepted Option as a task for the GitHub Copilot coding agent; result (PR, deployment) returned as a reference.
6. **Measurement**: read Application Insights and Cost Management; target vs. actual on the operation screen.
7. **Operations agent**: connect SRE Agent; check whether approvals in review mode reach Remit via agent hooks.
8. **Emergency**: a load test creates a conflict target ↔ budget → a TPO question appears for the TPO.

## Acceptance criteria

- [ ] The TPO creates a Need and receives a Proposal with at least two Options.
- [ ] On acceptance, Remit asks for exactly the `requiredProperties` of the Option.
- [ ] The cache is built without manual intervention in the portal; every AI Decision is in the log with a rationale.
- [ ] The operation screen shows target and actual for all properties from real measured values.
- [ ] A conflict between two "must" properties creates a TPO question with Options, deadline and default option.
- [ ] The TPO never sees code, Git or resource IDs anywhere.
- [ ] The entire infrastructure can be deployed via Bicep into an empty subscription.

## In parallel

From week 3, show the clickable mockup and interim status to two or three trusted customers or colleagues.
Watch in particular: the reaction to the autonomy levels.
