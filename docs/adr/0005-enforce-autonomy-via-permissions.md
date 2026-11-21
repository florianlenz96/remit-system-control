# ADR 0005: Autonomy is enforced via Azure permissions; no PIM for managed identities

- Status: accepted
- Date: 2026-11-21

## Context

Autonomy levels should not merely be agreed but technically guaranteed – a strong
argument for security officers. For level 2 ("implements after approval of each
step"), just-in-time access via Microsoft Entra PIM would be the obvious choice.

According to the Microsoft documentation, **no eligible role assignments** can be created for
applications, service principals and managed identities, because they cannot perform the
activation themselves:
https://learn.microsoft.com/azure/role-based-access-control/pim-integration

## Decision

| Level | Enforcement | SRE Agent mode |
| --- | --- | --- |
| 1 | Managed identity with read permissions only | Review |
| 2 | Approval of each step in Remit (by the agent, not by permissions) | Review |
| 3 | Write permissions on the Area's resources, limited by Azure Policy | Autonomous |
| 4 | as 3; additionally the agent may change "should" properties via Remit | Autonomous |

We start without just-in-time permissions.

## Consequences

- Levels 1 and 3 are technically guaranteed, level 2 only by the agent's behavior.
- Conceivable later: on approval, Remit itself grants a role and revokes it afterwards.
  For that, Remit would need the right to assign roles (e.g. "Role Based Access Control
  Administrator" with conditions) – making it a particularly sensitive target.
- To be checked in the PoC: whether SRE Agent approvals in review mode reach Remit via agent hooks.

## Rejected alternatives

- **PIM eligible assignments for managed identities**: not supported by Azure.
- **Autonomy only as configuration without permissions**: weaker security promise.
