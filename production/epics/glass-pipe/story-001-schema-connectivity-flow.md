# Story 001: Pipe content schema, connectivity, and Alpha flow

> **Epic**: Glass Pipe
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/glass-pipe.md`
**Requirement**: `TR-pipe-001`

**Governing ADRs**: ADR-0004 (primary), ADR-0005, ADR-0001
**Decision to make at story start**: fluid contents as Container extension vs parallel tables (GDD open question 1).

## Acceptance Criteria

- [ ] `PipeContents{components[]{SubstanceId, amount, phaseFraction}, T, P, liquidVolume, gasVolume}` persisted; loads into a build with reaction support without migration errors (GDD AC 7).
- [ ] Blocker on N prevents inflow from N (GDD AC 4); bridge channels stay separate (GDD AC 5).
- [ ] Two cells at P 2 and 1, k 0.5 -> 0.5 volume moves; substance conserved (GDD AC 6); overpressure clamps with `PipeOverpressure`.
- [ ] Duplicate SubstanceIds merge; out-of-range phase fractions clamp with warning.

## QA Test Cases

- GDD AC 4-7 verbatim

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Pipe/PipeContentsAndFlowTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: grid-world 002; flow-blocker 001; save-load 003
- Unlocks: Story 002
