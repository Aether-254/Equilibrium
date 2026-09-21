# Epic: Mechanical Power

> **Layer**: Core
> **GDD**: design/gdd/mechanical-power.md
> **Architecture Module**: MechNetworks, MechanicalSystem (Equilibrium.Simulation); shaft animation consumer (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: LOW (simulation); MEDIUM for the baked-sprite animation path
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories mechanical-power`

## Overview

Implement the Tier 1 fidelity contract: union-find mechanical networks over rotation ports, per-segment speed via gear ratios, path efficiency, torque demand aggregation, binary stall, source conflict fault, fuel engine consumption proportional to delivered power, and delivered power per machine. Includes gear/shaft/engine block definitions and the presentation hook that drives the existing baked shaft frames from committed omega (replacing `ShaftSpriteVisual`'s standalone timer).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | MechNetworks union-find; rotation ports | LOW |
| ADR-0001: Tick Transaction Engine | Network solve in Collect; power transactions | LOW |
| ADR-0007: Simulation/Presentation Split | Baked atlas frame stepping from omega | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-mech-001 | Tier 1 quantities and relationships; deferred set | ADR-0001, ADR-0004 |
| TR-mech-002 | Network topology; one speed per segment; SourceConflict | ADR-0004 |
| TR-mech-003 | Fuel engine consumption; NoFuel | ADR-0001 |
| TR-mech-004 | 5k-element solve under 1 ms | ADR-0004 |

## Definition of Done

- mechanical-power acceptance criteria as EditMode tests, benchmark for TR-mech-004.
- Shaft sprite in `Main.unity` rotates from committed omega in PlayMode test.

## Next Step

Run `/create-stories mechanical-power`.
