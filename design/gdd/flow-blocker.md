# Flow Blocker

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Simple Entry / Unfathomable Depth (one rule: this side does not accept), Realism First (a blocker is a physical wall, not a filter)
> **Normative Source**: design/confirmed-mechanics-spec.md s7.8, s19
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

A flow blocker is an attachment installed on one side (N/E/S/W) of a conveyor cell or GlassPipe cell. It disables that side's `IngressEdge` in the connection graph so nothing can enter from that direction; it does not affect other sides, the cell's output, or what is already inside. It is not a filter. Its main purpose is to seal open inputs that automatic belt routing creates so adjacent lines do not cross-feed. Blockers can be placed manually, in bulk by dragging, or automatically by the pathfinder; they are installed and removed via transactions effective next tick, and removal that would join two working networks must show the consequences first.

## Player Fantasy

Direct and small. Snapping a blocker onto a belt side is like closing a gate; the arrow disappears and the player stops worrying about that side. The deeper fantasy is trust in dense layouts: belts can run side by side without mystery merges.

## Detailed Design

### Core Rules

1. A blocker occupies one side of one cell; at most one per side, so up to four per cell.
2. Effect: the `IngressEdge` from the neighbor on that side into this cell is disabled. Adjacency remains in the graph; removing the blocker re-enables the edge on the effective tick without neighbor rediscovery.
3. Blockers do not delete items, do not consume transactions, and do not mix groups; a blocked entry transaction simply fails and the payload stays at its source.
4. Blockers do not change the cell's output direction.
5. On GlassPipe: fully blocks connection on that side regardless of liquid or gas.
6. Blockers are not filters: no per-substance or per-phase configuration.
7. Same logical item for belts and pipes; two visual skins by carrier (spec s20).
8. Install/remove are unified transactions. Transfers already accepted in the current tick are not retroactively cancelled; connection changes apply from the next tick.
9. Removal that would connect two currently working networks must display upstream, downstream, and potential cross-flow before commit and require confirmation.
10. Removal refunds the blocker item by default (a specific blocker type may later declare irreversible loss).
11. Placement modes: single click on a cell side in Blocker mode installs; clicking the same side again removes. Drag across cells installs on the same relative side of each. Pathfinder auto-placement (placement-editing rule 18) consumes warehouse blockers; if none, omits and marks open ends; a modifier key suppresses auto-placement for that drag.
12. Inspection UI shows all four sides' blocker status (machine-inspection-ui rule 4).

### States and Transitions (per cell side)

| State | Transition | Next |
|---|---|---|
| Open | install committed | Blocked (effective next tick) |
| Blocked | remove committed (confirmed if networks would join) | Open (effective next tick) |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| grid-world | both | IngressEdge enable/disable per side |
| conveyor | out | entry from a blocked side fails `IngressBlocked` |
| glass-pipe | out | same edge semantics for fluid connection |
| placement-editing | in | manual, drag, and auto placement; suppression modifier |
| market-interface (warehouse) | in | blocker item stock; refund |
| tick-simulation | in | install/remove transactions |
| build-hud | out | open-end marking, network-join warning |
| machine-inspection-ui | out | four-side status |
| save-load | out | blocker per side persisted as block attribute of the cell |

## Formulas

The ingress predicate with blockers is defined as:

`ingressEnabled(cell, side) = ¬hasBlocker(cell, side)`

which feeds grid-world's `connected(A.out, B.in)` and conveyor's `canMove`.

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| cell | cell | cell | - | receiving cell |
| side | side | enum | N,E,S,W | direction the flow would come from |

**Output Range:** boolean.
**Example:** belt B has a blocker on its N side; belt A at north of B facing S: `connected(A,B)` is false; A stalls `IngressBlocked`.

The network-join detection predicate is defined as:

`wouldJoin(cell, side) = hasFlow(network(neighbor(cell, side))) ∧ hasFlow(network(cell)) ∧ network(neighbor) ≠ network(cell)`

**Output Range:** boolean; true requires confirmation UI.
**Example:** removing a blocker between two active lines: confirmation shows both lines and the merge point.

## Edge Cases

- **If a blocker is on the side the cell's own output faces**: allowed; it blocks *incoming* from that side only and does not affect the outgoing edge. UI shows a hint that this blocker has no effect on output.
- **If a blocker is installed on a machine or chest**: not allowed in this version (`CarrierUnsupported`); only belts and pipes are carriers.
- **If the cell is removed**: its blockers are removed and refunded with it.
- **If a blocker is installed and a group tries to enter from that side in the same tick**: this tick's already-validated entry commits; from next tick it is blocked.
- **If the warehouse has zero blockers during auto-placement**: omitted; open ends marked; no failure.
- **If the player suppresses auto-blockers**: none placed; open ends marked.
- **If a drag-install crosses cells that already have a blocker on that side**: skipped, not toggled off.
- **If a save contains a blocker on a cell that is no longer a belt/pipe**: dropped to warehouse on load with a notice.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| grid-world, tick-simulation, conveyor | hard | |
| placement-editing | hard | placement modes |
| market-interface | soft | item stock |
| glass-pipe | downstream | Alpha |
| build-hud, machine-inspection-ui | downstream | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| blocker.basePrice | 1 | 0..10 | spam | players skip sealing |
| autoPlaceDefault | on | bool | see placement-editing | |
| refundOnRemove | true | bool | - | - |
| joinWarningEnabled | true | bool | silent cross-flow | - |

## Visual/Audio Requirements

Belt skin: small bar across the blocked edge; pipe skin: cap. 32 x 32 overlay canvas, rotated from one canonical direction (spec s20). Click cue on install/remove.

## UI Requirements

Blocker mode in the sidebar; drag preview showing which side; network-join confirmation dialog listing affected upstream/downstream; four-side status in inspection.

## Acceptance Criteria

- **GIVEN** belt B with a N-side blocker and belt A north of B facing S carrying wood, **WHEN** ticked, **THEN** A stalls `IngressBlocked` and B is unchanged.
- **GIVEN** the same with the blocker on B's E side, **WHEN** ticked, **THEN** A's group enters B.
- **GIVEN** a blocker installed on tick t while a group was already validated to enter from that side, **WHEN** tick t commits, **THEN** the group enters; on t+1 further entries from that side fail.
- **GIVEN** two working lines separated by a blocker, **WHEN** the player removes it, **THEN** a confirmation listing both networks appears before any transaction is submitted.
- **GIVEN** a cell with 4 blockers, **WHEN** inspected, **THEN** all four sides show Blocked.
- **GIVEN** a drag-install across 5 cells on side N where 2 already have N blockers, **WHEN** released, **THEN** 3 blockers are consumed and 5 cells show N Blocked.
- **GIVEN** removal, **WHEN** committed, **THEN** one blocker item returns to the warehouse.
- **GIVEN** a blocker on a pipe side, **WHEN** a neighbor pipe tries to push gas from that side, **THEN** no connection exists.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Blocker ItemId and final visuals (spec s19) | art-director | VS |
| 2 | Blockers on machine Ports as a later feature | systems-designer | Alpha |
| 3 | Irreversible-loss blocker variants | game-designer | Tier 2 |
