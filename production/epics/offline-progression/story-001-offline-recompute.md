# Story 001: Offline recompute with method selection, equivalence tests, and report

> **Epic**: Offline Progression
> **Status**: Blocked
> **Blocked reason**: Requires an Accepted ADR selecting the recompute method and event-predictor interface (architecture.md open question; offline-progression GDD OQ1). Run `/architecture-decision offline-recompute-method` first.
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/offline-progression.md`
**Requirement**: `TR-off-001`

**Governing ADRs**: ADR-0001 (primary), ADR-0002, ADR-0005, ADR-0008; pending offline-recompute ADR

## Acceptance Criteria

- [ ] Reference factory 1 h offline: full replay vs event-driven agree within 1 batch/machine; all constraints satisfied (GDD AC 1).
- [ ] Chest fills at 400 s -> OutputFull from 400 s, produced exactly the space (GDD AC 2); fuel for 30 min over 2 h -> NoFuel after 30 min (GDD AC 3).
- [ ] 10 h away, cap 8 h, factor 0 -> 8 h applied, 2 h discarded (GDD AC 4); market cap escrows excess (GDD AC 5).
- [ ] Negative clock delta -> nothing applied, logged (GDD AC 6); 500 machines / 8 h within `computeBudgetMs` (GDD AC 7).
- [ ] Presence-required tasks not completed; metric unlocks latch (GDD AC 8).
- [ ] Offline report UI.

## QA Test Cases

- GDD AC 1-8 verbatim

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Offline/RecomputeEquivalenceTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: offline-recompute ADR; save-load 003; market-interface 003; machine-processing 002; mechanical-power 002
- Unlocks: none
