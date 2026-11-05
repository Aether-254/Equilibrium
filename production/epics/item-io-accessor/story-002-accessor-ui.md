# Story 002: Accessor overlay sprite and settings controls

> **Epic**: Item I/O Accessor
> **Status**: In Progress — runtime controls integrated; final arm art pending
> **Layer**: Feature
> **Type**: UI
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/item-io-accessor.md` (UI / Visual)
**UX Spec**: `design/ux/machine-inspection.md` Logistics tab (IP-06, IP-07)
**Requirement**: `TR-acc-001`

**Governing ADRs**: ADR-0006 (primary), ADR-0007
**ADR Decision Summary**: UI Toolkit controls bound to a view model; edits are intents that become next-tick transactions; overlay sprite in the atlas.

**Engine**: Unity 6000.6.1f1 | **Risk**: HIGH (UI Toolkit runtime binding)

**Control Manifest Rules (UI)**:
- Required: direction-worded options; pending marker until commit; hotkey suppression on focus.
- Forbidden: UI submitting transactions directly (goes through Editing intents).

## Acceptance Criteria

- [ ] Accessor arm sprite on the belt edge facing the target; arrow glyph shows resolved direction; red tint when Blocked; pulse on transfer.
- [ ] Inspection Logistics tab shows flow mode selector with the three direction-worded options, filter picker, quantity mode (Exact/UpTo/Keep with one-line semantics), amount field.
- [ ] Changing amount to 20 applies as a transaction next tick; field shows pending clock glyph until commit (GDD AC 9 of inspection).
- [ ] Warning shown when `amount > effectiveCapacity` for the filtered item.
- [ ] Last tick result and ReasonCode displayed.

## Implementation Notes

2026-11-05: `FactoryHud` exposes side, flow, quantity mode, amount and validated optional ItemId;
Apply submits a FactorySession intent for the next commit. Last-tick result and reason are shown.
The side marker is temporary text, not final arm art. Full tabbed inspection, per-field pending glyphs
and explicit effective-capacity warnings remain. See `production/qa/evidence/factory-scene-runtime-evidence.md`.

Depends on the inspection panel shell (machine-inspection-ui 001). If that story is not done, implement the controls as a standalone UXML template that the panel later hosts.

## Out of Scope

- Panel shell and tabs (machine-inspection-ui)

## QA Test Cases

- Manual: set Container->Belt, Keep 50 on a chest with 130 wood; belt receives 64 next tick; panel shows pending then committed
- Manual: Exact 100 with plank cap 64 -> warning visible
- PlayMode: intent -> transaction on next tick

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/accessor-settings-ui-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; machine-inspection-ui 001
- Unlocks: none
