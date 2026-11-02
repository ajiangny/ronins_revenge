using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 2's warlord: a karakuri automaton wearing a dead pilgrim's hat and burial shroud, with a lantern-red heart
// and a long blade on a jointed arm. Everything it does keeps time to a ticking escapement. Its signature move, Hour
// Strikes, ends in a giant cleave that only a parry answers; sent back, it jams the gears.
public partial class Utsusemi : Boss
{
    // Per bar: it fights two, the second after its phase break.
    public const int StartingHealth = 120;

    internal static readonly Color Steel = new Color(0.55f, 0.68f, 0.95f);
    static readonly Color Gold = new Color(0.9f, 0.75f, 0.4f);
    static readonly Color CoreRed = new Color(1f, 0.22f, 0.18f);
    static readonly Color Lace = new Color(0.92f, 0.94f, 1f, 0.95f);
    static readonly Color Gunmetal = new Color(0.28f, 0.3f, 0.36f);
    // Its digital afterimages split into its own steel blue and gold, like a signal out of register.
    static readonly Color GhostBlue = new Color(0.4f, 0.62f, 1f);
    static readonly Color GhostGold = new Color(1f, 0.8f, 0.35f);

    enum Move { KasaWheel, Lattice, HourStrikes, PlumbLines, HeartLantern, Midnight, Escapement, StoppedHour, HyperBeam }
    // A move runs under its name ("Utsusemi_<Move>", Boss.Attack), so the shots and blows it lands can be told apart.
    IEnumerator As(Move move, IEnumerator routine) => Attacking($"Utsusemi_{move}", routine);

    // Each value is (phase 1, phase 2): once unwound, its clock runs fast.
    // A soul the Emperor summons keeps a longer rest and shorter combos: it already overlaps his own attacks.
    float Recovery => (secondPhase ? 0.15f : 0.35f) * (Summoned ? 2f : 1f);
    float ComboChance => secondPhase ? 0.9f : 0.75f;
    int MaxCombo => (secondPhase ? 4 : 3) - (Summoned ? 1 : 0);
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
                yield return As(Move.HyperBeam, HyperBeam(false));
                continue;
            }
            // Winding: it stands still for a beat between combos. This is the opening.
            yield return Wait(Recovery);

            // Low on its last bar, once: the Thirteenth Hour.
            if (secondPhase && !thirteenthStruck && Health <= MaxHealth * ThirteenthAt && !swordOut && !swordWheeling)
            {
                yield return Attacking("Utsusemi_ThirteenthHour", ThirteenthHour());
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
                    yield return Attacking("Utsusemi_PendulumKick", PendulumKick());
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
                    case Move.KasaWheel: yield return As(move, KasaWheel()); break;
                    case Move.Lattice: yield return As(move, LaceLattice()); break;
                    case Move.HourStrikes: yield return As(move, HourStrikes()); break;
                    case Move.PlumbLines: yield return As(move, PlumbLines()); break;
                    case Move.HeartLantern: yield return As(move, HeartLantern()); break;
                    case Move.Midnight: yield return As(move, Midnight()); break;
                    case Move.Escapement: yield return As(move, Escapement()); break;
                    case Move.StoppedHour: yield return As(move, StoppedHour()); break;
                    case Move.HyperBeam: yield return As(move, HyperBeam(true)); break;
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

}
