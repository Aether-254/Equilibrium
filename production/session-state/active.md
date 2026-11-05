# Active Session State

## Current Task
Stage: incremental development in the existing Unity project. Current milestone: first powered saw line in Main.

<!-- STATUS -->
Epic: Machine Processing / Mechanical Power / Build HUD
Feature: powered saw processing with live logistics
Task: recipe reservation, fuel/straight-shaft power, completion and runtime inspection; full mechanical networks and live-world save integration remain pending
<!-- /STATUS -->

## Progress (all 2026-09-20)
- [x] /start, /setup-engine (Unity 6000.6.1f1, C#, URP 2D)
- [x] /project-stage-detect, /adopt
- [x] game-concept.md (+ Visual Identity Anchor), systems-index.md (24 systems, 13 MVP)
- [x] 24 GDDs, 22 Approved (creator self-review), 2 Deferred indexes; entities.yaml populated
- [x] ADR-0001..0008 Accepted; docs/registry/architecture.yaml populated
- [x] docs/architecture/architecture.md; architecture-review + requirements-traceability; tr-registry (68 TR-IDs); control-manifest 2026-09-20.1
- [x] Assets/Tests EditMode/PlayMode asmdefs + smoke tests; .github/workflows/tests.yml
- [x] design/art/art-bible.md s1-4; accessibility-requirements (Basic); ux/interaction-patterns; ux/machine-inspection; ux/hud
- [x] gate-check: Concept CONCERNS, Systems Design CONCERNS, Technical Setup PASS w/ CONCERNS -> stage.txt = Pre-Production
- [~] /vertical-slice, /prototype, /playtest-report — WAIVED by creator: no sub-projects; implement directly in the main project
- [x] /create-epics all: 22 epics in production/epics/ + index.md (2 Deferred systems excluded)
- [x] /create-stories all: 51 stories across 22 epics (offline-progression 001 Blocked on ADR)
- [x] /sprint-plan new: production/sprints/sprint-1.md, production/sprint-status.yaml, production/qa/qa-plan-sprint-1.md
- [x] Sprint 1 Must Have DONE (2026-09-21): tick-simulation 001-005, item-registry 001-004. The original gate had 69 purecheck passes; the later 2026-09-23 Unity CLI regression now covers the real asmdef boundary as recorded below.
- [x] Sprint 1 Nice to Have GW-001 DONE (2026-09-23): world tables, structural transactions, immutable O(dirty) chunk snapshot deltas, occupancy/orientation/controller state, compaction. Purecheck 79 passed + 3 Unity-only skips; Unity EditMode 91 passed + 1 debug-define skip; 10-dirty-chunk rebuild 0.0570 ms.
- [x] Sprint 1 Should Have: save-load 001 (MessagePack spike), completed 2026-11-04
- [ ] Sprint 1 Should Have: machine-inspection-ui 001 (UITK spike)
- [ ] Creator: open Unity, run Equilibrium/Content/Generate Tier 1 Assets, run Test Runner, report failures
- [ ] Sprint 1 spikes: MessagePack AOT on 6.6; UITK runtime binding; /setup-engine refresh
- [ ] ADRs before implementation of grid/time: time-control; map/chunk model

## Progress (2026-11-04)

- [x] Save-load 001 schema/codec: MessagePack 3.1.12 + LZ4; canonical item strings, concrete reservations, byte-exact unknown values, detached JSON inspection.
- [x] Repaired prior NuGet extraction importer settings; no package DLLs moved or deleted. Final Unity EditMode: 114 passed, 1 existing debug-only skip, 0 failed; all 23 SaveLoad cases passed.
- [x] Final PureCheck: 101 passed, 3 Unity-only skips, 0 failed.
- [x] Windows IL2CPP probe build and Player execution passed, both exit 0; marker verifies LZ4, concrete reservations, unknown members, raw extensions and JSON export. Temporary scene removed and previous project settings restored.

## Progress: logistics implementation (2026-11-04)

- [x] Added atomic logistics batch, whole-group movement, side inputs, current-tick stalled merges,
  Exact/UpTo/Keep accessors, shared container budgets and explicit machine-port admission hook.
- [x] Added Editor-only UI Toolkit workbench: independent real simulation, single step / 1 Hz run,
  ingress toggle, output collection, full-output preset, inspection and committed-transfer sliding labels.
- [x] PureCheck: 133 passed, 3 existing Unity-only skips, 0 failed. Unity EditMode logistics subset:
  all 33 passed (32 logic cases plus workbench integration). Full EditMode: 146 passed, 1 skipped,
  1 existing oversized-save test failed with OutOfMemoryException; not a clean full-suite pass.
- [x] Unity PlayMode: 2 passed, 1 existing catalog-generation skip, 0 failed. Actual TickPump frame
  advancement moved the original group once, respected pause and delivered on the next tick.
- [x] Production scene/renderer and basic runtime accessor UI integrated 2026-11-05; save hydration remains pending.
- [x] The oversized-save case passed its isolated Unity rerun without code changes (1/1). The initial
  full-run OOM remains recorded; its exact root cause is not established.
- [x] Clean full EditMode run on 2026-11-05: 158 passed, 1 existing skip, 0 failed, including oversized-save case.
- [ ] Under-1-ms steady-state logistics benchmark.

## Progress: Main scene integration (2026-11-05)

- [x] Explicit FactoryRuntime/UIDocument root added through Editor API; original MachinePlacer component
  disabled while all legacy scripts, prefabs, scene objects and prior grid edits are preserved.
- [x] Finite local construction stock; chest placement, minimum-two-cell straight belt preview, F reverse,
  obstacle clamp, queued build/remove, missing-material ghosts, persistent chest recovery, attachment refunds.
- [x] Runtime toolbar, hotkeys, inspection, directional accessor/blocker installation, exact/up-to/keep
  controls and optional ItemId. UI input and text focus suppress world edits.
- [x] Original belt sprite, labelled chest placeholder and pooled transfer/count glyphs with no physics.
- [x] PureCheck 144 passed / 3 Unity-only skips / 0 failed; full EditMode 158 passed / 1 skip / 0 failed.
- [x] Main scene PlayMode integration initially 2/2 passed. Final full PlayMode with expanded UI checks:
  4 passed / 1 existing skip / 0 failed, CLI exit 0 using -nographics. Real Game View rendering inspected.
- [ ] Earlier graphical test-run shutdown crash did not reproduce in the final run; root cause unknown.
- [ ] Full routing/reuse/auto-blockers, multi-selection carry, powered processing, final art and save hydration.

Current implementation and verification evidence: `docs/architecture/factory-scene-runtime.md` and
`production/qa/evidence/factory-scene-runtime-evidence.md`. Expanded UI interaction tests are recorded there.

## Progress: first powered production line (2026-11-05)

- [x] Processing batch commits after logistics and before edits. Concrete input reservations, stable
  recipe selection/pinning API, cancellation, irreversible guard, work integration and pending output batches.
- [x] Single-source fuel engine and straight-shaft adjacency solve with content parameters, strict
  overload stall, fractional fuel accounting and conservation on the final partial fuel tick.
- [x] Main toolbar/placement and machine inspector; reserved inputs, progress, power, torque load,
  completed batches and rolling utilization. Temporary labelled art only.
- [x] Final PureCheck 168 passed / 3 skipped; full Unity EditMode 182 passed / 1 skipped; full Unity
  PlayMode 5 passed / 1 skipped. No failed tests; all runners exited 0.
- [ ] Gears, full multiple-source compatibility/sharing, cached topology, allocation/performance goals,
  final animation, ledger events and live-world save hydration remain pending.
- [x] Creator confirmed single transmission loss on 2026-11-05. Path efficiency increases source
  demand; received power is torque * local angular speed. One 99% shaft: 303.03 W source -> 300 W saw.
  Code, mechanical GDD, formula registry and acceptance cases updated together.

## Progress: repository publishing checkpoint (2026-11-05)

- [x] Added a project README with an actual Game View image, setup, controls, implementation boundaries
  and test entry points. Added a separately written LINUX DO introduction with publication notes.
- [x] Creator selected ARR (All Rights Reserved). Added LICENSE; the project is not advertised as
  open source and the post does not use the open-source-promotion declaration.
- [x] Removed an unimplemented save metadata type reference that prevented compilation. Retained
  the completed named-slot capture/restore and guarded tick-index restoration foundation.
- [x] Added regressions for detached working-machine captures with protected reservations and
  resumed event/boundary tick numbers. No live-world disk save/load feature is claimed.
- [x] Final PureCheck: 170 passed, 3 Unity-only skips, 0 failed. Unity EditMode: 184 passed, 1 skip,
  0 failed. Unity PlayMode: 5 passed, 1 skip, 0 failed. All runners exited 0.
- [x] CGS validate still reports 8 pre-existing template/state failures. docs-impact exits 0 but
  its stale src mapping misses Assets changes; manual documentation review remains necessary.
- [ ] Live-world SaveService, atomic file writes, backups and UI integration remain the next
  implementation slice. Publishing this checkpoint does not mark save-load Story 002 done.

Detailed evidence and publication scope: `production/qa/evidence/publish-checkpoint-evidence.md`.

## Key Decisions
- confirmed-mechanics-spec.md authoritative over game-concept.md on conflict.
- Assets/Script/ contains preserved prototypes; new runtime lives under Assets/Scripts. No prototype
  moving/deletion is authorized by this integration; Main only disables the old placement input.
- market-server, industrial-duel Deferred.
- Review mode solo; creator self-reviews everything; NO approval/review prompts.
- Design: pure power integration; fuel engine; fixed-price market with spread + volume cap; CSPRNG per tick.
- Architecture: pure C# TickEngine + SoA world; SO base + JSON addons; MessagePack saves; UI Toolkit + sprite markers; 8 asmdefs; no DOTS; no physics logistics.

## Open Questions
- Map model / chunking (ADR before VS).
- Time-control layer (ADR before VS).
- Fluid contents abstraction (Alpha).
- Tier 1 build materials free or not (MVP tuning).

## Documentation Impact

Decision: updated

Documents:
- `README.md`
- `LICENSE`
- `docs/community/linux-do-intro.md`
- `production/qa/evidence/publish-checkpoint-evidence.md`
- `design/confirmed-mechanics-spec.md`
- `design/gdd/mechanical-power.md`
- `design/registry/entities.yaml`
- `docs/architecture/factory-scene-runtime.md`
- `docs/architecture/machine-processing-runtime.md`
- `production/epics/machine-processing/story-001-recipes-and-reservation.md`
- `production/epics/machine-processing/story-002-work-completion-states.md`
- `production/epics/machine-processing/story-003-sprites-and-tier1-line.md`
- `production/epics/mechanical-power/story-001-network-and-segments.md`
- `production/epics/mechanical-power/story-002-torque-stall-power-fuel.md`
- `production/qa/evidence/machine-processing-evidence.md`

Reason: Main now exposes fuel-powered saw processing, material reservation and machine inspection.
Documented transaction composition, finite content stock, persistence limits, drivetrain scope and
remaining acceptance. Full epics remain incomplete. The stale src mapping means docs-impact alone
does not detect all Assets changes; manual review covers the actual source and UI files.
The publication checkpoint adds ARR copyright terms, contributor setup, actual controls, a separate
community introduction and fresh test evidence; it explicitly retains the live-save limitation.
