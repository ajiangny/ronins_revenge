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

    public KillSource Source { get; private set; } = KillSource.Kunai;

    Rigidbody body;
    SpriteRenderer art;
    Vector3 velocity;
    int pierceLeft;
    bool spent;
    bool launched;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        art = GetComponentInChildren<SpriteRenderer>();
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

    public void Tint(Color color)
    {
        art.color = color;
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

        Samurai samurai = coll.collider.GetComponentInParent<Samurai>();
        if (samurai != null)
        {
            samurai.Kill(Source);
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
