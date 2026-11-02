using System.Collections.Generic;
using UnityEngine;

// A wave rolling straight up the field from where it starts (Undertow Riposte, Tide Shadow, Riptide Blade's column
// wave): each samurai inside its band is shoved back once as the crest passes it, and soaked, and the breakable
// arrows in the band wash away. Built in code; it rides off the top of the view and is gone. Nothing here kills, so
// it can set off nothing else. It moves in Update, which is where SamuraiArmy.Shove must be called from.
public class RollingWave : MonoBehaviour
{
    // A row of crests is laid every this many units the wave travels.
    const float CrestStep = 1.1f;
    // A samurai is caught this far beyond the band's edge (about its own half-width), and this far below the crest,
    // so a fast wave on a slow frame can't skip one.
    const float Reach = 0.45f;
    const float Trail = 0.6f;

    SamuraiArmy army;
    float halfWidth;
    float speed;
    float push;
    float soakSeconds;
    Color water;
    float lastY;
    float nextCrest;
    // The glow ridden in place of the crests when the TideWave clip is missing.
    SpriteRenderer rider;
    readonly HashSet<Samurai> passed = new HashSet<Samurai>();
    // The army's list changes as samurai fall, so each frame works on a copy.
    readonly List<Samurai> inReach = new List<Samurai>();

    public static RollingWave Roll(SamuraiArmy army, Vector3 from, float halfWidth, float speed, float push, float soakSeconds, Color water)
    {
        var wave = new GameObject("RollingWave").AddComponent<RollingWave>();
        wave.transform.position = new Vector3(from.x, from.y, 0f);
        wave.army = army;
        wave.halfWidth = halfWidth;
        wave.speed = speed;
        wave.push = push;
        wave.soakSeconds = soakSeconds;
        wave.water = water;
        wave.lastY = from.y;
        wave.nextCrest = from.y;
        return wave;
    }

    void Update()
    {
        Vector3 at = transform.position;
        at.y += speed * Time.deltaTime;
        transform.position = at;
        Sweep(at);
        while (at.y >= nextCrest)
        {
            Crest(new Vector3(at.x, nextCrest, 0f));
            nextCrest += CrestStep;
        }
        lastY = at.y;
        Camera cam = Camera.main;
        float top = CameraShake.Center.y + (cam != null ? cam.orthographicSize : 6f);
        if (at.y > top + 1f) Destroy(gameObject);
    }

    // Everything between where the crest was last frame (less the trail) and where it is now.
    void Sweep(Vector3 at)
    {
        float low = lastY - Trail;
        if (army != null)
        {
            inReach.Clear();
            foreach (Samurai soldier in army.Soldiers)
            {
                if (soldier == null || soldier.IsDead || passed.Contains(soldier)) continue;
                Vector3 position = soldier.Body.position;
                if (Mathf.Abs(position.x - at.x) <= halfWidth + Reach && position.y > low && position.y <= at.y) inReach.Add(soldier);
            }
            foreach (Samurai soldier in inReach)
            {
                passed.Add(soldier);
                army.Shove(soldier, push);
                if (soakSeconds > 0f) soldier.Soak(soakSeconds);
            }
        }
        foreach (Arrow arrow in FindObjectsByType<Arrow>())
        {
            if (arrow.Unbreakable || arrow.Reflectable) continue;
            Vector3 position = arrow.transform.position;
            if (Mathf.Abs(position.x - at.x) <= halfWidth && position.y > low && position.y <= at.y) arrow.Wash(water);
        }
    }

    void Crest(Vector3 centre)
    {
        if (rider != null || TideFx.Band(centre, halfWidth)) return;
        rider = new GameObject("WaveRider").AddComponent<SpriteRenderer>();
        rider.sprite = ProceduralSprites.SoftGlow;
        rider.color = new Color(water.r, water.g, water.b, 0.5f);
        rider.sortingOrder = Fx.UnderArrows;
        // Parented, so it rides up with the wave and goes with it.
        rider.transform.SetParent(transform, false);
        rider.transform.localPosition = Vector3.forward * 0.1f;
        Vector2 size = ProceduralSprites.SoftGlow.bounds.size;
        rider.transform.localScale = new Vector3(2f * halfWidth / size.x, 1.2f / size.y, 1f);
    }
}
