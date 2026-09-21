# Save / Load

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Depth Is Progress (a factory built today must load in a later tier), Factory and Economy Are One (save binds to exactly one market world)
> **Normative Source**: design/confirmed-mechanics-spec.md s2.1 (full ItemId), s2A.6 (Tags not persisted), s2B.3 (UnknownItem), s3 (persistent attributes), s6.2 (string keys), s17 (market binding); game-concept Offline Income constraints
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Save/load persists the simulation at a tick boundary and restores it exactly, subject to the rules other systems impose: full `ItemId`s, no expanded Tags, `UnknownItem` for missing Addons, Port bindings by string key, persistent versus block attributes, and a one-time market-mode binding. It also owns schema versioning and migration so that saves survive content updates and later fidelity tiers. Players experience it as "my factory is exactly as I left it" and as the market-mode choice made at world creation.

## Player Fantasy

Indirect, with one direct moment: choosing local NPC market or a Market Server at world creation, knowing it is permanent barring a migration credential. Otherwise the fantasy is safety: nothing lost, nothing duplicated, nothing quietly changed.

## Detailed Design

### Core Rules

1. A save is taken only at a tick boundary (tick-simulation rule). It records `tickIndex`, world grid, block instances, Containers, mechanical/market/ledger state, and the Addon namespace set with versions.
2. Item references are full `ItemId` strings. Tag expansions are never stored.
3. Items whose namespace is missing at load become `UnknownItem` preserving id, count, and extra data.
4. Container and Port bindings are serialized by string keys (`containerKey`, `slotKey`), never array indices.
5. Block attributes (orientation, controller coordinate) and persistent attributes (items, energy, progress) are both saved for placed blocks; dropped items carry persistent attributes only.
6. Reserved recipe inputs store the concrete chosen `ItemId`.
7. The save records `marketMode` (`LocalNpc` | `RemoteServer`) and, if remote, the bound server identity. This binding cannot be changed by the client except through a server-issued migration credential (market-interface / market-server GDDs).
8. Every save carries `schemaVersion`. Load applies ordered migrations `v -> v+1` until current. Migration never deletes data it does not understand; unknown fields are preserved verbatim in an `extras` bag per object.
9. Tie-break RNG state is NOT saved (tick-simulation rule 5).
10. Time-control state (speed, pause) is not simulation state and is saved as a UI preference only.
11. Offline elapsed wall time is recorded at save; offline-progression (Full Vision) decides what to do with it. Tier 1 ignores it.
12. Load is all-or-nothing: a save that fails validation after migration is not loaded; the original file is untouched.

### States and Transitions

| State | Transition | Next |
|---|---|---|
| Playing | save requested | PendingSave (until tick boundary) |
| PendingSave | tick commits | Serializing → Playing |
| MainMenu | load selected | Validating → Migrating → Hydrating → Playing |
| Validating | schema unknown / corrupt | LoadFailed (message, original intact) |
| Hydrating | missing Addon | Playing with UnknownItems + notice |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | boundary hook; snapshot source |
| item-registry | in | ItemId validation; UnknownItem construction; Addon set |
| grid-world, container, multiblock, mechanical-power, machine-processing | both | each exposes `Serialize(SaveWriter)` / `Deserialize(SaveReader)` for its state |
| market-interface | both | marketMode binding; escrowed exports; reserved warehouse capacity |
| economy-ledger | both | ledger history |
| offline-progression | out | last save wall-clock timestamp |

## Formulas

The migration chain formula is defined as:

`load(save) = hydrate(M_{n-1} ∘ ... ∘ M_{v}(save))` where `v = save.schemaVersion`, `n = currentSchemaVersion`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| saved version | v | int | 1..n | version stamped in file |
| current version | n | int | >= v | build's schema |
| migration | M_k | function | - | transforms schema k to k+1 |

**Output Range:** a current-schema document or a validation failure.
**Example:** save v3, current v5: apply M_3 then M_4, then hydrate.

## Edge Cases

- **If the save's `schemaVersion` is newer than the build**: refuse to load with `SaveFromNewerVersion`; never downgrade.
- **If an Addon is present but at a different version than saved**: load; Addon is responsible for its own item migration; unknown fields preserved in `extras`.
- **If a block definition no longer exists**: the block becomes an `UnknownBlock` occupying its cells, holding its persistent attributes, connectable to nothing; removable by the player into an `UnknownItem`.
- **If a Port's `slotKey` is missing after content change**: container GDD `PortBindingMissing`.
- **If the file is truncated**: Validating fails; report `SaveCorrupt`; keep the last good backup (rotate N backups).
- **If two saves are requested in one tick**: coalesce into one.
- **If marketMode is RemoteServer and the server is unreachable at load**: load succeeds; market enters Offline (cached prices, no new remote trades).
- **If escrowed export items exist in the save**: they load as escrowed and market-interface resumes the idempotent submission.
- **If the world size in the save exceeds the current `worldWidth/Height`**: refuse with `WorldSizeUnsupported` (map model is open; do not truncate).

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation | hard | boundary |
| item-registry | hard | ids, Addon set |
| grid-world, container | hard | primary payload |
| market-interface | hard | binding |
| all other simulated systems | soft | each serializes its own state |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| autosaveIntervalTicks | 300 (5 min) | 60..3600 | IO churn | large loss on crash |
| backupRotationCount | 3 | 1..10 | no recovery | disk use |
| compression | on | bool | large files | CPU on save |

## Visual/Audio Requirements

Save indicator icon; no audio.

## UI Requirements

World creation: market mode selector with a permanent-choice warning. Load screen: schema version, Addon set, missing-Addon warning list. In-game: autosave indicator.

## Acceptance Criteria

- **GIVEN** a running factory, **WHEN** saved and loaded, **THEN** every Container slot, block orientation, machine progress, engine fuel, and ledger total is identical to the pre-save tick snapshot.
- **GIVEN** a save with `farmer_addon:seed x10` and the Addon absent, **WHEN** loaded, **THEN** an `UnknownItem` with that id and count occupies the same slot; re-enabling the Addon and reloading restores `farmer_addon:seed x10`.
- **GIVEN** a reserved Tag-matched input, **WHEN** saved, **THEN** the file contains the concrete ItemId string and no Tag string in that field.
- **GIVEN** a content update that reorders a machine's slot array, **WHEN** an old save loads, **THEN** Ports resolve by `slotKey` to the correct slots.
- **GIVEN** a save at schema v1 and a build at v3, **WHEN** loaded, **THEN** M_1 and M_2 run in order and the result validates.
- **GIVEN** a save from a newer schema, **WHEN** loaded, **THEN** load is refused with `SaveFromNewerVersion` and the file is unchanged.
- **GIVEN** a truncated save and 3 rotating backups, **WHEN** load fails, **THEN** the UI offers the most recent valid backup.
- **GIVEN** a save request mid-tick, **WHEN** the tick completes, **THEN** the file reflects post-commit state.
- **GIVEN** a world created with `RemoteServer`, **WHEN** the player attempts to switch to `LocalNpc` without a credential, **THEN** the option is unavailable.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Serialization format (binary, JSON, MessagePack) and Unity integration | technical-director (ADR) | before implementation |
| 2 | Cloud save / Steam Cloud | release-manager | Full Vision |
| 3 | Persistent attribute list for fluids and temperature | glass-pipe GDD | Alpha |
| 4 | Offline elapsed-time semantics | offline-progression GDD | Full Vision |
