# Market Server (Remote Market Backend)

> **Status**: Deferred
> **Scope**: Full Vision. `design/market-server-spec.md` is the authoritative, creator-confirmed specification and remains the source of truth. This GDD is a template-compliant index into it so pipeline skills can trace dependencies; it does not restate or extend the spec, and every TBD in the spec remains TBD here.
> **Author**: creator (spec) + design session (index)
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Factory and Economy Are One (a shared, persistent economy), Realism First (idempotent, conserved transfers; no client-trusted prices)
> **Implementation boundary (from spec s1, s15)**: no server, account system, OAuth, networking package, market UI, or Unity network code is to be created until the creator re-enables this work.

## Overview

The Market Server is the remote implementation of the market-interface backend: a persistent, independent economic world serving many players with a hybrid order book (player orders plus server-simulated liquidity), instant and limit trades, export escrow with idempotent submission, offline fills delivered by signed receipts, server-held currency, compatibility realms for Addon/version differences, HTTPS commands and WSS streams, header-based auth, trust levels, and server-issued migration credentials as the sole way to rebind a save. See `design/market-server-spec.md` sections 2-16 and `Bruno/MarketAPI/openapi.yaml` for the confirmed semantics and API entry.

## Player Fantasy

Direct. Selling into a market other real factories also feed, watching your plank price fall because someone else built a sawmill, and reading a contract board that reacts to collective output. The fantasy is being one producer among many in a living economy.

## Detailed Design

### Core Rules

The normative rules are `design/market-server-spec.md`. Section map:

| Spec section | Topic | Notes for pipeline |
|---|---|---|
| 3 | Backend boundary, save binding, server identity fields | shared with market-interface rules 1-2 and save-load rule 7 |
| 4 | Independent economic worlds | no cross-server assets |
| 5 | Hybrid market, trade types, liquidity modes (Finite/Infinite/Disabled), protections | pricing, matching, min lot: TBD |
| 6 | Export escrow and idempotency | mirrors market-interface rule 4 |
| 7 | Buy, save-level warehouse, full capacity reservation, offline fills via receipts | mirrors market-interface rules 5-6 |
| 8 | Currency authority and offline behavior | |
| 9 | Addon and version compatibility, realms | item-registry namespace set |
| 10 | Tradeable items and instance state clearing | clearing happens in the local export transaction |
| 11 | Auth and transport (HTTPS, WSS, Auth Header) | |
| 12 | Trust levels (community experimental first; Open vs Verified realms reserved) | |
| 13 | Migration credentials | only legal rebind path |
| 14 | Open questions | remain open |
| 15 | Implementation forbidden zone | binding |
| 16 | API contract entry | `Bruno/MarketAPI/` |

### States and Transitions

Escrow batch and order lifecycle per spec s6-7; not restated.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| market-interface | implements | the remote backend of the interface |
| save-load | in | binding fields (ServerUrl, ServerId, ServerIdentityKey, RealmId, AccountId, CompatibilityProfileHash, MarketProtocolVersion); Auth Token is not a save field |
| item-registry | in | namespace set for compatibility profiles |
| container | in | save-level warehouse reservations |
| tick-simulation | in | local escrow and receipt application are local transactions |
| economy-ledger | out | settlements |
| offline-progression | both | offline fills via receipts |
| industrial-duel | down | shared-market duel would build on this (Deferred) |

## Formulas

Deferred. Price formation, matching priority, protections, and fee formulas are TBD in spec s5 and s14. The only binding formal properties are idempotency (`apply(receipt) ∘ apply(receipt) = apply(receipt)`) and full-capacity reservation (`reserve(order) = order.count` before submit).

## Edge Cases

Handled in spec s6.2 (unknown result stays pending), s7.2 (partial fills consume proportional reservation; cancel releases), s7.3 (receipt applied once), s8 (offline: cached quotes only). Additional cases are TBD and must not be invented here.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| market-interface | hard | interface contract |
| item-registry, save-load, container, tick-simulation | hard | |
| economy-ledger | downstream | |
| network-programmer / security-engineer review | required before any implementation | trust, auth, anti-abuse |

## Tuning Knobs

Server-side configuration per spec s5.3-5.4 (liquidity mode per item, price bounds, order limits, volume windows, circuit breakers). Defaults TBD. No client-side knobs.

## Visual/Audio Requirements

None defined. Market UI is out of scope until re-enabled.

## UI Requirements

None defined; see spec s1 non-goals.

## Acceptance Criteria

Binding only for the interface-level properties already confirmed:

- **GIVEN** an export submitted with idempotency key K and an unknown network result, **WHEN** the client retries with K, **THEN** the server credits at most once and the client never re-sells the batch.
- **GIVEN** a buy order of N with warehouse acceptable < N, **WHEN** submitted, **THEN** it is rejected before any network call.
- **GIVEN** a fill receipt R, **WHEN** applied twice, **THEN** the warehouse changes once.
- **GIVEN** a save bound to server S, **WHEN** the player tries to bind to S' without a credential from S, **THEN** rebind is refused.
- **GIVEN** a save whose Addon namespace set is incompatible with the realm, **WHEN** connecting, **THEN** the server assigns a dedicated realm or refuses, per its compatibility profile.

Full acceptance criteria are authored when the mode is re-enabled.

## Open Questions

All items in spec s14 remain open. Nothing here resolves them.
