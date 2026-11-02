using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The clock itself: the escape wheel's Escapement, the Pendulum Kick, the Stopped Hour and the Thirteenth Hour.
public partial class Utsusemi
{
    // A small spinning gold cog: breakable, one of a bullet-hell curtain. Tinted (the Thirteenth Hour's molten
    // sparks) it keeps its shape in that colour.
    Arrow GearShot(Vector3 from, Vector3 velocity, Color? tint = null)
    {
        Color c = tint ?? Gold;
        Arrow shot = BulletHell.Enlist(Shoot(from, velocity));
        Color body = tint.HasValue ? Color.Lerp(Color.white, c, 0.6f) : Color.white;
        shot.Restyle(ProceduralSprites.Cog, 14f, body, new Vector2(0.42f, 0.42f), 0.17f, new Color(c.r, c.g, c.b, 0.45f), 0.9f);
        return shot;
    }

    // Escapement's escape wheel: a great brass wheel turning behind its body, one tooth a tick.
    SpriteRenderer escapeWheel;
    const int WheelTeeth = 30;
    const float WheelSize = 5.6f;
    Vector3 WheelCentre => Core + new Vector3(0, 0.5f, 0.4f);

    void ShowEscapeWheel(bool show)
    {
        if (show && escapeWheel == null)
        {
            Sprite[] frames = Fx.Frames(Fx.UtsuEscapeWheel);
            if (frames == null) return;
            escapeWheel = new GameObject("EscapeWheel").AddComponent<SpriteRenderer>();
            escapeWheel.sprite = frames[0];
            escapeWheel.sortingOrder = sprite.sortingOrder - 3;
            transient.Add(escapeWheel.gameObject);
            // Wider than its body, so the toothed rim rings it like a clock halo instead of hiding behind it.
            escapeWheel.transform.localScale = Vector3.one * (WheelSize / frames[0].bounds.size.x);
            escapeWheel.color = new Color(1f, 1f, 1f, 0f);
        }
        else if (!show && escapeWheel != null)
        {
            RemoveTransient(escapeWheel);
            escapeWheel = null;
        }
    }

    // Steps the wheel on one tooth with a snap and a flash, from where it hangs behind the core.
    IEnumerator TurnEscapeWheel(float alpha, float seconds)
    {
        if (escapeWheel == null) yield break;
        float from = escapeWheel.transform.eulerAngles.z, to = from - 360f / WheelTeeth;
        for (float t = 0; t < seconds && escapeWheel != null; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / seconds);
            // Eased hard into a dead stop, like its steps.
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            escapeWheel.transform.SetPositionAndRotation(WheelCentre, Quaternion.Euler(0, 0, Mathf.Lerp(from, to, eased)));
            escapeWheel.color = new Color(1f, 0.95f, 0.85f, alpha);
            yield return null;
        }
    }

    // Escapement: the clock it winds. It rocks side to side like an escapement's anchor, and on every tick its core
    // spits a small fan of gear teeth at the ronin, the beat quickening as it goes. Every fourth tick is a bronze bell:
    // parried, it rings out and bursts the teeth around it. Once unwound, violet darts bracket him on the off-beats,
    // so standing still is right as often as stepping.
    IEnumerator Escapement()
    {
        int ticks = secondPhase ? 14 : 10;
        Sfx.Play(Sfx.GearGrind, 0.5f, 1.6f);
        ElementFx.Glow(Core, BeamColour, 0.7f, 2.5f, 0.4f, transform);
        // The escape wheel rises behind it, turning up to speed.
        ShowEscapeWheel(true);
        for (float t = 0; t < 0.45f && escapeWheel != null; t += Time.deltaTime)
        {
            escapeWheel.transform.SetPositionAndRotation(WheelCentre, Quaternion.Euler(0, 0, -t * 90f));
            escapeWheel.color = new Color(1f, 0.95f, 0.85f, 0.75f * t / 0.45f);
            yield return null;
        }
        float home = anchor.x;
        for (int i = 0; i < ticks; i++)
        {
            Beat();
            float k = i / (ticks - 1f);
            float side = (i % 2 == 0 ? 1f : -1f) * 0.45f;
            yield return Dash(new Vector3(Mathf.Clamp(home + side, LeftEdge + 1.5f, RightEdge - 1.5f), anchor.y, anchor.z), 0.07f);
            Tick(0.7f);
            Vector3 from = Core;
            bool bell = i % 4 == 3;
            // Each tick: the wheel snaps on a tooth, and its heart flashes as the shot leaves it.
            StartCoroutine(TurnEscapeWheel(bell ? 0.95f : 0.75f, 0.12f));
            Fx.Play(Fx.Impact, from, bell ? BulletHell.Bronze : Gold, bell ? 1.1f : 0.6f);
            ElementFx.Glow(from, bell ? BulletHell.Bronze : BeamColour, 0.6f, bell ? 3f : 1.6f, 0.2f, transform);
            // Every tick warps the air: a hard ripple of space out from the wheel, the beat made visible. The bell's is
            // the strongest, and for an instant the world greys toward stopped time.
            if (bell)
            {
                TimeRipple.Emit(WheelCentre, 14f, 2.8f, 0.9f, band: 1.1f);
                TimeRipple.Emit(WheelCentre, 6f, 1.6f, 0.45f, band: 0.6f);
                StartCoroutine(StopFlicker(0.45f, 0.3f));
            }
            else TimeRipple.Emit(WheelCentre, 9f, 1.8f + k, 0.65f, band: 0.8f);
            Vector3 at = ronin.Bounds.center;
            yield return BulletHell.Room(4);
            if (bell)
            {
                BulletHell.MakeBell(BulletHell.Enlist(Shoot(from, (at - from).normalized * 5f)), 1.1f);
                Sfx.Play(Sfx.MidnightChime, 0.35f, 1.6f);
            }
            else if (secondPhase && i % 2 == 1)
            {
                // Either side of him, a clear stretch between: the step he'd take is the one they punish.
                foreach (float off in new[] { -1.9f, 1.9f })
                {
                    BulletHell.MakeSealDart(BulletHell.Enlist(Shoot(from, (at + Vector3.right * off - from).normalized * 7.5f)), BulletHell.Violet);
                }
            }
            else BulletHell.Fan((f, v) => GearShot(f, v), from, at, 3, secondPhase ? 9f : 8f, secondPhase ? 7.5f : 6.5f);
            yield return Wait(Mathf.Lerp(0.6f, secondPhase ? 0.28f : 0.34f, k));
        }
        yield return Dash(new Vector3(home, anchor.y, anchor.z), 0.1f);
        // The wheel winds down and fades.
        for (float t = 0; t < 0.35f && escapeWheel != null; t += Time.deltaTime)
        {
            escapeWheel.color = new Color(1f, 0.95f, 0.85f, 0.75f * (1f - t / 0.35f));
            yield return null;
        }
        ShowEscapeWheel(false);
        PlayIdle();
    }

    // Cuts the ronin landed from almost underneath it, by time. Too many too fast and it kicks clear (Pendulum Kick).
    readonly List<float> campHits = new List<float>();
    const float CampRange = 1.6f, CampWindow = 3f;
    const int CampHits = 8;

    protected override void OnDamaged()
    {
        if (ronin != null && Mathf.Abs(ronin.Bounds.center.x - Centre.x) < CampRange) campHits.Add(Time.time);
    }

    bool Camped
    {
        get
        {
            campHits.RemoveAll(t => Time.time - t > CampWindow);
            return campHits.Count >= CampHits;
        }
    }

    // Pendulum Kick: he has stood under it hacking away. It swings aside like a pendulum and its core bursts a ring
    // of gear teeth with one clear arc, past it on the far side: the answer is to move, not to keep cutting.
    IEnumerator PendulumKick()
    {
        campHits.Clear();
        float away = ronin.Bounds.center.x < Centre.x ? 1f : -1f;
        Tick(0.9f);
        ElementFx.Glow(Core, BeamColour, 0.8f, 2f, 0.25f, transform);
        Sfx.Play(Sfx.GearGrind, 0.5f, 1.8f);
        yield return Wait(0.3f);
        float x = Mathf.Clamp(anchor.x + away * 2.2f, LeftEdge + 1.5f, RightEdge - 1.5f);
        yield return Dash(new Vector3(x, anchor.y, anchor.z), 0.12f);
        Sfx.Play(Sfx.HatClang, 0.7f, 1.3f);
        CameraShake.Add(0.15f);
        yield return BulletHell.Room(18);
        Vector3 from = Core;
        Vector3 clear = new Vector3(x + away * 3f, ronin.Bounds.center.y, 0);
        float clearAngle = Vector3.SignedAngle(Vector3.right, clear - from, Vector3.forward);
        for (int i = 0; i < 18; i++)
        {
            float a = i * 20f + 10f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, clearAngle)) < 16f) continue;
            GearShot(from, Quaternion.Euler(0, 0, a) * Vector3.right * 6f);
        }
        yield return Wait(0.2f);
        PlayIdle();
    }

    // The Stopped Hour's lace fragments and cleaves, hung in the air while time is held.
    readonly List<Arrow> needles = new List<Arrow>();
    readonly List<Arrow> stoppedCleaves = new List<Arrow>();
    int stoppedHours;
    const float NeedleSpeed = 6f;
    // Rows hang this far apart, so they land a third of a second apart once released.
    const float NeedleRowGap = 1.9f;
    // How far apart they come for him once time runs, so each is its own parry.
    const float CleaveGap = 0.55f;
    const float StoppedCleaveSpeed = 9.5f;

    // Stopped Hour: the hour it keeps, held still. It raises its burning fist and time stops: colour drains, the clock
    // falls silent, the air warps out from its fist, and everything stands frozen, the ronin too: he can't move,
    // strike or parry. Only Utsusemi moves. It hangs rows of its burning lace in the air over him, each with one way
    // through, then steps through the stopped world to three places around him, and at each swings, leaving a great
    // hour cleave hanging aimed at him. Back in its row, the warps rush into its fist, a bell tolls and time runs: the
    // lace falls, lowest row first, and as the last rows pass, the cleaves come for him one after another, homing and
    // unbreakable. Walk the gaps, then parry each cleave; sent back, they jam its gears. Each Stopped Hour adds a row
    // and narrows the gaps.
    IEnumerator StoppedHour()
    {
        stoppedHours++;
        Vector3 row = anchor;
        int rows = stoppedHours > 1 ? 3 : 2;
        float gapWidth = Mathf.Max(BulletHell.MinGap, 3.4f - 0.4f * (stoppedHours - 1));
        float gapLo = LeftEdge + 0.6f + gapWidth / 2f, gapHi = RightEdge - 0.6f - gapWidth / 2f;

        if (stopFrames != null) PlayFrames(stopFrames, 0, -1, true, 10f);
        else PlayAction(art.parry);
        Sfx.Play(Sfx.LanternCharge, 0.6f, 0.6f);
        yield return Wait(0.5f);

        // Time stops: the clock falls silent, everything in the air hangs, the ronin with it, and the air warps out
        // from its raised fist as the colour drains away.
        Vector3 Fist() => stopFrames != null ? OnSprite(FistPixel.x, FistPixel.y) : Head;
        Vector3 hand = Fist();
        Sfx.Play(Sfx.MidnightChime, 0.8f, 0.5f);
        Music.Duck(0.2f, 4f);
        CameraShake.Add(0.2f);
        HoldTime(true);
        // The world warps hard as time stops: a great ring out from its fist and a tighter one inside it.
        TimeRipple.Emit(hand, 18f, 3f, 1.6f, band: 1.3f);
        TimeRipple.Emit(hand, 9f, 2f, 1.1f, band: 0.8f);
        yield return StopFade(0f, 1f, 0.35f);

        // The lace: rows hung over him, the first with its way through a few steps off to one side (time runs before
        // he has to reach it), each higher row landing a beat later with its gap a step on from the last's.
        needles.Clear();
        float lowest = ronin.Bounds.center.y + 5.5f;
        float lastGap = ronin.Bounds.center.x;
        for (int r = 0; r < rows; r++)
        {
            Beat();
            float gap;
            if (r == 0)
            {
                float away = Random.value < 0.5f ? -1f : 1f;
                gap = lastGap + away * Random.Range(2.5f, 4.5f);
                if (gap < gapLo || gap > gapHi) gap = lastGap - away * Random.Range(2.5f, 4.5f);
                gap = Mathf.Clamp(gap, gapLo, gapHi);
                BulletHell.GapOk(lastGap, gap, (lowest - ronin.Bounds.center.y) / NeedleSpeed, gapWidth, "Stopped Hour");
            }
            else
            {
                float seconds = NeedleRowGap / NeedleSpeed;
                float want = lastGap + (Random.value < 0.5f ? -1f : 1f) * BulletHell.MaxDodgeSpeed * seconds;
                gap = BulletHell.NextGap(lastGap, want, seconds * 0.9f, gapLo, gapHi);
                BulletHell.GapOk(lastGap, gap, seconds, gapWidth, "Stopped Hour");
            }
            float y = lowest + r * NeedleRowGap;
            yield return BulletHell.Room(30);
            needles.AddRange(BulletHell.Curtain(Needle, y, LeftEdge + 0.3f, RightEdge - 0.3f, 0.8f, new[] { gap }, gapWidth, 0f));
            TimeRipple.Emit(new Vector3(gap, y, 0), 4f, 1.8f, 0.6f, band: 0.7f);
            Sfx.Play(Sfx.ParryClang, 0.3f, 2f);
            lastGap = gap;
            yield return Wait(0.45f);
        }

        // Three places around him, either side and then straight over him, in an order he can't count on.
        float roninX = ronin.Bounds.center.x;
        float lo = LeftEdge + 1.6f, hi = RightEdge - 1.6f;
        float side = Random.value < 0.5f ? -1f : 1f;
        var spots = new[]
        {
            new Vector3(Mathf.Clamp(roninX + side * 5f, lo, hi), row.y - 1.8f, row.z),
            new Vector3(Mathf.Clamp(roninX - side * 5f, lo, hi), row.y - 1.8f, row.z),
            new Vector3(Mathf.Clamp(roninX, lo, hi), row.y + 0.2f, row.z),
        };
        stoppedCleaves.Clear();
        foreach (Vector3 spot in spots)
        {
            Beat();
            // It steps through stopped time, the air rippling where it lands.
            yield return Dash(spot, 0.14f);
            TimeRipple.Emit(Centre, 6f, 1.6f, 0.5f, band: 0.7f);
            const float swingFps = 22f;
            PlayFrames(slashFrames, 1, -1, false, swingFps);
            yield return Wait(slashFrames != null ? (SlashReleaseFrame - 1) / swingFps : 0.15f);
            stoppedCleaves.Add(HangCleave(Centre + (ronin.Bounds.center - Centre).normalized * 1.4f));
            Sfx.Play(Sfx.HourSlash, 0.5f, 0.8f);
            Sfx.Play(Sfx.ParryClang, 0.3f, 2f);
            yield return Wait(0.3f);
        }
        yield return Dash(row, 0.16f);
        PlayIdle();

        // Held: a beat to see what's coming, its fist throbbing slow rings through the frozen air.
        Beat();
        for (float t = 0; t < 0.9f; t += HeldPulse)
        {
            TimeRipple.Emit(Fist(), 12f, 1.4f, 0.8f, band: 1f);
            yield return Wait(HeldPulse);
        }

        // The warps rush back into its fist, hard, a bell tolls, and time runs again.
        hand = Fist();
        TimeRipple.Emit(hand, 18f, 3f, 0.6f, inward: true, band: 1.3f);
        yield return Wait(0.6f);
        if (Sfx.Has(Sfx.BellToll)) Sfx.Play(Sfx.BellToll, 0.9f, 1f);
        else Sfx.Play(Sfx.MidnightChime, 1f, 0.9f);
        CameraShake.Add(0.25f);
        HoldTime(false);
        // Time snaps back with a shockwave out from its fist.
        TimeRipple.Emit(hand, 18f, 2.6f, 0.7f, band: 1.2f);
        StartCoroutine(StopFade(1f, 0f, 0.15f));
        Tick(0.8f);
        // The lace falls at once, the lowest row first.
        foreach (Arrow needle in needles) if (needle != null) needle.Turn(Vector3.down * NeedleSpeed);
        float lastRowLands = (lowest + (rows - 1) * NeedleRowGap - ronin.Bounds.center.y) / NeedleSpeed;
        needles.Clear();
        // Then, as the last rows pass, the cleaves: launched, so a stagger from the first parried one won't stop the
        // others coming.
        var loosed = new List<Arrow>(stoppedCleaves);
        stoppedCleaves.Clear();
        yield return Launch(LooseCleaves(loosed, Mathf.Max(0.3f, lastRowLands - 0.7f)));
        PlayIdle();
        Beat();
        yield return Wait(0.3f);
    }

    // An hour cleave left hanging in stopped time, facing the ronin: unbreakable, sent back only by a parry.
    Arrow HangCleave(Vector3 from)
    {
        Vector3 aim = (ronin.Bounds.center - from).normalized;
        // Launched a hair's breadth so it turns to face him, then held dead still.
        Arrow cleave = Own(Shoot(from, aim * 0.01f));
        cleave.Steer(Vector3.zero);
        Sprite[] frames = Fx.Frames(Fx.UtsuHourSlash);
        if (frames != null) cleave.Restyle(frames, 14f, Color.white, new Vector2(4.2f, 2.8f), 0.9f, new Color(Molten.r, Molten.g, Molten.b, 0.6f), 3.6f);
        else cleave.Restyle(ProceduralSprites.White, new Color(0.9f, 0.93f, 1f), new Vector2(4f, 0.4f), 0.9f, new Color(Molten.r, Molten.g, Molten.b, 0.6f), 3.6f);
        cleave.MakeUnbreakable();
        cleave.MakeReflectable(transform);
        ElementFx.Glow(from, Molten, 0.8f, 2.4f, 0.25f);
        return cleave;
    }

    // Time runs: after `delay`, each hanging cleave turns on the ronin and hunts him, a beat after the last. Real time,
    // launched; they hang where they are until their turn.
    IEnumerator LooseCleaves(List<Arrow> cleaves, float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (Arrow cleave in cleaves)
        {
            if (cleave == null) continue;
            cleave.Turn((ronin.Bounds.center - cleave.transform.position).normalized * StoppedCleaveSpeed);
            StartCoroutine(Pursue(cleave, StoppedCleaveSpeed));
            Sfx.Play(Sfx.HourSlash, 0.8f, 1.1f);
            CameraShake.Add(0.15f);
            yield return new WaitForSeconds(CleaveGap);
        }
    }

    // How often the stopped clock sends a ring through the frozen air while it holds.
    const float HeldPulse = 0.45f;

    // A fragment of its burning lace, hung in stopped time: unparryable, through cover, and blades break on it, so
    // the only answer is to move. Without the phase-two lace art, a violet seal-dart.
    Arrow Needle(Vector3 from, Vector3 velocity)
    {
        Arrow shot = BulletHell.Enlist(Shoot(from, velocity));
        Sprite[] frames = Fx.Frames(Fx.UtsuLaceP2);
        if (frames == null) return BulletHell.MakeSealDart(shot, BulletHell.Violet);
        shot.Restyle(frames, 8f, Color.white, new Vector2(0.85f, 0.85f), 0.27f, new Color(Molten.r, Molten.g, Molten.b, 0.45f), 1f);
        // Each hangs at its own tilt, like a shard caught mid-fall.
        shot.Art.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-20f, 20f));
        shot.Unparryable = true;
        shot.PassesCover = true;
        return shot;
    }

    // While time is held: everything else in the air hangs where it is, its own fire stands still, and the ronin
    // stands frozen mid-frame, his own shots with him.
    bool timeHeld;
    readonly List<(Arrow shot, Vector3 velocity)> held = new List<(Arrow, Vector3)>();
    readonly List<(Projectile shot, Rigidbody body, Vector3 velocity)> heldShots = new List<(Projectile, Rigidbody, Vector3)>();

    void HoldTime(bool hold)
    {
        timeHeld = hold;
        if (ronin != null) ronin.TimeHeld = hold;
        if (hold)
        {
            foreach (Arrow shot in FindObjectsByType<Arrow>(FindObjectsSortMode.None))
            {
                if (shot == null || !shot.isActiveAndEnabled || stoppedCleaves.Contains(shot) || needles.Contains(shot)) continue;
                held.Add((shot, shot.Velocity));
                shot.Steer(Vector3.zero);
            }
            foreach (Projectile shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                var body = shot.GetComponent<Rigidbody>();
                if (body == null) continue;
                heldShots.Add((shot, body, body.linearVelocity));
                // Its own steering (homing, spinning) stops too, so it can't drift on.
                shot.enabled = false;
                body.linearVelocity = Vector3.zero;
            }
        }
        else
        {
            foreach ((Arrow shot, Vector3 velocity) in held) if (shot != null) shot.Steer(velocity);
            held.Clear();
            foreach ((Projectile shot, Rigidbody body, Vector3 velocity) in heldShots)
            {
                if (shot == null) continue;
                shot.enabled = true;
                body.linearVelocity = velocity;
            }
            heldShots.Clear();
        }
    }

    // A brief grey flicker toward stopped time and back (Escapement's bell); never while time is really held.
    IEnumerator StopFlicker(float peak, float seconds)
    {
        for (float t = 0; t < seconds && !timeHeld; t += Time.deltaTime)
        {
            TimeRipple.SetStop(peak * Mathf.Sin(t / seconds * Mathf.PI));
            yield return null;
        }
        if (!timeHeld) TimeRipple.SetStop(0f);
    }

    IEnumerator StopFade(float from, float to, float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            TimeRipple.SetStop(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        TimeRipple.SetStop(to);
    }

    // Lets time run again, frees the ronin, and bursts any cleaves still hung in the air (the move cut short).
    void ClearStoppedTime()
    {
        TimeRipple.SetStop(0f);
        if (timeHeld) HoldTime(false);
        foreach (Arrow cleave in stoppedCleaves) if (cleave != null) cleave.Pop(Molten);
        stoppedCleaves.Clear();
        foreach (Arrow needle in needles) if (needle != null) needle.Pop(Molten);
        needles.Clear();
    }

    // The Thirteenth Hour comes once, under this share of its last bar.
    const float ThirteenthAt = 0.15f;
    const float ThirteenthSeconds = 10f;
    // Each sweep of its hand over one half of the ground, its warning line included.
    const float HandSeconds = 2.5f, HandTell = 0.7f;
    bool thirteenthStruck;

    // The Thirteenth Hour: the hour that should never come. Low on its last bar it strides to the middle and its core
    // turns like a clock face: a spiral of molten sparks wheels out while its heart's beam, one hand, sweeps one half
    // of the ground and then the other, a warning line before each. It can be cut down throughout: a last stand,
    // not a wall.
    IEnumerator ThirteenthHour()
    {
        thirteenthStruck = true;
        float mid = CameraShake.Center.x;
        yield return Stride(mid);
        Sfx.Play(Sfx.MidnightChime, 1f, 0.6f);
        Music.Duck(0.25f, 3f);
        TimeRipple.Emit(Core, 12f, 0.8f, 1.4f);
        ElementFx.Glow(Core, Molten, 1f, 5f, 0.6f, transform);
        yield return Wait(0.8f);

        // The spiral's beat is worked out from its height, so one arm's landings sit a gap apart (as BulletHell.Spiral).
        const float spin = 38f, sparkSpeed = 5f, sparkRadius = 0.17f;
        // Only arms pointing down fire, so six keep two or three raining at once.
        const int arms = 6;
        float height = Mathf.Max(1f, Core.y - ronin.Bounds.center.y);
        float interval = Mathf.Max(0.08f, (BulletHell.MinGap + 2f * sparkRadius) / (height * spin * Mathf.Deg2Rad));
        float nextSpark = 0f;
        bool left = ronin.Bounds.center.x > mid;
        int sweep = -1;
        Beam hand = null;
        SpriteRenderer guide = null;
        for (float t = 0; t < ThirteenthSeconds && !IsDead; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            if (t >= nextSpark)
            {
                nextSpark = t + interval;
                for (int a = 0; a < arms; a++)
                {
                    float angle = Mathf.Repeat(spin * t + 360f * a / arms + 180f, 360f) - 180f;
                    if (Mathf.Abs(angle) > 80f) continue;
                    GearShot(Core, Quaternion.Euler(0, 0, angle) * Vector3.down * sparkSpeed, Molten);
                }
            }

            // The hand: each sweep takes the other half of the ground.
            int now = (int)(t / HandSeconds);
            if (now != sweep)
            {
                sweep = now;
                left = !left;
                if (hand != null) EndBeam(hand);
                hand = null;
                TimeRipple.Emit(Core, 6f, 0.4f, 0.7f);
                Tick(0.9f);
            }
            float s = t - sweep * HandSeconds;
            float dir = left ? -1f : 1f;
            float reach = (left ? mid - LeftEdge : RightEdge - mid) - 0.5f;
            if (s < HandTell)
            {
                // A thin warning line down the half it's about to sweep.
                Vector3 mark = new Vector3(mid + dir * reach * 0.55f, GroundY, 0);
                if (guide == null) guide = Bar(Core, mark, 0.06f, new Color(Molten.r, Molten.g, Molten.b, 0.5f));
                Stretch(guide, Core, mark, 0.06f + 0.05f * Mathf.PingPong(t * 8f, 1f));
            }
            else
            {
                if (guide != null) RemoveTransient(guide);
                guide = null;
                if (hand == null) hand = MakeBeam(Core, 1.2f, true);
                float u = (s - HandTell) / (HandSeconds - HandTell);
                // Out from under it to the edge and back, never quite under its own feet.
                float x = mid + dir * (0.9f + (reach - 0.9f) * Mathf.Sin(u * Mathf.PI));
                Vector3 end = BeamEnd(x, out _);
                DrawBeam(hand, Core, end, false, true);
                HitIfCrossed(Core, end);
            }
            yield return null;
        }
        if (hand != null) EndBeam(hand);
        if (guide != null) RemoveTransient(guide);
        Tick(0.9f);
        PlayIdle();
        yield return Wait(0.5f);
    }

    // The return leg's fan of gear teeth only fires from at least this high above the ronin.
    const float HatFanHeight = 4f;
}
