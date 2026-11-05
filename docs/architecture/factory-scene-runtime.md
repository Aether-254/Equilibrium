# Factory scene runtime

Updated: 2026-11-05. Scope: the existing Main scene's interactive logistics and first powered saw line.

## Entry and preservation

`Assets/Scenes/Main.unity` contains one explicit `FactoryRuntime` root with a `UIDocument`,
`FactoryHud` and `FactoryInputController`. `FactorySceneSetup` connects the scene through Unity's
Editor API; it is an explicit menu/batch operation, not an import hook or runtime scene generator.
The original GameManager, MachinePlacer, prefabs, cameras, grid and shaft test objects remain.
Only the original MachinePlacer component is disabled to prevent duplicate input processing.
The grid position already present before this change is preserved.

`Assets/Settings/FactoryPresentation.asset` references the original conveyor sprite and the white
prototype machine sprite as a labelled chest stand-in. Wood, accessor and blocker art is not invented:
item counts, `A` and cross glyphs are temporary markers. No existing artwork or importer settings are
changed. `FactoryPanelSettings.asset`, `FactoryHud.uss` and `FactoryTheme.tss` own the runtime HUD.
Chinese HUD text uses an installed system font; no third-party font files are copied into the game.

## Input, construction and inventory

- B/Esc: inspect; M: chest; C: straight belt; I: accessor; K: directional blocker; X: remove.
- 1: saw; 2: fuel engine; 3: straight shaft. R rotates these single-cell placements as well as chests.
  Powered processing details and its current boundaries: `docs/architecture/machine-processing-runtime.md`.
- R rotates a chest preview. The first crossed cell fixes a belt's axis and initial flow. F toggles
  the current drag's flow without committing. Release queues a valid run of at least two cells.
- This slice offers only straight belt drawing, including while Shift is held. Free routing,
  reuse analysis, automatic blockers, machine-port origins and multi-selection carry remain pending.
- A drag stops before an obstacle. Returning across the origin shortens it to an invalid one-cell
  preview. Releasing over UI/outside the grid or losing application focus cancels the drag.
- UI hit testing includes dropdown popups. Text input focus suppresses construction hotkeys.
  Middle mouse pans; the wheel zooms. Space pauses the factory; Single Step commits one full tick.
- Click a belt edge to install an accessor, or toggle that edge's input blocker. The inspector's
  side selector is world-oriented; configuration is converted to the belt's local side.
- Accessor settings expose Auto/deposit/withdraw, Exact/UpTo/Keep, quantity 1..3000 and optional
  canonical ItemId. Apply queues an intent. Footer pending count persists until the next commit.
  Invalid or unavailable ItemIds are rejected in the UI. Deposit still accepts a possible partial
  delivery; the quantity mode applies to extraction.

`FactorySession` owns a local warehouse, the live world and construction intents. Its initial stock
is a configurable play/learning preset: 128 belt cells, 16 chests, 32 accessors, 32 blockers, 256 wood,
128 logs, 4 saws, 4 fuel engines and 32 shafts.
These values are not an approved balance or market decision. It uses the actual Tier 1 registry:
wood maxStack is 64, a chest has four 200-capacity slots, hence 256 effective wood capacity.

Input never changes simulation stock immediately. Collect captures queued commands; its edit batch
explicitly depends on the current processing batch, which depends on logistics. Logistics commits
first, processing next, construction/configuration last; observers update at the tick boundary.
New connectivity is used on the following tick.
This dependency avoids invalidating logistics' mutation-revision guard with an edit earlier in Commit.
Rejected commands report a reason; they do not prevent other independently valid commands in the batch.

Construction validates the entire requested path, reserves its empty cells and consumes one matching
warehouse item per built cell. Lack of materials leaves reserved construction ghosts. Later refunded
materials can complete them during the same commit. Removing an unbuilt ghost cancels its reservation
without refunding unspent materials. Already-queued edits capture their expected BlockRef so a stale
click cannot affect a replacement placed in the same cell.

Removing a chest returns its item with persistent inventory; the next chest placement restores that
bag with the new orientation. Removing a belt returns its group to the local warehouse. Accessories
and blockers are refunded, and ingress bits are reset so a blocker cannot survive its removed block.
This is single-cell removal/rebuilding, not the planned multi-selection carry feature.

## Rendering and inspection

World positions map through `GridOrigin=(-32,-32)` onto a 64x64 logical grid. A cell's center is
`origin + cell + (0.5,0.5)`. Rendering uses pooled SpriteRenderers and TextMeshes; there are no physics
objects in the new belt/item path. Existing prototype physics objects are not deleted or repurposed.

Committed transfer records drive one-second sliding groups. Whole belt-to-belt groups never merge.
Stationary remainders stay on their cell. A container-bound visual disappears at its destination;
the chest count already reflects committed stock. Manual stepping snaps groups to their destination.
Visual objects represent transfers, not additional inventory, and are not a source of simulation truth.
The inspector shows actual slots, effective capacity, flow direction, current-tick stall result and
accessor transfer/reason. Machine inspection now shows measured work, received power, batch count,
reserved inputs and rolling utilization; full throughput and cost reporting remain pending.

## Boundaries

- Play Mode starts a fresh local session. World/warehouse/ghost/accessor save capture and hydration
  are not connected to the existing MessagePack codec yet; leaving Play Mode discards the session.
- Chests, belt cells, attachments, saws, fuel engines and straight shafts are built. Gear networks,
  GlassPipe, market backend and multiblock/carry tools remain separate work.
- No SpriteAtlas optimization, animated belt tread, viewport culling, jam delay or 4k-object
  performance acceptance is claimed. Markers are deliberately temporary art.
- The inspector is a small scrollable logistics panel, not the complete tabbed machine inspector.
- Hotkeys are presently direct Input System bindings, not a finished rebindable action-map UI.

Verification: `production/qa/evidence/factory-scene-runtime-evidence.md`.
