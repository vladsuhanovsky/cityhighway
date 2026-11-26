#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Original procedural rigid-part visual study for testing the runtime preview.
    /// No external assets, no skinned animation and no automatic use in player builds.</summary>
    public static class ProfileAvatarPrototype
    {
        public static GameObject Build(string gender, Func<Color, Material> material, bool detailed = false)
        {
            if (gender != CharacterCatalog.Male && gender != CharacterCatalog.Female)
                throw new ArgumentException("Unsupported gender.", nameof(gender));
            if (material == null) throw new ArgumentNullException(nameof(material));
            var root = new GameObject((detailed ? "Character Art 2.0 " : "Prototype ") + gender);
            try
            {
                var rig = root.AddComponent<ProfileAvatarRig>();
                var bindings = new List<ProfileAvatarRig.Binding>();
                var joints = new List<ProfileAvatarMotion.Joint>();
                foreach (string slot in CharacterCatalog.Slots)
                    foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                    {
                        var variant = new GameObject(item.id);
                        variant.transform.SetParent(root.transform, false);
                        BuildVariant(variant.transform, item, gender == CharacterCatalog.Female, material, detailed);
                        AttachJoints(variant.transform, item, gender == CharacterCatalog.Female, joints);
                        if (variant.transform.childCount == 0)
                            throw new InvalidOperationException("Empty prototype variant: " + item.id);
                        bindings.Add(new ProfileAvatarRig.Binding { itemId = item.id, visual = variant });
                    }
                rig.ConfigurePrototype(gender, bindings.ToArray());
                var motion = root.AddComponent<ProfileAvatarMotion>();
                motion.ConfigurePrototype(joints.ToArray());
                if (!motion.IsValid()) throw new InvalidOperationException("Prototype articulation is incomplete.");
                if (!rig.TryApply(ProfileAppearance.FromOutfit(Outfit.DefaultFor(gender)), out var error))
                    throw new InvalidOperationException(error);
                if (rig.MissingCatalogIds().Length != 0)
                    throw new InvalidOperationException("Prototype catalog is incomplete.");
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        // Each variant owns its own rigid pivots. All pivots for a given joint share
        // the same local origin and phase, so sleeves, hands and held props move together.
        static void AttachJoints(Transform variant, CharacterItem item, bool female,
            List<ProfileAvatarMotion.Joint> joints)
        {
            var parts = new List<Transform>();
            foreach (Transform child in variant) parts.Add(child);
            var pivots = new Dictionary<ProfileAvatarJoint, Transform>();
            float shoulder = female ? .45f : .52f;
            foreach (var part in parts)
            {
                float x = part.localPosition.x;
                bool left = x < 0f;
                ProfileAvatarJoint? kind = null;
                if (item.slot == "body")
                {
                    if (part.name == "Hand" || part.name == "Forearm" || part.name == "Thumb")
                        kind = left ? ProfileAvatarJoint.LeftArm : ProfileAvatarJoint.RightArm;
                    else if (part.name != "Neck") kind = ProfileAvatarJoint.Head;
                }
                else if (item.slot == "hair" || item.slot == "hat") kind = ProfileAvatarJoint.Head;
                else if (item.slot == "top" && (part.name == "Sleeve" || part.name == "Sleeve hem"))
                    kind = left ? ProfileAvatarJoint.LeftArm : ProfileAvatarJoint.RightArm;
                else if (item.slot == "shoes" || (item.slot == "bottom" &&
                    (part.name == "Trouser leg" || part.name == "Legging" || part.name == "Knee panel" || part.name == "Cargo pocket" || part.name == "Trouser cuff")))
                    kind = left ? ProfileAvatarJoint.LeftLeg : ProfileAvatarJoint.RightLeg;
                else if (item.slot == "accessory")
                {
                    if (item.id.Contains("glasses")) kind = ProfileAvatarJoint.Head;
                    else if (item.id.Contains("radio") || item.id.Contains("tablet") || item.id.Contains("blueprint"))
                        kind = ProfileAvatarJoint.RightArm;
                }
                if (!kind.HasValue) continue;
                if (!pivots.TryGetValue(kind.Value, out var pivot))
                {
                    pivot = new GameObject("Joint_" + kind.Value).transform;
                    pivot.SetParent(variant, false);
                    switch (kind.Value)
                    {
                        case ProfileAvatarJoint.Head: pivot.localPosition = new Vector3(0,1.43f,0); break;
                        case ProfileAvatarJoint.LeftArm: pivot.localPosition = new Vector3(-shoulder*.5f-.045f,1.33f,0); break;
                        case ProfileAvatarJoint.RightArm: pivot.localPosition = new Vector3(shoulder*.5f+.045f,1.33f,0); break;
                        case ProfileAvatarJoint.LeftLeg: pivot.localPosition = new Vector3(-.12f,.85f,0); break;
                        case ProfileAvatarJoint.RightLeg: pivot.localPosition = new Vector3(.12f,.85f,0); break;
                    }
                    pivots.Add(kind.Value, pivot);
                    joints.Add(new ProfileAvatarMotion.Joint { kind = kind.Value, pivot = pivot, rest = pivot.localRotation });
                }
                part.SetParent(pivot, true);
            }
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

        static void BuildVariant(Transform parent, CharacterItem item, bool female, Func<Color, Material> material, bool detailed)
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
                    Ball("Head",0,1.66f,0,detailed?(female?.355f:.39f):.40f,.44f,.37f,skin);
                    if (detailed)
                    {
                        Ball("Jaw",0,1.565f,.035f,female?.265f:.305f,.22f,.30f,skin);
                        Ball("Chin",0,1.525f,.097f,.17f,.09f,.14f,skin);
                    }
                    Disc("Neck",0,1.43f,0,.16f,.09f,.16f,skin);
                    Ball("Nose",0,1.65f,.181f,.062f,.068f,.055f,skin);
                    if (detailed)
                    {
                        Ball("Upper lip",0,1.565f,.186f,.076f,.017f,.018f,Color.Lerp(skin,Brown,.46f));
                        Ball("Lower lip",0,1.550f,.183f,.070f,.017f,.02f,Color.Lerp(skin,new Color(.72f,.28f,.22f),.30f));
                    }
                    else Box("Mouth",0,1.57f,.17f,.085f,.014f,.02f,Brown);
                    foreach (int side in new[] {-1,1})
                    {
                        Ball("Ear",side*.20f,1.65f,0,.07f,.13f,.08f,skin);
                        Ball("Eye",side*.075f,1.70f,.166f,.046f,.050f,.032f,White);
                        Ball("Pupil",side*.075f,1.70f,.180f,.022f,.027f,.018f,Dark);
                        if (detailed)
                        {
                            Ball("Eye glint",side*.075f-.004f,1.707f,.190f,.008f,.009f,.006f,White);
                            Box("Brow",side*.076f,1.746f,.166f,.066f,.019f,.019f,Brown,side*-8f);
                            Ball("Thumb",side*(shoulder*.5f+.045f),.855f,.047f,.055f,.095f,.065f,skin);
                        }
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
                    if (detailed && !Has("curly"))
                    {
                        Ball("Side sweep",-.105f,1.81f,.103f,.21f,.13f,.19f,Color.Lerp(Brown,Gold,.11f));
                        Ball("Temple lock",.151f,1.75f,.038f,.092f,.17f,.20f,Brown);
                    }
                    break;
                case "top":
                    Color shirt = Has("vest") ? Orange : Has("jacket") || Has("blazer") ? Navy :
                        Has("sweater") || Has("cardigan") ? Green : Has("shirt") && !Has("tshirt") || Has("blouse") ? White : Slate;
                    Pill("Torso",0,1.15f,0,shoulder,.30f,.30f,shirt);
                    if (detailed)
                    {
                        Ball("Chest",0,1.26f,0,shoulder*.99f,.29f,.31f,shirt);
                        Box("Waist hem",0,.925f,0,shoulder*.80f,.05f,.24f,Color.Lerp(shirt,Dark,.28f));
                        Disc("Collar",0,1.405f,0,.21f,.028f,.19f,Has("vest")?Navy:Color.Lerp(shirt,Dark,.25f));
                    }
                    foreach (int side in new[] {-1,1})
                    {
                        Pill("Sleeve",side*(shoulder*.5f+.045f),1.24f,0,.175f,.17f,.20f,detailed&&Has("vest")?Navy:shirt,side*8);
                        if (detailed) Box("Sleeve hem",side*(shoulder*.5f+.065f),1.106f,0,.14f,.038f,.17f,Color.Lerp(shirt,Dark,.30f),side*8);
                        if (Has("vest"))
                            Box("Reflective strip",side*.11f,1.18f,.15f,.032f,.30f,.024f,White);
                    }
                    if (detailed)
                    {
                        if (Has("vest"))
                        {
                            Box("Reflective waist",0,1.045f,.148f,shoulder*.82f,.033f,.025f,White);
                            Box("Utility pocket",-.13f,1.12f,.166f,.115f,.10f,.035f,Color.Lerp(Orange,Dark,.19f));
                            Box("Utility pocket flap",-.13f,1.176f,.185f,.13f,.025f,.022f,Orange);
                            Box("Company patch",.13f,1.29f,.167f,.080f,.038f,.022f,Navy);
                            Box("Vest zip",0,1.20f,.164f,.018f,.35f,.024f,Dark);
                        }
                        else if (Has("jacket") || Has("blazer") || Has("shirt") || Has("blouse"))
                            Box("Chest pocket",-.115f,1.24f,.158f,.085f,.09f,.025f,Color.Lerp(shirt,Dark,.18f));
                    }
                    if ((!detailed || !Has("tshirt")) && (Has("shirt") || Has("blouse") || Has("jacket") || Has("blazer") || Has("cardigan")))
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
                    {
                        Pill("Trouser leg",side*.12f,.47f,0,.20f,.32f,.23f,trousers);
                        if (detailed)
                        {
                            Box("Knee panel",side*.12f,.43f,.109f,.145f,.16f,.035f,Color.Lerp(trousers,Dark,.24f));
                            Box("Cargo pocket",side*.205f,.65f,.015f,.075f,.15f,.15f,Color.Lerp(trousers,Dark,.15f));
                            Box("Trouser cuff",side*.12f,.22f,0,.174f,.045f,.19f,Color.Lerp(trousers,Dark,.25f));
                        }
                    }
                    if (detailed && !Has("overalls"))
                    {
                        Box("Belt",0,.93f,0,.39f,.055f,.285f,Dark);
                        Box("Buckle",0,.93f,.15f,.067f,.05f,.025f,Gold);
                    }
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
                        if (detailed && !Has("heels"))
                        {
                            Ball("Toe cap",side*.12f,.12f,.165f,.20f,.13f,.13f,Color.Lerp(shoe,Dark,.25f));
                            if (!Has("rubber"))
                                Box("Laces",side*.12f,.211f,.075f,.085f,.018f,.085f,Has("sneakers")?Slate:Gold);
                        }
                    }
                    break;
                case "hat":
                    Color hat = Has("hardhat") ? Orange : Has("cap") ? Denim : Has("sunhat") ? Gold : Dark;
                    if (Has("tophat")) Disc("Tall crown",0,1.96f,0,.33f,.17f,.33f,hat);
                    else Ball("Crown",0,1.84f,0,.45f,.23f,.42f,hat);
                    if (!Has("beret"))
                        Disc("Brim",0,1.78f,Has("cap")?.105f:0f,Has("sunhat")?.70f:.52f,.025f,Has("sunhat")?.65f:.47f,hat);
                    if (Has("hardhat"))
                    {
                        Box("Helmet ridge",0,1.94f,0,.055f,.045f,.32f,hat);
                        if (detailed)
                        {
                            Box("Helmet front plate",0,1.83f,.201f,.112f,.055f,.024f,Navy);
                            foreach(int side in new[]{-1,1})
                                Box("Helmet vent",side*.16f,1.865f,.104f,.055f,.025f,.035f,Color.Lerp(hat,Dark,.25f));
                        }
                    }
                    else if (detailed && !Has("beret"))
                        Disc("Hat band",0,Has("tophat")?1.83f:1.814f,0,Has("tophat")?.337f:.415f,.025f,.38f,Color.Lerp(hat,Dark,.35f));
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
