using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wires the UI fonts into the scene's GameUI. Safe to re-run; a missing font leaves the pixel font in its place.
public static class UiSetup
{
    const string FontRoot = "Assets/Art/Fonts/";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Ninja/Apply UI Fonts")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = new SerializedObject(Object.FindAnyObjectByType<GameUI>());
        ui.FindProperty("bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "ZenKakuGothicNew-Bold.ttf");
        ui.FindProperty("headerFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "Cinzel.ttf");
        ui.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("UI fonts applied.");
    }
}
