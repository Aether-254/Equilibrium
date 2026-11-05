# Epic: Tutorial and Tasks

> **Layer**: Polish
> **GDD**: design/gdd/tutorial-task.md
> **Architecture Module**: TaskEvaluator (Equilibrium.Simulation); TaskPanel, PonderViewer (Equilibrium.UI); tutorial scenes (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: Vertical Slice
> **Engine Risk**: HIGH (UI Toolkit); MEDIUM (scene management for tutorial levels)
> **Manifest Version**: 2026-09-20.1
> **Stories**: 2 (table below)

## Overview

Implement metric-predicate tasks with windowed conditions, knowledge/tool/opportunity rewards with validator-enforced bans on multipliers and repeatable income, bounded refunds, task panel and tracker, Ponder demos running the real simulation on a throwaway world, standalone tutorial levels, and cross-form lesson tracking. Task content for Tier 1 is authored as data alongside.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | Metrics from committed events; Ponder reuses the engine | LOW |
| ADR-0003: Item Identity | Task/reward data and validation codes | MEDIUM |
| ADR-0006: UI Technology Stack | Task panel, Ponder viewer | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-task-001 | Windowed metric predicates; reward types; forbidden rewards | ADR-0001, ADR-0003 |

## Definition of Done

- tutorial-task acceptance criteria as EditMode tests (evaluator) and PlayMode tests (panel, Ponder isolation).

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | Task evaluator, rewards validation, and task panel | Logic | Ready |
| 002 | Ponder demos and standalone tutorial levels | Integration | Ready |

## Next Step

Run `/story-readiness production/epics/tutorial-task/story-001-task-evaluator-and-panel.md` then `/dev-story`.
