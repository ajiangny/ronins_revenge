using TMPro;
using UnityEngine;

// What waits beyond a portal: the next round's reward, given when that round is cleared, or the stage's boss.
public enum PortalReward { Soul, Bead, Notes, Spring, Scroll, Boss }

// A spirit gate that opens where the army stood once a round is cleared, like Hades' chamber doors: each shows the
// reward of the round behind it, and the ronin picks one by standing under it. Built in code; without art it is a
// tall glow in the reward's colour around the reward's icon (a disc without art).
public class Portal : MonoBehaviour
{
    const float Width = 1.5f;
    const float Height = 2.4f;
    const float OpenTime = 0.5f;
    const float CloseTime = 0.35f;
    const float IconSize = 1.3f;

    public PortalReward Reward { get; private set; }
    public Element Element { get; private set; }

    SpriteRenderer glow;
    SpriteRenderer core;
    SpriteRenderer icon;
    TextMeshPro label;
    Sprite[] iconFrames;
    Color colour;
    float age;
    float closingAt = -1f;
    bool lit;

    public static Portal Spawn(Vector3 position, PortalReward reward, Element element, Color colour, string text,
        Sprite[] iconFrames, TMP_FontAsset font)
    {
        var go = new GameObject($"Portal_{reward}");
        go.transform.position = new Vector3(position.x, position.y, -0.4f);
        var portal = go.AddComponent<Portal>();
        portal.Reward = reward;
        portal.Element = element;
        portal.colour = colour;
        portal.iconFrames = iconFrames != null && iconFrames.Length > 0 ? iconFrames : null;

        portal.glow = Layer(go.transform, "Glow", ProceduralSprites.Glow, 30);
        portal.core = Layer(go.transform, "Core", ProceduralSprites.Glow, 31);
        portal.icon = Layer(go.transform, "Icon", portal.iconFrames != null ? portal.iconFrames[0] : ProceduralSprites.Disc, 32);
        if (portal.iconFrames != null)
        {
            Sprite first = portal.iconFrames[0];
            // Most of the portal's width, so the reward's art reads at a glance.
            portal.icon.transform.localScale = Vector3.one * (IconSize / Mathf.Max(first.bounds.size.x, first.bounds.size.y));
        }
        else
        {
            portal.icon.transform.localScale = Vector3.one * (0.5f / ProceduralSprites.Disc.bounds.size.x);
            portal.icon.color = Color.Lerp(colour, Color.white, 0.4f);
        }

        portal.label = new GameObject("Label").AddComponent<TextMeshPro>();
        portal.label.transform.SetParent(go.transform, false);
        portal.label.transform.localPosition = Vector3.down * (Height * 0.5f + 0.35f);
        if (font != null) portal.label.font = font;
        portal.label.fontSize = 4.5f;
        portal.label.alignment = TextAlignmentOptions.Center;
        portal.label.text = text;
        portal.label.color = Color.Lerp(colour, Color.white, 0.5f);
        portal.label.outlineWidth = 0.2f;
        portal.label.outlineColor = Color.black;
        portal.label.rectTransform.sizeDelta = new Vector2(5f, 1.2f);
        portal.label.sortingOrder = 33;

        portal.Animate();
        return portal;
    }

    static SpriteRenderer Layer(Transform parent, string name, Sprite sprite, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.sprite = sprite;
        layer.sortingOrder = order;
        return layer;
    }

    // The portal above the ronin brightens and swells a little, so the pick reads before he steps in.
    public void SetLit(bool on) => lit = on;

    public void Close()
    {
        if (closingAt < 0f) closingAt = age;
    }

    void Update()
    {
        age += Time.deltaTime;
        Animate();
        if (closingAt >= 0f && age - closingAt >= CloseTime) Destroy(gameObject);
    }

    void Animate()
    {
        float open = Mathf.SmoothStep(0f, 1f, age / OpenTime);
        if (closingAt >= 0f) open *= 1f - Mathf.Clamp01((age - closingAt) / CloseTime);
        float pulse = 1f + 0.06f * Mathf.Sin(age * 3f);
        float swell = lit ? 1.12f : 1f;
        float spriteSize = ProceduralSprites.Glow.bounds.size.x;

        glow.transform.localScale = new Vector3(Width * 1.6f, Height * 1.4f, 1f) * (open * pulse * swell / spriteSize);
        glow.color = new Color(colour.r, colour.g, colour.b, (lit ? 0.75f : 0.45f) * open);
        core.transform.localScale = new Vector3(Width, Height, 1f) * (open * swell / spriteSize);
        Color dark = Color.Lerp(colour, Color.black, 0.75f);
        core.color = new Color(dark.r, dark.g, dark.b, 0.85f * open);

        if (iconFrames != null) icon.sprite = iconFrames[(int)(age * 8f) % iconFrames.Length];
        icon.transform.localPosition = Vector3.up * (0.12f * Mathf.Sin(age * 2.5f));
        Color tint = icon.color;
        icon.color = new Color(tint.r, tint.g, tint.b, open);
        label.alpha = open * (lit ? 1f : 0.75f);
    }
}
