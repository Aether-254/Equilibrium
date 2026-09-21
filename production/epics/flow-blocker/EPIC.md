# Epic: Flow Blocker

> **Layer**: Feature
> **GDD**: design/gdd/flow-blocker.md
> **Architecture Module**: BlockerSystem over EdgeTable ingress bits (Equilibrium.Simulation); blocker placement mode (Equilibrium.Editing); overlay skins (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: Vertical Slice
> **Engine Risk**: LOW
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories flow-blocker`

## Overview

Implement side-attached blockers that disable one ingress edge without neighbor rediscovery, effective next tick, refunded on removal, with single-click, drag, and pathfinder auto-placement modes, warehouse stock consumption, network-join confirmation before removal, and four-side status in inspection. Two visual skins for belt and pipe carriers.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: World Data Model | Ingress bits per side | LOW |
| ADR-0001: Tick Transaction Engine | Install/remove as structural transactions | LOW |
| ADR-0006: UI Technology Stack | Join warning dialog; status display | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-blk-001 | Ingress edge disable; not a filter; next-tick effect | ADR-0004, ADR-0001 |
| TR-blk-002 | Join-networks confirmation with upstream/downstream shown | ADR-0006 |

## Definition of Done

- flow-blocker acceptance criteria as EditMode tests; dialog PlayMode test.

## Next Step

Run `/create-stories flow-blocker`.
