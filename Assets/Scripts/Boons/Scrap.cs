using System;
using UnityEngine;

// One piece of Steel's scrap: a tumbling chunk of iron with a bright seam and a hard hot streak behind it, falling from
// where it was shed to the top of the cover while drawing sideways to a target. Falling straight it would land in the gap
// he shoots from, so it's drawn. No halo and no bob, which are a pickup's, and chunky where an arrow is thin. Under the
// arrows in the draw order. It's parented to the wall, so a Rebuild clears whatever is still in the air.
public class Scrap : MonoBehaviour
{
    const float FallSpeed = 9f;
    const float DrawSize = 0.32f;
    const float SeamSize = 0.6f;
    const int SortingOrder = Fx.UnderArrows;
    // The hot streak behind it, whole pixels wide on the 48-to-a-unit grid.
    const float StreakWidth = 2f / 48f;
    const float StreakLength = 0.25f;
    // Behind the seam-bright part, the same length again of metal cooling to orange.
    static readonly Color Cooling = new Color32(0xF5, 0x9A, 0x2E, 255);
    // A piece worth more than a point (Quenching's) is drawn this much bigger.
    const float HeavyScale = 1.5f;
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
    Transform streak;
    Transform tail;

    public static Scrap Drop(Vector3 from, float toX, float toY, Transform parent, Action<float> onLanded, bool redHot = false, Color ember = default, bool heavy = false)
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
        art.sortingOrder = SortingOrder;
        scrap.hot = redHot;
        scrap.burnsLeft = MaxBurns;
        scrap.fire = ember;
        Color seam = redHot ? ember : Statuses.SeamColor;
        Sprite[] frames = Fx.Frames(redHot ? Fx.SteelScrapHot : Fx.SteelScrap);
        if (frames != null && frames.Length > 0)
        {
            // Native size, so it stays on the pixel grid; a heavy piece gives that up to be told apart.
            art.sprite = frames[UnityEngine.Random.Range(0, frames.Length)];
            scrap.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 12f);
            if (heavy) scrap.transform.localScale = Vector3.one * HeavyScale;
            scrap.AddStreak(seam);
        }
        else
        {
            // No art: the iron disc with its seam's glow behind it.
            art.sprite = ProceduralSprites.Disc;
            art.color = redHot ? Color.Lerp(Statuses.CrackColor, ember, 0.75f) : Statuses.CrackColor;
            scrap.transform.localScale = Vector3.one * (DrawSize / art.sprite.bounds.size.y);
            // The glow's size is in the disc's scaled space, so it's measured against the disc.
            Projectile.CreateGlow(scrap.transform, SeamSize / DrawSize, SortingOrder - 1, new Color(seam.r, seam.g, seam.b, 0.6f));
        }
        return scrap;
    }

    // A bar of the seam's colour standing behind the piece, its pivot at the piece, turned along the fall in Update.
    void AddStreak(Color seam)
    {
        streak = StreakBar(seam);
        tail = StreakBar(hot ? fire : Cooling);
    }

    Transform StreakBar(Color color)
    {
        var bar = new GameObject("Streak").AddComponent<SpriteRenderer>();
        bar.transform.SetParent(transform, false);
        bar.sprite = ProceduralSprites.White;
        bar.color = color;
        bar.sortingOrder = SortingOrder - 1;
        bar.transform.localScale = new Vector3(StreakWidth, StreakLength, 1f);
        // Hidden until it knows which way is behind, on the first step of the fall.
        bar.enabled = false;
        return bar.transform;
    }

    void Awake() => Live++;
    void OnDestroy() => Live--;

    void Update()
    {
        fallen += FallSpeed * Time.deltaTime;
        float drop = startY - landY;
        // Shed from below the cover's top (a diver at the wall): it has no way to fall, so it lands at once.
        float t = drop > 0f ? Mathf.Clamp01(fallen / drop) : 1f;
        Vector3 was = transform.position;
        transform.position = new Vector3(Mathf.Lerp(startX, targetX, t), Mathf.Lerp(startY, landY, t), 0f);
        if (streak != null) PointStreak(transform.position - was);
        if (hot && burnsLeft > 0 && Time.frameCount % BurnEveryFrames == 0) BurnArrows();
        if (t < 1f) return;
        Action<float> land = landed;
        landed = null;
        land?.Invoke(targetX);
        Destroy(gameObject);
    }

    void PointStreak(Vector3 moved)
    {
        if (moved.sqrMagnitude < 0.000001f) return;
        Vector3 behind = -moved.normalized;
        float length = StreakLength * transform.lossyScale.y;
        Quaternion along = Quaternion.FromToRotation(Vector3.up, behind);
        streak.SetPositionAndRotation(transform.position + behind * (length * 0.5f), along);
        tail.SetPositionAndRotation(transform.position + behind * (length * 1.5f), along);
        streak.GetComponent<SpriteRenderer>().enabled = tail.GetComponent<SpriteRenderer>().enabled = true;
    }

    // The same arrows Storm Step's static would break: spears and what only a parry answers fly on.
    void BurnArrows()
    {
        int found = Physics.OverlapSphereNonAlloc(transform.position, BurnReach, Nearby, BulletHell.Mask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < found && burnsLeft > 0; i++)
        {
            Arrow arrow = Nearby[i].GetComponentInParent<Arrow>();
            if (arrow == null || arrow.Unbreakable || arrow.Reflectable || arrow.Unparryable) continue;
            // The sparks are its burst: Burn's flat puff of fire would sit over the arrows around it.
            Fx.Steel(Fx.SteelSparks, arrow.transform.position);
            arrow.Catch();
            burnsLeft--;
        }
    }
}
