using UnityEngine;

// A round's reward made visible: it gathers out of the air, hangs there a moment so the player sees what they've
// earned, then is pulled into the ronin and gone. Built in code; without art
// it is a disc in the reward's colour. The caller waits on it by checking for null.
public class RewardSpirit : MonoBehaviour
{
    const float MaterializeTime = 0.7f;
    const float HoverTime = 0.45f;
    const float StartSpeed = 1.5f;
    const float Acceleration = 34f;
    const float ArriveRadius = 0.3f;
    const float GlowSize = 2.4f;
    // Gives up the chase if he's gone, so the round never hangs on it.
    const float MaxFlightTime = 2.5f;

    Ronin ronin;
    SpriteRenderer glow;
    SpriteRenderer icon;
    Sprite[] frames;
    Color colour;
    float iconScale;
    float age;
    float hoverUntil;
    float speed = StartSpeed;

    // frames may be null: a disc in `colour` stands in. `size` is its width in world units.
    public static RewardSpirit Spawn(Vector3 position, Sprite[] frames, Color colour, Ronin ronin, float size = 1.6f)
    {
        var go = new GameObject("RewardSpirit");
        go.transform.position = new Vector3(position.x, position.y, -0.6f);
        var spirit = go.AddComponent<RewardSpirit>();
        spirit.ronin = ronin;
        spirit.colour = colour;
        spirit.frames = frames != null && frames.Length > 0 && frames[0] != null ? frames : null;
        spirit.hoverUntil = MaterializeTime + HoverTime;

        spirit.glow = Layer(go.transform, "Glow", ProceduralSprites.Glow, 44);
        spirit.icon = Layer(go.transform, "Icon", spirit.frames != null ? spirit.frames[0] : ProceduralSprites.Disc, 45);
        Sprite first = spirit.icon.sprite;
        spirit.iconScale = (spirit.frames != null ? size : size * 0.4f) / Mathf.Max(first.bounds.size.x, first.bounds.size.y);
        if (spirit.frames == null) spirit.icon.color = Color.Lerp(colour, Color.white, 0.4f);

        // A burst in the reward's colour where it forms.
        Fx.Play(Fx.Impact, go.transform.position, colour, 2.2f);
        Sfx.Play(Sfx.SoulSpawn, 1f, 0.85f);
        spirit.Animate();
        return spirit;
    }

    static SpriteRenderer Layer(Transform parent, string name, Sprite sprite, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.sprite = sprite;
        layer.sortingOrder = order;
        return layer;
    }

    void Update()
    {
        age += Time.deltaTime;
        Animate();
        if (age < hoverUntil) return;

        bool roninHere = ronin != null && ronin.isActiveAndEnabled;
        if (!roninHere || age > hoverUntil + MaxFlightTime)
        {
            Destroy(gameObject);
            return;
        }
        // Speeds up as it goes, like a pickup gathered at the end of a round.
        speed += Acceleration * Time.deltaTime;
        Vector3 target = ronin.Bounds.center;
        target.z = transform.position.z;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude > ArriveRadius * ArriveRadius) return;

        Fx.Play(Fx.Impact, target, colour, 1.8f);
        Destroy(gameObject);
    }

    void Animate()
    {
        float grow = GameUI.EaseOut(Mathf.Clamp01(age / MaterializeTime));
        bool flying = age >= hoverUntil;
        // Shrinks a little as it's drawn in, so it reads as going into him rather than hitting him.
        float shrink = flying ? Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((age - hoverUntil) / 0.4f)) : 1f;
        float pulse = 1f + 0.08f * Mathf.Sin(age * 6f);

        if (frames != null) icon.sprite = frames[(int)(age * 8f) % frames.Length];
        icon.transform.localScale = Vector3.one * (iconScale * grow * shrink);
        icon.transform.localPosition = flying ? Vector3.zero : Vector3.up * (0.1f * Mathf.Sin(age * 2.5f));
        Color tint = icon.color;
        icon.color = new Color(tint.r, tint.g, tint.b, grow);

        // A bright flash as it forms that settles to a steady glow.
        float flash = 1f + 0.8f * Mathf.Clamp01(1f - Mathf.Abs(age - MaterializeTime * 0.8f) / 0.25f);
        glow.transform.localScale = Vector3.one * (GlowSize * grow * shrink * pulse * flash / ProceduralSprites.Glow.bounds.size.x);
        glow.color = new Color(colour.r, colour.g, colour.b, 0.55f * grow);
    }
}
