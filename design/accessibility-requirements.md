# Accessibility Requirements

> **Status**: Approved (creator self-review 2026-09-20)
> **Committed Tier**: Basic (Standard is the stretch target for Polish)
> **Platform**: PC, keyboard/mouse primary, partial gamepad

## Tier Definition

| Tier | Commitment |
|---|---|
| **Basic (committed)** | Full keyboard remapping via Input System; UI text scaling 100-150%; no information conveyed by color alone (every state color pairs with a glyph or label); no time-limited inputs in the core loop (ticks pause; UI never auto-dismisses); subtitles n/a (no speech); screen-reader labels on UI Toolkit controls where the platform supports it |
| Standard (stretch) | Colorblind-safe alternative palettes for substance colors; high-contrast theme; camera zoom to 200%; reduced-motion option that snaps belt interpolation and shaft frames |
| Full | Not targeted |

## Rules Binding on All UX Specs

1. Every state (Working, Starved, Blocked, Stalled, Faulted, Idle, Ghost) MUST be represented by color plus glyph or text; state colors from the art bible are never the sole signal.
2. All hotkeys MUST be remappable; the HUD shows current bindings.
3. All UI text MUST scale with a global setting from 100% to 150% without clipping; panels reflow or scroll.
4. No interaction MUST require holding a key for a timed duration or reacting within a real-time window; drag operations may be completed with click-click as an alternative (VS).
5. Focus order in UI Toolkit panels MUST be logical (top to bottom, left to right) and visible.
6. Confirmation dialogs MUST be dismissible by keyboard (Esc = cancel, Enter = confirm).
7. Minimum body text size 14 px at 100% scale on a 1080p reference; minimum interactive target 24 x 24 px.
8. Audio is never the sole cue for any event.

## Open Items
- Gamepad navigation of the grid (cursor mode) is Vertical Slice scope.
- Colorblind palette for substances requires the substance list (Tier 3).
