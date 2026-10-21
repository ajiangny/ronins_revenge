using System;
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
    }

    public Sound[] sounds;

    [Header("Music")]
    public AudioClip titleMusic;
    public AudioClip battleMusic;
    [Range(0, 1)] public float musicVolume = 0.45f;

    public Sound Find(string id)
    {
        if (sounds == null) return null;
        foreach (Sound sound in sounds)
        {
            if (sound.id == id && sound.clips != null && sound.clips.Length > 0) return sound;
        }
        return null;
    }
}
