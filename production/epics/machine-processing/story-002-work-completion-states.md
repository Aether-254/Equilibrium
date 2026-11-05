# Story 002: Work integration, completion, blocked output, states and ReasonCodes

> **Epic**: Machine Processing
> **Status**: In Progress — first powered saw line; complete epic acceptance still pending
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/gdd/machine-processing.md`
**Requirement**: `TR-mach-003`, `TR-mach-004`

**Governing ADRs**: ADR-0001 (primary), ADR-0004, ADR-0006
**ADR Decision Summary**: `workDone += P_recv * 1 s`; atomic batch output else Blocked; four states plus composable ReasonCodes exposed for UI.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: one completion per tick; atomic batch insertion.

## Acceptance Criteria

- [ ] Saw (log->plank x2, workRequired 1350) at 135.5 W completes on tick 10; log consumed then (GDD AC 1).
- [ ] `P_recv = 0` for 100 ticks: Working + `Stalled`, no progress, no consumption (GDD AC 2).
- [ ] Output full at completion: Blocked `OutputFull`, inputs consumed, batch pending; inserts when space appears (GDD AC 3).
- [ ] Working machine removed and re-placed resumes with same `workDone` and reservation (GDD AC 6).
- [ ] Utilization over 60 ticks with 42 powered Working ticks = 0.70 (GDD AC 7).
- [ ] States {Idle, Working, Blocked, Starved}; ReasonCodes composable; `tau_req` demanded only while Working.
- [ ] Surplus work carries over capped at `workRequired`; `workRequired 0` completes on first Working tick with content warning.
- [ ] Probabilistic byproduct rolls use `ITieBreakSource.NextKey()` at completion.

## Implementation Notes

2026-11-05: Runtime subset implemented in ProcessingSystem, RecipeInputPlanner and
StraightMechanicalSolver, with Main placement/inspection integration. See
docs/architecture/machine-processing-runtime.md and
production/qa/evidence/machine-processing-evidence.md for exact coverage. Gears, full multi-source
networks, final animations, ledger events and performance acceptance remain pending.


`MachineSystem.Collect` reads `PowerDelivered` payloads from the mechanical solve (same tick, snapshot-based: mechanical system publishes into a transient table before machine collect; register mechanical first and expose via snapshot extension). Completion emits one atomic insertion transaction covering outputs and byproducts across containers; validator checks all fit.

## Out of Scope

- Ledger metrics (economy-ledger reads events)

## QA Test Cases

- GDD AC 1, 2, 3, 6, 7 verbatim
- Carry-over: P_recv 2000 W, workRequired 1350 -> one completion per tick, surplus capped

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/MachineProcessing/WorkAndStatesTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; mechanical-power 002; tick-simulation 004
- Unlocks: Story 003; economy-ledger 002; machine-inspection-ui
