# UX Spec: Machine Inspection Panel

> **Status**: Approved (creator self-review 2026-09-20)
> **Source GDD**: design/gdd/machine-inspection-ui.md (behavior and content are binding there); data from machine-processing, mechanical-power, container, economy-ledger
> **Stack**: UI Toolkit (ADR-0006); patterns IP-01, IP-02, IP-05, IP-06, IP-07, IP-10, IP-11, IP-13
> **Accessibility**: Basic tier (design/accessibility-requirements.md)

## 1. Purpose and Entry

Opened by left-click on a block in Browse mode. One instance; clicking another block retargets. Closed by Esc, close button, entering any placement mode, or removal of the block (with a 2 s notice toast). The panel is docked to the right edge, 360 px wide at 100% scale, full height minus the HUD sidebar; it never covers the selected block (camera nudges if needed, VS).

## 2. Layout

```
+------------------------------------------+
| [icon] Saw #12          Working  STALLED |  <- IP-01 status bar (color+glyph+label+ReasonCode), 40 px
+------------------------------------------+
| Overview | Logistics | Mechanics | Economy|  <- IP-02 tabs, 32 px
+------------------------------------------+
| tab body (scrolls)                        |
|                                           |
+------------------------------------------+
| [Pin recipe v] [Cancel work]   [Why?]     |  <- actions row, 40 px; Why? = IP-13 Ponder link for the top ReasonCode
+------------------------------------------+
```

Narrow (< 900 px viewport width): panel becomes a bottom sheet 40% height; tabs collapse to icons with tooltips; status bar remains full width.

## 3. Tab Contents

### Overview (default)
- Row: state + ReasonCode list (all active codes, priority order; top one is already in the bar).
- Row: current recipe name and icon; pin selector (dropdown of applicable recipes, "Auto" default).
- Row: progress ring (IP-11) with `workDone / workRequired` and estimated ticks remaining at current `P_recv`.
- Block: "Biggest bottleneck (last 60 s)": ReasonCode, loss value, suggestion text from the data table, Ponder link.
- Non-machines: chest shows capacity summary; belt cell shows group ItemId, count, state; engine shows fuel and network load.

### Logistics
- IP-10 slot lists grouped by Container (Input, Output, Byproduct, Internal) with headers.
- Counters: blocked ticks, starved ticks in the 60 s window.
- Blocker status: four-side indicator (N/E/S/W) for belt/pipe cells; toggle affordance (VS).
- Accessor target: IP-06 flow mode, filter picker, IP-07 quantity mode + amount.

### Mechanics (machines, engines)
- Table: omega (RPM; rad/s toggle in settings), tau_req, tau_avail, load % with a horizontal bar colored by state thresholds (< 60 grey, 60-95 green, > 95 amber, > 100 red), eta_path.
- Rates: theoretical vs actual batches/min; utilization % bar.
- Engine extra: fuel slot (IP-10 single slot), fuel/tick, P_deliv.
- Trend sparkline for load % (capability unlocked via tech-unlock; hidden until then).

### Economy
- Unit cost of current product; revenue, cost, profit contribution for 60 s / 5 min / session selector.
- Losses by ReasonCode as a sorted bar list.
- Trend chart placeholder (window TBD, spec s13).

## 4. Interactions

| Action | Input | Result |
|---|---|---|
| Switch tab | click / Tab-cycle hotkey (default Q/E) | tab changes; status bar unchanged |
| Pin recipe | dropdown | intent -> transaction next tick; panel shows pending state until commit |
| Cancel work | button | if recipe irreversible: IP-05 dialog listing lost inputs; else immediate intent |
| Edit accessor | IP-06/IP-07 controls | intent -> transaction next tick; field shows last committed value + pending marker |
| Why? | button | opens Ponder for the top ReasonCode; save unchanged |
| Close | Esc / X | panel closes |

All numeric fields validate on commit (amount 1..3000; warn if amount > effective capacity).

## 5. States and Feedback

- Status bar colors: Working #4CAF50, Starved #FFB300, Blocked #FB8C00, Stalled/Faulted #E53935, Idle #9E9E9E; each with its glyph (check, hourglass, stop-bar, lightning-slash, dash) so color is never alone.
- Pending edits show a small clock glyph until the committing tick.
- Empty ledger (first minute): "collecting" placeholders, no zeros.
- Values refresh on `CommittedBatch` (1 Hz); no per-frame updates.

## 6. Accessibility Compliance

- Color + glyph on every state (rule 1). Text scales 100-150% with panel reflow (rule 3). Esc/Enter on dialogs (rule 6). Focus order top-to-bottom (rule 5). Minimum 14 px text, 24 px targets (rule 7). Tab-cycle hotkeys remappable (rule 2).

## 7. Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | Economy trend window and chart type | ux-designer | VS |
| 2 | Camera nudge to keep the selected block visible | ux-designer | VS |
| 3 | Suggestion text table authoring | writer | VS |
