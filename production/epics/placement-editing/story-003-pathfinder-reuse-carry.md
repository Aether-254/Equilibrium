# Story 003: Free-drag pathfinder, reuse analysis, auto-blockers, and carry mode (VS)

> **Epic**: Placement and Editing
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L (split into 3a pathfinder, 3b reuse analysis + blockers, 3c carry at sprint planning)
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/placement-editing.md` (VS tranche)
**Requirement**: `TR-place-003`, `TR-place-004`

**Governing ADRs**: ADR-0004 (primary), ADR-0001, ADR-0002 (forbidden use)
**ADR Decision Summary**: budgeted search; re-validation at commit; carry as one transaction.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM

**Control Manifest Rules (Editing)**:
- Required: node/recompute budgets; stale analysis never commits; carry preserves state and orientation.

## Acceptance Criteria

- [ ] Objectives Fastest/Cheapest/Balanced with the GDD cost formula and default weights; 1-wide paths extend the vertical segment one cell (GDD AC 8).
- [ ] Equal-cost paths: mouse jitter within a cell does not change the preview (GDD AC 6).
- [ ] Reuse of a working belt requires confirmation; conclusions re-validated at commit (GDD AC 7).
- [ ] `NetworkAnalysisBudgetExceeded` excludes the candidate, offers deeper analysis, disables commit for it until complete.
- [ ] Auto-blocker proposals on open inputs; omitted when stock is empty; suppressed by modifier.
- [ ] Maze exceeding `maxNodes` keeps frame time under budget and shows a reason (GDD AC 9).
- [ ] Carry: single transaction; persistent state and orientation preserved; Type II auto-complete (GDD AC 10); Type I partial warning with "select whole structure"; mid-segment cut leaving length 1 is invalid.

## Implementation Notes

A* over cells with turn penalties; incremental recompute limited to `maxRecomputeHz`; reuse analyzer BFS over belt networks with node budget. Blocker proposal requires flow-blocker epic; if not yet available, proposals are computed but not placed.

## Out of Scope

- HUD visuals (build-hud)

## QA Test Cases

- GDD AC 6-10
- Cost formula: worked example from GDD (12/12 vs 14/8 under three objectives)
- Frame budget test with a 200x200 maze

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Editing/PathfinderTests.cs`, `ReuseAnalyzerTests.cs`, `CarryModeTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002; flow-blocker 001; multiblock 001
- Unlocks: blueprint 001
