using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Replaces a capped subset of generated visual props. Unselected props retain
    /// district mesh batching and game-state-independent generation.
    /// </summary>
    public static class CityProceduralPropPrefabSpawner
    {
        public static int Apply(CityPlanner.Plan plan, string cityName, Transform root,
            IDictionary<string, GameObject> districtVisuals, float waterLevel)
        {
            return Apply(plan, cityName, CityProceduralPropPrefabRules.FindForMap(cityName),
                root, districtVisuals, waterLevel, null);
        }

        public static int Apply(CityPlanner.Plan plan, string cityName,
            CityProceduralPropPrefabRules config, Transform root,
            IDictionary<string, GameObject> districtVisuals, float waterLevel)
            => Apply(plan, cityName, config, root, districtVisuals, waterLevel, null);

        public static int Apply(CityPlanner.Plan plan, string cityName,
            CityProceduralPropPrefabRules config, Transform root,
            IDictionary<string, GameObject> districtVisuals, float waterLevel,
            CityProceduralPrefabBudget.Session budget)
        {
            if (plan == null || config == null || root == null ||
                !config.AppliesTo(cityName)) return 0;
            var rules = config.Rules;
            if (rules == null || rules.Length == 0) return 0;

            var used = new int[rules.Length];
            // These group roots are registered into City3DScene's existing district
            // visibility system. The underscore district stays visible by default.
            var groupRoots = new Dictionary<string, GameObject>();
            int applied = 0;
            for (int i = 0; i < plan.Props.Count;)
            {
                var prop = plan.Props[i];
                int selected = -1;
                for (int j = 0; j < rules.Length; j++)
                {
                    var rule = rules[j];
                    if (rule == null || rule.visualPrefab == null ||
                        rule.maxInstances <= used[j] ||
                        rule.propKind != prop.Kind ||
                        !CityProceduralBuildingPrefabSpawner.Selected(
                            prop.Seed, prop.At, rule.probability))
                        continue;
                    selected = j;
                    break;
                }
                if (selected < 0) { i++; continue; }

                var chosen = rules[selected];
                if (budget != null && !budget.TryReserve(chosen.visualPrefab)) { i++; continue; }
                var key = string.IsNullOrEmpty(prop.District) ? "_" : prop.District;
                if (!groupRoots.TryGetValue(key, out var group))
                {
                    group = new GameObject("Rule props " + key);
                    group.transform.SetParent(root, false);
                    groupRoots.Add(key, group);
                    if (key != "_" && districtVisuals != null)
                        districtVisuals[key + "#ruleprops"] = group;
                }

                var instance = Object.Instantiate(chosen.visualPrefab, group.transform);
                if (instance == null) { i++; continue; }

                instance.name = "Generated " + prop.Kind + " (rule)";
                var world = CityPlanner.ToWorld(prop.At,
                    prop.Kind == CityPlanner.PropKind.Boat ? waterLevel - 0.001f : 0f);
                instance.transform.SetPositionAndRotation(
                    world, Quaternion.Euler(0f, prop.Angle, 0f));
                // Prop Prefabs use authored world-unit proportions, unlike the
                // normalized unit-height tree Prefabs. Preserve authored scale.
                instance.transform.localScale *= Mathf.Clamp(chosen.prefabScale, 0.05f, 5f);
                plan.Props.RemoveAt(i);
                used[selected]++;
                applied++;
            }
            return applied;
        }
    }
}
