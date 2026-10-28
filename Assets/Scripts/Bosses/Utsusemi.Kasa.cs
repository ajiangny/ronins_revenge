using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kasa Wheel and the Shattered Halo: the hat (or its shards, or the sword) thrown on a loop, its trail, and the eye it leaves bare.
public partial class Utsusemi
{
    // Kasa Wheel: it hurls its wide hat like a boomerang. A dotted gold trail shows the loop first: the hat curves
    // down into the ronin's lane where he stands, skims along it, then curls back up to its head. The fight carries on
    // meanwhile, with its slit-eye exposed and every hit doubled. In the second phase its sword wheels after it a beat
    // later on its own loop, aimed at wherever the ronin dodged to. A parried hat or sword flies back and circles it,
    // and only comes home once that riposte is gone.
    IEnumerator KasaWheel()
    {
        if (Random.value < 0.6f) Speak(BossLine.KasaWheel);
        PlayAction(art.parry);
        ElementFx.Glow(Head, Gold, 1f, 3f, 0.4f, transform);
        Sfx.Play(Sfx.ParrySwing, 0.8f, 0.6f);
        if (secondPhase)
        {
            yield return ShatteredHalo();
            yield break;
        }
        HatPath path = AimHat();
        List<SpriteRenderer> trail = TrailDots(path, Gold);
        // The hat leaves its hand on the throw clip's last frame.
        yield return Wait(0.55f);

        StartCoroutine(FlyHat(path, trail, false));
        ShowEye(true);
        Sfx.Play(Sfx.HatWhirr);
    }

    // The second phase's Kasa Wheel: its hat is a broken halo now, and the three pieces fly apart. The crown flies
    // the hat's old loop at the ronin; a beat later each side shard dives in on its own side of him and skims away
    // outward, so there's room to dodge just beside where he stood but not far along the lane. Then its sword wheels
    // after them. Each piece is parried on its own, and every hit is doubled until all of them are home.
    IEnumerator ShatteredHalo()
    {
        float roninX = ronin.Bounds.center.x;
        HatPath crown = AimHat();
        HatPath left = AimHat(roninX - HaloShardOffset, -1f, HaloShardSkim);
        HatPath right = AimHat(roninX + HaloShardOffset, 1f, HaloShardSkim);
        List<SpriteRenderer> crownTrail = TrailDots(crown, Gold);
        List<SpriteRenderer> leftTrail = TrailDots(left, Molten);
        List<SpriteRenderer> rightTrail = TrailDots(right, Molten);
        yield return Wait(0.55f);

        // The halo bursts apart off its head.
        Fx.Play(Fx.Impact, Head, Molten, 1.2f);
        ElementFx.Glow(Head, Molten, 0.6f, 2.5f, 0.3f, transform);
        Sfx.Play(Sfx.HatWhirr);
        Sfx.Play(Sfx.ParryClang, 0.4f, 0.7f);
        StartCoroutine(FlyHat(crown, crownTrail, false, HaloCrown));
        ShowEye(true);
        // The side shards follow a beat later, so the three don't land at once.
        yield return Wait(0.35f);
        StartCoroutine(FlyHat(left, leftTrail, false, HaloLeft));
        StartCoroutine(FlyHat(right, rightTrail, false, HaloRight));
        Sfx.Play(Sfx.HatWhirr, 0.7f, 1.2f);
        StartCoroutine(ThrowSwordWheel(1.1f));
    }

    // The side shards of the broken halo dive this far either side of the ronin and skim this far outward.
    const float HaloShardOffset = 2.6f;
    const float HaloShardSkim = 3.5f;
    // Which piece of the broken halo flies: its frames in Fx.UtsuHaloShards.
    const int HaloLeft = 0, HaloCrown = 1, HaloRight = 2;

    IEnumerator ThrowSwordWheel(float delay)
    {
        swordWheeling = true;
        yield return Wait(delay);
        if (IsDead) yield break;
        HatPath path = AimHat();
        StartCoroutine(FlyHat(path, TrailDots(path, SwordGlow), true));
        Sfx.Play(Sfx.SwordWhirl, 0.8f, 1.15f);
    }

    // The hat's loop: down into the lane at the ronin, along it, and back up. Each leg is a cubic curve, except the
    // straight skim along the lane.
    struct HatPath
    {
        public Vector3 a0, a1, a2, a3;
        public Vector3 skimEnd;
        public float sweepDir;
    }

    HatPath AimHat()
    {
        float roninX = ronin.Bounds.center.x;
        // It skims toward the side with more open ground, coming in steeply from the other side.
        return AimHat(roninX, roninX < CameraShake.Center.x ? 1f : -1f, 8f);
    }

    HatPath AimHat(float roninX, float sweepDir, float skim)
    {
        float laneY = ronin.Bounds.center.y;
        float bound = HalfWidth - 1.5f;
        roninX = Mathf.Clamp(roninX, CameraShake.Center.x - bound, CameraShake.Center.x + bound);
        var path = new HatPath { sweepDir = sweepDir };
        path.a0 = Head;
        path.a1 = Head + new Vector3(-sweepDir * 5f, -1.5f, 0);
        path.a2 = new Vector3(roninX - sweepDir * 2f, laneY + 4f, 0);
        path.a3 = new Vector3(roninX, laneY, 0);
        float skimX = Mathf.Clamp(roninX + sweepDir * skim, CameraShake.Center.x - bound, CameraShake.Center.x + bound);
        path.skimEnd = new Vector3(skimX, laneY, 0);
        return path;
    }

    // The way back up, from the end of the skim to the head wherever the automaton now stands.
    Vector3 HatReturn(HatPath path, float t)
    {
        Vector3 from = path.skimEnd;
        Vector3 c1 = from + new Vector3(path.sweepDir * 2.5f, 4f, 0);
        Vector3 c2 = new Vector3(from.x, Head.y + 1.5f, 0);
        return Bezier(from, c1, c2, Head, t);
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    // Faint dots along the whole loop, so the ronin can read where it will dip.
    List<SpriteRenderer> TrailDots(HatPath path, Color colour)
    {
        var dots = new List<SpriteRenderer>();
        const int perLeg = 9;
        for (int leg = 0; leg < 3; leg++)
        {
            for (int i = 0; i < perLeg; i++)
            {
                float t = (i + 0.5f) / perLeg;
                Vector3 p = leg == 0 ? Bezier(path.a0, path.a1, path.a2, path.a3, t)
                    : leg == 1 ? Vector3.Lerp(path.a3, path.skimEnd, t) : HatReturn(path, t);
                SpriteRenderer dot = Bar(p, p + Vector3.right * 0.22f, 0.22f, new Color(colour.r, colour.g, colour.b, 0.55f), 52);
                dot.sprite = ProceduralSprites.Glow;
                dots.Add(dot);
            }
        }
        return dots;
    }

    // The hat (or, with sword set, the sword wheeling end over end, or with a halo piece set that piece of the broken
    // halo, spinning) flown around the loop as an unbreakable shot.
    IEnumerator FlyHat(HatPath path, List<SpriteRenderer> trail, bool sword, int haloPiece = -1)
    {
        if (sword)
        {
            swordWheeling = swordThrown = true;
            PlayIdle();
        }
        else
        {
            hatsOut++;
        }
        Arrow hat = Own(Instantiate(arrowPrefab, Head, Quaternion.identity));
        // Launched straight down so its heading stays level; Steer then flies it along the curve without turning it.
        hat.Launch(Vector3.down * 0.01f, 5f);
        hat.MakeUnbreakable(null);
        Sprite[] frames = sword ? FlyingSwordFrames() : Fx.Frames(Fx.UtsuKasa);
        Vector3 bladeMiddle = Vector3.zero;
        if (sword)
        {
            Sprite blade = frames != null ? frames[0] : ProceduralSprites.White;
            Vector2 size = frames != null ? (Vector2)blade.bounds.size * SwordScale : new Vector2(0.15f, SwordCoverHeight);
            hat.Restyle(blade, frames != null ? Color.white : Lace, size, 0.6f, new Color(SwordGlow.r, SwordGlow.g, SwordGlow.b, 0.4f), 2.2f);
            // The sword art pivots where the blade meets the ground; it wheels about its middle instead.
            bladeMiddle = Vector3.Scale(blade.bounds.center, hat.Art.transform.lossyScale);
        }
        else if (haloPiece >= 0)
        {
            Sprite[] pieces = Fx.Frames(Fx.UtsuHaloShards);
            Sprite piece = pieces != null ? pieces[Mathf.Min(haloPiece, pieces.Length - 1)] : null;
            // Drawn at the hat's own pixel size, so the pieces add up to the hat.
            float scale = frames != null ? 2.6f / frames[0].bounds.size.x : 1f;
            if (piece != null) hat.Restyle(piece, Color.white, (Vector2)piece.bounds.size * scale, 0.5f, new Color(Molten.r, Molten.g, Molten.b, 0.45f), 2f);
            else hat.Restyle(ProceduralSprites.Disc, Gunmetal, haloPiece == HaloCrown ? new Vector2(1.8f, 0.6f) : new Vector2(1f, 0.6f), 0.5f, new Color(Molten.r, Molten.g, Molten.b, 0.45f), 2f);
        }
        else if (frames != null) hat.Restyle(frames, 14f, Color.white, new Vector2(2.6f, 1.18f), 0.55f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 2.6f);
        else hat.Restyle(ProceduralSprites.Disc, Gunmetal, new Vector2(2.6f, 0.7f), 0.55f, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 2.6f);
        transient.Add(hat.gameObject);

        float pace = secondPhase ? 0.85f : 1f;
        float[] legSeconds = { 0.9f * pace, 0.7f * pace, 1f * pace };
        float nextTassel = Time.time + 0.3f;
        bool fanned = false;
        int dotsGone = 0;
        float total = legSeconds[0] + legSeconds[1] + legSeconds[2];
        bool knockedAway = false;
        // Steered once per physics step, so the hat follows the curve exactly.
        for (float t = 0; t < total; t += Time.fixedDeltaTime * Mathf.Max(Tempo, 0.4f))
        {
            if (hat == null)
            {
                knockedAway = true;
                break;
            }
            Vector3 target;
            if (t < legSeconds[0]) target = Bezier(path.a0, path.a1, path.a2, path.a3, Mathf.SmoothStep(0, 1, t / legSeconds[0]));
            else if (t < legSeconds[0] + legSeconds[1]) target = Vector3.Lerp(path.a3, path.skimEnd, (t - legSeconds[0]) / legSeconds[1]);
            else target = HatReturn(path, Mathf.SmoothStep(0, 1, (t - legSeconds[0] - legSeconds[1]) / legSeconds[2]));
            hat.Steer((target - hat.transform.position) / Time.fixedDeltaTime);
            if (sword)
            {
                Quaternion spin = Quaternion.Euler(0, 0, -720f * t);
                hat.Art.transform.SetPositionAndRotation(hat.transform.position - spin * bladeMiddle, spin);
                if (Random.value < 0.4f) Afterimage(hat.Art, new Color(SwordGlow.r, SwordGlow.g, SwordGlow.b, 0.4f));
            }
            else if (haloPiece == HaloLeft || haloPiece == HaloRight)
            {
                // The side shards cartwheel, each turning toward the side it skims to.
                hat.Art.transform.rotation = Quaternion.Euler(0, 0, -path.sweepDir * 600f * t);
                if (Random.value < 0.4f) Afterimage(hat.Art, new Color(Molten.r, Molten.g, Molten.b, 0.4f));
            }

            // The trail fades out behind the hat as it flies.
            int passed = Mathf.Min(trail.Count, Mathf.FloorToInt(trail.Count * t / total));
            for (; dotsGone < passed; dotsGone++) RemoveTransient(trail[dotsGone]);
            // Climbing home on its last leg, once well above the ronin, the hat spits a fan of gear teeth down at him.
            // Never lower: a shot along his own row is one he couldn't step out of.
            if (!sword && haloPiece < 0 && !fanned && t >= legSeconds[0] + legSeconds[1]
                && hat.transform.position.y - ronin.Bounds.center.y > HatFanHeight)
            {
                fanned = true;
                BulletHell.Fan((from, v) => GearShot(from, v), hat.transform.position, ronin.Bounds.center, 3, 8f, 6.5f);
                Sfx.Play(Sfx.HatClang, 0.4f, 1.7f);
            }
            if (!sword && haloPiece < 0 && t < legSeconds[0] && Time.time >= nextTassel)
            {
                nextTassel = Time.time + (secondPhase ? 0.3f : 0.4f);
                Tassel(hat.transform.position);
            }
            Beat();
            yield return new WaitForFixedUpdate();
        }
        for (; dotsGone < trail.Count; dotsGone++) RemoveTransient(trail[dotsGone]);

        if (knockedAway)
        {
            Sfx.Play(sword ? Sfx.ParryClang : Sfx.HatClang);
            // Parried: it circles the automaton as the ronin's riposte, and is only back once that fades. (The
            // destroyed arrow still holds what replaced it.) Struck the ronin or cut short: a while bare instead.
            Projectile riposte = hat.Riposte;
            if (riposte != null)
            {
                while (riposte != null) yield return null;
            }
            else
            {
                yield return Wait(2f);
            }
            if (!IsDead) Fx.Play(Fx.Impact, sword ? Shoulder : Head, sword ? SwordGlow : haloPiece >= 0 ? Molten : Gold, 0.9f);
        }
        else if (hat != null)
        {
            transient.Remove(hat.gameObject);
            Destroy(hat.gameObject);
            Tick(0.7f);
        }
        if (sword)
        {
            swordWheeling = swordThrown = false;
        }
        else
        {
            hatsOut--;
            if (hatsOut == 0) ShowEye(false);
        }
        // Back on its head, or in its hand.
        if (!IsDead) PlayIdle();
    }

    // Its own shots pass through one another: a swinging weight or a wheeling hat is never cut down by its lace,
    // tassels or cleave. The ronin's attacks still break them.
    static Arrow Own(Arrow shot)
    {
        shot.PassesShots = true;
        return shot;
    }

    void RemoveTransient(SpriteRenderer thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    // A gold weight snapping off the hat's brim and dropping straight down.
    void Tassel(Vector3 from)
    {
        Arrow weight = Own(Shoot(from + Vector3.down * 0.4f, Vector3.down * (secondPhase ? 7.5f : 6.5f)));
        Sprite[] frames = Fx.Frames(Fx.UtsuPlumb);
        if (frames != null) weight.Restyle(frames, 8f, Color.white, new Vector2(0.65f, 0.77f), 0.2f, new Color(Gold.r, Gold.g, Gold.b, 0.35f), 0.8f);
        else weight.Restyle(ProceduralSprites.Disc, Gold, new Vector2(0.4f, 0.55f), 0.2f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 0.8f);
    }

    void ShowEye(bool open)
    {
        if (open && eye == null)
        {
            eye = new GameObject("Eye").AddComponent<SpriteRenderer>();
            eye.transform.SetParent(sprite.transform, false);
            // Under the hat brim in its first form; its broken halo floats clear of the head in the second.
            eye.transform.position = (SecondPhase ? Eye : Head) + Vector3.back * 0.3f;
            eye.transform.localScale = new Vector3(0.25f, 0.7f, 1f) / transform.lossyScale.x;
            eye.sprite = ProceduralSprites.Glow;
            // Molten like its beams once its core has opened.
            eye.color = SecondPhase ? CoreMolten : CoreRed;
            eye.sortingOrder = 45;
        }
        else if (!open && eye != null)
        {
            Destroy(eye.gameObject);
            eye = null;
        }
    }
}
