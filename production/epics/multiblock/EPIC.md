# Epic: Multiblock Structures

> **Layer**: Feature
> **GDD**: design/gdd/multiblock.md
> **Architecture Module**: StructureDetector, MultiblockSystem (Equilibrium.Simulation)
> **Status**: Ready
> **Priority**: Vertical Slice
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories multiblock`

## Overview

Implement Controller/Slave structures: Type I pattern detection on block update with automatic formation and invalidation (structure data cleared, parts kept), Type II multi-cell machines with single drop and anchor rules, ports on slaves routing to controller containers, Frozen state when the controller chunk is unavailable, and the carry/removal interaction rules. Chunk loading policy waits on the map-model ADR; the state machine is implemented regardless.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | controllerCoord block attribute; footprints | LOW |
| ADR-0005: Save Format | Structure id and frozen flag persistence | MEDIUM |
| ADR-0001: Tick Transaction Engine | Formation/invalidation as structural transactions | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-multi-001 | Type I/II formation, invalidation, single drop | ADR-0004 |
| TR-multi-002 | Frozen never treated as removed | ADR-0004, ADR-0005 |

## Definition of Done

- multiblock acceptance criteria as EditMode tests.

## Next Step

Run `/create-stories multiblock`.
