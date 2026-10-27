using System;
using System.Collections.Generic;
using UnityEngine;

// The ElevenLabs sound effects and music, looked up by id. Built by Ninja/Apply Audio.
[CreateAssetMenu(menuName = "Ninja/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class Sound
    {
        public string id;
        [Tooltip("One is picked at random each time, so repeated sounds don't feel mechanical.")]
        public AudioClip[] clips;
        [Range(0, 1)] public float volume = 1f;
        [Tooltip("Random pitch change either way, as a fraction (0.05 = 5%).")]
        public float pitchJitter = 0.05f;
        [Tooltip("Real seconds before this sound can play again, so bursts of kills don't stack into noise.")]
        public float minInterval = 0.03f;
        [Tooltip("Seconds of each clip that are actually heard, measured by Apply Audio. The generated clips carry " +
                 "long silent tails; a voice is free for the next sound once this much has played.")]
        public float[] audible;
        [Tooltip("Plays on a source of its own that each new play restarts, so a rapid beat can't fill the shared pool.")]
        public bool ownSource;

        // How long clip i keeps a voice busy at normal pitch; the whole clip until Apply Audio has measured it.
        public float AudibleLength(int i) =>
            audible != null && i < audible.Length && audible[i] > 0f ? audible[i] : clips[i].length;
    }

    // A stage's music, keyed by the boss who ends it so it follows the stage if stages are reordered.
    [Serializable]
    public class StageMusic
    {
        public BossKind boss;
        [Tooltip("Plays through the stage's rounds. Empty plays battleMusic.")]
        public AudioClip rounds;
        [Tooltip("Plays once the boss's intro is over. Empty plays battleMusic.")]
        public AudioClip fight;
        [Tooltip("Takes over from the fight track when the boss's second form is revealed. Empty keeps the fight track.")]
        public AudioClip phase;
    }

    public Sound[] sounds;

    [Header("Music")]
    public AudioClip titleMusic;
    [Tooltip("The temple between runs. Empty plays titleMusic.")]
    public AudioClip templeMusic;
    [Tooltip("Played wherever a stage has no music of its own.")]
    public AudioClip battleMusic;
    public StageMusic[] stageMusic;
    [Range(0, 1)] public float musicVolume = 0.45f;

    public AudioClip RoundsMusic(BossKind boss) => OrBattle(MusicFor(boss)?.rounds);
    public AudioClip FightMusic(BossKind boss) => OrBattle(MusicFor(boss)?.fight);
    public AudioClip PhaseMusic(BossKind boss)
    {
        AudioClip phase = MusicFor(boss)?.phase;
        return phase != null ? phase : FightMusic(boss);
    }
    public AudioClip TempleMusic => templeMusic != null ? templeMusic : titleMusic;

    // Not ??, which skips Unity's check for a clip that was deleted.
    AudioClip OrBattle(AudioClip clip) => clip != null ? clip : battleMusic;

    StageMusic MusicFor(BossKind boss)
    {
        if (stageMusic == null) return null;
        foreach (StageMusic music in stageMusic)
        {
            if (music != null && music.boss == boss) return music;
        }
        return null;
    }

    // Sounds are looked up on every hit and kill, so they are indexed by id on first use (and again if Apply
    // Audio swaps the array).
    [NonSerialized] Dictionary<string, Sound> byId;
    [NonSerialized] Sound[] indexed;

    public Sound Find(string id)
    {
        if (sounds == null || id == null) return null;
        if (byId == null || indexed != sounds)
        {
            byId = new Dictionary<string, Sound>();
            foreach (Sound sound in sounds)
            {
                if (sound.id != null && sound.clips != null && sound.clips.Length > 0 && !byId.ContainsKey(sound.id)) byId[sound.id] = sound;
            }
            indexed = sounds;
        }
        return byId.TryGetValue(id, out Sound found) ? found : null;
    }
}
