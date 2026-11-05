# Story 001: Task evaluator, rewards validation, and task panel

> **Epic**: Tutorial and Tasks
> **Status**: Ready
> **Layer**: Polish
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tutorial-task.md`
**Requirement**: `TR-task-001`

**Governing ADRs**: ADR-0001 (primary), ADR-0003, ADR-0006

## Acceptance Criteria

- [ ] Windowed metric predicates evaluated each tick; sustained-truth windows restart on dips (GDD AC 1-2).
- [ ] Reward validator rejects multipliers (`ForbiddenRewardType`) (GDD AC 3); bounded one-time refund (GDD AC 4).
- [ ] Task states Locked/Available/Active/Complete with abandon; rewards granted once; pending grants for missing content.
- [ ] Tasks disabled -> all Tier 1 content reachable via tech-unlock non-task paths (GDD AC 7).
- [ ] Task panel and tracker overlay (IP-02, IP-11) per interaction patterns.

## Out of Scope

- Story 002: Ponder demos and tutorial levels

## QA Test Cases

- GDD AC 1-4, 7 verbatim

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Tasks/TaskEvaluatorTests.cs`; panel evidence doc
**Status**: [ ] Not yet created

## Dependencies

- Depends on: economy-ledger 002; tech-unlock 001; machine-inspection-ui 001
- Unlocks: Story 002
