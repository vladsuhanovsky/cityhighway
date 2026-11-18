using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Меню «City Highways → Скачать 3D-модели из Scenario»: фура, башенный кран и терминалы хабов.
/// Модели сделаны в Scenario: концепт GPT Image 2.6 → Tripo P2 (image-to-3D) → Tripo Retopology v3
/// (чистая сетка, 2–4 тыс. граней, Smart UV, текстура запечена на новую развёртку). GLB кладётся в
/// Resources/CityHighways/Models3D (его импортирует glTFast, игра берёт через Resources.Load), FBX — в Models3D_FBX
/// (для Blender и других программ). Масштаб приводит игра (Models3D.Instantiate), опорная точка файла не важна.
///
/// Ссылки подписаны и действуют до 1 декабря 2026 года. Позже — откройте ассет в Scenario (id в списке) и скачайте
/// GLB в ту же папку с тем же именем. При первом открытии проекта загрузка запускается сама один раз.
/// </summary>
[InitializeOnLoad]
public static class ScenarioModelsDownloader
{
    const string GlbFolder = "Assets/CityHighways/Resources/CityHighways/Models3D";
    const string FbxFolder = "Assets/CityHighways/Models3D_FBX";
    const string AutoKey = "CityHighways.Models3D.AutoDownload.v1";

    readonly struct Entry
    {
        public readonly string Id, Title, AssetId, Glb, Fbx;
        public Entry(string id, string title, string assetId, string glb, string fbx)
        {
            Id = id; Title = title; AssetId = assetId; Glb = glb; Fbx = fbx;
        }
    }

    static readonly Entry[] Models =
    {
        new Entry("truck", "Фура", "asset_EJ8qFsZQPBuVo6Xs6SLiPYzu",
            "https://cdn.cloud.scenario.com/assets-transform/asset_EJ8qFsZQPBuVo6Xs6SLiPYzu?p=100&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfRUo4cUZzWlFQQnVWbzZYczZTTGlQWXp1P3A9MTAwKiIsIkNvbmRpdGlvbiI6eyJEYXRlTGVzc1RoYW4iOnsiQVdTOkVwb2NoVGltZSI6MTc5NjA4MzE5OX19fV19&Key-Pair-Id=K36FIAB9LE2OLR&Signature=m3w4bnVEkDQtXlsTu6GTTpkeFI2jC3AOq-477MD6Ly09cgFyxNR7tUHW0XMoSBL7LOvC1tZZ6Hpx9VXdDuaD3MtfsnPoZSgQbg1DN6OS86JceKlMRkEj1PdBQeEJx7OIx~GcUP9kqV6zqNt8qziTfZQFobL5NDHnkycn0X80LE1R8~XxujRLcrh2PLuYjt1waYrFYspZW~dZeSlXaeG0B3eQUg~-AmDZbrxYviIWXayKbhZj70UUfE1WYzNuvK7qZk4lnsDGd0Z2tjEQCj5TerCPhxvLCLm~-x8c97UwePmeLOSqX-sBvcrECajrHTdyDqZBRJe~snl-~Yv-B6eJpg__",
            "https://cdn.cloud.scenario.com/assets-transform/asset_EJ8qFsZQPBuVo6Xs6SLiPYzu?p=100&format=fbx&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfRUo4cUZzWlFQQnVWbzZYczZTTGlQWXp1P3A9MTAwJmZvcm1hdD1mYngqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=awbRjFdGAwOaH75xZ68ZYQlbvmh8qePsYVbsS4ytq~LtNMgeoPNBdrphpWQ73-1owfsKxb~2fCrs~T8O30K9XJiZ9eoG3E3AH5VtOwpaNzVe~bKeqxaUoNUDYVG4knWRoyVTLJOMHzVi3O-HPMepVbiet68RctGOctCY4LsDOkKOcY4aJnfknlt2sPuZp7hyDxuDZWfwXpVreun0PS--TUOapl5UjsFdk5VF0JYmCSwreFn58nqLuvzdZzfwViq1Cr2BSahR5kli4BWfaKuSEu-rsb4BUbLk0wY-kKVr3uCSwpYjI1gqBcAm1ur4enzdIuA1lWpXu1pJ6cI73w95bQ__"),
        new Entry("tower_crane", "Башенный кран", "asset_7dQLyD4VY5KN5ZxhFPXHSxed",
            "https://cdn.cloud.scenario.com/assets-transform/asset_7dQLyD4VY5KN5ZxhFPXHSxed?p=100&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfN2RRTHlENFZZNUtONVp4aEZQWEhTeGVkP3A9MTAwKiIsIkNvbmRpdGlvbiI6eyJEYXRlTGVzc1RoYW4iOnsiQVdTOkVwb2NoVGltZSI6MTc5NjA4MzE5OX19fV19&Key-Pair-Id=K36FIAB9LE2OLR&Signature=N8z2ltKh70R%7EuJ0g2WQvF7CdDKIKxMA9RxoLRWQaCzWBqGez%7EaWaae%7E9O3O9MzLkcqzt3yiHv6PYV04qclOV5hgo78xJ8shwus%7EcpC79YJew0R8L0AZivicJIAyrDR6yWrvCxOwggaVEn%7EBgGIjChFTEq4bF7Uph-B93IT-NONEMg1NjdXtSDoI457Ku-ojvObtXw7OfnkWmjXwdUNk2xie5-p90aD0ud-9neRBR6ckGKELWogCT5VlZiN1wuzvEPzbPAdlpYZYdMjeDKyJZvhWdMZaKX9mXnr8BC0DhSMlz5KmHfyai4vxuSGuhZ5VftK51dtLctojFY9a3BjIu0Q__",
            "https://cdn.cloud.scenario.com/assets-transform/asset_7dQLyD4VY5KN5ZxhFPXHSxed?p=100&format=fbx&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfN2RRTHlENFZZNUtONVp4aEZQWEhTeGVkP3A9MTAwJmZvcm1hdD1mYngqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=DoPjuIFxeAJiwhGzN1fpr~r0CCAN6J--GJYPhyeAHgAD4TRTve4gmpC26WNGDc1AMSjG9pbgHgcZtDZtXzK0HqPJya9hVnFO0aAIS7R7HJklovGYm7RDEbFdJ9Owg3bbkKnCqcJ4grx0j636IXQu1WHnPsl2uSecAjEytBZsmQPhIG33M26A8KmmG4yH9yPzTvFTg-gqRNj2o7kBZWgNxaYmeJBHHG2-c7TDveg8WJXRdgae9mCY-mi0qa6epgD1ogVpJcrzaOiMZcr51FLiPWxf~HvaiFmPVcB1f2u6NoTkCwjceQ9i6GbG~3BgNY5oSdAFmYz0bxBTU1wa-H9Aww__"),
        new Entry("terminal_airport", "Терминал аэропорта", "asset_j4srgiFa2THEqKVWqr2aduKs",
            "https://cdn.cloud.scenario.com/assets-transform/asset_j4srgiFa2THEqKVWqr2aduKs?p=100&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfajRzcmdpRmEyVEhFcUtWV3FyMmFkdUtzP3A9MTAwKiIsIkNvbmRpdGlvbiI6eyJEYXRlTGVzc1RoYW4iOnsiQVdTOkVwb2NoVGltZSI6MTc5NjA4MzE5OX19fV19&Key-Pair-Id=K36FIAB9LE2OLR&Signature=VxJqIOoL8zzSuuIt60mW-3TzUVxNqgfinK%7EIFxVmCBFTALfRa0g6QRNs64QlVdZ566s1yiUkuhjqMBm1n8KnA347nenpJXyCIbVGgMuBEWTPjynlk1z5jqerb262HDPywRwd3vUDEeL67sjojQSm628e%7EFDAG6kbwlOQrlB8442UJS7nozaagAa3Z3CSkJN8Aitfd2PDxEecrm3vcYktByxz96dQX0dcKbPEZV9O4dmLUpfWoBVZsGXj9cv%7EP3CjoRiOkNhnTuTWVDUrp2FuIvrsg4k0swHNZOMC5y%7EvfaWlFnUuC4yJmRxLtyLws0XvHUx0oI%7Eo910dnCBTuTOZ3A__",
            "https://cdn.cloud.scenario.com/assets-transform/asset_j4srgiFa2THEqKVWqr2aduKs?p=100&format=fbx&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfajRzcmdpRmEyVEhFcUtWV3FyMmFkdUtzP3A9MTAwJmZvcm1hdD1mYngqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=dlEKsvUoOq0Efn8N8mysx~U~9efSLgbCSWRpuNLlsgCiJW9hDFf4cdiGPmMApez-IpDAcfbusAWc9BuJbkCvYsBDmHJgmUXvOf7GPKFTcJtc4XWGMVrK51pekuw1eHAXqQRlVP3iNtT0DnDKTnZF6FYxq1-hCUmshfLKHEcQ-K3PH-BXoS6gLfzn9bb9J~t9Ln8hYFFCMbEuYbhcoJKDPY~m-cBpiy1EKf7dbD5IsV5MrLRzxWZzlkvPq4ronrBD0bNzg-KVBpEMfST23SSyDOuLylPOG8yGrCjxNLSD7OrUag~yJ~j9lwvaDpxEQ8oFd9FpfPQpORy3D~6xd0g8Rg__"),
        new Entry("terminal_station", "Вокзал", "asset_rD6dtnUyoq74PB5BstqoEewh",
            "https://cdn.cloud.scenario.com/assets-transform/asset_rD6dtnUyoq74PB5BstqoEewh?p=100&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfckQ2ZHRuVXlvcTc0UEI1QnN0cW9FZXdoP3A9MTAwKiIsIkNvbmRpdGlvbiI6eyJEYXRlTGVzc1RoYW4iOnsiQVdTOkVwb2NoVGltZSI6MTc5NjA4MzE5OX19fV19&Key-Pair-Id=K36FIAB9LE2OLR&Signature=jimCrmU0T7H%7E7EwDH0XgzO6bBhsH0ABL-maQ5TUR2VXbUWjuwuov2KxorHlfBEmO8MtrFBExdmBNiKTV3zEsFH38orIpxUwVsLAc-Kl-j1Y-oqgqRv-FNpKP25A1o-hHl-bTaYe3ynHPo-n%7ERkCTNx5qpO0NOeTdufUEYLikqcvDOK3sGBXk8GGf27zJEeDzzscBA6akpPy1Tt8TZrXmVaT8SS1m5uNUrDs%7Eh869IjY5EN9rdfHyBpymdODqv8ATHZX1sb%7EV9aOHwnRL3qN7mQEkVcouIArmNV63PBRWh9YDFstt0KERszSRieTszApmravlIAOP8SvUkzhmO7Z1nA__",
            "https://cdn.cloud.scenario.com/assets-transform/asset_rD6dtnUyoq74PB5BstqoEewh?p=100&format=fbx&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfckQ2ZHRuVXlvcTc0UEI1QnN0cW9FZXdoP3A9MTAwJmZvcm1hdD1mYngqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=DrOSMjcIyEzGLCt4d~Kqn6cuseGjPYrB4hTqsp8Q8qkHX9HuFnhGGHuAMIaa803U3r-952e4Y-JUK3L2pq0CA3~hzoboEWwSGBBbD2pQ~-eER5qK83XKFUyxMfmqEK13TbZ8jIjKGTx7KSAzH-l7i6vh0TLRV4jq7xZ3a80SnL~05495EDeokOyq4jipOfv6UHoCr8b5CWg~qBTmCNIIKg13BeAC27zHnhxVTb-qNsoRum7DyLMCBi6N8xB3oy1QRDD7Da8Re6PUGrk1OUXLCraZDUpsnxonl7KX8hkuPCGJOX4IuSJDMcpvCgBhvN5U~1sCKPAy1rXCfD0uvq~Pdg__"),
        new Entry("terminal_port", "Терминал порта", "asset_qccn3SNLDfmnM3zo7e38ZgVk",
            "https://cdn.cloud.scenario.com/assets-transform/asset_qccn3SNLDfmnM3zo7e38ZgVk?p=100&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfcWNjbjNTTkxEZm1uTTN6bzdlMzhaZ1ZrP3A9MTAwKiIsIkNvbmRpdGlvbiI6eyJEYXRlTGVzc1RoYW4iOnsiQVdTOkVwb2NoVGltZSI6MTc5NjA4MzE5OX19fV19&Key-Pair-Id=K36FIAB9LE2OLR&Signature=UvPgBJuFjZahVD4nUD1Lj-yVgwGSsOWLrC3%7ELZn4bYoNEWWzQCf1W6Sx4bGlkfuZc7Od1zEOhZkLQqdf1xxyoib9PnTvoppwKl7BHogXLFYuxWO8evAaSiCzITIBiJSPzOypjTIUGr7hfCKJQM6RljYNkRDSNgH%7EugBqbEAvIiuJiSlHFsAqE8Z1%7EepzVw20AXzXCVKCu%7E01bC0C67k3X4Vy17fpGrMocZS%7EKOyF5y0PlFee-%7E-ufeTCbaz6S6ASczq4RkQyN0Qoj0SfT1WW1EFR1V0KprVV7ePFIKp5fiYb5tmr427HVHwdw51CUQFiimvYOdHzyB3WoRM2sDZyOA__",
            "https://cdn.cloud.scenario.com/assets-transform/asset_qccn3SNLDfmnM3zo7e38ZgVk?p=100&format=fbx&Policy=eyJTdGF0ZW1lbnQiOlt7IlJlc291cmNlIjoiaHR0cHM6Ly9jZG4uY2xvdWQuc2NlbmFyaW8uY29tL2Fzc2V0cy10cmFuc2Zvcm0vYXNzZXRfcWNjbjNTTkxEZm1uTTN6bzdlMzhaZ1ZrP3A9MTAwJmZvcm1hdD1mYngqIiwiQ29uZGl0aW9uIjp7IkRhdGVMZXNzVGhhbiI6eyJBV1M6RXBvY2hUaW1lIjoxNzk2MDgzMTk5fX19XX0_&Key-Pair-Id=K36FIAB9LE2OLR&Signature=v51Wm716w3DdTDeDUjw5ceAkANY1nwoyaJv2egUWtEYU-meIsWHKGwcSgun5ZFCbW1IGY~STKZePylM31Gon9nsL~mZDZ2634g4UaNSIu5U5NtxEY3vUi2r2q0pEpH12y1S~APr6aJpwcaB3UwpuZKdn14-ZVqmBeTxIEvk2ncSvfVoMWvvnAR8oR4fQIEJA8Lvs9Jln~W3zA9~v3ZWwmIDaDmFiVJjUKR-~9LKqvfWtNYRzebi0YeJXqiIgEIOeETzcoCgz9t~GD57PY~ITG4OI8xznviG-BpMJSEVaXJJYOkT30tsx8hEpVpfh3KuyF2gODQeIXvBJnpWjdyh-Og__"),
    };

    static ScenarioModelsDownloader()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AutoKey, false) || Application.isBatchMode) return;
            bool missing = false;
            foreach (var model in Models)
                if (!File.Exists(Path.Combine(GlbFolder, model.Id + ".glb"))) missing = true;
            if (!missing) return;
            EditorPrefs.SetBool(AutoKey, true);
            Debug.Log("City Highways: скачиваю 3D-модели из Scenario (один раз; повторить — меню City Highways).");
            Download();
        };
    }

    [MenuItem("City Highways/Скачать 3D-модели из Scenario")]
    public static void Download()
    {
        Directory.CreateDirectory(GlbFolder);
        Directory.CreateDirectory(FbxFolder);
        int done = 0, failed = 0;
        try
        {
            for (int i = 0; i < Models.Length; i++)
            {
                var model = Models[i];
                if (Fetch(model.Glb, Path.Combine(GlbFolder, model.Id + ".glb"), $"{model.Title} (GLB)", i * 2f / (Models.Length * 2f))) done++;
                else failed++;
                if (Fetch(model.Fbx, Path.Combine(FbxFolder, model.Id + ".fbx"), $"{model.Title} (FBX)", (i * 2f + 1f) / (Models.Length * 2f))) done++;
                else failed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.Refresh();
        if (failed == 0) Debug.Log($"City Highways: 3D-модели скачаны ({done} файлов). Хабы магната покажут их в 3D-городе.");
        else
        {
            var ids = string.Join(", ", System.Array.ConvertAll(Models, m => $"{m.Id} = {m.AssetId}"));
            Debug.LogWarning($"City Highways: не скачалось {failed} файлов (ссылки действуют до 01.12.2026). " +
                             $"Скачайте GLB вручную в Scenario и положите в {GlbFolder} под этими именами: {ids}.");
        }
    }

    static bool Fetch(string url, string path, string title, float progress)
    {
        EditorUtility.DisplayProgressBar("3D-модели из Scenario", title, progress);
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 120;
            var operation = request.SendWebRequest();
            while (!operation.isDone) System.Threading.Thread.Sleep(30);
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"City Highways: {title} не скачался: {request.error}");
                return false;
            }
            File.WriteAllBytes(path, request.downloadHandler.data);
            return true;
        }
    }
}
