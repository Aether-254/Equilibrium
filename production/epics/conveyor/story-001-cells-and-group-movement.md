# Story 001: Belt cells, whole-group movement, and same-tick chaining

> **Epic**: Conveyor
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-04

## Context

**GDD**: `design/gdd/conveyor.md`
**Requirement**: `TR-conv-001`, `TR-conv-002`

**Governing ADRs**: ADR-0001 (primary), ADR-0004
**ADR Decision Summary**: belt cell = one-slot container capacity 3000; move transactions declare dependency on the ahead cell's move; priority default 0.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Forbidden: belt-to-belt merge or split; physics.
- Required: dependencies declared for chained moves.

## Acceptance Criteria

- [x] A->B->C with A=100, B=200, C=empty -> A=empty, B=100, C=200, distinct groups (GDD AC 1).
- [x] A->B with B stalled: A unchanged, no merge or partial (GDD AC 2).
- [x] X and Y both facing empty Z: exactly one enters (GDD AC 5).
- [x] 4-cell loop with one group: after 100 ticks exactly 100 hops, never duplicated (GDD AC 6).
- [x] A group that entered B this tick does not move to C the same tick (GDD AC 7).
- [x] Group of 3000 facing a cell with effective capacity 64 -> `DownstreamCapacity`, no partial move.
- [x] Belt block def: one slot cap 3000, direction from orientation, `AcceptsDirectBelt` true on its input side.

## Implementation Notes

`ConveyorSystem.Collect`: for each non-empty belt cell compute `ahead`; if ahead is a belt, submit move with `DependsOn = ahead's move id` (if ahead non-empty) and validator `acceptsWhole`; if ahead is a machine direct port, submit insertion to that port's container. `enteredThisTick` is derived: a cell's move transaction is only submitted for groups present at snapshot. Segment min-length is enforced by placement, but a `SegmentTooShort` flag is set on load if violated.

## Out of Scope

- Story 002: stall derivation and partial delivery via accessor
- Story 003: presentation

## QA Test Cases

- GDD AC 1, 2, 5, 6, 7 as listed; tie-break via `FixedTieBreakSource`
- Capacity: 3000 group vs cap 64 -> stays

## Test Evidence

The runtime rules are implemented in `Assets/Scripts/Simulation/World/LogisticsSystem.cs` and tested
by `Assets/Tests/EditMode/Logistics/LogisticsTests.cs`. The implementation uses one atomic system
batch with an internal dependency graph, rather than separate generic transactions per belt.
See `docs/architecture/logistics-runtime.md` and `production/qa/evidence/logistics-runtime-evidence.md`.
Main-scene rendering, segment validation on placement/load, and the full epic performance gate remain open.

**Story Type**: Logic
**Evidence**: `Assets/Tests/EditMode/Logistics/LogisticsTests.cs`; `production/qa/evidence/logistics-runtime-evidence.md`
**Status**: runtime logic verified; broader integration boundaries listed above remain open

## Dependencies

- Depends on: tick-simulation 003; container 002; grid-world 002
- Unlocks: Story 002
