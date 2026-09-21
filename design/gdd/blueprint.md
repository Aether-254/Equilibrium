# Blueprint

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Simple Entry / Unfathomable Depth (copy what works, then refine it), Realism First (nothing is built without materials; construction follows real dependencies)
> **Normative Source**: design/confirmed-mechanics-spec.md s12, s19
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Blueprints capture a region of the factory (blocks, orientations, belts, attachments, multiblock structures, and configuration such as accessor modes) and stamp it elsewhere as a set of construction ghosts. Construction consumes warehouse items; when materials are short the player chooses "start when complete" or "build as materials arrive". Build order supports Controller-first, infrastructure-first, player-defined priority, and automatic dependency topology, with the constraint that topology always respects Controller and infrastructure prerequisites and player priority only orders peers. Rotation transforms orientations, Port facings, belt directions, and multiblock anchors together.

## Player Fantasy

Direct. Once a line is tuned, the player frames it, saves it, and stamps three more; watching ghosts fill in as the warehouse supplies them turns planning into a visible construction sequence. The fantasy is leverage: your understanding, reproduced.

## Detailed Design

### Core Rules

1. Capture: select a rectangular region (or a multi-select set); the blueprint stores per cell: block ItemId, orientation, attachments (accessor mode/filter/amount, blockers per side), belt direction, and multiblock membership (Type II as one unit; Type I as its parts plus a structure hint). Persistent attributes (items inside) are not captured.
2. Blueprints are items in the warehouse (ItemId `equilibrium:blueprint` with instance data) or library entries; both are data.
3. Stamp: choose a blueprint, position, and rotation (0/90/180/270); preview validity per cell (occupancy, bounds, segment rules); confirm to create ghosts (placement-editing ghost rules).
4. Rotation MUST transform: block orientation, Port facings, belt directions, blocker sides, accessor sides, multiblock anchor and footprint.
5. Material policy on stamp: `StartWhenComplete` (no ghost builds until all materials are reserved) or `BuildIncrementally` (ghosts build as materials arrive, in build order).
6. Build order strategies: `ControllerFirst`, `InfrastructureFirst` (belts, shafts, blockers before machines), `PlayerPriority` (integer per ghost), `AutoTopology` (dependency sort: multiblock Controllers before Slaves; belt segments in flow order; blockers after their carrier). Auto topology always enforces Controller and infrastructure prerequisites; player priority orders only within a topological layer.
7. Ghosts from a blueprint carry their configuration and apply it on build (accessor settings, blockers).
8. Cancelling a stamp removes unbuilt ghosts and releases reservations; built blocks remain.

### States and Transitions (stamp)

| State | Transition | Next |
|---|---|---|
| Previewing | confirm valid | Ghosted |
| Ghosted (StartWhenComplete) | all materials reserved | Building |
| Ghosted (BuildIncrementally) | any material available | Building |
| Building | all ghosts built | Complete |
| Building | cancel | Partial (built stays) |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| placement-editing | both | ghost creation, validity, build transactions |
| grid-world | in | occupancy, facings |
| multiblock | in | anchor rules, Type I/II capture |
| conveyor, flow-blocker, item-io-accessor | in | direction/side/config transform |
| market-interface (warehouse) | in | materials and reservations |
| tick-simulation | in | build transactions |
| save-load | out | blueprint library and pending stamps |
| build-hud | out | preview, material status, order controls |

## Formulas

The rotation transform is defined as:

`cell' = R(θ) · (cell − pivot) + pivot`, `facing' = (facing + θ/90) mod 4`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| angle | θ | int | 0, 90, 180, 270 | rotation |
| pivot | pivot | cell | - | blueprint pivot (open question) |
| rotation matrix | R | 2x2 int | - | quarter-turn |

**Output Range:** integer cells; facings N/E/S/W.
**Example:** belt at (2,0) facing E, θ = 90 about (0,0): cell (0,2), facing S.

The material completeness predicate is defined as:

`complete = ∀ item: reserved(item) >= required(item)`

**Example:** requires 12 belts, 2 blockers, 1 saw; warehouse has 12/2/0: incomplete; incremental mode builds belts and blockers.

## Edge Cases

- **If the stamp overlaps existing identical blocks**: those cells are marked "already present" and skipped (no ghost, no material); differing blocks make the cell invalid.
- **If a rotated multiblock footprint no longer fits**: invalid cells shown; stamp blocked until moved.
- **If a captured Type I structure is stamped and forms only after all parts build**: expected; structure forms via multiblock detection.
- **If a blueprint references an ItemId now unknown** (Addon absent): those ghosts are `UnknownItem` placeholders and never build; others proceed.
- **If the warehouse gains and loses materials repeatedly in incremental mode**: builds proceed in order as available; no thrash because reservations are per ghost.
- **If two stamps compete for the same material**: reservation order by stamp time.
- **If the player edits a ghost's config before build**: allowed; applied on build.
- **If captured belt segments are cut at the region edge leaving length 1**: capture warns and extends selection or marks the segment as needing manual completion.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| placement-editing, grid-world, multiblock, save-load | hard | |
| market-interface | hard | materials |
| conveyor, flow-blocker, item-io-accessor | hard | transforms |
| build-hud | downstream | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| maxBlueprintCells | 1024 | 64..8192 | small stamps | perf |
| defaultMaterialPolicy | BuildIncrementally | enum | - | - |
| defaultOrder | AutoTopology | enum | - | - |
| ghostBuildsPerTick | 4 | 1..64 | slow | instant |

## Visual/Audio Requirements

Ghost preview with rotation; per-ghost material status; build progress sweep; completion cue.

## UI Requirements

Blueprint library, capture tool, stamp controls (rotate, policy, order), per-stamp material list. UX spec via `/ux-design`.

## Acceptance Criteria

- **GIVEN** a captured 3-cell E-facing belt, **WHEN** stamped at 90 deg, **THEN** the ghosts form a 3-cell S-facing belt.
- **GIVEN** a stamp needing 12 belts with 5 in stock under BuildIncrementally, **WHEN** ticks run, **THEN** 5 ghosts build in AutoTopology order and 7 remain ghosts.
- **GIVEN** the same under StartWhenComplete, **WHEN** ticks run, **THEN** nothing builds until 12 belts are reserved.
- **GIVEN** a captured Type II press, **WHEN** stamped rotated, **THEN** its footprint and anchor rotate together and a single machine builds.
- **GIVEN** a blueprint with accessor Keep 50, **WHEN** the accessor ghost builds, **THEN** the accessor has Keep 50.
- **GIVEN** a stamp overlapping an identical existing belt cell, **WHEN** previewed, **THEN** that cell is skipped and not counted in materials.
- **GIVEN** a cancel during Building, **WHEN** executed, **THEN** built blocks remain and unbuilt ghosts and reservations are removed.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Capture region selection, rotation pivot, rotation step, multiblock interaction details (spec s19) | ux-designer, systems-designer | Alpha |
| 2 | Blueprint as item vs library entry | game-designer | Alpha |
| 3 | Sharing blueprints between saves or players | game-designer | Full Vision |
