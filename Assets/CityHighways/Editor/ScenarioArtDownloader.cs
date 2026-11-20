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
    const string AutoKey = "CityHighways.ScenarioArt.AutoDownload.v2";

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
        },
        new Art
        {
            Title = "Экран загрузки соревнования (2048 × 1152)",
            Path = "Assets/CityHighways/Resources/CityHighways/UI/competition_loading.jpg",
            AssetId = "asset_ToBCF1KyeMV4he1p4SG4nw74",
            MinBytes = 300000,
            Url = "https://cdn.cloud.scenario.com/assets-transform/asset_ToBCF1KyeMV4he1p4SG4nw74?p=100&format=jpg&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfVG9CQ0YxS3llTVY0aGUxcDRTRzRudzc0P3A9MTAwJmZvcm1hdD1qcGcqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=wWQEPijEU9CnOwbo4cG64YJneYRuOYSvIYJMwMhx54P8L-U3uD9qUvKZKAuQbtHQy7uEZuWDmL6bvMqHMlwb5ftPfYIe0--~SH-VPZcX3v9B-sBxQ7VLjTW98hEWIVGg3Dr9xV7b2TwJrPt5XpKKHFbjA30669ksaqYGyX9Z4QwXCGDArdnJuF4tK0~uNwt6A9sjyEsrAnWvdIGdMu5NrqTfIXKHr0pRAYLIQrTZsQhrOtMSwM3A1b7QrryZVcvePgxC79IsnfYS6kq8aJS9rbXbi6Zf0DVKlbDXyvLGBSym4TdYO4LyGUfxPwMSeqGUConqP3MdcUd4nAp2BcWCEA__"
        },
        new Art
        {
            Title = "Экран загрузки кооператива (2048 × 1152)",
            Path = "Assets/CityHighways/Resources/CityHighways/UI/coop_loading.jpg",
            AssetId = "asset_rndqJ1cC6ZJLb8Bw7v81pCJH",
            MinBytes = 300000,
            Url = "https://cdn.cloud.scenario.com/assets-transform/asset_rndqJ1cC6ZJLb8Bw7v81pCJH?p=100&format=jpg&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfcm5kcUoxY0M2WkpMYjhCdzd2ODFwQ0pIP3A9MTAwJmZvcm1hdD1qcGcqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=AqZhhgBvT-qncEiRHk3UZkfXGMlUtfRlu6DnW3rILUVRgTooFu8CZHdee13kX6oj183SUWn~zGYCnQIcY7qZ6r3Qthn~qgpq8gBqMAx~qd9pBBkhyx2ezHtvUZkYRPHWW6VRV0XlZprfxuMM2FhoyJ-32oJ4cSG3DleRD9eiaP8fMFnbSYQRClC~3zqj-9BQ3SBBq0E7Y-PoEGm4oF~QMwiuWc8FdTNKQUtCKgiFvvfuHuMlpJGOxqGYBey9wh-M9YGSg14XKqjUx4mpZkQVFoe6nL~FRa6Vss~~oNRVa1kvoeb5KfuuP4cLFtEoDHWJVep3kpL6Tpuy2N-Y7cOdFw__"
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
