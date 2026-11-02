using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 4's final boss, "The Thousand-Soul Throne": Emperor Kagetsune, colossal and deathless inside an armor of a
// thousand sealed souls, with the Soul Coffin on his shoulder as a hammer. He barely moves; everything he does is
// slow, heavy and telegraphed. Phase 1 is his law (the Coffer Slam, the Toll of Fealty, the Ofuda Decree, the
// Spirit Hyper Beam, and the parry-only Last Toll), and his levy of the dead army falls in below him. His phase break snaps the coffin's chains and he becomes the
// Unchained Wraith: faster, with skull swarms, lashing chains, the parry-only Soul Coffin, the Night of a Thousand
// Bells, a far greater host of the dead, and the three bosses' souls dragged out of the coffin at 30% health to fight beside him with their own moves.
public partial class Kagetsune : Boss
{
    public const int StartingHealth = 210;
    public const string PhaseEpithet = "The Unchained Wraith";

    internal static readonly Color Crimson = new Color(0.78f, 0.1f, 0.14f);
    static readonly Color Teal = new Color(0.3f, 1f, 0.82f);
    static readonly Color Bronze = new Color(0.88f, 0.62f, 0.32f);
    static readonly Color Paper = new Color(0.96f, 0.9f, 0.78f);
    static readonly Color SealRed = new Color(0.95f, 0.15f, 0.12f);

    // Parry-only moves: one is offered once this many moves have passed, and forced by ParryAtLatest.
    const int ParryEvery = 3;
    const int ParryAtLatest = 4;
    // In phase 2 a soul is summoned at least this many moves after the last one left the field, and the Night of a
    // Thousand Bells comes every UltimateEvery moves.
    const int SoulAtLatest = 4;
    const int UltimateEvery = 6;
    const float ExposedSeconds = 1.8f;

    float Recovery => secondPhase ? (lastBell ? 0.35f : 0.45f) : 0.7f;
    float ComboChance => secondPhase ? 0.65f : 0.55f;
    int MaxCombo => 4;
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
        BossLine.SoulCoffin => "Into the coffin!",
        BossLine.ThousandBells => "A thousand bells, Oni! Ring!",
        BossLine.SummonBenibana => "Dance for me, Benibana.",
        BossLine.EchoBenibana => "Your strings again, Kagetsune? Tch... I'm no puppet of yours.",
        BossLine.SummonUtsusemi => "Strike the hour, clock.",
        BossLine.EchoUtsusemi => "The hour... strikes... Run... boy.",
        BossLine.SummonShirogane => "Rise, Shirogane. Kneel, and strike.",
        BossLine.EchoShirogane => "Move, Oni. I cannot hold my blades.",
        BossLine.KilledOni => "Back to your hill, little demon. I will be here. I am always here.",
        BossLine.SoulAbsorbed => "Mine again.",
        BossLine.RosaryCurtain => "Kneel, in rows.",
        BossLine.SoulRiver => "Drown in them.",
        BossLine.Rend => "Again. And again.",
        BossLine.Pendulum => "Reap.",
        BossLine.Harvest => "Come home, all of you.",
        BossLine.Levy => "Rise, my dead. Fall in.",
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
        CofferSlam, Toll, Ofuda, Beam, LastToll, Curtain,
        Swarm, SoulCoffin, ThousandBells, River,
        Summon, Rend, Pendulum, Harvest, Levy,
    }

    bool secondPhase => SecondPhase;
    bool breaking;
    // The Night of a Thousand Bells is a set piece the stall watchdog must wait out.
    bool ultimate;
    // After a parried coffin or the ultimate he stands open, taking double damage.
    float exposedUntil;
    Move? lastMove;
    int movesSinceParry;
    // Moves since a summoned soul last left the field (or since the phase break).
    int movesSinceSoul;
    int movesSinceUltimate;
    float coffinThrownAt = float.NegativeInfinity;

    protected override bool HasSecondPhase => true;
    protected override bool Busy => breaking || ultimate;
    protected override float StaggerSeconds => secondPhase ? 1.4f : 1.8f;
    // The final exam: his stance holds longer than the others'.
    protected override float StanceResistance => secondPhase ? 0.7f : 0.8f;
    // Open is double damage, but not on top of a stagger's own double: the best burst is ×2, not ×4.
    protected override int DamageMultiplier => Time.time < exposedUntil && !Staggered ? 2 : 1;
    public override float DefeatSeconds => 11f;

    // Where his body parts sit on the art: the soul-fire chest, the bells at his waist, the coffin-hammer's head.
    Vector3 Extents => sprite.bounds.extents;
    // Measured on his PixelLab frames, which leave room round the figure on a square canvas.
    Vector3 Chest => Centre + Vector3.up * (Extents.y * 0.06f);
    Vector3 Waist => Centre + Vector3.down * (Extents.y * 0.25f);
    Vector3 HammerHead => Centre + new Vector3(Extents.x * 0.45f, Extents.y * 0.45f, 0);
    Vector3 Shoulder(float side) => Centre + new Vector3(side * Extents.x * 0.3f, Extents.y * 0.25f, 0);

    SpriteRenderer chestGlow;
    SpriteRenderer[] eyes;
    // The soul-fire he stands in once the chains snap.
    FxPlayer soulFire;

    void Start()
    {
        // The soul-fire in his open chest, always burning; it flares with each blow.
        viewHome = sprite.transform.localPosition;
        viewScale = sprite.transform.localScale;
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
        UpdateSoul();
        if (eyes != null)
        {
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] == null) continue;
                eyes[i].transform.position = Eye(i) + Vector3.back * 0.1f;
                eyes[i].color = new Color(Teal.r, Teal.g, Teal.b, IsDead ? eyes[i].color.a : 0.75f + 0.25f * Mathf.Sin(Time.time * 6f + i));
            }
        }
        UpdateLastBell();
    }

    // His eyes behind the visor, turned teal at the phase break: the souls looking out.
    Vector3 Eye(int i) => Centre + new Vector3((i == 0 ? -1f : 1f) * Extents.x * 0.045f, Extents.y * 0.44f, 0);

    // The Wraith's skull spirits, a few small ghosts circling him once the chains have snapped.
    readonly List<SpriteRenderer> wisps = new List<SpriteRenderer>();

    void UpdateWisps()
    {
        if (!secondPhase || breaking || IsDead)
        {
            foreach (SpriteRenderer wisp in wisps) if (wisp != null) wisp.color = Color.Lerp(wisp.color, Color.clear, Time.deltaTime * 3f);
            return;
        }
        // Freed-soul ghosts when their art is there, else the skulls he fires.
        Sprite[] frames = Fx.Frames(Fx.KageSoulRise) ?? Fx.Frames(Fx.KageSkull);
        if (wisps.Count == 0)
        {
            for (int i = 0; i < 5; i++)
            {
                var wisp = new GameObject("SoulWisp").AddComponent<SpriteRenderer>();
                wisp.sprite = frames != null ? frames[0] : ProceduralSprites.Glow;
                wisp.sortingOrder = 6;
                float size = frames != null ? 0.75f / frames[0].bounds.size.y : 0.6f;
                wisp.transform.localScale = Vector3.one * size;
                wisps.Add(wisp);
            }
        }
        for (int i = 0; i < wisps.Count; i++)
        {
            if (wisps[i] == null) continue;
            float angle = Time.time * 0.9f + i * Mathf.PI * 2f / wisps.Count;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * Extents.x * 0.7f, Mathf.Sin(angle * 1.3f) * Extents.y * 0.45f, -0.1f);
            Vector3 before = wisps[i].transform.position;
            wisps[i].transform.position = Centre + offset;
            if (frames != null) wisps[i].sprite = frames[(int)(Time.time * 12f + i * 3) % frames.Length];
            // Each ghost faces the way it drifts.
            wisps[i].flipX = wisps[i].transform.position.x < before.x;
            // Those passing behind him dim.
            bool behind = Mathf.Sin(angle * 1.3f) > 0.3f;
            wisps[i].sortingOrder = behind ? 4 : 6;
            wisps[i].color = new Color(0.85f, 1f, 0.95f, (behind ? 0.3f : 0.6f) + 0.1f * Mathf.Sin(Time.time * 4f + i));
        }
    }

    // The Last Bell: once his last bar runs low, the dark closes in round the field and pulses with his heart.
    SpriteRenderer lastBellDark;

    void UpdateLastBell()
    {
        // The ultimate draws its own dark: this one gives way so the two don't stack.
        if (IsDead || !lastBell || ultimate)
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
        // His heart: a beat each pulse, the dark and the chest-fire swelling with it.
        float beat = Mathf.Sin(Time.time * 3.1f);
        lastBellDark.transform.position = new Vector3(CameraShake.Center.x, CameraShake.Center.y, 0.5f);
        lastBellDark.color = new Color(0.02f, 0.06f, 0.06f, 0.22f + 0.07f * beat);
        if (beat > 0.95f && Time.time >= nextHeartbeat)
        {
            nextHeartbeat = Time.time + 1.5f;
            StartCoroutine(Heartbeat());
        }
    }

    // A halt (the ronin fell) or his fall: the soul beside him goes back into the coffin with him.
    protected override void OnCleared()
    {
        BanishSoul();
        DisbandLevy();
    }

    float nextHeartbeat;

    // Lub-dub, low and felt more than heard.
    IEnumerator Heartbeat()
    {
        Slam(0.25f);
        CameraShake.Add(0.06f);
        yield return new WaitForSeconds(0.22f);
        Slam(0.18f);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        BanishSoul();
        if (lastBellDark != null) Destroy(lastBellDark.gameObject);
        foreach (SpriteRenderer wisp in wisps) if (wisp != null) Destroy(wisp.gameObject);
        if (soulFire != null) Destroy(soulFire.gameObject);
    }

    protected override IEnumerator Fight()
    {
        // A fight restarted mid-levy (a halt, the watchdog) mustn't leave him armoured.
        Armour(false);
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
                movesSinceSoul = move == Move.Summon || SoulOut ? 0 : movesSinceSoul + 1;
                movesSinceLevy = move == Move.Levy || LevyOut ? 0 : movesSinceLevy + 1;
                // His ultimate's count holds while a soul fights: it waits for the field to clear.
                movesSinceUltimate = move == Move.ThousandBells ? 0 : SoulOut ? movesSinceUltimate : movesSinceUltimate + 1;
                lastMove = move;
                // A summoned soul fights beside him on its own: he calls it, then goes on with his own moves.
                if (move == Move.Summon)
                {
                    yield return CallSoul(NextSoul());
                    continue;
                }
                // A soul he has swallowed lends its power to his next plain blows.
                Borrow(move);
                yield return Attacking($"Kagetsune_{move}", Play(move));
                // The ultimate ends the combo.
                if (move == Move.ThousandBells) i = combo;
                yield return Wait(secondPhase ? 0.2f : 0.3f);
            }
        }
    }

    IEnumerator Play(Move move) => move switch
    {
        Move.CofferSlam => CofferSlam(),
        Move.Toll => TollOfFealty(secondPhase ? 5 : 3),
        Move.Ofuda => OfudaDecree(),
        Move.Beam => SpiritHyperBeam(),
        Move.LastToll => LastToll(),
        Move.Curtain => RosaryCurtain(),
        Move.Swarm => WraithSwarm(),
        Move.SoulCoffin => SoulCoffin(),
        Move.ThousandBells => NightOfAThousandBells(),
        Move.Rend => ReliquaryRend(),
        Move.Pendulum => CoffinPendulum(),
        Move.Harvest => SoulHarvest(),
        Move.Levy => RaiseLevy(),
        _ => SoulRiver(),
    };

    Move NextMove()
    {
        // While a soul fights beside him he keeps to plain patterns: no seal (the soul's own parry-only blows must
        // stay answerable), no parry-only blow of his own, no ultimate, no second soul.
        if (SoulOut)
        {
            var plain = new List<Move> { Move.CofferSlam, Move.Toll, Move.Swarm, Move.Curtain, Move.Rend, Move.Toll };
            if (lastMove is Move previous) plain.Remove(previous);
            return plain[Random.Range(0, plain.Count)];
        }
        if (secondPhase && movesSinceUltimate >= Ultimate - 1 && lastMove != null) return Move.ThousandBells;
        Move parry = secondPhase ? Move.SoulCoffin : Move.LastToll;
        if (movesSinceParry >= ParryAtLatest) return parry;
        if (secondPhase && CanSummon && movesSinceSoul >= SoulAtLatest) return Move.Summon;
        // His dead are called again once the last levy is cut down: at the latest after a few moves, sometimes sooner.
        if (!LevyOut && (movesSinceLevy >= LevyAtLatest || movesSinceLevy >= 2 && Random.value < 0.2f)) return Move.Levy;

        var options = new List<Move> { Move.CofferSlam, Move.Toll, Move.Ofuda, Move.Beam, Move.Curtain, Move.Rend };
        if (secondPhase)
        {
            options.Add(Move.Swarm);
            options.Add(Move.River);
            options.Add(Move.Pendulum);
            options.Add(Move.Harvest);
        }
        else
        {
            // The slam is his signature: weighted twice.
            options.Add(Move.CofferSlam);
        }
        if (movesSinceParry >= ParryEvery) options.Add(parry);
        if (lastMove is Move last) options.Remove(last);
        Move pick = options[Random.Range(0, options.Count)];
        if (secondPhase && CanSummon && movesSinceSoul >= 2 && Random.value < 0.35f) return Move.Summon;
        return pick;
    }
}
