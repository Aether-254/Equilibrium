# ADR-0005: Save Format and Schema Versioning

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1 |
| **Domain** | Core (persistence) |
| **Knowledge Risk** | MEDIUM: MessagePack-CSharp is a third-party dependency; its Unity integration (source generator / mpc) must be verified against the .NET profile shipped with Unity 6.6 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None (Unity side); MessagePack-CSharp version to be pinned |
| **Verification Required** | IL2CPP AOT compatibility of generated resolvers; `Application.persistentDataPath` write/rotate on Windows |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (tick boundary), ADR-0003 (ItemId strings), ADR-0004 (keys) |
| **Enables** | ADR-0008 (market binding persisted), offline-progression |
| **Blocks** | save-load epic |
| **Ordering Note** | Schema v1 must be frozen before the first playable is shared |

## Context

### Problem Statement
The save-load GDD requires a versioned, migratable format that preserves unknown fields, stores full `ItemId` strings and string keys for containers/slots, never persists Tag expansions or tie-break RNG, and supports `UnknownItem`/`UnknownBlock` for missing content. Format choice was open among custom binary, MessagePack, and JSON+gzip.

### Constraints
- Saves of a 512x512 world with tens of thousands of slots must write in under a few hundred ms.
- Unknown fields must survive round-trip for forward-compatible Addon data.
- IL2CPP builds: no runtime reflection-based serialization.

### Requirements
- Header: `schemaVersion`, game version, Addon namespace set with versions, `marketMode` binding, `lastSaveWallTime`, `tickIndex`.
- Body: per-system sections each with their own sub-version.
- Backup rotation; atomic write (temp + rename).

## Decision

Decision (user, 2026-09-20): **MessagePack** via MessagePack-CSharp with AOT-generated resolvers.

1. `SaveDocument` is a MessagePack map with string keys at the top level (`header`, `world`, `containers`, `mech`, `market`, `ledger`, `unlocks`, `tasks`, `extras`). Each section is a `[MessagePackObject(keyAsPropertyName: true)]` record so unknown keys are retained via `IDictionary<string, object> Extras` on every record (`[MessagePackFormatter]` custom formatter preserving unknown members).
2. `ItemId` serializes as its canonical string; `containerKey`/`slotKey` as strings; indices never appear.
3. Migrations are ordered `ISaveMigration { int From; int To; void Apply(SaveDocument doc); }` applied in sequence; each is unit-tested with a fixture file.
4. Files: `saves/<slot>/save.eqs` (MessagePack, LZ4 block compression via `MessagePackSerializerOptions.WithCompression`), rotating `save.1.eqs`..`save.3.eqs`; write to `save.tmp` then rename.
5. Never save mid-tick: `TickPump` boundary hook (ADR-0001).
6. Refuse newer `schemaVersion`; refuse `WorldSizeUnsupported`; corrupt -> offer backups.

### Architecture Diagram
```
[TickPump boundary] -> SaveService.Capture(IWorldView, systems) -> SaveDocument -> MessagePack+LZ4 -> save.tmp -> rename
Load: file -> MessagePack -> Migrations(v..n) -> Validate -> Hydrate(IWorldWriter, ContentRegistry) -> UnknownItem/UnknownBlock
```

### Key Interfaces
```csharp
public interface ISaveable { string SectionKey { get; } int SectionVersion { get; } void Write(SaveWriter w); void Read(SaveReader r); }
public interface ISaveMigration { int From { get; } int To { get; } void Apply(SaveDocument doc); }
public sealed class SaveService { Task SaveAsync(int slot); Task<LoadResult> LoadAsync(int slot); }
```

## Alternatives Considered

### Alternative 1: Custom binary TLV
- **Description**: Hand-written tag-length-value writer.
- **Pros**: No dependency; smallest files.
- **Cons**: Unknown-field preservation and migrations hand-rolled; bug surface.
- **Rejection Reason**: Dependency cost judged lower than maintenance cost.

### Alternative 2: JSON + gzip (System.Text.Json)
- **Description**: Readable JSON, compressed.
- **Pros**: Diffable; zero external dependency; `JsonElement` preserves unknowns.
- **Cons**: 3-10x larger and slower than MessagePack for large slot tables.
- **Rejection Reason**: User chose MessagePack for performance; a debug JSON export command is kept for inspection.

## Consequences

### Positive
- Fast, compact saves; unknown-field preservation built in.
- Clear migration chain with fixtures.

### Negative
- Third-party dependency to pin and keep AOT-compatible; must add to Allowed Libraries in technical-preferences.

### Risks
- MessagePack-CSharp / Unity 6.6 compatibility: verify at implementation start; fallback is Alternative 2 with the same `ISaveable` interface.
- Schema drift: every `ISaveable` change requires a migration and fixture; enforced by test that loads all fixtures.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| save-load.md | Rules 1-12; edge cases (newer schema, corrupt, truncated, UnknownBlock) | `SaveService`, migrations, backups |
| item-registry.md | Rules 15, 19 (no Tag expansion; UnknownItem) | Canonical strings; hydrate step |
| container.md | Rule 6 keys not indices | String keys in sections |
| grid-world.md | Rules 4-6 block vs persistent attributes | Separate fields in `world` section |
| market-interface.md | Rule 1 binding; escrow persistence | `market` section |
| tick-simulation.md | Rule 5 RNG not saved; boundary saves | No RNG section; boundary hook |
| offline-progression.md | Rule 1 lastSaveWallTime | Header field |

## Performance Implications
- **CPU**: Serialization of 100k slots target under 200 ms; LZ4 fast path.
- **Memory**: Streaming writer; no full string JSON.
- **Load Time**: Parse + migrate + hydrate; target under 1 s for large saves.
- **Network**: None.

## Migration Plan
1. Add MessagePack-CSharp (pinned) to `Packages/manifest.json` via UPM git or OpenUPM; record in Allowed Libraries.
2. Implement `SaveService`, `ISaveable` on each system, schema v1, fixtures.
3. Debug menu: export current save as JSON for inspection.

## Validation Criteria
- save-load GDD acceptance criteria pass.
- Fixture corpus loads across all migrations.
- IL2CPP Windows build saves and loads without reflection errors.

## Related Decisions
- ADR-0001, ADR-0003, ADR-0004, ADR-0008
