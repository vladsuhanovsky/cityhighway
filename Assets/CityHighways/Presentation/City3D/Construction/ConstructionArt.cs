using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Низкополигональные модели стройки, собранные кодом: рабочий в оранжевом жилете и каске, жёлтый
    /// экскаватор (гусеницы, башня, стрела → рукоять → ковш), каток (валец, кабина-навес, задние колёса) и
    /// облачко пыли. Цвета — из маленькой палитры 4 × 4 (каждая грань берёт один тексель), материал — Lit.
    /// Это запасные «префабы»: если в RoadConstructionSet заданы свои, используются они.
    /// </summary>
    public static class ConstructionArt
    {
        const int Vest = 0, Reflective = 1, Trousers = 2, Skin = 3, Helmet = 4, MachineYellow = 5, Dark = 6, Glass = 7,
            Steel = 8, Rubber = 9, DustColor = 10, Red = 11;

        static readonly Color[] Colors =
        {
            new Color(1.00f, 0.45f, 0.08f), new Color(0.92f, 0.92f, 0.85f), new Color(0.16f, 0.20f, 0.32f), new Color(0.90f, 0.70f, 0.55f),
            new Color(0.98f, 0.82f, 0.15f), new Color(0.96f, 0.72f, 0.10f), new Color(0.18f, 0.18f, 0.20f), new Color(0.30f, 0.40f, 0.48f),
            new Color(0.55f, 0.56f, 0.58f), new Color(0.08f, 0.08f, 0.09f), new Color(0.62f, 0.54f, 0.42f), new Color(0.80f, 0.20f, 0.15f),
            Color.white, Color.white, Color.white, Color.white
        };

        static Texture2D _palette;

        /// <summary>Палитра 4 × 4 без сглаживания: грань модели берёт ровно один цвет.</summary>
        public static Texture2D Palette
        {
            get
            {
                if (_palette != null) return _palette;
                _palette = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "City3D Construction Palette",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                _palette.SetPixels(Colors);
                _palette.Apply(false, false);
                return _palette;
            }
        }

        static Rect C(int index) => new Rect((index % 4 + 0.5f) / 4f, (index / 4 + 0.5f) / 4f, 0f, 0f);

        /// <summary>Готовые шаблоны (неактивный родитель, сами шаблоны активны — их копии появляются включёнными).</summary>
        public struct Templates
        {
            public GameObject Worker, Excavator, Roller, Dust;
        }

        /// <param name="owned">Сюда складываются созданные меши — их уничтожит владелец сцены.</param>
        public static Templates Build(Transform parent, Material material, List<Object> owned)
        {
            return new Templates
            {
                Worker = BuildWorker(parent, material, owned),
                Excavator = BuildExcavator(parent, material, owned),
                Roller = BuildRoller(parent, material, owned),
                Dust = BuildDust(parent, material, owned)
            };
        }

        static Transform Part(Transform parent, string name, Vector3 localPosition, MeshBuilder mesh, Material material, List<Object> owned)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (mesh != null && mesh.VertexCount > 0)
            {
                var m = mesh.ToMesh("City3D " + name);
                owned.Add(m);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            return go.transform;
        }

        /// <summary>Цилиндр вдоль оси X (валец, колесо): призма по Y, повёрнутая на 90° вокруг Z.</summary>
        static void CylinderX(MeshBuilder b, Vector3 center, float radius, float width, int sides, Rect uv)
        {
            var local = new MeshBuilder();
            local.Prism(0f, 0f, radius, -width / 2f, width, sides, uv, uv);
            b.Append(local, Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, 90f), Vector3.one));
        }

        // ------------------------------------------------------------------ Рабочий (рост ≈ 0,03)

        static GameObject BuildWorker(Transform parent, Material material, List<Object> owned)
        {
            var root = new GameObject("Worker");
            root.transform.SetParent(parent, false);
            var b = new MeshBuilder();
            b.Box(-0.0022f, 0f, 0.0034f, 0.004f, 0f, 0.012f, C(Trousers), C(Trousers), true);   // ноги
            b.Box(0.0022f, 0f, 0.0034f, 0.004f, 0f, 0.012f, C(Trousers), C(Trousers), true);
            b.Box(0f, 0f, 0.010f, 0.0065f, 0.012f, 0.0105f, C(Vest), C(Vest));                 // жилет
            b.Box(0f, 0f, 0.0104f, 0.0069f, 0.0165f, 0.0015f, C(Reflective), C(Reflective));   // светоотражающая полоса
            b.Box(-0.0062f, 0f, 0.0024f, 0.0034f, 0.0125f, 0.009f, C(Vest), C(Vest), true);   // руки
            b.Box(0.0062f, 0f, 0.0024f, 0.0034f, 0.0125f, 0.009f, C(Vest), C(Vest), true);
            b.Box(0f, 0f, 0.006f, 0.006f, 0.0225f, 0.0055f, C(Skin), C(Skin));                  // голова
            b.Box(0f, 0.0006f, 0.0074f, 0.0078f, 0.0275f, 0.0025f, C(Helmet), C(Helmet), true); // каска
            var body = Part(root.transform, "Body", Vector3.zero, b, material, owned);
            root.AddComponent<ConstructionWorker>().body = body;
            return root;
        }

        // ------------------------------------------------------------------ Экскаватор (длина ≈ 0,07)

        static GameObject BuildExcavator(Transform parent, Material material, List<Object> owned)
        {
            var root = new GameObject("Excavator");
            root.transform.SetParent(parent, false);

            var tracks = new MeshBuilder();
            tracks.Box(-0.016f, 0f, 0.011f, 0.066f, 0f, 0.011f, C(Dark), C(Rubber), true);
            tracks.Box(0.016f, 0f, 0.011f, 0.066f, 0f, 0.011f, C(Dark), C(Rubber), true);
            tracks.Box(0f, 0f, 0.022f, 0.04f, 0.004f, 0.008f, C(Dark), C(Dark));
            Part(root.transform, "Tracks", Vector3.zero, tracks, material, owned);

            var house = new MeshBuilder();
            house.Box(0f, -0.004f, 0.040f, 0.042f, 0f, 0.018f, C(MachineYellow), C(MachineYellow), true);
            house.Box(-0.010f, 0.010f, 0.016f, 0.018f, 0.018f, 0.017f, C(Glass), C(MachineYellow));     // кабина
            house.Box(0.008f, -0.012f, 0.016f, 0.016f, 0.018f, 0.006f, C(MachineYellow), C(Dark));      // капот двигателя
            house.Box(0f, -0.023f, 0.038f, 0.010f, 0.002f, 0.014f, C(Dark), C(Dark));                   // противовес
            var turret = Part(root.transform, "Turret", new Vector3(0f, 0.011f, 0f), house, material, owned);

            var boomMesh = new MeshBuilder();
            boomMesh.Box(0f, 0.023f, 0.007f, 0.046f, -0.0035f, 0.007f, C(MachineYellow), C(MachineYellow), true);
            var boom = Part(turret, "Boom", new Vector3(0.008f, 0.016f, 0.016f), boomMesh, material, owned);

            var stickMesh = new MeshBuilder();
            stickMesh.Box(0f, 0.016f, 0.006f, 0.032f, -0.003f, 0.006f, C(MachineYellow), C(MachineYellow), true);
            var stick = Part(boom, "Stick", new Vector3(0f, 0f, 0.046f), stickMesh, material, owned);

            var bucketMesh = new MeshBuilder();
            bucketMesh.Box(0f, 0.006f, 0.013f, 0.012f, -0.008f, 0.010f, C(Dark), C(Steel), true);
            var bucket = Part(stick, "Bucket", new Vector3(0f, 0f, 0.032f), bucketMesh, material, owned);
            var tip = Part(bucket, "Tip", new Vector3(0f, -0.006f, 0.012f), null, material, owned);

            var machine = root.AddComponent<ConstructionMachine>();
            machine.turret = turret;
            machine.boom = boom;
            machine.stick = stick;
            machine.bucket = bucket;
            machine.bucketTip = tip;
            return root;
        }

        // ------------------------------------------------------------------ Каток (длина ≈ 0,06)

        static GameObject BuildRoller(Transform parent, Material material, List<Object> owned)
        {
            var root = new GameObject("Roller");
            root.transform.SetParent(parent, false);

            var body = new MeshBuilder();
            body.Box(0f, -0.010f, 0.030f, 0.032f, 0.008f, 0.016f, C(MachineYellow), C(MachineYellow), true); // корпус
            body.Box(0f, 0.016f, 0.036f, 0.006f, 0.016f, 0.006f, C(MachineYellow), C(MachineYellow), true);  // рама вальца
            body.Box(0f, -0.006f, 0.010f, 0.008f, 0.024f, 0.006f, C(Dark), C(Dark));                          // сиденье
            foreach (var x in new[] { -0.013f, 0.013f })
            foreach (var z in new[] { -0.019f, 0.003f })
                body.Box(x, z, 0.002f, 0.002f, 0.024f, 0.014f, C(Dark), C(Dark));                             // стойки навеса
            body.Box(0f, -0.008f, 0.030f, 0.026f, 0.038f, 0.002f, C(Dark), C(Red), true);                     // крыша
            CylinderX(body, new Vector3(-0.016f, 0.008f, -0.018f), 0.008f, 0.006f, 8, C(Rubber));              // задние колёса
            CylinderX(body, new Vector3(0.016f, 0.008f, -0.018f), 0.008f, 0.006f, 8, C(Rubber));
            var chassis = Part(root.transform, "Chassis", Vector3.zero, body, material, owned);

            var drumMesh = new MeshBuilder();
            CylinderX(drumMesh, Vector3.zero, 0.012f, 0.034f, 10, C(Steel));
            var drum = Part(chassis, "Drum", new Vector3(0f, 0.012f, 0.020f), drumMesh, material, owned);

            var machine = root.AddComponent<ConstructionMachine>();
            machine.drum = drum;
            machine.drumRadius = 0.012f;
            machine.chassis = chassis;
            return root;
        }

        // ------------------------------------------------------------------ Пыль

        static GameObject BuildDust(Transform parent, Material material, List<Object> owned)
        {
            var root = new GameObject("Dust");
            root.transform.SetParent(parent, false);
            var b = new MeshBuilder();
            b.Blob(new Vector3(0f, 0.004f, 0f), 0.008f, 0.006f, C(DustColor));
            b.Blob(new Vector3(0.005f, 0.003f, 0.002f), 0.005f, 0.004f, C(DustColor));
            Part(root.transform, "Puff", Vector3.zero, b, material, owned);
            return root;
        }
    }
}
