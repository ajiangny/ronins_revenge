using TMPro;
using UnityEngine;

// What waits down a path: the next round's reward, given when that round is cleared, or the stage's boss.
public enum PathReward { Soul, Bead, Notes, Spring, Scroll, Boss }

// One of the paths that open where the army stood once a round is cleared, like Hades' chamber doors: each shows the
// reward of the round down it, floating over a soft glow. The one the ronin picks floats away up the field ahead of
// him, to come back as that round's reward when it's cleared. Built in code; without art the reward's icon is a disc
// in its colour.
public class RewardPath : MonoBehaviour
{
    const float GlowSize = 2.6f;
    const float OpenTime = 0.5f;
    const float CloseTime = 0.35f;
    const float IconSize = 1.3f;
    const float RiseStart = 0.5f;
    const float RiseAcceleration = 9f;
    // Gives up on the icon once it's this long gone, wherever it is.
    const float RiseTime = 3f;

    public PathReward Reward { get; private set; }
    public Element Element { get; private set; }
    public Sprite[] IconFrames => iconFrames;
    public Color Colour => colour;

    SpriteRenderer glow;
    SpriteRenderer halo;
    SpriteRenderer icon;
    TextMeshPro label;
    Sprite[] iconFrames;
    Color colour;
    float age;
    float closingAt = -1f;
    bool lit;
    bool released;
    float riseSpeed = RiseStart;

    public static RewardPath Spawn(Vector3 position, PathReward reward, Element element, Color colour, string text,
        Sprite[] iconFrames, TMP_FontAsset font)
    {
        var go = new GameObject($"Path_{reward}");
        go.transform.position = new Vector3(position.x, position.y, -0.4f);
        var path = go.AddComponent<RewardPath>();
        path.Reward = reward;
        path.Element = element;
        path.colour = colour;
        path.iconFrames = iconFrames != null && iconFrames.Length > 0 && iconFrames[0] != null ? iconFrames : null;

        path.glow = Layer(go.transform, "Glow", ProceduralSprites.Glow, 30);
        path.halo = Layer(go.transform, "Halo", ProceduralSprites.Glow, 31);
        path.icon = Layer(go.transform, "Icon", path.iconFrames != null ? path.iconFrames[0] : ProceduralSprites.Disc, 32);
        if (path.iconFrames != null)
        {
            Sprite first = path.iconFrames[0];
            path.icon.transform.localScale = Vector3.one * (IconSize / Mathf.Max(first.bounds.size.x, first.bounds.size.y));
        }
        else
        {
            path.icon.transform.localScale = Vector3.one * (0.5f / ProceduralSprites.Disc.bounds.size.x);
            path.icon.color = Color.Lerp(colour, Color.white, 0.4f);
        }

        path.label = new GameObject("Label").AddComponent<TextMeshPro>();
        path.label.transform.SetParent(go.transform, false);
        path.label.transform.localPosition = Vector3.down * (IconSize * 0.5f + 0.55f);
        if (font != null) path.label.font = font;
        path.label.fontSize = 4.5f;
        path.label.alignment = TextAlignmentOptions.Center;
        path.label.text = text;
        path.label.color = Color.Lerp(colour, Color.white, 0.5f);
        path.label.outlineWidth = 0.2f;
        path.label.outlineColor = Color.black;
        path.label.rectTransform.sizeDelta = new Vector2(5f, 1.2f);
        path.label.sortingOrder = 33;

        path.Animate();
        return path;
    }

    static SpriteRenderer Layer(Transform parent, string name, Sprite sprite, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.sprite = sprite;
        layer.sortingOrder = order;
        return layer;
    }

    // The path he's lined up with brightens and swells a little, so the pick reads before he takes it.
    public void SetLit(bool on) => lit = on;

    // Chosen: the glow it floated over closes and its reward rises away up the field, for the ronin to follow.
    public void Release()
    {
        released = true;
        Fx.Play(Fx.Impact, icon.transform.position, colour, 1.6f);
        Close();
    }

    public void Close()
    {
        if (closingAt < 0f) closingAt = age;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (released) Rise();
        Animate();
        if (closingAt < 0f) return;
        bool iconGone = !released || age - closingAt >= RiseTime || transform.position.y > ViewTop + GlowSize;
        if (age - closingAt >= CloseTime && iconGone) Destroy(gameObject);
    }

    static float ViewTop => CameraShake.Center.y + Camera.main.orthographicSize;

    // Faster as it goes, its light with it, until it's off the top of the view.
    void Rise()
    {
        riseSpeed += RiseAcceleration * Time.deltaTime;
        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
    }

    void Animate()
    {
        float open = Mathf.SmoothStep(0f, 1f, age / OpenTime);
        if (closingAt >= 0f) open *= 1f - Mathf.Clamp01((age - closingAt) / CloseTime);
        // A released one keeps a smaller light around it as it rises; only its name fades.
        float light = released ? 0.75f : open;
        float pulse = 1f + 0.06f * Mathf.Sin(age * 3f);
        float swell = lit || released ? 1.15f : 1f;
        float spriteSize = ProceduralSprites.Glow.bounds.size.x;

        glow.transform.localScale = Vector3.one * (GlowSize * light * pulse * swell / spriteSize);
        glow.color = new Color(colour.r, colour.g, colour.b, (lit ? 0.7f : 0.4f) * light);
        // A tighter, brighter heart to the glow, so the icon sits on light rather than on the field.
        halo.transform.localScale = Vector3.one * (GlowSize * 0.5f * light * swell / spriteSize);
        Color bright = Color.Lerp(colour, Color.white, 0.5f);
        halo.color = new Color(bright.r, bright.g, bright.b, (lit ? 0.55f : 0.3f) * light);

        if (iconFrames != null) icon.sprite = iconFrames[(int)(age * 8f) % iconFrames.Length];
        label.alpha = open * (lit ? 1f : 0.75f);
        if (released) return;
        icon.transform.localPosition = Vector3.up * (0.12f * Mathf.Sin(age * 2.5f));
        Color tint = icon.color;
        icon.color = new Color(tint.r, tint.g, tint.b, open);
    }
}
