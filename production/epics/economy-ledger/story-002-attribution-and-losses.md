# Story 002: Fuel apportionment, per-machine records, and loss attribution

> **Epic**: Economy Ledger
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/economy-ledger.md`
**Requirement**: `TR-ledg-002`

**Governing ADRs**: ADR-0001
**ADR Decision Summary**: attribution derived from machine state ticks, power delivered, fuel consumed, conveyor stall events.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

## Acceptance Criteria

- [ ] Engine fuel cost 40, machines received 300 J and 100 J -> attributed 30 and 10 (GDD AC 3).
- [ ] Machine stalled 18/60 ticks, theoretical 0.1 batch/s, batch value 18 -> `loss(Stalled) = 32.4` (GDD AC 4).
- [ ] Per-machine record per window: ticks by state and ReasonCode, completions, energy, inputs, outputs, attributed fuel, attributed revenue, profit contribution.
- [ ] Conveyor stalls attributed to the machine whose output they block, else "logistics: open end".
- [ ] Product with no quote -> loss shown in ticks, value "n/a".
- [ ] `bottleneck(m) = argmax loss(m, reason)` over 60 s exposed for the inspection Overview.

## Implementation Notes

Attribution needs the mechanical network membership per machine (from `IWorldView`) to map engine fuel events to machines by energy share within the window.

## Out of Scope

- UI (inspection, hud)

## QA Test Cases

- GDD AC 3, 4 verbatim
- Open-end attribution scenario
- Bottleneck argmax with two reasons

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Ledger/AttributionTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; machine-processing 002; mechanical-power 002; conveyor 002
- Unlocks: machine-inspection-ui 002
