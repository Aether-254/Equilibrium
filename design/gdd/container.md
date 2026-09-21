# Container

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Realism First (conservation: nothing appears or vanishes), Simple Entry / Unfathomable Depth (one abstraction reused by chests, machines, conveyors, pipes)
> **Normative Source**: design/confirmed-mechanics-spec.md s6.1-6.5, s18
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

A Container is the single item-storage and transaction abstraction shared by chests, machine buffers, conveyor cells, and (by analogy) pipe contents. It is a configurable list of independent Item Slots, each holding at most one `ItemId` with an effective capacity of `min(slot.capacity, itemDef.maxStack)`, optional allow/deny filters by ItemId or Tag, and a deterministic insertion algorithm with at most one auto-tidy per transaction. Ports bind to a Container (and optionally a Slot) by stable string key so machines expose exactly which inventory a conveyor or accessor touches. Players feel Containers as "full storage" losses, as visible slots in the inspection UI, and as the reason chests need accessors rather than magically pulling from belts.

## Player Fantasy

Indirect with a direct surface. The player sees slots fill and drain and learns that capacity is a design variable: a bigger warehouse lets them wait for a better price (Pillar 2), a tiny machine buffer means the belt must feed steadily. The fantasy is control: "I know exactly where every item is and why it stopped."

## Detailed Design

### Core Rules

1. A Container is an ordered list of Item Slots indexed by stable `SlotIndex` (0..n-1) and optionally by stable string `slotKey`.
2. A non-empty Slot holds exactly one `ItemId` and a count in `[1, effectiveCapacity]`.
3. `effectiveCapacity(slot, item) = min(slot.capacity, itemDef.maxStack)`. All acceptance calculations use effective capacity, never raw `slot.capacity`.
4. Slot filters: allow ItemIds, allow Tags, deny ItemIds, deny Tags. Deny beats allow when both match.
5. A Container is storage plus transaction capability only; it does not imply conveyor connectivity. Connectivity is a Port property (grid-world GDD).
6. Ports bind to a Container by string key and optionally to a Slot by string key. Saves and content data MUST NOT bind by array index.
7. Chests are plain Containers: they MUST NOT accept direct conveyor input/output and MUST NOT auto-connect to adjacent belts; exchange happens via item-io-accessor.
8. Machines typically own several Containers (input, output, byproduct, internal). Each Port names which one it reads or writes.
9. A single conveyor cell is a special Container with one Slot of capacity 3000 (conveyor GDD owns movement rules).
10. Default machine port orientation before rotation: left = main input, right = main output, top = energy/rotation input, bottom = byproduct output. Rotation rotates all four (grid-world GDD).

### Deterministic Insertion

Given an insert request `(item, count)`:
1. Fill existing Slots that already hold `item`, are not full, and pass filters, in ascending `SlotIndex`.
2. Fill empty Slots that pass filters, ascending `SlotIndex`.
3. If the request cannot be fully placed under the current layout but could be after tidying, run auto-tidy once.
4. Re-plan the insertion after tidy.
5. At most one auto-tidy per insert transaction.

Auto-tidy: merge same-ItemId Slots toward the lowest `SlotIndex`, respecting filters and effective capacity. Ordering MUST be fixed by `SlotIndex` and `ItemId` string order, never by runtime object enumeration order.

Acceptance quantity for a request is computed up front (`acceptable = Σ free effective capacity across eligible slots after at most one tidy`) and reported to the tick engine so partial-accept rules (conveyor s7.6) and exact-mode rules (accessor s8.4) can decide.

### States and Transitions

Containers have no state machine; each Slot is Empty or Holding(item, count). Transitions occur only through committed transactions.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | insert/extract/move requests; `acceptable(item)` and `available(item)` queries against snapshot |
| item-registry | in | `itemDef.maxStack`, Tag membership for filters |
| conveyor | both | conveyor cell is a Container; belt-to-belt transfer is a Container move with group semantics enforced by conveyor GDD |
| item-io-accessor | both | reads `available`, `acceptable`; submits transfers |
| machine-processing | both | reservation of inputs; output insertion; byproduct routing |
| market-interface | both | save-level warehouse is a Container; buy requires reserved acceptable capacity |
| save-load | out | Slot contents by `ItemId` and count; Port bindings by string key |
| machine-inspection-ui | out | slot contents per Container for the Logistics tab |

## Formulas

The effective capacity formula is defined as:

`effectiveCapacity = min(slot.capacity, itemDef.maxStack)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| slot capacity | slot.capacity | int | 1..3000 | Configured per slot |
| item max stack | itemDef.maxStack | int | 1..3000 | From item-registry |

**Output Range:** 1..3000.
**Example:** Conveyor slot capacity 3000, `iron_gear.maxStack = 64` gives effective 64 on a belt cell.

The acceptable quantity formula is defined as:

`acceptable(C, item) = Σ_{s ∈ eligible(C,item)} (effectiveCapacity(s,item) − count(s))`

where `eligible` = slots passing filters that are empty or hold `item`, evaluated after at most one hypothetical tidy.

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| container | C | Container | - | target |
| item | item | ItemId | - | requested item |
| current count | count(s) | int | 0..cap | per slot |

**Output Range:** 0..Σ capacities.
**Example:** Chest with one slot capacity 200 holding wood x199: `acceptable = 1`.

## Edge Cases

- **If an item matches both an allow Tag and a deny ItemId**: denied (rule 4).
- **If `slot.capacity` < `itemDef.maxStack` for every item**: fine; effective capacity is the slot value.
- **If tidy would move an item into a slot whose filter denies it**: that move is skipped; tidy never violates filters.
- **If two inserts of different items target one empty slot in the same tick**: tick-simulation conflict resolution decides; loser stays at source.
- **If a Slot holds an `UnknownItem`**: it is treated as a distinct ItemId (its original id); filters evaluate against the original id; `maxStack` falls back to the default max stack knob.
- **If a Port's bound `slotKey` no longer exists after a content update**: Port is marked invalid with ReasonCode `PortBindingMissing`; machine enters Blocked state.
- **If a Container definition changes slot count in a content update and a save has more filled slots than now exist**: extra items are placed into an overflow Container on the same block and surfaced in UI; never deleted.
- **If insertion count is 0**: no-op transaction, accepted trivially, no event.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation | hard | All mutations are transactions |
| item-registry | hard | maxStack and Tags |
| conveyor, item-io-accessor, machine-processing, market-interface, multiblock, glass-pipe | downstream hard | Reuse the abstraction |
| save-load | downstream hard | Serialization contract |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| conveyorSlotCapacity | 3000 | fixed (spec) | - | - |
| chestSlotCount | 27 | 1..64 | storage useless | UI unwieldy |
| chestSlotCapacity | 200 | 16..3000 | constant "full" losses | storage trivialises market timing |
| machineInputBufferSlots | 1 per input | 1..4 | belts must be perfect | buffers hide bottlenecks |
| autoTidyEnabled | true | bool | fragmentation blocks inserts | none |

## Visual/Audio Requirements

Slot render in inspection UI: icon, count, filter badge. "Full" state exposes a loss-cause event to economy-ledger; visual is owned by machine-inspection-ui / build-hud.

## UI Requirements

Inspection UI Logistics tab lists every Container of a block with its slots and filters. Filter editing UI for chests is Vertical Slice scope.

## Acceptance Criteria

- **GIVEN** a slot capacity 3000 and `iron_gear.maxStack = 64`, **WHEN** inserting 100 iron_gear into an empty single-slot container, **THEN** 64 are accepted and 36 remain at source.
- **GIVEN** slots [wood x10, empty, wood x5] with capacity 64, **WHEN** inserting wood x60, **THEN** slot 0 becomes 64, slot 2 becomes 11, slot 1 stays empty (existing-first, ascending index).
- **GIVEN** slots [wood x32, wood x32] capacity 64 and an insert of stone x1, **WHEN** processed, **THEN** one auto-tidy merges wood into slot 0 (x64) and stone enters slot 1.
- **GIVEN** the same container state twice, **WHEN** the same insert runs, **THEN** resulting slot layouts are identical (determinism).
- **GIVEN** a slot denying `#ingot` and allowing `iron_ingot`, **WHEN** inserting iron_ingot, **THEN** rejected.
- **GIVEN** a chest adjacent to a belt with no accessor, **WHEN** 100 ticks pass, **THEN** the chest contents are unchanged.
- **GIVEN** a Port bound to `slotKey: "input_a"`, **WHEN** saved and the slot array is reordered in content, **THEN** on load the Port still resolves to the slot with key `input_a`.
- **GIVEN** 1000 containers each receiving one insert per tick, **WHEN** measured, **THEN** container processing costs under 0.5 ms per tick.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Whether Containers expose fluid/gas slots or glass-pipe uses a parallel abstraction | systems-designer | Alpha (glass-pipe GDD) |
| 2 | Overflow Container UX on content shrink | ux-designer | VS |
| 3 | Per-slot filter editing for chests in MVP or VS | game-designer | VS |
