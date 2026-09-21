# Story 005: Event emission, save boundary hook, and mutation audit

> **Epic**: Tick Simulation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tick-simulation.md`
**Requirement**: `TR-tick-004`, `TR-tick-001`

**Governing ADRs**: ADR-0001: Tick Transaction Engine (primary), ADR-0005: Save Format, ADR-0007: Simulation/Presentation Split
**ADR Decision Summary**: Emit one `CommittedEvent` per commit into an `EventBus`; `TickPump` exposes a boundary hook used by `SaveService`; world tables are `internal` to Simulation so no other assembly can mutate.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation / Presentation)**:
- Required: one event per committed transaction; consumers read events and `IWorldView` only.
- Forbidden: visuals inferring state; writes outside `IWorldWriter`.

## Acceptance Criteria

- [ ] After `Advance()`, `EventBus.Committed` contains exactly one `CommittedEvent` per accepted transaction and one `RejectedEvent` per rejected one, each with `ReasonCode`.
- [ ] `TickPump.OnTickBoundary` fires after Emit and before the next Snapshot; a save requested mid-tick is deferred to that hook (GDD AC 7).
- [ ] Paused: no ticks processed; boundary hook not fired.
- [ ] Mutation audit test: reflection over `Equilibrium.Simulation` finds all world table fields `internal` or `private`; `IWorldView` exposes only readonly views.
- [ ] A fake presentation sink receives events in commit order and can reconstruct the fake world's final state from events plus the initial snapshot.

## Implementation Notes

`EventBus` clears at Snapshot; consumers must read during Emit callbacks or before the next tick. Provide `IPresentationSink.OnBatchCommitted(in CommittedBatch, IWorldView, ulong tick)` per ADR-0007. `TickPump` calls `SaveService`-registered callbacks at the boundary (SaveService itself is the save-load epic).

## Out of Scope

- SaveService implementation (save-load epic)
- Real renderers (presentation stories in conveyor/mechanical epics)

## QA Test Cases

- **AC-1**: event count
  - Given: 3 accepted, 2 rejected transactions
  - When: tick
  - Then: 3 committed events, 2 rejected events with reasons
- **AC-2**: boundary
  - Given: save request issued during Collect
  - When: tick completes
  - Then: hook fires once after Emit; captured state equals post-commit
- **AC-5**: replay from events
  - Given: initial snapshot + committed events
  - When: applied to a copy
  - Then: equals final world

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/TickSimulation/EventBusAndBoundaryTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 004
- Unlocks: save-load, economy-ledger, presentation stories
