using System;

namespace CityHighways.Presentation
{
    /// <summary>One pending user intent per banner; a new selection invalidates old callbacks.</summary>
    public sealed class GhostBannerRequest
    {
        readonly SocialRequestGate _gate = new SocialRequestGate();
        public bool Pending { get; private set; }

        public bool TryBegin(out int token)
        {
            token = 0;
            if (Pending) return false;
            token = _gate.Begin();
            Pending = true;
            return true;
        }

        public bool TryComplete(int token)
        {
            if (!Pending || !_gate.IsCurrent(token)) return false;
            Pending = false;
            return true;
        }

        public void Cancel()
        {
            _gate.Invalidate();
            Pending = false;
        }

        public static bool SameRecord(Ghost a, Ghost b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.RecordId == b.RecordId && a.ReplayId == b.ReplayId &&
                a.Source == b.Source && a.Mode == b.Mode && a.MapPath == b.MapPath &&
                a.Name == b.Name && a.Score == b.Score && a.Won == b.Won &&
                a.Seats == b.Seats && a.Date == b.Date &&
                a.SteamOwner == b.SteamOwner && a.SteamGeneration == b.SteamGeneration;
        }
    }
}
