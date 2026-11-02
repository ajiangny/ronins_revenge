using System;
using UnityEngine;

// Oliver's pixel clips (Art/Sprites/Characters/Oliver/Oliver_<Clip>_<i>.png, wired by Ninja/Apply Maps with the rest of
// the temple). Any clip left empty plays his idle; with no idle at all he's a block of forge-gold and iron.
[Serializable]
public class OliverArt
{
    [Tooltip("Standing at the rack: a slow breath, the doll swaying, a spark off the hammer.")]
    public Sprite[] idle;
    [Tooltip("The head lifts a pixel; the mouth barely moves.")]
    public Sprite[] talk;
    [Tooltip("Asleep on the rack's platform, a mound with a doll beside it: how Oni finds him at the meeting.")]
    public Sprite[] loaf;
    [Tooltip("The hammer comes up and falls once, the only fast thing he does.")]
    public Sprite[] strike;
    public float fps = 4f;
}

// Oliver, the Smith's Cat (Story/Oliver.md): the first thing that was ever in the coffin, in a smith's bulk he built out
// of memory. He stands at the top of the weapon rack's dais once Oni has met him, a station of his own. Code-
// built by SafeArea at Oni's pixel size, which makes him about 1.8 times Oni's height, and animated on unscaled time so he
// keeps moving while a talk scene holds the temple still.
public class Oliver : MonoBehaviour
{
    public enum Clip { Idle, Talk, Loaf, Strike }

    // Without art: the placeholder's size in world units, about 1.8 times Oni's height.
    const float FallbackWidth = 1.6f, FallbackHeight = 1.9f;
    static readonly Color Iron = new Color(0.435f, 0.416f, 0.388f);
    // The Zs he breathes out asleep: how many at once, how long each rises, and how far up and across, in world units.
    const int SleepZs = 3;
    const float SleepZSeconds = 2.7f, SleepZRise = 2.1f, SleepZDrift = 0.6f;
    // Where they start, from his feet, in pixels of his art.
    static readonly Vector2 SleepZStart = new Vector2(16f, 64f);
    // Warm, like his hammer's heat: never a soul's teal.
    static readonly Color SleepZColour = new Color(1f, 0.93f, 0.72f);
    static readonly string[] SleepZRows = { "#####", "...#.", "..#..", ".#...", "#####" };
    static Sprite sleepZ;

    OliverArt art;
    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer shadow;
    bool hasArt;
    Sprite[] clip;
    bool clipLoop;
    float clipAt;
    float artPixel;
    SpriteRenderer[] zs;

    public SpriteRenderer Body => body;
    // His drawn extent.
    public Bounds Bounds => body.bounds;
    public bool HasClip(Clip which) => Has(FramesOf(which));

    static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;

    // His feet at `feet`, drawn at `pixel` world units a pixel, on `order` like the other props.
    public static Oliver Create(OliverArt art, Transform parent, Vector3 feet, float pixel, int order)
    {
        var oliver = new GameObject("Oliver").AddComponent<Oliver>();
        oliver.transform.SetParent(parent, false);
        oliver.transform.position = feet;
        oliver.art = art ?? new OliverArt();
        oliver.Build(pixel, order);
        return oliver;
    }

    void Build(float pixel, int order)
    {
        hasArt = Has(art.idle);
        artPixel = pixel;
        // A soft shadow where he meets the planks, as the other props have.
        shadow = Layer("Shadow", ProceduralSprites.Shadow, order - 2);
        shadow.color = new Color(0f, 0f, 0f, 0.45f);
        shadow.transform.localPosition = Vector3.up * 0.08f;
        // The hammer's heat, low on him: it's a forge's glow, never a soul's.
        glow = Layer("Glow", ProceduralSprites.SoftGlow, order - 1);
        glow.color = Color.clear;
        if (hasArt)
        {
            body = Layer("Body", art.idle[0], order);
            float scale = pixel * art.idle[0].pixelsPerUnit;
            body.transform.localScale = Vector3.one * scale;
            // His lowest drawn pixel stands on his feet (the tight mesh skips the canvas's empty rows).
            float bottom = float.MaxValue;
            foreach (Vector2 v in art.idle[0].vertices) bottom = Mathf.Min(bottom, v.y);
            if (bottom == float.MaxValue) bottom = art.idle[0].bounds.min.y;
            body.transform.localPosition = -Vector3.up * (bottom * scale);
            Play(Clip.Idle);
        }
        else
        {
            // No art yet: a wide block of iron with a forge-gold seam across it.
            body = Layer("Body", ProceduralSprites.White, order);
            Vector2 native = ProceduralSprites.White.bounds.size;
            body.transform.localScale = new Vector3(FallbackWidth / native.x, FallbackHeight / native.y, 1f);
            body.transform.localPosition = Vector3.up * (FallbackHeight / 2f);
            body.color = Iron;
            SpriteRenderer seam = Layer("Seam", ProceduralSprites.White, order + 1);
            seam.transform.localScale = new Vector3(FallbackWidth / native.x, 0.1f / native.y, 1f);
            seam.transform.localPosition = Vector3.up * (FallbackHeight * 0.3f);
            seam.color = Boons.Info(Element.Steel).Color;
        }
        Bounds b = body.bounds;
        shadow.transform.localScale = new Vector3(b.size.x * 1.1f / ProceduralSprites.Shadow.bounds.size.x, b.size.x * 0.32f / ProceduralSprites.Shadow.bounds.size.y, 1f);
        glow.transform.localPosition = Vector3.up * (b.size.y * 0.15f);
        glow.transform.localScale = new Vector3(b.size.x * 1.6f / ProceduralSprites.SoftGlow.bounds.size.x, b.size.y * 0.5f / ProceduralSprites.SoftGlow.bounds.size.y, 1f);
        // The roof beside the dais hides him (SafeArea); his Zs rise over it.
        body.maskInteraction = glow.maskInteraction = shadow.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        zs = new SpriteRenderer[SleepZs];
        for (int i = 0; i < SleepZs; i++)
        {
            zs[i] = Layer("SleepZ", SleepZ(), order + 1);
            zs[i].gameObject.SetActive(false);
        }
    }

    // A Z in five rows of pixels, one world unit a sprite pixel so its scale is set in Oni's pixels.
    static Sprite SleepZ()
    {
        if (sleepZ != null) return sleepZ;
        var texture = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                texture.SetPixel(x, 4 - y, SleepZRows[y][x] == '#' ? Color.white : Color.clear);
        texture.Apply();
        sleepZ = Sprite.Create(texture, new Rect(0f, 0f, 5f, 5f), new Vector2(0.5f, 0.5f), 1f);
        return sleepZ;
    }

    // Zs rise off him one after another while he sleeps, growing as they go, and fade out at the top.
    void UpdateSleepZs(float time)
    {
        bool asleep = hasArt && clip != null && clip == art.loaf;
        // Just over the top of his back: the canvas stands far taller than the mound, so its bounds won't do.
        Vector3 from = transform.position + new Vector3(SleepZStart.x * artPixel, SleepZStart.y * artPixel, 0f);
        for (int i = 0; i < SleepZs; i++)
        {
            zs[i].gameObject.SetActive(asleep);
            if (!asleep) continue;
            float t = Mathf.Repeat(time / SleepZSeconds + i / (float)SleepZs, 1f);
            // Whole pixels of size, so the Z stays crisp: 2 pixels of art per pixel, then 3.
            float size = artPixel * (t < 0.45f ? 2f : 3f);
            zs[i].transform.localScale = new Vector3(size, size, 1f);
            zs[i].transform.position = from + new Vector3(SleepZDrift * t + 0.12f * Mathf.Sin(t * Mathf.PI * 3f), SleepZRise * t, 0f);
            Color c = SleepZColour;
            c.a = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 3f);
            zs[i].color = c;
        }
    }

    SpriteRenderer Layer(string name, Sprite sprite, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = sprite;
        r.sortingOrder = order;
        return r;
    }

    Sprite[] FramesOf(Clip which) => which switch
    {
        Clip.Talk => art.talk,
        Clip.Loaf => art.loaf,
        Clip.Strike => art.strike,
        _ => art.idle,
    };

    // A clip once (the idle loops), falling back to the idle; nothing changes without art.
    public void Play(Clip which, bool loop = false)
    {
        if (!hasArt) return;
        Sprite[] frames = FramesOf(which);
        if (!Has(frames)) frames = art.idle;
        clip = frames;
        clipLoop = loop || frames == art.idle;
        clipAt = Time.unscaledTime;
    }

    // Drawn over or under Oni: the rack's sorting, kept by SafeArea each frame.
    public void SetOrder(int order)
    {
        body.sortingOrder = order;
        glow.sortingOrder = order - 1;
        shadow.sortingOrder = order - 2;
        foreach (SpriteRenderer z in zs) z.sortingOrder = order + 1;
        foreach (Transform child in transform)
            if (child.name == "Seam") child.GetComponent<SpriteRenderer>().sortingOrder = order + 1;
    }

    void Update()
    {
        float time = Time.unscaledTime;
        Color heat = Boons.Info(Element.Steel).Color;
        glow.color = new Color(heat.r, heat.g, heat.b, 0.07f + 0.03f * Mathf.Sin(time * 0.6f));
        UpdateSleepZs(time);
        if (!hasArt || clip == null) return;
        int frame = (int)((time - clipAt) * Mathf.Max(1f, art.fps));
        if (clipLoop) frame %= clip.Length;
        else if (frame >= clip.Length)
        {
            Play(Clip.Idle);
            return;
        }
        body.sprite = clip[frame];
    }
}
