# Story 001: Type I/II structures, formation, invalidation, and frozen state

> **Epic**: Multiblock Structures
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L (split 1a Type II, 1b Type I, 1c Frozen at sprint planning)
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/multiblock.md`
**Requirement**: `TR-multi-001`, `TR-multi-002`

**Governing ADRs**: ADR-0004 (primary), ADR-0005, ADR-0001
**ADR Decision Summary**: `controllerCoord` block attribute; footprint occupancy; structure id and frozen flag persisted.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

## Acceptance Criteria

- [ ] 3x3 Type I pattern forms when the last brick is placed; core is Controller; bricks store its coordinate (GDD AC 1).
- [ ] Removing a required brick clears structure data, keeps the other parts, no extra drops (GDD AC 2); replacing re-forms automatically (GDD AC 3).
- [ ] Type I removal/carry warning is raised before the transaction (GDD AC 4; UI in build-hud).
- [ ] 2x2 Type II press: removing any cell drops exactly one item with contents and clears all cells (GDD AC 5); carry selection auto-completes (GDD AC 6).
- [ ] Controller chunk unavailable -> Frozen `ControllerUnloaded`, no processing, data intact; later load -> Formed (GDD AC 7-8).
- [ ] Ports on slaves route to controller containers (GDD AC 9).
- [ ] Stable scan order resolves competing patterns; orphan slaves logged.

## Implementation Notes

`StructureDetector` runs on structural commits over affected cells; patterns from `StructureDef`. Chunk availability is a stubbed predicate until the map-model ADR; Frozen path is tested through the stub.

## Out of Scope

- Blueprint capture of structures (blueprint)

## QA Test Cases

- GDD AC 1-9 verbatim with stubbed chunk availability

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Multiblock/StructureTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: grid-world 003; machine-processing 002; save-load 003
- Unlocks: placement-editing 003; blueprint 001
