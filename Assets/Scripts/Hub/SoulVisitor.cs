using System;
using System.Collections;
using UnityEngine;

// A visiting soul's pixel clips (Art/Sprites/Characters/<Soul>/<Soul>_<Clip>_<i>.png, wired by Ninja/Apply Maps with the
// rest of the temple). Empty clips draw the visitor as a column of their element's glow.
[Serializable]
public class SoulVisitorArt
{
    public Element element;
    [Tooltip("How large the frames are drawn against Oni's pixels. These keep their paintings' detail, so they're drawn smaller, in finer pixels than his, at the height wanted beside him.")]
    public float artScale = 1f;
    [Tooltip("Standing in their painting's pose.")]
    public Sprite[] idle;
    [Tooltip("Their idle with the mouth moving.")]
    public Sprite[] talk;
    [Tooltip("Breaking up from the feet into rising motes, ending empty; played backward it's their arrival.")]
    public Sprite[] leave;
    public float fps = 8f;
    public float leaveFps = 12f;
}

// Akagane, Mizore or Hibiki at the temple's gate after a run (GameManager.SoulVisit): their soul sinks in and takes
// shape, says its line, and goes back into a soul that rises over the wall. Asagi and Oliver live in the temple, so
// they speak from where they stand instead. Code-built by SafeArea, and never solid to Oni.
public class SoulVisitor : MonoBehaviour
{
    // Without art: the column's size in world units, about Oni's height.
    const float FallbackWidth = 1f, FallbackHeight = 2.1f;
    const float WispSize = 1.1f;
    // The soul comes down from, and goes back up to, this far over their head.
    const float WispRise = 3.2f;

    SoulVisitorArt art;
    Sprite[] soulFrames;
    Color colour;
    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer shadow;
    Bounds drawn;
    bool hasArt;
    Sprite[] clip;
    float clipAt;
    int order;
    float glowLevel;

    // Over their head at full height, whichever frame they're on: Leave's frames shrink as they go.
    public Vector3 Head => new Vector3(drawn.center.x, drawn.max.y + 0.2f, 0f);
    Vector3 Chest => new Vector3(drawn.center.x, Mathf.Lerp(drawn.min.y, drawn.max.y, 0.55f), 0f);

    static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;

    // Their feet at `feet`, each of Oni's pixels `pixel` world units; nothing shows until Arrive.
    public static SoulVisitor Create(SoulVisitorArt art, Transform parent, Vector3 feet, float pixel, int order, Sprite[] soulFrames)
    {
        var visitor = new GameObject("SoulVisitor_" + art.element).AddComponent<SoulVisitor>();
        visitor.transform.SetParent(parent, false);
        visitor.transform.position = feet;
        visitor.art = art;
        visitor.soulFrames = soulFrames;
        visitor.colour = Boons.Info(art.element).Color;
        visitor.Build(pixel, order);
        return visitor;
    }

    void Build(float pixel, int sortOrder)
    {
        hasArt = Has(art.idle);
        order = sortOrder;
        shadow = Layer("Shadow", ProceduralSprites.Shadow);
        glow = Layer("Glow", ProceduralSprites.SoftGlow);
        if (hasArt)
        {
            body = Layer("Body", art.idle[0]);
            float scale = pixel * art.idle[0].pixelsPerUnit * art.artScale;
            body.transform.localScale = Vector3.one * scale;
            // Their lowest drawn pixel stands on their feet (the tight mesh skips the canvas's empty rows).
            Vector2 low = Vector2.positiveInfinity, high = Vector2.negativeInfinity;
            foreach (Vector2 v in art.idle[0].vertices)
            {
                low = Vector2.Min(low, v);
                high = Vector2.Max(high, v);
            }
            if (low.y == float.PositiveInfinity)
            {
                low = art.idle[0].bounds.min;
                high = art.idle[0].bounds.max;
            }
            body.transform.localPosition = -Vector3.up * (low.y * scale);
            // Their drawn extent: a renderer's bounds are the whole canvas, empty rows and all.
            Vector3 world = body.transform.position;
            drawn = new Bounds();
            drawn.SetMinMax(world + (Vector3)(low * scale), world + (Vector3)(high * scale));
        }
        else
        {
            body = Layer("Body", ProceduralSprites.SoftGlow);
            Vector2 native = ProceduralSprites.SoftGlow.bounds.size;
            body.transform.localScale = new Vector3(FallbackWidth / native.x, FallbackHeight / native.y, 1f);
            body.transform.localPosition = Vector3.up * (FallbackHeight / 2f);
            drawn = body.bounds;
        }
        shadow.transform.localScale = new Vector3(drawn.size.x * 0.8f / ProceduralSprites.Shadow.bounds.size.x, drawn.size.x * 0.22f / ProceduralSprites.Shadow.bounds.size.y, 1f);
        shadow.transform.localPosition = Vector3.up * 0.08f;
        glow.transform.localScale = new Vector3(drawn.size.x * 1.8f / ProceduralSprites.SoftGlow.bounds.size.x, drawn.size.y * 1.2f / ProceduralSprites.SoftGlow.bounds.size.y, 1f);
        glow.transform.position = new Vector3(drawn.center.x, drawn.center.y, transform.position.z);
        SetOrder(sortOrder);
        Reveal(0f);
        // Out of sight until they start to take shape.
        if (hasArt) body.enabled = false;
    }

    SpriteRenderer Layer(string name, Sprite sprite)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = sprite;
        return r;
    }

    // How much of them is there. Their clips do the showing when they have art; this carries the shadow and glow, and
    // the whole figure without it.
    void Reveal(float k)
    {
        glowLevel = k;
        shadow.color = new Color(0f, 0f, 0f, 0.4f * k);
        if (!hasArt) body.color = new Color(colour.r, colour.g, colour.b, 0.75f * k);
    }

    public void SetOrder(int sortOrder)
    {
        order = sortOrder;
        body.sortingOrder = sortOrder;
        glow.sortingOrder = sortOrder - 1;
        shadow.sortingOrder = sortOrder - 2;
    }

    public void Talk(bool talking)
    {
        if (!hasArt) return;
        clip = talking && Has(art.talk) ? art.talk : art.idle;
        clipAt = Time.unscaledTime;
    }

    // Their soul sinks in from over the wall, and they take shape around it, from the head down.
    public IEnumerator Arrive(float seconds)
    {
        Sfx.Play(Sfx.MistSwirl, 0.6f, 0.9f);
        float sink = seconds * 0.45f;
        SpriteRenderer wisp = Wisp(out FxPlayer crackle, out Vector3 start);
        Vector3 end = Chest;
        for (float t = 0f; t < sink; t += Time.deltaTime)
        {
            if (this == null) yield break;
            float k = Mathf.SmoothStep(0f, 1f, t / sink);
            wisp.transform.position = Vector3.Lerp(start, end, k) + Vector3.right * Mathf.Sin(t * 5f) * 0.12f * (1f - k);
            wisp.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / (sink * 0.4f)));
            yield return null;
        }
        if (this == null) yield break;
        ElementFx.Glow(Chest, colour, 0.5f, 3.2f, 0.7f);
        Sprite[] leave = hasArt && Has(art.leave) ? art.leave : null;
        float form = seconds - sink;
        if (crackle != null) crackle.Stop();
        if (leave == null && hasArt) body.color = new Color(1f, 1f, 1f, 0f);
        body.enabled = true;
        for (float t = 0f; t < form; t += Time.deltaTime)
        {
            if (this == null) yield break;
            float k = t / form;
            if (leave != null) body.sprite = leave[Mathf.Clamp(leave.Length - 1 - (int)(k * leave.Length), 0, leave.Length - 1)];
            else if (hasArt) body.color = new Color(1f, 1f, 1f, k);
            Reveal(k);
            wisp.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        if (this == null) yield break;
        Destroy(wisp.gameObject);
        Reveal(1f);
        if (hasArt) body.color = Color.white;
        Talk(false);
    }

    // They come apart from the feet up in their element's colour and go back into their soul, which rises over the
    // wall and thins out; then they're gone. `speed` above 1 cuts it short (Oni setting out mid-line).
    public IEnumerator Depart(float speed = 1f)
    {
        clip = null;
        Sfx.Play(Sfx.SoulCollect, 0.7f, 1.2f);
        ElementFx.Glow(Chest, colour, 0.6f, 4f, 0.9f / speed);
        Sprite[] leave = hasArt && Has(art.leave) ? art.leave : null;
        float come = (leave != null ? leave.Length / Mathf.Max(1f, art.leaveFps) : 0.8f) / speed;
        float rise = 1.7f / speed;
        SpriteRenderer wisp = Wisp(out FxPlayer crackle, out _);
        Vector3 from = Chest;
        Vector3 to = from + Vector3.up * (drawn.size.y * 0.5f + WispRise);
        for (float t = 0f; t < come + rise; t += Time.deltaTime)
        {
            if (this == null) yield break;
            float k = Mathf.Clamp01(t / come);
            if (leave != null) body.sprite = leave[Mathf.Min((int)(k * leave.Length), leave.Length - 1)];
            else if (hasArt) body.color = new Color(1f, 1f, 1f, 1f - k);
            Reveal(1f - k);
            // It gathers at their chest as they come apart, then goes.
            float up = Mathf.Clamp01((t - come * 0.6f) / rise);
            wisp.transform.position = Vector3.Lerp(from, to, up * up * (3f - 2f * up)) + Vector3.right * Mathf.Sin(t * 4f) * 0.18f * up;
            wisp.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / (come * 0.5f)) * (1f - Mathf.Clamp01((up - 0.6f) / 0.4f)));
            yield return null;
        }
        if (this == null) yield break;
        if (crackle != null) crackle.Stop();
        Destroy(gameObject);
    }

    // Their soul as a pickup looks in a run, its sparks with it; a soft glow without the frames.
    SpriteRenderer Wisp(out FxPlayer crackle, out Vector3 start)
    {
        start = Chest + Vector3.up * (drawn.size.y * 0.5f + WispRise);
        var wisp = new GameObject("Wisp").AddComponent<SpriteRenderer>();
        wisp.transform.SetParent(transform, true);
        wisp.transform.position = Chest;
        bool frames = Has(soulFrames);
        wisp.sprite = frames ? soulFrames[0] : ProceduralSprites.Glow;
        wisp.transform.localScale = Vector3.one * (frames ? WispSize : 0.6f);
        wisp.sortingOrder = order + 1;
        wisp.color = new Color(1f, 1f, 1f, 0f);
        if (frames) wisp.gameObject.AddComponent<SpriteAnimator>().Play(soulFrames, true, null, 8f);
        crackle = Fx.Play(Fx.SoulCrackle(art.element), wisp.transform.position, Color.white, WispSize, wisp.transform, order + 2);
        return wisp;
    }

    void Update()
    {
        float time = Time.unscaledTime;
        glow.color = new Color(colour.r, colour.g, colour.b, (0.1f + 0.04f * Mathf.Sin(time * 1.1f)) * glowLevel);
        if (clip == null) return;
        body.sprite = clip[(int)((time - clipAt) * Mathf.Max(1f, art.fps)) % clip.Length];
    }
}
