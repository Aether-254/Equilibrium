# Conveyor

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Realism First (whole groups move, nothing teleports or merges silently), Simple Entry / Unfathomable Depth (a belt is one arrow, one slot, one rule)
> **Normative Source**: design/confirmed-mechanics-spec.md s7.1-7.6
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

A conveyor cell is a one-slot Container (capacity 3000, one ItemId) that moves its entire item group one cell forward per tick along its fixed direction, if the cell ahead is free or is itself vacating in the same tick. Belts are straight segments of at least two cells; direction changes happen only where segments meet end-to-end orthogonally. Belts never merge groups, never split groups, never partially move a group belt-to-belt, and never use physics; items visually slide to represent committed transactions. Stall is a per-tick derived condition with explicit causes. Together with accessors, belts are the visible bloodstream of the factory and the main place players spot bottlenecks.

## Player Fantasy

Direct. The player lays a belt and watches groups march in lockstep; a jam is visible as a queue with a cause. The fantasy is legibility: belts do exactly one thing, so when they stop the player knows to look at the end of the line, not the belt itself.

## Detailed Design

### Core Rules

**Cell**
1. A conveyor cell is a Container with one Slot, capacity 3000, subject to `effectiveCapacity` (container GDD).
2. A cell has one direction (N/E/S/W). A single cell is never a corner.
3. Each distinct straight segment MUST contain at least two cells. This applies per segment, including the segments of an orthogonal path.
4. Segments connect at endpoints orthogonally to form paths; the connection is via the "cell ahead" rule (grid-world rule 9).

**Groups**
5. All items in a cell form one group. The group is the unit of movement.
6. Belt-to-belt transfer MUST NOT merge groups, even with identical ItemId, and MUST NOT split a group. If the whole group cannot enter, the whole group stays.
7. A cell may have multiple legal upstream inputs but exactly one output (its facing).
8. Multiple upstream groups competing for the same vacated cell in one tick are resolved by tick-simulation priority (default 0) and non-reproducible tie-break; losers stay.

**Movement per tick**
9. A group moves at most one cell per tick.
10. A group may enter a cell that is being vacated in the same tick (dependency-ordered commit).
11. A group that just entered a cell this tick cannot move again this tick.
12. Belts are unpowered in Tier 1 and have constant throughput of one group-hop per tick.

**Stall (derived per tick, per target cell)**
13. Preconditions: the cell holds items, and its group was held back this tick (fully or partially).
14. Causes (any): no receiving Port ahead; accessor ahead pointing at air or a non-Container; downstream Container full; downstream accepted only part of the group; target machine in a non-accepting state with full input slot; target does not accept this ItemId on that Port; target damaged/errored; any other downstream rejection.
15. Stall MUST be computed from this tick's validation results, not last tick's display state.

**Partial acceptance into Containers**
16. When a belt delivers to a Container through an accessor, the Container accepts as much as it can; the remainder stays on the belt and the cell is stalled for this tick (spec s7.6). This exception is only for belt→Container via accessor, never belt→belt.

**Accessor merge exception**
17. An accessor may add items to a belt cell that already holds the same ItemId only if that cell is stalled, and only up to 3000 / effective capacity (spec s7.4). Details in item-io-accessor GDD.

### States and Transitions (per cell)

| State | Condition |
|---|---|
| Empty | no group |
| Moving | group present and committed to move this tick |
| Stalled(ReasonCode) | group present and held this tick |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | one move transaction per non-empty cell, dependency on the ahead cell's move |
| container | in | cell is a Container; capacity and acceptance |
| grid-world | in | direction, cell-ahead, IngressEdge status |
| item-io-accessor | both | accessor attached to a cell alters its output target and enables merge/partial rules |
| flow-blocker | in | disabled IngressEdge rejects entry from that side |
| machine-processing | both | direct-accepting Ports receive groups; machine output places groups on the belt ahead |
| group-processing-devices | out | the only legal way to split/merge/batch |
| economy-ledger | out | stall ticks with cause as logistics loss |
| build-hud | out | stall visualization, open-end marks |

## Formulas

The move predicate is defined as:

`canMove(c) = hasGroup(c) ∧ ingressEnabled(ahead(c), dir(c)) ∧ (isEmpty(ahead(c)) ∨ willVacate(ahead(c))) ∧ ¬enteredThisTick(group(c)) ∧ acceptsWhole(ahead(c), group(c))`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| cell | c | cell | - | source cell |
| ahead | ahead(c) | cell | - | the cell c faces |
| willVacate | willVacate | bool | - | ahead's own move is accepted this tick |
| acceptsWhole | acceptsWhole | bool | - | ahead is a belt with capacity for the whole group, or a direct-accepting machine Port accepting the whole group |

**Output Range:** boolean; false implies stalled with the first failing clause as ReasonCode.
**Example:** A→B→C, C empty, B's move accepted; then A's move is accepted with dependency on B's.

The throughput formula (for the ledger and planning UI) is defined as:

`throughput_max = groupSize / 1 tick` per belt lane, bounded by upstream accessor rate

**Output Range:** up to 3000 items/tick per lane in principle; effective throughput is set by the accessor feeding it (item-io-accessor `amount`).
**Example:** Accessor Exact 10 per tick onto an empty belt: 10 items/tick steady state.

## Edge Cases

- **If a segment is only one cell long**: placement is invalid (placement-editing enforces rule 3); if such a state exists from a bad save, the cell functions but is flagged `SegmentTooShort`.
- **If a belt loops onto itself**: rule 11 guarantees each group moves at most once per tick; no infinite loop. Groups circulate forever if nothing consumes them.
- **If two groups of the same ItemId are adjacent and the front one stalls**: the rear one stalls behind it; no merging.
- **If the ahead cell is a belt facing back at this cell**: not a valid connection (grid-world connected predicate fails); stall `NoReceivingPort`.
- **If a group of 3000 wood faces a belt cell whose effective capacity for wood is 64**: cannot move whole; stall `DownstreamCapacity`. Content design should keep belt-side `maxStack` large; this is a legitimate design constraint for tiny-stack items.
- **If a cell is removed while holding a group**: the group goes to the player's warehouse via the removal transaction (grid-world); if the warehouse is full, removal fails.
- **If a blocker is installed on the side a group is entering from, in the same tick**: this tick's committed move stands; blocking applies from next tick (flow-blocker).
- **If an accessor on a cell is set to belt→container but the cell's group is moving forward normally**: the accessor transaction competes with the forward move for the same group; the accessor has priority +1 by default so extraction wins when possible.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation, container, grid-world | hard | |
| item-io-accessor | soft | belts work without accessors between machines with direct Ports |
| flow-blocker | soft | |
| machine-processing | soft | |
| group-processing-devices, build-hud, economy-ledger | downstream | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| beltCellCapacity | 3000 | fixed by spec | - | - |
| hopsPerTick | 1 | fixed Tier 1 | - | faster belts are a Tier 2 unlock |
| minSegmentLength | 2 | fixed by spec | - | - |
| accessorPriorityOverMove | +1 | -1..+1 | extraction starves | forward flow starves |
| stallVisualDelayTicks | 0 | 0..3 | flicker on single-tick stalls | hides real stalls |

## Visual/Audio Requirements

Item group sprite slides from cell center to next cell center over one tick's wall duration; stalled groups sit still with a subtle jam indicator. Belt surface sprites animate in the direction of travel (32 x 32 tile, PPU 32, Point filter per spec s20). Audio: low continuous belt hum; stall stops the hum on that segment.

## UI Requirements

Hover on a cell: group ItemId, count, state and ReasonCode. Build-hud marks open ends and stalled cells.

## Acceptance Criteria

- **GIVEN** A→B→C with A=wood x100, B=wood x200, C=empty, **WHEN** one tick runs, **THEN** A=empty, B=wood x100, C=wood x200 and B, C groups are distinct.
- **GIVEN** A→B with A=wood x100, B=wood x50 and B stalled, **WHEN** ticked, **THEN** A stays wood x100 (no merge, no partial).
- **GIVEN** a belt end facing a chest with no accessor, **WHEN** ticked, **THEN** the end cell is Stalled `NoReceivingPort`.
- **GIVEN** a belt end with an accessor into a chest holding 199/200 wood and the cell holding wood x2, **WHEN** ticked, **THEN** chest=200, cell=wood x1 Stalled `DownstreamCapacity`.
- **GIVEN** two belts X and Y both facing empty cell Z, **WHEN** ticked, **THEN** exactly one group enters Z and the other stays.
- **GIVEN** a 4-cell loop with one group, **WHEN** 100 ticks run, **THEN** the group has moved exactly 100 hops and never duplicated.
- **GIVEN** a group that entered cell B this tick, **WHEN** the same tick continues, **THEN** it does not also move to C.
- **GIVEN** 2,000 belt cells with groups, **WHEN** ticked, **THEN** conveyor processing costs under 1 ms.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Exact cell occupancy and arrow rendering at orthogonal joints | placement-editing GDD / art | VS |
| 2 | Tier 2 belt speeds and whether belts become mechanical loads | game-designer | Tier 2 |
| 3 | Whether large-group / small-maxStack belt jams need a UX hint | ux-designer | VS |
