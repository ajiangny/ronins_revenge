using UnityEngine;

// Small sprites generated at runtime: a plain white square for UI fills and a soft glowing disc for auras,
// placeholders and flashes. Built once and shared.
public static class ProceduralSprites
{
    static Sprite white;
    static Sprite glow;
    static Sprite softGlow;
    static Sprite shadow;
    static Sprite disc;
    static Sprite arrowDown;
    static Sprite fadeDown;

    static readonly string[][] NotePatterns =
    {
        new[]
        {
            "...##..",
            "...#.#.",
            "...#..#",
            "...#..#",
            "...#...",
            ".###...",
            "####...",
            "####...",
            ".##....",
        },
        new[]
        {
            "..#######",
            "..#######",
            "..#.....#",
            "..#.....#",
            "..#.....#",
            ".##....##",
            "###...###",
            "###...###",
            ".#.....#.",
        },
    };
    static readonly Sprite[] notes = new Sprite[NotePatterns.Length];

    // Rows are listed top to bottom; '#' is a filled pixel.
    static Sprite FromPattern(string[] rows)
    {
        int width = rows[0].Length, height = rows.Length;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[(height - 1 - y) * width + x] = rows[y][x] == '#' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
    }

    static Sprite BuildFadeDown(int height)
    {
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[height];
        for (int y = 0; y < height; y++)
        {
            float k = (y + 0.5f) / height;
            pixels[y] = new Color32(255, 255, 255, (byte)(k * k * 255));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), height);
    }

    public static Sprite White => white != null ? white : white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

    // Just the outline of a sprite's shape, drawn white to be tinted, with its inside filled in the given colour:
    // an empty heart beside the full ones.
    public static Sprite Hollow(Sprite source, Color inside)
    {
        Texture2D copy = ReadBack(source);
        int width = copy.width, height = copy.height;
        // Only the shape matters, so the colours are thrown away.
        Color32[] source32 = copy.GetPixels32();
        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && source32[y * width + x].a > 127;
        var pixels = new Color32[width * height];
        Color32 inside32 = inside;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!Solid(x, y)) continue;
                bool edge = !Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y - 1) || !Solid(x, y + 1);
                pixels[y * width + x] = edge ? new Color32(255, 255, 255, 255) : inside32;
            }
        }
        copy.SetPixels32(pixels);
        copy.Apply();
        return Sprite.Create(copy, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
    }

    // baseSprite with one patch taken from b: the box (padded by pad pixels) where frames a and b differ. Utsusemi's
    // empty-handed stance with its hat off comes from its unarmed frame and the hat region of its bare-headed idle.
    // Null when the three frames aren't the same size.
    public static Sprite Patched(Sprite baseSprite, Sprite a, Sprite b, int pad)
    {
        Texture2D result = ReadBack(baseSprite), first = ReadBack(a), second = ReadBack(b);
        int width = result.width, height = result.height;
        if (first.width != width || second.width != width || first.height != height || second.height != height) return null;
        Color32[] pixels = result.GetPixels32(), aPixels = first.GetPixels32(), bPixels = second.GetPixels32();
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (aPixels[i].Equals(bPixels[i])) continue;
            int x = i % width, y = i / width;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < 0) return baseSprite;
        for (int y = Mathf.Max(0, minY - pad); y <= Mathf.Min(height - 1, maxY + pad); y++)
        {
            for (int x = Mathf.Max(0, minX - pad); x <= Mathf.Min(width - 1, maxX + pad); x++)
            {
                pixels[y * width + x] = bPixels[y * width + x];
            }
        }
        result.SetPixels32(pixels);
        result.Apply();
        Rect rect = baseSprite.rect;
        return Sprite.Create(result, new Rect(0, 0, width, height), new Vector2(baseSprite.pivot.x / rect.width, baseSprite.pivot.y / rect.height), baseSprite.pixelsPerUnit);
    }

    // A sprite's pixels in a new point-filtered texture, read back through the GPU so the art needn't be marked
    // readable. The copy keeps the art's sRGB colours byte for byte.
    static Texture2D ReadBack(Sprite source)
    {
        Rect rect = source.textureRect;
        int width = (int)rect.width, height = (int)rect.height;
        var target = RenderTexture.GetTemporary(source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source.texture, target);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var copy = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return copy;
    }

    // A disc whose alpha fades from the centre to the edge.
    public static Sprite Glow => glow != null ? glow : glow = Circle(64, soft: true);

    // The same glow, smoothly filtered, for big painted-looking washes of light where pixel steps would show.
    public static Sprite SoftGlow => softGlow != null ? softGlow : softGlow = Circle(64, soft: true, FilterMode.Bilinear);

    // A smooth disc that stays solid through its middle and only fades over its outer part, for shading behind text.
    public static Sprite Shadow => shadow != null ? shadow : shadow = BuildShadow(64);

    // A hard-edged pixel disc, used for round UI medallions until the art is in.
    public static Sprite Disc => disc != null ? disc : disc = Circle(64, soft: false);

    // Opaque at the top fading to clear at the bottom, for shading behind the HUD.
    public static Sprite FadeDown => fadeDown != null ? fadeDown : fadeDown = BuildFadeDown(32);

    // Little pixel music notes (a single eighth note and a beamed pair), drawn white to be tinted.
    public static Sprite Note(int kind) => notes[kind % NotePatterns.Length] != null
        ? notes[kind % NotePatterns.Length]
        : notes[kind % NotePatterns.Length] = FromPattern(NotePatterns[kind % NotePatterns.Length]);

    // A dark horizontal brush stroke, drawn white to be tinted: feathered where the brush lands on the left, uneven
    // along its top and bottom, and breaking into dry-brush bristle streaks at its right tail. The game's shared
    // panel shape, behind stage cards, boon cards and menu selections.
    public static Sprite InkStroke => inkStroke != null ? inkStroke : inkStroke = BuildInkStroke(1024, 256);

    // A thin brush line that swells in the middle and tapers to points at both ends, for dividers.
    public static Sprite BrushLine => brushLine != null ? brushLine : brushLine = BuildBrushLine(256, 16);

    static Sprite inkStroke;
    static Sprite brushLine;

    static Sprite BuildInkStroke(int width, int height)
    {
        // A fixed seed, so the stroke is the same shape every run.
        var random = new System.Random(381);
        float[] top = Noise(random, 9), bottom = Noise(random, 9), start = Noise(random, 5), reach = Noise(random, 12);

        // Every row is a bristle: it runs out of ink at its own length, carries a little more or less ink, and, if
        // it's a dry one, skips along its tail. Neighbouring rows are blended so the bristles come in loose bunches.
        var length = new float[height];
        var tone = new float[height];
        var dryness = new float[height];
        var skips = new float[height][];
        float lastLength = 0f, lastTone = 1f, lastDry = 0f;
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            lastLength = Mathf.Lerp(lastLength, (float)random.NextDouble(), y == 0 ? 1f : 0.45f);
            lastTone = Mathf.Lerp(lastTone, 0.9f + 0.1f * (float)random.NextDouble(), 0.5f);
            lastDry = Mathf.Lerp(lastDry, (float)random.NextDouble(), 0.4f);
            length[y] = 0.74f + 0.14f * Sample(reach, v) + 0.12f * lastLength;
            tone[y] = lastTone;
            dryness[y] = lastDry;
            skips[y] = Noise(random, 14);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                // Ragged top and bottom edges that wander a little along the stroke.
                float low = 0.1f + 0.06f * Sample(bottom, u);
                float high = 0.9f - 0.06f * Sample(top, u);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(low, low + 0.02f, v))
                    * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(high, high - 0.02f, v));
                // Feathered where the brush first touched the paper.
                float land = 0.02f + 0.04f * Sample(start, v);
                a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(land - 0.01f, land + 0.015f, u));
                // The bristle thins to a point as its ink runs out.
                float fade = Mathf.InverseLerp(length[y] - 0.1f, length[y], u);
                a *= 1f - fade * fade;
                // Dry bristles skip towards the tail, leaving streaks of bare paper.
                float dry = dryness[y] * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.92f, u));
                a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dry - 0.08f, dry, Sample(skips[y], u)));
                a *= tone[y];
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
    }

    static Sprite BuildBrushLine(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float fromMiddle = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
            for (int x = 0; x < width; x++)
            {
                // Full thickness in the middle, tapering to a point at each end.
                float thickness = Mathf.Pow(Mathf.Sin(Mathf.PI * (x + 0.5f) / width), 0.7f);
                float a = Mathf.Clamp01((thickness - fromMiddle) * height * 0.5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
    }

    // A few random points from 0 to 1, smoothly interpolated by Sample, for gently wandering edges.
    static float[] Noise(System.Random random, int points)
    {
        var values = new float[points];
        for (int i = 0; i < points; i++) values[i] = (float)random.NextDouble();
        return values;
    }

    static float Sample(float[] noise, float u)
    {
        float at = Mathf.Clamp01(u) * (noise.Length - 1);
        int i = Mathf.Min((int)at, noise.Length - 2);
        return Mathf.Lerp(noise[i], noise[i + 1], Mathf.SmoothStep(0f, 1f, at - i));
    }

    // A hard-edged arrow pointing down, standing in for the soul hint's art.
    public static Sprite ArrowDown => arrowDown != null ? arrowDown : arrowDown = BuildArrowDown(16);

    static Sprite BuildArrowDown(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        int shaftHalf = size / 6;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Bottom half is the head (a triangle widening upward), top half the shaft.
                float fromCentre = Mathf.Abs(x + 0.5f - size / 2f);
                bool head = y < size / 2 && fromCentre <= y + 0.5f;
                bool shaft = y >= size / 2 && fromCentre <= shaftHalf;
                pixels[y * size + x] = head || shaft ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite BuildShadow(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite Circle(int size, bool soft, FilterMode filter = FilterMode.Point)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
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
