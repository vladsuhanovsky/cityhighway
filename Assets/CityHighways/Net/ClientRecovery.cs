namespace CityHighways.Net
{
    /// <summary>Bounded client wait policy. Never resubmit a move; only request authoritative history.</summary>
    public sealed class ClientRecovery
    {
        public enum NextAction { None, RequestSync, Close }
        enum State { Idle, Submit, Sync }
        State _state;
        double _deadline;
        int _syncAttempts;

        public const double TimeoutSeconds = 6.0;
        public const int MaxSyncAttempts = 2;
        public bool Pending => _state != State.Idle;
        public bool Synchronizing => _state == State.Sync;

        public void BeginSubmit(double now)
        {
            _state = State.Submit;
            _deadline = now + TimeoutSeconds;
            _syncAttempts = 0;
        }

        public bool BeginSync(double now)
        {
            if (Synchronizing) return false;
            _state = State.Sync;
            _syncAttempts = 1;
            _deadline = now + TimeoutSeconds;
            return true;
        }

        public NextAction Tick(double now)
        {
            if (!Pending || now < _deadline) return NextAction.None;
            if (_state == State.Submit)
            {
                BeginSync(now);
                return NextAction.RequestSync;
            }
            if (_syncAttempts >= MaxSyncAttempts)
            {
                Complete();
                return NextAction.Close;
            }
            _syncAttempts++;
            _deadline = now + TimeoutSeconds;
            return NextAction.RequestSync;
        }

        public void Complete()
        {
            _state = State.Idle;
            _deadline = 0;
            _syncAttempts = 0;
        }
    }
}
