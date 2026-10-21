using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 2's warlord: a karakuri automaton wearing a dead pilgrim's hat and burial shroud, with a lantern-red heart
// and a long blade on a jointed arm. Everything it does keeps time to a ticking escapement. Its signature move, the
// Escapement, can be parried straight back to jam its gears.
public class Utsusemi : Boss
{
    public const int StartingHealth = 90;

    static readonly Color Steel = new Color(0.55f, 0.68f, 0.95f);
    static readonly Color Gold = new Color(0.9f, 0.75f, 0.4f);
    static readonly Color CoreRed = new Color(1f, 0.22f, 0.18f);
    static readonly Color Lace = new Color(0.92f, 0.94f, 1f, 0.95f);
    static readonly Color Gunmetal = new Color(0.28f, 0.3f, 0.36f);

    enum Move { KasaWheel, Lattice, Escapement, PlumbLines, HeartLantern }

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
        BossLine.Escapement => "Tick...",
        BossLine.PlumbLines => "Swing.",
        BossLine.HeartLantern => "Behold... my heart.",
        _ => null,
    };

    bool secondPhase;
    bool breaking;
    bool tock;
    int hatsOut;
    Move? lastMove;
    int movesSinceEscapement;
    SpriteRenderer eye;
    SpriteRenderer coreGlow;
    readonly List<Arrow> lattice = new List<Arrow>();
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
    protected override Sprite[] IdleClip => hatsOut > 0 && bareIdle != null ? bareIdle : art.idle;
    float TopOfScreen => CameraShake.Center.y + Camera.main.orthographicSize + 0.5f;
    float GroundY => ronin.Feet.y - 0.3f;
    float LeftEdge => CameraShake.Center.x - HalfWidth;
    float RightEdge => CameraShake.Center.x + HalfWidth;
    Vector3 Head => new Vector3(Centre.x, sprite.bounds.max.y - sprite.bounds.size.y * 0.12f, 0);
    Vector3 Core => Centre + Vector3.up * sprite.bounds.size.y * 0.12f;
    Vector3 Shoulder => Centre + new Vector3(sprite.bounds.extents.x * 0.5f, sprite.bounds.size.y * 0.1f, 0);

    protected override IEnumerator Fight()
    {
        while (true)
        {
            while (breaking) yield return null;
            // Winding: it stands still for a beat between combos. This is the opening.
            yield return Wait(Recovery);

            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            bool escapementUsed = false;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                yield return Stride(RandomX(2.5f));
                yield return Wait(0.15f);

                Move move = NextMove(escapementUsed);
                lastMove = move;
                escapementUsed |= move == Move.Escapement;
                movesSinceEscapement = move == Move.Escapement ? 0 : movesSinceEscapement + 1;
                switch (move)
                {
                    case Move.KasaWheel: yield return KasaWheel(); break;
                    case Move.Lattice: yield return LaceLattice(); break;
                    case Move.Escapement: yield return Escapement(); break;
                    case Move.PlumbLines: yield return PlumbLines(); break;
                    case Move.HeartLantern: yield return HeartLantern(); break;
                }
            }
        }
    }

    // Never the same move twice in a row; the Escapement comes at most once a combo but at least every third move,
    // so the parry rhythm stays part of the fight. The Heart Lantern only opens in the second phase.
    Move NextMove(bool escapementUsed)
    {
        if (!escapementUsed && movesSinceEscapement >= 2 && lastMove != Move.Escapement) return Move.Escapement;
        var options = new List<Move> { Move.KasaWheel, Move.Lattice, Move.PlumbLines };
        if (!escapementUsed) options.Add(Move.Escapement);
        if (secondPhase) options.Add(Move.HeartLantern);
        if (lastMove is Move last) options.Remove(last);
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

    // Kasa Wheel: it hurls its wide hat, which zig-zags down the field like the army's own march, rebounding off
    // the screen edges and shedding tassel weights as it goes, then climbs back to its head. With the hat off, its
    // slit-eye is exposed and hits deal double. In the second phase a phantom hat flies the mirror path.
    IEnumerator KasaWheel()
    {
        if (Random.value < 0.6f) Speak(BossLine.KasaWheel);
        PlayAction(art.parry);
        ElementFx.Glow(Head, Gold, 1f, 3f, 0.4f, transform);
        Sfx.Play(Sfx.ParrySwing, 0.8f, 0.6f);
        // The hat leaves its hand on the throw clip's last frame.
        yield return Wait(0.55f);

        float dir = ronin.Bounds.center.x < anchor.x ? -1f : 1f;
        StartCoroutine(FlyHat(dir, false));
        if (secondPhase) StartCoroutine(FlyHat(-dir, true));
        ShowEye(true);
        Sfx.Play(Sfx.HatWhirr);

        // It waits, head bare, for the hats to come home; the wait is part of the move, so it keeps the watchdog fed.
        for (float t = 0; t < 12f && hatsOut > 0; t += Time.deltaTime)
        {
            Beat();
            yield return null;
        }
        ShowEye(false);
    }

    IEnumerator FlyHat(float dir, bool phantom)
    {
        hatsOut++;
        Arrow hat = Instantiate(arrowPrefab, Head, Quaternion.identity);
        // Launched straight down so its heading stays level; Steer then flies it sideways without turning it.
        hat.Launch(Vector3.down * 0.01f, 5f);
        hat.MakeUnbreakable(null);
        Sprite[] frames = Fx.Frames(Fx.UtsuKasa);
        Color tint = phantom ? new Color(0.7f, 0.8f, 1f, 0.5f) : Color.white;
        if (frames != null) hat.Restyle(frames, 14f, tint, new Vector2(2.6f, 1.18f), 0.55f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 2.6f);
        else hat.Restyle(ProceduralSprites.Disc, phantom ? new Color(0.4f, 0.45f, 0.55f, 0.5f) : Gunmetal, new Vector2(2.6f, 0.7f), 0.55f, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 2.6f);
        transient.Add(hat.gameObject);

        float speed = secondPhase ? 14f : 12f;
        // Three passes: from its own height down to just over the cover, so the tassels it sheds rain on the ronin
        // but the hat itself leaves the blockers standing for the Heart Lantern.
        BlockerWall wall = FindAnyObjectByType<BlockerWall>();
        float top = Head.y - 1f;
        float bottom = (wall != null ? wall.TopY : ronin.TopY + 2f) + 1.4f;
        int pass = 0;
        bool dropping = false;
        float nextY = top;
        float nextTassel = Time.time + 0.4f;
        float bound = HalfWidth - 1.3f;
        while (hat != null && pass < 3)
        {
            Vector3 position = hat.transform.position;
            float pace = Mathf.Max(Tempo, 0.4f);
            if (dropping)
            {
                hat.Steer(Vector3.down * (14f * pace));
                if (position.y <= nextY) dropping = false;
            }
            else
            {
                hat.Steer(Vector3.right * (dir * speed * pace));
                bool atEdge = dir > 0 ? position.x >= CameraShake.Center.x + bound : position.x <= CameraShake.Center.x - bound;
                if (atEdge)
                {
                    dir = -dir;
                    pass++;
                    nextY = Mathf.Lerp(top, bottom, pass / 2f);
                    dropping = pass < 3;
                    Tassel(position);
                    Tick(0.6f);
                }
            }
            if (Time.time >= nextTassel)
            {
                nextTassel = Time.time + (secondPhase ? 0.45f : 0.6f);
                Tassel(position);
            }
            yield return null;
        }

        // Home again, straight to its head.
        while (hat != null && !IsDead)
        {
            Vector3 toHead = Head - hat.transform.position;
            if (toHead.magnitude < 0.6f) break;
            hat.Steer(toHead.normalized * 18f);
            yield return null;
        }
        if (hat != null)
        {
            transient.Remove(hat.gameObject);
            Destroy(hat.gameObject);
            Tick(0.7f);
        }
        hatsOut--;
        // Hat back on its head.
        if (hatsOut == 0 && !IsDead) PlayIdle();
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

    // Escapement: the jointed arm reaches out over the ronin and the blade tip ticks down towards him, re-aiming on
    // every tick, then thrusts. Only a parry answers it: sent back, it jams the gears and staggers the automaton.
    // In the second phase it ticks six times, faster, and thrusts twice.
    IEnumerator Escapement()
    {
        // Always named: the callout is the cue to start counting.
        Speak(BossLine.Escapement);
        int ticks = secondPhase ? 6 : 4;
        float tickTime = secondPhase ? 0.32f : 0.45f;
        float lowest = ronin.Bounds.center.y + 3.2f;

        SpriteRenderer arm = Bar(Shoulder, Shoulder, 0.16f, Gunmetal, 54);
        SpriteRenderer guide = Bar(Shoulder, Shoulder, 0.05f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.35f));
        Vector3 tip = Shoulder;
        for (int i = 1; i <= ticks; i++)
        {
            tip = new Vector3(ronin.Bounds.center.x, Mathf.Lerp(Shoulder.y, lowest, i / (float)ticks), 0);
            Stretch(arm, Shoulder, tip, 0.16f);
            Stretch(guide, tip, new Vector3(tip.x, GroundY, 0), 0.05f);
            ElementFx.Glow(Shoulder, CoreRed, 0.6f, 1.4f, tickTime * 0.6f);
            ElementFx.Glow(tip, CoreRed, 0.4f, 1f, tickTime * 0.6f);
            Tick(0.9f);
            yield return Wait(tickTime);
        }

        Thrust(tip);
        if (secondPhase)
        {
            // The follow-up: the arm snaps over to where the ronin dodged and thrusts again.
            yield return Wait(0.35f);
            tip = new Vector3(ronin.Bounds.center.x, lowest, 0);
            Stretch(arm, Shoulder, tip, 0.16f);
            Tick(0.9f);
            yield return Wait(0.15f);
            Thrust(tip);
        }
        yield return Wait(0.3f);
        foreach (SpriteRenderer bar in new[] { arm, guide })
        {
            transient.Remove(bar.gameObject);
            Destroy(bar.gameObject);
        }
    }

    void Thrust(Vector3 tip)
    {
        PlayAction(art.attack);
        Arrow blade = Shoot(tip, Vector3.down * 20f);
        Sprite[] frames = Fx.Frames(Fx.UtsuThrust);
        if (frames != null) blade.Restyle(frames, 14f, Color.white, new Vector2(0.7f, 2.45f), 0.35f, new Color(1f, 0.2f, 0.18f, 0.45f), 2f);
        else blade.Restyle(ProceduralSprites.White, new Color(0.85f, 0.9f, 1f), new Vector2(0.22f, 2.4f), 0.35f, new Color(1f, 0.2f, 0.18f, 0.5f), 2f);
        blade.MakeReflectable(transform);
        Sfx.Play(Sfx.ParrySwing, 1f, 0.5f);
        CameraShake.Add(0.15f);
    }

    // Plumb Lines: gold weights drop from its tassels on silk threads and swing as pendulums across the lower
    // field. A hit on a weight cuts it loose.
    IEnumerator PlumbLines()
    {
        if (Random.value < 0.5f) Speak(BossLine.PlumbLines);
        int count = secondPhase ? 5 : Random.Range(3, 5);
        float pivotY = anchor.y - 0.5f;
        var pivots = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(LeftEdge + 3f, RightEdge - 3f, (i + 0.5f) / count) + Random.Range(-1f, 1f);
            pivots.Add(new Vector3(x, pivotY, 0));
            ElementFx.Glow(pivots[i], Gold, 0.4f, 1.2f, 0.5f);
        }
        Tick(0.6f);
        yield return Wait(0.3f);
        Tick(0.6f);
        yield return Wait(0.3f);

        var weights = new Arrow[count];
        var threads = new SpriteRenderer[count];
        var lengths = new float[count];
        var phases = new float[count];
        Sprite[] frames = Fx.Frames(Fx.UtsuPlumb);
        for (int i = 0; i < count; i++)
        {
            // Some swing low enough to reach the ronin, others only through the cover above him.
            float lowY = ronin.Bounds.center.y + Random.Range(0f, 3f);
            lengths[i] = pivotY - lowY;
            phases[i] = Random.Range(0f, Mathf.PI * 2f);
            weights[i] = Shoot(pivots[i], Vector3.down * 0.1f);
            if (frames != null) weights[i].Restyle(frames, 8f, Color.white, new Vector2(0.9f, 1.07f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.35f), 1.1f);
            else weights[i].Restyle(ProceduralSprites.Disc, Gold, new Vector2(0.6f, 0.8f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 1.1f);
            threads[i] = Bar(pivots[i], pivots[i], 0.06f, new Color(0.95f, 0.95f, 1f, 0.6f), 50);
        }
        Sfx.Play(Sfx.ShurikenThrow, 0.7f, 0.4f);

        const float drop = 0.7f;
        const float amplitude = 28f * Mathf.Deg2Rad;
        float swing = secondPhase ? 4.5f : 4f;
        float omega = Mathf.PI * 2f / (secondPhase ? 2f : 2.4f);
        // Steered once per physics step: a velocity worked out from a render frame's delta overshoots the next step.
        for (float t = 0; t < drop + swing; t += Time.fixedDeltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            for (int i = 0; i < count; i++)
            {
                if (weights[i] == null)
                {
                    if (threads[i] != null) threads[i].gameObject.SetActive(false);
                    continue;
                }
                float length = lengths[i] * Mathf.Clamp01(t / drop);
                float angle = t < drop ? 0f : amplitude * Mathf.Sin(omega * (t - drop) + phases[i]) * Mathf.Clamp01((t - drop) / 0.6f);
                Vector3 target = pivots[i] + new Vector3(Mathf.Sin(angle), -Mathf.Cos(angle), 0) * length;
                weights[i].Steer((target - weights[i].transform.position) / Time.fixedDeltaTime);
                Stretch(threads[i], pivots[i], weights[i].transform.position, 0.06f);
            }
            yield return new WaitForFixedUpdate();
        }

        // The threads snap and the weights fall.
        for (int i = 0; i < count; i++)
        {
            if (weights[i] != null) weights[i].Steer(Vector3.down * 9f);
            transient.Remove(threads[i].gameObject);
            Destroy(threads[i].gameObject);
        }
        Sfx.Play(Sfx.WardShatter, 0.5f, 1.5f);
    }

    // Heart Lantern (second phase only): the core charges and a slow beam sweeps the ground from one side to the
    // other. Only cover stops it, and it burns through the cover it hits.
    IEnumerator HeartLantern()
    {
        yield return Stride(CameraShake.Center.x);
        Speak(BossLine.HeartLantern);
        ElementFx.Glow(Core, CoreRed, 0.5f, 4f, 0.8f, transform);
        for (int i = 0; i < 3; i++)
        {
            Tick(0.7f);
            yield return Wait(0.2f);
        }

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
        transient.Remove(guide.gameObject);
        Destroy(guide.gameObject);
        yield return Wait(0.3f);

        PlayAction(art.attack);
        SpriteRenderer beam = Bar(Core, Core, 0.5f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.8f));
        SpriteRenderer heart = Bar(Core, Core, 0.16f, new Color(1f, 0.9f, 0.85f), 56);
        const float sweep = 3.2f;
        float nextBurn = 0f;
        float nextHum = 0f;
        for (float t = 0; t < sweep; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            float x = Mathf.Lerp(from, to, t / sweep);
            Vector3 end = BeamEnd(x, out Blocker cover);
            Stretch(beam, Core, end, 0.5f + 0.1f * Mathf.Sin(t * 40f));
            Stretch(heart, Core, end, 0.16f);
            if (cover != null)
            {
                if (Time.time >= nextBurn)
                {
                    nextBurn = Time.time + 0.25f;
                    cover.Hit(end);
                }
            }
            else
            {
                // Where the beam crosses the ronin's height, it burns him.
                Vector3 ground = new Vector3(x, GroundY, 0);
                float k = Mathf.InverseLerp(Core.y, ground.y, ronin.Bounds.center.y);
                float beamX = Mathf.Lerp(Core.x, ground.x, k);
                if (Mathf.Abs(ronin.Bounds.center.x - beamX) < 0.45f) ronin.TakeHit(new Vector3(beamX, ronin.Bounds.center.y, 0));
            }
            if (Time.time >= nextHum)
            {
                nextHum = Time.time + 0.3f;
                Sfx.Play(Sfx.FireBlast, 0.35f, 0.6f);
                Fx.Play(Fx.Impact, end, CoreRed, 0.7f);
            }
            yield return null;
        }
        foreach (SpriteRenderer bar in new[] { beam, heart })
        {
            transient.Remove(bar.gameObject);
            Destroy(bar.gameObject);
        }
    }

    // Where a beam from the core aimed at x on the ground stops: on the nearest blocker in the way, or the ground.
    Vector3 BeamEnd(float x, out Blocker cover)
    {
        cover = null;
        Vector3 ground = new Vector3(x, GroundY, 0);
        Vector3 delta = ground - Core;
        float nearest = delta.magnitude;
        foreach (RaycastHit hit in Physics.RaycastAll(Core, delta.normalized, delta.magnitude))
        {
            if (hit.distance < nearest && hit.collider.TryGetComponent(out Blocker blocker))
            {
                nearest = hit.distance;
                cover = blocker;
            }
        }
        return Core + delta.normalized * nearest;
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
        if (!secondPhase && HealthFraction <= 0.5f) StartCoroutine(PhaseBreak());
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
