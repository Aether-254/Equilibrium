# Market Interface

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Factory and Economy Are One (the only place factory output becomes money), Realism First (finite market capacity; no infinite sink)
> **Normative Source**: design/confirmed-mechanics-spec.md s17, s18 (fixed price allowed first), s19; design/market-server-spec.md (remote backend, Deferred); decision 2026-09-20: MVP local market = spread + volume limit
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The market interface is the replaceable boundary between the factory and any market backend. It exposes price queries, buy and sell transactions, a save-level warehouse with reserved capacity, and an export escrow so remote sales are idempotent. Every save binds to one market mode at creation: local NPC market or a remote Market Server. The MVP backend is the local NPC market with fixed base prices, a bid/ask spread, and a per-item volume limit per window; excess volume queues. Later backends (floating NPC simulation, remote server) implement the same interface without changing the factory.

## Player Fantasy

Direct. The player sells planks and sees money arrive, buys logs and fuel, and notices that dumping 500 planks at once does not all clear this minute. The fantasy is being a producer in a market that has limits: timing and storage matter, so the warehouse is a strategic tool rather than a bucket.

## Detailed Design

### Core Rules

**Binding**
1. World creation selects `marketMode`: `LocalNpc` or `RemoteServer(serverId)`. The choice is persisted (save-load) and cannot be changed by the client without a server-issued migration credential.
2. Each Market Server is an independent economic world; assets are not shared across servers by default.

**Interface (all backends)**
3. `GetQuote(itemId) -> {bid, ask, availableSellVolume, availableBuyVolume, asOfServerTime}`.
4. `Sell(itemId, count)`: local unified transaction moves items from the warehouse into export escrow; the backend then settles. For LocalNpc settlement is immediate within the same tick. For RemoteServer settlement is an idempotent request keyed by escrow id; unknown result means the batch stays escrowed and is never re-sold.
5. `Buy(itemId, count)`: requires reserving `count` of acceptable warehouse capacity first; insufficient capacity rejects the order. Bought items enter the save-level warehouse Container.
6. Remote orders keep filling while the player is offline; fill receipts are verified on reconnect and delivered into the reserved warehouse space via a local transaction.
7. Currency for LocalNpc is stored in the save; for RemoteServer it is server-held.
8. Offline (no network): cached quotes viewable; no new remote trades; factory continues.
9. Addon/version differences are isolated by server-confirmed compatibility profiles.
10. Transport: HTTPS for commands/queries/idempotent recovery, WSS for streams; server time governs windows and cooldowns. (Detail in market-server GDD; not implemented in MVP.)

**MVP LocalNpc backend**
11. Each tradeable ItemId has `basePrice`, `spreadFraction`, `maxSellVolumePerWindow`, `maxBuyVolumePerWindow`, `windowTicks`.
12. `bid = basePrice * (1 − spreadFraction/2)`, `ask = basePrice * (1 + spreadFraction/2)`.
13. Sales in the current window up to `maxSellVolumePerWindow` settle immediately at bid. Excess is queued in escrow and settles FIFO in subsequent windows at that window's bid.
14. Buys in the current window up to `maxBuyVolumePerWindow` settle at ask. Excess is rejected (not queued) so the player never has money silently committed.
15. Prices do not move in MVP. Floating prices are a later backend.

**Warehouse**
16. The save-level warehouse is a Container with many slots (knob). It is the drop target for removals, the source for blueprints/auto-blockers, and the buy target.
17. Reserved capacity for open buy orders is excluded from `acceptable` for other insertions until the order fills or cancels.

### States and Transitions (escrow batch)

| State | Transition | Next |
|---|---|---|
| InWarehouse | Sell committed | Escrowed |
| Escrowed | backend settles | Settled (currency credited) |
| Escrowed (remote) | result unknown | Escrowed (retry idempotently) |
| Escrowed (local, over volume) | next window | Settled |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | Sell/Buy are transactions; local settlement is same tick |
| container | both | warehouse Container; reservation |
| item-registry | in | tradeable flags; compatibility by namespace set |
| save-load | both | marketMode binding; escrow; currency (local) |
| economy-ledger | out | settlement events: item, count, price, currency delta, queued volume |
| market-server | down | remote backend implementing this interface (Deferred) |
| offline-progression | both | queued escrow settles during offline recompute |
| placement-editing, blueprint, flow-blocker | in | consume warehouse items |

## Formulas

The quote formula (LocalNpc) is defined as:

`bid = basePrice * (1 − s/2)`, `ask = basePrice * (1 + s/2)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| base price | basePrice | float | 0.01..1e6 | per ItemId, currency units |
| spread fraction | s | float | 0..0.5 | round-trip loss fraction |

**Output Range:** bid < basePrice < ask.
**Example:** plank basePrice 10, s = 0.2: bid 9, ask 11.

The immediate settlement formula is defined as:

`settledNow = min(count, maxSellVolumePerWindow − soldThisWindow)`, `queued = count − settledNow`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| count | count | int | 1..3000 | items sold |
| window cap | maxSellVolumePerWindow | int | 1..1e6 | per item |
| sold so far | soldThisWindow | int | 0..cap | |

**Output Range:** 0..count.
**Example:** cap 200/window, already 150 sold, sell 100: 50 settle now, 50 queue.

The revenue formula is defined as:

`revenue = settled * bid_atSettlement`

**Example:** 50 planks at bid 9 = 450.

The buy cost formula is defined as:

`cost = count * ask`; reject if `count > maxBuyVolumePerWindow − boughtThisWindow` or `acceptable(warehouse, item) < count` or `currency < cost`.

**Example:** 20 logs at ask 5.5 = 110.

## Edge Cases

- **If the player sells an item with no quote**: reject `NotTradeable`.
- **If warehouse is full when a queued sale is cancelled**: items stay escrowed with `CancelBlockedWarehouseFull`; player must free space.
- **If currency would go negative**: buy rejected `InsufficientFunds`; no partial buy.
- **If the window rolls while a sale is mid-transaction**: settlement uses the window in which the transaction commits.
- **If a remote settlement receipt arrives for an escrow id already settled**: ignore (idempotent).
- **If the remote server is unreachable at Sell**: items go to escrow; retry per backoff; never re-sell.
- **If `spreadFraction = 0`**: allowed; arbitrage-free anyway since prices are fixed.
- **If `maxSellVolumePerWindow` is 0 for an item**: sells always queue forever; content validation error.
- **If the player has escrowed items and switches saves**: escrow belongs to the save; nothing crosses.
- **If a migration credential is presented**: validate with the issuing server; on success rebind `marketMode` and transfer per credential terms (market-server GDD).

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation, container, item-registry, save-load | hard | |
| economy-ledger | downstream hard | |
| market-server | downstream (Deferred) | remote backend |
| offline-progression | downstream (Full Vision) | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| plank.basePrice | 10 | 1..100 | profit invisible | fuel/logs irrelevant |
| log.basePrice | 5 | 0.5..50 | inputs free | no margin |
| wood_fuel.basePrice | 2 | 0.1..20 | running cost irrelevant | engines unaffordable |
| spreadFraction | 0.2 | 0.05..0.5 | round trips free | buying finished goods never viable |
| maxSellVolumePerWindow (plank) | 200 | 20..10000 | storage dominates | volume limit invisible |
| maxBuyVolumePerWindow (log) | 500 | 50..10000 | starve lines | - |
| windowTicks | 60 | 10..600 | limit meaningless | long stalls of cash |
| warehouseSlots | 64 | 16..512 | constant WarehouseFull | storage strategy moot |
| startingCurrency | 500 | 100..5000 | cannot afford first engine | capital never binding |

All price values are data (ScriptableObject or JSON), never code.

## Visual/Audio Requirements

Market panel only; a coin cue on settlement. Queued volume shown as a bar.

## UI Requirements

Market screen: per item bid/ask, window cap remaining, queued escrow, currency balance; Sell/Buy with quantity; warehouse view (Container UI). World creation: market mode selector with permanence warning. Offline banner when remote unreachable.

## Acceptance Criteria

- **GIVEN** plank base 10, spread 0.2, **WHEN** quoted, **THEN** bid 9, ask 11.
- **GIVEN** cap 200 and 150 sold this window, **WHEN** selling 100 planks, **THEN** 50 settle at 9 (+450), 50 are escrowed, warehouse plank count drops by 100.
- **GIVEN** the next window, **WHEN** it opens, **THEN** the 50 escrowed settle (+450).
- **GIVEN** warehouse acceptable 10 for logs, **WHEN** buying 20 logs, **THEN** rejected `WarehouseCapacity`, currency unchanged.
- **GIVEN** currency 100 and ask 5.5, **WHEN** buying 20 logs, **THEN** rejected `InsufficientFunds`.
- **GIVEN** a world bound to LocalNpc, **WHEN** the player looks for a server switch, **THEN** none is offered.
- **GIVEN** a save with escrowed items, **WHEN** saved and reloaded, **THEN** escrow persists and settles normally.
- **GIVEN** a Sell transaction, **WHEN** it commits, **THEN** economy-ledger receives one settlement event with item, count, price, and currency delta.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Floating NPC market: aggregate supply/demand vs production-chain simulation (concept open question 4) | economy-designer | Tier 2 |
| 2 | Fees, min lot sizes | economy-designer | Tier 2 |
| 3 | Whether the warehouse is a physical block or purely UI in Tier 1 | game-designer | MVP |
| 4 | Migration credential UX | ux-designer | Full Vision |
