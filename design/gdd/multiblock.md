# Multiblock Structures

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Depth Is Progress (larger processes are compositions of parts the player already knows), Realism First (a structure is only as intact as its parts)
> **Normative Source**: design/confirmed-mechanics-spec.md s5, s11.3 (carry interactions)
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Multiblock structures let a machine span several cells. Every structure has one Controller and N Slaves; each Slave stores the Controller's world coordinate as a block attribute. Type I structures are compositions of independent machines/blocks that together satisfy a pattern; removing any required part invalidates the structure and loses its structure data, while the remaining blocks stay and the structure re-forms automatically when the pattern is restored. Type II structures are a single machine instance occupying several cells; removing any cell drops the whole machine once. Structures may expose dedicated item, energy, or rotation Ports and optional state animations. If a Slave is loaded but its Controller's chunk is not, the structure freezes rather than being mistaken for removed.

## Player Fantasy

Direct. Assembling a Type I furnace from bricks and a core and seeing it "click" into a working whole is a construction payoff; placing a big Type II press that stamps down across four cells is a scale payoff. The fantasy is composition: bigger processes are built from parts you understand.

## Detailed Design

### Core Rules

**Common**
1. Structure definition data declares: type (I or II), pattern/footprint, Controller position rule, Ports (kind, facing, bound Container/Slot key, direct-belt or accessor-required), optional state animations, and for Type I the set of required parts.
2. Each Slave stores `controllerCoord` as a block attribute (vanishes on removal).
3. The Controller hosts the machine-processing instance and all Containers; Slaves have none of their own beyond what their base block already has (Type I) or nothing (Type II).
4. Structure Ports may be on Slaves; they route to the Controller's Containers by key.

**Type I: composition**
5. Parts are independent placed blocks. The structure exists when the pattern is satisfied; detection runs on block update (place/remove/rotate) of any cell in or adjacent to a candidate pattern.
6. Removing any required part invalidates the structure immediately: Controller and Slave structure data (including `controllerCoord`) is cleared; persistent attributes of the *structure* (its Containers, progress) are lost unless the definition marks them to fall back into the Controller block's own Containers.
7. Remaining parts MUST NOT auto-drop; they remain as ordinary blocks.
8. Restoring the pattern re-forms the structure via local update; no player action needed.
9. Because structure data is lost on invalidation, removal or carry of a required part MUST warn the player via UI before commit (placement-editing rule 24).

**Type II: single machine, many cells**
10. One machine instance occupies a footprint; placement anchors at the pointed cell or footprint center per the definition's anchor rule.
11. Removing any occupied cell removes the whole machine and produces exactly one drop carrying the machine's persistent attributes.
12. Selecting any cell in carry mode selects the whole structure.

**Cross-chunk**
13. If a Slave's chunk is loaded and the Controller's is not, the system force-loads the Controller's chunk.
14. If that fails, the structure freezes: no processing, logistics, energy, or structural progression; machine state `Frozen` with `ControllerUnloaded`.
15. Retry periodically; on success and re-validation, unfreeze automatically.
16. Frozen MUST NOT be interpreted as removed; structure data is never cleared by unload.

### States and Transitions

| State | Enter | Exit |
|---|---|---|
| Unformed (Type I parts present, pattern incomplete) | any part placed | pattern satisfied → Formed |
| Formed | pattern satisfied / Type II placed | required part removed → Unformed (Type I); any cell removed → Removed (Type II) |
| Frozen | Controller chunk unavailable | Controller loaded and validated → Formed |
| Removed | Type II drop | - |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| grid-world | both | footprint occupancy; `controllerCoord` block attribute; Port facings; block-update hooks |
| machine-processing | out | one instance on the Controller; `Frozen` state and `ControllerUnloaded` ReasonCode |
| container | both | structure Containers on the Controller; Ports bound by key |
| conveyor, item-io-accessor | in | Ports on Slaves accept/emit per Port definition |
| mechanical-power | in | rotation Ports on Slaves route to the Controller's load |
| placement-editing | both | anchor rule, selection completion, Type I warning and "select whole structure" |
| blueprint | in | rotation must transform anchor and Port facings |
| save-load | out | `controllerCoord`, structure id, frozen flag; chunk loading policy |
| machine-inspection-ui | out | Controller panel with footprint highlight; Slave links to Controller |

## Formulas

The Type I pattern predicate is defined as:

`formed(P, anchor, orientation) = ∀ (offset, requiredBlock) ∈ P : block(anchor + rotate(offset, orientation)) matches requiredBlock`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| pattern | P | set<(offset, matcher)> | 1..64 cells | from definition |
| anchor | anchor | cell | - | Controller candidate |
| orientation | orientation | int | 0..3 | rotations to try |
| matcher | requiredBlock | ItemId or Tag | - | acceptable block |

**Output Range:** boolean; first satisfied (anchor, orientation) in stable scan order wins.
**Example:** 3x3 furnace: 8 `#brick` around one `furnace_core`; core is the Controller.

The Type II drop rule is defined as:

`drops(structure) = 1` regardless of footprint size.

## Edge Cases

- **If two Type I patterns could claim the same part**: stable scan order (lowest anchor coordinate, then orientation) wins; the other stays Unformed.
- **If a Type I part is rotated in place** and the pattern requires a facing: re-evaluate; may invalidate.
- **If a Type I structure invalidates while Working**: reservations are released to the Controller block's own Containers if it has them, else items drop to the warehouse with a notice; progress is lost (documented risk, rule 9).
- **If a Type II placement footprint partially overlaps**: placement fails `CellOccupied`.
- **If a Slave's `controllerCoord` points to a cell that is not a Controller** (corrupt save): the Slave is treated as an ordinary block; log `OrphanSlave`.
- **If the Controller chunk load fails for more than N retries**: keep Frozen; surface in UI; never clear.
- **If the player carries a whole Type I selection**: structure data is preserved through the move because the pattern is re-satisfied at the destination on commit; the definition may opt out if positions matter.
- **If a Type II structure is partially selected in carry**: selection auto-completes (rule 12).
- **If chunking does not exist yet** (Tier 1 fixed map): rules 13-16 are trivially satisfied; the state machine still exists.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| grid-world, machine-processing, save-load | hard | |
| container, tick-simulation | hard | |
| placement-editing, blueprint | soft | interaction rules |
| mechanical-power, conveyor, item-io-accessor | soft | Ports |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| maxPatternCells | 64 | 4..256 | limits designs | scan cost |
| controllerLoadRetryTicks | 60 | 5..600 | churn | long freezes |
| typeIProgressFallback | toControllerContainers | enum | items lost | - |
| invalidationWarning | on | fixed | - | - |

## Visual/Audio Requirements

Formation flash and settle animation; Formed structures may show a combined sprite or per-part overlays; state GIFs optional per definition. Frozen: desaturated overlay. Invalidation: brief break cue.

## UI Requirements

Removal/carry warning for Type I with highlighted missing parts and "select whole structure"; inspection on any cell opens the Controller with footprint highlight; Frozen reason visible.

## Acceptance Criteria

- **GIVEN** a valid 3x3 Type I pattern, **WHEN** the last brick is placed, **THEN** the structure forms on the next tick and the core becomes Controller with all bricks storing its coordinate.
- **GIVEN** a formed Type I, **WHEN** one required brick is removed, **THEN** structure data is cleared, the other 7 bricks remain placed, and no drops occur beyond the removed brick.
- **GIVEN** the same, **WHEN** the brick is replaced, **THEN** the structure re-forms without player action.
- **GIVEN** a player removing a required Type I part, **WHEN** they click remove, **THEN** a warning is shown before the transaction is submitted.
- **GIVEN** a 2x2 Type II press holding items, **WHEN** any of its 4 cells is removed, **THEN** exactly one drop with the items appears and all 4 cells are empty.
- **GIVEN** a Type II press, **WHEN** one cell is selected in carry mode, **THEN** all 4 are selected.
- **GIVEN** a Slave loaded with its Controller chunk unavailable, **WHEN** ticked, **THEN** state Frozen `ControllerUnloaded` and no processing occurs; structure data intact.
- **GIVEN** the Controller chunk later loads, **WHEN** validated, **THEN** state returns to Formed.
- **GIVEN** a Port on a Slave, **WHEN** a belt feeds it, **THEN** items land in the Controller's bound Container.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Chunking model (depends on map model) | technical-director | VS |
| 2 | Whether Type I structures can be blueprinted as a unit | blueprint GDD | Alpha |
| 3 | Type I progress preservation policy per structure | game-designer | VS |
