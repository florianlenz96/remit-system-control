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
  only proposes and waits. Budgets apply to the whole Area: if the running cost plus the upper end of the Option's
  cost exceeds a budget, it is always a TPO question with Options, deadline and default option.
- An Option whose expected effect misses a "must" target of the Area is refused when accepting; a missed "should"
  target is noted in the AI's rationale.
- Operation: open TPO questions, System properties with target and expected effect, decision log, system map,
  Policy of the Area (autonomy and transparency) changeable with a rationale. Policy changes and rejected Needs
  are Decisions in the log; answers after the deadline are refused because the default option already applies.
- Technical: `Remit.App` (ASP.NET Core Minimal API, in-memory event store with optimistic concurrency, read models
  derived from the event streams), `Remit.Web` (Vue 3), one container via `Dockerfile`.

Not yet included: real agents (MCP), PostgreSQL, sign-in, measured values, Azure deployment.
