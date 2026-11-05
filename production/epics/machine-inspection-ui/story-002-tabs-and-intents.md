# Story 002: Tab contents, bottleneck line, and intents

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
**Requirement**: `TR-insp-001`, `TR-insp-002`

**Governing ADRs**: ADR-0006
**ADR Decision Summary**: view models fed by presenters from machine-processing, mechanical-power, container, economy-ledger data; intents go through Editing/Simulation, not direct.

**Engine**: Unity 6000.6.1f1 | **Risk**: HIGH

## Acceptance Criteria

- [ ] Overview: state + all ReasonCodes, recipe with pin dropdown, progress ring with ticks remaining, bottleneck line = argmax loss with suggestion text and Ponder link placeholder (GDD AC 2).
- [ ] Logistics: containers grouped with slot lists (IP-10), blocked/starved tick counters, four-side blocker indicator, accessor controls host slot (GDD AC 3).
- [ ] Mechanics: omega (RPM), tau_req, tau_avail, load % bar with thresholds, eta_path, theoretical vs actual rate, utilization; engine fields (GDD AC 4).
- [ ] Economy: unit cost, revenue/cost/profit per window, losses by reason; "collecting" placeholders when empty.
- [ ] Intents: pin recipe, cancel work (irreversible -> IP-05 dialog), edits show pending glyph until commit (GDD AC 9).
- [ ] Undefined fields for a block type are hidden, not zero.

## Implementation Notes

Suggestion text table is data (`SuggestionByReason` string table). Load % thresholds colors per UX spec. Slot lists virtualized with `ListView`.

## Out of Scope

- Accessor control internals (item-io-accessor 002)
- Trend charts (VS)

## QA Test Cases

- Manual: losses {Stalled 32.4, OutputFull 5.1} -> bottleneck text names Stalled
- Manual: engine Mechanics shows fuel/tick and P_deliv
- PlayMode: pin recipe -> transaction next tick

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/inspection-tabs-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; economy-ledger 002; mechanical-power 002
- Unlocks: none
