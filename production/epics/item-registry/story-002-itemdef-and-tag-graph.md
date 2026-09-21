# Story 002: ItemDef records and hierarchical Tag graph

> **Epic**: Item Registry
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/item-registry.md`
**Requirement**: `TR-item-002`, `TR-item-003`

**Governing ADRs**: ADR-0003
**ADR Decision Summary**: Plain record definitions in `Equilibrium.Content`; Tag graph built at load with `/` parent inclusion, references, cycle detection, remove-wins edits and child cascade.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Content)**:
- Required: duplicate ItemId is a hard error; Tag cycle refuses the involved content; remove beats add; parent removal cascades to children.
- Forbidden: `#common:` handling in base.

## Acceptance Criteria

- [ ] `ItemDef` record: `Id`, `MaxStack >= 1`, `DisplayNameKey`, `IconRef`, optional `Tradeable` flags, optional `EnergyJ` (fuel), optional machine props (`TauReq`).
- [ ] Registering a duplicate `ItemId` in one pack is a hard error `DuplicateItemId`; first wins across packs and the second pack is Rejected (GDD AC 3).
- [ ] `#ingot/iron` containing `iron_ingot` makes `InTag(iron_ingot, #ingot)` true (GDD AC 4).
- [ ] Removing `iron_ingot` from `#ingot` also removes it from `#ingot/iron` (GDD AC 5).
- [ ] Tag A references B references A: both refuse to load with `TagCycle` (GDD AC 6).
- [ ] `#ingot` shorthand resolves to `#equilibrium:ingot`; plural or affixed names rejected by validator warning.
- [ ] `MaxStack <= 0` is a registration error; missing display key falls back to canonical id with a warning.

## Implementation Notes

Build the Tag graph as: direct members per tag, children derived by path prefix, references as edges; topologically resolve references (Kahn) and mark cycle leftovers. Membership expansion computed once into a `BitSet` per tag over item handles for O(1) `InTag`. Removals applied after expansion: remove from the tag and every descendant tag (rule 14).

## Out of Scope

- Story 003: Addon manifest and load order
- Story 004: SO/JSON sources

## QA Test Cases

- **AC-2**: duplicate within pack -> `DuplicateItemId`; across packs -> second pack `Rejected`
- **AC-3/4**: `#ingot/iron` member is in `#ingot`; removal from `#ingot` clears both
- **AC-5**: reference cycle -> `TagCycle`, `InTag` false for both
- **AC-6**: `#ingot` == `#equilibrium:ingot`
- Edge: depth 16 nested tags resolve; depth 17 rejected by knob

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/ItemRegistry/TagGraphTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001
- Unlocks: Story 003, container epic
