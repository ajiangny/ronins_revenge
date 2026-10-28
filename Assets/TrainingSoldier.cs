using System;
using System.Collections;
using UnityEngine;

// One of the five soldiers who find Oni before the army in Poguru's tutorial, his practice targets: an archer for the
// slash, two chargers for the shuriken, a spearman for the parry and a coward who runs for the army. Built in code like
// the Scout, on the Samurai layer so his blows land on them; without art each is a plain marker.
[RequireComponent(typeof(Rigidbody))]
public class TrainingSoldier : MonoBehaviour, IHittable
{
    public enum Kind { Archer, Charger, Spearman, Coward }

    const int SamuraiLayer = 7;
    const float WalkSpeed = 4f;
    const float ShotSpeed = 6f;
    // A beat with the weapon drawn before each shot, so the player sees where it comes from.
    const float Aim = 0.5f;
    static readonly Color Tint = new Color(1f, 0.85f, 0.8f);

    public Kind Role { get; private set; }
    public bool Dead { get; private set; }
    public bool Moving => moving != null;
    public event Action<TrainingSoldier> Killed;

    Rigidbody body;
    SpriteRenderer sprite;
    SpriteAnimator animator;
    CharacterArt art;
    Coroutine moving;
    bool trembling;

    public static TrainingSoldier Spawn(CharacterArt art, Kind kind, Vector3 at)
    {
        var go = new GameObject($"Training{kind}") { layer = SamuraiLayer };
        go.transform.position = at;
        go.transform.localScale = Vector3.one * 1.4f;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        go.AddComponent<SphereCollider>().radius = 0.5f;

        var soldier = go.AddComponent<TrainingSoldier>();
        soldier.body = body;
        soldier.art = art;
        soldier.Role = kind;
        var view = new GameObject("Sprite") { layer = SamuraiLayer };
        view.transform.SetParent(go.transform, false);
        soldier.sprite = view.AddComponent<SpriteRenderer>();
        soldier.sprite.color = Tint;
        soldier.animator = view.AddComponent<SpriteAnimator>();
        soldier.Idle();
        if (!soldier.HasClip(art?.idle)) soldier.sprite.sprite = ProceduralSprites.Glow;
        return soldier;
    }

    bool HasClip(Sprite[] clip) => clip != null && clip.Length > 0;

    void Idle()
    {
        if (art != null && HasClip(art.idle)) animator.Play(art.idle, true, null, art.idleFps * 2f);
    }

    // Walks (or runs) to a spot on the field; a new order replaces the last.
    public void MoveTo(Vector3 to, float speed = WalkSpeed)
    {
        if (Dead) return;
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(Walk(to, speed));
    }

    IEnumerator Walk(Vector3 to, float speed)
    {
        to.z = 0f;
        while (!Dead && (body.position - to).sqrMagnitude > 0.0004f)
        {
            body.MovePosition(Vector3.MoveTowards(body.position, to, speed * Time.fixedDeltaTime));
            yield return new WaitForFixedUpdate();
        }
        moving = null;
    }

    // Draws, then looses one ordinary arrow (or with a spear sprite, an unbreakable spear) at the target.
    public IEnumerator Shoot(Arrow prefab, Vector3 target, Action<Arrow> loosed, Sprite spear = null)
    {
        if (Dead) yield break;
        if (art != null && HasClip(art.attack)) animator.Play(art.attack, false, null, art.attack.Length / Aim);
        yield return new WaitForSeconds(Aim);
        if (Dead) yield break;
        Vector3 muzzle = transform.position + Vector3.down * 0.5f;
        Vector3 to = target - muzzle;
        to.z = 0f;
        Arrow shot = Instantiate(prefab, muzzle, Quaternion.identity);
        if (spear != null) shot.MakeUnbreakable(spear);
        shot.Launch(to.normalized * ShotSpeed, 2f);
        Sfx.Play(Sfx.ArrowFire, 1f, spear != null ? 0.7f : 1f);
        Idle();
        loosed?.Invoke(shot);
    }

    // Knocked back up the field (Poguru's headbutt on a charger that got through), shaken and tinted.
    public void Knockback(Vector3 to)
    {
        if (Dead) return;
        HitFlash.Flash(sprite);
        MoveTo(to, 14f);
    }

    // Blows pass him by (the spearman as his own spear comes back past his head, so he lives to run).
    public bool Untouchable
    {
        set { foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = !value && !Dead; }
    }

    // He's the one talking: he bobs on the spot for as long as his line lasts, so it's clear which soldier speaks.
    public void Speak(float seconds) => speakingUntil = Time.unscaledTime + seconds;
    float speakingUntil;

    // The coward's quiver while he hangs back.
    public void Tremble(bool on) => trembling = on;

    void LateUpdate()
    {
        if (sprite == null) return;
        Vector3 at = trembling && !Dead ? new Vector3(Mathf.Sin(Time.time * 60f) * 0.03f, 0f, 0f) : Vector3.zero;
        if (Time.unscaledTime < speakingUntil && !Dead) at.y += Mathf.Abs(Mathf.Sin(Time.unscaledTime * 12f)) * 0.08f;
        sprite.transform.localPosition = at;
    }

    // Runs off the top of the screen and is gone.
    public void Flee(float speed = 7f)
    {
        if (Dead) return;
        sprite.flipX = !sprite.flipX;
        Vector3 off = new Vector3(transform.position.x, CameraShake.Center.y + Camera.main.orthographicSize + 2f, 0f);
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(FleeTo(off, speed));
    }

    IEnumerator FleeTo(Vector3 off, float speed)
    {
        yield return Walk(off, speed);
        Destroy(gameObject);
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (Dead) return;
        Dead = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Debris.BurstSprites(transform.position, art != null ? art.deathParticles : null, Tint, 14, 1f, 7f);
        Fx.Play(Fx.Explosion, transform.position, Color.white, 1.2f);
        CameraShake.Add(0.15f);
        Sfx.Play(Sfx.SamuraiDeath);
        Killed?.Invoke(this);
        Destroy(gameObject);
    }
}
