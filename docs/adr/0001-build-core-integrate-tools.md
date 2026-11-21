# ADR 0001: Build the core ourselves, connect existing tools via Adapters

- Status: accepted
- Date: 2026-11-20

## Context

There were two options: develop everything ourselves or connect existing tools (Jira, GitHub,
Azure SRE Agent, Confluence). Customers already have these tools and will not
replace them. The real added value lies in the layer above them.

## Decision

We build ourselves what sets us apart:

- the data model (Need, System property, Policy, Decision …)
- the logic that checks properties and decides on autonomy and escalation
- the user interface for the TPO

Everything else is connected. Where a tool does not fit (e.g. the SRE Agent for part of
operations), we develop that part ourselves – behind the same Role interface.

The truth about intent and properties lives in Remit. Connected tools receive copies.

## Consequences

- Faster to a presentable result; we benefit from the vendors' improvements.
- Dependency on third-party interfaces; every Role needs an interchangeable Adapter.
- System properties must never live only in Confluence or Jira – otherwise Remit is just a UI.

## Rejected alternatives

- **Build everything ourselves**: enormous effort, competition with Microsoft and Atlassian, hard to sell.
- **Only connect**: no core of our own, lowest common denominator, no unique selling point.
