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
    [Tooltip("How long the Taisho's spear is drawn, in world units.")]
    [SerializeField] float spearLength = 1.4f;

    bool deflected;
    SpriteRenderer art;
    SpriteRenderer glow;
    Rigidbody body;
    Vector3 velocity;
    Quaternion heading;

    // A parry sends a reflectable shot back at ReflectTarget (a boss) as a KillSource.Reflected hit that staggers her.
    public bool Reflectable => ReflectTarget != null;
    public Transform ReflectTarget { get; private set; }
    public Sprite Sprite => art.sprite;
    public SpriteRenderer Art => art;
    public Color Color => art.color;
    // A Taisho's spear: kunai and arrows break on it and it cuts through cover, so it can only be dodged or parried.
    public bool Unbreakable { get; private set; }
    // Drifts through the courtyard blockers without wearing them down (Utsusemi's lace).
    public bool PassesCover { get; set; }
    // What a parry sent back in its place (the first, for a spread), so its thrower can tell when that's gone.
    public Kunai Riposte { get; set; }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
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
        // The prefab's hitbox is a capsule; restyled shots use it as a ball of the given radius.
        Collider hitbox = GetComponentInChildren<Collider>();
        if (hitbox == null) return;
        float local = hitRadius / Mathf.Max(hitbox.transform.lossyScale.x, hitbox.transform.lossyScale.y);
        switch (hitbox)
        {
            case SphereCollider sphere:
                sphere.radius = local;
                break;
            case CapsuleCollider capsule:
                capsule.radius = local;
                capsule.height = local * 2f;
                break;
        }
    }

    // An animated restyle: the shot loops through frames as it flies.
    public void Restyle(Sprite[] frames, float fps, Color color, Vector2 artSize, float hitRadius, Color glowTint, float glowSize)
    {
        Restyle(frames[0], color, artSize, hitRadius, glowTint, glowSize);
        if (frames.Length < 2) return;
        SpriteAnimator animator = art.GetComponent<SpriteAnimator>();
        if (animator == null) animator = art.gameObject.AddComponent<SpriteAnimator>();
        animator.Play(frames, true, null, fps);
    }

    public void MakeReflectable(Transform target)
    {
        ReflectTarget = target;
    }

    // Unbreakable in its own look, for a boss shot that draws itself (Utsusemi's hour cleave).
    public void MakeUnbreakable()
    {
        Unbreakable = true;
    }

    // Swaps in the Taisho's yari and gives it a pulsing crimson halo so it reads as a different threat from plain arrows.
    // Without spear art, a darkened, bigger arrow stands in.
    public void MakeUnbreakable(Sprite spear)
    {
        Unbreakable = true;
        if (spear != null)
        {
            art.sprite = spear;
            art.color = Color.white;
            art.transform.localScale = Vector3.one * (spearLength / spear.bounds.size.y);
            // Stretch the hitbox along the shaft so the blade hits where it's drawn.
            if (GetComponentInChildren<Collider>() is CapsuleCollider capsule)
            {
                capsule.height = spearLength * 0.8f / capsule.transform.lossyScale.y;
            }
            // The halo sits on the blade, which leads the way down.
            glow.transform.localPosition += Vector3.down * (spearLength * 0.25f);
        }
        else
        {
            art.color = new Color(0.55f, 0.35f, 0.75f);
            art.transform.localScale *= 1.35f;
        }
        glow.color = new Color(1f, 0.1f, 0.2f, 0.6f);
        glow.transform.localScale *= 1.4f;
    }

    void Update()
    {
        if (!Unbreakable) return;
        float pulse = 0.45f + 0.25f * Mathf.Sin(Time.time * 14f);
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, pulse);
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
        transform.rotation = heading = Quaternion.FromToRotation(Vector3.down, velocity);
        body.linearVelocity = this.velocity = velocity;
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

    // Changes course mid-flight, for shots a boss flies along a path (a thrown hat, a swinging weight).
    public void Steer(Vector3 newVelocity)
    {
        body.linearVelocity = velocity = newVelocity;
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
    // An unbreakable spear only stops on the ronin and shrugs off everything else. A reflectable slash (the Crimson
    // Iaido) can only be answered by a parry: it cuts through the ronin's attacks and cover, and only the ronin or
    // another Iaido slash parried back at the boss stops it.
    void OnCollisionEnter(Collision coll)
    {
        if (Reflectable)
        {
            Kunai riposte = coll.collider.GetComponentInParent<Kunai>();
            if (riposte != null && riposte.Source == KillSource.Reflected)
            {
                Shatter();
                return;
            }
            if (coll.collider.GetComponentInParent<Ronin>() == null)
            {
                KeepFlying(coll.collider);
                return;
            }
        }
        else if (PassesCover && coll.collider.GetComponentInParent<Blocker>() != null)
        {
            KeepFlying(coll.collider);
            return;
        }
        else if (Unbreakable && coll.collider.GetComponentInParent<Ronin>() == null)
        {
            KeepFlying(coll.collider);
            return;
        }
        Destroy(gameObject);
    }

    // Wildfire: a fire blast burns the arrow away.
    public void Burn(Color fire)
    {
        if (deflected) return;
        deflected = true;
        Fx.Play(Fx.Impact, transform.position, fire, 0.9f);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    // A parried Iaido meeting the next one breaks it in a burst of crimson.
    void Shatter()
    {
        Fx.Play(Fx.Impact, transform.position, new Color(1f, 0.3f, 0.35f), 1.6f);
        Fx.Play(Fx.SlashArc, transform.position, new Color(1f, 0.25f, 0.3f), 1.5f);
        Sfx.Play(Sfx.WardShatter, 0.8f, 0.8f);
        CameraShake.Add(0.2f);
        Destroy(gameObject);
    }

    // Undo the knock from the collision and never touch that collider again.
    void KeepFlying(Collider passedThrough)
    {
        foreach (Collider own in GetComponentsInChildren<Collider>())
        {
            Physics.IgnoreCollision(own, passedThrough);
        }
        // Lace slipping through cover makes no spark; a blade or spear cutting through does.
        if (!PassesCover) Fx.Play(Fx.Impact, transform.position, new Color(1f, 0.4f, 0.45f), 0.5f);
        body.linearVelocity = velocity;
        body.angularVelocity = Vector3.zero;
        transform.rotation = heading;
    }
}
