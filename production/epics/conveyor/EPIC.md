# Epic: Conveyor

> **Layer**: Feature
> **GDD**: design/gdd/conveyor.md
> **Architecture Module**: ConveyorSystem (Equilibrium.Simulation); belt rendering + group interpolation (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: MEDIUM (sprite atlas / SRP Batcher on URP 17)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories conveyor`

## Overview

Implement belts as one-slot containers with whole-group hops, dependency-ordered same-tick chaining, no belt-to-belt merge or split, per-tick stall derivation with enumerated causes, partial delivery to containers via accessors, and the accessor merge exception. Includes belt block definitions, the presentation of sliding groups and animated chevrons, and removal of the prototype trigger-collider `Conveyor` component.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | Move transactions with dependencies and priorities | LOW |
| ADR-0004: World Data Model | Belt block def; cell container; ingress bits | LOW |
| ADR-0007: Simulation/Presentation Split | No physics; interpolation; atlas | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-conv-001 | One-slot cap 3000; one direction; segments >= 2; no physics | ADR-0004, ADR-0007 |
| TR-conv-002 | Whole-group hop; enter vacating cell same tick; never merge/split | ADR-0001 |
| TR-conv-003 | Stall derived per tick with causes | ADR-0001 |
| TR-conv-004 | Partial delivery via accessor; remainder stays and stalls | ADR-0001, ADR-0004 |

## Definition of Done

- conveyor acceptance criteria as EditMode tests; 2k-cell benchmark under 1 ms.
- PlayMode: group sprite position equals committed cell at each tick boundary.
- Prototype `Conveyor.cs` and `Conveyor.prefab` trigger collider removed.

## Next Step

Run `/create-stories conveyor`.
