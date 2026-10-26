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
    public const string ClockTick = "ClockTick";
    public const string GearGrind = "GearGrind";
    public const string HatWhirr = "HatWhirr";
    public const string HatClang = "HatClang";
    public const string LanternCharge = "LanternCharge";
    public const string LanternBeam = "LanternBeam";
    public const string MidnightChime = "MidnightChime";
    public const string BladeChop = "BladeChop";
    public const string HourSlash = "HourSlash";
    public const string SwordWhirl = "SwordWhirl";
    public const string SwordPlant = "SwordPlant";
    // Shirogane and her wolf.
    public const string WolfGrowl = "WolfGrowl";
    public const string WolfHowl = "WolfHowl";
    public const string BladeRing = "BladeRing";
    public const string GhostSwordFall = "GhostSwordFall";
    public const string SpiritBeam = "SpiritBeam";
    public const string MistSwirl = "MistSwirl";
    // The Emperor's: his bells, the coffin-hammer, the chains, the souls' wail, and the dawn.
    public const string BellToll = "BellToll";
    public const string BellSilenced = "BellSilenced";
    public const string HammerSlam = "HammerSlam";
    public const string ChainSnap = "ChainSnap";
    public const string SoulWail = "SoulWail";
    public const string OfudaFlutter = "OfudaFlutter";
    public const string SkullShriek = "SkullShriek";
    public const string DawnChime = "DawnChime";

    const int VoiceCount = 16;

    public static SfxLibrary Library { get; set; }

    // The player's sound-effect volume (0-1) from the settings menu, kept between sessions.
    public static float Volume
    {
        get => savedVolume >= 0f ? savedVolume : savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            savedVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, savedVolume);
            PlayerPrefs.Save();
        }
    }

    const string VolumeKey = "SfxVolume";
    static float savedVolume = -1f;

    static AudioSource[] voices;
    // When each voice's sound has finished being heard (unscaled time); after that it can be reused even while its
    // clip's silent tail is still playing.
    static float[] busyUntil;
    static readonly Dictionary<string, AudioSource> ownSources = new Dictionary<string, AudioSource>();
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    // The project enters Play mode without a domain reload, so statics survive from the last session. Its
    // timestamps are from a clock that has since restarted near zero and would hold every sound back until
    // the new clock caught up, and its voices were destroyed with the old scene.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        voices = null;
        busyUntil = null;
        ownSources.Clear();
        lastPlayed.Clear();
    }

    // Whether a sound has clips, so a caller can fall back on an older one until it's generated.
    public static bool Has(string id) => Library != null && Library.Find(id) != null;

    public static void Play(string id, float volume = 1f, float pitch = 1f)
    {
        SfxLibrary.Sound sound = Library != null ? Library.Find(id) : null;
        if (sound == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < sound.minInterval) return;
        lastPlayed[id] = now;

        int pick = Random.Range(0, sound.clips.Length);
        float finalPitch = pitch * (1f + Random.Range(-sound.pitchJitter, sound.pitchJitter));
        AudioSource voice;
        if (sound.ownSource)
        {
            voice = OwnSource(id);
        }
        else
        {
            int index = NextVoice(now);
            voice = voices[index];
            // A lower pitch plays the clip slower, so its sound lasts longer.
            busyUntil[index] = now + sound.AudibleLength(pick) / Mathf.Max(0.1f, finalPitch);
        }
        voice.clip = sound.clips[pick];
        voice.volume = sound.volume * volume * Volume;
        voice.pitch = finalPitch;
        voice.Play();
    }

    // A held sound on its own source (a beam's hum), looping until Stop. Needs the sound marked ownSource.
    public static void PlayLoop(string id, float volume = 1f, float pitch = 1f)
    {
        SfxLibrary.Sound sound = Library != null ? Library.Find(id) : null;
        if (sound == null || !sound.ownSource) return;
        AudioSource source = OwnSource(id);
        source.clip = sound.clips[Random.Range(0, sound.clips.Length)];
        source.volume = sound.volume * volume * Volume;
        source.pitch = pitch;
        source.loop = true;
        source.Play();
    }

    public static void Stop(string id)
    {
        if (!ownSources.TryGetValue(id, out AudioSource source) || source == null) return;
        source.loop = false;
        source.Stop();
    }

    // A voice whose sound has finished if there is one, otherwise the one that will finish soonest.
    static int NextVoice(float now)
    {
        if (voices == null || voices[0] == null) CreateVoices();
        int soonest = 0;
        for (int i = 0; i < VoiceCount; i++)
        {
            if (busyUntil[i] <= now || !voices[i].isPlaying) return i;
            if (busyUntil[i] < busyUntil[soonest]) soonest = i;
        }
        return soonest;
    }

    static AudioSource OwnSource(string id)
    {
        if (voices == null || voices[0] == null) CreateVoices();
        if (!ownSources.TryGetValue(id, out AudioSource source) || source == null)
        {
            source = voices[0].gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            ownSources[id] = source;
        }
        return source;
    }

    static void CreateVoices()
    {
        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        voices = new AudioSource[VoiceCount];
        busyUntil = new float[VoiceCount];
        ownSources.Clear();
        for (int i = 0; i < VoiceCount; i++)
        {
            voices[i] = go.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
    }
}
