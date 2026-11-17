using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>
    /// Классическая партия как «доска» для «Логистического магната»: магнат сам решает, чьи дороги, где пробки
    /// и какие дороги — магистрали, а карта, 3D-город и интерфейс читают всё это из Game как обычно.
    /// </summary>
    public sealed partial class Game
    {
        readonly Dictionary<string, int> _logisticsTraffic = new Dictionary<string, int>();
        readonly HashSet<string> _logisticsJams = new HashSet<string>();

        internal void LogisticsSetOwner(string roadId, int owner) => _roadOwners[roadId] = owner;

        internal void LogisticsSetHighway(string roadId) => _rushHourRoads.Add(roadId);

        internal void LogisticsSetTraffic(IDictionary<string, int> traffic, IEnumerable<string> jams)
        {
            _logisticsTraffic.Clear();
            _logisticsJams.Clear();
            if (traffic != null) foreach (var pair in traffic) _logisticsTraffic[pair.Key] = pair.Value;
            if (jams != null) foreach (var id in jams) _logisticsJams.Add(id);
        }

        internal void LogisticsSetPhase(GamePhase phase) => Phase = phase;
    }
}
