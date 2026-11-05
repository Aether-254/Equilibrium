# Epic: Item I/O Accessor

> **Layer**: Feature
> **GDD**: design/gdd/item-io-accessor.md
> **Architecture Module**: AccessorSystem (Equilibrium.Simulation); accessor overlay sprite (Equilibrium.Presentation); settings UI (Equilibrium.UI)
> **Status**: In Progress
> **Priority**: MVP
> **Engine Risk**: LOW (simulation); HIGH for the UI Toolkit settings controls
> **Manifest Version**: 2026-09-20.1
> **Stories**: 2 (table below)

## Overview

Implement the belt attachment that moves items between plain containers (or accessor-required ports) and belt cells: three direction-worded flow modes with Auto resolution, single-ItemId filter, Exact/UpTo/Keep quantity formulas with amount 1..3000, stalled-only merge onto belts, default +1 priority over forward moves, and `TargetInvalid`/`ExactUnsatisfied`/`SourceEmpty` reason codes. Includes the inspection-panel settings controls per the machine-inspection UX spec. Replaces prototype `ItemTransferLink`/`ItemSource`/`StorageBox` behavior.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | One transaction per accessor per tick; priority | LOW |
| ADR-0004: World Data Model | Attachment facing; container queries | LOW |
| ADR-0006: UI Technology Stack | Settings controls in UI Toolkit | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-acc-001 | Direction-worded modes; Auto resolution | ADR-0004 |
| TR-acc-002 | Exact/UpTo/Keep formulas; amount bounds; no partial Exact | ADR-0001 |
| TR-acc-003 | Merge only onto stalled same-ItemId cell within capacity | ADR-0001, ADR-0004 |

## Definition of Done

- item-io-accessor acceptance criteria as EditMode tests.
- Settings edits apply as next-tick transactions (PlayMode UI test).
- Prototype `ItemTransferLink`, `ItemSource`, `StorageBox` removed.

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | Accessor attachment, flow modes, filter, and quantity formulas | Logic | In Progress |
| 002 | Accessor overlay sprite and settings controls | UI | Ready |

## Next Step

Integrate queued settings edits, save capture/hydration and world overlays on the verified atomic
logistics core. See `production/qa/evidence/logistics-runtime-evidence.md`.
