using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Small scene-owned lazy pool. No unbounded creation on consecutive constructions.
    /// The material is borrowed; caller disposes it after clearing this pool.</summary>
    public sealed class ConstructionFleetPool
    {
        public const int MaxPerType = 2;
        readonly Transform root;
        readonly Material material;
        readonly FleetEmblemCache emblems = new FleetEmblemCache();
        readonly List<FleetVehicleVisual> models = new List<FleetVehicleVisual>();
        readonly HashSet<FleetVehicleVisual> active = new HashSet<FleetVehicleVisual>();
        public int Count => models.Count;
        public int ActiveCount => active.Count;

        public ConstructionFleetPool(Transform root, Material material)
        {
            this.root = root;
            this.material = material;
        }

        public FleetVehicleVisual Acquire(FleetLivery value)
        {
            if (value == null || root == null || material == null) return null;
            // Prune externally destroyed roots before counting or creating replacements.
            for (int i = models.Count - 1; i >= 0; i--)
            {
                if (models[i].Root != null) continue;
                active.Remove(models[i]);
                models.RemoveAt(i);
            }
            int count = 0;
            foreach (var model in models)
            {
                if (model.Vehicle != value.Vehicle || model.Root == null) continue;
                count++;
                if (active.Contains(model)) continue;
                if (!model.Apply(value)) return null;
                model.ResetMotion();
                model.Root.SetActive(true);
                active.Add(model);
                return model;
            }
            if (count >= MaxPerType) return null;
            var created = FleetVehicleVisual.Build(value, material, root, emblems);
            if (created == null) return null;
            models.Add(created);
            active.Add(created);
            return created;
        }

        public void Release(FleetVehicleVisual model)
        {
            if (model == null || !active.Remove(model) || model.Root == null) return;
            model.ResetMotion();
            model.ClearBranding();
            model.Root.SetActive(false);
            model.Root.transform.SetParent(root, false);
            model.Root.transform.localPosition = Vector3.zero;
            model.Root.transform.localRotation = Quaternion.identity;
            model.Root.transform.localScale = Vector3.one;
        }

        public void Clear()
        {
            foreach (var model in models)
            {
                if (model.Root == null) continue;
                model.Root.SetActive(false);
                if (Application.isPlaying) Object.Destroy(model.Root);
                else Object.DestroyImmediate(model.Root);
            }
            models.Clear();
            active.Clear();
            emblems.Clear();
        }
    }
}
