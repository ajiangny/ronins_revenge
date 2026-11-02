using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The bullet-hell kit the bosses build their curtains from. The ronin only runs left and right along one row, so a
// pattern is fair when, wherever it crosses that row, it leaves a gap he can reach: at least MinGap wide, and never
// further from the last one than MaxDodgeSpeed can carry him before it lands. Bullets sit on their own physics
// layer, which ignores itself and the ordinary arrows, so a screen full of them doesn't knock itself apart.
public static class BulletHell
{
    // The narrowest clear stretch of his row a pattern may leave, between the edges of the shots either side.
    public const float MinGap = 2.2f;
    // How fast a gap may move along his row: 60% of his 10 u/s, so reaching it is a read, not a sprint.
    public const float MaxDodgeSpeed = 6f;
    // Shots aimed at him are never faster than this.
    public const float AimedSpeedCap = 9f;
    // Patterns wait for room rather than put more than this many bullets on the field.
    public const int Budget = 140;
    public const float ResonanceRadius = 2.5f;

    // The colour language every fight keeps: crimson can't be parried, bronze is the parry that opens a way.
    public static readonly Color Bronze = new Color(0.88f, 0.62f, 0.32f);
    public static readonly Color Crimson = new Color(0.95f, 0.15f, 0.12f);

    public delegate Arrow Shooter(Vector3 from, Vector3 velocity);

    static readonly List<Arrow> live = new List<Arrow>();
    static int layer = -2;

    // Bullets still flying on screen (a parried or popped one leaves physics at once, so it no longer counts). Ones
    // that have left the view, like the upper half of a ring, are culled here rather than flying out their lifetime
    // and holding the budget.
    public static int Live
    {
        get
        {
            Camera cam = Camera.main;
            Vector3 centre = CameraShake.Center;
            float halfHeight = cam != null ? cam.orthographicSize + 2f : 100f;
            float halfWidth = cam != null ? cam.orthographicSize * cam.aspect + 2f : 100f;
            live.RemoveAll(a =>
            {
                if (a == null || !a.isActiveAndEnabled) return true;
                Vector3 at = a.transform.position;
                if (Mathf.Abs(at.x - centre.x) <= halfWidth && Mathf.Abs(at.y - centre.y) <= halfHeight) return false;
                UnityEngine.Object.Destroy(a.gameObject);
                return true;
            });
            return live.Count;
        }
    }

    // The BossBullet layer, set up the first time it's needed: it collides with whatever arrows do, except arrows
    // and other bullets. Without the layer, bullets fall back to passing each other as shots.
    static int Layer
    {
        get
        {
            if (layer != -2) return layer;
            layer = LayerMask.NameToLayer("BossBullet");
            int arrows = LayerMask.NameToLayer("Arrow");
            if (layer < 0 || arrows < 0) return layer = -1;
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, Physics.GetIgnoreLayerCollision(arrows, i));
            Physics.IgnoreLayerCollision(layer, layer, true);
            Physics.IgnoreLayerCollision(layer, arrows, true);
            return layer;
        }
    }

    static int Mask => (Layer >= 0 ? 1 << Layer : 0) | LayerMask.GetMask("Arrow");

    // Makes a boss's shot one of the curtain: on the bullet layer, counted against the budget, and capped when parried.
    public static Arrow Enlist(Arrow shot, bool capped = true)
    {
        if (shot == null) return null;
        if (Layer >= 0)
        {
            foreach (Transform part in shot.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = Layer;
        }
        else shot.PassesShots = true;
        shot.CappedRiposte = capped;
        live.Add(shot);
        return shot;
    }

    // Holds a pattern until there's room on the field for `count` more bullets, but never for more than a couple of
    // seconds, so a crowded field can't stall a boss's fight loop.
    public static IEnumerator Room(int count)
    {
        for (float t = 0; t < 2f && Live + count > Budget; t += Time.deltaTime) yield return null;
    }

    // `count` shots spread evenly round a circle, the first `turn` degrees round from straight right.
    public static List<Arrow> Ring(Shooter shoot, Vector3 at, int count, float speed, float turn = 0f)
    {
        var ring = new List<Arrow>(count);
        for (int i = 0; i < count; i++) ring.Add(shoot(at, Quaternion.Euler(0, 0, turn + 360f * i / count) * Vector3.right * speed));
        return ring;
    }

    // A fan aimed at a point; an offset fan fills the gaps the last one left.
    public static List<Arrow> Fan(Shooter shoot, Vector3 from, Vector3 target, int count, float spread, float speed, bool offset = false)
    {
        var fan = new List<Arrow>(count);
        Vector3 aim = (target - from).normalized;
        float step = count > 1 ? 2f * spread / (count - 1f) : 0f;
        speed = Mathf.Min(speed, AimedSpeedCap);
        for (int i = 0; i < count; i++)
        {
            float angle = (count > 1 ? Mathf.Lerp(-spread, spread, i / (count - 1f)) : 0f) + (offset ? step / 2f : 0f);
            fan.Add(shoot(from, Quaternion.Euler(0, 0, angle) * aim * speed));
        }
        return fan;
    }

    // Arms wound out from a turning point, so where they cross his row the landings step sideways. Only the arms
    // pointing down are fired. The time between shots is worked out from the height above his row so that
    // consecutive landings of one arm sit far enough apart for him to stand between: the gap holds by construction.
    public static IEnumerator Spiral(Shooter shoot, Func<Vector3> from, float rowY, int arms, float spin, float seconds,
        float speed, float bulletRadius, Func<float, IEnumerator> wait, float turn = 0f)
    {
        float height = Mathf.Max(1f, from().y - rowY);
        float spacing = MinGap + 2f * bulletRadius;
        float interval = Mathf.Max(0.08f, spacing / (height * Mathf.Abs(spin) * Mathf.Deg2Rad));
        for (float t = 0; t < seconds; t += interval)
        {
            for (int a = 0; a < arms; a++)
            {
                float angle = Mathf.Repeat(turn + spin * t + 360f * a / arms + 180f, 360f) - 180f;
                if (Mathf.Abs(angle) > 80f) continue;
                shoot(from(), Quaternion.Euler(0, 0, angle) * Vector3.down * speed);
            }
            yield return wait(interval);
        }
    }

    // A row of shots falling straight down from height y between left and right, with a clear stretch round each
    // gap. Spacing is centre to centre; it has to be tight enough that he can't slip between two shots.
    public static List<Arrow> Curtain(Shooter shoot, float y, float left, float right, float spacing, IList<float> gaps,
        float gapWidth, float speed, Func<int, Shooter> shooterFor = null)
    {
        if (gapWidth < MinGap) Debug.LogWarning($"BulletHell: a curtain gap of {gapWidth:0.0} is under MinGap ({MinGap}).");
        var row = new List<Arrow>();
        int i = 0;
        for (float x = left; x <= right + 0.01f; x += spacing, i++)
        {
            bool open = false;
            if (gaps != null)
            {
                foreach (float gap in gaps) if (Mathf.Abs(x - gap) < gapWidth / 2f + spacing / 2f) open = true;
            }
            if (open) continue;
            Shooter fire = shooterFor != null ? shooterFor(i) ?? shoot : shoot;
            row.Add(fire(new Vector3(x, y, 0), Vector3.down * speed));
        }
        return row;
    }

    // The next gap of a staircase: towards `want`, but no further from `from` than he can run in `seconds` at
    // MaxDodgeSpeed, and inside the field.
    public static float NextGap(float from, float want, float seconds, float left, float right)
    {
        float reach = MaxDodgeSpeed * seconds;
        return Mathf.Clamp(Mathf.Clamp(want, from - reach, from + reach), left, right);
    }

    // Logs a pattern that broke the rules, for tuning: a gap too narrow, or too far for him to reach in time.
    public static bool GapOk(float from, float to, float seconds, float width, string pattern)
    {
        bool ok = width >= MinGap && Mathf.Abs(to - from) <= MaxDodgeSpeed * seconds + 0.01f;
        if (!ok && Debug.isDebugBuild) Debug.LogWarning($"BulletHell: {pattern} gap {from:0.0} → {to:0.0} in {seconds:0.00}s, width {width:0.0}.");
        return ok;
    }

    // Shots stop where they are, hang, then turn on him in `volleys` waves (every volley-th shot together). Aimed
    // shots meet where he stood, so one step clears a volley; the next volley punishes stepping back.
    public static IEnumerator HaltAndAim(List<Arrow> shots, Func<Vector3> target, float drift, float hold, int volleys,
        float between, float speed, Func<float, IEnumerator> wait)
    {
        yield return wait(drift);
        foreach (Arrow shot in shots) if (shot != null) shot.Steer(Vector3.zero);
        yield return wait(hold);
        speed = Mathf.Min(speed, AimedSpeedCap);
        for (int v = 0; v < volleys; v++)
        {
            Vector3 at = target();
            for (int i = v; i < shots.Count; i += volleys)
            {
                if (shots[i] == null) continue;
                shots[i].Turn((at - shots[i].transform.position).normalized * speed);
            }
            if (v < volleys - 1) yield return wait(between);
        }
    }

    // A parried bronze bell rings out: every breakable shot near it bursts, opening a way through a wall that had none.
    public static void Resonance(Vector3 at, float radius = ResonanceRadius)
    {
        foreach (Collider hit in Physics.OverlapSphere(at, radius, Mask, QueryTriggerInteraction.Collide))
        {
            Arrow shot = hit.GetComponentInParent<Arrow>();
            if (shot == null || shot.Unbreakable || shot.Reflectable || shot.Unparryable) continue;
            shot.Pop(Color.Lerp(Bronze, Color.white, 0.4f));
        }
        ElementFx.Glow(at, new Color(Bronze.r, Bronze.g, Bronze.b, 0.7f), 0.6f, radius * 2.4f, 0.35f);
        if (Sfx.Has(Sfx.BellToll)) Sfx.Play(Sfx.BellToll, 0.7f, 1.5f);
        else Sfx.Play(Sfx.MidnightChime, 0.6f, 1.2f);
        CameraShake.Add(0.15f);
    }

    // Dresses a shot as a bronze bell: heavy-looking, parryable, and ringing Resonance where it's parried.
    public static Arrow MakeBell(Arrow shot, float size = 1f)
    {
        if (shot == null) return null;
        Sprite[] frames = Fx.Frames(Fx.KageBellShot);
        if (frames != null) shot.Restyle(frames, 10f, Color.white, new Vector2(0.9f, 0.9f) * size, 0.34f * size, new Color(Bronze.r, Bronze.g, Bronze.b, 0.55f), 1.6f * size);
        else shot.Restyle(ProceduralSprites.Disc, Bronze, new Vector2(0.7f, 0.75f) * size, 0.34f * size, new Color(1f, 0.8f, 0.45f, 0.6f), 1.6f * size);
        return MakeResonant(shot);
    }

    // Any shot can ring: parried, it sends Resonance out from where it was turned. Always sent back, never capped.
    public static Arrow MakeResonant(Arrow shot)
    {
        if (shot == null) return null;
        Vector3 last = shot.transform.position;
        shot.CappedRiposte = false;
        shot.Resonant = true;
        shot.Parried = riposte => Resonance(riposte != null ? riposte.transform.position : last);
        return shot;
    }

    // Dresses a shot as a crimson seal-dart: the parry fizzles on it, and cover doesn't stop it.
    public static Arrow MakeSealDart(Arrow shot, Color tint)
    {
        if (shot == null) return null;
        Sprite[] frames = Fx.Frames(Fx.KageSealDart);
        if (frames != null) shot.Restyle(frames, 10f, tint, new Vector2(0.45f, 1.1f), 0.22f, new Color(tint.r, tint.g, tint.b, 0.5f), 1.1f);
        else shot.Restyle(ProceduralSprites.White, tint, new Vector2(0.16f, 0.9f), 0.22f, new Color(tint.r, tint.g, tint.b, 0.55f), 1.1f);
        shot.Unparryable = true;
        shot.PassesCover = true;
        return shot;
    }
}
