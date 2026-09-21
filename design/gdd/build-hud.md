# Build HUD

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Simple Entry / Unfathomable Depth (the HUD shows only what the current mode needs), Realism First (previews never lie about what will be built or connected)
> **Normative Source**: design/confirmed-mechanics-spec.md s11.1, s11.2 (preview/feedback rules), s7.8 (open-end and network-join display); inferred system
> **Note**: Behavior and content here; layout and visuals in `design/ux/hud.md` via `/ux-design`.
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The build HUD is the always-on interface layer for building: the top sidebar of placement modes with hotkeys, the belt objective toggle, placement and path previews with validity and reasons, affected-network highlights, open-end and stalled-cell markers, construction ghost material status, and the confirmation dialogs that placement-editing and flow-blocker require. It also hosts the global economy strip (balance, profit per minute) so the factory-economy link is always on screen. It contains no simulation logic; it renders state from placement-editing, conveyor, flow-blocker, and economy-ledger and forwards player intent.

## Player Fantasy

Direct. The HUD should feel like a drafting table: quiet in Browse, informative in Belt mode, never in the way of the factory itself. The player always knows which mode they are in, what will happen on release, and why something is red.

## Detailed Design

### Core Rules

1. **Mode sidebar** (top of screen): buttons for Machine, Belt, Pipe (Alpha), Blocker, Carry, Remove, and Browse; each shows its hotkey; the active mode is highlighted; entering any placement mode closes the inspection panel.
2. **Belt objective toggle**: visible only in Belt mode; Fastest / Cheapest / Balanced; persists per session.
3. **Placement preview**: footprint ghost with port arrows; valid = neutral tint, invalid = red tint with a reason label (`CellOccupied`, `SegmentTooShort`, `OutOfBounds`, ...).
4. **Path preview**: cells with direction arrows; per-segment validity; proposed blockers shown as icons; open ends marked with a distinct glyph; on failure, the last valid path as a faded outline and the failing segment red with reason (`NoPath`, `NetworkAnalysisBudgetExceeded`).
5. **Affected-network highlight**: when the preview would reuse or join a working belt, highlight the entire affected upstream and downstream in a warning color and show a summary ("joins line A upstream of Saw 2").
6. **Confirmation dialogs**: network-affecting reuse (placement-editing rule 16), blocker removal that joins networks (flow-blocker rule 9), Type I partial carry/removal (multiblock rule 9), irreversible recipe start (machine-processing rule 8). Each lists consequences and offers Confirm / Cancel; Type I dialog also offers "Select whole structure".
7. **World markers** (Browse and build modes): stalled belt cells show a jam glyph; open belt ends show an open-end glyph (toggleable); ghosts show missing materials on hover.
8. **Economy strip**: currency balance, profit over the last 60 s and 5 min, queued market volume indicator; click opens the market screen.
9. **Modifier hints**: while dragging, show active modifiers (Shift = straight, suppress-auto-blocker key) as small chips.
10. **Deeper analysis request**: when `NetworkAnalysisBudgetExceeded` appears, offer a button to run deeper analysis asynchronously; the preview updates when done; commit is disabled for that candidate until then.
11. HUD reads only committed snapshots; previews are editor state, not simulation state.

### States and Transitions

| HUD state | Shows |
|---|---|
| Browse | sidebar, economy strip, world markers |
| Placement(Machine) | + footprint preview, rotate hint |
| Placement(Belt) | + objective toggle, path preview, modifier chips, proposed blockers |
| Placement(Blocker) | + side-target preview |
| Carry | + selection box, move preview, Type I warning |
| Dialog | modal over any state |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| placement-editing | both | mode selection; preview data (cells, validity, reasons, proposed blockers, affected networks); confirmations; deeper-analysis request |
| flow-blocker | both | side preview; join warning |
| multiblock | in | partial-selection warning content |
| conveyor | in | stall and open-end state per cell |
| economy-ledger | in | balance, profit windows, queued volume |
| market-interface | out | open market screen |
| machine-inspection-ui | sibling | mutual exclusion with placement modes |
| tutorial-task | in | may highlight sidebar buttons or cells (VS) |
| Input System | in | hotkeys as actions |

## Formulas

The marker visibility rule is defined as:

`showOpenEnd(cell) = isBeltEnd(cell) ∧ ¬connectedAhead(cell) ∧ openEndMarkersEnabled`
`showJam(cell) = stalled(cell) for ≥ jamMarkerDelayTicks consecutive ticks`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| jam delay | jamMarkerDelayTicks | int | 0..10 | consecutive stalled ticks before marker |

**Output Range:** boolean per cell.
**Example:** delay 2: a one-tick stall from a normal group handoff does not flash.

## Edge Cases

- **If a dialog is open and the world changes** (a ghost builds): dialog content re-validates; if the premise is gone, dialog closes with a notice.
- **If the mouse is over the sidebar while dragging**: the drag continues; the sidebar does not capture until release.
- **If hotkeys conflict with text input** (market quantity field focused): text input wins.
- **If the window is resized narrow**: sidebar collapses to icons; economy strip abbreviates.
- **If economy-ledger has no data**: strip shows placeholders.
- **If too many markers exist** (thousands of open ends): cull to those in view; no per-frame allocation.
- **If a preview cell is off-screen**: still valid; the camera does not auto-scroll (VS may add edge scroll).
- **If the player switches mode mid-drag**: drag cancels; nothing is created.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| placement-editing | hard | primary data source |
| conveyor, flow-blocker, economy-ledger | hard | markers and strip |
| multiblock, market-interface | soft | |
| machine-inspection-ui | sibling | |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| openEndMarkersEnabled | on | bool | missed leaks | clutter |
| jamMarkerDelayTicks | 2 | 0..10 | flicker | late warnings |
| economyStripWindows | 60 s, 5 min | list | - | clutter |
| markerCullDistanceCells | viewport + 4 | - | pop-in | cost |

## Visual/Audio Requirements

Style per art bible (not yet authored). Color semantics shared with inspection UI: red invalid, amber warning, green valid. Glyphs: open end, jam, proposed blocker, ghost missing materials. Audio: mode switch tick; invalid release buzz; confirm/cancel cues.

## UI Requirements

Author `design/ux/hud.md` with wireframes for sidebar, objective toggle, previews, dialogs, economy strip, narrow layout, and the hotkey map.

## Acceptance Criteria

- **GIVEN** Browse mode, **WHEN** the player presses the Belt hotkey, **THEN** the sidebar highlights Belt, the objective toggle appears, and any open inspection panel closes.
- **GIVEN** a Shift drag of one cell, **WHEN** previewed, **THEN** the segment is red with label `SegmentTooShort`.
- **GIVEN** a free drag whose path would join a working line, **WHEN** previewed, **THEN** the affected upstream and downstream cells are highlighted and a summary is shown; on release a confirmation dialog appears.
- **GIVEN** a path with an open input side and blockers in stock, **WHEN** previewed, **THEN** a blocker icon appears on that side; with no stock, an open-end glyph appears instead.
- **GIVEN** a belt cell stalled for 3 ticks, **WHEN** rendered, **THEN** the jam glyph is shown; after a 1-tick stall it is not.
- **GIVEN** a Type I partial removal, **WHEN** the player clicks remove, **THEN** the dialog highlights missing parts and offers "Select whole structure".
- **GIVEN** `NetworkAnalysisBudgetExceeded`, **WHEN** shown, **THEN** a deeper-analysis button exists and commit of that candidate is disabled until analysis completes.
- **GIVEN** any state, **WHEN** the economy strip is read, **THEN** balance and 60 s profit match economy-ledger.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Hotkey map and gamepad partial support | ux-designer | VS ux-spec |
| 2 | Camera edge-scroll during drag | ux-designer | VS |
| 3 | UGUI vs UI Toolkit (shared with inspection UI) | technical-director | ADR |
