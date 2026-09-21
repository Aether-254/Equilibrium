# Epic: Placement and Editing

> **Layer**: Feature
> **GDD**: design/gdd/placement-editing.md
> **Architecture Module**: PlacementRules, BeltPathfinder, ReuseAnalyzer, CarryMode, GhostSystem (Equilibrium.Editing + Equilibrium.Simulation); input handling and previews (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: MVP (subset) / Vertical Slice (full)
> **Engine Risk**: MEDIUM (Input System actions; preview rendering)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories placement-editing`

## Overview

Implement the player's editing tools in two tranches. MVP: mode selection with hotkeys, machine ghost placement with rotation, removal to warehouse, Shift straight-line belt drag with min-length validation, construction ghosts that build as warehouse materials arrive, and the deterministic preview comparator. Vertical Slice: budgeted orthogonal pathfinder with three objectives, network reuse analysis with re-validation at commit, auto-blocker proposals, carry mode with multi-select and Type I/II rules. Replaces the prototype `MachinePlacer`.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | Occupancy, reservations, facings | LOW |
| ADR-0001: Tick Transaction Engine | Ghost/build/remove/move as structural transactions | LOW |
| ADR-0002: Tie-Break Entropy | Forbidden in editing; deterministic comparator | LOW |
| ADR-0007: Simulation/Presentation Split | Editing is pure C#; previews in Presentation | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-place-001 | Ghosts reserve and build on materials; invalid release creates nothing | ADR-0004, ADR-0001 |
| TR-place-002 | Shift straight segment; min length 2; reverse affects preview only | ADR-0004 |
| TR-place-003 | Budgeted pathfinder; reuse analysis; deterministic tie-break (VS) | ADR-0002, ADR-0004 |
| TR-place-004 | Carry mode single transaction; Type II auto-complete; Type I warning (VS) | ADR-0001, ADR-0004 |

## Definition of Done

- MVP acceptance criteria (Shift drag, ghosts, place/remove) as EditMode + PlayMode tests.
- VS criteria tracked as separate stories; frame-budget test for pathfinding.
- Prototype `MachinePlacer.cs` removed; `conveyorPrefab` unassigned-reference issue resolved by deletion.

## Next Step

Run `/create-stories placement-editing`.
