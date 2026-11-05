# Story 003: Buy with full capacity reservation, currency guards, and ledger events

> **Epic**: Market Interface
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/market-interface.md`
**Requirement**: `TR-mkt-003`, `TR-mkt-004`

**Governing ADRs**: ADR-0008 (primary), ADR-0004
**ADR Decision Summary**: reserve full warehouse capacity before submitting; reject on capacity or funds; emit events consumed one-way by the ledger.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

## Acceptance Criteria

- [ ] Warehouse acceptable 10 for logs, buy 20 -> rejected `WarehouseCapacity`, currency unchanged (GDD AC 4).
- [ ] Currency 100, ask 5.5, buy 20 -> rejected `InsufficientFunds` (GDD AC 5).
- [ ] Successful buy within `maxBuyVolumePerWindow`: items inserted into reserved warehouse space same tick, currency debited `count * ask`; excess over cap rejected (not queued).
- [ ] Reserved capacity excluded from other insertions until fill or cancel.
- [ ] Every Sell/Buy outcome emits exactly one `MarketSettled`, `MarketQueued`, or `MarketRejected` event with item, count, price, currency delta (GDD AC 8).

## Implementation Notes

Reservation is a container-level flag on the warehouse slots chosen by the insertion plan (container 002 plan API). Local buys settle in the same tick so reservations are short-lived; the API still reserves first for remote parity.

## Out of Scope

- Market screen UI (Story 004)

## QA Test Cases

- GDD AC 4, 5, 8
- Cap: buy cap 500, buy 600 -> rejected

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Market/BuyAndEventsTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002
- Unlocks: economy-ledger 001; Story 004
