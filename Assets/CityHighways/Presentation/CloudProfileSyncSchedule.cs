using System;

namespace CityHighways.Presentation
{
    /// <summary>Single-flight sync schedule. Ticket callbacks are fenced by token;
    /// HTTP completion uses Unity's existing request timeout.</summary>
    public sealed class CloudProfileSyncSchedule
    {
        public const float TicketTimeout = 15f;
        public const float SaveDelay = 8f;
        public const float Period = 300f;
        const float InitialRetry = 15f;
        enum Phase { Idle, Ticket, Http }
        Phase _phase;
        float _due = 1f, _retryAt, _ticketDeadline;
        int _token, _failures;

        public int Token => _token;
        public bool Busy => _phase != Phase.Idle;
        public bool WaitingForTicket => _phase == Phase.Ticket;

        void Advance()
        {
            unchecked { _token++; }
            if (_token == 0) _token = 1;
        }

        public void Reset(float now)
        {
            Advance();
            _phase = Phase.Idle;
            _failures = 0;
            _retryAt = 0;
            _due = now + 1f;
        }

        public void MarkDirty(float now, float delay = SaveDelay) =>
            _due = Math.Min(_due, now + delay);

        public bool Due(float now) => !Busy && now >= _due && now >= _retryAt;
        public bool IsCurrent(int token) => Busy && token != 0 && token == _token;
        public bool TicketExpired(float now) => WaitingForTicket && now >= _ticketDeadline;

        public bool TryBegin(float now, out int token)
        {
            token = 0;
            if (Busy || now < _retryAt) return false;
            Advance();
            token = _token;
            _phase = Phase.Ticket;
            _ticketDeadline = now + TicketTimeout;
            _due = float.PositiveInfinity;
            return true;
        }

        public bool BeginHttp(int token)
        {
            if (!IsCurrent(token) || !WaitingForTicket) return false;
            _phase = Phase.Http;
            return true;
        }

        public bool Fail(int token, float now)
        {
            if (!IsCurrent(token)) return false;
            _phase = Phase.Idle;
            _failures = Math.Min(_failures + 1, 6);
            _retryAt = now + Math.Min(Period, InitialRetry * (1 << (_failures - 1)));
            _due = Math.Min(_due, _retryAt);
            return true;
        }

        public bool Complete(int token, float now)
        {
            if (!IsCurrent(token)) return false;
            _phase = Phase.Idle;
            _failures = 0;
            _retryAt = 0;
            _due = Math.Min(_due, now + Period);
            return true;
        }

        public void Defer(float now)
        {
            if (!Busy) _due = now + Period;
        }
    }
}
