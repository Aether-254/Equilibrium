# ADR-0001: Tick Transaction Engine

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1 (URP 17.6, 2D Renderer) |
| **Domain** | Core |
| **Knowledge Risk** | HIGH (version post-cutoff) but this decision uses no Unity API beyond `MonoBehaviour.FixedUpdate`/custom timer for tick pumping; risk to this ADR is LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Tick pump timing under variable frame rate; allocation-free request pooling verified with Profiler |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0002 (tie-break entropy), ADR-0004 (world data model), ADR-0005 (save at tick boundary), ADR-0007 (sim/presentation split), ADR-0008 (market transactions) |
| **Blocks** | Every simulation epic (conveyor, machine, mechanical power, market) |
| **Ordering Note** | Must be implemented and tested first; all other simulation code is written against `ITransactionSink` |

## Context

### Problem Statement
The GDDs (tick-simulation, conveyor, machine-processing, container, market-interface) require that every state change in a tick be collected as a transaction, validated against a tick-start snapshot, resolved by explicit priority and dependency, then committed atomically. Unity's default MonoBehaviour update order and per-object mutation make this impossible to guarantee. We need a deterministic-by-construction engine that owns the tick lifecycle and is the only writer of simulation state.

### Constraints
- `1 tick = 1 s` simulated; wall-clock mapping via a separate time-control layer.
- Frame budget 8.3 ms at 120 fps; tick processing target under 4 ms for 10,000 conveyor transactions (tick-simulation GDD).
- No per-tick heap allocation in steady state.
- Must run headless (EditMode tests, offline recompute) without a scene.

### Requirements
- Phases: Snapshot, Collect, Validate, Resolve, Commit, Emit.
- Transactions carry source, target, payload, priority, dependencies, validation predicate.
- Atomic commit: all-or-nothing per transaction; dependency failure cascades.
- Committed events are the only input to presentation and ledger.
- Save points only at tick boundaries.

## Decision

Implement a pure C# `TickEngine` class (no Unity dependency) in a runtime assembly `Equilibrium.Simulation`. Systems implement `ITickSystem { void Collect(in TickSnapshot snap, TransactionBuffer buf); }` and never mutate world state outside `Commit`. The engine:

1. Builds an immutable `TickSnapshot` (struct views over the world arrays; no copy for unchanged chunks).
2. Calls `Collect` on registered systems in a fixed registration order (order affects nothing semantically because all reads are from the snapshot).
3. Validates each `Transaction` via its `IValidator` against the snapshot and running capacity ledgers.
4. Resolves: topological sort over declared dependencies; conflict groups sorted by `(-priority, tieBreakKey)` where `tieBreakKey` comes from ADR-0002.
5. Commits accepted transactions through `IWorldWriter`, the only object with write access to world state.
6. Emits `CommittedEvent` records to an `EventBus` consumed by presentation, ledger, unlock, and task systems.

A `TickPump` MonoBehaviour (in `Equilibrium.Unity`) advances the engine by whole ticks based on the time-control layer; it never partially advances a tick.

### Architecture Diagram
```
[TickPump (MonoBehaviour)] --advance(n)--> [TickEngine]
   Snapshot -> Collect(ITickSystem[]) -> Validate -> Resolve -> Commit(IWorldWriter) -> Emit(EventBus)
                                                                      |                      |
                                                              [WorldState arrays]     [Presentation, Ledger, Unlock, Tasks]
```

### Key Interfaces
```csharp
public interface ITickSystem { void Collect(in TickSnapshot snapshot, TransactionBuffer buffer); }

public struct Transaction {
    public TransactionId Id;
    public EntityRef Source;      // may be None
    public EntityRef Target;      // may be None
    public Payload Payload;       // items / energy / torque / currency / structural
    public int Priority;          // default 0; -1000..1000
    public DependencyList DependsOn;
    public ValidatorId Validator;
}

public interface IWorldWriter { void Apply(in Transaction accepted); }
public readonly struct CommittedEvent { public TransactionId Id; public Payload Payload; public ReasonCode Reason; }
public interface ITieBreakSource { ulong NextKey(); }   // provided by ADR-0002
```

Transactions and snapshots are structs in pooled `NativeArray`/`Span<T>`-backed buffers; no class allocation per transaction.

## Alternatives Considered

### Alternative 1: MonoBehaviour Update with ordered Script Execution
- **Description**: Each block is a MonoBehaviour; rely on Script Execution Order and direct mutation.
- **Pros**: Idiomatic Unity; fast to prototype (current `Assets/Script/` does this).
- **Cons**: Cannot guarantee atomic multi-object commits; order-dependent results; not headless-testable; allocation-heavy.
- **Rejection Reason**: Violates tick-simulation GDD rules 2, 3, 5.

### Alternative 2: Unity DOTS/ECS with ISystem
- **Description**: Model blocks as entities; systems as Burst-compiled `ISystem`.
- **Pros**: Performance; data-oriented.
- **Cons**: Transaction/conflict resolution is not a natural ECS pattern; Entities 1.3 API is post-cutoff (HIGH risk); heavy for a 64x64 Tier 1 map; complicates Addon-loaded content.
- **Rejection Reason**: Complexity and knowledge risk outweigh benefit at current scale. Revisit if Tier 4 fluid simulation needs it.

### Alternative 3: Event-sourced immutable world (persistent data structures)
- **Description**: Each tick produces a new immutable world; transactions are pure functions.
- **Pros**: Trivial snapshotting and replay.
- **Cons**: GC pressure in C#; harder to reach 4 ms with 10k transactions.
- **Rejection Reason**: Performance budget.

## Consequences

### Positive
- Determinism by construction except the intentional tie-break entropy.
- Headless and EditMode testable; offline recompute reuses the engine.
- Single write path simplifies save/load and debugging.

### Negative
- All existing `Assets/Script/` runtime code (`Conveyor`, `ItemTransferLink`, `ContainerTransfer`) must be refactored into `ITickSystem` collectors.
- Systems cannot "just set a field"; every mutation is a transaction, which adds boilerplate.

### Risks
- Dependency cycles between transactions hang or explode: mitigated by cycle detection with `TransactionCycle` rejection (GDD edge case).
- Snapshot cost on large worlds: mitigated by chunk-level dirty flags and struct views.
- Developers bypass `IWorldWriter`: mitigated by making world arrays `internal` to `Equilibrium.Simulation` and exposing read-only views.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| tick-simulation.md | Rules 2-8, phases table, atomic commit, dependency-ordered commit | `TickEngine` phases and `IWorldWriter` single write path |
| conveyor.md | Rule 10 (enter a vacating cell same tick), rule 11 | Dependency declarations resolved topologically before commit |
| container.md | Acceptable quantity computed against snapshot | Validators read `TickSnapshot` capacity ledgers |
| machine-processing.md | Reservation and atomic batch output | Reservation and output insertion are single transactions |
| market-interface.md | Local settlement same tick; escrow as local transaction | Market transactions flow through the same engine |
| save-load.md | Rule 1: save only at tick boundary | `TickPump` exposes boundary hook |

## Performance Implications
- **CPU**: Target under 4 ms for 10k transactions; validators must be O(1) amortized.
- **Memory**: Pooled transaction buffers sized to peak; snapshot views not copies.
- **Load Time**: None.
- **Network**: None.

## Migration Plan
1. Create `Equilibrium.Simulation.asmdef` (no Unity references beyond `UnityEngine.CoreModule` for math if needed; prefer `System.Numerics`).
2. Implement `TickEngine`, `Transaction`, `TickSnapshot`, `IWorldWriter`, `EventBus` with EditMode tests for the GDD acceptance criteria.
3. Port `ItemContainer`/`ContainerTransfer` into transaction validators and writers.
4. Replace `Conveyor` trigger-collider motion with an `ITickSystem` collector; `ShaftSpriteVisual` consumes events.
5. Delete or archive physics-based movement.

## Validation Criteria
- All tick-simulation GDD acceptance criteria pass as EditMode tests.
- Profiler: 10k conveyor transactions under 4 ms on reference PC; zero GC alloc per tick after warmup.
- Mutation audit: no writes to world state outside `IWorldWriter` (Roslyn analyzer or reflection test).

## Related Decisions
- ADR-0002 tie-break entropy
- ADR-0004 world data model
- ADR-0007 simulation/presentation split
- design/gdd/tick-simulation.md
