using System.Collections;
using UnityEngine;

// Shirogane's soul in the ending, drawn at Oni's size and in his style: chained to her bell where the Emperor fell,
// kneeling, until Oni cuts the chain, as she once cut the tether dragging his soul into the armor. Then she rises,
// warms from soul-fire teal to her own silver, bows, and goes into the wisp that stays at his shoulder. Code-built;
// without her ending frames she falls back to her boss idle, tinted and scaled down.
public class ShiroganeFreed : MonoBehaviour
{
    static readonly Color Teal = new Color(0.3f, 1f, 0.82f);
    static readonly Color Bronze = new Color(0.88f, 0.62f, 0.32f);
    static readonly Color SoulTint = new Color(0.55f, 1f, 0.9f, 0.8f);
    static readonly Color Silver = new Color(0.86f, 0.95f, 1f);

    SpriteRenderer body;
    SpriteAnimator animator;
    SpriteRenderer aura;
    SpriteRenderer chain;
    SpriteRenderer bell;
    Sprite[] kneel;
    Sprite[] bow;
    Sprite[] idle;
    float fps = 8f;

    public Vector3 Centre => body.bounds.center;
    // Where the chain meets her: her chest.
    Vector3 Heart => body.bounds.center + Vector3.up * body.bounds.extents.y * 0.15f;

    // Kneels her at `feet`, each art pixel `pixelSize` world units (Oni's), chained to her bell at `bellAt`.
    // `kneel` is her sinking to her knees: its last frame is her kneel, and played backwards it is her rising.
    public static ShiroganeFreed Create(Vector3 feet, Vector3 bellAt, float pixelSize, int order,
        Sprite[] kneel, Sprite[] bow, Sprite[] idle, CharacterArt fallback)
    {
        var freed = new GameObject("ShiroganeFreed").AddComponent<ShiroganeFreed>();
        bool art = kneel != null && kneel.Length > 0;
        freed.kneel = art ? kneel : fallback != null ? fallback.death : null;
        freed.idle = idle != null && idle.Length > 0 ? idle : fallback != null ? fallback.idle : null;
        freed.bow = bow != null && bow.Length > 0 ? bow : null;

        freed.body = new GameObject("Body").AddComponent<SpriteRenderer>();
        freed.body.transform.SetParent(freed.transform, false);
        freed.body.sortingOrder = order;
        freed.animator = freed.body.gameObject.AddComponent<SpriteAnimator>();
        Sprite first = freed.kneel != null ? freed.kneel[freed.kneel.Length - 1] : ProceduralSprites.Glow;
        freed.body.sprite = first;
        // Her boss frames are drawn bigger than Oni's; scaled so she still stands his height.
        float scale = art ? pixelSize * first.pixelsPerUnit : 1.4f / Mathf.Max(0.01f, first.bounds.size.y);
        freed.transform.localScale = Vector3.one * scale;
        // Her ending frames stand on their bottom edge; anything else is placed by its bounds.
        freed.transform.position = feet;
        freed.transform.position += feet - new Vector3(freed.body.bounds.center.x, freed.body.bounds.min.y, feet.z);
        freed.body.color = new Color(SoulTint.r, SoulTint.g, SoulTint.b, 0f);

        freed.aura = new GameObject("Aura").AddComponent<SpriteRenderer>();
        freed.aura.sprite = ProceduralSprites.SoftGlow;
        freed.aura.sortingOrder = order - 2;
        freed.aura.transform.position = freed.Centre + Vector3.forward * 0.1f;
        freed.aura.transform.localScale = Vector3.one * freed.body.bounds.size.y * 1.6f;
        freed.aura.transform.SetParent(freed.transform, true);
        freed.aura.color = new Color(Teal.r, Teal.g, Teal.b, 0f);

        Sprite[] bellFrames = Fx.Frames(Fx.KageBell);
        freed.bell = new GameObject("HerBell").AddComponent<SpriteRenderer>();
        freed.bell.sprite = bellFrames != null ? bellFrames[0] : ProceduralSprites.Disc;
        freed.bell.sortingOrder = order - 1;
        freed.bell.transform.position = bellAt;
        freed.bell.transform.localScale = Vector3.one * (1.4f / Mathf.Max(0.01f, freed.bell.sprite.bounds.size.y));
        freed.bell.color = bellFrames != null ? new Color(1f, 1f, 1f, 0f) : new Color(Bronze.r, Bronze.g, Bronze.b, 0f);

        Sprite[] chainFrames = Fx.Frames(Fx.KageChain);
        freed.chain = new GameObject("Chain").AddComponent<SpriteRenderer>();
        freed.chain.sprite = chainFrames != null ? chainFrames[0] : ProceduralSprites.White;
        if (chainFrames != null) freed.chain.drawMode = SpriteDrawMode.Tiled;
        freed.chain.sortingOrder = order - 1;
        freed.LayChain(0f);
        return freed;
    }

    // Her soul fades up out of the fallen armor's last light, the chain drawn taut to her bell.
    public IEnumerator Appear(float seconds)
    {
        Sfx.Play(Sfx.MistSwirl, 0.7f, 0.8f);
        Fx.Play(Fx.SmokePuff, Centre, Teal, 1.6f);
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            body.color = new Color(SoulTint.r, SoulTint.g, SoulTint.b, SoulTint.a * k);
            aura.color = new Color(Teal.r, Teal.g, Teal.b, 0.35f * k);
            bell.color = new Color(bell.color.r, bell.color.g, bell.color.b, k);
            LayChain(0.85f * k);
            yield return null;
        }
        // The links tremble while she pulls against them.
        StartCoroutine(Strain());
    }

    IEnumerator Strain()
    {
        while (chain != null)
        {
            LayChain(chain.color.a, Mathf.Sin(Time.time * 23f) * 0.03f);
            yield return null;
        }
    }

    // Where a blade crossing the chain bites: a third of the way from her to the bell.
    public Vector3 CutPoint => Vector3.Lerp(Heart, bell.transform.position, 0.35f);

    // Oni's cut lands: the chain bursts into links, and her bell rings for the first time, then falls silent.
    public void Snap()
    {
        Vector3 at = CutPoint;
        Sfx.Play(Sfx.ChainSnap);
        CameraShake.Add(0.35f);
        Fx.Play(Fx.Impact, at, Color.white, 1.3f);
        ElementFx.Glow(at, Teal, 0.6f, 3f, 0.6f);
        if (chain != null)
        {
            Debris.BurstSprites(at, new[] { chain.sprite }, chain.drawMode == SpriteDrawMode.Tiled ? Color.white : Bronze, 10, 0.25f, 5f);
            Fader.Fade(chain, 0.35f);
            chain = null;
        }
        StartCoroutine(RingAndSilence());
    }

    IEnumerator RingAndSilence()
    {
        if (Sfx.Has(Sfx.BellToll)) Sfx.Play(Sfx.BellToll, 0.7f, 1.5f);
        else Sfx.Play(Sfx.SoulCollect, 0.8f, 0.7f);
        ElementFx.Glow(bell.transform.position, Teal, 0.5f, 2.5f, 1.2f);
        Transform swing = bell.transform;
        for (float t = 0; t < 1.6f; t += Time.deltaTime)
        {
            swing.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 14f) * 22f * (1f - t / 1.6f));
            yield return null;
        }
        swing.localRotation = Quaternion.identity;
        if (Sfx.Has(Sfx.BellSilenced)) Sfx.Play(Sfx.BellSilenced, 0.6f);
        Fader.Fade(bell, 1.2f);
        Kagetsune.RisingSoul(bell.transform.position);
    }

    // She gets up off her knees as her colour comes back, from soul-fire teal to her own white silver.
    public IEnumerator Rise(float seconds)
    {
        if (kneel != null)
        {
            var back = new Sprite[kneel.Length];
            for (int i = 0; i < kneel.Length; i++) back[i] = kneel[kneel.Length - 1 - i];
            animator.Play(back, false, () => Idle(), back.Length / Mathf.Max(0.1f, seconds));
        }
        Color from = body.color;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            body.color = Color.Lerp(from, new Color(1f, 1f, 1f, 1f), k);
            aura.color = Color.Lerp(new Color(Teal.r, Teal.g, Teal.b, 0.35f), new Color(Silver.r, Silver.g, Silver.b, 0.25f), k);
            yield return null;
        }
    }

    void Idle()
    {
        if (idle != null) animator.Play(idle, true, null, 5f);
    }

    // A short bow before she goes.
    public IEnumerator Bow()
    {
        if (bow == null) yield break;
        bool done = false;
        animator.Play(bow, false, () => done = true, fps);
        while (!done) yield return null;
        Idle();
    }

    // She thins into light and drifts up a little; the wisp at his shoulder takes her place.
    public IEnumerator Dissolve(float seconds)
    {
        Sfx.Play(Sfx.SoulCollect, 0.7f, 1.2f);
        Fx.Play(Fx.SmokePuff, Centre, Silver, 1.4f);
        Vector3 start = transform.position;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            body.color = new Color(Mathf.Lerp(1f, Silver.r, k), 1f, 1f, 1f - k);
            aura.color = new Color(Silver.r, Silver.g, Silver.b, 0.25f + 0.35f * Mathf.Sin(k * Mathf.PI));
            transform.position = start + Vector3.up * 0.25f * k;
            yield return null;
        }
        Destroy(gameObject);
    }

    // The bell and chain stand apart from her scaled body, so they go with her by hand.
    void OnDestroy()
    {
        if (chain != null) Destroy(chain.gameObject);
        if (bell != null) Destroy(bell.gameObject);
    }

    void LayChain(float alpha, float sway = 0f)
    {
        if (chain == null) return;
        Vector3 bellTop = bell.transform.position + Vector3.up * bell.bounds.extents.y * 0.6f;
        Kagetsune.Lay(chain, Heart + Vector3.right * sway, bellTop, 0.34f);
        chain.color = chain.drawMode == SpriteDrawMode.Tiled ? new Color(1f, 1f, 1f, alpha) : new Color(Bronze.r, Bronze.g, Bronze.b, alpha);
    }
}
