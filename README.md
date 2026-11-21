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

Concept phase complete, PoC starting. See [`docs/poc-plan.md`](docs/poc-plan.md).

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
