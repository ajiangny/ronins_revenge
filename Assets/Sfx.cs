using System.Collections.Generic;
using UnityEngine;

// Plays the sounds in the SfxLibrary through a small pool of 2D voices that outlives scene reloads.
// When the library or a sound is missing, Play does nothing. Audio ignores Time.timeScale, so sounds
// play normally through the parry hit-stop and the paused boon screen.
public static class Sfx
{
    public const string SwordSlash = "SwordSlash";
    public const string KunaiThrow = "KunaiThrow";
    public const string ShurikenThrow = "ShurikenThrow";
    public const string ParrySwing = "ParrySwing";
    public const string Deflect = "Deflect";
    public const string ArrowFire = "ArrowFire";
    public const string SamuraiDeath = "SamuraiDeath";
    public const string PlayerHit = "PlayerHit";
    public const string WardShatter = "WardShatter";
    public const string PlayerDeath = "PlayerDeath";
    public const string BlockerHit = "BlockerHit";
    public const string BlockerBreak = "BlockerBreak";
    public const string SoulSpawn = "SoulSpawn";
    public const string SoulCollect = "SoulCollect";
    public const string FireBlast = "FireBlast";
    public const string LightningStrike = "LightningStrike";
    public const string FrostChill = "FrostChill";
    public const string UiMove = "UiMove";
    public const string UiConfirm = "UiConfirm";
    public const string StageStart = "StageStart";
    public const string VictorySting = "VictorySting";
    public const string DefeatSting = "DefeatSting";
    public const string MarchStep = "MarchStep";
    public const string ParryClang = "ParryClang";
    public const string SamuraiCry = "SamuraiCry";
    public const string PlayerHurt = "PlayerHurt";
    public const string BossHit = "BossHit";
    public const string BossStagger = "BossStagger";

    const int VoiceCount = 16;

    public static SfxLibrary Library { get; set; }

    static AudioSource[] voices;
    static int nextVoice;
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    public static void Play(string id, float volume = 1f, float pitch = 1f)
    {
        SfxLibrary.Sound sound = Library != null ? Library.Find(id) : null;
        if (sound == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < sound.minInterval) return;
        lastPlayed[id] = now;

        AudioSource voice = NextVoice();
        voice.clip = sound.clips[Random.Range(0, sound.clips.Length)];
        voice.volume = sound.volume * volume;
        voice.pitch = pitch * (1f + Random.Range(-sound.pitchJitter, sound.pitchJitter));
        voice.Play();
    }

    // A free voice if there is one, otherwise the one that started longest ago.
    static AudioSource NextVoice()
    {
        if (voices == null || voices[0] == null) CreateVoices();
        for (int i = 0; i < VoiceCount; i++)
        {
            AudioSource voice = voices[(nextVoice + i) % VoiceCount];
            if (!voice.isPlaying)
            {
                nextVoice = (nextVoice + i + 1) % VoiceCount;
                return voice;
            }
        }
        AudioSource oldest = voices[nextVoice];
        nextVoice = (nextVoice + 1) % VoiceCount;
        return oldest;
    }

    static void CreateVoices()
    {
        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            voices[i] = go.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
    }
}
