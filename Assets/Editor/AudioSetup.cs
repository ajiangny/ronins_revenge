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

    // Below this a sample counts as silence when measuring where a clip's sound ends.
    const float SilenceLevel = 0.02f;
    // Kept after the last loud sample so a fading tail isn't clipped.
    const float TailPadding = 0.08f;

    // The march beat comes up to ten times a second; on a source of its own it can't crowd out every other sound.
    // The beam hum loops on its own source for as long as a laser burns.
    static readonly HashSet<string> OwnSource = new HashSet<string> { Sfx.MarchStep, Sfx.LanternBeam, Sfx.SpiritBeam, Sfx.SoulWail, Sfx.PoolAmbience };

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
        (Sfx.HatClang, 0.9f, 0.05f, 0.2f),
        (Sfx.LanternCharge, 0.8f, 0f, 0.5f),
        (Sfx.LanternBeam, 0.6f, 0f, 0.1f),
        (Sfx.MidnightChime, 1f, 0f, 0.5f),
        (Sfx.BladeChop, 0.9f, 0.08f, 0.08f),
        (Sfx.HourSlash, 1f, 0f, 0.5f),
        (Sfx.SwordWhirl, 0.8f, 0.04f, 0.3f),
        (Sfx.SwordPlant, 1f, 0f, 0.3f),
        (Sfx.WolfGrowl, 0.8f, 0.05f, 0.3f),
        (Sfx.WolfHowl, 1f, 0f, 0.8f),
        (Sfx.BladeRing, 0.7f, 0.06f, 0.1f),
        // A rain of swords falls close together.
        (Sfx.GhostSwordFall, 0.6f, 0.1f, 0.05f),
        (Sfx.SpiritBeam, 0.7f, 0f, 0.1f),
        (Sfx.MistSwirl, 0.6f, 0.08f, 0.15f),
        // The Emperor's bells clamour close together in his phase break and his ultimate.
        (Sfx.BellToll, 0.8f, 0.03f, 0.05f),
        (Sfx.BellSilenced, 0.7f, 0.05f, 0.1f),
        (Sfx.HammerSlam, 1f, 0.04f, 0.2f),
        (Sfx.ChainSnap, 0.85f, 0.08f, 0.08f),
        (Sfx.SoulWail, 0.7f, 0f, 0.1f),
        (Sfx.OfudaFlutter, 0.7f, 0.05f, 0.3f),
        (Sfx.SkullShriek, 0.6f, 0.08f, 0.2f),
        (Sfx.DawnChime, 0.9f, 0f, 1f),
        // The temple pool: quiet steps close together, and its murmur under the music.
        (Sfx.WaterStep, 0.45f, 0.1f, 0.2f),
        (Sfx.PoolSplash, 0.6f, 0.06f, 0.3f),
        (Sfx.PoolAmbience, 0.35f, 0f, 1f),
        // The temple gate's doors.
        (Sfx.GateOpen, 0.55f, 0.05f, 0.5f),
        (Sfx.GateClose, 0.5f, 0.05f, 0.5f),
        // The souls' stings, under their first line.
        (Sfx.SoulStingFire, 0.6f, 0f, 1f),
        (Sfx.SoulStingFrost, 0.6f, 0f, 1f),
        (Sfx.SoulStingStorm, 0.6f, 0f, 1f),
        (Sfx.TaikoHit, 0.8f, 0.03f, 0.04f),
        (Sfx.TaikoThunder, 0.8f, 0.02f, 1f),
        (Sfx.SoulStingWater, 0.6f, 0f, 1f),
        (Sfx.SoulStingSteel, 0.6f, 0f, 1f),
        // Asagi's water.
        (Sfx.TideWave, 0.7f, 0.05f, 0.3f),
        (Sfx.Splash, 0.5f, 0.1f, 0.15f),
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
            sound.audible = System.Array.ConvertAll(clips, AudibleSeconds);
            sound.ownSource = OwnSource.Contains(id);
            sounds.Add(sound);
        }
        library.sounds = sounds.ToArray();
        library.titleMusic = LoadMusic("TitleMusic");
        library.templeMusic = LoadMusic("TempleMusic");
        library.battleMusic = LoadMusic("BattleMusic");
        // Stage_<Boss>.mp3 for the rounds, Boss_<Boss>.mp3 for the fight (a missing one falls back to BattleMusic) and
        // Phase_<Boss>.mp3 for the second form (missing keeps the fight track).
        var stageMusic = new List<SfxLibrary.StageMusic>();
        foreach (BossKind boss in System.Enum.GetValues(typeof(BossKind)))
        {
            if (boss == BossKind.None) continue;
            stageMusic.Add(new SfxLibrary.StageMusic { boss = boss, rounds = LoadMusic($"Stage_{boss}"), fight = LoadMusic($"Boss_{boss}"), phase = LoadMusic($"Phase_{boss}") });
        }
        library.stageMusic = stageMusic.ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = new SerializedObject(Object.FindAnyObjectByType<GameManager>());
        manager.FindProperty("sfxLibrary").objectReferenceValue = library;
        manager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Audio applied: {sounds.Count} sounds, title music {(library.titleMusic ? "set" : "missing")}, battle music {(library.battleMusic ? "set" : "missing")}, temple music {(library.templeMusic ? "set" : "missing")}, {CountStageTracks(library)} stage, boss and phase tracks.");
    }

    static int CountStageTracks(SfxLibrary library)
    {
        int count = 0;
        foreach (SfxLibrary.StageMusic music in library.stageMusic)
        {
            if (music.rounds != null) count++;
            if (music.fight != null) count++;
            if (music.phase != null) count++;
        }
        return count;
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

    // Where the clip's sound actually ends: the generated clips are padded with silence to ElevenLabs' minimum lengths.
    static float AudibleSeconds(AudioClip clip)
    {
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        var samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0)) return clip.length;
        int last = samples.Length - 1;
        while (last > 0 && Mathf.Abs(samples[last]) < SilenceLevel) last--;
        return Mathf.Min(clip.length, (float)last / clip.channels / clip.frequency + TailPadding);
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
