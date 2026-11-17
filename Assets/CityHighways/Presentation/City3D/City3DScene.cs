using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityHighways.Presentation
{
    /// <summary>Освещение поля по времени суток (из MapView): все величины 0..1, кроме часа и SunX (−1..1).</summary>
    public struct CityLight
    {
        public float Hour, Daylight, Night, Golden, Morning, Twilight, Lamps, SunX;
    }

    /// <summary>
    /// 3D-город под интерфейсом партии. Земля-плита с местностью, река с гранитными набережными и текущей водой,
    /// низкополигональные здания и деревья (по районам, одним мешем на район), объёмные мосты с опорами и арками.
    /// Камера получает ту же перспективу, что и BoardLayout, поэтому дороги, места и клики интерфейса лежат точно
    /// на 3D-земле. Солнце, тени, амбиент, туман и свечение окон идут за временем суток партии.
    /// Рисует отдельная камера с рендерером Universal (второй в ассете URP); если его нет — рендерер создаётся
    /// кодом на время партии. На выходе всё убирается и настройки сцены возвращаются как были.
    /// </summary>
    public sealed class City3DScene : IDisposable
    {
        const string ResourceFolder = "CityHighways/City3D/";
        /// <summary>Высота воды и набережных (метры сцены). Дороги интерфейса лежат на высоте 0.</summary>
        const float WaterLevel = 0.004f, BankHeight = 0.03f, RiverWidth = 0.26f, BankWidth = 0.025f;
        const float DeckTop = 0.05f;

        readonly CityMap _map;
        readonly GameObject _root;
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly Dictionary<string, GameObject> _districts = new Dictionary<string, GameObject>();
        readonly List<(Camera camera, bool enabled)> _otherCameras = new List<(Camera, bool)>();
        Camera _camera;
        Light _sun;
        Bloom _bloom;
        Material _buildings, _ground, _water;
        RenderPipelineAsset _previousPipeline;
        bool _swappedPipeline;
        float _waterOffset;

        // Что было в RenderSettings до партии.
        AmbientMode _ambientMode;
        Color _ambientSky, _ambientEquator, _ambientGround;
        bool _fog;
        FogMode _fogMode;
        Color _fogColor;
        float _fogStart, _fogEnd;

        public int BuildingCount { get; private set; }
        public int TreeCount { get; private set; }

        City3DScene(CityMap map)
        {
            _map = map;
            _root = new GameObject("City Highways 3D");
        }

        /// <summary>Построить город; null — 3D недоступно (нет URP, ошибка) — тогда остаётся прежнее плоское поле.</summary>
        /// <param name="quality">0 — низкое (без теней, меньше зданий), 1 — среднее, 2 — высокое.</param>
        public static City3DScene TryCreate(CityMap map, int quality)
        {
            City3DScene scene = null;
            try
            {
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                    && !(QualitySettings.renderPipeline is UniversalRenderPipelineAsset))
                {
                    Debug.LogWarning("3D-город: проект не на URP — остаётся плоское поле.");
                    return null;
                }
                scene = new City3DScene(map);
                scene.Build(Mathf.Clamp(quality, 0, 2));
                return scene;
            }
            catch (Exception e)
            {
                Debug.LogWarning("3D-город не построился, остаётся плоское поле: " + e);
                scene?.Dispose();
                return null;
            }
        }

        // ================================================================== Сборка

        void Build(int quality)
        {
            float density = quality == 0 ? 0.55f : quality == 1 ? 0.8f : 1f;
            var plan = CityPlanner.Build(_map, density);
            BuildingCount = plan.Buildings.Count;
            TreeCount = plan.Trees.Count;

            SetupRendering(quality);
            CreateMaterials(plan);
            BuildGround();
            BuildRivers();
            BuildDistricts(plan);
            BuildBridges();
        }

        void SetupRendering(int quality)
        {
            // Камера: рендерер Universal (второй в ассете URP), иначе — созданный кодом на время партии.
            var cameraObject = new GameObject("City 3D Camera");
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Theme.Background;
            _camera.depth = 10f;
            _camera.allowHDR = true;
            _camera.allowMSAA = true;
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = quality > 0;
            data.antialiasing = quality > 0 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.renderShadows = quality > 0;
            // Страховка: пиксели NaN/Inf (если где-то всё же проскочит вырожденная нормаль) заменяются чёрными
            // до пост-обработки, а не растекаются по Bloom белыми вспышками.
            data.stopNaN = quality > 0;
            int rendererIndex = FindUniversalRenderer();
            if (rendererIndex >= 0) data.SetRenderer(rendererIndex);
            else SwapToRuntimePipeline();

            // Остальные камеры сцены не нужны: 3D-камера рисует весь экран, интерфейс — поверх.
            foreach (var other in Camera.allCameras)
            {
                if (other == _camera) continue;
                _otherCameras.Add((other, other.enabled));
                other.enabled = false;
            }

            var sunObject = new GameObject("City 3D Sun");
            sunObject.transform.SetParent(_root.transform, false);
            _sun = sunObject.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = quality == 0 ? LightShadows.None : quality == 1 ? LightShadows.Hard : LightShadows.Soft;
            _sun.shadowStrength = 0.75f;
            _sun.shadowBias = 0.02f;
            _sun.shadowNormalBias = 0.25f;

            _ambientMode = RenderSettings.ambientMode;
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _fogStart = RenderSettings.fogStartDistance;
            _fogEnd = RenderSettings.fogEndDistance;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            if (quality > 0)
            {
                // Пост-обработка: мягкое свечение (окна ночью, подсвеченные дороги), нейтральная тональная кривая, виньетка.
                var volumeObject = new GameObject("City 3D Volume");
                volumeObject.transform.SetParent(_root.transform, false);
                var volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 50f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.DontSave;
                _owned.Add(profile);
                _bloom = profile.Add<Bloom>(true);
                // Clamp режет «светлячки»: один пересвеченный пиксель (блик солнца на ребре здания или воде)
                // больше не раздувается во вспышку при движении камеры.
                _bloom.threshold.Override(1.1f);
                _bloom.intensity.Override(0.35f);
                _bloom.scatter.Override(0.55f);
                _bloom.clamp.Override(3f);
                _bloom.highQualityFiltering.Override(quality > 1);
                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.18f);
                vignette.smoothness.Override(0.6f);
                volume.sharedProfile = profile;
            }
        }

        /// <summary>Индекс рендерера Universal в текущем ассете URP или −1.</summary>
        static int FindUniversalRenderer()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return -1;
            for (int i = 0; i < 2; i++)
            {
                ScriptableRenderer renderer;
                try { renderer = asset.GetRenderer(i); }
                catch { break; }
                if (renderer is UniversalRenderer) return i;
            }
            return -1;
        }

        /// <summary>Запасной путь: ассет URP с рендерером Universal, созданный кодом, — только на время партии.</summary>
        void SwapToRuntimePipeline()
        {
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.hideFlags = HideFlags.DontSave;
            var asset = UniversalRenderPipelineAsset.Create(rendererData);
            asset.hideFlags = HideFlags.DontSave;
            asset.shadowDistance = 60f;
            _owned.Add(rendererData);
            _owned.Add(asset);
            _previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = asset;
            _swappedPipeline = true;
            Debug.Log("3D-город: рендерер Universal не найден в ассете URP — создан на время партии.");
        }

        Material LoadMaterial(string name, Color fallbackColor)
        {
            var source = Resources.Load<Material>(ResourceFolder + name);
            Material material = null;
            if (source != null && source.shader != null && source.shader.isSupported && !source.shader.name.Contains("InternalError"))
                material = new Material(source);
            if (material == null)
            {
                var lit = UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.defaultMaterial : null;
                material = lit != null ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            material.name = "City3D " + name;
            material.hideFlags = HideFlags.DontSave;
            material.SetColor("_BaseColor", fallbackColor);
            _owned.Add(material);
            return material;
        }

        void CreateMaterials(CityPlanner.Plan plan)
        {
            _buildings = LoadMaterial("CityBuildings", Color.white);
            _buildings.SetTexture("_BaseMap", CityAtlas.Albedo);
            _buildings.SetTexture("_EmissionMap", CityAtlas.Emission);
            _buildings.SetFloat("_Smoothness", 0.18f);
            NoSpecular(_buildings);
            _buildings.EnableKeyword("_EMISSION");
            _buildings.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _buildings.SetColor("_EmissionColor", Color.black);

            _ground = LoadMaterial("CityGround", Color.white);
            _ground.SetTexture("_BaseMap", TerrainTexture.ForCity3D(_map, plan.Footprints));
            _ground.SetFloat("_Smoothness", 0.04f);
            NoSpecular(_ground);

            _water = LoadMaterial("CityWater", new Color(0.55f, 0.74f, 0.88f));
            _water.SetTexture("_BaseMap", RoadTextures.Water);
            _water.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            _water.SetFloat("_Smoothness", 0.72f);
        }

        /// <summary>
        /// Матовая поверхность: без бликов солнца и отражений неба. У низкополигональных граней блик
        /// мерцает на рёбрах при зуме, а на атласе с окнами смотрится как шум.
        /// </summary>
        static void NoSpecular(Material material)
        {
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        GameObject AddMesh(string name, Mesh mesh, Material material, Transform parent, bool castShadows)
        {
            _owned.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return go;
        }

        /// <summary>Земля — плита с местностью сверху и тёмными торцами (поле лежит на «столе», как раньше).</summary>
        void BuildGround()
        {
            const float m = TerrainTexture.Margin, span = 1f + 2f * m;
            var top = new MeshBuilder();
            const int cells = 8; // несколько клеток — чтобы тени и туман ложились ровнее
            for (int iy = 0; iy < cells; iy++)
            for (int ix = 0; ix < cells; ix++)
            {
                float x0 = -m + span * ix / cells, x1 = -m + span * (ix + 1) / cells;
                float y0 = -m + span * iy / cells, y1 = -m + span * (iy + 1) / cells;
                var a = BoardLayout.World(x0, y1); var b = BoardLayout.World(x0, y0);
                var c = BoardLayout.World(x1, y0); var d = BoardLayout.World(x1, y1);
                var uv = new Rect((x0 + m) / span, 1f - (y1 + m) / span, (x1 - x0) / span, (y1 - y0) / span);
                top.Quad(a, b, c, d, uv);
            }
            AddMesh("Ground", top.ToMesh("City3D Ground"), _ground, _root.transform, false);

            // Торцы плиты.
            var sides = new MeshBuilder();
            var edge = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Asphalt);
            var nw = BoardLayout.World(-m, -m); var ne = BoardLayout.World(1f + m, -m);
            var sw = BoardLayout.World(-m, 1f + m); var se = BoardLayout.World(1f + m, 1f + m);
            var down = Vector3.down * 0.35f;
            sides.Quad(sw + down, sw, se, se + down, edge);
            sides.Quad(se + down, se, ne, ne + down, edge);
            sides.Quad(ne + down, ne, nw, nw + down, edge);
            sides.Quad(nw + down, nw, sw, sw + down, edge);
            AddMesh("Board edge", sides.ToMesh("City3D Board Edge"), _buildings, _root.transform, false);
        }

        /// <summary>Реки: вода (текстура течёт вдоль русла) и гранитные набережные с двух сторон.</summary>
        void BuildRivers()
        {
            var water = new MeshBuilder();
            var banks = new MeshBuilder();
            var granite = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Granite);
            foreach (var line in _map.Rivers)
            {
                if (line.Count < 2) continue;
                var raw = line.Select(p => BoardLayout.World(p.X, p.Y)).ToList();
                // Продлеваем концы за край карты, чтобы вода не обрывалась на кромке поля.
                raw.Insert(0, raw[0] + (raw[0] - raw[1]).normalized * 1.6f);
                raw.Add(raw[raw.Count - 1] + (raw[raw.Count - 1] - raw[raw.Count - 2]).normalized * 1.6f);
                var points = Smooth(raw, 5);
                float along = 0f;
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var na = Side(points, i);
                    var nb = Side(points, i + 1);
                    float length = Vector3.Distance(a, b);
                    float u0 = along / 0.9f, u1 = (along + length) / 0.9f;
                    along += length;
                    float half = RiverWidth / 2f;
                    var wl = Vector3.up * WaterLevel;
                    // Вода: левый и правый край.
                    water.Quad(a + na * half + wl, a - na * half + wl, b - nb * half + wl, b + nb * half + wl, new Rect(u0, 0f, u1 - u0, 1f));
                    // Набережные: верх, стенка к воде, стенка к земле — на каждом берегу.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var ia = a + na * half * s; var ib = b + nb * half * s;
                        var oa = a + na * (half + BankWidth) * s; var ob = b + nb * (half + BankWidth) * s;
                        var up = Vector3.up * BankHeight;
                        // Порядок вершин подобран так, чтобы все грани смотрели наружу (верх — вверх, стенки — от камня).
                        if (s > 0)
                        {
                            banks.Quad(oa + up, ia + up, ib + up, ob + up, granite);
                            banks.Quad(ib + wl, ib + up, ia + up, ia + wl, granite);
                            banks.Quad(oa, oa + up, ob + up, ob, granite);
                        }
                        else
                        {
                            banks.Quad(ia + up, oa + up, ob + up, ib + up, granite);
                            banks.Quad(ia + wl, ia + up, ib + up, ib + wl, granite);
                            banks.Quad(ob, ob + up, oa + up, oa, granite);
                        }
                    }
                }
            }
            AddMesh("Water", water.ToMesh("City3D Water"), _water, _root.transform, false);
            AddMesh("Embankments", banks.ToMesh("City3D Embankments"), _buildings, _root.transform, true);
        }

        /// <summary>Перпендикуляр к руслу в точке i (по горизонтали), направлен «вправо» по течению.</summary>
        static Vector3 Side(List<Vector3> points, int i)
        {
            var prev = points[Mathf.Max(0, i - 1)];
            var next = points[Mathf.Min(points.Count - 1, i + 1)];
            var tangent = (next - prev);
            tangent.y = 0f;
            tangent.Normalize();
            return new Vector3(tangent.z, 0f, -tangent.x);
        }

        /// <summary>Сглаживание ломаной сплайном Катмулла — Рома.</summary>
        static List<Vector3> Smooth(List<Vector3> points, int steps)
        {
            var result = new List<Vector3>();
            for (int i = 0; i + 1 < points.Count; i++)
            {
                var p0 = points[Mathf.Max(0, i - 1)]; var p1 = points[i];
                var p2 = points[i + 1]; var p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        /// <summary>Здания и деревья: один меш на район (районы-расширения прячутся, пока закрыты).</summary>
        void BuildDistricts(CityPlanner.Plan plan)
        {
            var builders = new Dictionary<string, MeshBuilder>();
            MeshBuilder For(string district)
            {
                string key = district ?? "_";
                if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                return b;
            }
            var local = new MeshBuilder();
            foreach (var building in plan.Buildings)
            {
                local.Clear();
                var random = new System.Random(building.Seed);
                CityShapes.Build(local, building.Kind, building.Width / CityPlanner.PixelsPerUnit, building.Depth / CityPlanner.PixelsPerUnit,
                    building.Height, random);
                For(building.District).Append(local, Matrix4x4.TRS(CityPlanner.ToWorld(building.Center), Quaternion.Euler(0f, building.Angle, 0f), Vector3.one));
            }
            foreach (var tree in plan.Trees)
            {
                var random = new System.Random(tree.Seed);
                CityShapes.BuildTree(For(tree.District), tree.Kind, CityPlanner.ToWorld(tree.At), tree.Size, random, false);
            }
            foreach (var pair in builders)
            {
                if (pair.Value.VertexCount == 0) continue;
                var go = AddMesh("District " + pair.Key, pair.Value.ToMesh("City3D " + pair.Key), _buildings, _root.transform, true);
                _districts[pair.Key] = go;
            }
        }

        /// <summary>
        /// Мосты: полотно, перила и опоры в воде; у длинных мостов — две арки с подвесками.
        /// Дорога интерфейса ложится прямо на полотно.
        /// </summary>
        void BuildBridges()
        {
            var deckSide = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeConcrete);
            var deckTop = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Asphalt);
            var steel = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
            var rail = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            var builders = new Dictionary<string, MeshBuilder>();
            foreach (var road in _map.Roads.Where(r => r.IsBridge))
            {
                var la = _map.GetLocation(road.A);
                var lb = _map.GetLocation(road.B);
                if (la == null || lb == null) continue;
                // Мост к району-расширению прячется вместе с ним.
                string district = _map.GetDistrict(la.DistrictId)?.IsExpansion == true ? la.DistrictId
                    : _map.GetDistrict(lb.DistrictId)?.IsExpansion == true ? lb.DistrictId : "_bridges";
                if (!builders.TryGetValue(district, out var b)) builders[district] = b = new MeshBuilder();

                var a = BoardLayout.World(la.X, la.Y);
                var e = BoardLayout.World(lb.X, lb.Y);
                var dir = (e - a).normalized;
                a += dir * 0.13f;
                e -= dir * 0.13f;
                float length = Vector3.Distance(a, e);
                var side = new Vector3(dir.z, 0f, -dir.x);
                const float width = 0.17f, thickness = 0.03f;
                var deckCenter = Vector3.up * (DeckTop - thickness / 2f);
                b.Beam(a + deckCenter, e + deckCenter, width, thickness, deckSide, deckTop);
                for (int s = -1; s <= 1; s += 2)
                {
                    var off = side * (width / 2f - 0.006f) * s + Vector3.up * (DeckTop + 0.01f);
                    b.Beam(a + off, e + off, 0.008f, 0.02f, rail, rail);
                }
                // Опоры: через ~0.4 м, от воды до полотна.
                int piers = Mathf.Max(1, Mathf.FloorToInt(length / 0.4f));
                for (int i = 1; i <= piers; i++)
                {
                    var at = Vector3.Lerp(a, e, i / (float)(piers + 1));
                    b.Prism(at.x, at.z, 0.028f, 0f, DeckTop - thickness, 6, deckSide, deckSide);
                }
                // Арки у длинных мостов.
                if (length > 1.0f)
                {
                    const int segments = 10;
                    float rise = Mathf.Min(0.16f, length * 0.12f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var off = side * (width / 2f) * s;
                        Vector3 Arc(float t) => Vector3.Lerp(a, e, t) + off + Vector3.up * (DeckTop + Mathf.Sin(t * Mathf.PI) * rise);
                        for (int i = 0; i < segments; i++)
                        {
                            b.Beam(Arc(i / (float)segments), Arc((i + 1) / (float)segments), 0.014f, 0.014f, steel, steel);
                            if (i > 0)
                            {
                                var hangerTop = Arc(i / (float)segments);
                                b.Beam(new Vector3(hangerTop.x, DeckTop, hangerTop.z), hangerTop, 0.004f, 0.004f, steel, steel);
                            }
                        }
                    }
                }
            }
            foreach (var pair in builders)
            {
                var go = AddMesh("Bridges " + pair.Key, pair.Value.ToMesh("City3D Bridges " + pair.Key), _buildings, _root.transform, true);
                if (pair.Key != "_bridges") _districts[pair.Key + "#bridges"] = go;
            }
        }

        // ================================================================== Каждый кадр

        /// <summary>
        /// Подогнать камеру под поле, свет — под время суток, показать открытые районы, сдвинуть воду.
        /// panelSize — размер панели интерфейса, offset — левый верхний угол поля на панели (оба в пикселях панели).
        /// </summary>
        public void Sync(BoardLayout layout, Vector2 panelSize, Vector2 offset, CityLight light, Func<string, bool> districtOpen, float dt)
        {
            if (_camera == null || layout == null || !layout.Perspective) return;
            _camera.transform.SetPositionAndRotation(layout.CameraPosition, layout.CameraRotation);
            _camera.nearClipPlane = layout.CameraNear;
            _camera.farClipPlane = layout.CameraFar;
            _camera.fieldOfView = BoardLayout.FieldOfView;
            _camera.projectionMatrix = layout.CameraProjection(panelSize, offset);

            UpdateLight(light, layout.CameraPosition.magnitude);

            foreach (var pair in _districts)
            {
                string id = pair.Key.EndsWith("#bridges") ? pair.Key.Substring(0, pair.Key.Length - 8) : pair.Key;
                bool open = id == "_" || districtOpen == null || districtOpen(id);
                if (pair.Value.activeSelf != open) pair.Value.SetActive(open);
            }

            _waterOffset = (_waterOffset + dt * 0.06f) % 1f;
            _water.SetTextureOffset("_BaseMap", new Vector2(-_waterOffset, 0f));
        }

        void UpdateLight(CityLight l, float cameraDistance)
        {
            float phase = Mathf.PI * (l.Hour - 5f) / 16f;
            float altitude = Mathf.Sin(phase);
            // Солнце: утром с востока, днём с юга (со стороны зрителя — фасады освещены), вечером с запада.
            var toSun = new Vector3(l.SunX, Mathf.Max(0.18f, altitude) * 1.4f, -0.35f - 0.4f * (1f - Mathf.Abs(l.SunX))).normalized;
            var toMoon = new Vector3(-0.45f, 0.9f, 0.35f).normalized;
            var direction = Vector3.Slerp(toSun, toMoon, l.Night);
            _sun.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);

            var day = new Color(1f, 0.96f, 0.90f);
            var golden = new Color(1f, 0.64f, 0.36f);
            var morning = new Color(1f, 0.80f, 0.74f);
            var moon = new Color(0.55f, 0.65f, 1f);
            var sunColor = Color.Lerp(Color.Lerp(day, golden, l.Golden), morning, l.Morning * 0.6f);
            _sun.color = Color.Lerp(sunColor, moon, l.Night);
            _sun.intensity = Mathf.Lerp(1.25f * Mathf.Max(0.25f, l.Daylight), 0.22f, l.Night);
            _sun.shadowStrength = Mathf.Lerp(0.78f, 0.35f, l.Night);

            var skyDay = new Color(0.58f, 0.66f, 0.78f);
            var skyGolden = new Color(0.70f, 0.56f, 0.48f);
            var skyNight = new Color(0.07f, 0.09f, 0.18f);
            var sky = Color.Lerp(Color.Lerp(skyDay, skyGolden, l.Golden + l.Twilight * 0.6f), skyNight, l.Night);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, new Color(0.42f, 0.42f, 0.40f), 0.4f) * Mathf.Lerp(1f, 0.6f, l.Night);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.24f, 0.26f, 0.20f), new Color(0.04f, 0.05f, 0.08f), l.Night);

            // Дымка вдали: на наклонном поле дальний край чуть растворяется.
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(new Color(0.70f, 0.76f, 0.84f), new Color(0.80f, 0.62f, 0.50f), l.Golden), new Color(0.05f, 0.06f, 0.11f), l.Night);
            RenderSettings.fogStartDistance = cameraDistance * 0.95f;
            RenderSettings.fogEndDistance = cameraDistance * 2.6f;

            // Окна загораются вместе с фонарями.
            _buildings.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.62f) * (l.Lamps * 1.8f));
            if (_bloom != null) _bloom.intensity.Override(Mathf.Lerp(0.3f, 1.1f, l.Lamps));
        }

        // ================================================================== Уборка

        public void Dispose()
        {
            foreach (var (camera, enabled) in _otherCameras)
                if (camera != null) camera.enabled = enabled;
            _otherCameras.Clear();
            if (_swappedPipeline)
            {
                QualitySettings.renderPipeline = _previousPipeline;
                _swappedPipeline = false;
            }
            if (_sun != null)
            {
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientSkyColor = _ambientSky;
                RenderSettings.ambientEquatorColor = _ambientEquator;
                RenderSettings.ambientGroundColor = _ambientGround;
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
            }
            if (_root != null) Destroy(_root);
            foreach (var item in _owned) if (item != null) Destroy(item);
            _owned.Clear();
            _districts.Clear();
            _camera = null;
            _sun = null;
        }

        static void Destroy(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
