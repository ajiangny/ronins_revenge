using System;
using System.Collections.Generic;
using UnityEngine;

// A crack Kagetsune's hammer splits the courtyard with, drawn as pixel art in code along its own jagged path: a
// near-black rift, wide where the blow landed and tapering to a hairline, its far wall lit by the soul-fire below, a
// bright lip on its near edge and a shadow on its far one, short side cracks branching off, and a stepped teal pool of
// light bleeding onto the stone round it. It opens piece by piece as it runs (each piece white-hot as it splits,
// raising `Opened` for the chips, dust and broken cover), burns teal with motes rising out of it, then cools to a dark
// scar and fades. Flare brightens it where a column is about to burst.
public class SoulFissure : MonoBehaviour
{
    // How wide the rift is where it starts and at its tip, in art pixels, and how far its light bleeds.
    const float StartWidth = 5f;
    const float TipWidth = 1.2f;
    const float GlowRadius = 5f;
    // About how long one piece is (world units), and how far the path wanders sideways between its kinks.
    const float PieceLength = 0.9f;
    const float Jitter = 0.22f;
    // Its life once a piece has opened: the flash, the burn, the cooling and the scar.
    const float FlashSeconds = 0.12f;
    const float BurnSeconds = 2.2f;
    const float CoolSeconds = 1.2f;
    const float ScarSeconds = 1.2f;

    static readonly Color32 Rift = new Color32(6, 8, 14, 255);
    static readonly Color32 Wall = new Color32(120, 255, 214, 255);
    static readonly Color32 Lip = new Color32(160, 230, 210, 255);
    static readonly Color32 Shadow = new Color32(12, 10, 16, 190);
    static readonly Color GlowColour = new Color(0.3f, 1f, 0.82f);
    static readonly Color Live = new Color(0.7f, 1f, 0.9f);
    static readonly Color Scar = new Color(0.32f, 0.4f, 0.42f);

    // Raised as each piece opens, with its two ends: where the owner kicks up debris and breaks cover.
    public event Action<Vector3, Vector3> Opened;
    // How long it takes to open from end to end.
    public float OpenSeconds { get; private set; }

    class Piece
    {
        public Vector3 from, to;
        public float openAt;
        public SpriteRenderer crack, glow;
        public float flareUntil;
        public bool opened;
    }

    readonly List<Piece> pieces = new List<Piece>();
    // The motes rising out of it: each sprite, where it rose from, how high it goes, and its age.
    readonly List<(SpriteRenderer look, Vector3 start, float height, float born)> motes = new List<(SpriteRenderer, Vector3, float, float)>();
    const float MoteSeconds = 0.8f;
    float age;
    float nextMote;

    // A fissure from one point to another, opening at `speed` (world units a second), drawn in pixels `pixel` world
    // units across. `width` scales how wide the rift opens.
    public static SoulFissure Open(Vector3 from, Vector3 to, float speed, float pixel, float width = 1f)
    {
        var fissure = new GameObject("SoulFissure").AddComponent<SoulFissure>();
        fissure.Build(from, to, Mathf.Max(0.1f, speed), Mathf.Max(0.005f, pixel), width);
        return fissure;
    }

    // Brightens the stretch nearest `at` for a while: the warning over a column about to burst out of it.
    public void Flare(Vector3 at, float seconds)
    {
        Piece nearest = null;
        float best = float.MaxValue;
        foreach (Piece piece in pieces)
        {
            float d = ((piece.from + piece.to) / 2f - at).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = piece;
            }
        }
        if (nearest != null) nearest.flareUntil = Mathf.Max(nearest.flareUntil, age + seconds);
    }

    void Build(Vector3 from, Vector3 to, float speed, float pixel, float width)
    {
        float length = Vector3.Distance(from, to);
        Vector3 dir = length > 0.001f ? (to - from) / length : Vector3.down;
        Vector3 side = new Vector3(-dir.y, dir.x, 0);
        OpenSeconds = length / speed;

        // The path: kinks every so often, wandering sideways, pinned at both ends.
        int kinks = Mathf.Max(2, Mathf.CeilToInt(length / PieceLength));
        var path = new List<Vector3> { from };
        for (int i = 1; i < kinks; i++) path.Add(from + dir * (length * i / kinks) + side * UnityEngine.Random.Range(-Jitter, Jitter));
        path.Add(to);

        // Side cracks: a few short ones off the main line, narrower than it where they leave it.
        var branches = new List<(int piece, List<Vector3> line, float width)>();
        int count = Mathf.Max(1, Mathf.RoundToInt(length / 2.5f));
        for (int b = 0; b < count; b++)
        {
            int at = UnityEngine.Random.Range(1, path.Count - 1);
            float k = (float)at / (path.Count - 1);
            float turn = UnityEngine.Random.Range(30f, 60f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            Vector3 heading = Quaternion.Euler(0, 0, turn) * dir;
            float reach = UnityEngine.Random.Range(0.5f, 1.3f);
            var line = new List<Vector3> { path[at] };
            for (int s = 1; s <= 3; s++)
            {
                Vector3 bend = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-15f, 15f)) * heading;
                line.Add(line[s - 1] + bend * (reach / 3f));
            }
            branches.Add((at - 1, line, Mathf.Lerp(StartWidth, TipWidth, k) * 0.55f * width));
        }

        // One piece per stretch of the path, opening as the front reaches it.
        float travelled = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            float k0 = (float)i / (path.Count - 1), k1 = (float)(i + 1) / (path.Count - 1);
            var stamps = new List<(Vector3 at, float radius)>();
            Stamp(stamps, path[i], path[i + 1], WidthAt(k0) * width / 2f, WidthAt(k1) * width / 2f, pixel);
            foreach (var branch in branches)
            {
                if (branch.piece != i) continue;
                for (int s = 0; s < branch.line.Count - 1; s++)
                {
                    float r0 = Mathf.Lerp(branch.width, 1f, s / 3f) / 2f, r1 = Mathf.Lerp(branch.width, 1f, (s + 1) / 3f) / 2f;
                    Stamp(stamps, branch.line[s], branch.line[s + 1], r0, r1, pixel);
                }
            }
            var piece = new Piece { from = path[i], to = path[i + 1], openAt = travelled / speed };
            Draw(piece, stamps, pixel);
            pieces.Add(piece);
            travelled += Vector3.Distance(path[i], path[i + 1]);
        }
    }

    // The rift narrows from the blow to its tip, slowly at first.
    static float WidthAt(float k) => Mathf.Lerp(StartWidth, TipWidth, Mathf.Pow(k, 0.8f));

    // Dabs along a line every half pixel, each a disc of the rift's radius there (in pixels).
    static void Stamp(List<(Vector3, float)> stamps, Vector3 a, Vector3 b, float r0, float r1, float pixel)
    {
        float length = Vector3.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / (pixel * 0.5f)));
        for (int s = 0; s <= steps; s++)
        {
            float k = (float)s / steps;
            stamps.Add((Vector3.Lerp(a, b, k), Mathf.Max(0.5f, Mathf.Lerp(r0, r1, k))));
        }
    }

    // Rasterises one piece into two point-filtered sprites: the crack itself and the light round it.
    void Draw(Piece piece, List<(Vector3 at, float radius)> stamps, float pixel)
    {
        float margin = (GlowRadius + StartWidth + 2f) * pixel;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
        foreach (var (at, _) in stamps)
        {
            min = Vector3.Min(min, at);
            max = Vector3.Max(max, at);
        }
        min -= new Vector3(margin, margin);
        max += new Vector3(margin, margin);
        // Snapped to the pixel grid so neighbouring pieces' pixels line up.
        min = new Vector3(Mathf.Floor(min.x / pixel) * pixel, Mathf.Floor(min.y / pixel) * pixel, 0);
        int w = Mathf.CeilToInt((max.x - min.x) / pixel) + 1, h = Mathf.CeilToInt((max.y - min.y) / pixel) + 1;

        var core = new bool[w * h];
        var light = new float[w * h];
        foreach (var (at, radius) in stamps)
        {
            float cx = (at.x - min.x) / pixel, cy = (at.y - min.y) / pixel;
            int reach = Mathf.CeilToInt(radius + GlowRadius);
            for (int y = Mathf.Max(0, (int)cy - reach); y <= Mathf.Min(h - 1, (int)cy + reach); y++)
            {
                for (int x = Mathf.Max(0, (int)cx - reach); x <= Mathf.Min(w - 1, (int)cx + reach); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    int i = y * w + x;
                    if (d <= radius) core[i] = true;
                    float glow = 1f - Mathf.Max(0f, d - radius) / GlowRadius;
                    if (glow > light[i]) light[i] = glow;
                }
            }
        }

        var crackPixels = new Color32[w * h];
        var glowPixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool above = y + 1 < h && core[i + w], below = y > 0 && core[i - w];
                if (core[i])
                {
                    // Inside the rift: its far wall (the top edge) catches the soul-fire from below; the rest is dark.
                    crackPixels[i] = above ? Rift : Wall;
                }
                // The near lip, just under the rift, is lit; the far edge, just over it, is in shadow.
                else if (above) crackPixels[i] = Lip;
                else if (below) crackPixels[i] = Shadow;
                // The light on the stone falls off in hard steps, as pixel art would draw it.
                float step = light[i] > 0.75f ? 0.35f : light[i] > 0.4f ? 0.18f : light[i] > 0.15f ? 0.08f : 0f;
                if (step > 0f) glowPixels[i] = new Color32(255, 255, 255, (byte)(step * 255));
            }
        }
        piece.crack = Sprite(crackPixels, w, h, min, pixel, 3, "Crack");
        piece.glow = Sprite(glowPixels, w, h, min, pixel, 2, "CrackLight");
        piece.crack.enabled = piece.glow.enabled = false;
    }

    SpriteRenderer Sprite(Color32[] pixels, int w, int h, Vector3 corner, float pixel, int order, string label)
    {
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(transform, false);
        renderer.transform.position = corner + Vector3.forward * 0.5f;
        renderer.sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.zero, 1f / pixel);
        renderer.sortingOrder = order;
        return renderer;
    }

    void Update()
    {
        age += Time.deltaTime;
        bool alive = false;
        foreach (Piece piece in pieces)
        {
            if (age < piece.openAt)
            {
                alive = true;
                continue;
            }
            if (!piece.opened)
            {
                piece.opened = true;
                piece.crack.enabled = piece.glow.enabled = true;
                Opened?.Invoke(piece.from, piece.to);
            }
            float t = age - piece.openAt;
            float end = BurnSeconds + CoolSeconds + ScarSeconds;
            if (t < end) alive = true;
            // White-hot as it splits, teal while it burns, cooling to a dark scar, then gone.
            Color tint = t < FlashSeconds ? Color.white
                : t < BurnSeconds ? Live
                : Color.Lerp(Live, Scar, (t - BurnSeconds) / CoolSeconds);
            tint.a = t < BurnSeconds + CoolSeconds ? 1f : 1f - Mathf.Clamp01((t - BurnSeconds - CoolSeconds) / ScarSeconds);
            piece.crack.color = tint;
            float flare = age < piece.flareUntil ? 0.35f + 0.25f * Mathf.Sin(age * 30f) : 0f;
            float breathe = 0.85f + 0.15f * Mathf.Sin(age * 5f + piece.openAt * 7f);
            float glow = t < FlashSeconds ? 1f : t < BurnSeconds ? breathe : Mathf.Max(0f, 1f - (t - BurnSeconds) / CoolSeconds) * breathe;
            piece.glow.color = new Color(GlowColour.r, GlowColour.g, GlowColour.b, Mathf.Clamp01(glow * 0.6f + flare));
        }
        // Souls drift up out of the open crack while it burns.
        if (age >= nextMote)
        {
            nextMote = age + 0.12f;
            Piece source = pieces[UnityEngine.Random.Range(0, pieces.Count)];
            if (source.opened && age - source.openAt < BurnSeconds) Mote(Vector3.Lerp(source.from, source.to, UnityEngine.Random.value));
        }
        for (int m = motes.Count - 1; m >= 0; m--)
        {
            var (look, start, height, born) = motes[m];
            float k = (age - born) / MoteSeconds;
            if (k >= 1f || look == null)
            {
                if (look != null) Destroy(look.gameObject);
                motes.RemoveAt(m);
                continue;
            }
            look.transform.position = start + new Vector3(Mathf.Sin((age - born) * 6f) * 0.06f, height * k, 0);
            look.color = new Color(GlowColour.r, GlowColour.g, GlowColour.b, 0.8f * (1f - k));
        }
        if (!alive && motes.Count == 0) Destroy(gameObject);
    }

    // A small teal mote rising from the crack and fading.
    void Mote(Vector3 at)
    {
        var mote = new GameObject("CrackMote").AddComponent<SpriteRenderer>();
        mote.transform.SetParent(transform, false);
        mote.sprite = ProceduralSprites.Glow;
        mote.sortingOrder = 6;
        mote.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.22f);
        motes.Add((mote, at + Vector3.back * 0.2f, UnityEngine.Random.Range(0.6f, 1.2f), age));
    }

    void OnDestroy()
    {
        // The textures are made here, so they go with it.
        foreach (Piece piece in pieces)
        {
            if (piece.crack != null && piece.crack.sprite != null) Destroy(piece.crack.sprite.texture);
            if (piece.glow != null && piece.glow.sprite != null) Destroy(piece.glow.sprite.texture);
        }
    }
}
