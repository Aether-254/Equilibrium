# Story 001: Mechanical network discovery and segment speeds

> **Epic**: Mechanical Power
> **Status**: In Progress — first powered saw line; complete epic acceptance still pending
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/gdd/mechanical-power.md`
**Requirement**: `TR-mech-002`, `TR-mech-004`

**Governing ADRs**: ADR-0004 (primary), ADR-0001
**ADR Decision Summary**: `MechNetworks` rebuilt incrementally via union-find over rotation ports on structural commits; per-network cached segments separated by gear pairs.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Guardrail: 5,000-element solve under 1 ms; no per-tick allocation.

## Acceptance Criteria

- [ ] Shafts, gears, engines, and machine rotation-in ports form networks through adjacent compatible rotation ports only (grid-world Connected); adding/removing a shaft splits/joins networks on the next tick.
- [ ] Segments: shafts pass speed unchanged; a gear pair with N_in/N_out creates a new segment with `omega_out = omega_in / r`, direction flipped.
- [ ] Two engines on one segment with differing `omega_src` -> network Faulted `SourceConflict`; all machines receive 0 (GDD AC 6).
- [ ] Gear loop with inconsistent ratio product -> `GearLoopInconsistent`; solver terminates.
- [ ] Network with no fueled source -> Idle, omega 0 everywhere.
- [ ] Benchmark 5,000 elements under 1 ms (GDD AC 8).

## Implementation Notes

2026-11-05: Runtime subset implemented in ProcessingSystem, RecipeInputPlanner and
StraightMechanicalSolver, with Main placement/inspection integration. See
docs/architecture/machine-processing-runtime.md and
production/qa/evidence/machine-processing-evidence.md for exact coverage. Gears, full multi-source
networks, final animations, ledger events and performance acceptance remain pending.


Union-find keyed by block index with rotation-port adjacency edges; segments found by BFS from the source treating gear pairs as ratio-labelled edges; speed assignment by DFS multiplying ratios, checking consistency at revisits. Block defs: engine (rotation-out, fuel container), shaft (rotation pass-through both ends), gear8/16/32 (rotation in/out with tooth count), saw rotation-in.

## Out of Scope

- Story 002: torque, stall, power delivery, fuel

## QA Test Cases

- **AC-2**: 8:16 gear -> omega halves, sign flips (GDD AC 7)
- **AC-3**: two engines 10 and 12 rad/s -> `SourceConflict`
- **AC-4**: 3-gear loop with mismatched ratios -> `GearLoopInconsistent`
- **AC-6**: benchmark

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/MechanicalPower/NetworkTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: grid-world 002; item-registry 004 (block defs)
- Unlocks: Story 002
