using UnityEngine;

// Small sprites generated at runtime: a plain white square for UI fills and a soft glowing disc for auras,
// placeholders and flashes. Built once and shared.
public static class ProceduralSprites
{
    static Sprite white;
    static Sprite glow;
    static Sprite disc;

    public static Sprite White => white != null ? white : white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

    // A disc whose alpha fades from the centre to the edge.
    public static Sprite Glow => glow != null ? glow : glow = Circle(64, soft: true);

    // A hard-edged pixel disc, used for round UI medallions until the art is in.
    public static Sprite Disc => disc != null ? disc : disc = Circle(64, soft: false);

    static Sprite Circle(int size, bool soft)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = soft ? Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) : d <= 1f ? 1f : 0f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
