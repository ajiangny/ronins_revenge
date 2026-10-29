using UnityEngine;

// Short-lived visuals for the boon effects: a glowing blast for fire, a jagged bolt for lightning, a flash for parries.
// Everything is built in code and fades itself out.
public class ElementFx : MonoBehaviour
{
    SpriteRenderer sprite;
    LineRenderer line;
    Vector3 boltFrom, boltTo;
    float rejag;
    float jag = 0.35f;
    Color color;
    float startScale;
    float endScale;
    float lifetime;
    float age;
    // A wave: the glow is stretched this wide for its height, and travels from where it starts to `driftTo`.
    Vector2 stretch = Vector2.one;
    Vector3 driftFrom, driftTo;
    bool drifts;

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

    // A wave of water: a wide, flat glow that rolls from one point to another and fades, with a few drops thrown
    // off as it goes. Stands in for the TideWave art when the library has none.
    public static void Wave(Vector3 from, Vector3 to, Color color, float width, float seconds, Sprite[] drops = null)
    {
        var go = new GameObject("Wave");
        go.transform.position = from + Vector3.back * 0.5f;
        var fx = go.AddComponent<ElementFx>();
        fx.sprite = go.AddComponent<SpriteRenderer>();
        fx.sprite.sprite = ProceduralSprites.Glow;
        fx.sprite.sortingOrder = 50;
        fx.stretch = new Vector2(width, Mathf.Max(0.6f, width * 0.3f));
        fx.driftFrom = go.transform.position;
        fx.driftTo = new Vector3(to.x, to.y, go.transform.position.z);
        fx.drifts = true;
        fx.Begin(color, 0.6f, 1f, seconds);
        if (drops != null) Debris.BurstSprites((from + to) * 0.5f, drops, color, 8, 0.8f, 4f);
    }

    // A zig-zag lightning bolt between two points: a wide coloured glow with a thin white-hot core, re-jagged every
    // couple of frames so it crackles. material should draw vertex colours (any sprite material does).
    public static void Bolt(Vector3 from, Vector3 to, Color color, Material material)
    {
        BoltLayer(from, to, color, material, 0.28f, 50);
        BoltLayer(from, to, Color.Lerp(color, Color.white, 0.8f), material, 0.08f, 51);
        Glow(to, color, 0.6f, 1.8f, 0.25f);
    }

    // Lightning arcing sideways from one target to the next: two crackling strands and a white core that linger long
    // enough to follow, with a flash where it leaves and where it lands.
    public static void Chain(Vector3 from, Vector3 to, Color color, Material material)
    {
        BoltLayer(from, to, color, material, 0.42f, 50, 0.45f, 16, 0.45f);
        BoltLayer(from, to, Color.Lerp(color, Color.white, 0.4f), material, 0.16f, 51, 0.45f, 16, 0.6f);
        BoltLayer(from, to, Color.white, material, 0.07f, 52, 0.4f, 16, 0.3f);
        Glow(from, color, 0.8f, 1.6f, 0.3f);
        Glow(to, color, 0.8f, 2.2f, 0.35f);
    }

    static void BoltLayer(Vector3 from, Vector3 to, Color color, Material material, float width, int order,
        float seconds = 0.28f, int points = 10, float jag = 0.35f)
    {
        var go = new GameObject("Bolt");
        var fx = go.AddComponent<ElementFx>();
        fx.line = go.AddComponent<LineRenderer>();
        fx.line.sharedMaterial = material;
        fx.line.useWorldSpace = true;
        fx.line.sortingOrder = order;
        fx.line.widthMultiplier = width;
        fx.line.positionCount = points;
        fx.boltFrom = from;
        fx.boltTo = to;
        fx.jag = jag;
        fx.Jag();
        fx.Begin(color, 1f, 1f, seconds);
    }

    void Jag()
    {
        int last = line.positionCount - 1;
        Vector3 side = Vector3.Cross(boltTo - boltFrom, Vector3.forward).normalized;
        for (int i = 0; i <= last; i++)
        {
            float jitter = i == 0 || i == last ? 0f : Random.Range(-jag, jag);
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
            float size = Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t));
            transform.localScale = new Vector3(size * stretch.x, size * stretch.y, 1f);
            if (drifts) transform.position = Vector3.Lerp(driftFrom, driftTo, 1f - (1f - t) * (1f - t));
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
