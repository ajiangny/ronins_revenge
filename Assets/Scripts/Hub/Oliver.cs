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
// of memory. He stands by the weapon rack once Oni has met him; the rack is his station, so he has none of his own. Code-
// built by SafeArea at Oni's pixel size, which makes him about 1.8 times Oni's height, and animated on unscaled time so he
// keeps moving while a talk scene holds the temple still.
public class Oliver : MonoBehaviour
{
    public enum Clip { Idle, Talk, Loaf, Strike }

    // Without art: the placeholder's size in world units, about 1.8 times Oni's height.
    const float FallbackWidth = 1.6f, FallbackHeight = 1.9f;
    static readonly Color Iron = new Color(0.435f, 0.416f, 0.388f);

    OliverArt art;
    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer shadow;
    bool hasArt;
    Sprite[] clip;
    bool clipLoop;
    float clipAt;

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
        foreach (Transform child in transform)
            if (child.name == "Seam") child.GetComponent<SpriteRenderer>().sortingOrder = order + 1;
    }

    void Update()
    {
        float time = Time.unscaledTime;
        Color heat = Boons.Info(Element.Steel).Color;
        glow.color = new Color(heat.r, heat.g, heat.b, 0.07f + 0.03f * Mathf.Sin(time * 0.6f));
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
