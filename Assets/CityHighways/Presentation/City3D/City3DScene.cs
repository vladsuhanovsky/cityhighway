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
        /// <summary>Декали лежат чуть выше земли (0) и ниже воды (WaterLevel).</summary>
        const float DecalHeight = 0.002f;
        /// <summary>Слой Unity «Water» (встроенный, номер 4): вода не отражает сама себя.</summary>
        const int WaterLayer = 4;
        /// <summary>Сторона клетки для деревьев (м): клетка целиком переключается между объёмными деревьями и карточками.</summary>
        const float TreeChunkSize = 1.2f;
        const string ReflectiveShaderName = "CityHighways/City3D Water";

        /// <summary>Деревья одной клетки одного района: объёмный меш и меш карточек (для дали).</summary>
        sealed class TreeChunk
        {
            public string District;
            public Vector3 Center;
            public GameObject Root, Full, Cards;
            public Mesh CardMesh;
            public bool ShowCards;
            public readonly List<CardSpot> Spots = new List<CardSpot>();
        }

        struct CardSpot
        {
            public Vector3 At;
            public float Size;
            public int Tile;
        }

        readonly CityMap _map;
        readonly GameObject _root;
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly Dictionary<string, GameObject> _districts = new Dictionary<string, GameObject>();
        readonly List<(Camera camera, bool enabled)> _otherCameras = new List<(Camera, bool)>();
        Camera _camera;
        Light _sun;
        Bloom _bloom;
        Material _buildings, _ground, _water, _cards, _decals;
        int _quality, _rendererIndex = -1;
        readonly List<TreeChunk> _treeChunks = new List<TreeChunk>();
        float _cardLean = -999f;
        // Отражения в воде: своя камера рисует зеркальный город в текстуру, шейдер воды берёт из неё картинку.
        bool _reflective;
        Camera _reflectionCamera;
        RenderTexture _reflectionTexture;
        float _reflectionScale = 0.5f;
        Vector4 _waveOffset;
        Color _skyColor = new Color(0.62f, 0.72f, 0.86f);
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

        /// <summary>
        /// Ширина дороги в метрах сцены — общая для дороги интерфейса (11 px поля), 3D-дорог, подъездов и мостов.
        /// MapView рисует дорогу на экране ровно такой ширины при любом зуме.
        /// </summary>
        public const float RoadWidth = BoardLayout.RoadThickness / CityPlanner.PixelsPerUnit;

        RoadBuilder _roads;
        RoadPrefabSet _roadSet;
        readonly List<List<Vector3>> _riverLines = new List<List<Vector3>>();
        readonly Dictionary<string, BuiltRoad> _builtRoads = new Dictionary<string, BuiltRoad>();
        bool _roadsSynced;
        RoadConstructionPlayer _construction;
        readonly HashSet<string> _constructing = new HashSet<string>();

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

            _quality = quality;
            SetupRendering(quality);
            CreateMaterials(plan);
            BuildGround();
            BuildDecals(plan);
            BuildRivers();
            BuildDistricts(plan);
            BuildTrees(plan);
            SetupRoads();
            BuildBridges();
            SetupReflections(quality);
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
            _rendererIndex = rendererIndex;
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
            // Неровности земли: крупные (холмы, пригорки парков) на всю доску и мелкая деталь,
            // повторяющаяся по полю, — шероховатость и пятна (множитель ×2: серый 0,5 цвет не меняет).
            _ground.SetTexture("_BumpMap", CityTextures.GroundNormal(_map));
            _ground.SetFloat("_BumpScale", 0.8f);
            _ground.EnableKeyword("_NORMALMAP");
            _ground.SetTexture("_DetailAlbedoMap", CityTextures.DetailAlbedo);
            _ground.SetTexture("_DetailNormalMap", CityTextures.DetailNormal);
            _ground.SetFloat("_DetailAlbedoMapScale", 1f);
            _ground.SetFloat("_DetailNormalMapScale", 0.6f);
            const float detailTiles = 40f;
            _ground.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTiles, detailTiles * BoardLayout.WorldDepth / BoardLayout.WorldWidth));
            _ground.EnableKeyword("_DETAIL_MULX2");

            // Карточки дальних деревьев: альфа-срез, обе стороны, без бликов.
            _cards = LoadMaterial("CityTreeCards", Color.white);
            _cards.SetTexture("_BaseMap", CityTextures.TreeCards);
            _cards.SetFloat("_AlphaClip", 1f);
            _cards.SetFloat("_Cutoff", 0.45f);
            _cards.EnableKeyword("_ALPHATEST_ON");
            _cards.SetFloat("_Cull", 0f);
            _cards.SetOverrideTag("RenderType", "TransparentCutout");
            _cards.renderQueue = (int)RenderQueue.AlphaTest;
            _cards.SetFloat("_Smoothness", 0.05f);
            NoSpecular(_cards);

            // Декали: полупрозрачные пятна на земле.
            _decals = LoadMaterial("CityDecals", Color.white);
            _decals.SetTexture("_BaseMap", CityTextures.Decals);
            _decals.SetFloat("_Smoothness", 0.05f);
            MakeTransparent(_decals);
            NoSpecular(_decals);

            _water = LoadWater();
        }

        /// <summary>
        /// Вода с настоящими отражениями (свой шейдер); если шейдер недоступен — прежняя вода на Lit
        /// с блеском и течением.
        /// </summary>
        Material LoadWater()
        {
            var source = Resources.Load<Material>(ResourceFolder + "CityWaterReflective");
            if (source != null && source.shader != null && source.shader.isSupported && source.shader.name == ReflectiveShaderName)
            {
                var reflective = new Material(source) { name = "City3D Reflective Water", hideFlags = HideFlags.DontSave };
                _owned.Add(reflective);
                reflective.SetTexture("_BaseMap", RoadTextures.Water);
                reflective.SetTexture("_WaveMap", CityTextures.WaveNormal);
                _reflective = true;
                return reflective;
            }
            Debug.Log("3D-город: шейдер отражающей воды недоступен — вода без отражений.");
            var water = LoadMaterial("CityWater", new Color(0.55f, 0.74f, 0.88f));
            water.SetTexture("_BaseMap", RoadTextures.Water);
            water.SetFloat("_Smoothness", 0.72f);
            return water;
        }

        /// <summary>Полупрозрачный режим материала Lit (как «Surface Type: Transparent, Blend: Alpha» в инспекторе).</summary>
        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", 5f);       // BlendMode.SrcAlpha
            material.SetFloat("_DstBlend", 10f);      // BlendMode.OneMinusSrcAlpha
            material.SetFloat("_SrcBlendAlpha", 1f);  // BlendMode.One
            material.SetFloat("_DstBlendAlpha", 10f);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
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
            var groundMesh = top.ToMesh("City3D Ground");
            groundMesh.RecalculateTangents(); // карте неровностей нужны касательные, иначе освещение ломается
            AddMesh("Ground", groundMesh, _ground, _root.transform, false);

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
                _riverLines.Add(points);
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
            var waterMesh = water.ToMesh("City3D Water");
            var waterObject = AddMesh("Water", waterMesh, _water, _root.transform, false);
            waterObject.layer = WaterLayer;
            // Коллайдер на слое Water — по нему RoadBuilder находит реку лучом (LayerMask «Water»).
            if (waterMesh.vertexCount > 0) waterObject.AddComponent<MeshCollider>().sharedMesh = waterMesh;
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
            foreach (var pair in builders)
            {
                if (pair.Value.VertexCount == 0) continue;
                var go = AddMesh("District " + pair.Key, pair.Value.ToMesh("City3D " + pair.Key), _buildings, _root.transform, true);
                _districts[pair.Key] = go;
            }
        }

        /// <summary>Декали на земле: один меш на район (поля, пятна, площадки — в порядке слоёв из планировщика).</summary>
        void BuildDecals(CityPlanner.Plan plan)
        {
            var builders = new Dictionary<string, MeshBuilder>();
            foreach (var decal in plan.Decals)
            {
                string key = decal.District ?? "_";
                if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                var center = CityPlanner.ToWorld(decal.Center, DecalHeight);
                float hw = decal.Width / CityPlanner.PixelsPerUnit / 2f, hd = decal.Depth / CityPlanner.PixelsPerUnit / 2f;
                var rotation = Quaternion.Euler(0f, decal.Angle, 0f);
                // Порядок углов (юго-запад, северо-запад, северо-восток, юго-восток) — грань смотрит вверх.
                var a = center + rotation * new Vector3(-hw, 0f, -hd);
                var nw = center + rotation * new Vector3(-hw, 0f, hd);
                var ne = center + rotation * new Vector3(hw, 0f, hd);
                var se = center + rotation * new Vector3(hw, 0f, -hd);
                b.Quad(a, nw, ne, se, CityTextures.DecalUv(decal.Kind));
            }
            foreach (var pair in builders)
            {
                var go = AddMesh("Decals " + pair.Key, pair.Value.ToMesh("City3D Decals " + pair.Key), _decals, _root.transform, false);
                if (pair.Key != "_") _districts[pair.Key + "#decals"] = go;
            }
        }

        /// <summary>
        /// Деревья по клеткам: в каждой — объёмные деревья и их карточки. Вблизи видны объёмные,
        /// вдали (мелко на экране) — карточки: одна плоскость на дерево, повёрнутая к камере.
        /// </summary>
        void BuildTrees(CityPlanner.Plan plan)
        {
            var chunks = new Dictionary<string, TreeChunk>();
            var builders = new Dictionary<string, MeshBuilder>();
            foreach (var tree in plan.Trees)
            {
                var at = CityPlanner.ToWorld(tree.At);
                int cx = Mathf.FloorToInt(at.x / TreeChunkSize), cz = Mathf.FloorToInt(at.z / TreeChunkSize);
                string district = tree.District ?? "_";
                string key = district + "#" + cx + "," + cz;
                if (!chunks.TryGetValue(key, out var chunk))
                {
                    chunk = new TreeChunk { District = district, Center = new Vector3((cx + 0.5f) * TreeChunkSize, 0f, (cz + 0.5f) * TreeChunkSize) };
                    chunks[key] = chunk;
                    builders[key] = new MeshBuilder();
                }
                CityShapes.BuildTree(builders[key], tree.Kind, at, tree.Size, new System.Random(tree.Seed), false);
                chunk.Spots.Add(new CardSpot { At = at, Size = tree.Size, Tile = CardTile(tree) });
            }
            foreach (var pair in chunks)
            {
                var chunk = pair.Value;
                chunk.Root = new GameObject("Trees " + pair.Key);
                chunk.Root.transform.SetParent(_root.transform, false);
                chunk.Full = AddMesh("Trees 3D", builders[pair.Key].ToMesh("City3D Trees " + pair.Key), _buildings, chunk.Root.transform, true);
                chunk.CardMesh = new Mesh { name = "City3D Tree Cards " + pair.Key, hideFlags = HideFlags.DontSave };
                chunk.Cards = AddMesh("Tree cards", chunk.CardMesh, _cards, chunk.Root.transform, true);
                chunk.Cards.SetActive(false);
                _treeChunks.Add(chunk);
            }
            RebuildCards(0f);
        }

        /// <summary>Плитка карточки — того же вида и оттенка, что объёмное дерево (тот же порядок случайных чисел, что в BuildTree).</summary>
        static int CardTile(CityPlanner.TreeSpot tree)
        {
            var random = new System.Random(tree.Seed);
            int leaf = random.Next(4);
            switch (tree.Kind)
            {
                case CityShapes.Tree.Spruce: return random.NextDouble() < 0.5 ? CityTextures.CardSpruce : CityTextures.CardSpruceDark;
                case CityShapes.Tree.Poplar: return CityTextures.CardPoplar + leaf;
                default: return CityTextures.CardRound + leaf;
            }
        }

        /// <summary>
        /// Пересобрать карточки под наклон камеры: плоскость перпендикулярна взгляду (сверху — лежит, под углом —
        /// наклонена к зрителю), середина — на половине высоты дерева, низ никогда не уходит под землю.
        /// </summary>
        void RebuildCards(float leanDegrees)
        {
            _cardLean = leanDegrees;
            float t = leanDegrees * Mathf.Deg2Rad;
            var toCamera = new Vector3(0f, Mathf.Cos(t), -Mathf.Sin(t));
            var cardUp = new Vector3(0f, Mathf.Sin(t), Mathf.Cos(t));
            // Нормаль почти вверх — карточка освещается как крона, а не как стена.
            var normal = (Vector3.up + toCamera * 0.4f).normalized;
            var builder = new MeshBuilder();
            foreach (var chunk in _treeChunks)
            {
                builder.Clear();
                foreach (var spot in chunk.Spots)
                {
                    float size = spot.Size * 1.05f, half = size / 2f;
                    var center = spot.At + Vector3.up * half;
                    var right = Vector3.right * half;
                    var up = cardUp * half;
                    builder.QuadWithNormal(center - right - up, center - right + up, center + right + up, center + right - up,
                        CityTextures.CardUv(spot.Tile), normal);
                }
                builder.WriteTo(chunk.CardMesh);
            }
        }

        /// <summary>Камера отражений (качество среднее и высокое): рисует зеркальный город в текстуру для шейдера воды.</summary>
        void SetupReflections(int quality)
        {
            Shader.SetGlobalFloat("_CityReflectionOn", 0f);
            Shader.SetGlobalColor("_CitySkyColor", _skyColor);
            if (!_reflective || quality == 0) return;
            var cameraObject = new GameObject("City 3D Reflection Camera");
            cameraObject.transform.SetParent(_root.transform, false);
            _reflectionCamera = cameraObject.AddComponent<Camera>();
            _reflectionCamera.depth = _camera.depth - 1f; // рисуется раньше основной камеры
            _reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            _reflectionCamera.backgroundColor = _skyColor;
            _reflectionCamera.cullingMask = ~(1 << WaterLayer);
            _reflectionCamera.allowHDR = true;
            _reflectionCamera.allowMSAA = false;
            _reflectionCamera.useOcclusionCulling = false;
            var data = _reflectionCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            if (_rendererIndex >= 0) data.SetRenderer(_rendererIndex);
            _reflectionScale = quality >= 2 ? 0.5f : 0.33f;
        }

        /// <summary>
        /// Зеркальная камера: мир отражён относительно воды (y = WaterLevel), а чтобы грани не вывернулись,
        /// отражение дополнено зеркалом по X в пространстве камеры и в проекции — картинка в текстуре
        /// перевёрнута по горизонтали, но шейдер берёт её той же матрицей, так что всё сходится.
        /// Косая ближняя плоскость отрезает всё, что ниже воды.
        /// </summary>
        void UpdateReflection()
        {
            if (_reflectionCamera == null) return;
            int width = Mathf.Max(64, Mathf.RoundToInt(Screen.width * _reflectionScale));
            int height = Mathf.Max(64, Mathf.RoundToInt(Screen.height * _reflectionScale));
            if (_reflectionTexture == null || _reflectionTexture.width != width || _reflectionTexture.height != height)
            {
                _reflectionCamera.targetTexture = null;
                if (_reflectionTexture != null)
                {
                    _reflectionTexture.Release();
                    Destroy(_reflectionTexture);
                }
                _reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = "City3D Reflection",
                    hideFlags = HideFlags.DontSave
                };
                _reflectionTexture.Create();
                _reflectionCamera.targetTexture = _reflectionTexture;
            }

            var mirror = Matrix4x4.identity;
            mirror.m11 = -1f;
            mirror.m13 = 2f * WaterLevel;
            var flipX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            var view = flipX * _camera.worldToCameraMatrix * mirror;
            var projection = flipX * _camera.projectionMatrix * flipX;
            _reflectionCamera.worldToCameraMatrix = view;
            _reflectionCamera.projectionMatrix = projection;
            _reflectionCamera.cullingMatrix = projection * view;
            _reflectionCamera.transform.position = mirror.MultiplyPoint(_camera.transform.position);
            var clip = CameraSpacePlane(view, new Vector3(0f, WaterLevel + 0.002f, 0f), Vector3.up);
            _reflectionCamera.projectionMatrix = _reflectionCamera.CalculateObliqueMatrix(clip);
            _reflectionCamera.backgroundColor = _skyColor;

            Shader.SetGlobalMatrix("_CityReflectionVP", projection * view);
            Shader.SetGlobalTexture("_CityReflectionTex", _reflectionTexture);
            Shader.SetGlobalFloat("_CityReflectionOn", 1f);
        }

        static Vector4 CameraSpacePlane(Matrix4x4 view, Vector3 point, Vector3 normal)
        {
            var p = view.MultiplyPoint(point);
            var n = view.MultiplyVector(normal).normalized;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
        }

        /// <summary>
        /// Дороги и мосты 3D-города строит RoadBuilder: ширина у дороги, подъездов и моста одна (RoadWidth —
        /// та же, что у дороги интерфейса), мост встаёт сам там, где отрезок проходит над водой, и заходит
        /// на набережные, а подъезды плавно поднимают дорогу к полотну. Если в Resources есть свой
        /// RoadPrefabs.asset с префабами — берутся они, иначе куски собираются кодом.
        /// </summary>
        void SetupRoads()
        {
            var source = Resources.Load<RoadPrefabSet>(ResourceFolder + "RoadPrefabs");
            _roadSet = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<RoadPrefabSet>();
            _roadSet.hideFlags = HideFlags.DontSave;
            _owned.Add(_roadSet);
            // Размеры, которые обязаны совпадать со сценой: ширина дороги интерфейса, высота воды и полотна.
            _roadSet.width = RoadWidth;
            _roadSet.deckTop = DeckTop;
            _roadSet.waterLevel = WaterLevel;
            _roadSet.bankMargin = Mathf.Max(_roadSet.bankMargin, BankWidth + 0.02f);

            var templates = new GameObject("Road templates");
            templates.transform.SetParent(_root.transform, false);
            templates.SetActive(false); // шаблоны не рисуются, их копии — активны
            var asphalt = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Asphalt);
            var concrete = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeConcrete);
            var rail = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            float road = _roadSet.roadThickness, deck = _roadSet.deckThickness;
            if (_roadSet.roadPrefab == null)
                _roadSet.roadPrefab = Template(templates, "Road", b => b.Beam(new Vector3(0f, -road / 2f, 0f), new Vector3(0f, -road / 2f, 1f), 1f, road, asphalt, asphalt));
            if (_roadSet.bridgeDeckPrefab == null)
                _roadSet.bridgeDeckPrefab = Template(templates, "Deck", b => b.Beam(new Vector3(0f, -deck / 2f, 0f), new Vector3(0f, -deck / 2f, 1f), 1f, deck, concrete, asphalt));
            if (_roadSet.pierPrefab == null)
                _roadSet.pierPrefab = Template(templates, "Pier", b => b.Prism(0f, 0f, 0.5f, 0f, 1f, 6, concrete, concrete));
            if (_roadSet.railingPrefab == null)
                _roadSet.railingPrefab = Template(templates, "Railing", b => b.Beam(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 1f), 1f, 1f, rail, rail));

            var container = new GameObject("Roads");
            container.transform.SetParent(_root.transform, false);
            _roads = _root.AddComponent<RoadBuilder>();
            _roads.Prefabs = _roadSet;
            _roads.Container = container.transform;
            _roads.Animate = true;
            int mask = LayerMask.GetMask("Water");
            if (mask == 0) mask = 1 << WaterLayer;
            // Луч по слою Water (у воды MeshCollider) и, для надёжности, расстояние до линии реки.
            _roads.Detector = new RiverCrossingDetector(RiverDetection.Both, mask, 1f, _riverLines, RiverWidth / 2f);

            // Стройка: рабочие, экскаватор и каток из пулов. Свои префабы — в Resources/…/RoadConstruction.asset,
            // пустые поля заполняются моделями, собранными кодом.
            var crewSource = Resources.Load<RoadConstructionSet>(ResourceFolder + "RoadConstruction");
            var crew = crewSource != null ? UnityEngine.Object.Instantiate(crewSource) : ScriptableObject.CreateInstance<RoadConstructionSet>();
            crew.hideFlags = HideFlags.DontSave;
            _owned.Add(crew);
            if (crew.workerPrefab == null || crew.excavatorPrefab == null || crew.rollerPrefab == null || crew.dustPrefab == null)
            {
                var crewMaterial = LoadMaterial("CityBuildings", Color.white);
                crewMaterial.SetTexture("_BaseMap", ConstructionArt.Palette);
                crewMaterial.SetColor("_EmissionColor", Color.black);
                crewMaterial.DisableKeyword("_EMISSION");
                NoSpecular(crewMaterial);
                var art = ConstructionArt.Build(templates.transform, crewMaterial, _owned);
                if (crew.workerPrefab == null) crew.workerPrefab = art.Worker;
                if (crew.excavatorPrefab == null) crew.excavatorPrefab = art.Excavator;
                if (crew.rollerPrefab == null) crew.rollerPrefab = art.Roller;
                if (crew.dustPrefab == null) crew.dustPrefab = art.Dust;
            }
            _construction = _root.AddComponent<RoadConstructionPlayer>();
            _construction.Init(_roads, crew);
        }

        /// <summary>Идёт ли сейчас стройка этой дороги (интерфейс не рисует её поверх, пока видна техника).</summary>
        public bool IsUnderConstruction(string roadId) => _constructing.Contains(roadId);

        /// <summary>Стройка 2–3 секунды: чем длиннее дорога, тем дольше.</summary>
        static float ConstructionSeconds(Vector3 a, Vector3 b) => Mathf.Clamp(1.9f + 0.45f * Vector3.Distance(a, b), 2f, 3f);

        GameObject Template(GameObject parent, string name, Action<MeshBuilder> build)
        {
            var builder = new MeshBuilder();
            build(builder);
            return AddMesh("Template " + name, builder.ToMesh("City3D Road " + name), _buildings, parent.transform, true);
        }

        /// <summary>Мосты карты стоят всегда (как и раньше); мосты к районам-расширениям прячутся вместе с ними.</summary>
        void BuildBridges()
        {
            foreach (var road in _map.Roads.Where(r => r.IsBridge))
            {
                var la = _map.GetLocation(road.A);
                var lb = _map.GetLocation(road.B);
                if (la == null || lb == null) continue;
                var start = BoardLayout.World(la.X, la.Y);
                var end = BoardLayout.World(lb.X, lb.Y);
                var built = _roads.BuildRoad(start, end, false);
                if (built == null) continue;
                if (!built.HasBridge)
                {
                    // Река не нашлась (узел у самой воды) — мост по правилам карты всё равно нужен: средняя часть.
                    float length = built.Length;
                    _roads.Remove(built);
                    built = _roads.BuildRoad(start, end, false, new WaterSpan(length * 0.3f, length * 0.7f));
                }
                AddArches(built);
                _builtRoads[road.Id] = built;
                string district = _map.GetDistrict(la.DistrictId)?.IsExpansion == true ? la.DistrictId
                    : _map.GetDistrict(lb.DistrictId)?.IsExpansion == true ? lb.DistrictId : null;
                if (district != null) _districts[district + "#bridge:" + road.Id] = built.Root;
            }
        }

        /// <summary>У длинных пролётов — две стальные арки с подвесками по краям полотна (внутри его ширины).</summary>
        void AddArches(BuiltRoad built)
        {
            var steel = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
            var builder = new MeshBuilder();
            var flat = built.End - built.Start;
            flat.y = 0f;
            var side = new Vector3(flat.z, 0f, -flat.x).normalized;
            const int segments = 10;
            foreach (var piece in built.Pieces)
            {
                float length = piece.To - piece.From;
                if (piece.Kind != RoadPieceKind.Bridge || length <= 1.0f) continue;
                float rise = Mathf.Min(0.16f, length * 0.12f);
                for (int s = -1; s <= 1; s += 2)
                {
                    var off = side * (_roadSet.width / 2f - 0.007f) * s;
                    float from = piece.From, to = piece.To;
                    Vector3 Arc(float t) => built.PointAt(Mathf.Lerp(from, to, t)) + off + Vector3.up * (_roadSet.deckTop + Mathf.Sin(t * Mathf.PI) * rise);
                    for (int i = 0; i < segments; i++)
                    {
                        builder.Beam(Arc(i / (float)segments), Arc((i + 1) / (float)segments), 0.014f, 0.014f, steel, steel);
                        if (i > 0)
                        {
                            var top = Arc(i / (float)segments);
                            builder.Beam(new Vector3(top.x, _roadSet.deckTop, top.z), top, 0.004f, 0.004f, steel, steel);
                        }
                    }
                }
            }
            if (builder.VertexCount > 0) AddMesh("Arches", builder.ToMesh("City3D Arches"), _buildings, built.Root.transform, true);
        }

        /// <summary>
        /// Построенные игроками дороги появляются в 3D (DOTween): плита под дорогой интерфейса с настоящей тенью,
        /// а если дорога идёт над водой — мост. При первом вызове (загрузка партии) — сразу, без анимации.
        /// </summary>
        public void SyncRoads(Game game)
        {
            if (_roads == null || game == null) return;
            bool animated = _roadsSynced;
            _roadsSynced = true;
            foreach (var road in game.Map.Roads)
            {
                if (road.IsBridge) continue;
                bool owned = game.GetRoadOwner(road.Id).HasValue;
                bool has = _builtRoads.TryGetValue(road.Id, out var built);
                if (owned && !has)
                {
                    var la = game.Map.GetLocation(road.A);
                    var lb = game.Map.GetLocation(road.B);
                    if (la == null || lb == null) continue;
                    var start = BoardLayout.World(la.X, la.Y);
                    var end = BoardLayout.World(lb.X, lb.Y);
                    if (animated && _construction != null)
                    {
                        // Стройка: дорога проявляется по фронту, вокруг — рабочие, экскаватор и каток.
                        string id = road.Id;
                        built = _construction.PlayConstruction(start, end, ConstructionSeconds(start, end), done => _constructing.Remove(id));
                        if (built != null) _constructing.Add(id);
                    }
                    else built = _roads.BuildRoad(start, end, false);
                    if (built != null) _builtRoads[road.Id] = built;
                }
                else if (!owned && has)
                {
                    _roads.Remove(built);
                    _builtRoads.Remove(road.Id);
                    _constructing.Remove(road.Id);
                }
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
            UpdateReflection();

            foreach (var pair in _districts)
            {
                int mark = pair.Key.IndexOf('#'); // «район#bridges», «район#decals»
                string id = mark >= 0 ? pair.Key.Substring(0, mark) : pair.Key;
                bool open = id == "_" || districtOpen == null || districtOpen(id);
                if (pair.Value.activeSelf != open) pair.Value.SetActive(open);
            }

            UpdateTrees(layout, districtOpen);

            _waterOffset = (_waterOffset + dt * 0.06f) % 1f;
            _water.SetTextureOffset("_BaseMap", new Vector2(-_waterOffset, 0f));
            if (_reflective)
            {
                _waveOffset += new Vector4(0.021f, 0.013f, -0.017f, 0.024f) * dt;
                _waveOffset = new Vector4(Mathf.Repeat(_waveOffset.x, 1f), Mathf.Repeat(_waveOffset.y, 1f),
                    Mathf.Repeat(_waveOffset.z, 1f), Mathf.Repeat(_waveOffset.w, 1f));
                _water.SetVector("_WaveOffset", _waveOffset);
            }
        }

        /// <summary>
        /// Дальние деревья — карточками: клетка переключается, когда метр сцены на экране становится меньше порога
        /// (с запасом в обе стороны, чтобы не мигало на границе). Зум приближает — деревья снова объёмные.
        /// </summary>
        void UpdateTrees(BoardLayout layout, Func<string, bool> districtOpen)
        {
            float lean = layout.CameraTilt;
            if (Mathf.Abs(lean - _cardLean) > 3f) RebuildCards(lean);
            float threshold = _quality == 0 ? 125f : _quality == 1 ? 100f : 80f;
            foreach (var chunk in _treeChunks)
            {
                bool open = chunk.District == "_" || districtOpen == null || districtOpen(chunk.District);
                if (chunk.Root.activeSelf != open) chunk.Root.SetActive(open);
                if (!open) continue;
                var left = layout.WorldToScreen(chunk.Center - Vector3.right * 0.5f);
                var right = layout.WorldToScreen(chunk.Center + Vector3.right * 0.5f);
                float perUnit = (right - left).magnitude;
                bool cards = perUnit < (chunk.ShowCards ? threshold + 10f : threshold);
                if (cards != chunk.ShowCards || chunk.Full.activeSelf == cards)
                {
                    chunk.ShowCards = cards;
                    chunk.Full.SetActive(!cards);
                    chunk.Cards.SetActive(cards);
                }
            }
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
            // Небо в отражениях воды: днём голубое, на закате тёплое, ночью тёмно-синее.
            _skyColor = Color.Lerp(Color.Lerp(new Color(0.62f, 0.72f, 0.86f), new Color(0.86f, 0.66f, 0.52f), Mathf.Clamp01(l.Golden + l.Twilight * 0.5f)),
                new Color(0.04f, 0.05f, 0.10f), l.Night);
            if (_reflective) Shader.SetGlobalColor("_CitySkyColor", _skyColor);
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
            if (_reflectionCamera != null) _reflectionCamera.targetTexture = null;
            if (_reflectionTexture != null)
            {
                _reflectionTexture.Release();
                Destroy(_reflectionTexture);
                _reflectionTexture = null;
            }
            Shader.SetGlobalFloat("_CityReflectionOn", 0f);
            _treeChunks.Clear();
            _builtRoads.Clear();
            _constructing.Clear();
            _roads = null;
            _construction = null;
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
