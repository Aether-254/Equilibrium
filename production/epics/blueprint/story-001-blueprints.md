# Story 001: Blueprint capture, rotation transform, stamping, and build orders

> **Epic**: Blueprint
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L (split capture/transform vs stamp/order at sprint planning)
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/blueprint.md`
**Requirement**: `TR-bp-001`

**Governing ADRs**: ADR-0004 (primary), ADR-0005, ADR-0001, ADR-0006

## Acceptance Criteria

- [ ] Captured 3-cell E belt stamped at 90 deg yields a 3-cell S belt (GDD AC 1); Type II footprint and anchor rotate together (GDD AC 4).
- [ ] BuildIncrementally with 5/12 belts builds 5 in AutoTopology order (GDD AC 2); StartWhenComplete builds nothing until 12 reserved (GDD AC 3).
- [ ] Accessor Keep 50 config survives capture and build (GDD AC 5).
- [ ] Identical existing cells skipped and not counted (GDD AC 6); cancel keeps built, removes ghosts and reservations (GDD AC 7).
- [ ] Unknown ItemIds in a blueprint become non-building placeholders; length-1 cut segments flagged at capture.
- [ ] Blueprint library UI and stamp controls (rotate, policy, order) per IP-02/IP-04.

## QA Test Cases

- GDD AC 1-7 verbatim

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Blueprint/BlueprintTests.cs`; UI evidence doc
**Status**: [ ] Not yet created

## Dependencies

- Depends on: placement-editing 003; multiblock 001; save-load 003
- Unlocks: none
