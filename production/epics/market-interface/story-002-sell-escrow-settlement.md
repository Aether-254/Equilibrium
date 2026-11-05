# Story 002: Sell escrow, immediate settlement, and FIFO window queue

> **Epic**: Market Interface
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/market-interface.md`
**Requirement**: `TR-mkt-002`, `TR-mkt-004`

**Governing ADRs**: ADR-0008 (primary), ADR-0001, ADR-0005
**ADR Decision Summary**: Sell moves warehouse -> escrow as a tick transaction; backend settles idempotently by `EscrowId`; local excess queues FIFO and settles at the next window's bid.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Market)**:
- Required: escrow first; idempotent settlement; emit `MarketSettled/Queued`.

## Acceptance Criteria

- [ ] Cap 200, 150 sold this window, sell 100 planks -> 50 settle at 9 (+450), 50 escrowed; warehouse planks -100 (GDD AC 2).
- [ ] Next window opens -> the 50 escrowed settle (+450) (GDD AC 3).
- [ ] Settlement for an already-settled `EscrowId` is ignored (idempotent).
- [ ] Escrow persists across save/reload and settles normally (GDD AC 7).
- [ ] Cancelling a queued sale returns items only if warehouse space exists, else `CancelBlockedWarehouseFull`.
- [ ] `maxSellVolumePerWindow = 0` fails content validation.

## Implementation Notes

Escrow table: `EscrowId -> (itemId, count, createdTick, state)`; `AdvanceWindow(tick)` settles FIFO up to the new window's cap. `MarketSystem` implements `ISaveable` section `market`.

## Out of Scope

- Buy path (Story 003)

## QA Test Cases

- GDD AC 2, 3, 7 verbatim
- Idempotency: settle twice -> currency +450 once
- Cancel blocked when warehouse full

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Market/SellEscrowTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; save-load 002
- Unlocks: Story 003; economy-ledger 001
