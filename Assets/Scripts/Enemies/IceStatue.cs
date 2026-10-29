using System;
using System.Collections.Generic;
using UnityEngine;

// A samurai cut down while held in ice doesn't fall: it leaves the formation and stands frozen where it died.
// The ronin's next hit on it (a slash, shuriken or riposte, never a boon effect) bursts it into ice shards,
// which BoonEffects sends at the samurai nearby. Left alone it melts away, so the ice has to be used quickly rather than
// stockpiled across the field. Built in code, from the samurai's last frame; the melt is drawn in code too.
public class IceStatue : MonoBehaviour, IHittable
{
    static readonly Color IceColor = new Color(0.62f, 0.9f, 1f);
    static readonly Color IceGlow = new Color(0.6f, 0.9f, 1f, 0.6f);
    static readonly Color Meltwater = new Color(0.45f, 0.7f, 1f, 0.75f);
    // Seconds it stands solid, then seconds it takes to melt into a puddle.
    const float SolidTime = 3f;
    const float MeltTime = 1.6f;
    // Past this much of the melt there's too little ice left to burst.
    const float BreakableUntil = 0.65f;
    // Seconds the puddle lingers and fades once the ice is gone.
    const float PuddleTime = 0.8f;
    const float DripInterval = 0.07f;
    const float DripGravity = 9f;

    class Drip
    {
        public SpriteRenderer sprite;
        public Vector3 velocity;
        public float age;
    }

    // Raised with the statue's position, and whether its samurai was soaked, when the ronin breaks it.
    public static event Action<Vector3, bool> Shattered;
    // The samurai froze while soaked: Black Ice bursts it into double shards.
    public bool Soaked { get; set; }

    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer shell;
    Sprite[] chips;
    bool broken;
    float age;
    // The statue's feet and half-width relative to its root, so it melts down onto the spot it stands on.
    float feet;
    float halfWidth;
    float tall;
    SpriteRenderer puddle;
    Vector3 baseScale;
    readonly List<Drip> drips = new List<Drip>();
    float nextDrip;
    bool melting;

    // chips are the bits of ice it breaks into (the samurai's death particles, tinted icy).
    public static IceStatue Create(SpriteRenderer source, int layer, Sprite[] chips, Sprite shell)
    {
        var go = new GameObject("IceStatue") { layer = layer };
        go.transform.position = source.transform.position;
        var statue = go.AddComponent<IceStatue>();
        statue.chips = chips;

        statue.body = new GameObject("Body").AddComponent<SpriteRenderer>();
        statue.body.transform.SetParent(go.transform, false);
        statue.body.transform.localScale = source.transform.lossyScale;
        statue.baseScale = source.transform.lossyScale;
        statue.body.sprite = source.sprite;
        statue.body.flipX = source.flipX;
        statue.body.sortingOrder = source.sortingOrder;
        statue.body.color = IceColor;
        // The ice block is a child of the body, so it slumps and spreads with it as it melts.
        statue.shell = Samurai.AddIceShell(statue.body, shell);
        statue.glow = Projectile.CreateGlow(go.transform, 1.8f, source.sortingOrder - 1, IceGlow);

        // The ronin's shots find it by its collider, sized to the frame it froze in.
        Bounds bounds = source.bounds;
        statue.feet = bounds.min.y - go.transform.position.y;
        statue.halfWidth = bounds.extents.x;
        statue.tall = bounds.size.y;
        var hitbox = go.AddComponent<SphereCollider>();
        hitbox.center = bounds.center - go.transform.position;
        hitbox.radius = Mathf.Max(0.3f, Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.85f);

        HitFlash.Flash(statue.body, 0.15f);
        Fx.Play(Fx.IceBurst, go.transform.position, Color.white, 1.1f);
        Debris.BurstSprites(go.transform.position, chips, IceColor, 6, 0.6f, 4f);
        Sfx.Play(Sfx.FrostChill, 0.8f, 1.2f);
        return statue;
    }

    void Update()
    {
        age += Time.deltaTime;
        float melt = Mathf.Clamp01((age - SolidTime) / MeltTime);
        if (melt <= 0f)
        {
            // A slow shimmer, so the ice reads as something to strike rather than a corpse; it quickens as the
            // ice is about to give.
            float speed = age > SolidTime - 0.8f ? 12f : 4f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * speed);
            glow.color = new Color(IceGlow.r, IceGlow.g, IceGlow.b, 0.4f + 0.3f * pulse);
        }
        else
        {
            Melt(melt);
        }
        UpdateDrips();
    }

    // The ice slumps down onto its feet and spreads a little, clearing to meltwater as it goes, drips run off it and
    // a puddle grows beneath. Once the ice is gone the puddle fades.
    void Melt(float t)
    {
        if (!melting)
        {
            melting = true;
            Sfx.Play(Sfx.FrostChill, 0.5f, 0.6f);
            puddle = new GameObject("Puddle").AddComponent<SpriteRenderer>();
            puddle.transform.SetParent(transform, false);
            puddle.transform.localPosition = new Vector3(0f, feet, 0.1f);
            puddle.sprite = ProceduralSprites.Glow;
            puddle.sortingOrder = body.sortingOrder - 2;
        }
        if (t > BreakableUntil)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        // Melting speeds up as the ice thins.
        float slump = t * t;
        float height = Mathf.Lerp(1f, 0.05f, slump);
        float width = Mathf.Lerp(1f, 1.35f, slump);
        body.transform.localScale = new Vector3(baseScale.x * width, baseScale.y * height, baseScale.z);
        // Scaled about its pivot, so shift it down to keep its feet on the ground.
        body.transform.localPosition = new Vector3(0f, feet * (1f - height), 0f);
        Color ice = Color.Lerp(IceColor, Meltwater, t);
        ice.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 0.7f) / 0.3f));
        body.color = ice;
        if (shell != null) shell.color = new Color(1f, 1f, 1f, ice.a);
        glow.color = new Color(IceGlow.r, IceGlow.g, IceGlow.b, 0.4f * (1f - t));

        float spread = Mathf.Lerp(0.4f, 1.6f, t) * Mathf.Max(halfWidth, 0.5f);
        puddle.transform.localScale = new Vector3(spread * 2f, spread * 0.45f, 1f);
        float fade = Mathf.Clamp01((age - SolidTime - MeltTime) / PuddleTime);
        puddle.color = new Color(Meltwater.r, Meltwater.g, Meltwater.b, Meltwater.a * Mathf.Min(1f, t * 2f) * (1f - fade));

        if (t < 1f && age >= nextDrip)
        {
            nextDrip = age + DripInterval;
            SpawnDrip(height);
        }
        if (fade >= 1f && drips.Count == 0) Destroy(gameObject);
    }

    void SpawnDrip(float height)
    {
        var sprite = new GameObject("Drip").AddComponent<SpriteRenderer>();
        sprite.sprite = ProceduralSprites.Disc;
        sprite.sortingOrder = body.sortingOrder + 1;
        sprite.color = Color.Lerp(Meltwater, Color.white, 0.3f);
        // Drips run off the sides of what's left of the ice.
        float side = UnityEngine.Random.Range(-1f, 1f) * halfWidth;
        float y = feet + UnityEngine.Random.Range(0.2f, 0.9f) * height * tall;
        sprite.transform.position = transform.position + new Vector3(side, y, -0.05f);
        sprite.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.07f, 0.12f);
        drips.Add(new Drip { sprite = sprite, velocity = new Vector3(side * 0.6f, UnityEngine.Random.Range(0f, 0.6f), 0f) });
    }

    void UpdateDrips()
    {
        float floor = transform.position.y + feet;
        for (int i = drips.Count - 1; i >= 0; i--)
        {
            Drip drip = drips[i];
            drip.age += Time.deltaTime;
            drip.velocity.y -= DripGravity * Time.deltaTime;
            drip.sprite.transform.position += drip.velocity * Time.deltaTime;
            // A drip ends where it lands in the puddle.
            if (drip.sprite.transform.position.y > floor && drip.age < 1f) continue;
            Destroy(drip.sprite.gameObject);
            drips.RemoveAt(i);
        }
    }

    void OnDestroy()
    {
        foreach (Drip drip in drips)
        {
            if (drip.sprite != null) Destroy(drip.sprite.gameObject);
        }
    }

    public void TakeHit(int damage, KillSource source)
    {
        // Effects never break the ice, so shards can't set off a chain of statues; nor do the lesser shadow slashes.
        if (broken || !Damage.Procs(source)) return;
        Burst(1.5f);
        Shattered?.Invoke(transform.position, Soaked);
    }

    // The round is over: the ice breaks apart with no shards thrown.
    public static void CrumbleAll()
    {
        foreach (IceStatue statue in FindObjectsByType<IceStatue>()) statue.Burst(1f);
    }

    void Burst(float size)
    {
        broken = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Fx.Play(Fx.IceBurst, transform.position, Color.white, size);
        Debris.BurstSprites(transform.position, chips, IceColor, 14, 0.9f, 8f);
        CameraShake.Add(0.12f * size);
        Sfx.Play(Sfx.WardShatter, 0.9f, 1.15f);
        Destroy(gameObject);
    }
}
