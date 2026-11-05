# Story 002: Shift straight-line belt drag and deterministic preview comparator

> **Epic**: Placement and Editing
> **Status**: In Progress — straight drag integrated; routing/origin rules pending
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/placement-editing.md`
**Requirement**: `TR-place-002`, `TR-place-003` (comparator only)

**Governing ADRs**: ADR-0004 (primary), ADR-0002 (forbidden use)
**ADR Decision Summary**: preview tie-breaks deterministic; belt segments obey min length 2.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM

**Control Manifest Rules (Editing)**:
- Required: min segment 2; reverse hotkey affects preview only; deterministic comparator.

## Acceptance Criteria

- [ ] Shift drag (0,0)->(5,3) previews a straight X segment (0,0)->(5,0) (GDD AC 1); first crossed axis fixes the axis; length clamps at the first obstacle.
- [ ] Length-1 preview is invalid `SegmentTooShort`; release creates nothing (GDD AC 2).
- [ ] Reverse hotkey (F) flips preview direction without placing; second press restores; disabled when origin is a machine output port or an existing belt end.
- [ ] Release of a valid 6-cell preview creates 6 ghosts (GDD AC 3).
- [ ] Origin on a machine output port fixes the first direction to the port facing.
- [ ] `PreviewComparator` orders candidate previews by (fewer turns, fewer open ends, fewer blockers, closest to last preview, stable coords) and never references `ITieBreakSource` (static test).

## Implementation Notes

2026-11-05: `StraightBeltPreview` implements first-crossed-axis locking, obstacle clamp, minimum length,
F reverse and cancellation. Current toolbar explicitly offers straight-only drawing, whether or not Shift
is held. Machine-port/belt-end origins and the multi-candidate path comparator are not implemented.
Pure and Main PlayMode evidence: `production/qa/evidence/factory-scene-runtime-evidence.md`.

The comparator is built now so the VS pathfinder plugs into it. Shift-drag path is a single-segment "path" through the same preview pipeline.

## Out of Scope

- Free-drag pathfinding, reuse analysis, auto-blockers, carry (Story 003, VS)

## QA Test Cases

- GDD AC 1, 2, 3 verbatim
- Reverse toggles twice -> original
- Comparator: two equal previews differing only in turns -> fewer turns wins

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/Editing/StraightDragTests.cs`; PlayMode drag test
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; conveyor 001
- Unlocks: Story 003; build-hud
