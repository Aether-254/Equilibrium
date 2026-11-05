using UnityEngine;

namespace Equilibrium.Presentation
{
    using Equilibrium.Simulation;

    /// <summary>
    /// Advances the <see cref="TickEngine"/> by whole ticks based on wall time, <see cref="TimeScale"/>
    /// and <see cref="Paused"/>. Ticks are integral; fractional progress is carried between frames
    /// and exposed as <see cref="Interpolation"/> for presentation smoothing. Never advances a partial tick.
    /// </summary>
    public sealed class TickPump : MonoBehaviour
    {
        [Min(0f)] [SerializeField] private float timeScale = 1f;
        [SerializeField] private bool paused;
        [Min(1)] [SerializeField] private int maxTicksPerFrame = 8;

        private TickEngine _engine;
        private double _accumulator;

        /// <summary>Simulated seconds per wall second. 1 tick = 1 simulated second (GDD rule 1).</summary>
        public float TimeScale { get => timeScale; set => timeScale = Mathf.Max(0f, value); }
        public bool Paused { get => paused; set => paused = value; }
        public int MaxTicksPerFrame { get => maxTicksPerFrame; set => maxTicksPerFrame = Mathf.Max(1, value); }

        /// <summary>Fraction [0,1) of the way to the next tick; presentation interpolates with this.</summary>
        public float Interpolation => (float)_accumulator;
        public TickEngine Engine => _engine;
        public ulong TickIndex => _engine?.TickIndex ?? 0;

        public void Attach(TickEngine engine)
        {
            _engine = engine;
            _accumulator = 0;
        }

        /// <summary>Feeds wall-clock delta and advances whole ticks. Public for tests; Update calls it.</summary>
        public int Step(float wallDeltaSeconds)
        {
            if (_engine == null || paused || timeScale <= 0f) return 0;
            _accumulator += wallDeltaSeconds * timeScale;
            int ticks = 0;
            while (_accumulator >= 1.0 && ticks < maxTicksPerFrame)
            {
                _engine.Advance();
                _accumulator -= 1.0;
                ticks++;
            }
            if (ticks == maxTicksPerFrame && _accumulator >= 1.0)
                _accumulator = 0; // drop backlog rather than spiral; time scale change snaps visuals (ADR-0007)
            return ticks;
        }

        /// <summary>Snaps interpolation to a boundary; call when TimeScale changes abruptly.</summary>
        public void Snap() => _accumulator = 0;

        private void Update()
        {
            Step(Time.unscaledDeltaTime);
        }
    }
}
