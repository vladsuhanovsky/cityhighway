using UnityEditor;

/// <summary>
/// Настройки импорта листа иконок грузов (Resources/CityHighways/UI/cargo_icons.png): лист шириной 3584 px
/// не ужимается до 2048, прозрачность сохраняется, без сжатия — чтобы иконки не мылились и не давали ореолов.
/// </summary>
sealed class CargoIconsImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').EndsWith("Resources/CityHighways/UI/cargo_icons.png")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
