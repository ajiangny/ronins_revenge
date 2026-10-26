using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 2's warlord: a karakuri automaton wearing a dead pilgrim's hat and burial shroud, with a lantern-red heart
// and a long blade on a jointed arm. Everything it does keeps time to a ticking escapement. Its signature move, Hour
// Strikes, ends in a giant cleave that only a parry answers; sent back, it jams the gears.
public class Utsusemi : Boss
{
    // Per bar: it fights two, the second after its phase break.
    public const int StartingHealth = 120;

    static readonly Color Steel = new Color(0.55f, 0.68f, 0.95f);
    static readonly Color Gold = new Color(0.9f, 0.75f, 0.4f);
    static readonly Color CoreRed = new Color(1f, 0.22f, 0.18f);
    static readonly Color Lace = new Color(0.92f, 0.94f, 1f, 0.95f);
    static readonly Color Gunmetal = new Color(0.28f, 0.3f, 0.36f);
    // Its digital afterimages split into its own steel blue and gold, like a signal out of register.
    static readonly Color GhostBlue = new Color(0.4f, 0.62f, 1f);
    static readonly Color GhostGold = new Color(1f, 0.8f, 0.35f);

    enum Move { KasaWheel, Lattice, HourStrikes, PlumbLines, HeartLantern, Midnight }

    // Each value is (phase 1, phase 2): once unwound, its clock runs fast.
    float Recovery => secondPhase ? 0.45f : 0.9f;
    float ComboChance => secondPhase ? 0.85f : 0.65f;
    int MaxCombo => secondPhase ? 3 : 2;
    float StepTime => secondPhase ? 0.11f : 0.16f;
    const float StepLength = 0.9f;

    public override string Title => "UTSUSEMI";
    public override string Epithet => "The Hollow Pilgrim";
    public const string PhaseEpithet = "The Unwound Hour";
    public override Color Colour => Steel;

    public override string Line(BossLine line) => line switch
    {
        // Its story is in Story/Utsusemi.md; the defeat line's last words are the monk inside it.
        BossLine.Intro => "Tick... tock... The monk who wore this hat once gave you shelter. Now I walk every road he walked, looking for you. Yours... ends here.",
        BossLine.Phase => "Mainspring... unwound. I will not... stop.",
        BossLine.Defeat => "The bell... has... stopped... Run... boy...",
        BossLine.Stagger => "Gears... seized...",
        BossLine.KasaWheel => "Kasa.",
        BossLine.Lattice => "Unravel.",
        BossLine.HourStrikes => "Remember the hours... one... by one.",
        BossLine.PlumbLines => "Swing.",
        BossLine.HeartLantern => "Behold... my heart.",
        BossLine.Midnight => "Midnight.",
        _ => null,
    };

    bool secondPhase => SecondPhase;
    bool breaking;
    bool tock;
    int hatsOut;
    // The Kasa Wheel's sword in the second phase: wheeling from the throw until it's back (meanwhile it fights on
    // with only the bladeless moves), and thrown once it has left its hand.
    bool swordWheeling, swordThrown;
    // Hat off and sword out at once, patched together from its other idles; built the first time it's needed.
    Sprite[] bareUnarmedIdle;
    Move? lastMove;
    int movesSinceHours;
    // Phase 2 is the laser phase: a laser at least every other move, never the same one twice running.
    int movesSinceLaser;
    Move? lastLaser;
    SpriteRenderer eye;
    SpriteRenderer coreGlow;
    readonly List<Arrow> lattice = new List<Arrow>();
    readonly List<Beam> beams = new List<Beam>();
    // Its idle with the hat off, for while the hat is out; null keeps the hatted idle.
    Sprite[] bareIdle;

    // Its overdriven versions of the extra clips, worn with the phase art; any that is missing keeps the first.
    Sprite[] phaseBareIdle, phaseUnarmedIdle, phaseDash, phaseChop, phaseSlash, phaseSwordThrow;

    public void SetPhaseFrames(Sprite[] bare, Sprite[] unarmed, Sprite[] dash, Sprite[] chop, Sprite[] slash, Sprite[] swordThrow)
    {
        Sprite[] Some(Sprite[] f, int min) => f != null && f.Length > min ? f : null;
        phaseBareIdle = Some(bare, 0);
        phaseUnarmedIdle = Some(unarmed, 0);
        phaseDash = Some(dash, 1);
        phaseChop = Some(chop, ChopImpactFrame);
        phaseSlash = Some(slash, 0);
        phaseSwordThrow = Some(swordThrow, SwordReleaseFrame);
    }

    public void SetBareIdle(Sprite[] frames)
    {
        bareIdle = frames != null && frames.Length > 0 ? frames : null;
    }

    protected override bool Busy => breaking;
    protected override float StaggerSeconds => secondPhase ? 2f : 2.5f;
    // Its slit-eye is only exposed while the hat is off.
    protected override int DamageMultiplier => hatsOut > 0 ? 2 : 1;
    protected override Sprite[] IdleClip
    {
        get
        {
            bool bare = hatsOut > 0 && bareIdle != null;
            bool unarmed = (swordOut || swordThrown) && unarmedIdle != null;
            if (bare && unarmed && BareUnarmedIdle is Sprite[] both) return both;
            // Without that art the bare head wins: the missing hat is what the ronin is watching for.
            return bare ? bareIdle : unarmed ? unarmedIdle : art.idle;
        }
    }

    // Its unarmed stance with the hat region of its bare-headed idle swapped in.
    Sprite[] BareUnarmedIdle
    {
        get
        {
            if (bareUnarmedIdle == null && bareIdle != null && unarmedIdle != null && art.idle != null && art.idle.Length > 0)
            {
                Sprite patched = ProceduralSprites.Patched(unarmedIdle[0], art.idle[0], bareIdle[0], 3);
                bareUnarmedIdle = patched != null ? new[] { patched } : new Sprite[0];
            }
            return bareUnarmedIdle != null && bareUnarmedIdle.Length > 0 ? bareUnarmedIdle : null;
        }
    }
    Vector3 Head => new Vector3(Centre.x, sprite.bounds.max.y - sprite.bounds.size.y * 0.12f, 0);
    // The red slit-eye under the hat brim and the lantern heart in its chest, where its frames draw them.
    Vector3 Eye => OnSprite(57.5f, 38.5f);
    Vector3 Core => OnSprite(58f, 50.5f);
    Vector3 Shoulder => Centre + new Vector3(sprite.bounds.extents.x * 0.5f, sprite.bounds.size.y * 0.1f, 0);

    protected override IEnumerator Fight()
    {
        armored = false;
        // A fight restarted mid-laser (a stagger) clears the planted sword with the attack: it's back in hand.
        if (swordOut)
        {
            swordOut = false;
            plantedSword = null;
            PlayIdle();
        }
        while (true)
        {
            while (breaking) yield return null;
            // Winding: it stands still for a beat between combos. This is the opening.
            yield return Wait(Recovery);

            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            bool hourStruck = false;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                Move move = NextMove(hourStruck);
                // A short shuffle to a new spot before a move, not a long walk: the lasers walk to the centre
                // themselves, and a combo's follow-ups often go straight in.
                bool laser = move == Move.HeartLantern || move == Move.Midnight;
                if (!laser && (i == 0 || Random.value < 0.5f))
                {
                    yield return Stride(NearX(1.8f, 3.6f));
                    yield return Wait(0.05f);
                }

                lastMove = move;
                hourStruck |= move == Move.HourStrikes;
                movesSinceHours = move == Move.HourStrikes ? 0 : movesSinceHours + 1;
                movesSinceLaser = laser ? 0 : movesSinceLaser + 1;
                if (laser) lastLaser = move;
                switch (move)
                {
                    case Move.KasaWheel: yield return KasaWheel(); break;
                    case Move.Lattice: yield return LaceLattice(); break;
                    case Move.HourStrikes: yield return HourStrikes(); break;
                    case Move.PlumbLines: yield return PlumbLines(); break;
                    case Move.HeartLantern: yield return HeartLantern(); break;
                    case Move.Midnight: yield return Midnight(); break;
                }
            }
        }
    }

    // Never the same move twice in a row; the Hour Strikes come at most once a combo but at least every third move,
    // so the parry rhythm stays part of the fight. No new hat while one is still out. Once its core opens, the lasers
    // (the Heart Lantern and Midnight) take over: one at least every other move, alternating, with the lace and the
    // plumbs as rarer filler between them.
    Move NextMove(bool hourStruck)
    {
        // While its sword wheels it fights on with the moves that need no blade: the lace and the plumbs.
        if (swordWheeling) return lastMove == Move.PlumbLines ? Move.Lattice : lastMove == Move.Lattice ? Move.PlumbLines
            : Random.value < 0.5f ? Move.Lattice : Move.PlumbLines;
        if (secondPhase && movesSinceLaser >= 1)
        {
            if (lastLaser == Move.HeartLantern) return Move.Midnight;
            if (lastLaser == Move.Midnight) return Move.HeartLantern;
            return Random.value < 0.5f ? Move.HeartLantern : Move.Midnight;
        }
        if (!hourStruck && movesSinceHours >= 2 && lastMove != Move.HourStrikes) return Move.HourStrikes;
        var options = new List<Move> { Move.Lattice, Move.PlumbLines };
        if (hatsOut == 0) options.Add(Move.KasaWheel);
        if (!hourStruck) options.Add(Move.HourStrikes);
        if (secondPhase)
        {
            // The hat and the blade count twice, so the lace and plumbs come up less often between lasers.
            if (hatsOut == 0) options.Add(Move.KasaWheel);
            if (!hourStruck) options.Add(Move.HourStrikes);
        }
        if (lastMove is Move last) options.RemoveAll(m => m == last);
        if (options.Count == 0) options.Add(Move.Lattice);
        return options[Random.Range(0, options.Count)];
    }

    // The clock that times everything: a high tick and a lower tock in turn.
    void Tick(float volume = 0.4f)
    {
        Sfx.Play(Sfx.ClockTick, volume, tock ? 0.8f : 1f);
        tock = !tock;
    }

    // Stilt Stride: no blinking. It stalks to a new spot in stiff clockwork steps, one per tick.
    IEnumerator Stride(float x)
    {
        float distance = x - anchor.x;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(distance) / StepLength), 1, 6);
        float step = distance / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 from = anchor;
            Vector3 to = from + Vector3.right * step;
            // Two digital afterimages a step: one as it sets off and one halfway.
            DigitalGhost(0.3f);
            bool halfway = false;
            for (float t = 0; t < StepTime; t += Time.deltaTime * Tempo)
            {
                anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / StepTime));
                if (!halfway && t >= StepTime / 2f)
                {
                    halfway = true;
                    DigitalGhost(0.22f);
                }
                yield return null;
            }
            anchor = to;
            Tick(0.3f);
            yield return Wait(0.03f);
        }
    }

    // Its entrance: once it has descended, before its title card, the slit-eye under its hat brim charges and a thin
    // red beam rakes the row of blockers from end to end, burning a line of fire along it, and every blocker it
    // touches bursts apart. A show of force that can't hurt the ronin; from here on the only cover in this duel is the
    // sword it throws before its lasers.
    public override IEnumerator Entrance() => RazeCover();

    IEnumerator RazeCover()
    {
        var cover = new List<Blocker>(FindObjectsByType<Blocker>());
        if (cover.Count == 0) yield break;
        // The beam runs along the foot of the row, where the blockers stand: their wall's own height.
        Transform wall = cover[0].transform.parent;
        float rowY = wall != null ? wall.position.y : cover[0].transform.position.y;
        ElementFx.Glow(Eye, CoreRed, 1.2f, 3.5f, 0.8f, transform);
        Sfx.Play(Sfx.LanternCharge, 0.8f, 1.25f);
        for (int i = 0; i < 3; i++)
        {
            Beat();
            Tick(0.7f);
            yield return Wait(0.25f);
        }

        float dir = Random.value < 0.5f ? 1f : -1f;
        float from = dir > 0 ? LeftEdge + 0.3f : RightEdge - 0.3f;
        float to = dir > 0 ? RightEdge - 0.3f : LeftEdge + 0.3f;
        Beam beam = MakeBeam(Eye, 1f);
        Sfx.PlayLoop(Sfx.LanternBeam, 0.8f, 1.2f);
        const float seconds = 1.8f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            Vector3 end = BeamEnd(Eye, new Vector3(Mathf.Lerp(from, to, t / seconds), rowY, 0), out Blocker hit);
            DrawBeam(beam, Eye, end, false, hit == null);
            if (hit != null)
            {
                hit.Demolish();
                GroundFire.Light(new Vector3(end.x, rowY, 0));
                CameraShake.Add(0.12f);
            }
            yield return null;
        }
        EndBeam(beam);
        Sfx.Stop(Sfx.LanternBeam);
        // Anything the sweep's angle couldn't reach goes too.
        foreach (Blocker left in cover)
        {
            if (left != null) left.Demolish();
        }
        yield return Wait(0.4f);
    }

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
        Arrow hat = Instantiate(arrowPrefab, Head, Quaternion.identity);
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
            Kunai riposte = hat.Riposte;
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

    void RemoveTransient(SpriteRenderer thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    // A gold weight snapping off the hat's brim and dropping straight down.
    void Tassel(Vector3 from)
    {
        Arrow weight = Shoot(from + Vector3.down * 0.4f, Vector3.down * (secondPhase ? 7.5f : 6.5f));
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
            eye.transform.position = Head + Vector3.back * 0.3f;
            eye.transform.localScale = new Vector3(0.25f, 0.7f, 1f) / transform.lossyScale.x;
            eye.sprite = ProceduralSprites.Glow;
            eye.color = CoreRed;
            eye.sortingOrder = 45;
        }
        else if (!open && eye != null)
        {
            Destroy(eye.gameObject);
            eye = null;
        }
    }

    // Lace Lattice: the shroud flares and a curtain of lace diamonds drifts down with a gap or two in it. Each
    // diamond breaks in one hit, so the ronin can cut his own way through. Two offset layers in the second phase.
    IEnumerator LaceLattice()
    {
        if (Random.value < 0.5f) Speak(BossLine.Lattice);
        // Its attack clip has the sword in hand; with the sword wheeling it flares its shroud from its stance.
        if (!swordThrown) PlayAction(art.attack);
        ElementFx.Glow(Centre, Lace, 1f, 5f, 0.5f, transform);
        Sfx.Play(Sfx.FrostChill, 0.6f, 0.8f);
        yield return Wait(0.35f);

        int layers = secondPhase ? 2 : 1;
        for (int layer = 0; layer < layers; layer++)
        {
            if (layer > 0) yield return Wait(0.6f);
            SpinLattice(layer * 0.55f, secondPhase ? 1 : 2);
            Sfx.Play(Sfx.WardShatter, 0.5f, 1.8f);
        }
    }

    void SpinLattice(float offset, int gaps)
    {
        const float spacing = 1.1f;
        const int rows = 3;
        float speed = secondPhase ? 3f : 2.6f;
        float left = LeftEdge + 0.4f + offset;
        int columns = Mathf.FloorToInt((RightEdge - 0.4f - left) / spacing) + 1;
        // Each gap is two columns wide, enough for the ronin to slip through.
        var open = new HashSet<int>();
        for (int g = 0; g < gaps; g++)
        {
            int c = Random.Range(1, columns - 2);
            open.Add(c);
            open.Add(c + 1);
        }
        Sprite[] frames = Fx.Frames(Fx.UtsuLace);
        float y0 = anchor.y - 1.6f;
        for (int row = 0; row < rows; row++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (open.Contains(c)) continue;
                float x = left + c * spacing + (row % 2 == 1 ? spacing / 2f : 0f);
                Arrow diamond = Shoot(new Vector3(x, y0 + row * 0.9f, 0), Vector3.down * speed);
                if (frames != null) diamond.Restyle(frames, 8f, Color.white, new Vector2(0.7f, 0.7f), 0.22f, new Color(0.8f, 0.85f, 1f, 0.25f), 0.7f);
                else
                {
                    diamond.Restyle(ProceduralSprites.White, Lace, new Vector2(0.42f, 0.42f), 0.22f, new Color(0.8f, 0.85f, 1f, 0.3f), 0.7f);
                    diamond.Art.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                }
                diamond.PassesCover = true;
                lattice.Add(diamond);
            }
        }
        lattice.RemoveAll(a => a == null);
    }

    // Hour Strikes: one hour at a time, a thin red sight line from its eye tracks the ronin, then locks on where he
    // stands, and a moment later its eye lasers the spot, burning him if he hasn't stepped off. Then it counts the
    // hours again with its blade: for each, the sight line hunts him down and locks on, the hour's numeral (I, II,
    // III...) rises on the spot, and after a short window it dashes down out of its row onto it, chops it with a
    // ground-cracking blow and dashes back up. Then, back in its row, it strikes the next hour: that numeral rises under
    // the ronin and follows him while its core glows and the blade draws back over three ticks, and a giant crescent
    // cleave flies at him. It hunts him, breaks his shots and cuts through cover, so only a parry answers it: sent
    // back, it jams the gears and staggers the automaton. In the second phase there are five hours, faster.
    IEnumerator HourStrikes()
    {
        int count = secondPhase ? 5 : 3;
        // The dashes come fast: a short tick before each, and a quick blow.
        float beat = secondPhase ? 0.18f : 0.24f;
        Vector3 row = anchor;

        // The announcement, in super armour: it calls the move and stands its ground in a gold shell while it lasers
        // the hours in one at a time. Nothing staggers or freezes it until the shell drops.
        Speak(BossLine.HourStrikes);
        armored = true;
        SpriteRenderer[] shell = ArmorShell();
        Sfx.Play(Sfx.GearGrind, 0.6f, 1.4f);
        yield return HoldArmor(shell, 0.3f);
        for (int i = 0; i < count; i++) yield return LaserHour(shell);
        yield return HoldArmor(shell, 0.3f);
        Tick(0.4f);
        yield return HoldArmor(shell, 0.25f);
        armored = false;
        foreach (SpriteRenderer part in shell) RemoveTransient(part);

        // The dashes: each one locks onto wherever the ronin stands now.
        for (int hour = 1; hour <= count; hour++)
        {
            Beat();
            Tick(0.9f);
            yield return Wait(beat);
            yield return LockOnDash(hour, row);
            PlayIdle();
        }

        Beat();
        yield return StrikeTheHour(count + 1);
        PlayIdle();
    }

    // One hour lasered where the ronin stands: a thin sight line from its eye follows him, then locks on and
    // brightens. He has a moment to step off before its eye's laser strikes the spot; it burns him if he's still there.
    IEnumerator LaserHour(SpriteRenderer[] shell)
    {
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        SpriteRenderer sight = Bar(Eye, Spot(x), 0.05f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.4f));
        yield return HoldArmor(shell, secondPhase ? 0.45f : 0.6f, () =>
        {
            x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
            Stretch(sight, Eye, Spot(x), 0.05f);
        });

        // Locked: the sight line brightens, and the dodge window opens.
        Tick(0.7f);
        Vector3 spot = Spot(x);
        sight.color = new Color(1f, 0.55f, 0.5f, 0.85f);
        yield return HoldArmor(shell, secondPhase ? 0.25f : 0.35f, () => Stretch(sight, Eye, spot, 0.07f));
        RemoveTransient(sight);

        Beam beam = MakeBeam(Eye, 0.8f);
        Sfx.Play(Sfx.LanternCharge, 0.4f, 2f);
        CameraShake.Add(0.1f);
        yield return HoldArmor(shell, 0.3f, () =>
        {
            if (beam.body == null) return;
            DrawBeam(beam, Eye, spot, false, false);
            HitIfCrossed(Eye, spot);
        });
        EndBeam(beam);
        yield return HoldArmor(shell, 0.1f);
    }

    // One hour struck where the ronin stands: a red sight line from its eye follows him, then locks on, and the
    // hour's numeral rises there. After a short window to step off, it dashes down onto the spot and chops it.
    IEnumerator LockOnDash(int hour, Vector3 row)
    {
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        SpriteRenderer sight = Bar(Eye, Spot(x), 0.05f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.4f));
        float track = secondPhase ? 0.4f : 0.5f;
        for (float t = 0; t < track; t += Time.deltaTime * Tempo)
        {
            x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
            Stretch(sight, Eye, Spot(x), 0.05f);
            yield return null;
        }

        // Locked: the hour's numeral rises where he stood, and the dodge window opens.
        var mark = new HourMark(this, hour, x);
        mark.Show();
        Tick(0.7f);
        Vector3 spot = Spot(x);
        sight.color = new Color(1f, 0.55f, 0.5f, 0.85f);
        float window = secondPhase ? 0.3f : 0.4f;
        for (float t = 0; t < window; t += Time.deltaTime * Tempo)
        {
            Stretch(sight, Eye, spot, 0.07f);
            yield return null;
        }
        RemoveTransient(sight);

        yield return Dash(new Vector3(x, Landing(), row.z), HourDashTime);
        yield return Chop(x, mark, 36f, 0.08f);
        yield return Dash(row, HourDashTime);
    }

    Vector3 Spot(float x) => new Vector3(x, GroundY + 0.15f, 0);

    bool armored;
    protected override bool SuperArmor => armored;
    // Its armour shrugs off 95% of every blow.
    protected override float ArmorDamageTaken => 0.05f;

    // The armour's look: a gold outline, made of solid gold silhouettes of its sprite nudged one sprite pixel out in
    // eight directions and drawn just behind it, so only a thin gold edge shows around the figure.
    static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1),
    };

    SpriteRenderer[] ArmorShell()
    {
        Material flat = Fx.Library != null ? Fx.Library.flashMaterial : null;
        if (flat == null) return new SpriteRenderer[0];
        float pixel = 1f / (sprite.sprite != null ? sprite.sprite.pixelsPerUnit : 48f);
        var shell = new SpriteRenderer[OutlineOffsets.Length];
        for (int i = 0; i < shell.Length; i++)
        {
            shell[i] = new GameObject("ArmorOutline").AddComponent<SpriteRenderer>();
            shell[i].transform.SetParent(sprite.transform, false);
            shell[i].transform.localPosition = (Vector3)(OutlineOffsets[i] * pixel);
            shell[i].sharedMaterial = flat;
            shell[i].sortingOrder = sprite.sortingOrder - 1;
            transient.Add(shell[i].gameObject);
        }
        ElementFx.Glow(Centre, Gold, 1f, 3.5f, 0.4f, transform);
        return shell;
    }

    // Made on first use: Unity won't build one in a field initializer.
    MaterialPropertyBlock outlineColour;
    static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

    // Real time, not boss time: the armour ignores a freeze.
    IEnumerator HoldArmor(SpriteRenderer[] shell, float seconds, System.Action eachFrame = null)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            Beat();
            eachFrame?.Invoke();
            // A shimmer between deep gold and pale gold.
            float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            if (outlineColour == null) outlineColour = new MaterialPropertyBlock();
            outlineColour.SetColor(FlashColor, Color.Lerp(new Color(1f, 0.7f, 0.15f), new Color(1f, 0.93f, 0.6f), shimmer));
            foreach (SpriteRenderer edge in shell)
            {
                if (edge == null) continue;
                edge.sprite = sprite.sprite;
                edge.flipX = sprite.flipX;
                edge.SetPropertyBlock(outlineColour);
            }
            yield return null;
        }
    }

    // The feet in its frames stand on this row of the 116-px canvas.
    const float FeetPx = 98f;

    // Where its anchor sits when its feet stand on the ronin's ground.
    float Landing() => anchor.y + (GroundY + 0.15f) - OnSprite(0, FeetPx).y;

    // A point given in the sprite frame's own pixels (from its top-left), so effects meet the art.
    Vector3 OnSprite(float px, float py)
    {
        Bounds b = sprite.bounds;
        Rect frame = sprite.sprite != null ? sprite.sprite.rect : new Rect(0, 0, 116, 116);
        float u = px / frame.width, v = 1f - py / frame.height;
        if (sprite.flipX) u = 1f - u;
        return new Vector3(b.min.x + u * b.size.x, b.min.y + v * b.size.y, 0);
    }

    // A straight clockwork dash, eased hard into a dead stop, leaving digital afterimages along the way.
    // How long each Hour Strikes dash takes, down to an hour or back up to the row.
    const float HourDashTime = 0.11f;

    IEnumerator Dash(Vector3 to, float seconds = 0.2f)
    {
        Vector3 from = anchor;
        PlayFrames(dashFrames, 1, -1, true, 30f);
        Sfx.Play(Sfx.ParrySwing, 0.6f, 1.4f);
        int ghosts = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            if (k >= ghosts / 3f && ghosts < 3)
            {
                DigitalGhost(0.38f - ghosts * 0.08f);
                ghosts++;
            }
            anchor = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k) * (1f - k));
            yield return null;
        }
        anchor = to;
        Tick(0.6f);
    }

    // A digital afterimage where it stands now: a flat silhouette split into steel blue and gold a few pixels apart,
    // nudged sideways a pixel or two like a bad signal, that blinks and fades out in hard steps. Without the flash
    // material a plain blue-tinted copy stands in.
    void DigitalGhost(float alpha)
    {
        Material flat = Fx.Library != null ? Fx.Library.flashMaterial : null;
        if (flat == null || sprite.sprite == null)
        {
            Afterimage(new Color(GhostBlue.r, GhostBlue.g, GhostBlue.b, alpha));
            return;
        }
        float pixel = sprite.transform.lossyScale.x / sprite.sprite.pixelsPerUnit;
        float jitter = Random.Range(-2, 3) * pixel;
        SpawnGhost(flat, GhostBlue, alpha, jitter - 3f * pixel, pixel);
        SpawnGhost(flat, GhostGold, alpha * 0.7f, jitter + 3f * pixel, pixel);
    }

    void SpawnGhost(Material flat, Color color, float alpha, float offsetX, float pixel)
    {
        var ghost = new GameObject("DigitalGhost").AddComponent<SpriteRenderer>();
        ghost.transform.SetPositionAndRotation(sprite.transform.position + Vector3.right * offsetX, sprite.transform.rotation);
        ghost.transform.localScale = sprite.transform.lossyScale;
        ghost.sprite = sprite.sprite;
        ghost.flipX = sprite.flipX;
        ghost.sharedMaterial = flat;
        ghost.sortingOrder = sprite.sortingOrder - 1;
        ghost.gameObject.AddComponent<GlitchFade>().Begin(color, alpha, 0.35f, pixel);
    }

    // Fades a digital afterimage in three hard steps, dropping out for a frame now and then and tearing a couple of
    // pixels sideways, like a bad signal.
    class GlitchFade : MonoBehaviour
    {
        SpriteRenderer target;
        MaterialPropertyBlock block;
        Color color;
        float alpha, seconds, age, pixel;
        Vector3 home;

        public void Begin(Color tint, float startAlpha, float lifetime, float pixelSize)
        {
            target = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
            color = tint;
            alpha = startAlpha;
            seconds = lifetime;
            pixel = pixelSize;
            home = transform.position;
            Apply();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        void Apply()
        {
            float stepped = Mathf.Ceil((1f - age / seconds) * 3f) / 3f;
            bool dropped = Random.value < 0.15f;
            transform.position = home + Vector3.right * (Random.value < 0.25f ? Random.Range(-2, 3) * pixel : 0f);
            block.SetColor(FlashColor, new Color(color.r, color.g, color.b, dropped ? 0f : alpha * stepped));
            target.SetPropertyBlock(block);
        }
    }

    const float ChopReach = 0.95f;
    // The chop's blade meets the ground on this frame.
    const int ChopImpactFrame = 5;

    // fps is how fast the chop clip plays, and so how soon the blow lands; recover is the pause after it.
    IEnumerator Chop(float x, HourMark mark, float fps = 20f, float recover = 0.18f)
    {
        PlayFrames(chopFrames, 1, -1, true, fps);
        yield return Wait(chopFrames != null ? (ChopImpactFrame - 1) / fps : 0.2f);

        Vector3 point = new Vector3(x, GroundY + 0.15f, 0);
        if (Mathf.Abs(ronin.Bounds.center.x - x) < ChopReach)
        {
            ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
        }
        if (Fx.Play(Fx.UtsuChop, point, Color.white, 2.4f) == null) Fx.Play(Fx.Impact, point, CoreRed, 1.2f);
        ElementFx.Glow(point, Color.white, 0.6f, 1.8f, 0.15f);
        Sfx.Play(Sfx.BladeChop, 1f, Random.Range(0.92f, 1.08f));
        CameraShake.Add(0.2f);
        mark?.Struck();
        yield return Wait(recover);
    }

    // The hour slash draws the blade back by this frame, holds it through the ticks, and lets the cleave go on the
    // release frame, as its swing passes the front.
    const int SlashDrawnFrame = 5;
    const int SlashReleaseFrame = 7;

    // The hour: its numeral under the ronin follows him for three ticks while the core glows and the blade is drawn
    // back, then flares red as the swing flings a giant crescent cleave at him.
    IEnumerator StrikeTheHour(int hour)
    {
        var mark = new HourMark(this, hour, ronin.Bounds.center.x);
        mark.Show();
        Sfx.Play(Sfx.LanternCharge, 0.8f, 1.3f);
        PlayFrames(slashFrames, 1, SlashDrawnFrame, true, 14f);

        float tickTime = secondPhase ? 0.32f : 0.42f;
        for (int i = 0; i < 3; i++)
        {
            Tick(1f);
            ElementFx.Glow(Core, i == 2 ? Color.white : CoreRed, 0.7f, 1.6f + i * 0.6f, tickTime * 0.8f, transform);
            for (float t = 0; t < tickTime; t += Time.deltaTime * Tempo)
            {
                mark.MoveTo(ronin.Bounds.center.x);
                yield return null;
            }
        }
        mark.Struck();

        const float swingFps = 18f;
        PlayFrames(slashFrames, SlashDrawnFrame + 1, -1, false, swingFps);
        yield return Wait(slashFrames != null ? (SlashReleaseFrame - SlashDrawnFrame - 1) / swingFps : 0.05f);

        Vector3 from = OnSprite(58f, FeetPx) + Vector3.down * 0.6f;
        Arrow cleave = Shoot(from, (ronin.Bounds.center - from).normalized * HourSpeed);
        Sprite[] frames = Fx.Frames(Fx.UtsuHourSlash);
        if (frames != null) cleave.Restyle(frames, 14f, Color.white, new Vector2(5.6f, 3.7f), 1.1f, new Color(1f, 0.2f, 0.18f, 0.6f), 4.5f);
        else cleave.Restyle(ProceduralSprites.White, new Color(0.9f, 0.93f, 1f), new Vector2(5.4f, 0.5f), 1.1f, new Color(1f, 0.2f, 0.18f, 0.6f), 4.5f);
        cleave.MakeUnbreakable();
        cleave.MakeReflectable(transform);
        StartCoroutine(Pursue(cleave, HourSpeed));
        Sfx.Play(Sfx.HourSlash, 1f);
        Sfx.Play(Sfx.MidnightChime, 0.6f, 1.4f);
        CameraShake.Add(0.35f);
        ElementFx.Glow(from, Color.white, 0.8f, 3f, 0.25f);
        yield return Wait(0.4f);
    }

    // Fast, but it turns as fast as the ronin runs: there's no outrunning it, only parrying it.
    const float HourSpeed = 11f;

    // Its Hour Strikes frames; without them it uses its thrust frames.
    Sprite[] dashFrames, chopFrames, slashFrames;

    public void SetSwordFrames(Sprite[] dash, Sprite[] chop, Sprite[] slash)
    {
        dashFrames = dash != null && dash.Length > 1 ? dash : null;
        chopFrames = chop != null && chop.Length > ChopImpactFrame ? chop : null;
        slashFrames = slash != null && slash.Length > SlashReleaseFrame ? slash : null;
    }

    // Plays frames first..last of a clip (last -1 for the end), then holds its final frame or returns to the idle.
    void PlayFrames(Sprite[] clip, int first, int last, bool hold, float fps)
    {
        if (clip == null)
        {
            PlayAction(art.attack);
            return;
        }
        if (last < 0 || last >= clip.Length) last = clip.Length - 1;
        var frames = new Sprite[last - first + 1];
        System.Array.Copy(clip, first, frames, 0, frames.Length);
        if (hold) animator.Play(frames, false, () => animator.Play(new[] { frames[frames.Length - 1] }, true), fps);
        else animator.Play(frames, false, PlayIdle, fps);
    }

    // The warning where a dash or a thrown sword will land: a brass Roman numeral (I to V) standing on a brass ring on
    // the ground, glowing red-hot at the edges, which rises in with a flash and flares red when the blow lands. Without
    // the numeral art it's drawn from bars over a soft pool of light: I and V strokes are all the first six hours need.
    class HourMark
    {
        const float Height = 0.8f;
        // The numeral art is drawn this many world units wide, about the reach of the blow it marks.
        const float ArtWidth = 1.9f;
        public float x { get; private set; }
        readonly Utsusemi owner;
        readonly int hour;
        readonly List<SpriteRenderer> strokes = new List<SpriteRenderer>();
        SpriteRenderer pool;

        public HourMark(Utsusemi owner, int hour, float x)
        {
            this.owner = owner;
            this.hour = hour;
            this.x = x;
        }

        public void Show() => Show(Color.white);

        // Slides the numeral along the ground to follow the ronin.
        public void MoveTo(float newX)
        {
            Vector3 shift = Vector3.right * (newX - x);
            x = newX;
            foreach (SpriteRenderer part in strokes)
            {
                if (part != null) part.transform.position += shift;
            }
            if (pool != null) pool.transform.position += shift;
        }

        void Show(Color tint)
        {
            Hide(0f);
            Vector3 foot = new Vector3(x, owner.GroundY + 0.15f, 0);
            Sprite[] numerals = Fx.Frames(Fx.UtsuNumerals);
            if (numerals != null)
            {
                // Its ring sits on the ground line, a touch below the feet so it reads as lying flat.
                var art = new GameObject("HourNumeral").AddComponent<SpriteRenderer>();
                art.sprite = numerals[Mathf.Clamp(hour - 1, 0, numerals.Length - 1)];
                art.transform.position = new Vector3(x, foot.y - 0.2f, 0);
                art.transform.localScale = Vector3.one * (ArtWidth / art.sprite.bounds.size.x);
                art.color = tint;
                art.sortingOrder = 8;
                owner.transient.Add(art.gameObject);
                strokes.Add(art);
                ElementFx.Glow(foot + Vector3.up * 0.5f, tint == Color.white ? Gold : tint, 0.5f, 1.6f, 0.3f);
                return;
            }

            Color colour = tint == Color.white ? Gold : tint;
            pool = owner.Bar(foot, foot, 1f, new Color(colour.r, colour.g, colour.b, 0.45f), 6);
            pool.sprite = ProceduralSprites.Glow;
            pool.transform.rotation = Quaternion.identity;
            pool.transform.localScale = new Vector3(ChopReach * 2.2f, 0.45f, 1f);

            string glyphs = Roman(hour);
            float stroke = Height * 0.16f, vWidth = Height * 0.6f, gap = Height * 0.18f;
            float width = 0;
            foreach (char c in glyphs) width += (c == 'V' ? vWidth : stroke) + gap;
            width -= gap;
            float left = x - width / 2f;
            float bottom = foot.y + 0.25f, top = bottom + Height;
            foreach (char c in glyphs)
            {
                if (c == 'V')
                {
                    Stroke(new Vector3(left, top, 0), new Vector3(left + vWidth / 2f, bottom, 0), stroke, colour);
                    Stroke(new Vector3(left + vWidth, top, 0), new Vector3(left + vWidth / 2f, bottom, 0), stroke, colour);
                    left += vWidth + gap;
                }
                else
                {
                    Stroke(new Vector3(left + stroke / 2f, top, 0), new Vector3(left + stroke / 2f, bottom, 0), stroke, colour);
                    left += stroke + gap;
                }
            }
            ElementFx.Glow(new Vector3(x, bottom + Height / 2f, 0), colour, 0.4f, 1.4f, 0.3f);
        }

        static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "VI" };

        // A gold stroke on a dark outline, so it reads against the grass.
        void Stroke(Vector3 from, Vector3 to, float width, Color colour)
        {
            Vector3 along = (to - from).normalized * width * 0.5f;
            strokes.Add(owner.Bar(from - along, to + along, width * 2f, new Color(0.08f, 0.06f, 0.04f, 0.9f), 7));
            strokes.Add(owner.Bar(from, to, width, colour, 8));
        }

        // The hour it was: it flares red under the blow, then fades.
        public void Struck()
        {
            Show(new Color(1f, 0.45f, 0.35f));
            Hide(0.5f);
        }

        public void Hide(float seconds)
        {
            foreach (SpriteRenderer part in strokes) Fade(part, seconds);
            Fade(pool, seconds);
            strokes.Clear();
            pool = null;
        }

        void Fade(SpriteRenderer part, float seconds)
        {
            if (part == null) return;
            owner.transient.Remove(part.gameObject);
            if (seconds > 0) Fader.Fade(part, seconds);
            else Destroy(part.gameObject);
        }
    }

    // Plumb Lines: gold clockwork portals open in its row, one by one, behind it. Threads show their full reach, then
    // a gold weight drops from each and swings as a pendulum through the ronin's lane, the swings rippling along the
    // row like a pendulum-wave toy. One hangs right above him. A hit on a weight cuts it loose; when the time is up
    // the swings die down and the rest are reeled back up into their portals, which close behind them.
    IEnumerator PlumbLines()
    {
        if (Random.value < 0.5f) Speak(BossLine.PlumbLines);
        int count = secondPhase ? 5 : Random.Range(3, 5);
        // The portals hang just above its hat, whose brim tops out near this row of its frames.
        float pivotY = OnSprite(58f, 28f).y + 0.35f;
        float roninX = ronin.Bounds.center.x;
        const float margin = 2.5f;
        // The first hangs over the ronin; the rest spread out either side of him. Left to right, for the wave.
        // It doesn't stand and watch: it strides to fresh spots under the portals from the moment they open until the
        // swings die down.
        float swing = secondPhase ? 3.6f : 3.2f;
        const float drop = 0.6f, fadeIn = 0.8f, damp = 0.6f, reel = 0.5f;
        pacing = StartCoroutine(Pace(count * 0.12f + 0.6f + drop + swing + damp));
        var xs = new List<float>();
        for (int i = 0; i < count; i++)
        {
            float offset = i == 0 ? 0f : ((i + 1) / 2) * 4.5f * (i % 2 == 1 ? 1f : -1f) + Random.Range(-0.7f, 0.7f);
            xs.Add(Mathf.Clamp(roninX + offset, LeftEdge + margin, RightEdge - margin));
        }
        xs.Sort();
        var pivots = new Vector3[count];
        for (int i = 0; i < count; i++) pivots[i] = new Vector3(xs[i], pivotY, 0);

        // The portals open in turn, behind it.
        var portals = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            portals[i] = OpenPortal(pivots[i]);
            Tick(0.5f);
            yield return Wait(0.12f);
        }

        // Every thread is the same length, bottoming out just below the ronin's middle, so each swing crosses his
        // height over most of its arc.
        float length = pivotY - (ronin.Bounds.center.y - 0.6f);
        var threads = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            threads[i] = Bar(pivots[i], pivots[i] + Vector3.down * length, 0.06f, new Color(Gold.r, Gold.g, Gold.b, 0.3f), PortalOrder);
        }
        Tick(0.6f);
        yield return Wait(0.3f);
        Tick(0.6f);
        yield return Wait(0.3f);

        var weights = new Arrow[count];
        Sprite[] frames = Fx.Frames(Fx.UtsuPlumb);
        for (int i = 0; i < count; i++)
        {
            weights[i] = Shoot(pivots[i], Vector3.down * 0.1f);
            if (frames != null) weights[i].Restyle(frames, 8f, Color.white, new Vector2(0.9f, 1.07f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.35f), 1.1f);
            else weights[i].Restyle(ProceduralSprites.Disc, Gold, new Vector2(0.6f, 0.8f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 1.1f);
            threads[i].color = new Color(0.95f, 0.95f, 1f, 0.6f);
        }
        // Neighbours' arcs cross, and shots break each other, so the weights must pass through one another.
        IgnoreEachOther(new List<Arrow>(weights));
        Sfx.Play(Sfx.ShurikenThrow, 0.7f, 0.4f);

        // Where each weight hung last, so a cut thread snaps where it was.
        var lastHang = new Vector3[count];
        const float amplitude = 28f * Mathf.Deg2Rad;
        // Each swings this far behind its left-hand neighbour, so a ripple runs along the row.
        const float waveStep = 0.7f;
        float omega = Mathf.PI * 2f / (secondPhase ? 2f : 2.4f);
        float halfPeriod = Mathf.PI / omega;
        float nextTick = drop + halfPeriod * 0.5f;
        float total = drop + swing + damp + reel;
        // Steered once per physics step: a velocity worked out from a render frame's delta overshoots the next step.
        for (float t = 0; t < total; t += Time.fixedDeltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            float swingT = t - drop;
            // Faded in smoothly after the drop, and damped to a stop before the reel.
            float envelope = Mathf.SmoothStep(0, 1, swingT / fadeIn) * (1f - Mathf.SmoothStep(0, 1, (swingT - swing) / damp));
            // Out with a slight settle at the bottom; wound back in at the end.
            float reach;
            if (t < drop) reach = DropEase(t / drop);
            else if (swingT < swing + damp) reach = 1f;
            else reach = 1f - Mathf.Pow(Mathf.Clamp01((swingT - swing - damp) / reel), 2f);
            // The lead weight's tick at each end of its swing keeps the clock's time.
            if (swingT > 0 && t >= nextTick && swingT < swing)
            {
                nextTick += halfPeriod;
                Tick(0.3f);
            }
            for (int i = 0; i < count; i++)
            {
                float angle = t < drop ? 0f : amplitude * envelope * Mathf.Sin(omega * swingT - i * waveStep);
                Vector3 hang = new Vector3(Mathf.Sin(angle), -Mathf.Cos(angle), 0) * length * reach;
                if (weights[i] == null)
                {
                    // Cut loose: the thread snaps and whips back up into its portal, which closes after it.
                    if (threads[i] == null) continue;
                    StartCoroutine(SnapThread(threads[i], pivots[i], lastHang[i], portals[i]));
                    threads[i] = null;
                    portals[i] = null;
                    continue;
                }
                lastHang[i] = weights[i].transform.position;
                Vector3 target = pivots[i] + hang;
                weights[i].Steer((target - weights[i].transform.position) / Time.fixedDeltaTime);
                weights[i].Art.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                Stretch(threads[i], pivots[i], weights[i].transform.position, 0.06f);
                // A gold streak trails each weight as it drops.
                if (t < drop && Random.value < 0.5f) Afterimage(weights[i].Art, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            }
            yield return new WaitForFixedUpdate();
        }

        StopPacing();
        // Back in: each weight vanishes into its portal with a glint, and the portals close.
        for (int i = 0; i < count; i++)
        {
            if (weights[i] != null)
            {
                ElementFx.Glow(pivots[i], Gold, 0.6f, 1.4f, 0.25f);
                Destroy(weights[i].gameObject);
            }
            RemoveTransient(threads[i]);
        }
        Sfx.Play(Sfx.HatClang, 0.35f, 1.6f);
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(ClosePortal(portals[i]));
            yield return Wait(0.06f);
        }
    }

    // A cut thread: it parts at `at` with a spray of gold and a high ting, the loose end drops away tumbling, and the
    // rest whips back up into its portal, wriggling as it goes, and the portal flares as it swallows it and closes.
    IEnumerator SnapThread(SpriteRenderer thread, Vector3 pivot, Vector3 at, Transform portal)
    {
        Fx.Play(Fx.Impact, at, Gold, 0.7f);
        ElementFx.Glow(at, Gold, 0.3f, 1.2f, 0.2f);
        Debris.BurstSprites(at, art.deathParticles, Gold, 5, 0.4f, 3.5f);
        Sfx.Play(Sfx.ParryClang, 0.3f, 2f);
        StartCoroutine(DropLooseEnd(at, at - pivot));

        Vector3 reach = at - pivot;
        Vector3 across = new Vector3(-reach.y, reach.x, 0).normalized;
        const float seconds = 0.35f;
        for (float t = 0; t < seconds && thread != null; t += Time.deltaTime)
        {
            float k = t / seconds;
            float left = (1f - k) * (1f - k);
            // A dying wriggle sideways, like a cut string recoiling.
            Vector3 tip = pivot + reach * left + across * Mathf.Sin(k * Mathf.PI * 4f) * 0.3f * (1f - k);
            Stretch(thread, pivot, tip, 0.05f);
            if (Random.value < 0.5f) Afterimage(thread, new Color(Gold.r, Gold.g, Gold.b, 0.3f));
            yield return null;
        }
        RemoveTransient(thread);
        ElementFx.Glow(pivot, Gold, 0.4f, 1.6f, 0.25f);
        Fx.Play(Fx.Impact, pivot, Gold, 0.5f);
        Tick(0.35f);
        yield return ClosePortal(portal);
    }

    // The thread's loose end below the cut: a short gold strand that tumbles down and fades.
    IEnumerator DropLooseEnd(Vector3 at, Vector3 along)
    {
        Vector3 piece = along.normalized * 0.6f;
        SpriteRenderer end = Bar(at, at - piece, 0.05f, new Color(0.95f, 0.95f, 1f, 0.6f), PortalOrder);
        Vector3 velocity = new Vector3(Random.Range(-1.5f, 1.5f), 1.5f, 0);
        float spin = Random.Range(-540f, 540f);
        const float seconds = 0.5f;
        for (float t = 0; t < seconds && end != null; t += Time.deltaTime)
        {
            velocity += Vector3.down * 18f * Time.deltaTime;
            end.transform.position += velocity * Time.deltaTime;
            end.transform.Rotate(0, 0, spin * Time.deltaTime);
            Color c = end.color;
            end.color = new Color(c.r, c.g, c.b, 0.6f * (1f - t / seconds));
            yield return null;
        }
        RemoveTransient(end);
    }

    // The portals and their threads sit just behind the automaton's own sprite.
    const int PortalOrder = 4;

    Coroutine pacing;

    // Strides about while an attack plays out by itself, so it isn't a still target. Started beside the fight, so a
    // stagger (OnStaggered) or the attack's end stops it.
    IEnumerator Pace(float seconds)
    {
        float until = Time.time + seconds;
        yield return Wait(0.2f);
        while (Time.time < until - 0.3f)
        {
            yield return Stride(RandomX(2.5f));
            yield return Wait(Random.Range(0.25f, 0.5f));
        }
        pacing = null;
    }

    void StopPacing()
    {
        if (pacing != null) StopCoroutine(pacing);
        pacing = null;
    }
    const float PortalSize = 1.3f;

    // Out fast, a touch past full length, then settling back.
    static float DropEase(float k)
    {
        k = Mathf.Clamp01(k);
        float overshoot = 1.06f;
        return k < 0.8f ? overshoot * (1f - Mathf.Pow(1f - k / 0.8f, 3f)) : Mathf.Lerp(overshoot, 1f, (k - 0.8f) / 0.2f);
    }

    // A gold clockwork portal opening in the air: it grows from a point with a small flash.
    Transform OpenPortal(Vector3 at)
    {
        FxPlayer portal = Fx.Play(Fx.UtsuPlumbPortal, at, Color.white, PortalSize, null, PortalOrder);
        Transform shape;
        if (portal != null) shape = portal.transform;
        else
        {
            SpriteRenderer disc = new GameObject("Portal").AddComponent<SpriteRenderer>();
            disc.sprite = ProceduralSprites.Glow;
            disc.color = new Color(Gold.r, Gold.g, Gold.b, 0.8f);
            disc.sortingOrder = PortalOrder;
            disc.transform.position = new Vector3(at.x, at.y, -1f);
            shape = disc.transform;
        }
        transient.Add(shape.gameObject);
        ElementFx.Glow(at, Gold, 0.5f, 1.6f, 0.3f);
        StartCoroutine(ScalePortal(shape, 0f, shape.localScale.x, 0.2f, false));
        return shape;
    }

    IEnumerator ClosePortal(Transform portal)
    {
        if (portal == null) yield break;
        yield return ScalePortal(portal, portal.localScale.x, 0f, 0.2f, true);
    }

    IEnumerator ScalePortal(Transform portal, float from, float to, float seconds, bool remove)
    {
        for (float t = 0; t < seconds && portal != null; t += Time.deltaTime)
        {
            portal.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
            yield return null;
        }
        if (portal == null) yield break;
        portal.localScale = Vector3.one * to;
        if (remove)
        {
            transient.Remove(portal.gameObject);
            Destroy(portal.gameObject);
        }
    }

    // Heart Lantern (second phase only): first it hurls its sword at the ronin, and the sword plants itself in the
    // ground as the one piece of cover there is. Sparks pour into the core as it charges, then a beam sweeps the
    // ground from one side to the other, flashes a warning and sweeps back; only the sword (or a blocker) stops it.
    // Then it dashes down to the sword and wrenches it out with a strike, so the ronin must leave its shelter.
    IEnumerator HeartLantern()
    {
        yield return Stride(CameraShake.Center.x);
        Speak(BossLine.HeartLantern);
        yield return ThrowSword(SwordLanding());
        yield return Charge();

        float dir = Random.value < 0.5f ? 1f : -1f;
        float from = dir > 0 ? LeftEdge + 0.5f : RightEdge - 0.5f;
        float to = dir > 0 ? RightEdge - 0.5f : LeftEdge + 0.5f;

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
        if (Mathf.Abs(ronin.Bounds.center.x - x) < 0.6f) ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
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
        SpriteRenderer ward = Kunai.CreateGlow(sword.transform, 1f, 3, new Color(SwordGlow.r, SwordGlow.g, SwordGlow.b, 0.5f));
        ward.transform.localScale = new Vector3(SwordCoverWidth * 1.6f, SwordCoverHeight * 1.3f, 1f);

        Blocker cover = sword.AddComponent<Blocker>();
        cover.Init(int.MaxValue, look, new[] { look.sprite }, art.deathParticles);
        return cover;
    }

    // It takes its sword back: the blade glints, it dashes down onto it, wrenches it out of the ground with a strike
    // that cuts anyone still sheltering there, and dashes back to its row.
    IEnumerator ReclaimSword()
    {
        if (plantedSword == null)
        {
            swordOut = false;
            PlayIdle();
            yield break;
        }
        Vector3 row = anchor;
        float x = plantedSword.transform.position.x;
        ElementFx.Glow(plantedSword.transform.position, Color.white, 0.9f, 2.5f, 0.4f);
        Sfx.Play(Sfx.ParryClang, 0.5f, 1.6f);
        Tick(0.9f);
        yield return Wait(secondPhase ? 0.45f : 0.55f);

        Beat();
        yield return Dash(new Vector3(x, Landing(), row.z));
        Destroy(plantedSword.gameObject);
        transient.Remove(plantedSword.gameObject);
        plantedSword = null;
        swordOut = false;
        yield return Chop(x, null);
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
                if (shade == null) HitIfCrossed(Core, between);
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
                yield return HoldingHands(RecallSword(), left, right, leftEdge, rightEdge);
                yield return HoldingHands(ThrowSword(AtRonin(3f), strike + 2), left, right, leftEdge, rightEdge);
                if (plantedSword != null) markX = plantedSword.transform.position.x;
            }
        }
        EndBeam(left);
        EndBeam(right);
        Sfx.Stop(Sfx.LanternBeam);
        yield return ReclaimSword();
    }

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
        else HitIfCrossed(from, end);
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

    // A laser redrawn every frame: a soft glow under its body, which is its beam art tiled along its length and
    // scrolling (red, or molten orange and wilder once its core opens), or without the art a flickering red bar over a
    // white-hot core. A flare at the source, sparks where it lands and a trail of fire it leaves burning on the ground.
    class Beam
    {
        public SpriteRenderer glow, body, core;
        public Sprite[] art;
        public FxPlayer flare, hit;
        public float nextFire, nextBurn, nextSpark, lastFireX;
        // Fired from the slit-eye rather than the core.
        public bool fromEye;
    }

    Beam MakeBeam() => MakeBeam(Core, 1.6f);

    Beam MakeBeam(Vector3 from, float flareSize)
    {
        var beam = new Beam
        {
            art = Fx.Frames(secondPhase ? Fx.UtsuBeamMolten : Fx.UtsuBeamRed),
            glow = Bar(from, from, 1f, new Color(BeamColour.r, BeamColour.g, BeamColour.b, 0.35f), 53),
            flare = Fx.Play(Fx.UtsuBeamFlare, from, secondPhase ? MoltenTint : Color.white, flareSize, transform, 57),
            hit = Fx.Play(Fx.UtsuBeamHit, from, secondPhase ? MoltenTint : Color.white, 1.3f, null, 57),
        };
        if (beam.art != null)
        {
            beam.body = new GameObject("BeamBody").AddComponent<SpriteRenderer>();
            beam.body.sprite = beam.art[0];
            beam.body.drawMode = SpriteDrawMode.Tiled;
            beam.body.sortingOrder = 55;
            transient.Add(beam.body.gameObject);
        }
        else
        {
            beam.body = Bar(from, from, 1f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.9f), 55);
            beam.core = Bar(from, from, 1f, new Color(1f, 0.92f, 0.88f), 56);
        }
        beam.glow.sprite = ProceduralSprites.Glow;
        // Cleared with the rest of the attack if it's cut short.
        if (beam.flare != null) transient.Add(beam.flare.gameObject);
        if (beam.hit != null) transient.Add(beam.hit.gameObject);
        beams.Add(beam);
        return beam;
    }

    // An interrupted laser (a stagger, its defeat, the ronin falling) loses its bars with the attack; its hum goes too.
    void LateUpdate()
    {
        if (beams.Count == 0) return;
        beams.RemoveAll(b => b.body == null);
        if (beams.Count == 0) Sfx.Stop(Sfx.LanternBeam);
    }

    void DrawBeam(Beam beam, Vector3 from, Vector3 to, bool flash, bool onGround)
    {
        float flicker = 1f + 0.18f * Mathf.Sin(Time.time * 47f) + 0.08f * Mathf.Sin(Time.time * 113f);
        // The molten beam thrashes: its width jumps about every frame on top of the flicker.
        if (secondPhase) flicker *= Random.Range(0.85f, 1.2f);
        Stretch(beam.glow, from, to, (secondPhase ? 2f : 1.6f) * flicker);
        beam.glow.color = flash ? new Color(1f, 1f, 1f, 0.5f) : new Color(BeamColour.r, BeamColour.g, BeamColour.b, 0.35f);
        if (beam.art != null)
        {
            beam.body.sprite = beam.art[(int)(Time.time * BeamFps) % beam.art.Length];
            // Flipped now and then, so the molten plasma never settles into a pattern.
            if (secondPhase && Random.value < 0.15f) beam.body.flipY = !beam.body.flipY;
            LayTiled(beam.body, from, to, (secondPhase ? 0.95f : 0.7f) * flicker * (flash ? 1.3f : 1f));
            if (secondPhase && Time.time >= beam.nextSpark)
            {
                // Embers spit off its length.
                beam.nextSpark = Time.time + 0.06f;
                Vector3 along = Vector3.Lerp(from, to, Random.value);
                Fx.Play(Fx.Impact, along, Molten, Random.Range(0.3f, 0.55f));
            }
        }
        else
        {
            Stretch(beam.body, from, to, 0.42f * flicker);
            Stretch(beam.core, from, to, 0.14f);
            beam.body.color = flash ? Color.white : new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.9f);
        }
        if (beam.hit != null) beam.hit.transform.position = to + Vector3.back * 0.3f;
        else if (beam.art == null && Time.time >= beam.nextSpark)
        {
            beam.nextSpark = Time.time + 0.12f;
            Fx.Play(Fx.Impact, to, CoreRed, 0.7f);
        }
        // A trail of fire along the ground behind it: a new patch every half unit it drags, or every so often if
        // it holds still, so the burn reads as one unbroken line.
        if (onGround && (Mathf.Abs(to.x - beam.lastFireX) >= 0.5f || Time.time >= beam.nextFire))
        {
            beam.nextFire = Time.time + 0.25f;
            beam.lastFireX = to.x;
            GroundFire.Light(to);
        }
    }

    // The beam art scrolls at this many frames a second.
    const float BeamFps = 18f;
    // Its laser's colour: red, and molten orange once its core opens.
    static readonly Color MoltenTint = new Color(1f, 0.75f, 0.45f);
    Color BeamColour => secondPhase ? Molten : CoreRed;

    // Lays tiled beam art from one point to another: scaled to the width and tiled along the length, so the art
    // doesn't smear.
    static void LayTiled(SpriteRenderer strip, Vector3 from, Vector3 to, float width)
    {
        Vector3 delta = to - from;
        float art = strip.sprite.bounds.size.y;
        float scale = width / art;
        strip.transform.position = (from + to) / 2f;
        strip.transform.rotation = delta.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.right, delta) : Quaternion.identity;
        strip.transform.localScale = Vector3.one * scale;
        strip.size = new Vector2(delta.magnitude / scale, art);
    }

    void BurnCover(Beam beam, Blocker cover, Vector3 at)
    {
        if (Time.time < beam.nextBurn) return;
        beam.nextBurn = Time.time + 0.25f;
        // The sword can't burn: the beam only strikes sparks off its steel.
        if (cover == plantedSword)
        {
            Fx.Play(Fx.Impact, at, Color.white, 0.7f);
            Sfx.Play(Sfx.ParryClang, 0.25f, 1.8f);
            return;
        }
        cover.Hit(at);
        Sfx.Play(Sfx.BlockerHit, 0.5f, 1.6f);
    }

    void EndBeam(Beam beam)
    {
        beams.Remove(beam);
        RemoveTransient(beam.glow);
        RemoveTransient(beam.body);
        RemoveTransient(beam.core);
        if (beam.flare != null) beam.flare.Stop();
        if (beam.hit != null) beam.hit.Stop();
    }

    // Where a beam from the core aimed at x on the ground stops: on the nearest blocker in the way, or the ground.
    Vector3 BeamEnd(float x, out Blocker cover) => BeamEnd(Core, x, out cover);

    Vector3 BeamEnd(Vector3 from, float x, out Blocker cover) => BeamEnd(from, new Vector3(x, GroundY, 0), out cover);

    // Fire burns holes in the lace.
    public override void Burn(Vector3 point, float radius)
    {
        foreach (Arrow diamond in lattice)
        {
            if (diamond != null && Vector2.Distance(diamond.transform.position, point) < radius * 1.5f) Destroy(diamond.gameObject);
        }
        lattice.RemoveAll(a => a == null);
    }

    // A parried thrust jams the gears: sparks and a grinding shudder.
    protected override void OnStaggered()
    {
        StopPacing();
        if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 2f) == null) Fx.Play(Fx.Impact, Centre, Gold, 2f);
        Debris.BurstSprites(Centre, art.deathParticles, Gold, 12, 0.8f, 7f);
        Sfx.Play(Sfx.GearGrind);
    }

    protected override bool HasSecondPhase => true;

    // When its first bar empties the mainspring snaps: the shroud tears, the chest opens on its glowing core, and the
    // clock runs fast from then on.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        // A break mid-Hour Strikes drops the armour with the rest of the move.
        armored = false;
        ShowEye(false);

        // The mainspring slips: it seizes and shudders, sparks spitting from its joints while the clock inside it
        // races, each tick closer on the last.
        animator.Play(art.hit, false, null, art.actionFps * 0.5f);
        HitFlash.Flash(sprite, 0.15f);
        CameraShake.Add(0.3f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.75f);
        Music.Duck(0.15f, 6f);
        StartCoroutine(Tremble(1.5f, 0.1f, 2f));
        float tick = 0.3f;
        for (float t = 0; t < 1.5f; t += tick, tick = Mathf.Max(0.05f, tick * 0.8f))
        {
            Sfx.Play(Sfx.ClockTick, 0.7f, 1f + t * 0.4f);
            Vector3 joint = Centre + new Vector3(Random.Range(-1f, 1f) * sprite.bounds.extents.x * 0.6f,
                Random.Range(-1f, 1f) * sprite.bounds.extents.y * 0.6f, 0);
            Fx.Play(Fx.Impact, joint, Gold, 0.7f);
            yield return new WaitForSeconds(tick);
        }

        // It stops dead. A bell tolls once.
        Sfx.Play(Sfx.MidnightChime, 1f, 0.7f);
        yield return new WaitForSeconds(0.55f);

        // Then it breaks open: gears and parts burst off it, the hat shatters into a halo and the core blazes out.
        CameraShake.Add(0.6f);
        HitFlash.Flash(sprite, 0.2f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.6f);
        Sfx.Play(Sfx.StageStart, 1f, 0.8f);
        Sfx.Play(Sfx.HatClang, 0.9f, 0.7f);
        if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 3f) == null) Fx.Play(Fx.Explosion, Centre, Gold, 2.5f);
        if (Fx.Play(Fx.UtsuParts, Head, Color.white, 2.5f) == null) Fx.Play(Fx.Impact, Head, Gold, 2.5f);
        Debris.BurstSprites(Centre, art.deathParticles, Lace, 30, 1.2f, 8f);
        bodyTint = Color.Lerp(bodyTint, new Color(0.75f, 0.7f, 0.8f), 0.5f);
        WearPhaseArt();
        if (phaseBareIdle != null) bareIdle = phaseBareIdle;
        if (phaseUnarmedIdle != null) unarmedIdle = phaseUnarmedIdle;
        if (phaseDash != null) dashFrames = phaseDash;
        if (phaseChop != null) chopFrames = phaseChop;
        if (phaseSlash != null) slashFrames = phaseSlash;
        if (phaseSwordThrow != null) swordThrowFrames = phaseSwordThrow;
        // The patched bare-and-unarmed stance is built from the idles, so it is rebuilt from the new ones.
        bareUnarmedIdle = null;
        PlayIdle();
        AddCore();
        // The core opens with a radial burst of light: phase 2 fights with lasers.
        for (int i = 0; i < 8; i++)
        {
            Vector3 ray = Quaternion.Euler(0, 0, i * 45f + 22.5f) * Vector3.right * 22f;
            SpriteRenderer flash = Bar(Core, Core + ray, 0.25f, new Color(1f, 0.85f, 0.8f, 0.9f), 57);
            transient.Remove(flash.gameObject);
            Fader.Fade(flash, 0.35f);
        }
        Sfx.Play(Sfx.LanternCharge, 1f, 1.3f);
        ElementFx.Glow(Core, new Color(1f, 0.45f, 0.12f), 0.8f, 4f, 0.9f, transform);
        // Overdriven: it hangs there, burning, then a title card of its broken form, over which it speaks; without
        // its art, a subtitle.
        if (PhaseCard != null)
        {
            yield return new WaitForSeconds(1f);
            yield return PhaseCard(this);
        }
        else
        {
            Speak(BossLine.Phase);
            yield return new WaitForSeconds(1.8f);
        }
        breaking = false;
    }

    void AddCore()
    {
        coreGlow = Kunai.CreateGlow(sprite.transform, 1.4f, sprite.sortingOrder + 1, CoreRed);
        coreGlow.transform.position = Core + Vector3.back * 0.3f;
        StartCoroutine(PulseCore());
    }

    // The heart beats in time with the clock.
    IEnumerator PulseCore()
    {
        while (coreGlow != null && !IsDead)
        {
            float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 4f)), 4f);
            coreGlow.color = new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.45f + 0.45f * pulse);
            coreGlow.transform.localScale = Vector3.one * (1.2f + 0.5f * pulse) / transform.lossyScale.x;
            yield return null;
        }
    }

    // It locks up mid-tick and comes apart: hat, arm and shroud fall away and the heart gutters out.
    protected override IEnumerator Fall()
    {
        ShowEye(false);
        foreach (Arrow diamond in lattice)
        {
            if (diamond != null) Destroy(diamond.gameObject);
        }
        if (coreGlow != null) Destroy(coreGlow.gameObject);
        if (Fx.Play(Fx.UtsuParts, Centre, Color.white, 3f) == null) Fx.Play(Fx.Impact, Centre, Gold, 3f);
        Debris.BurstSprites(Centre, art.deathParticles, Gold, 20, 1f, 8f);
        Debris.BurstSprites(Centre, art.deathParticles, Lace, 16, 0.9f, 6f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.6f);
        return base.Fall();
    }
}
