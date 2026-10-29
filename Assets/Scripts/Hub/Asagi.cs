using System;
using UnityEngine;

// Asagi's pixel clips (Art/Sprites/Characters/Asagi/Asagi_<Clip>_<i>.png, wired by Ninja/Apply Maps with the rest of
// the temple). Any clip left empty plays her idle; with no idle at all she's a column of her sea-blue glow.
[Serializable]
public class AsagiArt
{
    [Tooltip("Standing at the rim: the koi drifting in her bowl, a bubble now and then.")]
    public Sprite[] idle;
    [Tooltip("The hands come up while she speaks.")]
    public Sprite[] talk;
    [Tooltip("Pulling Oni out of the pool, on the wake she's met.")]
    public Sprite[] lift;
    public float fps = 6f;
}

// Asagi, the Pond Physician (Story/Asagi.md): the koi of the temple pond, in a made body with a glass bowl for a head.
// She stands on the spirit pool's back-left rim once Oni has met her, and is the temple's Asagi station. Code-
// built by SafeArea at Oni's pixel size, and animated on unscaled time so she keeps moving while a talk scene holds
// the temple still.
public class Asagi : MonoBehaviour
{
    public enum Clip { Idle, Talk, Lift }

    // The Water element's sea-blue (#2F7BFF), kept well apart from Frost's ice-blue.
    public static readonly Color Colour = new Color(0.184f, 0.482f, 1f);
    // Without art: the placeholder's size in world units, about Oni's height.
    const float FallbackWidth = 0.9f, FallbackHeight = 2.1f;

    AsagiArt art;
    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer shadow;
    bool hasArt;
    Sprite[] clip;
    bool clipLoop;
    float clipAt;
    float nextDrop;

    public SpriteRenderer Body => body;
    // Her drawn extent, for sorting against Oni and floating the prompt over her.
    public Bounds Bounds => body.bounds;
    public bool HasClip(Clip which) => Has(FramesOf(which));

    static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;

    // Her feet at `feet`, drawn at `pixel` world units a pixel, on `order` like the other props.
    public static Asagi Create(AsagiArt art, Transform parent, Vector3 feet, float pixel, int order)
    {
        var asagi = new GameObject("Asagi").AddComponent<Asagi>();
        asagi.transform.SetParent(parent, false);
        asagi.transform.position = feet;
        asagi.art = art ?? new AsagiArt();
        asagi.Build(pixel, order);
        return asagi;
    }

    void Build(float pixel, int order)
    {
        hasArt = Has(art.idle);
        // A soft shadow where she meets the stone, as the other props have.
        shadow = Layer("Shadow", ProceduralSprites.Shadow, order - 2);
        shadow.color = new Color(0f, 0f, 0f, 0.45f);
        shadow.transform.localPosition = Vector3.up * 0.08f;
        // Her water-light, breathing, under her.
        glow = Layer("Glow", ProceduralSprites.SoftGlow, order - 1);
        glow.color = new Color(Colour.r, Colour.g, Colour.b, 0f);
        if (hasArt)
        {
            body = Layer("Body", art.idle[0], order);
            float scale = pixel * art.idle[0].pixelsPerUnit;
            body.transform.localScale = Vector3.one * scale;
            // Her lowest drawn pixel stands on her feet (the tight mesh skips the canvas's empty rows).
            float bottom = float.MaxValue;
            foreach (Vector2 v in art.idle[0].vertices) bottom = Mathf.Min(bottom, v.y);
            if (bottom == float.MaxValue) bottom = art.idle[0].bounds.min.y;
            body.transform.localPosition = -Vector3.up * (bottom * scale);
            Play(Clip.Idle);
        }
        else
        {
            // No art yet: a tall block of her colour with the bowl as a paler disc on top.
            body = Layer("Body", ProceduralSprites.White, order);
            Vector2 native = ProceduralSprites.White.bounds.size;
            body.transform.localScale = new Vector3(FallbackWidth / native.x, FallbackHeight / native.y, 1f);
            body.transform.localPosition = Vector3.up * (FallbackHeight / 2f);
            body.color = Color.Lerp(Colour, new Color(0.2f, 0.22f, 0.3f), 0.45f);
            SpriteRenderer bowl = Layer("Bowl", ProceduralSprites.Disc, order + 1);
            bowl.transform.localScale = Vector3.one * (FallbackWidth * 0.7f / ProceduralSprites.Disc.bounds.size.x);
            bowl.transform.localPosition = Vector3.up * (FallbackHeight - FallbackWidth * 0.3f);
            bowl.color = Color.Lerp(Colour, Color.white, 0.55f);
        }
        Bounds b = body.bounds;
        shadow.transform.localScale = new Vector3(b.size.x * 1.1f / ProceduralSprites.Shadow.bounds.size.x, b.size.x * 0.32f / ProceduralSprites.Shadow.bounds.size.y, 1f);
        glow.transform.localPosition = Vector3.up * (b.size.y * 0.25f);
        glow.transform.localScale = new Vector3(b.size.x * 2.2f / ProceduralSprites.SoftGlow.bounds.size.x, b.size.y * 0.7f / ProceduralSprites.SoftGlow.bounds.size.y, 1f);
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
        Clip.Lift => art.lift,
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

    // Drawn over or under Oni: the station's sorting, kept by SafeArea each frame.
    public void SetOrder(int order)
    {
        body.sortingOrder = order;
        glow.sortingOrder = order - 1;
        shadow.sortingOrder = order - 2;
        foreach (Transform child in transform)
            if (child.name == "Bowl") child.GetComponent<SpriteRenderer>().sortingOrder = order + 1;
    }

    void Update()
    {
        float time = Time.unscaledTime;
        glow.color = new Color(Colour.r, Colour.g, Colour.b, 0.09f + 0.04f * Mathf.Sin(time * 0.8f));
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
