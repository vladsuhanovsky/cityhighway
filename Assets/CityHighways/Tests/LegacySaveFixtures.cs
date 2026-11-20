using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>
    /// Deterministic compatibility fixtures produced from a known small test map.
    /// These are version-tagged smoke fixtures, not archival files produced by old binaries.
    /// True historical JSON snapshots should be added as separate fixtures before release.
    /// </summary>
    internal static class LegacySaveFixtures
    {
        public static GameSaveData Snapshot(Game game, int version)
        {
            var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            snapshot.version = version;
            if (version < 5)
            {
                snapshot.start = null;
                snapshot.history = null;
            }
            return JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(snapshot));
        }
    }
}
