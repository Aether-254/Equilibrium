# Story 004: Market screen and world-creation market mode selector

> **Epic**: Market Interface
> **Status**: Ready
> **Layer**: Feature
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/market-interface.md` (UI Requirements)
**Requirement**: `TR-mkt-001`, `TR-mkt-004`

**Governing ADRs**: ADR-0006 (primary), ADR-0008
**ADR Decision Summary**: UI Toolkit screen bound to a view model updated on market events; permanent-choice warning at world creation.

**Engine**: Unity 6000.6.1f1 | **Risk**: HIGH (UI Toolkit)

**Control Manifest Rules (UI)**:
- Required: strings from string table; keyboard-dismissible dialogs; text scaling.

## Acceptance Criteria

- [ ] Market screen lists tradeable items with bid, ask, window cap remaining, queued escrow; currency balance; Sell/Buy with quantity; warehouse view (IP-10 slot list).
- [ ] World creation: market mode selector (LocalNpc / RemoteServer) with a permanence warning; RemoteServer disabled with "not available" text in MVP.
- [ ] LocalNpc world offers no server-switch control (GDD AC 6).
- [ ] Offline banner shows when a remote backend reports Offline (stub path testable via fake).
- [ ] Meets accessibility rules 1, 3, 6, 7.

## Implementation Notes

UX spec for the market screen is not yet authored; follow interaction-patterns IP-02, IP-07, IP-09, IP-10 and the inspection panel chrome. Author `design/ux/market.md` as part of this story (short).

## Out of Scope

- Remote client (deferred)

## QA Test Cases

- Manual: sell 100 planks over cap -> screen shows 50 settled, 50 queued; next window clears
- Manual: buy with insufficient funds -> rejection message
- Manual: world creation warning text visible; RemoteServer disabled

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/market-screen-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 003; machine-inspection-ui 001 (UITK shell)
- Unlocks: none
