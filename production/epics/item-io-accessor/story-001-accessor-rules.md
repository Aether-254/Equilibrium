# Story 001: Accessor attachment, flow modes, filter, and quantity formulas

> **Epic**: Item I/O Accessor
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-04

## Context

**GDD**: `design/gdd/item-io-accessor.md`
**Requirement**: `TR-acc-001`, `TR-acc-002`, `TR-acc-003`

**Governing ADRs**: ADR-0001 (primary), ADR-0004
**ADR Decision Summary**: one transaction per accessor per tick with default priority +1 over the cell's forward move; attachment facing resolves the target block.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: Exact/UpTo/Keep formulas exactly as GDD; amount 1..3000; merge only onto stalled same-ItemId cell.

## Acceptance Criteria

- [ ] Exact 10, source 9 -> 0, `ExactUnsatisfied`; source 10, belt acceptable 64 -> 10 moved (GDD AC 1-2).
- [ ] UpTo 10, source 7 -> 7 (GDD AC 3).
- [ ] Keep 50, source 130, acceptable 64 -> 64; source 40 -> 0 (GDD AC 4-5).
- [ ] Belt->Container: chest 199/200, belt wood x2 -> chest 200, belt x1, stalled (GDD AC 6).
- [ ] Container->Belt onto a moving cell holding wood x10: no merge; onto a stalled cell (cap 64), UpTo 100 -> 54 added (GDD AC 7-8).
- [ ] Filter iron_ingot with only wood in source -> `SourceEmpty` (GDD AC 9); facing air -> `TargetInvalid` (GDD AC 10).
- [ ] Auto mode resolves per GDD rule 2; mid-segment resolves to Belt->Container if target is a container.
- [ ] Unfiltered extraction picks the lowest non-empty SlotIndex's ItemId; amount 0 clamps to 1 with warning.

## Implementation Notes

Accessor is an attachment record on the belt cell (side, mode, filter, quantityMode, amount) stored as block attributes of the cell. `AccessorSystem.Collect` runs after `ConveyorSystem` registers but ordering is irrelevant due to snapshot reads; its transaction carries `Priority = +1`. Merge check uses the conveyor stall table from the previous validation pass within Resolve: implement as a validator that reads "target cell's forward move rejected this tick" through the resolver's dependency mechanism (accessor depends on the forward move failing; model as `DependsOnFailure` or evaluate merge in a second Collect pass). Choose the second-pass approach if simpler; document it.

## Out of Scope

- Story 002: settings UI

## QA Test Cases

- GDD AC 1-10 verbatim
- Auto resolution table: belt start / belt end / mid-segment

## Test Evidence

Live block attributes and quantity/flow rules are implemented by the shared `LogisticsSystem` batch.
Regression tests are in `Assets/Tests/EditMode/Logistics/LogisticsTests.cs`, including exact atomicity,
shared source/destination capacity, reserved stock and stale-stall rejection. This implementation uses
staged resolution and a post-movement intake pass; no prior-tick display state is consulted.
Save hydration and queued runtime configuration edits remain separate integrations. See
`docs/architecture/logistics-runtime.md` and `production/qa/evidence/logistics-runtime-evidence.md`.

**Story Type**: Logic
**Evidence**: `Assets/Tests/EditMode/Logistics/LogisticsTests.cs`; `production/qa/evidence/logistics-runtime-evidence.md`
**Status**: bounded runtime coverage recorded; saved/settings integration remains open

## Dependencies

- Depends on: conveyor 001; container 002
- Unlocks: conveyor 002; Story 002
