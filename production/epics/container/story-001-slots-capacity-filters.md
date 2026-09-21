# Story 001: SlotTable, effective capacity, and filters

> **Epic**: Container
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/container.md`
**Requirement**: `TR-cont-001`, `TR-cont-002`, `TR-cont-005`

**Governing ADRs**: ADR-0004 (primary), ADR-0003
**ADR Decision Summary**: `SlotTable` parallel arrays (item handle, count, capacity, filter, reserved); `Capacity.Effective = min(slot.capacity, def.MaxStack)`.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: acceptance math uses effective capacity.
- Forbidden: plain containers connecting to belts.

## Acceptance Criteria

- [ ] Slot capacity 3000, `iron_gear.maxStack 64`: inserting 100 accepts 64, 36 remain (GDD AC 1).
- [ ] Filters: allow ItemId, allow Tag, deny ItemId, deny Tag; deny beats allow (GDD AC 5).
- [ ] `acceptable(C, item)` and `available(C, item)` computed against the snapshot per GDD formulas.
- [ ] `UnknownItem` in a slot is treated as its original id for filters; maxStack falls back to the default knob.
- [ ] Chest def marks `AcceptsDirectBelt = false` on all ports; a belt facing a chest never moves items (GDD AC 6) (verified with grid-world Connected).
- [ ] Reserved flag on a slot excludes its count from `available`.

## Implementation Notes

Filters are content-defined `FilterDef` records compiled to a small struct with allow/deny handle sets and tag bitsets. Container definitions expose `slotKey` strings resolved to indices at load (story 003 does the key resolution; store keys now).

## Out of Scope

- Story 002: deterministic insertion and tidy
- Story 003: key binding, overflow container, benchmark

## QA Test Cases

- **AC-1**: 100 gears into cap-3000 slot with maxStack 64 -> 64 / 36
- **AC-2**: allow `#ingot`, deny `iron_ingot` -> iron_ingot rejected; copper_ingot accepted
- **AC-3**: chest 199/200 -> acceptable(wood) == 1
- **AC-6**: reserve 5 of 10 -> available == 5

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Container/SlotTableTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: grid-world 001; item-registry 002
- Unlocks: Story 002
