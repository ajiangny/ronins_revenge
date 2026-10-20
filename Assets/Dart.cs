using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Dart : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] float speed = 18f;
    [SerializeField] float lifetime = 2f;

    bool spent;

    void Start()
    {
        GetComponent<Rigidbody>().linearVelocity = Vector3.up * speed;
        Destroy(gameObject, lifetime);
    }

    // A dart can only destroy one alien, even if it touches two in the same physics step.
    public bool TryConsume()
    {
        if (spent) return false;
        spent = true;
        return true;
    }

    void OnCollisionEnter(Collision coll)
    {
        Destroy(gameObject);
    }
}
