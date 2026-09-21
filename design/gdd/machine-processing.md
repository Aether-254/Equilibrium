# Machine Processing

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Realism First (work is energy; no free speed), Depth Is Progress (recipe conditions grow with tiers without replacing the base schema), Factory and Economy Are One (utilization and unit cost are machine-level facts)
> **Normative Source**: design/confirmed-mechanics-spec.md s9, s10; decision 2026-09-20: pure power integration
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

A machine accepts inputs through Ports into input Containers, reserves a recipe's inputs when it starts, accumulates work from the mechanical power it receives each tick, and on completion consumes the reservation and emits main product and byproducts into output Containers. Recipes are data: id, applicable machine or machine Tag, inputs (ItemId or Tag), outputs, byproducts, and `workRequired` in joules. Machine state is a small stable set (Idle, Working, Blocked, Starved) plus composable ReasonCodes. Because work is integrated power, a machine's speed is entirely a consequence of the drivetrain feeding it.

## Player Fantasy

Direct. The player watches a saw start, sees its progress bar crawl when the shaft is under-geared and race when they fix the ratio, and reads "Blocked: output full" to know exactly what to do. The fantasy is diagnosis and control: every machine tells you why it is not at 100%.

## Detailed Design

### Core Rules

**Recipes**
1. Recipes are data-driven. Minimum schema: `recipeId`, `machineId` or `machineTag`, `inputs[]` (ItemId or Tag, count), `outputs[]` (ItemId, count), `byproducts[]` (ItemId, count, optional probability), `workRequired` (J). `baseTicks` is a derived display value, not stored.
2. Tag inputs: whether several matching ItemIds may be mixed to satisfy a count is per-recipe (`allowMixedTagInputs`, default false).
3. Advanced conditions (temperature, pressure, catalyst, probability) are explicit, validatable data nodes added in later tiers. Tier 1 recipes carry no unused nodes.
4. A machine may have multiple applicable recipes; selection is automatic by "first recipe whose inputs are fully present" in stable recipe order, or player-pinned.

**Lifecycle**
5. Starting: when Idle and a recipe's inputs are present in input Containers, the machine reserves them (they remain physically in the Container but are unavailable to other transactions) and enters Working with `workDone = 0`.
6. Each tick while Working, `workDone += P_recv * tickDuration` where `P_recv` comes from mechanical-power. If `P_recv = 0` the machine stays Working with a ReasonCode (`Stalled`, `NoFuel`, `SourceConflict`).
7. Completion: when `workDone >= workRequired`, consume the reservation, insert outputs and byproducts into their Containers. If any output cannot be fully inserted, the machine enters Blocked, keeps the finished batch pending, and retries each tick; nothing is lost.
8. Cancel: releases reservations by default. A recipe may set `irreversible: true`; then cancel is refused after start and the UI warned before start.
9. Input buffering: while Working, a machine continues to accept inputs into its input Containers if the machine/recipe allows (`acceptWhileWorking`, default true), up to Container capacity.
10. Machines declare `tau_req` (torque demand) as an ItemDef-level machine property; it is demanded only while Working.

**Ports**
11. Input Ports may allow direct belt transfer or require an accessor (per Port definition). Output Ports push to the belt ahead each tick as a group insertion; if the belt cell cannot accept the whole batch, the machine holds it (Blocked `OutputFull`).

### States and Transitions

| State | Enter when | ReasonCodes (composable) |
|---|---|---|
| Idle | no recipe inputs present and nothing pending | `NoRecipe`, `NoInput` |
| Starved | recipe selected but inputs incomplete | `MissingInput:<ItemId or Tag>` |
| Working | reservation made | `Stalled`, `NoFuel`, `SourceConflict`, `LowPower` |
| Blocked | outputs cannot be placed | `OutputFull`, `PortBindingMissing`, `NoReceivingPort` |

Top-level states stay four; every other cause is a ReasonCode. UI shows both.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| tick-simulation | in | reservation, progress, completion, output insertion transactions |
| container | both | input/output/byproduct Containers; reservation flags per slot |
| item-registry | in | recipe ItemIds and Tags; `tau_req`, `machineTag` |
| mechanical-power | both | `tau_req` out; `P_recv`, `omega`, ReasonCodes in |
| conveyor / item-io-accessor | both | input arrival; output push |
| economy-ledger | out | per-completion: inputs consumed, outputs produced, energy used, ticks in each state |
| machine-inspection-ui | out | all state, progress, rates, utilization |
| multiblock | in | Type I/II structures expose one machine-processing instance on the Controller |
| tech-unlock | in | recipe availability |

## Formulas

The work integration formula is defined as:

`workDone_{t+1} = workDone_t + P_recv * Δt`, `Δt = 1 s`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| work done | workDone | float | 0..workRequired | joules accumulated |
| received power | P_recv | float | 0..1e5 | from mechanical-power |

**Output Range:** monotonic; completes when `>= workRequired`.
**Example:** workRequired 1350 J, P_recv 135.5 W: completes on tick 10 (1355 J).

The theoretical rate formula is defined as:

`rate_theoretical = P_recv / workRequired` (batches per second)

**Example:** 135.5 / 1350 = 0.1004 batches/s, i.e. one batch per ~10 s.

The utilization formula is defined as:

`utilization = ticksWorkingWithPower / ticksObserved`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| working ticks with power | ticksWorkingWithPower | int | 0..N | ticks in Working with P_recv > 0 |
| observed ticks | ticksObserved | int | 1..N | rolling window (knob) |

**Output Range:** 0..1.
**Example:** 60-tick window, 42 productive ticks: 0.70.

The actual rate formula is defined as:

`rate_actual = completions / ticksObserved`

**Example:** 4 completions in 60 ticks: 0.067 batches/s versus theoretical 0.100 means 33% loss, attributed by ReasonCode ticks.

The unit energy cost formula (for the ledger) is defined as:

`energyPerBatch = Σ_{ticks in batch} P_recv * Δt = workRequired` (exact under pure integration)

**Example:** always equals `workRequired`; drivetrain inefficiency shows up as extra fuel at the engine, not at the machine.

## Edge Cases

- **If inputs are present for two recipes**: pick by stable recipe order unless pinned; UI shows which.
- **If a reserved input is physically removed** (player takes it from the slot): reservation cannot be honored; machine cancels, returns to Starved, logs `ReservationBroken`.
- **If `workRequired` is 0**: completes on the first Working tick; content validation warns.
- **If `P_recv` overshoots so `workDone` exceeds `workRequired` by more than one batch**: complete one batch per tick maximum; surplus work carries over capped at `workRequired` (no double batches per tick in Tier 1).
- **If output Container is full but byproduct Container has space**: Blocked; nothing is inserted until the whole batch fits (atomic batch output). Rationale: conservation and predictability over throughput.
- **If a probabilistic byproduct rolls**: uses the tick's tie-break stream (non-reproducible per spec), evaluated at completion.
- **If the machine is removed while Working**: reservation and `workDone` travel with the drop as persistent attributes; re-placing resumes.
- **If the recipe is removed by a content update while Working**: cancel with refund on load; log.
- **If `acceptWhileWorking` is false and belts push inputs**: belt stalls `MachineNotAccepting`.
- **If a Tag input with `allowMixedTagInputs=false` sees 3 iron + 2 copper for a 5x `#ingot` recipe**: Starved `MissingInput:#ingot`.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| tick-simulation, container, item-registry, mechanical-power | hard | |
| conveyor, item-io-accessor | soft | manual insertion works without belts |
| economy-ledger, machine-inspection-ui, multiblock, tech-unlock, group-processing-devices | downstream | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| utilizationWindowTicks | 60 | 10..600 | jittery | slow to reflect fixes |
| acceptWhileWorking default | true | bool | belts stall often | buffers hide starvation |
| maxCompletionsPerTick | 1 | fixed Tier 1 | - | - |
| recipe.workRequired (saw: log→plank) | 1350 J | 200..20000 | trivial | machines crawl |
| machine.tau_req (saw) | 30 N*m | 5..200 | no stall strategy | single machine stalls engine |
| irreversible default | false | bool | - | - |

## Visual/Audio Requirements

Machine sprite per state (Idle, Working loop, Blocked, Starved) as optional GIF-style frame sets; progress ring on hover. Working animation speed may scale with `P_recv / P_nominal`. Audio: working loop pitched by omega; distinct short cue on Blocked.

## UI Requirements

Inspection (machine-inspection-ui GDD): state + top ReasonCode always visible; Overview shows recipe, progress, bottleneck; Mechanics shows omega, tau_req, tau_avail, load, theoretical vs actual rate, utilization; Logistics shows slots and blocked/starved ticks; Economy shows unit cost and profit contribution. Recipe pin selector.

## Acceptance Criteria

- **GIVEN** a saw with recipe log x1 → plank x2, workRequired 1350, receiving 135.5 W, **WHEN** 10 ticks run, **THEN** one completion occurs on tick 10 and the log is consumed then.
- **GIVEN** the same saw with `P_recv = 0` (stalled), **WHEN** 100 ticks run, **THEN** state Working with `Stalled`, progress unchanged, no consumption.
- **GIVEN** a full output Container, **WHEN** a batch completes, **THEN** state Blocked `OutputFull`, inputs already consumed, batch pending, and it inserts on the first tick space appears.
- **GIVEN** a machine Working with reserved log x1, **WHEN** the player removes the log, **THEN** the machine returns to Starved with `ReservationBroken`.
- **GIVEN** a 5x `#ingot` recipe with mixed inputs and `allowMixedTagInputs=false`, **WHEN** evaluated, **THEN** Starved.
- **GIVEN** a Working machine removed and re-placed, **WHEN** re-placed, **THEN** it resumes with the same `workDone` and reservation.
- **GIVEN** 60 ticks with 42 powered Working ticks, **WHEN** utilization is read, **THEN** 0.70.
- **GIVEN** a cancel on a non-irreversible recipe, **WHEN** executed, **THEN** reservations are released and inputs are again available.
- **GIVEN** a cancel on an irreversible recipe after start, **WHEN** attempted, **THEN** refused with `Irreversible`.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Advanced condition node schema and evaluation order | systems-designer | Tier 3 |
| 2 | Multiple completions per tick for very fast recipes | game-designer | Tier 2 |
| 3 | Recipe pinning UI in MVP or VS | ux-designer | VS |
| 4 | Byproduct probability entropy (tie-break stream vs separate) | technical-director | ADR |
