using System.Collections.Generic;
using UnityEngine;

// A marker any boss can lay on the ronin's row to show where he's safe from a coming attack: a cool cyan ward of
// light on the ground (PixelLab art: a gate post at each end and a rune band between, tiled to any width so its
// pixels never stretch), a soft column of light rising out of it, and a shield icon with a chevron bobbing above,
// pointing down into it. Calm cyan and white, nothing like the red and orange of the attacks. Without the art it's
// drawn from plain bars. Place it every frame it moves; Dismiss fades it out.
public class SafeZone : MonoBehaviour
{
    public static readonly Color Colour = new Color(0.7f, 0.97f, 1f);
    // The ward art is drawn at this many times its pixel size, and the icon floats this high over the ground.
    const float ArtScale = 2f;
    const float IconHeight = 3f;

    SpriteRenderer leftCap, band, rightCap, icon, pillar;
    // Without the art: a floor line, two posts and a two-stroke chevron.
    SpriteRenderer floor, leftPost, rightPost, chevronL, chevronR;
    readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
    float lo, hi, groundY, age, strength = 1f;
    bool placed, dismissed;

    // How bright it burns, 0 to 1 (a boss raises it while the attack is live).
    public float Strength { set => strength = Mathf.Clamp01(value); }

    public static SafeZone Create()
    {
        var zone = new GameObject("SafeZone").AddComponent<SafeZone>();
        zone.Build();
        return zone;
    }

    void Build()
    {
        pillar = Part("Pillar", ProceduralSprites.Glow, 5);
        Sprite[] ward = Fx.Frames(Fx.SafeZone);
        Sprite[] iconArt = Fx.Frames(Fx.SafeZoneIcon);
        if (ward != null && ward.Length >= 3)
        {
            leftCap = Part("LeftCap", ward[0], 7);
            band = Part("Band", ward[1], 6);
            band.drawMode = SpriteDrawMode.Tiled;
            rightCap = Part("RightCap", ward[2], 7);
        }
        else
        {
            floor = Part("Floor", ProceduralSprites.White, 7);
            leftPost = Part("LeftPost", ProceduralSprites.White, 7);
            rightPost = Part("RightPost", ProceduralSprites.White, 7);
        }
        if (iconArt != null) icon = Part("Icon", iconArt[0], 58);
        else
        {
            chevronL = Part("ChevronL", ProceduralSprites.White, 58);
            chevronR = Part("ChevronR", ProceduralSprites.White, 58);
        }
    }

    SpriteRenderer Part(string name, Sprite art, int order)
    {
        var part = new GameObject(name).AddComponent<SpriteRenderer>();
        part.transform.SetParent(transform, false);
        part.sprite = art;
        part.sortingOrder = order;
        part.color = Color.clear;
        parts.Add(part);
        return part;
    }

    // The safe stretch of the row, from lo to hi, standing on the ground at groundY.
    public void Place(float lo, float hi, float groundY)
    {
        this.lo = lo;
        this.hi = hi;
        this.groundY = groundY;
        placed = true;
    }

    public void Dismiss(float seconds)
    {
        if (dismissed) return;
        dismissed = true;
        foreach (SpriteRenderer part in parts) Fader.Fade(part, seconds);
        Destroy(gameObject, seconds + 0.05f);
    }

    void Update()
    {
        if (dismissed || !placed) return;
        age += Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * 6f);
        float alpha = strength * Mathf.Clamp01(age / 0.3f);
        float mid = (lo + hi) / 2f, width = Mathf.Max(hi - lo, 0.1f);

        pillar.transform.SetPositionAndRotation(new Vector3(mid, groundY + 1.3f, 0), Quaternion.identity);
        pillar.transform.localScale = new Vector3(width * 1.3f / pillar.sprite.bounds.size.x, 3.2f / pillar.sprite.bounds.size.y, 1f);
        pillar.color = Tint((0.1f + 0.08f * pulse) * alpha);

        if (band != null) LayWard(mid, width, alpha * (0.8f + 0.2f * pulse));
        else
        {
            Lay(floor, new Vector3(lo, groundY, 0), new Vector3(hi, groundY, 0), 0.07f, Tint(0.9f * alpha));
            Lay(leftPost, new Vector3(lo, groundY, 0), new Vector3(lo, groundY + 1.2f, 0), 0.1f, Tint((0.45f + 0.3f * pulse) * alpha));
            Lay(rightPost, new Vector3(hi, groundY, 0), new Vector3(hi, groundY + 1.2f, 0), 0.1f, Tint((0.45f + 0.3f * pulse) * alpha));
        }

        // The icon bobs over the middle of the zone, above the ronin's head.
        Vector3 over = new Vector3(mid, groundY + IconHeight + 0.15f * Mathf.Sin(age * 5f), 0);
        if (icon != null)
        {
            icon.transform.SetPositionAndRotation(over, Quaternion.identity);
            icon.transform.localScale = Vector3.one * ArtScale;
            icon.color = new Color(1f, 1f, 1f, (0.75f + 0.25f * pulse) * alpha);
        }
        else
        {
            Color white = new Color(1f, 1f, 1f, (0.6f + 0.4f * pulse) * alpha);
            Lay(chevronL, over + new Vector3(-0.5f, 0.5f, 0), over, 0.16f, white);
            Lay(chevronR, over + new Vector3(0.5f, 0.5f, 0), over, 0.16f, white);
        }
    }

    // The gate posts stand at the ends with the rune band tiled between, never narrower than the two posts.
    void LayWard(float mid, float width, float alpha)
    {
        float cap = leftCap.sprite.bounds.size.x * ArtScale;
        width = Mathf.Max(width, cap * 2f);
        float left = mid - width / 2f, right = mid + width / 2f;
        Color tint = new Color(1f, 1f, 1f, alpha);
        leftCap.transform.SetPositionAndRotation(new Vector3(left + cap / 2f, groundY, 0), Quaternion.identity);
        rightCap.transform.SetPositionAndRotation(new Vector3(right - cap / 2f, groundY, 0), Quaternion.identity);
        leftCap.transform.localScale = rightCap.transform.localScale = band.transform.localScale = Vector3.one * ArtScale;
        band.transform.SetPositionAndRotation(new Vector3(mid, groundY, 0), Quaternion.identity);
        band.size = new Vector2(Mathf.Max(0.01f, (width - cap * 2f) / ArtScale + 0.05f), band.sprite.bounds.size.y);
        leftCap.color = band.color = rightCap.color = tint;
    }

    Color Tint(float alpha) => new Color(Colour.r, Colour.g, Colour.b, alpha);

    static void Lay(SpriteRenderer bar, Vector3 from, Vector3 to, float thickness, Color colour)
    {
        Vector3 delta = to - from;
        bar.transform.SetPositionAndRotation((from + to) / 2f, Quaternion.FromToRotation(Vector3.right, delta.sqrMagnitude > 0.0001f ? delta : Vector3.right));
        bar.transform.localScale = new Vector3(delta.magnitude / bar.sprite.bounds.size.x, thickness / bar.sprite.bounds.size.y, 1f);
        bar.color = colour;
    }
}
