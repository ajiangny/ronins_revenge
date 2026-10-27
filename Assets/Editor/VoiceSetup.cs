using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the VoiceLibrary from every clip in Audio/Voice, so the reactive dialogue's lines (Dialogue.cs) find
// their voices by id, and hands it to the GameManager. Re-run after generating new lines with `npm run voice`.
public static class VoiceSetup
{
    const string VoiceRoot = "Assets/Audio/Voice";
    const string LibraryPath = "Assets/Audio/VoiceLibrary.asset";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Ninja/Apply Voice Lines")]
    public static void Apply()
    {
        var library = AssetDatabase.LoadAssetAtPath<VoiceLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<VoiceLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        var clips = new List<VoiceLibrary.Clip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            clips.Add(new VoiceLibrary.Clip { id = Path.GetFileNameWithoutExtension(path), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
        }
        library.clips = clips.ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = new SerializedObject(Object.FindAnyObjectByType<GameManager>());
        manager.FindProperty("voiceLibrary").objectReferenceValue = library;
        manager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Lines written for the dialogue system that have no clip yet play as subtitles only.
        var missing = new List<string>();
        foreach (Conversation c in Dialogue.All)
        {
            for (int i = 0; i < c.Lines.Length; i++)
            {
                if (library.Find(c.VoiceId(i)) == null) missing.Add(c.VoiceId(i));
            }
        }
        Debug.Log($"Voice lines applied: {clips.Count} clips. Written but not voiced yet ({missing.Count}): {string.Join(", ", missing)}");
    }
}
