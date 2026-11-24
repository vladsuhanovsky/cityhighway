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
    public sealed partial class City3DScene : IDisposable
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
            // One reusable scratch builder per chunk. Camera movement can refresh
            // billboard geometry frequently without allocating new List buffers.
            public MeshBuilder CardBuilder;
            public bool ShowCards;
            public float CardLean = float.NaN, CardYaw = float.NaN;
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
        // Each chunk tracks its own billboard orientation to avoid rebuilding hidden meshes.
        // Отражения в воде: своя камера рисует зеркальный город в текстуру, шейдер воды берёт из неё картинку.
        bool _reflective;
        Camera _reflectionCamera;
        RenderTexture _reflectionTexture;
        float _reflectionScale = 0.5f;
        Vector4 _waveOffset;
        Color _skyColor = new Color(0.62f, 0.72f, 0.86f);
        RenderPipelineAsset _previousPipeline = null;
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
        /// <summary>Река впадает в другую (хотя бы один конец внутри карты): её вода лежит ниже, под водой главной.</summary>
        readonly List<bool> _riverIsTributary = new List<bool>();
        readonly Dictionary<string, BuiltRoad> _builtRoads = new Dictionary<string, BuiltRoad>();
        bool _roadsSynced;
        RoadConstructionPlayer _construction;
        readonly HashSet<string> _constructing = new HashSet<string>();

        public int BuildingCount { get; private set; }
        public int TreeCount { get; private set; }

        /// <summary>Read-only handle for explicit editor Play Mode geometry audits.</summary>
        public static City3DScene ActiveScene { get; private set; }
        public Transform GeneratedRoot => _root != null ? _root.transform : null;
        public string CityName => _map != null ? _map.Name : null;
        public int GraphicsQuality => _quality;
        public City3DFrameMonitor FrameMonitor { get; private set; }
        public City3DGenerationTiming GenerationTiming { get; private set; }

        City3DScene(CityMap map)
        {
            _map = map;
            _root = new GameObject("City Highways 3D");
        }

        /// <summary>Построить город; null — 3D недоступно (нет URP, ошибка) — тогда остаётся прежнее плоское поле.</summary>
        /// <param name="quality">0 — низкое (без теней, меньше зданий), 1 — среднее, 2 — высокое.</param>
        List<string> _hubSites;
        Dictionary<string, HubPlacement.Site> _hubPlaces = new Dictionary<string, HubPlacement.Site>();

        /// <param name="hubSites">Станции с комплексами хабов (магнат): вокруг них планировщик не ставит дома и деревья.</param>
        public static City3DScene TryCreate(CityMap map, int quality, IEnumerable<string> hubSites = null)
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
                if (hubSites != null) scene._hubSites = hubSites.ToList();
                scene.Build(Mathf.Clamp(quality, 0, 2));
                ActiveScene = scene;
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
            // Profiling must not prevent otherwise valid unnamed maps from building.
            var timingCity = string.IsNullOrWhiteSpace(_map.Name)
                ? "(unnamed city)" : _map.Name;
            var generation = new City3DGenerationTiming(timingCity, quality,
                System.Diagnostics.Stopwatch.GetTimestamp());
            float density = quality == 0 ? 0.55f : quality == 1 ? 0.8f : 1f;
            // Рельеф — до планировки: сначала сглаженные русла (по ним долины), затем поле высот. Планировщик
            // смотрит на высоты (ельники на холмах), а всё дальнейшее встаёт на холмы.
            using (City3DGenerationMarkers.Relief.Auto())
            {
                PrepareRiverLines();
                // Места хабов — до рельефа: под ними земля выравнивается.
                _hubPlaces = HubPlacement.Plan(_map, _hubSites, RiverWidth / 2f + BankWidth);
                _relief = new ReliefField(_map, _riverLines, RiverWidth / 2f + BankWidth, _hubPlaces);
                Relief.Use(_relief);
            }
            generation.Mark("Rivers and relief", System.Diagnostics.Stopwatch.GetTimestamp());
            // Scene-authored visual overrides are optional and default to the legacy layout.
            var tuning = CityGenerationTuning.FindForMap(_map.Name);
            float buildingMultiplier = tuning != null ? tuning.BuildingDensity : 1f;
            float treeMultiplier = tuning != null ? tuning.TreeDensity : 1f;
            var clearances = CityGenerationExclusionZone.Collect(_map.Name);
            CityPlanner.Plan plan;
            using (City3DGenerationMarkers.Planning.Auto())
            {
                plan = CityPlanner.Build(_map, density, _hubSites,
                    _hubPlaces.Values.Select(h => h.Origin),
                    buildingMultiplier, treeMultiplier, clearances);
            }
            generation.Mark("City planning", System.Diagnostics.Stopwatch.GetTimestamp());
            // Replace selected visual instances before district/tree meshes and counts are built.
            CityProceduralPrefabBudget.Session prefabBudget;
            using (City3DGenerationMarkers.Prefabs.Auto())
            {
                CityProceduralReplacementResolver.Apply(plan, _map.Name, _root.transform);
                // Authored stable-ID replacements have priority over automatic rules.
                var prefabBudgetConfig = CityProceduralPrefabBudget.FindForMap(_map.Name);
                prefabBudget = prefabBudgetConfig != null
                    ? prefabBudgetConfig.Begin(quality) : null;
                CityProceduralBuildingPrefabSpawner.Apply(plan, _map.Name,
                    CityProceduralBuildingPrefabRules.FindForMap(_map.Name),
                    _root.transform, prefabBudget);
                // Distant CardOnly trees remain in the plan for optimized billboard rendering.
                CityProceduralTreePrefabSpawner.Apply(plan, _map.Name,
                    CityProceduralTreePrefabRules.FindForMap(_map.Name),
                    _root.transform, prefabBudget);
                BuildingCount = plan.Buildings.Count;
                TreeCount = plan.Trees.Count;
            }
            generation.Mark("Prefab replacements", System.Diagnostics.Stopwatch.GetTimestamp());

            _quality = quality;
            SetupRendering(quality);
            CreateMaterials(plan);
            generation.Mark("Rendering and materials", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Ground.Auto())
                BuildGround();
            generation.Mark("Ground", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Decals.Auto())
                BuildDecals(plan);
            generation.Mark("Decals", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Rivers.Auto())
                BuildRivers();
            generation.Mark("River meshes", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Districts.Auto())
                BuildDistricts(plan);
            generation.Mark("District buildings", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Trees.Auto())
                BuildTrees(plan);
            generation.Mark("Trees", System.Diagnostics.Stopwatch.GetTimestamp());
            // Optional capped visual replacements; unselected props still use
            // the original district-batched geometry and visibility rules.
            CityProceduralPropPrefabSpawner.Apply(
                plan, _map.Name, CityProceduralPropPrefabRules.FindForMap(_map.Name),
                _root.transform, _districts, WaterLevel, prefabBudget);
            BuildProps(plan);
            generation.Mark("Props and replacement models", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Roads.Auto())
                SetupRoads();
            generation.Mark("Road setup", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Junctions.Auto())
                BuildJunctions();
            generation.Mark("Junctions", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Stations.Auto())
                BuildStations();
            generation.Mark("Stations", System.Diagnostics.Stopwatch.GetTimestamp());
            using (City3DGenerationMarkers.Bridges.Auto())
                BuildBridges();
            generation.Mark("Bridges", System.Diagnostics.Stopwatch.GetTimestamp());
            // Optional scene-authored visuals. Core map geometry and rules remain procedural.
            using (City3DGenerationMarkers.Decorations.Auto())
                SceneDecorationSpawner.Spawn(_map.Name, _root.transform);
            generation.Mark("Authored scene decorations", System.Diagnostics.Stopwatch.GetTimestamp());
            SetupReflections(quality);
            // Attach after construction so the first samples exclude one-time map build.
            FrameMonitor = _root.AddComponent<City3DFrameMonitor>();
            generation.Mark("Reflections and sampling setup",
                System.Diagnostics.Stopwatch.GetTimestamp());
            GenerationTiming = generation;
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
        internal static int FindUniversalRenderer()
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

        /// <summary>
        /// Запасной путь, только в редакторе: ассет URP с рендерером Universal, созданный кодом, — на время партии.
        /// UniversalRenderPipelineAsset.Create есть только в редакторе (в сборке его нет — была ошибка CS0117),
        /// а созданному кодом рендереру в сборке всё равно не хватило бы шейдеров пост-обработки. В сборке рендерер
        /// CityRenderer3D уже лежит в UniversalRP.asset, поэтому FindUniversalRenderer его находит и сюда не доходит.
        /// </summary>
        void SwapToRuntimePipeline()
        {
#if UNITY_EDITOR
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
#else
            Debug.LogWarning("3D-город: в ассете URP нет рендерера Universal (CityRenderer3D). Добавьте его в список Renderer List " +
                             "у Assets/Settings/UniversalRP.asset — пока 3D рисуется рендерером по умолчанию.");
#endif
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
        ReliefField _relief;

        void BuildGround()
        {
            const float m = TerrainTexture.Margin, span = 1f + 2f * m;
            // Exact quad count: avoid geometric List growth during map construction.
            const int cellsX = 240, cellsY = 136;
            var top = new MeshBuilder(cellsX * cellsY);
            // Сетка по рельефу: клетка ~7 см — вдвое мельче полосы вдоль дорог, холмы и подрезки под дорогами видны.
            for (int iy = 0; iy < cellsY; iy++)
            for (int ix = 0; ix < cellsX; ix++)
            {
                float x0 = -m + span * ix / cellsX, x1 = -m + span * (ix + 1) / cellsX;
                float y0 = -m + span * iy / cellsY, y1 = -m + span * (iy + 1) / cellsY;
                var a = BoardLayout.World(x0, y1); var b = BoardLayout.World(x0, y0);
                var c = BoardLayout.World(x1, y0); var d = BoardLayout.World(x1, y1);
                var uv = new Rect((x0 + m) / span, 1f - (y1 + m) / span, (x1 - x0) / span, (y1 - y0) / span);
                top.Quad(a, b, c, d, uv);
            }
            var groundMesh = top.ToMesh("City3D Ground");
            groundMesh.RecalculateNormals();   // склоны освещаются по-настоящему
            groundMesh.RecalculateTangents(); // карте неровностей нужны касательные, иначе освещение ломается
            AddMesh("Ground", groundMesh, _ground, _root.transform, false);

            BuildOutskirtsGround();
        }

        /// <summary>Расстояние по горизонтали до ближайшего сглаженного русла (не дальше limit).</summary>
        float RiverDistance2D(Vector2 p, float limit)
        {
            float best = limit;
            foreach (var line in _riverLines)
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    var a = new Vector2(line[i].x, line[i].z);
                    var b = new Vector2(line[i + 1].x, line[i + 1].z);
                    if (p.x < Mathf.Min(a.x, b.x) - best || p.x > Mathf.Max(a.x, b.x) + best ||
                        p.y < Mathf.Min(a.y, b.y) - best || p.y > Mathf.Max(a.y, b.y) + best) continue;
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
                }
            return best;
        }

        /// <summary>
        /// Земля вокруг доски до горизонта: квадрат 60 × 60 м с текстурой окрестностей (поля, лесная подстилка,
        /// деревни; у края доски — тот же цвет шва, что у доски) и за ним ровная равнина 400 × 400 м, которую
        /// растворяет туман. Обе чуть ниже доски — доска лежит поверх без мерцания.
        /// </summary>
        void BuildOutskirtsGround()
        {
            const float extent = CityTextures.OutskirtsExtent, y = -0.003f;
            const int cells = 120;   // клетка 0,5 м: холмы за полем продолжаются, к горизонту сходят на нет
            // Conservative estimate: river-adjacent cells subdivide further.
            var near = new MeshBuilder(cells * cells);
            float plateX = BoardLayout.WorldWidth * (0.5f + TerrainTexture.Margin), plateZ = BoardLayout.WorldDepth * (0.5f + TerrainTexture.Margin);
            for (int iz = 0; iz < cells; iz++)
            for (int ix = 0; ix < cells; ix++)
            {
                float x0 = -extent + 2f * extent * ix / cells, x1 = -extent + 2f * extent * (ix + 1) / cells;
                float z0 = -extent + 2f * extent * iz / cells, z1 = -extent + 2f * extent * (iz + 1) / cells;
                // Под доской (с запасом в полметра) окрестностей нет: крупная сетка на рельефе вылезла бы сквозь
                // мелкую там, где земля подрезана под дорогу.
                if (x0 > -plateX + 0.5f && x1 < plateX - 0.5f && z0 > -plateZ + 0.5f && z1 < plateZ - 0.5f) continue;
                // У реки клетка 0,5 м перекрывает русло целиком: её углы стоят на берегах, и земля между ними
                // вылезала зубцами поверх воды. Такие клетки делятся на 5-сантиметровые — дно долины повторяет русло.
                var middle = new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f);
                int split = RiverDistance2D(middle, 1.2f) < 0.36f + RiverWidth / 2f + BankWidth + 0.3f ? 10 : 1;
                for (int sz = 0; sz < split; sz++)
                for (int sx = 0; sx < split; sx++)
                {
                    float ax = Mathf.Lerp(x0, x1, (float)sx / split), bx = Mathf.Lerp(x0, x1, (float)(sx + 1) / split);
                    float az = Mathf.Lerp(z0, z1, (float)sz / split), bz = Mathf.Lerp(z0, z1, (float)(sz + 1) / split);
                    var uv = new Rect((ax + extent) / (2f * extent), (az + extent) / (2f * extent), (bx - ax) / (2f * extent), (bz - az) / (2f * extent));
                    // Юго-запад, северо-запад, северо-восток, юго-восток — грань смотрит вверх.
                    near.Quad(new Vector3(ax, y, az), new Vector3(ax, y, bz), new Vector3(bx, y, bz), new Vector3(bx, y, az), uv);
                }
            }
            var outskirts = LoadMaterial("CityGround", Color.white);
            outskirts.SetTexture("_BaseMap", CityTextures.Outskirts(_map));
            outskirts.SetFloat("_Smoothness", 0.04f);
            NoSpecular(outskirts);
            // Та же мелкая деталь, что у доски, с тем же шагом на метр.
            outskirts.EnableKeyword("_NORMALMAP");
            outskirts.SetTexture("_DetailAlbedoMap", CityTextures.DetailAlbedo);
            outskirts.SetTexture("_DetailNormalMap", CityTextures.DetailNormal);
            outskirts.SetFloat("_DetailAlbedoMapScale", 1f);
            outskirts.SetFloat("_DetailNormalMapScale", 0.6f);
            float tiles = 40f / (BoardLayout.WorldWidth * (1f + 2f * TerrainTexture.Margin)) * 2f * extent;
            outskirts.SetTextureScale("_DetailAlbedoMap", new Vector2(tiles, tiles));
            outskirts.EnableKeyword("_DETAIL_MULX2");
            near.Drape(Relief.At);
            var nearMesh = near.ToMesh("City3D Outskirts");
            nearMesh.RecalculateNormals();
            nearMesh.RecalculateTangents();
            AddMesh("Outskirts", nearMesh, outskirts, _root.transform, false);

            // Равнина до горизонта: ровный цвет шва, вырез под квадрат окрестностей.
            var plain = new MeshBuilder();
            var flat = new Rect(0.5f, 0.5f, 0f, 0f);
            const float far = 200f, low = y - 0.002f;
            plain.Quad(new Vector3(-far, low, -far), new Vector3(-far, low, -extent), new Vector3(far, low, -extent), new Vector3(far, low, -far), flat);
            plain.Quad(new Vector3(-far, low, extent), new Vector3(-far, low, far), new Vector3(far, low, far), new Vector3(far, low, extent), flat);
            plain.Quad(new Vector3(-far, low, -extent), new Vector3(-far, low, extent), new Vector3(-extent, low, extent), new Vector3(-extent, low, -extent), flat);
            plain.Quad(new Vector3(extent, low, -extent), new Vector3(extent, low, extent), new Vector3(far, low, extent), new Vector3(far, low, -extent), flat);
            var plainMaterial = LoadMaterial("CityGround", TerrainTexture.SeamColor);
            NoSpecular(plainMaterial);
            AddMesh("Plain", plain.ToMesh("City3D Plain"), plainMaterial, _root.transform, false);
        }

        /// <summary>
        /// Сглаженные русла (до рельефа: высота 0). Конец у края карты уходит за доску до горизонта (окрестности —
        /// 30 м), а конец внутри карты (приток впадает в другую реку) — продлевается чуть-чуть, чтобы вода слилась.
        /// </summary>
        void PrepareRiverLines()
        {
            _riverLines.Clear();
            _riverIsTributary.Clear();
            float half = RiverWidth / 2f;
            var inner = new List<(bool start, bool end)>();
            foreach (var line in _map.Rivers)
            {
                if (line.Count < 2) continue;
                // Почти совпадающие точки убираем: сплайн на крошечном отрезке после длинного «перехлёстывает» назад,
                // и продолжение реки за край карты разворачивалось внутрь (в Амстердаме 30-метровый хвост канала
                // прошёл через весь город — по станциям NDSM и Вестерпарк).
                var raw = new List<Vector3>();
                foreach (var point in line.Select(p => BoardLayout.World(p.X, p.Y)))
                    if (raw.Count == 0 || Flat(point - raw[raw.Count - 1]) > MinRiverStep) raw.Add(point);
                    else raw[raw.Count - 1] = point;                       // крайняя точка важнее (она у края карты)
                if (raw.Count < 2) continue;
                bool startAtEdge = AtMapEdge(line[0].X, line[0].Y), endAtEdge = AtMapEdge(line[line.Count - 1].X, line[line.Count - 1].Y);
                // Сначала сглаживаем само русло, и только потом достраиваем прямые продолжения. Раньше 30-метровый
                // хвост входил в сплайн: рядом с коротким отрезком у края касательная становилась огромной, и кривая
                // уходила от края внутрь карты почти на 2 м прямым «языком» — вода ложилась на дома (их ставят
                // по настоящему руслу), а у края получался кривой излом.
                var smooth = Smooth(raw, 5);
                // Направление продолжения — по исходным точкам (по горизонтали): у сплайна последний шаг может смотреть назад.
                var startDir = FlatDir(raw[0] - raw[1]);
                var endDir = FlatDir(raw[raw.Count - 1] - raw[raw.Count - 2]);
                smooth.Insert(0, smooth[0] + startDir * (startAtEdge ? 30f : half * 0.6f));
                smooth.Add(smooth[smooth.Count - 1] + endDir * (endAtEdge ? 30f : half * 0.6f));
                _riverLines.Add(smooth);
                inner.Add((!startAtEdge, !endAtEdge));
            }

            // Стык: приток доходил до середины главной реки и ещё дальше — почти до противоположного берега, и тот
            // берег вокруг устья вырезался. Теперь внутренний конец обрезается в воде главной реки, на JoinDepth от её
            // середины: устье открыто, дальний берег цел, конец притока спрятан под водой главной (она выше).
            var originals = _riverLines.Select(l => new List<Vector3>(l)).ToList();
            for (int r = 0; r < _riverLines.Count; r++)
            {
                if (inner[r].end) _riverLines[r] = TrimAtJoin(_riverLines[r], r, originals, half * JoinDepth);
                if (inner[r].start)
                {
                    var reversed = new List<Vector3>(_riverLines[r]);
                    reversed.Reverse();
                    reversed = TrimAtJoin(reversed, r, originals, half * JoinDepth);
                    reversed.Reverse();
                    _riverLines[r] = reversed;
                }
                _riverIsTributary.Add(inner[r].start || inner[r].end);
            }
        }

        /// <summary>Конец притока — на этой доле полуширины от середины главной реки (внутри её воды).</summary>
        const float JoinDepth = 0.35f;

        /// <summary>
        /// Обрезать конец русла там, где оно входит глубже limit в воду другой реки (по расстоянию до её середины).
        /// Не заходит в другую реку — русло без изменений.
        /// </summary>
        static List<Vector3> TrimAtJoin(List<Vector3> line, int self, List<List<Vector3>> rivers, float limit)
        {
            float DistanceToOthers(Vector3 point)
            {
                float best = float.MaxValue;
                var p = new Vector2(point.x, point.z);
                for (int r = 0; r < rivers.Count; r++)
                {
                    if (r == self) continue;
                    var other = rivers[r];
                    for (int i = 0; i + 1 < other.Count; i++)
                    {
                        var a = new Vector2(other[i].x, other[i].z);
                        var ab = new Vector2(other[i + 1].x, other[i + 1].z) - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                        best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
                    }
                }
                return best;
            }

            // С конца назад — первая точка, которая ещё дальше limit: между ней и следующей и проходит обрез.
            for (int i = line.Count - 1; i > 0; i--)
            {
                float inside = DistanceToOthers(line[i]);
                if (inside > limit) return line;               // конец не в другой реке — не трогаем
                float before = DistanceToOthers(line[i - 1]);
                if (before <= limit) continue;
                float t = Mathf.InverseLerp(before, inside, limit);
                var cut = line.GetRange(0, i);
                cut.Add(Vector3.Lerp(line[i - 1], line[i], t));
                return cut.Count >= 2 ? cut : line;
            }
            return line;
        }

        /// <summary>Реки: вода (текстура течёт вдоль русла) и гранитные набережные с двух сторон — в долинах рельефа.</summary>
        void BuildRivers()
        {
            var water = new MeshBuilder();
            var banks = new MeshBuilder();
            var granite = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Granite);
            float half = RiverWidth / 2f;

            // Вода и набережные. Там, где русло заходит в воду другой реки (слияние, пересечение),
            // набережная не строится — иначе гранитная стенка перегородила бы воду.
            for (int r = 0; r < _riverLines.Count; r++)
            {
                var points = _riverLines[r];
                // Притоки — ниже главных рек: их обрезанный конец лежит под водой главной и не виден. Внутри одной
                // группы каждая следующая река чуть выше: на слиянии вода не мерцает.
                bool tributary = r < _riverIsTributary.Count && _riverIsTributary[r];
                var wl = Vector3.up * (WaterLevel + (tributary ? 0f : 0.0008f) + r * 0.0002f);
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
                    water.Quad(a + na * half + wl, a - na * half + wl, b - nb * half + wl, b + nb * half + wl, new Rect(u0, 0f, u1 - u0, 1f));
                    // Набережные: верх, стенка к воде, стенка к земле — на каждом берегу. Кусками не длиннее BankPiece:
                    // у стыка рек каждый кусок проверяется отдельно — набережная обрывается ровно у воды другой реки,
                    // а не торчит в неё (раньше решение принималось по середине длинного куска).
                    int pieces = Mathf.Max(1, Mathf.CeilToInt(length / BankPiece));
                    for (int k = 0; k < pieces; k++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float t0 = k / (float)pieces, t1 = (k + 1) / (float)pieces;
                        var pa = Vector3.Lerp(a, b, t0); var pb = Vector3.Lerp(a, b, t1);
                        var sa = Vector3.Lerp(na, nb, t0).normalized; var sb = Vector3.Lerp(na, nb, t1).normalized;
                        var ia = pa + sa * half * s; var ib = pb + sb * half * s;
                        var oa = pa + sa * (half + BankWidth) * s; var ob = pb + sb * (half + BankWidth) * s;
                        if (InOtherRiver(r, (ia + ib + oa + ob) * 0.25f)) continue;
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

        /// <summary>Длина куска набережной (м): на стыке рек каждый кусок проверяется отдельно.</summary>
        const float BankPiece = 0.03f;

        /// <summary>Шаг русла меньше этого (м) — точка-дубль: сплайн на таком отрезке перехлёстывает.</summary>
        const float MinRiverStep = 0.15f;

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-10f ? v.normalized : Vector3.forward;
        }

        /// <summary>Точка реки у края карты (там река уходит за доску), а не внутри (впадает в другую реку).</summary>
        static bool AtMapEdge(float x, float y) => x < 0.03f || x > 0.97f || y < 0.03f || y > 0.97f;

        /// <summary>
        /// Не строить здесь набережную реки self: точка в воде другой реки (или в её набережной). Набережная притока
        /// обрывается у набережной главной реки, а в набережной главной вырезается ровно ширина воды притока —
        /// углы устья смыкаются без щелей. На пересечении равных рек — по воде и половине набережной.
        /// </summary>
        bool InOtherRiver(int self, Vector3 point)
        {
            var p = new Vector2(point.x, point.z);
            float half = RiverWidth / 2f;
            bool selfTributary = self < _riverIsTributary.Count && _riverIsTributary[self];
            for (int r = 0; r < _riverLines.Count; r++)
            {
                if (r == self) continue;
                bool otherTributary = r < _riverIsTributary.Count && _riverIsTributary[r];
                float radius = otherTributary && !selfTributary ? half : half + BankWidth * 0.5f;
                float limit = radius * radius;
                var line = _riverLines[r];
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    var a = new Vector2(line[i].x, line[i].z);
                    var ab = new Vector2(line[i + 1].x, line[i + 1].z) - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                    if ((p - (a + ab * t)).sqrMagnitude < limit) return true;
                }
            }
            return false;
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
            var plinth = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Granite);
            foreach (var building in plan.Buildings)
            {
                local.Clear();
                var random = new System.Random(building.Seed);
                float w = building.Width / CityPlanner.PixelsPerUnit, d = building.Depth / CityPlanner.PixelsPerUnit;
                CityShapes.Build(local, building.Kind, w, d, building.Height, random);
                // На склоне дом стоит на высоте верхнего угла, а под нижним — цоколь до земли.
                var rotation = Quaternion.Euler(0f, building.Angle, 0f);
                var at = CityPlanner.ToWorld(building.Center);
                float low = at.y, high = at.y;
                for (int k = 0; k < 4; k++)
                {
                    var corner = at + rotation * new Vector3((k & 1) == 0 ? -w / 2f : w / 2f, 0f, (k & 2) == 0 ? -d / 2f : d / 2f);
                    float h = Relief.At(corner.x, corner.z);
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
                if (high - low > 0.002f)
                    local.Box(0f, 0f, w * 0.98f, d * 0.98f, low - high - 0.004f, high - low + 0.004f, plinth, plinth);
                at.y = high;
                For(building.District).Append(local, Matrix4x4.TRS(at, rotation, Vector3.one));
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
                var center = CityPlanner.ToWorld(decal.Center, 0f);
                center.y = DecalHeight;   // высота над землёй; рельеф добавит Drape
                float hw = decal.Width / CityPlanner.PixelsPerUnit / 2f, hd = decal.Depth / CityPlanner.PixelsPerUnit / 2f;
                var rotation = Quaternion.Euler(0f, decal.Angle, 0f);
                var uv = CityTextures.DecalUv(decal.Kind);
                // На рельефе декаль — сетка до 6 × 6 клеток по 6 см, каждая вершина ляжет на землю.
                int nx = Mathf.Clamp(Mathf.CeilToInt(hw * 2f / 0.06f), 1, 6), nz = Mathf.Clamp(Mathf.CeilToInt(hd * 2f / 0.06f), 1, 6);
                for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float u0 = ix / (float)nx, u1 = (ix + 1) / (float)nx, v0 = iz / (float)nz, v1 = (iz + 1) / (float)nz;
                    Vector3 P(float u, float v) => center + rotation * new Vector3(Mathf.Lerp(-hw, hw, u), 0f, Mathf.Lerp(-hd, hd, v));
                    // Порядок углов (юго-запад, северо-запад, северо-восток, юго-восток) — грань смотрит вверх.
                    b.Quad(P(u0, v0), P(u0, v1), P(u1, v1), P(u1, v0),
                        new Rect(uv.x + uv.width * u0, uv.y + uv.height * v0, uv.width * (u1 - u0), uv.height * (v1 - v0)));
                }
            }
            foreach (var pair in builders)
            {
                pair.Value.Drape(Relief.At);
                var go = AddMesh("Decals " + pair.Key, pair.Value.ToMesh("City3D Decals " + pair.Key), _decals, _root.transform, false);
                if (pair.Key != "_") _districts[pair.Key + "#decals"] = go;
            }
        }

        /// <summary>
        /// Мелочи города одним мешем на район: фонари (столб, плечо над дорогой, плафон), кусты-шары и лодки
        /// (корпус, рубка) на воде. Закрытые районы прячут и их.
        /// </summary>
        void BuildProps(CityPlanner.Plan plan)
        {
            var builders = new Dictionary<string, MeshBuilder>();
            var local = new MeshBuilder();
            var metal = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
            var lampHead = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
            var white = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            var red = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofRed);
            var wood = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBrown);
            foreach (var prop in plan.Props)
            {
                local.Clear();
                var random = new System.Random(prop.Seed);
                float y = 0f;
                switch (prop.Kind)
                {
                    case CityPlanner.PropKind.Lamp when plan.Style == CityPlanner.CityStyle.Chinese:
                    {
                        // Чунцин: тёмно-красный столб с перекладиной и два красных фонаря-бочонка.
                        var lacquer = CityAtlas.Solid(CityAtlas.Row.Style, CityAtlas.Lacquer);
                        var dark = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
                        local.Prism(0f, 0f, 0.0011f, 0f, 0.028f, 5, dark, dark);
                        local.Beam(new Vector3(-0.005f, 0.027f, 0f), new Vector3(0.008f, 0.027f, 0f), 0.001f, 0.001f, dark, dark);
                        foreach (float x in new[] { -0.004f, 0.0068f })
                        {
                            local.Prism(x, 0f, 0.0016f, 0.0205f, 0.0045f, 6, lacquer, lampHead, 0.0012f);
                            local.Prism(x, 0f, 0.0004f, 0.025f, 0.002f, 4, dark, dark);
                        }
                        break;
                    }
                    case CityPlanner.PropKind.Lamp when plan.Style == CityPlanner.CityStyle.European:
                    {
                        // Амстердам: чугунный столб без плеча и фонарь-«домик» наверху.
                        var iron = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
                        local.Prism(0f, 0f, 0.0013f, 0f, 0.003f, 6, iron, iron);
                        local.Prism(0f, 0f, 0.0008f, 0.003f, 0.022f, 6, iron, iron);
                        local.Prism(0f, 0f, 0.0018f, 0.025f, 0.0035f, 4, lampHead, iron, 0.0024f);
                        local.Cone(0f, 0f, 0.0026f, 0.0285f, 0.002f, 4, iron);
                        break;
                    }
                    case CityPlanner.PropKind.Lamp:
                        // Столб 3 см, плечо 6 мм к дороге, плафон на конце.
                        local.Prism(0f, 0f, 0.0011f, 0f, 0.03f, 5, metal, metal);
                        local.Beam(new Vector3(0f, 0.029f, 0f), new Vector3(0.007f, 0.03f, 0f), 0.0012f, 0.0012f, metal, metal);
                        local.Box(0.0075f, 0f, 0.004f, 0.0022f, 0.0285f, 0.0012f, lampHead, lampHead);
                        break;
                    case CityPlanner.PropKind.Bush:
                    {
                        var leaf = CityAtlas.Solid(CityAtlas.Row.Nature, random.Next(4));
                        float r = prop.Size;
                        local.Blob(new Vector3(0f, r * 0.7f, 0f), r, r * 0.75f, leaf);
                        if (random.NextDouble() < 0.5)
                            local.Blob(new Vector3(r * 0.9f, r * 0.55f, r * 0.2f), r * 0.7f, r * 0.55f, leaf);
                        break;
                    }
                    case CityPlanner.PropKind.Boat:
                    {
                        // Корпус с заострённым носом (+X), рубка; цвет — белый или красный.
                        y = WaterLevel - 0.001f;
                        var hull = random.NextDouble() < 0.6 ? white : red;
                        local.Box(-0.004f, 0f, 0.022f, 0.009f, 0f, 0.004f, hull, wood);
                        local.Quad(new Vector3(0.007f, 0f, -0.0045f), new Vector3(0.007f, 0.004f, -0.0045f),
                            new Vector3(0.015f, 0.004f, 0f), new Vector3(0.015f, 0f, 0f), hull);
                        local.Quad(new Vector3(0.015f, 0f, 0f), new Vector3(0.015f, 0.004f, 0f),
                            new Vector3(0.007f, 0.004f, 0.0045f), new Vector3(0.007f, 0f, 0.0045f), hull);
                        local.Tri(new Vector3(0.007f, 0.004f, -0.0045f), new Vector3(0.007f, 0.004f, 0.0045f), new Vector3(0.015f, 0.004f, 0f), wood);
                        local.Box(-0.006f, 0f, 0.008f, 0.006f, 0.004f, 0.004f, white, lampHead);
                        break;
                    }
                }
                string key = prop.District ?? "_";
                if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                b.Append(local, Matrix4x4.TRS(CityPlanner.ToWorld(prop.At, y), Quaternion.Euler(0f, prop.Angle, 0f), Vector3.one));
            }
            foreach (var pair in builders)
            {
                if (pair.Value.VertexCount == 0) continue;
                var go = AddMesh("Props " + pair.Key, pair.Value.ToMesh("City3D Props " + pair.Key), _buildings, _root.transform, true);
                if (pair.Key != "_") _districts[pair.Key + "#props"] = go;
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
                string key = district + "#" + cx + "," + cz + (tree.CardOnly ? "#far" : "");
                if (!chunks.TryGetValue(key, out var chunk))
                {
                    chunk = new TreeChunk { District = district, Center = new Vector3((cx + 0.5f) * TreeChunkSize, 0f, (cz + 0.5f) * TreeChunkSize) };
                    chunks[key] = chunk;
                    builders[key] = new MeshBuilder();
                }
                if (!tree.CardOnly) CityShapes.BuildTree(builders[key], tree.Kind, at, tree.Size, new System.Random(tree.Seed), false);
                chunk.Spots.Add(new CardSpot { At = at, Size = tree.Size, Tile = CardTile(tree) });
            }
            foreach (var pair in chunks)
            {
                var chunk = pair.Value;
                chunk.Root = new GameObject("Trees " + pair.Key);
                chunk.Root.transform.SetParent(_root.transform, false);
                // Дальние окрестности — только карточки: туда камера близко не подлетает.
                if (builders[pair.Key].VertexCount > 0)
                    chunk.Full = AddMesh("Trees 3D", builders[pair.Key].ToMesh("City3D Trees " + pair.Key), _buildings, chunk.Root.transform, true);
                chunk.CardMesh = new Mesh { name = "City3D Tree Cards " + pair.Key, hideFlags = HideFlags.DontSave };
                chunk.Cards = AddMesh("Tree cards", chunk.CardMesh, _cards, chunk.Root.transform, true);
                chunk.Cards.SetActive(chunk.Full == null);
                chunk.ShowCards = chunk.Full == null;
                _treeChunks.Add(chunk);
            }
            foreach (var chunk in _treeChunks)
                RebuildCards(chunk, 0f, 0f);
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
        void RebuildCards(TreeChunk chunk, float leanDegrees, float yawDegrees)
        {
            chunk.CardLean = leanDegrees;
            chunk.CardYaw = yawDegrees;
            float t = leanDegrees * Mathf.Deg2Rad;
            // Карточки повёрнуты к камере и по наклону, и по повороту вокруг вертикали.
            var yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            var toCamera = yaw * new Vector3(0f, Mathf.Cos(t), -Mathf.Sin(t));
            var cardUp = yaw * new Vector3(0f, Mathf.Sin(t), Mathf.Cos(t));
            var cardRight = yaw * Vector3.right;
            // Нормаль почти вверх — карточка освещается как крона, а не как стена.
            var normal = (Vector3.up + toCamera * 0.4f).normalized;
            if (chunk.CardBuilder == null)
                chunk.CardBuilder = new MeshBuilder(chunk.Spots.Count);
            var builder = chunk.CardBuilder;
            builder.Clear();
            foreach (var spot in chunk.Spots)
                {
                    float size = spot.Size * 1.05f, half = size / 2f;
                    var center = spot.At + Vector3.up * half;
                    var right = cardRight * half;
                    var up = cardUp * half;
                    builder.QuadWithNormal(center - right - up, center - right + up, center + right + up, center + right - up,
                        CityTextures.CardUv(spot.Tile), normal);
                }
            builder.WriteTo(chunk.CardMesh);
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

        // ================================================================== Хабы магната

        readonly Dictionary<string, (GameObject go, int level)> _hubs = new Dictionary<string, (GameObject, int)>();

        /// <summary>
        /// Комплекс хаба по уровню (вид: 0 — аэропорт, 1 — вокзал, 2 — порт): 1 — терминал, 2 — склады,
        /// 3 — кран (у порта — портальный), 4 — расширенная площадка и ещё склады, 5 — большой комплекс
        /// (у аэропорта — с вертолётной площадкой). Стоит в самом широком промежутке между дорогами станции,
        /// за перекрёстком. Пересобирается только при смене уровня.
        /// </summary>
        public void SetHub(string locationId, int kind, int level)
        {
            if (_hubs.TryGetValue(locationId, out var old))
            {
                if (old.level == level) return;
                if (old.go != null) Destroy(old.go);
            }
            var location = _map.GetLocation(locationId);
            if (location == null) return;
            // Место выбрано при сборке (HubPlacement: в стороне от всех дорог, рек и станций), рельеф под ним ровный.
            // Хаб, которого не было при сборке, ищет место так же, но без выравнивания земли.
            bool planned = _hubPlaces.TryGetValue(locationId, out var place);
            if (!planned)
            {
                var others = _hubPlaces.Where(p => p.Key != locationId).Select(p => p.Key).ToList();
                others.Add(locationId);
                place = HubPlacement.Plan(_map, others, RiverWidth / 2f + BankWidth)[locationId];
            }
            float yaw = place.Yaw;
            var origin = new Vector3(place.Origin.x, 0f, place.Origin.y);
            float groundLow = float.MaxValue, groundHigh = float.MinValue;
            {
                var turn = Quaternion.Euler(0f, yaw, 0f);
                for (int gz = 0; gz <= 4; gz++)
                for (int gx = 0; gx <= 4; gx++)
                {
                    var q = origin + turn * new Vector3(Mathf.Lerp(-HubPlacement.HalfX, HubPlacement.HalfX, gx / 4f), 0f,
                        Mathf.Lerp(HubPlacement.ZMin, HubPlacement.ZMax, gz / 4f));
                    float h = Relief.At(q.x, q.z);
                    groundLow = Mathf.Min(groundLow, h);
                    groundHigh = Mathf.Max(groundHigh, h);
                }
            }
            // Ровная площадка — по центру; без выравнивания (хаб появился позже) — по самой высокой точке.
            origin.y = planned ? Relief.At(origin.x, origin.z) : groundHigh;

            var builder = new MeshBuilder();
            var local = new MeshBuilder();
            var random = new System.Random(locationId.GetHashCode());
            var asphalt = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Asphalt);
            var yellow = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire);
            var white = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            var red = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.ChimneyStripe);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            void Put(CityShapes.Kind shape, float x, float z, float w, float d, float h, float angle = 0f)
            {
                local.Clear();
                CityShapes.Build(local, shape, w, d, h, random);
                builder.Append(local, Matrix4x4.TRS(origin + rotation * new Vector3(x, 0f, z), rotation * Quaternion.Euler(0f, angle, 0f), Vector3.one));
            }
            Vector3 P(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);

            // Площадка: с уровнем растёт (уровень 4 — расширенная территория).
            float pad = level >= 4 ? 0.13f : level >= 2 ? 0.09f : 0.06f;
            builder.Quad(P(-pad, 0.0035f, -pad), P(-pad, 0.0035f, pad), P(pad, 0.0035f, pad), P(pad, 0.0035f, -pad), asphalt);
            if (groundHigh - groundLow > 0.003f)
            {
                local.Clear();
                if (planned)
                {
                    // Земля выровнена; если у края (рядом с дорогой) она всё же ниже — только асфальтовый бортик
                    // площадки, без большой плиты.
                    local.Box(0f, 0f, pad * 2f, pad * 2f, groundLow - origin.y - 0.004f, origin.y - groundLow + 0.0075f, asphalt, asphalt);
                }
                else
                {
                    var concrete = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeConcrete);
                    local.Box(0f, 0.01f, 0.4f, 0.28f, groundLow - groundHigh - 0.006f, groundHigh - groundLow + 0.009f, concrete, concrete);
                }
                builder.Append(local, Matrix4x4.TRS(origin, rotation, Vector3.one));
            }
            // Подъезд от станции к площадке, если комплекс отодвинут от неё (рядом тесно): асфальт по рельефу.
            {
                var station = _map.GetLocation(locationId);
                var from = HubPlacement.World(station.X, station.Y);
                var forward2 = place.Forward;
                var start = from + forward2 * 0.08f;
                var end = place.Origin + forward2 * -pad;
                float length = Vector2.Dot(end - start, forward2);
                if (length > 0.02f)
                {
                    var side = place.Right * 0.018f;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.03f));
                    Vector3 V(Vector2 q) => new Vector3(q.x, Relief.At(q.x, q.y) + 0.0032f, q.y);
                    for (int i = 0; i < steps; i++)
                    {
                        var a = start + forward2 * (length * i / steps);
                        var b = start + forward2 * (length * (i + 1) / steps);
                        builder.Quad(V(a - side), V(b - side), V(b + side), V(a + side), asphalt);
                    }
                }
            }
            // 1: у вокзала — здание вокзала с путями и платформами, у аэропорта — аэровокзал, вышка, перрон
            // с самолётами и взлётная полоса, у порта — терминал-склад.
            // Если скачаны модели из Scenario — главное здание модельное, а пути, перрон и полоса остаются свои.
            string terminalModel = kind == 0 ? Models3D.AirportTerminal : kind == 1 ? Models3D.StationTerminal : Models3D.PortTerminal;
            bool modelTerminal = Models3D.Has(terminalModel);
            var site = new MeshBuilder();
            if (kind == 1) BuildRailwayStation(site, random, !modelTerminal, level);
            else if (kind == 0) BuildAirport(site, random, !modelTerminal, level);
            else
            {
                if (!modelTerminal) Put(CityShapes.Kind.Warehouse, 0f, -0.02f, 0.05f, 0.035f, 0.04f);
                BuildPortYard(site, random, level);
            }
            if (site.VertexCount > 0) builder.Append(site, Matrix4x4.TRS(origin, rotation, Vector3.one));
            // Склады: у вокзала и аэропорта — по бокам от главного здания, у порта — за терминалом.
            float storeX = kind == 2 ? 0.055f : 0.1f, storeZ = kind == 2 ? 0.035f : -0.035f;
            // 2: склады.
            if (level >= 2)
            {
                Put(CityShapes.Kind.Warehouse, -storeX, storeZ, 0.045f, 0.025f, 0.028f);
                Put(CityShapes.Kind.Warehouse, storeX, storeZ, 0.045f, 0.025f, 0.028f);
            }
            // 3: кран — портальный у порта, башенный у остальных.
            if (level >= 3)
            {
                if (kind == 2)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        builder.Beam(P(-0.03f, 0f, 0.06f * s), P(-0.03f, 0.075f, 0.06f * s), 0.006f, 0.006f, yellow, yellow);
                        builder.Beam(P(0.03f, 0f, 0.06f * s), P(0.03f, 0.075f, 0.06f * s), 0.006f, 0.006f, yellow, yellow);
                    }
                    builder.Beam(P(-0.03f, 0.075f, -0.09f), P(-0.03f, 0.075f, 0.09f), 0.007f, 0.007f, yellow, yellow);
                    builder.Beam(P(0.03f, 0.075f, -0.09f), P(0.03f, 0.075f, 0.09f), 0.007f, 0.007f, yellow, yellow);
                }
                else if (Models3D.Has(Models3D.TowerCrane)) { /* модельный кран ставится после сборки меша */ }
                else
                {
                    // У аэропорта справа вышка — кран ставим слева.
                    float m = kind == 0 ? -1f : 1f;
                    builder.Beam(P(0.07f * m, 0f, -0.05f), P(0.07f * m, 0.11f, -0.05f), 0.006f, 0.006f, yellow, yellow);
                    builder.Beam(P(0.03f * m, 0.11f, -0.05f), P(0.13f * m, 0.11f, -0.05f), 0.005f, 0.005f, yellow, yellow);
                    builder.Beam(P(0.12f * m, 0.11f, -0.05f), P(0.12f * m, 0.06f, -0.05f), 0.0015f, 0.0015f, white, white);
                }
            }
            // 4: ещё склады на расширенной площадке.
            if (level >= 4)
            {
                Put(CityShapes.Kind.Warehouse, -0.06f, -0.075f, 0.05f, 0.03f, 0.03f, 90f);
                Put(CityShapes.Kind.Sawtooth, 0.07f, -0.085f, 0.05f, 0.035f, 0.035f);
            }
            // 5: большой логистический комплекс; у аэропорта — вертолётная площадка на крыше рядом.
            if (level >= 5)
            {
                // За путями и взлётной полосой места нет — комплекс встаёт сбоку.
                if (kind == 2) Put(CityShapes.Kind.Block, 0f, 0.09f, 0.07f, 0.04f, 0.08f);
                else Put(kind == 0 ? CityShapes.Kind.Stepped : CityShapes.Kind.Block, -0.17f, -0.02f, 0.05f, 0.04f, 0.08f);
                if (kind == 0)
                {
                    var c = P(0.17f, 0.0045f, -0.02f);
                    const int sides = 16;
                    for (int i = 0; i < sides; i++)
                    {
                        float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                        var p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.03f;
                        var p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.03f;
                        if (Vector3.Cross(p0 - c, p1 - c).y < 0f) builder.Tri(c, p1, p0, red); else builder.Tri(c, p0, p1, red);
                    }
                    var hy = Vector3.up * 0.0052f;
                    builder.Beam(c + rotation * new Vector3(-0.01f, 0f, -0.013f) + hy, c + rotation * new Vector3(-0.01f, 0f, 0.013f) + hy, 0.004f, 0.0005f, white, white);
                    builder.Beam(c + rotation * new Vector3(0.01f, 0f, -0.013f) + hy, c + rotation * new Vector3(0.01f, 0f, 0.013f) + hy, 0.004f, 0.0005f, white, white);
                    builder.Beam(c + rotation * new Vector3(-0.01f, 0f, 0f) + hy, c + rotation * new Vector3(0.01f, 0f, 0f) + hy, 0.004f, 0.0005f, white, white);
                }
            }
            var go = AddMesh("Hub " + locationId, builder.ToMesh("City3D Hub " + locationId), _buildings, _root.transform, true);
            PlaceHubModels(go.transform, kind, level, origin, rotation, yaw, random, modelTerminal ? terminalModel : null);
            _hubSpots[locationId] = (origin, rotation);
            _hubs[locationId] = (go, level);
        }

        /// <summary>
        /// Вокзал у станции в локальных координатах комплекса (−Z — к станции): здание вокзала фасадом к дороге,
        /// за ним три пути на щебёнке, две платформы с навесами на столбиках.
        /// </summary>
        static void BuildRailwayStation(MeshBuilder b, System.Random random, bool building = true, int level = 1)
        {
            var local = new MeshBuilder();
            if (building)
            {
                CityShapes.Build(local, CityShapes.Kind.Station, 0.11f, 0.042f, 0.034f, random);
                b.Append(local, Matrix4x4.Translate(new Vector3(0f, 0f, -0.035f)));
            }
            var ballast = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Granite);
            var rail = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
            var concrete = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeConcrete);
            var canopy = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofMetal);
            const float y = 0.004f, half = 0.13f;
            b.Quad(new Vector3(-half, y, -0.006f), new Vector3(-half, y, 0.078f), new Vector3(half, y, 0.078f), new Vector3(half, y, -0.006f), ballast);
            foreach (float z in new[] { 0.008f, 0.036f, 0.064f })
            {
                b.Beam(new Vector3(-half, y + 0.0008f, z - 0.0035f), new Vector3(half, y + 0.0008f, z - 0.0035f), 0.0012f, 0.0016f, rail, rail);
                b.Beam(new Vector3(-half, y + 0.0008f, z + 0.0035f), new Vector3(half, y + 0.0008f, z + 0.0035f), 0.0012f, 0.0016f, rail, rail);
            }
            foreach (float z in new[] { 0.022f, 0.05f })
            {
                b.Box(0f, z, 0.2f, 0.011f, y, 0.004f, concrete, concrete);
                b.Box(0f, z, 0.16f, 0.016f, 0.017f, 0.0025f, canopy, canopy);
                for (int i = -3; i <= 3; i++)
                    b.Box(i * 0.025f, z, 0.0018f, 0.0018f, y, 0.013f, concrete, concrete);
            }
            // Светофоры путей на обоих концах.
            var dark = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofDark);
            var redLamp = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofRed);
            foreach (float x in new[] { -half + 0.006f, half - 0.006f })
            {
                b.Box(x, 0.074f, 0.0012f, 0.0012f, y, 0.022f, rail, rail);
                b.Box(x, 0.074f, 0.003f, 0.003f, y + 0.018f, 0.006f, dark, redLamp);
            }
            // Уровень 2: грузовой состав на дальнем пути — тепловоз и цветные вагоны.
            if (level >= 2) Train(b, 0.064f, -0.115f, 6, false, random);
            // Уровень 3: пассажирский поезд у ближней платформы.
            if (level >= 3) Train(b, 0.008f, -0.09f, 5, true, random);
        }

        /// <summary>Поезд вдоль оси X на пути z: локомотив и cars вагонов от x0; пассажирский — длинные белые вагоны с окнами.</summary>
        static void Train(MeshBuilder b, float z, float x0, int cars, bool passenger, System.Random random)
        {
            const float y = 0.0045f;
            var loco = CityAtlas.Solid(CityAtlas.Row.Roof, passenger ? CityAtlas.RoofRed : CityAtlas.RoofCopper);
            var roof = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofDark);
            var windows = CityAtlas.Uv(CityAtlas.Row.Tall, 1);
            float x = x0;
            b.Box(x + 0.012f, z, 0.024f, 0.0085f, y, 0.0095f, loco, roof);
            x += 0.026f;
            var colors = new[] { CityAtlas.RoofBrown, CityAtlas.RoofMetal, CityAtlas.RoofRed, CityAtlas.RoofCopper, CityAtlas.RoofLight };
            for (int i = 0; i < cars; i++)
            {
                float length = passenger ? 0.03f : 0.022f;
                var side = passenger ? windows : CityAtlas.Solid(CityAtlas.Row.Roof, colors[random.Next(colors.Length)]);
                b.Box(x + length / 2f, z, length, 0.0082f, y, passenger ? 0.009f : 0.008f, side, roof);
                x += length + 0.002f;
            }
        }

        /// <summary>
        /// Двор порта: штабели контейнеров (по два в высоту) по бокам площадки и погрузчики; с уровнем 3 — второй
        /// ряд штабелей.
        /// </summary>
        static void BuildPortYard(MeshBuilder b, System.Random random, int level)
        {
            if (level < 2) return;
            var colors = new[] { CityAtlas.RoofRed, CityAtlas.RoofCopper, CityAtlas.RoofBrown, CityAtlas.RoofMetal, CityAtlas.RoofLight };
            var top = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofMetal);
            int rows = level >= 3 ? 2 : 1;
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < rows; row++)
            for (int i = 0; i < 3; i++)
            for (int tier = 0; tier < 2; tier++)
            {
                if (tier == 1 && random.NextDouble() < 0.3) continue;
                var paint = CityAtlas.Solid(CityAtlas.Row.Roof, colors[random.Next(colors.Length)]);
                b.Box(side * (0.112f + row * 0.013f), -0.012f + i * 0.026f, 0.011f, 0.024f, 0.0035f + tier * 0.0105f, 0.01f, paint, top);
            }
            var yellow = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire);
            for (int k = 0; k < 2; k++)
                b.Box(k == 0 ? -0.09f : 0.09f, 0.075f, 0.007f, 0.01f, 0.0035f, 0.006f, yellow, yellow);
        }

        /// <summary>
        /// Аэропорт в локальных координатах комплекса (−Z — к станции): аэровокзал фасадом к дороге, диспетчерская
        /// вышка, перрон с двумя самолётами и взлётно-посадочная полоса с разметкой.
        /// </summary>
        static void BuildAirport(MeshBuilder b, System.Random random, bool buildings = true, int level = 1)
        {
            var local = new MeshBuilder();
            if (buildings)
            {
                CityShapes.Build(local, CityShapes.Kind.Terminal, 0.12f, 0.03f, 0.022f, random);
                b.Append(local, Matrix4x4.Translate(new Vector3(0f, 0f, -0.04f)));
                local.Clear();
                CityShapes.Build(local, CityShapes.Kind.ControlTower, 0.018f, 0.018f, 0.1f, random);
                b.Append(local, Matrix4x4.Translate(new Vector3(0.085f, 0f, -0.03f)));
            }

            var asphalt = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Asphalt);
            var white = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            var tail = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.ChimneyStripe);
            void Slab(float x0, float z0, float x1, float z1, float y, Rect uv) =>
                b.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), uv);

            // Перрон, рулёжка и полоса: тёмный асфальт, белая осевая пунктиром и «зебры» на торцах.
            Slab(-0.1f, -0.012f, 0.1f, 0.062f, 0.0042f, asphalt);
            Slab(-0.012f, 0.062f, 0.012f, 0.088f, 0.0042f, asphalt);
            const float runwayZ = 0.1f, runwayHalf = 0.016f, runwayLength = 0.17f;
            Slab(-runwayLength, runwayZ - runwayHalf, runwayLength, runwayZ + runwayHalf, 0.0044f, asphalt);
            for (float x = -runwayLength + 0.03f; x < runwayLength - 0.03f; x += 0.022f)
                Slab(x, runwayZ - 0.0012f, x + 0.012f, runwayZ + 0.0012f, 0.0047f, white);
            for (int s = -1; s <= 1; s += 2)
                for (int k = -2; k <= 2; k++)
                {
                    float x = s * (runwayLength - 0.012f), z = runwayZ + k * 0.0055f;
                    Slab(x - 0.007f, z - 0.0016f, x + 0.007f, z + 0.0016f, 0.0047f, white);
                }

            // Самолёты на перроне: фюзеляж, крылья, стабилизатор и киль (носом к терминалу).
            void Plane(float x, float z, float yaw)
            {
                var plane = new MeshBuilder();
                const float y = 0.0075f;
                plane.Beam(new Vector3(0f, y, -0.02f), new Vector3(0f, y, 0.02f), 0.0055f, 0.0055f, white, white);
                plane.Box(0f, 0.002f, 0.044f, 0.009f, y - 0.0008f, 0.0014f, white, white);
                plane.Box(0f, 0.017f, 0.016f, 0.004f, y + 0.0005f, 0.0011f, white, white);
                plane.Box(0f, 0.018f, 0.0013f, 0.006f, y + 0.002f, 0.007f, tail, tail);
                b.Append(plane, Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            }
            Plane(-0.05f, 0.03f, (float)(random.NextDouble() * 10.0 - 5.0));
            Plane(0.035f, 0.034f, 180f + (float)(random.NextDouble() * 10.0 - 5.0));

            // Перронная техника: багажные тележки и топливозаправщик.
            var cart = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire);
            foreach (var (x, z) in new[] { (-0.025f, 0.012f), (-0.019f, 0.012f), (0.06f, 0.05f) })
                b.Box(x, z, 0.004f, 0.006f, 0.0042f, 0.003f, cart, cart);
            b.Box(0.012f, 0.05f, 0.006f, 0.014f, 0.0042f, 0.005f, white, white);
            // Уровень 2: ангар и топливные баки сбоку от перрона.
            if (level >= 2)
            {
                var hangar = new MeshBuilder();
                CityShapes.Build(hangar, CityShapes.Kind.Hangar, 0.05f, 0.04f, 0.026f, random);
                b.Append(hangar, Matrix4x4.Translate(new Vector3(-0.145f, 0f, 0.035f)));
                var tank = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
                foreach (float z in new[] { 0.03f, 0.056f })
                    b.Prism(0.138f, z, 0.011f, 0.0042f, 0.012f, 10, tank, tank);
            }
            // Уровень 3: самолёт выруливает к полосе.
            if (level >= 3) Plane(0f, 0.07f, 180f);
        }

        /// <summary>
        /// Модели из Scenario у хаба (если скачаны): терминал вида хаба фасадом к станции, башенный кран с уровня 3
        /// (не у порта — там свой портальный) и две фуры у складов. Всё — дочерние объекты меша хаба: при смене
        /// уровня удаляются вместе с ним.
        /// </summary>
        static void PlaceHubModels(Transform hub, int kind, int level, Vector3 origin, Quaternion rotation, float yaw,
            System.Random random, string terminalModel)
        {
            Vector3 P(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);
            const float ground = 0.0045f;
            // Модели смотрят фасадом на +Z (как в glTF), а к станции у комплекса −Z: разворот на 180°.
            if (terminalModel != null)
            {
                if (kind == 1) Models3D.Instantiate(terminalModel, hub, P(0f, ground, -0.05f), yaw + 180f, 0.125f);
                else if (kind == 0) Models3D.Instantiate(terminalModel, hub, P(0f, ground, -0.045f), yaw + 180f, 0.14f);
                else Models3D.Instantiate(terminalModel, hub, P(0f, ground, -0.005f), yaw + 180f, 0.13f);
            }
            if (level >= 3 && kind != 2 && Models3D.Has(Models3D.TowerCrane))
            {
                float m = kind == 0 ? -1f : 1f;
                Models3D.Instantiate(Models3D.TowerCrane, hub, P(0.075f * m, ground, -0.05f), yaw + (float)(random.NextDouble() * 360.0), 0.12f, true);
            }
            if (Models3D.Has(Models3D.Truck))
            {
                // Фуры у складов, вдоль оси «к станции» — на погрузке.
                float along = Models3D.LongAlongX(Models3D.Truck) ? 90f : 0f;
                Models3D.Instantiate(Models3D.Truck, hub, P(-0.11f, ground, -0.066f), yaw + along, 0.045f);
                if (level >= 2) Models3D.Instantiate(Models3D.Truck, hub, P(0.11f, ground, -0.07f), yaw + along + 180f, 0.045f);
            }
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

        /// <summary>Верх перекрёстка — чуть выше дороги (0,006), чтобы стык не мерцал.</summary>
        const float JunctionTop = 0.0062f;
        /// <summary>Радиус скругления бордюра между соседними дорогами на перекрёстке.</summary>
        const float JunctionFillet = 0.06f;

        /// <summary>
        /// Каждая станция — перекрёсток: асфальтовый пятачок, рукава по всем дорогам станции длиной
        /// BoardLayout.JunctionArm, скругления бордюра между соседними рукавами и «зебра» на краю каждого рукава.
        /// Дороги (3D и интерфейса) начинаются у края рукава и вливаются в перекрёсток без щелей и острых углов.
        /// Перекрёстки районов-расширений прячутся вместе с районом.
        /// </summary>
        void BuildJunctions()
        {
            var asphalt = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Asphalt);
            var paint = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            var builders = new Dictionary<string, MeshBuilder>();
            var neighbours = new List<Vector2>();
            var dirs = new List<Vector2>();
            var corners = new List<JunctionGeometry.Corner>();
            float half = RoadWidth / 2f, arm = BoardLayout.JunctionArm;
            Vector3 W(Vector2 p, float y) => new Vector3(p.x, y, p.y);
            var lightBuilders = new Dictionary<string, MeshBuilder>();
            var lightLocal = new MeshBuilder();
            var steel = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
            var dark = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofDark);
            var red = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofRed);
            var greenLamp = CityAtlas.Solid(CityAtlas.Row.Nature, 1);

            foreach (var location in _map.Locations)
            {
                var c3 = BoardLayout.World(location.X, location.Y);
                var center = new Vector2(c3.x, c3.z);
                neighbours.Clear();
                foreach (var road in _map.Roads)
                {
                    string other = road.A == location.Id ? road.B : road.B == location.Id ? road.A : null;
                    var target = other == null ? null : _map.GetLocation(other);
                    if (target == null) continue;
                    var w = BoardLayout.World(target.X, target.Y);
                    neighbours.Add(new Vector2(w.x, w.z));
                }
                JunctionGeometry.Directions(center, neighbours, dirs);
                if (dirs.Count == 0) continue;

                string key = _map.GetDistrict(location.DistrictId)?.IsExpansion == true ? location.DistrictId : "_";
                if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();

                // Пятачок в центре — круг по ширине дороги: заполняет промежутки между рукавами.
                const int sides = 18;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = Mathf.PI * 2f * s / sides, a1 = Mathf.PI * 2f * (s + 1) / sides;
                    TriUp(b, W(center, JunctionTop), W(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * half, JunctionTop),
                        W(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * half, JunctionTop), asphalt);
                }
                // Рукава и «зебра» на краю каждого рукава (полосы вдоль дороги, поперёк — пять штук).
                foreach (var d in dirs)
                {
                    var n = new Vector2(-d.y, d.x);
                    QuadUp(b, W(center - n * half, JunctionTop), W(center + n * half, JunctionTop),
                        W(center + n * half + d * arm, JunctionTop), W(center - n * half + d * arm, JunctionTop), asphalt);
                    const int stripes = 5;
                    float stripe = RoadWidth / (stripes * 2f - 1f);
                    float near = arm - 0.036f, far = arm - 0.014f, y = JunctionTop + 0.0004f;
                    for (int k = 0; k < stripes; k++)
                    {
                        float x0 = -half + stripe * 2f * k, x1 = x0 + stripe;
                        QuadUp(b, W(center + n * x0 + d * near, y), W(center + n * x1 + d * near, y),
                            W(center + n * x1 + d * far, y), W(center + n * x0 + d * far, y), paint);
                    }
                }
                // Светофоры на перекрёстках с тремя и больше дорогами: справа от въезда, лицом к машинам.
                // Половина въездов горит зелёным, половина — красным.
                if (dirs.Count >= 3)
                {
                    if (!lightBuilders.TryGetValue(key, out var lights)) lightBuilders[key] = lights = new MeshBuilder();
                    for (int k = 0; k < dirs.Count; k++)
                    {
                        var d = dirs[k];
                        var n = new Vector2(-d.y, d.x);
                        var foot = center + d * (arm * 0.8f) + n * (half + 0.01f);
                        lightLocal.Clear();
                        lightLocal.Prism(0f, 0f, 0.0009f, 0f, 0.028f, 5, steel, steel);
                        lightLocal.Box(0f, 0f, 0.004f, 0.003f, 0.02f, 0.011f, dark, dark);
                        bool green = k % 2 == 0;
                        lightLocal.Box(0f, 0.0016f, 0.0026f, 0.0006f, 0.0273f, 0.0024f, green ? dark : red, green ? dark : red);
                        lightLocal.Box(0f, 0.0016f, 0.0026f, 0.0006f, 0.0242f, 0.0024f, dark, dark);
                        lightLocal.Box(0f, 0.0016f, 0.0026f, 0.0006f, 0.0211f, 0.0024f, green ? greenLamp : dark, green ? greenLamp : dark);
                        var at = new Vector3(foot.x, Relief.At(foot.x, foot.y), foot.y);
                        lights.Append(lightLocal, Matrix4x4.TRS(at, Quaternion.LookRotation(new Vector3(d.x, 0f, d.y), Vector3.up), Vector3.one));
                    }
                }
                // Скругления бордюра между соседними рукавами.
                JunctionGeometry.Corners(center, dirs, half, arm, JunctionFillet, corners);
                foreach (var corner in corners)
                {
                    const int steps = 8;
                    var previous = corner.T1;
                    for (int j = 1; j <= steps; j++)
                    {
                        var next = JunctionGeometry.Curve(corner, j / (float)steps);
                        TriUp(b, W(corner.X, JunctionTop), W(previous, JunctionTop), W(next, JunctionTop), asphalt);
                        previous = next;
                    }
                }
            }
            foreach (var pair in builders)
            {
                if (pair.Value.VertexCount == 0) continue;
                // Перекрёсток построен на высоте 0 — ложится на землю, а вдоль дорог земля и есть высота дороги.
                pair.Value.Drape(Relief.At);
                var go = AddMesh("Junctions " + pair.Key, pair.Value.ToMesh("City3D Junctions " + pair.Key), _buildings, _root.transform, false);
                if (pair.Key != "_") _districts[pair.Key + "#junctions"] = go;
            }
            foreach (var pair in lightBuilders)
            {
                if (pair.Value.VertexCount == 0) continue;
                var go = AddMesh("Traffic lights " + pair.Key, pair.Value.ToMesh("City3D Lights " + pair.Key), _buildings, _root.transform, true);
                if (pair.Key != "_") _districts[pair.Key + "#lights"] = go;
            }
        }

        /// <summary>Треугольник лицом вверх (порядок вершин подбирается сам).</summary>
        static void TriUp(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Rect uv)
        {
            if (Vector3.Cross(p1 - p0, p2 - p0).y < 0f) b.Tri(p0, p2, p1, uv);
            else b.Tri(p0, p1, p2, uv);
        }

        static void QuadUp(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Rect uv)
        {
            TriUp(b, p0, p1, p2, uv);
            TriUp(b, p0, p2, p3, uv);
        }

        /// <summary>Дорога начинается у края рукава перекрёстка (с небольшим нахлёстом), а не в центре станции.</summary>
        static void TrimForJunctions(ref Vector3 start, ref Vector3 end)
        {
            var flat = end - start;
            flat.y = 0f;
            float length = flat.magnitude, trim = BoardLayout.JunctionArm * 0.95f;
            if (length < trim * 2.5f) return;
            // По самой прямой дороги (с наклоном): начало и конец остаются на линии между станциями.
            var a = start;
            var b = end;
            start = Vector3.Lerp(a, b, trim / length);
            end = Vector3.Lerp(a, b, 1f - trim / length);
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
                TrimForJunctions(ref start, ref end);
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
                    TrimForJunctions(ref start, ref end);
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
            float yaw = layout.CameraYaw;
            float threshold = _quality == 0 ? 125f : _quality == 1 ? 100f : 80f;
            // A single shared camera-facing vector instead of per-chunk quaternion work.
            var across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            foreach (var chunk in _treeChunks)
            {
                bool open = chunk.District == "_" || districtOpen == null || districtOpen(chunk.District);
                if (chunk.Root.activeSelf != open) chunk.Root.SetActive(open);
                if (!open) continue;

                if (chunk.Full != null)
                {
                    // A meter across the camera: keep the existing quality-dependent
                    // screen-size threshold and switching hysteresis.
                    var left = layout.WorldToScreen(chunk.Center - across * 0.5f);
                    var right = layout.WorldToScreen(chunk.Center + across * 0.5f);
                    float perUnit = (right - left).magnitude;
                    bool cards = perUnit < (chunk.ShowCards ? threshold + 10f : threshold);
                    if (cards != chunk.ShowCards || chunk.Full.activeSelf == cards)
                    {
                        chunk.ShowCards = cards;
                        chunk.Full.SetActive(!cards);
                        chunk.Cards.SetActive(cards);
                    }
                }

                // Build the billboard mesh only for visible cards. Far-only chunks,
                // hidden districts and chunks currently showing full 3D meshes no
                // longer require card mesh writes on camera movement.
                if (CityTreeCardRefreshPolicy.ShouldRefresh(chunk.Cards.activeSelf,
                    chunk.CardLean, chunk.CardYaw, lean, yaw))
                    RebuildCards(chunk, lean, yaw);
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
            // Небо за горизонтом — цвета дымки: окрестности растворяются в тумане без резкой границы.
            _camera.backgroundColor = RenderSettings.fogColor;
            RenderSettings.fogEndDistance = cameraDistance * 2.6f;

            // Окна загораются вместе с фонарями.
            _buildings.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.62f) * (l.Lamps * 1.8f));
            if (_bloom != null) _bloom.intensity.Override(Mathf.Lerp(0.3f, 1.1f, l.Lamps));
        }

        // ================================================================== Уборка

        public void Dispose()
        {
            if (ReferenceEquals(ActiveScene, this)) ActiveScene = null;
            FrameMonitor = null;
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
            CityProceduralReplacementResolver.ClearCatalog();
            _treeChunks.Clear();
            _builtRoads.Clear();
            _constructing.Clear();
            _hubs.Clear();
            ClearLife();
            Relief.Release(_relief);
            _relief = null;
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
