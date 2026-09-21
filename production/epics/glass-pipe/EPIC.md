# Epic: Glass Pipe

> **Layer**: Feature
> **GDD**: design/gdd/glass-pipe.md
> **Architecture Module**: PipeContents tables, PipeFlowSystem (Equilibrium.Simulation); pipe mask material/shader (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: Alpha
> **Engine Risk**: HIGH (URP 17 shader/material work; Render Graph if a custom pass is needed)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories glass-pipe`

## Overview

Implement the fluid carrier's binding content schema (components with SubstanceId, amount, phase fraction; T, P, volumes), explicit pipe connectivity with blocker support and the two-channel bridge, the simplified Alpha pressure-equalization flow, and the mask-driven visual: full-mask liquid and gas layers, equal-area stable-ordered component regions, alpha by amount, no per-fill frames. Uses the existing `Assets/Art/GlassPipe` masks; missing topology art is authored by the creator, not generated.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | PipeContents tables; ingress bits | LOW |
| ADR-0005: Save Format | Component list persistence, forward-compatible | MEDIUM |
| ADR-0007: Simulation/Presentation Split | Shader-driven regions; Render Graph if custom pass | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-pipe-001 | Component schema; never single-phase constraint; persisted | ADR-0004, ADR-0005 |
| TR-pipe-002 | Full-mask layers; equal-area regions; alpha by amount; visuals never decide physics | ADR-0007 |

## Definition of Done

- glass-pipe acceptance criteria: schema and flow as EditMode tests; visual rules as PlayMode screenshot evidence in `production/qa/evidence/`.

## Next Step

Run `/create-stories glass-pipe`.
