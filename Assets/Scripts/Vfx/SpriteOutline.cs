using UnityEngine;

// A thin coloured edge round a sprite: flat silhouettes of its current frame nudged one sprite pixel out in eight
// directions and drawn just behind it, shimmering between two colours. Kagetsune's summoned souls wear one in his
// soul-fire teal, so they read as bound to him. Every enemy shot wears one in its ShotClass colour (Arrow.Dress): those
// use only the four axis nudges, since a full curtain is up to BulletHell.Budget of them, and a fixed world thickness,
// since their art is scaled to all sorts of sizes.
public class SpriteOutline : MonoBehaviour
{
    static readonly Vector2[] Offsets =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1),
    };
    const int CardinalCount = 4;
    static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

    SpriteRenderer source;
    SpriteRenderer[] edges;
    MaterialPropertyBlock block;
    Color colour, shimmer;
    // Above zero, how far out the edge sits in world units, instead of one sprite pixel.
    float worldThickness;

    // Null when there's no flat material to draw the edge with. cardinal draws only the four axis nudges (cheaper,
    // for shots); worldThickness above zero sets the edge's width in world units instead of one sprite pixel.
    public static SpriteOutline Wrap(SpriteRenderer target, Color colour, Color shimmer, bool cardinal = false,
        float worldThickness = 0f)
    {
        Material flat = Fx.Library != null ? Fx.Library.flashMaterial : null;
        if (target == null || flat == null) return null;
        var outline = target.gameObject.AddComponent<SpriteOutline>();
        outline.Begin(target, flat, colour, shimmer, cardinal ? CardinalCount : Offsets.Length, worldThickness);
        return outline;
    }

    void Begin(SpriteRenderer target, Material flat, Color edge, Color glint, int count, float thickness)
    {
        source = target;
        colour = edge;
        shimmer = glint;
        worldThickness = thickness;
        block = new MaterialPropertyBlock();
        edges = new SpriteRenderer[count];
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i] = new GameObject("Outline").AddComponent<SpriteRenderer>();
            edges[i].transform.SetParent(transform, false);
            edges[i].sharedMaterial = flat;
            edges[i].sortingLayerID = source.sortingLayerID;
            edges[i].sortingOrder = source.sortingOrder - 1;
        }
        Fit();
        LateUpdate();
    }

    // A new colour for the edge (a shot's flags changed); also refits its width, since a restyle rescales the art.
    public void Recolour(Color edge, Color glint)
    {
        colour = edge;
        shimmer = glint;
        Fit();
        LateUpdate();
    }

    // Places each edge one sprite pixel out, or worldThickness out measured against the art's current scale.
    void Fit()
    {
        if (edges == null) return;
        Vector2 step;
        if (worldThickness > 0f)
        {
            Vector3 scale = transform.lossyScale;
            step = new Vector2(worldThickness / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldThickness / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        }
        else step = Vector2.one / (source.sprite != null ? source.sprite.pixelsPerUnit : 48f);
        for (int i = 0; i < edges.Length; i++)
        {
            if (edges[i] != null) edges[i].transform.localPosition = (Vector3)Vector2.Scale(Offsets[i], step);
        }
    }

    // Taken off (Kagetsune's armour lifting), its edges go with it.
    void OnDestroy()
    {
        if (edges == null) return;
        foreach (SpriteRenderer edge in edges) if (edge != null) Destroy(edge.gameObject);
    }

    void LateUpdate()
    {
        if (source == null) return;
        Color now = Color.Lerp(colour, shimmer, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
        // The edge fades with the sprite, so a soul dissolving takes its outline with it.
        now.a *= source.color.a;
        block.SetColor(FlashColor, now);
        foreach (SpriteRenderer edge in edges)
        {
            if (edge == null) continue;
            edge.sprite = source.sprite;
            edge.flipX = source.flipX;
            edge.SetPropertyBlock(block);
        }
    }
}
