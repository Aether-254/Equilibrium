# Epic: Grid World

> **Layer**: Core
> **GDD**: design/gdd/grid-world.md
> **Architecture Module**: WorldGrid, BlockTable, PortTable, EdgeTable, IWorldView (Equilibrium.Simulation)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories grid-world`

## Overview

Implement the struct-of-arrays world: chunked cell grid, block instances with orientation and controller coordinate, port definitions with rotated facings, ingress-edge bits, the `connected()` predicate, and block/persistent attribute separation with drop semantics. Provides `IWorldView` snapshot accessors and the `IWorldWriter` operations for place, remove, rotate, and attribute updates. Tier 1 uses a fixed 64x64 map; chunk infrastructure exists but chunk loading policy is deferred to the map-model ADR.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | SoA tables; implicit grid adjacency; ingress bits; string keys | LOW |
| ADR-0001: Tick Transaction Engine | Structural transactions effective next tick | LOW |
| ADR-0005: Save Format | Block vs persistent attribute serialization | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-grid-001 | Strict 2D grid; one block per cell; four orientations | ADR-0004 |
| TR-grid-002 | Block attrs vanish; persistent attrs travel with drop and restore | ADR-0004, ADR-0005 |
| TR-grid-003 | Connections only via facing ports with ingress enabled; no proximity inference | ADR-0004 |
| TR-grid-004 | Rotation rotates all ports; default L-in R-out T-power B-byproduct | ADR-0004 |

## Definition of Done

- grid-world acceptance criteria as EditMode tests.
- Snapshot of 512x512 with 10 dirty chunks under 0.2 ms benchmark.
- Removal drop delivers persistent attributes; `WarehouseFull` failure path tested.

## Next Step

Run `/create-stories grid-world`.
