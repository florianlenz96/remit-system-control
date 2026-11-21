# Clickable Mockup

Interactive mockup: [Kommandozentrale – concept mockup](https://claude.ai/artifact/RN25QLDujYidGBDKAJ24so)
(all numbers are sample data).

## Screen 1 – Need and AI Proposal

- Header: Need B-042 with origin (Jira KUNDE-318), description in domain language, actual values
  (p95 820 ms, 1,400 ms in the evening), status "Waiting for TPO Decision".
- AI analysis with cause and three Options; the recommended Option shows effect, cost, risk
  and which properties the TPO must set.
- Actions: "Accept and set properties", "Ask a question".
- Sidebar: system map with Components and their state (new Component as "proposed"),
  further Needs.

## Screen 2 – Set System properties

- Guiding sentence: "You set what must apply. The AI decides how."
- Groups: target (p95, availability), freshness (TTL for prices, texts; never cache stock levels),
  failure and backup (behavior, backup, RTO), budget and constraints (budget, data location),
  autonomy (level 1–4).
- Sidebar: "What the AI does with it" – decides itself / asks you; check before approval.
- Action: "Approve – AI implements and operates".

## Screen 3 – Operation

- Target vs. actual tiles per property (met / at risk / violated).
- Highlighted TPO question: "Black Friday: target and budget cannot both be met" with three Options.
- The AI's decision log with date and rationale.
- Before/after comparison of response time; note on mirroring to Jira and Confluence.

## Not yet designed

- System graph as an overall overview (entry point)
- Data flow view for Areas with transparency level 3
