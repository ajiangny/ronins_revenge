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
        GetComponent<Rigidbody>().linearVelocity = Vector3.down * speed;
        Destroy(gameObject, lifetime);
    }

    // Whatever was hit handles its own damage; the arrow only removes itself.
    void OnCollisionEnter(Collision coll)
    {
        Destroy(gameObject);
    }
}
