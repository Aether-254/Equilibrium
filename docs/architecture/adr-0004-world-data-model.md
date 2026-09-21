# ADR-0004: World Data Model (Grid, Containers, Ports, Networks)

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1 |
| **Domain** | Core |
| **Knowledge Risk** | LOW for this ADR: plain C# data structures |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None beyond profiling |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001, ADR-0003 |
| **Enables** | ADR-0005, ADR-0007 |
| **Blocks** | grid-world, container, conveyor, mechanical-power, multiblock epics |
| **Ordering Note** | Data layout is shared by all simulation systems; finalize before porting conveyor |

## Context

### Problem Statement
The GDDs describe a strict cell grid, blocks with block attributes versus persistent attributes, Containers of Slots with effective capacity and filters, Ports bound by string keys, an explicit adjacency/ingress-edge graph, and mechanical networks. The current prototype stores everything on GameObjects. We need a data model that the tick engine can snapshot cheaply, that saves by stable keys, and that supports headless tests.

### Constraints
- 64x64 Tier 1, must scale to at least 512x512 without redesign.
- Snapshot views must be O(dirty) not O(world).
- No GameObject or MonoBehaviour in the simulation assembly.

### Requirements
- Cell occupancy, block instance, orientation, controller pointer.
- Containers with Slots (ItemId handle, count, capacity, filters), string keys resolved to handles at load.
- Ports with kind, facing, bound container/slot key, direct-vs-accessor flag.
- Adjacency edges with per-side ingress enable (blockers).
- Mechanical network graph built from rotation ports; solved per tick.

## Decision

Struct-of-arrays world in `Equilibrium.Simulation`:

- `WorldGrid`: `int[] cellToBlock` (block instance index or -1), chunked 16x16 with dirty bits.
- `BlockTable`: parallel arrays per block instance: `BlockDefHandle`, `Orientation`, `ControllerCoord`, `ContainerRange` (start,count into `SlotTable`), `PortRange`, persistent attribute bag index.
- `SlotTable`: `ItemId.Handle`, `Count`, `Capacity`, `FilterId`, `Reserved` per slot; `containerKey`/`slotKey` strings live only in definitions and are resolved to indices at load; saves emit keys.
- `PortTable`: `Kind`, `LocalFacing`, `ContainerLocalIndex`, `SlotLocalIndex`, `AcceptsDirectBelt`.
- `EdgeTable`: for each cell, 4 `IngressEnabled` bits; adjacency is implicit from grid coordinates (no stored neighbor lists); blockers flip bits.
- `MechNetworks`: rebuilt incrementally on structural commits via union-find over rotation ports; per network cached segment list; solved per tick by the mechanical-power system.
- `PersistentAttributeBag`: per block, a small typed map serialized on drop; block attributes are never serialized into drops.

Read views (`ref readonly` accessors) are the `TickSnapshot`; `IWorldWriter` is the only mutator (ADR-0001).

### Architecture Diagram
```
WorldGrid(cells) -> BlockTable -> {SlotTable ranges, PortTable ranges, PersistentBag}
                 -> EdgeTable(ingress bits per side)
                 -> MechNetworks(union-find over rotation ports)
```

### Key Interfaces
```csharp
public readonly struct CellRef { public readonly int X, Y; }
public readonly struct BlockRef { public readonly int Index; }
public interface IWorldView {
    BlockRef BlockAt(CellRef c);
    Orientation OrientationOf(BlockRef b);
    ReadOnlySpan<Slot> Slots(BlockRef b, ContainerLocal ci);
    Port Port(BlockRef b, PortLocal pi);
    bool IngressEnabled(CellRef c, Side side);
    bool Connected(BlockRef a, PortLocal outPort, out BlockRef target, out PortLocal inPort);
}
public static class Capacity { public static int Effective(in Slot s, in ItemDef def) => Math.Min(s.Capacity, def.MaxStack); }
```

## Alternatives Considered

### Alternative 1: GameObject-per-block with components
- **Description**: Continue the current prototype.
- **Pros**: Inspector visibility; fast to hack.
- **Cons**: Not snapshot-able; not headless; allocation heavy; order-dependent.
- **Rejection Reason**: Incompatible with ADR-0001.

### Alternative 2: DOTS Entities
- **Description**: Blocks as entities; buffers for slots.
- **Pros**: Cache-friendly, Burst.
- **Cons**: See ADR-0001 alt 2.
- **Rejection Reason**: Deferred; the SoA layout here is DOTS-shaped enough to migrate later.

### Alternative 3: Stored neighbor lists per block
- **Description**: Explicit adjacency lists.
- **Pros**: Fast lookup for irregular graphs.
- **Cons**: Redundant for a grid; must be maintained on every structural change.
- **Rejection Reason**: Grid adjacency is computable; only ingress bits need storage.

## Consequences

### Positive
- Cheap snapshots, headless tests, straightforward serialization by keys.
- Blocker semantics are a bit flip; no neighbor rediscovery (flow-blocker GDD rule 2).

### Negative
- Less convenient debugging than Inspector; needs a debug overlay (ADR-0007).
- Table compaction on block removal must preserve indices used by transactions in flight (handled at tick boundary).

### Risks
- Index invalidation when compacting: compact only between ticks; transactions hold `BlockRef` valid for one tick.
- Persistent bag growth: typed small map with schema per block def.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| grid-world.md | Rules 1-13; connected predicate | `WorldGrid`, `PortTable`, `EdgeTable`, `Connected()` |
| container.md | Rules 1-10; effective capacity; string-key binding | `SlotTable`, `Capacity.Effective`, key resolution at load |
| conveyor.md | Cell as one-slot container | Belt block def with one slot capacity 3000 |
| flow-blocker.md | Rule 2 ingress edge disable without rediscovery | `EdgeTable` bits |
| multiblock.md | Controller coordinate as block attribute | `BlockTable.ControllerCoord` |
| mechanical-power.md | Network topology rules 1-4 | `MechNetworks` union-find |
| save-load.md | Rules 4-5 keys not indices; block vs persistent attributes | Key resolution and `PersistentAttributeBag` |

## Performance Implications
- **CPU**: O(dirty chunks) snapshot; O(1) adjacency.
- **Memory**: 512x512 grid ints = 1 MB; tables proportional to blocks.
- **Load Time**: Key resolution pass at load.
- **Network**: None.

## Migration Plan
1. Implement tables and `IWorldView` with EditMode tests for grid-world and container acceptance criteria.
2. Port `ItemContainer`/`ItemStack` logic into `SlotTable` operations.
3. Presentation reads `IWorldView` to place sprites (ADR-0007).

## Validation Criteria
- grid-world and container GDD acceptance criteria pass.
- Snapshot of a 512x512 world with 10 dirty chunks under 0.2 ms.

## Related Decisions
- ADR-0001, ADR-0003, ADR-0005, ADR-0007
