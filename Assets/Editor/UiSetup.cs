using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wires the UI fonts, the title logo and the title screen's flute-playing ronin into the scene. Safe to re-run;
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
        ui.FindProperty("bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "ZenKakuGothicNew-Bold.ttf");
        ui.FindProperty("headerFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "Cinzel.ttf");
        ui.FindProperty("titleLogo").objectReferenceValue = Logo();
        ui.ApplyModifiedPropertiesWithoutUndo();

        // Ronin_Flute_* frames, in name order, once they've been drawn.
        var frames = new List<Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("Ronin_Flute_ t:Sprite", new[] { FluteFrames }))
        {
            frames.Add(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)));
        }
        frames.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        var manager = new SerializedObject(Object.FindAnyObjectByType<GameManager>());
        SerializedProperty flute = manager.FindProperty("titleFluteFrames");
        flute.arraySize = frames.Count;
        for (int i = 0; i < frames.Count; i++) flute.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        manager.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"UI fonts and title art applied ({frames.Count} flute frames).");
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
