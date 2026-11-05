# Story 002: Ponder demos and standalone tutorial levels

> **Epic**: Tutorial and Tasks
> **Status**: Ready
> **Layer**: Polish
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tutorial-task.md`
**Requirement**: `TR-task-001`

**Governing ADRs**: ADR-0001 (primary), ADR-0007, ADR-0006
**ADR Decision Summary**: Ponder runs the real `TickEngine` on a throwaway world instance; tutorial levels are scenes with fixed starts, never touching saves.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM (additive scene loading)

## Acceptance Criteria

- [ ] Ponder viewer opens from a ReasonCode suggestion without changing the main save (GDD AC 5); scrubbable timeline with step captions.
- [ ] Tutorial level reset returns to fixed start; no save data changes (GDD AC 8).
- [ ] Lesson completed in a tutorial level marks the Ponder seen and collapses the main-save task hint (GDD AC 6).
- [ ] Ponder scripts addressable by id from ReasonCodes, tooltips, tasks.

## Out of Scope

- Task content authoring beyond Tier 1 samples

## QA Test Cases

- PlayMode: open Ponder, run 30 ticks, close -> main world tick index unchanged
- PlayMode: tutorial reset -> initial state hash equal

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/PlayMode/Tasks/PonderIsolationTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; build-hud 001
- Unlocks: none
