# Story 002: Stall derivation and partial delivery through accessors

> **Epic**: Conveyor
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-04

## Context

**GDD**: `design/gdd/conveyor.md`
**Requirement**: `TR-conv-003`, `TR-conv-004`

**Governing ADRs**: ADR-0001 (primary), ADR-0004
**ADR Decision Summary**: stall is derived from this tick's validation results; partial acceptance only for belt -> container via accessor.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: stall computed from current-tick validation, never last tick's display.

## Acceptance Criteria

- [x] Belt end facing a chest with no accessor -> Stalled `NoReceivingPort`, chest unchanged (GDD AC 3).
- [x] Belt end with accessor into chest 199/200 and cell wood x2 -> chest 200, cell wood x1, Stalled `DownstreamCapacity` (GDD AC 4).
- [ ] Each enumerated cause maps to a ReasonCode: `NoReceivingPort`, `TargetInvalid` (accessor to air/non-container), `DownstreamCapacity`, `PartialAccepted`, `MachineNotAccepting`, `ItemNotAccepted`, `TargetErrored`, `IngressBlocked`.
- [ ] Stall state per cell is available on `IWorldView` for the tick and emitted as an event for the ledger and HUD.
- [ ] 2,000 belt cells with groups processed under 1 ms (GDD AC 8).

## Implementation Notes

After Resolve, `ConveyorSystem` receives rejection reasons for its transactions and writes a per-cell `StallReason` into a simulation-side transient table (cleared at snapshot). Partial delivery: the accessor's belt->container transaction is a split transaction (delivered part + remainder stays); conveyor only needs to observe the remainder.

## Out of Scope

- Accessor rules themselves (item-io-accessor epic)

## QA Test Cases

- GDD AC 3, 4, 8
- Cause table: build one scenario per ReasonCode

## Test Evidence

Current-tick stall, partial delivery, restoration after capacity becomes available, ingress rejection
and machine admission-policy rejection are covered by `Assets/Tests/EditMode/Logistics/LogisticsTests.cs`.
Committed status is currently read through `LogisticsSystem.BeltResult`, not an added `IWorldView` member.
The Editor workbench displays these results. Full HUD/ledger wiring and under-1-ms steady-state evidence
remain open. See `production/qa/evidence/logistics-runtime-evidence.md`.

**Story Type**: Logic
**Evidence**: `Assets/Tests/EditMode/Logistics/LogisticsTests.cs`; `production/qa/evidence/logistics-runtime-evidence.md`
**Status**: bounded runtime coverage recorded; detailed cause/UI/performance integration remains open

## Dependencies

- Depends on: Story 001; item-io-accessor 001
- Unlocks: economy-ledger 002; build-hud markers
