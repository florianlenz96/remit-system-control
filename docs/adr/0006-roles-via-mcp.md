# ADR 0006: Roles are connected via MCP; Remit offers its own MCP server

- Status: accepted
- Date: 2026-11-20

## Context

Customers use different tools (Jira or Azure Boards, GitHub or Azure Repos).
MCP servers already exist for many of them. The Azure SRE Agent can connect MCP servers itself and
can be controlled from outside via the Azure MCP Server
(https://learn.microsoft.com/azure/sre-agent/mcp-server).

## Decision

- The core only knows Roles: ticket system, repository, build agent, operations agent, measurement source, documentation.
- Concrete tools are connected as Adapters, preferably via MCP.
- Remit offers its own MCP server. Through it, agents read System properties and
  Policies and report Decisions, Deviations and TPO questions.

## Consequences

- Switching a tool means swapping an Adapter, not changing the core.
- Agents query the truth directly from Remit instead of from copies.
- Remit's MCP server is a security-critical interface (authentication via managed identity / Entra ID).

## Rejected alternatives

- **Point-to-point integrations per tool**: high maintenance effort, poorly interchangeable.
