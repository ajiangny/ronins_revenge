using System.Collections.Generic;
using UnityEngine;

// The mood laid over a map: cloud shadows drifting across the ground, a pool of moonlight from the moon's corner, low
// mist, drifting motes (fireflies, embers, dust) and darkened edges. Every texture is built in code, so it needs no art.
public class Atmosphere : MonoBehaviour
{
    // Shadows, mist and light lie on the ground under everything that moves, so the field stays readable; the motes and
    // the vignette float over it.
    const int ShadowOrder = -96;
    const int MistOrder = -95;
    const int ShadeOrder = -94;
    const int LightOrder = -93;
    const int MoteOrder = 58;
    const int VignetteOrder = 60;

    // One map's settings. Zero counts and clear colours switch a part off.
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Cloud shadows drifting over the ground at once.")]
        public int clouds;
        public Color cloudShadow = new Color(0.02f, 0.03f, 0.08f, 0.3f);
        [Tooltip("The wind: world units per second that clouds and mist drift.")]
        public Vector2 wind = new Vector2(0.8f, -0.3f);
        [Tooltip("A soft glow on the ground under the moon, in the top-left; alpha is its strength.")]
        public Color moonlight = Color.clear;
        [Tooltip("How much darker the ground falls away from the moon, 0 to 1, so the light reads as coming from it.")]
        public float shade = 0.35f;
        [Tooltip("How dark the edges of the view fall, 0 to 1.")]
        public float vignette = 0.3f;
        [Tooltip("Banks of low mist rolling across the ground.")]
        public int mist;
        public Color mistColor = new Color(0.75f, 0.8f, 0.9f, 0.15f);
        [Tooltip("Small drifting lights: fireflies, embers, dust.")]
        public int motes;
        public Color moteColor = new Color(0.85f, 1f, 0.5f, 0.8f);
    }

    class Drifter
    {
        public SpriteRenderer sprite;
        public Vector2 velocity;
        public float phase, rate, alpha, size;
    }

    readonly List<Drifter> clouds = new List<Drifter>();
    readonly List<Drifter> mists = new List<Drifter>();
    readonly List<Drifter> motes = new List<Drifter>();
    Settings settings;
    SpriteRenderer moonlight;
    SpriteRenderer shade;
    SpriteRenderer vignette;
    Color moonTint = Color.white;
    Vector2 centre;
    float halfWidth, halfHeight;
    float z;

    static Sprite[] cloudSprites;
    static Sprite mistSprite, lightSprite, shadeSprite, vignetteSprite;

    public void Init(Vector2 viewCentre, float viewHalfWidth, float viewHalfHeight, float depth)
    {
        centre = viewCentre;
        halfWidth = viewHalfWidth;
        halfHeight = viewHalfHeight;
        z = depth;
    }

    // Swaps to another map's mood. Called under the travel fade, so the change is instant.
    public void Apply(Settings mood)
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        clouds.Clear();
        mists.Clear();
        motes.Clear();
        moonlight = shade = vignette = null;
        settings = mood ?? new Settings();
        BuildSprites();

        for (int i = 0; i < settings.clouds; i++)
        {
            // Spread over the field to start, so the sky isn't empty while the first ones drift in.
            var cloud = Spawn(clouds, "CloudShadow", cloudSprites[i % cloudSprites.Length], ShadowOrder, Random.Range(0.7f, 1.1f), settings.cloudShadow);
            cloud.velocity = settings.wind * Random.Range(0.8f, 1.2f);
            cloud.sprite.flipX = Random.value < 0.5f;
        }
        for (int i = 0; i < settings.mist; i++)
        {
            var bank = Spawn(mists, "Mist", mistSprite, MistOrder, Random.Range(0.5f, 0.8f), settings.mistColor);
            bank.velocity = new Vector2(settings.wind.x * Random.Range(0.4f, 0.7f), 0f);
            bank.rate = Random.Range(0.15f, 0.3f);
        }
        for (int i = 0; i < settings.motes; i++)
        {
            var mote = Spawn(motes, "Mote", ProceduralSprites.Glow, MoteOrder, Random.Range(0.35f, 0.6f), settings.moteColor);
            mote.velocity = Random.insideUnitCircle * 0.6f + settings.wind * 0.3f;
            mote.rate = Random.Range(1.5f, 3.5f);
        }

        if (settings.moonlight.a > 0f)
        {
            // A pool under the moon's corner, reaching about a third of the way into the field.
            moonlight = Create("Moonlight", lightSprite, new Vector2(centre.x - halfWidth * 0.8f, centre.y + halfHeight * 0.75f), LightOrder);
            moonlight.transform.localScale = Vector3.one * (halfWidth * 1.4f / lightSprite.bounds.size.x);
        }
        if (settings.shade > 0f)
        {
            shade = Create("MoonShade", shadeSprite, centre, ShadeOrder);
            Vector2 size = shadeSprite.bounds.size;
            shade.transform.localScale = new Vector3(2f * halfWidth / size.x, 2f * halfHeight / size.y, 1f);
            shade.color = new Color(0f, 0.01f, 0.04f, settings.shade);
        }
        if (settings.vignette > 0f)
        {
            vignette = Create("Vignette", vignetteSprite, centre, VignetteOrder);
            Vector2 size = vignetteSprite.bounds.size;
            vignette.transform.localScale = new Vector3(2f * halfWidth / size.x, 2f * halfHeight / size.y, 1f);
            vignette.color = new Color(0f, 0f, 0.02f, settings.vignette);
        }
    }

    // Shades the moonlight with the boss moon (blood red, cold steel) by the given amount.
    public void TintMoon(Color tint, float amount)
    {
        moonTint = Color.Lerp(Color.white, tint, amount);
    }

    Drifter Spawn(List<Drifter> list, string name, Sprite sprite, int order, float size, Color colour)
    {
        var drifter = new Drifter
        {
            sprite = Create(name, sprite, centre + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight)), order),
            phase = Random.Range(0f, Mathf.PI * 2f),
            alpha = colour.a,
            size = size,
        };
        drifter.sprite.transform.localScale = Vector3.one * size;
        drifter.sprite.color = colour;
        list.Add(drifter);
        return drifter;
    }

    SpriteRenderer Create(string name, Sprite sprite, Vector2 position, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(position.x, position.y, z);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void Update()
    {
        if (settings == null) return;
        float dt = Time.deltaTime;
        float t = Time.time;

        foreach (Drifter cloud in clouds) Drift(cloud, cloud.velocity * dt);
        foreach (Drifter bank in mists)
        {
            Drift(bank, bank.velocity * dt);
            // Mist thickens and thins as it rolls.
            Color c = bank.sprite.color;
            c.a = bank.alpha * (0.7f + 0.3f * Mathf.Sin(t * bank.rate + bank.phase));
            bank.sprite.color = c;
        }
        foreach (Drifter mote in motes)
        {
            // Wanders on a lazy curve and pulses like a firefly.
            Vector2 wander = new Vector2(Mathf.Sin(t * 0.7f + mote.phase), Mathf.Cos(t * 0.5f + mote.phase * 1.3f)) * 0.4f;
            Drift(mote, (mote.velocity + wander) * dt);
            Color c = mote.sprite.color;
            c.a = mote.alpha * Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(t * mote.rate + mote.phase));
            mote.sprite.color = c;
        }

        if (moonlight != null)
        {
            // The moonlight breathes as unseen clouds pass over the moon.
            Color c = settings.moonlight * moonTint;
            c.a = settings.moonlight.a * (0.85f + 0.15f * Mathf.Sin(t * 0.25f));
            moonlight.color = c;
        }
    }

    // Moves a drifter and wraps it to the far side once it has fully left the field.
    void Drift(Drifter drifter, Vector2 step)
    {
        Transform tr = drifter.sprite.transform;
        Vector3 p = tr.position + (Vector3)step;
        Vector2 extent = drifter.sprite.bounds.extents;
        float left = centre.x - halfWidth - extent.x, right = centre.x + halfWidth + extent.x;
        float bottom = centre.y - halfHeight - extent.y, top = centre.y + halfHeight + extent.y;
        if (p.x > right) p.x = left;
        else if (p.x < left) p.x = right;
        if (p.y < bottom) p.y = top;
        else if (p.y > top) p.y = bottom;
        tr.position = p;
    }

    // Clouds and mist have their alpha stepped into a few bands, so they read as painted pixel-art shapes; the
    // screen-wide light, shade and vignette are stepped finely, since coarse steps there show as hard edges.
    static void BuildSprites()
    {
        if (cloudSprites != null) return;
        cloudSprites = new Sprite[3];
        for (int i = 0; i < cloudSprites.Length; i++) cloudSprites[i] = Noise(96, 64, 7.3f * (i + 1), 4f, 4);
        mistSprite = Noise(128, 32, 41.7f, 4f, 6);
        lightSprite = Radial(64, 64, 32, 1.6f, 4f);
        vignetteSprite = Edges(64, 36, 32, 2f);
        shadeSprite = AwayFromMoon(64, 36, 32);
    }

    // A cloud-like blob: layered noise faded out towards an ellipse's rim.
    static Sprite Noise(int width, int height, float seed, float pixelsPerUnit, int bands)
    {
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f, v = (y + 0.5f) / height * 2f - 1f;
                float rim = Mathf.Clamp01(1f - (u * u + v * v));
                float n = 0f, amp = 0.5f, freq = 3f;
                for (int o = 0; o < 4; o++)
                {
                    n += amp * Mathf.PerlinNoise(seed + x * freq / width, seed * 0.5f + y * freq / width);
                    amp *= 0.5f;
                    freq *= 2f;
                }
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Band(Mathf.Clamp01((n * rim * 2.2f - 0.25f) * 1.6f), bands)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // Brightest in the middle, falling away to nothing at the rim.
    static Sprite Radial(int width, int height, int bands, float falloff, float pixelsPerUnit)
    {
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f, v = (y + 0.5f) / height * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), falloff);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Band(a, bands)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // Clear in the middle, darkening towards the edges and most in the corners.
    static Sprite Edges(int width, int height, int bands, float falloff)
    {
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f, v = (y + 0.5f) / height * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(u * u * 0.8f + v * v) - 0.55f) / 0.75f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Band(Mathf.Pow(d, falloff), bands)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
    }

    // Clear at the top-left, where the moon is, deepening towards the far corner.
    static Sprite AwayFromMoon(int width, int height, int bands)
    {
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width, v = 1f - (y + 0.5f) / height;
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) / 1.3f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Band(Mathf.SmoothStep(0f, 1f, d), bands)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
    }

    // The sprites made from these use full-rect meshes: a tight outline would cut straight edges through their faint rims.
    static Texture2D NewTexture(int width, int height) =>
        new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

    static float Band(float value, int bands) => Mathf.Floor(value * bands + 0.5f) / bands;
}
