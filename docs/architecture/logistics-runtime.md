# Logistics runtime and workbench

Date: 2026-11-04

## Implemented scope

The existing world tables now run a complete chest-to-chest logistics line through `TickEngine`.
`LogisticsSystem` handles conveyor movement and belt-mounted accessor transfers as one atomic
system batch. It uses real `WorldState`, `WorldWriter`, item definitions, slot capacities, filters,
reservations, rotated ports and the existing per-tick entropy source. It does not use Unity physics.

Source files live under `Assets/Scripts/Simulation/World/`:

- `LogisticsSystem.cs`: candidate collection, arbitration, dependency chains and committed results.
- `LogisticsSlots.cs`: reusable scratch inventory, original-stock budgets and tentative insertion/tidy.
- `LogisticsTypes.cs`: immutable accessor settings, transfer records, status records and machine admission hook.
- `WorldWriter.Logistics.cs`: accessor configuration and atomic plan application.

## Tick integration

1. Collect snapshots slot item/count/reservation availability and the world's mutation revision.
   It submits one `PayloadKind.LogisticsBatch` transaction. It does not change world inventories.
2. After ordinary transaction validation/resolution, the engine invokes `ITickBatchPlanner.Plan`
   during Resolve using the same tick's `ITieBreakSource`.
3. The logistics planner resolves accessor deposits before lower-priority forward delivery, reserves
   higher-priority accessor intake claims, arbitrates competing belt inputs and resolves whole-group
   dependency chains. Accessor intake then uses the resulting current-tick stall condition.
4. All accepted belt sources are removed in scratch data before any belt destinations are populated.
   This gives A=100, B=200, C=empty -> A=empty, B=100, C=200 without merging or repeated hopping.
5. `WorldWriter` verifies that the world still has the captured mutation revision, then applies the
   scratch slot values in Commit. A stale batch fails before inventory writes.
6. `LogisticsSystem` publishes `Transfers`, `BeltResult` and `AccessorResult` during Emit only after
   successful commit. `LastBatchSucceeded` reports a rejected batch. Register it with `Attach(engine)`
   before registering presentation listeners that consume those results.

The generic event bus emits one batch event. Detailed item transfers are exposed by the logistics
system's committed span; they are not additional generic `Payload.Items` events. A ledger must consume
the transfer span in Emit instead of interpreting the batch event as an individual transfer. Like
`EventBus`, these spans are reused next tick and must be copied by consumers that retain history.

## Rules covered

- Belt input ports are behind and on both sides; there is only one forward output. Ports and attachments
  rotate with the block. Opposing outputs, air and disabled ingress do not connect.
- A belt slot holds one item type, with capacity `min(3000, itemDef.maxStack)`. Belt-to-belt transfers
  move the original whole group, never merge, never split and never move newly arrived stock again.
- Equal-priority belt contenders use the production per-tick entropy stream. Tests inject fixed keys
  without changing production RNG or persisting it.
- Ordinary chests require accessors. Machine transfers require the matching, correctly facing port;
  direct transfer also requires `AcceptsDirectBelt`. `ILogisticsPortPolicy` lets the machine system
  reject a transfer with its current admission reason; no machine lifecycle is synthesized here.
- Accessor deposit accepts the possible part and leaves the remainder stalled. A direct machine port
  accepts the whole belt group or nothing.
- Accessor withdrawal supports Auto / BeltToContainer / ContainerToBelt, optional ItemId, Exact / UpTo /
  Keep and an amount clamped to 1..3000. `AmountClamped` lets a future settings UI show a warning.
- Exact requires both stock and full target space. Keep counts reserved stock toward the retained
  total but cannot extract reserved items. Newly delivered stock cannot be re-extracted this tick.
- An accessor may refill a vacated cell with a new group. It may add to an existing group only when
  the original group has a current-tick stalled remainder of the same ItemId.
- Shared containers use the staged remaining capacity and original-stock budget. Tentative tidy is
  rolled back if an Exact insertion cannot finish; reserved source slots are never moved by tidy.
- Removing a belt removes its accessor block attributes, including after block-index recycling.

## Workbench

Open **Equilibrium > Logistics > Open Workbench** in Unity. Play Mode is not required.
`Assets/Editor/LogisticsWorkbench.cs` is an Editor-only UI Toolkit window and is excluded from Player
builds. It creates an independent, disposable world; it never saves or rewrites `Main.unity`.

The preset has an input chest with 128 wood, four belts, two Auto accessors and an output chest. It
uses the actual Tier 1 wood maxStack of 64. Each chest has four slots with slot capacity 200, so its
effective total wood capacity is 256. These values are a diagnostic preset, not new balance decisions.

Controls: run/pause at one tick per second, single step, block/reopen the middle ingress, collect output,
reset, or load a full-output scenario. Clicking a cell shows committed quantities, capacity, accessor
direction and stall reasons. Actual transfer records drive one-second sliding quantity labels. Cell
labels show the committed final inventory while moving labels visualize the preceding transfer; the
moving labels are not additional inventory. The original conveyor sprite is reused without new art.

Closing/resetting the workbench discards its world. Production input bindings, world rendering, build
tools and the full machine inspection UI are separate integrations.

## Current boundaries

- Structural and logistics systems do not yet share fine-grained resource claims. A world mutation
  committed before the logistics batch conservatively rejects that entire batch as `CommitFault`.
  Setup, configuration and workbench edits use the existing writer boundary APIs between ticks.
  The production scene's `FactorySession` now makes its edit batch explicitly depend on the current
  logistics batch. Building/configuration commits afterward and is next-tick connectivity input.
  Other future machine systems must coordinate their writes before enabling concurrent commands.
- Intake claims have priority +1. Claims that later lose shared-source contention are not rerolled
  within the same tick; lower-priority belt losers retry next tick. Container delivery is planned
  before withdrawals, so it does not reuse capacity freed by a later container withdrawal in that tick.
- A fully occupied dependency cycle is rejected, matching the current tick engine cycle policy.
  Loops with a vacancy circulate; a four-cell loop with one group has a 100-tick regression test.
- Existing segment minimum-length enforcement and malformed-save warnings belong to placement/load.
  This implementation can simulate individual cells and does not claim placement validation complete.
- Accessor settings are live block attributes only. Save capture/hydration for them is still pending,
  as is live-world capture for the previously completed save codec.
- The workbench remains independent. Main now has an explicit runtime entry, straight construction,
  accessor settings and pooled world visuals; see `factory-scene-runtime.md`. Legacy placement input
  is disabled in Main while its component, script and prefab references are preserved.
- The 2,000-cell cold test is measurement evidence, not acceptance of the under-1-ms steady-state target.

## Verification

See `production/qa/evidence/logistics-runtime-evidence.md` for commands, exact results and remaining
environment/test limitations. The implementation does not mark the full conveyor/accessor epics complete.
