# Story 003: Addon manifest, dependency graph, and UnknownItem

> **Epic**: Item Registry
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/item-registry.md`
**Requirement**: `TR-item-004`, `TR-item-005`

**Governing ADRs**: ADR-0003 (primary), ADR-0005
**ADR Decision Summary**: `AddonManifest{Namespace, Version, DependsOn[], Modifies[]}`; topological load; cycles and undeclared modification refuse; `UnknownItem` factories for missing namespaces.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Content)**:
- Required: rejection codes `NamespaceReserved`, `DependencyRejected`, `AddonCycle`, `UndeclaredModification`.

## Acceptance Criteria

- [ ] Addon without `namespace` refuses load; `namespace: equilibrium` refuses with `NamespaceReserved`.
- [ ] B depends on A, A rejected: B rejected `DependencyRejected` (GDD AC 7).
- [ ] A modifies B without declaring B: A rejected `UndeclaredModification`.
- [ ] Cycle A<->B: both rejected `AddonCycle`.
- [ ] Load order is topological; a Tag removal in B applied after A's addition.
- [ ] Save reference `farmer_addon:seed x10` with no such namespace hydrates to `UnknownItem{OriginalId, Count=10, Extras}` (GDD AC 8); after the Addon returns, hydration yields the real item.
- [ ] Registry lifecycle states: Unloaded -> Loading base -> Loading addons -> Ready; Rejected addons listed with codes.

## Implementation Notes

`AddonLoader.Load(IEnumerable<IContentSource>)` returns `LoadReport{Accepted[], Rejected[(ns, code)]}`. UnknownItem is an `ItemDef` created on demand with `IsUnknown = true`, `MaxStack = defaultMaxStack`, handle interned under the original canonical string so container filters evaluate against the original id.

## Out of Scope

- Story 004: concrete SO and JSON sources
- Recovery timing UX (open question)

## QA Test Cases

- **AC-1**: missing namespace / reserved namespace rejections
- **AC-2/3/4**: dependency, undeclared, cycle rejections with codes
- **AC-5**: order: A adds x to #t, B removes x; final `InTag(x,#t)` false
- **AC-6**: hydrate unknown -> `IsUnknown`, count 10; re-load with addon -> real def

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/ItemRegistry/AddonLoaderTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002
- Unlocks: Story 004, save-load epic
