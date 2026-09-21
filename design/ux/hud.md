# UX Spec: Build HUD

> **Status**: Approved (creator self-review 2026-09-20)
> **Source GDD**: design/gdd/build-hud.md, placement-editing.md, flow-blocker.md
> **Stack**: UI Toolkit for the sidebar, dialogs, economy strip; sprite overlays for world markers (ADR-0006/0007); patterns IP-03, IP-04, IP-05, IP-08, IP-09, IP-12, IP-14
> **Accessibility**: Basic tier

## 1. Layout

```
+----------------------------------------------------------------------------------+
| [Browse B] [Machine M] [Belt C] [Pipe P] [Blocker K] [Carry V] [Remove X]  | $520  +38/min  Q:50 |  <- top bar 48 px: IP-03 sidebar left, IP-09 economy strip right
| Belt objective: (Fastest) (Cheapest) (Balanced)          [Shift: straight] [Alt: no blockers] |  <- context row 32 px, visible in Belt mode; IP-12 chips
+----------------------------------------------------------------------------------+
|                                                                                  |
|                         world view with IP-04 previews and IP-08 markers         |
|                                                                                  |
+----------------------------------------------------------------------------------+
```

Inspection panel (separate spec) docks right and closes when a placement mode is entered.

## 2. Modes and Hotkeys (defaults, all remappable)

| Mode | Key | Cursor behavior |
|---|---|---|
| Browse | B / Esc | click inspects |
| Machine | M | footprint ghost follows cursor; R rotates; click places ghost |
| Belt | C | mouse-down origin; drag; Shift straight; F reverses preview; release creates ghosts |
| Pipe | P | as Belt (Alpha) |
| Blocker | K | side-target preview on hovered cell edge; click installs/removes; drag installs same side across cells (VS) |
| Carry | V | box/click select; drag preview; Enter commits, Esc cancels (VS) |
| Remove | X | click removes; Type I parts trigger IP-05 |

Text field focus suppresses all mode hotkeys.

## 3. Preview and Marker Semantics

- Valid preview: neutral ghost with port arrows. Invalid: red tint + reason label near cursor (`CellOccupied`, `SegmentTooShort`, `OutOfBounds`, `NoPath`, `NetworkAnalysisBudgetExceeded`).
- Path preview: arrows per cell; proposed blockers as dashed bars; open ends as amber rings; failed segment red with reason; last valid path as 30% alpha outline.
- Affected-network highlight: amber outline over all reachable upstream/downstream cells + a summary chip ("joins line feeding Saw #2").
- IP-14 deep analysis button appears on the context row when a budget error is shown.
- World markers in Browse: jam glyph after 2 consecutive stalled ticks; open-end glyphs toggleable (context row toggle in Browse).

## 4. Dialogs (IP-05)

| Trigger | Content | Actions |
|---|---|---|
| Path reuses/joins a working belt | affected networks listed; cell count; possible cross-flow | Confirm / Cancel |
| Blocker removal joins networks | both networks and the merge point | Confirm / Cancel |
| Type I partial removal/carry | highlighted missing parts; data-loss warning | Confirm / Select whole structure / Cancel |
| Irreversible recipe start | inputs that will be lost on cancel | Start / Cancel |

Esc cancels, Enter confirms; dialogs re-validate if the world changes and close with a notice if the premise is gone.

## 5. Economy Strip (IP-09)

Currency balance; profit 60 s and 5 min (selector); queued market volume badge; click opens the market screen. Placeholders until the ledger has data.

## 6. Feedback and Audio

Mode switch tick; invalid release buzz; ghost placed click; build complete chime (soft). All also visible (rule 8).

## 7. Accessibility Compliance

Color + glyph on all previews and markers; remappable keys shown on buttons; text scaling reflows the top bar to two rows at 150%; dialogs keyboard-dismissible; click-click alternative to drag for belts is VS scope (recorded in accessibility open items).

## 8. Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Gamepad cursor mode | ux-designer | VS |
| 2 | Camera edge scroll during drag | ux-designer | VS |
| 3 | Final hotkey map after playtest | ux-designer | VS |
