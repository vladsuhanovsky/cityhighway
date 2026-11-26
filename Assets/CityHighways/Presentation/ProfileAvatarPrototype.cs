#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Original procedural static visual study for testing the runtime preview.
    /// No external assets, no skinned animation and no automatic use in player builds.</summary>
    public static class ProfileAvatarPrototype
    {
        public static GameObject Build(string gender, Func<Color, Material> material)
        {
            if (gender != CharacterCatalog.Male && gender != CharacterCatalog.Female)
                throw new ArgumentException("Unsupported gender.", nameof(gender));
            if (material == null) throw new ArgumentNullException(nameof(material));
            var root = new GameObject("Prototype " + gender);
            try
            {
                var rig = root.AddComponent<ProfileAvatarRig>();
                var bindings = new List<ProfileAvatarRig.Binding>();
                foreach (string slot in CharacterCatalog.Slots)
                    foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                    {
                        var variant = new GameObject(item.id);
                        variant.transform.SetParent(root.transform, false);
                        BuildVariant(variant.transform, item, gender == CharacterCatalog.Female, material);
                        if (variant.transform.childCount == 0)
                            throw new InvalidOperationException("Empty prototype variant: " + item.id);
                        bindings.Add(new ProfileAvatarRig.Binding { itemId = item.id, visual = variant });
                    }
                rig.ConfigurePrototype(gender, bindings.ToArray());
                if (!rig.TryApply(ProfileAppearance.FromOutfit(Outfit.DefaultFor(gender)), out var error))
                    throw new InvalidOperationException(error);
                if (rig.MissingCatalogIds().Length != 0)
                    throw new InvalidOperationException("Prototype catalog is incomplete.");
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        static readonly Color Navy = new Color(.15f,.23f,.32f);
        static readonly Color Slate = new Color(.29f,.36f,.42f);
        static readonly Color Denim = new Color(.25f,.40f,.60f);
        static readonly Color Orange = new Color(.94f,.43f,.09f);
        static readonly Color White = new Color(.91f,.90f,.83f);
        static readonly Color Dark = new Color(.10f,.12f,.14f);
        static readonly Color Brown = new Color(.24f,.15f,.10f);
        static readonly Color Green = new Color(.30f,.46f,.40f);
        static readonly Color Gold = new Color(.83f,.63f,.25f);

        static void BuildVariant(Transform parent, CharacterItem item, bool female, Func<Color, Material> material)
        {
            string id = item.id;
            bool Has(string word) => id.Contains(word);
            float shoulder = female ? .45f : .52f;
            void Part(string name, PrimitiveType shape, float x, float y, float z,
                float sx, float sy, float sz, Color color, float tilt = 0f)
            {
                var obj = GameObject.CreatePrimitive(shape);
                obj.name = name;
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = new Vector3(x,y,z);
                obj.transform.localScale = new Vector3(sx,sy,sz);
                obj.transform.localRotation = Quaternion.Euler(0,0,tilt);
                var collider = obj.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
                var mat = material(color);
                if (mat != null) obj.GetComponent<Renderer>().sharedMaterial = mat;
            }
            void Ball(string n, float x,float y,float z,float sx,float sy,float sz,Color c) =>
                Part(n,PrimitiveType.Sphere,x,y,z,sx,sy,sz,c);
            void Box(string n, float x,float y,float z,float sx,float sy,float sz,Color c,float a=0f) =>
                Part(n,PrimitiveType.Cube,x,y,z,sx,sy,sz,c,a);
            void Pill(string n,float x,float y,float z,float sx,float sy,float sz,Color c,float a=0f) =>
                Part(n,PrimitiveType.Capsule,x,y,z,sx,sy,sz,c,a);
            void Disc(string n,float x,float y,float z,float sx,float sy,float sz,Color c) =>
                Part(n,PrimitiveType.Cylinder,x,y,z,sx,sy,sz,c);

            switch (item.slot)
            {
                case "body":
                    Color skin = item.tone == "dark" ? new Color(.43f,.26f,.17f) :
                        item.tone == "tan" ? new Color(.72f,.48f,.32f) : new Color(.90f,.70f,.55f);
                    Ball("Head",0,1.66f,0,.40f,.44f,.37f,skin);
                    Disc("Neck",0,1.43f,0,.16f,.09f,.16f,skin);
                    Ball("Nose",0,1.65f,.181f,.062f,.068f,.055f,skin);
                    Box("Mouth",0,1.57f,.17f,.085f,.014f,.02f,Brown);
                    foreach (int side in new[] {-1,1})
                    {
                        Ball("Ear",side*.20f,1.65f,0,.07f,.13f,.08f,skin);
                        Ball("Eye",side*.075f,1.70f,.166f,.046f,.050f,.032f,White);
                        Ball("Pupil",side*.075f,1.70f,.180f,.022f,.027f,.018f,Dark);
                        Ball("Hand",side*(shoulder*.5f+.09f),.84f,0,.135f,.16f,.13f,skin);
                        Pill("Forearm",side*(shoulder*.5f+.075f),1.01f,0,.125f,.15f,.13f,skin,side*5);
                    }
                    break;
                case "hair":
                    Ball("Hair crown",0,1.79f,-.035f,.43f,.23f,.39f,Brown);
                    if (Has("long") || Has("bob") || Has("curly"))
                        Ball("Hair back",0,Has("long")?1.52f:1.60f,-.13f,.43f,Has("long")?.50f:.36f,.24f,Brown);
                    if (Has("bun") || Has("ponytail"))
                        Ball("Tied hair",0,Has("bun")?1.83f:1.62f,-.24f,.19f,Has("bun")?.20f:.36f,.22f,Brown);
                    if (Has("beard")) Ball("Beard",0,1.53f,.12f,.28f,.15f,.18f,Brown);
                    if (Has("curly"))
                        for(int i=0;i<5;i++) Ball("Curl", (i-2)*.08f,1.85f,.035f,.12f,.12f,.13f,Brown);
                    break;
                case "top":
                    Color shirt = Has("vest") ? Orange : Has("jacket") || Has("blazer") ? Navy :
                        Has("sweater") || Has("cardigan") ? Green : Has("shirt") && !Has("tshirt") || Has("blouse") ? White : Slate;
                    Pill("Torso",0,1.15f,0,shoulder,.30f,.30f,shirt);
                    foreach (int side in new[] {-1,1})
                    {
                        Pill("Sleeve",side*(shoulder*.5f+.045f),1.24f,0,.175f,.17f,.20f,shirt,side*8);
                        if (Has("vest"))
                            Box("Reflective strip",side*.11f,1.18f,.15f,.032f,.30f,.024f,White);
                    }
                    if (Has("shirt") || Has("blouse") || Has("jacket") || Has("blazer") || Has("cardigan"))
                        Box("Placket",0,1.19f,.15f,.032f,.33f,.027f,Dark);
                    if (Has("jacket") || Has("blazer")) 
                        foreach(int side in new[]{-1,1}) Box("Lapel",side*.085f,1.32f,.14f,.07f,.18f,.04f,Slate,side*22);
                    break;
                case "bottom":
                    Color trousers = Has("jeans") ? Denim : Has("overalls") ? Orange : Has("classic") ? Dark : Navy;
                    Ball("Hips",0,.85f,0,.45f,.28f,.31f,trousers);
                    if (Has("skirt"))
                    {
                        Disc("Skirt",0,.67f,0,.47f,.20f,.34f,trousers);
                        foreach(int side in new[]{-1,1}) Pill("Legging",side*.12f,.36f,0,.145f,.25f,.15f,Dark);
                    }
                    else foreach(int side in new[]{-1,1})
                        Pill("Trouser leg",side*.12f,.47f,0,.20f,.32f,.23f,trousers);
                    if (Has("overalls"))
                    {
                        Box("Bib",0,1.08f,.18f,.28f,.25f,.045f,trousers);
                        foreach(int side in new[]{-1,1}) Box("Strap",side*.11f,1.30f,.16f,.055f,.23f,.04f,trousers);
                    }
                    break;
                case "shoes":
                    Color shoe = Has("sneakers") ? White : Has("rubber") ? Orange : Has("oxford") || Has("heels") ? Brown : Dark;
                    foreach(int side in new[]{-1,1})
                    {
                        Ball("Shoe",side*.12f,.12f,.055f,.21f,.19f,.34f,shoe);
                        Box("Sole",side*.12f,.055f,.055f,.20f,.05f,.32f,Dark);
                        if (Has("boots") || Has("rubber")) Disc("Cuff",side*.12f,.23f,0,.21f,Has("rubber")?.15f:.09f,.23f,shoe);
                        if (Has("heels")) Box("Heel",side*.12f,.04f,-.05f,.065f,.08f,.07f,Brown);
                    }
                    break;
                case "hat":
                    Color hat = Has("hardhat") ? Orange : Has("cap") ? Denim : Has("sunhat") ? Gold : Dark;
                    if (Has("tophat")) Disc("Tall crown",0,1.96f,0,.33f,.17f,.33f,hat);
                    else Ball("Crown",0,1.84f,0,.45f,.23f,.42f,hat);
                    if (!Has("beret"))
                        Disc("Brim",0,1.78f,Has("cap")?.105f:0f,Has("sunhat")?.70f:.52f,.025f,Has("sunhat")?.65f:.47f,hat);
                    if (Has("hardhat")) Box("Helmet ridge",0,1.94f,0,.055f,.045f,.32f,hat);
                    break;
                case "accessory":
                    if (Has("glasses"))
                    {
                        foreach(int side in new[]{-1,1}) Box("Lens",side*.083f,1.70f,.197f,.13f,.065f,.026f,Navy);
                        Box("Bridge",0,1.70f,.208f,.055f,.02f,.025f,Dark);
                    }
                    else if (Has("tie") || Has("scarf"))
                    {
                        Disc("Knot",0,1.39f,.13f,.11f,.06f,.10f,Has("tie")?Orange:Green);
                        Box("Tail",0,1.23f,.17f,.075f,.23f,.025f,Has("tie")?Orange:Green,Has("tie")?0f:12f);
                    }
                    else if (Has("chain") || Has("pearls"))
                    {
                        for(int i=-3;i<=3;i++)
                            Ball("Bead",i*.037f,1.34f+.012f*Mathf.Abs(i),.17f,.045f,.045f,.045f,Has("pearls")?White:Gold);
                    }
                    else if (Has("radio"))
                    {
                        Box("Radio",shoulder*.5f+.09f,.84f,.10f,.13f,.21f,.09f,Dark);
                        Box("Antenna",shoulder*.5f+.09f,1.01f,.10f,.025f,.17f,.025f,Dark);
                    }
                    else if (Has("blueprint"))
                        Disc("Rolled plan",shoulder*.5f+.09f,.85f,.12f,.12f,.21f,.12f,White);
                    else if (Has("tablet"))
                    {
                        Box("Tablet",shoulder*.5f+.08f,.85f,.13f,.20f,.29f,.045f,Dark);
                        Box("Screen",shoulder*.5f+.08f,.85f,.158f,.16f,.23f,.014f,Denim);
                    }
                    else throw new InvalidOperationException("Unsupported accessory: " + id);
                    break;
                default: throw new InvalidOperationException("Unsupported slot: " + item.slot);
            }
        }
    }
}
#endif
