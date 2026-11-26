using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Reusable original rigid construction vehicle visual. The caller owns the
    /// root and shared material. No physics, scripts, textures, material instances or game state.</summary>
    public sealed class FleetVehicleVisual
    {
        public GameObject Root { get; }
        public string Vehicle { get; }
        readonly List<Renderer> main = new List<Renderer>();
        readonly List<Renderer> accent = new List<Renderer>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        FleetVehicleVisual(GameObject root, string vehicle) { Root = root; Vehicle = vehicle; }

        public bool Apply(FleetLivery value)
        {
            if (Root == null || value == null || value.Vehicle != Vehicle) return false;
            foreach (var renderer in main) if (renderer != null) SetColor(renderer, value.Primary);
            foreach (var renderer in accent) if (renderer != null) SetColor(renderer, value.Secondary);
            return true;
        }

        void SetColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
            block.Clear();
        }

        /// <summary>Local +Z is forward, Y=0 is ground. Caller chooses scale, layer and lifetime.
        /// Material must support BaseColor or Color and use a neutral base texture.</summary>
        public static FleetVehicleVisual Build(FleetLivery livery, Material sharedMaterial, Transform parent = null)
        {
            if (livery == null || sharedMaterial == null ||
                (!sharedMaterial.HasProperty(BaseColor) && !sharedMaterial.HasProperty(ColorId))) return null;
            var root = new GameObject("Fleet " + livery.Vehicle);
            root.transform.SetParent(parent, false);
            if (parent != null) root.layer = parent.gameObject.layer;
            var visual = new FleetVehicleVisual(root, livery.Vehicle);
            try
            {
                visual.BuildParts(sharedMaterial);
                if (!visual.Apply(livery)) throw new InvalidOperationException("Invalid fleet livery.");
                return visual;
            }
            catch
            {
                root.SetActive(false);
                if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
                throw;
            }
        }

        void BuildParts(Material material)
        {
            var tire = new Color(.08f,.10f,.12f);
            var metal = new Color(.45f,.50f,.54f);
            var glass = new Color(.37f,.60f,.70f);
            var lamp = new Color(.93f,.87f,.62f);
            var black = new Color(.16f,.18f,.20f);
            // paint = 1 primary, 2 secondary, 0 mechanical detail.
            void Part(string name, PrimitiveType shape, Vector3 pos, Vector3 size, Color fixedColor, int paint = 0, Vector3 rotation = default)
            {
                var obj = GameObject.CreatePrimitive(shape);
                obj.name = name;
                obj.transform.SetParent(Root.transform, false);
                obj.layer = Root.layer;
                obj.transform.localPosition = pos;
                obj.transform.localScale = size;
                obj.transform.localRotation = Quaternion.Euler(rotation);
                var collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
                }
                var renderer = obj.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (paint == 1) main.Add(renderer);
                else if (paint == 2) accent.Add(renderer);
                else SetColor(renderer, fixedColor);
            }
            void Box(string n,float x,float y,float z,float w,float h,float d,Color c,int paint=0,Vector3 r=default) =>
                Part(n,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(w,h,d),c,paint,r);
            void Cylinder(string n,float x,float y,float z,float w,float h,float d,Color c,int paint=0,Vector3 r=default) =>
                Part(n,PrimitiveType.Cylinder,new Vector3(x,y,z),new Vector3(w,h,d),c,paint,r);
            void Wheel(float x,float z,float radius)
            {
                Cylinder("Tire",x,radius,z,radius*2,.115f,radius*2,tire,0,new Vector3(0,0,90));
                Cylinder("Hub",x+Mathf.Sign(x)*.12f,radius,z,radius*.92f,.025f,radius*.92f,metal,0,new Vector3(0,0,90));
            }
            void Cab(float z, float y=.80f)
            {
                Box("Cab",0,y,z,.77f,.64f,.60f,black,1);
                Box("Windshield",0,y+.12f,z+.31f,.65f,.29f,.025f,glass);
                foreach(int side in new[]{-1,1})
                {
                    Box("Side window",side*.391f,y+.12f,z,.025f,.28f,.42f,glass);
                    Box("Door stripe",side*.398f,y-.14f,z,.025f,.10f,.50f,black,2);
                    Box("Headlamp",side*.25f,y-.24f,z+.32f,.14f,.09f,.025f,lamp);
                }
                Box("Roof",0,y+.34f,z,.82f,.06f,.64f,black,2);
            }
            switch (Vehicle)
            {
                case "paver":
                    Box("Chassis",0,.30f,0,.92f,.22f,1.52f,black);
                    foreach(int side in new[]{-1,1})
                    {
                        Box("Track",side*.48f,.19f,0,.26f,.36f,1.28f,tire);
                        foreach(float z in new[]{-.45f,-.15f,.15f,.45f})
                            Cylinder("Track roller",side*.62f,.19f,z,.23f,.025f,.23f,metal,0,new Vector3(0,0,90));
                    }
                    Box("Engine",0,.60f,-.23f,.85f,.42f,.74f,black,1);
                    Cab(-.23f,1.03f);
                    Box("Hopper floor",0,.43f,.57f,1.08f,.16f,.62f,metal);
                    foreach(int side in new[]{-1,1})
                        Box("Hopper side",side*.49f,.65f,.60f,.13f,.43f,.72f,black,1,new Vector3(0,0,side*-16));
                    Box("Hopper edge",0,.77f,.90f,1.07f,.09f,.12f,black,2);
                    Box("Screed",0,.16f,-.91f,1.43f,.14f,.36f,metal);
                    break;
                case "mixer":
                    Box("Chassis",0,.35f,0,.83f,.20f,2.06f,black);
                    foreach(int side in new[]{-1,1}) foreach(float z in new[]{.72f,-.42f,-.78f}) Wheel(side*.48f,z,.24f);
                    Cab(.67f);
                    Box("Mixer support",0,.61f,-.38f,.76f,.27f,1.14f,black,2);
                    Cylinder("Mixing drum",0,.95f,-.43f,.76f,.57f,.76f,black,1,new Vector3(74,0,0));
                    Cylinder("Drum band",0,.95f,-.43f,.78f,.075f,.78f,black,2,new Vector3(74,0,0));
                    Box("Discharge chute",0,.58f,-1.03f,.30f,.11f,.46f,metal,0,new Vector3(-25,0,0));
                    break;
                case "grader":
                    Box("Frame",0,.46f,.10f,.30f,.18f,2.25f,black,1);
                    foreach(int side in new[]{-1,1}) foreach(float z in new[]{.91f,-.50f,-.85f}) Wheel(side*.48f,z,.24f);
                    Box("Engine",0,.63f,-.71f,.79f,.43f,.69f,black,1);
                    Cab(-.25f,1.02f);
                    Box("Front beam stripe",0,.56f,.58f,.33f,.08f,.77f,black,2);
                    Cylinder("Blade circle",0,.36f,.18f,.49f,.06f,.49f,metal);
                    Box("Grading blade",0,.17f,.18f,1.18f,.23f,.095f,metal,0,new Vector3(0,24,0));
                    break;
                case "crane":
                    Box("Chassis",0,.35f,0,.92f,.21f,2.10f,black);
                    foreach(int side in new[]{-1,1}) foreach(float z in new[]{.75f,-.42f,-.78f}) Wheel(side*.50f,z,.24f);
                    Cab(.70f);
                    Cylinder("Slew ring",0,.58f,-.37f,.62f,.12f,.62f,metal);
                    Box("Counterweight",0,.80f,-.68f,.78f,.39f,.48f,black,1);
                    Box("Boom base",0,1.03f,-.36f,.31f,.28f,.55f,black,2);
                    Box("Boom",0,1.42f,.12f,.24f,.24f,1.40f,black,1,new Vector3(-30,0,0));
                    Box("Boom extension",0,1.78f,.74f,.17f,.17f,.65f,black,2,new Vector3(-30,0,0));
                    Box("Cable",0,1.63f,1.01f,.025f,.48f,.025f,metal);
                    Box("Hook",0,1.36f,1.01f,.13f,.10f,.10f,metal);
                    break;
                default: throw new InvalidOperationException("Unknown vehicle " + Vehicle);
            }
        }
    }
}
