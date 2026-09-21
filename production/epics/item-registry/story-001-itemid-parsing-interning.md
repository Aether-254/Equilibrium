# Story 001: ItemId parsing, canonicalization, and interning

> **Epic**: Item Registry
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/item-registry.md`
**Requirement**: `TR-item-001`

**Governing ADRs**: ADR-0003: Item Identity, Content Format, Addon Loading
**ADR Decision Summary**: `ItemId` is a readonly struct with an interned `int Handle` and canonical string; regex validation; `equilibrium:` may be omitted in config/display only.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW (pure C#)

**Control Manifest Rules (Content)**:
- Required: validate regex; canonicalize default namespace at parse time.
- Forbidden: display names as identity; UnityEngine in `Equilibrium.Content`.

## Acceptance Criteria

- [ ] `ItemId.TryParse("wood")` yields canonical `equilibrium:wood` (GDD AC 1).
- [ ] `ItemId.TryParse("Iron_Ingot:foo")` fails with `InvalidItemId` (GDD AC 2); also fails on spaces, uppercase, empty segments, leading `/`.
- [ ] `farmer_addon:seed` parses unchanged; `farmer_addon` namespace cannot be omitted.
- [ ] Same canonical string interns to the same `Handle`; different strings to different handles; `Equals`/`GetHashCode` by handle.
- [ ] `ItemId.Canonical` round-trips exactly.

## Implementation Notes

Regex `^[a-z0-9][a-z0-9._-]*:[a-z0-9][a-z0-9._/-]*$` compiled once. Intern table is a `Dictionary<string,int>` plus `List<string>`; owned by `ContentRegistry` but exposed via a static-free `ItemIdTable` instance passed in (no singletons; DI per coding standards).

## Out of Scope

- Story 002: ItemDef and Tag graph
- Story 003: Addon loading

## QA Test Cases

- **AC-1**: `"wood"` -> `"equilibrium:wood"`
- **AC-2**: invalid inputs table: `"Iron_Ingot:foo"`, `"a b:c"`, `"a:"`, `":c"`, `"a:/c"`, `"a//b:c"` all fail `InvalidItemId`
- **AC-3**: `"farmer_addon:seed"` canonical unchanged
- **AC-4**: parse twice, handles equal; parse `"equilibrium:stone"`, handle differs
- Edge: 10k distinct ids intern without collision

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/ItemRegistry/ItemIdTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: tick-simulation Story 001 (assemblies)
- Unlocks: Story 002
