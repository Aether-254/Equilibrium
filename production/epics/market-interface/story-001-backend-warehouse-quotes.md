# Story 001: IMarketBackend interface, warehouse, and LocalNpc quotes

> **Epic**: Market Interface
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/market-interface.md`
**Requirement**: `TR-mkt-001`, `TR-mkt-004`

**Governing ADRs**: ADR-0008 (primary), ADR-0003, ADR-0004
**ADR Decision Summary**: `IMarketBackend` with quote/export/buy/receipts/connectivity/window; `LocalNpcMarketBackend` from `MarketItemDef`; save-level warehouse container owned by `MarketSystem`.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Market)**:
- Required: prices/caps from content; `RemoteMarketBackend` stub only.

## Acceptance Criteria

- [ ] `IMarketBackend` exactly as ADR-0008; `RemoteMarketBackend` throws `NotSupportedException`.
- [ ] Warehouse container (`warehouseSlots` from content, default 64) owned by `MarketSystem`; `IWarehouse` from grid-world 003 implemented by it.
- [ ] Quote: plank base 10, spread 0.2 -> bid 9, ask 11 (GDD AC 1); `sellVolumeLeft`/`buyVolumeLeft` reported per window.
- [ ] `marketMode` selection API at world creation; binding stored in save header (save-load 002 field).
- [ ] Not-tradeable item -> `NotTradeable`.

## Implementation Notes

`MarketItemDef{itemId, basePrice, spreadFraction, maxSellVolumePerWindow, maxBuyVolumePerWindow, windowTicks}`; window index = `tick / windowTicks`. `MarketSystem : ITickSystem, ISaveable` holds currency for LocalNpc.

## Out of Scope

- Story 002: sell escrow and settlement
- Story 003: buy reservation and events

## QA Test Cases

- GDD AC 1
- Volume left decrements after sells (tested fully in 002)
- Stub throws

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Market/BackendAndQuoteTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: container 003; item-registry 004
- Unlocks: Story 002
