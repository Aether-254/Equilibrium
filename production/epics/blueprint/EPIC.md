# Epic: Blueprint

> **Layer**: Feature
> **GDD**: design/gdd/blueprint.md
> **Architecture Module**: BlueprintCapture, BlueprintStamp, BuildOrder (Equilibrium.Editing); blueprint library UI (Equilibrium.UI)
> **Status**: Ready
> **Priority**: Alpha
> **Engine Risk**: MEDIUM
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories blueprint`

## Overview

Implement region capture into blueprint data, stamping with rotation that transforms orientations, ports, belt directions, blocker sides, accessor sides, and multiblock anchors, material policies (start-when-complete, incremental), build-order strategies respecting Controller and infrastructure prerequisites, per-ghost reservations, and cancel semantics. Builds on the ghost system from placement-editing.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | Occupancy and footprint transforms | LOW |
| ADR-0005: Save Format | Blueprint library and pending stamps | MEDIUM |
| ADR-0001: Tick Transaction Engine | Ghost/build transactions | LOW |
| ADR-0006: UI Technology Stack | Library and stamp controls | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-bp-001 | Capture/stamp/rotate; material policies; build orders | ADR-0004 |

## Definition of Done

- blueprint acceptance criteria as EditMode tests.

## Next Step

Run `/create-stories blueprint`.
