# Story 001: UI Toolkit shell, data-binding spike, and inspection status bar

> **Epic**: Machine Inspection UI
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/machine-inspection-ui.md`
**UX Spec**: `design/ux/machine-inspection.md`
**Requirement**: `TR-insp-001`

**Governing ADRs**: ADR-0006 (primary), ADR-0007
**ADR Decision Summary**: one `UIDocument` root; UXML templates; view models updated on `CommittedBatch`/selection; runtime data binding with manual fallback.

**Engine**: Unity 6000.6.1f1 | **Risk**: HIGH
**Engine Notes**: verify UI Toolkit runtime `DataBinding` / `[CreateProperty]` on the installed `com.unity.modules.uielements`; if unavailable or slow, implement `IUiPresenter` manual updates behind the same view model.

**Control Manifest Rules (UI)**:
- Required: UITK only; tick-rate updates; string table keys; status bar persistent; 1 ms budget.

## Acceptance Criteria

- [ ] `HudRoot.uxml` + `UIDocument` in `Main.unity`; `InspectionPanel.uxml` template instantiated on block click, retargeted on another click, closed on Esc/close/placement mode/removal (GDD AC 6, 7, 8).
- [ ] Data-binding spike result recorded: binding API works at 1 Hz for 50 fields under 0.2 ms, or fallback adopted.
- [ ] Status bar shows state color + glyph + label + top ReasonCode by the registered priority; visible on every tab (GDD AC 1).
- [ ] Tabs Overview/Logistics/Mechanics/Economy with last-tab persistence per session; per-block-type tab visibility (chest: Overview + Logistics) (GDD AC 5).
- [ ] Narrow layout: bottom sheet with icon tabs.
- [ ] Accessibility: text scales 100-150% with reflow; focus order; Esc closes.

## Implementation Notes

Follow the UX spec layout (360 px dock). Status colors from art bible section 3. Strings via a `StringTable` stub keyed by id until localization tooling exists.

## Out of Scope

- Story 002: tab contents and intents

## QA Test Cases

- Manual: open on saw -> status bar; switch tabs -> bar remains; click chest -> two tabs
- PlayMode: panel closes when Belt mode is entered
- Benchmark: UI frame cost with panel open

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/inspection-shell-evidence.md`; binding spike note
**Status**: [ ] Not yet created

## Dependencies

- Depends on: tick-simulation 005; machine-processing 002
- Unlocks: Story 002; item-io-accessor 002; market-interface 004; build-hud
