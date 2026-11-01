# SHINO // AXIS

**One core. Many possibilities.**

> **Status:** Foundation / Discovery. No implementation or technology stack has been approved.

SHINO // AXIS is a proposed **modular Windows-first platform** for connecting, operating, and extending the SHINO software ecosystem from one coherent desktop application.

AXIS is **not** a rewrite of every SHINO project, not a replacement for their domain expertise, and not a new dependency imposed on standalone applications. It is a common home for reusable capabilities, integrations, events, device connections, and user-facing controls.

## Vision

- **One Windows product:** a coherent interface, tray entry, installation, configuration, health overview, and supervised lifecycle.
- **Extensibility:** new capabilities can be added as modules or connected services without rebuilding the platform.
- **Reusable contracts:** standard inputs, outputs, state, events, commands, permissions, and versioning.
- **Device independence:** communicate with existing Android TV and SmallTV clients, and leave room for future hardware.
- **No VM required for the Windows host:** Windows can host the integration runtime and network gateway natively.
- **Preserve investments:** integrate functioning projects using adapters before considering targeted migrations.
- **Local-first and safe:** least privilege, controlled pairing, real data provenance, auditable actions, graceful offline behavior.

## Existing ecosystem — integration candidates

| Project | Current responsibility | AXIS direction |
| --- | --- | --- |
| [SHINO // CONTROL](https://github.com/shinobione/shinobione-shino-control) | Project radar, GitHub and ChatGPT-derived project state | Integrate existing business logic, not rewrite blindly |
| SHINO // LINK | PC, GPU, RAM, network and media telemetry | Integrate as a data producer, then evaluate migration |
| [CODEX MONITOR / PERISCOPE](https://github.com/shinobione/SHINO-CODEX-MONITOR) | Codex quotas and evidence-based local activity | Reuse trusted collectors and their freshness semantics |
| SHINO // RELAY / HUB | Transport, validation, authentication and distribution | Preserve working contracts, migrate domain by domain |
| [SHINO Television Android](https://github.com/shinobione/SHINO-Television-Android) | Native Android TV dashboard on Freebox Pop | Remain a device client |
| [SHINO // TV](https://github.com/shinobione/SHINO-TV) | ESP8266 SmallTV 240×240 display | Remain a constrained device client |
| SHINO // BRAIN | Local knowledge/memory vault and search | Optional service integration |
| [SHINO-OS](https://github.com/shinobione/shino-os) | Local-first voice/AI cockpit | Optional provider; never mandatory for telemetry |

These are **candidates**, not promised migrations. Check current local worktrees, deployed binaries, devices and GitHub branches before making changes. GitHub alone is not proof of the active installation.

## Proposed architecture — subject to validation

```text
SHINO // AXIS (Windows product)
  ├─ Desktop shell + system tray
  ├─ Integration runtime / module lifecycle
  ├─ Internal contracts: source, state, event, command, capability
  ├─ Adapters to existing SHINO applications
  ├─ Device gateway (authentication, compatibility, diagnostics)
  └─ Local configuration, persistence, health and logs
             │
             ├─ Android TV client
             ├─ SmallTV client (resource-constrained protocol)
             └─ Future devices / integrations
```

**Technology is deliberately undecided.** C#/.NET, WebView2, Tauri, Go, Rust and existing open-source platforms remain options until focused proofs compare integration cost, reliability, resources, licensing and maintainability.

## Non-negotiable guardrails

1. **No changes to live installations or hardware without explicit targeted approval.** No firmware flash, OTA, device reconfiguration, VM removal, public publication or destructive cleanup as part of discovery.
2. **No duplicate authorities:** one authoritative producer per domain at a time, with a tested switchover/rollback.
3. **Do not degrade existing contracts:** preserve the Android HUB v2 snapshot protocol, authentication, freshness and availability semantics until a compatible replacement is verified.
4. **Do not expose CONTROL's loopback-only service to LAN.** The future device gateway must have its own security boundary.
5. **No simulated live facts:** missing/expired/unverified source data stay clearly labeled.
6. **No mandatory background AI or VM** for ordinary monitoring and device delivery.
7. **No speculative framework-building:** demonstrate a real module and a real remote client before expanding the plugin system.
8. **No automatic migration of unrelated projects:** standalone applications can stay standalone.

## Roadmap

1. **Inventory & contracts** — audit actual local sources, dependencies, ports, licenses, schemas, running processes, and device firmware states. Capture baseline CPU/RAM and real data flows.
2. **Reversible comparison laboratory** — test the smallest viable Windows runtime and module interface against at least one existing platform; connect two independent producers and simulated clients.
3. **Unified Windows shell** — expose project state, telemetry, media and Codex through one application, initially with adapters to proven implementations.
4. **Device gateway migration** — preserve Android/SmallTV compatibility, validate data parity and security, and retire RELAY/HUB dependencies only after rollback-tested switchover.
5. **New module proof** — add a genuinely new module without changes to the core, proving that future ideas become modules rather than isolated applications.

**First milestone:** an evidence-backed architecture decision and a lab demonstrating integration without touching production.

## Related documentation

- [Architecture hypotheses](docs/ARCHITECTURE.md)
- [Execution roadmap](docs/ROADMAP.md)
- [Engineering constraints for agents](AGENTS.md)

## License

**Undecided.** This repository is public, but no license has been granted by default. Choose a project license deliberately after auditing any reused third-party code and components.
