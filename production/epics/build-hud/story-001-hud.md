# Story 001: Mode sidebar, economy strip, previews, markers, and dialogs

> **Epic**: Build HUD
> **Status**: In Progress — basic runtime toolbar and logistics inspection
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: L (split 1a sidebar+strip, 1b previews+markers, 1c dialogs at sprint planning)
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/build-hud.md`
**UX Spec**: `design/ux/hud.md`
**Requirement**: `TR-hud-001`

**Governing ADRs**: ADR-0006 (primary), ADR-0007
**ADR Decision Summary**: UITK for sidebar/strip/dialogs; pooled sprites for world markers.

**Engine**: Unity 6000.6.1f1 | **Risk**: HIGH

## Acceptance Criteria

- [ ] Sidebar per UX spec with hotkey labels; active mode highlight; entering a mode closes inspection (GDD AC 1).
- [ ] Belt objective toggle and modifier chips visible in Belt mode.
- [ ] Preview tints and reason labels (`SegmentTooShort` etc.) (GDD AC 2); affected-network highlight and summary chip (GDD AC 3); proposed blocker icons vs open-end glyphs by stock (GDD AC 4).
- [ ] Jam glyph after `jamMarkerDelayTicks` consecutive stalls (GDD AC 5); open-end toggle.
- [ ] Four dialogs (IP-05) with Esc/Enter and "Select whole structure" (GDD AC 6); premise re-validation.
- [ ] Deep-analysis button on budget error with commit disabled (GDD AC 7).
- [ ] Economy strip matches ledger values (GDD AC 8).
- [ ] Text-field focus suppresses hotkeys; markers culled to viewport without per-frame allocation.

## Implementation Notes

2026-11-05: Main has a UI Toolkit toolbar, local warehouse stock, pause/step, pending counts,
mode hints, scrolling inspection and input focus protection. This is not the economy strip, complete
machine inspector, routing controls or confirmation-dialog set. Real Game View inspection is recorded
in `production/qa/evidence/factory-scene-runtime-evidence.md`.

Markers are `SpriteRenderer` pools keyed by cell in Presentation; the HUD presenter reads the conveyor stall table and Editing preview model each frame (preview is editor state, not simulation).

## Out of Scope

- Pathfinding logic (placement-editing 003)

## QA Test Cases

- Manual checks per GDD AC 1-8 with screenshots
- PlayMode: focus suppression; jam delay

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/hud-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: machine-inspection-ui 001; placement-editing 002; economy-ledger 001; flow-blocker 001
- Unlocks: tutorial-task 001 (highlight hooks)
