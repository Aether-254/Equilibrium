# Story 001: Unlock nodes, condition evaluation, validation, and graph screen

> **Epic**: Tech Unlock
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tech-unlock.md`
**Requirement**: `TR-unlk-001`

**Governing ADRs**: ADR-0003 (primary), ADR-0005, ADR-0001, ADR-0006

## Acceptance Criteria

- [ ] `UnlockNodeDef` with any-of groups of all-of predicates; depth tag required (`UnlockWithoutDepth`) and non-task path required (`NoNonTaskPath`) at content validation (GDD AC 2-3).
- [ ] First Stalled event in the main save unlocks gear 32 (GDD AC 1); tutorial-level events do not count (GDD AC 5).
- [ ] Load % 80 for 120 s unlocks the Mechanics trend capability (GDD AC 4).
- [ ] Unlocked set persists (GDD AC 6); gate_2 makes Tier 2 nodes Visible (GDD AC 7).
- [ ] Unlock graph screen: tier columns, node details with condition progress, teaser state.
- [ ] Tier 1 node data authored per GDD rule 9.

## QA Test Cases

- GDD AC 1-7 verbatim

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Unlock/UnlockEvaluatorTests.cs`; graph screen evidence doc
**Status**: [ ] Not yet created

## Dependencies

- Depends on: economy-ledger 002; item-registry 004; save-load 003; machine-inspection-ui 001
- Unlocks: tutorial-task 001
