# Epics Index

Last Updated: 2026-09-20
Engine: Unity 6000.6.1f1 (URP 17.6, 2D Renderer)
Control Manifest: 2026-09-20.1
Review mode: solo (PR-EPIC, QL-STORY-READY skipped). All TR-IDs traced to Accepted ADRs.

| Epic | Layer | Priority | System | GDD | Stories | Status |
|------|-------|----------|--------|-----|---------|--------|
| tick-simulation | Foundation | MVP | tick-simulation | design/gdd/tick-simulation.md | 5 stories | Ready |
| item-registry | Foundation | MVP | item-registry | design/gdd/item-registry.md | 4 stories | Ready |
| grid-world | Core | MVP | grid-world | design/gdd/grid-world.md | 3 stories | Ready |
| container | Core | MVP | container | design/gdd/container.md | 3 stories | Ready |
| save-load | Core | MVP | save-load | design/gdd/save-load.md | 3 stories | Ready |
| mechanical-power | Core | MVP | mechanical-power | design/gdd/mechanical-power.md | 3 stories | Ready |
| conveyor | Feature | MVP | conveyor | design/gdd/conveyor.md | 3 stories | Ready |
| item-io-accessor | Feature | MVP | item-io-accessor | design/gdd/item-io-accessor.md | 2 stories | Ready |
| machine-processing | Feature | MVP | machine-processing | design/gdd/machine-processing.md | 3 stories | Ready |
| market-interface | Feature | MVP | market-interface | design/gdd/market-interface.md | 4 stories | Ready |
| economy-ledger | Feature | MVP | economy-ledger | design/gdd/economy-ledger.md | 2 stories | Ready |
| placement-editing | Feature | MVP / VS | placement-editing | design/gdd/placement-editing.md | 3 stories (003 is VS) | Ready |
| machine-inspection-ui | Presentation | MVP | machine-inspection-ui | design/gdd/machine-inspection-ui.md | 2 stories | Ready |
| build-hud | Presentation | VS | build-hud | design/gdd/build-hud.md | 1 story (split at sprint) | Ready |
| flow-blocker | Feature | VS | flow-blocker | design/gdd/flow-blocker.md | 1 story | Ready |
| multiblock | Feature | VS | multiblock | design/gdd/multiblock.md | 1 story (split at sprint) | Ready |
| tutorial-task | Polish | VS | tutorial-task | design/gdd/tutorial-task.md | 2 stories | Ready |
| tech-unlock | Feature | Alpha | tech-unlock | design/gdd/tech-unlock.md | 1 story | Ready |
| blueprint | Feature | Alpha | blueprint | design/gdd/blueprint.md | 1 story (split at sprint) | Ready |
| group-processing-devices | Feature | Alpha | group-processing-devices | design/gdd/group-processing-devices.md | 1 story | Ready |
| glass-pipe | Feature | Alpha | glass-pipe | design/gdd/glass-pipe.md | 2 stories | Ready |
| offline-progression | Feature | Full Vision | offline-progression | design/gdd/offline-progression.md | 1 story | Blocked (recompute-method ADR) |

Total: 22 epics, 51 stories (43 Ready MVP+VS+Alpha, 1 Blocked). Story files: `production/epics/[epic]/story-NNN-*.md`; each EPIC.md keeps its header; story tables live in the story files' Dependencies fields.

Not epics (Deferred, no implementation): market-server, industrial-duel.

## Implementation order (dependency-safe; story-level critical path)

1. tick-simulation 001 (assemblies) -> 002 -> 003 -> 004 -> 005; item-registry 001 -> 002 -> 003 -> 004 (parallel with tick 002+)
2. grid-world 001 -> 002 -> 003; container 001 -> 002 -> 003; mechanical-power 001 -> 002; save-load 001 spike (parallel)
3. conveyor 001; item-io-accessor 001; conveyor 002; machine-processing 001 -> 002
4. market-interface 001 -> 002 -> 003; economy-ledger 001 -> 002; save-load 002 -> 003
5. placement-editing 001 -> 002; machine-inspection-ui 001 -> 002; mechanical-power 003; conveyor 003; machine-processing 003 (Tier 1 line in Main.unity); item-io-accessor 002; market-interface 004
6. VS: flow-blocker 001; multiblock 001; placement-editing 003; build-hud 001; tech-unlock 001; tutorial-task 001 -> 002
7. Alpha: group-processing-devices 001; blueprint 001; glass-pipe 001 -> 002
8. Full Vision: offline-progression 001 (after ADR)

## Cross-epic prerequisites

- Sprint 1 spikes: save-load 001 (MessagePack AOT), machine-inspection-ui 001 (UITK binding).
- ADRs before VS: time-control layer; map/chunk model; before Full Vision: offline recompute method.
- `/setup-engine refresh` at sprint 1 start.
