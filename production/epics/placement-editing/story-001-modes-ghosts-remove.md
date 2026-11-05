# Story 001: Placement modes, machine ghosts, rotation, and removal

> **Epic**: Placement and Editing
> **Status**: In Progress — single-cell chest/belt slice integrated
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/placement-editing.md` (MVP subset)
**Requirement**: `TR-place-001`

**Governing ADRs**: ADR-0004 (primary), ADR-0001, ADR-0007
**ADR Decision Summary**: Editing is pure C# producing structural transactions; ghosts reserve cells; Presentation handles Input System actions and previews.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: Input System actions asset already in `Assets/Settings`; mouse world position via `Mouse.current.position.ReadValue()` and camera.

**Control Manifest Rules (Editing)**:
- Required: structural transactions only; invalid release creates nothing.
- Forbidden: direct world mutation; `ITieBreakSource`.

## Acceptance Criteria

- [ ] Modes Browse/Machine/Belt/Remove selectable via Input System actions (defaults B/M/C/X) and the sidebar (build-hud later; a minimal debug sidebar is acceptable now).
- [ ] Machine mode: footprint ghost follows the cursor; R rotates; click submits a ghost transaction; overlapping an occupied cell shows `CellOccupied` and creates nothing (GDD AC 4).
- [ ] Ghosts reserve cells and build when the warehouse has materials; with empty warehouse ghosts persist and build within one tick of materials arriving (GDD AC 3).
- [ ] Remove mode: click removes; drop with persistent attributes lands in the warehouse (GDD AC 5).
- [x] Original `MachinePlacer` disabled in Main, with script, component and prefab references retained;
  explicit FactoryRuntime root owns the new input. Deletion is superseded by the creator's preservation constraint.

## Implementation Notes

2026-11-05: `FactorySession` implements local stock, queued edits after logistics, material-backed ghosts,
chest recovery and belt-item refunds. `FactoryInputController`/`FactoryHud` expose these in Main.
This covers chests/belts, not machine footprints, market IWarehouse or carry. See
`production/qa/evidence/factory-scene-runtime-evidence.md`; do not mark the whole story done.

`Equilibrium.Editing.PlacementRules` validates footprints and returns `PlacementResult{valid, reason}`; `GhostSystem : ITickSystem` builds ghosts as materials are reserved from the warehouse (market-interface 001 provides `IWarehouse`). Presentation `EditingController` maps input to Editing calls and renders ghost sprites tinted per validity.

## Out of Scope

- Story 002: belt drag
- Story 003 (VS): pathfinding, reuse analysis, carry

## QA Test Cases

- EditMode: `PlacementRules` occupied -> invalid; empty -> valid
- EditMode: `GhostSystem` builds when warehouse gains items
- PlayMode: click on occupied cell -> no ghost; rotate -> ghost orientation changes
- Manual: MachinePlacer removed, no console errors

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/Editing/PlacementRulesTests.cs`; PlayMode input test
**Status**: [ ] Not yet created

## Dependencies

- Depends on: grid-world 003; market-interface 001
- Unlocks: Story 002
