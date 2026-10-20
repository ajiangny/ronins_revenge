using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Dart : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] float speed = 18f;
    [SerializeField] float lifetime = 2f;
    [Tooltip("Visual child that spins; the collider stays on the root so it doesn't turn with it.")]
    [SerializeField] Transform spinner;
    [SerializeField] float spinSpeed = 1080f;

    bool spent;

    void Update()
    {
        if (spinner != null)
        {
            spinner.Rotate(0, 0, -spinSpeed * Time.deltaTime);
        }
    }

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
