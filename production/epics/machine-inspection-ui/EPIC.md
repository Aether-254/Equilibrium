# Epic: Machine Inspection UI

> **Layer**: Presentation
> **GDD**: design/gdd/machine-inspection-ui.md
> **UX Spec**: design/ux/machine-inspection.md
> **Architecture Module**: InspectionPanel, InspectionViewModel, presenters (Equilibrium.UI)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: HIGH (UI Toolkit runtime data binding on Unity 6.6 — spike first)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories machine-inspection-ui`

## Overview

Implement the UI Toolkit inspection panel: persistent status bar with prioritized ReasonCode, four tabs (Overview with bottleneck and suggestion text, Logistics slot lists and blocker status, Mechanics readouts, Economy figures), tick-rate view-model updates, block-type-specific tab visibility, and intents (pin recipe, cancel work, accessor settings) submitted as next-tick transactions. Follows the UX spec layout including the narrow bottom-sheet variant. First story is the data-binding spike with a manual-update fallback.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: UI Technology Stack | UI Toolkit; view models bound at tick rate | HIGH |
| ADR-0007: Simulation/Presentation Split | Reads CommittedBatch/IWorldView only | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-insp-001 | Status bar; four tabs; snapshot reads at tick rate | ADR-0006 |
| TR-insp-002 | Bottleneck = argmax loss over 60 s | ADR-0006 |

## Definition of Done

- machine-inspection-ui acceptance criteria as PlayMode tests.
- UI under 1 ms per frame with the panel open (benchmark).
- Accessibility rules 1, 3, 5, 6, 7 verified (color+glyph, scaling, focus, keyboard, sizes).

## Next Step

Run `/create-stories machine-inspection-ui`.
