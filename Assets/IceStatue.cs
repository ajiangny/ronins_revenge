using System;
using UnityEngine;

// A samurai cut down while held in ice doesn't fall: it leaves the formation and stands frozen where it died.
// The ronin's next hit on it (a slash, kunai, shuriken or riposte, never a boon effect) bursts it into ice shards,
// which BoonEffects sends at the samurai nearby. Built in code, from the samurai's last frame.
public class IceStatue : MonoBehaviour, IHittable
{
    static readonly Color IceColor = new Color(0.62f, 0.9f, 1f);
    static readonly Color IceGlow = new Color(0.6f, 0.9f, 1f, 0.6f);

    // Raised with the statue's position when the ronin breaks it.
    public static event Action<Vector3> Shattered;

    SpriteRenderer body;
    SpriteRenderer glow;
    Sprite[] chips;
    bool broken;
    float age;

    // chips are the bits of ice it breaks into (the samurai's death particles, tinted icy).
    public static IceStatue Create(SpriteRenderer source, int layer, Sprite[] chips)
    {
        var go = new GameObject("IceStatue") { layer = layer };
        go.transform.position = source.transform.position;
        var statue = go.AddComponent<IceStatue>();
        statue.chips = chips;

        statue.body = new GameObject("Body").AddComponent<SpriteRenderer>();
        statue.body.transform.SetParent(go.transform, false);
        statue.body.transform.localScale = source.transform.lossyScale;
        statue.body.sprite = source.sprite;
        statue.body.flipX = source.flipX;
        statue.body.sortingOrder = source.sortingOrder;
        statue.body.color = IceColor;
        statue.glow = Kunai.CreateGlow(go.transform, 1.8f, source.sortingOrder - 1, IceGlow);

        // The ronin's shots find it by its collider, sized to the frame it froze in.
        Bounds bounds = source.bounds;
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
        // A slow shimmer, so the ice reads as something to strike rather than a corpse.
        age += Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * 4f);
        glow.color = new Color(IceGlow.r, IceGlow.g, IceGlow.b, 0.4f + 0.3f * pulse);
    }

    public void TakeHit(int damage, KillSource source)
    {
        // Effects never break the ice, so shards can't set off a chain of statues.
        if (broken || source == KillSource.Effect) return;
        Burst(1.5f);
        Shattered?.Invoke(transform.position);
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
