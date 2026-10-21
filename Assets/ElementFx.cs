using UnityEngine;

// Short-lived visuals for the boon effects: a glowing blast for fire, a jagged bolt for lightning, a flash for parries.
// Everything is built in code and fades itself out.
public class ElementFx : MonoBehaviour
{
    SpriteRenderer sprite;
    LineRenderer line;
    Vector3 boltFrom, boltTo;
    float rejag;
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

    // A zig-zag lightning bolt between two points: a wide coloured glow with a thin white-hot core, re-jagged every
    // couple of frames so it crackles. material should draw vertex colours (any sprite material does).
    public static void Bolt(Vector3 from, Vector3 to, Color color, Material material)
    {
        BoltLayer(from, to, color, material, 0.28f, 50);
        BoltLayer(from, to, Color.Lerp(color, Color.white, 0.8f), material, 0.08f, 51);
        Glow(to, color, 0.6f, 1.8f, 0.25f);
    }

    static void BoltLayer(Vector3 from, Vector3 to, Color color, Material material, float width, int order)
    {
        var go = new GameObject("Bolt");
        var fx = go.AddComponent<ElementFx>();
        fx.line = go.AddComponent<LineRenderer>();
        fx.line.sharedMaterial = material;
        fx.line.useWorldSpace = true;
        fx.line.sortingOrder = order;
        fx.line.widthMultiplier = width;
        fx.line.positionCount = 10;
        fx.boltFrom = from;
        fx.boltTo = to;
        fx.Jag();
        fx.Begin(color, 1f, 1f, 0.28f);
    }

    void Jag()
    {
        int last = line.positionCount - 1;
        Vector3 side = Vector3.Cross(boltTo - boltFrom, Vector3.forward).normalized;
        for (int i = 0; i <= last; i++)
        {
            float jitter = i == 0 || i == last ? 0f : Random.Range(-0.35f, 0.35f);
            line.SetPosition(i, Vector3.Lerp(boltFrom, boltTo, i / (float)last) + side * jitter + Vector3.back);
        }
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
            rejag += Time.deltaTime;
            if (rejag > 0.05f)
            {
                rejag = 0f;
                Jag();
            }
        }
    }
}
