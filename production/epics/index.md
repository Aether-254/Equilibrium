# Epics Index

Last Updated: 2026-09-20
Engine: Unity 6000.6.1f1 (URP 17.6, 2D Renderer)
Control Manifest: 2026-09-20.1
Review mode: solo (PR-EPIC skipped). All TR-IDs traced to Accepted ADRs; no untraced requirements.

| Epic | Layer | Priority | System | GDD | Stories | Status |
|------|-------|----------|--------|-----|---------|--------|
| tick-simulation | Foundation | MVP | tick-simulation | design/gdd/tick-simulation.md | Not yet created | Ready |
| item-registry | Foundation | MVP | item-registry | design/gdd/item-registry.md | Not yet created | Ready |
| grid-world | Core | MVP | grid-world | design/gdd/grid-world.md | Not yet created | Ready |
| container | Core | MVP | container | design/gdd/container.md | Not yet created | Ready |
| save-load | Core | MVP | save-load | design/gdd/save-load.md | Not yet created | Ready |
| mechanical-power | Core | MVP | mechanical-power | design/gdd/mechanical-power.md | Not yet created | Ready |
| conveyor | Feature | MVP | conveyor | design/gdd/conveyor.md | Not yet created | Ready |
| item-io-accessor | Feature | MVP | item-io-accessor | design/gdd/item-io-accessor.md | Not yet created | Ready |
| machine-processing | Feature | MVP | machine-processing | design/gdd/machine-processing.md | Not yet created | Ready |
| market-interface | Feature | MVP | market-interface | design/gdd/market-interface.md | Not yet created | Ready |
| economy-ledger | Feature | MVP | economy-ledger | design/gdd/economy-ledger.md | Not yet created | Ready |
| placement-editing | Feature | MVP / VS | placement-editing | design/gdd/placement-editing.md | Not yet created | Ready |
| machine-inspection-ui | Presentation | MVP | machine-inspection-ui | design/gdd/machine-inspection-ui.md | Not yet created | Ready |
| build-hud | Presentation | VS | build-hud | design/gdd/build-hud.md | Not yet created | Ready |
| flow-blocker | Feature | VS | flow-blocker | design/gdd/flow-blocker.md | Not yet created | Ready |
| multiblock | Feature | VS | multiblock | design/gdd/multiblock.md | Not yet created | Ready |
| tutorial-task | Polish | VS | tutorial-task | design/gdd/tutorial-task.md | Not yet created | Ready |
| tech-unlock | Feature | Alpha | tech-unlock | design/gdd/tech-unlock.md | Not yet created | Ready |
| blueprint | Feature | Alpha | blueprint | design/gdd/blueprint.md | Not yet created | Ready |
| group-processing-devices | Feature | Alpha | group-processing-devices | design/gdd/group-processing-devices.md | Not yet created | Ready |
| glass-pipe | Feature | Alpha | glass-pipe | design/gdd/glass-pipe.md | Not yet created | Ready |
| offline-progression | Feature | Full Vision | offline-progression | design/gdd/offline-progression.md | Not yet created | Ready (blocked on recompute-method ADR) |

Not epics (Deferred, no implementation): market-server, industrial-duel.

## Implementation order (dependency-safe)

1. tick-simulation, item-registry (parallel)
2. grid-world, container, mechanical-power; save-load spike in parallel
3. conveyor, item-io-accessor, machine-processing
4. market-interface, economy-ledger
5. placement-editing (MVP subset), machine-inspection-ui
6. VS: build-hud, flow-blocker, placement-editing (full), multiblock, tutorial-task
7. Alpha: tech-unlock, group-processing-devices, blueprint, glass-pipe
8. Full Vision: offline-progression

## Cross-epic prerequisites

- Sprint 1 spikes before epics 2 and 5: MessagePack AOT on 6.6; UI Toolkit runtime data binding.
- ADRs before VS: time-control layer; map/chunk model.
- Assembly definitions (ADR-0007) are created in the first tick-simulation story.
