using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] float lifetime = 4f;
    [Tooltip("Draws the arrow bigger than its hitbox so it's easy to see coming.")]
    [SerializeField] float artScale = 2f;
    [SerializeField] Color glowColor = new Color(1f, 0.35f, 0.25f, 0.4f);

    bool deflected;
    SpriteRenderer art;
    SpriteRenderer glow;

    // A parry sends a reflectable shot back at ReflectTarget (a boss) as a heavy KillSource.Reflected hit.
    public bool Reflectable => ReflectTarget != null;
    public Transform ReflectTarget { get; private set; }
    public Sprite Sprite => art.sprite;
    public Color Color => art.color;

    void Awake()
    {
        art = GetComponentInChildren<SpriteRenderer>();
        art.transform.localScale *= artScale;
        glow = Kunai.CreateGlow(transform, 0.8f, art.sortingOrder - 1, glowColor);
    }

    // Turns an arrow into another kind of shot (a smoke ring, a slash wave). artSize is in world units;
    // hitRadius sets the hitbox in world units too.
    public void Restyle(Sprite sprite, Color color, Vector2 artSize, float hitRadius, Color glowTint, float glowSize)
    {
        art.sprite = sprite;
        art.color = color;
        Vector2 spriteSize = sprite.bounds.size;
        art.transform.localScale = new Vector3(artSize.x / spriteSize.x, artSize.y / spriteSize.y, 1f);
        glow.color = glowTint;
        glow.transform.localScale = Vector3.one * glowSize;
        SphereCollider hitbox = GetComponentInChildren<SphereCollider>();
        hitbox.radius = hitRadius / Mathf.Max(hitbox.transform.lossyScale.x, hitbox.transform.lossyScale.y);
    }

    public void MakeReflectable(Transform target)
    {
        ReflectTarget = target;
    }

    // The ronin's parry removes the arrow and sends its own copy back up. Only the first parry check counts.
    public bool TryDeflect()
    {
        if (deflected) return false;
        deflected = true;
        // Leave physics right away: the riposte spawns on this spot and would otherwise hit the arrow it replaces.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    public void Launch(float speed)
    {
        Launch(Vector3.down * speed);
    }

    // Angled shots turn the arrow to face where it flies. Slow shots pass a longer lifetime so they reach the ground.
    public void Launch(Vector3 velocity, float lifetimeMultiplier = 1f)
    {
        transform.rotation = Quaternion.FromToRotation(Vector3.down, velocity);
        GetComponent<Rigidbody>().linearVelocity = velocity;
        Destroy(gameObject, lifetime * lifetimeMultiplier);
    }

    // count arrows spread evenly over ±spread degrees around direction. Arrows collide with each other,
    // so a volley is told to ignore itself.
    public static List<Arrow> Fan(Arrow prefab, Vector3 position, Vector3 direction, int count, float spread, float speed)
    {
        var volley = new List<Arrow>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? Mathf.Lerp(-spread, spread, i / (count - 1f)) : 0f;
            Arrow arrow = Instantiate(prefab, position, Quaternion.identity);
            arrow.Launch(Quaternion.Euler(0, 0, angle) * direction.normalized * speed);
            foreach (Arrow other in volley) arrow.IgnoreCollisionsWith(other);
            volley.Add(arrow);
        }
        return volley;
    }

    public void IgnoreCollisionsWith(Arrow other)
    {
        foreach (Collider mine in GetComponentsInChildren<Collider>())
        {
            foreach (Collider theirs in other.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(mine, theirs);
            }
        }
    }

    // Whatever was hit handles its own damage; the arrow only removes itself.
    void OnCollisionEnter(Collision coll)
    {
        Destroy(gameObject);
    }
}
