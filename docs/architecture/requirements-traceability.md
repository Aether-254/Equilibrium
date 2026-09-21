# Architecture Review and Requirements Traceability

> **Date**: 2026-09-20
> **Reviewer**: architecture-review (solo mode; creator self-review policy)
> **Inputs**: 24 GDDs, tr-registry.yaml (68 TR-IDs), ADR-0001..0008, docs/registry/architecture.yaml, docs/engine-reference/unity/*
> **Verdict**: PASS with CONCERNS

## 1. Coverage Summary

| Layer | Systems | TR-IDs | Covered by Accepted ADR | Gaps |
|---|---|---|---|---|
| Foundation | tick-simulation, item-registry | 10 | 10 | 0 |
| Core | grid-world, container, save-load, mechanical-power | 18 | 18 | 0 |
| Feature (MVP) | conveyor, item-io-accessor, machine-processing, market-interface, economy-ledger, placement-editing | 22 | 22 | 0 |
| Presentation (MVP) | machine-inspection-ui | 2 | 2 | 0 |
| VS / Alpha / Full Vision | flow-blocker, build-hud, multiblock, tutorial-task, tech-unlock, blueprint, group-processing-devices, glass-pipe, offline-progression, market-server | 16 | 16 | 0 |
| Deferred | industrial-duel | 0 | n/a | n/a |

Foundation layer gaps: **0** (gate requirement satisfied).

## 2. Traceability Matrix (TR -> ADR)

See `tr-registry.yaml` `adrs:` field per entry. Summary by ADR:

| ADR | TR-IDs addressed |
|---|---|
| 0001 Tick Transaction Engine | tick-001..002, tick-004..005, mech-001, mech-003, conv-002..004, acc-002..003, mach-002..003, mkt-002, ledg-001..002, place-001, place-004, blk-001, task-001, gpd-001, off-001, save-001 |
| 0002 Tie-Break Entropy | tick-003, save-003, place-003 (forbidden use), off-001 |
| 0003 Item Identity / Content | item-001..005, cont-002, mach-001, mkt-004, task-001, unlk-001 |
| 0004 World Data Model | grid-001..004, cont-001..005, mech-001..002, mech-004, conv-001, conv-004, acc-001, acc-003, mach-002, mkt-003, place-001..004, blk-001, multi-001..002, bp-001, gpd-001, pipe-001 |
| 0005 Save Format | item-005, grid-002, cont-004, save-001..005, mkt-001, ledg-001, multi-002, unlk-001, pipe-001, off-001 |
| 0006 UI Technology Stack | mach-004, insp-001..002, blk-002, hud-001 |
| 0007 Sim/Presentation Split | tick-004, conv-001, hud-001, pipe-002 |
| 0008 Market Backend | save-004, mkt-001..004, ledg-003, off-001, msrv-001 |

## 3. Cross-ADR Conflict Check

- Dependency graph: 0001 <- 0002, 0003(none), 0004 <- {0001,0003}, 0005 <- {0001,0003,0004}, 0006 <- 0007, 0007 <- {0001,0004}, 0008 <- {0001,0003,0005}. **Acyclic.**
- State ownership: 5 states, no double claims.
- Interface contracts: 6, no pattern conflicts (all transaction/event-bus based; market backend direct_call is internal to MarketSystem).
- API decisions: no two ADRs choose different APIs for the same purpose.
- Performance budgets: tick 4 ms (1 Hz, amortizable) + presentation 2 ms + UI 1 ms = 3 ms per frame steady state, under 8.3 ms. A tick landing in one frame costs up to 7 ms; still under budget but leaves 1.3 ms. **CONCERN C1**: consider spreading Commit/Emit across two frames if profiling shows spikes.

## 4. Engine Compatibility Audit

| Check | Result |
|---|---|
| All ADRs stamp engine 6000.6.1f1 | Yes |
| All ADRs have Engine Compatibility, ADR Dependencies, GDD Requirements sections | 8/8 |
| Deprecated API usage (deprecated-apis.md): legacy Input, UGUI for new UI, ComponentSystem | None used; UGUI explicitly forbidden (ADR-0006) |
| Post-cutoff APIs flagged | UI Toolkit runtime binding (0006, HIGH), Addressables 2.x (0003, MEDIUM), URP Render Graph if custom pass (0007), MessagePack AOT (0005, MEDIUM) |
| HIGH-risk domains addressed in architecture.md section 7 | Yes |

**CONCERN C2**: `docs/engine-reference/unity/` was verified against 6.3 LTS; 6.4-6.6 deltas unverified. Run `/setup-engine refresh` before the first implementation sprint.

## 5. GDD <-> ADR Naming Sync

Checked Key Interfaces against GDD terminology: `IWorldView`/`IWorldWriter`, `ITickSystem`, `Transaction`, `CommittedEvent`, `ITieBreakSource`, `ContentRegistry`/`ItemId`, `ISaveable`/`ISaveMigration`, `IMarketBackend`, `IUiPresenter`. GDDs use behavior-level names (e.g. "acceptable(item)", "effectiveCapacity", "P_recv") that ADRs preserve. No renames requiring GDD edits.

## 6. Concerns and Actions

| # | Concern | Severity | Action |
|---|---|---|---|
| C1 | Tick + frame budget coincidence | Low | Profile; amortize Commit/Emit if needed |
| C2 | Engine reference lags pinned version | Medium | `/setup-engine refresh` |
| C3 | No independent review of GDDs or ADRs (solo) | Accepted by creator | none |
| C4 | Time-control and map/chunk model have no ADR yet | Medium | ADRs before Vertical Slice |
| C5 | MessagePack and UI Toolkit binding unverified on 6.6 | Medium | Spike in first sprint; fallbacks documented |

## 7. Verdict

**PASS with CONCERNS.** Foundation and Core coverage complete; no conflicts; no deprecated APIs. Proceed to `/create-control-manifest`, `/test-setup`, `/art-bible`, `/ux-design`.
