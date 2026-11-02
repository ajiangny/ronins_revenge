using UnityEngine;

// A dropped blade falling on its own. It can't be a coroutine on the boss: her death stops all of those, which left
// the blades hanging in the air for the rest of the run.
public class TumblingBlade : MonoBehaviour
{
    SpriteRenderer blade;
    Vector3 velocity;
    float spin;
    float ground;

    public static void Drop(SpriteRenderer blade, float spin, float ground)
    {
        var tumble = blade.gameObject.AddComponent<TumblingBlade>();
        tumble.blade = blade;
        tumble.spin = spin;
        tumble.ground = ground;
        tumble.velocity = new Vector3(spin * 1.2f, 1.5f, 0f);
    }

    void Update()
    {
        velocity += Vector3.down * 12f * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(0, 0, spin * 400f * Time.deltaTime);
        if (transform.position.y > ground) return;
        Sfx.Play(Sfx.Deflect, 0.5f, 0.8f);
        Fader.Fade(blade, 2.5f);
        enabled = false;
    }
}
