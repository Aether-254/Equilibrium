# Group Processing Devices

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Realism First (belts never split or merge on their own; a device does it, visibly), Simple Entry / Unfathomable Depth (three explicit jobs: split, combine, batch)
> **Normative Source**: design/confirmed-mechanics-spec.md s7.7, s19
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Plain conveyors never split, merge, or resize item groups. Group processing devices are the explicit machines that do: a Splitter divides an incoming group across outputs, a Combiner merges groups from several inputs into one, and a Batcher accumulates and releases groups of a configured size. Whether these ship as three blocks or one configurable block with three modes is left open; this GDD defines the three behaviors so either form implements the same semantics. The first version implements the minimal set needed to close logistics semantics and does not grow into a sorting network.

## Player Fantasy

Direct. The player who needs to feed two saws from one log line places a Splitter and watches groups alternate; the player who wants exact 64-plank pallets for the market places a Batcher. The fantasy is deliberate logistics: nothing happens to your flow that you did not build.

## Detailed Design

### Core Rules

**Common**
1. A device occupies one cell, has one or more item-in Ports and one or more item-out Ports (direct belt transfer allowed), and an internal Container per Port.
2. Devices are unpowered in Tier 2 (open question 3); throughput is bounded by one group action per tick per Port.
3. Devices obey group semantics on their belt sides: whole-group entry, whole-group exit; internal operations are where regrouping happens.
4. A device's output push is a normal belt insertion (conveyor rule); if the target cell cannot accept the whole outgoing group, the device holds it (`OutputFull`).

**Splitter**
5. One input, 2..3 outputs. Modes: RoundRobin (whole incoming group to the next non-blocked output in rotation), Divide (split the group as evenly as possible across non-blocked outputs; remainder to the first in rotation), Ratio (data-configured integer weights).
6. Blocked outputs are skipped this tick; if all are blocked, the group waits internally and the input stalls.

**Combiner**
7. 2..3 inputs, one output. Each tick, take all internal input groups with the same ItemId and merge into one output group up to the output cell's effective capacity; excess stays internal. Different ItemIds are never merged; the Combiner emits groups one ItemId at a time in stable input-port order.

**Batcher**
8. One input, one output, configured `batchSize` in `[1, 3000]`. Accumulate incoming groups internally; when internal count >= batchSize, emit exactly `batchSize`. Optional `flushAfterTicks` emits a partial batch after idle time (default off).
9. Internal capacity is `min(3000, 2 * batchSize)` so a batcher can buffer while emitting.

### States and Transitions

| State | Condition |
|---|---|
| Idle | nothing internal |
| Holding | internal items, no action possible this tick (blocked or below batchSize) |
| Emitting | committed an output this tick |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| conveyor | both | belt-side group entry/exit; stall propagation |
| container | both | internal Containers; effective capacity for outputs |
| tick-simulation | in | one transaction per Port per tick |
| machine-processing | sibling | devices are machine-like blocks but with no recipe or power; reuse state/ReasonCode display |
| grid-world | in | Port facings; rotation |
| economy-ledger | out | holding ticks as logistics loss |
| tech-unlock | in | availability |

## Formulas

The Divide split formula is defined as:

`share_i = floor(count / k) + (1 if i < count mod k else 0)` for `i` in rotation order over the `k` non-blocked outputs

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| group count | count | int | 1..3000 | incoming group |
| open outputs | k | int | 1..3 | non-blocked this tick |

**Output Range:** Σ share_i = count.
**Example:** 100 across 3: 34, 33, 33.

The Ratio split formula is defined as:

`share_i = round-robin allocation of single units by weight w_i` implemented as largest-remainder: `share_i = floor(count * w_i / Σw) + remainder units by descending fractional part, ties by port order`.

**Example:** 100 with weights 2:1: 67, 33.

The Batcher emit rule is defined as:

`emit = batchSize if internal >= batchSize ∧ acceptable(outCell, item) >= batchSize else 0`

**Example:** batchSize 64, internal 70, out cell empty (cap 64 for planks): emit 64, internal 6.

## Edge Cases

- **If a Splitter receives two ItemIds over time**: each group is handled independently; no mixing.
- **If a Combiner has iron in port A and copper in port B**: emits iron first (port order), copper next tick.
- **If a Batcher's `batchSize` exceeds the output cell's effective capacity for the item**: permanently `OutputFull`; inspection warns `BatchExceedsCapacity`.
- **If Divide would give an output 0 items**: that output is skipped this tick (no empty groups).
- **If a device is removed with internal items**: items travel with the drop as persistent attributes.
- **If all Splitter outputs are blocked**: input stalls upstream with `DeviceFull`.
- **If `flushAfterTicks` triggers with 0 internal**: no-op.
- **If a device is rotated**: Ports rotate; internal items remain.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| conveyor, container, tick-simulation, grid-world | hard | |
| tech-unlock | soft | availability |
| machine-processing | sibling | shared UI patterns |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| splitterOutputsMax | 3 | 2..4 | - | sorting network creep |
| combinerInputsMax | 3 | 2..4 | - | same |
| batcher.batchSizeMax | 3000 | fixed | - | - |
| batcher.internalCapacityFactor | 2 | 1..4 | starves | hides jams |
| flushAfterTicksDefault | off | 0..600 | - | partial pallets |
| devicesPowered | false | bool (Tier 2 decision) | - | - |

## Visual/Audio Requirements

Directional glyphs per Port; mode indicator; internal count on hover. Clunk cue on batch emit.

## UI Requirements

Inspection: mode selector (Splitter), ratio weights, batchSize, flush toggle; internal Containers in Logistics tab.

## Acceptance Criteria

- **GIVEN** a RoundRobin Splitter with 2 open outputs and groups of 10 arriving each tick, **WHEN** 4 ticks run, **THEN** outputs receive groups on alternating ticks (A, B, A, B).
- **GIVEN** Divide with 3 open outputs and a group of 100, **WHEN** processed, **THEN** 34/33/33.
- **GIVEN** Divide with output B blocked, **WHEN** a group of 100 arrives, **THEN** 50/0/50 and B receives nothing.
- **GIVEN** Ratio 2:1 and 100 items, **WHEN** processed, **THEN** 67/33.
- **GIVEN** a Combiner with wood x10 and wood x20 internal, **WHEN** the out cell is empty (cap 64), **THEN** one group wood x30 exits.
- **GIVEN** a Combiner with wood and stone internal, **WHEN** ticked twice, **THEN** two separate groups exit, wood first.
- **GIVEN** a Batcher batchSize 64 with 70 internal, **WHEN** the out cell is empty, **THEN** a group of exactly 64 exits and 6 remain.
- **GIVEN** a Batcher batchSize 100 and plank cap 64, **WHEN** inspected, **THEN** `BatchExceedsCapacity` warning.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | One configurable device vs three blocks (spec s19) | game-designer | Alpha |
| 2 | Recipes, speed, and energy for devices | systems-designer | Tier 2 |
| 3 | Whether devices become mechanical loads | mechanical-power GDD | Tier 2 |
