# Epic: Market Interface

> **Layer**: Feature
> **GDD**: design/gdd/market-interface.md
> **Architecture Module**: MarketSystem, IMarketBackend, LocalNpcMarketBackend, RemoteMarketBackend stub (Equilibrium.Simulation); market screen (Equilibrium.UI)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: LOW (simulation); HIGH for the UI Toolkit market screen
> **Manifest Version**: 2026-09-20.1
> **Stories**: 4 (table below)

## Overview

Implement the replaceable market boundary and the MVP local backend: save-level warehouse container, export escrow with idempotent settlement, full-capacity buy reservation, fixed base prices with bid/ask spread, per-window sell cap with FIFO escrow queue, buy cap rejection, local currency, market-mode binding at world creation, `MarketSettled/Queued/Rejected` events, the market screen, and the world-creation mode selector. The remote backend remains a stub per spec s15.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0008: Market Backend Interface | IMarketBackend; MarketSystem; local backend rules | LOW |
| ADR-0005: Save Format | Binding header; escrow persistence | MEDIUM |
| ADR-0003: Item Identity | MarketItemDef content | MEDIUM |
| ADR-0006: UI Technology Stack | Market screen | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-mkt-001 | Replaceable backend; per-save binding | ADR-0008, ADR-0005 |
| TR-mkt-002 | Escrow then idempotent settlement; never re-sell | ADR-0008, ADR-0001 |
| TR-mkt-003 | Full capacity reservation before buy | ADR-0008, ADR-0004 |
| TR-mkt-004 | MVP local backend pricing and caps | ADR-0008, ADR-0003 |

## Definition of Done

- market-interface acceptance criteria as EditMode tests; contract tests against a fake remote backend.
- Market screen and world-creation selector implemented per UX (spec to be authored).

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | IMarketBackend interface, warehouse, and LocalNpc quotes | Logic | Ready |
| 002 | Sell escrow, immediate settlement, and FIFO window queue | Logic | Ready |
| 003 | Buy with full capacity reservation, currency guards, and ledger events | Logic | Ready |
| 004 | Market screen and world-creation market mode selector | UI | Ready |

## Next Step

Run `/story-readiness production/epics/market-interface/story-001-backend-warehouse-quotes.md` then `/dev-story`.
