# Game Concept: Equilibrium

*Created: 2026-09-20*
*Status: Approved*

> Source: migrated from `design/mechanic-brainstorm.md` (creator-confirmed direction).
> Normative mechanic rules live in `design/confirmed-mechanics-spec.md`; on conflict
> that file wins. This document carries direction, pillars, loops, and scope.

---

## Elevator Pitch

> Equilibrium is a factory-optimization game built on real physics, chemistry,
> fluid dynamics, and economics. You start with intuitive mechanical transmission,
> climb through deeper layers of natural law, and turn better engineering into
> higher profit in a finite space with finite capital and a living market.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Factory / automation simulation with economic strategy |
| **Platform** | PC (Windows primary) |
| **Target Audience** | Optimizers and mastery-seekers; see Player Profile |
| **Player Count** | Single-player. A deferred 1v1 "Industrial Duel" mode is recorded in `design/industrial-duel-concept.md` and is out of current scope. |
| **Session Length** | 30-120 min: set a small goal, plan, build, observe, optimize |
| **Monetization** | None defined. Personal learning project. |
| **Estimated Scope** | Large, delivered in tiers; each tier must form a playable factory-economy loop on its own |
| **Comparable Titles** | Create (Minecraft mod) for mechanical intuition; Factorio / Satisfactory for factory logistics; departmental task pacing referenced from Create-style modpacks. Direct market-sim comparables: TBD. |

---

## Core Fantasy

The player is an ever-improving industrial systems designer. Starting from the
most intuitive mechanical transmission, they progressively understand matter,
energy, chemical reactions, heat, fluids, and markets, and convert that
understanding into a more reliable, more efficient, more profitable factory.

The core emotion is not "I solved this puzzle" but:

- I made it better.
- The system I planned actually got built and runs.
- I understand why it runs the way it does.
- I control the relationship between material, energy, capital, and market.

---

## Unique Hook

It's like Create meets Factorio, AND ALSO the laws are real: when fun and realism
conflict, the game keeps the real relationship and makes it the source of fun.
Optimization is measured in profit inside a finite map, finite capital, and a
simulated market, so every engineering gain has an economic consequence.

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Challenge** (mastery) | 1 | Real physical/chemical/economic constraints; bottleneck diagnosis; multiple valid solutions |
| **Discovery** (understanding systems) | 2 | Each fidelity layer reinterprets earlier systems; phenomena first, instruments second, math last |
| **Expression** (build variety) | 3 | Layout, gear ratio, buffer, bypass, recycling, and market-timing choices with different trade-offs |
| **Sensation** | 4 | Visible rotation, material flow, and gauges; 2D URP rendering with baked shaft animation |
| **Submission** (low-stress loops) | 5 | Time control and pause-build are open questions; ambient optimization is supported, idle waiting is not |
| **Fantasy** | 6 | Industrial designer identity; no character or story layer |
| **Fellowship** | N/A | Single-player; duel mode deferred |
| **Narrative** | N/A | No authored story |

### Key Dynamics

- Players read gauges (speed, torque, inventory, price) to locate losses and bottlenecks rather than following a recipe.
- Players trade off speed vs running cost, storage vs cash, self-production vs buying finished goods.
- Players reuse low-tier machines when deeper understanding reveals new uses; old equipment does not become obsolete by numbers alone.
- Players restructure product mix in response to market price shifts.

### Core Mechanics

1. Tick-based deterministic simulation with atomic per-tick transactions (1 tick = 1 s).
2. Mechanical transmission with direction-correct real relationships (gear ratio, torque, `P = tau * omega`, stall conditions, finite efficiency).
3. Grid placement with explicit connections, containers/ports, conveyors, and item I/O.
4. Machines with recipes whose rate, load, and energy derive from the same mechanical model.
5. Simulated single-player market with floating prices, purchase, sale, and warehouse buffering.

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** | Goals may be self-set, task-guided, or market-driven; no single correct layout | Core |
| **Competence** | Understanding real laws yields measurable profit and reliability gains | Core |
| **Relatedness** | None in current scope | Minimal |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — Task system provides goals and pacing; profit and throughput are visible scores.
- [x] **Explorers** — Primary secondary type: discovering how real mechanisms couple.
- [ ] **Socializers** — Not served.
- [ ] **Killers/Competitors** — Not served in current scope; duel mode deferred.

Primary types from source: Optimizer and Master. Non-target: idle-number watchers, short-session snack players, decoration-only casual builders, ranked competitive players.

### Flow State Design

- **Onboarding curve**: Rotation, direction, speed, and gear ratio are directly observable before any instrument or formula is introduced.
- **Difficulty scaling**: Gentle difficulty curve, bottomless depth curve; complexity comes from coupling between systems, not from parameter-heavy UIs.
- **Feedback clarity**: Every line must show input/output inventory, shaft speed, load, processing rate, unit cost, unit price, period profit, and loss causes (stall, starvation, full storage, logistics).
- **Recovery from failure**: Failure is diagnostic; the 30-second loop is "adjust one thing, see the consequence immediately."

---

## Core Loop

### Moment-to-Moment (30 seconds)
Place or connect a machine, watch material flow, spot a bottleneck, make one targeted adjustment, see the result immediately.

### Short-Term (5-15 minutes)
Build one complete production line, get it running, optimize to an acceptable state, connect it to the market or warehouse.

### Session-Level (30-120 minutes)
Set a small goal (self-chosen, task-given, or market opportunity), plan, build, observe, optimize, achieve it.

### Economic Loop
Produce goods, sell for currency, buy raw materials, machines, or finished goods; reinvest to expand or optimize; factory capability changes which market activities are viable.

### Long-Term Progression
Within bounded expansion, descend through fidelity layers: mechanics, matter and motion, inorganic chemistry with thermodynamics, fluids and gases, organics and ecology. Economic play grows more complex through process chains, substitutes, by-products, and capital needs.

### Retention Hooks
- **Curiosity**: Next fidelity layer reinterprets what the player already built.
- **Investment**: A running factory with tuned lines and market positions.
- **Social**: None.
- **Mastery**: Deeper understanding unlocks more precise instruments, better controllers, and more complex processes.

---

## Game Pillars

### Pillar 1: Realism First
When fun and realism conflict, move toward the real law and make realism itself the source of fun rather than diluting it with gamified sugar.

*Design test*: When debating whether to simplify a physical or chemical mechanism for feel, keep the real relationship unless it makes the game entirely unplayable.

Mechanic implications: simplification may reduce input, measurement, or presentation cost but must not invert core laws; values may be scaled to game time but proportional, conservation, and causal relationships stay faithful; teaching and visualization lower the barrier, not law removal; shallow models are allowed but must not block deeper state variables later.

### Pillar 2: Factory and Economy Are One
Production and market are two ends of the same loop. Optimizing the factory is optimizing the factory-market interface.

*Design test*: When debating whether a feature belongs to factory or economy, make it belong to both so factory optimization shows up directly as profit.

Mechanic implications: throughput, energy, yield, material cost, maintenance, and inventory turnover all reach the profit result; price changes alter what is worth producing; storage is an interface between production rhythm, market timing, and capital lock-up; buying finished goods may beat self-production, and vice versa via by-products, quality, or supply stability.

### Pillar 3: Depth Is Progress
Growth is understanding deeper, not building more. Every step of progress should deepen understanding of the world's laws.

*Design test*: When debating breadth vs depth for new content, prefer content that deepens understanding of existing systems.

Mechanic implications: unlocks add state variables, measurement, process relationships, or control; low-tier machines stay useful; rewards may be better gauges, controllers, processes, or explanations; new tiers reinterpret existing systems.

### Pillar 4: Simple Entry, Unfathomable Depth
Intuitive entry inspired by Create-style mechanical transmission; gentle difficulty curve, bottomless depth curve.

*Design test*: When debating whether to introduce a system now, ship only its most intuitive part first and leave complexity for later unlocks.

Mechanic implications: early intuition from rotation, direction, speed, gear ratio; phenomena before instruments before math and automation; every mechanic works both "by intuition" and "by precise calculation"; late complexity comes from system coupling.

### Anti-Pillars (What This Game Is NOT)

- **NOT undesigned idle**: Offline income must be driven by design quality, never by scaling up and waiting for numbers to grow.
- **NOT feel over realism**: Never distort physics or chemistry for instant gratification; improve feel via controls, feedback, time scaling, and tools.
- **NOT infinite expansion**: No infinite map or machine-count sandbox; finite space, capital, and market force optimization.
- **NOT a pure puzzle**: Goals may be explicit but never single-solution; market, capital, space, energy, reliability, and process choice allow multiple viable styles.

---

## Visual Identity Anchor

**One-line visual rule**: Every pixel is an instrument reading: the picture shows the true state of the machine and hides nothing the player must reason about.

Supporting principles:
- Legible before beautiful: direction, connection, fill, speed, and state readable at default zoom.
- Real materials, flat light: wood, iron, brass, glass through value and texture under one top-down key light.
- Motion is data: things move only because a committed transaction happened.
- Consistent 32 px grid; state colors reserved and never decorative.

Full specification: `design/art/art-bible.md`.

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| Create (Minecraft mod) | Intuitive, visible mechanical transmission; departmental task pacing | Real torque/power relationships and economic consequence instead of free rotation | Validates that mechanical intuition is an accessible entry point |
| Factorio / Satisfactory | Bottleneck-hunting satisfaction, logistics building | Finite space and capital; profit rather than throughput as the score; no infinite expansion | Validates the optimization loop as primary fun |
| Real engineering / process chemistry | Conservation, `P = tau * omega`, phase and thermal coupling | Scaled to game time, layered by fidelity tier | Source of the Realism First pillar |

**Non-game inspirations**: Industrial process engineering, chemical plant design, commodity markets.

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | TBD (not specified by creator) |
| **Gaming experience** | Mid-core to hardcore; comfortable with systems-heavy games |
| **Time availability** | 30-120 minute focused sessions |
| **Platform preference** | PC |
| **Current games they play** | Factorio, Satisfactory, Create-based modpacks, engineering sims |
| **What they're looking for** | Optimization and mastery grounded in real laws, with economic payoff |
| **What would turn them away** | Idle progression, forced linear puzzles, gamified physics, infinite sandbox sprawl |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Unity 6000.6.1f1, C#, URP 2D (already in use; see `.claude/docs/technical-preferences.md`) |
| **Key Technical Challenges** | Deterministic tick simulation with atomic transactions; offline progression that obeys the same conservation and capacity constraints as online play (requires replayable logic, not frame-driven GameObjects); market model that neither degenerates to a random price table nor is trivially manipulable; multi-scale physics across fidelity layers |
| **Art Style** | 2D, top-down grid; baked sprite animation for 3D-sourced parts (shaft bake pipeline exists) |
| **Art Pipeline Complexity** | Medium (custom 2D plus render-to-sprite bake) |
| **Audio Needs** | TBD |
| **Networking** | None for current scope. Market backend and duel mode are deferred (`design/market-server-spec.md`, `design/industrial-duel-concept.md`). |
| **Content Volume** | Tier 1: one raw material, one product, a handful of mechanical parts. Later tiers TBD. |
| **Procedural Systems** | None planned; map model (fixed level vs bounded expansion vs multiple bounded maps) is an open question |

---

## Risks and Open Questions

### Design Risks
- "Realism" without explicit fidelity tiers may never converge.
- Market weakly coupled to factory state degrades into a random price table; market fully driven by player output is manipulable or locks up.
- Unified currency needs enough real cost sources or profit optimization collapses into throughput optimization.
- Deep systems without measurement, explanation, and visualization only open up to guide-readers.
- Later laws must not overturn earlier ones or knowledge accumulation breaks.

### Technical Risks
- Online physics simulation and offline analytic progression may diverge.
- Floating market plus long offline time may produce extreme gains or losses.
- Pure steady-state offline extrapolation would make transients, faults, and buffer design meaningless.
- Different scales of real laws may not fit one time step and spatial scale.

### Market Risks
- Not assessed; personal learning project with no commercial commitment.

### Scope Risks
- Mechanics, physics, chemistry, thermodynamics, fluids, gases, organics, ecology, and simulated economy could each be a full game. Tiered delivery is mandatory and each tier must independently form a playable factory-economy loop.

### Open Questions
1. Minimum mechanical state set for Tier 1: speed, torque, power, inertia, stress, efficiency.
2. How time scaling and pause-build coexist with real processes.
3. Map model: fixed level, bounded expansion, or multiple bounded maps.
4. NPC market: true production-chain simulation vs conservation-constrained aggregate supply/demand.
5. Whether wear and maintenance belong in first-version mechanical depth.
6. Whether product quality derives from operating conditions and how it enters price.
7. Online/offline simulation approach: event-driven, analytic approximation, or layered hybrid.
8. Task rewards: currency, equipment, knowledge, licenses, or market opportunities.

---

## MVP Definition

**Core hypothesis**: A player with finite money and space can build a
power-source -> shaft/gears -> processing machine -> storage/sale line whose
speed, load, energy, and output follow one direction-correct mechanical model,
see revenue/cost/profit, and find at least two distinct optimization strategies
that improve profit.

**Required for MVP (Tier 1)**:
1. Finite capital and finite build area.
2. Market with floating price for one raw material and one product; buy and sell.
3. Rotational power source, shafts, gears, one processing machine driven by the mechanical model.
4. Warehouse / direct sale path.
5. Visible: input/output inventory, shaft speed, load, processing rate, unit cost, unit price, period revenue/cost/profit, loss causes.
6. At least two viable optimization strategies (e.g., high speed/high running cost vs stable utilization; larger storage for price timing vs immediate sale).

**Explicitly NOT in MVP**:
- Full chemistry, real fluid solving, final offline income, automated trading logic, multiple maps, departmental tasks, large item/machine catalogs, full NPC industry chains.

### Scope Tiers

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **Tier 0** | Existing project | Fix/confirm prefab refs; verify place/remove, scene, input; deterministic tests for containers and transfer; clarify physics conveyor vs logical container roles | TBD |
| **Tier 1 (MVP)** | One material, one product | Mechanical production + economy minimal loop as above | TBD |
| **Tier 2** | Mechanical depth | More transmission structures and load profiles; efficiency, maintenance, inertia, startup; blueprints; richer bounded market; first automation control | TBD |
| **Tier 3** | Matter and thermochemistry | Material properties and motion; inorganic chemistry with thermodynamics; process choice affects cost, yield, by-products, value | TBD |
| **Tier 4** | Fluids, gases, late economy | Fluid/gas dynamics; automated trader; mixing, separation, reducer (80% target recovery); fuller simulated economy; bounded offline progression | TBD |
| **Tier 5** | Organics and ecology | Built on all prior layers | TBD |

---

## Next Steps

- [x] Engine configured (`/setup-engine`, 2026-09-20)
- [ ] `/map-systems` — decompose into systems index from this document and `confirmed-mechanics-spec.md`
- [ ] Define the Tier 1 mechanical fidelity contract (which quantities are kept, which relationships must hold, which phenomena are deferred) — this is the next decision named in the source
- [ ] `/design-system` per MVP system
- [ ] `/prototype` the Tier 1 loop once fidelity contract exists
- [ ] Deterministic Unity tests for containers, transfer, mechanical relationships, cost/profit
