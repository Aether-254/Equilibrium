using System;
using Equilibrium.Editing;
using Equilibrium.Presentation;
using Equilibrium.Simulation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Equilibrium.UI
{
    public enum FactoryTool { Inspect, Chest, Belt, Accessor, Blocker, Remove, Saw, Engine, Shaft }

    /// <summary>Input produces queued intents. Only the Tick engine writes world state.</summary>
    public sealed class FactoryInputController : MonoBehaviour
    {
        public FactoryRuntime Runtime;
        public FactoryHud Hud;
        public FactoryTool Tool { get; private set; }
        public CellRef Selected { get; private set; } = CellRef.None;
        public Side SelectedSide { get; private set; } = Side.West;
        public StraightBeltPreview Drag { get; } = new StraightBeltPreview();
        public int Orientation { get; private set; }
        public event Action SelectionChanged;
        private readonly CellRef[] _single = new CellRef[1];
        private Vector2 _lastMouse;

        public void SetTool(FactoryTool tool)
        {
            CancelDrag(); Tool = tool; Hud?.Refresh();
        }
        public void Select(CellRef cell, Side worldSide)
        { Selected = cell; SelectedSide = worldSide; SelectionChanged?.Invoke(); }
        public void CancelDrag()
        { Drag.Cancel(); Runtime?.WorldRenderer?.ClearPreview(); }

        // Also used by integration tests; a UI hit cannot start, extend or commit a world edit.
        public void PointerDown(CellRef cell, Side worldSide, bool overUi)
        {
            if (overUi || Runtime.IsLoading || Hud?.HasModal == true || !Runtime.Session.World.InBounds(cell)) return;
            var session = Runtime.Session;
            Select(cell, worldSide);
            switch (Tool)
            {
                case FactoryTool.Chest:
                case FactoryTool.Saw:
                case FactoryTool.Engine:
                case FactoryTool.Shaft:
                    _single[0] = cell; session.QueueBuild(PlacementKind, _single, Orientation); break;
                case FactoryTool.Belt:
                    if (session.CanPreview(cell)) { Drag.Begin(cell); PointerMove(cell, false); } break;
                case FactoryTool.Accessor:
                    var block = session.World.BlockAt(cell);
                    if (!block.IsNone && session.Kind(block) == BlockKind.Conveyor)
                    {
                        Side local = worldSide.Rotate(-session.World.OrientationOf(block));
                        if (session.World.Accessor(block, local) == null)
                            session.QueueAccessor(cell, local, new AccessorConfiguration());
                    }
                    break;
                case FactoryTool.Blocker: session.QueueBlocker(cell, worldSide); break;
                case FactoryTool.Remove: session.QueueRemove(cell); break;
            }
            Hud?.Refresh();
        }

        public void PointerMove(CellRef cell, bool overUi)
        {
            if (Runtime.IsLoading || Hud?.HasModal == true) { CancelDrag(); return; }
            if (overUi) { Runtime.WorldRenderer.ClearPreview(); return; }
            if (Drag.Active)
            {
                Drag.Update(cell, Runtime.Session.CanPreview);
                Runtime.WorldRenderer.Preview(Drag.Cells, true, Drag.Orientation, Drag.Cells.Count >= 2);
            }
            else if (Tool == FactoryTool.Chest || Tool == FactoryTool.Saw || Tool == FactoryTool.Engine || Tool == FactoryTool.Shaft)
            {
                _single[0] = cell;
                Runtime.WorldRenderer.Preview(_single, false, Orientation, Runtime.Session.CanPreview(cell), PlacementKind);
            }
        }

        public void PointerUp(bool overUi)
        {
            if (Runtime.IsLoading || Hud?.HasModal == true) { CancelDrag(); return; }
            if (!Drag.Active) return;
            if (!overUi && Drag.Cells.Count >= 2)
                Runtime.Session.QueueBuild(Buildable.Belt, Drag.Cells, Drag.Orientation);
            CancelDrag(); Hud?.Refresh();
        }

        private void Update()
        {
            if (Runtime?.Session == null || Mouse.current == null) return;
            if (Runtime.IsLoading || Hud?.HasModal == true) { CancelDrag(); return; }
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            Vector2 screen = mouse.position.ReadValue(); bool overUi = Hud != null && Hud.HitTest(screen);
            bool typing = Hud != null && Hud.EditingText;
            if (keyboard != null && !typing)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame) SetTool(FactoryTool.Inspect);
                if (keyboard.mKey.wasPressedThisFrame) SetTool(FactoryTool.Chest);
                if (keyboard.cKey.wasPressedThisFrame) SetTool(FactoryTool.Belt);
                if (keyboard.iKey.wasPressedThisFrame) SetTool(FactoryTool.Accessor);
                if (keyboard.kKey.wasPressedThisFrame) SetTool(FactoryTool.Blocker);
                if (keyboard.xKey.wasPressedThisFrame) SetTool(FactoryTool.Remove);
                if (keyboard.digit1Key.wasPressedThisFrame) SetTool(FactoryTool.Saw);
                if (keyboard.digit2Key.wasPressedThisFrame) SetTool(FactoryTool.Engine);
                if (keyboard.digit3Key.wasPressedThisFrame) SetTool(FactoryTool.Shaft);
                if (keyboard.rKey.wasPressedThisFrame) Orientation = (Orientation + 1) & 3;
                if (keyboard.fKey.wasPressedThisFrame) Drag.Reverse();
                if (keyboard.spaceKey.wasPressedThisFrame) { Runtime.Pump.Paused = !Runtime.Pump.Paused; Hud?.Refresh(); }
                if (keyboard.f5Key.wasPressedThisFrame) Hud?.SaveClicked();
                if (keyboard.f9Key.wasPressedThisFrame) Hud?.LoadClicked();
            }
            if (Hud?.HasModal == true) return;
            bool inside = Runtime.ScreenCell(screen, out var cell, out var position);
            if (!overUi && !typing && Runtime.WorldCamera != null)
            {
                var camera = Runtime.WorldCamera;
                float zoom = mouse.scroll.ReadValue().y;
                if (Math.Abs(zoom) > 0.1f) camera.orthographicSize = Mathf.Clamp(camera.orthographicSize * (zoom > 0 ? 0.9f : 1.1f), 2, 20);
                if (mouse.middleButton.isPressed && !mouse.middleButton.wasPressedThisFrame)
                {
                    var delta = camera.ScreenToWorldPoint(_lastMouse) - camera.ScreenToWorldPoint(screen);
                    camera.transform.position += new Vector3(delta.x, delta.y, 0);
                }
            }
            _lastMouse = screen;
            if (typing) { if (mouse.leftButton.wasReleasedThisFrame) CancelDrag(); return; }
            if (inside && !overUi)
            {
                Vector3 relative = position - Runtime.CellCenter(cell);
                Side side = Mathf.Abs(relative.x) >= Mathf.Abs(relative.y)
                    ? (relative.x >= 0 ? Side.East : Side.West) : (relative.y >= 0 ? Side.North : Side.South);
                if (mouse.leftButton.wasPressedThisFrame) PointerDown(cell, side, false);
                PointerMove(cell, false);
                if (mouse.rightButton.wasPressedThisFrame) { CancelDrag(); Runtime.Session.QueueRemove(cell); Hud?.Refresh(); }
            }
            else Runtime.WorldRenderer.ClearPreview();
            if (mouse.leftButton.wasReleasedThisFrame) PointerUp(overUi || !inside);
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private Buildable PlacementKind => Tool == FactoryTool.Saw ? Buildable.Saw : Tool == FactoryTool.Engine ? Buildable.Engine : Tool == FactoryTool.Shaft ? Buildable.Shaft : Buildable.Chest;
        private void OnDisable() => CancelDrag();
    }
}
