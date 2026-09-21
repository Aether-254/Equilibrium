# ADR-0003: Item Identity, Content Data Format, and Addon Loading

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1 |
| **Domain** | Core (content pipeline) |
| **Knowledge Risk** | MEDIUM: `ScriptableObject` and `Resources`/Addressables loading are stable; Addressables 2.x API details are post-cutoff |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/plugins/addressables.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | Addressables 2.x `Addressables.LoadAssetsAsync` labels (verify signature) |
| **Verification Required** | Addressables label-based bulk load of `ItemDef` assets; JSON parse of Addon packs on IL2CPP |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0004 (Port/Container keys reference ItemIds), ADR-0005 (save serializes ItemIds), ADR-0008 (market keyed by ItemId) |
| **Blocks** | container, machine-processing, market epics |
| **Ordering Note** | Registry must exist before any Container test fixture |

## Context

### Problem Statement
The item-registry GDD fixes the identity model (namespaced lowercase `ItemId`, hierarchical Tags, Addon load graph, `UnknownItem`) but the storage format of content (ItemDefs, recipes, Tags, unlock nodes) was open. Base-game authors want Inspector workflows; Addon authors must not need Unity. Both must resolve to one runtime registry with identical validation.

### Constraints
- Runtime identity is a string `ItemId`; runtime hot paths need an interned integer handle.
- Duplicate `ItemId` is a hard error; cycles in Tag references or Addon dependencies refuse load.
- Saves store full `ItemId` strings (ADR-0005).

### Requirements
- Base content: `ItemDef`, `RecipeDef`, `TagDef`, `UnlockNodeDef`, `MachineDef`, `StructureDef` authored as ScriptableObjects.
- Addon content: JSON files in an Addon directory with a manifest declaring `namespace`, version, `dependsOn[]`, `modifies[]`.
- One `ContentRegistry` produced from both; validation identical.

## Decision

Decision (user, 2026-09-20): **ScriptableObject for base content, JSON for Addons**, both loaded into one runtime model.

1. **Runtime model** (`Equilibrium.Content`, plain C#): `ItemId` is a readonly struct wrapping an interned `int Handle` plus the canonical string; `ContentRegistry` holds `ItemDef`, `RecipeDef`, `TagGraph`, etc. as plain records. No Unity types in this assembly.
2. **Base authoring** (`Equilibrium.Content.Unity`): `ItemDefAsset : ScriptableObject` etc. with an `Export()` producing the plain record. Loaded via Addressables label `content-base` at startup.
3. **Addon authoring**: `addon.json` manifest + `items/*.json`, `recipes/*.json`, `tags/*.json`, `unlocks/*.json`. Parsed with `System.Text.Json` source-generated contexts into the same plain records.
4. **Editor parity**: an editor menu exports base ScriptableObjects to the Addon JSON schema so the base game can be validated by the same JSON validator and Addon authors get a reference pack.
5. **Loader**: builds the Addon dependency/modification DAG, topologically loads, rejects cycles and undeclared cross-namespace modification, applies Tag add/remove with remove-wins, validates `ItemId` regex and duplicates, expands Tags, and produces `UnknownItem` factories for missing namespaces on save load.
6. **Validation tool**: CLI-invocable (EditMode test + `-batchmode` entry) validator returning the same error codes (`InvalidItemId`, `DuplicateItemId`, `TagCycle`, `DependencyRejected`, `NamespaceReserved`, `UnlockWithoutDepth`, `NoNonTaskPath`, `ForbiddenRewardType`).

### Architecture Diagram
```
[ItemDefAsset (SO)] --Export--> \
                                 > [ContentRegistry (plain records, interned ItemId)] --> systems
[addon.json + *.json] --Parse-->/          ^
                                     [Validator: same rules both paths]
```

### Key Interfaces
```csharp
public readonly struct ItemId : IEquatable<ItemId> { public readonly int Handle; public string Canonical { get; } }
public sealed class ContentRegistry {
    public bool TryGet(string canonical, out ItemId id);
    public ItemDef Item(ItemId id);
    public bool InTag(ItemId id, TagId tag);
    public IReadOnlyList<RecipeDef> RecipesFor(MachineKey machine);
}
public interface IContentSource { ContentPack Load(); }   // SO source, JSON source
public sealed record AddonManifest(string Namespace, string Version, string[] DependsOn, string[] Modifies);
```

## Alternatives Considered

### Alternative 1: JSON everywhere
- **Description**: Base and Addons both JSON.
- **Pros**: One pipeline; diffable; no Inspector coupling.
- **Cons**: Loses Inspector asset references (sprites, prefabs) and drag-drop; custom editor tooling needed.
- **Rejection Reason**: User preferred Inspector workflow for base content.

### Alternative 2: ScriptableObject everywhere, Addons via AssetBundles/Addressables
- **Description**: Addon authors build Unity asset bundles.
- **Pros**: One type system.
- **Cons**: Addon authors need Unity and matching engine version; bundle compatibility pain.
- **Rejection Reason**: Contradicts the Addon accessibility goal.

## Consequences

### Positive
- Base authors keep Inspector ergonomics; Addon authors need only a text editor.
- One validator ensures identical error semantics.

### Negative
- Two authoring paths to maintain; export step must be kept in sync (mitigated by round-trip test).

### Risks
- Drift between SO fields and JSON schema: mitigated by generating the JSON schema from the plain record types and a round-trip EditMode test.
- Addressables 2.x API changes: verify against installed package version before implementation.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| item-registry.md | Rules 1-19 (ItemId format, Tags, Addon DAG, UnknownItem) | Loader and validator |
| container.md | `itemDef.maxStack` lookup on hot path | Interned `ItemId.Handle` |
| machine-processing.md | Recipe schema rule 1, Tag input policy rule 2 | `RecipeDef` record and `RecipesFor` |
| tech-unlock.md | Rules 1-2 validation (`UnlockWithoutDepth`, `NoNonTaskPath`) | Validator error codes |
| tutorial-task.md | Rule 8 `ForbiddenRewardType` | Validator |
| save-load.md | Rules 2-3 (full ItemId strings; UnknownItem) | `ItemId.Canonical` and UnknownItem factory |

## Performance Implications
- **CPU**: Startup parse/validate of a few hundred records: under 200 ms target.
- **Memory**: Registry small; interning table.
- **Load Time**: Addressables label load for base pack; Addon JSON parse.
- **Network**: None.

## Migration Plan
1. Replace existing `Assets/Script/ItemDef.cs` (MonoBehaviour/SO) with `ItemDefAsset` exporting to the plain `ItemDef` record.
2. Create `Equilibrium.Content` and `Equilibrium.Content.Unity` assemblies.
3. Author Tier 1 content (log, plank, wood fuel, engine, shaft, gears, saw, chest, belt, accessor, blocker) as SOs; export a reference Addon pack.

## Validation Criteria
- item-registry GDD acceptance criteria as EditMode tests using the JSON source.
- Round-trip test: SO -> export JSON -> parse == SO export.
- Startup content load under 200 ms.

## Related Decisions
- ADR-0005 save format
- design/gdd/item-registry.md
