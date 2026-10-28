using System;
using UnityEngine;

// A sealed technique scroll, the game's Daedalus Hammer: rarely dropped (mostly by kite scouts), it settles on the
// ronin's row like a soul, and catching it offers the Sword Arts that reshape his slash. Built in code; without art a
// parchment bar on a gold glow stands in.
public class TechniqueScroll : Pickup
{
    static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
    static readonly Color Parchment = new Color(0.93f, 0.86f, 0.7f);
    const float HintHeight = 0.95f;
    const float HintBounce = 0.18f;
    const float HintScale = 1.5f;

    Action<TechniqueScroll> collected;
    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer hint;

    public static TechniqueScroll Spawn(Vector3 position, Sprite[] frames, Ronin ronin, Action<TechniqueScroll> onCollected)
    {
        var go = new GameObject("TechniqueScroll");
        go.transform.position = new Vector3(position.x, position.y, -0.5f);
        var scroll = go.AddComponent<TechniqueScroll>();
        scroll.Begin(ronin, position);
        scroll.collected = onCollected;

        scroll.glow = CreateLayer(go.transform, "Glow", ProceduralSprites.Glow, 1.8f, 40);
        bool hasArt = frames != null && frames.Length > 0 && frames[0] != null;
        scroll.body = CreateLayer(go.transform, "Scroll", hasArt ? frames[0] : ProceduralSprites.White, 1f, 41);
        if (hasArt)
        {
            Sprite art = frames[0];
            // Sized to about two thirds of a unit across, whatever the art's pixel density.
            scroll.body.transform.localScale = Vector3.one * (0.7f / Mathf.Max(art.bounds.size.x, art.bounds.size.y));
            if (frames.Length > 1) scroll.body.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
        }
        else
        {
            scroll.body.transform.localScale = new Vector3(0.6f, 0.22f, 1f) / ProceduralSprites.White.bounds.size.x;
            scroll.body.color = Parchment;
        }
        Fx.Play(Fx.Impact, go.transform.position, Gold, 1.4f);
        Sfx.Play(Sfx.SoulSpawn, 1f, 0.75f);
        return scroll;
    }

    // A bobbing arrow over the scroll the first time one drops in a run, so it reads as a pickup.
    public void ShowHint(Sprite arrow)
    {
        bool hasArt = arrow != null;
        hint = CreateLayer(transform, "Hint", hasArt ? arrow : ProceduralSprites.ArrowDown, hasArt ? HintScale : 0.65f, 43);
        if (!hasArt) hint.color = Gold;
    }

    protected override void Collect() => collected?.Invoke(this);

    protected override void Animate(float dt)
    {
        // A slow, stately gold pulse, apart from a soul's quick flicker.
        float pulse = 1f + 0.15f * Mathf.Sin(age * 4f);
        glow.transform.localScale = Vector3.one * (1.8f * pulse);
        glow.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
        body.transform.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(age * 2f));
        if (hint != null) hint.transform.localPosition = Vector3.up * (HintHeight + HintBounce * Mathf.Abs(Mathf.Sin(age * 5f)));
    }
}
