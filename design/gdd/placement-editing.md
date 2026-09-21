# Placement and Editing

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Simple Entry / Unfathomable Depth (drag a belt, get a belt; later, pathfinding and network analysis), Realism First (nothing is built without materials; nothing is moved without a committed transaction)
> **Normative Source**: design/confirmed-mechanics-spec.md s11, s19
> **Scope split**: MVP subset = mode sidebar + hotkeys, machine place/remove/rotate, Shift straight-line belt drag, construction ghosts. Vertical Slice = auto orthogonal pathfinding, network reuse analysis, carry mode, multi-select.
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Placement and editing is how the player changes the grid: choosing a placement mode (machine, belt, pipe) from a top sidebar or hotkey, placing and rotating machines, drawing belts by drag, and later moving objects in a carry mode. Every edit produces a construction ghost that becomes real when warehouse materials arrive, and every world change goes through a unified transaction. Belt drawing has two forms: Shift-drag forces a straight line; free drag runs a budgeted orthogonal pathfinder with three player-selectable objectives (Fastest, Cheapest, Balanced), analyzes any existing belts it might reuse, proposes blockers for open ends, and never commits a stale analysis. Pathfinding tie-breaks are deterministic so previews do not flicker.

## Player Fantasy

Direct; this is the player's hands. Placing should feel immediate and legible: the ghost shows exactly what will exist and which ports face where. Drawing a belt should feel like sketching, with the game handling the tedious part (path, blockers) while never surprising the player with a connection they did not intend.

## Detailed Design

### Core Rules

**Modes**
1. Placement modes: Machine, Belt, Pipe (Pipe is Alpha), Blocker (VS), Carry (VS), Remove. Selected via the top sidebar UI or hotkeys.
2. Machine placement: choose a machine item from the warehouse, preview footprint and port facings, rotate with a hotkey (four orientations), click to create a ghost.
3. Remove: click a block to submit a removal transaction; drop goes to the warehouse with persistent attributes (grid-world).

**Construction ghosts**
4. Releasing a placement creates ghosts, not blocks. A ghost reserves its cells (`Reserved` state, grid-world) and lists required materials.
5. Ghosts are built when the warehouse has the materials; building consumes them via transaction. Blueprint GDD extends ordering options; single placements build immediately if materials exist.
6. Ghosts never participate in simulation; they are not Containers and connect to nothing.

**Belt drag, common**
7. Drag origin may be an empty cell, an existing belt end, or a machine output Port.
8. The first cell crossed sets the first segment's direction.
9. A hotkey reverses the direction of the current preview without placing; pressing again restores.
10. All results must satisfy: straight segments of at least two cells (conveyor rule 3), occupancy, endpoint facings, blocker rules.
11. On release with a valid preview: create ghosts. On release with an invalid preview: create nothing; never delete, overwrite, or move obstacles.

**Shift drag (MVP)**
12. Shift constrains the preview to a single straight segment along the first-crossed axis from origin to the current cell; length is clamped at the first obstacle; if length < 2 the preview is invalid with `SegmentTooShort`.

**Free drag with pathfinding (VS)**
13. Pathfinder recomputes on mouse move within a search radius, node budget, and recompute rate cap. Exceeding budget must not block the interaction frame.
14. Objectives: Fastest (min ticks for an item to traverse, i.e. min cells with few detours), Cheapest (min new belts + blockers + other parts, may reuse existing belts after safe analysis), Balanced (weighted; weights data-driven).
15. Path shapes are orthogonal polylines; "width" (vertical gap between start and end horizontal segments) may be 1, 2, 3, ...; 1-wide extends the vertical segment one cell past the lower horizontal to satisfy min length 2; the extra cell is a real belt cell.
16. Reuse analysis: before reusing an existing belt, analyze its reachable upstream/downstream, direction, blockers, and connection state; an empty, direction-compatible belt with no unintended upstream/downstream is a direct candidate; joining a working belt or changing existing relations requires highlighting affected networks and an extra confirmation before commit.
17. If analysis exceeds budget: exclude that candidate, show `NetworkAnalysisBudgetExceeded`, keep searching without it; the player may request deeper analysis which still never blocks the frame and never allows "safe reuse" commit until complete.
18. Blocker proposal: where the path leaves an open input that could receive unintended flow, propose a blocker; auto-place consuming warehouse blockers; if none in stock, omit and mark open ends clearly; a modifier key suppresses auto-blockers for this drag.
19. Every reuse conclusion is re-validated against the latest world state at commit; stale analysis never commits.
20. Tie-break among equal-score paths: fewer turns, fewer new open ends, fewer blockers, closest to last frame's preview, then stable coordinate order. Deterministic; never uses the tick tie-break RNG.
21. On no path within budget: keep a faded outline of the last valid path, mark the failing segment invalid with a reason; release creates nothing.

**Carry mode (VS)**
22. Entering carry temporarily disconnects the selection; preview does not mutate the world; confirmation commits move + connection changes as one transaction; persistent state and orientation are preserved.
23. Multi-select by box or additive click; the set is one carry transaction.
24. Selecting any cell of a Type II multiblock selects the whole structure. Selecting part of a Type I warns, highlights missing parts, offers "select whole structure", and allows proceeding with the documented consequence (structure invalidation).

### States and Transitions (editor)

| State | Transition | Next |
|---|---|---|
| Browse | pick mode | ModeActive |
| ModeActive(Belt) | mouse down | Dragging(origin) |
| Dragging | mouse move | Dragging (preview recompute, budgeted) |
| Dragging | release valid | GhostsCreated → ModeActive |
| Dragging | release invalid | ModeActive (nothing) |
| ModeActive(Carry) | select | CarrySelecting → CarryPreview → commit/cancel |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| grid-world | both | occupancy, reservation, facings, adjacency, connected predicate |
| tick-simulation | in | ghost creation, build, remove, move as structural transactions |
| conveyor | in | segment rules; stall/open-end semantics for analysis |
| flow-blocker | both | auto-placement proposals; blocker mode |
| multiblock | in | selection completion, Type I invalidation warning |
| market-interface (warehouse) | in | material availability and consumption |
| blueprint | out | ghost and ordering model reused |
| build-hud | out | sidebar, previews, highlights, reasons |
| save-load | out | ghosts and reservations persist |

## Formulas

The path cost formula is defined as:

`cost(path) = w_t * ticks(path) + w_m * materials(path) + w_d * detour(path)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| traversal ticks | ticks | int | >= 2 | cells an item traverses (1 hop/tick) |
| materials | materials | int | >= 0 | new belts + blockers + parts |
| detour | detour | int | >= 0 | cells beyond Manhattan distance |
| weights | w_t, w_m, w_d | float | >= 0 | Fastest (1,0,0.1), Cheapest (0,1,0.1), Balanced (data-driven, default 0.5,0.5,0.2) |

**Output Range:** >= 0; lower is better.
**Example:** two paths, 12 cells/12 materials vs 14 cells/8 materials (reuse): Fastest picks the first, Cheapest the second, Balanced: 0.5*12+0.5*12=12 vs 0.5*14+0.5*8=11 picks the second.

The 1-wide extension rule is defined as:

`verticalLength = max(2, gap)` where `gap` is the vertical distance between the two horizontal segments; when `gap = 1` the vertical segment extends one cell beyond the lower horizontal segment.

**Example:** spec figure "一宽": vertical segment of 2 cells with one cell protruding below.

Budgets: `maxNodes = 4000`, `maxRecomputeHz = 30`, `maxAnalysisNodes = 2000` per candidate per frame (knobs).

## Edge Cases

- **If the drag origin is a machine output Port**: first segment direction is fixed to the Port facing; the reverse hotkey is disabled.
- **If the drag origin is an existing belt end**: continue in that belt's direction; reversing would make the joined belt a head-on collision, so reverse is disabled.
- **If the mouse leaves the world bounds**: clamp the target to the last in-bounds cell.
- **If the warehouse lacks belts on release**: ghosts are still created; they build as belts arrive.
- **If a proposed blocker cannot be afforded**: omit; open end stays marked; no failure.
- **If world state changes during a drag** (another ghost builds, a machine is removed): recompute; a reuse conclusion that changed is invalidated before commit.
- **If two ghosts are created on the same cell in one tick** (e.g. two players in the future, or blueprint plus manual): `CellContested`; loser rejected.
- **If a ghost's footprint becomes blocked before build**: ghost stays `Blocked`, shows reason; never auto-removed.
- **If a carry selection includes a belt cell mid-segment**: the remaining halves must each still satisfy min length 2 or the move preview is invalid with `SegmentTooShort`.
- **If the reverse hotkey is pressed with no drag active**: no-op.
- **If Shift is pressed mid-drag**: constrain from that moment using the already-fixed first axis.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| grid-world, tick-simulation, conveyor | hard | |
| market-interface (warehouse) | hard | materials |
| flow-blocker, multiblock | soft (VS) | full feature set only |
| build-hud | downstream | presentation |
| blueprint | downstream | reuses ghosts |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| pathSearchRadius | 48 cells | 8..256 | long drags fail | frame cost |
| maxNodes | 4000 | 500..50000 | fails in mazes | frame cost |
| maxRecomputeHz | 30 | 5..60 | laggy preview | CPU |
| analysisNodeBudget | 2000 | 200..20000 | reuse rarely allowed | frame cost |
| balancedWeights (w_t, w_m, w_d) | 0.5, 0.5, 0.2 | 0..1 each | - | - |
| autoBlockerDefault | on | bool | open ends everywhere | blocker spam |
| ghostBuildImmediate | true | bool | - | - |

## Visual/Audio Requirements

Ghost sprites at reduced alpha with port arrows; invalid segments tinted with a reason label; affected-network highlight during reuse; faded last-valid outline on failure. Placement click and build-complete cues.

## UI Requirements

Top sidebar with mode buttons and hotkey hints; objective toggle (Fastest / Cheapest / Balanced) visible in Belt mode; confirmation dialog for network-affecting reuse; carry-mode selection HUD. Detailed in build-hud UX spec.

## Acceptance Criteria

- **GIVEN** Belt mode with Shift held, **WHEN** dragging from (0,0) to (5,3), **THEN** the preview is a single straight segment along X from (0,0) to (5,0).
- **GIVEN** a Shift drag of length 1, **WHEN** released, **THEN** nothing is created and the reason `SegmentTooShort` was shown.
- **GIVEN** a valid 6-cell preview and an empty warehouse, **WHEN** released, **THEN** 6 ghosts exist and no belts; adding 6 belts to the warehouse builds them within one tick each.
- **GIVEN** a machine ghost overlapping an existing block, **WHEN** clicked, **THEN** no ghost is created and `CellOccupied` is shown.
- **GIVEN** a placed machine, **WHEN** removed, **THEN** its item with persistent attributes is in the warehouse and the cells are empty.
- **GIVEN** (VS) two equal-cost paths, **WHEN** the mouse jitters by one pixel within the same cell, **THEN** the preview does not change.
- **GIVEN** (VS) a free drag whose best path reuses a working belt, **WHEN** released, **THEN** an affected-network confirmation is required before ghosts are created.
- **GIVEN** (VS) a 1-wide path, **WHEN** previewed, **THEN** the vertical segment has 2 cells with one protruding past the lower horizontal segment.
- **GIVEN** (VS) a maze exceeding `maxNodes`, **WHEN** dragging, **THEN** the frame time stays under budget and the failing segment shows a reason.
- **GIVEN** (VS) a Type II multiblock, **WHEN** one cell is selected in Carry, **THEN** the whole structure is selected.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Exact cell occupancy and arrow rendering at orthogonal joints (spec s19) | systems-designer, art | VS |
| 2 | Balanced weights and search budgets after playtest | game-designer | VS |
| 3 | Whether MVP builds need materials at all or Tier 1 gives free belts | economy-designer | MVP tuning |
| 4 | Hotkey map (Input System actions) | ux-designer | MVP ux-spec |
