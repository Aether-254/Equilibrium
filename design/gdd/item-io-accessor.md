# Item I/O Accessor

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Simple Entry / Unfathomable Depth (three flow modes anyone can read; three quantity modes for precise control), Factory and Economy Are One (accessor rates set line throughput and thus profit)
> **Normative Source**: design/confirmed-mechanics-spec.md s7.4, s7.6, s8
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The item accessor is a belt attachment that exchanges items between a plain Container (or a machine Port that requires an accessor) and the belt cell it sits on. It has a flow mode (belt→container, container→belt, auto), an optional ItemId filter, and a quantity mode (Exact, UpTo, Keep) with an `amount` up to 3000. It is the only way chests talk to belts, and its quantity modes are the player's first precise flow-control tool.

## Player Fantasy

Direct. The player snaps an accessor onto a belt beside a chest and picks "chest → belt, up to 10"; the belt starts pulsing ten wood per tick. Later they discover Keep mode to hold a safety stock while exporting surplus. The fantasy is precise, readable control over a flow they can see.

## Detailed Design

### Core Rules

1. An accessor is an attachment installed on a conveyor cell, facing one orthogonal side. The target is the block on that side.
2. Flow modes shown to the player: `Belt -> Container`, `Container -> Belt`, `Auto`. Auto resolves to a concrete mode from the belt's direction: if the target is ahead of the belt end, Belt -> Container; if the belt starts here, Container -> Belt. Player-visible text MUST express direction, never bare "insert/extract".
3. Optional filter: a single ItemId (Tag filters are VS scope). Non-matching items MUST NOT move.
4. `Belt -> Container` uses best-effort partial delivery (conveyor rule 16): deliver `min(groupCount, acceptable)`; the remainder stays and the cell stalls.
5. `Container -> Belt` uses a quantity mode:
   - **Exact**: transfer `amount` or nothing.
   - **UpTo**: transfer `min(sourceCount, amount, acceptable)`.
   - **Keep**: transfer `min(max(sourceCount - amount, 0), 3000, acceptable)`; `amount` is the desired remaining stock.
6. `amount` is in `[1, 3000]`.
7. Container -> Belt onto a cell that already holds the same ItemId is allowed only if that cell is stalled this tick, and never above the cell's effective capacity (spec s7.4). Different ItemId: never.
8. Container -> Belt onto an empty cell creates a new group.
9. Accessor transactions have default priority +1 over the cell's own forward move (conveyor edge case) so extraction from a stalled cell is preferred.
10. If the target is air or not a Container, the accessor reports `TargetInvalid` and the cell stalls when it holds items.

### States and Transitions

| State | Condition |
|---|---|
| Idle | nothing to move this tick |
| Transferred(n) | committed n items |
| Blocked(ReasonCode) | attempted but rejected: `TargetInvalid`, `FilterMismatch`, `ExactUnsatisfied`, `TargetFull`, `SourceEmpty` |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| conveyor | both | installed on a cell; partial delivery; merge exception; priority |
| container | both | `available(item)`, `acceptable(item)`; transfer transactions |
| tick-simulation | in | one transaction per accessor per tick |
| grid-world | in | attachment facing; target block lookup |
| machine-processing | both | Ports flagged `requiresAccessor` are reached only via accessors |
| economy-ledger | out | items moved; blocked ticks with cause |
| machine-inspection-ui / build-hud | out | mode, filter, amount, state |

## Formulas

The Exact formula is defined as:

`transfer = amount` if `sourceCount >= amount ∧ acceptable >= amount` else `0`

The UpTo formula is defined as:

`transfer = min(sourceCount, amount, acceptable)`

The Keep formula is defined as:

`transfer = min(max(sourceCount − amount, 0), 3000, acceptable)`

**Variables (all three):**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| source count | sourceCount | int | 0..Σcap | count of the filtered ItemId in the source Container |
| amount | amount | int | 1..3000 | configured |
| acceptable | acceptable | int | 0..3000 | target belt cell's acceptable for this ItemId (container GDD) |

**Output Range:** 0..3000 per tick.
**Example:** Keep, amount 50, source 130, belt cell empty with effective cap 64: `min(80, 3000, 64) = 64` moved; next tick source 66 → `min(16, 3000, 64) = 16`.

The Belt -> Container delivery formula is defined as:

`delivered = min(groupCount, acceptable(target, item))`; remainder `groupCount − delivered` stays on the belt.

**Example:** chest 199/200, belt wood x2 → delivered 1, remainder 1, cell stalled `DownstreamCapacity`.

## Edge Cases

- **If filter is set and the source has other items**: only the filtered ItemId is considered; `SourceEmpty` if none.
- **If filter is unset in Container -> Belt and the source holds several ItemIds**: pick the lowest `SlotIndex` non-empty slot's ItemId for this tick (deterministic); the belt cell only ever holds one ItemId.
- **If Exact amount exceeds the belt cell's effective capacity for the item**: permanently `ExactUnsatisfied`; inspection UI warns that `amount > effectiveCapacity`.
- **If Auto mode and the belt cell is mid-segment (neither start nor end)**: Auto resolves to Belt -> Container if the target is a Container, else `TargetInvalid`; UI shows the resolved mode.
- **If the target is a machine Port that accepts direct belt transfer**: the accessor is redundant; it still works, using the Port's Container.
- **If two accessors on adjacent cells target the same chest in one tick**: two transactions; container acceptable is shared; conflict resolved by tick-simulation.
- **If the accessor is removed mid-tick**: this tick's committed transfer stands.
- **If `amount` is set to 0**: clamp to 1 with UI warning.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| conveyor, container, tick-simulation, grid-world | hard | |
| item-registry | soft | filter ItemId; Tag filter later |
| machine-processing | soft | requiresAccessor ports |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| amountMax | 3000 | fixed by spec | - | - |
| defaultMode | Auto | enum | - | - |
| defaultQuantityMode | UpTo | enum | Exact confuses new players | - |
| defaultAmount | 16 | 1..3000 | belts trickle | first belt saturates |
| accessorPriority | +1 | -1..+1 | see conveyor GDD | |

## Visual/Audio Requirements

Small arm sprite on the belt edge facing the target; arrow shows resolved direction; pulse on transfer. Blocked: red tint on the arm.

## UI Requirements

Click accessor: mode selector (three direction-worded options), filter picker, quantity mode (Exact / UpTo / Keep with one-line semantics), amount field. Shows last tick result and ReasonCode.

## Acceptance Criteria

- **GIVEN** Exact 10, source 9, **WHEN** ticked, **THEN** 0 moved, `ExactUnsatisfied`.
- **GIVEN** Exact 10, source 10, belt acceptable 64, **WHEN** ticked, **THEN** 10 moved, source 0.
- **GIVEN** UpTo 10, source 7, **WHEN** ticked, **THEN** 7 moved.
- **GIVEN** Keep 50, source 130, belt acceptable 64, **WHEN** ticked, **THEN** 64 moved.
- **GIVEN** Keep 50, source 40, **WHEN** ticked, **THEN** 0 moved.
- **GIVEN** Belt -> Container, chest 199/200 wood, belt wood x2, **WHEN** ticked, **THEN** chest 200, belt wood x1, cell Stalled.
- **GIVEN** Container -> Belt onto a moving (non-stalled) cell holding wood x10, **WHEN** ticked, **THEN** no merge occurs.
- **GIVEN** the same onto a stalled cell holding wood x10 with effective cap 64, UpTo 100, **WHEN** ticked, **THEN** 54 added (cap reached).
- **GIVEN** filter iron_ingot and a chest holding only wood, **WHEN** ticked, **THEN** `SourceEmpty`.
- **GIVEN** accessor facing air, **WHEN** ticked, **THEN** `TargetInvalid`.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Tag filters on accessors | systems-designer | VS |
| 2 | Whether accessors have a build cost item or are free in Tier 1 | economy-designer | MVP tuning |
| 3 | Multi-ItemId round-robin extraction vs lowest-slot rule | systems-designer | VS |
