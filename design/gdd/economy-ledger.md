# Economy Ledger

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Factory and Economy Are One (every engineering quantity reaches profit), Depth Is Progress (loss attribution teaches the player where the next understanding is)
> **Normative Source**: game-concept MVP "visible quantities"; confirmed-mechanics-spec s13 (economy tab data); inferred system
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The economy ledger consumes committed transaction events and turns them into the numbers the player optimizes against: revenue, material cost, running cost (fuel), profit, unit cost per product, and losses attributed to causes (stall, starvation, output full, logistics, queued market volume). It keeps rolling windows and per-machine attributions so the inspection UI can show "this saw contributed 42 profit in the last minute and lost 18% of its time to Stalled". It never mutates simulation state; it is a pure observer with persistence.

## Player Fantasy

Direct. The player opens the Economy tab and sees the factory as a business: which line pays, which machine bleeds, what a stall costs per minute. The fantasy is insight: profit is not a score that goes up, it is a diagnosis that points at the next optimization.

## Detailed Design

### Core Rules

1. The ledger subscribes to committed events: market settlements, machine completions, engine fuel consumption, machine state ticks (with ReasonCode), conveyor stall ticks, accessor blocked ticks, market escrow queueing.
2. Currency ledger: `balance` (LocalNpc) mirrors market-interface; every delta has a source event.
3. Cost attribution per product ItemId: material cost is the market ask paid for consumed inputs (or last known ask if self-produced inputs), fuel cost is fuel consumed by engines apportioned to machines by delivered energy share.
4. Per-machine records over a rolling window: ticks in each state and ReasonCode, completions, energy received, inputs consumed, outputs produced, attributed fuel cost, attributed revenue (when outputs are sold), profit contribution.
5. Loss attribution: for each unproductive tick of a Working-capable machine, the loss equals its theoretical output value per tick at current bid, tagged with the ReasonCode; conveyor stalls are attributed to the machine whose output they block, or to "logistics" if none.
6. Windows: 60 s, 5 min, session, all-time. Session and all-time persist via save-load.
7. All values are derived; the ledger can be rebuilt from the event log for a window. It never blocks a tick.

### States and Transitions

None; the ledger is a stream consumer with persisted aggregates.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | committed event stream |
| market-interface | in | settlements, queued volume, quotes |
| machine-processing | in | completions, state ticks, ReasonCodes |
| mechanical-power | in | fuel consumed, power delivered per machine |
| conveyor, item-io-accessor | in | stall/blocked ticks with cause |
| save-load | both | persisted aggregates |
| machine-inspection-ui | out | Economy tab and Overview bottleneck |
| tech-unlock | out | economic milestones as unlock conditions |
| industrial-duel | out | public metrics (Deferred) |

## Formulas

The profit formula is defined as:

`profit_w = revenue_w − materialCost_w − fuelCost_w − otherCost_w`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| window | w | enum | 60s, 5m, session, all | |
| revenue | revenue_w | float | >= 0 | settled sales at bid |
| material cost | materialCost_w | float | >= 0 | ask paid for inputs consumed in w |
| fuel cost | fuelCost_w | float | >= 0 | ask of fuel consumed in w |
| other | otherCost_w | float | >= 0 | build materials consumed (blueprints, blockers), placeholder for Tier 2 maintenance |

**Output Range:** any real.
**Example:** revenue 900, logs 250, fuel 40: profit 610.

The unit cost formula is defined as:

`unitCost(product) = (materialCost_attr + fuelCost_attr) / unitsProduced`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| attributed material | materialCost_attr | float | >= 0 | inputs consumed by recipes producing this product |
| attributed fuel | fuelCost_attr | float | >= 0 | fuel apportioned by energy share |
| units produced | unitsProduced | int | >= 1 | in window |

**Output Range:** >= 0.
**Example:** 100 planks from 50 logs at 5.5 (275) plus fuel 30: unitCost 3.05; bid 9 gives margin 5.95 per plank.

The fuel apportionment formula is defined as:

`fuelCost_attr(m) = fuelCost_engine * E_recv(m) / Σ_k E_recv(k)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| engine fuel cost | fuelCost_engine | float | >= 0 | fuel consumed by the engine driving m's network, at ask |
| energy received | E_recv(m) | float | >= 0 | Σ P_recv over window |

**Output Range:** shares sum to engine fuel cost.
**Example:** engine burned 40 of fuel; machine A received 300 J, B 100 J: A 30, B 10.

The loss attribution formula is defined as:

`loss(m, reason) = ticks(m, reason) * rate_theoretical(m) * outputValuePerBatch(m)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| unproductive ticks | ticks(m, reason) | int | >= 0 | ticks in Working-without-power, Blocked, Starved by reason |
| theoretical rate | rate_theoretical | float | batches/s | from machine-processing at nominal power |
| batch value | outputValuePerBatch | float | >= 0 | Σ outputs * bid |

**Output Range:** >= 0.
**Example:** 18 stalled ticks, 0.1 batch/s, batch value 18: loss 32.4 attributed to `Stalled`.

## Edge Cases

- **If an input was self-produced and never bought**: use last known ask for that ItemId; if none, use basePrice; flag as estimated.
- **If a product is never sold**: revenue 0; unit cost still computed; margin shown as "unsold".
- **If a machine has no output value (e.g. intermediate with no market)**: loss shown in ticks only, value "n/a".
- **If two engines drive one network** (Tier 2): apportion by each engine's delivered share.
- **If a window rolls during a tick**: events belong to the tick's window at commit time.
- **If the event log is truncated in a save**: rebuild windows from what exists; mark as partial.
- **If currency goes negative through a migration or bug**: display and flag; the ledger never corrects the market.
- **If a conveyor stall has no downstream machine**: attribute to "logistics: open end".

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation, market-interface, machine-processing, mechanical-power | hard | |
| conveyor, item-io-accessor | soft | attribution detail |
| save-load | hard | persistence |
| machine-inspection-ui, tech-unlock, industrial-duel | downstream | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| windows | 60s, 5m, session, all | list | - | memory |
| eventLogRetentionTicks | 3600 | 300..86400 | cannot rebuild | memory |
| attributionMode | energy-share | enum | - | - |
| estimatedCostSource | lastAsk → basePrice | enum | - | - |

## Visual/Audio Requirements

None beyond UI.

## UI Requirements

Economy tab per machine (revenue, cost, profit, unit cost, losses by reason). Global economy panel: balance, profit per window, top losses by reason, per-product unit cost vs bid. Overview tab's "biggest bottleneck" is the largest `loss(m, reason)` for that machine in the 60 s window.

## Acceptance Criteria

- **GIVEN** 100 planks sold at bid 9 and 50 logs bought at ask 5.5 and fuel worth 40 consumed in one window, **WHEN** profit is read, **THEN** 900 − 275 − 40 = 585.
- **GIVEN** the same window, **WHEN** unit cost of plank is read, **THEN** 3.15.
- **GIVEN** engine fuel cost 40 and machines receiving 300 J and 100 J, **WHEN** attributed, **THEN** 30 and 10.
- **GIVEN** a machine stalled 18 of 60 ticks with theoretical 0.1 batch/s and batch value 18, **WHEN** losses are read, **THEN** `Stalled: 32.4`.
- **GIVEN** a save and reload, **WHEN** session and all-time windows are read, **THEN** they match pre-save values.
- **GIVEN** a machine whose product has no quote, **WHEN** the Economy tab is opened, **THEN** unit cost is shown and revenue reads "unsold".
- **GIVEN** 500 machines emitting events per tick, **WHEN** measured, **THEN** ledger processing costs under 0.5 ms per tick.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Whether intermediates get shadow prices from downstream sales | economy-designer | Tier 2 |
| 2 | Capital cost of machines (depreciation) in profit | economy-designer | Tier 2 |
| 3 | Historical trend window for the Economy tab (spec s13 TBD) | ux-designer | VS |
