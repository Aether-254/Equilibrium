# Equilibrium Market API Design

## 1. Contract status

- API version: `v1` design draft.
- HTTP base path: `/v1`.
- WSS endpoint: `/v1/events`.
- Discovery endpoint: `/.well-known/equilibrium-market`.
- HTTP schema: `openapi.yaml`.
- WSS schema: `asyncapi.yaml`.
- Authentication: server-advertised OAuth/OIDC; API calls use an Auth header.
- Server implementation, storage engine, matching engine, and deployment are out
  of scope for this artifact.

## 2. Resource vocabulary

| Term | Meaning |
| --- | --- |
| Server | One independently operated Market Server and economic world. |
| Realm | An economy partition selected from a compatibility profile. |
| Compatibility Profile | Game version, protocol version, Addon set, and registry fingerprints. |
| Registry Snapshot | Immutable effective Item/Tag/Add-on definition view identified by a digest. |
| Item ID | Fully qualified canonical ID such as `equilibrium:wood`. |
| Tag ID | Fully qualified canonical ID with `#`, such as `#equilibrium:ingot/iron`. |
| Instrument | A tradable commodity identified by Realm and Fungibility Key. |
| Export Lot | Locally escrowed items accepted by the server for selling. |
| Order | Market or limit buy/sell instruction. |
| Settlement | Durable delivery or credit result that must be applied exactly once. |
| Migration | Server-authorized change of a save's immutable market binding. |

IDs received through the API are opaque. Clients must not derive database keys,
sorting, permissions, or compatibility from their string shape.

## 3. Canonical Item and Tag IDs

The API always uses fully qualified IDs. Authoring conveniences that omit the
`equilibrium:` namespace are not accepted at the network boundary.

```text
Item: equilibrium:wood
Item: farmer_addon:seed
Tag:  #equilibrium:ingot
Tag:  #equilibrium:ingot/iron
Tag:  #common:ingot/iron
```

`/` carries hierarchical tag meaning. Membership in
`#equilibrium:ingot/iron` implies membership in `#equilibrium:ingot` after the
effective registry applies Addon additions and removals. `#common:` remains an
Addon/Mod-maintained namespace and is not supplied by base-game content.

Item and Tag IDs are passed in JSON bodies or query values rather than split into
arbitrary URL path segments. This avoids ambiguity when future canonical paths
contain `/`.

## 4. Common protocol rules

### 4.1 Headers

| Header | Required | Meaning |
| --- | --- | --- |
| `Authorization` | Authenticated routes | Bearer access token or server-advertised Auth scheme. |
| `Idempotency-Key` | State-changing retryable commands | Stable UUID/ULID-like key reused for the same logical command. |
| `X-Request-Id` | Recommended | Client correlation ID; server echoes or replaces it. |
| `X-Market-Protocol-Version` | Authenticated client routes | Client protocol version, initially `1`. |
| `X-Game-Version` | Compatibility-sensitive routes | Full game version. |
| `If-None-Match` | Registry and market snapshots | Conditional cache lookup using ETag. |

The server returns `Date`, `X-Request-Id`, and rate-limit headers when relevant.

### 4.2 Numeric representation

- Item quantities are non-negative decimal integer strings.
- Currency amounts and prices are decimal strings plus a currency ID.
- JSON floating-point numbers are not used for authoritative quantities, money,
  prices, fees, or balances.
- Percentages and ratios are decimal strings with an endpoint-defined scale.
- Times use RFC 3339 UTC strings.

Example:

```json
{
  "quantity": "3000",
  "price": { "currencyId": "equilibrium:credit", "amount": "12.5000" }
}
```

### 4.3 Pagination

Collection endpoints use opaque cursor pagination:

```text
?limit=100&cursor=<opaque>
```

Responses contain `items` and `nextCursor`. An absent or null `nextCursor`
means the page is final. Clients must not parse cursors.

### 4.4 Error envelope

Errors use `application/problem+json` and contain:

```json
{
  "type": "https://market.example.invalid/problems/warehouse-capacity",
  "title": "Warehouse capacity is not reserved",
  "status": 409,
  "code": "WAREHOUSE_CAPACITY_NOT_RESERVED",
  "detail": "The complete buy quantity requires an active reservation.",
  "instance": "/v1/orders/01J...",
  "traceId": "01J...",
  "retryable": false,
  "details": {}
}
```

`code` is stable for program logic. Human-readable `title` and `detail` are not
stable identifiers.

### 4.5 Idempotency

- Reusing a key with the identical canonical command returns the original result.
- Reusing a key with a different body returns `409 IDEMPOTENCY_KEY_REUSED`.
- Timeout means unknown result. The client queries the created resource or retries
  the identical command with the same key.
- Idempotency retention duration is advertised in server capabilities.

## 5. Endpoint catalog

### 5.1 Discovery, auth, and server state

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/.well-known/equilibrium-market` | No | Stable server identity, API roots, public key set, and protocol range. |
| GET | `/v1/server` | No | Server identity and display metadata. |
| GET | `/v1/capabilities` | No | Protocol features, limits, auth modes, idempotency retention. |
| GET | `/v1/time` | No | Server-authoritative time and clock uncertainty. |
| GET | `/v1/auth/config` | No | OAuth/OIDC issuer, endpoints, client ID policy, and scopes. |

OAuth token issuance remains at the advertised identity provider. Market API
routes do not invent a second password-login protocol.

### 5.2 Account

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| GET | `/v1/me` | `account:read` | Account, bound save, Realm, and compatibility status. |
| GET | `/v1/me/balances` | `balances:read` | Server-authoritative currency balances. |
| GET | `/v1/me/limits` | `account:read` | Rate, order, and trust-tier limits. |

### 5.3 Compatibility and Realms

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/v1/compatibility/evaluations` | Optional | Evaluate game/Add-on profile and return allow, isolate, restrict, or reject. |
| GET | `/v1/realms` | Optional | List visible Realms and transaction visibility. |
| GET | `/v1/realms/{realmId}` | Optional | Realm compatibility constraints and lifecycle state. |

Compatibility evaluation returns one of:

```text
ALLOW
ISOLATE
ALLOW_RESTRICTED
REJECT
```

It also returns the exact registry snapshot digest the client must use.

### 5.4 Effective Item/Tag registry

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/v1/registry/snapshots/current` | Effective immutable registry metadata and digest. |
| POST | `/v1/registry/items:resolve` | Resolve multiple canonical Item IDs in one snapshot. |
| POST | `/v1/registry/tags:resolve` | Resolve tags, parents, effective members, additions, and removals. |
| GET | `/v1/registry/changes` | Cursor-paged changes between compatible snapshots. |

Resolution requests include `snapshotDigest`. A server returns `409` if the
snapshot is no longer usable for new transactions rather than silently resolving
against a different registry.

### 5.5 Addon manifests and review

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| POST | `/v1/addons/manifests:validate` | `addons:submit` | Data-only schema, namespace, dependency, mutation, and fingerprint validation. |
| POST | `/v1/addons/manifests` | `addons:submit` | Submit a developer manifest for review. |
| GET | `/v1/addons/manifests/{submissionId}` | `addons:read` | Read validation and review state. |
| POST | `/v1/admin/addons/manifests/{submissionId}:review` | `admin:addons` | Approve, isolate, restrict, or reject. |

The server never executes uploaded Addon code. The manifest describes effective
data contributions, including:

- namespace and version;
- content fingerprint;
- dependencies and modifications;
- economic impact declaration;
- added/changed Items;
- Tag additions and removals;
- recipe/economic fingerprints;
- requested Realm policy.

Dependency and mutation cycles are rejected. If a required or modified namespace
is rejected, dependants are rejected for that compatibility profile.

### 5.6 Instruments and public market data

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/v1/realms/{realmId}/instruments` | List tradable Fungibility Keys. |
| GET | `/v1/realms/{realmId}/instruments/{instrumentId}` | Instrument metadata and trading state. |
| GET | `/v1/realms/{realmId}/instruments/{instrumentId}/order-book` | Current local-Realm book. |
| GET | `/v1/realms/{realmId}/instruments/{instrumentId}/trades` | Recent trades. |
| GET | `/v1/realms/{realmId}/instruments/{instrumentId}/candles` | OHLCV time buckets. |
| GET | `/v1/realms/{realmId}/observations` | Delayed aggregate visibility into other Realms. |

Cross-Realm observations never grant transaction authority. Their response marks
delay, aggregation period, source Realm, and `tradableFromCurrentRealm: false`.

### 5.7 Export lots

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| POST | `/v1/exports` | `exports:write` | Accept a locally escrowed, normalized sell lot. |
| GET | `/v1/exports` | `exports:read` | List export lots by state. |
| GET | `/v1/exports/{exportId}` | `exports:read` | Recover an unknown submission result. |
| POST | `/v1/exports/{exportId}:release` | `exports:write` | Release only an unused/rejected lot back to local recovery. |

Submitting an export requires `Idempotency-Key`, save binding, Realm, registry
digest, local escrow transaction ID, and normalized commodity lines. The server
returns a durable `exportId`; sell orders consume available quantity from it.

### 5.8 Orders

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| POST | `/v1/orders` | `orders:write` | Create MARKET or LIMIT, BUY or SELL order. |
| GET | `/v1/orders` | `orders:read` | List caller orders with cursor pagination. |
| GET | `/v1/orders/{orderId}` | `orders:read` | Get authoritative order state. |
| POST | `/v1/orders/{orderId}:cancel` | `orders:write` | Idempotently request cancellation. |

BUY orders require a complete local warehouse capacity reservation reference.
SELL orders require an accepted export lot. Cancellation is not retroactive: any
fill committed before cancellation remains valid.

### 5.9 Settlements

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| GET | `/v1/settlements` | `settlements:read` | List pending or historical settlements. |
| GET | `/v1/settlements/{settlementId}` | `settlements:read` | Fetch a signed delivery/credit result. |
| POST | `/v1/settlements/{settlementId}:acknowledge` | `settlements:write` | Confirm exact-once local application. |

Acknowledgement includes the local unified-transaction ID and resulting local
state digest. An acknowledgement retry with the same idempotency key returns the
original result. It does not create a second delivery.

### 5.10 Contracts

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| GET | `/v1/contracts` | `contracts:read` | List active public industrial contracts. |
| GET | `/v1/contracts/{contractId}` | `contracts:read` | Contract requirements and server deadline. |
| POST | `/v1/contracts/{contractId}/deliveries` | `contracts:write` | Deliver accepted export-lot quantities. |

Contract reward and competition rules remain data-driven per server/Realm.

### 5.11 Migration

| Method | Path | Scope | Purpose |
| --- | --- | --- | --- |
| POST | `/v1/migrations` | `migration:request` | Request the currently bound server to prepare migration. |
| GET | `/v1/migrations/{migrationId}` | `migration:read` | Poll preparation/freeze state and retrieve signed credential. |
| POST | `/v1/migrations/{migrationId}:redeem` | server-to-server or authenticated NPC flow | Atomically consume the source nonce. |
| POST | `/v1/migration-imports` | `migration:import` | Present a credential to the destination server. |

Migration is a state machine:

```text
REQUESTED -> PREPARING -> READY -> REDEEMED
                     \-> REJECTED
                     \-> EXPIRED
```

The source freezes affected market state before `READY`. A destination must
validate the signature and atomically redeem the nonce with the source before
accepting the binding. A credential can authorize any compatible server, an
allow-list, recommended servers, or NPC mode. The transfer payload explicitly
lists which balances, orders, exports, settlements, and restrictions migrate;
omitted assets do not silently transfer.

## 6. WSS events

Clients connect to:

```text
wss://<server>/v1/events?cursor=<lastAcknowledgedCursor>
```

Auth is supplied through the supported Auth header during the upgrade. Every
event uses a common envelope:

```json
{
  "eventId": "01J...",
  "cursor": "opaque",
  "type": "order.updated",
  "occurredAt": "2026-09-20T12:00:00Z",
  "realmId": "vanilla-1",
  "subject": "orders/01J...",
  "data": {}
}
```

Required event types:

```text
market.instrument.updated
market.trade.executed
order.updated
export.updated
settlement.created
balance.updated
contract.updated
realm.updated
registry.snapshot.published
server.maintenance.updated
migration.updated
```

The stream is resumable, not an authoritative replacement for HTTP state reads.
On cursor expiration the server sends `stream.resync-required`; the client fetches
fresh HTTP snapshots before resuming.

## 7. Important state and conflict codes

| Code | HTTP | Meaning |
| --- | --- | --- |
| `COMPATIBILITY_REJECTED` | 403 | Client profile cannot enter this server/Realm. |
| `REALM_READ_ONLY` | 409 | Legacy or observed Realm does not allow new trades. |
| `REGISTRY_SNAPSHOT_STALE` | 409 | Transaction used a retired registry digest. |
| `UNKNOWN_ITEM_ID` | 422 | Canonical Item ID is absent from the snapshot. |
| `UNKNOWN_TAG_ID` | 422 | Canonical Tag ID is absent from the snapshot. |
| `ITEM_NOT_TRADABLE` | 422 | Item lacks effective `MarketTradable`. |
| `FUNGIBILITY_MISMATCH` | 422 | Commodity state does not match the instrument. |
| `WAREHOUSE_CAPACITY_NOT_RESERVED` | 409 | Complete BUY quantity lacks reservation. |
| `EXPORT_NOT_AVAILABLE` | 409 | SELL quantity is not available in the export lot. |
| `ORDER_ALREADY_FINAL` | 409 | Filled, cancelled, rejected, or expired order cannot change. |
| `IDEMPOTENCY_KEY_REUSED` | 409 | Same key was used with a different command. |
| `SETTLEMENT_ALREADY_APPLIED` | 200/409 | Same application is safe; conflicting application is rejected. |
| `MIGRATION_NOT_READY` | 409 | Source state is not frozen and credential is unavailable. |
| `MIGRATION_ALREADY_REDEEMED` | 409 | Credential nonce has already been consumed. |
| `RATE_LIMITED` | 429 | Caller exceeded an advertised limit. |

## 8. Acceptance tests

1. Retrying `POST /v1/exports` with the same key and body returns one `exportId`.
2. Reusing that key with a changed quantity returns `IDEMPOTENCY_KEY_REUSED`.
3. A BUY order without full capacity reservation is rejected before entering the book.
4. A SELL order cannot exceed unconsumed quantity in its accepted export lot.
5. A stale registry digest cannot silently resolve against the current snapshot.
6. Resolving `#equilibrium:ingot/iron` also reports parent `#equilibrium:ingot`.
7. Addon Tag removals show provenance and affect the immutable effective snapshot.
8. A BalanceAffecting or Overhaul Addon cannot silently enter a vanilla Realm.
9. Cross-Realm observation responses are marked non-tradable.
10. Replaying a settlement acknowledgement cannot duplicate warehouse delivery.
11. A cancelled order retains fills committed before cancellation.
12. A migration credential cannot be redeemed twice, including at two destinations.
13. Changing only `ServerUrl` cannot change the saved `ServerId` binding.
14. Expired WSS cursors force HTTP resynchronization rather than silent event loss.
