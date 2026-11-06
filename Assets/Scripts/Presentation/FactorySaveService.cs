using System;
using System.Threading.Tasks;
using Equilibrium.Editing;
using Equilibrium.Simulation;
using Equilibrium.Simulation.Save;

namespace Equilibrium.Presentation
{
    /// <summary>Main-thread boundary capture; only detached serialization and disk I/O run on a worker.</summary>
    public sealed class FactorySaveService : IDisposable
    {
        private readonly FactorySession _session;
        private readonly Func<SaveDocument, Task> _write;
        private readonly Func<DateTimeOffset> _clock;
        private Task _writing;
        private SaveDocument _captured;
        private bool _disposed;
        public bool Pending { get; private set; }
        public bool Busy => _writing != null;
        public bool AutosaveEnabled { get; set; }
        public int AutosaveInterval { get; }
        public Exception LastError { get; private set; }
        public ulong? LastSavedTick { get; private set; }
        public event Action Changed;
        public event Action<SaveDocument> Saved;

        public FactorySaveService(FactorySession session, Func<SaveDocument, Task> write,
            bool autosaveEnabled, int autosaveInterval = 300, Func<DateTimeOffset> clock = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _write = write ?? throw new ArgumentNullException(nameof(write)); _clock = clock ?? (() => DateTimeOffset.UtcNow);
            if (autosaveInterval < 1) throw new ArgumentOutOfRangeException(nameof(autosaveInterval));
            AutosaveInterval = autosaveInterval; AutosaveEnabled = autosaveEnabled;
            session.Engine.TickBoundary += Boundary;
        }

        public void RequestSave()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FactorySaveService));
            Pending = true; LastError = null; Changed?.Invoke();
        }

        private void Boundary(ulong tick)
        {
            if (AutosaveEnabled && tick % (ulong)AutosaveInterval == 0) Pending = true;
            TryCapture();
        }

        public void Poll()
        {
            if (_disposed) return;
            if (_writing?.IsCompleted == true)
            {
                var completed = _writing; _writing = null;
                try
                {
                    completed.GetAwaiter().GetResult(); LastSavedTick = _captured.Header.TickIndex; LastError = null;
                    Saved?.Invoke(_captured);
                }
                catch (Exception error) { LastError = error; }
                _captured = null; Changed?.Invoke();
            }
            if (_session.Engine.Phase == TickPhase.Idle) TryCapture();
        }

        private void TryCapture()
        {
            if (_disposed || !Pending || Busy || !_session.AtSaveBoundary || _session.PendingCommands != 0) return;
            Pending = false;
            try
            {
                _captured = _session.CaptureSave(_clock());
                _writing = _write(_captured) ?? throw new InvalidOperationException("Save writer returned no task.");
            }
            catch (Exception error) { LastError = error; _captured = null; _writing = null; }
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _session.Engine.TickBoundary -= Boundary;
            // A captured write may finish after the scene is gone. Observe failure without touching Unity objects.
            if (_writing != null) _ = _writing.ContinueWith(task => { var error = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
