using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Heart Lantern and Midnight: the sword thrown and planted as cover, the heart's sweeping beam, the clock-hand lasers, and Poguru called out to point at the shelter.
public partial class Utsusemi
{
    // Heart Lantern (second phase only): first it hurls its sword at the ronin, and the sword plants itself in the
    // ground as the one piece of cover there is. Sparks pour into the core as it charges, then a beam sweeps the
    // ground from one side to the other, flashes a warning and sweeps back; only the sword (or a blocker) stops it.
    // Then it dashes down to the sword and wrenches it out with a strike, so the ronin must leave its shelter.
    IEnumerator HeartLantern()
    {
        yield return Stride(CameraShake.Center.x);
        Speak(BossLine.HeartLantern);
        yield return ThrowSword(SwordLanding());
        float dir = Random.value < 0.5f ? 1f : -1f;
        float from = dir > 0 ? LeftEdge + 0.5f : RightEdge - 0.5f;
        float to = dir > 0 ? RightEdge - 0.5f : LeftEdge + 0.5f;
        WarnSword("Poguru_Utsu_Sword");
        yield return Charge();

        // A faint guide line runs the sweep first, so the ronin knows which way to run for cover.
        SpriteRenderer guide = Bar(Core, Core, 0.06f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.35f));
        for (float t = 0; t < 0.7f; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            Stretch(guide, Core, BeamEnd(Mathf.Lerp(from, to, t / 0.7f), out _), 0.06f);
            yield return null;
        }
        RemoveTransient(guide);
        yield return Wait(0.3f);

        // Its attack clip has the sword in hand; with the sword planted it fires from its unarmed stance.
        if (!swordOut) PlayAction(art.attack);
        Beam beam = MakeBeam();
        Sfx.PlayLoop(Sfx.LanternBeam);
        // Out at a brisk sweep, then back very fast: the ronin must already be in the sword's shade.
        yield return SweepBeam(beam, from, to, 2f);
        // The warning: the beam flickers white at the edge, then turns back.
        for (float t = 0; t < 0.4f; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            DrawBeam(beam, Core, BeamEnd(to, out _), Mathf.PingPong(t * 10f, 1f) > 0.5f, false);
            Beat();
            yield return null;
        }
        Tick(0.8f);
        yield return SweepBeam(beam, to, from, 0.5f);
        EndBeam(beam);
        Sfx.Stop(Sfx.LanternBeam);
        yield return ReclaimSword();
    }

    // The planted sword's cover: the steel ward shimmering round it, wider than the blade itself, and tall enough
    // that the shade it casts away from the core leaves the ronin room to stand in.
    const float SwordCoverWidth = 1.6f, SwordCoverHeight = 2.6f;
    // The thrown sword's frames are drawn this many world units tall.
    const float SwordScale = 2f;
    // The throw clip lets the sword go on this frame, with the blade high overhead.
    const int SwordReleaseFrame = 4;

    // Once its core opens, its blade glows molten orange like the one in its phase 2 frames.
    static readonly Color Molten = new Color(1f, 0.55f, 0.15f);
    Color SwordGlow => secondPhase ? Molten : Steel;

    // The sword in flight: its planted art without the cracked ground under the blade, molten in phase 2.
    Sprite[] FlyingSwordFrames()
    {
        Sprite[] frames = secondPhase ? Fx.Frames(Fx.UtsuSwordFlyingP2) : null;
        if (frames == null) frames = Fx.Frames(Fx.UtsuSwordFlying);
        return frames ?? Fx.Frames(Fx.UtsuSword);
    }

    // Its sword while it lies planted as cover; null while it's in hand.
    Blocker plantedSword;
    bool swordOut;
    Sprite[] swordThrowFrames, unarmedIdle;

    public void SetThrowFrames(Sprite[] throwFrames, Sprite[] unarmed)
    {
        swordThrowFrames = throwFrames != null && throwFrames.Length > SwordReleaseFrame ? throwFrames : null;
        unarmedIdle = unarmed != null && unarmed.Length > 0 ? unarmed : null;
    }

    // Where it throws: at the ronin, but never right under its core, where a blade's shadow would be too thin to hide in.
    float SwordLanding()
    {
        const float offCentre = 3.5f;
        float x = AtRonin(2f);
        if (Mathf.Abs(x - CameraShake.Center.x) < offCentre) x = CameraShake.Center.x + (x < CameraShake.Center.x ? -offCentre : offCentre);
        return x;
    }

    // The sword hurled at the ronin: an hour's numeral rises where it will land, as before each Hour Strikes dash, it
    // raises the blade overhead and throws, and the sword wheels end over end to plant itself point-down there,
    // cutting him if he hasn't moved.
    IEnumerator ThrowSword(float x, int hour = 1)
    {
        swordHour = hour;
        Vector3 ground = new Vector3(x, GroundY + 0.15f, 0);
        var mark = new HourMark(this, hour, x);
        mark.Show();
        const float fps = 12f;
        PlayFrames(swordThrowFrames, 0, SwordReleaseFrame, true, fps);
        Sfx.Play(Sfx.ParrySwing, 0.7f, 0.7f);
        Tick(0.8f);
        yield return Wait(swordThrowFrames != null ? (SwordReleaseFrame + 1) / fps : 0.45f);

        swordOut = true;
        PlayIdle();
        Sfx.Play(Sfx.SwordWhirl);
        yield return FlySword(Head + Vector3.up * 0.8f, ground, 0.55f, 2f, 1.2f);
        mark.Struck();

        plantedSword = PlantSword(ground);
        if (Mathf.Abs(ronin.Bounds.center.x - x) < 0.6f) Strike(new Vector3(x, ronin.Bounds.center.y, 0), "Utsusemi_SwordThrow");
        if (Fx.Play(Fx.UtsuChop, ground, Color.white, 1.8f) == null) Fx.Play(Fx.Impact, ground, SwordGlow, 1f);
        Debris.BurstSprites(ground, art.deathParticles, new Color(0.6f, 0.55f, 0.45f), 6, 0.6f, 4f);
        ElementFx.Glow(ground + Vector3.up * 1.2f, Color.white, 0.8f, 2.5f, 0.25f);
        Sfx.Play(Sfx.SwordPlant);
        CameraShake.Add(0.3f);
        yield return Wait(0.3f);
    }

    // The sword in flight, wheeling end over end from `start` to where its blade meets the ground at `to` (or, for a
    // sword pulled back up, to its hand), on an arc rising `lift` above the straight line, trailing steel afterimages.
    IEnumerator FlySword(Vector3 start, Vector3 to, float seconds, float turns, float lift, bool planting = true)
    {
        Sprite[] frames = FlyingSwordFrames();
        var flying = new GameObject("ThrownSword").AddComponent<SpriteRenderer>();
        flying.sortingOrder = 50;
        transient.Add(flying.gameObject);
        if (frames != null)
        {
            flying.sprite = frames[0];
            flying.transform.localScale = Vector3.one * SwordScale;
        }
        else
        {
            flying.sprite = ProceduralSprites.White;
            flying.color = Lace;
            flying.transform.localScale = new Vector3(0.15f, SwordCoverHeight, 1f);
        }
        // The sprite pivots where the blade meets the ground; it spins about its middle as it flies.
        float pivotToMiddle = frames != null ? (72f - 44f) / 48f * SwordScale : 0f;
        Vector3 end = planting ? to + Vector3.up * pivotToMiddle : to;
        for (float t = 0; t < seconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            Beat();
            float k = t / seconds;
            Vector3 middle = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * lift;
            Quaternion spin = Quaternion.Euler(0, 0, -360f * turns * Mathf.SmoothStep(0, 1, k));
            flying.transform.SetPositionAndRotation(middle - spin * Vector3.up * pivotToMiddle, spin);
            if (Random.value < 0.6f) Afterimage(flying, new Color(SwordGlow.r, SwordGlow.g, SwordGlow.b, 0.45f));
            yield return null;
        }
        RemoveTransient(flying);
    }

    // The planted sword wrenches itself out of the ground and wheels back up to its hand.
    IEnumerator RecallSword()
    {
        if (plantedSword == null) yield break;
        Vector3 from = plantedSword.transform.position;
        Destroy(plantedSword.gameObject);
        transient.Remove(plantedSword.gameObject);
        plantedSword = null;
        Sfx.Play(Sfx.SwordWhirl, 0.8f, 1.15f);
        Fx.Play(Fx.Impact, new Vector3(from.x, GroundY + 0.15f, 0), SwordGlow, 0.8f);
        yield return FlySword(from, Head + Vector3.up * 0.8f, 0.4f, -1f, 0.6f, false);
        Tick(0.8f);
    }

    // The planted sword is a blocker that never breaks: it stops beams and shots alike, and the beam only strikes
    // sparks off it.
    Blocker PlantSword(Vector3 ground)
    {
        var sword = new GameObject("PlantedSword");
        sword.transform.position = ground + Vector3.up * SwordCoverHeight / 2f;
        sword.AddComponent<BoxCollider>().size = new Vector3(SwordCoverWidth, SwordCoverHeight, 0.5f);
        transient.Add(sword.gameObject);

        FxPlayer planted = secondPhase ? Fx.Play(Fx.UtsuSwordP2, ground, Color.white, SwordScale, null, 4) : null;
        if (planted == null) planted = Fx.Play(Fx.UtsuSword, ground, Color.white, SwordScale, null, 4);
        SpriteRenderer look;
        if (planted != null)
        {
            planted.transform.SetParent(sword.transform, true);
            look = planted.GetComponent<SpriteRenderer>();
        }
        else
        {
            look = new GameObject("Blade").AddComponent<SpriteRenderer>();
            look.transform.SetParent(sword.transform, false);
            look.transform.localScale = new Vector3(0.15f, SwordCoverHeight, 1f);
            look.sprite = ProceduralSprites.White;
            look.color = Lace;
            look.sortingOrder = 4;
        }
        // A ward round the blade, showing how far its cover reaches.
        SpriteRenderer ward = Projectile.CreateGlow(sword.transform, 1f, 3, new Color(SwordGlow.r, SwordGlow.g, SwordGlow.b, 0.5f));
        ward.transform.localScale = new Vector3(SwordCoverWidth * 1.6f, SwordCoverHeight * 1.3f, 1f);

        Blocker cover = sword.AddComponent<Blocker>();
        cover.Init(int.MaxValue, look, new[] { look.sprite }, art.deathParticles);
        return cover;
    }

    // The numeral the sword was thrown under, raised again when it takes the sword back.
    int swordHour = 1;
    // Seconds between the reclaim's warning and its dash down onto the sword.
    const float ReclaimWarning = 0.7f;

    // It takes its sword back: the blade glints, it dashes down onto it, wrenches it out of the ground with a strike
    // that cuts anyone still sheltering there, and dashes back to its row.
    IEnumerator ReclaimSword()
    {
        if (plantedSword == null)
        {
            swordOut = false;
            RestPoguru();
            PlayIdle();
            yield break;
        }
        Vector3 row = anchor;
        float x = plantedSword.transform.position.x;
        // Poguru, who was pointing at the sword, flies back to Oni with a quick yelp: it's coming down on it.
        CallPoguru("Poguru_Utsu_Reclaim", null);
        ElementFx.Glow(plantedSword.transform.position, Color.white, 0.9f, 2.5f, 0.4f);
        Sfx.Play(Sfx.ParryClang, 0.5f, 1.6f);
        Tick(0.9f);
        // The sword's numeral rises again under it, as before an Hour Strikes dash, so the shelter reads as the
        // next target: play-testers stayed by the sword and took the chop. A longer window to step off than before.
        var mark = new HourMark(this, swordHour, x);
        mark.Show();
        yield return Wait(ReclaimWarning);

        Beat();
        // The chop can be parried: a gold ring closes on the sword's spot through the dash and wind-up and flashes
        // white just before the blade lands.
        const float dashTime = 0.2f;
        StartCoroutine(ChopTell(x, dashTime + ChopWindUp(), 0.16f));
        yield return Dash(new Vector3(x, Landing(), row.z), dashTime);
        Destroy(plantedSword.gameObject);
        transient.Remove(plantedSword.gameObject);
        plantedSword = null;
        swordOut = false;
        yield return Chop(x, mark, parryable: true);
        RestPoguru();
        yield return Dash(row);
        PlayIdle();
    }

    IEnumerator SweepBeam(Beam beam, float from, float to, float seconds)
    {
        float lastX = from;
        for (float t = 0; t < seconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            float x = Mathf.Lerp(from, to, t / seconds);
            Vector3 end = BeamEnd(x, out Blocker cover);
            DrawBeam(beam, Core, end, false, cover == null);
            if (cover != null) BurnCover(beam, cover, end);
            // A fast sweep moves further in a frame than the ronin is wide, so the ground it skipped is checked too.
            for (float step = 0; step <= 1f; step += SweepCheckStep / Mathf.Max(Mathf.Abs(x - lastX), SweepCheckStep))
            {
                Vector3 between = BeamEnd(Mathf.Lerp(lastX, x, step), out Blocker shade);
                if (shade == null) BeamHit(Core, between);
            }
            lastX = x;
            yield return null;
        }
    }

    // How finely a sweep's path between frames is checked against the ronin.
    const float SweepCheckStep = 0.3f;

    // Midnight (second phase only): it hurls its sword at the ronin and the sword plants itself there, then two
    // clock-hand beams sweep in from the screen's edges, closing on the sword from both sides. Beside the sword is the
    // one ground they never cross (it's cover too). As they meet, a bell strikes and they flash, then snap back out.
    // Below a quarter of its health they meet twice: with the hands held at the edges it wrenches the sword back to
    // its hand and throws it again at wherever the ronin ran, so he must dodge it and get beside it again. Then it
    // dashes down and takes its sword back, as after the Heart Lantern.
    IEnumerator Midnight()
    {
        yield return Stride(CameraShake.Center.x);
        Speak(BossLine.Midnight);
        int strikes = HealthFraction <= 0.25f ? 2 : 1;
        yield return ThrowSword(AtRonin(3f));
        float markX = plantedSword != null ? plantedSword.transform.position.x : ronin.Bounds.center.x;
        // The slit-eye flares as the core charges, so the second source is no surprise.
        ElementFx.Glow(Eye, CoreRed, 0.4f, 2.5f, 0.9f, transform);
        yield return Charge();

        // One hand burns from its slit-eye, the other from its heart.
        Beam left = MakeBeam(Eye, 1.1f), right = MakeBeam();
        left.fromEye = true;
        Sfx.PlayLoop(Sfx.LanternBeam, 1f, 0.9f);
        float leftEdge = LeftEdge + 0.3f, rightEdge = RightEdge - 0.3f;
        // The hands stop this far either side of the sword: the gap they leave is the safe spot.
        const float gap = 1.1f;
        WarnSword("Poguru_Utsu_Sword");
        for (int strike = 0; strike < strikes; strike++)
        {
            // Close: slow at first, quickening as they near twelve.
            for (float t = 0; t < 1.6f; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
            {
                Beat();
                float k = t / 1.6f;
                k *= k;
                DrawHand(left, Mathf.Lerp(leftEdge, markX - gap, k), false);
                DrawHand(right, Mathf.Lerp(rightEdge, markX + gap, k), false);
                yield return null;
            }
            // Strike: the bell and a white flash, held a moment.
            Sfx.Play(Sfx.MidnightChime);
            CameraShake.Add(0.3f);
            ElementFx.Glow(new Vector3(markX, GroundY + 0.5f, 0), Gold, 1f, 4f, 0.5f);
            for (float t = 0; t < 0.4f; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
            {
                Beat();
                DrawHand(left, markX - gap, true);
                DrawHand(right, markX + gap, true);
                yield return null;
            }
            // Open: back out to the edges, faster.
            for (float t = 0; t < 0.9f; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
            {
                Beat();
                float k = Mathf.SmoothStep(0, 1, t / 0.9f);
                DrawHand(left, Mathf.Lerp(markX - gap, leftEdge, k), false);
                DrawHand(right, Mathf.Lerp(markX + gap, rightEdge, k), false);
                yield return null;
            }
            // Another hour to strike: the sword comes back and goes out again while the hands wait at the edges.
            if (strike + 1 < strikes)
            {
                // Poguru comes back to Oni while the sword flies, and over to it again once it lands.
                CallPoguru(null, null);
                yield return HoldingHands(RecallSword(), left, right, leftEdge, rightEdge);
                yield return HoldingHands(ThrowSword(AtRonin(3f), strike + 2), left, right, leftEdge, rightEdge);
                if (plantedSword != null) markX = plantedSword.transform.position.x;
                WarnSword(null);
            }
        }
        EndBeam(left);
        EndBeam(right);
        Sfx.Stop(Sfx.LanternBeam);
        yield return ReclaimSword();
    }

    // Once the sword plants, Poguru flies out of the wisp yelling to hide behind it and hovers over it until the laser is
    // over; that is the shelter's only marker. A line of null just moves him over the sword (Midnight's second throw).
    void WarnSword(string line)
    {
        if (plantedSword == null) return;
        Vector3 top = plantedSword.transform.position + Vector3.up * (SwordCoverHeight / 2f + 0.7f);
        CallPoguru(line, top);
    }

    // Whether this fight has Poguru out of the wisp, so he's only sent back when it called him.
    bool poguruOut;

    void CallPoguru(string line, Vector3? over)
    {
        poguruOut = true;
        PoguruWarn?.Invoke(line, over);
    }

    void RestPoguru()
    {
        if (!poguruOut) return;
        poguruOut = false;
        PoguruRest?.Invoke();
    }

    // A beam that crosses the ronin's height burns him, unless he stands within the planted sword's ward: the whole
    // ward is his cover, not just the shade the blade casts. Unless told otherwise it's a sword laser (the Heart
    // Lantern's sweep and Midnight's hands), the one Poguru says to hide behind the sword from.
    void BeamHit(Vector3 from, Vector3 end, string attack = "Utsusemi_SwordLaser")
    {
        if (InSwordWard(ronin.Bounds.center.x)) return;
        HitIfCrossed(from, end, attack);
    }

    bool InSwordWard(float x) => plantedSword != null && Mathf.Abs(x - plantedSword.transform.position.x) <= SwordCoverWidth / 2f;

    // Runs a step of the attack (and everything it waits on) while both hands keep burning at the given spots.
    IEnumerator HoldingHands(IEnumerator step, Beam left, Beam right, float leftX, float rightX)
    {
        var steps = new Stack<IEnumerator>();
        steps.Push(step);
        while (steps.Count > 0)
        {
            IEnumerator top = steps.Peek();
            if (!top.MoveNext())
            {
                steps.Pop();
                continue;
            }
            if (top.Current is IEnumerator inner)
            {
                steps.Push(inner);
                continue;
            }
            Beat();
            DrawHand(left, leftX, false);
            DrawHand(right, rightX, false);
            yield return top.Current;
        }
    }

    // One hand of the clock: a beam from the core (or eye) to the ground at x, stopped by cover, burning the ronin
    // it crosses.
    void DrawHand(Beam hand, float x, bool flash)
    {
        Vector3 from = hand.fromEye ? Eye : Core;
        Vector3 end = BeamEnd(from, x, out Blocker cover);
        DrawBeam(hand, from, end, flash, cover == null);
        if (cover != null) BurnCover(hand, cover, end);
        else BeamHit(from, end);
    }

    // The ronin's spot on the ground, kept at least margin inside the screen.
    float AtRonin(float margin) => Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + margin, RightEdge - margin);

    // Sparks pour into the core while it whines up to full power.
    IEnumerator Charge()
    {
        if (Fx.Play(Fx.UtsuCharge, Core, secondPhase ? MoltenTint : Color.white, 2.2f, transform) == null) ElementFx.Glow(Core, BeamColour, 2f, 0.5f, 0.7f, transform);
        ElementFx.Glow(Core, BeamColour, 0.5f, 4f, 0.9f, transform);
        Sfx.Play(Sfx.LanternCharge);
        CameraShake.Add(0.15f);
        for (int i = 0; i < 3; i++)
        {
            Tick(0.7f);
            yield return Wait(0.25f);
        }
    }
}
