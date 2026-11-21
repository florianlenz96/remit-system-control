Source code of Remit (structure: see ADR 0008).

- `Remit.Domain` – domain core: entities, events, rules for autonomy, escalation and conflicts. No dependencies.
- `Remit.App` – web host: Minimal API, application services (use cases of the TPO), in-memory event store
  (version 0.0.1), simulated agents. Serves the built Vue app from `wwwroot`.
- `Remit.App/Mcp` – MCP server for operations agents (ADR 0009); `Remit.App/Application/RemitService.Operations.cs` holds its use cases.
- `Remit.Web` – Vue 3 single-page app (TypeScript, Vite, Vitest). `npm run build` writes into `Remit.App/wwwroot`.
