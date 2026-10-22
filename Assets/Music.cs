using UnityEngine;

// Background music on two looping decks that crossfade in real time. It outlives scene reloads, so a
// track that's already playing carries on instead of restarting.
public class Music : MonoBehaviour
{
    static Music instance;

    readonly AudioSource[] decks = new AudioSource[2];
    readonly float[] targets = new float[2];
    int live;
    float fadeSpeed = 1f;
    float duckLevel = 1f;
    float duckUntil;

    // The player's music volume (0-1) from the settings menu, kept between sessions.
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

    const string VolumeKey = "MusicVolume";
    static float savedVolume = -1f;

    public static void Play(AudioClip clip, float volume, float fadeSeconds = 1f)
    {
        if (clip == null) return;
        Music music = Instance;
        music.fadeSpeed = 1f / Mathf.Max(fadeSeconds, 0.01f);

        AudioSource current = music.decks[music.live];
        if (current.clip == clip && current.isPlaying)
        {
            music.targets[music.live] = volume;
            return;
        }

        music.targets[music.live] = 0f;
        music.live = 1 - music.live;
        AudioSource next = music.decks[music.live];
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        music.targets[music.live] = volume;
    }

    // Lowers the music to `level` of its volume for a while, under a voiceline.
    public static void Duck(float level, float seconds)
    {
        Music music = Instance;
        music.duckLevel = level;
        music.duckUntil = Time.unscaledTime + seconds;
    }

    public static void Stop(float fadeSeconds = 1f)
    {
        if (instance == null) return;
        instance.fadeSpeed = 1f / Mathf.Max(fadeSeconds, 0.01f);
        instance.targets[0] = instance.targets[1] = 0f;
    }

    static Music Instance
    {
        get
        {
            if (instance != null) return instance;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Music>();
            for (int i = 0; i < 2; i++)
            {
                AudioSource deck = go.AddComponent<AudioSource>();
                deck.loop = true;
                deck.playOnAwake = false;
                instance.decks[i] = deck;
            }
            return instance;
        }
    }

    void Update()
    {
        // Applied here rather than baked into targets, so the settings slider works while a track plays.
        float volumeSetting = Volume;
        for (int i = 0; i < 2; i++)
        {
            AudioSource deck = decks[i];
            if (!deck.isPlaying) continue;
            float target = targets[i] * volumeSetting * (Time.unscaledTime < duckUntil ? duckLevel : 1f);
            // Ducking eases in and out quickly whatever the crossfade speed.
            deck.volume = Mathf.MoveTowards(deck.volume, target, Mathf.Max(fadeSpeed, 2f) * Time.unscaledDeltaTime);
            if (deck.volume <= 0f && targets[i] <= 0f) deck.Stop();
        }
    }
}
