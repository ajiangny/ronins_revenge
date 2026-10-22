using UnityEngine;

// A patch of fire a laser leaves where it drags along the ground: it flares up, burns, then dies down to a glowing
// smear of embers that cools and fades. Purely for show; only the beam itself hurts.
public class GroundFire : MonoBehaviour
{
    const float Width = 1.3f;
    const float Flare = 0.12f;
    const float Burn = 0.8f;
    const float DieDown = 0.6f;
    const float Cool = 1.4f;

    static readonly Color HotEmbers = new Color(1f, 0.45f, 0.15f, 0.75f);
    static readonly Color ColdEmbers = new Color(0.3f, 0.05f, 0.03f, 0.45f);

    // The burning flame: the Utsu_Burn loop, or a flickering glow when that art is missing.
    SpriteRenderer flame;
    bool drawnFlame;
    SpriteRenderer embers;
    Vector3 flameScale;
    float size;
    float age;

    public static void Light(Vector3 at)
    {
        var fire = new GameObject("GroundFire").AddComponent<GroundFire>();
        fire.transform.position = new Vector3(at.x, at.y, 0f);
        fire.size = Random.Range(0.85f, 1.15f);

        Sprite[] frames = Fx.Frames(Fx.UtsuBurn);
        FxPlayer player = frames != null ? Fx.Play(Fx.UtsuBurn, at, Color.white, Width * fire.size / frames[0].bounds.size.x, fire.transform, 3) : null;
        if (player != null)
        {
            fire.flame = player.GetComponent<SpriteRenderer>();
            fire.drawnFlame = true;
        }
        else
        {
            fire.flame = new GameObject("Flame").AddComponent<SpriteRenderer>();
            fire.flame.transform.SetParent(fire.transform, false);
            fire.flame.transform.localPosition = new Vector3(0, 0.25f, -0.5f);
            fire.flame.transform.localScale = new Vector3(0.8f, 0.6f, 1f) * fire.size;
            fire.flame.sprite = ProceduralSprites.Glow;
            fire.flame.color = new Color(1f, 0.4f, 0.12f, 0.8f);
            fire.flame.sortingOrder = 3;
        }
        fire.flameScale = fire.flame.transform.localScale;

        fire.embers = new GameObject("Embers").AddComponent<SpriteRenderer>();
        fire.embers.transform.SetParent(fire.transform, false);
        fire.embers.transform.localPosition = new Vector3(0, 0.02f, 0.1f);
        fire.embers.transform.localScale = new Vector3(Width * 1.3f * fire.size, 0.32f, 1f);
        fire.embers.sprite = ProceduralSprites.Glow;
        fire.embers.color = HotEmbers;
        fire.embers.sortingOrder = 2;
    }

    void Update()
    {
        age += Time.deltaTime;
        float burnt = Flare + Burn;
        if (flame != null)
        {
            // Flares up tall and thin, burns with a flicker, then sinks into the embers.
            float height, alpha = 1f;
            if (age < Flare) height = Mathf.Lerp(0.2f, 1.2f, age / Flare);
            else if (age < burnt) height = Mathf.Lerp(1.2f, 1f, (age - Flare) / 0.15f);
            else
            {
                float k = Mathf.Clamp01((age - burnt) / DieDown);
                height = Mathf.Lerp(1f, 0.15f, k);
                alpha = 1f - k * k;
            }
            if (!drawnFlame) height *= 1f + 0.15f * Mathf.Sin(age * 40f + size * 17f);
            float width = age < Flare ? Mathf.Lerp(0.6f, 1f, age / Flare) : 1f;
            flame.transform.localScale = new Vector3(flameScale.x * width, flameScale.y * height, flameScale.z);
            Color c = flame.color;
            c.a = (drawnFlame ? 1f : 0.8f) * alpha;
            flame.color = c;
            if (age >= burnt + DieDown)
            {
                Destroy(flame.gameObject);
                flame = null;
            }
        }

        // The embers glow hot under the flame, then cool from orange to a dull red and fade.
        float cooling = Mathf.Clamp01((age - burnt) / (DieDown + Cool));
        Color e = Color.Lerp(HotEmbers, ColdEmbers, cooling);
        e.a *= age < burnt ? 1f : 1f - cooling * cooling;
        embers.color = e;
        if (age >= burnt + DieDown + Cool) Destroy(gameObject);
    }
}
