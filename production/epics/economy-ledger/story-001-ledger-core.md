# Story 001: Ledger event consumer, windows, and profit/unit-cost formulas

> **Epic**: Economy Ledger
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/economy-ledger.md`
**Requirement**: `TR-ledg-001`, `TR-ledg-002`, `TR-ledg-003`

**Governing ADRs**: ADR-0001 (primary), ADR-0008, ADR-0005
**ADR Decision Summary**: pure consumer of `CommittedEvent`s; never calls the market; persisted aggregates.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Forbidden: ledger calling `IMarketBackend`/`MarketSystem`.

## Acceptance Criteria

- [ ] Windows 60 s / 5 min / session / all-time; events bucketed by commit tick.
- [ ] 100 planks at bid 9, 50 logs at ask 5.5, fuel 40 -> profit 585 (GDD AC 1); unit cost of plank 3.15 (GDD AC 2).
- [ ] Estimated cost source for self-produced inputs: last ask, else basePrice, flagged estimated.
- [ ] Product never sold -> revenue 0, unit cost computed, margin "unsold" (GDD AC 6).
- [ ] Session and all-time windows persist via `ISaveable` and match after reload (GDD AC 5).
- [ ] Static test: `EconomyLedger` has no reference to `IMarketBackend` or `MarketSystem`.
- [ ] 500 machines emitting per tick under 0.5 ms (GDD AC 7).

## Implementation Notes

Ring buffers per window per metric; per-product and per-machine dictionaries keyed by handle/index. Event log retention `eventLogRetentionTicks` for rebuild.

## Out of Scope

- Story 002: per-machine attribution and losses

## QA Test Cases

- GDD AC 1, 2, 5, 6, 7
- Reflection: referenced types exclude market implementation types

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Ledger/LedgerCoreTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: tick-simulation 005; market-interface 003
- Unlocks: Story 002; build-hud economy strip
