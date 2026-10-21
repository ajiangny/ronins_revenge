using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the SfxLibrary from the ElevenLabs clips in Audio/Sfx and Audio/Music and hands it to the GameManager.
// Safe to re-run after adding or regenerating clips; volumes tuned in the library are kept.
public static class AudioSetup
{
    const string SfxRoot = "Assets/Audio/Sfx/";
    const string MusicRoot = "Assets/Audio/Music/";
    const string LibraryPath = "Assets/Audio/SfxLibrary.asset";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // id, starting volume, pitch jitter, and the real seconds before it can repeat.
    static readonly (string id, float volume, float jitter, float minInterval)[] Sounds =
    {
        (Sfx.SwordSlash, 0.55f, 0.08f, 0.05f),
        (Sfx.KunaiThrow, 0.55f, 0.08f, 0.05f),
        (Sfx.ShurikenThrow, 0.8f, 0.04f, 0.1f),
        (Sfx.ParrySwing, 0.7f, 0.06f, 0.05f),
        (Sfx.Deflect, 0.85f, 0.06f, 0.04f),
        (Sfx.ArrowFire, 0.5f, 0.1f, 0.08f),
        (Sfx.SamuraiDeath, 0.7f, 0.1f, 0.04f),
        (Sfx.PlayerHit, 1f, 0.03f, 0.2f),
        (Sfx.WardShatter, 0.9f, 0.03f, 0.2f),
        (Sfx.PlayerDeath, 1f, 0f, 0.5f),
        (Sfx.BlockerHit, 0.5f, 0.1f, 0.05f),
        (Sfx.BlockerBreak, 0.7f, 0.08f, 0.08f),
        (Sfx.SoulSpawn, 0.7f, 0.03f, 0.2f),
        (Sfx.SoulCollect, 0.9f, 0f, 0.2f),
        (Sfx.FireBlast, 0.75f, 0.08f, 0.06f),
        (Sfx.LightningStrike, 0.7f, 0.08f, 0.06f),
        (Sfx.FrostChill, 0.6f, 0.06f, 0.08f),
        (Sfx.UiMove, 0.6f, 0.04f, 0.03f),
        (Sfx.UiConfirm, 0.8f, 0f, 0.1f),
        (Sfx.StageStart, 0.9f, 0f, 0.5f),
        (Sfx.VictorySting, 0.9f, 0f, 1f),
        (Sfx.DefeatSting, 0.9f, 0f, 1f),
        (Sfx.MarchStep, 0.7f, 0f, 0.1f),
        (Sfx.ParryClang, 1f, 0.05f, 0.1f),
        // Mass kills would otherwise be a wall of screams.
        (Sfx.SamuraiCry, 0.55f, 0.12f, 0.12f),
        (Sfx.PlayerHurt, 0.9f, 0.05f, 0.2f),
        (Sfx.BossHit, 0.75f, 0.08f, 0.05f),
        (Sfx.BossStagger, 1f, 0f, 0.5f),
        // Utsusemi's ticks land on every step and every telegraph beat, so they must be allowed close together.
        (Sfx.ClockTick, 0.6f, 0f, 0.04f),
        (Sfx.GearGrind, 0.9f, 0.05f, 0.4f),
        (Sfx.HatWhirr, 0.7f, 0.05f, 0.2f),
    };

    [MenuItem("Ninja/Apply Audio")]
    public static void Apply()
    {
        var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        var sounds = new List<SfxLibrary.Sound>();
        foreach (var (id, volume, jitter, minInterval) in Sounds)
        {
            AudioClip[] clips = LoadVariants(id);
            if (clips.Length == 0)
            {
                Debug.LogWarning($"No clips for sound {id} in {SfxRoot}; it will stay silent.");
                continue;
            }
            SfxLibrary.Sound sound = library.Find(id) ?? new SfxLibrary.Sound { id = id, volume = volume, pitchJitter = jitter, minInterval = minInterval };
            sound.clips = clips;
            sounds.Add(sound);
        }
        library.sounds = sounds.ToArray();
        library.titleMusic = LoadMusic("TitleMusic");
        library.battleMusic = LoadMusic("BattleMusic");
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = new SerializedObject(Object.FindAnyObjectByType<GameManager>());
        manager.FindProperty("sfxLibrary").objectReferenceValue = library;
        manager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Audio applied: {sounds.Count} sounds, title music {(library.titleMusic ? "set" : "missing")}, battle music {(library.battleMusic ? "set" : "missing")}.");
    }

    // <id>.mp3, then <id>_2.mp3, <id>_3.mp3, ... Short effects load decompressed and mono for instant playback.
    static AudioClip[] LoadVariants(string id)
    {
        var clips = new List<AudioClip>();
        for (int i = 1; ; i++)
        {
            string path = i == 1 ? $"{SfxRoot}{id}.mp3" : $"{SfxRoot}{id}_{i}.mp3";
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) break;
            Configure(importer, AudioClipLoadType.DecompressOnLoad, mono: true);
            clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        return clips.ToArray();
    }

    // Music streams from disk so long tracks don't sit decompressed in memory.
    static AudioClip LoadMusic(string id)
    {
        string path = $"{MusicRoot}{id}.mp3";
        if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
        {
            Debug.LogWarning($"No music at {path}; that part of the game will be quiet.");
            return null;
        }
        Configure(importer, AudioClipLoadType.Streaming, mono: false);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void Configure(AudioImporter importer, AudioClipLoadType loadType, bool mono)
    {
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.loadType == loadType && settings.compressionFormat == AudioCompressionFormat.Vorbis && importer.forceToMono == mono) return;
        settings.loadType = loadType;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = mono;
        importer.SaveAndReimport();
    }
}
