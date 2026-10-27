using System.IO;
using UnityEditor;
using UnityEngine;

// Cuts the temple's spirit pool out of its map into full-canvas layers, so they line up with the map with no offsets:
// the water (the shape SoulPool masks its ripples and shimmer to) and the near half of the stone rim (drawn over his
// shins while he wades). Re-run after the map changes, then Ninja/Apply Maps.
public static class TempleLayers
{
    const string Root = "Assets/Art/Sprites/Background/";
    const string Map = Root + "Temple_Hub.png";
    public const string Water = Root + "Temple_PoolWater.png";
    public const string RimFront = Root + "Temple_PoolRimFront.png";

    // In map pixels from the top-left, measured off Temple_Hub.png. GameManager.DefaultSafeAreas uses the same water.
    public static readonly Vector2 WaterCentre = new Vector2(343f, 319f);
    public static readonly Vector2 WaterRadii = new Vector2(39f, 30f);
    static readonly Vector2 RimCentre = new Vector2(343f, 315f);
    static readonly Vector2 RimRadii = new Vector2(53f, 48f);

    [MenuItem("Ninja/Temple/Cut Pool Layers")]
    public static void Cut()
    {
        var map = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        map.LoadImage(File.ReadAllBytes(Map));
        int w = map.width, h = map.height;
        Color32[] source = map.GetPixels32();
        var water = new Color32[source.Length];
        var rim = new Color32[source.Length];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Textures count rows from the bottom; the art's coordinates from the top.
                var px = new Vector2(x + 0.5f, h - y - 0.5f);
                int i = y * w + x;
                bool inWater = Inside(px, WaterCentre, WaterRadii - Vector2.one * 0.5f);
                if (inWater) water[i] = source[i];
                // Only the near half: he stands behind the far rim, which the map already draws under him.
                else if (px.y > WaterCentre.y && Inside(px, RimCentre, RimRadii)) rim[i] = source[i];
            }
        }
        Write(Water, water, w, h);
        Write(RimFront, rim, w, h);
        Object.DestroyImmediate(map);
        Debug.Log("Temple pool layers cut. Run Ninja/Apply Maps to wire them.");
    }

    static bool Inside(Vector2 px, Vector2 centre, Vector2 radii)
    {
        Vector2 d = (px - centre) / radii;
        return d.sqrMagnitude <= 1f;
    }

    static void Write(string path, Color32[] pixels, int w, int h)
    {
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
    }
}
