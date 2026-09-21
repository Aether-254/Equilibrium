# ADR-0006: UI Technology Stack

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1, URP 17.6 2D Renderer |
| **Domain** | UI |
| **Knowledge Risk** | HIGH: UI Toolkit runtime data binding (`DataBinding`, `[CreateProperty]`) and UXML runtime templates evolved through Unity 6.x; verify against installed `com.unity.modules.uielements` |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/ui.md`, `docs/engine-reference/unity/deprecated-apis.md` (UGUI marked legacy-recommended-against) |
| **Post-Cutoff APIs Used** | UI Toolkit runtime data binding API (Unity 6.0+), `UIDocument` panel settings for world-space is NOT used |
| **Verification Required** | Data binding refresh cost at 1 Hz tick for the inspection panel; UXML template instantiation cost; input focus handoff with Input System |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0007 (presentation reads snapshot) |
| **Enables** | machine-inspection-ui, build-hud, market screen, task panel, unlock graph epics |
| **Blocks** | All UI epics |
| **Ordering Note** | UX specs (`/ux-design`) should target UI Toolkit components |

## Context

### Problem Statement
Several GDDs specify screen-space UI (inspection panel with four tabs and a persistent status bar, HUD sidebar, market screen, task tracker, unlock graph) plus world-space markers (jam glyphs, open-end glyphs, ghost previews, port arrows). UGUI is installed and used by the prototype; Unity 6 recommends UI Toolkit for new runtime UI.

### Constraints
- 120 fps target; UI must not exceed ~1 ms per frame.
- Mouse-first, partial gamepad; Input System package.
- Many numeric readouts updated once per tick, not per frame.

### Requirements
- Tabbed panel with data-bound readouts; persistent status bar.
- HUD sidebar with hotkey labels; dialogs; economy strip.
- World-space markers aligned to grid cells at PPU 32.

## Decision

Decision (user, 2026-09-20): **UI Toolkit** for all screen-space UI. World-space markers (jam, open end, port arrows, ghosts, proposed blockers, affected-network highlights) are rendered with `SpriteRenderer`/tilemap overlays in the presentation layer (ADR-0007), not UI Toolkit.

1. One `UIDocument` root with a `HudRoot.uxml`; panels are UXML templates instantiated on demand; USS themes per art bible later.
2. View models are plain C# classes updated by an `IUiPresenter` on `TickCommitted` events (1 Hz) and on selection changes; UI Toolkit runtime data binding binds labels/progress bars to view-model properties; no per-frame polling.
3. Input: UI Toolkit consumes pointer events via the Input System UI module; hotkeys are Input System actions handled by the editing layer; when a text field has focus, hotkeys are suppressed (build-hud edge case).
4. UGUI is not used for new UI; the existing prototype UGUI (if any) is removed during migration. TextMeshPro is unnecessary under UI Toolkit.
5. Localization: all strings via a string table key from day one (UI Toolkit `Localization` bindings or a thin wrapper).

### Architecture Diagram
```
EventBus(TickCommitted, SelectionChanged) -> Presenters -> ViewModels --(UITK data binding)--> UXML panels
World markers: Presentation layer -> SpriteRenderer overlays (not UITK)
```

### Key Interfaces
```csharp
public interface IUiPresenter { void OnTick(in CommittedBatch batch); void OnSelection(BlockRef? b); }
public sealed class InspectionViewModel { /* [CreateProperty] State, ReasonCode, Recipe, Progress, Omega, TauReq, ... */ }
```

## Alternatives Considered

### Alternative 1: UGUI + TextMeshPro
- **Description**: Canvas-based UI.
- **Pros**: Prototype already uses it; mature; world-space canvases easy.
- **Cons**: Recommended-against for new projects in Unity 6 reference docs; layout rebuild cost; manual binding.
- **Rejection Reason**: User chose UI Toolkit.

### Alternative 2: UI Toolkit for everything including world markers
- **Description**: Draw markers in a world-space UITK panel.
- **Pros**: One stack.
- **Cons**: World-space UITK panels are awkward with 2D pixel grid and sorting layers.
- **Rejection Reason**: Sprites are the natural fit for grid-aligned pixel markers.

## Consequences

### Positive
- Declarative layouts for the four-tab panel and dialogs; USS theming when the art bible lands.
- Data binding at tick rate keeps UI cost low.

### Negative
- HIGH knowledge risk: runtime binding APIs must be verified; fallback is manual `Q<Label>().text` updates behind the same view models.
- Team must learn UXML/USS.

### Risks
- Binding performance at scale (many slots): virtualize slot lists with `ListView`.
- Input focus conflicts: explicit focus tracking in the editing layer.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| machine-inspection-ui.md | Rules 1-10; status bar across tabs; 1-tick refresh | UXML tabbed template; view model bound at tick rate |
| build-hud.md | Rules 1-11; sidebar, dialogs, economy strip; world markers | UITK for screen UI; sprites for world markers |
| market-interface.md | UI requirements | UITK market screen |
| tutorial-task.md | Task panel, tracker, Ponder viewer | UITK panels |
| tech-unlock.md | Unlock graph screen | UITK with custom `VisualElement` graph |
| placement-editing.md | Hotkeys, focus handling | Input System actions with focus suppression |

## Performance Implications
- **CPU**: Under 1 ms per frame for UI; binding updates only on tick/selection.
- **Memory**: UXML templates cached.
- **Load Time**: Template load on first open.
- **Network**: None.

## Migration Plan
1. Add `UIDocument` root to `Main.unity`; build inspection panel first from the UX spec.
2. Remove any UGUI canvases from the prototype.
3. Record UI Toolkit in Allowed Libraries (built-in module; note runtime binding version).

## Validation Criteria
- machine-inspection-ui and build-hud acceptance criteria pass in PlayMode tests.
- Profiler: UI under 1 ms with panel open and 1,000 belts marked.

## Related Decisions
- ADR-0007 presentation layer
- design/ux/machine-inspection.md, design/ux/hud.md (to be authored)
