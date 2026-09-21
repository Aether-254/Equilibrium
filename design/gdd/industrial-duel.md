# Industrial Duel (1v1 Mode)

> **Status**: Deferred
> **Scope**: Full Vision. `design/industrial-duel-concept.md` is the authoritative deferred concept. This GDD is a template-compliant index into it so pipeline skills can trace dependencies; it does not promote any TBD to a rule and does not expand current scope (concept "实施禁区").
> **Author**: creator (concept) + design session (index)
> **Last Updated**: 2026-09-20
> **Implements Pillar**: all four (concept "与项目支柱的关系"); competition through efficiency, never through destruction

## Overview

Two players run independent factories from near-symmetric starts inside one shared economy for a 30-minute match. Competition is indirect: efficiency, changeover speed, capital use, market judgment, contract fulfillment, and resilience. Limited public metrics allow inference without full visibility. Result is a unified "industrial value" (delivered goods value + contract value + remaining assets − liabilities). After the match both factories are revealed for comparison and learning. Everything beyond this summary is TBD in the concept.

## Player Fantasy

Direct. "Not destroying their factory, but making it look inefficient next to yours." The post-match reveal turns a win or loss into a design lesson.

## Detailed Design

### Core Rules

Normative content is `design/industrial-duel-concept.md`:

| Concept section | Topic |
|---|---|
| 核心体验 | symmetric start, shared market, limited information |
| 对局结构 | 0-5 build, 5-25 market and contracts, 25-30 high-demand, post-match reveal |
| 主要系统 | shared market, public contracts, contested resources, information, technology race (no research system), pre-announced shocks |
| 结果计算 | industrial value formula (intent only; valuation rules TBD) |
| 当前范围边界 | Now: record only. Later: evaluate after single-player core is stable. Future: minimal slice |
| 尚未决定 | 9 open items |
| 实施禁区 | binding |

### States and Transitions

Match phases per concept; lobby, disconnect, pause, surrender, timeout rules are TBD.

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| market-server | in | shared economy; requires the remote backend or a match-local shared market |
| economy-ledger | in | public metrics (sales, contract completion, partial import/export, resource consumption windows, market share, total energy) and post-match comparison stats |
| tutorial-task | reuse | contract expression |
| tech-unlock | in | technology race must stay consistent with "no research system" |
| tick-simulation | in | authority, tick consistency, and determinism boundaries are undesigned technical risks |
| grid-world | in | fair-start map configuration (mirror vs statistical symmetry TBD) |

## Formulas

Deferred. Industrial value: `deliveredGoodsValue + contractValue + remainingAssets − liabilities` is a stated intent; valuation of inventory, machines, land, WIP, and debt cost are TBD. Price formation is TBD and must not be fixed for this mode ahead of the single-player market.

## Edge Cases

Deferred. Listed risks (price lock-in, snowballing, symmetry vs fairness, information transparency, last-five-minute swing, single tech path, telemetry consistency, netcode) are risks, not resolved cases.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| Stable single-player core loop | prerequisite | concept "Later" gate |
| tick-simulation, container semantics, market-interface, task/contract expression, economy-ledger metrics, fair map configuration | hard | listed prerequisites in the concept |
| market-server | hard for shared remote market | or a match-local shared market variant |

## Tuning Knobs

Deferred. Match length (30 min stated), phase boundaries, order value curve, shock lead time, public metric windows are all TBD.

## Visual/Audio Requirements

None defined.

## UI Requirements

None defined; no multiplayer UI is to be created.

## Acceptance Criteria

None binding until re-enabled. The only current criterion is procedural:

- **GIVEN** the current milestone, **WHEN** any story or task references this mode, **THEN** it is rejected as out of scope per the concept's forbidden zone.

## Open Questions

All nine items in the concept remain open.
