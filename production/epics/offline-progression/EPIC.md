# Epic: Offline Progression

> **Layer**: Feature
> **GDD**: design/gdd/offline-progression.md
> **Architecture Module**: OfflineRecompute (Equilibrium.Simulation, reusing TickEngine); offline report (Equilibrium.UI)
> **Status**: Ready
> **Priority**: Full Vision
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: 1 (table below)

## Overview

Implement capped offline advancement using the same engine and constraints: full replay for short spans, event-driven and piecewise steady-state methods with per-subsystem event predictors for long spans, fresh tie-break entropy, market window settlement, remote receipt application, equivalence tests against replay, compute budget fallback, and the offline report. Requires a method-selection ADR before implementation (architecture open question).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | Engine reuse for recompute | LOW |
| ADR-0002: Tie-Break Entropy | Fresh entropy offline | LOW |
| ADR-0005: Save Format | lastSaveWallTime | MEDIUM |
| ADR-0008: Market Backend Interface | AdvanceWindow; receipts | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-off-001 | Capped, constraint-equivalent offline advance with report | ADR-0001, ADR-0002, ADR-0005, ADR-0008 |

## Definition of Done

- offline-progression acceptance criteria as EditMode tests, including replay-vs-event equivalence on reference factories.

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | Offline recompute with method selection, equivalence tests, and report | Logic | Blocked |

## Next Step

Run `/story-readiness production/epics/offline-progression/story-001-offline-recompute.md` then `/dev-story`.
