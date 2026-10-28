using System;
using System.Collections;
using TMPro;
using UnityEngine;

// A heart that sometimes falls out of a broken blocker. Like a soul it settles on the ronin's row; catching it heals
// a lost life. At full health he can't take it, so it waits there until he's been hurt, or until the round ends and
// it fades away into points.
public class Heart : Pickup
{
    static readonly Color Red = new Color(1f, 0.3f, 0.35f);

    Func<bool> canHeal;
    Action<Heart> collected;
    SpriteRenderer body;
    SpriteRenderer glow;

    // icon is the HUD's life heart; without it a red orb stands in.
    // Across, in world units: the heart, and the heartbeat glow behind it.
    const float Size = 0.85f;
    const float GlowSize = 1.9f;

    public static Heart Spawn(Vector3 position, Sprite icon, Ronin ronin, Func<bool> canHeal, Action<Heart> onCollected)
    {
        var go = new GameObject("Heart");
        go.transform.position = new Vector3(position.x, position.y, -0.5f);
        var heart = go.AddComponent<Heart>();
        heart.Begin(ronin, position);
        heart.canHeal = canHeal;
        heart.collected = onCollected;

        heart.glow = CreateLayer(go.transform, "Glow", ProceduralSprites.Glow, GlowSize, 40);
        bool hasArt = icon != null;
        heart.body = CreateLayer(go.transform, "Body", hasArt ? icon : ProceduralSprites.Glow, 0.5f, 41);
        // The HUD icon is a UI sprite of any pixel density, so size it in units, big enough to read as it falls.
        if (hasArt) heart.body.transform.localScale = Vector3.one * (Size / Mathf.Max(icon.bounds.size.x, icon.bounds.size.y));
        else heart.body.color = Color.Lerp(Red, Color.white, 0.3f);
        Fx.Play(Fx.Impact, go.transform.position, Red, 0.8f);
        return heart;
    }

    public const float DissipateTime = 0.8f;
    static readonly Color PointsColour = new Color(1f, 0.84f, 0.42f);

    // A spare heart at the round's end: it stops where it is and fades upward, leaving its worth in points.
    public void Dissipate(int points, TMP_FontAsset font)
    {
        StartCoroutine(Fade(points, font));
        // No longer a pickup: it can't be gathered or caught while it fades.
        enabled = false;
    }

    IEnumerator Fade(int points, TMP_FontAsset font)
    {
        Fx.Play(Fx.Impact, transform.position, Red, 0.9f);
        TextMeshPro text = new GameObject("Points").AddComponent<TextMeshPro>();
        text.transform.SetParent(transform, false);
        if (font != null) text.font = font;
        text.fontSize = 6f;
        text.alignment = TextAlignmentOptions.Center;
        text.text = $"+{points}";
        text.color = PointsColour;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.rectTransform.sizeDelta = new Vector2(3f, 1f);
        text.sortingOrder = 46;

        Vector3 from = transform.position;
        Color bodyTint = body.color;
        for (float t = 0f; t < DissipateTime; t += Time.deltaTime)
        {
            float k = t / DissipateTime;
            transform.position = from + Vector3.up * (0.8f * GameUI.EaseOut(k));
            body.transform.localScale *= 1f + Time.deltaTime * 0.6f;
            body.color = new Color(bodyTint.r, bodyTint.g, bodyTint.b, 1f - k);
            glow.color = new Color(Red.r, Red.g, Red.b, 0.45f * (1f - k));
            // The points hold, then fade in the last third.
            text.alpha = Mathf.Clamp01((1f - k) * 3f);
            yield return null;
        }
        Destroy(gameObject);
    }

    protected override bool CanCollect => canHeal == null || canHeal();

    protected override void Collect() => collected?.Invoke(this);

    protected override void Animate(float dt)
    {
        // A heartbeat: two quick swells, then a rest.
        float beat = Mathf.Repeat(age, 0.9f);
        float swell = beat < 0.12f || (beat > 0.22f && beat < 0.34f) ? 1.15f : 1f;
        glow.transform.localScale = Vector3.one * (GlowSize * swell);
        glow.color = new Color(Red.r, Red.g, Red.b, 0.45f * swell);
    }
}
