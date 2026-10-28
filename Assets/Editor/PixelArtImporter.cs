using UnityEditor;
using UnityEngine;

// Imports everything under Assets/Art/Sprites as crisp pixel-art sprites, so regenerated art drops in with no manual setup.
public class PixelArtImporter : AssetPostprocessor
{
    const string SpriteRoot = "Assets/Art/Sprites/";
    const string CharacterRoot = SpriteRoot + "Characters/";
    const float PixelsPerUnit = 48f;
    // Utsusemi's second form was drawn from its painting at a larger size (97 px tall against the first form's 70),
    // so it's packed denser to stand only about an eighth taller than its first form once it breaks open.
    const string UtsusemiPhaseRoot = CharacterRoot + "UtsusemiPhase2/";
    const float UtsusemiPhasePixelsPerUnit = 60f;

    // Effects drawn with SpriteDrawMode.Tiled, by file-name prefix.
    static readonly string[] TiledArt = { "Utsu_BeamRed_", "Utsu_BeamMolten_", "Kage_Chain_", "Kage_SoulPillar_", "SafeZone_" };

    static bool IsTiled(string path)
    {
        string file = System.IO.Path.GetFileName(path);
        foreach (string prefix in TiledArt) if (file.StartsWith(prefix)) return true;
        return false;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteRoot)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = assetPath.StartsWith(UtsusemiPhaseRoot) ? UtsusemiPhasePixelsPerUnit : PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // Characters stand on their feet; everything else (projectiles, effects, tiles) is centred.
        // A custom pivot set by a setup script (effects anchored on their strike point) is kept.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        // Art laid in Tiled draw mode (beams, chains, the safe-zone band) needs a full-rect mesh to tile.
        if (IsTiled(assetPath)) settings.spriteMeshType = SpriteMeshType.FullRect;
        if (settings.spriteAlignment != (int)SpriteAlignment.Custom)
        {
            settings.spriteAlignment = (int)(assetPath.StartsWith(CharacterRoot) ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        }
        importer.SetTextureSettings(settings);
    }
}
