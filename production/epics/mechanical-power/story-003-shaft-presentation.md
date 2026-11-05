# Story 003: Shaft and gear presentation from committed omega

> **Epic**: Mechanical Power
> **Status**: Ready
> **Layer**: Core
> **Type**: Visual/Feel
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/mechanical-power.md` (Visual/Audio Requirements)
**Requirement**: `TR-mech-001` (direction and speed visible)

**Governing ADRs**: ADR-0007: Simulation/Presentation Split
**ADR Decision Summary**: presentation consumes events and `IWorldView`; shaft frames advance by `omega * dt` from the baked atlas; single Sprite Atlas with Point filter.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: Sprite Atlas v2 packing of the 128 baked frames; verify batching under URP 17 2D Renderer.

**Control Manifest Rules (Presentation)**:
- Forbidden: visuals inferring state; caching truth from animation.
- Required: baked frames stepped from committed omega; editor bake tooling only in `Equilibrium.Editor`.

## Acceptance Criteria

- [ ] `ShaftView` reads per-cell omega from the latest committed batch and advances the frame index by `omega * dt / (2π) * frameCount` with correct sign.
- [ ] Stalled/Idle/Faulted networks show frozen shafts; NoFuel shows engine idle pose.
- [ ] Gear sprites by tooth count (8/16/32 = small/medium/large); direction visible via tooth motion.
- [ ] The 128 baked frames are packed into the project Sprite Atlas (Point filter, PPU 32) and the old per-frame references in `Main.unity` are replaced.
- [ ] Prototype `ShaftSpriteVisual.cs` removed or reduced to a thin `IPresentationSink` consumer.

## Implementation Notes

Keep the existing bake output but re-import as a multi-sprite atlas (spec s20); the baker moves to `Equilibrium.Editor` (done in tick-simulation 001). Frame stepping accumulates fractional frames between ticks for smoothness; snap on time-scale change.

## Out of Scope

- Machine state sprites (machine-processing)
- Belt rendering (conveyor)

## QA Test Cases

- Manual check: omega 10 rad/s vs 5 rad/s
  - Setup: engine + 8:16 gear + shaft on both segments in Main.unity
  - Verify: downstream shaft turns at half the rate and opposite direction
  - Pass: visually confirmed; frame delta per tick equals expected within 1 frame
- Manual check: stall
  - Setup: two saws on one engine
  - Verify: all shafts freeze the tick the stall commits
- PlayMode: frame index after 1 tick at omega 10 equals expected value

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/mechanical-shaft-visual-evidence.md` with screenshots + PlayMode frame test
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002; tick-simulation 005
- Unlocks: none
