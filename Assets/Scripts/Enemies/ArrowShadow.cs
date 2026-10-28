using UnityEngine;

// The spot on the ronin's row where a falling shot will land, marked on the ground under it. He only runs left and
// right, so where things will cross his row is all he needs to read: the mark brightens and grows as the shot nears.
// It glows in the shot's own halo colour, since a dark shadow vanishes on the Emperor's black courtyard. Every enemy
// arrow carries one; shots flying up or nearly sideways cast none.
public class ArrowShadow : MonoBehaviour
{
    const float Alpha = 0.45f;
    // Shadows only show for shots landing within this many seconds, so a slow curtain doesn't clutter the floor early.
    const float Lookahead = 1.6f;
    const float MinFall = 0.5f;
    const float Width = 0.9f;


    Rigidbody body;
    Arrow arrow;
    SpriteRenderer shadow;

    public static void Attach(Arrow arrow)
    {
        var mark = arrow.gameObject.AddComponent<ArrowShadow>();
        mark.body = arrow.GetComponent<Rigidbody>();
        mark.arrow = arrow;
    }

    void Start()
    {
        // Its own object, not a child, so the arrow's turn and scale don't tilt it.
        shadow = new GameObject("ArrowShadow").AddComponent<SpriteRenderer>();
        shadow.sprite = ProceduralSprites.Shadow;
        shadow.sortingOrder = 2;
        shadow.color = Color.clear;
    }

    void LateUpdate()
    {
        if (shadow == null) return;
        Ronin ronin = Ronin.Instance;
        Vector3 velocity = body.linearVelocity;
        if (ronin == null || velocity.y > -MinFall)
        {
            shadow.color = Color.clear;
            return;
        }
        float row = ronin.Feet.y;
        Vector3 at = transform.position;
        float seconds = (at.y - row) / -velocity.y;
        if (seconds < 0f || seconds > Lookahead)
        {
            shadow.color = Color.clear;
            return;
        }
        // Recomputed every frame, so a steered or homing shot's shadow follows its new course.
        float near = 1f - seconds / Lookahead;
        shadow.transform.position = new Vector3(at.x + velocity.x * seconds, row, 0.4f);
        shadow.transform.localScale = new Vector3(Width * (0.5f + 0.5f * near), Width * 0.3f * (0.5f + 0.5f * near), 1f);
        Color tint = Color.Lerp(arrow.GlowColor, Color.white, 0.3f);
        shadow.color = new Color(tint.r, tint.g, tint.b, Alpha * near);
    }

    void OnDisable()
    {
        if (shadow != null) shadow.color = Color.clear;
    }

    void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }
}
