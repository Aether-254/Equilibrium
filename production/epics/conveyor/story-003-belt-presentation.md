# Story 003: Belt rendering, group interpolation, and prototype removal

> **Epic**: Conveyor
> **Status**: In Progress — Main scene sprites and group interpolation
> **Layer**: Feature
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/conveyor.md` (Visual/Audio)
**Requirement**: `TR-conv-001` (no physics; visual sliding)

**Governing ADRs**: ADR-0007
**ADR Decision Summary**: `WorldRenderer` with pooled sprites; group positions interpolate between committed cells; belt bodies in one atlas; remove trigger-collider conveyors.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: Sprite Atlas v2; SRP Batcher with a single material; verify <= 500 draw calls at 4k visible cells.

**Control Manifest Rules (Presentation)**:
- Required: interpolation between committed positions; single atlas; 2 ms budget.
- Forbidden: colliders/rigidbodies for belts or items.

## Acceptance Criteria

- [ ] Belt body sprite per cell from the atlas oriented by direction; chevrons advance one step per tick.
- [ ] Item group sprite slides from cell center to next center over the wall duration of one tick; stalled groups hold position; a jam marker hook is exposed for build-hud.
- [ ] Group sprite position equals the committed cell center at each tick boundary (PlayMode).
- [ ] 4,000 visible belt cells + 200 machines under 500 draw calls and presentation under 2 ms.
- [x] Prototype Conveyor script/prefab retained under the creator's preservation constraint. Main disables
  legacy placement and new belts/items use SpriteRenderers with no physics components.

## Implementation Notes

2026-11-05: `FactoryWorldRenderer` uses original belt art, pooled sprites/text and committed transfer
interpolation. PlayMode verifies midpoint positions and real deliveries; manual step snaps destinations.
Atlas/tread animation, viewport culling and 4k-object performance targets remain open. Temporary text
markers are used pending creator artwork. See `production/qa/evidence/factory-scene-runtime-evidence.md`.

Pool `SpriteRenderer`s per visible chunk; use a `Tilemap` for static belt bodies if cheaper. Interpolation factor `t = (wallTimeSinceTick / tickWallDuration)`; snap to 1 on time-scale change. Count badge for groups > 1 is a small sprite from the atlas digits.

## Out of Scope

- Machine sprites (machine-processing)
- Marker glyphs (build-hud)

## QA Test Cases

- Manual: 10-cell belt with three groups slides smoothly; stalled group holds
- PlayMode: position at boundary == cell center
- PlayMode: draw call count via `UnityStats`/FrameDebugger sample <= 500 at 4k cells
- Manual: Main.unity opens with no missing-script warnings after prototype removal

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/conveyor-visual-evidence.md` with screenshots and draw-call reading
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002; tick-simulation 005
- Unlocks: none
