# ADR 0010: Monitoring alerts are derived from System properties

- Status: proposed
- Date: 2026-11-21

## Context

The operations agent reacts to alerts. If alerts are maintained by hand, targets live twice – as System
properties in Remit and as thresholds in Azure Monitor – and drift apart. Measured values stay in Azure
Monitor (ADR 0004); Remit must not become a monitoring system.

## Decision

Remit derives an alert rule from every active numeric System property with a known measurement source and
hands it over as Bicep parameters to `infra/modules/property-alerts.bicep`. Each alert carries the property's
id in its name and description, so the operations agent can map an incident back to the property via
`get_rules`. "Must" properties become severity 1, "should" properties severity 3.

Supported in the first slice:

| Property | Measurement source | Alert |
| --- | --- | --- |
| Response time (p95) | Application Insights requests | log search alert on the 95th percentile of the request duration |
| Availability | Application Insights requests | log search alert on the share of successful requests |
| Budget | Cost Management | budget with notification at 80 % (forecast) and 100 % (actual) |

Other properties (freshness, exclusion, failure behavior, location) are checked by the build agent, Azure
Policy or experiments, not by alerts, and are listed as "not covered by an alert" in the output.

## Consequences

- Changing a target in Remit and redeploying the alert module is enough to change the monitoring.
- Deployment is still a step that someone (later: the build agent) runs; Remit does not write to Azure itself
  in this slice.
- The mapping metric → signal is product-specific and lives in the application layer, not in the domain core.

## Rejected alternatives

- **Remit creates alerts directly via ARM at runtime**: needs write permissions for Remit itself and bypasses
  Infrastructure as Code.
- **Hand-written alerts**: targets would exist twice.
