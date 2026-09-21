# Project Stage Analysis

**Date**: 2026-09-20
**Stage**: Concept
**Stage Confidence**: PASS — `production/stage.txt` is set to `Concept` and auto-detection agrees. Concept material exists but no `design/gdd/game-concept.md`, no systems index, and no ADRs. Code volume (12 scripts) would otherwise suggest Production, but it is a learning prototype without formalized design, so the code count is not a stage signal here.

## Completeness Overview

- Design: ~35% — 4 non-template drafts (1463 lines total): `design/mechanic-brainstorm.md` (concept + pillars), `design/confirmed-mechanics-spec.md` (658 lines of MUST/MUST NOT rules across several systems), `design/market-server-spec.md` (implementation forbidden zone), `design/industrial-duel-concept.md` (deferred concept). `design/gdd/` does not exist. `design/registry/entities.yaml` is an empty template.
- Code: ~20% — `Assets/Script/` has 12 C# files (886 LOC incl. editor tooling) covering grid placement, conveyor motion, item/container/transfer primitives, shaft sprite bake. No assembly definitions. `MachinePlacer.conveyorPrefab` unassigned in `Main.unity`.
- Architecture: ~5% — `docs/architecture/` contains only an empty `tr-registry.yaml`. 0 ADRs, no control manifest, no architecture overview.
- Production: ~30% — `adoption-report.md`, `timeline.md`, `stage.txt`, `review-mode.txt` (solo). No sprints, milestones, or roadmap.
- Tests: 0% — no `Assets/Tests/`, no EditMode/PlayMode tests, no test assemblies.

## Engine

Unity 6000.6.1f1, URP 17.6.0 (2D Renderer), C#. Configured via `/setup-engine` on 2026-09-20. Knowledge risk HIGH; reference docs verified against 6.3 LTS only.

## Gaps Identified and Decisions

| # | Gap | Decision (2026-09-20) |
|---|-----|-----------------------|
| 1 | Design drafts do not follow the 8-section GDD template | Split: migrate `mechanic-brainstorm.md` into `design/gdd/game-concept.md` (+ pillars), and split `confirmed-mechanics-spec.md` into per-system GDDs. Original files remain as source until migration is verified. |
| 2 | `market-server-spec.md` and `industrial-duel-concept.md` are out of current implementation scope | Include in systems-index as Deferred entries so they are tracked, not lost. |
| 3 | `Assets/Script/` code has no matching GDD | `confirmed-mechanics-spec.md` is authoritative. Existing code is a prototype to be refactored toward the spec, not reverse-documented as design truth. |
| 4 | No ADRs despite de-facto architecture decisions in the spec (Tick transaction atomic commit, non-reproducible tie-break RNG, namespaced ItemId) | Write ADRs now via `/architecture-decision` rather than waiting for `/create-architecture`. |
| 5 | `.gitignore` excludes `.claude/`, `CLAUDE.md`, and framework state | Intentional. All framework surfaces stay local; only `design/`, `docs/`, `production/` artifacts are tracked. |
| 6 | No tests for `ItemStack` / `ItemContainer` / `ContainerTransfer` | Defer `/test-setup` until GDDs are stable. |

## Recommended Next Steps

1. `/adopt` — audit the 4 drafts for template compliance and produce the migration plan (gaps 1, 2).
2. `/map-systems` — decompose brainstorm + confirmed spec into `design/gdd/systems-index.md`, marking MVP vs Deferred.
3. `/design-system retrofit` — author each system GDD from the corresponding spec section; register cross-system facts in `design/registry/entities.yaml`.
4. `/architecture-decision` (x3) — ADRs for Tick transaction model, tie-break entropy source, ItemId namespace/registration (gap 4).
5. `/gate-check` — Concept -> Systems Design once game-concept and systems-index exist.
6. Later: `/test-setup`, then EditMode tests for the container/transfer domain (gap 6).

## Open Risks

- Unity compile / Play Mode behavior of `Main.unity` still unverified (no connected Editor during adoption).
- Engine reference docs lag the pinned version (6.3 LTS docs vs 6000.6.1f1); run `/setup-engine refresh`.
- Worktree is dirty with unrelated Unity template deletions; keep design/production changes in separate commits.
