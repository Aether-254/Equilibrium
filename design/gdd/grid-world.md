# Grid World

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Simple Entry / Unfathomable Depth (what you see connected is what is connected), Realism First (no action at a distance)
> **Normative Source**: design/confirmed-mechanics-spec.md s3, s4, s6.5
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The grid world is the spatial and structural model: a strict cell grid in which every machine, container, conveyor, and pipe occupies definite cells; a split between transient block attributes (orientation, controller pointer) and persistent attributes (internal items, stored energy, progress) with defined removal semantics; and an explicit connection model where logistics and energy links exist only through directional, compatible Ports. The system never infers a link from proximity. It is the substrate on which conveyors, machines, mechanical networks, blockers, and placement all operate.

## Player Fantasy

Indirect but always visible. The player can read the factory like a diagram: an arrow points into a port or it does not. Rotating a machine visibly rotates its ports. Picking up a machine keeps what was inside it. There are no hidden connections to hunt for, which is what makes "I understand why it runs" possible.

## Detailed Design

### Core Rules

**Grid**
1. The world is a 2D integer grid. Each cell holds at most one block occupant (plus overlay attachments such as blockers and accessors, owned by their GDDs).
2. Blocks have a footprint of one or more cells (multi-cell footprints are Type II multiblocks, multiblock GDD).
3. Orientation is one of four orthogonal directions.

**Block attributes vs persistent attributes**
4. Block attributes belong to the current placed instance and vanish on removal: orientation, controller coordinate (multiblock), port facing derived from orientation.
5. Persistent attributes survive removal into the dropped machine item and are restored on re-placement: internal items, stored electrical energy (when that system exists), processing progress, and any state the block's definition marks persistent. Whether fluid contents and temperature persist is decided by their systems.
6. Normal removal writes persistent attributes into the dropped item. Re-placing restores them. Orientation is not restored unless a dedicated move rule says so (placement-editing GDD carry mode does preserve it).

**Connections**
7. Logistics and energy connections exist only between an output Port and a compatible input Port on the adjacent cell that the output faces.
8. The system MUST NOT search "nearby containers" and MUST NOT connect blocks because they are adjacent.
9. A conveyor end checks only the cell directly ahead for a compatible input Port or configured attachment.
10. Rotating a block rotates all its Ports; input/output/energy/byproduct facings follow the rotation.
11. Default machine port layout (unrotated): left main input, right main output, top energy or rotational input, bottom byproduct output.
12. Each Port declares: kind (item-in, item-out, rotation-in, rotation-out, energy-in, energy-out, byproduct-out, fluid-in/out), facing (relative to block), bound Container/Slot key (for item ports), and whether it accepts direct conveyor transfer or requires an accessor.
13. Adjacency edges are stored in the connection graph as `IngressEdge`s per direction; a flow-blocker disables an edge without deleting the adjacency (flow-blocker GDD).

### States and Transitions

| Cell state | Transition | Next |
|---|---|---|
| Empty | place committed | Occupied(block) |
| Occupied | remove committed | Empty; drop item with persistent attrs |
| Occupied | rotate committed | Occupied (ports re-faced next tick) |
| Occupied | carry-mode pickup | Reserved(ghost) until move commits |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | place/remove/rotate are structural transactions effective next tick |
| container | both | Port -> Container/Slot key binding |
| conveyor, item-io-accessor, flow-blocker | both | adjacency graph, IngressEdge toggles, "cell ahead" query |
| mechanical-power | both | rotation ports and shaft adjacency form the mechanical network |
| multiblock | both | controller pointers as block attributes; footprint occupancy |
| placement-editing, blueprint | in | occupancy queries, ghost reservations |
| save-load | out | cell occupancy, orientation, persistent attributes |

## Formulas

The port facing formula is defined as:

`worldFacing = rotate(localFacing, orientation)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| local facing | localFacing | enum | N,E,S,W | facing in the block's unrotated definition |
| orientation | orientation | int | 0..3 | quarter turns clockwise |
| rotate | rotate | function | - | `(facingIndex + orientation) mod 4` |

**Output Range:** N,E,S,W.
**Example:** Main output local E, orientation 1 (90 deg CW) gives world S.

The connection predicate is defined as:

`connected(A.out, B.in) = adjacent(A.cell(out), B.cell(in)) ∧ A.out.facing == dir(A→B) ∧ B.in.facing == dir(B→A) ∧ compatible(A.out.kind, B.in.kind) ∧ ingressEnabled(B, dir(A→B))`

**Output Range:** boolean.
**Example:** Belt cell facing E at (3,4); machine at (4,4) with item-in port facing W and no blocker on its W side: connected.

## Edge Cases

- **If two output ports face each other**: not connected (kinds incompatible); no error, both are open ends.
- **If a block is rotated while a belt is feeding it**: the in-flight transactions of this tick commit against the old facing; from next tick the port has moved and the belt stalls with `NoReceivingPort`.
- **If a machine is removed while holding reserved recipe inputs**: reservations are released into persistent attributes and travel with the drop.
- **If a removal drop cannot be delivered to the player's warehouse (full)**: removal transaction fails with `WarehouseFull`; block stays.
- **If a Port's bound Container key is missing**: block flagged `PortBindingMissing` (container GDD).
- **If the world boundary is reached**: cells outside bounds are treated as permanently Occupied by an immutable Void block; nothing connects.
- **If a block definition's footprint overlaps an existing block on placement**: placement fails with `CellOccupied`.
- **If orientation data is missing in a save**: default to 0 and flag a warning; never fail load.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation | hard | structural transactions |
| item-registry | hard | dropped items are ItemIds; block definitions keyed by ItemId |
| container, conveyor, mechanical-power, multiblock, placement-editing, flow-blocker, glass-pipe, save-load | downstream hard | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| worldWidth / worldHeight (Tier 1) | 64 x 64 | 16..512 | no room for two strategies | undermines finite-space pillar |
| cellSizeWorldUnits | 1.0 (32 px at PPU 32) | fixed | - | - |
| dropToWarehouse | true | bool | drops litter the world | none |

Map model (fixed vs bounded expansion vs multiple maps) is open question 3 in game-concept and will change `worldWidth/Height` semantics.

## Visual/Audio Requirements

Port arrows rendered on hover/inspection; rotation preview shows re-faced ports before commit. Open ends optionally highlighted (build-hud GDD).

## UI Requirements

Inspection shows orientation and each Port's kind/facing/binding. Placement preview shows footprint and port facings.

## Acceptance Criteria

- **GIVEN** a chest adjacent to a machine output port with no accessor, **WHEN** 100 ticks pass, **THEN** no items move.
- **GIVEN** a machine with output facing E and a belt at E facing E, **WHEN** the machine produces, **THEN** items enter the belt.
- **GIVEN** the same machine rotated 90 deg CW, **WHEN** it produces, **THEN** output goes S, and the belt at E receives nothing.
- **GIVEN** a machine holding wood x10 internally, **WHEN** removed and re-placed, **THEN** it holds wood x10 and has orientation 0.
- **GIVEN** a belt whose end faces a cell with a chest, **WHEN** ticked, **THEN** the belt stalls with `NoReceivingPort` and the chest is unchanged.
- **GIVEN** a placement overlapping an occupied cell, **WHEN** committed, **THEN** it fails with `CellOccupied` and world state is unchanged.
- **GIVEN** a block at the world edge with output facing outward, **WHEN** it produces, **THEN** it blocks with `NoReceivingPort`.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Map model (fixed / bounded expansion / multi-map) and chunking | game-designer, technical-director | VS |
| 2 | Whether the mechanical network uses the same Port graph or a parallel shaft graph | systems-designer | mechanical-power GDD |
| 3 | Persistent attribute list for fluids/temperature | glass-pipe GDD | Alpha |
