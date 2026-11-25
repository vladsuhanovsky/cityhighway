using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Epoch for session-bound social state. An A -> B -> A switch must not
    /// revive an HTTP callback that belonged to the earlier A session.
    /// This deliberately contains no network calls and can be tested in NUnit.
    /// </summary>
    public sealed class SocialAccountScope
    {
        string _owner;
        int _generation;

        public string Owner => _owner;
        public int Generation => _generation;

        public bool Update(string steamId)
        {
            string next = SocialReportOutbox.ValidOwner(steamId) ? steamId : null;
            if (string.Equals(_owner, next, StringComparison.Ordinal)) return false;
            _owner = next;
            unchecked { _generation++; }
            return true;
        }

        public bool Matches(string owner, int generation)
        {
            return generation == _generation && !string.IsNullOrEmpty(owner) &&
                string.Equals(_owner, owner, StringComparison.Ordinal);
        }
    }
}
