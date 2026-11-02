using UnityEngine;

// Cracked's bright seams: a copy of the sprite on top of it that draws only its cracks (Ninja/SpriteSeams), so the status
// stays on the body as a tint does and covers nothing. The body's own tint and material are never touched, and the copy
// follows its animation frames. Without the material the tint alone shows it.
public class CrackSeams : MonoBehaviour
{
    static readonly int SeamColor = Shader.PropertyToID("_FlashColor");
    // How fast the seams breathe, and the white heat they reach at the top of each breath.
    const float BeatSpeed = 5f;
    const float BeatFloor = 0.65f;
    static readonly Color WhiteHot = new Color(1f, 0.97f, 0.85f);

    SpriteRenderer source;
    SpriteRenderer overlay;
    MaterialPropertyBlock block;
    float until;
    float phase;

    public static bool Available => Fx.Library != null && Fx.Library.seamMaterial != null;

    // Shows the seams until the time given; a longer crack just extends them.
    public static void Hold(SpriteRenderer target, float untilTime)
    {
        if (target == null || !Available) return;
        CrackSeams seams = target.GetComponent<CrackSeams>();
        if (seams == null) seams = target.gameObject.AddComponent<CrackSeams>();
        seams.Begin(target, untilTime);
    }

    // The crack is spent, or what wore it has fallen.
    public static void Clear(SpriteRenderer target)
    {
        if (target != null && target.TryGetComponent(out CrackSeams seams)) seams.until = 0f;
    }

    void Begin(SpriteRenderer target, float untilTime)
    {
        source = target;
        until = untilTime;
        if (overlay == null)
        {
            overlay = new GameObject("Seams").AddComponent<SpriteRenderer>();
            overlay.transform.SetParent(transform, false);
            overlay.sharedMaterial = Fx.Library.seamMaterial;
            overlay.sortingLayerID = source.sortingLayerID;
            block = new MaterialPropertyBlock();
            phase = transform.position.x;
        }
        overlay.enabled = true;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (overlay == null || !overlay.enabled) return;
        if (Time.time >= until)
        {
            overlay.enabled = false;
            return;
        }
        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        // Above whatever the body sits at now: a boss's flash or a phase can move it.
        overlay.sortingOrder = source.sortingOrder + 1;
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * BeatSpeed + phase);
        Color seam = Color.Lerp(Statuses.SeamColor, WhiteHot, beat * beat);
        seam.a = source.color.a * Mathf.Lerp(BeatFloor, 1f, beat);
        overlay.GetPropertyBlock(block);
        block.SetColor(SeamColor, seam);
        overlay.SetPropertyBlock(block);
    }
}
