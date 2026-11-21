# Remit

**Intent in, operated system out.**

A web application in which a Technical Product Owner describes what an IT system should deliver.
AI agents decide how it is built, build it and operate it – within limits that the TPO sets
per Area (autonomy and transparency levels).

```
Need  →  AI Proposal  →  TPO sets system properties  →  AI builds  →  AI operates
                                                           ↑                │
                                                           └── Deviation ───┘
```

## Status

Version **0.0.1** – first preview you can click through (see [CHANGELOG](CHANGELOG.md)).
PoC plan: [`docs/poc-plan.md`](docs/poc-plan.md).

## Try it

**In GitHub Codespaces (recommended):** open the repository in a Codespace (*Code → Codespaces → Create*),
then run

```bash
./start.sh
```

and open the forwarded port 5000 when the Codespace offers it. The port is private to you by default.

**Locally** (needs .NET 10 SDK and Node 22): the same `./start.sh`, then open http://localhost:5000.

**As a container:**

```bash
docker build -t remit:0.0.1 .
docker run --rm -p 8080:8080 remit:0.0.1
```

then open http://localhost:8080.

What you can do in 0.0.1:

1. Enter a Need in your own words – the AI analysis answers with a Proposal of two or three Options.
2. Accept an Option – Remit asks exactly for the System properties that Option requires.
3. Approve – within autonomy level 3 the AI builds on its own and logs its Decision with a rationale.
4. Set a budget below the cost of the Option – the AI may not decide that itself and asks you (TPO question
   with Options, deadline and default option).
5. Change the Policy of the Area (autonomy 1–4) on the Operation screen and see how the AI's behaviour changes.

Limits of the preview: the AI agents are simulated (keyword-based, prepared Options), nothing is built in a
real system, no sign-in, no measured values yet, and all data lives in memory until the next restart.

Development: `dotnet test` (backend), `npm --prefix src/Remit.Web test` (frontend),
`npm --prefix src/Remit.Web run dev` for the Vue dev server on port 5173 next to `dotnet run --project src/Remit.App`.

## Documentation

| Document | Content |
| --- | --- |
| [CLAUDE.md](CLAUDE.md) | Project context for Claude Code |
| [.github/copilot-instructions.md](.github/copilot-instructions.md) | Project context for GitHub Copilot |
| [.devcontainer/](.devcontainer/) | Codespaces / dev container setup (.NET 10, Node, Azure CLI + Bicep, PostgreSQL) |
| [docs/vision.md](docs/vision.md) | Idea, roles, flow, business model |
| [docs/data-model.md](docs/data-model.md) | Data model with cache example |
| [docs/poc-plan.md](docs/poc-plan.md) | PoC: scope, steps, acceptance |
| [docs/mockup.md](docs/mockup.md) | Clickable mockup of the UI |
| [docs/adr/](docs/adr/) | Architecture decisions |
