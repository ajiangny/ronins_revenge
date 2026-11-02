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
    // Red-Hot Iron: what a falling piece burns, how close, and how often it looks. Many may fall at once, so it checks
    // every third frame and with no garbage.
    const int MaxBurns = 2;
    const float BurnReach = 1f;
    const int BurnEveryFrames = 3;
    static readonly Collider[] Nearby = new Collider[16];

    // How many are in the air, so a burst can't fill the screen (BoonEffects.MaxScrapLive).
    public static int Live { get; private set; }

    float startX;
    float startY;
    float targetX;
    float landY;
    float fallen;
    Action<float> landed;
    bool hot;
    int burnsLeft;
    Color fire;

    public static Scrap Drop(Vector3 from, float toX, float toY, Transform parent, Action<float> onLanded, bool redHot = false, Color ember = default)
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
        art.color = redHot ? Color.Lerp(Statuses.CrackColor, ember, 0.75f) : Statuses.CrackColor;
        scrap.hot = redHot;
        scrap.burnsLeft = MaxBurns;
        scrap.fire = ember;
        art.sortingOrder = SortingOrder;
        scrap.transform.localScale = Vector3.one * (DrawSize / art.sprite.bounds.size.y);
        Color seam = redHot ? ember : Statuses.SeamColor;
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
        if (hot && burnsLeft > 0 && Time.frameCount % BurnEveryFrames == 0) BurnArrows();
        if (t < 1f) return;
        Action<float> land = landed;
        landed = null;
        land?.Invoke(targetX);
        Destroy(gameObject);
    }

    // The same arrows Storm Step's static would break: spears and what only a parry answers fly on.
    void BurnArrows()
    {
        int found = Physics.OverlapSphereNonAlloc(transform.position, BurnReach, Nearby, BulletHell.Mask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < found && burnsLeft > 0; i++)
        {
            Arrow arrow = Nearby[i].GetComponentInParent<Arrow>();
            if (arrow == null || arrow.Unbreakable || arrow.Reflectable || arrow.Unparryable) continue;
            arrow.Burn(fire);
            burnsLeft--;
        }
    }
}
