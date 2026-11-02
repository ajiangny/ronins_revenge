using System;
using UnityEngine;

// One piece of Steel's scrap: an iron disc with a bright seam, built in code, falling from where it was shed to the top
// of the cover while drawing sideways to a target. Falling straight it would land in the gap he shoots from, so it's
// drawn. It's parented to the wall, so a Rebuild clears whatever is still in the air.
public class Scrap : MonoBehaviour
{
    const float FallSpeed = 9f;
    const float DrawSize = 0.32f;
    const float SeamSize = 0.6f;
    const int SortingOrder = 30;

    // How many are in the air, so a burst can't fill the screen (BoonEffects.MaxScrapLive).
    public static int Live { get; private set; }

    float startX;
    float startY;
    float targetX;
    float landY;
    float fallen;
    Action<float> landed;

    public static Scrap Drop(Vector3 from, float toX, float toY, Transform parent, Action<float> onLanded)
    {
        var scrap = new GameObject("Scrap").AddComponent<Scrap>();
        scrap.transform.SetParent(parent);
        scrap.transform.position = new Vector3(from.x, from.y, 0f);
        scrap.startX = from.x;
        scrap.startY = from.y;
        scrap.targetX = toX;
        scrap.landY = toY;
        scrap.landed = onLanded;

        var art = scrap.gameObject.AddComponent<SpriteRenderer>();
        art.sprite = ProceduralSprites.Disc;
        art.color = Statuses.CrackColor;
        art.sortingOrder = SortingOrder;
        scrap.transform.localScale = Vector3.one * (DrawSize / art.sprite.bounds.size.y);
        Color seam = Statuses.SeamColor;
        // The glow's size is in the disc's scaled space, so it's measured against the disc.
        Projectile.CreateGlow(scrap.transform, SeamSize / DrawSize, SortingOrder - 1, new Color(seam.r, seam.g, seam.b, 0.6f));
        return scrap;
    }

    void Awake() => Live++;
    void OnDestroy() => Live--;

    void Update()
    {
        fallen += FallSpeed * Time.deltaTime;
        float drop = startY - landY;
        // Shed from below the cover's top (a diver at the wall): it has no way to fall, so it lands at once.
        float t = drop > 0f ? Mathf.Clamp01(fallen / drop) : 1f;
        transform.position = new Vector3(Mathf.Lerp(startX, targetX, t), Mathf.Lerp(startY, landY, t), 0f);
        if (t < 1f) return;
        Action<float> land = landed;
        landed = null;
        land?.Invoke(targetX);
        Destroy(gameObject);
    }
}
