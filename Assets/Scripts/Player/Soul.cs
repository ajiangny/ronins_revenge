using System;
using UnityEngine;

// An elemental soul dropped by a fallen samurai. It drifts down to the ronin's row and waits there until he
// collects it by moving into it.
public class Soul : Pickup
{
    const float HintHeight = 0.95f;
    const float HintBounce = 0.18f;
    // The 32px arrow art is drawn half again as big so it reads at a glance.
    const float HintScale = 1.5f;

    public Element Element { get; private set; }

    Action<Soul> collected;
    SpriteRenderer body;
    SpriteRenderer aura;
    SpriteRenderer hint;
    Color color;

    // frames may be empty: a glowing orb in the element's colour stands in until the soul art is wired up.
    public static Soul Spawn(Vector3 position, Element element, Sprite[] frames, Ronin ronin, Action<Soul> onCollected)
    {
        var go = new GameObject("Soul_" + element);
        go.transform.position = new Vector3(position.x, position.y, -0.5f);
        var soul = go.AddComponent<Soul>();
        soul.Begin(ronin, position);
        soul.Element = element;
        soul.collected = onCollected;
        soul.color = Boons.Info(element).Color;
        Sfx.Play(Sfx.SoulSpawn);

        soul.aura = CreateLayer(go.transform, "Aura", ProceduralSprites.Glow, 1.6f, 40);
        bool hasArt = frames != null && frames.Length > 0;
        soul.body = CreateLayer(go.transform, "Wisp", hasArt ? frames[0] : ProceduralSprites.Glow, hasArt ? 1f : 0.6f, 41);
        if (hasArt)
        {
            soul.body.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
        }
        else
        {
            soul.body.color = Color.Lerp(soul.color, Color.white, 0.6f);
        }
        // Element-coloured sparks crackling around the wisp.
        Fx.Play(Fx.SoulCrackle(element), go.transform.position, Color.white, 1.1f, go.transform, 42);
        return soul;
    }

    // A bobbing arrow over the soul, so a new player knows to catch it. Without art a plain gold arrow stands in.
    public void ShowHint(Sprite arrow)
    {
        bool hasArt = arrow != null;
        hint = CreateLayer(transform, "Hint", hasArt ? arrow : ProceduralSprites.ArrowDown, hasArt ? HintScale : 0.65f, 43);
        if (!hasArt) hint.color = new Color(1f, 0.82f, 0.3f);
    }

    protected override void Collect() => collected?.Invoke(this);

    protected override void Animate(float dt)
    {
        float pulse = 1f + 0.12f * Mathf.Sin(age * 8f);
        aura.transform.localScale = Vector3.one * (1.6f * pulse);
        aura.color = new Color(color.r, color.g, color.b, 0.55f);
        if (hint != null)
        {
            // Bounces down toward the soul, like a pickup marker.
            hint.transform.localPosition = Vector3.up * (HintHeight + HintBounce * Mathf.Abs(Mathf.Sin(age * 5f)));
        }
    }
}
