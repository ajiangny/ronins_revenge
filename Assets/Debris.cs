using UnityEngine;

// A small cube that flies outward, spins and shrinks away. Used for collision explosions.
public class Debris : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock colorBlock;

    Vector3 velocity;
    Vector3 spin;
    Vector3 startScale;
    float lifetime;
    float age;

    public static void Burst(Vector3 position, Material material, Color color, int count, float size, float speed)
    {
        colorBlock ??= new MaterialPropertyBlock();
        colorBlock.SetColor(BaseColorId, color);

        for (int i = 0; i < count; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // Remove the collider immediately so a fragment can never block a shot this frame.
            DestroyImmediate(piece.GetComponent<Collider>());
            piece.name = "Debris";
            piece.transform.SetPositionAndRotation(position, Random.rotation);
            piece.transform.localScale = Vector3.one * (size * Random.Range(0.5f, 1.2f));

            Renderer r = piece.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.SetPropertyBlock(colorBlock);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Debris debris = piece.AddComponent<Debris>();
            Vector3 direction = Random.insideUnitSphere;
            direction.z *= 0.3f;
            debris.velocity = direction.normalized * (speed * Random.Range(0.4f, 1f));
            debris.spin = Random.insideUnitSphere * 720f;
            debris.startScale = piece.transform.localScale;
            debris.lifetime = Random.Range(0.35f, 0.7f);
        }
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
