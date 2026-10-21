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

    void Awake()
    {
        SpriteRenderer art = GetComponentInChildren<SpriteRenderer>();
        art.transform.localScale *= artScale;
        Kunai.CreateGlow(transform, 0.8f, art.sortingOrder - 1, glowColor);
    }

    public Sprite Sprite => GetComponentInChildren<SpriteRenderer>().sprite;

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

    // Angled shots turn the arrow to face where it flies.
    public void Launch(Vector3 velocity)
    {
        transform.rotation = Quaternion.FromToRotation(Vector3.down, velocity);
        GetComponent<Rigidbody>().linearVelocity = velocity;
        Destroy(gameObject, lifetime);
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
