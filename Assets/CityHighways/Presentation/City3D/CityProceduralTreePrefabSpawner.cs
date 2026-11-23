using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Capped, deterministic tree Prefab replacements. Existing far-tree card LOD is preserved
    /// by never replacing CardOnly trees, and by leaving all unmatched trees in the original plan.
    /// </summary>
    public static class CityProceduralTreePrefabSpawner
    {
        public static int Apply(CityPlanner.Plan plan, string mapName, Transform cityRoot)
        {
            return Apply(plan, mapName, CityProceduralTreePrefabRules.FindForMap(mapName), cityRoot, null);
        }

        /// <summary>Overload for Edit Mode tests and direct configuration.</summary>
        public static int Apply(CityPlanner.Plan plan, string mapName,
            CityProceduralTreePrefabRules config, Transform cityRoot)
            => Apply(plan, mapName, config, cityRoot, null);

        public static int Apply(CityPlanner.Plan plan, string mapName,
            CityProceduralTreePrefabRules config, Transform cityRoot,
            CityProceduralPrefabBudget.Session budget)
        {
            if (plan == null || cityRoot == null || config == null ||
                !config.AppliesTo(mapName)) return 0;
            var rules = config.Rules;
            if (rules == null || rules.Length == 0) return 0;
            var used = new int[rules.Length];
            int replaced = 0;
            for (int i = 0; i < plan.Trees.Count;)
            {
                var tree = plan.Trees[i];
                // These trees have only a camera-facing card mesh; don't replace them with
                // potentially expensive always-on Prefabs in distant forests.
                if (tree.CardOnly) { i++; continue; }

                int chosen = -1;
                for (int j = 0; j < rules.Length; j++)
                {
                    var rule = rules[j];
                    if (rule == null || rule.visualPrefab == null ||
                        rule.maxInstances <= 0 || used[j] >= rule.maxInstances ||
                        rule.treeKind != tree.Kind ||
                        !CityProceduralBuildingPrefabSpawner.Selected(
                            tree.Seed, tree.At, rule.probability))
                        continue;
                    chosen = j;
                    break;
                }
                if (chosen < 0) { i++; continue; }

                var selected = rules[chosen];
                if (budget != null && !budget.TryReserve(selected.visualPrefab)) { i++; continue; }
                var instance = Object.Instantiate(selected.visualPrefab, cityRoot);
                if (instance == null) { i++; continue; }
                instance.name = "Generated " + tree.Kind + " tree (rule)";
                var position = CityPlanner.ToWorld(tree.At);
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                // A unit-height Prefab represents a 1m tree; tree.Size supplies the
                // procedural world's target height, and prefabScale is an art multiplier.
                instance.transform.localScale *=
                    tree.Size * Mathf.Clamp(selected.prefabScale, 0.05f, 5f);

                // A tree has no matching footprint entry. Remove only the selected tree.
                if (CityProceduralReplacementResolver.TryRemoveById(plan,
                    CityProceduralReplacement.TargetKind.Tree,
                    CityProceduralReplacementResolver.TreeId(tree)))
                {
                    used[chosen]++;
                    replaced++;
                }
                else
                {
                    if (Application.isPlaying) Object.Destroy(instance);
                    else Object.DestroyImmediate(instance);
                    i++;
                }
            }
            return replaced;
        }
    }
}
