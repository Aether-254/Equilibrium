# ADR-0008: Market Backend Interface

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1 |
| **Domain** | Core / Networking (remote backend deferred) |
| **Knowledge Risk** | LOW for the MVP local backend (pure C#); remote backend (deferred) would use `UnityWebRequest`/`System.Net.Http` and a WebSocket client, to be decided when re-enabled |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/networking.md` |
| **Post-Cutoff APIs Used** | None (MVP) |
| **Verification Required** | None (MVP) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001, ADR-0003, ADR-0005 |
| **Enables** | economy-ledger, offline-progression, market-server (deferred) |
| **Blocks** | market epic |
| **Ordering Note** | Interface frozen now so the remote backend can be added without touching factory code |

## Context

### Problem Statement
The market-interface GDD requires a replaceable backend boundary with a per-save binding, price queries, buy/sell transactions through the tick engine, export escrow with idempotent settlement, warehouse capacity reservation, and offline fill receipts. MVP ships only the local NPC backend (fixed price, spread, volume limit) but the interface must already fit the deferred remote server.

### Constraints
- Sell/Buy are tick transactions (ADR-0001).
- Currency authority differs by backend (local save vs server).
- No network code in MVP.

### Requirements
- `IMarketBackend` with quote, sell (escrow then settle), buy (reserve then settle), receipts, connectivity state.
- Local backend deterministic per window; remote backend async with idempotency keys.
- Binding persisted (ADR-0005) and immutable without a credential.

## Decision

1. `IMarketBackend` in `Equilibrium.Simulation` (pure C#):
```csharp
public interface IMarketBackend {
    MarketMode Mode { get; }
    Quote GetQuote(ItemId item);                                   // bid, ask, sellVolumeLeft, buyVolumeLeft, asOf
    SettlementResult SubmitExport(EscrowId id, ItemId item, int count);  // idempotent by EscrowId
    OrderResult SubmitBuy(OrderId id, ItemId item, int count);     // capacity already reserved by caller
    IEnumerable<FillReceipt> PollReceipts();                       // remote fills; empty for local
    void AcknowledgeReceipt(ReceiptId id);
    ConnectivityState Connectivity { get; }
    void AdvanceWindow(ulong tick);                                // local backend window rolls
}
```
2. `MarketSystem : ITickSystem` owns the save-level warehouse Container, escrow table, reservations, and currency (local mode). It submits transactions: `Sell` moves items warehouse -> escrow; on `SettlementResult.Settled` it credits currency; `Buy` reserves capacity, calls backend, on fill inserts items and debits.
3. `LocalNpcMarketBackend`: per-item `basePrice`, `spreadFraction`, `maxSellVolumePerWindow`, `maxBuyVolumePerWindow`, `windowTicks` from content (ADR-0003 `MarketItemDef`). Immediate settle up to cap; FIFO escrow queue settles on window roll at that window's bid; excess buys rejected.
4. `RemoteMarketBackend` is a stub throwing `NotSupportedException` until re-enabled; its contract is `design/market-server-spec.md` and `Bruno/MarketAPI/openapi.yaml`.
5. `MarketBinding` (mode, server identity fields) is a save header field; only `MigrationCredential` verification (deferred) may change it.
6. Ledger consumes `MarketSettled`, `MarketQueued`, `MarketRejected` events; no back-channel from ledger to market (breaks the potential cycle noted in systems-index).

### Architecture Diagram
```
Factory systems -> MarketSystem(ITickSystem) -> IMarketBackend { LocalNpc | Remote(stub) }
                        |-- warehouse Container, escrow, reservations, currency(local)
                        `-- emits MarketSettled/Queued/Rejected -> EconomyLedger
```

## Alternatives Considered

### Alternative 1: Market as plain service called directly by UI
- **Rejection Reason**: Bypasses tick transactions; escrow atomicity lost.

### Alternative 2: Build the remote client now
- **Rejection Reason**: Spec s15 forbids; interface freeze suffices.

## Consequences

### Positive
- Factory code never knows which backend is bound.
- Deferred server work slots in behind one interface.

### Negative
- Some interface members are dead in MVP (`PollReceipts`, `Connectivity`).

### Risks
- Interface may need changes when the remote spec resolves its TBDs: version the interface; adapter allowed.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| market-interface.md | Rules 1-17; quote/settlement/buy formulas; escrow states | `IMarketBackend`, `MarketSystem`, `LocalNpcMarketBackend` |
| economy-ledger.md | Settlement events; one-way consumption | Events only |
| save-load.md | Rule 7 binding; escrow persistence | `MarketBinding` header; `MarketSystem : ISaveable` |
| market-server.md | Interface-level acceptance criteria (idempotency, reservation) | `EscrowId`/`OrderId` idempotency; reserve-before-submit |
| offline-progression.md | Rule 4 window settlement offline | `AdvanceWindow(tick)` callable by recompute |

## Performance Implications
- **CPU**: Negligible (per-window settlement).
- **Memory**: Escrow table.
- **Load Time**: None.
- **Network**: None (MVP).

## Migration Plan
New code; no prototype market exists.

## Validation Criteria
- market-interface GDD acceptance criteria as EditMode tests against `LocalNpcMarketBackend`.
- Contract tests for `IMarketBackend` runnable against a fake remote backend.

## Related Decisions
- ADR-0001, ADR-0003, ADR-0005
- design/market-server-spec.md
