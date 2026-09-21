# Epic: Container

> **Layer**: Core
> **GDD**: design/gdd/container.md
> **Architecture Module**: SlotTable, ContainerOps, Capacity (Equilibrium.Simulation)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories container`

## Overview

Implement multi-slot containers on the SoA world: slot storage with effective capacity, allow/deny filters by ItemId and Tag, deterministic insertion with single auto-tidy, `acceptable`/`available` snapshot queries, reservation flags, string-key port binding resolution, overflow container on content shrink, and the plain-container rule that belts never connect directly. This replaces the prototype `ItemStack`/`ItemContainer`/`ContainerTransfer` classes; their semantics are ported into transaction validators and writers.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | SlotTable layout; Capacity.Effective; key resolution | LOW |
| ADR-0001: Tick Transaction Engine | Insert/extract/move as validated transactions | LOW |
| ADR-0003: Item Identity | maxStack and Tag membership lookups | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-cont-001 | effectiveCapacity = min(slot.capacity, maxStack) | ADR-0004 |
| TR-cont-002 | Filters by ItemId/Tag; deny beats allow | ADR-0004, ADR-0003 |
| TR-cont-003 | Deterministic insertion; one auto-tidy per transaction | ADR-0004 |
| TR-cont-004 | String-key binding; no indices in saves/content | ADR-0004, ADR-0005 |
| TR-cont-005 | Plain containers exchange only via accessors | ADR-0004 |

## Definition of Done

- container acceptance criteria as EditMode tests (including determinism and 0.5 ms benchmark).
- Prototype `ItemStack`/`ItemContainer`/`ContainerTransfer` removed from `Assets/Script/`.

## Next Step

Run `/create-stories container`.
