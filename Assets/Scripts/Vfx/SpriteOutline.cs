using UnityEngine;

// A thin coloured edge round a sprite: flat silhouettes of its current frame nudged one sprite pixel out in eight
// directions and drawn just behind it, shimmering between two colours. Kagetsune's summoned souls wear one in his
// soul-fire teal, so they read as bound to him.
public class SpriteOutline : MonoBehaviour
{
    static readonly Vector2[] Offsets =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1),
    };
    static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

    SpriteRenderer source;
    SpriteRenderer[] edges;
    MaterialPropertyBlock block;
    Color colour, shimmer;

    // Null when there's no flat material to draw the edge with.
    public static SpriteOutline Wrap(SpriteRenderer target, Color colour, Color shimmer)
    {
        Material flat = Fx.Library != null ? Fx.Library.flashMaterial : null;
        if (target == null || flat == null) return null;
        var outline = target.gameObject.AddComponent<SpriteOutline>();
        outline.Begin(target, flat, colour, shimmer);
        return outline;
    }

    void Begin(SpriteRenderer target, Material flat, Color edge, Color glint)
    {
        source = target;
        colour = edge;
        shimmer = glint;
        block = new MaterialPropertyBlock();
        float pixel = 1f / (source.sprite != null ? source.sprite.pixelsPerUnit : 48f);
        edges = new SpriteRenderer[Offsets.Length];
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i] = new GameObject("Outline").AddComponent<SpriteRenderer>();
            edges[i].transform.SetParent(transform, false);
            edges[i].transform.localPosition = (Vector3)(Offsets[i] * pixel);
            edges[i].sharedMaterial = flat;
            edges[i].sortingLayerID = source.sortingLayerID;
            edges[i].sortingOrder = source.sortingOrder - 1;
        }
        LateUpdate();
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
