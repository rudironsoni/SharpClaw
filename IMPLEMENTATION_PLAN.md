# SharpClaw Implementation Plan (.NET 10 + Microsoft Agent Framework)

## 0) Scope and intent

This plan translates the corrected OpenClaw product investigation into an implementation roadmap for SharpClaw on .NET 10 and Microsoft Agent Framework (AF).

Goals:

- Preserve OpenClaw product behavior where required for parity (protocol, operational workflows, Control UI/API expectations).
- Keep security defaults strict (fail closed, explicit opt-in for insecure behavior).
- Deliver a local-first single-host deployment first, then scale out incrementally.

Non-goals for v1:

- Requiring multi-host clustering for all deployments.
- Requiring Daytona or Kata in baseline self-hosted deployments.
- Full ecosystem channel parity in the first release.

---

## 1) Product-level requirements to preserve

From the corrected investigation, SharpClaw v1 must preserve these externally observable behaviors:

- Gateway default bind is local loopback on port `18789`.
- Multiplexed gateway surface on one port:
  - WS protocol endpoint (control/data plane).
  - HTTP health/status endpoints.
  - Control UI static app + API usage.
  - Optional OpenResponses-compatible HTTP endpoint (`POST /v1/responses`, SSE streaming).
- Handshake and protocol envelope behaviors:
  - `connect` / `hello-ok` shape compatibility.
  - Role model (`operator`, `node`), scope model, capability declarations.
  - Event frame support for optional `seq` and `stateVersion`.
- Error envelope compatibility:
  - String error codes: `NOT_LINKED`, `NOT_PAIRED`, `AGENT_TIMEOUT`, `INVALID_REQUEST`, `UNAVAILABLE`.
- Chat semantics:
  - Non-blocking `chat.send` ACK (`{ runId, status: "started" }`).
  - Idempotency key support for side-effecting methods.
  - `chat.abort` and `chat.inject` behavior.
- Operational workflows:
  - Pairing, approvals, config validate/apply, logs tail, cron management, update/restart, doctor diagnostics.

---

## 2) Solution architecture and project layout

## 2.1 High-level architecture

Use a modular core with clear boundaries, then deploy it in profile-specific topologies.

- `Gateway`: transports, protocol framing, connection/session runtime.
- `Control Plane`: authn/authz, pairing, approvals, config, device registry, scheduling.
- `Runtime`: AF agent orchestration, model routing/failover, tools.
- `Execution Fabric`: pluggable sandbox providers for subagents.
- `Operations`: logs, health, metrics, diagnostics, update workflow.
- `Persistence`: state stores and durability abstractions.

## 2.2 Proposed solution layout

```text
SharpClaw.slnx
  src/
    SharpClaw.Host/                           # Composition root and startup
    SharpClaw.Abstractions/                   # Cross-cutting primitives
    SharpClaw.Protocol.Contracts/             # Wire DTO contracts
    SharpClaw.Protocol.Abstractions/          # Parser/validator interfaces
    SharpClaw.Execution.Abstractions/         # Sandbox provider interfaces
    SharpClaw.Persistence.Abstractions/       # Repository/store interfaces
    SharpClaw.Extensions.Hosting/             # Hosting bootstrap extensions
    SharpClaw.Extensions.DependencyInjection/ # Module registration extensions
    SharpClaw.Gateway/                        # WS/HTTP ingress + dispatch
    SharpClaw.Identity/                       # Pairing, auth, scopes, tokens
    SharpClaw.Conversations/                  # Session and chat behavior
    SharpClaw.Runs/                           # Orchestration, idempotency, leases
    SharpClaw.Approvals/                      # Human-in-the-loop workflows
    SharpClaw.Configuration/                  # Config validation/apply/reload
    SharpClaw.Operations/                     # Health, logging, diagnostics
    SharpClaw.HttpApi/                        # Health/admin HTTP endpoints
    SharpClaw.OpenResponses.HttpApi/          # /v1/responses compatibility
    SharpClaw.Web/                            # Control UI host assets
    SharpClaw.Execution.SandboxManager/       # Spawn/stop broker boundary
    SharpClaw.Execution.Docker/               # Docker-in-Docker provider (default)
    SharpClaw.Execution.Podman/               # Podman-in-container provider (fallback)
    SharpClaw.Execution.Daytona/              # Daytona provider (optional)
    SharpClaw.Execution.Kubernetes/           # Kubernetes provider (+ optional Kata)
    SharpClaw.Persistence.Sqlite/             # Local profile durability
    SharpClaw.Persistence.Postgres/           # Enterprise profile durability
  tests/
    SharpClaw.Protocol.Tests/
    SharpClaw.Identity.Tests/
    SharpClaw.Runs.Tests/
    SharpClaw.Execution.Tests/
    SharpClaw.Integration.Tests/
    SharpClaw.Conformance.Tests/
    SharpClaw.Security.Tests/
    SharpClaw.Load.Tests/
```

## 2.3 Boundary rules

- `*.Contracts` and `*.Abstractions` are dependency roots and never depend on domain or provider projects.
- Domain/context modules (`Identity`, `Conversations`, `Runs`, `Approvals`, `Configuration`) depend on abstractions/contracts only.
- Adapter/provider modules (`Execution.*`, `Persistence.*`, `HttpApi`, `Web`) depend inward on abstractions/domain interfaces, never the reverse.
- `SharpClaw.Execution.*` is the only layer allowed to spawn sandboxes.
- `SharpClaw.Host` composes all modules; inner layers never reference `Host`.

## 2.4 Naming convention

- Use Microsoft-style suffixes for shared foundations: `Abstractions`, `Contracts`, `Extensions.*`.
- Use context/domain names for business modules: `Identity`, `Runs`, `Approvals`, `Conversations`, `Configuration`.
- Use technology/provider suffixes for infrastructure: `Persistence.Sqlite`, `Persistence.Postgres`, `Execution.Docker`, `Execution.Podman`, `Execution.Daytona`, `Execution.Kubernetes`.

---

## 3) AF mapping and custom infrastructure

## 3.1 AF primitives to use directly

- `AIAgent` / `DelegatingAIAgent`: core execution abstraction.
- `AIAgentBuilder`: compose middleware pipeline.
- `FunctionInvocationDelegatingAgent`: tool-calling boundary.
- `ChatClientAgent`: model provider integration.
- `AgentSession` + context providers: conversational/session context.
- `WorkflowBuilder` + checkpointing APIs: long-running orchestration.

## 3.2 SharpClaw custom components (must build)

- WebSocket connection manager + framed protocol engine.
- Handshake negotiator (`connect`/`hello-ok`) with role/scope/cap validation.
- Idempotency registry for side-effecting operations.
- Run queue with lane-aware scheduling and backpressure.
- Pairing/device identity subsystem (challenge-response, signed device identity metadata).
- Approval orchestration subsystem for human-in-the-loop actions.
- Model selection/failover strategy with cooldown and policy limits.
- Config pipeline (validate, hash/version guard, apply, targeted reload).
- OpenResponses API adapter + SSE event mapper.
- Sandbox manager + provider adapters.

---

## 4) Vertical slice (first shippable slice)

Deliver one end-to-end scenario early:

**Scenario**: Local operator opens Control UI, sends `chat.send`, receives streaming output, can abort run.

Slice includes:

1. Host boot with loopback bind on `18789`.
2. WS connect handshake + `hello-ok` with policy/features.
3. Minimal operator auth path with paired local device.
4. `chat.send` command accepted non-blocking with `{runId,status:"started"}`.
5. Runtime executes through AF pipeline and emits `chat.delta`/`chat.done` events.
6. `chat.abort` cancels run and emits terminal status.
7. Basic Control UI wiring for this path.

Acceptance criteria:

- Golden transcript passes for connect/send/stream/abort.
- Event stream ordering stable under normal load.
- No insecure toggles required to complete scenario.

---

## 5) Durability and persistence plan

## 5.1 Store model

Local profile (default): SQLite-backed repositories.

- `Devices` (paired identities, tokens, scope grants).
- `Sessions` (session metadata, lifecycle status).
- `Runs` (run states, idempotency mapping, timestamps).
- `Approvals` (pending/approved/denied with actor and reason).
- `Config` (active config, candidate config, revision hash).
- `Jobs` (cron/scheduled task metadata and execution history).
- `Audit` (security and administrative audit records).

Enterprise profile:

- Postgres for source-of-truth state.
- Redis for hot coordination/idempotency cache.
- NATS/Kafka for event backplane.
- Object storage for large artifacts/transcripts.

## 5.2 Consistency guarantees

- At-least-once request handling with idempotency dedupe for side-effecting methods.
- Monotonic state transitions for runs/sessions.
- Durable run terminal state before emitting final completion event when feasible.
- Config apply is revision-guarded (`expectedRevision` semantics).

## 5.3 Recovery behavior

- On restart: restore paired devices, config, active sessions metadata, pending approvals.
- In-flight run execution is resumable only where AF workflow/checkpoint semantics permit; otherwise marked interrupted and surfaced to clients.
- Startup reconciliation transitions orphaned in-progress runs.

---

## 6) Security model

## 6.1 Baseline defaults (must hold)

- Loopback bind default (`127.0.0.1:18789`).
- Device identity and pairing required by default.
- Scope enforcement per method on every request.
- Rate limiting and payload size limits enabled.
- Private-network URL access denied by default for outbound fetch/tool URL operations unless allowlisted.

## 6.2 Hard container boundary requirements

- **Never mount host `/var/run/docker.sock` into SharpClaw or subagent containers.**
- Spawn actions must go through `SandboxManager` only.
- Per-sandbox constraints: `read_only`, dropped capabilities, `no-new-privileges`, seccomp/apparmor, CPU/memory/pids/time limits.
- Per-run short-lived credentials (no long-lived host credentials in sandbox env).
- Sandbox network default-deny egress with explicit allowlists.

## 6.3 Insecure toggle policy

If insecure compatibility toggles are implemented, they must all be:

- Off by default.
- Explicitly named as dangerous in config.
- Startup-warned and audit-logged when enabled.
- Covered by regression tests ensuring defaults remain secure.

## 6.4 Authorization model

- `operator` role: method access governed by scopes (`read`, `write`, `admin`, `approvals`, `pairing`).
- `node` role: permissions constrained to declared capabilities/commands.
- Policy engine returns canonical protocol errors (`INVALID_REQUEST`, `UNAVAILABLE`, etc.) without leaking sensitive internals.

---

## 7) Subagent execution and sidecar model

## 7.1 Subagent lifecycle under SharpClaw umbrella

1. `chat.send` accepted and run created.
2. Scheduler selects provider by policy (default `dind`).
3. SandboxManager spawns isolated sandbox/sidecar.
4. Sidecar bootstraps and registers as protocol `node` with declared `caps/commands/permissions`.
5. Work executes and streams events through backplane to edge.
6. Lease heartbeat maintained; expiry triggers reclaim/requeue.
7. On completion/abort/timeout, sidecar is torn down and audited.

## 7.2 Provider selection policy (locked)

- Default: `dind`
- Fallback: `podman`
- Optional: `daytona`
- Enterprise option: `k8s` with optional `kata` runtime class routing for sensitive workloads

## 7.3 Scheduler and lease semantics

- Run lease state: `assigned`, `running`, `draining`, `expired`, `reclaimed`, `completed`.
- Heartbeats required from provider runtime.
- Expired lease behavior configurable:
  - resume from checkpoint if possible
  - otherwise mark run interrupted and notify client.

---

## 8) Deployment profiles

## 8.1 Profile A: Self-hosted Docker / Compose

Objectives:

- One-command bring-up.
- Strong local isolation for spawned subagents.

Reference services:

- `sharpclaw`
- `sandbox-manager`
- `postgres` (or sqlite-only lightweight mode)
- `redis`
- `nats`

Network model:

- `edge_net`: ingress to SharpClaw only.
- `control_net`: SharpClaw + state/event infra.
- `sandbox_net`: sandbox manager + spawned sidecars.

## 8.2 Profile B: Enterprise Kubernetes

This profile is optional for initial v1 rollout and becomes primary as enterprise requirements grow.

Objectives:

- Horizontal edge/control scale.
- Policy-driven execution placement.

Reference workloads:

- `sharpclaw-edge` (HPA)
- `sharpclaw-control`
- `sharpclaw-scheduler`
- `sharpclaw-sandbox-manager`
- provider workers (`dind`/`podman`/`daytona`/`k8s`)

Isolation add-ons:

- NetworkPolicy + RBAC + namespace boundaries.
- `runtimeClass: kata` for high-isolation jobs.

---

## 9) Testing strategy

## 9.1 Component-scoped test projects

Current component test projects:

- `tests/SharpClaw.Abstractions.UnitTests`
- `tests/SharpClaw.Protocol.UnitTests`
- `tests/SharpClaw.Gateway.UnitTests`
- `tests/SharpClaw.Runs.UnitTests`
- `tests/SharpClaw.Gateway.IntegrationTests`
- `tests/SharpClaw.Persistence.IntegrationTests`
- `tests/SharpClaw.Runs.IntegrationTests`
- `tests/SharpClaw.Gateway.End2EndTests`

Planned additions as components land:

- `tests/SharpClaw.Identity.UnitTests` / `tests/SharpClaw.Identity.IntegrationTests`
- `tests/SharpClaw.Execution.UnitTests` / `tests/SharpClaw.Execution.IntegrationTests`
- `tests/SharpClaw.Execution.Docker.UnitTests` / `tests/SharpClaw.Execution.Docker.IntegrationTests`
- `tests/SharpClaw.Execution.Podman.UnitTests` / `tests/SharpClaw.Execution.Podman.IntegrationTests`
- `tests/SharpClaw.Execution.Daytona.UnitTests` / `tests/SharpClaw.Execution.Daytona.IntegrationTests`
- `tests/SharpClaw.OpenResponses.UnitTests` / `tests/SharpClaw.OpenResponses.IntegrationTests` / `tests/SharpClaw.OpenResponses.End2EndTests`

## 9.2 Coverage model (applies per component issue)

- Unit: deterministic logic boundaries and invariants for that component.
- Integration: component interactions and provider/repository roundtrips.
- End2End: scenario-level contracts through gateway/api surfaces.
- Conformance: transcript and envelope compatibility checks.
- Security: auth/scope/replay/egress/socket-mount guardrails.
- Performance: throughput, latency, and sidecar spawn/load behavior where relevant.

## 9.3 CI gates

- Build + test on every PR.
- Component test projects are required to pass before issue closure.
- Conformance suite required for merge.
- Security suite required for merge on security-sensitive changes.
- Basic load/perf smoke on main branch.

---

## 10) Dependency matrix and maximum parallelism plan

This plan is intentionally dependency-first (not time-based). Work moves when prerequisites are satisfied.

## 10.1 Work packages

- `WP-01` Protocol contracts + validators (`connect`/`hello-ok`, frames, errors)
- `WP-02` Gateway core (WS/HTTP ingress, dispatch, keepalive)
- `WP-03` Identity + auth (pairing, scopes, device identity)
- `WP-04` Runs core (`chat.send` ACK, streaming, abort, idempotency)
- `WP-05` AF runtime integration + tool pipeline
- `WP-06` Persistence core (sessions/runs/config/audit repos)
- `WP-07` Sandbox manager boundary
- `WP-08` `dind` provider (default)
- `WP-09` `podman` provider (fallback)
- `WP-10` `daytona` provider (optional)
- `WP-11` OpenResponses HTTP API surface
- `WP-12` Control UI integration path
- `WP-13` Enterprise provider (`k8s` + optional `kata` placement)
- `WP-14` Conformance + security + load harness

## 10.2 Dependency matrix

| Work Package | Depends On | Blocks |
|---|---|---|
| `WP-01` Protocol contracts + validators | None | `WP-02`, `WP-03`, `WP-04`, `WP-11`, `WP-14` |
| `WP-06` Persistence core | None | `WP-03`, `WP-04`, `WP-07`, `WP-14` |
| `WP-05` AF runtime integration | None | `WP-04`, `WP-12`, `WP-14` |
| `WP-02` Gateway core | `WP-01` | `WP-03`, `WP-04`, `WP-11`, `WP-12`, `WP-14` |
| `WP-03` Identity + auth | `WP-01`, `WP-02`, `WP-06` | `WP-04`, `WP-12`, `WP-14` |
| `WP-04` Runs core | `WP-01`, `WP-02`, `WP-03`, `WP-05`, `WP-06` | `WP-07`, `WP-11`, `WP-12`, `WP-14` |
| `WP-07` Sandbox manager | `WP-04`, `WP-06` | `WP-08`, `WP-09`, `WP-10`, `WP-13`, `WP-14` |
| `WP-08` `dind` provider | `WP-07` | `WP-14` |
| `WP-09` `podman` provider | `WP-07` | `WP-14` |
| `WP-10` `daytona` provider (optional) | `WP-07` | `WP-14` |
| `WP-11` OpenResponses HTTP API | `WP-01`, `WP-02`, `WP-04` | `WP-14` |
| `WP-12` Control UI integration | `WP-02`, `WP-03`, `WP-04`, `WP-05` | `WP-14` |
| `WP-13` Enterprise provider (`k8s`/`kata`) | `WP-07` | `WP-14` |
| `WP-14` Conformance/security/load harness | `WP-01`, `WP-02`, `WP-03`, `WP-04`, `WP-05`, `WP-06`, `WP-07`, `WP-08`, `WP-09`, `WP-11` | Release readiness |

## 10.3 Maximum parallelism frontiers

- **Frontier A (independent roots)**: `WP-01`, `WP-05`, `WP-06`
- **Frontier B (after A)**: `WP-02`
- **Frontier C (after B + A)**: `WP-03`
- **Frontier D (core behavior gate)**: `WP-04`
- **Frontier E (parallel execution-fabric burst)**: `WP-07`, then `WP-08` + `WP-09` + `WP-10` + `WP-13` in parallel
- **Frontier F (surface expansion in parallel)**: `WP-11` and `WP-12`
- **Frontier G (global verification gate)**: `WP-14`

This topology maximizes concurrent work while preserving correctness gates.

---

## 11) Known risks and mitigations

- **Protocol drift risk**: upstream OpenClaw evolves.
  - Mitigation: pin transcript suites, versioned protocol adapters.
- **AF preview/stability risk**: AF RC APIs may shift.
  - Mitigation: isolate AF calls behind runtime adapter interfaces.
- **Container boundary risk**: accidental host daemon exposure.
  - Mitigation: strict no-host-socket policy + automated checks.
- **Operational complexity risk**: multiple providers and profiles.
  - Mitigation: provider contract discipline, staged milestones, compatibility suite.

---

## 12) Beads issue graph (dependency-first tracking)

Track the plan in Beads using one issue per work package (`WP-*`) and explicit dependency edges.

- Create parent epic: `SharpClaw dependency-matrix execution plan`.
- Create child issues for `WP-01` through `WP-14`.
- Encode edges exactly as in Section 10.2 using `bd dep add`.
- Drive execution from `bd ready` to always pull maximum parallel-ready work.
- Enforce release gate by keeping `WP-14` blocked on all mandatory prerequisites.

Suggested operators:

- `bd ready` to surface parallel-ready issues.
- `bd dep tree <issue-id>` to verify dependency closure.
- `bd blocked` to inspect blockers and unblock strategy.
- `bd graph` to validate overall DAG shape.
