using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AlienBullet : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] float lifetime = 4f;

    public void Launch(float speed)
    {
        GetComponent<Rigidbody>().linearVelocity = Vector3.down * speed;
        Destroy(gameObject, lifetime);
    }

    // Whatever was hit handles its own damage; the bullet only removes itself.
    void OnCollisionEnter(Collision coll)
    {
        Destroy(gameObject);
    }
}
