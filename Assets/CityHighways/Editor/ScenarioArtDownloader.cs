using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Картинки из Scenario в полном разрешении. В репозитории лежат превью (1024 px) — игра работает с ними сразу,
/// а при первом открытии проекта (или из меню City Highways) скрипт один раз заменяет их полной версией.
/// Ссылки подписаны и действуют до 01.12.2026; потом картинку можно скачать в Scenario по id ассета.
/// </summary>
[InitializeOnLoad]
static class ScenarioArtDownloader
{
    const string AutoKey = "CityHighways.ScenarioArt.AutoDownload.v1";

    struct Art
    {
        public string Title, Path, AssetId, Url;
        public int MinBytes;   // файл меньше — это превью, его стоит заменить
    }

    static readonly Art[] Items =
    {
        new Art
        {
            Title = "Экран загрузки магната (2048 × 1152)",
            Path = "Assets/CityHighways/Resources/CityHighways/UI/magnate_loading.jpg",
            AssetId = "asset_NHQ568HM3tA99sU3KX9XmHPU",
            MinBytes = 300000,
            Url = "https://cdn.cloud.scenario.com/assets-transform/asset_NHQ568HM3tA99sU3KX9XmHPU?p=100&format=jpg&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfTkhRNTY4SE0zdEE5OXNVM0tYOVhtSFBVP3A9MTAwJmZvcm1hdD1qcGcqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=KtU8I0~XtUl3LnOJuXDZbPtMTCZ9crBvONPQro4J4k7RfQBHYOMDalvV-eeAumFKzo93jXbHIxlml9ifIm1FZz1UNvz3UTXiJIrtJkEzZ~9bhnB75lnJEeHO95ID9QbBrz6LfSauAgXNXjWWZTSPH7KIqZdvBUq84IqqrHKAePxqOoDfP~QDUCERh7BZtlO~13~3IdQ7gXqFp7Jp1nmS53aVq~nqjArMOySe059EWDyLTtlb3l7yniJi80hqD-wt0KOC2rwt-5BbWBu-m29fO5Zu5r9yPC4QUqcaHavVHBiOTQyRvDncEZtp6U~tSBi23KSrXF8CWhR3eCOudu3lpg__"
        }
    };

    static ScenarioArtDownloader()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AutoKey, false) || Application.isBatchMode) return;
            bool preview = false;
            foreach (var item in Items)
                if (!File.Exists(item.Path) || new FileInfo(item.Path).Length < item.MinBytes) preview = true;
            if (!preview) return;
            EditorPrefs.SetBool(AutoKey, true);
            Debug.Log("City Highways: скачиваю картинки из Scenario в полном разрешении (один раз; повторить — меню City Highways).");
            Download();
        };
    }

    [MenuItem("City Highways/Скачать картинки из Scenario в полном разрешении")]
    public static void Download()
    {
        int failed = 0;
        try
        {
            for (int i = 0; i < Items.Length; i++)
            {
                var item = Items[i];
                EditorUtility.DisplayProgressBar("Картинки из Scenario", item.Title, i / (float)Items.Length);
                using (var request = UnityWebRequest.Get(item.Url))
                {
                    request.timeout = 120;
                    var operation = request.SendWebRequest();
                    while (!operation.isDone) System.Threading.Thread.Sleep(30);
                    if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data == null || request.downloadHandler.data.Length < 1000)
                    {
                        Debug.LogWarning($"City Highways: «{item.Title}» не скачалась ({request.error}). Осталось превью; " +
                                         $"полную версию можно скачать в Scenario (ассет {item.AssetId}) и положить в {item.Path}.");
                        failed++;
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Path));
                    File.WriteAllBytes(item.Path, request.downloadHandler.data);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.Refresh();
        if (failed == 0) Debug.Log("City Highways: картинки из Scenario скачаны в полном разрешении.");
    }
}
