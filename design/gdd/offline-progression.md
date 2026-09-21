# Offline Progression

> **Status**: Approved (creator self-review 2026-09-20; Full Vision, implementation deferred to Tier 4)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: anti-pillar Not Undesigned Idle (offline income is design quality, not waiting), Realism First (offline obeys the same conservation and capacity constraints as online)
> **Normative Source**: game-concept Economy Baseline "Offline Income" and its architecture constraints; confirmed-mechanics-spec s19 (offline simulation open); market-server-spec s7.3, s8 (offline fills)
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

When a save is loaded after wall-clock time has passed, the factory is advanced as if it had run, up to a capped duration, using the same rules the online simulation uses: fuel is consumed, inputs deplete, storage fills, market volume limits apply, stalls and starvation happen. Offline and online yield the same result for the same conditions; nothing is a fixed per-hour bonus. Because full tick-by-tick replay of hours is expensive, the system may use event-driven stepping, piecewise steady-state solves, or a hybrid, but any approximation must respect every conservation and capacity constraint the online systems enforce and must never produce outcomes the online simulation could not. Time beyond the cap is discarded or discounted.

## Player Fantasy

Direct at load time. Coming back to find the warehouse full, fuel gone, and a report that says "line 2 starved after 40 minutes because the log chest ran dry" is the payoff for designing a self-sufficient factory, and a lesson when it was not. The fantasy is that the factory is real even when you are not watching.

## Detailed Design

### Core Rules

1. On load, compute `offlineSeconds = min(now − lastSaveWallTime, offlineCapSeconds)`; seconds beyond the cap are discarded or discounted by `overCapFactor` (knob; default discard).
2. Advance the simulation by `offlineSeconds` ticks worth of state using an approved method (rule 5). The result MUST satisfy: no item created or destroyed except by recipes and market settlements; no Container exceeds effective capacity; no machine runs without power or inputs; no belt moves faster than one hop per tick; no sale exceeds market volume limits per window.
3. Tie-break entropy during offline recompute is fresh (tick-simulation rule 5); offline outcomes are therefore not bit-identical to what would have happened, but statistically equivalent.
4. Remote market fills that occurred offline are applied via receipts (market-interface rule 6); local NPC queued escrow settles per window as time advances.
5. Approved methods (implementation chooses per network, can mix):
   - Full replay: run ticks (exact; used when `offlineSeconds` is small or the factory is small).
   - Event-driven: compute the next state change time for each subsystem (fuel out, chest full, batch complete) and jump between events.
   - Piecewise steady state: for stable intervals, compute rates analytically and integrate; break at events.
   Any method must produce the same event ordering constraints as ticks (a chest cannot be filled past capacity before its producer stalls).
6. Produce an offline report: duration applied, per-line production, fuel consumed, sales settled, first stall/starve/full event per machine with timestamp, currency delta.
7. Offline never triggers unlocks or task completions that require player presence; metric-based unlocks may latch (tech-unlock rule) since the factory really achieved them.
8. Offline computation runs before the first online tick and must not exceed a wall-clock budget; if it would, fall back to a coarser approved method, never to a fixed bonus.

### States and Transitions

| State | Transition | Next |
|---|---|---|
| Loading | offlineSeconds = 0 | Playing |
| Loading | offlineSeconds > 0 | Advancing (method chosen) → Report → Playing |
| Advancing | budget exceeded | Advancing with coarser method |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | both | replay or event stepping using the same engine; fresh entropy |
| machine-processing, mechanical-power, conveyor, container | in | rates, capacities, and event predictors (`ticksUntilNextEvent`) |
| market-interface | both | window volume limits; queued escrow settlement; remote receipts |
| economy-ledger | out | offline events feed the same ledger windows |
| save-load | in | lastSaveWallTime |
| tech-unlock, tutorial-task | out | metric latching allowed; presence-required conditions excluded |
| machine-inspection-ui / build-hud | out | offline report UI |

## Formulas

The applied offline time formula is defined as:

`offlineSeconds = min(Δt_wall, cap)`, `discounted = max(0, Δt_wall − cap) * overCapFactor`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| wall elapsed | Δt_wall | float | >= 0 | seconds since last save |
| cap | cap | int | 3600..86400 | knob |
| over-cap factor | overCapFactor | float | 0..0.5 | fraction of over-cap time still applied (0 = discard) |

**Output Range:** 0..cap (+ discounted part).
**Example:** 10 h away, cap 8 h, factor 0: apply 8 h.

The steady-state interval production formula is defined as:

`produced(m, interval) = min(rate_actual(m) * interval, inputsAvailable(m) / inputsPerBatch, outputSpace(m) / outputsPerBatch, fuelAvailable / fuelPerBatch) * outputsPerBatch`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| actual rate | rate_actual(m) | float | batches/s | machine-processing at current power |
| interval | interval | float | s | until next event |
| constraints | inputs/outputs/fuel | int | >= 0 | binding capacities |

**Output Range:** never exceeds any constraint; the binding constraint defines the next event.
**Example:** rate 0.1/s over 600 s = 60 batches; output space allows 40: produce 40 and emit "OutputFull at t = 400 s".

The equivalence requirement is defined as:

`∀ constraint c ∈ online constraints: offlineResult satisfies c`; tested by comparing event-driven results against full replay on reference factories within tolerance `ε = 1 batch per machine`.

## Edge Cases

- **If the system clock moved backwards**: `Δt_wall < 0` treated as 0; log.
- **If the clock jumped forward implausibly** (> 30 days): apply the cap; flag possible clock manipulation in the report; no punishment.
- **If a market window limit is hit offline**: sales queue exactly as online; the report shows queued volume.
- **If a remote receipt conflicts with local state** (warehouse reservation gone): receipt stays pending; market-interface rules apply.
- **If a machine's recipe changed by content update between save and load**: offline uses the recipe as of load; report notes it.
- **If offline computation hits budget with full replay**: switch to event-driven; if still over, steady-state with coarse intervals; never skip constraints.
- **If the factory is in a fault state at save (SourceConflict)**: it stays faulted offline; zero production for that network.
- **If a Type I structure would invalidate offline**: impossible (no player actions offline); structures do not change.
- **If offline time is zero but the save is from an older schema**: no offline step; migration only.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation, machine-processing, mechanical-power, conveyor, container, market-interface, save-load | hard | must expose event predictors |
| economy-ledger | hard | report |
| market-server | soft | remote receipts |
| tech-unlock, tutorial-task | soft | latching policy |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| offlineCapSeconds | 28800 (8 h) | 3600..86400 | idle pillar violated? no; but factories feel frozen | idle income dominates |
| overCapFactor | 0 | 0..0.5 | - | idle rewarded |
| computeBudgetMs | 2000 | 200..10000 | coarse results | long loads |
| fullReplayMaxTicks | 3600 | 600..36000 | approximations early | slow loads |
| equivalenceTolerance ε | 1 batch/machine | - | tests brittle | drift hidden |

## Visual/Audio Requirements

Offline report panel; optional fast-forward visualization of key events (Tier 4).

## UI Requirements

Offline report on load: applied time, discarded time, production, sales, fuel, first-event list per machine with timestamps and ReasonCodes, currency delta. Link each event to its machine.

## Acceptance Criteria

- **GIVEN** a reference factory and 1 h offline, **WHEN** computed by full replay and by event-driven, **THEN** results agree within ε and both satisfy all constraints.
- **GIVEN** a chest that fills at t = 400 s, **WHEN** 600 s offline applies, **THEN** the producer shows OutputFull from 400 s and produced exactly the space available.
- **GIVEN** fuel for 30 min and 2 h offline, **WHEN** applied, **THEN** the engine reports NoFuel from 30 min and downstream machines produce nothing after.
- **GIVEN** 10 h away with cap 8 h and factor 0, **WHEN** loaded, **THEN** 8 h applied and 2 h reported discarded.
- **GIVEN** market volume 200/window, **WHEN** offline production exceeds it, **THEN** excess is escrowed, not sold.
- **GIVEN** a negative clock delta, **WHEN** loaded, **THEN** no offline time applies and a log entry exists.
- **GIVEN** a factory of 500 machines and 8 h offline, **WHEN** loaded, **THEN** offline computation completes within `computeBudgetMs`.
- **GIVEN** a presence-required task active, **WHEN** offline completes its metric, **THEN** the task is not completed; a metric-only unlock does latch.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Event predictor interface for each subsystem | technical-director (ADR) | Tier 4 |
| 2 | Floating market behavior over long offline windows (extreme outcomes risk) | economy-designer | Tier 4 |
| 3 | Cap and discount values after playtest | game-designer | Tier 4 |
| 4 | Whether Tier 1 ships a trivial "no offline" mode or a small cap | game-designer | MVP tuning |
