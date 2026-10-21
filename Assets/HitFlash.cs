using UnityEngine;

// Flashes a sprite solid white for a moment by laying a silhouette copy over it, so the sprite's own tint
// (clan colour, frost, ward) is never touched. The copy follows the sprite's animation frames while it lasts.
public class HitFlash : MonoBehaviour
{
    SpriteRenderer source;
    SpriteRenderer overlay;
    float remaining;
    float duration;

    public static void Flash(SpriteRenderer target, float seconds = 0.09f)
    {
        if (target == null || Fx.Library == null || Fx.Library.flashMaterial == null) return;
        HitFlash flash = target.GetComponent<HitFlash>();
        if (flash == null) flash = target.gameObject.AddComponent<HitFlash>();
        flash.Begin(target, seconds);
    }

    void Begin(SpriteRenderer target, float seconds)
    {
        source = target;
        duration = remaining = seconds;
        if (overlay == null)
        {
            overlay = new GameObject("Flash").AddComponent<SpriteRenderer>();
            overlay.transform.SetParent(transform, false);
            overlay.sharedMaterial = Fx.Library.flashMaterial;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
        }
        overlay.enabled = true;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (overlay == null || !overlay.enabled) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            overlay.enabled = false;
            return;
        }
        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.color = new Color(1f, 1f, 1f, Mathf.Clamp01(remaining / duration) * 0.9f);
    }
}
