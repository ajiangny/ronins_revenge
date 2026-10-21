using System;
using UnityEngine;

// A kite-borne scout that crosses the top of the screen now and then, like the mystery ship in Space Invaders.
// One hit brings it down for bonus points and a soul. Built in code, so it needs no prefab.
[RequireComponent(typeof(Rigidbody))]
public class Scout : MonoBehaviour, IHittable
{
    const float Speed = 4.5f;
    const float BobHeight = 0.35f;
    const float BobRate = 2.2f;
    const int SamuraiLayer = 7;
    static readonly int[] BonusPoints = { 100, 150, 300 };
    static readonly Color KiteColor = new Color(1f, 0.55f, 0.45f);

    public int Points { get; private set; }

    Rigidbody body;
    SpriteRenderer sprite;
    CharacterArt art;
    Action<Scout> killed;
    float direction;
    float baseY;
    float age;
    float exitX;
    bool dead;

    // Enters from just off one side of the screen at height y and flies to the other.
    public static Scout Spawn(CharacterArt art, float y, Action<Scout> onKilled)
    {
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        float direction = UnityEngine.Random.value < 0.5f ? 1f : -1f;
        float centerX = CameraShake.Center.x;

        var go = new GameObject("Scout") { layer = SamuraiLayer };
        go.transform.position = new Vector3(centerX - direction * (halfWidth + 1f), y, 0);
        go.transform.localScale = Vector3.one * 1.4f;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        go.AddComponent<SphereCollider>().radius = 0.5f;

        var scout = go.AddComponent<Scout>();
        scout.body = body;
        scout.art = art;
        scout.killed = onKilled;
        scout.direction = direction;
        scout.baseY = y;
        scout.exitX = centerX + direction * (halfWidth + 1.5f);
        scout.Points = BonusPoints[UnityEngine.Random.Range(0, BonusPoints.Length)];

        var view = new GameObject("Sprite") { layer = SamuraiLayer };
        view.transform.SetParent(go.transform, false);
        scout.sprite = view.AddComponent<SpriteRenderer>();
        scout.sprite.color = KiteColor;
        scout.sprite.flipX = direction < 0;
        view.AddComponent<SpriteAnimator>().Play(art.idle, true, null, art.idleFps * 2f);
        // A warm glow so the scout reads as something special against the sky.
        Kunai.CreateGlow(go.transform, 1.4f, scout.sprite.sortingOrder - 1, new Color(1f, 0.5f, 0.3f, 0.45f));
        Sfx.Play(Sfx.SoulSpawn, 0.7f, 0.7f);
        return scout;
    }

    void FixedUpdate()
    {
        if (dead) return;
        age += Time.fixedDeltaTime;
        Vector3 position = body.position;
        position.x += direction * Speed * Time.fixedDeltaTime;
        position.y = baseY + Mathf.Sin(age * BobRate) * BobHeight;
        body.MovePosition(position);

        if (direction > 0 ? position.x > exitX : position.x < exitX) Destroy(gameObject);
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (dead) return;
        dead = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Debris.BurstSprites(transform.position, art.deathParticles, KiteColor, 14, 1f, 7f);
        Fx.Play(Fx.Explosion, transform.position, Color.white, 1.2f);
        CameraShake.Add(0.15f);
        Sfx.Play(Sfx.SamuraiDeath, 1f, 0.8f);
        Sfx.Play(Sfx.SamuraiCry, 1f, 0.85f);
        killed?.Invoke(this);
        Destroy(gameObject);
    }
}
