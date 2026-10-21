using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the bosses' CharacterArt from their PixelLab frames and hands it to the GameManager in the scene.
// Safe to re-run after regenerating any of the art.
public static class BossArtSetup
{
    const string SpriteRoot = "Assets/Art/Sprites/Characters/";
    const string ArtRoot = "Assets/Art/Characters/";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    // About twice a samurai's height, so she reads as the warlord the moment she lands.
    const float BenibanaScale = 2.3f;
    // Low enough that her ponytail clears the boss bar.
    const float BossDepth = 4.5f;

    [MenuItem("Ninja/Apply Boss Art")]
    public static void Apply()
    {
        var particles = new[]
        {
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Effects/Petal.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Effects/Smoke.png"),
        };

        // The pipe puff goes in the parry slot: CharacterArt has no slot of its own for it.
        CharacterArt benibana = SaveArt("Benibana",
            idle: Frames("Benibana", "Idle"),
            attack: Frames("Benibana", "Slash"),
            parry: Frames("Benibana", "Puff"),
            hit: Frames("Benibana", "Hit"),
            death: Frames("Benibana", "Defeat"),
            particles);

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = Object.FindAnyObjectByType<GameManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("benibanaArt").objectReferenceValue = benibana;
        so.FindProperty("benibanaScale").floatValue = BenibanaScale;
        so.FindProperty("bossDepth").floatValue = BossDepth;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Benibana art applied: {benibana.idle.Length} idle, {benibana.attack.Length} slash, {benibana.parry.Length} puff, " +
            $"{benibana.hit.Length} hit, {benibana.death.Length} defeat frames.");
    }

    static CharacterArt SaveArt(string name, Sprite[] idle, Sprite[] attack, Sprite[] parry, Sprite[] hit, Sprite[] death, Sprite[] particles)
    {
        string path = ArtRoot + name + ".asset";
        var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(path);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<CharacterArt>();
            AssetDatabase.CreateAsset(art, path);
        }
        art.idle = idle;
        art.attack = attack;
        art.parry = parry;
        art.hit = hit;
        art.death = death;
        art.deathParticles = particles;
        art.idleFps = 6f;
        art.actionFps = 11f;
        EditorUtility.SetDirty(art);
        return art;
    }

    // Every numbered frame of the clip, in order.
    static Sprite[] Frames(string character, string clip)
    {
        var frames = new List<Sprite>();
        for (int i = 0; ; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{character}/{character}_{clip}_{i}.png");
            if (sprite == null) break;
            frames.Add(sprite);
        }
        if (frames.Count == 0) throw new System.IO.FileNotFoundException($"No frames for {character} {clip}");
        return frames.ToArray();
    }
}
