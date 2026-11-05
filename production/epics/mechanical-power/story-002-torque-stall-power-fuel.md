# Story 002: Torque demand, efficiency, binary stall, delivered power, and fuel

> **Epic**: Mechanical Power
> **Status**: In Progress — first powered saw line; complete epic acceptance still pending
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/gdd/mechanical-power.md`
**Requirement**: `TR-mech-001`, `TR-mech-003`

**Governing ADRs**: ADR-0001 (primary), ADR-0004
**ADR Decision Summary**: network solve inside Collect on the snapshot; fuel consumption and power delivery as transactions.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: all numeric knobs from content (`omega_src`, `tau_max`, `eta_engine`, `eta_shaft`, `eta_gear`, `E_fuel`).

## Acceptance Criteria

- [ ] `tau_demand_src = Σ tau_req(m) / (r_path(m) * eta_path(m))`; `tau_deliv = min(demand, tau_max)`.
- [x] Engine (10 rad/s, 40 N*m) + one saw tau_req 30: Running, omega 10, `P_recv = 300 W`;
  one 99%-efficient shaft requires `P_src = 300 / 0.99 W`. Loss is counted once (creator 2026-11-05).
- [ ] Two saws on the same segment: demand 60 > 40 -> Stalled, omega 0, both `Stalled` (GDD AC 2).
- [ ] Insert 8:16 gear: demand 60/(2*0.95) = 31.6 -> Running, omega at saws 5 (GDD AC 3).
- [ ] 20-cell shaft at eta 0.99: P_recv = P_src * 0.99^20 within 1e-6 (GDD AC 4).
- [ ] Fuel: 1 wood (8000 J), P_deliv 200 W, eta_engine 0.25 -> consumed on tick 10; tick 11 `NoFuel`, omega 0 (GDD AC 5).
- [ ] Demand exactly equal to tau_max -> Running (strict >).
- [ ] Machines Idle/Blocked demand 0; content errors (negative tau_req, eta outside (0,1]) clamp with warnings.

## Implementation Notes

2026-11-05: Runtime subset implemented in ProcessingSystem, RecipeInputPlanner and
StraightMechanicalSolver, with Main placement/inspection integration. See
docs/architecture/machine-processing-runtime.md and
production/qa/evidence/machine-processing-evidence.md for exact coverage. Gears, full multi-source
networks, final animations, ledger events and performance acceptance remain pending.


Per tick: for each network compute path ratio and efficiency per attached machine (cache per structural change), sum demand at source, decide state, then emit per-machine `PowerDelivered(P_recv, omega, reason)` payloads consumed by machine-processing, and a fuel-consumption transaction against the engine's fuel container using a fractional accumulator stored as a persistent attribute.

## Out of Scope

- Machine work integration (machine-processing)
- Ledger attribution (economy-ledger)

## QA Test Cases

- GDD AC 1-5 as listed with exact numbers
- Boundary: demand == tau_max -> Running
- Accumulator: 0.1 fuel/tick consumes one item every 10 ticks exactly

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/MechanicalPower/SolveAndFuelTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; container 002
- Unlocks: machine-processing 002; Story 003
