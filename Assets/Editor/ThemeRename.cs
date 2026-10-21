using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: renames the template's Space Invaders prefabs, scene objects and layers to the ronin/samurai theme.
// Runs inside Unity so prefab and scene GUIDs stay intact. Safe to run again; anything already renamed is skipped.
public static class ThemeRename
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static readonly (string from, string to)[] Prefabs =
    {
        ("Alien", "Samurai"), ("AlienBullet", "Arrow"), ("AlienFleet", "SamuraiArmy"), ("Spaceship", "Ronin"), ("Dart", "Kunai"),
    };

    static readonly (string from, string to)[] SceneObjects =
    {
        ("AlienFleet", "SamuraiArmy"), ("Spaceship", "Ronin"), ("Shields", "Blockers"), ("AlienCounter", "SamuraiCounter"),
    };

    // Renamed in place, so the physics collision matrix (stored by layer index) is kept.
    static readonly (string from, string to)[] Layers =
    {
        ("Spaceship", "Ronin"), ("Alien", "Samurai"), ("Bullet", "Kunai"), ("Alien_Bullet", "Arrow"),
    };

    [MenuItem("Ninja/Rename To Ronin Theme")]
    public static void Rename()
    {
        foreach (var (from, to) in Prefabs)
        {
            string path = $"Assets/{from}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            string error = AssetDatabase.RenameAsset(path, to);
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"Couldn't rename {path}: {error}");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (var (from, to) in SceneObjects)
                {
                    if (t.name == from) t.name = to;
                }
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            foreach (var (from, to) in Layers)
            {
                if (layer.stringValue == from) layer.stringValue = to;
            }
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        Debug.Log("Renamed prefabs, scene objects and layers to the ronin/samurai theme.");
    }
}
