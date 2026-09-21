# Story 002: Deterministic insertion with single auto-tidy

> **Epic**: Container
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/container.md`
**Requirement**: `TR-cont-003`

**Governing ADRs**: ADR-0004 (primary), ADR-0001
**ADR Decision Summary**: insertion as a validated transaction; ordering by `SlotIndex` and `ItemId` string order, never runtime enumeration order.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: at most one auto-tidy per insert transaction; tidy respects filters and effective capacity.

## Acceptance Criteria

- [ ] Slots [wood x10, empty, wood x5] cap 64, insert wood x60 -> [64, empty, 11] (GDD AC 2).
- [ ] Slots [wood x32, wood x32] cap 64, insert stone x1 -> one tidy merges wood to slot 0 (64), stone enters slot 1 (GDD AC 3).
- [ ] Same state and same insert twice -> identical layouts (GDD AC 4).
- [ ] Tidy never moves an item into a slot whose filter denies it.
- [ ] Insert count 0 is an accepted no-op with no event.
- [ ] Acceptable quantity reported up front equals what commit actually places.

## Implementation Notes

Plan-then-apply: compute a placement plan against the snapshot (phase 1 existing, phase 2 empty, optional tidy plan, replan); the transaction's validator stores the plan; `IWorldWriter.Apply` executes it. Tidy ordering: iterate slots ascending, for each ItemId in string order merge toward lowest index.

## Out of Scope

- Extraction transactions (accessor epic)
- Belt-specific rules (conveyor epic)

## QA Test Cases

- **AC-1/2/3**: exact layouts as listed; run twice for determinism
- **AC-4**: slot 1 denies wood; tidy leaves wood split rather than violate
- **AC-6**: acceptable(60) then commit places 60

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Container/InsertionTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; tick-simulation 003
- Unlocks: Story 003
