using UnityEditor;

/// <summary>
/// Настройки импорта арта из Scenario, чтобы файлы достаточно было просто положить в Resources:
/// листы иконок (cargo_icons, hub_event_icons) — до 4096 px, без сжатия, с прозрачностью; ключевой арт меню и
/// арт карточек — без ужатия до 2048 и с качественным сжатием; текстуры 3D-города — повторяющиеся (Repeat);
/// слои гардероба (Character/) — с прозрачностью и мип-картами; портреты 3D-аватаров — с прозрачностью; модели 3D-аватаров (FBX) — без анимаций и риг-настроек.
/// </summary>
sealed class GeneratedArtImporter : AssetPostprocessor
{
    // Номер версии настроек: при изменении Unity сам переимпортирует затронутые файлы (2 — мип-карты у гардероба).
    public override uint GetVersion() => 2;

    const string Root = "Resources/CityHighways/";

    string PathInResources
    {
        get
        {
            string path = assetPath.Replace('\\', '/');
            int index = path.IndexOf(Root, System.StringComparison.Ordinal);
            return index < 0 ? null : path.Substring(index + Root.Length);
        }
    }

    void OnPreprocessTexture()
    {
        string path = PathInResources;
        if (path == null) return;
        var importer = (TextureImporter)assetImporter;

        if (path == "UI/cargo_icons.png" || path == "UI/hub_event_icons.png")
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
        else if (path == "UI/key_art.png" || path.StartsWith("Cards/Art/"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
        else if (path.StartsWith("City3D/Textures/"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
        else if (path.StartsWith("Character/"))
        {
            // Слои гардероба 512×768 показываются уменьшенными примерно вдвое (персонаж в профиле — 230 px в ширину).
            // Без мип-карт уменьшение берёт по 4 текселя из 16 — края и складки мерцают («блики» при покачивании).
            // С мип-картами и альфой-прозрачностью (цвет края растягивается в прозрачное) картинка ровная.
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapsPreserveCoverage = false;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
        else if (path.StartsWith("Avatars3D/") && path.EndsWith("_portrait.png"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }

    void OnPreprocessModel()
    {
        string path = PathInResources;
        if (path == null || !path.StartsWith("Avatars3D/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }
}
