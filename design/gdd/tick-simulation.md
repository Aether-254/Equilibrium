# Tick Simulation

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Realism First (conservation and causality hold exactly), Simple Entry / Unfathomable Depth (one visible cause per visible effect)
> **Normative Source**: design/confirmed-mechanics-spec.md s1, s19
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The tick simulation is the clock and the transaction engine for every gameplay state change. Simulation time advances in discrete ticks of one second. Within a tick, systems do not mutate world state directly; they submit transaction requests (move items, consume inputs, transfer torque, execute a trade). The engine validates capacity, type, and preconditions, resolves conflicts by explicit priority and a non-reproducible tie-break, then commits all accepted transactions atomically and emits the visual events that animate them. Players never see the tick engine, but every "I changed one thing and saw the consequence" moment depends on it: partial commits, phantom items, or order-dependent outcomes would make the factory impossible to reason about.

## Player Fantasy

Indirect. The player experiences the tick engine as trustworthiness: when a conveyor group moves, it moves whole; when a machine consumes inputs, the outputs appear in the same breath; when two lines compete for one slot, exactly one wins and nothing is lost. The fantasy served is "I understand why it runs the way it does" (game-concept Core Fantasy). Animation is presentation of already-committed facts, so the player can always trust what they see against what the ledger says.

## Detailed Design

### Core Rules

1. Simulation time is measured in ticks. `1 tick = 1 second` of simulated time. Wall-clock mapping (time scaling, pause) is owned by a future time-control system and never changes tick semantics.
2. Every state change of a simulated object MUST be expressed as a transaction request submitted during the collection phase of a tick.
3. Systems MUST NOT read another object's final state for this tick during collection; they read the tick-start snapshot.
4. A transaction has: a source (optional), a target (optional), a payload (items, energy, torque, currency, structural change), an explicit priority integer, and a validation predicate.
5. Transaction commit MUST be atomic: source deduction and target write succeed together or neither happens. A failed transaction produces zero partial modification.
6. Transactions may declare dependencies on other transactions in the same tick (for example, conveyor cell A entering B depends on B's group leaving). Dependency resolution is a topological pass; a dependency that fails causes the dependent to fail.
7. After commit, the engine emits one visual event per committed transaction. Visual systems consume events; they never infer state from animation.
8. Ticks are processed sequentially and completely. A tick never yields mid-phase.

### Tick Phases

| Phase | Input | Output |
|---|---|---|
| Snapshot | World state | Immutable read view for this tick |
| Collect | Each system submits requests against the snapshot | Request set |
| Validate | Request set | Accepted candidates, rejected requests with ReasonCode |
| Resolve | Candidates | Ordered accepted set (priority, dependency, tie-break) |
| Commit | Accepted set | New world state |
| Emit | Committed set | Visual/audio events, ledger events |

### Conflict Resolution

1. Two or more candidates conflict when they compete for the same source quantity or the same target capacity and cannot all be satisfied.
2. Higher explicit priority wins first. Priority values are defined by the submitting system's GDD (default 0).
3. Among equal-priority conflicting candidates, the winner order is drawn from the tick's tie-break random stream.
4. The tie-break stream MUST NOT be derivable from world seed, save state, tick number, coordinates, transaction ID, or any state a player can read, replay, or manipulate. It is seeded per tick from OS-provided cryptographic randomness mixed into a fast PRNG for the duration of that tick.
5. The engine MUST NOT promise identical tie-break outcomes for identical inputs across runs, saves, or offline recomputation.
6. The loser of a conflict keeps its payload at the source. It may be resubmitted next tick.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| conveyor, item-io-accessor, machine-processing, container | in | submit item transactions with priority and dependencies |
| mechanical-power | in | submit torque/speed network solve as a single structural transaction |
| market-interface | in | submit buy/sell transactions; remote export escrow is a local transaction |
| placement-editing, blueprint, flow-blocker | in | structural transactions (place, remove, move, toggle IngressEdge) effective next tick |
| economy-ledger | out | consumes committed transaction events to record cost, revenue, loss causes |
| Visual layer (ShaftSpriteVisual etc.) | out | consumes emitted events |
| save-load | out | snapshot at tick boundary is the only legal save point |
| offline-progression | in/out | replays ticks with the same engine; tie-break entropy is fresh |

## Formulas

The tie-break seed formula is defined as:

`tickSeed = H(osEntropy_256 || tickIndex)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| OS entropy | osEntropy_256 | 32 bytes | any | Fresh bytes from the platform CSPRNG each tick |
| Tick index | tickIndex | uint64 | 0..2^64 | Current tick; included only for domain separation, contributes no secrecy |
| Hash | H | function | - | Fixed non-cryptographic mixer (e.g. SplitMix64 finalizer over the concatenation) |

**Output Range:** 64-bit seed for the tick's PRNG.
**Example:** Two conveyors A and C both target empty cell B with priority 0. `tickSeed` yields ordering [C, A]; C commits, A's group stays in A and resubmits next tick.

The conflict ordering formula is defined as:

`order(t) = (-priority(t), rand(tickSeed, t.localIndex))`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| priority | priority(t) | int | -1000..1000 | Explicit transaction priority; higher wins |
| local index | t.localIndex | int | 0..N | Position in the conflict group; only used to draw a random key |
| random key | rand | uint64 | full | Value from the tick PRNG |

**Output Range:** Sort key; lower sorts first.
**Example:** Priorities 5, 0, 0. The priority-5 transaction resolves first; the two zeros are ordered by random keys.

## Edge Cases

- **If a transaction's source and target are the same container**: validate as a move within the container; commit only if the container's insertion rules (container GDD) accept it.
- **If a dependency chain exceeds 1000 transactions in one tick**: process normally; emit a performance warning event. There is no semantic cap.
- **If a dependency cycle is detected in Resolve**: all transactions in the cycle fail with ReasonCode `TransactionCycle`. Systems must design requests so cycles cannot form (conveyor loops are broken by the "one cell per tick" rule).
- **If the OS CSPRNG call fails**: the tick MUST NOT proceed with a predictable seed. Retry once; if still failing, halt simulation with a visible error. Never fall back to time or tick-derived seeds.
- **If two structural transactions touch the same cell** (place and remove): both are priority-resolved like any conflict; the loser is rejected with `CellContested`.
- **If a save is requested mid-tick**: defer to the tick boundary. Saves never observe a partially committed tick.
- **If time is paused**: no ticks are processed; structural previews (placement ghosts) are not simulation state and may update.
- **If a system submits a request that reads post-commit state of this tick**: undefined by contract; the engine detects snapshot violations in debug builds and fails the request with `SnapshotViolation`.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| none upstream | - | Foundation layer |
| item-registry | soft | Payload item identities are ItemIds; engine treats them opaquely |
| Every simulated system | downstream hard | All submit through this engine |
| save-load | downstream hard | Save points are tick boundaries |
| economy-ledger | downstream hard | Consumes committed events |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| tickDurationSeconds | 1 | fixed at 1 | - | - (changing it invalidates every rate in every GDD) |
| priorityMin / priorityMax | -1000 / 1000 | any int | systems collide on same values | meaningless spread |
| dependencyWarnThreshold | 1000 | 100..100000 | log spam | miss real perf issues |
| debugSnapshotChecks | on in dev, off in release | bool | silent contract violations | CPU cost |

Time scaling and pause knobs belong to the future time-control system, not here.

## Visual/Audio Requirements

None directly. All animation consumes emitted events; conveyor sliding, machine state GIFs, and shaft rotation are specified in their own GDDs.

## UI Requirements

None player-facing. Debug overlay may show tick index, requests/accepted/rejected counts per tick.

## Acceptance Criteria

- **GIVEN** a chest with 199/200 wood and a conveyor cell holding wood x2 feeding it via an accessor, **WHEN** one tick runs, **THEN** the chest holds 200 and the cell holds wood x1, with no intermediate state observable.
- **GIVEN** a transaction whose validation fails, **WHEN** the tick commits, **THEN** neither source nor target differs from the snapshot.
- **GIVEN** two priority-0 transactions targeting the same single-capacity slot, **WHEN** the tick runs, **THEN** exactly one commits and the other's payload remains at its source.
- **GIVEN** the same save loaded twice with identical inputs, **WHEN** an equal-priority conflict occurs, **THEN** the winner is not guaranteed to be the same across the two runs (test asserts absence of a determinism guarantee by checking the seed is not derived from save state).
- **GIVEN** a priority-5 and a priority-0 transaction in conflict, **WHEN** resolved, **THEN** the priority-5 transaction commits.
- **GIVEN** conveyor chain A -> B -> C with A=wood x100, B=wood x200, C=empty, **WHEN** one tick runs, **THEN** A=empty, B=wood x100, C=wood x200 (dependency-ordered commit in one tick).
- **GIVEN** a save request issued during Collect, **WHEN** the tick finishes, **THEN** the save reflects the post-commit state of that tick.
- **GIVEN** 10,000 conveyor transactions in one tick, **WHEN** processed, **THEN** the tick completes within 4 ms on the reference PC (half the 8.3 ms frame budget).

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Exact mixer function and PRNG (SplitMix64 vs xoshiro) | technical-director (ADR) | before implementation |
| 2 | Whether transaction requests are pooled structs or objects (performance) | lead-programmer | ADR |
| 3 | Time-control system ownership (speed, pause) | game-designer | Vertical Slice |
| 4 | Offline recomputation strategy (event-driven vs analytic) | technical-director | Full Vision |
