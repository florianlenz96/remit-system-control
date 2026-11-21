# Changelog

## 0.0.1 – 2026-11-21

First preview that can be clicked through end to end.

- Needs: list, enter a new Need in domain language, reject a Need.
- Proposal: simulated AI analysis with two or three Options (effect, cost, risks, required properties) and a
  recommendation. Recognises response-time and availability needs; everything else gets general Options.
- Set System properties: Remit asks exactly for the `requiredProperties` of the chosen Option (more can be
  added), each with "must"/"should" and how it is checked.
- Building: the escalation rules of the domain core decide. Autonomy 3–4: the simulated build agent decides,
  creates the Component and logs the Decision with rationale. Autonomy 2: it asks for approval. Autonomy 1: it
  only proposes and waits. Budget below the cost of the Option: always a TPO question with Options, deadline and
  default option.
- Operation: open TPO questions, System properties with target and expected effect, decision log, system map,
  Policy of the Area (autonomy and transparency) changeable with a rationale.
- Technical: `Remit.App` (ASP.NET Core Minimal API, in-memory event store with optimistic concurrency, read models
  derived from the event streams), `Remit.Web` (Vue 3), one container via `Dockerfile`.

Not yet included: real agents (MCP), PostgreSQL, sign-in, measured values, Azure deployment.
