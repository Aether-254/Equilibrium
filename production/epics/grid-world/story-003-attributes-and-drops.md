# Story 003: Block vs persistent attributes and removal drops

> **Epic**: Grid World
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/grid-world.md`
**Requirement**: `TR-grid-002`

**Governing ADRs**: ADR-0004 (primary), ADR-0005
**ADR Decision Summary**: `PersistentAttributeBag` per block, typed small map serialized into drops; block attributes (orientation, controllerCoord) never enter drops.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: drops carry persistent attributes; removal fails `WarehouseFull` if the warehouse cannot accept.

## Acceptance Criteria

- [ ] Machine holding wood x10 removed and re-placed: holds wood x10, orientation 0 (GDD AC 4).
- [ ] Drop record contains persistent bag (items, progress, energy) and excludes orientation/controllerCoord.
- [ ] Removal when the warehouse `acceptable` is 0 for the drop item: transaction fails `WarehouseFull`, block stays.
- [ ] Missing orientation on hydrate defaults to 0 with a warning (edge case).
- [ ] Reserved recipe inputs travel with the drop (interface for machine-processing: bag key `reservations`).

## Implementation Notes

Bag schema per block def lists persistent keys; unknown keys are preserved in an extras dictionary for save-load. Warehouse acceptance is a container query supplied by an `IWarehouse` interface stubbed here until market-interface implements it.

## Out of Scope

- Warehouse implementation (market-interface)
- Carry mode orientation preservation (placement-editing)

## QA Test Cases

- **AC-1**: remove/re-place preserves items, resets orientation
- **AC-3**: stub warehouse acceptable 0 -> `WarehouseFull`, block present
- **AC-4**: hydrate without orientation -> 0 + warning logged

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/GridWorld/AttributesAndDropsTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; container Story 001
- Unlocks: save-load, machine-processing
