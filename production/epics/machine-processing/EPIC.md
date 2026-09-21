# Epic: Machine Processing

> **Layer**: Feature
> **GDD**: design/gdd/machine-processing.md
> **Architecture Module**: MachineSystem, RecipeMatcher (Equilibrium.Simulation); machine state sprites (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: MEDIUM (content pipeline for recipes)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories machine-processing`

## Overview

Implement machines that reserve recipe inputs, integrate received mechanical power into work, complete batches atomically, hold pending output when blocked, expose four top-level states with composable ReasonCodes, support cancel/irreversible rules, recipe selection by stable order or pin, `tau_req` demand while Working, and persistent progress across removal. Includes Tier 1 recipe content (log -> plank on the saw) and state sprite switching. Replaces the prototype `Machine.prefab` behavior.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | Reservation, progress, output as transactions | LOW |
| ADR-0003: Item Identity | RecipeDef schema; Tag inputs | MEDIUM |
| ADR-0004: World Data Model | Machine containers and ports | LOW |
| ADR-0006: UI Technology Stack | State + ReasonCode display contract | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-mach-001 | Data-driven recipe schema | ADR-0003 |
| TR-mach-002 | Reserve at start; consume on completion; cancel rules | ADR-0001, ADR-0004 |
| TR-mach-003 | Work integration; one completion per tick; atomic batch output | ADR-0001 |
| TR-mach-004 | Four states + ReasonCodes visible | ADR-0006 |

## Definition of Done

- machine-processing acceptance criteria as EditMode tests.
- Saw with engine and belts runs end to end in `Main.unity` (PlayMode).

## Next Step

Run `/create-stories machine-processing`.
