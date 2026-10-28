using UnityEngine;

// A small sprite that flies outward, spins and shrinks away. Used for collision explosions.
public class Debris : MonoBehaviour
{
    Vector3 velocity;
    Vector3 spin;
    Vector3 startScale;
    float lifetime;
    float age;

    // Bursts sprite pieces (sakura petals, smoke, sparks, wood chips) out from a point. They only spin in the screen plane.
    public static void BurstSprites(Vector3 position, Sprite[] sprites, Color color, int count, float size, float speed)
    {
        if (sprites == null || sprites.Length == 0) return;

        for (int i = 0; i < count; i++)
        {
            var piece = new GameObject("Debris");
            piece.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            piece.transform.localScale = Vector3.one * (size * Random.Range(0.5f, 1.2f));

            SpriteRenderer r = piece.AddComponent<SpriteRenderer>();
            r.sprite = sprites[Random.Range(0, sprites.Length)];
            r.color = color;

            Launch(piece.AddComponent<Debris>(), speed, new Vector3(0, 0, Random.Range(-540f, 540f)));
        }
    }

    static void Launch(Debris debris, float speed, Vector3 spin)
    {
        Vector3 direction = Random.insideUnitSphere;
        direction.z *= 0.3f;
        debris.velocity = direction.normalized * (speed * Random.Range(0.4f, 1f));
        debris.spin = spin;
        debris.startScale = debris.transform.localScale;
        debris.lifetime = Random.Range(0.35f, 0.7f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += velocity * dt;
        velocity *= 1f - 3f * dt;
        transform.Rotate(spin * dt);
        transform.localScale = startScale * (1f - age / lifetime);
    }
}
