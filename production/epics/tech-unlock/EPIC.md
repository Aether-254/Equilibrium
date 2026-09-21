# Epic: Tech Unlock

> **Layer**: Feature
> **GDD**: design/gdd/tech-unlock.md
> **Architecture Module**: UnlockEvaluator (Equilibrium.Simulation); unlock graph screen (Equilibrium.UI)
> **Status**: Ready
> **Priority**: Alpha
> **Engine Risk**: HIGH (custom VisualElement graph)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories tech-unlock`

## Overview

Implement data-driven unlock nodes with any-of condition groups (metrics, task completion, licenses, first-time phenomena), depth-tag and non-task-path validation, latching per save, tier gates, teaser visibility, and the unlock graph screen. Tier 1 node set authored as data.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0003: Item Identity | UnlockNodeDef; validator codes | MEDIUM |
| ADR-0005: Save Format | Unlocked set persistence | MEDIUM |
| ADR-0001: Tick Transaction Engine | Phenomenon events | LOW |
| ADR-0006: UI Technology Stack | Graph screen | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-unlk-001 | No research; condition groups; depth tag; non-task path; latched | ADR-0003, ADR-0005 |

## Definition of Done

- tech-unlock acceptance criteria as EditMode tests; graph screen PlayMode smoke.

## Next Step

Run `/create-stories tech-unlock`.
