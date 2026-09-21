# Art Bible — Equilibrium

> **Status**: Sections 1-4 Approved (creator self-review 2026-09-20); sections 5-9 to be authored before Production
> **Created**: 2026-09-20
> **Source**: game-concept.md, confirmed-mechanics-spec s7.9, s20; existing assets under Assets/Art (GlassPipe masks, Conveyor sprites, Shaft bake)
> **AD-ART-BIBLE**: skipped (solo mode)

---

## 1. Visual Identity Foundation

### One-line visual rule
**Every pixel is an instrument reading**: the picture shows the true state of the machine, nothing decorative that could be mistaken for state, and nothing hidden that the player needs to reason about.

### Supporting principles
1. **Legible before beautiful.** Direction, connection, fill level, rotation speed, and state must be readable at the default zoom without opening a panel. Ornament never competes with signal.
2. **Real materials, flat light.** Surfaces read as wood, cast iron, brass, glass, and steel through value and texture, not through painted highlights; a fixed top-down key light keeps every tile consistent across rotations.
3. **Motion is data.** Anything that moves on screen moves because a committed transaction happened: belts slide one cell per tick, shafts rotate at true omega and direction, pipes fill by amount. Idle things are still.
4. **Consistent pixel grid.** 32 x 32 px per cell, PPU 32, Point filtering; overlays share the canvas and rotate around its center. No sub-pixel placement, no mixed resolutions.
5. **Color says category, not decoration.** Hue distinguishes material classes and states (see section 3); everything else is neutral so state colors stay unmistakable.

### Mood
Workshop-quiet industrial: a clean drafting table over a working shop floor. Not grimy dieselpunk, not toy-bright. Confidence and control, with warmth from wood and brass.

## 2. Style and References

- **Perspective**: strict top-down orthographic 2D (spec s7.9 uses a top-down mask model; pipes show liquid and gas as stacked full-cell layers).
- **Rendering**: URP 2D Renderer, sprites from one atlas; 3D-sourced rotating parts (shafts, gears) are pre-baked to sprite frames via the existing shaft baker so they read as true rotation without runtime 3D.
- **Line and shape**: 1 px dark outlines on machines and parts; belts and pipes outline-less so long runs read as continuous.
- **Reference touchstones**: Create (readable mechanical motion), Factorio (belt/inserter legibility at density), technical schematics and P&ID drawings (symbol discipline), brass-and-oak laboratory instruments (material warmth).
- **Anti-references**: cartoon squash-and-stretch, painterly lighting, particle-heavy VFX that obscure tiles, isometric perspective.

## 3. Color

### Neutral base
| Role | Hex | Use |
|---|---|---|
| Ground | #2B2F33 | grid floor |
| Grid line | #383D42 | 1 px cell borders at high zoom only |
| Steel | #8A9096 | machine bodies, shafts |
| Cast iron | #4A4E52 | heavy machine frames |
| Wood | #A8783C | conveyor bodies, crates, fuel |
| Brass | #C9A24A | gears, ports, accessors |
| Glass | #BFE3F0 at 35% alpha | pipe walls |

### State colors (reserved; never used decoratively)
| State | Hex | Where |
|---|---|---|
| Working / valid | #4CAF50 | status bar, valid preview |
| Starved / warning | #FFB300 | status bar, open-end glyph, affected-network highlight |
| Blocked | #FB8C00 | status bar, jam glyph |
| Stalled / faulted / invalid | #E53935 | status bar, invalid preview, blocked accessor arm |
| Idle | #9E9E9E | status bar |
| Ghost | base color at 40% alpha + #90CAF9 tint | construction ghosts |

### Substance colors (pipes)
Assigned per `SubstanceId` in content; must be distinguishable from all state colors and from each other at 35-100% alpha over the glass base. Reserve #4CAF50/#FFB300/#FB8C00/#E53935 exclusively for states.

## 4. Shape Language and Readability Rules

- **Machines**: rectangular bodies filling the cell with a 2 px inset; port sides marked by a brass notch (input) or a steel arrow (output); rotating machines show the rotation-in port as a gear stub.
- **Belts**: full-width wood body with two 2 px steel rails; chevrons in travel direction animate one chevron per tick; item groups draw as a single stacked-icon sprite with a count badge when > 1.
- **Shafts and gears**: baked frames from the 3D source; gear tooth count reflected by radius (8/16/32 teeth = small/medium/large); rotation direction visible from tooth motion.
- **Accessors**: brass arm on the belt edge pointing at the target; arrow glyph shows resolved flow direction.
- **Blockers**: belt skin = dark bar across the edge; pipe skin = brass cap. Same logical item, two skins (spec s20).
- **Pipes**: glass walls; internal masks per topology; liquid layer then gas layer; crossing bridge uses two independent masks (spec s7.9).
- **Markers**: open-end glyph (amber ring at the open side), jam glyph (orange chevron stack), proposed blocker (dashed bar), affected-network highlight (amber outline over cells).
- **Readability minimums**: any two adjacent states must differ in both hue and value; every glyph must survive 50% zoom-out; text never appears inside tiles.

## 5. Machine and Part Catalog
[To be authored]

## 6. Animation Rules
[To be authored: belt chevron cadence, shaft frame stepping, machine state loops, ghost build sweep]

## 7. UI Visual Style (UI Toolkit theme)
[To be authored: panel chrome, status bar, tab styling, typography]

## 8. Asset Production Pipeline
[To be authored: source formats, atlas packing, shaft bake procedure and provenance note, naming]

## 9. Review Checklist
[To be authored]
