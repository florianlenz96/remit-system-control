# ADR 0002: Git and code are invisible to the TPO

- Status: accepted
- Date: 2026-11-20

## Context

An early idea was "GitOps for systems", with Git as the interface for the target state.
However, the TPO should deal exclusively with Needs, properties and Decisions.

## Decision

The TPO works only in Remit. Git, code, pipelines and resource IDs do not appear in
their UI. Technically, repositories may continue to exist underneath
(e.g. for IaC and the agents' application code), but appear at most as a reference in the log.

## Consequences

- Remit needs its own persistence for intent and history (see ADR 0004).
- The build agent works with repositories; Remit translates results into domain language.
- For developers who do want to look "under the hood", an add-on is conceivable later (e.g. a VS Code extension).

## Rejected alternatives

- **Git as the source of truth for the TPO**: suits developers, not the role of the TPO.
