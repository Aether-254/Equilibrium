# Machine Inspection UI

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Depth Is Progress (phenomena, then instruments, then numbers), Simple Entry / Unfathomable Depth (Overview first; Mechanics and Economy when you want them)
> **Normative Source**: design/confirmed-mechanics-spec.md s13, s9.2, s7.8 (blocker status display)
> **Note**: This GDD fixes behavior and content. Layout, interaction, and visuals are authored in `design/ux/machine-inspection.md` via `/ux-design` before implementation.
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The machine inspection panel opens when the player clicks a machine (or engine, chest, belt cell, accessor) and is the primary instrument for understanding why a line performs as it does. It has a persistent top status bar (state + highest-priority ReasonCode) and four tabs: Overview (default), Logistics, Mechanics, Economy. Every number shown is sourced from a system GDD (machine-processing, mechanical-power, container, economy-ledger); this UI adds no simulation logic. Its job is to make the biggest bottleneck obvious within two seconds and to reward deeper reading with the exact quantities an engineer would want.

## Player Fantasy

Direct. Clicking a machine feels like opening an instrument panel: the top bar says what is wrong, Overview says what to do about it, and the Mechanics and Economy tabs let the player prove their intuition with numbers. The fantasy is competence: "I can read this factory."

## Detailed Design

### Core Rules

1. Opening: click on a block in Browse mode (not in a placement mode). One panel at a time; clicking another block retargets it.
2. Top status bar (always visible across tabs): block name, top-level state (Idle / Working / Blocked / Starved for machines; Running / Stalled / Idle / Faulted for engines; state for belt cells), and the highest-priority ReasonCode. Priority order for ReasonCodes: `SourceConflict` > `Stalled` > `NoFuel` > `OutputFull` > `NoReceivingPort` > `MissingInput` > `PortBindingMissing` > others.
3. **Overview tab** (default): current state and ReasonCode, current recipe (with pin control), processing progress (0..100%), and the single biggest bottleneck: the largest `loss(m, reason)` from economy-ledger in the 60 s window, phrased as cause + suggestion text keyed by ReasonCode.
4. **Logistics tab**: every Container of the block with its slots (item, count, effective capacity, filter badge, reservation marker), blocked ticks and starved ticks in the window, and for belts/accessors the mode/filter/amount. Blocker status for all four sides (spec s7.8).
5. **Mechanics tab** (machines and engines): omega (RPM, optional rad/s), tau_req, tau_avail on the segment, network load % (`tau_demand_src / tau_max`), eta_path, theoretical rate, actual rate, utilization. Engines additionally: fuel slot, fuel/tick, P_deliv.
6. **Economy tab**: unit cost of the current product, revenue, profit contribution, losses by reason, and historical trend (window TBD).
7. Switching tabs never hides the status bar. Panel state (last tab) persists per session.
8. Values refresh every tick; UI reads from the last committed snapshot, never mid-tick.
9. The panel exposes actions that are transactions: pin recipe, cancel work (with irreversible warning), set accessor mode/filter/amount, toggle blockers (VS).
10. Non-machine blocks show only the tabs that apply (chest: Overview + Logistics; belt cell: Overview + Logistics; engine: Overview + Logistics + Mechanics + Economy).

### States and Transitions

| Panel state | Transition | Next |
|---|---|---|
| Closed | click block | Open(block, lastTab) |
| Open | click other block | Open(other, lastTab) |
| Open | Esc / close / enter placement mode | Closed |
| Open | block removed | Closed with a brief notice |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| machine-processing | in | state, ReasonCodes, recipe, progress, rates, utilization; actions: pin, cancel |
| mechanical-power | in | omega, tau_req, tau_avail, load %, eta_path, fuel data |
| container | in | slot contents, capacities, filters, reservations |
| item-io-accessor | both | mode/filter/amount display and edit |
| flow-blocker | both | four-side status; toggle (VS) |
| economy-ledger | in | unit cost, revenue, profit, losses by reason, trend |
| tick-simulation | in | snapshot timing |
| build-hud | sibling | shares the HUD layer; inspection closes when a placement mode is entered |
| tutorial-task | out | Ponder-style links from ReasonCode text (VS) |

## Formulas

The bottleneck selection formula is defined as:

`bottleneck(m) = argmax_{reason} loss(m, reason)` over the 60 s window; if all losses are 0, show "No bottleneck in the last minute".

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| loss | loss(m, reason) | float | >= 0 | from economy-ledger |

**Output Range:** one ReasonCode or none.
**Example:** losses {Stalled: 32.4, OutputFull: 5.1}: bottleneck `Stalled`, text "Drivetrain cannot supply required torque. Add gearing or a second engine."

The load percentage formula is defined as:

`load% = 100 * tau_demand_src / tau_max` (from mechanical-power), clamped for display to 0..999.

**Example:** 31.6 / 40 = 79%.

## Edge Cases

- **If a machine has multiple ReasonCodes**: status bar shows the highest by the priority list; Overview lists all.
- **If the block is a ghost**: panel shows required materials and build state only.
- **If the block is part of a Type II multiblock**: panel targets the Controller; footprint is highlighted.
- **If the block is a Type I component**: panel shows its own data plus a link to the structure Controller.
- **If economy-ledger has no data yet** (first minute): Economy tab shows "collecting" placeholders; Overview bottleneck falls back to the current ReasonCode.
- **If the panel is open when the game is paused**: values freeze at the last snapshot; actions still queue as transactions for the next tick.
- **If the window is narrow** (layout TBD): tabs collapse to icons; status bar remains.
- **If a value is undefined for the block type** (e.g. omega for a chest): the field is hidden, not shown as 0.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| machine-processing, mechanical-power, container, economy-ledger | hard | data |
| item-io-accessor, flow-blocker | soft | edit actions |
| tick-simulation | hard | snapshot |
| build-hud | sibling | HUD coordination |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| bottleneckWindowTicks | 60 | 10..600 | noisy | stale |
| refreshEveryNTicks | 1 | 1..5 | - | laggy readouts |
| reasonPriorityOrder | as rule 2 | list | - | - |
| showRadPerSec | false | bool | - | clutter |
| suggestionTextByReason | data table | - | - | - |

## Visual/Audio Requirements

Panel visual style per art bible (not yet authored). Status bar color by state: Working green, Starved amber, Blocked orange, Stalled/Faulted red, Idle grey. Progress ring. No audio beyond open/close cues.

## UI Requirements

This GDD is itself the requirements source. Author `design/ux/machine-inspection.md` with wireframes for the four tabs, the status bar, narrow layout, and the suggestion text table. UGUI vs UI Toolkit is an ADR decision.

## Acceptance Criteria

- **GIVEN** a stalled machine, **WHEN** the panel opens, **THEN** the status bar reads "Working — Stalled" in red and stays visible on every tab.
- **GIVEN** losses {Stalled 32.4, OutputFull 5.1}, **WHEN** Overview is shown, **THEN** the bottleneck line names Stalled with its suggestion text.
- **GIVEN** a machine with two input Containers, **WHEN** Logistics is shown, **THEN** both Containers and all slots with counts and effective capacities are listed.
- **GIVEN** an engine, **WHEN** Mechanics is shown, **THEN** fuel/tick, P_deliv, omega, and network load % are present.
- **GIVEN** a chest, **WHEN** opened, **THEN** only Overview and Logistics tabs exist.
- **GIVEN** the panel open on tab Mechanics, **WHEN** the player clicks another machine, **THEN** the panel retargets and stays on Mechanics.
- **GIVEN** the player enters Belt placement mode, **WHEN** the mode activates, **THEN** the panel closes.
- **GIVEN** a machine removed while inspected, **WHEN** the removal commits, **THEN** the panel closes with a notice.
- **GIVEN** an accessor, **WHEN** the player changes amount to 20, **THEN** the change applies as a transaction on the next tick and the panel reflects it.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Narrow layout and trend window (spec s13 TBD) | ux-designer | VS ux-spec |
| 2 | UGUI vs UI Toolkit | technical-director | ADR |
| 3 | Suggestion text authoring and localization | writer, localization-lead | VS |
| 4 | Ponder links from ReasonCodes | tutorial-task GDD | VS |
