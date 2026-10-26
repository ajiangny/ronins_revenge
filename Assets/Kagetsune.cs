using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 4's final boss, "The Thousand-Soul Throne": Emperor Kagetsune, colossal and deathless inside an armor of a
// thousand sealed souls, with the Soul Coffin on his shoulder as a hammer. He barely moves; everything he does is
// slow, heavy and telegraphed. Phase 1 is his law (the Coffer Slam, the Toll of Fealty, the Ofuda Decree, the
// Thousand-Soul Wail, and the parry-only Last Toll). His phase break snaps the coffin's chains and he becomes the
// Unchained Wraith: faster, with skull swarms, lashing chains, the parry-only Soul Coffin, the Night of a Thousand
// Bells, and the souls of the three bosses summoned out of the coffin to use their own moves on Oni.
public class Kagetsune : Boss
{
    public const int StartingHealth = 170;
    public const string PhaseEpithet = "The Unchained Wraith";

    static readonly Color Crimson = new Color(0.78f, 0.1f, 0.14f);
    static readonly Color Teal = new Color(0.3f, 1f, 0.82f);
    static readonly Color Bronze = new Color(0.88f, 0.62f, 0.32f);
    static readonly Color Paper = new Color(0.96f, 0.9f, 0.78f);
    static readonly Color SealRed = new Color(0.95f, 0.15f, 0.12f);

    // Parry-only moves: one is offered once this many moves have passed, and forced by ParryAtLatest.
    const int ParryEvery = 3;
    const int ParryAtLatest = 4;
    // In phase 2 a summoned soul comes at least this often, and the Night of a Thousand Bells every UltimateEvery moves.
    const int EchoAtLatest = 3;
    const int UltimateEvery = 6;
    const float ExposedSeconds = 2.2f;

    float Recovery => secondPhase ? 0.6f : 1.05f;
    float ComboChance => secondPhase ? 0.55f : 0.35f;
    int MaxCombo => secondPhase ? 3 : 2;
    float ShotSpeed => secondPhase ? 1.2f : 1f;

    public override string Title => "KAGETSUNE";
    public override string Epithet => "The Thousand-Soul Throne";
    public override Color Colour => Crimson;

    public override string Line(BossLine line) => line switch
    {
        BossLine.Intro => "Every bell upon me is a soul that knelt, Oni. One still hangs silent... yours.",
        BossLine.Phase => "Kneel! KNEEL! You are MINE, every one of you...",
        BossLine.Defeat => "The bells... why do they not ring...?",
        BossLine.Hurt => "Insolence.",
        BossLine.Stagger => "You dare ring MY bell?",
        BossLine.CofferSlam => "Bow.",
        BossLine.TollOfFealty => "Answer me.",
        BossLine.OfudaDecree => "By my decree.",
        BossLine.SoulWail => "Hear them, little demon.",
        BossLine.LastToll => "Hear the last toll.",
        BossLine.WraithSwarm => "Go. Fetch him.",
        BossLine.BrokenChains => "Chains do not hold me.",
        BossLine.SoulCoffin => "Into the coffin!",
        BossLine.ThousandBells => "A thousand bells, Oni! Ring!",
        BossLine.SummonBenibana => "Dance for me, Benibana.",
        BossLine.EchoBenibana => "Forgive me, Oni... he has my bell.",
        BossLine.SummonUtsusemi => "Strike the hour, clock.",
        BossLine.EchoUtsusemi => "The hour... strikes... Run... boy.",
        BossLine.SummonShirogane => "Rise, Shirogane. Kneel, and strike.",
        BossLine.EchoShirogane => "Move, Oni. I cannot hold my blades.",
        BossLine.KilledOni => "Back to your hill, little demon. I will be here. I am always here.",
        _ => null,
    };

    public override string Line(BossLine line, int take) => line == BossLine.Hurt ? take switch
    {
        1 => "A scratch, little demon.",
        2 => "Hm.",
        _ => "Insolence.",
    } : Line(line);

    // The souls he summons speak for themselves.
    public override string Speaker(BossLine line) => line switch
    {
        BossLine.EchoBenibana => "BENIBANA",
        BossLine.EchoUtsusemi => "UTSUSEMI",
        BossLine.EchoShirogane => "SHIROGANE",
        _ => Title,
    };

    public override Color SpeakerColour(BossLine line) => line switch
    {
        BossLine.EchoBenibana or BossLine.EchoUtsusemi or BossLine.EchoShirogane => Teal,
        _ => Colour,
    };

    enum Move
    {
        CofferSlam, Toll, Ofuda, Wail, LastToll,
        Swarm, Chains, SoulCoffin, ThousandBells,
        EchoBenibana, EchoUtsusemi, EchoShirogane,
    }

    bool secondPhase => SecondPhase;
    bool breaking;
    // The Night of a Thousand Bells is a set piece the stall watchdog must wait out.
    bool ultimate;
    // After a parried coffin or the ultimate he stands open, taking double damage.
    float exposedUntil;
    Move? lastMove;
    int movesSinceParry;
    int movesSinceEcho;
    int movesSinceUltimate;
    // The souls take their turns in order the first time round, then come at random.
    int nextEcho;
    float coffinThrownAt = float.NegativeInfinity;

    // Lanes the ofuda have sealed: no parry inside them until they lift.
    readonly List<(float x, float halfWidth, float until)> seals = new List<(float, float, float)>();
    bool SealActive => seals.Exists(s => Time.time < s.until);

    protected override bool HasSecondPhase => true;
    protected override bool Busy => breaking || ultimate;
    protected override float StaggerSeconds => secondPhase ? 1.7f : 2.2f;
    protected override int DamageMultiplier => Time.time < exposedUntil ? 2 : 1;
    public override float DefeatSeconds => 11f;

    // The bosses whose souls he can summon: their art and the scale each is drawn at.
    readonly Dictionary<BossKind, (CharacterArt art, float scale)> echoes = new Dictionary<BossKind, (CharacterArt, float)>();

    public void SetEcho(BossKind kind, CharacterArt echoArt, float scale)
    {
        if (echoArt != null) echoes[kind] = (echoArt, scale);
    }

    // Where his body parts sit on the art: the soul-fire chest, the bells at his waist, the coffin-hammer's head.
    Vector3 Extents => sprite.bounds.extents;
    // Measured on his PixelLab frames, which leave room round the figure on a square canvas.
    Vector3 Chest => Centre + Vector3.up * (Extents.y * 0.06f);
    Vector3 Waist => Centre + Vector3.down * (Extents.y * 0.25f);
    Vector3 HammerHead => Centre + new Vector3(Extents.x * 0.45f, Extents.y * 0.45f, 0);
    Vector3 Shoulder(float side) => Centre + new Vector3(side * Extents.x * 0.3f, Extents.y * 0.25f, 0);

    SpriteRenderer chestGlow;
    SpriteRenderer eyes;

    void Start()
    {
        // The soul-fire in his open chest, always burning; it flares with each blow.
        chestGlow = Glowing(Chest, Teal, 2.2f, 4, false);
        chestGlow.transform.SetParent(transform, true);
    }

    void LateUpdate()
    {
        if (chestGlow == null) return;
        chestGlow.transform.position = Chest + Vector3.back * 0.1f;
        float pulse = 0.35f + 0.12f * Mathf.Sin(Time.time * 3.1f) + (Time.time < exposedUntil ? 0.3f : 0f);
        chestGlow.color = new Color(Teal.r, Teal.g, Teal.b, pulse);
        if (eyes != null) eyes.transform.position = Centre + Vector3.up * (Extents.y * 0.44f) + Vector3.back * 0.1f;
    }

    protected override IEnumerator Fight()
    {
        ronin.ParrySealed = InSealedLane;
        while (breaking) yield return null;
        while (true)
        {
            yield return Wait(Recovery);
            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                if (Random.value < 0.4f) yield return Stride(NearX(1.5f, 4f));
                Move move = NextMove();
                movesSinceParry = move == Move.LastToll || move == Move.SoulCoffin ? 0 : movesSinceParry + 1;
                movesSinceEcho = IsEcho(move) ? 0 : movesSinceEcho + 1;
                movesSinceUltimate = move == Move.ThousandBells ? 0 : movesSinceUltimate + 1;
                lastMove = move;
                yield return Play(move);
                // A set piece ends the combo.
                if (move == Move.ThousandBells || IsEcho(move)) i = combo;
                yield return Wait(secondPhase ? 0.25f : 0.4f);
            }
        }
    }

    static bool IsEcho(Move move) => move == Move.EchoBenibana || move == Move.EchoUtsusemi || move == Move.EchoShirogane;

    IEnumerator Play(Move move) => move switch
    {
        Move.CofferSlam => CofferSlam(),
        Move.Toll => TollOfFealty(secondPhase ? 5 : 3),
        Move.Ofuda => OfudaDecree(),
        Move.Wail => ThousandSoulWail(),
        Move.LastToll => LastToll(),
        Move.Swarm => WraithSwarm(),
        Move.Chains => BrokenChains(),
        Move.SoulCoffin => SoulCoffin(),
        Move.ThousandBells => NightOfAThousandBells(),
        Move.EchoBenibana => EchoBenibana(),
        Move.EchoUtsusemi => EchoUtsusemi(),
        _ => EchoShirogane(),
    };

    Move NextMove()
    {
        if (secondPhase && movesSinceUltimate >= UltimateEvery - 1 && lastMove != null) return Move.ThousandBells;
        Move parry = secondPhase ? Move.SoulCoffin : Move.LastToll;
        if (movesSinceParry >= ParryAtLatest) return parry;
        if (secondPhase && movesSinceEcho >= EchoAtLatest) return NextEcho();

        var options = new List<Move> { Move.CofferSlam, Move.Toll, Move.Ofuda, Move.Wail };
        if (secondPhase)
        {
            options.Add(Move.Swarm);
            options.Add(Move.Swarm);
            options.Add(Move.Chains);
        }
        else
        {
            // The slam is his signature: weighted twice.
            options.Add(Move.CofferSlam);
        }
        if (movesSinceParry >= ParryEvery) options.Add(parry);
        if (lastMove is Move last) options.Remove(last);
        Move pick = options[Random.Range(0, options.Count)];
        // Rather than open a seal over a parry-only blow, it waits for its own turn.
        if (pick == Move.Ofuda && movesSinceParry >= ParryEvery) pick = Move.Toll;
        if (secondPhase && movesSinceEcho >= 2 && Random.value < 0.35f) return NextEcho();
        return pick;
    }

    Move NextEcho()
    {
        var order = new[] { Move.EchoBenibana, Move.EchoUtsusemi, Move.EchoShirogane };
        Move pick = nextEcho < order.Length ? order[nextEcho++] : order[Random.Range(0, order.Length)];
        if (pick == lastMove) pick = order[(System.Array.IndexOf(order, pick) + 1) % order.Length];
        return pick;
    }

    // --- Movement ---

    // He doesn't blink or glide: he takes a few heavy steps, and the ground shakes under each.
    IEnumerator Stride(float x)
    {
        float distance = x - anchor.x;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(distance) / 1.4f), 1, 3);
        float step = distance / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 from = anchor;
            Vector3 to = from + Vector3.right * step;
            for (float t = 0; t < 0.32f; t += Time.deltaTime * Tempo)
            {
                anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 0.32f));
                yield return null;
            }
            anchor = to;
            CameraShake.Add(0.12f);
            Sfx.Play(Sfx.MarchStep, 0.9f, 0.45f);
            yield return Wait(0.1f);
        }
    }

    // --- Shots and telegraphs ---

    // A soul-skull: a breakable teal orb. Parried, it flies home at him as a freed soul.
    Arrow Skull(Vector3 from, Vector3 velocity)
    {
        Arrow skull = Shoot(from, velocity * ShotSpeed);
        Sprite[] frames = Fx.Frames(Fx.KageSkull);
        if (frames != null) skull.Restyle(frames, 10f, Color.white, new Vector2(0.95f, 0.95f), 0.32f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 1.5f);
        else skull.Restyle(ProceduralSprites.Disc, new Color(0.75f, 1f, 0.92f), new Vector2(0.6f, 0.6f), 0.32f, new Color(Teal.r, Teal.g, Teal.b, 0.55f), 1.5f);
        skull.PassesShots = true;
        skull.Parried = FreeSoul;
        return skull;
    }

    // A parried skull is a soul stolen back: it flies home at him wrapped in its own teal fire, ringing as it goes.
    static void FreeSoul(Kunai riposte)
    {
        if (riposte == null) return;
        riposte.Tint(new Color(0.8f, 1f, 0.95f), Teal);
        if (Sfx.Has(Sfx.BellSilenced)) Sfx.Play(Sfx.BellSilenced, 0.45f, 1.3f);
        else Sfx.Play(Sfx.MidnightChime, 0.3f, 1.9f);
    }

    // Fans of skulls aimed at the ronin; an offset fan fills the gaps the last one left.
    void SkullFan(Vector3 from, int count, float spread, float speed, bool offset)
    {
        Vector3 aim = (ronin.Bounds.center - from).normalized;
        float step = count > 1 ? 2f * spread / (count - 1f) : 0f;
        var volley = new List<Arrow>();
        for (int i = 0; i < count; i++)
        {
            float angle = (count > 1 ? Mathf.Lerp(-spread, spread, i / (count - 1f)) : 0f) + (offset ? step / 2f : 0f);
            volley.Add(Skull(from, Quaternion.Euler(0, 0, angle) * aim * speed));
        }
        IgnoreEachOther(volley);
    }

    // A soft glow sprite, tracked with the attack unless told otherwise.
    SpriteRenderer Glowing(Vector3 at, Color colour, float size, int order = 55, bool track = true)
    {
        var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
        glow.sprite = ProceduralSprites.Glow;
        glow.color = colour;
        glow.sortingOrder = order;
        glow.transform.position = at + Vector3.back * 0.1f;
        glow.transform.localScale = Vector3.one * size;
        if (track) transient.Add(glow.gameObject);
        return glow;
    }

    void Drop(Component thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    // A wide faint band down a lane that narrows and brightens over the tell, so the blow lands as it closes.
    IEnumerator CloseLanes(List<SpriteRenderer> bands, List<float> xs, float fromWidth, float toWidth, Color colour, float seconds, int beats = 3, float peak = 0.45f)
    {
        int rung = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            if (k >= rung / (float)beats && rung < beats)
            {
                rung++;
                Toll(0.35f, 0.8f + 0.12f * rung);
            }
            float width = Mathf.Lerp(fromWidth, toWidth, Mathf.SmoothStep(0, 1, k));
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i] == null) continue;
                Stretch(bands[i], new Vector3(xs[i], TopOfScreen, 0), new Vector3(xs[i], GroundY, 0), width);
                bands[i].color = new Color(colour.r, colour.g, colour.b, 0.12f + peak * k);
            }
            yield return null;
        }
    }

    // His bell: the generated toll, or a low chime until it's made.
    static void Toll(float volume, float pitch)
    {
        if (Sfx.Has(Sfx.BellToll)) Sfx.Play(Sfx.BellToll, volume, pitch);
        else Sfx.Play(Sfx.MidnightChime, volume, pitch * 0.55f);
    }

    static void Slam(float volume)
    {
        if (Sfx.Has(Sfx.HammerSlam)) Sfx.Play(Sfx.HammerSlam, volume);
        else Sfx.Play(Sfx.BossStagger, volume, 0.5f);
    }

    float ClampX(float x) => Mathf.Clamp(x, LeftEdge + 1f, RightEdge - 1f);

    // Rings round the ronin and closes on him, then flashes as the blow comes: the parry beat.
    IEnumerator LockOn(float seconds, Color colour)
    {
        SpriteRenderer ring = Glowing(ronin.Bounds.center, new Color(colour.r, colour.g, colour.b, 0f), 5f, 58);
        SpriteRenderer core = Glowing(ronin.Bounds.center, new Color(1f, 1f, 1f, 0f), 1f, 59);
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            ring.transform.position = core.transform.position = ronin.Bounds.center + Vector3.back * 0.1f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(5f, 1.8f, Mathf.SmoothStep(0, 1, k));
            ring.color = new Color(colour.r, colour.g, colour.b, 0.25f + 0.4f * k);
            core.color = new Color(1f, 1f, 1f, 0.5f * k);
            yield return null;
        }
        ring.color = Color.white;
        Sfx.Play(Sfx.BladeRing, 0.6f, 0.7f);
        yield return Wait(0.15f);
        Drop(ring);
        Drop(core);
    }

    // --- Phase 1 ---

    // Coffer Slam: the hammer marks the ronin's lane (two in phase 2), comes down, and a shockwave runs down it.
    // The first cover in its way takes the blow and breaks; past that it strikes the ronin. Skulls burst from the
    // crack and curve back down at him.
    IEnumerator CofferSlam()
    {
        Bark(BossLine.CofferSlam, 7f);
        var xs = new List<float> { ClampX(ronin.Bounds.center.x) };
        if (secondPhase) xs.Add(ClampX(xs[0] + (Random.value < 0.5f ? -1f : 1f) * Random.Range(3f, 4.5f)));
        var bands = new List<SpriteRenderer>();
        foreach (float x in xs) bands.Add(Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), 2.6f, new Color(Crimson.r, Crimson.g, Crimson.b, 0.12f)));
        SpriteRenderer raised = Glowing(HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.4f), 2.5f);
        PlayAction(art.attack);
        yield return CloseLanes(bands, xs, 2.6f, 1.5f, Crimson, secondPhase ? 0.75f : 0.95f);
        Drop(raised);
        foreach (SpriteRenderer band in bands) Drop(band);

        Slam(1f);
        CameraShake.Add(0.55f);
        var waves = new List<SpriteRenderer>();
        var ends = new List<Vector3>();
        foreach (float x in xs)
        {
            Vector3 top = new Vector3(x, anchor.y - Extents.y * 0.5f, 0);
            Vector3 end = BeamEnd(top, new Vector3(x, GroundY, 0), out Blocker cover);
            ends.Add(end);
            // A white-hot core inside a broad soul-fire glow.
            SpriteRenderer halo = Bar(top, top, 1f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 55);
            halo.sprite = ProceduralSprites.Glow;
            waves.Add(halo);
            waves.Add(Bar(top, top, 1f, new Color(0.9f, 1f, 0.96f, 0.9f), 56));
            if (cover != null) StartCoroutine(BreakAfter(cover, 0.18f));
        }
        bool hit = false;
        for (float t = 0; t < 0.22f; t += Time.deltaTime)
        {
            float k = t / 0.22f;
            for (int i = 0; i < xs.Count; i++)
            {
                Vector3 top = new Vector3(xs[i], anchor.y - Extents.y * 0.5f, 0);
                Vector3 tip = Vector3.Lerp(top, ends[i], k);
                Stretch(waves[2 * i], top, tip, 2.6f);
                Stretch(waves[2 * i + 1], top, tip, 0.7f * (1f - 0.4f * k));
                if (!hit && Mathf.Abs(ronin.Bounds.center.x - xs[i]) < 0.95f && tip.y <= ronin.Bounds.center.y + 0.3f)
                {
                    hit = true;
                    ronin.TakeHit(new Vector3(xs[i], ronin.Bounds.center.y, 0));
                }
            }
            yield return null;
        }
        foreach (Vector3 end in ends)
        {
            Fx.Play(Fx.Impact, end, Teal, 2.2f);
            Fx.Play(Fx.SmokePuff, end, new Color(0.25f, 0.25f, 0.28f, 0.8f), 2f);
            GroundFire.Light(end);
            Debris.BurstSprites(end, art.deathParticles, Bronze, 10, 0.8f, 7f);
        }
        foreach (SpriteRenderer wave in waves) StartCoroutine(FadeOut(wave, 0.3f));
        yield return Wait(0.15f);
        foreach (Vector3 end in ends) StartCoroutine(SkullBurst(end, secondPhase ? 4 : 3));
        yield return Wait(0.5f);
    }

    IEnumerator BreakAfter(Blocker cover, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (cover == null) yield break;
        cover.Demolish();
        Sfx.Play(Sfx.BlockerBreak, 0.8f, 0.8f);
    }

    IEnumerator FadeOut(SpriteRenderer thing, float seconds)
    {
        if (thing == null) yield break;
        Color from = thing.color;
        for (float t = 0; t < seconds && thing != null; t += Time.deltaTime)
        {
            thing.color = new Color(from.r, from.g, from.b, from.a * (1f - t / seconds));
            yield return null;
        }
        Drop(thing);
    }

    // Skulls leap up out of the crack, hang for a beat, then turn and dive at the ronin.
    IEnumerator SkullBurst(Vector3 at, int count)
    {
        var skulls = new List<Arrow>();
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-40f, 40f, count > 1 ? i / (count - 1f) : 0.5f);
            skulls.Add(Skull(at + Vector3.up * 0.4f, Quaternion.Euler(0, 0, angle) * Vector3.up * 6f / ShotSpeed));
        }
        IgnoreEachOther(skulls);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.6f);
        yield return Wait(0.45f);
        foreach (Arrow skull in skulls)
        {
            if (skull == null) continue;
            skull.Turn((ronin.Bounds.center - skull.transform.position).normalized * 7.5f * ShotSpeed);
        }
    }

    // Toll of Fealty: his bells ring, and each toll looses a fan of soul-skulls at the ronin, each fan's gaps
    // where the last one's skulls were.
    IEnumerator TollOfFealty(int tolls)
    {
        Bark(BossLine.TollOfFealty, 7f);
        PlayAction(art.parry);
        for (int i = 0; i < tolls; i++)
        {
            SpriteRenderer bell = Glowing(Waist, new Color(Bronze.r, Bronze.g, Bronze.b, 0.7f), 1.6f, 56);
            yield return Wait(0.22f);
            Toll(0.75f, 1f - 0.05f * i);
            Fx.Play(Fx.Impact, Waist, Teal, 1.6f);
            SkullFan(Waist, secondPhase ? 7 : 5, secondPhase ? 55f : 45f, 6.5f, i % 2 == 1);
            Drop(bell);
            yield return Wait(secondPhase ? 0.42f : 0.55f);
        }
    }

    // Ofuda Decree: talisman strips drift down and hang in two or three lanes, one always the ronin's. Inside a
    // sealed lane the law holds: no parry. Then a slam or a toll comes while he's caught in it.
    IEnumerator OfudaDecree()
    {
        Bark(BossLine.OfudaDecree, 7f);
        var lanes = Lanes(6);
        float halfWidth = (RightEdge - LeftEdge - 2.4f) / 12f;
        int mine = 0;
        for (int i = 1; i < lanes.Count; i++)
        {
            if (Mathf.Abs(lanes[i] - ronin.Bounds.center.x) < Mathf.Abs(lanes[mine] - ronin.Bounds.center.x)) mine = i;
        }
        var chosen = new List<float> { lanes[mine] };
        var others = new List<float>(lanes);
        others.RemoveAt(mine);
        for (int i = 0; i < (secondPhase ? 2 : 1); i++)
        {
            int pick = Random.Range(0, others.Count);
            chosen.Add(others[pick]);
            others.RemoveAt(pick);
        }
        if (Sfx.Has(Sfx.OfudaFlutter)) Sfx.Play(Sfx.OfudaFlutter, 0.8f);
        else Sfx.Play(Sfx.MistSwirl, 0.6f, 1.4f);

        // The strips fall in a loose row across each lane, swaying as they come.
        var strips = new List<(SpriteRenderer strip, float x, float sway)>();
        var curtains = new List<SpriteRenderer>();
        foreach (float x in chosen)
        {
            curtains.Add(Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), halfWidth * 2f, new Color(SealRed.r, SealRed.g, SealRed.b, 0f), 3));
            for (int s = 0; s < 3; s++)
            {
                float sx = x + Mathf.Lerp(-halfWidth * 0.7f, halfWidth * 0.7f, s / 2f);
                SpriteRenderer strip = Ofuda(new Vector3(sx, TopOfScreen + Random.Range(0f, 1.5f), 0));
                strips.Add((strip, sx, Random.value * 6f));
            }
        }
        float fall = 0.9f;
        float hang = ronin.Bounds.center.y + 1.6f;
        for (float t = 0; t < fall; t += Time.deltaTime * Tempo)
        {
            float k = Mathf.SmoothStep(0, 1, t / fall);
            foreach (var (strip, x, sway) in strips)
            {
                if (strip == null) continue;
                float y = Mathf.Lerp(TopOfScreen + 1f, hang + Mathf.Sin(sway) * 0.6f, k);
                strip.transform.position = new Vector3(x + Mathf.Sin(t * 4f + sway) * 0.25f, y, -0.2f);
                strip.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 5f + sway) * 12f);
            }
            foreach (SpriteRenderer curtain in curtains) curtain.color = new Color(SealRed.r, SealRed.g, SealRed.b, 0.08f * k);
            yield return null;
        }
        float seconds = secondPhase ? 5f : 4.5f;
        foreach (float x in chosen) seals.Add((x, halfWidth, Time.time + seconds));
        StartCoroutine(LiftSeals(strips.ConvertAll(s => s.strip), curtains, seconds));
        Fx.Play(Fx.Impact, new Vector3(lanes[mine], hang, 0), SealRed, 1.4f);

        // The law is laid down; now the blow that can't be parried inside it.
        yield return Wait(0.3f);
        if (Random.value < 0.5f) yield return TollOfFealty(secondPhase ? 3 : 2);
        else yield return CofferSlam();
    }

    SpriteRenderer Ofuda(Vector3 at)
    {
        var strip = new GameObject("Ofuda").AddComponent<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.KageOfuda);
        strip.sprite = frames != null ? frames[0] : ProceduralSprites.White;
        strip.color = frames != null ? Color.white : Paper;
        strip.sortingOrder = 45;
        strip.transform.position = at;
        strip.transform.localScale = frames != null ? Vector3.one * (1.3f / frames[0].bounds.size.y) : new Vector3(0.32f, 1.1f, 1f);
        if (frames == null)
        {
            // A red sigil down the paper.
            var sigil = new GameObject("Sigil").AddComponent<SpriteRenderer>();
            sigil.sprite = ProceduralSprites.White;
            sigil.color = SealRed;
            sigil.sortingOrder = 46;
            sigil.transform.SetParent(strip.transform, false);
            sigil.transform.localScale = new Vector3(0.3f, 0.7f, 1f);
        }
        transient.Add(strip.gameObject);
        return strip;
    }

    // The seals hold their time even if the attack that laid them is cut short; then the strips burn away.
    IEnumerator LiftSeals(List<SpriteRenderer> strips, List<SpriteRenderer> curtains, float seconds)
    {
        foreach (SpriteRenderer s in strips) if (s != null) transient.Remove(s.gameObject);
        foreach (SpriteRenderer c in curtains) if (c != null) transient.Remove(c.gameObject);
        yield return new WaitForSeconds(seconds - 0.5f);
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            foreach (SpriteRenderer s in strips) if (s != null) s.color = new Color(s.color.r, s.color.g, s.color.b, 1f - t / 0.5f);
            foreach (SpriteRenderer c in curtains) if (c != null) c.color = new Color(c.color.r, c.color.g, c.color.b, 0.08f * (1f - t / 0.5f));
            yield return null;
        }
        foreach (SpriteRenderer s in strips) if (s != null) { Fx.Play(Fx.Impact, s.transform.position, SealRed, 0.6f); Destroy(s.gameObject); }
        foreach (SpriteRenderer c in curtains) if (c != null) Destroy(c.gameObject);
    }

    bool InSealedLane()
    {
        if (ronin == null) return false;
        float x = ronin.Bounds.center.x;
        foreach (var (lane, halfWidth, until) in seals)
        {
            if (Time.time < until && Mathf.Abs(x - lane) < halfWidth) return true;
        }
        return false;
    }

    // The Thousand-Soul Wail: a beam of soul-fire from his open chest sweeps the field, edge to edge (and back in
    // phase 2). Cover stops it and burns; it lights the ground as it drags.
    IEnumerator ThousandSoulWail()
    {
        Bark(BossLine.SoulWail, 7f);
        float side = ronin.Bounds.center.x < CameraShake.Center.x ? 1f : -1f;
        float from = side > 0 ? RightEdge - 0.5f : LeftEdge + 0.5f;
        float to = side > 0 ? LeftEdge + 0.5f : RightEdge - 0.5f;

        // The chest gathers, and a thin guide shows where the beam will start.
        SpriteRenderer gather = Glowing(Chest, new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 57);
        SpriteRenderer guide = Bar(Chest, BeamEnd(Chest, new Vector3(from, GroundY, 0), out _), 0.06f, new Color(Teal.r, Teal.g, Teal.b, 0.5f));
        if (Sfx.Has(Sfx.SoulWail)) Sfx.PlayLoop(Sfx.SoulWail, 0.4f, 0.8f);
        else Sfx.Play(Sfx.LanternCharge, 0.8f, 0.7f);
        for (float t = 0; t < 1f; t += Time.deltaTime * Tempo)
        {
            gather.transform.localScale = Vector3.one * Mathf.Lerp(1f, 4f, t);
            gather.color = new Color(Teal.r, Teal.g, Teal.b, 0.7f * t);
            yield return null;
        }
        Drop(gather);
        Drop(guide);

        var glow = Bar(Chest, Chest, 1f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 53);
        glow.sprite = ProceduralSprites.Glow;
        var body = Bar(Chest, Chest, 1f, new Color(Teal.r, Teal.g, Teal.b, 0.9f), 55);
        var core = Bar(Chest, Chest, 1f, new Color(0.9f, 1f, 0.97f), 56);
        if (Sfx.Has(Sfx.SoulWail)) Sfx.PlayLoop(Sfx.SoulWail, 1f, 1f);
        else Sfx.PlayLoop(Sfx.LanternBeam, 1f, 0.8f);
        CameraShake.Add(0.3f);
        int passes = secondPhase ? 2 : 1;
        float sweep = secondPhase ? 1.9f : 2.6f;
        float nextBurn = 0, lastFire = from;
        for (int pass = 0; pass < passes; pass++)
        {
            float a = pass % 2 == 0 ? from : to, b = pass % 2 == 0 ? to : from;
            for (float t = 0; t < sweep; t += Time.deltaTime * Tempo)
            {
                float x = Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, t / sweep));
                Vector3 start = Chest;
                Vector3 end = BeamEnd(start, new Vector3(x, GroundY, 0), out Blocker cover);
                float flicker = 1f + 0.18f * Mathf.Sin(Time.time * 47f) + 0.08f * Mathf.Sin(Time.time * 113f);
                Stretch(glow, start, end, 1.8f * flicker);
                Stretch(body, start, end, 0.5f * flicker);
                Stretch(core, start, end, 0.16f);
                HitIfCrossed(start, end);
                if (cover != null && Time.time >= nextBurn)
                {
                    nextBurn = Time.time + 0.25f;
                    cover.Hit(end);
                    Sfx.Play(Sfx.BlockerHit, 0.5f, 1.4f);
                }
                if (cover == null && Mathf.Abs(x - lastFire) >= 0.6f)
                {
                    lastFire = x;
                    GroundFire.Light(end);
                }
                if (Random.value < 0.2f) Fx.Play(Fx.Impact, end, Teal, 0.7f);
                yield return null;
            }
        }
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        Drop(glow);
        Drop(body);
        Drop(core);
    }

    // The Last Toll, parry-only: a lock-on rings the ronin, then a great bronze bell drops on him, steered onto
    // his lane as fast as he runs. Parried, it flies back and rings against the Emperor's own armor.
    IEnumerator LastToll()
    {
        while (SealActive) yield return Wait(0.2f);
        Bark(BossLine.LastToll, 5f);
        PlayAction(art.parry);
        Vector3 drop = new Vector3(ronin.Bounds.center.x, TopOfScreen + 1f, 0);
        SpriteRenderer shadow = Glowing(drop + Vector3.down * 1.5f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.5f), 2.5f);
        Toll(0.9f, 0.6f);
        yield return LockOn(1.1f, Bronze);
        Drop(shadow);
        Arrow bell = Shoot(new Vector3(ronin.Bounds.center.x, TopOfScreen + 1f, 0), Vector3.down * 8f);
        Sprite[] frames = Fx.Frames(Fx.KageGreatBell);
        if (frames != null) bell.Restyle(frames, 8f, Color.white, new Vector2(2f, 2.2f), 0.7f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.5f), 3f);
        else bell.Restyle(ProceduralSprites.Disc, Bronze, new Vector2(1.7f, 2f), 0.7f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.55f), 3f);
        bell.MakeUnbreakable();
        bell.MakeReflectable(transform);
        StartCoroutine(Pursue(bell, 8.5f));
        Toll(1f, 0.5f);
        yield return Wait(1.6f);
    }

    // --- Phase break ---

    // The coffin's chains snap one by one as the bells clamour faster, the souls tear loose in a swarm of skulls,
    // and his eyes go from red to teal: the souls are looking out now.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        ultimate = false;
        exposedUntil = 0;
        seals.Clear();
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        PlayAction(art.hit);
        Music.Duck(0.25f, 9f);

        var chains = new List<SpriteRenderer>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = HammerHead + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.5f, 0.5f), 0);
            Vector3 b = Centre + new Vector3((i - 1.5f) * Extents.x * 0.4f, -Extents.y * 0.1f, 0);
            chains.Add(Bar(a, b, 0.14f, Bronze, 57));
        }
        StartCoroutine(Tremble(2.4f, 0.16f));
        // The bells: slow at first, then a clamour.
        for (int i = 0; i < 10; i++)
        {
            Toll(0.5f + 0.04f * i, 0.8f + 0.06f * i);
            yield return new WaitForSeconds(Mathf.Lerp(0.38f, 0.08f, i / 9f));
        }
        foreach (SpriteRenderer chain in chains)
        {
            Vector3 at = chain.transform.position;
            if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.9f);
            else Sfx.Play(Sfx.ParryClang, 0.8f, 0.7f);
            Fx.Play(Fx.Impact, at, Bronze, 1.5f);
            Debris.BurstSprites(at, art.deathParticles, Bronze, 6, 0.6f, 6f);
            CameraShake.Add(0.2f);
            Drop(chain);
            yield return new WaitForSeconds(0.22f);
        }
        // The souls tear loose.
        CameraShake.Add(0.7f);
        Fx.Play(Fx.Explosion, Chest, Teal, 3f);
        Debris.BurstSprites(Chest, art.deathParticles, Teal, 40, 1.2f, 10f);
        ElementFx.Glow(Chest, Teal, 2f, 14f, 1f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 1f, 0.8f);
        WearPhaseArt();
        PlayIdle();
        eyes = Glowing(Centre + Vector3.up * (Extents.y * 0.44f), new Color(Teal.r, Teal.g, Teal.b, 0.6f), 0.8f, 7, false);
        eyes.transform.SetParent(transform, true);
        yield return new WaitForSeconds(0.6f);

        if (PhaseCard != null) yield return PhaseCard(this);
        else
        {
            Speak(BossLine.Phase);
            yield return new WaitForSeconds(2.2f);
        }
        movesSinceEcho = EchoAtLatest - 1;
        movesSinceUltimate = 0;
        movesSinceParry = 0;
        breaking = false;
    }

    // --- Phase 2 ---

    // Wraith Swarm: skull spirits tear out of his armor, wheel round him, and dive at the ronin a few at a time.
    IEnumerator WraithSwarm()
    {
        Bark(BossLine.WraithSwarm, 7f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.8f);
        for (int wave = 0; wave < 3; wave++)
        {
            var skulls = new List<Arrow>();
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * 360f + wave * 25f;
                Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
                skulls.Add(Skull(Chest, dir * 3.5f / ShotSpeed));
            }
            IgnoreEachOther(skulls);
            Fx.Play(Fx.Impact, Chest, Teal, 1.6f);
            yield return Wait(0.45f);
            foreach (Arrow skull in skulls)
            {
                if (skull == null) continue;
                skull.Steer(Vector3.zero);
            }
            yield return Wait(0.25f);
            foreach (Arrow skull in skulls)
            {
                if (skull == null) continue;
                StartCoroutine(Hunt(skull, 7f * ShotSpeed));
                yield return Wait(0.12f);
            }
            yield return Wait(0.5f);
        }
    }

    // Curves a skull after the ronin until it is level with him, then lets it fly on.
    IEnumerator Hunt(Arrow skull, float speed)
    {
        Vector3 velocity = (ronin.Bounds.center - skull.transform.position).normalized * speed;
        skull.Turn(velocity);
        while (skull != null && ronin != null && skull.transform.position.y > ronin.Bounds.center.y + 0.8f)
        {
            Vector3 want = (ronin.Bounds.center - skull.transform.position).normalized * speed;
            velocity = Vector3.RotateTowards(velocity, want, Mathf.Deg2Rad * 120f * Time.fixedDeltaTime, 0f);
            skull.Turn(velocity);
            yield return new WaitForFixedUpdate();
        }
    }

    // Broken Chains: the snapped chains lash from his shoulders, each sweeping a stretch of the ground. A thin
    // line marks where each lash starts and ends.
    IEnumerator BrokenChains()
    {
        Bark(BossLine.BrokenChains, 7f);
        int lashes = Random.Range(2, 4);
        for (int i = 0; i < lashes; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 root = Shoulder(side);
            float centre = ClampX(ronin.Bounds.center.x + Random.Range(-1f, 1f));
            float dir = Random.value < 0.5f ? -1f : 1f;
            float a = ClampX(centre - dir * 4.5f), b = ClampX(centre + dir * 4.5f);
            SpriteRenderer startTell = Bar(root, new Vector3(a, GroundY, 0), 0.05f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.6f));
            SpriteRenderer endTell = Bar(root, new Vector3(b, GroundY, 0), 0.05f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.3f));
            yield return Wait(0.65f);
            Drop(startTell);
            Drop(endTell);

            var chain = Bar(root, root, 0.22f, Bronze, 56);
            var glow = Bar(root, root, 0.8f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 54);
            glow.sprite = ProceduralSprites.Glow;
            if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.7f, 0.8f);
            else Sfx.Play(Sfx.ParrySwing, 0.8f, 0.6f);
            float nextBurn = 0;
            for (float t = 0; t < 0.45f; t += Time.deltaTime * Tempo)
            {
                root = Shoulder(side);
                float x = Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, t / 0.45f));
                Vector3 end = BeamEnd(root, new Vector3(x, GroundY, 0), out Blocker cover);
                Stretch(chain, root, end, 0.22f);
                Stretch(glow, root, end, 0.8f);
                HitIfCrossed(root, end);
                if (cover != null && Time.time >= nextBurn)
                {
                    nextBurn = Time.time + 0.15f;
                    cover.Hit(end);
                }
                yield return null;
            }
            CameraShake.Add(0.2f);
            Drop(chain);
            Drop(glow);
            yield return Wait(0.2f);
        }
    }

    // Soul Coffin, parry-only: he hurls the coffin-hammer down on its broken chain. Parried, it bursts against his
    // own armor, and he stands open.
    IEnumerator SoulCoffin()
    {
        while (SealActive) yield return Wait(0.2f);
        Bark(BossLine.SoulCoffin, 5f);
        PlayAction(art.attack);
        SpriteRenderer raised = Glowing(HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.6f), 3f);
        yield return LockOn(1f, Teal);
        Drop(raised);
        Vector3 from = HammerHead;
        Arrow coffin = Shoot(from, (ronin.Bounds.center - from).normalized * 9f);
        Sprite[] frames = Fx.Frames(Fx.KageCoffin);
        if (frames != null) coffin.Restyle(frames, 8f, Color.white, new Vector2(1.6f, 2.4f), 0.7f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 3f);
        else coffin.Restyle(ProceduralSprites.White, new Color(0.22f, 0.2f, 0.22f), new Vector2(1.2f, 2f), 0.7f, new Color(Teal.r, Teal.g, Teal.b, 0.6f), 3f);
        coffin.MakeUnbreakable();
        coffin.MakeReflectable(transform);
        coffinThrownAt = Time.time;
        StartCoroutine(Pursue(coffin, 9f));
        Slam(0.6f);
        // The chain pays out behind it.
        SpriteRenderer chain = Bar(from, from, 0.12f, Bronze, 54);
        for (float t = 0; t < 1.6f && coffin != null; t += Time.deltaTime)
        {
            Stretch(chain, HammerHead, coffin.transform.position, 0.12f);
            yield return null;
        }
        Drop(chain);
        yield return Wait(0.4f);
    }

    // The coffin or the last toll sent home breaks his stance; a coffin bursting on him leaves him open.
    protected override void OnStaggered()
    {
        ultimate = false;
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        Toll(1f, 0.7f);
        Fx.Play(Fx.Impact, Centre, Bronze, 3f);
        if (Time.time - coffinThrownAt < 4f)
        {
            coffinThrownAt = float.NegativeInfinity;
            exposedUntil = Time.time + StaggerSeconds + ExposedSeconds;
            Fx.Play(Fx.Explosion, Chest, Teal, 2.5f);
            Debris.BurstSprites(Chest, art.deathParticles, Teal, 24, 1f, 8f);
        }
    }

    // The Night of a Thousand Bells, his ultimate: the field goes dark, bells hang across the top, and soul-fire
    // falls down every lane but one in waves, the safe lane stepping aside each time. Then he kneels, spent.
    IEnumerator NightOfAThousandBells()
    {
        while (SealActive) yield return Wait(0.2f);
        ultimate = true;
        Bark(BossLine.ThousandBells, 5f);
        yield return Stride(CameraShake.Center.x);
        SpriteRenderer dark = Darkness();
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            dark.color = new Color(0, 0, 0, 0.55f * t / 0.8f);
            yield return null;
        }
        const int LaneCount = 8;
        List<float> lanes = Lanes(LaneCount);
        float halfWidth = (RightEdge - LeftEdge - 2.4f) / (LaneCount * 2f);
        var bells = new List<SpriteRenderer>();
        foreach (float x in lanes) bells.Add(Glowing(new Vector3(x, TopOfScreen - 1.2f, 0), new Color(Bronze.r, Bronze.g, Bronze.b, 0.35f), 1.2f, 57));

        int safe = 0;
        for (int i = 1; i < LaneCount; i++) if (Mathf.Abs(lanes[i] - ronin.Bounds.center.x) < Mathf.Abs(lanes[safe] - ronin.Bounds.center.x)) safe = i;
        safe = Mathf.Clamp(safe + (Random.value < 0.5f ? -1 : 1), 0, LaneCount - 1);
        for (int wave = 0; wave < 5; wave++)
        {
            var bands = new List<SpriteRenderer>();
            var xs = new List<float>();
            for (int i = 0; i < LaneCount; i++)
            {
                if (i == safe) continue;
                xs.Add(lanes[i]);
                bands.Add(Bar(new Vector3(lanes[i], TopOfScreen, 0), new Vector3(lanes[i], GroundY, 0), halfWidth * 2f, new Color(Teal.r, Teal.g, Teal.b, 0.1f)));
                if (bells[i] != null) bells[i].color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.8f);
            }
            if (bells[safe] != null) bells[safe].color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.15f);
            yield return CloseLanes(bands, xs, halfWidth * 1.6f, halfWidth * 0.5f, Teal, wave == 0 ? 0.9f : 0.7f, 2, 0.2f);
            foreach (SpriteRenderer band in bands) Drop(band);

            Toll(1f, 0.7f + 0.05f * wave);
            CameraShake.Add(0.3f);
            var columns = new List<SpriteRenderer>();
            foreach (float x in xs)
            {
                // A soul-fire column: a broad soft glow round a narrow white-hot core.
                SpriteRenderer halo = Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), halfWidth * 2.2f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 55);
                halo.sprite = ProceduralSprites.Glow;
                columns.Add(halo);
                columns.Add(Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), halfWidth * 0.55f, new Color(0.85f, 1f, 0.96f, 0.8f), 56));
                GroundFire.Light(new Vector3(x, GroundY, 0));
            }
            if (Mathf.Abs(ronin.Bounds.center.x - lanes[safe]) > halfWidth * 0.95f) ronin.TakeHit(ronin.Bounds.center);
            foreach (SpriteRenderer column in columns) StartCoroutine(FadeOut(column, 0.35f));
            yield return Wait(0.3f);
            // The safe lane steps one over, never off the field.
            int stepTo = safe + (Random.value < 0.5f ? -1 : 1);
            if (stepTo < 0 || stepTo >= LaneCount) stepTo = safe - (stepTo - safe);
            safe = stepTo;
        }
        foreach (SpriteRenderer bell in bells) Drop(bell);
        for (float t = 0; t < 0.6f && dark != null; t += Time.deltaTime)
        {
            dark.color = new Color(0, 0, 0, 0.55f * (1f - t / 0.6f));
            yield return null;
        }
        Drop(dark);
        ultimate = false;
        // Spent: he sags, and the soul-fire in his chest gutters open.
        exposedUntil = Time.time + ExposedSeconds + 0.6f;
        PlayAction(art.hit);
        yield return Tremble(0.6f, 0.08f);
        yield return Wait(ExposedSeconds);
    }

    // A full-screen dark under the action, for his ultimate.
    SpriteRenderer Darkness()
    {
        var dark = new GameObject("Darkness").AddComponent<SpriteRenderer>();
        dark.sprite = ProceduralSprites.White;
        dark.color = Color.clear;
        dark.sortingOrder = 1;
        Camera cam = Camera.main;
        dark.transform.position = new Vector3(CameraShake.Center.x, CameraShake.Center.y, 0.5f);
        dark.transform.localScale = new Vector3(cam.orthographicSize * cam.aspect * 2.4f, cam.orthographicSize * 2.4f, 1f);
        transient.Add(dark.gameObject);
        return dark;
    }

    // --- The summoned souls ---

    // A soul he drags out of the coffin: a translucent teal ghost of the boss, in its own idle, on a chain from the
    // coffin-hammer. Without its art, a teal wisp stands in.
    class Echo
    {
        public SpriteRenderer body;
        public SpriteRenderer chain;
        public SpriteRenderer aura;
        public Vector3 Centre => body.bounds.center;
        public float Height => body.bounds.size.y;
    }

    IEnumerator Summon(BossKind kind, BossLine call, System.Action<Echo> ready)
    {
        Speak(call);
        PlayAction(art.parry);
        Toll(0.8f, 0.65f);
        float side = anchor.x > CameraShake.Center.x ? -1f : 1f;
        float x = Mathf.Clamp(anchor.x + side * 6.5f, LeftEdge + 3f, RightEdge - 3f);
        Vector3 at = new Vector3(x, anchor.y - 0.6f, 0);

        var echo = new Echo();
        var body = new GameObject("Echo_" + kind).AddComponent<SpriteRenderer>();
        body.sortingOrder = 4;
        if (echoes.TryGetValue(kind, out var look) && look.art.idle != null && look.art.idle.Length > 0)
        {
            body.transform.localScale = Vector3.one * look.scale;
            var animator = body.gameObject.AddComponent<SpriteAnimator>();
            animator.Play(look.art.idle, true, null, look.art.idleFps);
            body.sprite = look.art.idle[0];
            body.transform.position = at - (body.bounds.center - body.transform.position);
        }
        else
        {
            body.sprite = ProceduralSprites.Glow;
            body.transform.position = at;
            body.transform.localScale = Vector3.one * 3f;
        }
        body.color = new Color(0.55f, 1f, 0.9f, 0f);
        transient.Add(body.gameObject);
        echo.body = body;
        echo.aura = Glowing(at, new Color(Teal.r, Teal.g, Teal.b, 0f), echo.Height * 1.1f, 3);
        echo.chain = Bar(HammerHead, at, 0.1f, new Color(Bronze.r, Bronze.g, Bronze.b, 0f), 3);

        Fx.Play(Fx.SmokePuff, at, new Color(Teal.r, Teal.g, Teal.b, 0.7f), 2.5f);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 0.8f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime * Tempo)
        {
            float k = t / 0.8f;
            body.color = new Color(0.55f, 1f, 0.9f, 0.7f * k);
            echo.aura.color = new Color(Teal.r, Teal.g, Teal.b, 0.3f * k);
            echo.chain.color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.8f * k);
            yield return null;
        }
        ready(echo);
    }

    IEnumerator Dismiss(Echo echo)
    {
        if (echo == null || echo.body == null) yield break;
        for (float t = 0; t < 0.6f && echo.body != null; t += Time.deltaTime)
        {
            float k = 1f - t / 0.6f;
            echo.body.color = new Color(0.55f, 1f, 0.9f, 0.7f * k);
            if (echo.aura != null) echo.aura.color = new Color(Teal.r, Teal.g, Teal.b, 0.3f * k);
            if (echo.chain != null) echo.chain.color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.8f * k);
            yield return null;
        }
        Drop(echo.body);
        Drop(echo.aura);
        Drop(echo.chain);
    }

    // The chain from the coffin-hammer follows the ghost while the soul works.
    void HoldChain(Echo echo)
    {
        if (echo?.chain != null && echo.body != null) Stretch(echo.chain, HammerHead, echo.Centre, 0.1f);
    }

    // Benibana's soul: her smoke rings in offset fans, then her Crimson Iaido, a crescent only a parry answers.
    // Parried, it flies back at the Emperor, not at her.
    IEnumerator EchoBenibana()
    {
        Echo echo = null;
        yield return Summon(BossKind.Benibana, BossLine.SummonBenibana, e => echo = e);
        Vector3 mouth = echo.Centre + Vector3.up * (echo.Height * 0.25f);
        for (int volley = 0; volley < 3; volley++)
        {
            HoldChain(echo);
            Fx.Play(Fx.SmokePuff, mouth, new Color(0.8f, 0.75f, 0.8f, 0.8f), 1.6f);
            Sfx.Play(Sfx.ParrySwing, 0.6f, 0.6f);
            Vector3 aim = (ronin.Bounds.center - mouth).normalized;
            var rings = new List<Arrow>();
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(-40f, 40f, i / 4f) + (volley % 2 == 1 ? 10f : 0f);
                Arrow ring = Shoot(mouth, Quaternion.Euler(0, 0, angle) * aim * 5.5f);
                Sprite[] frames = Fx.Frames(Fx.BeniSmokeRing);
                if (frames != null) ring.Restyle(frames, 10f, new Color(0.75f, 1f, 0.9f, 0.85f), new Vector2(1.15f, 1.15f), 0.3f, new Color(Teal.r, Teal.g, Teal.b, 0.3f), 1.4f);
                else ring.Restyle(ProceduralSprites.Disc, new Color(0.7f, 0.9f, 0.85f, 0.8f), new Vector2(0.75f, 0.75f), 0.3f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 1.4f);
                ring.gameObject.AddComponent<Swell>().Begin(1f, 1.9f, 2.2f);
                rings.Add(ring);
            }
            IgnoreEachOther(rings);
            // She answers once his summons has been heard.
            if (volley == 0) Speak(BossLine.EchoBenibana);
            yield return Wait(0.6f);
        }
        // The draw.
        HoldChain(echo);
        yield return LockOn(0.8f, new Color(1f, 0.3f, 0.35f));
        Vector3 from = echo.Centre;
        Arrow wave = Shoot(from, (ronin.Bounds.center - from).normalized * 10f);
        Sprite[] crescent = Fx.Frames(Fx.BeniCrescent);
        if (crescent != null) wave.Restyle(crescent, 14f, new Color(0.7f, 1f, 0.9f), new Vector2(3.2f, 2f), 0.55f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 3.5f);
        else wave.Restyle(ProceduralSprites.Glow, new Color(0.6f, 1f, 0.9f), new Vector2(3.2f, 1f), 0.55f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 3.5f);
        wave.MakeReflectable(transform);
        Fx.Play(Fx.SlashArc, from, Teal, 3f);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        yield return Wait(0.8f);
        yield return Dismiss(echo);
    }

    // Utsusemi's soul: three hour marks, each laid where the ronin stands and struck by its eye-laser a beat later,
    // then its Hour Strike, a giant cleave steered onto him that only a parry turns back (onto the Emperor).
    IEnumerator EchoUtsusemi()
    {
        Echo echo = null;
        yield return Summon(BossKind.Utsusemi, BossLine.SummonUtsusemi, e => echo = e);
        for (int i = 0; i < 3; i++)
        {
            HoldChain(echo);
            Vector3 eye = echo.Centre + Vector3.up * (echo.Height * 0.3f);
            Vector3 spot = new Vector3(ronin.Bounds.center.x, ronin.Feet.y, 0);
            FxPlayer mark = Fx.Play(Fx.UtsuClockMark, spot, new Color(0.6f, 1f, 0.9f), 3f, null, 6);
            SpriteRenderer ring = mark == null ? Glowing(spot, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 2.2f, 6) : null;
            if (mark != null) transient.Add(mark.gameObject);
            SpriteRenderer sight = Bar(eye, spot, 0.05f, new Color(Teal.r, Teal.g, Teal.b, 0.5f));
            Sfx.Play(Sfx.ClockTick, 0.8f, 0.8f);
            if (i == 0) Speak(BossLine.EchoUtsusemi);
            yield return Wait(0.7f);
            var laser = Bar(eye, spot, 0.3f, new Color(0.85f, 1f, 0.95f, 0.95f), 56);
            Sfx.Play(Sfx.LanternBeam, 0.5f, 1.3f);
            if (Mathf.Abs(ronin.Bounds.center.x - spot.x) < 0.9f) ronin.TakeHit(spot + Vector3.up * 0.5f);
            Fx.Play(Fx.Impact, spot, Teal, 1.4f);
            GroundFire.Light(spot);
            yield return Wait(0.15f);
            Drop(laser);
            Drop(sight);
            Drop(ring);
            if (mark != null) { transient.Remove(mark.gameObject); mark.Stop(); }
            yield return Wait(0.2f);
        }
        HoldChain(echo);
        yield return LockOn(0.8f, Teal);
        Sfx.Play(Sfx.MidnightChime, 0.8f, 0.7f);
        Vector3 from = echo.Centre;
        Arrow cleave = Shoot(from, (ronin.Bounds.center - from).normalized * 11f);
        Sprite[] frames = Fx.Frames(Fx.UtsuHourSlash);
        if (frames != null) cleave.Restyle(frames, 14f, new Color(0.7f, 1f, 0.9f), new Vector2(3.4f, 2.2f), 0.6f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 3.5f);
        else cleave.Restyle(ProceduralSprites.Glow, new Color(0.6f, 1f, 0.9f), new Vector2(3.4f, 1.1f), 0.6f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 3.5f);
        cleave.MakeUnbreakable();
        cleave.MakeReflectable(transform);
        StartCoroutine(Pursue(cleave, 11f));
        Sfx.Play(Sfx.HourSlash, 0.9f);
        yield return Wait(0.9f);
        yield return Dismiss(echo);
    }

    // Shirogane's soul: her Phantom Blade Rain, two waves of ghost swords down every lane but two. Bound to his will,
    // she can only warn Oni before her blades fall.
    IEnumerator EchoShirogane()
    {
        Echo echo = null;
        yield return Summon(BossKind.Shirogane, BossLine.SummonShirogane, e => echo = e);
        const int LaneCount = 8;
        List<float> lanes = Lanes(LaneCount);
        int gap = Random.Range(0, LaneCount - 1);
        for (int wave = 0; wave < 2; wave++)
        {
            HoldChain(echo);
            var tells = new List<SpriteRenderer>();
            var xs = new List<float>();
            for (int i = 0; i < LaneCount; i++)
            {
                if (i == gap || i == gap + 1) continue;
                xs.Add(lanes[i]);
                tells.Add(Bar(new Vector3(lanes[i], TopOfScreen, 0), new Vector3(lanes[i], GroundY, 0), 0.1f, new Color(0.6f, 0.95f, 1f, 0.3f)));
            }
            Sfx.Play(Sfx.BladeRing, 0.7f);
            if (wave == 0) Speak(BossLine.EchoShirogane);
            yield return Wait(0.8f);
            foreach (SpriteRenderer tell in tells) Drop(tell);
            foreach (float x in xs)
            {
                Arrow blade = Shoot(new Vector3(x, TopOfScreen, 0), Vector3.down * 12f);
                Sprite[] frames = Fx.Frames(Fx.ShiroSword);
                if (frames != null) blade.Restyle(frames, 10f, Color.white, new Vector2(0.7f, 1.9f), 0.3f, new Color(0.4f, 0.9f, 1f, 0.4f), 1.4f);
                else blade.Restyle(ProceduralSprites.White, new Color(0.85f, 0.97f, 1f), new Vector2(0.16f, 1.6f), 0.3f, new Color(0.4f, 0.9f, 1f, 0.45f), 1.4f);
                blade.PassesCover = true;
                blade.PassesShots = true;
            }
            Sfx.Play(Sfx.GhostSwordFall, 0.9f);
            // The next wave's gap is one lane over, so one lane carries through.
            gap = Mathf.Clamp(gap + (Random.value < 0.5f ? -1 : 1), 0, LaneCount - 2);
            yield return Wait(0.7f);
        }
        yield return Dismiss(echo);
    }

    // --- Defeat ---

    // The coffin bursts. One by one his bells fall silent, each toll lower and quieter than the last, a soul rising
    // free with each, until there is silence and the armor stands hollow, then fades.
    protected override IEnumerator Fall()
    {
        seals.Clear();
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        if (ronin != null && ronin.ParrySealed != null) ronin.ParrySealed = null;
        PlayAction(art.hit);
        CameraShake.Add(0.9f);
        Fx.Play(Fx.Explosion, HammerHead, Teal, 3.5f);
        Debris.BurstSprites(HammerHead, art.deathParticles, Teal, 50, 1.3f, 10f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 1f, 0.7f);
        ElementFx.Glow(Centre, Teal, 3f, 16f, 1.2f);
        yield return new WaitForSeconds(1f);
        if (art.death != null && art.death.Length > 0) animator.Play(art.death, false, null, art.actionFps * 0.5f);

        const int Bells = 7;
        for (int i = 0; i < Bells; i++)
        {
            float k = i / (Bells - 1f);
            Toll(Mathf.Lerp(0.9f, 0.25f, k), Mathf.Lerp(0.9f, 0.5f, k));
            StartCoroutine(RisingSoul(Centre + new Vector3(Random.Range(-Extents.x, Extents.x) * 0.6f, Random.Range(-0.3f, 0.5f) * Extents.y, 0)));
            yield return new WaitForSeconds(Mathf.Lerp(0.7f, 1.05f, k));
        }
        // Silence. The armor sinks hollow and fades.
        yield return new WaitForSeconds(0.6f);
        Vector3 from = anchor;
        for (float t = 0; t < 2.2f; t += Time.deltaTime)
        {
            float k = t / 2.2f;
            anchor = from + Vector3.down * (0.5f * k);
            body.MovePosition(anchor);
            Color c = Color.Lerp(bodyTint, new Color(0.3f, 0.3f, 0.32f), k);
            c.a = 1f - k * k;
            sprite.color = c;
            if (chestGlow != null) chestGlow.color = new Color(Teal.r, Teal.g, Teal.b, 0.4f * (1f - k));
            if (eyes != null) eyes.color = new Color(Teal.r, Teal.g, Teal.b, 0.75f * (1f - k));
            if (Random.value < 0.1f) StartCoroutine(RisingSoul(Centre + (Vector3)Random.insideUnitCircle * Extents.x * 0.5f));
            yield return null;
        }
        sprite.color = Color.clear;
        if (chestGlow != null) chestGlow.color = Color.clear;
        if (eyes != null) eyes.color = Color.clear;
        // The last souls finish rising before he's gone (they rise on his clock).
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    // A freed soul: a teal wisp drifting up and away, its fire leaving with it.
    static IEnumerator RisingSoul(Vector3 from)
    {
        var soul = new GameObject("FreedSoul").AddComponent<SpriteRenderer>();
        soul.sprite = ProceduralSprites.Glow;
        soul.sortingOrder = 58;
        soul.transform.position = from + Vector3.back * 0.3f;
        float drift = Random.Range(-1f, 1f);
        float size = Random.Range(0.8f, 1.5f);
        for (float t = 0; t < 3f && soul != null; t += Time.deltaTime)
        {
            float k = t / 3f;
            soul.transform.position = from + new Vector3(drift * k * 2f + Mathf.Sin(t * 3f) * 0.3f, k * 9f, -0.3f);
            soul.transform.localScale = Vector3.one * size * (1f - 0.4f * k);
            soul.color = new Color(Teal.r, Teal.g, Teal.b, 0.8f * (1f - k));
            yield return null;
        }
        if (soul != null) Destroy(soul.gameObject);
    }
}
