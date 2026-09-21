# Epic: Group Processing Devices

> **Layer**: Feature
> **GDD**: design/gdd/group-processing-devices.md
> **Architecture Module**: DeviceSystem (Equilibrium.Simulation)
> **Status**: Ready
> **Priority**: Alpha
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories group-processing-devices`

## Overview

Implement Splitter (RoundRobin, Divide, Ratio), Combiner (same-ItemId merge in port order), and Batcher (exact batchSize, optional flush) as the only regrouping path, with internal containers, whole-group belt semantics on their ports, and inspection controls. Single-device-with-modes versus three blocks is decided at story time (open question 1).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | One transaction per port per tick | LOW |
| ADR-0004: World Data Model | Device block defs and containers | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-gpd-001 | Splitter/Combiner/Batcher semantics | ADR-0001, ADR-0004 |

## Definition of Done

- group-processing-devices acceptance criteria as EditMode tests.

## Next Step

Run `/create-stories group-processing-devices`.
