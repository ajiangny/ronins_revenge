using System.Collections.Generic;
using UnityEngine;

// Spatial ripples and stopped-time colour for Utsusemi's time stop. Emit sends a thin ring out from (or in to) a
// point, bending the picture a few pixels as it passes; SetStop drains and cools the whole field while time is held.
// Drawn by TimeRippleFeature's full-screen pass; without it, a faint pixel ring outline stands in.
// Kept deliberately quiet: a few rings per move, never on ordinary hits.
public class TimeRipple : MonoBehaviour
{
    const int MaxRings = 4;
    // How far a ring at full strength pushes the picture, in view heights (about 3 px at 1080p).
    const float MaxPush = 0.0028f;
    // Half the width of a ring's band, in world units.
    const float BandHalfWidth = 0.45f;
    const float FallbackAlpha = 0.22f;

    static readonly int RingsId = Shader.PropertyToID("_RippleRings");
    static readonly int BandsId = Shader.PropertyToID("_RippleBands");
    static readonly int StopId = Shader.PropertyToID("_TimeStop");

    class Ring
    {
        public Vector3 centre;
        public float radius, strength, seconds, age, band;
        public bool inward;
        public SpriteRenderer fallback;
    }

    static TimeRipple instance;
    readonly List<Ring> rings = new List<Ring>();
    readonly Vector4[] packed = new Vector4[MaxRings];
    readonly Vector4[] bands = new Vector4[MaxRings];
    float stop;

    // Whether the full-screen pass has anything to draw this frame.
    public static bool Live => instance != null && (instance.rings.Count > 0 || instance.stop > 0.001f);

    // A ring from centre out to radius (or, inward, from radius in to centre) over seconds. Strength 1 is the quiet
    // default (about 3 px at 1080p); up to MaxStrength for a hard warp, kept brief. Band is the ring's half width.
    public static void Emit(Vector3 centre, float radius, float strength, float seconds, bool inward = false, float band = BandHalfWidth)
    {
        TimeRipple self = Instance();
        if (self == null) return;
        if (self.rings.Count >= MaxRings) self.Remove(0);
        self.rings.Add(new Ring { centre = centre, radius = radius, strength = Mathf.Clamp(strength, 0f, MaxStrength), seconds = Mathf.Max(0.05f, seconds), inward = inward, band = Mathf.Max(0.1f, band) });
    }

    const float MaxStrength = 3f;

    // How stopped the world looks, 0 (normal) to 1 (drained and cool).
    public static void SetStop(float amount)
    {
        TimeRipple self = Instance();
        if (self != null) self.stop = Mathf.Clamp01(amount);
    }

    // Clears everything at once (a boss halting, the scene ending).
    public static void Clear()
    {
        if (instance == null) return;
        while (instance.rings.Count > 0) instance.Remove(0);
        instance.stop = 0f;
        instance.Push();
    }

    static TimeRipple Instance()
    {
        if (instance != null) return instance;
        Camera cam = Camera.main;
        if (cam == null) return null;
        instance = cam.GetComponent<TimeRipple>();
        if (instance == null) instance = cam.gameObject.AddComponent<TimeRipple>();
        return instance;
    }

    // Also runs after the Editor reloads scripts mid-play, which clears the static.
    void OnEnable()
    {
        instance = this;
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(StopId, 0f);
        Shader.SetGlobalVectorArray(RingsId, new Vector4[MaxRings]);
    }

    // Scaled time, so a pause or a hit-stop holds the rings where they are.
    void Update()
    {
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            rings[i].age += Time.deltaTime;
            if (rings[i].age >= rings[i].seconds) Remove(i);
        }
        Push();
    }

    void Remove(int index)
    {
        if (rings[index].fallback != null) Destroy(rings[index].fallback.gameObject);
        rings.RemoveAt(index);
    }

    void Push()
    {
        Camera cam = GetComponent<Camera>();
        float viewHeight = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
        // The pass reports in every frame it's on the renderer; a few frames' silence means it isn't there.
        bool passReady = Time.frameCount - TimeRippleFeature.LastSeenFrame <= 3;

        for (int i = 0; i < MaxRings; i++)
        {
            packed[i] = Vector4.zero;
            bands[i] = new Vector4(BandHalfWidth / viewHeight, 0, 0, 0);
            if (i >= rings.Count) continue;
            Ring ring = rings[i];
            float t = ring.age / ring.seconds;
            // Eased out, so a ring leaves quickly and settles; inward rings run the same curve backwards.
            float travel = 1f - (1f - t) * (1f - t);
            float radius = ring.radius * (ring.inward ? 1f - travel : travel);
            // Fades in over the first moment (no pop) and out as it spreads.
            float strength = ring.strength * Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - t);

            if (passReady)
            {
                if (ring.fallback != null) ring.fallback.enabled = false;
                // The live camera, not CameraShake.Center: this bends the picture as drawn, shake and all.
                Vector3 view = cam != null ? cam.WorldToViewportPoint(ring.centre) : new Vector3(0.5f, 0.5f, 0f);
                packed[i] = new Vector4(view.x, view.y, radius / viewHeight, strength * MaxPush);
                bands[i] = new Vector4(ring.band / viewHeight, 0, 0, 0);
            }
            else
            {
                DrawFallback(ring, radius, strength);
            }
        }

        Shader.SetGlobalVectorArray(RingsId, packed);
        Shader.SetGlobalVectorArray(BandsId, bands);
        Shader.SetGlobalFloat(StopId, passReady ? stop : 0f);
    }

    // A faint one-pixel ring for renderers without the pass. ProceduralSprites.Ring is squashed for the pool's
    // perspective, so it's stretched back to round here.
    void DrawFallback(Ring ring, float radius, float strength)
    {
        if (ring.fallback == null)
        {
            var go = new GameObject("TimeRippleRing");
            ring.fallback = go.AddComponent<SpriteRenderer>();
            ring.fallback.sprite = ProceduralSprites.Ring(64);
            ring.fallback.sortingOrder = 60;
        }
        ring.fallback.enabled = radius > 0.05f;
        float scale = radius / 64f;
        ring.fallback.transform.position = ring.centre;
        ring.fallback.transform.localScale = new Vector3(scale, scale / 0.55f, 1f);
        ring.fallback.color = new Color(0.85f, 0.92f, 1f, FallbackAlpha * Mathf.Min(1f, strength));
    }
}
