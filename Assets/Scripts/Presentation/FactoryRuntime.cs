using System;
using System.Collections.Generic;
using Equilibrium.Editing;
using Equilibrium.Simulation.World;
using UnityEngine;

namespace Equilibrium.Presentation
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class FactoryRuntime : MonoBehaviour
    {
        public FactoryAssets Assets;
        public Camera WorldCamera;
        public Vector2Int GridOrigin = new Vector2Int(-32, -32);
        [Min(8)] public int Width = 64, Height = 64;
        public FactorySession Session { get; private set; }
        public TickPump Pump { get; private set; }
        public FactoryWorldRenderer WorldRenderer { get; private set; }
        public event Action Changed;

        private void Start() => Initialize();

        public void Initialize()
        {
            if (Session != null) return;
            if (Assets == null) throw new InvalidOperationException("Factory presentation assets are not assigned.");
            var stock = new Dictionary<string, int> { ["belt"] = Assets.StartingBelts, ["chest"] = Assets.StartingChests,
                ["accessor"] = Assets.StartingAccessors, ["blocker"] = Assets.StartingBlockers, ["wood"] = Assets.StartingWood,
                ["log"] = Assets.StartingLogs, ["saw"] = Assets.StartingSaws, ["engine"] = Assets.StartingEngines, ["shaft"] = Assets.StartingShafts };
            Session = new FactorySession(Width, Height, stock);
            if (WorldCamera == null) WorldCamera = Camera.main;
            Pump = GetComponent<TickPump>() ?? gameObject.AddComponent<TickPump>();
            Pump.Attach(Session.Engine);
            WorldRenderer = GetComponent<FactoryWorldRenderer>() ?? gameObject.AddComponent<FactoryWorldRenderer>();
            WorldRenderer.Initialize(this);
            Session.Engine.TickBoundary += OnBoundary;
            InitializePersistence();
        }

        private void OnBoundary(ulong tick) => Changed?.Invoke();
        public void Step() { Initialize(); Session.Engine.Advance(); Pump.Snap(); WorldRenderer.Snap(); }
        public Vector3 CellCenter(CellRef cell) => new Vector3(GridOrigin.x + cell.X + 0.5f, GridOrigin.y + cell.Y + 0.5f, 0);
        public CellRef CellAt(Vector3 position) => new CellRef(Mathf.FloorToInt(position.x) - GridOrigin.x, Mathf.FloorToInt(position.y) - GridOrigin.y);
        public bool ScreenCell(Vector2 screen, out CellRef cell, out Vector3 position)
        {
            cell = CellRef.None; position = default;
            if (WorldCamera == null) return false;
            var ray = WorldCamera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return false;
            position = ray.GetPoint(distance); cell = CellAt(position);
            return Session.World.InBounds(cell);
        }
        private void OnDestroy()
        {
            _destroyed = true; _saves?.Dispose();
            if (Session != null) Session.Engine.TickBoundary -= OnBoundary;
        }
    }
}
