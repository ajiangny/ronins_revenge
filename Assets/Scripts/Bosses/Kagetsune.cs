using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 4's final boss, "The Thousand-Soul Throne": Emperor Kagetsune, colossal and deathless inside an armor of a
// thousand sealed souls, with the Soul Coffin on his shoulder as a hammer. He barely moves; everything he does is
// slow, heavy and telegraphed. Phase 1 is his law (the Coffer Slam, the Toll of Fealty, the Ofuda Decree, the
// Thousand-Soul Wail, and the parry-only Last Toll). His phase break snaps the coffin's chains and he becomes the
// Unchained Wraith: faster, with skull swarms, lashing chains, the parry-only Soul Coffin, the Night of a Thousand
// Bells, and the souls of the three bosses summoned out of the coffin to use their own moves on Oni.
public partial class Kagetsune : Boss
{
    public const int StartingHealth = 210;
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
    const float ExposedSeconds = 1.8f;

    float Recovery => secondPhase ? (lastBell ? 0.35f : 0.45f) : 0.8f;
    float ComboChance => secondPhase ? 0.65f : 0.5f;
    int MaxCombo => secondPhase ? 4 : 3;
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
        CofferSlam, Toll, Ofuda, Wail, LastToll, Curtain,
        Swarm, Chains, SoulCoffin, ThousandBells, River,
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
    protected override float StaggerSeconds => secondPhase ? 1.4f : 1.8f;
    // The final exam: his stance holds longer than the others'.
    protected override float StanceResistance => secondPhase ? 0.7f : 0.8f;
    // Open is double damage, but not on top of a stagger's own double: the best burst is ×2, not ×4.
    protected override int DamageMultiplier => Time.time < exposedUntil && !Staggered ? 2 : 1;
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
        Damaged += OnHitBy;
    }

    void LateUpdate()
    {
        if (chestGlow == null) return;
        chestGlow.transform.position = Chest + Vector3.back * 0.1f;
        float pulse = 0.35f + 0.12f * Mathf.Sin(Time.time * 3.1f) + (Time.time < exposedUntil ? 0.3f : 0f);
        chestGlow.color = new Color(Teal.r, Teal.g, Teal.b, pulse);
        // As his last bar drains the souls burn fiercer in him, so the fight visibly builds to its end.
        float drained = secondPhase && !IsDead ? 1f - HealthFraction : 0f;
        chestGlow.transform.localScale = Vector3.one * (2.2f + 2.2f * drained) / transform.lossyScale.x;
        UpdateWisps();
        if (eyes != null) eyes.transform.position = Centre + Vector3.up * (Extents.y * 0.44f) + Vector3.back * 0.1f;
        UpdateLastBell();
    }

    // The Wraith's skull spirits, a few small ghosts circling him once the chains have snapped.
    readonly List<SpriteRenderer> wisps = new List<SpriteRenderer>();

    void UpdateWisps()
    {
        if (!secondPhase || breaking || IsDead)
        {
            foreach (SpriteRenderer wisp in wisps) if (wisp != null) wisp.color = Color.Lerp(wisp.color, Color.clear, Time.deltaTime * 3f);
            return;
        }
        if (wisps.Count == 0)
        {
            Sprite[] frames = Fx.Frames(Fx.KageSkull);
            for (int i = 0; i < 4; i++)
            {
                var wisp = new GameObject("SkullWisp").AddComponent<SpriteRenderer>();
                wisp.sprite = frames != null ? frames[0] : ProceduralSprites.Glow;
                wisp.sortingOrder = 6;
                float size = frames != null ? 0.6f / frames[0].bounds.size.y : 0.6f;
                wisp.transform.localScale = Vector3.one * size;
                wisps.Add(wisp);
            }
        }
        for (int i = 0; i < wisps.Count; i++)
        {
            if (wisps[i] == null) continue;
            float angle = Time.time * 0.9f + i * Mathf.PI / 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * Extents.x * 0.7f, Mathf.Sin(angle * 1.3f) * Extents.y * 0.45f, -0.1f);
            wisps[i].transform.position = Centre + offset;
            wisps[i].color = new Color(0.75f, 1f, 0.92f, 0.35f + 0.1f * Mathf.Sin(Time.time * 4f + i));
        }
    }

    // The Last Bell: once his last bar runs low, the dark closes in round the field and pulses with his heart.
    SpriteRenderer lastBellDark;

    void UpdateLastBell()
    {
        if (IsDead || !lastBell)
        {
            if (lastBellDark != null) lastBellDark.color = Color.Lerp(lastBellDark.color, Color.clear, Time.deltaTime * 2f);
            return;
        }
        if (!lastBellRung)
        {
            lastBellRung = true;
            Toll(1f, 0.45f);
            CameraShake.Add(0.5f);
            ElementFx.Glow(Chest, Teal, 2f, 18f, 1f);
            lastBellDark = new GameObject("LastBellDark").AddComponent<SpriteRenderer>();
            lastBellDark.sprite = ProceduralSprites.White;
            lastBellDark.sortingOrder = 1;
            Camera cam = Camera.main;
            lastBellDark.transform.position = new Vector3(CameraShake.Center.x, CameraShake.Center.y, 0.5f);
            lastBellDark.transform.localScale = new Vector3(cam.orthographicSize * cam.aspect * 2.4f, cam.orthographicSize * 2.4f, 1f);
            lastBellDark.color = Color.clear;
        }
        float pulse = 0.2f + 0.06f * Mathf.Sin(Time.time * 3.1f);
        lastBellDark.color = new Color(0.02f, 0.06f, 0.06f, pulse);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (lastBellDark != null) Destroy(lastBellDark.gameObject);
        foreach (SpriteRenderer wisp in wisps) if (wisp != null) Destroy(wisp.gameObject);
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
                // A summoned soul fights beside him: it works its own pattern while he goes on with his.
                if (IsEcho(move))
                {
                    echoRoutine = StartCoroutine(RunEcho(move));
                    yield return Wait(1.2f);
                    continue;
                }
                yield return Play(move);
                // The ultimate ends the combo.
                if (move == Move.ThousandBells) i = combo;
                yield return Wait(secondPhase ? 0.2f : 0.3f);
            }
        }
    }

    Coroutine echoRoutine;
    bool EchoOut => echoRoutine != null;

    // Each soul sent out gets a number, so one that finishes late can't clear the flag for a newer one.
    int echoId;

    IEnumerator RunEcho(Move move)
    {
        int id = ++echoId;
        yield return Play(move);
        if (id == echoId) echoRoutine = null;
    }

    // A stagger, the phase break or his fall sends the soul back into the coffin; its ghost and chain are transient,
    // so the interrupt has already cleared them.
    void RecallEcho()
    {
        if (echoRoutine != null) StopCoroutine(echoRoutine);
        echoRoutine = null;
        echoId++;
        if (activeEcho != null)
        {
            Drop(activeEcho.body);
            Drop(activeEcho.aura);
            Drop(activeEcho.chain);
            activeEcho = null;
        }
        if (cageSpiral != null) StopCoroutine(cageSpiral);
        cageSpiral = null;
    }

    Echo activeEcho;
    Coroutine cageSpiral;

    static bool IsEcho(Move move) => move == Move.EchoBenibana || move == Move.EchoUtsusemi || move == Move.EchoShirogane;

    IEnumerator Play(Move move) => move switch
    {
        Move.CofferSlam => CofferSlam(),
        Move.Toll => TollOfFealty(secondPhase ? 5 : 3),
        Move.Ofuda => OfudaDecree(),
        Move.Wail => ThousandSoulWail(),
        Move.LastToll => LastToll(),
        Move.Curtain => RosaryCurtain(),
        Move.Swarm => WraithSwarm(),
        Move.Chains => BrokenChains(),
        Move.SoulCoffin => SoulCoffin(),
        Move.ThousandBells => NightOfAThousandBells(),
        Move.River => SoulRiver(),
        Move.EchoBenibana => EchoBenibana(),
        Move.EchoUtsusemi => EchoUtsusemi(),
        _ => EchoShirogane(),
    };

    Move NextMove()
    {
        // While a soul is out he keeps to light patterns: no parry-only blow, no ultimate, no second soul.
        if (EchoOut)
        {
            var light = new List<Move> { Move.Toll, Move.Swarm, Move.Toll, Move.Swarm, Move.CofferSlam };
            if (lastMove is Move previous) light.Remove(previous);
            return light[Random.Range(0, light.Count)];
        }
        if (secondPhase && movesSinceUltimate >= Ultimate - 1 && lastMove != null) return Move.ThousandBells;
        Move parry = secondPhase ? Move.SoulCoffin : Move.LastToll;
        if (movesSinceParry >= ParryAtLatest) return parry;
        if (secondPhase && movesSinceEcho >= EchoAtLatest) return NextEcho();

        var options = new List<Move> { Move.CofferSlam, Move.Toll, Move.Ofuda, Move.Wail, Move.Curtain };
        if (secondPhase)
        {
            options.Add(Move.Swarm);
            options.Add(Move.Swarm);
            options.Add(Move.Chains);
            options.Add(Move.River);
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
}
