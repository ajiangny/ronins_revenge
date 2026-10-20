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
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)(assetPath.StartsWith(CharacterRoot) ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        importer.SetTextureSettings(settings);
    }
}
