# Epic: Build HUD

> **Layer**: Presentation
> **GDD**: design/gdd/build-hud.md
> **UX Spec**: design/ux/hud.md
> **Architecture Module**: HudRoot, presenters, dialogs (Equilibrium.UI); world marker overlays (Equilibrium.Presentation)
> **Status**: Ready
> **Priority**: Vertical Slice (MVP subset: sidebar, economy strip, preview tints, basic reasons)
> **Engine Risk**: HIGH (UI Toolkit); MEDIUM (sprite overlays)
> **Manifest Version**: 2026-09-20.1
> **Stories**: 1 (table below)

## Overview

Implement the always-on building interface: mode sidebar with remappable hotkeys, belt objective toggle, placement and path previews with validity and reasons, proposed blockers and open-end glyphs, affected-network highlights, the four confirmation dialogs, jam markers, the economy strip, modifier chips, and the deep-analysis request. Screen elements are UI Toolkit; world markers are pooled sprites.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: UI Technology Stack | UITK for screen UI | HIGH |
| ADR-0007: Simulation/Presentation Split | Sprite markers; single atlas | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-hud-001 | Sidebar, previews, highlights, dialogs, economy strip; UITK + sprites | ADR-0006, ADR-0007 |

## Definition of Done

- build-hud acceptance criteria as PlayMode tests.
- Text-field focus suppresses hotkeys; dialogs keyboard-dismissible.

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | Mode sidebar, economy strip, previews, markers, and dialogs | UI | Ready |

## Next Step

Run `/story-readiness production/epics/build-hud/story-001-hud.md` then `/dev-story`.
