Source code of Remit (structure: see ADR 0008).

- `Remit.Domain` – domain core: entities, events, rules for autonomy, escalation and conflicts. No dependencies.
- `Remit.App` – web host: Minimal API, application services (use cases of the TPO), in-memory event store
  (version 0.0.1), simulated agents. Serves the built Vue app from `wwwroot`.
- `Remit.Web` – Vue 3 single-page app (TypeScript, Vite, Vitest). `npm run build` writes into `Remit.App/wwwroot`.
