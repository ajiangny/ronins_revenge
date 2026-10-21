using System;
using UnityEngine;

// An elemental soul dropped by a fallen samurai. It drifts down, and the ronin collects it by moving under it.
// Missed souls fade away once they fall past him.
public class Soul : MonoBehaviour
{
    const float FallSpeed = 2f;
    const float SwayWidth = 0.25f;
    const float FadeTime = 0.5f;
    const float PickupRadius = 0.35f;

    public Element Element { get; private set; }

    Ronin ronin;
    Action<Soul> collected;
    SpriteRenderer body;
    SpriteRenderer aura;
    FxPlayer crackle;
    Color color;
    float originX;
    float age;
    float fadeAge = -1f;

    // frames may be empty: a glowing orb in the element's colour stands in until the soul art is wired up.
    public static Soul Spawn(Vector3 position, Element element, Sprite[] frames, Ronin ronin, Action<Soul> onCollected)
    {
        var go = new GameObject("Soul_" + element);
        go.transform.position = new Vector3(position.x, position.y, -0.5f);
        var soul = go.AddComponent<Soul>();
        soul.Element = element;
        soul.ronin = ronin;
        soul.collected = onCollected;
        soul.color = Boons.Info(element).Color;
        soul.originX = position.x;
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
        soul.crackle = Fx.Play(Fx.SoulCrackle(element), go.transform.position, Color.white, 1.1f, go.transform, 42);
        return soul;
    }

    static SpriteRenderer CreateLayer(Transform parent, string name, Sprite sprite, float scale, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.transform.localScale = Vector3.one * scale;
        layer.sprite = sprite;
        layer.sortingOrder = order;
        return layer;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        Vector3 position = transform.position;
        position.y -= FallSpeed * dt;
        position.x = originX + Mathf.Sin(age * 3f) * SwayWidth;
        transform.position = position;

        float pulse = 1f + 0.12f * Mathf.Sin(age * 8f);
        aura.transform.localScale = Vector3.one * (1.6f * pulse);

        float alpha = 1f;
        if (fadeAge >= 0f)
        {
            fadeAge += dt;
            alpha = 1f - fadeAge / FadeTime;
            if (alpha <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }
        aura.color = new Color(color.r, color.g, color.b, 0.55f * alpha);
        Color b = body.color;
        body.color = new Color(b.r, b.g, b.b, alpha);
        if (crackle != null) crackle.Color = new Color(1f, 1f, 1f, alpha);

        if (ronin == null || !ronin.isActiveAndEnabled) return;
        Bounds bounds = ronin.Bounds;
        if (fadeAge < 0f && ronin.ControlsEnabled && bounds.SqrDistance(new Vector3(position.x, position.y, bounds.center.z)) <= PickupRadius * PickupRadius)
        {
            collected?.Invoke(this);
            Destroy(gameObject);
        }
        else if (fadeAge < 0f && position.y < bounds.min.y)
        {
            fadeAge = 0f;
        }
    }
}
