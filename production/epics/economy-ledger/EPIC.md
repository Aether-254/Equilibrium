# Epic: Economy Ledger

> **Layer**: Feature
> **GDD**: design/gdd/economy-ledger.md
> **Architecture Module**: EconomyLedger (Equilibrium.Simulation); economy strip and Economy tab presenters (Equilibrium.UI)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories economy-ledger`

## Overview

Implement the pure event consumer that turns committed events into profit, unit cost, energy-share fuel apportionment, per-machine state/ReasonCode tick counts, loss attribution, and rolling windows (60 s, 5 min, session, all-time) persisted via `ISaveable`. Provides the data for the inspection Economy tab, the Overview bottleneck, the HUD economy strip, and unlock/task metric predicates.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | CommittedEvent stream | LOW |
| ADR-0008: Market Backend Interface | Settlement events; one-way consumption | LOW |
| ADR-0005: Save Format | Persisted aggregates | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ledg-001 | Event consumer; windows; persisted | ADR-0001, ADR-0005 |
| TR-ledg-002 | Profit, unit cost, apportionment, loss attribution formulas | ADR-0001 |
| TR-ledg-003 | Never calls the market | ADR-0008 |

## Definition of Done

- economy-ledger acceptance criteria as EditMode tests; 500-machine benchmark under 0.5 ms.
- Static test: no reference from EconomyLedger to IMarketBackend/MarketSystem.

## Next Step

Run `/create-stories economy-ledger`.
