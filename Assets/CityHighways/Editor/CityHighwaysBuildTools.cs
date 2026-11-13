using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню «City Highways» в редакторе:
/// 1) настроить проект для сборки (название, иконка, окно, сцена);
/// 2) собрать игру для Windows в папку Builds/Windows.
/// </summary>
public static class CityHighwaysBuildTools
{
    const string ProductName = "City Highways";
    const string CompanyName = "CityHighways";
    const string ScenePath = "Assets/CityHighways/Scenes/CityHighways.unity";
    const string IconPath = "Assets/CityHighways/Art/Icon.png";
    const string WindowsBuildPath = "Builds/CityHighways/CityHighways.exe";

    [MenuItem("City Highways/1. Настроить проект для сборки")]
    public static void SetupProject()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SplashScreen.backgroundColor = new Color(0.10f, 0.11f, 0.14f);

        CreateIcon();
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        if (!CreateOrOpenScene()) return;

        EditorUtility.DisplayDialog("City Highways",
            "Проект настроен:\n" +
            $"• название окна и игры — «{ProductName}»;\n" +
            "• иконка — " + IconPath + ";\n" +
            "• запуск в полноэкранном окне 1920×1080;\n" +
            "• сцена " + ScenePath + " добавлена в сборку.\n\n" +
            "Теперь: City Highways → 2. Собрать для Windows.", "Хорошо");
    }

    [MenuItem("City Highways/2. Собрать для Windows")]
    public static void BuildWindows()
    {
        if (!File.Exists(ScenePath))
        {
            if (!EditorUtility.DisplayDialog("City Highways", "Сначала нужно настроить проект. Сделать это сейчас?", "Да", "Отмена")) return;
            SetupProject();
            if (!File.Exists(ScenePath)) return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = WindowsBuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            // Steam в сборке без запуска через Steam ищет AppID в steam_appid.txt рядом с .exe (тестовый 480).
            if (File.Exists("steam_appid.txt"))
                File.Copy("steam_appid.txt", Path.Combine(Path.GetDirectoryName(WindowsBuildPath), "steam_appid.txt"), true);
            double megabytes = report.summary.totalSize / (1024.0 * 1024.0);
            EditorUtility.DisplayDialog("City Highways",
                $"Готово! Сборка: {Path.GetFullPath(WindowsBuildPath)} ({megabytes:0} МБ).\n\n" +
                "Чтобы отправить игру, заархивируйте всю папку Builds/CityHighways целиком — без соседних файлов .exe не запустится.",
                "Открыть папку");
            EditorUtility.RevealInFinder(WindowsBuildPath);
        }
        else
        {
            EditorUtility.DisplayDialog("City Highways",
                $"Сборка не удалась ({report.summary.result}). Подробности — в консоли.\n\n" +
                "Частая причина: не установлен модуль Windows Build Support (Unity Hub → Installs → Add modules).",
                "Понятно");
        }
    }

    /// <summary>Создаёт сцену с игрой или открывает уже созданную и проверяет, что в ней есть CityHighwaysApp.</summary>
    static bool CreateOrOpenScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.14f);
            }
        }

        if (Object.FindFirstObjectByType<CityHighwaysApp>() == null)
            new GameObject("Game").AddComponent<CityHighwaysApp>();

        // Event System нужен UI Toolkit, если в проекте включена только новая система ввода.
        // Пункт меню сам выберет правильный модуль ввода.
        // Ищем по имени, чтобы скрипт не зависел от пакета uGUI.
        if (GameObject.Find("EventSystem") == null)
            EditorApplication.ExecuteMenuItem("GameObject/UI/Event System");

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        return true;
    }

    /// <summary>Рисует иконку кодом: зелёный дорожный указатель с белой рамкой и дорогой в перспективе.</summary>
    static void CreateIcon()
    {
        if (File.Exists(IconPath)) return;

        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var sign = new Color(0.10f, 0.42f, 0.30f);
        var asphalt = new Color(0.22f, 0.23f, 0.26f);
        var marking = new Color(0.99f, 0.84f, 0.20f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var color = Color.clear;
                if (InRoundedRect(x, y, 6, 6, size - 12, size - 12, 50)) color = Color.white;
                if (InRoundedRect(x, y, 18, 18, size - 36, size - 36, 40)) color = sign;

                // Дорога сужается к горизонту (вверх по картинке).
                float t = (y - 30f) / 170f;                // 0 внизу, 1 у горизонта
                if (t >= 0f && t <= 1f)
                {
                    float half = Mathf.Lerp(80f, 10f, t);
                    float dx = Mathf.Abs(x - 128f);
                    if (dx < half) color = asphalt;
                    if (dx > half - 5f && dx < half) color = Color.white;          // края
                    if (dx < Mathf.Lerp(5f, 1f, t) && Mathf.Repeat(t * 7f, 1f) < 0.55f) color = marking; // разметка
                }
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
        File.WriteAllBytes(IconPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(IconPath);

        if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    static bool InRoundedRect(int x, int y, int left, int top, int width, int height, int radius)
    {
        if (x < left || y < top || x >= left + width || y >= top + height) return false;
        int cx = Mathf.Clamp(x, left + radius, left + width - radius);
        int cy = Mathf.Clamp(y, top + radius, top + height - radius);
        int dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }
}
