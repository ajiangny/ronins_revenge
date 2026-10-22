using System;
using UnityEngine;

// What killed a samurai. Boon effects only trigger on the ronin's own attacks, never on other effects' kills,
// so explosions and lightning can't chain into each other forever. Reflected is a boss's own slash parried back at it.
// Slash is the katana's crescent wave; new values go on the end so saved ints keep their meaning.
public enum KillSource { Kunai, Shuriken, Deflected, Effect, Reflected, Slash }

// Every projectile the ronin sends upward: katana slash waves, thrown kunai, the Spirit Shuriken and parried arrows.
[RequireComponent(typeof(Rigidbody))]
public class Kunai : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] float speed = 18f;
    [SerializeField] float lifetime = 2f;
    [Tooltip("Visual child that spins; the collider stays on the root so it doesn't turn with it.")]
    [SerializeField] Transform spinner;
    [SerializeField] float spinSpeed = 1080f;
    [Tooltip("Draws the art bigger than the hitbox so projectiles are easy to follow without changing what they hit.")]
    [SerializeField] float artScale = 1.8f;
    [Tooltip("Soft glow behind the projectile, in world units; its colour comes from Tint.")]
    [SerializeField] float glowSize = 0.9f;

    [Header("Parried shots")]
    [Tooltip("How fast a parried shot turns toward its target, in degrees per second.")]
    [SerializeField] float homingTurnRate = 540f;
    [Tooltip("Size of the pulsing parry aura, in world units.")]
    [SerializeField] float parryAuraSize = 1.7f;
    [Tooltip("Seconds between the fading afterimages a parried shot leaves behind.")]
    [SerializeField] float afterimageInterval = 0.035f;

    public KillSource Source { get; private set; } = KillSource.Kunai;
    // Raised with the contact point whenever this shot damages something (fireball and ice shard ripostes).
    public event Action<Vector3, IHittable> Impacted;
    // Ice shards encase a samurai before they hit it, so one they cut down freezes into an ice statue.
    public float FreezeOnHit { get; set; }
    // Piercing Slash: cuts through arrows (they break on it) instead of being stopped by them.
    public bool CutsArrows { get; set; }

    Rigidbody body;
    SpriteRenderer art;
    SpriteRenderer glow;
    Vector3 velocity;
    int pierceLeft;
    bool spent;
    bool launched;
    Func<Transform> pickTarget;
    Transform target;
    Collider[] targetColliders;
    SpriteRenderer parryAura;
    Color parryColor;
    float nextAfterimage;
    Action<Kunai> split;
    float splitAt;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        art = GetComponentInChildren<SpriteRenderer>();
        art.transform.localScale *= artScale;
        glow = CreateGlow(transform, glowSize, art.sortingOrder - 1, new Color(0.55f, 1f, 0.85f, 0.45f));
    }

    // Shared with Arrow: a soft disc behind a projectile so it reads against the grass.
    public static SpriteRenderer CreateGlow(Transform parent, float size, int sortingOrder, Color color)
    {
        var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(parent, false);
        glow.transform.localPosition = Vector3.forward * 0.05f;
        glow.transform.localScale = Vector3.one * size;
        glow.sprite = ProceduralSprites.Glow;
        glow.sortingOrder = sortingOrder;
        glow.color = color;
        return glow;
    }

    void Start()
    {
        if (!launched) Launch(Vector3.up, 1f, 0, KillSource.Kunai);
    }

    // direction is normalised by the caller; sprite (optional) replaces the prefab's art.
    public void Launch(Vector3 direction, float speedMultiplier, int pierce, KillSource source,
        Sprite sprite = null, bool flipSprite = false, float scale = 1f, bool spin = false, float lifetimeMultiplier = 1f)
    {
        launched = true;
        Source = source;
        pierceLeft = pierce;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        transform.localScale *= scale;
        if (sprite != null) art.sprite = sprite;
        art.flipY = flipSprite;
        if (spin) spinner = art.transform;

        velocity = direction * (speed * speedMultiplier);
        body.linearVelocity = velocity;
        Destroy(gameObject, lifetime * lifetimeMultiplier);
    }

    // spriteColor tints the art (white for recoloured element art); glowColor is the halo, usually the element's colour.
    public void Tint(Color spriteColor, Color glowColor)
    {
        art.color = spriteColor;
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.45f);
    }

    // Takes on a parried shot's own look: its sprite, colour, drawn size and animation.
    // Call after Launch, which sets the hitbox scale this has to undo for the art.
    public void Mimic(SpriteRenderer source)
    {
        art.sprite = source.sprite;
        art.color = source.color;
        Vector3 world = source.transform.lossyScale;
        Vector3 parent = art.transform.parent.lossyScale;
        art.transform.localScale = new Vector3(Mathf.Abs(world.x / parent.x), Mathf.Abs(world.y / parent.y), 1f);
        if (source.TryGetComponent(out SpriteAnimator sourceAnimator) && sourceAnimator.Clip != null)
        {
            if (!art.TryGetComponent(out SpriteAnimator animator)) animator = art.gameObject.AddComponent<SpriteAnimator>();
            animator.Play(sourceAnimator.Clip, sourceAnimator.Looping, null, sourceAnimator.ClipFps);
        }
    }

    // Swaps the art for a looping animation drawn drawSize world units tall, such as a fireball or ice shard.
    // Call after Launch, which sets the hitbox scale this has to undo for the art.
    public void Animate(Sprite[] frames, float fps, float drawSize)
    {
        art.sprite = frames[0];
        art.color = Color.white;
        art.flipY = false;
        float size = drawSize / (frames[0].bounds.size.y * art.transform.parent.lossyScale.y);
        art.transform.localScale = new Vector3(size, size, 1f);
        if (!art.TryGetComponent(out SpriteAnimator animator)) animator = art.gameObject.AddComponent<SpriteAnimator>();
        animator.Play(frames, true, null, fps);
    }

    // Seeks whatever picker returns, steering at homingTurnRate. When the target falls (its colliders switch off)
    // it asks for a new one; with nothing left it flies straight on.
    public void Home(Func<Transform> picker)
    {
        pickTarget = picker;
        AcquireTarget();
        // Turn straight at the first target so the shot leaves the parry already on its way.
        if (target != null) Steer(TargetPoint() - transform.position, float.MaxValue);
    }

    // A pulsing halo in the parry's colour and a trail of fading afterimages: this shot was parried.
    public void ParryAura(Color color)
    {
        parryColor = color;
        parryAura = CreateGlow(transform, parryAuraSize, art.sortingOrder - 2, color);
    }

    void AcquireTarget()
    {
        target = pickTarget?.Invoke();
        targetColliders = target != null ? target.GetComponentsInChildren<Collider>() : null;
    }

    bool TargetAlive()
    {
        if (target == null) return false;
        foreach (Collider c in targetColliders)
        {
            if (c != null && c.enabled) return true;
        }
        return false;
    }

    bool IsTarget(Collider other) => Array.IndexOf(targetColliders, other) >= 0;

    Vector3 TargetPoint()
    {
        foreach (Collider c in targetColliders)
        {
            if (c != null && c.enabled) return c.bounds.center;
        }
        return target.position;
    }

    void Steer(Vector3 toward, float maxDegrees)
    {
        toward.z = 0;
        if (toward.sqrMagnitude < 0.0001f) return;
        Vector3 direction = Vector3.RotateTowards(velocity.normalized, toward.normalized, maxDegrees * Mathf.Deg2Rad, 0f);
        velocity = direction * velocity.magnitude;
        body.linearVelocity = velocity;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
    }

    void FixedUpdate()
    {
        if (pickTarget == null || spent) return;
        if (!TargetAlive()) AcquireTarget();
        if (TargetAlive()) Steer(TargetPoint() - transform.position, homingTurnRate * Time.fixedDeltaTime);
    }

    // Slashes forked together (Split Slash) start on the same spot; they must not knock each other out.
    public void IgnoreCollisionsWith(Kunai other)
    {
        foreach (Collider mine in GetComponentsInChildren<Collider>())
        {
            foreach (Collider theirs in other.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(mine, theirs);
            }
        }
    }

    // Split Slash: calls back once, this many seconds into the flight, so the ronin can fork it.
    public void SplitAfter(float seconds, Action<Kunai> onSplit)
    {
        split = onSplit;
        splitAt = Time.time + seconds;
    }

    void Update()
    {
        if (split != null && !spent && Time.time >= splitAt)
        {
            Action<Kunai> fork = split;
            split = null;
            fork(this);
        }
        if (spinner != null)
        {
            spinner.Rotate(0, 0, -spinSpeed * Time.deltaTime);
        }
        if (parryAura != null) UpdateParryAura();
    }

    void UpdateParryAura()
    {
        float pulse = Mathf.Sin(Time.time * 18f);
        parryAura.transform.localScale = Vector3.one * (parryAuraSize * (1f + 0.15f * pulse));
        parryAura.color = new Color(parryColor.r, parryColor.g, parryColor.b, 0.55f + 0.2f * pulse);
        if (Time.time < nextAfterimage) return;
        nextAfterimage = Time.time + afterimageInterval;

        var ghost = new GameObject("Afterimage").AddComponent<SpriteRenderer>();
        ghost.transform.SetPositionAndRotation(art.transform.position, art.transform.rotation);
        ghost.transform.localScale = art.transform.lossyScale;
        ghost.sprite = art.sprite;
        ghost.flipY = art.flipY;
        ghost.sortingOrder = art.sortingOrder - 1;
        ghost.color = new Color(parryColor.r, parryColor.g, parryColor.b, 0.5f);
        Fader.Fade(ghost, 0.18f);
    }

    // The kunai decides what its hit does, so piercing doesn't depend on which OnCollisionEnter Unity calls first.
    void OnCollisionEnter(Collision coll)
    {
        if (spent) return;

        // The ronin's own projectiles pass through each other, and a homing shot passes everything on its way
        // to the enemy it picked (arrows, the samurai in front) except the ronin's cover, which still stops a
        // parried arrow. A parried boss slash cuts through cover too (below).
        bool cover = coll.collider.GetComponentInParent<Blocker>() != null;
        bool passes = TargetAlive() && !IsTarget(coll.collider) && !(cover && Source != KillSource.Reflected);
        if (coll.collider.GetComponentInParent<Kunai>() != null || passes)
        {
            KeepFlying(coll.collider);
            return;
        }

        IHittable target = coll.collider.GetComponentInParent<IHittable>();
        if (target != null)
        {
            if (FreezeOnHit > 0f && target is Samurai samurai) samurai.Encase(FreezeOnHit);
            target.TakeHit(Damage.Of(Source), Source);
            Impacted?.Invoke(transform.position, target);
            if (pierceLeft > 0)
            {
                pierceLeft--;
                KeepFlying(coll.collider);
                return;
            }
        }
        // The shuriken and reflected slashes cut through arrows and don't chip the ronin's own blockers.
        else if (Source == KillSource.Shuriken || Source == KillSource.Reflected)
        {
            KeepFlying(coll.collider);
            return;
        }
        // A piercing slash cuts through arrows (they break on it), but cover still stops it.
        else if (CutsArrows && coll.collider.GetComponentInParent<Arrow>() != null)
        {
            KeepFlying(coll.collider);
            return;
        }

        spent = true;
        // A small spark where it breaks on a blocker or an arrow (hits on enemies get their own).
        if (target == null) Fx.Play(Fx.Impact, transform.position, glow.color * new Color(1f, 1f, 1f, 2f), 0.55f);
        Destroy(gameObject);
    }

    void KeepFlying(Collider passedThrough)
    {
        foreach (Collider own in GetComponentsInChildren<Collider>())
        {
            Physics.IgnoreCollision(own, passedThrough);
        }
        body.linearVelocity = velocity;
    }
}
