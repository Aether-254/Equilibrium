# ADR-0007: Simulation / Presentation Split and Assembly Layout

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1, URP 17.6 2D Renderer |
| **Domain** | Core / Rendering |
| **Knowledge Risk** | MEDIUM: SRP Batcher and 2D Renderer sprite batching behavior in URP 17.x; Sprite Atlas v2 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/rendering.md`, `docs/engine-reference/unity/breaking-changes.md` (URP renderer feature API) |
| **Post-Cutoff APIs Used** | None required; if a custom ScriptableRenderPass is added for pipe masks, use the Render Graph API (URP 17) not the legacy `Execute` signature |
| **Verification Required** | Draw-call count under 500 with 4k visible belt cells via atlasing; frame-to-tick interpolation smoothness |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001, ADR-0004 |
| **Enables** | ADR-0006, conveyor/shaft/pipe visuals |
| **Blocks** | All visual epics |
| **Ordering Note** | Assembly definitions must exist before porting any prototype code |

## Context

### Problem Statement
Spec s1.1 and the tick-simulation GDD require that animation represent already-committed transactions and never influence simulation. The prototype couples both (physics conveyors, MonoBehaviour containers). The project also has no assembly definitions, so runtime, editor (shaft baker), and future tests share default assemblies. We need a hard boundary and a rendering approach that meets 500 draw calls and 8.3 ms.

### Constraints
- Simulation must compile without UnityEngine (except math if unavoidable) for EditMode speed and headless offline recompute.
- Editor bake tooling must not ship in the Player (spec s20).
- 32 px tiles, PPU 32, Point filtering (spec s20).

### Requirements
- Assemblies: `Equilibrium.Simulation`, `Equilibrium.Content`, `Equilibrium.Content.Unity`, `Equilibrium.Editing` (placement/pathfinding logic, plain C#), `Equilibrium.Presentation` (Unity), `Equilibrium.UI` (UI Toolkit), `Equilibrium.Editor` (editor-only), `Equilibrium.Tests.EditMode`, `Equilibrium.Tests.PlayMode`.
- Presentation consumes `CommittedEvent`s and `IWorldView` snapshots; interpolates visuals between ticks; never writes.

## Decision

1. **Assembly layout** as listed, with `Equilibrium.Simulation`, `Content`, and `Editing` having `noEngineReferences: true` where possible (use `System.Numerics` for vectors). `Equilibrium.Editor` lives under `Assets/Editor/` and includes the shaft baker.
2. **Presentation**: a `WorldRenderer` maintains pooled `SpriteRenderer`s (or a `Tilemap` for static belt/pipe bodies) per visible chunk; item groups on belts are sprites whose position interpolates from `lastTickPos` to `thisTickPos` over the wall duration of a tick; machine state sprites switch on `StateChanged` events; shafts advance the baked atlas frame by `omega * dt`.
3. **No physics** for logistics: remove trigger-collider conveyors; Box2D remains only for future non-simulation effects if any.
4. **Sprite atlasing**: all 32x32 logistics tiles in one Sprite Atlas (v2) with Point filter; overlays (blockers, accessors, markers) in the same atlas; one material per atlas so SRP Batcher batches.
5. **Debug overlay**: an editor/development-only overlay draws `IWorldView` data (ports, ingress bits, network ids) to compensate for losing Inspector visibility.
6. **Time control** (speed, pause) lives in Presentation/`TickPump`; ticks are integral; visuals interpolate.

### Architecture Diagram
```
Equilibrium.Simulation (pure C#)  <-- Equilibrium.Editing (pure C#)
        | CommittedEvent, IWorldView
Equilibrium.Presentation (Unity) --> SpriteRenderer/Tilemap, atlas, interpolation
Equilibrium.UI (UI Toolkit)      --> panels (ADR-0006)
Equilibrium.Editor (Editor only) --> shaft baker, debug tools
```

### Key Interfaces
```csharp
public interface IPresentationSink { void OnBatchCommitted(in CommittedBatch batch, IWorldView view, ulong tick); }
public sealed class TickPump : MonoBehaviour { public float TimeScale; public bool Paused; /* advances whole ticks */ }
```

## Alternatives Considered

### Alternative 1: Keep MonoBehaviour-per-block with physics conveyors
- **Rejection Reason**: Violates spec s7.1 (no physics for belts) and ADR-0001.

### Alternative 2: ECS hybrid renderer
- **Rejection Reason**: Deferred with DOTS (ADR-0001 alt 2).

### Alternative 3: Single default assembly
- **Rejection Reason**: Editor code leaks into Player; slow test iteration; no enforced boundary.

## Consequences

### Positive
- Enforced boundary; fast EditMode tests; headless offline recompute.
- Batching friendly rendering.

### Negative
- Rewrite of prototype visuals; interpolation code needed for smooth belts.

### Risks
- Interpolation artifacts when time scale changes mid-tick: snap on scale change.
- Draw calls with many distinct machine sprites: keep machines in the atlas too; measure.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| tick-simulation.md | Rule 7 visuals consume events; Emit phase | `IPresentationSink` |
| conveyor.md | Rules 12 (unpowered, 1 hop/tick), visual sliding | Interpolated group sprites |
| mechanical-power.md | Visual omega-driven shaft animation | Baked atlas frame stepping |
| glass-pipe.md | Rules 10-15 mask/shader-driven visuals; no per-fill frames | Custom material parameters; Render Graph pass if needed |
| confirmed-mechanics-spec s20 | 32 px, PPU 32, Point filter, atlas, editor-only baker | Atlas settings; `Equilibrium.Editor` |
| technical-preferences | 120 fps, 500 draw calls | Atlasing and pooling |

## Performance Implications
- **CPU**: Presentation under 2 ms with 4k visible cells; pooled renderers.
- **Memory**: Atlas; pooled sprites per chunk.
- **Load Time**: Atlas load.
- **Network**: None.

## Migration Plan
1. Create asmdefs; move `Assets/Editor/ShaftSpriteBakerEditor.cs` and `Assets/Art/Shaft/ShaftBaker.cs` under `Equilibrium.Editor`.
2. Move existing scripts into the right assemblies or archive them under `prototypes/`.
3. Implement `WorldRenderer` for belts and machines; convert `ShaftSpriteVisual` to consume omega events.
4. Build Sprite Atlas; verify Point filter and PPU 32.

## Validation Criteria
- Simulation assembly compiles with no UnityEngine reference.
- Draw calls under 500 with 4k visible belt cells and 200 machines.
- Visual position of a moving group matches committed cell at each tick boundary.

## Related Decisions
- ADR-0001, ADR-0004, ADR-0006
