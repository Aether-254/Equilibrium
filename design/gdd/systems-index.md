# Systems Index: Equilibrium

> **Status**: Approved
> **Created**: 2026-09-20
> **Last Updated**: 2026-09-20
> **Source Concept**: design/gdd/game-concept.md
> **Normative Rules**: design/confirmed-mechanics-spec.md (section numbers referenced below as "spec sN")

---

## Overview

Equilibrium is a tick-driven factory-optimization game whose systems fall into
two tightly coupled halves: a deterministic production simulation (grid,
containers, conveyors, machines, mechanical power) and an economy layer
(market interface, ledger) that turns engineering quality into profit. The
30-second loop is "place or connect, watch flow, spot bottleneck, adjust,
see result"; the MVP (Tier 1) proves that a power-source -> shaft/gears ->
machine -> storage/sale line with one material and one product yields at
least two distinct profitable optimization strategies. Every system must obey
the four pillars: Realism First, Factory and Economy Are One, Depth Is
Progress, Simple Entry / Unfathomable Depth. Later tiers layer matter, heat,
chemistry, fluids, and ecology onto the same foundations without replacing them.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | tick-simulation | Core | MVP | Approved | design/gdd/tick-simulation.md | — |
| 2 | item-registry | Core | MVP | Approved | design/gdd/item-registry.md | — |
| 3 | grid-world | Core | MVP | Approved | design/gdd/grid-world.md | tick-simulation, item-registry |
| 4 | container | Core | MVP | Approved | design/gdd/container.md | tick-simulation, item-registry |
| 5 | save-load (inferred) | Persistence | MVP | Approved | design/gdd/save-load.md | item-registry, grid-world, container |
| 6 | mechanical-power | Gameplay | MVP | Approved | design/gdd/mechanical-power.md | tick-simulation, grid-world |
| 7 | conveyor | Gameplay | MVP | Approved | design/gdd/conveyor.md | container, grid-world, tick-simulation |
| 8 | item-io-accessor | Gameplay | MVP | Approved | design/gdd/item-io-accessor.md | conveyor, container |
| 9 | machine-processing | Gameplay | MVP | Approved | design/gdd/machine-processing.md | container, mechanical-power, item-registry, tick-simulation |
| 10 | market-interface | Economy | MVP | Approved | design/gdd/market-interface.md | item-registry, container, save-load |
| 11 | economy-ledger (inferred) | Economy | MVP | Approved | design/gdd/economy-ledger.md | machine-processing, mechanical-power, market-interface |
| 12 | placement-editing | Gameplay | MVP | Approved | design/gdd/placement-editing.md | grid-world, conveyor, flow-blocker, multiblock |
| 13 | machine-inspection-ui | UI | MVP | Approved | design/gdd/machine-inspection-ui.md | machine-processing, mechanical-power, economy-ledger |
| 14 | flow-blocker | Gameplay | Vertical Slice | Approved | design/gdd/flow-blocker.md | conveyor, grid-world |
| 15 | build-hud (inferred) | UI | Vertical Slice | Approved | design/gdd/build-hud.md | placement-editing, flow-blocker |
| 16 | multiblock | Gameplay | Vertical Slice | Approved | design/gdd/multiblock.md | grid-world, machine-processing, save-load |
| 17 | tutorial-task | Meta | Vertical Slice | Approved | design/gdd/tutorial-task.md | tech-unlock, machine-inspection-ui, build-hud |
| 18 | blueprint | Gameplay | Alpha | Approved | design/gdd/blueprint.md | placement-editing, multiblock, save-load |
| 19 | group-processing-devices | Gameplay | Alpha | Approved | design/gdd/group-processing-devices.md | conveyor, machine-processing |
| 20 | tech-unlock | Progression | Alpha | Approved | design/gdd/tech-unlock.md | machine-processing, economy-ledger |
| 21 | glass-pipe | Gameplay | Alpha | Approved | design/gdd/glass-pipe.md | grid-world, container, flow-blocker |
| 22 | offline-progression (inferred) | Economy | Full Vision | Approved | design/gdd/offline-progression.md | tick-simulation, machine-processing, market-interface, save-load |
| 23 | market-server | Economy | Full Vision | Not Started | design/gdd/market-server.md (index into design/market-server-spec.md; Deferred) | market-interface, item-registry |
| 24 | industrial-duel | Meta | Full Vision | Not Started | design/gdd/industrial-duel.md (index into design/industrial-duel-concept.md; Deferred) | market-server, economy-ledger |

Notes:
- All GDDs were authored 2026-09-20 in Solo review mode: no specialist or
  director agents were consulted. All 22 non-deferred GDDs were self-reviewed and Approved by the creator on 2026-09-20.
- 23 and 24 are **Deferred**: their drafts self-declare implementation forbidden
  zones. Their GDD files are indexes into the source drafts, not new design.
- placement-editing depends on flow-blocker and multiblock only for its full
  (VS) feature set. Its MVP subset (machine place/remove, Shift straight-line
  conveyor drag) depends only on grid-world and conveyor.
- machine-inspection-ui and build-hud have GDDs fixing behavior and content;
  layout and visuals still require `/ux-design` specs before implementation.
- Decisions taken during authoring (2026-09-20): machine rate = pure power
  integration; MVP power source = fuel engine; MVP market = fixed price with
  spread + per-window volume limit; tie-break entropy = OS CSPRNG mixed per tick.

### Spec section -> system mapping

| Spec sections | System |
|---|---|
| s1 | tick-simulation |
| s2, s2A, s2B | item-registry |
| s3, s4 | grid-world |
| s5 | multiblock |
| s6 | container |
| s7.1-7.6 | conveyor |
| s7.4, s8 | item-io-accessor |
| s7.7 | group-processing-devices |
| s7.8 | flow-blocker |
| s7.8 (GlassPipe parts), s7.9 | glass-pipe |
| s9, s10 | machine-processing |
| s11 | placement-editing (+ build-hud for preview/feedback rules) |
| s12 | blueprint |
| s13 | machine-inspection-ui |
| s14 | tutorial-task |
| s15 | tech-unlock |
| s16 | mechanical-power |
| s17 | market-interface (market-server for remote backend) |
| s18, s19 | not systems; stay in source as priorities / open boundaries |
| s20 | not a system; relocate to technical-preferences / art spec |
| concept: Economy Baseline / Offline | offline-progression |
| concept: MVP visible quantities | economy-ledger |
| concept + spec s3 persistence, s2A.6, s2B.3, s17 binding | save-load |

---

## Categories

| Category | Description | Systems here |
|----------|-------------|--------------|
| **Core** | Foundation everything depends on | tick-simulation, item-registry, grid-world, container |
| **Gameplay** | Production simulation and building | mechanical-power, conveyor, item-io-accessor, machine-processing, placement-editing, flow-blocker, multiblock, blueprint, group-processing-devices, glass-pipe |
| **Economy** | Value creation and exchange | market-interface, economy-ledger, offline-progression, market-server |
| **Progression** | Depth as progress | tech-unlock |
| **Persistence** | Save state | save-load |
| **UI** | Player-facing information | machine-inspection-ui, build-hud |
| **Meta** | Outside the core loop | tutorial-task, industrial-duel |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Tier 1 loop: one material, one product, mechanical line, buy/sell, visible profit, two optimization strategies | First playable | Design FIRST |
| **Vertical Slice** | Tier 2 mechanical depth and building comfort: blockers, full drag pathfinding, multiblock, tasks | Vertical slice | Design SECOND |
| **Alpha** | Blueprints, group devices, unlock structure, fluid content schema | Alpha | Design THIRD |
| **Full Vision** | Offline progression, remote market, duel mode | Beta / later | Design as needed |

---

## Dependency Map

### Foundation Layer (no dependencies)

1. tick-simulation — every state change is a transaction inside a tick; nothing else can be specified without it
2. item-registry — item identity underlies containers, recipes, market, and saves

### Core Layer (depends on foundation)

1. grid-world — depends on: tick-simulation, item-registry
2. container — depends on: tick-simulation, item-registry
3. save-load — depends on: item-registry, grid-world, container
4. mechanical-power — depends on: tick-simulation, grid-world

### Feature Layer (depends on core)

1. conveyor — depends on: container, grid-world, tick-simulation
2. item-io-accessor — depends on: conveyor, container
3. machine-processing — depends on: container, mechanical-power, item-registry, tick-simulation
4. flow-blocker — depends on: conveyor, grid-world
5. glass-pipe — depends on: grid-world, container, flow-blocker
6. group-processing-devices — depends on: conveyor, machine-processing
7. multiblock — depends on: grid-world, machine-processing, save-load
8. placement-editing — depends on: grid-world, conveyor, flow-blocker, multiblock
9. blueprint — depends on: placement-editing, multiblock, save-load
10. market-interface — depends on: item-registry, container, save-load
11. economy-ledger — depends on: machine-processing, mechanical-power, market-interface
12. tech-unlock — depends on: machine-processing, economy-ledger
13. offline-progression — depends on: tick-simulation, machine-processing, market-interface, save-load
14. market-server — depends on: market-interface, item-registry

### Presentation Layer (depends on features)

1. build-hud — depends on: placement-editing, flow-blocker
2. machine-inspection-ui — depends on: machine-processing, mechanical-power, economy-ledger

### Polish Layer (depends on everything)

1. tutorial-task — depends on: tech-unlock, machine-inspection-ui, build-hud
2. industrial-duel — depends on: market-server, economy-ledger

---

## Recommended Design Order

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | tick-simulation | MVP | Foundation | systems-designer, technical-director | M |
| 2 | item-registry | MVP | Foundation | systems-designer | M |
| 3 | container | MVP | Core | systems-designer | M |
| 4 | grid-world | MVP | Core | systems-designer | S |
| 5 | mechanical-power | MVP | Core | systems-designer, game-designer | L (needs Tier 1 fidelity contract) |
| 6 | save-load | MVP | Core | systems-designer, technical-director | S |
| 7 | conveyor | MVP | Feature | systems-designer | M |
| 8 | item-io-accessor | MVP | Feature | systems-designer | S |
| 9 | machine-processing | MVP | Feature | systems-designer | M |
| 10 | market-interface | MVP | Feature | economy-designer | M |
| 11 | economy-ledger | MVP | Feature | economy-designer | S |
| 12 | placement-editing (MVP subset) | MVP | Feature | game-designer, ux-designer | M |
| 13 | machine-inspection-ui | MVP | Presentation | ux-designer | S (ux-spec) |
| 14 | flow-blocker | VS | Feature | systems-designer | S |
| 15 | multiblock | VS | Feature | systems-designer | M |
| 16 | placement-editing (full) | VS | Feature | game-designer, ux-designer | L |
| 17 | build-hud | VS | Presentation | ux-designer | M |
| 18 | tutorial-task | VS | Polish | game-designer | M |
| 19 | tech-unlock | Alpha | Feature | game-designer | M |
| 20 | group-processing-devices | Alpha | Feature | systems-designer | S |
| 21 | blueprint | Alpha | Feature | game-designer | M |
| 22 | glass-pipe | Alpha | Feature | systems-designer, technical-artist | M |
| 23 | offline-progression | Full Vision | Feature | systems-designer, technical-director | L |
| 24 | market-server | Full Vision | Feature | economy-designer, network-programmer | L (deferred) |
| 25 | industrial-duel | Full Vision | Polish | game-designer | L (deferred) |

Effort: S = 1 session, M = 2-3 sessions, L = 4+ sessions. Most MVP GDDs start
from existing normative text in the spec, so the sessions are mostly filling
Formulas, Edge Cases, Tuning Knobs, and Acceptance Criteria.

---

## Circular Dependencies

- economy-ledger <-> market-interface (potential): the ledger needs sale
  prices; the market could be tempted to record trades in the ledger.
  Resolution: market-interface exposes only price queries and buy/sell
  transactions; economy-ledger consumes market events one-way. No cycle.
- None other found.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| tick-simulation | Technical | Non-reproducible tie-break RNG (spec s1.3) conflicts with any future replay/offline determinism; entropy source undefined | ADR before implementation; isolate tie-break entropy behind an interface |
| mechanical-power | Design | "Realism First" without a fixed Tier 1 fidelity contract may never converge; formula for machine rate vs speed/torque undefined (spec s9) | Write the fidelity contract as the GDD's Formulas section before conveyor/machine GDDs are finalized |
| container | Scope | Bottleneck: 7 systems depend on it; auto-tidy stable ordering (s6.1.3) is under-specified | Design early; lock SlotIndex/tidy ordering in Formulas; EditMode tests |
| placement-editing | Scope | Orthogonal pathfinding, network reuse analysis, budgets, three cost modes (s11.2) is a large feature; risk of blocking MVP | Split MVP subset (place/remove + Shift straight line) from VS full version |
| market-interface | Design | Fixed-price MVP may make profit optimization degenerate into throughput optimization (concept risk) | Ensure at least two cost sources (raw price + running cost) even at fixed price |
| save-load | Technical | Schema referenced by many spec rules (string keys not indices, UnknownItem, Tag not persisted); late definition forces rework | Design in MVP even if implementation is minimal |
| glass-pipe | Technical | Two-phase content model must be forward-compatible before any fluid sim exists | Lock `components[]` schema in Alpha; no fluid solver required |
| offline-progression | Technical | Online/offline divergence; extreme market outcomes | Defer; requires deterministic core first |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 24 |
| Design docs started | 24 |
| Design docs reviewed | 22 (creator self-review) |
| Design docs approved | 22 |
| MVP systems designed | 13/13 |
| Vertical Slice systems designed | 4/4 |

---

## Next Steps

- [x] Systems enumeration approved (2026-09-20)
- [x] All 24 GDDs authored via `/design-system` (2026-09-20)
- [ ] `/design-review design/gdd/[system].md` in fresh sessions, MVP first
- [ ] `/review-all-gdds` for cross-GDD consistency
- [ ] `/architecture-decision` x3 for tick transactions, tie-break entropy, item identity (adoption plan 2e)
- [ ] `/gate-check` Concept -> Systems Design
- [ ] `/ux-design` for machine-inspection and hud before epics
- [ ] `/gate-check pre-production` when MVP GDDs are approved
