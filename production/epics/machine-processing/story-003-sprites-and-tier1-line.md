# Story 003: Machine state sprites and the Tier 1 end-to-end line

> **Epic**: Machine Processing
> **Status**: In Progress — first powered saw line; complete epic acceptance still pending
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/gdd/machine-processing.md` (Visual/Audio); `design/gdd/game-concept.md` MVP hypothesis
**Requirement**: `TR-mach-004`; integration of TR-mech, TR-conv, TR-acc

**Governing ADRs**: ADR-0007 (primary), ADR-0001
**ADR Decision Summary**: state sprites switch on `StateChanged` events; the main scene hosts the real simulation.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM

**Control Manifest Rules (Presentation)**:
- Forbidden: visuals inferring state.

## Acceptance Criteria

- [ ] Saw sprite set: Idle, Working loop (speed scaled by `P_recv / P_nominal`), Blocked, Starved; switches on committed state events.
- [ ] `Main.unity` contains a placed engine (fueled), shaft, 8:16 gear, saw, input chest + accessor + belt, output belt + accessor + chest; after 60 ticks planks exist in the output chest and the ledger receives completion events.
- [ ] Prototype `Machine.prefab` sprite-only behavior replaced by the new block def and renderer.
- [ ] Adding a second saw to the same segment stalls the network and both saws show Stalled sprites; inserting gear 32 recovers (demonstrates strategy one).
- [ ] Console has no errors during a 5-minute PlayMode run of the line.

## Implementation Notes

2026-11-05: Runtime subset implemented in ProcessingSystem, RecipeInputPlanner and
StraightMechanicalSolver, with Main placement/inspection integration. See
docs/architecture/machine-processing-runtime.md and
production/qa/evidence/machine-processing-evidence.md for exact coverage. Gears, full multi-source
networks, final animations, ledger events and performance acceptance remain pending.


This is the first full-loop integration in the main project (no slice). Build the scene with the placement tools if available, otherwise author it directly. Use provisional content values from the GDD tuning tables.

## Out of Scope

- Market sale of planks (market-interface)
- UI panel (machine-inspection-ui)

## QA Test Cases

- PlayMode: after 60 ticks, output chest plank count > 0
- PlayMode: second saw -> stall events; gear 32 -> recovery
- Manual: state sprites change; screenshots for Idle/Working/Blocked/Starved

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/PlayMode/MachineProcessing/Tier1LineTests.cs`; `production/qa/evidence/machine-sprites-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002; conveyor 003; item-io-accessor 001; mechanical-power 003
- Unlocks: market-interface 002 (sell what the line produces)
