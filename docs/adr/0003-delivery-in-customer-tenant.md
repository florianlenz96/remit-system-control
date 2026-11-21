# ADR 0003: Delivery into the customer's Azure tenant, one instance per customer

- Status: accepted
- Date: 2026-11-20

## Context

Options: a local app, SaaS hosted by the vendor, or operation in the customer's tenant.
Agents must run around the clock close to the systems; several people work together;
the TPO is not a developer. The target group is mainly German enterprise customers
(data protection, works council, external access to production systems).

## Decision

Remit is a web application and runs **in the customer's Azure tenant**.
One instance per customer, deployed via Bicep; later as an Azure Managed Application.

- Sign-in via the customer's Entra ID
- Agents with managed identities and minimal permissions
- Models via Microsoft Foundry in one of the customer's EU regions
- Versioned releases; the customer decides when to update

## Consequences

- No multi-tenancy needed in the code.
- Everything must be deployable as Infrastructure as Code – from the start, including in the PoC.
- We need an update channel and support for many separate instances.
- Business model: license per instance plus introduction and support as consulting.

## Rejected alternatives

- **Local app**: round-the-clock operation and collaboration not possible.
- **SaaS hosted by the vendor**: simpler to operate, but hard to sell to the target group.
