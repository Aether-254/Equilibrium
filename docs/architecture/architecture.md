# Equilibrium — Master Architecture

> **Status**: Accepted
> **Created**: 2026-09-20
> **Engine**: Unity 6000.6.1f1, C#, URP 17.6.0 (2D Renderer)
> **Knowledge Risk**: HIGH (engine version post-cutoff). Every ADR lists post-cutoff APIs and verification steps.
> **Inputs**: design/gdd/*.md (24 GDDs), design/gdd/systems-index.md, docs/architecture/adr-0001..0008, design/confirmed-mechanics-spec.md, .claude/docs/technical-preferences.md

---

## 1. Architecture Summary

Equilibrium is a deterministic-by-construction factory simulation. All gameplay
state lives in a struct-of-arrays world owned by a pure C# tick engine that
advances in 1-second ticks. Every state change is a transaction collected against
an immutable snapshot, validated, resolved by explicit priority and a
non-reproducible tie-break, and committed atomically. Presentation, UI, ledger,
unlocks and tasks consume committed events; none of them may write simulation
state. Content is identity-first (namespaced ItemIds, hierarchical Tags) and
loaded from ScriptableObjects (base) and JSON (Addons) into one registry. Saves
are MessagePack documents keyed by stable strings with unknown-field
preservation and ordered migrations. The market is reached only through a
replaceable backend interface; MVP ships a local fixed-price backend.

## 2. Layer Model

| Layer | Assembly | Unity refs | Contents |
|---|---|---|---|
| Content | `Equilibrium.Content` | none | ItemId, ContentRegistry, defs, Tag graph, Addon loader, validator |
| Content.Unity | `Equilibrium.Content.Unity` | yes | ScriptableObject defs + exporter, Addressables loading |
| Simulation | `Equilibrium.Simulation` | none | TickEngine, world tables, all ITickSystems (container, conveyor, accessor, machine, mechanical power, market, multiblock, blockers), ledger, unlock/task evaluators, save sections |
| Editing | `Equilibrium.Editing` | none | Placement rules, belt pathfinder, reuse analysis, carry mode logic, blueprint transform (produces structural transactions) |
| Presentation | `Equilibrium.Presentation` | yes | TickPump, WorldRenderer, sprite pools, interpolation, world markers, shaft animation, offline report presenter |
| UI | `Equilibrium.UI` | yes | UI Toolkit panels, view models, presenters |
| Editor | `Equilibrium.Editor` (Assets/Editor) | editor | Shaft baker, content export, debug overlays, validators as menu items |
| Tests | `Equilibrium.Tests.EditMode`, `.PlayMode` | test | GDD acceptance criteria as tests |

Dependency direction: Content <- Simulation <- Editing; Presentation and UI
depend on Simulation/Editing read-only surfaces; nothing depends on
Presentation/UI. Editor depends on everything but is not shipped.

## 3. Runtime Flow (one tick)

```
TickPump (wall clock, TimeScale, Paused) -> TickEngine.Advance()
  1 Snapshot     : TickSnapshot over world tables (dirty-chunk aware)
  2 Collect      : ITickSystem.Collect for Mechanical, Machine, Conveyor, Accessor, Blocker/Structural, Market, Multiblock
  3 Validate     : per-transaction validators (capacity, filters, ports, reservations, funds)
  4 Resolve      : dependency topo-sort; conflicts by (-priority, TieBreakSource.NextKey())
  5 Commit       : IWorldWriter applies accepted transactions
  6 Emit         : CommittedBatch -> EventBus
Consumers: WorldRenderer (interpolate), UI presenters (view models), EconomyLedger (windows), TechUnlock, TaskEvaluator, SaveService (boundary), OfflineRecompute (reuses engine)
```

Mechanical power is solved inside Collect as a network solve on the snapshot;
machines read delivered power from that solve within the same tick.

## 4. System Map (GDD -> module -> ADR)

| GDD | Module (assembly) | Governing ADRs |
|---|---|---|
| tick-simulation | TickEngine, TieBreakSource (Simulation) | 0001, 0002 |
| item-registry | ContentRegistry, AddonLoader (Content, Content.Unity) | 0003 |
| grid-world | WorldGrid, BlockTable, PortTable, EdgeTable (Simulation) | 0004 |
| container | SlotTable, ContainerOps (Simulation) | 0004 |
| save-load | SaveService, ISaveable, migrations (Simulation) | 0005 |
| mechanical-power | MechNetworks, MechanicalSystem (Simulation) | 0001, 0004 |
| conveyor | ConveyorSystem (Simulation) | 0001, 0004, 0007 |
| item-io-accessor | AccessorSystem (Simulation) | 0001, 0004 |
| machine-processing | MachineSystem, RecipeMatcher (Simulation) | 0001, 0003, 0004 |
| market-interface | MarketSystem, IMarketBackend, LocalNpcMarketBackend (Simulation) | 0008, 0005 |
| economy-ledger | EconomyLedger (Simulation) | 0001, 0008 |
| placement-editing | PlacementRules, BeltPathfinder, ReuseAnalyzer, CarryMode (Editing) | 0004, 0002 (forbidden), 0007 |
| machine-inspection-ui | InspectionPanel + InspectionViewModel (UI) | 0006 |
| flow-blocker | EdgeTable ingress bits; BlockerSystem (Simulation) | 0004 |
| build-hud | HudRoot, presenters (UI); world markers (Presentation) | 0006, 0007 |
| multiblock | StructureDetector, MultiblockSystem (Simulation) | 0004 |
| tutorial-task | TaskEvaluator (Simulation); TaskPanel, PonderViewer (UI) | 0001, 0006 |
| blueprint | BlueprintCapture/Stamp (Editing) | 0004, 0005 |
| group-processing-devices | DeviceSystem (Simulation) | 0001, 0004 |
| tech-unlock | UnlockEvaluator (Simulation) | 0001, 0003 |
| glass-pipe | PipeContents tables, PipeFlowSystem (Simulation); pipe material (Presentation) | 0004, 0005, 0007 |
| offline-progression | OfflineRecompute (Simulation) reusing TickEngine | 0001, 0002, 0005, 0008 |
| market-server | RemoteMarketBackend (stub) | 0008 (deferred) |
| industrial-duel | none (deferred) | — |

## 5. Data Model Summary (ADR-0004)

- `WorldGrid.cellToBlock[]` chunked 16x16 with dirty bits.
- `BlockTable`: def handle, orientation, controllerCoord, container range, port range, persistent bag.
- `SlotTable`: ItemId handle, count, capacity, filter, reserved.
- `PortTable`: kind, local facing, container/slot local index, acceptsDirectBelt.
- `EdgeTable`: 4 ingress bits per cell.
- `MechNetworks`: union-find over rotation ports; per-network segments.
- `PipeContents` (Alpha): components[] with SubstanceId, amount, phase fraction; T, P, volumes.
- Keys: definitions carry `containerKey`/`slotKey` strings; runtime resolves to indices; saves emit keys (forbidden: index-based serialization).

## 6. Cross-Cutting Concerns

- **Determinism**: everything deterministic except `TieBreakSource` (OS CSPRNG per tick). Pathfinding/preview use deterministic comparators only.
- **Persistence**: tick-boundary saves; MessagePack + LZ4; `Extras` on every record; ordered migrations with fixtures; UnknownItem/UnknownBlock for missing content.
- **Content/Addons**: namespace DAG load; remove-wins Tag edits; one validator for SO and JSON paths; error codes shared with GDDs.
- **Time control**: TimeScale/Paused in TickPump; ticks integral; visuals interpolate.
- **Offline**: same engine; approved methods (replay, event-driven, piecewise steady state) with constraint equivalence tests.
- **Input**: Input System actions; UI Toolkit focus suppresses hotkeys.
- **Localization**: string table keys from day one (UI).
- **Performance budgets**: 8.3 ms frame; tick 4 ms (1 Hz), presentation 2 ms, UI 1 ms; 500 draw calls via single atlas.
- **Testing**: EditMode for Content/Simulation/Editing (pure C#); PlayMode for Presentation/UI; every GDD acceptance criterion maps to a test.

## 7. HIGH-Risk Engine Domains (VERSION.md) and Disposition

| Domain | Risk | Disposition |
|---|---|---|
| DOTS/Entities 1.3 | HIGH | Not used (ADR-0001 alt 2 rejected; SoA layout keeps migration possible) |
| Input System default | MEDIUM | Used exclusively; legacy Input forbidden |
| URP 17 Render Graph | HIGH | Only relevant if a custom pass is added for pipe masks (ADR-0007); use Render Graph API, not legacy Execute |
| UI Toolkit runtime binding | HIGH | Adopted (ADR-0006) with manual-binding fallback behind view models; verify at implementation |
| Addressables 2.x | MEDIUM | Base pack label load (ADR-0003); verify API |
| MessagePack-CSharp on Unity 6.6 IL2CPP | MEDIUM | ADR-0005; verify AOT; JSON fallback behind ISaveable |

## 8. Required ADR List and Status

| ADR | Title | Status | Covers |
|---|---|---|---|
| 0001 | Tick Transaction Engine | Accepted | Foundation |
| 0002 | Tie-Break Entropy Source | Accepted | Foundation |
| 0003 | Item Identity, Content Format, Addon Loading | Accepted | Foundation |
| 0004 | World Data Model | Accepted | Foundation/Core |
| 0005 | Save Format | Accepted | Core |
| 0006 | UI Technology Stack | Accepted | Presentation |
| 0007 | Simulation/Presentation Split, Assemblies | Accepted | Foundation |
| 0008 | Market Backend Interface | Accepted | Feature |

Future ADRs (not blocking Pre-Production): time-control semantics (speed/pause vs real processes, concept OQ2); map/chunk model (concept OQ3); offline recompute method selection (Tier 4); fluid contents abstraction (Alpha); remote market client (deferred).

## 9. Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Map model and chunking | technical-director | Vertical Slice |
| 2 | Time-control layer design | game-designer | Vertical Slice |
| 3 | Fluid contents as Container extension vs parallel tables | systems-designer | Alpha |
| 4 | MessagePack / UI Toolkit binding API verification results | lead-programmer | first implementation sprint |
| 5 | Whether Tier 1 builds consume materials | economy-designer | MVP tuning |

## 10. Migration From the Prototype

`Assets/Script/` (MachinePlacer, Conveyor, ItemDef, ItemStack, IContainer, ItemContainer, ContainerTransfer, IItemContainerOwner, ItemSource, StorageBox, ItemTransferLink, ShaftSpriteVisual) is a learning prototype. Migration: create asmdefs (ADR-0007); port ItemStack/ItemContainer/ContainerTransfer semantics into SlotTable ops with EditMode tests; replace physics Conveyor with ConveyorSystem; convert ShaftSpriteVisual to an event consumer; move MachinePlacer input handling into Editing + Presentation; archive the rest under `prototypes/learning-2026/`.
