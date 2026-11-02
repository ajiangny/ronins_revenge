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
    public const string RoofFront = Root + "Temple_RoofFront.png";

    // In map pixels from the top-left, measured off Temple_Hub.png. GameManager.DefaultSafeAreas uses the same water.
    public static readonly Vector2 WaterCentre = new Vector2(343f, 319f);
    public static readonly Vector2 WaterRadii = new Vector2(39f, 30f);
    static readonly Vector2 RimCentre = new Vector2(343f, 315f);
    static readonly Vector2 RimRadii = new Vector2(53f, 48f);
    // The fallen roof beside the rack's dais, traced off the map in map pixels: Oliver stands behind it.
    static readonly Vector2[] RoofOutline =
    {
        new Vector2(568.75f, 138.75f), new Vector2(632.5f, 100f), new Vector2(640f, 110f), new Vector2(655f, 190f),
        new Vector2(650f, 212.5f), new Vector2(590f, 243.75f), new Vector2(578.75f, 237.5f), new Vector2(588.75f, 223.75f),
        new Vector2(590f, 210f), new Vector2(580f, 200f), new Vector2(573.75f, 152.5f),
    };

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

    // The roof as a layer of its own, the shape SafeArea masks Oliver with so he never seems to stand on it.
    [MenuItem("Ninja/Temple/Cut Roof Layer")]
    public static void CutRoof()
    {
        var map = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        map.LoadImage(File.ReadAllBytes(Map));
        int w = map.width, h = map.height;
        Color32[] source = map.GetPixels32();
        var roof = new Color32[source.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!InsideOutline(new Vector2(x + 0.5f, h - y - 0.5f), RoofOutline)) continue;
                int i = y * w + x;
                roof[i] = new Color32(source[i].r, source[i].g, source[i].b, 255);
            }
        Write(RoofFront, roof, w, h);
        Object.DestroyImmediate(map);
        // A map layer's settings, so it lines up with the map at the same scale.
        var importer = (TextureImporter)AssetImporter.GetAtPath(RoofFront);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 48f;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.isReadable = false;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Debug.Log("Temple roof layer cut. Run Ninja/Apply Maps to wire it.");
    }

    static bool InsideOutline(Vector2 p, Vector2[] outline)
    {
        bool inside = false;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            if ((outline[i].y > p.y) != (outline[j].y > p.y) &&
                p.x < (outline[j].x - outline[i].x) * (p.y - outline[i].y) / (outline[j].y - outline[i].y) + outline[i].x)
                inside = !inside;
        return inside;
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
