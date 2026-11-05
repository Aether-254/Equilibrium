# Story 001: Splitter, Combiner, and Batcher

> **Epic**: Group Processing Devices
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/group-processing-devices.md`
**Requirement**: `TR-gpd-001`

**Governing ADRs**: ADR-0001 (primary), ADR-0004
**Decision to make at story start**: one configurable device with three modes vs three blocks (GDD open question 1); default to one device with modes unless content authoring argues otherwise.

## Acceptance Criteria

- [ ] RoundRobin alternates A,B,A,B (GDD AC 1); Divide 100 across 3 -> 34/33/33 (GDD AC 2); blocked B -> 50/0/50 (GDD AC 3); Ratio 2:1 -> 67/33 (GDD AC 4).
- [ ] Combiner merges wood 10 + 20 -> one group 30 (GDD AC 5); wood and stone exit as separate groups, wood first (GDD AC 6).
- [ ] Batcher 64 with 70 internal emits 64, keeps 6 (GDD AC 7); batchSize > capacity warns `BatchExceedsCapacity` (GDD AC 8).
- [ ] Internal items travel with drop; all-outputs-blocked stalls upstream `DeviceFull`.

## QA Test Cases

- GDD AC 1-8 verbatim

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Devices/GroupDeviceTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: conveyor 002; machine-processing 002; tech-unlock 001 (availability)
- Unlocks: none
