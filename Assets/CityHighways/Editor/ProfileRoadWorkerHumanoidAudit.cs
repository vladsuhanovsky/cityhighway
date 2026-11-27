using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

public static class ProfileRoadWorkerHumanoidAudit
{
    [MenuItem("City Highways/Profile/Audit Road Worker Humanoid")]
    public static void Audit()
    {
        foreach (bool female in new[] { false, true })
        {
            string path = ProfileRoadWorkerTrial.AssetPath(female, false);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!ProfileRoadWorkerTrial.TryInspect(asset, out _, out var error))
            { Debug.LogWarning(path + ": " + error); continue; }
            PreviewRenderUtility preview = null;
            Avatar avatar = null;
            GameObject clone = null;
            try
            {
                preview = new PreviewRenderUtility();
                clone = preview.InstantiatePrefabInScene(asset);
                foreach (var a in clone.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                foreach (var a in clone.GetComponentsInChildren<Animation>(true)) a.enabled = false;
                var boneSet = new HashSet<Transform>();
                foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (var bone in skin.bones)
                        for (var t = bone; t != null; t = t.parent)
                        {
                            boneSet.Add(t);
                            if (t == clone.transform) break;
                        }
                var ordered = clone.GetComponentsInChildren<Transform>(true).Where(boneSet.Contains).ToArray();
                if (ordered.Select(t => t.name).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
                    throw new InvalidOperationException("Duplicate skeleton transform names.");
                var names = ordered.ToDictionary(t => t.name, StringComparer.Ordinal);
                var human = new List<HumanBone>();
                var missing = new List<string>();
                string[] trait = HumanTrait.BoneName;
                for (int i = 0; i < trait.Length; i++)
                {
                    string name = trait[i];
                    if (names.TryGetValue(name.Replace(" ", ""), out var bone))
                        human.Add(new HumanBone { humanName = name, boneName = bone.name,
                            limit = new HumanLimit { useDefaultValues = true } });
                    else if (HumanTrait.RequiredBone(i)) missing.Add(name);
                }
                if (missing.Count != 0)
                { Debug.LogWarning(path + ": missing required bones: " + string.Join(", ", missing)); continue; }
                var desc = new HumanDescription {
                    human = human.ToArray(),
                    skeleton = ordered.Select(t => new SkeletonBone { name = t.name, position = t.localPosition,
                        rotation = t.localRotation, scale = t.localScale }).ToArray(),
                    upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                    armStretch = .05f, legStretch = .05f, feetSpacing = 0f, hasTranslationDoF = false
                };
                avatar = AvatarBuilder.BuildHumanAvatar(clone, desc);
                bool valid = avatar != null && avatar.isValid && avatar.isHuman;
                string message = path + ": mapped " + human.Count + " human bones; temporary Humanoid Avatar valid: " +
                    valid + ". GLB clips remain transform clips; T-pose, retargeting and imported Human clips require separate verification.";
                if (valid) Debug.Log(message); else Debug.LogWarning(message);
            }
            catch (Exception ex) { Debug.LogWarning(path + ": Humanoid audit failed: " + ex.Message); }
            finally
            {
                if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                preview?.Cleanup();
            }
        }
    }
}
