using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 2's warlord: a karakuri automaton wearing a dead pilgrim's hat and burial shroud, with a lantern-red heart
// and a long blade on a jointed arm. Everything it does keeps time to a ticking escapement. Its signature move, Hour
// Strikes, ends in a giant cleave that only a parry answers; sent back, it jams the gears.
public class Utsusemi : Boss
{
    public const int StartingHealth = 120;

    static readonly Color Steel = new Color(0.55f, 0.68f, 0.95f);
    static readonly Color Gold = new Color(0.9f, 0.75f, 0.4f);
    static readonly Color CoreRed = new Color(1f, 0.22f, 0.18f);
    static readonly Color Lace = new Color(0.92f, 0.94f, 1f, 0.95f);
    static readonly Color Gunmetal = new Color(0.28f, 0.3f, 0.36f);

    enum Move { KasaWheel, Lattice, HourStrikes, PlumbLines, HeartLantern, Midnight }

    // Each value is (phase 1, phase 2): once unwound, its clock runs fast.
    float Recovery => secondPhase ? 0.8f : 1.5f;
    float ComboChance => secondPhase ? 0.75f : 0.5f;
    int MaxCombo => secondPhase ? 3 : 2;
    float StepTime => secondPhase ? 0.11f : 0.16f;
    const float StepLength = 0.9f;

    public override string Title => "UTSUSEMI";
    public override string Epithet => "The Hollow Pilgrim";
    public override Color Colour => Steel;

    public override string Line(BossLine line) => line switch
    {
        BossLine.Intro => "Tick... tock... The pilgrim who wore this hat walked a thousand roads. I have walked every one of them again. Yours... ends here.",
        BossLine.Phase => "Mainspring... unwound. I will not... stop.",
        BossLine.Defeat => "The bell... has... stopped...",
        BossLine.Stagger => "Gears... seized...",
        BossLine.KasaWheel => "Kasa.",
        BossLine.Lattice => "Unravel.",
        BossLine.HourStrikes => "Remember the hours... one... by one.",
        BossLine.PlumbLines => "Swing.",
        BossLine.HeartLantern => "Behold... my heart.",
        BossLine.Midnight => "Midnight.",
        _ => null,
    };

    bool secondPhase;
    bool breaking;
    bool tock;
    int hatsOut;
    // Its sword wheeling out on the Kasa Wheel's loop in the second phase.
    bool swordWheeling;
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

    public void SetBareIdle(Sprite[] frames)
    {
        bareIdle = frames != null && frames.Length > 0 ? frames : null;
    }

    protected override bool Busy => breaking;
    protected override float StaggerSeconds => secondPhase ? 2f : 2.5f;
    // Its slit-eye is only exposed while the hat is off.
    protected override int DamageMultiplier => hatsOut > 0 ? 2 : 1;
    // The bare head wins while both are out: the missing hat is what the ronin is watching for.
    protected override Sprite[] IdleClip => hatsOut > 0 && bareIdle != null ? bareIdle
        : (swordOut || swordWheeling) && unarmedIdle != null ? unarmedIdle : art.idle;
    float TopOfScreen => CameraShake.Center.y + Camera.main.orthographicSize + 0.5f;
    float GroundY => ronin.Feet.y - 0.3f;
    float LeftEdge => CameraShake.Center.x - HalfWidth;
    float RightEdge => CameraShake.Center.x + HalfWidth;
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
                yield return Stride(RandomX(2.5f));
                yield return Wait(0.15f);

                Move move = NextMove(hourStruck);
                lastMove = move;
                hourStruck |= move == Move.HourStrikes;
                movesSinceHours = move == Move.HourStrikes ? 0 : movesSinceHours + 1;
                bool laser = move == Move.HeartLantern || move == Move.Midnight;
                // Both lasers throw the sword, so they wait for it to wheel home.
                while (laser && swordWheeling)
                {
                    Beat();
                    yield return null;
                }
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
            for (float t = 0; t < StepTime; t += Time.deltaTime * Tempo)
            {
                anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / StepTime));
                yield return null;
            }
            anchor = to;
            Tick(0.3f);
            yield return Wait(0.06f);
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
        HatPath path = AimHat();
        List<SpriteRenderer> trail = TrailDots(path);
        // The hat leaves its hand on the throw clip's last frame.
        yield return Wait(0.55f);

        StartCoroutine(FlyHat(path, trail, false));
        if (secondPhase) StartCoroutine(ThrowSwordWheel(0.8f));
        ShowEye(true);
        Sfx.Play(Sfx.HatWhirr);
    }

    IEnumerator ThrowSwordWheel(float delay)
    {
        swordWheeling = true;
        yield return Wait(delay);
        if (IsDead) yield break;
        HatPath path = AimHat();
        StartCoroutine(FlyHat(path, TrailDots(path), true));
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
        float laneY = ronin.Bounds.center.y;
        float roninX = ronin.Bounds.center.x;
        // It skims toward the side with more open ground, coming in steeply from the other side.
        float sweepDir = roninX < CameraShake.Center.x ? 1f : -1f;
        float bound = HalfWidth - 1.5f;
        var path = new HatPath { sweepDir = sweepDir };
        path.a0 = Head;
        path.a1 = Head + new Vector3(-sweepDir * 5f, -1.5f, 0);
        path.a2 = new Vector3(roninX - sweepDir * 2f, laneY + 4f, 0);
        path.a3 = new Vector3(roninX, laneY, 0);
        float skimX = Mathf.Clamp(roninX + sweepDir * 8f, CameraShake.Center.x - bound, CameraShake.Center.x + bound);
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

    // Faint gold dots along the whole loop, so the ronin can read where it will dip.
    List<SpriteRenderer> TrailDots(HatPath path)
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
                SpriteRenderer dot = Bar(p, p + Vector3.right * 0.22f, 0.22f, new Color(Gold.r, Gold.g, Gold.b, 0.55f), 52);
                dot.sprite = ProceduralSprites.Glow;
                dots.Add(dot);
            }
        }
        return dots;
    }

    // The hat (or, with sword set, the sword wheeling end over end) flown around the loop as an unbreakable shot.
    IEnumerator FlyHat(HatPath path, List<SpriteRenderer> trail, bool sword)
    {
        if (sword) swordWheeling = true;
        else hatsOut++;
        Arrow hat = Instantiate(arrowPrefab, Head, Quaternion.identity);
        // Launched straight down so its heading stays level; Steer then flies it along the curve without turning it.
        hat.Launch(Vector3.down * 0.01f, 5f);
        hat.MakeUnbreakable(null);
        Sprite[] frames = Fx.Frames(sword ? Fx.UtsuSword : Fx.UtsuKasa);
        Vector3 bladeMiddle = Vector3.zero;
        if (sword)
        {
            Sprite blade = frames != null ? frames[0] : ProceduralSprites.White;
            Vector2 size = frames != null ? (Vector2)blade.bounds.size * SwordScale : new Vector2(0.15f, SwordCoverHeight);
            hat.Restyle(blade, frames != null ? Color.white : Lace, size, 0.6f, new Color(Steel.r, Steel.g, Steel.b, 0.4f), 2.2f);
            // The sword art pivots where the blade meets the ground; it wheels about its middle instead.
            bladeMiddle = Vector3.Scale(blade.bounds.center, hat.Art.transform.lossyScale);
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
                if (Random.value < 0.4f) Afterimage(hat.Art, new Color(Steel.r, Steel.g, Steel.b, 0.4f));
            }

            // The trail fades out behind the hat as it flies.
            int passed = Mathf.Min(trail.Count, Mathf.FloorToInt(trail.Count * t / total));
            for (; dotsGone < passed; dotsGone++) RemoveTransient(trail[dotsGone]);
            if (!sword && t < legSeconds[0] && Time.time >= nextTassel)
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
            if (!IsDead) Fx.Play(Fx.Impact, sword ? Shoulder : Head, sword ? Steel : Gold, 0.9f);
        }
        else if (hat != null)
        {
            transient.Remove(hat.gameObject);
            Destroy(hat.gameObject);
            Tick(0.7f);
        }
        if (sword)
        {
            swordWheeling = false;
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
        PlayAction(art.attack);
        ElementFx.Glow(Centre, Lace, 1f, 5f, 0.5f, transform);
        Sfx.Play(Sfx.FrostChill, 0.6f, 0.8f);
        yield return Wait(0.5f);

        int layers = secondPhase ? 2 : 1;
        for (int layer = 0; layer < layers; layer++)
        {
            if (layer > 0) yield return Wait(0.8f);
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

    // Hour Strikes: gold hour numerals (I, II, III...) appear along the ground out of order, so the dashes zig-zag. In
    // turn, each burns red; it dashes down out of its row onto that hour, chops it with a ground-cracking blow and
    // dashes back up, so he weaves between the marks. Then, back in its row, it strikes the hour: a clock mark settles
    // under the ronin and follows him while its core glows and the blade draws back over three ticks, and a giant
    // crescent cleave flies at him. It hunts him, breaks his shots and cuts through cover, so only a parry answers it:
    // sent back, it jams the gears and staggers the automaton. In the second phase there are five hours, faster.
    IEnumerator HourStrikes()
    {
        int count = secondPhase ? 5 : 3;
        float beat = secondPhase ? 0.32f : 0.5f;
        Vector3 row = anchor;

        // The announcement, in super armour: it calls the move and stands its ground in a gold shell while the
        // hours light up one at a time, then go dark. Nothing staggers or freezes it until the shell drops.
        Speak(BossLine.HourStrikes);
        armored = true;
        SpriteRenderer[] shell = ArmorShell();
        Sfx.Play(Sfx.GearGrind, 0.6f, 1.4f);
        List<HourMark> marks = PlaceHours(count);
        yield return HoldArmor(shell, 0.5f);
        foreach (HourMark mark in marks)
        {
            mark.Show(Gold);
            Tick(0.7f);
            yield return HoldArmor(shell, secondPhase ? 0.4f : 0.55f);
        }
        yield return HoldArmor(shell, 0.45f);
        foreach (HourMark mark in marks) mark.Hide(0.3f);
        Tick(0.4f);
        yield return HoldArmor(shell, 0.35f);
        armored = false;
        foreach (SpriteRenderer part in shell) RemoveTransient(part);
        // A phase break the armour held off lands now.
        OnDamaged();

        // The dashes: from memory now.
        foreach (HourMark mark in marks)
        {
            Beat();
            Tick(0.9f);
            yield return Wait(beat);
            yield return Dash(new Vector3(mark.x, Landing(), row.z));
            yield return Chop(mark);
            yield return Dash(row);
            PlayIdle();
            yield return Wait(beat * 0.4f);
        }

        Beat();
        yield return StrikeTheHour();
        PlayIdle();
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
    IEnumerator HoldArmor(SpriteRenderer[] shell, float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            Beat();
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

    // The hours stand in evenly spread spots around the ronin, but they're numbered out of order: the dashes zig-zag
    // back and forth across the lane (3 1 5 2 4, never 1 2 3 4 5), so the order has to be remembered, not guessed.
    List<HourMark> PlaceHours(int count)
    {
        const float margin = 1.2f, spacing = 5f, jitter = 0.5f;
        float lo = LeftEdge + margin, hi = RightEdge - margin;
        float span = Mathf.Min((count - 1) * spacing, hi - lo);
        float left = Mathf.Clamp(ronin.Bounds.center.x - span / 2f, lo, hi - span);
        var spots = new float[count];
        for (int i = 0; i < count; i++)
        {
            float x = left + (count > 1 ? span * i / (count - 1) : 0f);
            spots[i] = Mathf.Clamp(x + Random.Range(-jitter, jitter), lo, hi);
        }

        int[] order = ZigZag(count);
        var marks = new List<HourMark>();
        for (int hour = 0; hour < count; hour++) marks.Add(new HourMark(this, hour + 1, spots[order[hour]]));
        return marks;
    }

    // A random order of the spots 0..count-1 whose every step turns back the other way, so no three hours run in a
    // line. Found by shuffling until one fits; with five spots about one shuffle in seven does.
    static int[] ZigZag(int count)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            bool turns = true;
            for (int i = 2; i < count && turns; i++) turns = (order[i] - order[i - 1]) * (order[i - 1] - order[i - 2]) < 0;
            if (turns) return order;
        }
        return order;
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

    // A straight clockwork dash, eased hard into a dead stop, leaving steel afterimages along the way.
    IEnumerator Dash(Vector3 to)
    {
        const float seconds = 0.2f;
        Vector3 from = anchor;
        PlayFrames(dashFrames, 1, -1, true, 30f);
        Sfx.Play(Sfx.ParrySwing, 0.6f, 1.4f);
        int ghosts = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            if (k >= ghosts / 3f && ghosts < 3)
            {
                Afterimage(new Color(Steel.r, Steel.g, Steel.b, 0.5f - ghosts * 0.12f));
                ghosts++;
            }
            anchor = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k) * (1f - k));
            yield return null;
        }
        anchor = to;
        Tick(0.6f);
    }

    const float ChopReach = 0.95f;
    // The chop's blade meets the ground on this frame.
    const int ChopImpactFrame = 5;

    IEnumerator Chop(HourMark mark) => Chop(mark.x, mark);

    IEnumerator Chop(float x, HourMark mark)
    {
        const float fps = 20f;
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
        yield return Wait(0.18f);
    }

    // The hour slash draws the blade back by this frame, holds it through the ticks, and lets the cleave go on the
    // release frame, as its swing passes the front.
    const int SlashDrawnFrame = 5;
    const int SlashReleaseFrame = 7;

    // The hour: a clock mark under the ronin follows him for three ticks while the core glows and the blade is drawn
    // back, then the swing flings a giant crescent cleave at him.
    IEnumerator StrikeTheHour()
    {
        FxPlayer hour = Fx.Play(Fx.UtsuClockMark, ronin.Feet, Color.white, 3.2f, null, 6);
        if (hour != null) transient.Add(hour.gameObject);
        SpriteRenderer fallback = hour == null ? HourGlow() : null;
        Sfx.Play(Sfx.LanternCharge, 0.8f, 1.3f);
        PlayFrames(slashFrames, 1, SlashDrawnFrame, true, 14f);

        float tickTime = secondPhase ? 0.32f : 0.42f;
        for (int i = 0; i < 3; i++)
        {
            Tick(1f);
            ElementFx.Glow(Core, i == 2 ? Color.white : CoreRed, 0.7f, 1.6f + i * 0.6f, tickTime * 0.8f, transform);
            for (float t = 0; t < tickTime; t += Time.deltaTime * Tempo)
            {
                FollowRonin(hour, fallback);
                yield return null;
            }
        }
        if (hour != null) RemoveTransient(hour.GetComponent<SpriteRenderer>());
        if (fallback != null) RemoveTransient(fallback);

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
        StartCoroutine(HuntRonin(cleave));
        Sfx.Play(Sfx.HourSlash, 1f);
        Sfx.Play(Sfx.MidnightChime, 0.6f, 1.4f);
        CameraShake.Add(0.35f);
        ElementFx.Glow(from, Color.white, 0.8f, 3f, 0.25f);
        yield return Wait(0.6f);
    }

    // Fast, but it turns as fast as the ronin runs: there's no outrunning it, only parrying it.
    const float HourSpeed = 11f;

    IEnumerator HuntRonin(Arrow cleave)
    {
        while (cleave != null && ronin != null && cleave.transform.position.y > ronin.Bounds.center.y)
        {
            float dx = ronin.Bounds.center.x - cleave.transform.position.x;
            cleave.Steer(new Vector3(Mathf.Clamp(dx * 6f, -ronin.speed * 1.1f, ronin.speed * 1.1f), -HourSpeed, 0));
            yield return new WaitForFixedUpdate();
        }
    }

    void FollowRonin(FxPlayer hour, SpriteRenderer fallback)
    {
        Vector3 at = new Vector3(ronin.Bounds.center.x, ronin.Feet.y, -1f);
        if (hour != null) hour.transform.position = at;
        if (fallback != null) fallback.transform.position = at;
    }

    SpriteRenderer HourGlow()
    {
        SpriteRenderer glow = Bar(ronin.Feet, ronin.Feet, 1f, new Color(Gold.r, Gold.g, Gold.b, 0.7f), 6);
        glow.sprite = ProceduralSprites.Glow;
        glow.transform.rotation = Quaternion.identity;
        glow.transform.localScale = new Vector3(2.2f, 0.7f, 1f);
        return glow;
    }

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

    // A gold Roman numeral standing on the ground over a soft pool of light, drawn from bars: I and V strokes are all
    // the first six hours need. Shown during the announcement and then hidden; it flashes red again when struck.
    class HourMark
    {
        const float Height = 0.8f;
        public readonly float x;
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

        public void Show(Color colour)
        {
            Hide(0f);
            Vector3 foot = new Vector3(x, owner.GroundY + 0.15f, 0);
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

        // The hour it was: it flashes up red under the blow, then fades.
        public void Struck()
        {
            Show(CoreRed);
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

        const float drop = 0.6f, fadeIn = 0.8f, damp = 0.6f, reel = 0.5f, snapBack = 0.3f;
        // When each weight was cut loose, if it was.
        var cutAt = new float[count];
        for (int i = 0; i < count; i++) cutAt[i] = -1f;
        const float amplitude = 28f * Mathf.Deg2Rad;
        // Each swings this far behind its left-hand neighbour, so a ripple runs along the row.
        const float waveStep = 0.7f;
        float swing = secondPhase ? 4.5f : 4f;
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
                    // Cut loose: its empty thread snaps back up into the portal, which closes after it.
                    if (threads[i] == null) continue;
                    if (cutAt[i] < 0) cutAt[i] = t;
                    float left = 1f - (t - cutAt[i]) / snapBack;
                    if (left > 0) Stretch(threads[i], pivots[i], pivots[i] + Vector3.down * length * left * left, 0.05f);
                    else
                    {
                        RemoveTransient(threads[i]);
                        StartCoroutine(ClosePortal(portals[i]));
                        portals[i] = null;
                    }
                    continue;
                }
                Vector3 target = pivots[i] + hang;
                weights[i].Steer((target - weights[i].transform.position) / Time.fixedDeltaTime);
                weights[i].Art.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                Stretch(threads[i], pivots[i], weights[i].transform.position, 0.06f);
                // A gold streak trails each weight as it drops.
                if (t < drop && Random.value < 0.5f) Afterimage(weights[i].Art, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            }
            yield return new WaitForFixedUpdate();
        }

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

    // The portals and their threads sit just behind the automaton's own sprite.
    const int PortalOrder = 4;
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

    // The sword hurled at the ronin: a steel glint shows where it will land, it raises the blade overhead and throws,
    // and the sword wheels end over end to plant itself point-down there, cutting him if he hasn't moved.
    IEnumerator ThrowSword(float x)
    {
        Vector3 ground = new Vector3(x, GroundY + 0.15f, 0);
        SpriteRenderer glint = Bar(ground, ground, 1f, new Color(Steel.r, Steel.g, Steel.b, 0.7f), 6);
        glint.sprite = ProceduralSprites.Glow;
        glint.transform.rotation = Quaternion.identity;
        glint.transform.localScale = new Vector3(1.8f, 0.5f, 1f);
        const float fps = 12f;
        PlayFrames(swordThrowFrames, 0, SwordReleaseFrame, true, fps);
        Sfx.Play(Sfx.ParrySwing, 0.7f, 0.7f);
        Tick(0.8f);
        yield return Wait(swordThrowFrames != null ? (SwordReleaseFrame + 1) / fps : 0.45f);

        swordOut = true;
        PlayIdle();
        Sfx.Play(Sfx.SwordWhirl);
        yield return FlySword(Head + Vector3.up * 0.8f, ground, 0.55f, 2f, 1.2f);
        RemoveTransient(glint);

        plantedSword = PlantSword(ground);
        if (Mathf.Abs(ronin.Bounds.center.x - x) < 0.6f) ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
        if (Fx.Play(Fx.UtsuChop, ground, Color.white, 1.8f) == null) Fx.Play(Fx.Impact, ground, Steel, 1f);
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
        Sprite[] frames = Fx.Frames(Fx.UtsuSword);
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
            if (Random.value < 0.6f) Afterimage(flying, new Color(Steel.r, Steel.g, Steel.b, 0.45f));
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
        Fx.Play(Fx.Impact, new Vector3(from.x, GroundY + 0.15f, 0), Steel, 0.8f);
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

        FxPlayer planted = Fx.Play(Fx.UtsuSword, ground, Color.white, SwordScale, null, 4);
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
        // A steel ward round the blade, showing how far its cover reaches.
        SpriteRenderer ward = Kunai.CreateGlow(sword.transform, 1f, 3, new Color(Steel.r, Steel.g, Steel.b, 0.5f));
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
                yield return HoldingHands(ThrowSword(AtRonin(3f)), left, right, leftEdge, rightEdge);
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
        if (Fx.Play(Fx.UtsuCharge, Core, Color.white, 2.2f, transform) == null) ElementFx.Glow(Core, CoreRed, 2f, 0.5f, 0.7f, transform);
        ElementFx.Glow(Core, CoreRed, 0.5f, 4f, 0.9f, transform);
        Sfx.Play(Sfx.LanternCharge);
        CameraShake.Add(0.15f);
        for (int i = 0; i < 3; i++)
        {
            Tick(0.7f);
            yield return Wait(0.25f);
        }
    }

    // A laser in three layers redrawn every frame (a soft red glow, a flickering red body, a white-hot core), with a
    // flare at the core, sparks where it lands and a trail of fire it leaves burning on the ground.
    class Beam
    {
        public SpriteRenderer glow, body, core;
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
            glow = Bar(from, from, 1f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.35f), 53),
            body = Bar(from, from, 1f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.9f), 55),
            core = Bar(from, from, 1f, new Color(1f, 0.92f, 0.88f), 56),
            flare = Fx.Play(Fx.UtsuBeamFlare, from, Color.white, flareSize, transform, 57),
            hit = Fx.Play(Fx.UtsuBeamHit, from, Color.white, 1.3f, null, 57),
        };
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
        Stretch(beam.glow, from, to, 1.6f * flicker);
        Stretch(beam.body, from, to, 0.42f * flicker);
        Stretch(beam.core, from, to, 0.14f);
        beam.body.color = flash ? Color.white : new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.9f);
        beam.glow.color = flash ? new Color(1f, 1f, 1f, 0.5f) : new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.35f);
        if (beam.hit != null) beam.hit.transform.position = to + Vector3.back * 0.3f;
        else if (Time.time >= beam.nextSpark)
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

    // Where a beam from `from` to `end` crosses the ronin's height, it burns him (unless cover stopped it above him).
    void HitIfCrossed(Vector3 from, Vector3 end)
    {
        float y = ronin.Bounds.center.y;
        if (end.y > y + 0.2f) return;
        float beamX = Mathf.Lerp(from.x, end.x, Mathf.InverseLerp(from.y, end.y, y));
        if (Mathf.Abs(ronin.Bounds.center.x - beamX) < 0.45f) ronin.TakeHit(new Vector3(beamX, y, 0));
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

    Vector3 BeamEnd(Vector3 from, Vector3 ground, out Blocker cover)
    {
        cover = null;
        Vector3 delta = ground - from;
        float nearest = delta.magnitude;
        foreach (RaycastHit hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude))
        {
            if (hit.distance < nearest && hit.collider.TryGetComponent(out Blocker blocker))
            {
                nearest = hit.distance;
                cover = blocker;
            }
        }
        return from + delta.normalized * nearest;
    }

    static void Stretch(SpriteRenderer bar, Vector3 from, Vector3 to, float width)
    {
        if (bar == null) return;
        Vector3 delta = to - from;
        bar.transform.position = (from + to) / 2f + Vector3.back * 0.2f;
        bar.transform.rotation = delta.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.right, delta) : Quaternion.identity;
        bar.transform.localScale = new Vector3(delta.magnitude, width, 1f);
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
        if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 2f) == null) Fx.Play(Fx.Impact, Centre, Gold, 2f);
        Debris.BurstSprites(Centre, art.deathParticles, Gold, 12, 0.8f, 7f);
        Sfx.Play(Sfx.GearGrind);
    }

    protected override void OnDamaged()
    {
        if (!secondPhase && !armored && !breaking && HealthFraction <= 0.5f) StartCoroutine(PhaseBreak());
    }

    // At half health the mainspring snaps: the shroud tears, the chest opens on its glowing core, and the clock
    // runs fast from then on.
    IEnumerator PhaseBreak()
    {
        secondPhase = true;
        breaking = true;
        Interrupt();
        ShowEye(false);
        SetInvulnerable(1.8f);
        CameraShake.Add(0.5f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.75f);
        Sfx.Play(Sfx.StageStart, 1f, 0.8f);
        if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 3f) == null) Fx.Play(Fx.Explosion, Centre, Gold, 2.5f);
        Debris.BurstSprites(Centre, art.deathParticles, Lace, 30, 1.2f, 8f);
        bodyTint = Color.Lerp(bodyTint, new Color(0.75f, 0.7f, 0.8f), 0.5f);
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
        Speak(BossLine.Phase);
        yield return new WaitForSeconds(1.8f);
        breaking = false;
        RestartFight();
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
