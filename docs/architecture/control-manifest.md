# Control Manifest

> **Manifest Version**: 2026-09-20.1
> **Source**: ADR-0001..0008 (all Accepted), docs/registry/architecture.yaml, .claude/docs/technical-preferences.md, docs/engine-reference/unity/deprecated-apis.md
> Stories embed this version; `/story-done` flags staleness.

Flat rules for programmers. "MUST" = blocking in code review. "SHOULD" = default unless an ADR says otherwise.

---

## Global

### Required
- MUST place code in the assembly its layer owns: `Equilibrium.Content`, `.Content.Unity`, `.Simulation`, `.Editing`, `.Presentation`, `.UI`, `.Editor`, `.Tests.EditMode`, `.Tests.PlayMode` (ADR-0007).
- MUST implement every GDD acceptance criterion as a test before marking a story done (coding standards).
- MUST read gameplay values from content data (ScriptableObject/JSON); never hardcode tuning numbers (coding standards, ADR-0003).
- MUST use Input System actions; MUST NOT call `UnityEngine.Input.*` (deprecated-apis).
- MUST reference items by `ItemId` (interned handle at runtime, canonical string in data); MUST NOT use display names as identity.
- SHOULD use `System.Numerics` math in non-Unity assemblies.

### Forbidden
- No `UnityEngine` references in `Equilibrium.Simulation`, `.Content`, `.Editing` (ADR-0007).
- No editor-only code outside `Equilibrium.Editor` / `Assets/Editor` (spec s20).
- No `Co-Authored-By` trailers, tool signatures, or emoji in commits, docs, or comments (user policy).

---

## Simulation layer (`Equilibrium.Simulation`)

### Required
- MUST express every state change as a `Transaction` submitted from `ITickSystem.Collect` (ADR-0001).
- MUST read only `TickSnapshot` during Collect; never live world tables.
- MUST declare transaction dependencies when correctness depends on another transaction committing first (conveyor chains).
- MUST set explicit `Priority` (default 0) and document non-zero priorities in the system's GDD.
- MUST compute container acceptance with `Capacity.Effective(slot, def)` = `min(slot.capacity, def.MaxStack)` (ADR-0004).
- MUST resolve equal-priority conflicts through `ITieBreakSource.NextKey()` only inside Resolve (ADR-0002).
- MUST emit one `CommittedEvent` per committed transaction; consumers rely on it.
- MUST keep steady-state tick processing allocation-free (pooled buffers).
- MUST bind Ports to containers/slots by string key in definitions; resolve to indices at load.

### Forbidden
- No writes to world tables outside `IWorldWriter.Apply` (registry: direct_world_state_write).
- No `UnityEngine.Random`, `System.Random(time)`, or world-state hashes for tie-breaks (registry: predictable_tie_break_seed).
- No persistence of `ITieBreakSource` state.
- No physics components or callbacks for logistics (registry: physics_driven_logistics).
- No merging or splitting of item groups belt-to-belt; only group-processing devices regroup.
- No proximity-based connection lookup; adjacency only through facing ports and ingress bits.
- No reads of animation or presentation state to decide simulation outcomes.

### Guardrails
- Transaction cycle detection MUST reject with `TransactionCycle`, never hang.
- CSPRNG failure MUST halt the tick with a visible error after one retry; never fall back.
- Table compaction only between ticks; `BlockRef` valid for one tick.

---

## Content layer (`Equilibrium.Content`, `.Content.Unity`)

### Required
- MUST validate `ItemId` against `^[a-z0-9][a-z0-9._-]*:[a-z0-9][a-z0-9._/-]*$`; MUST canonicalize `equilibrium:` omission at parse time.
- MUST treat duplicate `ItemId`, Tag cycles, undeclared cross-namespace modification, missing namespace as load rejections with the GDD error codes.
- MUST apply Tag edits with remove-wins and parent-removal cascading to children.
- MUST run the same validator on ScriptableObject exports and JSON Addon packs.
- MUST produce `UnknownItem` factories for missing namespaces.
- SHOULD generate the JSON schema from the plain record types; round-trip test required.

### Forbidden
- No `#common:` handling in base content.
- No content field that only exists in the SO path or only in the JSON path.

---

## Persistence (`SaveService`, `ISaveable`)

### Required
- MUST save only at tick boundaries via the `TickPump` hook (ADR-0005).
- MUST serialize with MessagePack-CSharp AOT resolvers; string keys; `Extras` bag on every record.
- MUST version every section; MUST add an `ISaveMigration` and fixture for every schema change.
- MUST write temp then rename; rotate backups.
- MUST refuse newer schema and unsupported world size; MUST offer backups on corruption.

### Forbidden
- No array indices for containers/slots/ports in saves (registry: index_based_serialization).
- No Tag expansions or TagIds in place of concrete ItemIds (registry: persist_tag_expansion).
- No RNG state, no time-control state in simulation sections.

---

## Editing layer (`Equilibrium.Editing`)

### Required
- MUST produce structural transactions (ghost, build, remove, move, blocker toggle); MUST NOT mutate world directly.
- MUST enforce belt segment min length 2, occupancy, facings, blocker rules in preview validity.
- MUST bound pathfinding by node budget and recompute rate; MUST NOT block the frame.
- MUST re-validate reuse analysis against latest world state at commit; stale analysis never commits.
- MUST use the deterministic tie-break comparator (fewer turns, fewer open ends, fewer blockers, closest to last preview, stable coords).

### Forbidden
- No `ITieBreakSource` usage (registry: tie_break_source_in_editing_or_preview).
- No auto-deleting, overwriting, or moving obstacles on invalid release.

---

## Presentation (`Equilibrium.Presentation`)

### Required
- MUST consume `CommittedBatch` + `IWorldView`; MUST interpolate positions between committed ticks.
- MUST use one Sprite Atlas (Point filter, PPU 32, 32x32 tiles) for logistics tiles and overlays; one material per atlas.
- MUST render world markers (jam, open end, port arrows, ghosts, proposed blockers, network highlight) as sprites, not UI Toolkit.
- MUST keep presentation under 2 ms/frame at 4k visible cells and <= 500 draw calls.
- MUST advance shaft animation frames from committed omega.
- If a custom render pass is added, MUST use URP 17 Render Graph API, not legacy `Execute`.

### Forbidden
- No caching of simulation truth derived from animation (registry: visuals_inferring_state).
- No generated placeholder art for missing pipe topology masks (glass-pipe rule 15).

---

## UI (`Equilibrium.UI`)

### Required
- MUST use UI Toolkit (`UIDocument`, UXML/USS) for all screen-space UI (ADR-0006).
- MUST update view models on `CommittedBatch`/selection events, not per frame.
- MUST keep the inspection status bar visible across tabs; MUST order ReasonCodes by the registered priority.
- MUST suppress hotkeys while a text field has focus.
- MUST source all player-facing strings from a string table key.
- MUST stay under 1 ms/frame with the inspection panel open.

### Forbidden
- No new UGUI Canvas or TextMeshPro UI (registry: ugui_for_new_ui).
- No UI code that submits transactions directly; UI calls Editing/Simulation intents.

---

## Market (`MarketSystem`, `IMarketBackend`)

### Required
- MUST route Sell/Buy through tick transactions; Sell moves to escrow first; settlement idempotent by `EscrowId`.
- MUST reserve full warehouse capacity before submitting a buy; reject otherwise.
- MUST read prices/caps from content (`MarketItemDef`).
- MUST emit `MarketSettled/Queued/Rejected` events for the ledger.

### Forbidden
- No `EconomyLedger` call into market (registry: ledger_writes_to_market).
- No network code until the remote backend is re-enabled (spec s15); `RemoteMarketBackend` stays a stub.

---

## Testing

### Required
- EditMode tests for all Content/Simulation/Editing acceptance criteria; PlayMode for Presentation/UI.
- Deterministic tests use `FixedTieBreakSource` (test assembly only).
- Performance criteria (tick 4 ms, mech solve 1 ms, container 0.5 ms, ledger 0.5 ms) as benchmark tests, advisory in CI.
- Save fixture corpus loads across all migrations in CI.

### Forbidden
- No tests depending on execution order, wall clock, or unseeded randomness.
- No skipping failing tests to make CI green.

---

## Engine version notes (Unity 6000.6.1f1)

- Verify before use: UI Toolkit runtime data binding API; Addressables 2.x label loads; MessagePack-CSharp AOT on IL2CPP; URP 17 Render Graph.
- Reference docs verified against 6.3 LTS; run `/setup-engine refresh` at sprint 1 start.
