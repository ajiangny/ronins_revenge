using System.Collections.Generic;
using UnityEngine;

// The painted backdrop behind a boss's Borderlands-style title card, generated at runtime: aged paper, a black
// ink band across the top, scratches, and a splatter of the boss's colour behind where her portrait sits.
// Built once per colour and shared.
public static class TitleCardArt
{
    // Half the screen's resolution: stretched over it with bilinear filtering it reads as soft painted texture.
    // Positions and sizes below are laid out on a 640x360 grid and scaled up by Scale.
    const int Width = 960, Height = 540;
    const float Scale = 1.5f;

    static readonly Color PaperLight = new Color(0.93f, 0.78f, 0.52f);
    static readonly Color PaperDark = new Color(0.58f, 0.38f, 0.19f);
    static readonly Color Ink = new Color(0.07f, 0.04f, 0.05f);
    static readonly Dictionary<Color, Texture2D> cache = new Dictionary<Color, Texture2D>();

    public static Texture2D Backdrop(Color paint)
    {
        if (cache.TryGetValue(paint, out Texture2D cached) && cached != null) return cached;

        // A fixed seed so every boss's card is painted the same way each time.
        var rng = new System.Random(381);
        var pixels = new Color[Width * Height];
        Paper(pixels);
        Wash(pixels, paint);
        Scratches(pixels, rng);
        InkBand(pixels);
        Splatter(pixels, paint, rng, 345, 195, 34);
        Splatter(pixels, paint, rng, 470, 60, 22);
        Splatter(pixels, paint, rng, 180, 70, 13);
        Splatter(pixels, paint, rng, 40, 215, 9);

        var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        cache[paint] = texture;
        return texture;
    }

    static void Paper(Color[] pixels)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float stain = Fbm(x * 0.011f / Scale, y * 0.011f / Scale, 4);
                float grain = Mathf.PerlinNoise(x * 0.4f / Scale + 91f, y * 0.4f / Scale + 37f);
                Color c = Color.Lerp(PaperDark, PaperLight, Mathf.SmoothStep(0f, 1f, stain * 1.6f - 0.25f + grain * 0.15f));
                // Burnt, darker edges, heaviest in the corners.
                float dx = x / (float)Width * 2f - 1f, dy = y / (float)Height * 2f - 1f;
                float edge = Mathf.Clamp01(dx * dx * 0.55f + dy * dy * 0.8f - 0.3f);
                pixels[y * Width + x] = Color.Lerp(c, PaperDark * 0.55f, edge * 0.9f);
            }
        }
    }

    // A loose wash of the boss's colour soaking into the right half, behind her portrait.
    static void Wash(Color[] pixels, Color paint)
    {
        Color tint = Color.Lerp(Color.white, paint, 0.75f);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float soak = Fbm(x * 0.007f / Scale + 50f, y * 0.007f / Scale + 20f, 3) + (x / (float)Width - 0.55f) * 1.1f;
                float a = Mathf.SmoothStep(0f, 1f, (soak - 0.45f) / 0.25f) * 0.6f;
                int i = y * Width + x;
                pixels[i] = Color.Lerp(pixels[i], pixels[i] * tint * 1.15f, a);
            }
        }
    }

    // A dry-brush stroke of black ink along the top, deepest at the top left, with a ragged, streaky lower edge.
    static void InkBand(Color[] pixels)
    {
        for (int x = 0; x < Width; x++)
        {
            float u = x / (float)Width;
            float edge = Height * (0.74f + 0.16f * u) + (Fbm(u * 6f, 3.3f, 3) - 0.5f) * 40f * Scale;
            for (int y = 0; y < Height; y++)
            {
                float into = y - edge;
                if (into < -18f * Scale) continue;
                // Bristle streaks run along the stroke and thin out towards its edge.
                float bristle = Mathf.PerlinNoise(x * 0.008f / Scale, y * 0.22f / Scale);
                float a = into >= 6f * Scale ? 0.95f : Mathf.Clamp01((into + 18f * Scale) / (24f * Scale)) * Mathf.SmoothStep(0.25f, 0.95f, (bristle - 0.32f) / 0.2f);
                int i = y * Width + x;
                pixels[i] = Color.Lerp(pixels[i], Ink, a);
            }
        }
    }

    // Thin scratches and scuffs across the paper, mostly steep diagonals.
    static void Scratches(Color[] pixels, System.Random rng)
    {
        for (int s = 0; s < 55; s++)
        {
            float x = Range(rng, 0, Width), y = Range(rng, 0, Height);
            float angle = Range(rng, 55f, 85f) * Mathf.Deg2Rad * (rng.NextDouble() < 0.7 ? 1f : -1f);
            float length = Range(rng, 25f, 160f) * Scale;
            bool light = rng.NextDouble() < 0.35;
            float strength = Range(rng, 0.2f, 0.5f);
            Vector2 step = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            for (float t = 0; t < length; t += 0.7f)
            {
                int px = Mathf.RoundToInt(x + step.x * t), py = Mathf.RoundToInt(y + step.y * t);
                if (px < 0 || px >= Width || py < 0 || py >= Height) break;
                int i = py * Width + px;
                float fade = Mathf.Sin(t / length * Mathf.PI) * strength;
                pixels[i] = Color.Lerp(pixels[i], light ? PaperLight * 1.1f : PaperDark * 0.45f, fade);
            }
        }
    }

    // A thrown splash of paint: a ragged blob, spiky rays of droplets flung out of it, loose spatter and drips.
    static void Splatter(Color[] pixels, Color paint, System.Random rng, float cx, float cy, float r)
    {
        cx *= Scale;
        cy *= Scale;
        r *= Scale;
        float seed = Range(rng, 0f, 100f);
        int reach = Mathf.CeilToInt(r * 1.4f);
        for (int y = (int)cy - reach; y <= cy + reach; y++)
        {
            for (int x = (int)cx - reach; x <= cx + reach; x++)
            {
                Vector2 d = new Vector2(x - cx, y - cy);
                float angle = Mathf.Atan2(d.y, d.x);
                float rim = r * (0.8f + 0.45f * Mathf.PerlinNoise(Mathf.Cos(angle) * 1.8f + seed, Mathf.Sin(angle) * 1.8f + seed));
                Paint(pixels, paint, x, y, Mathf.Clamp01(rim - d.magnitude + 0.5f));
            }
        }

        int rays = rng.Next(9, 15);
        for (int k = 0; k < rays; k++)
        {
            float angle = Range(rng, 0f, Mathf.PI * 2f);
            float length = r * Range(rng, 1.4f, 3.4f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // A thin streak out to the first droplet, then droplets shrinking along the ray.
            for (float t = r * 0.8f; t < length * 0.6f; t += 0.5f)
            {
                Disc(pixels, paint, cx + dir.x * t, cy + dir.y * t, Mathf.Lerp(r * 0.12f, 0.6f * Scale, (t - r * 0.8f) / (length * 0.6f - r * 0.8f)));
            }
            int drops = rng.Next(2, 5);
            for (int n = 0; n < drops; n++)
            {
                float t = Mathf.Lerp(length * 0.6f, length, (n + 1f) / drops) + Range(rng, -3f, 3f) * Scale;
                Disc(pixels, paint, cx + dir.x * t, cy + dir.y * t, Mathf.Max(0.8f * Scale, r * 0.13f * (1f - n / (float)drops)));
            }
        }

        for (int n = 0; n < r * 1.2f; n++)
        {
            float angle = Range(rng, 0f, Mathf.PI * 2f), distance = r * Range(rng, 1.1f, 3.6f);
            Disc(pixels, paint, cx + Mathf.Cos(angle) * distance, cy + Mathf.Sin(angle) * distance, Range(rng, 0.6f, 2.2f) * Scale);
        }

        // Drips run down the paper from the blob's underside, ending in a bead.
        int drips = rng.Next(2, 5);
        for (int n = 0; n < drips; n++)
        {
            float x = cx + Range(rng, -0.6f, 0.6f) * r;
            float length = r * Range(rng, 0.8f, 2.4f);
            float width = Range(rng, 1.2f, 2.6f) * Scale;
            for (float t = 0; t < length; t += 0.5f)
            {
                Disc(pixels, paint, x, cy - r * 0.6f - t, width * (1f - 0.4f * t / length));
            }
            Disc(pixels, paint, x, cy - r * 0.6f - length, width * 1.2f);
        }
    }

    static void Disc(Color[] pixels, Color paint, float cx, float cy, float r)
    {
        for (int y = Mathf.FloorToInt(cy - r - 1); y <= cy + r + 1; y++)
        {
            for (int x = Mathf.FloorToInt(cx - r - 1); x <= cx + r + 1; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                Paint(pixels, paint, x, y, Mathf.Clamp01(r - d + 0.5f));
            }
        }
    }

    // Paint lies thick but lets a little of the paper's grain through.
    static void Paint(Color[] pixels, Color paint, int x, int y, float a)
    {
        if (a <= 0f || x < 0 || x >= Width || y < 0 || y >= Height) return;
        int i = y * Width + x;
        float grain = Mathf.PerlinNoise(x * 0.3f / Scale + 13f, y * 0.3f / Scale + 71f);
        Color wet = paint * (0.72f + 0.3f * grain);
        wet.a = 1f;
        pixels[i] = Color.Lerp(pixels[i], wet, a * 0.95f);
    }

    static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amplitude = 0.5f, total = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Mathf.PerlinNoise(x, y) * amplitude;
            total += amplitude;
            x *= 2.03f;
            y *= 2.03f;
            amplitude *= 0.5f;
        }
        return sum / total;
    }

    static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
