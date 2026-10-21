using UnityEngine;

// Short-lived visuals for the boon effects: a glowing blast for fire, a jagged bolt for lightning, a flash for parries.
// Everything is built in code and fades itself out.
public class ElementFx : MonoBehaviour
{
    SpriteRenderer sprite;
    LineRenderer line;
    Color color;
    float startScale;
    float endScale;
    float lifetime;
    float age;

    // A glowing disc that grows to the blast's size and fades, plus a burst of embers.
    public static void Blast(Vector3 position, float radius, Color color, Sprite[] embers)
    {
        Glow(position, color, radius * 0.6f, radius * 2.4f, 0.35f);
        Debris.BurstSprites(position, embers, color, 10, 0.9f, 4f + 4f * radius);
    }

    public static void Glow(Vector3 position, Color color, float fromSize, float toSize, float seconds, Transform follow = null)
    {
        var go = new GameObject("Glow");
        go.transform.position = position + Vector3.back * 0.5f;
        if (follow != null) go.transform.SetParent(follow, true);
        var fx = go.AddComponent<ElementFx>();
        fx.sprite = go.AddComponent<SpriteRenderer>();
        fx.sprite.sprite = ProceduralSprites.Glow;
        fx.sprite.sortingOrder = 50;
        fx.Begin(color, fromSize, toSize, seconds);
    }

    // A zig-zag lightning bolt between two points. material should draw vertex colours (any sprite material does).
    public static void Bolt(Vector3 from, Vector3 to, Color color, Material material)
    {
        var go = new GameObject("Bolt");
        var fx = go.AddComponent<ElementFx>();
        fx.line = go.AddComponent<LineRenderer>();
        fx.line.sharedMaterial = material;
        fx.line.useWorldSpace = true;
        fx.line.sortingOrder = 50;
        fx.line.widthMultiplier = 0.12f;

        const int segments = 9;
        fx.line.positionCount = segments + 1;
        Vector3 side = Vector3.Cross(to - from, Vector3.forward).normalized;
        for (int i = 0; i <= segments; i++)
        {
            float jitter = i == 0 || i == segments ? 0f : Random.Range(-0.35f, 0.35f);
            fx.line.SetPosition(i, Vector3.Lerp(from, to, i / (float)segments) + side * jitter + Vector3.back);
        }
        fx.Begin(color, 1f, 1f, 0.25f);
        Glow(to, color, 0.6f, 1.8f, 0.25f);
    }

    void Begin(Color tint, float fromScale, float toScale, float seconds)
    {
        color = tint;
        startScale = fromScale;
        endScale = toScale;
        lifetime = seconds;
        Apply(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
        Apply(age / lifetime);
    }

    void Apply(float t)
    {
        Color c = color;
        c.a *= 1f - t * t;
        if (sprite != null)
        {
            sprite.color = c;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t));
        }
        if (line != null)
        {
            line.startColor = line.endColor = c;
        }
    }
}
