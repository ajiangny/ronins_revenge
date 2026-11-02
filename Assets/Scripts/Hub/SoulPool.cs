using System.Collections;
using UnityEngine;

// The temple's spirit pool come alive: light plays over the water, rings spread on it now and then and souls drift up
// out of it, and a few more wander up off the temple floor; until Oni has met Asagi, her koi's wake crosses it. When Oni steps in he wades: the water hides his feet, the
// near rim stands in front of his shins, rings spread from where he stands and it splashes as he enters and leaves. Built by SafeArea over the pool its map
// already draws; everything here is code-built on top, and without the water layer it skips the masking.
public class SoulPool : MonoBehaviour
{
    static readonly Color Teal = new Color(0.55f, 1f, 0.92f);
    // Sorting orders: the mask's range covers the surface layers only (the ground is -100, props -1, Oni 0).
    const int MaskBack = -13, MaskFront = -6;
    const int ShimmerOrder = -11, RingOrder = -8;

    SafeArea area;
    Ronin ronin;
    Vector2 centre, radii;
    Sprite[] souls;
    float px;
    bool masked;
    SpriteRenderer[] shimmer;
    Vector3 shimmerOrigin;
    Vector2 shimmerSize;

    bool inWater;
    SpriteRenderer meniscus;
    Vector3 lastFeet;
    float nextStep, nextStill, nextIdleRing, nextWisp, nextStray;

    // The koi under the water (Asagi, before Oni has met her): where she is in the water, as a fraction of its radii
    // from the middle, the way she's heading, and her next pair of rings.
    public bool Koi { get; set; }
    Vector2 koiAt;
    float koiHeading, koiSeed, nextKoiRing;
    const float KoiSpeed = 0.12f;
    static readonly Color KoiTint = new Color(0.6f, 0.85f, 1f);

    public static SoulPool Build(SafeArea area, SafeArea.Config config, Ronin ronin, Bounds ground)
    {
        var pool = new GameObject("SoulPool").AddComponent<SoulPool>();
        pool.transform.SetParent(area.transform, false);
        pool.Lay(area, config, ronin, ground);
        return pool;
    }

    // Where his feet are, 1 at the middle of the water and 0 at its edge; below 0 he's out of it.
    public float DepthAt(Vector3 feet)
    {
        Vector2 d = (area.WorldToMap(feet) - centre) / radii;
        return 1f - d.magnitude;
    }

    // A point inside the water, t of the way out from its middle.
    Vector3 PointInWater(float t)
    {
        float a = Random.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Random.value) * t;
        return area.MapToWorld(centre + new Vector2(Mathf.Cos(a) * radii.x * r, Mathf.Sin(a) * radii.y * r));
    }

    void Lay(SafeArea safeArea, SafeArea.Config config, Ronin oni, Bounds ground)
    {
        area = safeArea;
        ronin = oni;
        centre = config.poolCentre;
        radii = config.poolRadii;
        souls = config.poolSouls;
        px = area.MapPixel;

        // The layers are full-canvas cut-outs of the map, so they're laid exactly as the ground is.
        masked = config.poolWater != null;
        if (masked)
        {
            var mask = new GameObject("Water").AddComponent<SpriteMask>();
            mask.transform.SetParent(transform, false);
            mask.sprite = config.poolWater;
            mask.alphaCutoff = 0.5f;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = MaskBack;
            mask.frontSortingOrder = MaskFront;
            Overlay(mask.transform, config.poolWater, ground);
        }
        // The near rim hides his legs rather than being drawn over them: a mask on his order alone, so the map's own
        // rim shows through, as dark as the moon's shade leaves it. It only bites while he wades (Ronin.Wade).
        if (config.poolRimFront != null)
        {
            var rim = new GameObject("RimFront").AddComponent<SpriteMask>();
            rim.transform.SetParent(transform, false);
            rim.sprite = config.poolRimFront;
            rim.alphaCutoff = 0.5f;
            rim.isCustomRangeActive = true;
            rim.backSortingOrder = ronin.SpriteOrder - 1;
            rim.frontSortingOrder = ronin.SpriteOrder;
            Overlay(rim.transform, config.poolRimFront, ground);
        }

        // Two layers of light dashes drifting opposite ways, each a pair of tiles so it wraps without a gap.
        Sprite caustic = ProceduralSprites.Caustic;
        shimmerSize = (Vector2)caustic.bounds.size * px;
        shimmerOrigin = area.MapToWorld(centre);
        shimmer = new SpriteRenderer[4];
        for (int i = 0; i < shimmer.Length; i++)
        {
            var r = new GameObject("Shimmer").AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            r.sprite = caustic;
            r.transform.localScale = new Vector3(px, px, 1f);
            r.flipY = i >= 2;
            r.sortingOrder = ShimmerOrder + (i >= 2 ? 1 : 0);
            r.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            r.enabled = masked;
            shimmer[i] = r;
        }
        lastFeet = ronin.Feet;
        inWater = DepthAt(lastFeet) > 0f;
        live = this;
        Sfx.PlayLoop(Sfx.PoolAmbience);
    }

    // The pool whose ambience is playing: a rebuilt temple destroys the old pool at the end of the frame the new one
    // started in, and that old pool must not stop the new one's loop.
    static SoulPool live;

    static void Overlay(Transform t, Sprite sprite, Bounds ground)
    {
        t.position = new Vector3(ground.center.x, ground.center.y, 0f);
        t.localScale = Vector3.one * (ground.size.y / sprite.bounds.size.y);
    }

    void Update()
    {
        float time = Time.time;
        for (int i = 0; i < shimmer.Length; i++)
        {
            bool second = i >= 2;
            float speed = second ? -2.2f : 3.1f;
            float offset = Mathf.Repeat(time * speed * px, shimmerSize.x);
            float x = shimmerOrigin.x + offset - (i % 2 == 0 ? 0f : shimmerSize.x);
            float y = shimmerOrigin.y + (second ? 3f * px : 0f) + Mathf.Sin(time * 0.7f + i) * px;
            shimmer[i].transform.position = new Vector3(x, y, 0f);
            float pulse = 0.22f + 0.12f * Mathf.Sin(time * (second ? 1.7f : 1.1f) + i);
            shimmer[i].color = new Color(Teal.r, Teal.g, Teal.b, pulse);
        }

        if (time >= nextIdleRing)
        {
            nextIdleRing = time + Random.Range(1.6f, 3.2f);
            StartCoroutine(Ripple(PointInWater(0.75f), 2, Random.Range(6, 11), 1.4f, 0.35f));
        }
        if (Koi) Swim(time);
        if (time >= nextWisp)
        {
            nextWisp = time + Random.Range(0.9f, 1.8f);
            StartCoroutine(Mote(PointInWater(0.6f), 1f));
        }
        // Strays: a soul now and then rising off the flagstones anywhere in the temple, fainter than the pool's.
        if (time >= nextStray)
        {
            nextStray = time + Random.Range(0.8f, 1.8f);
            Rect floor = area.WalkArea;
            StartCoroutine(Mote(new Vector3(Random.Range(floor.xMin - 1f, floor.xMax + 1f), Random.Range(floor.yMin, floor.yMax), 0f), 0.6f));
        }

        Wading();
    }

    void Wading()
    {
        Vector3 feet = ronin.Feet;
        float depth = DepthAt(feet);
        bool now = depth > 0f;
        if (now != inWater)
        {
            inWater = now;
            Splash(feet, now);
        }
        if (!inWater)
        {
            ronin.Wade(0f);
            if (meniscus != null) meniscus.enabled = false;
            lastFeet = feet;
            return;
        }
        // Ankle-deep at the edge, to his shins in the middle (his short top-down legs make more look waist-deep).
        float wade = Mathf.Lerp(1.5f, 3.5f, Mathf.Clamp01(depth * 1.5f)) * px;
        ronin.Wade(wade);
        // Where the water meets him: rings spread from here, and one stays around him on the surface. It's drawn on
        // the water under him, so his body hides its far side and only the near arc shows.
        Vector3 waterline = feet + Vector3.up * wade;
        if (meniscus == null)
        {
            meniscus = new GameObject("Meniscus").AddComponent<SpriteRenderer>();
            meniscus.transform.SetParent(transform, false);
            meniscus.transform.localScale = new Vector3(px, px, 1f);
            meniscus.sprite = ProceduralSprites.Ring(7);
            meniscus.sortingOrder = RingOrder;
            meniscus.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        }
        meniscus.enabled = true;
        meniscus.transform.position = waterline;
        meniscus.color = new Color(Teal.r, Teal.g, Teal.b, 0.55f + 0.15f * Mathf.Sin(Time.time * 4f));
        feet = waterline;

        float time = Time.time;
        bool moving = (feet - lastFeet).sqrMagnitude > 0.0004f;
        lastFeet = feet;
        if (moving && time >= nextStep)
        {
            nextStep = time + 0.28f;
            nextStill = time + 0.6f;
            StartCoroutine(Ripple(feet, 3, 13, 0.7f, 0.8f));
            Sfx.Play(Sfx.WaterStep, 0.6f);
        }
        else if (!moving && time >= nextStill)
        {
            nextStill = time + 1.2f;
            StartCoroutine(Ripple(feet, 4, 10, 1.1f, 0.4f));
        }
    }

    // Something swims under the surface: a slow wandering point inside the water that leaves a pair of faint rings
    // behind it, one a little ahead of the other along the way it's going, like a fish's wake. Its heading drifts
    // on noise and bends back toward the middle near the edge, so it never leaves the pool.
    void Swim(float time)
    {
        if (koiSeed == 0f)
        {
            koiSeed = Random.Range(1f, 100f);
            koiHeading = Random.Range(0f, Mathf.PI * 2f);
            koiAt = Vector2.zero;
        }
        koiHeading += (Mathf.PerlinNoise(time * 0.25f, koiSeed) - 0.5f) * 1.6f * Time.deltaTime;
        Vector2 step = new Vector2(Mathf.Cos(koiHeading), Mathf.Sin(koiHeading)) * KoiSpeed * Time.deltaTime;
        koiAt += step;
        if (koiAt.magnitude > 0.7f)
        {
            // Turned back toward the middle, with a little of the old heading kept so the turn is a curve.
            float home = Mathf.Atan2(-koiAt.y, -koiAt.x);
            koiHeading = Mathf.LerpAngle(koiHeading * Mathf.Rad2Deg, home * Mathf.Rad2Deg, 0.1f) * Mathf.Deg2Rad;
            koiAt = koiAt.normalized * 0.7f;
        }
        if (time < nextKoiRing) return;
        nextKoiRing = time + 0.55f;
        // Map rows run downward, so a heading's y is flipped into map pixels. The second ring trails a few pixels.
        Vector2 ahead = new Vector2(Mathf.Cos(koiHeading), -Mathf.Sin(koiHeading));
        Vector2 here = centre + koiAt * radii;
        StartCoroutine(Ripple(area.MapToWorld(here + ahead * 2f), 1, 5, 1.6f, 0.2f, 0f, KoiTint));
        StartCoroutine(Ripple(area.MapToWorld(here - ahead * 4f), 1, 4, 1.4f, 0.14f, 0.18f, KoiTint));
    }

    // In: a few rings at once and drops thrown up. Out: one ring and a drop or two.
    void Splash(Vector3 feet, bool entering)
    {
        if (entering)
        {
            StartCoroutine(Ripple(feet, 3, 16, 0.8f, 0.9f));
            StartCoroutine(Ripple(feet, 2, 11, 0.6f, 0.7f, 0.12f));
            Sfx.Play(Sfx.PoolSplash);
        }
        else
        {
            StartCoroutine(Ripple(feet, 3, 10, 0.6f, 0.6f));
            Sfx.Play(Sfx.WaterStep, 0.5f);
        }
        int drops = entering ? Random.Range(4, 7) : 2;
        for (int i = 0; i < drops; i++) StartCoroutine(Drop(feet));
    }

    // A one-pixel ring stepping out from one radius to another (in map pixels) as it fades.
    IEnumerator Ripple(Vector3 at, int from, int to, float seconds, float alpha, float delay = 0f, Color? tint = null)
    {
        Color colour = tint ?? Teal;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var r = new GameObject("Ripple").AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.transform.position = at;
        r.transform.localScale = new Vector3(px, px, 1f);
        r.sortingOrder = RingOrder;
        r.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            r.sprite = ProceduralSprites.Ring(Mathf.RoundToInt(Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k))));
            r.color = new Color(colour.r, colour.g, colour.b, alpha * (1f - k));
            yield return null;
        }
        Destroy(r.gameObject);
    }

    // A drop of water thrown up and falling back, one map pixel big, in front of him.
    IEnumerator Drop(Vector3 from)
    {
        var r = new GameObject("Drop").AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = ProceduralSprites.White;
        Vector2 native = r.sprite.bounds.size;
        r.transform.localScale = new Vector3(px / native.x, px / native.y, 1f);
        r.sortingOrder = ronin.SpriteOrder + 4;
        Vector3 velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(2.2f, 3.6f), 0f);
        Vector3 at = from;
        for (float t = 0f; at.y >= from.y - 0.05f || t < 0.1f; t += Time.deltaTime)
        {
            velocity.y -= 14f * Time.deltaTime;
            at += velocity * Time.deltaTime;
            r.transform.position = at;
            r.color = new Color(Teal.r, Teal.g, Teal.b, Mathf.Clamp01(1f - t * 1.5f));
            if (t > 0.8f) break;
            yield return null;
        }
        Destroy(r.gameObject);
    }

    // A soul drifts up off the ground, swaying, and fades: its frames at the map's pixel size, or a soft light. It rises
    // from a spot on the ground, so it's in front of him when that spot is nearer the viewer than his feet.
    IEnumerator Mote(Vector3 from, float brightness)
    {
        bool drawn = souls != null && souls.Length > 0 && souls[0] != null;
        var r = new GameObject("Soul").AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = drawn ? souls[0] : ProceduralSprites.SoftGlow;
        r.transform.localScale = drawn ? Vector3.one * px * souls[0].pixelsPerUnit * Random.Range(0.45f, 0.6f) : Vector3.one * 0.35f;
        r.sortingOrder = ronin.SpriteOrder - 1;
        float sway = Random.Range(0f, 6f), life = Random.Range(2f, 2.8f);
        Color tint = drawn ? Color.white : Teal;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            if (drawn) r.sprite = souls[(int)(t * 10f) % souls.Length];
            r.sortingOrder = from.y < ronin.Feet.y ? ronin.SpriteOrder + 6 : ronin.SpriteOrder - 1;
            r.transform.position = from + new Vector3(Mathf.Sin(t * 2f + sway) * 0.15f, t * 0.6f, 0f);
            r.color = new Color(tint.r, tint.g, tint.b, brightness * (drawn ? 0.85f : 0.6f) * Mathf.Sin(t / life * Mathf.PI));
            yield return null;
        }
        Destroy(r.gameObject);
    }

    void OnDestroy()
    {
        if (ronin != null) ronin.Wade(0f);
        if (live == this) { live = null; Sfx.Stop(Sfx.PoolAmbience); }
    }
}
