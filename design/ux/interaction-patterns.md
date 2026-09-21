# Interaction Pattern Library

> **Status**: Initialized 2026-09-20; grows as UX specs are authored
> **Stack**: UI Toolkit (screen UI), sprite overlays (world markers), Input System actions

| ID | Pattern | Definition | Used by |
|---|---|---|---|
| IP-01 | Status bar | Persistent strip at top of a panel showing state color + glyph + label + top ReasonCode; never hidden by tab changes | machine-inspection |
| IP-02 | Tabbed panel | Horizontal tabs (Overview default); last tab persists per session; tabs collapse to icons when narrow | machine-inspection, market, task panel |
| IP-03 | Mode sidebar | Top-screen row of mode buttons with hotkey labels; active mode highlighted; entering a mode closes inspection | hud |
| IP-04 | Preview with reason | World-space ghost tinted valid/invalid; invalid shows a short reason label near the cursor | placement, blueprint, blocker |
| IP-05 | Confirmation dialog | Modal listing consequences; Confirm / Cancel; Esc cancels, Enter confirms; optional third action (e.g. "Select whole structure") | network join, blocker removal, Type I removal, irreversible recipe |
| IP-06 | Direction-worded selector | Options phrased as flows ("Belt -> Container") never as bare verbs | accessor settings |
| IP-07 | Quantity field with mode | Numeric field paired with a mode selector and one-line semantics | accessor amount, batcher size |
| IP-08 | World marker glyph | Cell-anchored sprite: open end (amber ring), jam (orange chevrons), proposed blocker (dashed bar), affected network (amber outline) | hud |
| IP-09 | Economy strip | Always-visible currency + windowed profit + queued volume; click opens market | hud |
| IP-10 | Slot list | Virtualized list of slots: icon, count / effective capacity, filter badge, reserved marker | inspection Logistics, warehouse |
| IP-11 | Progress ring | Circular progress for batch completion; also on hover in world | inspection Overview |
| IP-12 | Hotkey chip | Small chip showing active modifiers during drag (Shift, suppress-blockers) | hud |
| IP-13 | Ponder link | Inline "Why?" link from a ReasonCode or tooltip that opens a demo without changing the save | inspection, tooltips |
| IP-14 | Deep analysis request | Button appearing on `NetworkAnalysisBudgetExceeded`; async; commit disabled until done | hud path preview |

Rules: all patterns comply with `design/accessibility-requirements.md` (color + glyph, keyboard dismiss, text scaling).
