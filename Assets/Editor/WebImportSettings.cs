using UnityEditor;
using UnityEngine;

// Import settings for the web build, applied on every import so regenerated art and audio keep them:
// - the painted portraits and logo are crunch-compressed for WebGL at their own size: a small download that unpacks
//   to block-compressed textures. Each side must be a multiple of 4 or WebGL leaves them uncompressed, so they
//   aren't capped either (shrinking 1536x1024 to 1024 gives 683, which isn't);
// - sound effects and voice lines are preloaded, so the first time one plays mid-fight doesn't hitch on loading it;
// - voice lines are mono at a lower quality for WebGL, where they are most of the audio download.
// Pixel-art sprites stay uncompressed (PixelArtImporter): block compression smears their hard edges.
public class WebImportSettings : AssetPostprocessor
{
    const string PortraitRoot = "Assets/Art/Portraits/";
    const string LogoRoot = "Assets/Art/Logo/";
    const string SfxRoot = "Assets/Audio/Sfx/";
    const string VoiceRoot = "Assets/Audio/Voice/";
    const string WebGL = "WebGL";

    void OnPreprocessTexture()
    {
        bool portrait = assetPath.StartsWith(PortraitRoot);
        // Logo and portraits alike; the logo keeps a little more crunch quality for its fine linework.
        if (!portrait && !assetPath.StartsWith(LogoRoot)) return;
        var importer = (TextureImporter)assetImporter;
        TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebGL);
        web.overridden = true;
        web.maxTextureSize = 2048;
        web.textureCompression = TextureImporterCompression.Compressed;
        web.format = TextureImporterFormat.Automatic;
        web.crunchedCompression = true;
        web.compressionQuality = portrait ? 50 : 75;
        importer.SetPlatformTextureSettings(web);
    }

    void OnPreprocessAudio()
    {
        bool voice = assetPath.StartsWith(VoiceRoot);
        if (!voice && !assetPath.StartsWith(SfxRoot)) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        if (!voice) return;
        importer.forceToMono = true;
        AudioImporterSampleSettings web = importer.GetOverrideSampleSettings(WebGL);
        web.loadType = settings.loadType;
        web.compressionFormat = settings.compressionFormat;
        web.quality = 0.5f;
        web.preloadAudioData = true;
        importer.SetOverrideSampleSettings(WebGL, web);
    }

    [MenuItem("Ninja/Apply Web Import Settings")]
    public static void Apply()
    {
        // Reimporting runs the hooks above over everything they cover.
        foreach (string root in new[] { PortraitRoot, LogoRoot, SfxRoot, VoiceRoot })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D t:AudioClip", new[] { root.TrimEnd('/') }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }
        Debug.Log("Web import settings applied to portraits, logo, sound effects and voice lines.");
    }
}
