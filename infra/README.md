Bicep templates for Remit (ADR 0003, ADR 0008).

| File | Content |
| --- | --- |
| `main.bicep` | Remit on Azure Container Apps: registry, managed identity (AcrPull), environment with Log Analytics, container app (one replica), Entra ID sign-in via Easy Auth, secret for the operations agent's key |
| `modules/property-alerts.bicep` | Azure Monitor alerts and budgets derived from System properties (ADR 0010); parameters come from Remit |
| `deploy.sh` | Deploys `main.bicep` end to end, including image build and app registration |

Usage: [`docs/sre-agent.md`](../docs/sre-agent.md). The demo shop follows (PoC step 4).
