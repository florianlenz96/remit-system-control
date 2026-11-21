# ADR 0004: History as an event store, current state as derived tables

- Status: accepted
- Date: 2026-11-21

## Context

Remit must be able to prove who decided what and when, and which state of
properties and Policies applied at a given point in time (audit, GDPR accountability,
evaluation for earned autonomy). At the same time, the UI needs fast queries
on the current state.

## Decision

- **Event store (append-only)** for the history: Decisions, changes to System properties
  and Policies, status changes of Needs and Proposals.
- **Tables** as projections for the current state.
- **Raw measured values stay in Azure Monitor**; Remit stores only references and, where needed, aggregates.

## Consequences

- Versioning is built in: every earlier state can be reconstructed.
- Projections must be rebuildable; events are versioned and must never be changed.
- More complexity than plain CRUD – deliberately only for the history, not for everything.

## Rejected alternatives

- **Tables with change history only**: history gets bolted on afterwards and has gaps.
- **Measured values in the event store**: too much data; Azure Monitor is made for that.
