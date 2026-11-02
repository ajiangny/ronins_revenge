using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 3's warlord: Shirogane, the Forsaken Blade, a ghostly swordswoman in black, white and deep-blue armour
// whose twin katanas burn with blue spirit flame. She fights alone in her first phase, gliding about the top of
// the field and bringing pillars of ice down from the sky; once her first bar empties her spirit wolf answers her and
// the two fight side by side on one shared bar. Kuon is a foe of its own and hunts on its own clock, never waiting on
// her. Whichever of them falls first, the other is enraged and fights harder until it falls too. Everything they throw
// is a readable pattern with a lane or a gap left open, and once a phase a long tell asks for a parry.
public partial class Shirogane : Boss
{
    // Per bar: she fights two, the second with her wolf.
    public const int StartingHealth = 140;

    internal static readonly Color Spirit = new Color(0.45f, 0.85f, 1f);
    static readonly Color Cyan = new Color(0.3f, 0.92f, 1f);
    static readonly Color Frost = new Color(0.85f, 0.97f, 1f);
    static readonly Color MistColour = new Color(0.55f, 0.85f, 1f, 0.7f);
    static readonly Color Trail = new Color(0.45f, 0.85f, 1f, 0.5f);

    enum Move { CrescentSlash, BladeRain, MoonlitDash, BladeBarrage, MirrorMoon, Moonsplitter, OathFang, Moonfall, Glacier,
        WolfCharge, Howl, TwinAssault, WolfPack, EclipseFang, SpiritMaw, SpiritLances, WolfFang, FrozenHowl }
    // A move runs under its name ("Shirogane_<Move>", Boss.Attack), so the shots and blows it lands can be told apart.
    IEnumerator As(Move move, IEnumerator routine) => Attacking($"Shirogane_{move}", routine);

    // Each value is (phase 1, phase 2 with Kuon, phase 2 enraged): with the wolf at her side she gives no quarter, and
    // once it has fallen she gives less still.
    float Recovery => enraged ? 0.25f : secondPhase ? 0.5f : 0.8f;
    float ComboChance => enraged ? 0.85f : secondPhase ? 0.75f : 0.6f;
    int MaxCombo => enraged ? 4 : secondPhase ? 3 : 2;
    float GlideSpeed => enraged ? 10f : secondPhase ? 7.5f : 5f;
    float CrescentSpeed => enraged ? 11f : secondPhase ? 9f : 8f;
    float RainWarning => enraged ? 0.6f : secondPhase ? 0.7f : 0.9f;
    // The parry-only draws: how fast they come, and how long the sight line tracks the ronin before they do.
    float DrawSpeed => enraged ? 11f : 10f;
    float DrawAim => enraged ? 0.65f : secondPhase ? 0.8f : 0.9f;

    // Every shot she and Kuon throw flies this much faster than the speeds written in the moves.
    const float ShotSpeed = 1.35f;

    // Her rage, once Kuon has fallen: the blood-red edge in her spirit flame.
    static readonly Color Rage = new Color(1f, 0.4f, 0.5f);
    // The free stagger when Kuon falls, a moment to punish her grief.
    const float GriefStagger = 2.5f;
    // The fewest moves between two parry draws, and the most.
    const int ParryEvery = 3;
    const int ParryAtLatest = 4;

    public override string Title => "SHIROGANE";
    public override string Epithet => "The Forsaken Blade";
    // The epithet on her phase-two card.
    public const string OathEpithet = "Oath of the Spectral Wolf";
    public override Color Colour => Spirit;

    public override string Line(BossLine line) => line switch
    {
        BossLine.Intro => "I remember your blade. Do you remember mine?",
        BossLine.Phase => "You still fight as you did before... Then let us remind you what you've forgotten.",
        BossLine.Defeat => "Even after all this time... you still wield the blade I remember.",
        BossLine.DefeatFinal => "Go. Finish what we started.",
        BossLine.Stagger => "My stance...",
        BossLine.CrescentSlash => "Crescent Moon.",
        BossLine.BladeRain => "Blades... fall.",
        BossLine.MoonlitDash => "Too slow.",
        BossLine.BladeBarrage => "Scatter.",
        BossLine.WolfCharge => "Go.",
        BossLine.Howl => "Sing for them.",
        BossLine.TwinAssault => "Together.",
        BossLine.WolfPack => "Hunt.",
        BossLine.EclipseFang => "Eclipse... Fang.",
        BossLine.WolfFallen => "...Rest, Kuon.",
        BossLine.MirrorMoon => "Twin moons.",
        BossLine.Moonsplitter => "Answer me.",
        BossLine.OathFang => "Remember.",
        BossLine.KuonEnraged => "Kuon... don't hold back.",
        _ => null,
    };

    // Raised when Kuon takes the field at her phase break, so the HUD can show its bar.
    public event System.Action<SpiritWolf> WolfJoined;

    bool secondPhase => SecondPhase;
    // Kuon is in the fight and standing.
    bool WolfFights => secondPhase && wolf != null && wolf.Armed && !wolf.IsBroken;
    // Kuon stands and isn't mid-attack, so she can call it into a move of their own.
    bool WolfFree => WolfFights && !wolfBusy && !wolf.Dazed;
    // Kuon has fallen and she fights on enraged.
    bool enraged => secondPhase && wolf != null && wolf.IsBroken && !Down;
    bool breaking;
    Move? lastMove;
    // How many times running the last move has come up: never the same major move three times in a row.
    int repeats;
    Move? lastWolfMove;
    int wolfRepeats;
    int movesSinceEclipse;
    // Counts toward the next parry-only draw; starts part way so one comes within her first few moves.
    int movesSinceParry = 1;
    // Kuon's own attack loop, run on its own clock beside hers while it stands.
    Coroutine hunting;
    // True while Kuon is mid-attack of its own; she only calls it into a joint move while it's free.
    bool wolfBusy;
    // True while she has Kuon in a move of their own: its hunt skips its turn until she lets it go.
    bool borrowed;
    // Kuon's moves since its last parry draw and its last ultimate, once she is down and it fights enraged.
    int wolfMovesSinceFang;
    int wolfMovesSinceHowl;
    // Kuon's telegraphs, beams and orb: kept apart from hers, so her stagger doesn't wipe out an attack of its own.
    readonly List<GameObject> wolfTransient = new List<GameObject>();
    // True for the recovery after an ultimate, when she stands open and takes double damage.
    bool exposed;
    Coroutine roaming;
    // Her blades' spirit flame, lit at her hands; dark while she is spent.
    SpriteRenderer bladeGlow;
    // The dash frames from the GameManager, with her slash as the fallback.
    Sprite[] dashFrames;
    // Her spirit wolf: a faint silhouette behind her until her first bar empties, then her partner in the fight.
    SpiritWolf wolf;
    // True through the Eclipse Fang, which must play out whole.
    bool ultimate;
    // Which lanes the last wolf pack took, so the next takes others.
    readonly HashSet<int> lastPackLanes = new HashSet<int>();
    FxPlayer auraFlames;
    SpriteRenderer auraGlow;

    protected override bool Busy => breaking || ultimate;
    protected override bool HasSecondPhase => true;
    protected override float StaggerSeconds => secondPhase ? 1.6f : 2.2f;
    // Her fall is a scene: the blades drop, the wolf comes to her, and she has two things to say.
    public override float DefeatSeconds => 11f;
    // Just above the wolf's head and at the mouth it howls from.
    Vector3 WolfMouth => wolf != null ? wolf.Mouth : Centre;
    protected override int DamageMultiplier => exposed ? 2 : 1;
    // While Kuon stands, emptying her share of the bar only puts her down: Kuon fights on, enraged.
    protected override bool HoldOnAtZero => WolfFights;
    public override string PhaseTitle => "SHIROGANE & KUON";
    // In phase 2 she and Kuon each have half of the old 220 (scaled by the oaths like every warlord), so either can
    // fall first: her falling first is what enrages Kuon, which at 80 to her 140 almost never happened.
    public const int SecondPhaseShare = 110;
    protected override int SecondPhaseHealth => Mathf.RoundToInt(SecondPhaseShare * HealthScale);
    // From her phase break the bar is hers and Kuon's together. While her bar refills, Kuon's share fills with it.
    public override float BarFraction
    {
        get
        {
            if (!secondPhase || wolf == null) return HealthFraction;
            float wolfShare = wolf.Armed ? wolf.Health : wolf.MaxHealth * HealthFraction;
            return (Health + wolfShare) / (MaxHealth + wolf.MaxHealth);
        }
    }

    // The phase 2 bar in two halves, hers on the left and Kuon's on the right, so each one's health reads apart.
    public override bool SplitBar(out float left, out float right)
    {
        left = right = 0f;
        if (!secondPhase || wolf == null) return false;
        left = Down ? 0f : HealthFraction;
        right = !wolf.Armed ? HealthFraction : wolf.IsBroken ? 0f : wolf.HealthFraction;
        return true;
    }
    Vector3 Hands => Centre + Vector3.down * (sprite.bounds.size.y * 0.1f);

    public void SetDashFrames(Sprite[] dash)
    {
        dashFrames = dash != null && dash.Length > 0 ? dash : null;
    }

    // Her awakened dash, with spirit flame, for once the wolf arrives.
    Sprite[] phaseDash;

    public void SetPhaseDash(Sprite[] dash)
    {
        phaseDash = dash != null && dash.Length > 0 ? dash : null;
    }

    // The wolf's clips; any that are missing leave it drawn in code.
    Sprite[] wolfIdle, wolfHowl, wolfCharge, wolfVanish, wolfMaw;

    public void SetWolfFrames(Sprite[] idle, Sprite[] howl, Sprite[] charge, Sprite[] vanish, Sprite[] maw)
    {
        wolfMaw = maw != null && maw.Length > 0 ? maw : null;
        wolfIdle = idle != null && idle.Length > 0 ? idle : null;
        wolfHowl = howl != null && howl.Length > 0 ? howl : null;
        wolfCharge = charge != null && charge.Length > 0 ? charge : null;
        wolfVanish = vanish != null && vanish.Length > 0 ? vanish : null;
    }

    void Start()
    {
        bladeGlow = Projectile.CreateGlow(sprite.transform, 1.6f / transform.lossyScale.x, sprite.sortingOrder - 1, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        bladeGlow.transform.localPosition = new Vector3(0f, -0.1f, 0.05f);
        StartCoroutine(PulseBlades());
        // Summoned out of the coffin she comes alone: Kuon was never his.
        if (Summoned) return;
        wolf = SpiritWolf.Create(this, wolfIdle, wolfHowl, wolfCharge, wolfVanish, wolfMaw, transform.lossyScale.x, sprite.sortingOrder - 1);
        wolf.SetPresence(SpiritWolf.Presence.Silhouette);
        wolf.Broken += OnWolfBroken;
    }

    // Her wolf rests a little behind her and to the side, moving with her glides.
    public Vector3 WolfRest => anchor + new Vector3(anchor.x < CameraShake.Center.x ? 2.4f : -2.4f, 0.2f, 0f);

    IEnumerator PulseBlades()
    {
        while (bladeGlow != null && !IsDead)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            float lit = exposed || breaking || Down ? 0.08f : enraged ? 0.45f + 0.25f * pulse : 0.25f + 0.2f * pulse;
            // Enraged, her blades burn white-hot.
            Color flame = enraged ? Color.Lerp(Cyan, Color.white, 0.6f) : Cyan;
            bladeGlow.color = new Color(flame.r, flame.g, flame.b, lit);
            bladeGlow.transform.localScale = Vector3.one * ((1.5f + 0.3f * pulse) / transform.lossyScale.x);
            yield return null;
        }
    }

    protected override IEnumerator Fight()
    {
        exposed = false;
        // A fight restarted mid-move (the stall watchdog) must not leave Kuon lent to a move that will never end.
        if (borrowed) ReleaseWolf();
        while (true)
        {
            while (breaking) yield return null;
            // Kuon hunts on its own clock beside hers for as long as it stands.
            if (WolfFights && hunting == null) hunting = StartCoroutine(Hunt());
            // She glides on between combos: the opening is there, but she never stands still for it.
            yield return Roam(Recovery);

            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                Move move = NextMove();
                // A move of their own takes Kuon from its hunt until it ends. She only picks one while Kuon is free,
                // so neither of them ever stands waiting on the other.
                borrowed = WolfFree && (move == Move.TwinAssault || move == Move.EclipseFang || move == Move.OathFang);
                if (move == Move.EclipseFang || move == Move.Moonfall)
                {
                    // The ultimate ends every combo: nothing follows it but her recovery.
                    i = combo;
                }
                else if (move != Move.MoonlitDash && move != Move.TwinAssault && move != Move.MirrorMoon)
                {
                    yield return Glide(NearX(1.5f, 3.5f));
                }
                repeats = move == lastMove ? repeats + 1 : 1;
                lastMove = move;
                movesSinceEclipse = move == Move.EclipseFang || move == Move.Moonfall ? 0 : movesSinceEclipse + 1;
                movesSinceParry = move == Move.Moonsplitter || move == Move.OathFang ? 0 : movesSinceParry + 1;
                switch (move)
                {
                    case Move.CrescentSlash: yield return As(move, CrescentSlash()); break;
                    case Move.BladeRain: yield return As(move, BladeRain()); break;
                    case Move.MoonlitDash: yield return As(move, MoonlitDash()); break;
                    case Move.BladeBarrage: yield return As(move, BladeBarrage()); break;
                    case Move.MirrorMoon: yield return As(move, MirrorMoon()); break;
                    case Move.Moonsplitter: yield return As(move, Moonsplitter()); break;
                    case Move.OathFang: yield return As(move, OathFang()); break;
                    case Move.Moonfall: yield return As(move, Moonfall()); break;
                    case Move.Glacier: yield return As(move, Glacier()); break;
                    case Move.TwinAssault: yield return As(move, TwinAssault()); break;
                    case Move.EclipseFang: yield return As(move, EclipseFang()); break;
                }
                borrowed = false;
                yield return Wait(secondPhase ? 0.15f : 0.25f);
            }
        }
    }

    // Weighted at random: the phase's signature counts twice and the same move never comes three times running.
    // Her parry-only draw comes up once at least ParryEvery moves have passed, and for certain by ParryAtLatest;
    // in her second phase the ultimate comes every fifth move.
    Move NextMove()
    {
        Move parry = secondPhase ? Move.OathFang : Move.Moonsplitter;
        if (movesSinceParry >= ParryAtLatest) return parry;
        List<Move> options;
        if (!secondPhase)
        {
            options = new List<Move> { Move.CrescentSlash, Move.BladeRain, Move.BladeRain, Move.MoonlitDash, Move.BladeBarrage, Move.MirrorMoon };
        }
        else if (movesSinceEclipse >= 5 && lastMove != Move.EclipseFang && lastMove != Move.Moonfall)
        {
            // Their ultimate needs Kuon free for it; without it she brings the moon down herself.
            return WolfFree ? Move.EclipseFang : Move.Moonfall;
        }
        else if (enraged)
        {
            options = new List<Move> { Move.CrescentSlash, Move.CrescentSlash, Move.BladeRain, Move.MoonlitDash, Move.BladeBarrage, Move.MirrorMoon, Move.Glacier, Move.Glacier };
        }
        else
        {
            // Kuon's own moves run in Hunt; hers are the blades, and the attack they make together while it's free.
            options = new List<Move> { Move.CrescentSlash, Move.BladeRain, Move.MoonlitDash, Move.MirrorMoon, Move.Glacier };
            if (WolfFree) options.AddRange(new[] { Move.TwinAssault, Move.TwinAssault });
        }
        if (movesSinceParry >= ParryEvery) options.Add(parry);
        if (lastMove is Move last && repeats >= 2 && options.Exists(m => m != last)) options.RemoveAll(m => m == last);
        return options[Random.Range(0, options.Count)];
    }

    // Kuon's attack loop while it stands: a charge, a howl, the pack, its lances or its Maw, then a breath, then
    // another, on its own clock. It holds back only while it is dazed, through her phase break, and while she has
    // called it into a move of their own. Once she is down it fights on enraged, faster, with its own parry draw and
    // ultimate.
    IEnumerator Hunt()
    {
        yield return WolfWait(1f);
        while (WolfFights)
        {
            while (WolfFights && (breaking || borrowed || wolf.Dazed)) yield return null;
            if (!WolfFights) break;
            wolfBusy = true;
            Move move = NextWolfMove();
            wolfRepeats = move == lastWolfMove ? wolfRepeats + 1 : 1;
            lastWolfMove = move;
            wolfMovesSinceFang = move == Move.WolfFang ? 0 : wolfMovesSinceFang + 1;
            wolfMovesSinceHowl = move == Move.FrozenHowl ? 0 : wolfMovesSinceHowl + 1;
            switch (move)
            {
                case Move.WolfCharge: yield return As(move, WolfCharge()); break;
                case Move.Howl: yield return As(move, Howl()); break;
                case Move.WolfPack: yield return As(move, WolfPack()); break;
                case Move.SpiritMaw: yield return As(move, SpiritMaw()); break;
                case Move.SpiritLances: yield return As(move, SpiritLances()); break;
                case Move.WolfFang: yield return As(move, WolfFang()); break;
                case Move.FrozenHowl: yield return As(move, FrozenHowl()); break;
            }
            wolfBusy = false;
            yield return WolfWait(wolf.Enraged ? Random.Range(0.4f, 0.7f) : Random.Range(0.7f, 1.2f));
        }
        wolfBusy = false;
        hunting = null;
    }

    Move NextWolfMove()
    {
        if (wolf.Enraged)
        {
            // Enraged, it has an ultimate of its own every fifth move, and her parry draw falls to it.
            if (wolfMovesSinceHowl >= 5 && lastWolfMove != Move.FrozenHowl) return Move.FrozenHowl;
            if (wolfMovesSinceFang >= ParryAtLatest) return Move.WolfFang;
        }
        var options = new List<Move> { Move.WolfCharge, Move.WolfCharge, Move.Howl, Move.WolfPack, Move.SpiritMaw, Move.SpiritLances, Move.SpiritLances };
        if (wolf.Enraged && wolfMovesSinceFang >= ParryEvery) options.Add(Move.WolfFang);
        // Its Maw is the big one: never twice running.
        if (lastWolfMove == Move.SpiritMaw) options.Remove(Move.SpiritMaw);
        if (lastWolfMove is Move last && wolfRepeats >= 2) options.RemoveAll(m => m == last);
        return options[Random.Range(0, options.Count)];
    }

    void StopHunting()
    {
        if (hunting != null) StopCoroutine(hunting);
        hunting = null;
        wolfBusy = false;
        ClearWolfTransient();
    }

    // Kuon keeps its own time: her stagger, or frost on her, doesn't hold it.
    static IEnumerator WolfWait(float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
    }

    // Hands a telegraph or beam part to Kuon, so it outlasts an interruption of hers.
    void WolfOwns(GameObject thing)
    {
        if (thing == null) return;
        transient.Remove(thing);
        wolfTransient.Add(thing);
    }

    void ClearWolfTransient()
    {
        Sfx.Stop(Sfx.SpiritBeam);
        if (dimming != null) StopCoroutine(dimming);
        dimming = null;
        foreach (GameObject thing in wolfTransient)
        {
            if (thing != null) Destroy(thing);
        }
        wolfTransient.Clear();
        orb = orbHalo = orbCore = dimmer = null;
    }

    protected override void OnCleared()
    {
        ClearWolfTransient();
    }

    // Her lines over Kuon's moves; once she is down she has nothing more to call.
    void Command(BossLine line, float chance)
    {
        if (!Down && Random.value < chance) Speak(line);
    }

    // Kuon is broken. If she is already down it was all that kept her in the fight, and she falls. Otherwise grief
    // staggers her, the one long opening she gives, and she fights on enraged. Her farewell to it is said here; her
    // stagger cry would talk over it.
    void OnWolfBroken()
    {
        StopHunting();
        borrowed = false;
        if (Down)
        {
            Defeat();
            return;
        }
        ultimate = false;
        exposed = false;
        // The ultimate waits a full cycle, so her grief isn't followed straight away by Moonfall.
        movesSinceEclipse = 0;
        Fx.Play(Fx.Impact, Centre, Cyan, 3f);
        ElementFx.Glow(Centre, Frost, 1f, 4f, 0.5f);
        Sfx.Play(Sfx.WardShatter, 1f, 0.8f);
        CameraShake.Add(0.5f);
        Speak(BossLine.WolfFallen);
        RenameBar?.Invoke("SHIROGANE, The Enraged Blade");
        Stagger(GriefStagger, cry: false);
    }

    // Her share of the bar is empty while Kuon still stands: she drops her blades and kneels at the side, a fading
    // spirit nothing can touch, and calls on Kuon to fight on without her. It does, enraged.
    protected override void OnDowned()
    {
        StopRoaming();
        if (borrowed) ReleaseWolf();
        ultimate = false;
        exposed = false;
        StartCoroutine(Kneel());
        Speak(BossLine.KuonEnraged);
        RenameBar?.Invoke("KUON, The Enraged Guardian");
        wolf.Enrage();
        wolfMovesSinceFang = 1;
        wolfMovesSinceHowl = 0;
        if (hunting == null) hunting = StartCoroutine(Hunt());
    }

    // A move of theirs cut short: Kuon is let go, and its part of the move goes with hers.
    void ReleaseWolf()
    {
        borrowed = false;
        ClearWolfTransient();
        if (wolf != null && WolfFights) wolf.Recall();
    }

    // She sinks to a knee, her blades falling from her hands, and fades to a shade of herself.
    IEnumerator Kneel()
    {
        CameraShake.Add(0.4f);
        Sfx.Play(Sfx.BladeRing, 0.9f, 0.6f);
        DropBlade(Hands + Vector3.left * 0.6f, -1f);
        DropBlade(Hands + Vector3.right * 0.6f, 1f);
        if (auraFlames != null) auraFlames.Stop();
        if (art.death != null && art.death.Length > 0) animator.Play(art.death, false, null, art.actionFps * 0.6f);
        else sprite.transform.localRotation = Quaternion.Euler(0, 0, -10f);
        Vector3 from = anchor;
        Color tint = bodyTint;
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            float k = t / 1.2f;
            anchor = from + Vector3.down * (0.25f * k);
            bodyTint = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(tint.a, 0.45f, k));
            yield return null;
        }
        anchor = from + Vector3.down * 0.25f;
    }

    // --- Movement ---

    // A smooth glide along her row to x, trailing a little mist; never a blink, so she can always be followed.
    IEnumerator Glide(float x)
    {
        Vector3 from = anchor;
        Vector3 to = new Vector3(x, from.y, from.z);
        float seconds = Mathf.Max(0.15f, Mathf.Abs(to.x - from.x) / GlideSpeed);
        float nextGhost = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
            if (t >= nextGhost)
            {
                nextGhost = t + 0.08f;
                Afterimage(new Color(Spirit.r, Spirit.g, Spirit.b, 0.18f));
            }
            yield return null;
        }
        anchor = to;
    }

    // Drifts from spot to spot for a while, stopping short of its end so an attack never starts mid-glide.
    IEnumerator Roam(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until)
        {
            float x = NearX(1f, 2.5f);
            float trip = Mathf.Abs(x - anchor.x) / GlideSpeed;
            if (Time.time + trip > until) break;
            yield return Glide(x);
            yield return Wait(0.1f);
        }
        yield return Wait(Mathf.Max(0f, until - Time.time));
    }

    // Roams beside an attack that plays out on its own, so she isn't a still target while it does.
    void RoamMeanwhile(float seconds)
    {
        StopRoaming();
        roaming = StartCoroutine(Roam(seconds));
    }

    void StopRoaming()
    {
        if (roaming != null) StopCoroutine(roaming);
        roaming = null;
    }

    // Her stance breaks, but Kuon fights on: only a move they were making together is cut short for it too.
    protected override void OnStaggered()
    {
        StopRoaming();
        if (borrowed) ReleaseWolf();
        exposed = false;
        ultimate = false;
    }

    // --- Shots ---

    // One of her shots, at ShotSpeed, passing through the rest of hers and Kuon's so overlapping patterns stay whole.
    Arrow Fire(Vector3 from, Vector3 velocity)
    {
        Arrow shot = Shoot(from, velocity * ShotSpeed);
        shot.PassesShots = true;
        return shot;
    }

    // A blade of blue spirit flame cast from her katana; it takes two slashes to cut down, so slashing up her
    // column can't stop the fans by itself.
    Arrow Crescent(Vector3 from, Vector3 velocity)
    {
        Arrow wave = Fire(from, velocity);
        wave.Toughness = 2;
        Sprite[] frames = Fx.Frames(Fx.ShiroCrescent);
        if (frames != null) wave.Restyle(frames, 14f, Color.white, new Vector2(2.2f, 1.4f), 0.4f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 2.4f);
        else wave.Restyle(ProceduralSprites.Glow, Cyan, new Vector2(2f, 0.8f), 0.4f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), 2.4f);
        return wave;
    }

    // A ghostly katana that falls point first and drifts straight through cover.
    Arrow GhostSword(Vector3 from, Vector3 velocity)
    {
        Arrow blade = Fire(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroSword);
        if (frames != null) blade.Restyle(frames, 10f, Color.white, new Vector2(0.7f, 1.9f), 0.3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f), 1.4f);
        else blade.Restyle(ProceduralSprites.White, Frost, new Vector2(0.16f, 1.6f), 0.3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 1.4f);
        blade.PassesCover = true;
        return blade;
    }

    // A slow orb of the wolf's spirit energy, big enough to see coming and easy to read between.
    Arrow Orb(Vector3 from, Vector3 velocity)
    {
        Arrow orb = Fire(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroOrb);
        if (frames != null) orb.Restyle(frames, 12f, Color.white, new Vector2(1.1f, 1.1f), 0.35f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 1.8f);
        else orb.Restyle(ProceduralSprites.Glow, new Color(0.6f, 0.95f, 1f), new Vector2(1f, 1f), 0.35f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), 1.8f);
        return orb;
    }

    // The wolf (or one of its pack) as a charge down a lane: unbreakable, it stops only on the ronin, and dissolves
    // into mist where it meets the ground. Drawn from the charge art, or as a streak of spirit light.
    Arrow Charge(Vector3 from, float speed, float size)
    {
        Arrow charge = Fire(from, Vector3.down * speed);
        charge.MakeUnbreakable();
        WolfShot(charge, size, new Color(0.85f, 0.98f, 1f, 1f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        StartCoroutine(DissolveAtGround(charge));
        return charge;
    }

    // A charge's hit is its size times this either side of its lane, so its warning is drawn that wide.
    const float ChargeHalfWidth = 1.6f * 0.45f;
    const float PupHalfWidth = 1.1f * 0.45f;

    // Dresses a shot as a spirit wolf of `size` lunging along its flight, drawn at its own proportions: Kuon's charge
    // clip (its 104 px canvas is square, the wolf inside it tall and narrow), else the spirit streak, else a code
    // glow; it leaves a wake of fading ghosts of itself.
    void WolfShot(Arrow shot, float size, Color colour, Color halo)
    {
        if (wolfCharge != null)
        {
            float canvas = size * 2.6f;
            shot.Restyle(wolfCharge, 14f, colour, new Vector2(canvas, canvas), size * 0.45f, halo, size * 1.1f);
        }
        else if (Fx.Frames(Fx.ShiroWolfCharge) is Sprite[] streak)
        {
            // The streak is 20 by 64 px.
            shot.Restyle(streak, 14f, colour, new Vector2(size * 0.75f, size * 2.4f), size * 0.45f, halo, size * 2.2f);
        }
        else shot.Restyle(ProceduralSprites.Glow, colour, new Vector2(size * 1.1f, size * 2.2f), size * 0.45f, halo, size * 2.2f);
        StartCoroutine(Wake(shot, new Color(halo.r, halo.g, halo.b, 0.4f)));
    }

    // Fading ghosts left behind a shot as it flies.
    IEnumerator Wake(Arrow shot, Color colour)
    {
        while (shot != null)
        {
            Afterimage(shot.Art, colour);
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator DissolveAtGround(Arrow charge)
    {
        while (charge != null && charge.transform.position.y > GroundY) yield return null;
        if (charge == null) yield break;
        Puff(charge.transform.position, 1.8f);
        Destroy(charge.gameObject);
    }

    // A ghost sword's picture, hanging where it will fall from; drawn in code when the art is missing.
    SpriteRenderer SwordPicture(Vector3 at, float angle)
    {
        var picture = new GameObject("GhostSword").AddComponent<SpriteRenderer>();
        picture.transform.position = at + Vector3.back * 0.3f;
        picture.transform.rotation = Quaternion.Euler(0, 0, angle);
        picture.sortingOrder = 54;
        Sprite[] frames = Fx.Frames(Fx.ShiroSword);
        if (frames != null)
        {
            picture.sprite = frames[0];
            picture.transform.localScale = Vector3.one * (1.9f / frames[0].bounds.size.y);
            picture.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 10f);
            picture.color = new Color(1f, 1f, 1f, 0.85f);
        }
        else
        {
            picture.sprite = ProceduralSprites.White;
            picture.transform.localScale = new Vector3(0.16f, 1.6f, 1f);
            picture.color = Frost;
        }
        transient.Add(picture.gameObject);
        return picture;
    }

    // A thin lane warning from the top of the field to the ground that brightens as the blow comes.
    SpriteRenderer LaneWarning(float x)
    {
        return Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), 0.1f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));
    }

    IEnumerator Brighten(List<SpriteRenderer> tells, float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            float flicker = k > 0.7f ? 0.5f + 0.5f * Mathf.PingPong(t * 16f, 1f) : 1f;
            foreach (SpriteRenderer tell in tells)
            {
                if (tell != null) tell.color = new Color(Cyan.r, Cyan.g, Cyan.b, (0.3f + 0.6f * k) * flicker);
            }
            yield return null;
        }
    }

    void RemoveTells(List<SpriteRenderer> tells)
    {
        foreach (SpriteRenderer tell in tells)
        {
            if (tell == null) continue;
            transient.Remove(tell.gameObject);
            Destroy(tell.gameObject);
        }
        tells.Clear();
    }

    void Puff(Vector3 at, float size = 1.6f)
    {
        if (Fx.Play(Fx.ShiroMist, at, Color.white, size) == null) Fx.Play(Fx.SmokePuff, at, MistColour, size);
    }

    // --- Ice ---

    // An ice pillar's size, drawn and hit, in world units.
    const float PillarLength = 3.2f;
    const float PillarWidth = 1.1f;
    const float PillarRadius = 0.42f;
    // A glacier spire's: its hit reaches SpireRadius either side of its lane.
    const float SpireLength = 7.5f;
    const float SpireWidth = 3.2f;
    const float SpireRadius = 1.45f;
    static readonly Color DeepIce = new Color(0.25f, 0.45f, 1f);

    // Where a falling pillar hangs before it drops: whole, just inside the top of the field.
    float PillarHang => TopOfScreen - PillarLength * 0.5f - 0.9f;

    // A pillar of ice dropped down a lane, point first: blows and cover don't stop it, but a parry knocks it aside.
    // It shatters where it meets the ground.
    Arrow IcePillar(Vector3 from, float speed)
    {
        Arrow pillar = Fire(from, Vector3.down * speed);
        Sprite[] frames = Fx.Frames(Fx.ShiroIcePillar);
        Color halo = new Color(Frost.r, Frost.g, Frost.b, 0.4f);
        if (frames != null) pillar.Restyle(frames, 8f, Color.white, new Vector2(PillarWidth, PillarLength), PillarRadius, halo, 1.6f);
        else pillar.Restyle(ProceduralSprites.White, new Color(0.75f, 0.93f, 1f, 0.9f), new Vector2(0.55f, PillarLength * 0.9f), PillarRadius, halo, 1.6f);
        pillar.Lengthen(PillarLength * 0.85f);
        pillar.MakeUnbreakable();
        pillar.PassesCover = true;
        StartCoroutine(ShatterAtGround(pillar, PillarLength, false));
        return pillar;
    }

    // A glacier spire, far bigger than a pillar: it can't be parried, only dodged, and it bursts at the ground with
    // a shockwave.
    Arrow IceSpire(Vector3 from, float speed)
    {
        Arrow spire = Fire(from, Vector3.down * speed);
        Sprite[] frames = Fx.Frames(Fx.ShiroIceSpire);
        // Violet, the colour of a shot that can only be dodged.
        Color halo = ShotClasses.SealedGlow;
        if (frames != null) spire.Restyle(frames, 8f, Color.white, new Vector2(SpireWidth, SpireLength), SpireRadius, halo, SpireWidth * 1.3f);
        else spire.Restyle(ProceduralSprites.White, new Color(0.6f, 0.82f, 1f, 0.92f), new Vector2(SpireWidth * 0.7f, SpireLength * 0.95f), SpireRadius, halo, SpireWidth * 1.3f);
        spire.Lengthen(SpireLength * 0.85f);
        spire.MakeUnbreakable();
        spire.Unparryable = true;
        spire.PassesCover = true;
        StartCoroutine(ShatterAtGround(spire, SpireLength, true));
        return spire;
    }

    // Once its point reaches the ground the ice bursts into shards: a pillar with a crack, a spire with a shockwave.
    IEnumerator ShatterAtGround(Arrow shot, float length, bool huge)
    {
        while (shot != null && shot.transform.position.y - length * 0.5f > GroundY) yield return null;
        if (shot == null) yield break;
        Vector3 at = new Vector3(shot.transform.position.x, GroundY + 0.4f, 0);
        float size = huge ? 4f : 1.6f;
        if (Fx.Play(Fx.ShiroIceShatter, at, Color.white, size) == null) Fx.Play(Fx.Impact, at, Frost, size);
        Puff(at, huge ? 3f : 1.2f);
        Sfx.Play(Sfx.FrostChill, huge ? 1f : 0.5f, huge ? 0.6f : 1.3f);
        if (huge)
        {
            Fx.Play(Fx.Explosion, at, DeepIce, 3.5f);
            ElementFx.Glow(at, Frost, 1f, 6f, 0.4f);
            Sfx.Play(Sfx.WardShatter, 0.8f, 0.7f);
            CameraShake.Add(0.5f);
        }
        else CameraShake.Add(0.06f);
        Destroy(shot.gameObject);
    }

    // An ice pillar's (or spire's) picture, faint, hanging where it will fall from; drawn in code without the art.
    SpriteRenderer PillarPicture(Vector3 at, bool huge = false, bool wolfs = false)
    {
        var picture = new GameObject(huge ? "IceSpire" : "IcePillar").AddComponent<SpriteRenderer>();
        picture.transform.position = at + Vector3.back * 0.3f;
        picture.sortingOrder = 54;
        Sprite[] frames = Fx.Frames(huge ? Fx.ShiroIceSpire : Fx.ShiroIcePillar);
        Vector2 size = huge ? new Vector2(SpireWidth, SpireLength) : new Vector2(PillarWidth, PillarLength);
        if (frames != null)
        {
            picture.sprite = frames[0];
            Vector2 art = frames[0].bounds.size;
            picture.transform.localScale = new Vector3(size.x / art.x, size.y / art.y, 1f);
            if (frames.Length > 1) picture.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
            picture.color = new Color(1f, 1f, 1f, 0.75f);
        }
        else
        {
            picture.sprite = ProceduralSprites.White;
            picture.transform.localScale = new Vector3(size.x * (huge ? 0.7f : 0.5f), size.y * 0.9f, 1f);
            picture.color = new Color(Frost.r, Frost.g, Frost.b, 0.6f);
        }
        (wolfs ? wolfTransient : transient).Add(picture.gameObject);
        return picture;
    }

    // A ring of frost on the ground under a pillar's lane, which brightens with its warning line.
    SpriteRenderer FrostMark(float x)
    {
        SpriteRenderer mark = Glowing("FrostMark", ProceduralSprites.SoftGlow, 3);
        mark.transform.position = new Vector3(x, GroundY + 0.2f, 0);
        mark.transform.localScale = new Vector3(1.8f, 0.5f, 1f);
        return mark;
    }

    // A glacier spire down lane x: its lane darkens under a deep blue band, its shadow spreads on the ground and the
    // field rumbles as its point lowers into sight over `tell` seconds, then it falls. Cut short (her stagger, or
    // Kuon's attack swept away), it never falls.
    IEnumerator Spire(float x, float tell, bool wolfs = false)
    {
        float half = SpireRadius * TellMargin;
        SpriteRenderer band = Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), half * 2f, Color.clear, 52);
        if (wolfs) WolfOwns(band.gameObject);
        SpriteRenderer shadow = Glowing("SpireShadow", ProceduralSprites.SoftGlow, 3, wolfs);
        shadow.transform.position = new Vector3(x, GroundY + 0.2f, 0);
        float top = TopOfScreen + SpireLength * 0.5f;
        SpriteRenderer picture = PillarPicture(new Vector3(x, top - 0.6f, 0), true, wolfs);
        Puff(new Vector3(x, TopOfScreen - 0.8f, 0), 2.4f);
        Sfx.Play(Sfx.FrostChill, 0.9f, 0.55f);
        float nextRumble = 0f;
        for (float t = 0; t < tell; t += Time.deltaTime)
        {
            if (band == null || shadow == null || picture == null)
            {
                Drop(band);
                Drop(shadow);
                Drop(picture);
                yield break;
            }
            float k = t / tell;
            float flicker = k > 0.75f ? 0.6f + 0.4f * Mathf.PingPong(t * 14f, 1f) : 1f;
            band.color = new Color(DeepIce.r, DeepIce.g, DeepIce.b, (0.04f + 0.12f * k) * flicker);
            shadow.color = new Color(0.05f, 0.1f, 0.25f, 0.25f + 0.45f * k);
            shadow.transform.localScale = new Vector3(half * 2.4f * (0.6f + 0.4f * k), 0.9f, 1f);
            picture.transform.position = new Vector3(x, top - Mathf.Lerp(0.6f, 2.2f, k), -0.3f);
            if (t >= nextRumble)
            {
                nextRumble = t + 0.15f;
                CameraShake.Add(0.02f + 0.05f * k);
            }
            yield return null;
        }
        bool whole = picture != null;
        Vector3 from = whole ? picture.transform.position : Vector3.zero;
        Drop(band);
        Drop(shadow);
        Drop(picture);
        if (!whole) yield break;
        from.z = 0;
        IceSpire(from, 13f);
        Sfx.Play(Sfx.GhostSwordFall, 1f, 0.6f);
    }

}
