using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityHighways.Presentation
{
    /// <summary>
    /// 3D-модели из Scenario (Tripo P2 → Tripo Retopology v3: чистая сетка, 2–4 тыс. граней, развёртка UV, текстура
    /// запечена): фура, башенный кран и три терминала хабов. Файлы — Resources/CityHighways/Models3D/&lt;id&gt;.glb
    /// (их импортирует glTFast как префаб). Скачивает их меню «City Highways → Скачать 3D-модели из Scenario».
    /// Нет файла — сцена строит прежние процедурные здания, ничего не ломается.
    ///
    /// Масштаб: модели из Scenario нормированы (длинная сторона ≈ 1), ось и центр у каждой свои. Instantiate
    /// приводит любую модель к нужному размеру в метрах сцены и ставит её основанием на землю, центром в точку
    /// (по габаритам рендереров), поэтому ни масштаб, ни опорная точка файла не важны.
    /// </summary>
    public static class Models3D
    {
        public const string Folder = "CityHighways/Models3D/";
        public const string Truck = "truck", TowerCrane = "tower_crane",
            AirportTerminal = "terminal_airport", StationTerminal = "terminal_station", PortTerminal = "terminal_port";

        /// <summary>Все модели — для загрузчика и проверки.</summary>
        public static readonly string[] All = { Truck, TowerCrane, AirportTerminal, StationTerminal, PortTerminal };

        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        /// <summary>Префаб модели (кэш, включая отсутствие); null — файла нет.</summary>
        public static GameObject Prefab(string id)
        {
            if (!Prefabs.TryGetValue(id, out var prefab))
            {
                prefab = Resources.Load<GameObject>(Folder + id);
                Prefabs[id] = prefab;
            }
            return prefab;
        }

        public static bool Has(string id) => Prefab(id) != null;

        /// <summary>
        /// Поставить модель: основание — на высоте y точки at, центр габаритов — в точку at, поворот yaw (градусы).
        /// size — длина самой длинной горизонтальной стороны в метрах сцены (или высота, если byHeight).
        /// Возвращает обёртку (её можно двигать и удалять) или null, если модели нет.
        /// </summary>
        public static GameObject Instantiate(string id, Transform parent, Vector3 at, float yaw, float size, bool byHeight = false)
        {
            var prefab = Prefab(id);
            if (prefab == null) return null;
            var wrapper = new GameObject("Model " + id);
            var model = Object.Instantiate(prefab, wrapper.transform, false);
            model.name = id;

            // Габариты в системе обёртки (она пока в нуле, без поворота и масштаба).
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.Destroy(wrapper);
                return null;
            }
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float measure = byHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
            float scale = measure > 1e-5f ? size / measure : 1f;
            // Центр по горизонтали и низ — в начало координат обёртки.
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            wrapper.transform.localScale = Vector3.one * scale;
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = at;
            wrapper.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return wrapper;
        }

        /// <summary>Длинная горизонтальная ось модели — X (а не Z)? Нужна, чтобы развернуть фуру вдоль дороги.</summary>
        public static bool LongAlongX(string id)
        {
            var prefab = Prefab(id);
            if (prefab == null) return false;
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                Mesh mesh = null;
                if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                else if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                if (mesh == null) continue;
                var b = mesh.bounds;
                var m = renderer.transform.localToWorldMatrix;
                var c = m.MultiplyPoint3x4(b.center);
                var e = new Vector3(
                    Mathf.Abs(m.m00) * b.extents.x + Mathf.Abs(m.m01) * b.extents.y + Mathf.Abs(m.m02) * b.extents.z,
                    Mathf.Abs(m.m10) * b.extents.x + Mathf.Abs(m.m11) * b.extents.y + Mathf.Abs(m.m12) * b.extents.z,
                    Mathf.Abs(m.m20) * b.extents.x + Mathf.Abs(m.m21) * b.extents.y + Mathf.Abs(m.m22) * b.extents.z);
                var world = new Bounds(c, e * 2f);
                if (first) { bounds = world; first = false; }
                else bounds.Encapsulate(world);
            }
            return bounds.size.x > bounds.size.z;
        }
    }
}
