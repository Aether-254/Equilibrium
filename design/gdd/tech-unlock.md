# Tech Unlock

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Depth Is Progress (unlocks add state variables, instruments, and processes, never just bigger numbers), Simple Entry / Unfathomable Depth (linear trunk by fidelity tier, mesh inside a tier)
> **Normative Source**: design/confirmed-mechanics-spec.md s15; game-concept Fidelity Roadmap
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

There is no research system. Content becomes available through an unlock graph whose trunk is approximately linear along the fidelity tiers (mechanics → matter → thermochemistry → fluids → organics) and whose interior within each tier is a mesh. Unlock conditions combine real engineering achievements (a metric the factory demonstrably reached), tasks, licenses or technical documents (purchasable or rewarded), relationships (Tier 2+), and first-time phenomena (the first time a stall, a stable 90% load, or a byproduct occurs). Every unlock must add depth: a new state variable, a measurement, a process relationship, or a control capability. Machines that are merely higher-number copies are not permitted as unlock content.

## Player Fantasy

Direct. Unlocks feel earned by understanding: you got the gearbox because you ran a line at 90% load without stalling, not because a timer finished. The fantasy is that the game notices what you have learned and hands you the next instrument.

## Detailed Design

### Core Rules

1. Unlock nodes are data: `unlockId`, tier, content granted (ItemIds, recipes, UI capabilities, Ponders), conditions (any-of groups of all-of predicates), and a depth tag from {stateVariable, measurement, process, control, opportunity}.
2. A node with no depth tag fails content validation (`UnlockWithoutDepth`).
3. Trunk: entering tier N+1 requires a tier gate node whose conditions demonstrate mastery of tier N (e.g. profit threshold plus utilization plus a specific process achievement).
4. Mesh: within a tier, nodes may have multiple alternative condition groups (task path, achievement path, license path) so no single activity is mandatory; at least one non-task path must exist for every node (tutorial-task rule 10).
5. Condition types: metric predicate over ledger/system windows (as in tutorial-task), task completion, license possession (an ItemId in the warehouse or a flag), first-time phenomenon event (from tick-simulation emitted events), relationship state (Tier 2+ placeholder).
6. Unlocks are permanent per save.
7. Unlocks never modify physics or add multipliers; they only make content or capabilities available.
8. Tasks introduce and guide unlocks but do not replace exploration; an unlock can be discovered without its task.
9. Tier 1 unlock set (MVP → VS): engine, shaft, gear pair (8/16), saw, chest, belt, accessor available at start; gear 32 by "first Stalled event"; Keep mode by "sell 200 planks"; Mechanics tab trend by "load% between 60 and 95 for 120 s"; blockers by "first cross-feed detected or first open-end warning ignored for 60 s"; carry mode by "remove and re-place a machine 5 times".

### States and Transitions

| State | Transition | Next |
|---|---|---|
| Hidden | any condition group's first predicate becomes observable | Visible (teaser) |
| Visible | one condition group fully true | Unlocked |
| Unlocked | - | terminal |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| economy-ledger, machine-processing, mechanical-power | in | metrics and phenomena |
| tick-simulation | in | first-time phenomenon events |
| tutorial-task | both | task completion as condition; unlocks as rewards |
| market-interface | in/out | licenses as purchasable items; new suppliers as granted opportunities |
| item-registry | in | granted content ids; Addons may add nodes |
| machine-inspection-ui, build-hud | out | capability gates (e.g. trend view), availability in placement lists |
| save-load | out | unlocked set |

## Formulas

The unlock predicate is defined as:

`unlocked(n) = ∨_{g ∈ n.groups} ∧_{c ∈ g} holds(c)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| groups | n.groups | list | >= 1 | alternative paths |
| condition | c | predicate | - | see rule 5 |

**Output Range:** boolean; evaluated on relevant events, latched when true.
**Example:** gear 32: groups = [[firstEvent(Stalled)], [taskComplete(t_gearing)], [hasLicense(lic_gearing)]].

The tier gate example (Tier 1 → 2) is defined as:

`gate_2 = profit_5m >= 300 ∧ ∃ line: utilization >= 0.8 for 300 s ∧ firstEvent(Stalled) ∧ sold(plank) >= 500`

## Edge Cases

- **If content granted by a node is missing** (Addon absent): node unlocks; grant is pending until content returns.
- **If a node's only path is a task and tasks are disabled**: content validation error `NoNonTaskPath`.
- **If a phenomenon event occurs in a tutorial level**: does not count for the main save.
- **If a save is loaded into a build with a new node inserted upstream**: already-unlocked content stays unlocked; the new node is evaluated normally.
- **If an Addon adds a node granting a multiplier item**: item is allowed (registry does not know semantics) but the node fails `UnlockWithoutDepth` unless tagged; validation warns.
- **If a license is sold back to the market**: license possession conditions re-evaluate; already-latched unlocks remain.
- **If two nodes grant the same content**: content becomes available on the first; the second still latches.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| economy-ledger, machine-processing, tick-simulation | hard | conditions |
| tutorial-task | hard | interplay |
| market-interface, item-registry, save-load | hard | |
| machine-inspection-ui, build-hud | downstream | gates |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| tier gate thresholds | per rule 9 / gate_2 | data | tiers blur | walls |
| teaserVisibility | on | bool | mystery | overload |
| licensePrices | data | - | free depth | pay-to-progress feel |
| minPathsPerNode | 2 | 1..4 | forced path | authoring cost |

## Visual/Audio Requirements

Unlock notification with the depth tag ("New measurement: torque trend"); unlock graph view uses tier columns. Cue on unlock.

## UI Requirements

Unlock graph screen (tiers as columns, mesh inside), node details with condition groups and progress, teaser state for Visible nodes. UX spec via `/ux-design`.

## Acceptance Criteria

- **GIVEN** the first Stalled event in the main save, **WHEN** it is emitted, **THEN** gear 32 unlocks and appears in the placement list.
- **GIVEN** a node with only a task path, **WHEN** content validates, **THEN** `NoNonTaskPath` error.
- **GIVEN** a node without a depth tag, **WHEN** content validates, **THEN** `UnlockWithoutDepth` error.
- **GIVEN** load% held at 80 for 120 s, **WHEN** evaluated, **THEN** the Mechanics trend capability unlocks and the inspection UI shows the trend.
- **GIVEN** a Stalled event in a tutorial level, **WHEN** the main save is checked, **THEN** gear 32 is still locked.
- **GIVEN** an unlocked node, **WHEN** the save is reloaded, **THEN** it remains unlocked.
- **GIVEN** gate_2 conditions all met, **WHEN** evaluated, **THEN** Tier 2 nodes become Visible.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Full Tier 1 and Tier 2 node lists and thresholds (spec s19) | game-designer | Alpha |
| 2 | Relationship conditions (suppliers, NPCs) | game-designer | Tier 2 |
| 3 | Whether licenses are items or flags | economy-designer | Alpha |
