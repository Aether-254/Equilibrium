# Story 001: Blocker install/remove, ingress semantics, and placement modes

> **Epic**: Flow Blocker
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/flow-blocker.md`
**Requirement**: `TR-blk-001`, `TR-blk-002`

**Governing ADRs**: ADR-0004 (primary), ADR-0001, ADR-0006
**ADR Decision Summary**: ingress bit per side; install/remove as structural transactions effective next tick; join-network confirmation before removal.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

## Acceptance Criteria

- [ ] Belt B with N-side blocker, belt A north facing S: A stalls `IngressBlocked`, B unchanged; blocker on E side: A's group enters (GDD AC 1-2).
- [ ] Install on tick t after a validated entry: entry commits on t; blocked from t+1 (GDD AC 3).
- [ ] Removal joining two working networks: `wouldJoin` true -> confirmation required before transaction (GDD AC 4).
- [ ] Four blockers per cell max; inspection shows all sides (GDD AC 5).
- [ ] Drag-install across 5 cells with 2 pre-existing: 3 consumed, 5 blocked (GDD AC 6); removal refunds one item (GDD AC 7).
- [ ] Pipe side blocker prevents gas connection (GDD AC 8, testable once glass-pipe exists; stub carrier now).
- [ ] Carrier check: installing on a machine or chest -> `CarrierUnsupported`.

## Implementation Notes

Blocker mode in Editing (`BlockerTool`) with side hit-testing from cursor position within the cell; two overlay skins from the atlas. Auto-placement hook consumed by placement-editing 003.

## Out of Scope

- HUD dialog visuals (build-hud)

## QA Test Cases

- GDD AC 1-7 verbatim (8 deferred to glass-pipe)
- `CarrierUnsupported`

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Blocker/BlockerTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: grid-world 002; conveyor 001; placement-editing 001
- Unlocks: placement-editing 003; glass-pipe 001
