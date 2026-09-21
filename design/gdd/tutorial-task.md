# Tutorial and Tasks

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Depth Is Progress (rewards are knowledge and instruments), Simple Entry / Unfathomable Depth (phenomenon first, gauge second, math third), anti-pillar Not a Pure Puzzle (tasks judge outcomes, not layouts)
> **Normative Source**: design/confirmed-mechanics-spec.md s14; game-concept Task System
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Teaching uses three linked forms: standalone tutorial levels for zero-risk, resettable practice of basic operations and a minimal complete line; main-save tasks that place that knowledge into the real economy and long-lived factory; and Ponder-style dynamic demonstrations that explain one machine, connection, or mechanism on demand without gating anything. Tasks guide rather than restrict: they accept outcomes and system metrics, never a specific layout, and reward knowledge, analysis tools, and new engineering or trading opportunities rather than production multipliers. The three forms may link to each other but must never force the same lesson twice.

## Player Fantasy

Direct but optional. The player who wants a guide gets a mentor that points at the next thing worth understanding and then gets out of the way; the player who wants to explore never hits a wall. The fantasy is earned competence: a task is finished because the factory really does what it says, and the reward is seeing further into the machine.

## Detailed Design

### Core Rules

**Three forms**
1. **Tutorial levels**: separate scenes with a fixed starting factory and warehouse; resettable; no effect on any save; cover operations (place, rotate, remove, belt drag, accessor modes, inspection) and the minimal complete line (engine → shaft → saw → belt → warehouse → sell).
2. **Main-save tasks**: a directed graph of tasks tied to the real economy; completion judged by observed outcomes (see rule 6).
3. **Ponder demos**: per-topic scripted demonstrations of one mechanism (e.g. "why did this belt stall", "gear ratio", "Keep mode"), openable from inspection ReasonCodes, item tooltips, and the task panel; never required.
4. Cross-linking: tutorial levels may point to a task; tasks may open a Ponder; a Ponder may suggest a tutorial level. A lesson completed in one form is marked known and other forms do not re-force it.

**Tasks**
5. Task data: `taskId`, title, description, prerequisites (task ids or unlock conditions), acceptance conditions (metric predicates), rewards, optional hints and linked Ponder ids.
6. Acceptance conditions are metric predicates on system state and ledger windows: e.g. "utilization of at least one saw >= 0.8 over 60 s", "sell 100 planks", "profit_60s >= 50", "a network exists with load% between 60 and 95", "zero stalled ticks on line X for 60 s". Never "place block at (x,y)".
7. Rewards (primary): knowledge (mechanism explanations, recipe and property info), analysis capability (unlock a gauge, a trend window, a comparison view), engineering or trading opportunity (new machine availability, a supplier, a contract). Secondary: licenses, vendor access, content entry points.
8. Rewards MUST NOT grant production multipliers, efficiency bonuses detached from physics, or repeatable currency income. Teaching tasks may refund a bounded fraction of materials/currency consumed to complete them (knob), once.
9. Basic building and core diagnostics (place, belt, accessor, inspection Overview/Logistics/Mechanics) are never locked behind tasks.
10. Tasks are optional: the player may ignore them; unlock structure (tech-unlock GDD) has non-task paths.

**Ponder**
11. A Ponder is a scripted, paused mini-scene with step text and a scrubbable timeline; it uses the real simulation on a throwaway world so what it shows is what the game does.
12. Ponders are addressable by id and referenced from ReasonCodes (inspection suggestion text), item tooltips, and tasks.

### States and Transitions (task)

| State | Transition | Next |
|---|---|---|
| Locked | prerequisites met | Available |
| Available | player accepts (or auto-track if configured) | Active |
| Active | all acceptance predicates true for their windows | Complete (rewards granted once) |
| Active | player abandons | Available |
| Complete | - | terminal |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| economy-ledger | in | metrics for predicates (profit, utilization, losses, sales) |
| machine-processing, mechanical-power, conveyor | in | state metrics (utilization, load %, stalled ticks) |
| tech-unlock | both | tasks introduce and may satisfy unlock conditions; unlocks may be task rewards |
| market-interface | in | sales metrics; supplier/contract rewards |
| machine-inspection-ui, build-hud | both | Ponder links; task tracker overlay and highlights |
| save-load | out | task states, known-lesson set |
| item-registry | in | reward content ids |

## Formulas

The task completion predicate is defined as:

`complete(task) = ∧_{c ∈ task.conditions} holds(c, window(c))`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| condition | c | predicate | - | metric op threshold over window |
| window | window(c) | ticks | 1..3600 | evaluation window; 1 for instantaneous |

**Output Range:** boolean, evaluated each tick while Active.
**Example:** "utilization(saw) >= 0.8 over 60" and "sold(plank, session) >= 100".

The bounded refund formula is defined as:

`refund = min(consumedCost * refundFraction, refundCap)`, granted once per task.

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| consumed cost | consumedCost | float | >= 0 | ledger-measured cost during the task |
| fraction | refundFraction | float | 0..0.5 | knob |
| cap | refundCap | float | >= 0 | knob |

**Example:** consumed 200, fraction 0.25, cap 40: refund 40.

## Edge Cases

- **If a condition becomes true then false within its window**: not complete; windows require sustained truth for windowed metrics.
- **If a task's reward content is missing** (Addon absent): task completes; reward is recorded pending and granted when content returns.
- **If a player completes a lesson in a tutorial level**: the same lesson's Ponder is marked seen and the main-save task's hint is collapsed; the task itself still requires the real-economy outcome.
- **If two tasks share a condition**: both can complete from the same outcome.
- **If the player abandons an Active task after partial refund eligibility**: no refund; eligibility resets on re-accept.
- **If a Ponder's script references a machine removed by a content update**: Ponder marked unavailable; never crash.
- **If a task predicate references a machine that no longer exists**: evaluates false; UI shows "target missing".
- **If tasks are disabled in options**: unlock structure must still be reachable (tech-unlock non-task paths).

## Dependencies

| System | Kind | Notes |
|---|---|---|
| economy-ledger, machine-processing, mechanical-power, conveyor | hard | metrics |
| tech-unlock | hard | reward/unlock linkage |
| machine-inspection-ui, build-hud | hard | presentation surfaces |
| save-load | hard | |
| market-interface | soft | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| refundFraction | 0.25 | 0..0.5 | tasks feel costly | tasks become income |
| refundCap | 40 | 0..500 | - | income |
| autoTrackFirstTask | true | bool | - | - |
| ponderPlaybackSpeed | 1x | 0.25..4x | - | - |
| tasksEnabled | true | bool | - | - |

## Visual/Audio Requirements

Task tracker overlay with progress bars per condition; Ponder scene uses the real renderer with a vignette and step captions; completion cue. Highlighting of sidebar buttons or cells for tutorial steps.

## UI Requirements

Task panel (list, details, accept/abandon, rewards preview), tracker overlay, Ponder viewer with timeline scrubber and step list, "known lessons" indicator. UX spec via `/ux-design`.

## Acceptance Criteria

- **GIVEN** a task "utilization(saw) >= 0.8 over 60 s", **WHEN** the saw runs at 0.85 for 60 ticks, **THEN** the task completes and rewards are granted once.
- **GIVEN** the same task, **WHEN** utilization dips to 0.7 at tick 40, **THEN** the window restarts and the task is not complete at tick 60.
- **GIVEN** a task reward definition with a production multiplier, **WHEN** content validates, **THEN** validation fails with `ForbiddenRewardType`.
- **GIVEN** a task consumed cost 200 with fraction 0.25 and cap 40, **WHEN** completed, **THEN** refund is 40 and a second completion is impossible.
- **GIVEN** a stalled machine, **WHEN** the player clicks the Stalled suggestion in inspection, **THEN** the "drivetrain torque" Ponder opens without changing the main save.
- **GIVEN** a tutorial level completed lesson "accessor modes", **WHEN** the main-save task that teaches it becomes Available, **THEN** its hint is collapsed and the Ponder shows as seen.
- **GIVEN** tasks disabled, **WHEN** the player plays, **THEN** all Tier 1 machines remain obtainable via non-task unlock paths.
- **GIVEN** a tutorial level, **WHEN** the player resets it, **THEN** the level returns to its fixed start and no save data changes.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Task graph, individual reward values, license list, unlock links (spec s19) | game-designer | VS |
| 2 | Ponder authoring format (data vs scripted scenes) | tools-programmer | VS |
| 3 | Whether tutorial levels share the main renderer/scene or are separate scenes | technical-director | ADR |
