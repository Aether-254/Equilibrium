# Mechanical Power

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Realism First (P = tau * omega, conservation, direction), Simple Entry / Unfathomable Depth (see rotation first, measure later, compute last)
> **Normative Source**: design/confirmed-mechanics-spec.md s16, s9 (rate ownership), game-concept Fidelity Roadmap layer 1
> **Decisions (2026-09-20)**: machine work advances by pure power integration; MVP power source is fuel-driven with fixed output
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

Mechanical power is the Tier 1 energy system: rotating power sources drive shafts and gears that deliver angular speed and torque to machines. Each connected set of shafts and gears is one mechanical network solved once per tick: the source supplies torque up to a limit at a fixed speed, gear pairs transform speed and torque by ratio, efficiency removes a fraction of power at each transmission element, and every attached machine draws torque according to its load. If the total demanded torque exceeds what the source can deliver, the network stalls. A machine's work per tick is the mechanical power it actually receives, so a faster or stronger drivetrain is a faster factory, and an over-geared line stalls everything downstream. This GDD is also the Tier 1 fidelity contract: which quantities exist, which relationships must hold, which phenomena are deferred.

## Player Fantasy

Direct. The player sees shafts spinning at readable speeds, hears the line labor under load, watches a machine slow down when a second one is added to the same shaft, and fixes it with a gear ratio or a second engine. This is the Create-style intuition the concept names as the entry point: rotation, direction, and ratio are visible before any number is shown. Later the same line becomes an optimization surface: torque margin, efficiency losses, and fuel per unit of work.

## Detailed Design

### Tier 1 Fidelity Contract

Simulated in Tier 1 (must be direction-correct, may be scaled):

| Quantity | Symbol | Unit | Where it lives |
|---|---|---|---|
| angular speed | omega | rad/s (displayed as RPM) | per network segment |
| torque | tau | N*m | per element |
| power | P | W (= tau * omega) | per element |
| gear ratio | r | dimensionless | per gear pair |
| rotation direction | sign(omega) | +1/-1 | per segment |
| load (torque demand) | tau_req | N*m | per machine |
| stall | boolean | - | per network |
| efficiency | eta | 0..1 | per transmission element |

Explicitly deferred to later tiers (state must be extensible, not simulated): moment of inertia, angular acceleration, startup transients, shaft strength, overload damage, friction heating, vibration, material fatigue. Tier 1 networks reach steady state instantly each tick.

### Core Rules

**Network topology**
1. Shafts, gears, power sources, and machine rotation-in Ports are nodes in a mechanical graph. Edges exist only between adjacent cells with compatible rotation ports (grid-world rule 7); no proximity inference.
2. A connected component is a network. Each network has exactly one speed per segment; segments are separated by gear pairs.
3. A network with zero sources is idle: omega = 0 everywhere.
4. A network with two or more sources on the same segment MUST have matching nominal speed and direction, otherwise the network faults with `SourceConflict` and delivers nothing (Tier 1 has no clutches).

**Sources**
5. The MVP source is the fuel engine: it consumes fuel items from an input Container and, while fueled, outputs nominal speed `omega_src` and up to `tau_max` torque. It has no idle fuel cost when the network demands zero torque; fuel consumption scales with delivered power.
6. When fuel runs out, the source stops (omega = 0 next tick) and reports `NoFuel`.

**Gears**
7. A gear pair with tooth counts `N_in`, `N_out` has ratio `r = N_out / N_in`. Output speed `omega_out = omega_in / r`, output torque capacity `tau_out = tau_in * r * eta_gear`, direction flips.
8. Shafts pass speed unchanged and torque with efficiency `eta_shaft` per cell.

**Loads**
9. Each machine declares a torque demand `tau_req(machine)` when it is Working (machine-processing GDD); Idle or Blocked machines demand 0.
10. Total demand on a segment is the sum of its machines' demands, plus downstream segments' demands transformed back through the gear ratios.
11. The source delivers `tau_deliv = min(tau_demand_at_source, tau_max)`.

**Stall**
12. If `tau_demand_at_source > tau_max`, the whole network stalls: omega = 0 on every segment for this tick, all attached machines receive zero power and report `Stalled`. Tier 1 has no partial slowdown; it is binary, which makes the cause legible.

**Power delivery**
13. A non-stalled machine receives `P_recv = tau_req * omega_seg * eta_path`, where `eta_path` is the product of efficiencies from source to machine.
14. machine-processing integrates `P_recv` per tick into work done (its GDD owns `workRequired`).

### States and Transitions (per network)

| State | Condition | Effect |
|---|---|---|
| Idle | no source or source unfueled | omega = 0 |
| Running | source fueled, demand <= tau_max | speeds by ratio; power delivered |
| Stalled | demand > tau_max | omega = 0; machines get 0; ReasonCode `Stalled` |
| Faulted | SourceConflict | omega = 0; ReasonCode `SourceConflict` |

Transitions are evaluated fresh each tick from the snapshot; there is no hysteresis in Tier 1.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | network solve submitted as one structural read + per-machine power transactions |
| grid-world | in | rotation ports and adjacency |
| machine-processing | both | receives `tau_req`; delivers `P_recv`, `omega`, stall/fault ReasonCodes |
| container | in | engine fuel input Container |
| economy-ledger | out | fuel consumed per tick, power delivered, stall ticks as loss cause |
| machine-inspection-ui | out | Mechanics tab: omega, tau_req, tau_avail, load %, eta_path |
| ShaftSpriteVisual (existing) | out | omega per shaft cell for animation speed and direction |

## Formulas

The power formula is defined as:

`P = tau * omega`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| torque | tau | float | 0..1e5 | N*m |
| angular speed | omega | float | 0..200 | rad/s (~0..1900 RPM) |

**Output Range:** 0..2e7 W in principle; Tier 1 content stays under 1e5 W.
**Example:** tau = 20 N*m, omega = 10 rad/s gives P = 200 W.

The gear pair formula is defined as:

`omega_out = omega_in / r`, `tau_out_max = tau_in_max * r * eta_gear`, `r = N_out / N_in`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| tooth counts | N_in, N_out | int | 8..64 | gear sizes |
| gear efficiency | eta_gear | float | 0.90..0.99 | per pair |

**Output Range:** speed scales down as torque scales up, minus loss; direction flips.
**Example:** omega_in = 10 rad/s, tau_in_max = 20, N_in = 8, N_out = 16: r = 2, omega_out = 5 rad/s, tau_out_max = 20 * 2 * 0.95 = 38 N*m.

The path efficiency formula is defined as:

`eta_path = Π_{e ∈ path} eta_e`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| element efficiency | eta_e | float | 0.90..0.995 | eta_shaft per shaft cell, eta_gear per pair |

**Output Range:** (0, 1]. Long shaft runs visibly waste power.
**Example:** 5 shaft cells at 0.99 and one gear at 0.95: eta_path = 0.99^5 * 0.95 = 0.903.

The demand-at-source formula is defined as:

`tau_demand_src = Σ_m tau_req(m) * (1 / (r_path(m) * eta_path(m)))`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| machine demand | tau_req(m) | float | 0..1e4 | from machine-processing |
| cumulative ratio | r_path(m) | float | 1/64..64 | product of gear ratios source→machine |

**Output Range:** 0..large.
**Example:** one machine tau_req = 30 behind r = 2, eta_path = 0.903: demand at source = 30 / (2 * 0.903) = 16.6 N*m.

The delivered power formula is defined as:

`P_recv(m) = tau_req(m) * omega_seg(m) * eta_path(m)` if not stalled, else 0

**Output Range:** 0..tau_req * omega.
**Example:** tau_req = 30, omega_seg = 5, eta_path = 0.903: P_recv = 135.5 W.

The fuel consumption formula is defined as:

`fuelPerTick = P_deliv_src / (E_fuel * eta_engine)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| source delivered power | P_deliv_src | float | 0..P_max | tau_deliv * omega_src |
| fuel energy density | E_fuel | float | 1e3..1e7 | J per fuel item (ItemDef property, Tier 1 constant per fuel) |
| engine efficiency | eta_engine | float | 0.2..0.5 | thermal to mechanical |

**Output Range:** items/tick, fractional; accumulate and consume whole items when the accumulator crosses 1.
**Example:** P_deliv = 200 W, E_fuel = 8000 J, eta_engine = 0.25: 0.1 fuel/tick, one fuel item every 10 ticks.

## Edge Cases

- **If demand exactly equals tau_max**: Running, not stalled (strict `>` stalls).
- **If a gear pair connects two segments that both have sources**: treat as one network; speeds must be consistent through the ratio (`omega_A = omega_B * r`) or fault `SourceConflict`.
- **If a machine is added mid-tick**: it is in the next tick's snapshot; this tick's solve is unaffected.
- **If a shaft is removed, splitting a network**: from next tick two networks exist; the one without a source is Idle.
- **If fuel accumulator reaches 1 but the fuel Container is empty**: engine runs this tick on the accumulated fraction, then reports `NoFuel` next tick with omega = 0.
- **If a gear loop forms (A→B→C→A)**: ratios around the loop must multiply to 1 with consistent direction; otherwise fault `GearLoopInconsistent` for the network. Tier 1 content avoids loops but the solver must not hang.
- **If omega_src is configured as 0**: source is Idle; content validation warns.
- **If tau_req is negative**: content error; treat as 0.
- **If eta values are outside (0,1]**: content error; clamp to 1 with warning.
- **If a network has 10,000 elements**: solve must remain linear in elements; no per-tick allocation.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation | hard | |
| grid-world | hard | rotation ports and adjacency |
| container | hard | fuel input |
| item-registry | soft | fuel items and `E_fuel` property |
| machine-processing | downstream hard | consumes P_recv |
| economy-ledger, machine-inspection-ui | downstream | reporting |
| conveyor | none | conveyors are not powered in Tier 1 (spec s7) |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| engine.omega_src | 10 rad/s (~95 RPM) | 2..50 | machines crawl | animation unreadable |
| engine.tau_max | 40 N*m | 5..500 | one machine stalls it | stall never happens; strategy lost |
| engine.eta_engine | 0.25 | 0.15..0.5 | fuel cost dominates | fuel irrelevant |
| eta_shaft | 0.99 | 0.95..0.999 | long runs impossible | layout irrelevant |
| eta_gear | 0.95 | 0.85..0.99 | ratios punished | free torque |
| gear tooth counts available | 8, 16, 32 | 2..6 sizes | few ratios | UI overload |
| fuel.E_fuel (wood) | 8000 J | 1e3..1e5 | fuel logistics dominate | cost trivial |
| stall mode | binary | fixed Tier 1 | - | partial slowdown is Tier 2 |

## Visual/Audio Requirements

Shaft sprites rotate at speed proportional to omega with correct direction (existing ShaftSpriteVisual bake; frame index advances by omega * dt). Gears show tooth count by size. Stall: rotation stops instantly, engine emits a strain cue. NoFuel: engine idle animation. Tier 1 needs no smoke or heat visuals.

## UI Requirements

Inspection Mechanics tab per machine: omega (RPM), tau_req, tau_avail on segment, load % = tau_demand_src / tau_max for its network, eta_path, current ReasonCode. Engine inspection: fuel slot, fuel/tick, P_deliv, network load %. Persistent top status bar shows `Stalled` / `NoFuel` / `SourceConflict`.

## Acceptance Criteria

- **GIVEN** an engine (omega 10, tau_max 40) driving a shaft to one machine with tau_req 30, **WHEN** ticked, **THEN** omega_seg = 10, P_recv = 300 * eta_path, network Running.
- **GIVEN** the same network with a second identical machine on the same segment, **WHEN** ticked, **THEN** demand 60 > 40, network Stalled, both machines report `Stalled`, omega = 0.
- **GIVEN** the stalled network with an 8:16 gear inserted before the machines, **WHEN** ticked, **THEN** demand at source = 60 / (2 * 0.95) = 31.6 <= 40, Running, omega at machines = 5.
- **GIVEN** a shaft run of 20 cells at eta 0.99, **WHEN** measured, **THEN** P_recv = P_src * 0.99^20 (about 0.818) within floating tolerance.
- **GIVEN** an engine with 1 wood (8000 J) and P_deliv 200 W, **WHEN** 10 ticks run, **THEN** the wood is consumed on tick 10 and tick 11 reports `NoFuel`.
- **GIVEN** two engines on one segment with different omega_src, **WHEN** ticked, **THEN** network Faulted `SourceConflict` and no machine receives power.
- **GIVEN** a gear pair, **WHEN** input rotates clockwise, **THEN** output rotates counter-clockwise in the visual and `sign(omega)` flips.
- **GIVEN** a network of 5,000 elements, **WHEN** solved, **THEN** solve time under 1 ms.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Whether Tier 2 partial slowdown replaces binary stall or is an unlockable engine governor | game-designer | VS |
| 2 | Clutch / gearbox blocks for mixing sources | systems-designer | Tier 2 |
| 3 | Whether conveyors become powered loads in Tier 2 | game-designer | Tier 2 |
| 4 | Display units: RPM only, or RPM + rad/s toggle | ux-designer | VS |
