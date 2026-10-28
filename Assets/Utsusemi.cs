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

    enum Move { KasaWheel, Lattice, HourStrikes, PlumbLines, HeartLantern, Midnight, Escapement, StoppedHour, HyperBeam }

    // Each value is (phase 1, phase 2): once unwound, its clock runs fast.
    float Recovery => secondPhase ? 0.35f : 0.7f;
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
    // Its raised burning fist as it stops time (phase 2 only); without it, the throw clip stands in.
    Sprite[] stopFrames;
    // Its Hyper Beam wind-up (phase 2 only): arms flung back as its core swells; without it, its attack pose.
    Sprite[] hyperChargeFrames;

    public void SetHyperCharge(Sprite[] frames)
    {
        hyperChargeFrames = frames != null && frames.Length > 1 ? frames : null;
    }

    public void SetPhaseFrames(Sprite[] bare, Sprite[] unarmed, Sprite[] dash, Sprite[] chop, Sprite[] slash, Sprite[] swordThrow, Sprite[] stop)
    {
        Sprite[] Some(Sprite[] f, int min) => f != null && f.Length > min ? f : null;
        phaseBareIdle = Some(bare, 0);
        phaseUnarmedIdle = Some(unarmed, 0);
        phaseDash = Some(dash, 1);
        phaseChop = Some(chop, ChopImpactFrame);
        phaseSlash = Some(slash, 0);
        phaseSwordThrow = Some(swordThrow, SwordReleaseFrame);
        stopFrames = Some(stop, 0);
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
    // The red slit-eye under the hat brim and the lantern heart in its chest, where its frames draw them. The second
    // form, drawn from its painting on a larger canvas, has them elsewhere (PhaseBreak moves them).
    Vector2 eyePixel = new Vector2(57.5f, 38.5f), corePixel = new Vector2(58f, 50.5f);
    // The painted second form stands lower and leans in its empty-handed pose (the sword planted, as its lasers
    // fire), so its eye and core sit elsewhere there.
    static readonly Vector2 UnarmedEyePixel = new Vector2(77.5f, 51f), UnarmedCorePixel = new Vector2(78.5f, 66.5f);
    bool painted;
    bool InUnarmedPose => painted && sprite.sprite != null
        && ((unarmedIdle != null && unarmedIdle.Length > 0 && sprite.sprite == unarmedIdle[0])
            || (bareUnarmedIdle != null && bareUnarmedIdle.Length > 0 && sprite.sprite == bareUnarmedIdle[0]));
    Vector3 Eye => InUnarmedPose ? OnSprite(UnarmedEyePixel.x, UnarmedEyePixel.y) : OnSprite(eyePixel.x, eyePixel.y);
    Vector3 Core => InUnarmedPose ? OnSprite(UnarmedCorePixel.x, UnarmedCorePixel.y) : OnSprite(corePixel.x, corePixel.y);
    // Its raised fist at the height of its Stop clip, where time stops from.
    static readonly Vector2 FistPixel = new Vector2(109f, 40f);
    Vector3 Shoulder => Centre + new Vector3(sprite.bounds.extents.x * 0.5f, sprite.bounds.size.y * 0.1f, 0);

    // The height of its row, taken when the fight first starts. Its dashes down to the ronin's ground return here,
    // unless something cuts them short.
    float rowY = float.NaN;

    // Back up to its row if a stagger, a freeze-then-break or a phase break caught it down on the ronin's ground;
    // otherwise its next move would take the ground for its row and fight from there.
    IEnumerator BackToRow()
    {
        if (float.IsNaN(rowY)) rowY = anchor.y;
        else if (Mathf.Abs(anchor.y - rowY) > 0.05f) yield return Dash(new Vector3(anchor.x, rowY, anchor.z), 0.25f);
    }

    protected override IEnumerator Fight()
    {
        armored = false;
        // A fight restarted mid-Stopped Hour lets time run again (the ronin too) and bursts the cleaves it had hung.
        ClearStoppedTime();
        // Poguru goes back in the wisp if a stagger cut the move he was warning about short.
        RestPoguru();
        // A fight restarted mid-laser (a stagger) clears the planted sword with the attack: it's back in hand.
        if (swordOut)
        {
            swordOut = false;
            plantedSword = null;
            PlayIdle();
        }
        yield return BackToRow();
        while (true)
        {
            while (breaking) yield return null;
            // Fresh out of its phase break, with the rocks it shook loose still lying in the row: the Hyper Beam
            // comes first, without the overload (the break was it).
            if (hyperNext)
            {
                hyperNext = false;
                lastLaser = Move.HyperBeam;
                movesSinceLaser = 0;
                yield return HyperBeam(false);
                continue;
            }
            // Winding: it stands still for a beat between combos. This is the opening.
            yield return Wait(Recovery);

            // Low on its last bar, once: the Thirteenth Hour.
            if (secondPhase && !thirteenthStruck && Health <= MaxHealth * ThirteenthAt && !swordOut && !swordWheeling)
            {
                yield return ThirteenthHour();
                continue;
            }

            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            bool hourStruck = false, kicked = false;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                // Too many cuts taken from right underneath it: it swings clear first, once a combo.
                if (!kicked && Camped)
                {
                    kicked = true;
                    yield return PendulumKick();
                }
                Move move = NextMove(hourStruck);
                // A short shuffle to a new spot before a move, not a long walk: the lasers walk to the centre
                // themselves, and a combo's follow-ups often go straight in.
                bool laser = move == Move.HeartLantern || move == Move.Midnight || move == Move.StoppedHour || move == Move.HyperBeam;
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
                    case Move.Escapement: yield return Escapement(); break;
                    case Move.StoppedHour: yield return StoppedHour(); break;
                    case Move.HyperBeam: yield return HyperBeam(true); break;
                }
            }
        }
    }

    // Never the same move twice in a row; the Hour Strikes come at most once a combo but at least every third move,
    // so the parry rhythm stays part of the fight. No new hat while one is still out. Once its core opens, the laser
    // moves (the Heart Lantern, Midnight, the Stopped Hour and the Hyper Beam) take over: one at least every third move and often
    // sooner, never the same one twice running, so the order can't be counted off.
    Move NextMove(bool hourStruck)
    {
        // While its sword wheels it fights on with the moves that need no blade.
        if (swordWheeling)
        {
            var bladeless = new List<Move> { Move.Lattice, Move.Escapement };
            if (!plumbsOut) bladeless.Add(Move.PlumbLines);
            if (lastMove is Move previous) bladeless.Remove(previous);
            return bladeless[Random.Range(0, bladeless.Count)];
        }
        if (secondPhase && (movesSinceLaser >= 2 || (movesSinceLaser == 1 && Random.value < 0.5f)))
        {
            var lasers = new List<Move> { Move.HeartLantern, Move.Midnight, Move.StoppedHour, Move.HyperBeam };
            if (lastLaser is Move previous) lasers.Remove(previous);
            return lasers[Random.Range(0, lasers.Count)];
        }
        if (!hourStruck && movesSinceHours >= 2 && lastMove != Move.HourStrikes) return Move.HourStrikes;
        var options = new List<Move> { Move.Lattice, Move.Escapement };
        if (!plumbsOut) options.Add(Move.PlumbLines);
        if (hatsOut == 0) options.Add(Move.KasaWheel);
        if (!hourStruck) options.Add(Move.HourStrikes);
        // Once unwound, the hat counts twice, so the lace and plumbs come up less often between lasers.
        if (secondPhase && hatsOut == 0) options.Add(Move.KasaWheel);
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
        coverRowY = rowY;
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
                GroundFire.Scorch(new Vector3(end.x, rowY, 0));
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

        // Two layers with two ways through (three layers with one, once unwound). Each layer's ways through step on
        // from the last's, never further than he can follow before it lands.
        int layers = secondPhase ? 3 : 2;
        float interval = secondPhase ? 0.6f : 0.8f;
        float lo = LeftEdge + 1.6f, hi = RightEdge - 1.6f;
        var gaps = new List<float>();
        for (int g = 0; g < (secondPhase ? 1 : 2); g++) gaps.Add(PickGap(gaps, lo, hi));
        for (int layer = 0; layer < layers; layer++)
        {
            if (layer > 0)
            {
                yield return Wait(interval);
                for (int g = 0; g < gaps.Count; g++)
                {
                    float want = gaps[g] + Random.Range(-1f, 1f) * BulletHell.MaxDodgeSpeed * interval;
                    float next = BulletHell.NextGap(gaps[g], want, interval * 0.9f, lo, hi);
                    BulletHell.GapOk(gaps[g], next, interval, LatticeGap, "Lace Lattice");
                    gaps[g] = next;
                }
            }
            yield return BulletHell.Room(60);
            SpinLattice((layer % 2) * 0.55f, gaps);
            Sfx.Play(Sfx.WardShatter, 0.5f, 1.8f);
        }
    }

    // The clear width of each way through the lace, edge to edge.
    const float LatticeGap = 2.4f;
    const float DiamondRadius = 0.22f;

    // A way through somewhere in the field, well apart from any already chosen.
    static float PickGap(List<float> taken, float lo, float hi)
    {
        float x = Random.Range(lo, hi);
        for (int tries = 0; tries < 12; tries++)
        {
            x = Random.Range(lo, hi);
            if (taken.TrueForAll(g => Mathf.Abs(g - x) >= 4f)) break;
        }
        return x;
    }

    void SpinLattice(float offset, List<float> gaps)
    {
        const float spacing = 1.1f;
        const int rows = 3;
        float speed = secondPhase ? 3f : 2.6f;
        float left = LeftEdge + 0.4f + offset;
        int columns = Mathf.FloorToInt((RightEdge - 0.4f - left) / spacing) + 1;
        // Once unwound its lace burns: dark steel cracked with molten light.
        Sprite[] frames = secondPhase ? Fx.Frames(Fx.UtsuLaceP2) ?? Fx.Frames(Fx.UtsuLace) : Fx.Frames(Fx.UtsuLace);
        Color glow = secondPhase ? new Color(Molten.r, Molten.g, Molten.b, 0.35f) : new Color(0.8f, 0.85f, 1f, 0.25f);
        float y0 = anchor.y - 1.6f;
        for (int row = 0; row < rows; row++)
        {
            for (int c = 0; c < columns; c++)
            {
                float x = left + c * spacing + (row % 2 == 1 ? spacing / 2f : 0f);
                // Every row leaves the gap clear by its own diamonds, so the offset rows can't close it.
                if (gaps.Exists(g => Mathf.Abs(x - g) < LatticeGap / 2f + DiamondRadius)) continue;
                Arrow diamond = BulletHell.Enlist(Shoot(new Vector3(x, y0 + row * 0.9f, 0), Vector3.down * speed));
                if (frames != null) diamond.Restyle(frames, 8f, Color.white, new Vector2(0.7f, 0.7f), 0.22f, glow, 0.7f);
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
        int count = secondPhase ? 4 : 3;
        int lasers = secondPhase ? 3 : 2;
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
        // The shell drops once the move is called: it lasers the hours open to the blade.
        Armour(shell, false);
        for (int i = 0; i < lasers; i++) yield return LaserHour(shell);
        yield return HoldArmor(shell, 0.3f);
        Tick(0.4f);
        yield return HoldArmor(shell, 0.25f);

        // The dashes: each one locks onto wherever the ronin stands now. It keeps its shell while it takes aim, so the
        // hours give him an opening only while it is down on his ground.
        for (int hour = 1; hour <= count; hour++)
        {
            Beat();
            Tick(0.9f);
            yield return HoldArmor(shell, beat);
            yield return LockOnDash(hour, row, shell, hour == count ? RewindSeconds : 0f);
            PlayIdle();
        }
        armored = false;
        foreach (SpriteRenderer part in shell) RemoveTransient(part);

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
        yield return HoldArmor(shell, secondPhase ? 0.3f : 0.35f, () => Stretch(sight, Eye, spot, 0.07f));
        RemoveTransient(sight);

        Beam beam = MakeBeam(Eye, 0.8f);
        // The strike lands hard: a broad blaze where it hits, and a full-size burn left on the spot.
        if (beam.hit != null) beam.hit.transform.localScale *= 1.8f;
        Sfx.Play(Sfx.LanternCharge, 0.4f, 2f);
        CameraShake.Add(0.1f);
        GroundFire.Scorch(spot, 2f);
        Fx.Play(Fx.Impact, spot, BeamColour, 1.4f);
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
    // After the last hour it stays down on his ground this long, winding itself back up: the opening.
    const float RewindSeconds = 1.2f;

    IEnumerator LockOnDash(int hour, Vector3 row, SpriteRenderer[] shell, float linger)
    {
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        SpriteRenderer sight = Bar(Eye, Spot(x), 0.05f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.4f));
        // Armoured while it aims from its row.
        Armour(shell, true);
        yield return HoldArmor(shell, secondPhase ? 0.4f : 0.5f, () =>
        {
            x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
            Stretch(sight, Eye, Spot(x), 0.05f);
        });

        // Locked: the hour's numeral rises where he stood, and the dodge window opens.
        var mark = new HourMark(this, hour, x);
        mark.Show();
        Tick(0.7f);
        Vector3 spot = Spot(x);
        sight.color = new Color(1f, 0.55f, 0.5f, 0.85f);
        yield return HoldArmor(shell, secondPhase ? 0.3f : 0.4f, () => Stretch(sight, Eye, spot, 0.07f));
        RemoveTransient(sight);

        // The shell drops as it leaves its row: down on his ground it can be struck.
        Armour(shell, false);
        yield return Dash(new Vector3(x, Landing(), row.z), HourDashTime);
        yield return Chop(x, mark, 36f, 0.08f);
        if (linger > 0f)
        {
            // The hours spent, it winds itself back up where it landed: gears grinding, slow ticks, open to the blade.
            Sfx.Play(Sfx.GearGrind, 0.6f, 0.8f);
            if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 1.6f) == null) Fx.Play(Fx.Impact, Centre, Gold, 1.4f);
            PlayIdle();
            for (float t = 0; t < linger; t += 0.3f)
            {
                Beat();
                Sfx.Play(Sfx.ClockTick, 0.5f, 0.6f);
                yield return Wait(0.3f);
            }
        }
        yield return Dash(row, HourDashTime);
    }

    Vector3 Spot(float x) => new Vector3(x, GroundY + 0.15f, 0);

    void Armour(SpriteRenderer[] shell, bool on)
    {
        armored = on;
        foreach (SpriteRenderer part in shell)
        {
            if (part != null) part.enabled = on;
        }
    }

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
        // Its second form leaves burning ghosts: molten and ivory instead of steel blue and gold.
        SpawnGhost(flat, SecondPhase ? Molten : GhostBlue, alpha, jitter - 3f * pixel, pixel);
        SpawnGhost(flat, SecondPhase ? Lace : GhostGold, alpha * 0.7f, jitter + 3f * pixel, pixel);
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
        Arrow cleave = Own(Shoot(from, (ronin.Bounds.center - from).normalized * HourSpeed));
        Sprite[] frames = Fx.Frames(Fx.UtsuHourSlash);
        if (frames != null) cleave.Restyle(frames, 14f, Color.white, new Vector2(5.6f, 3.7f), 1.1f, new Color(1f, 0.2f, 0.18f, 0.6f), 4.5f);
        else cleave.Restyle(ProceduralSprites.White, new Color(0.9f, 0.93f, 1f), new Vector2(5.4f, 0.5f), 1.1f, new Color(1f, 0.2f, 0.18f, 0.6f), 4.5f);
        cleave.MakeUnbreakable();
        cleave.MakeReflectable(transform, setPiece: true);
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
        // The mark is drawn this many world units wide, about the reach of the blow it marks.
        const float ArtWidth = 2.1f;
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
                // A gold clock rim lying flat on the ground line with the hour inside, like the clock marks.
                var art = new GameObject("HourNumeral").AddComponent<SpriteRenderer>();
                art.sprite = numerals[Mathf.Clamp(hour - 1, 0, numerals.Length - 1)];
                art.transform.position = new Vector3(x, foot.y - 0.1f, 0);
                art.transform.localScale = Vector3.one * (ArtWidth / art.sprite.bounds.size.x);
                art.color = tint;
                art.sortingOrder = 8;
                owner.transient.Add(art.gameObject);
                strokes.Add(art);
                ElementFx.Glow(foot, tint == Color.white ? Gold : tint, 0.5f, 1.6f, 0.3f);
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
            weights[i] = Own(Shoot(pivots[i], Vector3.down * 0.1f));
            // A landing shadow reads a straight fall; a pendulum's swing makes it lie, so the weights go without.
            Destroy(weights[i].GetComponent<ArrowShadow>());
            if (frames != null) weights[i].Restyle(frames, 8f, Color.white, new Vector2(0.9f, 1.07f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.35f), 1.1f);
            else weights[i].Restyle(ProceduralSprites.Disc, Gold, new Vector2(0.6f, 0.8f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 1.1f);
            threads[i].color = new Color(0.95f, 0.95f, 1f, 0.6f);
        }
        // Neighbours' arcs cross, and shots break each other, so the weights must pass through one another.
        IgnoreEachOther(new List<Arrow>(weights));
        Sfx.Play(Sfx.ShurikenThrow, 0.7f, 0.4f);

        // The weights are away: a stagger now breaks its stance but not the swing, which plays out on its own.
        for (int i = 0; i < count; i++)
        {
            if (threads[i] != null) Detach(threads[i].gameObject);
            if (portals[i] != null) Detach(portals[i].gameObject);
        }
        plumbsOut = true;
        yield return Launch(SwingPlumbs(weights, threads, portals, pivots, length, drop, swing, damp, reel, fadeIn));
    }

    // Plumb Lines has weights swinging: it won't open more portals until they're reeled in.
    bool plumbsOut;

    // The Plumb Lines' swing, launched: the drop, the pendulum wave, the damping and the reel, in real time whatever
    // happens to the automaton meanwhile, then the portals close.
    IEnumerator SwingPlumbs(Arrow[] weights, SpriteRenderer[] threads, Transform[] portals, Vector3[] pivots, float length,
        float drop, float swing, float damp, float reel, float fadeIn)
    {
        int count = weights.Length;

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
        for (float t = 0; t < total; t += Time.fixedDeltaTime)
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
                    if (portals[i] != null) StartCoroutine(SnapThread(threads[i], pivots[i], lastHang[i], portals[i]));
                    else if (threads[i] != null) Destroy(threads[i].gameObject);
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
        plumbsOut = false;
        for (int i = 0; i < count; i++)
        {
            if (portals[i] != null) StartCoroutine(ClosePortal(portals[i]));
            yield return new WaitForSeconds(0.06f);
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
        // Poguru, who was pointing at the sword, flies back to Oni yelling to get clear: it's coming down on it.
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
        yield return Dash(new Vector3(x, Landing(), row.z));
        Destroy(plantedSword.gameObject);
        transient.Remove(plantedSword.gameObject);
        plantedSword = null;
        swordOut = false;
        yield return Chop(x, mark);
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
    // ward is his cover, not just the shade the blade casts.
    void BeamHit(Vector3 from, Vector3 end)
    {
        if (InSwordWard(ronin.Bounds.center.x)) return;
        HitIfCrossed(from, end);
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

    // A laser redrawn every frame: a soft glow under its body, which is its beam art tiled along its length and
    // scrolling (red, or molten orange and wilder once its core opens), or without the art a flickering red bar over a
    // white-hot core. A flare at the source, sparks where it lands and a trail of fire it leaves burning on the ground.
    class Beam
    {
        public SpriteRenderer glow, body, core;
        public Sprite[] art;
        // The phase 2 beam: molten orange and wild.
        public bool molten;
        public Color colour;
        public FxPlayer flare, hit;
        public float nextFire, nextBurn, nextSpark, lastFireX;
        // Fired from the slit-eye rather than the core.
        public bool fromEye;
        // How many times its usual breadth it's drawn (the Hyper Beam's is several).
        public float width = 1f;
    }

    Beam MakeBeam() => MakeBeam(Core, 1.6f);

    Beam MakeBeam(Vector3 from, float flareSize) => MakeBeam(from, flareSize, secondPhase);

    Beam MakeBeam(Vector3 from, float flareSize, bool molten)
    {
        Color colour = molten ? Molten : CoreRed;
        var beam = new Beam
        {
            molten = molten,
            colour = colour,
            art = Fx.Frames(molten ? Fx.UtsuBeamMolten : Fx.UtsuBeamRed),
            glow = Bar(from, from, 1f, new Color(colour.r, colour.g, colour.b, 0.35f), 53),
            // The flare at its source sits under the beam itself, so the laser reads on top of the glow. The molten
            // beam's is drawn in its own orange and gold, a little larger; without that art the red one is tinted.
            flare = molten && Fx.Frames(Fx.UtsuBeamFlareP2) != null
                ? Fx.Play(Fx.UtsuBeamFlareP2, from, Color.white, flareSize * 1.3f, transform, 54)
                : Fx.Play(Fx.UtsuBeamFlare, from, molten ? MoltenTint : Color.white, flareSize, transform, 54),
            hit = Fx.Play(Fx.UtsuBeamHit, from, molten ? MoltenTint : Color.white, 1.3f, null, 57),
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
        if (beam.molten) flicker *= Random.Range(0.85f, 1.2f);
        Stretch(beam.glow, from, to, (beam.molten ? 2f : 1.6f) * flicker * beam.width);
        beam.glow.color = flash ? new Color(1f, 1f, 1f, 0.5f) : new Color(beam.colour.r, beam.colour.g, beam.colour.b, 0.35f);
        if (beam.art != null)
        {
            beam.body.sprite = beam.art[(int)(Time.time * BeamFps) % beam.art.Length];
            // Flipped now and then, so the molten plasma never settles into a pattern.
            if (beam.molten && Random.value < 0.15f) beam.body.flipY = !beam.body.flipY;
            LayTiled(beam.body, from, to, (beam.molten ? 0.95f : 0.7f) * flicker * (flash ? 1.3f : 1f) * beam.width);
            if (beam.molten && Time.time >= beam.nextSpark)
            {
                // Embers spit off its length.
                beam.nextSpark = Time.time + 0.06f;
                Vector3 along = Vector3.Lerp(from, to, Random.value);
                Fx.Play(Fx.Impact, along, Molten, Random.Range(0.3f, 0.55f));
            }
        }
        else
        {
            Stretch(beam.body, from, to, 0.42f * flicker * beam.width);
            Stretch(beam.core, from, to, 0.14f * beam.width);
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
            GroundFire.Scorch(to);
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

    // The height of the cover row its entrance razed, where the rocks it shakes loose come to rest; just above the
    // ronin's head if the entrance never measured it.
    float coverRowY = float.NaN;
    float CoverRowY => float.IsNaN(coverRowY) ? ronin.TopY + 0.8f : coverRowY;

    // A rock lying in the cover row. Blows only flash off it; only the Hyper Beam breaks it, and only once the whole
    // beam has passed it.
    class Rock
    {
        public Blocker cover;
        public SpriteRenderer look;
        public Sprite[] states;
        public float halfWidth, height, heat;
        public bool scorched;
    }
    readonly List<Rock> rocks = new List<Rock>();
    // Set by the phase break: the Hyper Beam opens the second phase.
    bool hyperNext;

    // How tall a rock that stays stands, how many can lie in the row at once, and how far apart they come to rest.
    const float RockHeight = 1.5f;
    const int MaxRocks = 3;
    const float RockSpacing = 3.5f;
    // How long the move's overload shakes the hall and rains rocks.
    const float RockfallSeconds = 2.6f;
    // The Hyper Beam: its wind-up, its sweep from edge to edge, how many times a laser's breadth it's drawn, and how
    // far either side of where its middle crosses the ronin's height it burns.
    const float HyperChargeSeconds = 2.2f, HyperSweepSeconds = 3f, HyperWidth = 3.2f, HyperReach = 1.1f;

    // The Hyper Beam (second phase only). In super armour it overloads as at its phase break, lasers firing wild from
    // its eye and core (these ones burn) while the hall shakes and rocks rain down, one or two coming to rest in the
    // cover row. The armour drops, Poguru yells to get behind a rock and hovers in its shade, and it winds up a vast
    // beam that sweeps the field from one edge to the other. Only a rock saves the ronin: the beam can't get through
    // one, and a rock it burns crumbles only once the whole beam has passed it. Straight after the phase break it skips
    // the overload: the break shook the rocks down already.
    IEnumerator HyperBeam(bool overload)
    {
        yield return Stride(CameraShake.Center.x);
        if (overload)
        {
            armored = true;
            SpriteRenderer[] shell = ArmorShell();
            Sfx.Play(Sfx.GearGrind, 0.8f, 0.7f);
            StartCoroutine(Tremble(RockfallSeconds, 0.06f, 1.2f));
            bool fallen = false;
            StartCoroutine(Rockfall(RockfallSeconds, Random.Range(1, 3), true, () => fallen = true));
            float t = 0, nextShot = 0.15f;
            yield return HoldArmor(shell, RockfallSeconds, () =>
            {
                t += Time.deltaTime;
                if (t < nextShot) return;
                float k = t / RockfallSeconds;
                StartCoroutine(WildLaser(true, Mathf.Lerp(0.45f, 0.3f, k), true));
                nextShot = t + Mathf.Lerp(0.4f, 0.18f, k) * Random.Range(0.7f, 1.3f);
            });
            while (!fallen) yield return HoldArmor(shell, Time.deltaTime);
            Armour(shell, false);
            foreach (SpriteRenderer part in shell) RemoveTransient(part);
        }

        rocks.RemoveAll(r => r.cover == null);
        Rock nearest = NearestRock();
        if (nearest != null) CallPoguru("Poguru_Utsu_Rock", new Vector3(ShadeX(nearest), ronin.TopY + 0.3f, 0));
        // The sweep is laid out on the ronin's row, so its breadth reaches both edges there and every rock's shade;
        // aimed at the ground, its ends lie a little beyond the view.
        float dir = Random.value < 0.5f ? 1f : -1f;
        Vector3 aim = Core;
        float row = ronin.Bounds.center.y;
        float GroundFor(float rowX) => aim.x + (rowX - aim.x) * (aim.y - GroundY) / Mathf.Max(0.01f, aim.y - row);
        float from = GroundFor(dir > 0 ? LeftEdge - HyperReach : RightEdge + HyperReach);
        float to = GroundFor(dir > 0 ? RightEdge + HyperReach : LeftEdge - HyperReach);
        yield return HyperCharge(from);

        Beam beam = MakeBeam(Core, 2.6f);
        beam.width = HyperWidth;
        Sfx.PlayLoop(Sfx.LanternBeam, 1f, 0.6f);
        Sfx.Play(Sfx.LanternCharge, 1f, 0.45f);
        CameraShake.Add(0.6f);
        float lastX = from;
        for (float t = 0; t < HyperSweepSeconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            float x = Mathf.Lerp(from, to, t / HyperSweepSeconds);
            Vector3 end = BeamEnd(x, out Blocker cover);
            DrawBeam(beam, Core, end, false, cover == null);
            CameraShake.Add(2.4f * Time.deltaTime);
            Rock struck = rocks.Find(r => r.cover != null && r.cover == cover);
            if (struck != null) Scorch(struck, end);
            else if (cover != null) BurnCover(beam, cover, end);
            HyperHit(lastX, x);
            CrumblePassed(x, dir);
            lastX = x;
            yield return null;
        }
        EndBeam(beam);
        Sfx.Stop(Sfx.LanternBeam);
        foreach (Rock rock in rocks.ToArray())
        {
            if (rock.scorched) Crumble(rock);
        }
        RestPoguru();
        PlayIdle();
        yield return Wait(0.4f);
    }

    // Its wind-up: arms flung back as its core swells (its attack pose without that clip), streaks of light pulled
    // into its heart from all round, a molten orb growing there, the whine climbing and the ticks racing, the hall
    // shaking harder, and a guide line to the edge the beam will start from. Full, it flashes white with a ripple.
    IEnumerator HyperCharge(float from)
    {
        // The clip winds up once, then its last two frames (the blaze at full power) flicker until it fires.
        if (hyperChargeFrames != null)
        {
            Sprite[] blaze = { hyperChargeFrames[hyperChargeFrames.Length - 2], hyperChargeFrames[hyperChargeFrames.Length - 1] };
            animator.Play(hyperChargeFrames, false, () => animator.Play(blaze, true, null, 10f), hyperChargeFrames.Length / (HyperChargeSeconds * 0.6f));
        }
        else PlayAction(art.attack);
        Sfx.Play(Sfx.LanternCharge, 1f, 0.55f);
        SpriteRenderer orb = Bar(Core, Core, 1f, Color.clear, sprite.sortingOrder + 2);
        orb.sprite = ProceduralSprites.Glow;
        SpriteRenderer heart = Bar(Core, Core, 1f, Color.clear, sprite.sortingOrder + 3);
        heart.sprite = ProceduralSprites.Disc;
        SpriteRenderer guide = Bar(Core, Core, 0.05f, Color.clear);
        float nextStreak = 0, nextTick = 0, tick = 0.3f;
        bool whined = false;
        for (float t = 0; t < HyperChargeSeconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            Beat();
            float k = t / HyperChargeSeconds;
            Vector3 core = Core;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * (10f + 30f * k));
            orb.transform.SetPositionAndRotation(core + Vector3.back * 0.3f, Quaternion.identity);
            orb.transform.localScale = Vector3.one * (Mathf.Lerp(0.5f, 3.2f, k) * pulse / ProceduralSprites.Glow.bounds.size.x);
            orb.color = new Color(Molten.r, Molten.g, Molten.b, Mathf.Lerp(0.3f, 0.9f, k));
            heart.transform.SetPositionAndRotation(core + Vector3.back * 0.35f, Quaternion.identity);
            heart.transform.localScale = Vector3.one * (Mathf.Lerp(0.1f, 0.75f, k) * pulse / ProceduralSprites.Disc.bounds.size.x);
            heart.color = Color.Lerp(new Color(1f, 0.8f, 0.5f), Color.white, k);
            if (t >= nextStreak)
            {
                nextStreak = t + Mathf.Lerp(0.07f, 0.02f, k);
                StartCoroutine(Inflow(core, k));
            }
            if (t >= nextTick)
            {
                Sfx.Play(Sfx.ClockTick, 0.6f, 1f + k);
                nextTick = t + tick;
                tick = Mathf.Max(0.06f, tick * 0.85f);
            }
            if (!whined && k > 0.55f)
            {
                whined = true;
                Sfx.Play(Sfx.LanternCharge, 1f, 0.8f);
            }
            CameraShake.Add(Time.deltaTime * (1.5f + 1.5f * k));
            Stretch(guide, core, BeamEnd(from, out _), 0.04f + 0.08f * k);
            guide.color = new Color(Molten.r, Molten.g, Molten.b, (0.2f + 0.5f * k) * (0.7f + 0.3f * Mathf.Sin(Time.time * 40f)));
            yield return null;
        }
        RemoveTransient(orb);
        RemoveTransient(heart);
        RemoveTransient(guide);
        ElementFx.Glow(Core, Color.white, 1.2f, 4f, 0.35f, transform);
        TimeRipple.Emit(Core, 10f, 1.2f, 0.6f);
        HitFlash.Flash(sprite, 0.12f);
    }

    // A streak of light pulled in from the air to its core.
    IEnumerator Inflow(Vector3 core, float k)
    {
        Vector3 heading = Random.insideUnitCircle.normalized;
        float far = Random.Range(2.5f, 4.5f);
        SpriteRenderer streak = Bar(core, core + heading * 0.01f, 0.06f, Color.clear, 54);
        const float seconds = 0.28f;
        for (float t = 0; t < seconds && streak != null; t += Time.deltaTime)
        {
            float u = t / seconds;
            float near = 1f - u * u;
            Stretch(streak, core + heading * far * near, core + heading * far * Mathf.Min(1f, near + 0.35f), 0.05f + 0.05f * k);
            streak.color = new Color(1f, Mathf.Lerp(0.85f, 0.6f, u), 0.4f, Mathf.Sin(u * Mathf.PI) * 0.9f);
            yield return null;
        }
        RemoveTransient(streak);
    }

    // Within the beam's breadth on the ronin's row (counting the ground it swept since last frame), it burns him
    // unless a rock (or the sword) stands between his middle and its core.
    void HyperHit(float lastX, float x)
    {
        Vector3 core = Core;
        Vector3 middle = ronin.Bounds.center;
        float a = RowCross(core, lastX, middle.y), b = RowCross(core, x, middle.y);
        if (middle.x < Mathf.Min(a, b) - HyperReach || middle.x > Mathf.Max(a, b) + HyperReach) return;
        BeamEnd(core, middle, out Blocker cover);
        if (cover == null && !InSwordWard(middle.x)) ronin.TakeHit(middle);
    }

    // Where a ray from `from` to x on the ground crosses height y.
    float RowCross(Vector3 from, float x, float y) => from.x + (x - from.x) * (from.y - y) / (from.y - GroundY);

    // Where a ray from `from` through a point crosses height y.
    static float Project(Vector3 from, Vector3 through, float y) => from.x + (through.x - from.x) * (from.y - y) / Mathf.Max(0.01f, from.y - through.y);

    // A burnt rock crumbles only once the beam's trailing edge on the ronin's row has left its whole shade behind,
    // so whoever sheltered there is never caught by the beam's breadth as the rock goes.
    void CrumblePassed(float x, float dir)
    {
        Vector3 core = Core;
        float rowY = ronin.Bounds.center.y;
        float trailing = RowCross(core, x, rowY) - dir * HyperReach;
        foreach (Rock rock in rocks.ToArray())
        {
            if (!rock.scorched || rock.cover == null) continue;
            Vector3 foot = rock.cover.transform.position;
            Vector3 farFoot = foot + Vector3.right * dir * rock.halfWidth;
            float shadeFar = dir > 0
                ? Mathf.Max(Project(core, farFoot, rowY), Project(core, farFoot + Vector3.up * rock.height, rowY))
                : Mathf.Min(Project(core, farFoot, rowY), Project(core, farFoot + Vector3.up * rock.height, rowY));
            if (dir * (trailing - shadeFar) > 0f) Crumble(rock);
        }
    }

    // The beam bearing down on a rock: it glows hotter, cracks through its damage states and spits sparks.
    void Scorch(Rock rock, Vector3 at)
    {
        rock.scorched = true;
        rock.heat += Time.deltaTime;
        if (rock.states != null) rock.look.sprite = rock.states[rock.heat > 0.4f ? 2 : rock.heat > 0.15f ? 1 : 0];
        rock.look.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.35f), Mathf.Clamp01(rock.heat * 1.5f));
        if (Random.value < 0.3f) Fx.Play(Fx.Impact, at, Molten, Random.Range(0.5f, 0.9f));
        if (Random.value < 0.05f) Sfx.Play(Sfx.BlockerHit, 0.5f, Random.Range(0.6f, 0.8f));
    }

    void Crumble(Rock rock)
    {
        rocks.Remove(rock);
        if (rock.cover == null) return;
        Vector3 middle = rock.cover.transform.position + Vector3.up * rock.height * 0.5f;
        Debris.BurstSprites(middle, rock.states, new Color(0.8f, 0.72f, 0.65f), 8, 0.3f, 6f);
        Fx.Play(Fx.SmokePuff, middle, new Color(0.8f, 0.75f, 0.7f, 0.9f), 2f);
        rock.cover.Demolish();
        CameraShake.Add(0.2f);
    }

    Rock NearestRock()
    {
        Rock best = null;
        foreach (Rock rock in rocks)
        {
            if (rock.cover == null) continue;
            if (best == null || Mathf.Abs(ShadeX(rock) - ronin.Bounds.center.x) < Mathf.Abs(ShadeX(best) - ronin.Bounds.center.x)) best = rock;
        }
        return best;
    }

    // The middle of a rock's shade on the ronin's row, cast from its core.
    float ShadeX(Rock rock) => Project(Core, rock.cover.transform.position + Vector3.up * rock.height * 0.5f, ronin.Bounds.center.y);

    // The hall shakes and rocks rain down across it for `seconds`: most smash on the ground (on the ronin, if
    // `harmful` and he's still under it), and `keep` of them land in the cover row and stay there as cover, making
    // room first if the row already holds as many as it can.
    IEnumerator Rockfall(float seconds, int keep, bool harmful, System.Action done = null)
    {
        rocks.RemoveAll(r => r.cover == null);
        while (rocks.Count > 0 && rocks.Count + keep > MaxRocks) Crumble(rocks[0]);
        List<float> spots = RockSpots(keep);
        Sfx.Play(Sfx.GearGrind, 0.7f, 0.5f);
        CameraShake.Add(0.4f);
        float nextRock = 0.1f;
        int dropped = 0, landed = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            // A steady rumble: the shake topped up as fast as it fades.
            CameraShake.Add(2.3f * Time.deltaTime);
            if (t >= nextRock)
            {
                nextRock = t + Random.Range(0.12f, 0.24f);
                StartCoroutine(FallingRock(Random.Range(LeftEdge + 0.5f, RightEdge - 0.5f), harmful, null));
            }
            // The ones that stay come down through the middle of it.
            if (dropped < spots.Count && t >= seconds * (0.3f + 0.4f * dropped / spots.Count))
            {
                StartCoroutine(FallingRock(spots[dropped], false, () => landed++));
                dropped++;
            }
            yield return null;
        }
        for (; dropped < spots.Count; dropped++) StartCoroutine(FallingRock(spots[dropped], false, () => landed++));
        while (landed < spots.Count) yield return null;
        done?.Invoke();
    }

    // Spots in the cover row for new rocks, well apart from each other and from those already there.
    List<float> RockSpots(int count)
    {
        var spots = new List<float>();
        // Clear of the HUD's boon column at the left and of the edges.
        float lo = LeftEdge + 3.5f, hi = RightEdge - 3f;
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                float x = Random.Range(lo, hi);
                bool clear = spots.TrueForAll(s => Mathf.Abs(s - x) >= RockSpacing)
                    && rocks.TrueForAll(r => r.cover == null || Mathf.Abs(r.cover.transform.position.x - x) >= RockSpacing);
                if (!clear) continue;
                spots.Add(x);
                break;
            }
        }
        if (spots.Count == 0 && rocks.Count == 0) spots.Add(Random.Range(lo, hi));
        return spots;
    }

    // One rock falling from above the screen onto its shadow, which darkens as it nears. A rock that `stayed` lands
    // in the cover row and stays (calling it once it has); the rest smash on the ground.
    IEnumerator FallingRock(float x, bool harmful, System.Action stayed)
    {
        bool stays = stayed != null;
        Sprite[] frames = Fx.Frames(Fx.UtsuRock);
        Sprite[] states = null;
        if (frames != null && frames.Length >= 3)
        {
            int first = Random.Range(0, frames.Length / 3) * 3;
            states = new[] { frames[first], frames[first + 1], frames[first + 2] };
        }
        float height = stays ? RockHeight : Random.Range(0.45f, 0.85f);
        float landY = stays ? CoverRowY : GroundY + 0.15f;

        // The rock stands on its own foot: the object's origin is the bottom of its art. Launched, so a stagger leaves
        // it falling (and lying there); only a halt or its fall clears it.
        var root = new GameObject(stays ? "Rock" : "FallingRock");
        Detach(root);
        var look = new GameObject("Look").AddComponent<SpriteRenderer>();
        look.transform.SetParent(root.transform, false);
        look.sprite = states != null ? states[0] : ProceduralSprites.Disc;
        if (states == null) look.color = new Color(0.45f, 0.42f, 0.4f);
        float scale = height / look.sprite.bounds.size.y;
        look.transform.localScale = Vector3.one * scale;
        look.transform.localPosition = Vector3.up * (-look.sprite.bounds.min.y * scale);
        look.sortingOrder = stays ? 6 : 52;
        float halfWidth = look.sprite.bounds.size.x * scale / 2f;

        var shadow = new GameObject("RockShadow").AddComponent<SpriteRenderer>();
        shadow.sprite = ProceduralSprites.SoftGlow;
        shadow.sortingOrder = 3;
        Detach(shadow.gameObject);

        float top = TopOfScreen + height, fall = stays ? 0.9f : Random.Range(0.6f, 0.85f);
        float spin = stays ? 0f : Random.Range(-360f, 360f);
        for (float t = 0; t < fall && root != null; t += Time.deltaTime)
        {
            float k = t / fall;
            root.transform.SetPositionAndRotation(new Vector3(x, Mathf.Lerp(top, landY, k * k), 0), Quaternion.Euler(0, 0, spin * t));
            shadow.transform.position = new Vector3(x, landY, 0.3f);
            shadow.transform.localScale = new Vector3(halfWidth * 2.4f, halfWidth * 0.7f, 1f) * (0.5f + 0.5f * k) / ProceduralSprites.SoftGlow.bounds.size.x;
            shadow.color = new Color(0f, 0f, 0f, 0.15f + 0.4f * k);
            yield return null;
        }
        Destroy(shadow.gameObject);
        if (root == null) yield break;
        Vector3 foot = new Vector3(x, landY, 0);
        root.transform.SetPositionAndRotation(foot, Quaternion.identity);

        if (!stays)
        {
            Debris.BurstSprites(foot + Vector3.up * height * 0.4f, states, new Color(0.8f, 0.72f, 0.65f), 5, 0.2f, 4f);
            Fx.Play(Fx.SmokePuff, foot + Vector3.up * 0.2f, new Color(0.8f, 0.75f, 0.7f, 0.8f), 0.9f);
            Sfx.Play(Sfx.BlockerBreak, 0.35f, Random.Range(0.8f, 1.1f));
            CameraShake.Add(0.06f);
            if (harmful && Mathf.Abs(ronin.Bounds.center.x - x) < halfWidth + 0.3f) ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
            Destroy(root);
            yield break;
        }

        // It thuds into the row and stays: cover that only the Hyper Beam will break.
        Fx.Play(Fx.SmokePuff, foot + Vector3.up * 0.3f, new Color(0.8f, 0.75f, 0.7f, 0.9f), 2f);
        Debris.BurstSprites(foot + Vector3.up * 0.3f, states, new Color(0.8f, 0.72f, 0.65f), 6, 0.2f, 4f);
        Sfx.Play(Sfx.HammerSlam, 0.8f, 0.7f);
        CameraShake.Add(0.35f);
        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(halfWidth * 2f * 0.85f, height * 0.85f, 0.5f);
        box.center = Vector3.up * height / 2f;
        Blocker cover = root.AddComponent<Blocker>();
        cover.Init(int.MaxValue, look, states ?? new[] { look.sprite }, null);
        rocks.Add(new Rock { cover = cover, look = look, states = states, halfWidth = halfWidth, height = height });
        stayed();
    }

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

    // When its first bar empties the mainspring snaps: it overloads, lasering wildly in every direction as it cracks
    // open from the core out, then the shroud tears, the chest opens on its glowing core, and the clock runs fast from
    // then on.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        RestPoguru();
        // A break mid-Hour Strikes drops the armour with the rest of the move.
        armored = false;
        ShowEye(false);
        // It breaks open in its row, not wherever a cut-short dash left it.
        yield return BackToRow();

        // The mainspring slips: it seizes with a jolt.
        animator.Play(art.hit, false, null, art.actionFps * 0.5f);
        HitFlash.Flash(sprite, 0.15f);
        CameraShake.Add(0.3f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.75f);
        Music.Duck(0.15f, 8f);
        yield return new WaitForSeconds(0.4f);
        // The whole hall shakes with it and rocks rain down; one or two come to rest in the cover row and stay, which
        // its first move once it has broken open (the Hyper Beam) will need.
        StartCoroutine(Rockfall(OverloadSeconds + 0.6f, Random.Range(1, 3), false));
        yield return Overload();

        // It stops dead. A bell tolls once, and the air ripples with it.
        Sfx.Play(Sfx.MidnightChime, 1f, 0.7f);
        TimeRipple.Emit(Centre, 14f, 0.8f, 1.2f);
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
        WearPhaseArt();
        // The painting-drawn second form (152 px frames) carries its own burnt look; only the old recolour needs dimming.
        painted = art.idle != null && art.idle.Length > 0 && art.idle[0].rect.width > 140f;
        if (painted)
        {
            eyePixel = new Vector2(76.5f, 44.5f);
            corePixel = new Vector2(79.5f, 59.5f);
        }
        else bodyTint = Color.Lerp(bodyTint, new Color(0.75f, 0.7f, 0.8f), 0.5f);
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
        StartCoroutine(BurnShroud());
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
        hyperNext = true;
        breaking = false;
    }

    // How long it overloads before it stops dead, and how far apart its wild lasers fire at the start and the end.
    const float OverloadSeconds = 2.4f;
    const float WildLaserGapStart = 0.45f, WildLaserGapEnd = 0.07f;

    // It overloads: it shudders in place while cracks spread from its core over its body and hat, its clock racing,
    // each tick closer on the last, and its eye and core loose lasers in every direction, ever faster, turning molten
    // as the cracks open. The lasers are wild, not aimed: they scorch the ground but can't hurt the ronin. Without the
    // cracking art it shudders in its hit pose, sparks spitting from its joints.
    IEnumerator Overload()
    {
        Sprite[] cracks = Fx.Frames(Fx.UtsuCracking);
        StartCoroutine(Tremble(OverloadSeconds, 0.08f, 1.5f));
        int stage = -1;
        float nextTick = 0, tick = 0.3f, nextShot = 0.2f;
        for (float t = 0; t < OverloadSeconds; t += Time.deltaTime)
        {
            float k = t / OverloadSeconds;
            int now = cracks != null ? Mathf.Min(cracks.Length - 1, (int)(k * cracks.Length)) : (int)(k * 6);
            if (now != stage)
            {
                // Another crack splits open with a snap, spitting sparks.
                stage = now;
                if (cracks != null) animator.Play(new[] { cracks[stage] }, true);
                Vector3 split = Core + (Vector3)(Random.insideUnitCircle * sprite.bounds.extents.x * 0.5f);
                Fx.Play(Fx.Impact, split, stage < 3 ? Gold : Molten, 0.8f);
                Sfx.Play(Sfx.ParryClang, 0.35f, Random.Range(0.6f, 0.8f));
                ElementFx.Glow(Core, Color.Lerp(CoreRed, Molten, k), 0.5f, 1.5f + k * 2f, 0.3f, transform);
                CameraShake.Add(0.12f + 0.1f * k);
            }
            if (t >= nextTick)
            {
                Sfx.Play(Sfx.ClockTick, 0.7f, 1f + k * 0.8f);
                nextTick = t + tick;
                tick = Mathf.Max(0.05f, tick * 0.85f);
            }
            if (t >= nextShot)
            {
                StartCoroutine(WildLaser(k > 0.45f, Mathf.Lerp(0.4f, 0.2f, k)));
                nextShot = t + Mathf.Lerp(WildLaserGapStart, WildLaserGapEnd, k * k) * Random.Range(0.7f, 1.3f);
            }
            yield return null;
        }
    }

    // One wild laser from its eye or core: mostly raking the ground, some flung up and out to the sides, swinging as
    // it fires. It burns the ground where it lands; only a `harmful` one (the Hyper Beam's overload, not the phase
    // break's) hits the ronin, and cover stops that one.
    IEnumerator WildLaser(bool molten, float preview, bool harmful = false)
    {
        bool fromEye = Random.value < 0.4f;
        float angle = Random.value < 0.75f ? Random.Range(-160f, -20f) : Random.Range(-15f, 195f);
        float swing = Random.Range(-40f, 40f);
        float seconds = Random.Range(0.18f, 0.32f);

        // Previewed first: a thin flickering line where it will open fire, a fainter one where its swing will end, and
        // a glow growing on the ground where each lands, so even a wild shot reads before it comes.
        Color tint = molten ? Molten : CoreRed;
        Vector3 Reach(Vector3 from, float a, out bool ground)
        {
            Vector3 heading = Quaternion.Euler(0, 0, a) * Vector3.right;
            ground = heading.y < -0.05f;
            return ground ? from + heading * ((GroundY - from.y) / heading.y) : from + heading * 30f;
        }
        SpriteRenderer startLine = Bar(Core, Core, 0.04f, Color.clear, 54);
        SpriteRenderer endLine = Bar(Core, Core, 0.03f, Color.clear, 54);
        SpriteRenderer startMark = GroundMark(tint), endMark = GroundMark(tint);
        for (float t = 0; t < preview; t += Time.deltaTime)
        {
            float k = t / preview;
            float flicker = 0.6f + 0.4f * Mathf.Sin(Time.time * 60f);
            Vector3 from = fromEye ? Eye : Core;
            Vector3 start = Reach(from, angle, out bool startGround), end = Reach(from, angle + swing, out bool endGround);
            Stretch(startLine, from, start, 0.03f + 0.03f * k);
            Stretch(endLine, from, end, 0.02f);
            startLine.color = new Color(tint.r, tint.g, tint.b, (0.25f + 0.45f * k) * flicker);
            endLine.color = new Color(tint.r, tint.g, tint.b, 0.18f * flicker);
            PlaceMark(startMark, start, startGround, tint, k, 1f);
            PlaceMark(endMark, end, endGround, tint, k, 0.6f);
            yield return null;
        }
        RemoveTransient(startLine);
        RemoveTransient(endLine);
        RemoveTransient(startMark);
        RemoveTransient(endMark);

        Beam beam = MakeBeam(fromEye ? Eye : Core, 0.7f, molten);
        Sfx.Play(Sfx.LanternCharge, 0.3f, Random.Range(1.9f, 2.5f));
        CameraShake.Add(0.06f);
        for (float t = 0; t < seconds && beam.body != null; t += Time.deltaTime)
        {
            Vector3 from = fromEye ? Eye : Core;
            Vector3 heading = Quaternion.Euler(0, 0, angle + swing * t / seconds) * Vector3.right;
            bool ground = heading.y < -0.05f;
            Vector3 to = ground ? from + heading * ((GroundY - from.y) / heading.y) : from + heading * 30f;
            if (harmful && ground)
            {
                to = BeamEnd(from, to, out Blocker cover);
                if (cover == null) BeamHit(from, to);
                ground = cover == null;
            }
            DrawBeam(beam, from, to, t < 0.04f, ground);
            yield return null;
        }
        EndBeam(beam);
    }

    // A soft glow on the ground where a previewed laser will land.
    SpriteRenderer GroundMark(Color tint)
    {
        var mark = new GameObject("LaserMark").AddComponent<SpriteRenderer>();
        mark.sprite = ProceduralSprites.SoftGlow;
        mark.sortingOrder = 3;
        mark.color = Color.clear;
        transient.Add(mark.gameObject);
        return mark;
    }

    static void PlaceMark(SpriteRenderer mark, Vector3 at, bool onGround, Color tint, float k, float strength)
    {
        if (mark == null) return;
        mark.enabled = onGround;
        mark.transform.position = new Vector3(at.x, at.y, 0.3f);
        mark.transform.localScale = new Vector3(1.6f, 0.5f, 1f) * (0.5f + 0.7f * k);
        mark.color = new Color(tint.r, tint.g, tint.b, (0.2f + 0.45f * k) * strength);
    }

    void AddCore()
    {
        coreGlow = Projectile.CreateGlow(sprite.transform, 1.4f, sprite.sortingOrder + 1, CoreMolten);
        coreGlow.transform.position = Core + Vector3.back * 0.3f;
        StartCoroutine(PulseCore());
    }

    // The opened core burns molten orange, like the beams it fires, not the first form's red.
    static readonly Color CoreMolten = new Color(1f, 0.5f, 0.14f);

    // Where flames lick its second form, in its frames' pixels: the raised claw, then the torn hem's ragged ends.
    static readonly Vector2[] FlamePoints = { new Vector2(109f, 50f), new Vector2(40f, 106f), new Vector2(112f, 102f), new Vector2(64f, 121f), new Vector2(94f, 120f) };
    // How many of its own pixels tall each of those flames stands at most: the claw's burns tallest.
    static readonly float[] FlameScale = { 1.3f, 1f, 1f, 0.8f, 0.8f };
    const float EmberGap = 0.022f;
    const int MaxEmbers = 90;
    // Embers cool through these in hard steps, never blending: white-hot, gold, orange, ember red, a dying char.
    static readonly Color[] EmberSteps =
    {
        new Color(1f, 1f, 0.92f), new Color(1f, 0.86f, 0.36f), new Color(0.95f, 0.36f, 0.05f),
        new Color(0.62f, 0.12f, 0.04f), new Color(0.3f, 0.06f, 0.04f),
    };

    class Ember
    {
        public SpriteRenderer r;
        public Vector3 at, velocity;
        public float age, life, nextKick, drag;
    }
    readonly List<Ember> embers = new List<Ember>();

    // Embers in the air when the fight is torn down go with it.
    protected override void OnCleared()
    {
        foreach (Ember e in embers) if (e.r != null) Destroy(e.r.gameObject);
        embers.Clear();
        ClearStoppedTime();
        TimeRipple.Clear();
        plumbsOut = false;
        RestPoguru();
        foreach (Rock rock in rocks) if (rock.cover != null) Destroy(rock.cover.gameObject);
        rocks.Clear();
        hyperNext = false;
    }

    // One world pixel of its sprite, so its fire is drawn on its own pixel grid.
    float BodyPixel => sprite.sprite != null ? sprite.transform.lossyScale.x / sprite.sprite.pixelsPerUnit : 0.05f;

    Vector3 SnapToPixels(Vector3 at)
    {
        float pixel = BodyPixel;
        return new Vector3(Mathf.Round(at.x / pixel) * pixel, Mathf.Round(at.y / pixel) * pixel, at.z);
    }

    void SpawnEmber(Vector3 from, Vector3 velocity, float life)
    {
        if (embers.Count >= MaxEmbers) return;
        var ember = new GameObject("Ember").AddComponent<SpriteRenderer>();
        ember.sprite = ProceduralSprites.White;
        ember.sortingOrder = sprite.sortingOrder + 2;
        // Tiny specks: one of its own pixels, now and then two, so the sparks read as pixel art beside it.
        ember.transform.localScale = Vector3.one * BodyPixel * (Random.value < 0.85f ? 1f : 2f);
        embers.Add(new Ember { r = ember, at = from + Vector3.back * 0.25f, velocity = velocity, life = life, drag = Random.Range(0.5f, 2.5f) });
    }

    // Its second form burns, as in its painting: pixel flames lick its raised claw and the torn hem, and embers boil off
    // the shroud in fits, jerked about by gusts and kicks, some spat out sideways in bursts, cooling in hard steps on
    // its own pixel grid. Built in code on whatever frame it's in, so it follows every clip.
    IEnumerator BurnShroud()
    {
        Sprite[] flameFrames = ProceduralSprites.PixelFlame;
        var tongues = new List<SpriteRenderer>();
        var flameFrame = new float[FlamePoints.Length];
        for (int i = 0; i < FlamePoints.Length; i++)
        {
            var tongue = new GameObject("ShroudFlame").AddComponent<SpriteRenderer>();
            tongue.transform.SetParent(sprite.transform, false);
            tongue.sprite = flameFrames[0];
            tongue.sortingOrder = sprite.sortingOrder + 1;
            tongues.Add(tongue);
            flameFrame[i] = Random.Range(0f, flameFrames.Length);
        }
        // Heat glowing under its feet, beneath the body.
        var heat = new GameObject("Heat").AddComponent<SpriteRenderer>();
        heat.transform.SetParent(sprite.transform, false);
        heat.sprite = ProceduralSprites.SoftGlow;
        heat.sortingOrder = sprite.sortingOrder - 2;
        tongues.Add(heat);
        float nextEmber = 0f, nextBurst = Time.time + 0.5f, gustUntil = 0f, nextGust = Time.time + 1f;
        Vector3 gust = Vector3.zero;
        while (!IsDead && sprite != null)
        {
            // Its fire stands still while it holds time.
            if (timeHeld)
            {
                yield return null;
                continue;
            }
            float parent = sprite.transform.lossyScale.x;
            float pixel = BodyPixel;
            float throb = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            heat.transform.position = new Vector3(sprite.bounds.center.x, sprite.bounds.min.y + sprite.bounds.size.y * 0.12f, 0.1f);
            heat.transform.localScale = new Vector3(3.2f, 1.2f, 1f) / parent;
            heat.color = new Color(Molten.r, Molten.g * 0.8f, Molten.b, 0.18f + 0.08f * throb);

            // The flames jump between frames at an uneven rate and now and then flip, so no two lick alike.
            for (int i = 0; i < FlamePoints.Length; i++)
            {
                flameFrame[i] += Time.deltaTime * Random.Range(8f, 18f);
                SpriteRenderer tongue = tongues[i];
                tongue.sprite = flameFrames[(int)flameFrame[i] % flameFrames.Length];
                if (Random.value < 0.04f) tongue.flipX = !tongue.flipX;
                tongue.transform.position = SnapToPixels(OnSprite(FlamePoints[i].x, FlamePoints[i].y)) + Vector3.back * 0.2f;
                // Whole pixels only: stretched by a step now and then, never smoothly.
                float tall = FlameScale[i] * (Random.value < 0.15f ? 1.25f : 1f);
                tongue.transform.localScale = new Vector3(pixel, pixel * tall, 1f) / parent;
            }

            // Gusts: every so often every ember is shoved one way together.
            if (Time.time >= nextGust)
            {
                gust = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(-0.3f, 0.8f), 0);
                gustUntil = Time.time + Random.Range(0.15f, 0.35f);
                nextGust = Time.time + Random.Range(0.6f, 1.6f);
            }
            Vector3 gustNow = Time.time < gustUntil ? gust : Vector3.zero;

            Bounds b = sprite.bounds;
            if (Time.time >= nextEmber)
            {
                nextEmber = Time.time + EmberGap * Random.Range(0.3f, 1.7f);
                // Off the lower shroud, where the painting's hem burns.
                Vector3 from = new Vector3(Random.Range(b.min.x + b.size.x * 0.08f, b.max.x - b.size.x * 0.08f),
                    b.min.y + b.size.y * Random.Range(0.05f, 0.55f), 0);
                SpawnEmber(from, new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 2.4f), 0), Random.Range(0.4f, 1.2f));
            }
            // Bursts: the shroud spits a fistful of sparks out sideways from one ragged end.
            if (Time.time >= nextBurst)
            {
                nextBurst = Time.time + Random.Range(0.25f, 0.8f);
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 from = new Vector3(side < 0 ? b.min.x + b.size.x * 0.15f : b.max.x - b.size.x * 0.15f,
                    b.min.y + b.size.y * Random.Range(0.1f, 0.45f), 0);
                int count = Random.Range(6, 13);
                for (int n = 0; n < count; n++)
                {
                    SpawnEmber(from, new Vector3(side * Random.Range(1.5f, 4.5f), Random.Range(-0.5f, 2.5f), 0), Random.Range(0.25f, 0.6f));
                }
            }

            for (int i = embers.Count - 1; i >= 0; i--)
            {
                Ember e = embers[i];
                e.age += Time.deltaTime;
                if (e.age >= e.life || e.r == null)
                {
                    if (e.r != null) Destroy(e.r.gameObject);
                    embers.RemoveAt(i);
                    continue;
                }
                // Kicked about at random, rising as they go, slowed by the air.
                if (Time.time >= e.nextKick)
                {
                    e.velocity += new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-0.4f, 1.2f), 0);
                    e.nextKick = Time.time + Random.Range(0.04f, 0.16f);
                }
                e.velocity = Vector3.Lerp(e.velocity, new Vector3(0, 1.2f, 0), e.drag * Time.deltaTime);
                e.at += (e.velocity + gustNow) * Time.deltaTime;
                e.r.transform.position = SnapToPixels(e.at);
                // Cooling in hard steps, and guttering: some frames it blinks out.
                float k = e.age / e.life;
                Color c = EmberSteps[Mathf.Min(EmberSteps.Length - 1, (int)(k * EmberSteps.Length))];
                e.r.color = new Color(c.r, c.g, c.b, Random.value < 0.12f * k ? 0f : 1f);
            }
            yield return null;
        }
        foreach (SpriteRenderer t in tongues) if (t != null) Destroy(t.gameObject);
        OnCleared();
    }

    // The heart beats in time with the clock.
    IEnumerator PulseCore()
    {
        while (coreGlow != null && !IsDead)
        {
            float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 4f)), 4f);
            // On the heart in whatever pose it's in.
            coreGlow.transform.position = Core + Vector3.back * 0.3f;
            coreGlow.color = new Color(CoreMolten.r, CoreMolten.g, CoreMolten.b, 0.45f + 0.45f * pulse);
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
    // parried, it rings out and bursts the teeth around it. Once unwound, crimson darts bracket him on the off-beats,
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
                    BulletHell.MakeSealDart(BulletHell.Enlist(Shoot(from, (at + Vector3.right * off - from).normalized * 7.5f)), BulletHell.Crimson);
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
        yield return Wait(0.4f);
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
        yield return Wait(0.6f);
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

    // A fragment of its burning lace, hung in stopped time: unparryable and through cover, but a cut breaks it.
    // Without the phase-two lace art, a molten seal-dart.
    Arrow Needle(Vector3 from, Vector3 velocity)
    {
        Arrow shot = BulletHell.Enlist(Shoot(from, velocity));
        Sprite[] frames = Fx.Frames(Fx.UtsuLaceP2);
        if (frames == null) return BulletHell.MakeSealDart(shot, Molten);
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
