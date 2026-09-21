# Adoption Plan

> **Generated**: 2026-09-20
> **Project phase**: Pre-Production (as of 2026-09-20 evening)
> **Engine**: Unity 6000.6.1f1, C#, URP 17.6.0 (2D Renderer)
> **Template version**: v1.0+

All blocking, high, and medium items completed 2026-09-20 in a single session
under creator self-review. Retained for audit.

## Step 1: Blocking — DONE
- [x] `design/gdd/` created with `game-concept.md` (Approved)
- [x] `mechanic-brainstorm.md` migrated to template concept; source unchanged

## Step 2: High — DONE
- [x] `systems-index.md` (24 systems; exact status values; Deferred in notes)
- [x] confirmed-mechanics-spec split into 22 GDDs + 2 deferred index GDDs (final split differed from the initial 10-file proposal; mapping table lives in systems-index)
- [x] market-server / industrial-duel indexed as Deferred (GDD index files)
- [x] entities.yaml populated
- [x] ADR-0001..0008 Accepted (exceeded the planned 3)

## Step 3: Infrastructure — DONE
- [x] tr-registry.yaml (68 TR-IDs), requirements-traceability.md, architecture-review report
- [x] control-manifest.md 2026-09-20.1
- [ ] production/sprint-status.yaml — created by `/sprint-plan update` after the first sprint exists
- [x] stage.txt = Pre-Production via gate-check

## Step 4: Medium
- [x] architecture-traceability created
- [ ] technical-preferences Minimum Coverage — set at /test-setup follow-up (suggest 80% on Simulation/Content)
- [ ] first epic against Approved GDDs — next: /create-epics
- [ ] conveyorPrefab unassigned in Main.unity — becomes a story or is removed with the prototype

## Step 5: Optional
- [ ] /setup-engine refresh (6.4-6.6 deltas)
- [ ] spec section 20 relocation to technical-preferences/art-bible s8

## Re-run
`/adopt` may be re-run at any time; remaining items are pipeline-normal, not adoption gaps.
