# Story 004: ScriptableObject and JSON content sources with shared validator

> **Epic**: Item Registry
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-09-21

## Implementation Log (2026-09-21)

Deviations from the story plan, both to be reflected in ADR-0003 consequences:
- JSON parsing uses Newtonsoft.Json (shipped with Unity as `com.unity.nuget.newtonsoft-json`, referenced as a precompiled assembly so `Equilibrium.Content` stays `noEngineReferences`) instead of `System.Text.Json`, which is not available on Unity's .NET Standard 2.1 profile. Unknown item fields are preserved through `JObject` into `ItemDefData.Extras`.
- Base content ships as a `ContentCatalog` ScriptableObject under `Assets/Resources/` (loaded with `Resources.Load`) rather than an Addressables label, because the Addressables package is not installed. `ScriptableObjectContentSource` keeps the `IContentSource` boundary so switching to Addressables later touches one class.
- Tier 1 content is defined in code (`Tier1Content.Build()`); the editor menu `Equilibrium/Content/Generate Tier 1 Assets` materializes it as SO assets + catalog, `Export Reference Addon` writes `Addons/_reference/`, `Validate Catalog` runs the shared loader. `Assets/Resources/ContentCatalog.asset` and `Assets/Content/**` are created by the creator running the menu in Unity (not by the agent).
- Files: `Assets/Scripts/Content/{Items/ItemId.cs, Items/ItemIdTable.cs, Items/TagGraph.cs, ContentError.cs, ContentRegistry.cs, ContentLoader.cs, PackPass.cs, Data/*, Sources/JsonContentSource.cs, Tier1Content.cs}`, `Assets/Scripts/Content.Unity/ContentAssets.cs`, `Assets/Editor/ContentTools.cs`; tests `Assets/Tests/EditMode/ItemRegistry/*` (42 cases) and `Assets/Tests/PlayMode/ItemRegistry/ContentCatalogTests.cs`.

## Context

**GDD**: `design/gdd/item-registry.md`
**Requirement**: `TR-item-001`..`TR-item-005` (source parity)

**Governing ADRs**: ADR-0003
**ADR Decision Summary**: `ItemDefAsset : ScriptableObject` etc. with `Export()`; Addressables label `content-base`; JSON packs via `System.Text.Json` source-generated contexts; editor menu exports base to reference Addon pack; one validator.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: verify Addressables 2.x `LoadAssetsAsync<T>(label, callback)` signature against installed package; `System.Text.Json` source generation requires the .NET profile Unity 6.6 ships; fall back to reflection-based `JsonSerializer` if source-gen is unavailable on IL2CPP.

**Control Manifest Rules (Content)**:
- Required: same validator on both paths; no field exists in only one path.
- Guardrail: startup load under 200 ms.

## Acceptance Criteria

- [ ] `Equilibrium.Content.Unity` defines `ItemDefAsset`, `RecipeDefAsset`, `TagDefAsset`, `MachineDefAsset`, `MarketItemDefAsset`, `UnlockNodeDefAsset` each with `Export()` to the plain record.
- [ ] `ScriptableObjectContentSource` loads all assets with Addressables label `content-base` and yields a `ContentPack`.
- [ ] `JsonContentSource` parses `addon.json` + `items/*.json`, `recipes/*.json`, `tags/*.json`, `unlocks/*.json`, `market/*.json` into the same records.
- [ ] Round-trip: export base SOs to JSON, parse with `JsonContentSource`, resulting `ContentPack` equals the SO export.
- [ ] Validator returns identical error codes for the same defect injected via SO and via JSON.
- [ ] Editor menu `Equilibrium/Content/Export Reference Addon` writes the base pack to `Addons/_reference/`.
- [ ] Tier 1 content authored: log, plank, wood (fuel, EnergyJ 8000), engine, shaft, gear8/16/32, saw (TauReq 30), chest, belt, accessor, blocker; recipe `log_to_plank` (workRequired 1350); market entries per market-interface tuning knobs. Load under 200 ms.

## Implementation Notes

Keep records `sealed record` types in `Equilibrium.Content`; SO assets hold serializable fields mirroring them. JSON schema is generated from the records by a small editor tool and checked into `Addons/_reference/schema/`. Numeric tuning values come from the GDD tuning tables and are marked provisional in the asset names' tooltips, not in code.

## Out of Scope

- Save serialization of ItemIds (save-load)
- Recipe execution (machine-processing)

## QA Test Cases

- **AC-4**: round-trip equality (deep compare)
- **AC-5**: inject duplicate id via SO and via JSON -> both `DuplicateItemId`
- **AC-7**: load Tier 1 pack; assert 13 items, 1 recipe; stopwatch < 200 ms
- Manual (Editor): export menu produces files; schema validates the reference pack

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/ItemRegistry/ContentSourceParityTests.cs`; PlayMode Addressables load test
**Status**: [x] Created; passes under dotnet purecheck (Unity Test Runner run pending by creator)

## Dependencies

- Depends on: Story 003
- Unlocks: container, machine-processing, market-interface epics
