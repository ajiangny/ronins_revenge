using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Wires the UI fonts, the title logo and the title screen's flute-playing ronin (and his stand-up) into the scene. Safe to re-run;
// anything missing leaves the game's fallback in its place.
public static class UiSetup
{
    const string FontRoot = "Assets/Art/Fonts/";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string LogoPath = "Assets/Art/Logo/RoninsRevenge.png";
    const string FluteFrames = "Assets/Art/Sprites/Characters/Ronin";

    [MenuItem("Ninja/Apply UI Fonts")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = new SerializedObject(Object.FindAnyObjectByType<GameUI>());
        // The TTFs are baked into static TMP fonts holding just the characters the game prints; the scene then drops
        // the TTFs, so builds neither ship them nor rasterise them at startup.
        var body = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "ZenKakuGothicNew-Bold.ttf");
        var header = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "Cinzel.ttf");
        TMP_FontAsset bodyBaked = Bake(body, FontRoot + "ZenKakuGothicNew-Bold SDF.asset", GameUI.BodySampling, GameUI.BodyPadding);
        TMP_FontAsset headerBaked = Bake(header, FontRoot + "Cinzel SDF.asset", GameUI.HeaderSampling, GameUI.HeaderPadding);
        ui.FindProperty("bodyFontBaked").objectReferenceValue = bodyBaked;
        ui.FindProperty("headerFontBaked").objectReferenceValue = headerBaked;
        ui.FindProperty("bodyFont").objectReferenceValue = bodyBaked != null ? null : body;
        ui.FindProperty("headerFont").objectReferenceValue = headerBaked != null ? null : header;
        ui.FindProperty("titleLogo").objectReferenceValue = Logo();
        ui.ApplyModifiedPropertiesWithoutUndo();

        // Ronin_Flute_* frames and the Ronin_StandUp_* clip START plays, in name order, once they've been drawn.
        var manager = new SerializedObject(Object.FindAnyObjectByType<GameManager>());
        int flutes = SetFrames(manager.FindProperty("titleFluteFrames"), "Ronin_Flute_");
        int stand = SetFrames(manager.FindProperty("titleStandFrames"), "Ronin_StandUp_");
        manager.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"UI fonts and title art applied ({flutes} flute frames, {stand} stand-up frames).");
    }

    // A static SDF font of the game's printable characters, saved with its atlas and material inside it. Delete the
    // asset to re-bake (after adding characters to GameUI.PrintableCharacters).
    static TMP_FontAsset Bake(Font source, string path, int samplingSize, int padding)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null || source == null) return existing;
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, samplingSize, padding, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, false);
        if (asset == null) return null;
        asset.TryAddCharacters(GameUI.PrintableCharacters, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"{source.name} has no glyphs for: {missing}");
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        foreach (Texture2D atlas in asset.atlasTextures)
        {
            atlas.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
        }
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        var so = new SerializedObject(asset);
        SerializedProperty sourceFile = so.FindProperty("m_SourceFontFile");
        if (sourceFile != null) sourceFile.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return asset;
    }

    static int SetFrames(SerializedProperty property, string prefix)
    {
        var frames = new List<Sprite>();
        foreach (string guid in AssetDatabase.FindAssets(prefix + " t:Sprite", new[] { FluteFrames }))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
            if (sprite != null && sprite.name.StartsWith(prefix)) frames.Add(sprite);
        }
        frames.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        property.arraySize = frames.Count;
        for (int i = 0; i < frames.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        return frames.Count;
    }

    // The logo is painted art, not pixel art: a smooth, full-size UI sprite.
    static Sprite Logo()
    {
        if (AssetImporter.GetAtPath(LogoPath) is TextureImporter importer &&
            (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Bilinear || importer.maxTextureSize != 2048))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
    }
}
