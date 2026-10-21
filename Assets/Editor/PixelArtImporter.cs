using UnityEditor;
using UnityEngine;

// Imports everything under Assets/Art/Sprites as crisp pixel-art sprites, so regenerated art drops in with no manual setup.
public class PixelArtImporter : AssetPostprocessor
{
    const string SpriteRoot = "Assets/Art/Sprites/";
    const string CharacterRoot = SpriteRoot + "Characters/";
    const float PixelsPerUnit = 48f;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteRoot)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // Characters stand on their feet; everything else (projectiles, effects, tiles) is centred.
        // A custom pivot set by a setup script (effects anchored on their strike point) is kept.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteAlignment == (int)SpriteAlignment.Custom) return;
        settings.spriteAlignment = (int)(assetPath.StartsWith(CharacterRoot) ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        importer.SetTextureSettings(settings);
    }
}
