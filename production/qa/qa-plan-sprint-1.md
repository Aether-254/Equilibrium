# QA Plan — Sprint 1

> **Sprint**: 1 (2026-09-21 to 2026-10-02)
> **Scope**: tick-simulation 001-005, item-registry 001-004 (Must Have); save-load 001, machine-inspection-ui 001 (Should Have)
> **Author**: creator (solo mode; qa-lead not spawned)
> **Test framework**: Unity Test Framework 1.8, EditMode under `Assets/Tests/EditMode/`, PlayMode under `Assets/Tests/PlayMode/`

## Story Classification

| Story | Type | Evidence | Gate |
|---|---|---|---|
| tick-simulation-001 | Integration | `EditMode/TickSimulation/TickEngineSkeletonTests.cs` | BLOCKING |
| tick-simulation-002 | Logic | `EditMode/TickSimulation/TransactionCommitTests.cs` | BLOCKING |
| tick-simulation-003 | Logic | `EditMode/TickSimulation/ResolveTests.cs` | BLOCKING |
| tick-simulation-004 | Logic | `EditMode/TickSimulation/TieBreakSourceTests.cs` | BLOCKING |
| tick-simulation-005 | Integration | `EditMode/TickSimulation/EventBusAndBoundaryTests.cs` | BLOCKING |
| item-registry-001 | Logic | `EditMode/ItemRegistry/ItemIdTests.cs` | BLOCKING |
| item-registry-002 | Logic | `EditMode/ItemRegistry/TagGraphTests.cs` | BLOCKING |
| item-registry-003 | Logic | `EditMode/ItemRegistry/AddonLoaderTests.cs` | BLOCKING |
| item-registry-004 | Integration | `EditMode/ItemRegistry/ContentSourceParityTests.cs` + PlayMode Addressables load | BLOCKING |
| save-load-001 | Integration | `EditMode/SaveLoad/SchemaV1Tests.cs` + `production/qa/evidence/save-il2cpp-spike-evidence.md` | BLOCKING (test) / ADVISORY (spike note) |
| machine-inspection-ui-001 | UI | `production/qa/evidence/inspection-shell-evidence.md` | ADVISORY |

## Automated Tests Required

Test specs are embedded in each story's `## QA Test Cases` section and are authoritative. Summary of counts:

| Story | Automated cases | Benchmarks (advisory) |
|---|---|---|
| tick-simulation-001 | 4 | 0 |
| tick-simulation-002 | 5 | 1 (10k tx < 4 ms) |
| tick-simulation-003 | 5 | 0 |
| tick-simulation-004 | 4 | 1 (fairness 48-52%) |
| tick-simulation-005 | 3 | 0 |
| item-registry-001 | 5 | 1 (10k interns) |
| item-registry-002 | 5 | 0 |
| item-registry-003 | 6 | 0 |
| item-registry-004 | 3 + 1 PlayMode | 1 (< 200 ms load) |
| save-load-001 | 4 | 0 |

Naming: `test_[scenario]_[expected]`. Determinism: all conflict tests use `FixedTieBreakSource`; no wall clock, no unseeded random. Isolation: each test builds its own `FakeWorld`/`ContentPack`.

## Manual Test Cases

| ID | Story | Steps | Pass condition |
|---|---|---|---|
| M-1 | tick-simulation-001 | Open `Main.unity` after asmdef move | Console has zero errors; shaft baker still bakes from the Editor menu |
| M-2 | item-registry-004 | Run `Equilibrium/Content/Export Reference Addon` | `Addons/_reference/` contains items/recipes/tags/market JSON and schema; JSON validator passes |
| M-3 | save-load-001 | Build Windows IL2CPP, run round-trip scene | Log shows serialized bytes and equal deserialized record; or fallback decision recorded |
| M-4 | machine-inspection-ui-001 | Click a placeholder block, switch tabs, press Esc | Status bar persists across tabs; panel closes; frame cost < 1 ms in Profiler |

## Smoke Test Scope (`/smoke-check sprint`)

1. Project compiles with zero errors and zero new warnings in `Equilibrium.*` assemblies.
2. All EditMode tests pass (`Unity -runTests -testPlatform EditMode` or Test Runner window).
3. `Main.unity` opens; no missing-script warnings.
4. Reflection audit tests (no UnityEngine in pure assemblies; no ITieBreakSource in Editing) pass.

## Playtest Sign-off

Not applicable this sprint (no player-facing behavior beyond the UI shell spike).

## Sign-off

Solo mode: creator records `production/qa/qa-signoff-sprint-1.md` with test run summary and smoke result at sprint end.
