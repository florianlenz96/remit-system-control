# ADR 0007: PostgreSQL for event store and tables

- Status: proposed
- Date: 2026-11-21

## Context

ADR 0004 requires an append-only event store and tables for the current state.
In the PoC, as little infrastructure as possible should be operated.

## Decision (proposal)

Azure Database for PostgreSQL – Flexible Server for both: an event table (append-only,
with a sequential version per aggregate) and regular tables as projections.

## Consequences

- One database; one transaction for event and projection is possible.
- A well-known, well-supported system, also easy to deploy in the customer's tenant.
- With very high event volume, possibly a specialized store later – not to be expected for Remit's history.

## Rejected alternatives

- **Cosmos DB with change feed**: good for event sourcing, but a second database model alongside relational projections.
- **Specialized event store (e.g. EventStoreDB)**: an additional component without need in the PoC.
