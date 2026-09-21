# Story 001: World tables, occupancy, and orientation

> **Epic**: Grid World
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/grid-world.md`
**Requirement**: `TR-grid-001`

**Governing ADRs**: ADR-0004: World Data Model (primary), ADR-0001
**ADR Decision Summary**: `WorldGrid.cellToBlock[]` chunked 16x16 with dirty bits; `BlockTable` parallel arrays; `IWorldView` readonly accessors; compaction only between ticks.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: mutations only via `IWorldWriter`; snapshot views not copies.
- Guardrail: 512x512 snapshot with 10 dirty chunks under 0.2 ms.

## Acceptance Criteria

- [ ] `WorldGrid(width, height)` with cells outside bounds treated as an immutable Void block.
- [ ] `BlockTable` stores def handle, orientation (0..3), controllerCoord, container range, port range, persistent bag index.
- [ ] Place transaction on an occupied cell fails `CellOccupied`; world unchanged (GDD AC 6).
- [ ] Remove transaction empties the cells and yields a drop record; rotate updates orientation effective next tick.
- [ ] Dirty chunk bits set on any structural change; snapshot only rebuilds dirty chunk views; benchmark under 0.2 ms.
- [ ] Table compaction after removals preserves `BlockRef` validity within the tick and remaps between ticks.

## Implementation Notes

Use `int[]` for cellToBlock with -1 empty and -2 void sentinel; `BitArray` per chunk row for dirty flags. Footprints > 1 cell are handled by multiblock; this story supports 1-cell footprints plus a `footprintCells` list to be filled later.

## Out of Scope

- Story 002: ports and connections
- Story 003: attributes and drops

## QA Test Cases

- **AC-3**: place on occupied -> `CellOccupied`, snapshot equality
- **AC-4**: remove then query -> empty; rotate -> orientation visible next tick only
- **AC-5**: benchmark 512x512, 10 dirty chunks
- **AC-6**: remove block 3 of 5, references to block 5 still resolve this tick; remapped next tick

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/GridWorld/WorldTablesTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: tick-simulation 005; item-registry 002
- Unlocks: Story 002
