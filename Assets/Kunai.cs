using UnityEngine;

// What killed a samurai. Boon effects only trigger on the ronin's own attacks, never on other effects' kills,
// so explosions and lightning can't chain into each other forever.
public enum KillSource { Kunai, Shuriken, Deflected, Effect }

// Every projectile the ronin sends upward: thrown kunai, the Spirit Shuriken and parried arrows.
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

    public KillSource Source { get; private set; } = KillSource.Kunai;

    Rigidbody body;
    SpriteRenderer art;
    SpriteRenderer glow;
    Vector3 velocity;
    int pierceLeft;
    bool spent;
    bool launched;

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

    // Kunai thrown together (Inferno Fan) start on the same spot; they must not knock each other out.
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

    void Update()
    {
        if (spinner != null)
        {
            spinner.Rotate(0, 0, -spinSpeed * Time.deltaTime);
        }
    }

    // The kunai decides what its hit does, so piercing doesn't depend on which OnCollisionEnter Unity calls first.
    void OnCollisionEnter(Collision coll)
    {
        if (spent) return;

        // The ronin's own projectiles pass through each other.
        if (coll.collider.GetComponentInParent<Kunai>() != null)
        {
            KeepFlying(coll.collider);
            return;
        }

        IHittable target = coll.collider.GetComponentInParent<IHittable>();
        if (target != null)
        {
            target.TakeHit(Damage.Of(Source), Source);
            if (pierceLeft > 0)
            {
                pierceLeft--;
                KeepFlying(coll.collider);
                return;
            }
        }
        // The shuriken cuts through arrows and doesn't chip the ronin's own blockers.
        else if (Source == KillSource.Shuriken)
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
