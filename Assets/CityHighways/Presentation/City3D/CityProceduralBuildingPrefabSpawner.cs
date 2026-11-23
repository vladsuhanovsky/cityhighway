using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Deterministic and capped opt-in Prefab placement for generated buildings.</summary>
    public static class CityProceduralBuildingPrefabSpawner
    {
        /// <summary>Stable choice from the generated building seed and field coordinates.</summary>
        public static bool Selected(int seed, Vector2 position, float probability)
        {
            if (float.IsNaN(probability) || probability <= 0f) return false;
            if (probability >= 1f) return true;
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)Mathf.RoundToInt(position.x * 1000f) * 0x9e3779b9u;
                hash ^= (uint)Mathf.RoundToInt(position.y * 1000f) * 0x85ebca6bu;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash / 4294967296.0) < probability;
            }
        }

        /// <summary>Run after manually-authored replacements, before building meshes are batched.</summary>
        public static int Apply(CityPlanner.Plan plan, string mapName, Transform cityRoot)
        {
            var config = CityProceduralBuildingPrefabRules.FindForMap(mapName);
            return Apply(plan, mapName, config, cityRoot, null);
        }

        /// <summary>Explicit config overload for testability; absent settings are a no-op.</summary>
        public static int Apply(CityPlanner.Plan plan, string mapName,
            CityProceduralBuildingPrefabRules config, Transform cityRoot)
            => Apply(plan, mapName, config, cityRoot, null);

        public static int Apply(CityPlanner.Plan plan, string mapName,
            CityProceduralBuildingPrefabRules config, Transform cityRoot,
            CityProceduralPrefabBudget.Session budget)
        {
            if (plan == null || cityRoot == null || config == null ||
                !config.AppliesTo(mapName)) return 0;
            var rules = config.Rules;
            if (rules == null || rules.Length == 0) return 0;
            var used = new int[rules.Length];
            int replaced = 0;
            for (int i = 0; i < plan.Buildings.Count;)
            {
                var building = plan.Buildings[i];
                int chosen = -1;
                // First matching rule takes priority; each rule has an independent cap.
                for (int j = 0; j < rules.Length; j++)
                {
                    var rule = rules[j];
                    if (rule == null || rule.visualPrefab == null ||
                        rule.maxInstances <= 0 || used[j] >= rule.maxInstances ||
                        rule.buildingKind != building.Kind ||
                        !Selected(building.Seed, building.Center, rule.probability))
                        continue;
                    chosen = j;
                    break;
                }
                if (chosen < 0) { i++; continue; }

                var selected = rules[chosen];
                if (budget != null && !budget.TryReserve(selected.visualPrefab)) { i++; continue; }
                var instance = Object.Instantiate(selected.visualPrefab, cityRoot);
                if (instance == null) { i++; continue; }
                instance.name = "Generated " + building.Kind + " (rule)";
                var position = CityPlanner.ToWorld(building.Center);
                position.y = CityPlanner.BuildingBaseHeight(building);
                instance.transform.SetPositionAndRotation(
                    position, Quaternion.Euler(0f, building.Angle, 0f));
                instance.transform.localScale *= Mathf.Clamp(selected.prefabScale, 0.05f, 5f);
                // A successful Prefab instance replaces exactly one generated building.
                // Keep Footprints aligned with Building indices, including outer-city entries.
                if (CityProceduralReplacementResolver.TryRemoveById(plan,
                    CityProceduralReplacement.TargetKind.Building,
                    CityProceduralReplacementResolver.BuildingId(building)))
                {
                    used[chosen]++;
                    replaced++;
                }
                else
                {
                    Object.Destroy(instance);
                    i++;
                }
            }
            return replaced;
        }
    }
}
