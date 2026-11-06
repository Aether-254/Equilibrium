using System;
using System.Collections.Generic;
using Equilibrium.Editing;
using Equilibrium.Simulation.World;
using UnityEngine;

namespace Equilibrium.Presentation
{
    /// <summary>World sprites and labels driven by committed state; no colliders or rigidbodies.</summary>
    public sealed class FactoryWorldRenderer : MonoBehaviour
    {
        private sealed class BlockVisual
        {
            public GameObject Root;
            public SpriteRenderer Body;
            public TextMesh Label, Direction, State;
            public readonly TextMesh[] Edges = new TextMesh[4];
        }
        private sealed class GroupVisual
        {
            public GameObject Root;
            public TextMesh Count;
            public SpriteRenderer Sprite;
            public Vector3 From, To;
            public bool StaysOnBelt;
        }
        private FactoryRuntime _runtime;
        private Equilibrium.Simulation.TickEngine _subscribedEngine;
        private readonly Dictionary<int, BlockVisual> _blocks = new Dictionary<int, BlockVisual>();
        private readonly Stack<BlockVisual> _pool = new Stack<BlockVisual>();
        private readonly List<int> _removed = new List<int>();
        private readonly List<GroupVisual> _groups = new List<GroupVisual>();
        private readonly List<BlockVisual> _ghosts = new List<BlockVisual>(), _preview = new List<BlockVisual>();
        private int _groupCount;
        private float _elapsed = 1;
        public float Interpolation => Mathf.Clamp01(_elapsed);
        public int VisibleGroupCount => _groupCount;
        public Vector3 GroupPosition(int index) => _groups[index].Root.transform.position;
        public Vector3 GroupDestination(int index) => _groups[index].To;

        public void Initialize(FactoryRuntime runtime)
        {
            if (_subscribedEngine != null) _subscribedEngine.TickBoundary -= Boundary;
            foreach (var visual in _blocks.Values) Release(visual);
            _blocks.Clear(); ClearPreview();
            foreach (var visual in _ghosts) Release(visual);
            _ghosts.Clear();
            foreach (var group in _groups) group.Root.SetActive(false);
            _groupCount = 0; _elapsed = 1;
            _runtime = runtime; _subscribedEngine = runtime.Session.Engine;
            _subscribedEngine.TickBoundary += Boundary; Refresh();
        }
        private void Boundary(ulong tick) { _elapsed = 0; Refresh(); }
        private void OnDestroy()
        { if (_subscribedEngine != null) _subscribedEngine.TickBoundary -= Boundary; }

        private void Update()
        {
            if (_runtime?.Session == null) return;
            if (!_runtime.Pump.Paused) _elapsed = Mathf.Min(1, _elapsed + Time.unscaledDeltaTime * _runtime.Pump.TimeScale);
            RenderInterpolation(_elapsed);
        }

        public void RenderInterpolation(float fraction)
        {
            for (int i = 0; i < _groupCount; i++)
            {
                _groups[i].Root.transform.position = Vector3.Lerp(_groups[i].From, _groups[i].To, Mathf.Clamp01(fraction));
                _groups[i].Root.SetActive(_groups[i].StaysOnBelt || fraction < 1);
            }
        }

        public void Snap() { _elapsed = 1; RenderInterpolation(1); }

        private TextMesh Text(Transform parent, string name, Vector3 local, float size, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = local;
            var text = go.AddComponent<TextMesh>(); text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.characterSize = size; text.fontSize = 32; text.color = color;
            text.font = _runtime.Assets.WorldFont != null ? _runtime.Assets.WorldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = text.font.material; renderer.sortingOrder = 30;
            return text;
        }

        private BlockVisual Acquire()
        {
            if (_pool.Count > 0) { var pooled = _pool.Pop(); pooled.Root.SetActive(true); return pooled; }
            var v = new BlockVisual { Root = new GameObject("Factory block") };
            v.Root.transform.SetParent(transform, false);
            var body = new GameObject("Body"); body.transform.SetParent(v.Root.transform, false);
            v.Body = body.AddComponent<SpriteRenderer>(); v.Body.sortingOrder = 10;
            v.Label = Text(v.Root.transform, "Inventory", new Vector3(0, -0.25f, -0.1f), 0.065f, Color.white);
            v.Direction = Text(v.Root.transform, "Direction", new Vector3(0, 0.23f, -0.1f), 0.08f, Color.white);
            v.State = Text(v.Root.transform, "State", new Vector3(0.32f, 0.31f, -0.1f), 0.09f, new Color(1, 0.6f, 0.15f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = i == 0 ? Vector3.up : i == 1 ? Vector3.right : i == 2 ? Vector3.down : Vector3.left;
                v.Edges[i] = Text(v.Root.transform, "Attachment " + i, at * 0.43f + Vector3.back * 0.1f, 0.065f, new Color(0.95f, 0.8f, 0.3f));
            }
            return v;
        }
        private void Release(BlockVisual visual) { visual.Root.SetActive(false); _pool.Push(visual); }

        private void Style(BlockVisual v, CellRef cell, bool belt, int orientation, float alpha)
        {
            v.Root.transform.position = _runtime.CellCenter(cell);
            v.Body.sprite = belt ? _runtime.Assets.Belt : _runtime.Assets.Chest;
            v.Body.transform.localRotation = Quaternion.Euler(0, 0, -90 * orientation);
            v.Body.color = new Color(belt ? 1 : 0.52f, belt ? 1 : 0.43f, belt ? 1 : 0.32f, alpha);
            v.Label.text = ""; v.Direction.text = belt ? Arrow(Side.East.Rotate(orientation)) : "BOX";
            v.State.text = "";
            foreach (var edge in v.Edges) edge.text = "";
        }
        private static string Arrow(Side side) => side == Side.North ? "↑" : side == Side.East ? "→" : side == Side.South ? "↓" : "←";

        public void Refresh()
        {
            var session = _runtime.Session; var world = session.World;
            _removed.Clear();
            foreach (var pair in _blocks) if (!world.IsAlive(new BlockRef(pair.Key))) _removed.Add(pair.Key);
            foreach (int key in _removed) { Release(_blocks[key]); _blocks.Remove(key); }
            for (int b = 0; b < world.BlockIndexCount; b++)
            {
                var block = new BlockRef(b); if (!world.IsAlive(block)) continue;
                if (!_blocks.TryGetValue(b, out var v)) { v = Acquire(); _blocks.Add(b, v); }
                bool belt = session.Kind(block) == BlockKind.Conveyor;
                Style(v, world.AnchorOf(block), belt, world.OrientationOf(block), 1);
                if (!belt) MachineStyle(v, session.BlockId(block), world.OrientationOf(block));
                int count = 0; for (int s = 0; s < world.SlotCount(block); s++) count += world.SlotCountOf(world.SlotGlobal(block, s));
                if (!belt) v.Label.text = count.ToString();
                var machine = session.Processing.State(block);
                if (machine != null)
                {
                    v.State.text = machine.Phase == MachinePhase.Working ? (machine.ReceivedWatts > 0 ? ">" : "!") : machine.Phase == MachinePhase.Idle ? "" : "!";
                    v.Label.text = machine.Phase == MachinePhase.Working ? machine.WorkDone.ToString("F0") + " J" : count.ToString();
                }
                if (belt && session.Logistics.BeltResult(block).Stalled) v.State.text = "!";
                for (int side = 0; side < 4; side++)
                {
                    Side global = ((Side)side).Rotate(world.OrientationOf(block));
                    bool closed = !world.IngressEnabled(world.AnchorOf(block), global);
                    var accessor = world.Accessor(block, (Side)side);
                    if (belt) v.Edges[(int)global].text = closed ? "×" : accessor == null ? "" : "A";
                }
            }
            foreach (var ghost in _ghosts) Release(ghost); _ghosts.Clear();
            foreach (var ghost in session.Ghosts)
            {
                var v = Acquire(); Style(v, ghost.Cell, ghost.Kind == Buildable.Belt, ghost.Orientation, 0.35f);
                if (ghost.Kind != Buildable.Belt) MachineStyle(v, ghost.Kind.ToString().ToLowerInvariant(), ghost.Orientation);
                v.State.text = ghost.Reason == Equilibrium.Simulation.ReasonCode.SourceEmpty ? "?" : "+"; _ghosts.Add(v);
            }
            RebuildGroups();
        }

        private void RebuildGroups()
        {
            _groupCount = 0; var session = _runtime.Session; var world = session.World;
            var incoming = new Dictionary<int, int>();
            // Snapshot endpoints survive a removal in the same commit, even after source is dead.
            foreach (var transfer in session.Logistics.Transfers)
            {
                bool targetAlive = world.IsAlive(transfer.Target);
                bool targetBelt = targetAlive && session.Kind(transfer.Target) == BlockKind.Conveyor;
                if (targetBelt)
                {
                    incoming.TryGetValue(transfer.Target.Index, out int n); incoming[transfer.Target.Index] = n + transfer.Count;
                }
                if (!targetAlive) continue;
                AddGroup(_runtime.CellCenter(world.AnchorOf(transfer.Source)), _runtime.CellCenter(world.AnchorOf(transfer.Target)), transfer.Count, transfer.Item.Canonical, targetBelt);
            }
            foreach (var transfer in session.Processing.Transfers)
            {
                if (!world.IsAlive(transfer.Target)) continue;
                incoming.TryGetValue(transfer.Target.Index, out int n); incoming[transfer.Target.Index] = n + transfer.Count;
                AddGroup(_runtime.CellCenter(world.AnchorOf(transfer.Source)), _runtime.CellCenter(world.AnchorOf(transfer.Target)), transfer.Count, transfer.Item.Canonical, true);
            }
            for (int b = 0; b < world.BlockIndexCount; b++)
            {
                var block = new BlockRef(b);
                if (!world.IsAlive(block) || session.Kind(block) != BlockKind.Conveyor) continue;
                int s = world.SlotGlobal(block, 0), count = world.SlotCountOf(s);
                incoming.TryGetValue(b, out int arrived); int remainder = count - arrived;
                if (remainder <= 0) continue;
                Vector3 at = _runtime.CellCenter(world.AnchorOf(block));
                AddGroup(at, at, remainder, world.Registry.Items.FromHandle(world.SlotItemHandle(s)).Canonical, true);
            }
            for (int i = _groupCount; i < _groups.Count; i++) _groups[i].Root.SetActive(false);
            RenderInterpolation(0);
        }

        private void AddGroup(Vector3 from, Vector3 to, int count, string item, bool staysOnBelt)
        {
            if (_groupCount == _groups.Count)
            {
                var root = new GameObject("Item group"); root.transform.SetParent(transform, false);
                var group = new GroupVisual { Root = root };
                group.Count = Text(root.transform, "Count", new Vector3(0, 0, -0.2f), 0.065f, new Color(1, 0.86f, 0.57f));
                var icon = new GameObject("Icon"); icon.transform.SetParent(root.transform, false);
                group.Sprite = icon.AddComponent<SpriteRenderer>(); group.Sprite.sortingOrder = 20;
                icon.transform.localScale = Vector3.one * 0.4f; _groups.Add(group);
            }
            var v = _groups[_groupCount++]; v.Root.SetActive(true); v.From = from; v.To = to;
            v.Sprite.sprite = item == "equilibrium:wood" ? _runtime.Assets.Wood : null;
            string name = item == "equilibrium:wood" ? "wood" : item.Substring(item.IndexOf(':') + 1);
            v.Count.text = name + "\n×" + count;
            v.StaysOnBelt = staysOnBelt;
        }

        private void MachineStyle(BlockVisual visual, string id, int orientation)
        {
            visual.Direction.text = id == "saw" ? "SAW" : id == "engine" ? "ENGINE" : id == "shaft" ? "SHAFT" : "BOX";
            if (id == "saw" || id == "engine")
            {
                visual.Edges[(int)Side.West.Rotate(orientation)].text = "IN";
                visual.Edges[(int)(id == "saw" ? Side.East : Side.South).Rotate(orientation)].text = id == "saw" ? "OUT" : "P";
                if (id == "saw") visual.Edges[(int)Side.North.Rotate(orientation)].text = "P";
            }
            if (id == "shaft")
            {
                visual.Edges[(int)Side.North.Rotate(orientation)].text = "P";
                visual.Edges[(int)Side.South.Rotate(orientation)].text = "P";
            }
        }

        public void Preview(IReadOnlyList<CellRef> cells, bool belt, int orientation, bool valid, Buildable kind = Buildable.Chest)
        {
            while (_preview.Count < cells.Count) _preview.Add(Acquire());
            for (int i = 0; i < _preview.Count; i++)
            {
                var v = _preview[i]; v.Root.SetActive(i < cells.Count); if (i >= cells.Count) continue;
                Style(v, cells[i], belt, orientation, 0.4f);
                if (!belt) MachineStyle(v, kind.ToString().ToLowerInvariant(), orientation);
                v.Body.color = valid ? new Color(0.5f, 0.85f, 1, 0.4f) : new Color(1, 0.2f, 0.2f, 0.5f);
                v.State.text = valid ? "+" : "!";
            }
        }
        public void ClearPreview() { foreach (var v in _preview) v.Root.SetActive(false); }
    }
}
