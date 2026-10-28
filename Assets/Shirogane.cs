using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 3's warlord: Shirogane, the Forsaken Blade, a ghostly swordswoman in black, white and deep-blue armour
// whose twin katanas burn with blue spirit flame. She fights alone in her first phase, gliding about the top of
// the field and bringing pillars of ice down from the sky; once her first bar empties her spirit wolf answers her and
// the two fight side by side on one shared bar. Kuon is a foe of its own and hunts on its own clock, never waiting on
// her. Whichever of them falls first, the other is enraged and fights harder until it falls too. Everything they throw
// is a readable pattern with a lane or a gap left open, and once a phase a long tell asks for a parry.
public class Shirogane : Boss
{
    // Per bar: she fights two, the second with her wolf.
    public const int StartingHealth = 140;

    static readonly Color Spirit = new Color(0.45f, 0.85f, 1f);
    static readonly Color Cyan = new Color(0.3f, 0.92f, 1f);
    static readonly Color Frost = new Color(0.85f, 0.97f, 1f);
    static readonly Color MistColour = new Color(0.55f, 0.85f, 1f, 0.7f);
    static readonly Color Trail = new Color(0.45f, 0.85f, 1f, 0.5f);

    enum Move { CrescentSlash, BladeRain, MoonlitDash, BladeBarrage, MirrorMoon, Moonsplitter, OathFang, Moonfall, Glacier,
        WolfCharge, Howl, TwinAssault, WolfPack, EclipseFang, SpiritMaw, SpiritLances, WolfFang, FrozenHowl }

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
        bladeGlow = Kunai.CreateGlow(sprite.transform, 1.6f / transform.lossyScale.x, sprite.sortingOrder - 1, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        bladeGlow.transform.localPosition = new Vector3(0f, -0.1f, 0.05f);
        StartCoroutine(PulseBlades());
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
                    case Move.CrescentSlash: yield return CrescentSlash(); break;
                    case Move.BladeRain: yield return BladeRain(); break;
                    case Move.MoonlitDash: yield return MoonlitDash(); break;
                    case Move.BladeBarrage: yield return BladeBarrage(); break;
                    case Move.MirrorMoon: yield return MirrorMoon(); break;
                    case Move.Moonsplitter: yield return Moonsplitter(); break;
                    case Move.OathFang: yield return OathFang(); break;
                    case Move.Moonfall: yield return Moonfall(); break;
                    case Move.Glacier: yield return Glacier(); break;
                    case Move.TwinAssault: yield return TwinAssault(); break;
                    case Move.EclipseFang: yield return EclipseFang(); break;
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
                case Move.WolfCharge: yield return WolfCharge(); break;
                case Move.Howl: yield return Howl(); break;
                case Move.WolfPack: yield return WolfPack(); break;
                case Move.SpiritMaw: yield return SpiritMaw(); break;
                case Move.SpiritLances: yield return SpiritLances(); break;
                case Move.WolfFang: yield return WolfFang(); break;
                case Move.FrozenHowl: yield return FrozenHowl(); break;
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
        Color halo = new Color(DeepIce.r, DeepIce.g, DeepIce.b, 0.5f);
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

    // --- Phase 1 moves ---

    // Crescent Moon Slash: her blades glow bright cyan, then a swing casts five crescents in a fan beneath her.
    // In her second phase the fan is aimed, its centre blade at the ronin.
    IEnumerator CrescentSlash()
    {
        if (Random.value < 0.5f) Speak(BossLine.CrescentSlash);
        ElementFx.Glow(Hands, Cyan, 0.8f, 2.6f, 0.6f, transform);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.1f);
        yield return Wait(secondPhase ? 0.5f : 0.6f);

        PlayAction(art.attack);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        // Past the middle of her first bar she has his measure, and aims.
        bool aimed = secondPhase || HealthFraction < 0.5f;
        Vector3 aim = aimed ? (ronin.Bounds.center - Hands).normalized : Vector3.down;
        WolfSlash(Hands, aim, 2.6f);
        CrescentFan(aim, 0f);
        // Enraged, a second swing follows, its blades between the first's and aimed afresh.
        if (enraged)
        {
            yield return Wait(0.25f);
            PlayAction(art.attack);
            Sfx.Play(Sfx.SwordSlash, 0.9f, 0.8f);
            Vector3 again = (ronin.Bounds.center - Hands).normalized;
            WolfSlash(Hands, again, 2.6f);
            CrescentFan(again, 9f);
        }
        yield return Wait(0.3f);
    }

    // Her sword slash: a frost arc sweeps out from `at` toward `aim` and an ice wolf's head lunges along it, jaws
    // open, and fades. Without the art, the plain slash arc and a glow.
    void WolfSlash(Vector3 at, Vector3 aim, float size)
    {
        aim.z = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.down;
        FxPlayer arc = Fx.Play(Fx.ShiroIceSlash, at, Color.white, size * 0.6f);
        if (arc != null) arc.transform.rotation = Quaternion.FromToRotation(Vector3.right, aim);
        else if (Fx.Play(Fx.SlashArc, at, Cyan, size) == null) ElementFx.Glow(at, Cyan, 1f, size + 0.4f, 0.2f);
        Sprite[] head = Fx.Frames(Fx.ShiroWolfSlash);
        if (head != null) StartCoroutine(Lunge(head[0], at, aim, size));
    }

    // The ice wolf's head leaping out along a slash: it swells in, lunges past the arc and fades, leaving a ghost.
    IEnumerator Lunge(Sprite head, Vector3 at, Vector3 aim, float size)
    {
        SpriteRenderer wolfHead = Glowing("IceWolf", head, 57);
        wolfHead.transform.rotation = Quaternion.FromToRotation(Vector3.right, aim);
        // Drawn facing right; flipped so it never lunges upside down.
        wolfHead.flipY = aim.x < 0f;
        float scale = size * 1.3f / head.bounds.size.x;
        const float Seconds = 0.4f;
        for (float t = 0; t < Seconds && wolfHead != null; t += Time.deltaTime)
        {
            float k = t / Seconds;
            wolfHead.transform.position = at + aim * (size * (0.15f + 0.9f * Mathf.Sqrt(k))) + Vector3.back * 0.3f;
            wolfHead.transform.localScale = Vector3.one * scale * (0.7f + 0.4f * k);
            wolfHead.color = new Color(1f, 1f, 1f, k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f);
            if (k > 0.3f && Random.value < 0.35f) Afterimage(wolfHead, new Color(Frost.r, Frost.g, Frost.b, 0.35f));
            yield return null;
        }
        Drop(wolfHead);
    }

    // Five crescents in a fan about `aim`, turned by `offset` degrees.
    void CrescentFan(Vector3 aim, float offset)
    {
        var fan = new List<Arrow>();
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(-36f, 36f, i / 4f) + offset;
            fan.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
    }

    // Phantom Blade Rain: pillars of ice form at the top of six lanes, a thin warning line and a ring of frost under
    // each, then fall. At least one lane stays empty. In her second phase there are seven lanes, two empty, the
    // pillars fall in a ripple, and one lane (never beside an open one) brings down a glacier spire that must be
    // dodged; enraged, eight lanes with only one empty.
    IEnumerator BladeRain()
    {
        if (Random.value < 0.5f) Speak(BossLine.BladeRain);
        int laneCount = enraged ? 8 : secondPhase ? 7 : 6;
        int empty = secondPhase && !enraged ? 2 : 1;
        List<float> lanes = Lanes(laneCount);
        // The empty lanes are picked at random; the ronin's own lane is never made the only way out on purpose.
        var open = new HashSet<int>();
        while (open.Count < empty) open.Add(Random.Range(0, lanes.Count));
        int spire = -1;
        if (secondPhase)
        {
            var away = new List<int>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!open.Contains(i) && !open.Contains(i - 1) && !open.Contains(i + 1)) away.Add(i);
            }
            if (away.Count > 0) spire = away[Random.Range(0, away.Count)];
        }

        var tells = new List<SpriteRenderer>();
        var pillars = new List<SpriteRenderer>();
        var falling = new List<float>();
        float hang = PillarHang;
        for (int i = 0; i < lanes.Count; i++)
        {
            if (open.Contains(i)) continue;
            if (i == spire)
            {
                StartCoroutine(Spire(lanes[i], RainWarning));
                continue;
            }
            float x = lanes[i];
            falling.Add(x);
            tells.Add(LaneWarning(x));
            tells.Add(FrostMark(x));
            pillars.Add(PillarPicture(new Vector3(x, hang, 0)));
            Puff(new Vector3(x, hang, 0), 1f);
        }
        Sfx.Play(Sfx.MistSwirl, 0.6f, 1f);
        Sfx.Play(Sfx.FrostChill, 0.5f, 1.1f);
        RoamMeanwhile(RainWarning);
        yield return Brighten(tells, RainWarning);
        StopRoaming();

        for (int i = 0; i < falling.Count; i++)
        {
            Drop(pillars[i]);
            IcePillar(new Vector3(falling[i], hang, 0), 11f);
            if (secondPhase) yield return Wait(0.08f);
        }
        RemoveTells(tells);
        Sfx.Play(Sfx.GhostSwordFall, 0.9f, 0.8f);
        yield return Wait(0.25f);
        RingingFence();
        yield return Wait(0.4f);
    }

    // After the rain, a short fence of ghost swords falls over the ronin with one bronze-edged ringing blade at its
    // heart. He can walk out round the end, or parry the ringing blade: it rings, and the swords beside it burst.
    // It's the first bronze shot, so the Emperor's bells read as a parry when they come.
    void RingingFence()
    {
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 5f, RightEdge - 5f);
        float hang = TopOfScreen - 1.4f;
        BulletHell.Shooter sword = (from, velocity) => BulletHell.Enlist(GhostSword(from, velocity));
        BulletHell.Shooter ringing = (from, velocity) =>
        {
            Arrow blade = BulletHell.MakeResonant(BulletHell.Enlist(GhostSword(from, velocity)));
            blade.Tint(Color.Lerp(Color.white, BulletHell.Bronze, 0.7f), new Color(BulletHell.Bronze.r, BulletHell.Bronze.g, BulletHell.Bronze.b, 0.7f));
            return blade;
        };
        BulletHell.Curtain(sword, hang, x - 4.2f, x + 4.2f, 1.4f, null, BulletHell.MinGap, 7f, i => i == 3 ? ringing : null);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.8f);
    }

    // Moonlit Dash: a blue trail shows the way, she vanishes into mist and streaks across the field, and three
    // afterimages left along her path each drop a pillar of ice straight down, one after the other. Enraged, she
    // streaks straight back the way she came and leaves three more.
    IEnumerator MoonlitDash()
    {
        if (Random.value < 0.4f) Speak(BossLine.MoonlitDash);
        float range = HalfWidth - 2f;
        int passes = enraged ? 2 : 1;
        for (int pass = 0; pass < passes; pass++)
        {
            float far = anchor.x < CameraShake.Center.x ? CameraShake.Center.x + range : CameraShake.Center.x - range;
            Vector3 from = anchor;
            Vector3 to = new Vector3(far, anchor.y, anchor.z);
            SpriteRenderer trail = Bar(from, to, 0.08f, Trail);
            float tell = pass == 0 ? 0.5f : 0.3f;
            for (float t = 0; t < tell; t += Time.deltaTime * Tempo)
            {
                trail.color = new Color(Trail.r, Trail.g, Trail.b, 0.25f + 0.35f * Mathf.PingPong(t * 6f, 1f));
                yield return null;
            }
            transient.Remove(trail.gameObject);
            Destroy(trail.gameObject);

            Puff(Centre, 2.2f);
            Sfx.Play(Sfx.MistSwirl, 0.8f, 1.2f);
            if (dashFrames != null) animator.Play(dashFrames, true, null, art.actionFps);
            sprite.flipX = to.x < from.x;
            var ghosts = new List<SpriteRenderer>();
            float dash = 0.4f;
            for (float t = 0; t < dash; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
            {
                float k = t / dash;
                anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, k));
                if (ghosts.Count < 3 && k >= (ghosts.Count + 1) / 4f) ghosts.Add(Linger());
                yield return null;
            }
            anchor = to;
            sprite.flipX = false;
            PlayIdle();
            Puff(Centre, 2.2f);

            // The afterimages let fly in the order she left them, so the pattern walks across the field.
            foreach (SpriteRenderer ghost in ghosts)
            {
                if (ghost == null) continue;
                Vector3 at = ghost.bounds.center + Vector3.down * 0.4f;
                IcePillar(at, 10f);
                Sfx.Play(Sfx.GhostSwordFall, 0.6f, 1.2f);
                transient.Remove(ghost.gameObject);
                Fader.Fade(ghost, 0.3f);
                yield return Wait(pass < passes - 1 ? 0.18f : 0.25f);
            }
        }
    }

    // A spectral copy of her that stays where she stood until it has struck.
    SpriteRenderer Linger()
    {
        var ghost = new GameObject("Afterimage").AddComponent<SpriteRenderer>();
        ghost.transform.SetPositionAndRotation(sprite.transform.position, sprite.transform.rotation);
        ghost.transform.localScale = sprite.transform.lossyScale;
        ghost.sprite = sprite.sprite;
        ghost.flipX = sprite.flipX;
        ghost.sortingOrder = sprite.sortingOrder - 1;
        ghost.color = new Color(Spirit.r, Spirit.g, Spirit.b, 0.55f);
        transient.Add(ghost.gameObject);
        return ghost;
    }

    // Spirit Blade Barrage: two rows of floating katanas wheel about her, then the inner row flies down and to
    // the left and the outer row down and to the right, crossing in the middle of the field. Each sword curves after
    // the ronin as it falls, then flies straight for the last stretch, so standing still is a hit but a late step
    // aside still slips it.
    IEnumerator BladeBarrage()
    {
        if (Random.value < 0.5f) Speak(BossLine.BladeBarrage);
        PlayAction(art.parry);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.9f);
        const int perRow = 4;
        var inner = new List<SpriteRenderer>();
        var outer = new List<SpriteRenderer>();
        for (int i = 0; i < perRow; i++)
        {
            inner.Add(SwordPicture(Centre, 0f));
            outer.Add(SwordPicture(Centre, 0f));
        }
        // They wheel for a second, slowing into their firing spread as the second ends.
        float spin = secondPhase ? 0.8f : 1f;
        for (float t = 0; t < spin; t += Time.deltaTime * Tempo)
        {
            float k = t / spin;
            float turn = t * 240f * (1f - k * 0.6f);
            for (int i = 0; i < perRow; i++)
            {
                PlaceOrbiting(inner[i], 1.3f, turn + i * 90f, -30f, k);
                PlaceOrbiting(outer[i], 2.1f, -turn + i * 90f + 45f, 30f, k);
            }
            yield return null;
        }

        // Rows fire a beat apart so the crossing paths read as two separate waves.
        LooseRow(inner, -30f);
        Sfx.Play(Sfx.GhostSwordFall, 0.8f, 0.9f);
        yield return Wait(0.4f);
        LooseRow(outer, 30f);
        Sfx.Play(Sfx.GhostSwordFall, 0.8f, 1.1f);
        yield return Wait(0.3f);
    }

    // Eases a wheeling sword from its orbit into a line above her, spaced for its row's dive angle.
    void PlaceOrbiting(SpriteRenderer sword, float radius, float angle, float dive, float settle)
    {
        if (sword == null) return;
        int index = sword.transform.GetSiblingIndex();
        Vector3 orbit = Centre + Quaternion.Euler(0, 0, angle) * Vector3.right * radius;
        float slot = (index % 4 - 1.5f) * 1.6f;
        Vector3 rest = Centre + new Vector3(slot, radius * 0.5f + 0.4f, 0);
        float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.5f, 1f, settle));
        sword.transform.position = Vector3.Lerp(orbit, rest, k) + Vector3.back * 0.3f;
        sword.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(angle + 90f, dive, k));
    }

    void LooseRow(List<SpriteRenderer> row, float dive)
    {
        var volley = new List<Arrow>();
        Vector3 velocity = Quaternion.Euler(0, 0, dive) * Vector3.down * 7f;
        foreach (SpriteRenderer sword in row)
        {
            if (sword == null) continue;
            Vector3 at = sword.transform.position;
            at.z = 0;
            transient.Remove(sword.gameObject);
            Destroy(sword.gameObject);
            Arrow blade = GhostSword(at, velocity);
            volley.Add(blade);
            StartCoroutine(Seek(blade));
        }
        IgnoreEachOther(volley);
    }

    // How fast a barrage sword turns after the ronin, in degrees a second, and how far above him it gives up and
    // flies straight.
    float SeekTurn => enraged ? 240f : secondPhase ? 170f : 140f;
    const float SeekStops = 3.5f;
    const float SeekSeconds = 1.4f;

    IEnumerator Seek(Arrow blade)
    {
        for (float t = 0; t < SeekSeconds; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            if (blade == null || ronin == null) yield break;
            Vector3 at = blade.transform.position;
            Vector3 target = ronin.Bounds.center;
            if (at.y - target.y < SeekStops) yield break;
            Vector3 velocity = blade.GetComponent<Rigidbody>().linearVelocity;
            Vector3 toward = target - at;
            toward.z = 0;
            Vector3 turned = Vector3.RotateTowards(velocity, toward.normalized * velocity.magnitude, SeekTurn * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            blade.Turn(turned);
        }
    }

    // Mirror Moon: she takes one side of the field and leaves a reflection of herself standing on the other, mirrored
    // across the middle. Each looses an aimed fan of three crescents a beat apart, a crossfire with gaps between.
    // Enraged, a second reflection stands in the middle and fires last.
    IEnumerator MirrorMoon()
    {
        if (Random.value < 0.5f) Speak(BossLine.MirrorMoon);
        float side = anchor.x < CameraShake.Center.x ? -1f : 1f;
        yield return Glide(CameraShake.Center.x + side * HalfWidth * 0.45f);
        var reflections = new List<SpriteRenderer>();
        float mirrorX = CameraShake.Center.x * 2f - sprite.transform.position.x;
        var spots = new List<float> { mirrorX };
        if (enraged) spots.Add(CameraShake.Center.x);
        foreach (float x in spots)
        {
            SpriteRenderer reflection = Linger();
            reflection.transform.position = new Vector3(x, reflection.transform.position.y, reflection.transform.position.z);
            reflection.flipX = !sprite.flipX;
            Puff(reflection.bounds.center, 2f);
            ElementFx.Glow(ReflectionHands(reflection), Cyan, 0.7f, 2.4f, 0.6f);
            reflections.Add(reflection);
        }
        Sfx.Play(Sfx.MistSwirl, 0.7f, 1.3f);
        ElementFx.Glow(Hands, Cyan, 0.7f, 2.4f, 0.6f, transform);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.25f);
        yield return Wait(enraged ? 0.45f : 0.6f);

        PlayAction(art.attack);
        AimedFan(Hands);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.75f);
        foreach (SpriteRenderer reflection in reflections)
        {
            yield return Wait(enraged ? 0.3f : 0.35f);
            if (reflection == null) continue;
            AimedFan(ReflectionHands(reflection));
            Sfx.Play(Sfx.SwordSlash, 0.8f, 0.85f);
            transient.Remove(reflection.gameObject);
            Fader.Fade(reflection, 0.4f);
        }
        yield return Wait(0.3f);
    }

    static Vector3 ReflectionHands(SpriteRenderer reflection)
    {
        return reflection.bounds.center + Vector3.down * (reflection.bounds.size.y * 0.1f);
    }

    // Three crescents from a point, the middle one at the ronin.
    void AimedFan(Vector3 from)
    {
        Vector3 aim = (ronin.Bounds.center - from).normalized;
        WolfSlash(from, aim, 2.2f);
        var fan = new List<Arrow>();
        for (int i = 0; i < 3; i++)
        {
            float angle = Mathf.Lerp(-16f, 16f, i / 2f);
            fan.Add(Crescent(from, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
    }

    // --- Parry-only draws ---

    // Crossing Moons, her parry-only draw: she crosses her twin blades and a ghost crescent forms on each, brightening
    // while motes trace the curves they will take and a reticle closes on him, then flashing as it locks. She
    // flings them out to either side and they curve back in low along the ground like sickles, closing like scissors,
    // their curves bending to follow him and trailing afterimages, and meet on him together: there is nowhere to
    // step, so the answer is a parry as they cross. Parried, they fly back and break her stance.
    IEnumerator Moonsplitter()
    {
        Speak(BossLine.Moonsplitter);
        PlayAction(art.parry);
        ElementFx.Glow(Hands, Frost, 0.9f, 3f, DrawAim, transform);
        Sfx.Play(Sfx.BladeRing, 0.8f, 0.8f);

        float height = ronin.Bounds.center.y;
        // Each moon leaves one of her blades, flies out wide to its own side and curves back in low along the ground
        // to him: a sickle's curve, bending past the edge of the field at about his height.
        Vector3[] starts = { Hands + Vector3.left * 0.6f, Hands + Vector3.right * 0.6f };
        Vector3[] bends = { new Vector3(LeftEdge - 1.5f, height + MoonBend, 0), new Vector3(RightEdge + 1.5f, height + MoonBend, 0) };
        var ghosts = new List<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.ShiroCrescent);
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer ghost = Glowing("MoonGhost", frames != null ? frames[0] : ProceduralSprites.Glow, 54);
            ghost.transform.position = starts[i];
            // Drawn facing down, the way it sets off.
            ghost.transform.rotation = Quaternion.FromToRotation(Vector3.down, bends[i] - starts[i]);
            float art = frames != null ? frames[0].bounds.size.x : 1f;
            ghost.transform.localScale = Vector3.one * (2.4f / art);
            ghosts.Add(ghost);
            Puff(starts[i], 1.6f);
        }
        Sfx.Play(Sfx.MistSwirl, 0.7f, 1.2f);
        // The ghosts brighten and their paths are traced in motes while the reticle closes on him.
        StartCoroutine(Brighten(ghosts, DrawAim));
        Coroutine tracing = StartCoroutine(TraceMoonPaths(starts, bends, DrawAim + 0.18f));
        yield return LockOn(Hands, DrawAim, Frost);
        if (tracing != null) StopCoroutine(tracing);
        foreach (SpriteRenderer ghost in ghosts) Drop(ghost);

        PlayAction(art.attack);
        Sfx.Play(Sfx.SwordSlash, 1f, 0.55f);
        Sfx.Play(Sfx.BladeRing, 0.9f, 0.7f);
        CameraShake.Add(0.3f);
        var pair = new List<Arrow>();
        float speed = DrawSpeed * ShotSpeed * 0.9f;
        // Both take the same time, so they meet on him together; the curve is a little shorter than its corners.
        float seconds = 0.85f * ((bends[0] - starts[0]).magnitude + (ronin.Bounds.center - bends[0]).magnitude) / speed;
        for (int i = 0; i < 2; i++)
        {
            Arrow moon = Fire(starts[i], (bends[i] - starts[i]).normalized * speed / ShotSpeed);
            Color halo = new Color(Frost.r, Frost.g, Frost.b, 0.6f);
            if (frames != null) moon.Restyle(frames, 14f, Color.white, new Vector2(3.2f, 2.2f), 0.8f, halo, 3.6f);
            else moon.Restyle(ProceduralSprites.Glow, Frost, new Vector2(1.4f, 3f), 0.8f, halo, 3.6f);
            moon.MakeUnbreakable();
            moon.MakeReflectable(transform);
            WolfSlash(starts[i], bends[i] - starts[i], 2.6f);
            pair.Add(moon);
            StartCoroutine(Close(moon, starts[i], bends[i], seconds));
        }
        IgnoreEachOther(pair);
        yield return Wait(0.6f);
    }

    // How far above the ronin the crossing moons' curves bend back in.
    const float MoonBend = 1.2f;

    // A point on the sickle curve from `start`, bending at `bend`, ending at `end`, `u` of the way along.
    static Vector3 MoonPath(Vector3 start, Vector3 bend, Vector3 end, float u)
    {
        float v = 1f - u;
        return v * v * start + 2f * v * u * bend + u * u * end;
    }

    // Dots of light along both moons' paths to the ronin, following him, so the curve they will take reads before
    // they fly.
    IEnumerator TraceMoonPaths(Vector3[] starts, Vector3[] bends, float seconds)
    {
        const int Dots = 12;
        var dots = new List<SpriteRenderer>();
        for (int i = 0; i < Dots * 2; i++)
        {
            SpriteRenderer dot = Glowing("MoonPath", ProceduralSprites.Glow, 53);
            dot.transform.localScale = Vector3.one * 0.35f;
            dots.Add(dot);
        }
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = Mathf.Clamp01(t / seconds);
            for (int side = 0; side < 2; side++)
            {
                for (int d = 0; d < Dots; d++)
                {
                    // The dots run along the path toward him, so it reads as the way they will come.
                    float u = Mathf.Repeat((d + t * 6f) / Dots, 1f);
                    SpriteRenderer dot = dots[side * Dots + d];
                    if (dot == null) continue;
                    dot.transform.position = MoonPath(starts[side], bends[side], ronin.Bounds.center, u) + Vector3.back * 0.2f;
                    dot.color = new Color(Frost.r, Frost.g, Frost.b, (0.15f + 0.45f * k) * Mathf.Sin(Mathf.PI * u));
                }
            }
            yield return null;
        }
        foreach (SpriteRenderer dot in dots) Drop(dot);
    }

    // Flies a crossing moon along its sickle curve onto the ronin, the curve's end following him so it can't be
    // stepped out of, trailing pale afterimages; once there it flies on straight.
    IEnumerator Close(Arrow moon, Vector3 start, Vector3 bend, float seconds)
    {
        float nextGhost = 0f;
        for (float t = 0; moon != null && ronin != null && t < seconds; t += Time.fixedDeltaTime)
        {
            float u = Mathf.Clamp01((t + Time.fixedDeltaTime) / seconds);
            Vector3 next = MoonPath(start, bend, ronin.Bounds.center, u);
            Vector3 step = next - moon.transform.position;
            step.z = 0;
            if (step.sqrMagnitude > 0.0001f) moon.Turn(step / Time.fixedDeltaTime);
            if (Time.time >= nextGhost)
            {
                nextGhost = Time.time + 0.04f;
                Afterimage(moon.Art, new Color(Frost.r, Frost.g, Frost.b, 0.45f));
            }
            yield return new WaitForFixedUpdate();
        }
    }

    // Fang of the Oath: Kuon howls and she crosses her blades, and their spirit leaves as one bolt in the shape of a
    // wolf's head that turns with the ronin. Parried, it goes back at Kuon, a heavy blow that dazes it. Cast while Kuon
    // is busy with a move of its own, or once it has fallen, she casts it alone, and parried it breaks her stance.
    IEnumerator OathFang()
    {
        Speak(BossLine.OathFang);
        // Kuon joins in only if she could call it from its hunt.
        bool together = borrowed;
        if (together)
        {
            yield return wolf.Settle(wolf.Home);
            wolf.PlayHowl();
            wolf.Glare(DrawAim);
            Sfx.Play(Sfx.WolfHowl, 0.8f, 1.1f);
        }
        PlayAction(art.parry);
        Vector3 Source() => together ? Vector3.Lerp(Hands, WolfMouth, 0.5f) : Hands;
        yield return LockOn(Source(), DrawAim, Cyan);

        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.6f);
        Arrow fang = LooseFang(Source(), together ? wolf.transform : transform);

        // Kuon holds its ground at her side until the fang has struck and anything parried back has reached it.
        if (together)
        {
            for (float t = 0; fang != null && t < 3f; t += Time.deltaTime) yield return null;
            yield return Wait(1.4f);
        }
        else yield return Wait(0.5f);
    }

    // The Fang itself: a great spirit wolf lunging at the ronin from `from`, bright as their oath and trailing ghosts
    // of itself, steered onto him so it can only be parried; parried, it flies home to `home`.
    Arrow LooseFang(Vector3 from, Transform home)
    {
        Fx.Play(Fx.Impact, from, Cyan, 2.4f);
        Sfx.Play(Sfx.WolfGrowl, 1f, 0.9f);
        CameraShake.Add(0.3f);
        Arrow fang = Fire(from, (ronin.Bounds.center - from).normalized * DrawSpeed);
        WolfShot(fang, 2.2f, Color.white, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
        fang.MakeUnbreakable();
        fang.MakeReflectable(home, setPiece: true);
        StartCoroutine(Pursue(fang, DrawSpeed * ShotSpeed));
        return fang;
    }

    // Kuon's own Fang, once she is down and it fights enraged: the oath bolt from its jaws alone, behind the same
    // lock-on. Parried, it goes home to Kuon, a heavy blow that dazes it.
    IEnumerator WolfFang()
    {
        yield return wolf.Settle(wolf.Home);
        wolf.Hold();
        wolf.PlayHowl();
        wolf.Glare(DrawAim + 0.3f);
        Sfx.Play(Sfx.WolfHowl, 0.9f, 1.2f);
        yield return LockOn(WolfMouth, DrawAim, Cyan);
        Arrow fang = LooseFang(WolfMouth, wolf.transform);
        // It holds its ground until the fang has struck, so a parry sent home finds it.
        for (float t = 0; fang != null && t < 3f; t += Time.deltaTime) yield return null;
        yield return WolfWait(1f);
        wolf.Release();
    }

    // A lock-on reticle that settles over the ronin from wide, following him, turning and brightening with a
    // quickening ring, then flashes white and holds still a moment before the draw: the tell for a blow that must be
    // parried. A spark at `from` shows where the blow will come from.
    IEnumerator LockOn(Vector3 from, float seconds, Color colour)
    {
        ElementFx.Glow(from, colour, 0.9f, 3f, seconds, transform);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.8f);
        Sprite[] frames = Fx.Frames(Fx.ShiroLockOn);
        SpriteRenderer sight = Glowing("LockOn", frames != null ? frames[0] : ProceduralSprites.Glow, 58);
        if (frames != null) sight.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 12f);
        // The reticle art is this many world units across at scale 1 (64 px at 48 px a unit).
        float art = frames != null ? 64f / 48f : 1f;
        float rang = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            sight.transform.position = ronin.Bounds.center + Vector3.back * 0.3f;
            sight.transform.localScale = Vector3.one * Mathf.Lerp(4.5f, 2.2f, Mathf.SmoothStep(0, 1, k)) / art;
            sight.transform.rotation = Quaternion.Euler(0, 0, -90f * k);
            sight.color = new Color(colour.r, colour.g, colour.b, 0.35f + 0.6f * k);
            if (k > 0.4f && t - rang > Mathf.Lerp(0.2f, 0.08f, k))
            {
                rang = t;
                Sfx.Play(Sfx.BladeRing, 0.25f, 1.2f + k);
            }
            yield return null;
        }
        // The lock: it snaps tight and flashes white; this is the beat to parry on.
        sight.transform.localScale = Vector3.one * 1.9f / art;
        sight.color = Color.white;
        Fx.Play(Fx.Impact, ronin.Bounds.center, Color.white, 0.9f);
        Sfx.Play(Sfx.BladeRing, 0.6f, 2f);
        yield return Wait(0.18f);
        Drop(sight);
    }

    // Moonfall, her ultimate once Kuon has fallen (or while it's too busy for the Eclipse Fang): under the spirit moon
    // three waves of ice pillars ripple down across eight lanes, four enraged, each leaving a pair empty, and each
    // wave's pair one lane over from the last so one lane always carries through. Enraged, each wave also brings down
    // a glacier spire well clear of the gap. Then she stands spent, open to double damage.
    IEnumerator Moonfall()
    {
        Speak(BossLine.BladeRain);
        ultimate = true;
        yield return Glide(CameraShake.Center.x);
        SpriteRenderer moon = Moon(anchor + Vector3.up * 1.2f);
        Sfx.Play(Sfx.MistSwirl, 1f, 0.6f);
        CameraShake.Add(0.3f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            moon.color = new Color(0.6f, 0.9f, 1f, 0.55f * t / 0.8f);
            yield return null;
        }

        List<float> lanes = Lanes(8);
        int gap = Random.Range(0, lanes.Count - 1);
        float hang = PillarHang;
        int waves = enraged ? 4 : 3;
        for (int wave = 0; wave < waves; wave++)
        {
            if (wave > 0)
            {
                int step = Random.value < 0.5f ? -1 : 1;
                if (gap + step < 0 || gap + step > lanes.Count - 2) step = -step;
                gap += step;
            }
            float warning = wave == 0 ? 0.8f : 0.6f;
            int spire = -1;
            if (enraged)
            {
                var away = new List<int>();
                for (int i = 0; i < lanes.Count; i++)
                {
                    if (i < gap - 1 || i > gap + 2) away.Add(i);
                }
                if (away.Count > 0) spire = away[Random.Range(0, away.Count)];
            }
            var tells = new List<SpriteRenderer>();
            var pillars = new List<SpriteRenderer>();
            var falling = new List<float>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (i == gap || i == gap + 1) continue;
                if (i == spire)
                {
                    StartCoroutine(Spire(lanes[i], warning));
                    continue;
                }
                falling.Add(lanes[i]);
                tells.Add(LaneWarning(lanes[i]));
                pillars.Add(PillarPicture(new Vector3(lanes[i], hang, 0)));
            }
            PlayAction(art.parry);
            Sfx.Play(Sfx.BladeRing, 0.7f, 0.9f + wave * 0.1f);
            yield return Brighten(tells, warning);
            for (int i = 0; i < falling.Count; i++)
            {
                Drop(pillars[i]);
                IcePillar(new Vector3(falling[i], hang, 0), 13f);
            }
            RemoveTells(tells);
            Sfx.Play(Sfx.GhostSwordFall, 1f, 0.8f);
            yield return Wait(0.35f);
        }
        transient.Remove(moon.gameObject);
        Fader.Fade(moon, 0.8f);
        ultimate = false;

        exposed = true;
        PlayIdle();
        yield return Wait(2f);
        exposed = false;
    }

    // Glacier, from her second phase: she raises her blades and two glacier spires, far too big to parry, come down
    // on the ronin's lane and one beside it under long dark warnings. Enraged, a third follows him to wherever he
    // stepped.
    IEnumerator Glacier()
    {
        PlayAction(art.parry);
        ElementFx.Glow(Hands, Frost, 0.9f, 3f, 0.8f, transform);
        Sfx.Play(Sfx.BladeRing, 0.8f, 0.7f);
        float tell = enraged ? 0.95f : 1.15f;
        float lo = LeftEdge + 2f, hi = RightEdge - 2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        float beside = x < CameraShake.Center.x ? x + 4.2f : x - 4.2f;
        StartCoroutine(Spire(x, tell));
        StartCoroutine(Spire(Mathf.Clamp(beside, lo, hi), tell));
        yield return Wait(tell + 0.3f);
        if (enraged) yield return Spire(Mathf.Clamp(ronin.Bounds.center.x, lo, hi), 0.8f);
        yield return Wait(0.4f);
    }

    // --- Phase 2: the wolf ---
    // Kuon's own moves (charge, howl, pack, lances, Maw) run in Hunt on its own clock, so they leave her to hers and
    // carry on through her stagger. Each has a harder version for when it fights enraged.

    // Where Kuon perches over a lane: on her row, clear of the HUD (lower while she kneels, so it stays by her).
    float WolfRowY => anchor.y + (Down ? 0.55f : 0.3f);

    // A spot on her row for Kuon to stand: its own ground, well apart from hers.
    public Vector3 ProwlSpot()
    {
        float range = HalfWidth - 2.5f;
        float centre = CameraShake.Center.x;
        float y = WolfRowY - 0.1f;
        for (int i = 0; i < 8; i++)
        {
            float x = centre + Random.Range(-range, range);
            if (Down || Mathf.Abs(x - anchor.x) >= 3.5f) return new Vector3(x, y, 0);
        }
        return new Vector3(Mathf.Clamp(centre * 2f - anchor.x, centre - range, centre + range), y, 0);
    }

    // Spirit Wolf Charge: the wolf melts into mist and reappears over the ronin's lane, growling, a warning line
    // under it; then it comes down the lane like a bolt and dissolves where it lands, and returns to its ground.
    // Enraged, it perches again at once over wherever he went and comes down a second time.
    IEnumerator WolfCharge()
    {
        Command(BossLine.WolfCharge, 0.5f);
        yield return wolf.Vanish();
        yield return WolfWait(0.25f);
        int charges = wolf.Enraged ? 2 : 1;
        for (int c = 0; c < charges; c++)
        {
            float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.2f, RightEdge - 1.2f);
            Vector3 perch = new Vector3(x, WolfRowY, 0);
            yield return wolf.AppearAt(perch, true);
            Sfx.Play(Sfx.WolfGrowl, 0.9f, c == 0 ? 1f : 1.2f);
            float tell = c == 0 ? 0.9f : 0.6f;
            BeamTell lane = MarkBeam(x, ChargeHalfWidth, tell, () => perch);
            yield return WolfWait(tell);
            lane.Remove();

            wolf.Hide();
            Charge(perch, wolf.Enraged ? 16f : 14f, 1.6f);
            Puff(perch, 1.6f);
            Sfx.Play(Sfx.MistSwirl, 0.7f, 0.8f);
            yield return WolfWait(c < charges - 1 ? 0.35f : 0.5f);
        }
        yield return wolf.ReturnHome();
    }

    // Howl of the Departed: the wolf's eyes and jaws burn bright, then its howl sends seven orbs down in a wide
    // fan, and a second wave a beat later between the first's paths. Low on health a third wave follows; enraged,
    // three waves of nine.
    IEnumerator Howl()
    {
        Command(BossLine.Howl, 0.5f);
        yield return wolf.Settle(wolf.Home);
        wolf.Hold();
        wolf.Glare(0.8f);
        ElementFx.Glow(WolfMouth, Cyan, 0.6f, 2.4f, 0.8f, wolf.transform);
        yield return WolfWait(wolf.Enraged ? 0.6f : 0.8f);

        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 1f, 1f);
        CameraShake.Add(0.15f);
        int waves = wolf.Enraged || wolf.HealthFraction < 0.3f ? 3 : 2;
        int orbs = wolf.Enraged ? 9 : 7;
        for (int wave = 0; wave < waves; wave++)
        {
            if (wave > 0) yield return WolfWait(wolf.Enraged ? 0.45f : 0.6f);
            OrbFan(orbs, wolf.Enraged ? 66f : 60f, 4.5f, wave % 2 == 1);
        }
        yield return WolfWait(0.3f);
        wolf.Release();
    }

    // One fan of orbs from the wolf's jaws, aimed straight down; an offset fan fills the last one's gaps.
    void OrbFan(int count, float spread, float speed, bool offset)
    {
        float step = 2f * spread / (count - 1f);
        var fan = new List<Arrow>();
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-spread, spread, i / (count - 1f)) + (offset ? step / 2f : 0f);
            fan.Add(Orb(WolfMouth, Quaternion.Euler(0, 0, angle) * Vector3.down * speed));
        }
        IgnoreEachOther(fan);
    }

    // Twin Spirit Assault: she takes the left of the field and the wolf the right; both glow, then her crescents
    // sweep down and to the right while its orbs sweep down and to the left, crossing in the middle with gaps.
    IEnumerator TwinAssault()
    {
        if (Random.value < 0.6f) Speak(BossLine.TwinAssault);
        float quarter = HalfWidth * 0.5f;
        bool herLeft = anchor.x <= CameraShake.Center.x;
        float herX = CameraShake.Center.x + (herLeft ? -quarter : quarter);
        Vector3 wolfSpot = new Vector3(CameraShake.Center.x + (herLeft ? quarter : -quarter), anchor.y + 0.2f, 0);
        yield return wolf.Vanish();
        yield return Glide(herX);
        yield return wolf.AppearAt(wolfSpot, true);

        ElementFx.Glow(Hands, Cyan, 0.8f, 2.6f, 0.6f, transform);
        wolf.Glare(0.6f);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.2f);
        yield return Wait(0.6f);

        PlayAction(art.attack);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 0.7f, 1.2f);
        // Positive angles turn a downward shot to the right, negative to the left.
        float inward = herLeft ? 1f : -1f;
        WolfSlash(Hands, Quaternion.Euler(0, 0, 40f * inward) * Vector3.down, 2.6f);
        var volley = new List<Arrow>();
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(20f, 60f, i / 4f);
            volley.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle * inward) * Vector3.down * 7.5f));
            volley.Add(Orb(WolfMouth, Quaternion.Euler(0, 0, -angle * inward) * Vector3.down * 6.5f));
            IgnoreEachOther(volley);
            yield return Wait(0.15f);
        }
        yield return Wait(0.3f);
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    // Phantom Wolf Pack: three smaller ghost wolves form over three of six lanes, each with its warning line,
    // and charge down one after another. Never the same three lanes twice running. Enraged, four pups over seven
    // lanes, quicker.
    IEnumerator WolfPack()
    {
        Command(BossLine.WolfPack, 0.5f);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 0.6f, 1.4f);
        List<float> lanes = Lanes(wolf.Enraged ? 7 : 6);
        int count = wolf.Enraged ? 4 : 3;
        var picks = new List<int>();
        while (picks.Count < count)
        {
            int lane = Random.Range(0, lanes.Count);
            if (!picks.Contains(lane) && !(lastPackLanes.Contains(lane) && Random.value < 0.7f)) picks.Add(lane);
        }
        lastPackLanes.Clear();
        foreach (int lane in picks) lastPackLanes.Add(lane);

        float perchY = WolfRowY;
        float tell = wolf.Enraged ? 0.8f : 0.9f;
        var tells = new List<BeamTell>();
        var pups = new List<SpriteRenderer>();
        foreach (int lane in picks)
        {
            Vector3 perch = new Vector3(lanes[lane], perchY, 0);
            tells.Add(MarkBeam(lanes[lane], PupHalfWidth, tell, () => perch));
            pups.Add(wolf.Silhouette(perch, 0.6f, wolfTransient));
            Puff(perch, 1.2f);
        }
        yield return WolfWait(tell);

        for (int i = 0; i < picks.Count; i++)
        {
            if (pups[i] != null)
            {
                wolfTransient.Remove(pups[i].gameObject);
                Destroy(pups[i].gameObject);
            }
            tells[i].Remove();
            Charge(new Vector3(lanes[picks[i]], perchY, 0), wolf.Enraged ? 13f : 12f, 1.1f);
            Sfx.Play(Sfx.WolfGrowl, 0.6f, 1.3f);
            yield return WolfWait(wolf.Enraged ? 0.25f : 0.35f);
        }
        yield return WolfWait(0.3f);
    }

    // Eclipse Fang, their strongest attack together: she takes the centre, the wolf looms up huge behind her under
    // a spectral blue moon; two crescent waves, then the wolf's beam straight down a marked lane, then a last
    // spread of swords. Afterwards she stands spent for two seconds, open to double damage.
    IEnumerator EclipseFang()
    {
        Speak(BossLine.EclipseFang);
        ultimate = true;
        yield return wolf.Vanish();
        yield return Glide(CameraShake.Center.x);
        yield return wolf.AppearAt(anchor + new Vector3(0f, 0.9f, 0f), true);
        SpriteRenderer moon = Moon(anchor + Vector3.up * 1.2f);
        wolf.StartCoroutine(wolf.Grow(1.8f, 0.8f));
        Sfx.Play(Sfx.WolfHowl, 1f, 0.8f);
        CameraShake.Add(0.3f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            moon.color = new Color(0.6f, 0.9f, 1f, 0.55f * t / 0.8f);
            yield return null;
        }

        // Two crescent waves, the second's blades between the first's, so the safe spot moves.
        for (int wave = 0; wave < 2; wave++)
        {
            PlayAction(art.attack);
            Sfx.Play(Sfx.SwordSlash, 0.9f, 0.65f);
            WolfSlash(Hands, Vector3.down, 3f);
            var fan = new List<Arrow>();
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(-44f, 44f, i / 4f) + (wave == 1 ? 11f : 0f);
                fan.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle) * Vector3.down * 7.5f));
            }
            IgnoreEachOther(fan);
            yield return Wait(0.8f);
        }

        // The beam: the lane is marked where the ronin stands as Kuon starts to gather it, so stepping off it is the
        // dodge. Kuon draws the orb in and swallows it as in its own Maw, then looses the beam.
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.2f, RightEdge - 1.2f);
        BeamTell warning = MarkBeam(x, 1.1f, 1.3f);
        wolf.Glare(1.4f);
        yield return GatherOrb(() => WolfMouth, 0.8f, () => false);
        yield return SwallowOrb(0.2f);
        warning.Remove();
        yield return MawBeam(x, 1.1f, 0.5f);

        // The closing spread of swords, then she is spent.
        PlayAction(art.parry);
        Sfx.Play(Sfx.GhostSwordFall, 1f, 0.8f);
        var swords = new List<Arrow>();
        for (int i = 0; i < 7; i++)
        {
            float angle = Mathf.Lerp(-45f, 45f, i / 6f);
            swords.Add(GhostSword(Hands, Quaternion.Euler(0, 0, angle) * Vector3.down * 9f));
        }
        IgnoreEachOther(swords);
        wolf.StartCoroutine(wolf.Grow(1f, 0.6f));
        transient.Remove(moon.gameObject);
        Fader.Fade(moon, 0.8f);
        ultimate = false;

        exposed = true;
        PlayIdle();
        yield return Wait(2f);
        exposed = false;
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    // --- Kuon's Maw ---

    // Kuon's own ultimate, drawn like a tailed beast's bomb. It plants itself over the field and rears back, and motes
    // of spirit light, pale and deep, stream in from all round and spiral into an orb at its jaws that swells and
    // shakes the ground. The lane locks where the ronin stands as the orb is crushed to a white-hot point and
    // swallowed; a beat of silence, then the beam comes down that lane. Hit Kuon hard enough while it gathers and
    // the orb bursts in its own jaws. Enraged, the beam is wider and drags after him.
    IEnumerator SpiritMaw()
    {
        yield return wolf.Vanish();
        // It stands on her row, clear of the HUD, over the ronin's side of the field.
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 2f, RightEdge - 2f);
        yield return wolf.AppearAt(new Vector3(x, WolfRowY - 0.1f, 0), true);
        yield return MawStrike(MawGather, wolf.Enraged ? 1.8f : 1.4f, wolf.Enraged ? 1f : 0.8f, wolf.Enraged ? 2.2f : 0f);
        if (wolf.IsBroken) yield break;
        yield return WolfWait(0.4f);
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    // The Maw from where Kuon stands: the gather (which a hard enough beating bursts in its jaws), the lane locked on
    // the ronin, the swallow and the beam, `halfWidth` wide for `seconds`, dragging after him at `sweep`.
    IEnumerator MawStrike(float gather, float halfWidth, float seconds, float sweep)
    {
        wolf.PlayMaw(gather + 0.3f);
        wolf.Glare(gather + 0.2f);
        Sfx.Play(Sfx.WolfGrowl, 1f, 0.7f);
        wolf.StartCoroutine(wolf.Grow(1.3f, 0.5f));
        int before = wolf.Health;
        yield return GatherOrb(() => WolfMouth, gather, () => wolf.IsBroken || before - wolf.Health >= MawBreakDamage);
        if (orbBurst)
        {
            if (!wolf.IsBroken)
            {
                wolf.Backfire(MawBackfireDamage);
                wolf.StartCoroutine(wolf.Grow(1f, 0.4f));
            }
            CameraShake.Add(0.5f);
            Sfx.Play(Sfx.FireBlast, 0.9f, 0.8f);
            Sfx.Play(Sfx.WolfHowl, 0.8f, 1.6f);
            while (wolf.Dazed) yield return null;
            yield break;
        }

        float lane = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.4f, RightEdge - 1.4f);
        BeamTell warning = MarkBeam(lane, halfWidth, 0.8f);
        yield return SwallowOrb(0.5f);
        warning.Remove();
        yield return MawBeam(lane, halfWidth, seconds, sweep: sweep);
        Fx.Play(Fx.Explosion, new Vector3(beamX, GroundY, 0), Cyan, 3f);
        CameraShake.Add(0.4f);
        wolf.PlayIdle();
        wolf.StartCoroutine(wolf.Grow(1f, 0.4f));
    }

    // Spirit Lances: Kuon's quick beams. It stays on its ground and spits thin beams one after another, each down the
    // lane where the ronin stands as its warning opens, so standing still is the one way to be hit; four near its
    // end, five enraged, with shorter warnings. Each is the Maw's beam in small: a short trapezoid warning, a burst
    // at the jaws and a splash.
    IEnumerator SpiritLances()
    {
        yield return wolf.Settle(wolf.Home);
        wolf.Hold();
        wolf.Glare(0.5f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 1.3f);
        int lances = wolf.Enraged ? 5 : wolf.HealthFraction < 0.5f ? 4 : 3;
        float tell = wolf.Enraged ? 0.4f : LanceTell;
        for (int i = 0; i < lances; i++)
        {
            float lane = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1f, RightEdge - 1f);
            BeamTell warning = MarkBeam(lane, LanceHalfWidth, tell);
            ElementFx.Glow(WolfMouth, Cyan, 0.5f, 1.8f, tell, wolf.transform);
            yield return WolfWait(tell);
            warning.Remove();
            wolf.PlayHowl();
            yield return MawBeam(lane, LanceHalfWidth, LanceBurn, heavy: false);
            yield return WolfWait(0.12f);
        }
        wolf.Release();
    }

    // A lance's half width, how long its warning opens, and how long it burns.
    const float LanceHalfWidth = 0.7f;
    const float LanceTell = 0.5f;
    const float LanceBurn = 0.25f;

    // Frozen Howl, Kuon's ultimate once she is down and it fights for both of them: it takes the middle of the field
    // and gathers a longer Maw while two glacier spires come down on the ronin one after the other, then the beam
    // comes down his lane, wide, dragging after him. Afterwards it stands spent and reeling for two seconds, open to
    // double damage.
    IEnumerator FrozenHowl()
    {
        yield return wolf.Vanish();
        yield return wolf.AppearAt(new Vector3(CameraShake.Center.x, WolfRowY - 0.1f, 0), true);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 1f, 0.75f);
        CameraShake.Add(0.3f);
        yield return WolfWait(0.4f);
        StartCoroutine(SpireVolley(2, 0.9f, 0.8f));
        yield return MawStrike(FrozenGather, 2f, 1.2f, 2.6f);
        if (wolf.IsBroken) yield break;
        // Spent: it reels where it stands.
        wolf.Daze(2f);
        yield return WolfWait(2f);
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    const float FrozenGather = 2.2f;

    // Kuon's spires: `count` of them on wherever the ronin stands, `gap` seconds apart.
    IEnumerator SpireVolley(int count, float tell, float gap)
    {
        for (int i = 0; i < count && WolfFights; i++)
        {
            StartCoroutine(Spire(Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 2f, RightEdge - 2f), tell, wolfs: true));
            yield return WolfWait(gap);
        }
    }

    // How long Kuon's Maw gathers, how much damage on Kuon meanwhile bursts the orb, and what the burst costs it.
    const float MawGather = 1.6f;
    const int MawBreakDamage = 6;
    const int MawBackfireDamage = 10;

    // The orb at Kuon's jaws while it gathers (the pixel orb, or a code glow without it), the bloom behind it and,
    // drawn in code only, its white core; null when none is out.
    SpriteRenderer orb, orbHalo, orbCore;
    // Set when the gather was cut short by burst() returning true, the orb bursting where it hung.
    bool orbBurst;
    // The dark laid over the arena while Kuon gathers, so the orb and the beam blaze against it.
    SpriteRenderer dimmer;

    // The orb's pixel art is this many world units across at scale 1 (64 px at 48 px a unit).
    const float OrbArtSize = 64f / 48f;

    // Like a tailed beast's bomb: the arena darkens, streaks of light and shadow spiral in from all round to an
    // orb at `at` that swells and churns as they arrive, and the ground shakes harder as it grows.
    IEnumerator GatherOrb(System.Func<Vector3> at, float seconds, System.Func<bool> burst)
    {
        orbBurst = false;
        RemoveOrb();
        Dim(0.4f, 0.4f);
        orbHalo = Glowing("OrbHalo", ProceduralSprites.SoftGlow, 56, wolfs: true);
        Sprite[] frames = Fx.Frames(Fx.ShiroMawOrb);
        orb = Glowing("SpiritOrb", frames != null ? frames[0] : ProceduralSprites.SoftGlow, 58, wolfs: true);
        if (frames != null) orb.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 14f);
        else orbCore = Glowing("OrbCore", ProceduralSprites.Glow, 59, wolfs: true);

        const int count = 36;
        var motes = new SpriteRenderer[count];
        var offsets = new Vector3[count];
        var delays = new float[count];
        var spins = new float[count];
        for (int i = 0; i < count; i++)
        {
            // Streaks, pale and deep in turn: light and shadow drawn in together.
            motes[i] = Glowing("Mote", ProceduralSprites.White, 59, wolfs: true);
            motes[i].color = i % 2 == 0 ? new Color(Frost.r, Frost.g, Frost.b, 0f) : new Color(0.2f, 0.4f, 0.95f, 0f);
            offsets[i] = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
            delays[i] = Random.Range(0f, seconds * 0.65f);
            spins[i] = Random.Range(140f, 260f) * (Random.value < 0.5f ? -1f : 1f);
        }
        Sfx.Play(Sfx.LanternCharge, 0.9f, 0.8f);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 0.5f);
        float nextRumble = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            Vector3 centre = at() + Vector3.back * 0.4f;
            for (int i = 0; i < count; i++)
            {
                float m = Mathf.Clamp01((t - delays[i]) / (seconds * 0.35f));
                Color c = motes[i].color;
                c.a = m > 0f && m < 1f ? 0.95f : 0f;
                motes[i].color = c;
                if (c.a == 0f) continue;
                // Each streak spirals in, quickening and stretching as it nears the orb.
                Vector3 offset = Quaternion.Euler(0, 0, spins[i] * m) * offsets[i] * (1f - m * m);
                motes[i].transform.position = centre + offset;
                motes[i].transform.rotation = Quaternion.FromToRotation(Vector3.right, -offset);
                motes[i].transform.localScale = new Vector3(0.25f + 0.9f * m, 0.09f, 1f);
            }
            float size = Mathf.Lerp(0.5f, 3.6f, Mathf.SmoothStep(0, 1, k)) * (1f + 0.06f * Mathf.Sin(t * 40f));
            orbHalo.transform.position = centre + Vector3.forward * 0.05f;
            orbHalo.transform.localScale = Vector3.one * size * 2.8f;
            orbHalo.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.2f + 0.25f * k);
            orb.transform.position = centre;
            if (frames != null)
            {
                orb.transform.localScale = Vector3.one * size / OrbArtSize;
                orb.color = Color.white;
            }
            else
            {
                orb.transform.localScale = Vector3.one * size * 1.6f;
                orb.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f);
                orbCore.transform.position = centre + Vector3.back * 0.05f;
                orbCore.transform.localScale = Vector3.one * size * 0.7f;
                orbCore.color = new Color(0.9f, 1f, 1f, 0.9f);
            }
            if (t >= nextRumble)
            {
                nextRumble = t + 0.12f;
                CameraShake.Add(0.03f + 0.06f * k);
            }
            if (burst())
            {
                orbBurst = true;
                break;
            }
            yield return null;
        }
        foreach (SpriteRenderer mote in motes) Drop(mote);
        if (orbBurst)
        {
            Vector3 where = orb.transform.position;
            if (Fx.Play(Fx.ShiroMawBurst, where, Color.white, 2.4f, null, 58) == null) Fx.Play(Fx.Explosion, where, Cyan, 2.4f);
            Fx.Play(Fx.Explosion, where, Cyan, 2f);
            RemoveOrb();
            Dim(0f, 0.5f);
        }
    }

    // The orb crushes down to a white-hot point and is swallowed, and for a beat everything goes quiet.
    IEnumerator SwallowOrb(float beat)
    {
        if (orb == null) yield break;
        Vector3 at = orb.transform.position;
        Vector3 from = orb.transform.localScale;
        float halo = orbHalo.transform.localScale.x;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            // It bulges once before it gives, then crushes down fast.
            float squeeze = k < 0.25f ? 1f + 0.15f * (k / 0.25f) : Mathf.Lerp(1.15f, 0.12f, Mathf.SmoothStep(0, 1, (k - 0.25f) / 0.75f));
            orb.transform.localScale = from * squeeze;
            orbHalo.transform.localScale = Vector3.one * halo * squeeze;
            orbHalo.color = new Color(Frost.r, Frost.g, Frost.b, 0.45f + 0.4f * k);
            if (orbCore != null) orbCore.transform.localScale = Vector3.one * from.x * 0.45f * squeeze;
            yield return null;
        }
        RemoveOrb();
        Fx.Play(Fx.Impact, at, Color.white, 1.8f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 0.6f);
        wolf.Glare(beat + 0.3f);
        wolf.StartCoroutine(wolf.Gulp());
        // The held breath: the arena goes darker still for the beat before the beam.
        Dim(0.55f, beat);
        yield return WolfWait(beat);
    }

    // The beam segment's pixel art is this many world units across at scale 1 (34 px at 48 px a unit).
    const float BeamArtWidth = 34f / 48f;

    // The beam from Kuon's jaws down a lane; it burns the ronin while he stands within halfWidth of it. It bursts
    // from the jaws with a shockwave, snaps open wide, burns pulsing with spirit light streaming down it and
    // splashing where it lands, then pinches shut and the arena's dark lifts. With a sweep its foot drags after him
    // at that speed, slower than he runs.
    // A light one (Kuon's quick lances) leaves the arena lit and shakes it less.
    IEnumerator MawBeam(float x, float halfWidth, float seconds, bool heavy = true, float sweep = 0f)
    {
        Vector3 from = WolfMouth;
        Vector3 to = new Vector3(x, GroundY, 0);
        float w = halfWidth / 1.1f;
        Sprite[] frames = Fx.Frames(Fx.ShiroMawBeam);

        if (Fx.Play(Fx.ShiroMawBurst, from, Color.white, 2.6f * w, null, 58) == null) Fx.Play(Fx.Explosion, from, Cyan, 2.4f * w);
        SpriteRenderer glow = Bar(from, to, 3.4f * w, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f), 53);
        glow.sprite = ProceduralSprites.Glow;
        WolfOwns(glow.gameObject);
        SpriteRenderer body = null, core = null, beam = null;
        float beamScale = 2.7f * w / BeamArtWidth;
        if (frames != null)
        {
            beam = Glowing("MawBeam", frames[0], 55, wolfs: true);
            beam.color = Color.white;
            beam.drawMode = SpriteDrawMode.Tiled;
            beam.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 16f);
        }
        else
        {
            body = Bar(from, to, 1.1f * w, new Color(0.6f, 0.95f, 1f, 0.9f), 55);
            core = Bar(from, to, 0.35f * w, Color.white, 56);
            WolfOwns(body.gameObject);
            WolfOwns(core.gameObject);
        }
        FxPlayer flare = Fx.Play(Fx.ShiroBeamFlare, from, Color.white, 2f * w, wolf.transform, 57);
        if (flare != null) wolfTransient.Add(flare.gameObject);
        FxPlayer splash = Fx.Play(Fx.ShiroMawHit, to, Color.white, 2.4f * w, null, 57);
        if (splash != null) wolfTransient.Add(splash.gameObject);
        Sfx.PlayLoop(Sfx.SpiritBeam, heavy ? 0.9f : 0.6f, heavy ? 0.9f : 1.3f);
        if (heavy)
        {
            Sfx.Play(Sfx.FireBlast, 0.8f, 0.7f);
            CameraShake.Add(0.6f * w);
            Dim(0.45f, 0.1f);
        }
        else CameraShake.Add(0.15f);

        float nextSpark = 0f, nextRumble = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            if (sweep > 0f) x = Mathf.MoveTowards(x, Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1f, RightEdge - 1f), sweep * Time.deltaTime);
            to = new Vector3(x, GroundY, 0);
            Vector3 delta = to - from;
            // Snaps open past full width, settles, pulses, and pinches shut over its last moment.
            float open = t < 0.1f ? Mathf.Lerp(0.2f, 1.25f, t / 0.1f)
                : t < 0.2f ? Mathf.Lerp(1.25f, 1f, (t - 0.1f) / 0.1f)
                : 1f + 0.06f * Mathf.Sin(Time.time * 45f);
            open *= Mathf.Clamp01((seconds - t) / Mathf.Min(0.15f, seconds * 0.4f));
            if (beam != null)
            {
                beam.transform.position = (from + to) / 2f + Vector3.back * 0.2f;
                beam.transform.rotation = Quaternion.FromToRotation(Vector3.down, delta);
                beam.transform.localScale = new Vector3(beamScale * open, beamScale, 1f);
                beam.size = new Vector2(BeamArtWidth, delta.magnitude / beamScale);
            }
            Stretch(body, from, to, 1.1f * w * open);
            Stretch(core, from, to, 0.35f * w * open);
            Stretch(glow, from, to, 3.4f * w * open);
            if (splash != null) splash.transform.position = to;
            if (Mathf.Abs(ronin.Bounds.center.x - x) < halfWidth) ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
            if (Time.time >= nextSpark)
            {
                // Sparks thrown off the beam's edges along its length.
                nextSpark = Time.time + 0.06f;
                Vector3 along = Vector3.Lerp(from, to, Random.value);
                Vector3 side = Vector3.Cross(delta.normalized, Vector3.forward) * (halfWidth * 0.9f * (Random.value < 0.5f ? -1f : 1f));
                Fx.Play(Fx.Impact, along + side, Cyan, 0.5f * w);
            }
            if (heavy && Time.time >= nextRumble)
            {
                nextRumble = Time.time + 0.1f;
                CameraShake.Add(0.08f * w);
            }
            yield return null;
        }
        beamX = x;
        Sfx.Stop(Sfx.SpiritBeam);
        foreach (SpriteRenderer part in new[] { glow, body, core, beam }) Drop(part);
        if (flare != null) flare.Stop();
        if (splash != null) splash.Stop();
        if (heavy) Dim(0f, 0.6f);
    }

    // Where the last beam ended, after any sweep.
    float beamX;

    // The warning for a Maw beam: a trapezoid from Kuon's jaws, as wide there as the beam, to where the beam will
    // land, much wider there. Its outline shows at once; over three ringing beats its fill pours down from the jaws
    // to the ground, brightening on each, while the wide foot draws in until the shape is a rectangle, the beam's
    // own, flickering white as the beam fires into it. It follows the jaws as Kuon moves.
    class BeamTell
    {
        public GameObject shape;
        public Mesh mesh;
        public Coroutine opening;
        public Shirogane owner;

        public void Remove()
        {
            if (opening != null) owner.StopCoroutine(opening);
            if (shape == null) return;
            owner.wolfTransient.Remove(shape);
            UnityEngine.Object.Destroy(shape);
            UnityEngine.Object.Destroy(mesh);
        }
    }

    // The rectangle it ends as is a little wider than the beam's hit, so standing just outside it is safe; its foot
    // starts this many times as wide and shrinks to it.
    const float TellMargin = 1.15f;
    const float TellFoot = 2.2f;

    // From Kuon's jaws unless `from` says otherwise (a lane charge's perch, a pup's).
    BeamTell MarkBeam(float x, float halfWidth, float seconds, System.Func<Vector3> from = null)
    {
        var tell = new BeamTell { owner = this, shape = new GameObject("MawTell"), mesh = new Mesh() };
        tell.mesh.MarkDynamic();
        tell.shape.AddComponent<MeshFilter>().sharedMesh = tell.mesh;
        var renderer = tell.shape.AddComponent<MeshRenderer>();
        // Drawn with the sprites' own material, white, so the vertex colours give it its look.
        renderer.sharedMaterial = sprite.sharedMaterial;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", Texture2D.whiteTexture);
        renderer.SetPropertyBlock(block);
        renderer.sortingOrder = 52;
        wolfTransient.Add(tell.shape);
        tell.opening = StartCoroutine(OpenBeamTell(tell, x, halfWidth * TellMargin, seconds, from ?? (() => WolfMouth)));
        return tell;
    }

    IEnumerator OpenBeamTell(BeamTell tell, float x, float halfWidth, float seconds, System.Func<Vector3> source)
    {
        const float EdgeWidth = 0.12f;
        const int Quads = 5;
        float beatTime = seconds / 3f;
        int beats = 0;
        var vertices = new Vector3[Quads * 4];
        var colours = new Color[Quads * 4];
        var triangles = new int[Quads * 6];
        for (int q = 0; q < Quads; q++)
        {
            int v = q * 4, i = q * 6;
            triangles[i] = v; triangles[i + 1] = v + 1; triangles[i + 2] = v + 2;
            triangles[i + 3] = v; triangles[i + 4] = v + 2; triangles[i + 5] = v + 3;
        }
        for (float t = 0; tell.shape != null; t += Time.deltaTime)
        {
            if (t < seconds && t >= beats * beatTime)
            {
                beats++;
                Sfx.Play(Sfx.BladeRing, 0.45f, 1.5f + 0.2f * beats);
            }
            float filled = Mathf.Clamp01(t / seconds);
            float fill = 0.14f + 0.08f * beats;
            float edge = 0.4f + 0.2f * beats;
            // Full, it burns white and flickers: the beam is coming now.
            Color tint = filled >= 1f ? Color.Lerp(Cyan, Color.white, Mathf.PingPong(t * 12f, 1f)) : Cyan;

            Vector3 from = source();
            Vector3 to = new Vector3(x, GroundY, 0);
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.forward);
            // The jaws end is the beam's width throughout; the wide foot draws in over the beats until the shape is
            // a rectangle, the beam's own, as it fires.
            float top = halfWidth;
            float foot = halfWidth * Mathf.Lerp(TellFoot, 1f, Mathf.SmoothStep(0, 1, filled));
            Vector3 topLeft = from - side * top, topRight = from + side * top;
            Vector3 footLeft = to - side * foot, footRight = to + side * foot;
            // The fill pours down from the jaws, as far as it has got, widening as it goes.
            float pour = Mathf.SmoothStep(0, 1, filled);
            Vector3 frontLeft = Vector3.Lerp(topLeft, footLeft, pour), frontRight = Vector3.Lerp(topRight, footRight, pour);
            Quad(vertices, 0, topLeft, topRight, frontRight, frontLeft);
            // The outline: a strip down each side, one across the foot and one across the jaws, inside the shape.
            Vector3 inTop = side * Mathf.Min(EdgeWidth, top), inFoot = side * EdgeWidth;
            Vector3 down = (to - from).normalized * EdgeWidth;
            Quad(vertices, 4, topLeft, topLeft + inTop, footLeft + inFoot, footLeft);
            Quad(vertices, 8, topRight - inTop, topRight, footRight, footRight - inFoot);
            Quad(vertices, 12, footLeft - down, footRight - down, footRight, footLeft);
            Quad(vertices, 16, topLeft, topRight, topRight + down, topLeft + down);
            for (int i = 0; i < vertices.Length; i++)
            {
                Color c = tint;
                c.a = Mathf.Min(1f, i < 4 ? fill : edge);
                colours[i] = c;
            }
            tell.mesh.vertices = vertices;
            tell.mesh.colors = colours;
            if (tell.mesh.GetIndexCount(0) == 0) tell.mesh.triangles = triangles;
            tell.mesh.RecalculateBounds();
            yield return null;
        }
    }

    static void Quad(Vector3[] vertices, int at, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector3 back = Vector3.back * 0.2f;
        vertices[at] = a + back;
        vertices[at + 1] = b + back;
        vertices[at + 2] = c + back;
        vertices[at + 3] = d + back;
    }

    // Eases the dark over the arena to `alpha` over `seconds`; 0 lifts it and then removes it.
    void Dim(float alpha, float seconds)
    {
        if (dimmer == null)
        {
            if (alpha <= 0f) return;
            dimmer = new GameObject("MawDark").AddComponent<SpriteRenderer>();
            dimmer.sprite = ProceduralSprites.White;
            // Over the floor and its cover's shadows, under every character and shot.
            dimmer.sortingOrder = 1;
            dimmer.color = new Color(0.01f, 0.02f, 0.06f, 0f);
            dimmer.transform.position = new Vector3(CameraShake.Center.x, CameraShake.Center.y, 0f);
            float h = Camera.main.orthographicSize * 2f + 4f;
            dimmer.transform.localScale = new Vector3(h * Camera.main.aspect + 4f, h, 1f);
            wolfTransient.Add(dimmer.gameObject);
        }
        if (dimming != null) StopCoroutine(dimming);
        dimming = StartCoroutine(Dimming(alpha, seconds));
    }

    Coroutine dimming;

    IEnumerator Dimming(float alpha, float seconds)
    {
        float from = dimmer.color.a;
        for (float t = 0; t < seconds && dimmer != null; t += Time.deltaTime)
        {
            Color c = dimmer.color;
            c.a = Mathf.Lerp(from, alpha, t / seconds);
            dimmer.color = c;
            yield return null;
        }
        if (dimmer == null) yield break;
        if (alpha <= 0f) Drop(dimmer);
        else dimmer.color = new Color(dimmer.color.r, dimmer.color.g, dimmer.color.b, alpha);
        dimming = null;
    }

    // A code-drawn glow the current attack owns: hers, or Kuon's.
    SpriteRenderer Glowing(string name, Sprite shape, int order, bool wolfs = false)
    {
        var glow = new GameObject(name).AddComponent<SpriteRenderer>();
        glow.sprite = shape;
        glow.sortingOrder = order;
        glow.color = Color.clear;
        (wolfs ? wolfTransient : transient).Add(glow.gameObject);
        return glow;
    }

    void Drop(SpriteRenderer thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        wolfTransient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    void RemoveOrb()
    {
        Drop(orb);
        Drop(orbHalo);
        Drop(orbCore);
        orb = orbHalo = orbCore = null;
    }

    // The spectral moon that rises behind them for the Eclipse Fang.
    SpriteRenderer Moon(Vector3 at)
    {
        var moon = new GameObject("SpiritMoon").AddComponent<SpriteRenderer>();
        moon.transform.position = at + Vector3.forward * 0.5f;
        Sprite[] frames = Fx.Frames(Fx.ShiroMoon);
        if (frames != null)
        {
            moon.sprite = frames[0];
            moon.transform.localScale = Vector3.one * (6f / frames[0].bounds.size.x);
            moon.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
        }
        else
        {
            moon.sprite = ProceduralSprites.SoftGlow;
            moon.transform.localScale = Vector3.one * 7f;
        }
        moon.sortingOrder = 2;
        moon.color = Color.clear;
        transient.Add(moon.gameObject);
        return moon;
    }

    // When her first bar empties she staggers to a knee and her blades go dark; mist spreads over the field and a
    // howl sounds far off. The souls of her shrine answer: wisps of spirit light rise all over the court and spiral
    // into one soul beside her, which swells as each merges, Kuon's shape gathering inside it. The soul crushes to a
    // point, two eyes open in it, and it bursts: the wolf steps out of the merge in blue flame. She rises wreathed in
    // it, with her second bar.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        StopRoaming();
        StopHunting();
        ultimate = false;
        exposed = false;
        animator.Play(art.hit, false, null, art.actionFps * 0.7f);
        Sfx.Play(Sfx.BossStagger, 0.8f, 0.9f);
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < 7; i++)
        {
            float x = CameraShake.Center.x + Mathf.Lerp(-HalfWidth, HalfWidth, i / 6f);
            Puff(new Vector3(x, GroundY + Random.Range(0.5f, 2.5f), 0), 2.6f);
        }
        Sfx.Play(Sfx.MistSwirl, 1f, 0.6f);
        yield return new WaitForSeconds(0.6f);
        Sfx.Play(Sfx.WolfHowl, 0.5f, 0.85f);

        Vector3 heart = WolfRest + Vector3.up * 0.3f;
        yield return GatherSouls(heart, SoulGather);
        yield return wolf.OpenEyes(heart, 0.5f);

        // The merged soul bursts and Kuon steps out of it.
        CameraShake.Add(0.7f);
        if (Fx.Play(Fx.ShiroMawBurst, heart, Color.white, 3.2f, null, 58) == null) Fx.Play(Fx.Explosion, heart, Cyan, 3f);
        Fx.Play(Fx.Explosion, heart, Cyan, 2.4f);
        ElementFx.Glow(heart, Frost, 1f, 6f, 0.5f);
        Debris.BurstSprites(heart, art.deathParticles, Cyan, 30, 1.2f, 9f);
        Sfx.Play(Sfx.WolfHowl, 1f, 1f);
        Sfx.Play(Sfx.FireBlast, 0.8f, 1.3f);
        RemoveSoulCore();
        Dim(0f, 0.8f);
        wolf.SetPresence(SpiritWolf.Presence.Present);
        yield return wolf.AppearAt(WolfRest);

        bodyTint = new Color(0.85f, 0.96f, 1f);
        WearPhaseArt();
        if (phaseDash != null) dashFrames = phaseDash;
        AddAura();
        PlayIdle();
        // Kuon takes the field as a foe of its own, sharing her bar.
        wolf.Arm();
        WolfJoined?.Invoke(wolf);
        movesSinceParry = 1;
        movesSinceEclipse = 0;
        // Her oath: a title card with her and the wolf, over which she speaks; without its art, a subtitle.
        if (PhaseCard != null) yield return PhaseCard(this);
        else
        {
            Speak(BossLine.Phase);
            RenameBar?.Invoke($"{PhaseTitle}, {OathEpithet}");
            yield return new WaitForSeconds(2.2f);
        }
        breaking = false;
    }

    // How long the shrine's souls take to gather into Kuon, and how many come.
    const float SoulGather = 3.6f;
    const int Souls = 26;

    // The merging soul and the bloom behind it, while the souls gather.
    SpriteRenderer soulCore, soulHalo;
    // Each gathering wisp's soft glow.
    readonly SpriteRenderer[] wispGlows = new SpriteRenderer[Souls];

    // Wisps of spirit light rise from all over the court, near the ground and up the walls, and spiral in to `heart`,
    // each arriving on its own beat. Every one that merges swells the soul there with a rising chime, and Kuon's
    // shape grows inside it. The arena darkens and the ground shakes harder as it fills; once all have merged the
    // soul crushes to a white-hot point.
    IEnumerator GatherSouls(Vector3 heart, float seconds)
    {
        Dim(0.5f, 0.8f);
        soulHalo = Glowing("SoulHalo", ProceduralSprites.SoftGlow, 56);
        Sprite[] coreFrames = Fx.Frames(Fx.ShiroMawOrb);
        soulCore = Glowing("SoulCore", coreFrames != null ? coreFrames[0] : ProceduralSprites.SoftGlow, 57);
        if (coreFrames != null) soulCore.gameObject.AddComponent<SpriteAnimator>().Play(coreFrames, true, null, 10f);
        Sprite[] wispFrames = Fx.Frames(Fx.ShiroSpiritWisp);
        Sfx.Play(Sfx.MistSwirl, 0.9f, 0.8f);
        Sfx.Play(Sfx.LanternCharge, 0.6f, 0.6f);

        var wisps = new SpriteRenderer[Souls];
        var starts = new Vector3[Souls];
        var delays = new float[Souls];
        var travels = new float[Souls];
        var spins = new float[Souls];
        var last = new Vector3[Souls];
        for (int i = 0; i < Souls; i++)
        {
            // Spread across the whole court, most rising from the ground, a few from higher up the walls.
            float x = CameraShake.Center.x + Mathf.Lerp(-HalfWidth + 1f, HalfWidth - 1f, (i + Random.value) / Souls);
            float y = i % 3 == 0 ? Random.Range(GroundY + 3f, heart.y - 1f) : GroundY + Random.Range(0.3f, 1.2f);
            starts[i] = new Vector3(x, y, 0);
            // Their arrivals are spread over the whole gather, a trickle that becomes a flood.
            delays[i] = seconds * 0.55f * Mathf.Sqrt(Random.value);
            travels[i] = Random.Range(1.6f, 2.3f);
            spins[i] = Random.Range(90f, 200f) * (x < heart.x ? -1f : 1f);
            last[i] = starts[i];
        }

        int merged = 0;
        float nextRumble = 0f;
        float end = seconds;
        for (int i = 0; i < Souls; i++) end = Mathf.Max(end, delays[i] + travels[i]);
        for (float t = 0; t < end; t += Time.deltaTime)
        {
            for (int i = 0; i < Souls; i++)
            {
                float u = (t - delays[i]) / travels[i];
                if (u < 0f || (u >= 1f && wisps[i] == null)) continue;
                if (wisps[i] == null)
                {
                    // It rises out of a breath of mist.
                    wisps[i] = Glowing("Wisp", wispFrames != null ? wispFrames[0] : ProceduralSprites.Glow, 58);
                    if (wispFrames != null) wisps[i].gameObject.AddComponent<SpriteAnimator>().Play(wispFrames, true, null, 12f);
                    wisps[i].transform.localScale = Vector3.one * (wispFrames != null ? Random.Range(1.5f, 2.1f) : 0.7f);
                    wispGlows[i] = Glowing("WispGlow", ProceduralSprites.SoftGlow, 57);
                    wispGlows[i].transform.localScale = Vector3.one * 1.6f;
                    Puff(starts[i], 1.2f);
                }
                if (u >= 1f)
                {
                    // It merges: the soul swells, rings and flashes.
                    Drop(wisps[i]);
                    Drop(wispGlows[i]);
                    wisps[i] = null;
                    delays[i] = float.MaxValue;
                    merged++;
                    Fx.Play(Fx.Impact, heart, Frost, 0.8f + 0.04f * merged);
                    Sfx.Play(Sfx.SoulCollect, 0.35f, 0.8f + 0.05f * merged);
                    continue;
                }
                // Up off the ground first, then a spiral that tightens onto the soul.
                float pull = u * u;
                Vector3 offset = starts[i] - heart;
                Vector3 at = heart + Quaternion.Euler(0, 0, spins[i] * u) * offset * (1f - pull) + Vector3.up * (Mathf.Sin(Mathf.PI * u) * 1.2f);
                Vector3 step = at - last[i];
                last[i] = at;
                wisps[i].transform.position = at + Vector3.back * 0.3f;
                // Drawn flying right, so it turns to face where it goes.
                if (step.sqrMagnitude > 0.00001f) wisps[i].transform.rotation = Quaternion.FromToRotation(Vector3.right, step);
                float fade = Mathf.Clamp01(u / 0.2f);
                wisps[i].color = wispFrames != null ? new Color(1f, 1f, 1f, fade) : new Color(Frost.r, Frost.g, Frost.b, 0.9f * fade);
                wispGlows[i].transform.position = at + Vector3.back * 0.25f;
                wispGlows[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f * fade);
                if (Random.value < 0.15f) Afterimage(wisps[i], new Color(Spirit.r, Spirit.g, Spirit.b, 0.3f * fade));
            }

            float full = (float)merged / Souls;
            float size = Mathf.Lerp(0.5f, 3.6f, full) * (1f + 0.08f * Mathf.Sin(t * 30f));
            soulCore.transform.position = heart + Vector3.back * 0.4f;
            soulCore.transform.localScale = Vector3.one * size / (coreFrames != null ? OrbArtSize : 0.6f);
            // Kept see-through, so the wolf taking shape inside shows.
            soulCore.color = coreFrames != null ? new Color(1f, 1f, 1f, 0.3f + 0.3f * full) : new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f + 0.3f * full);
            soulHalo.transform.position = heart + Vector3.back * 0.35f;
            soulHalo.transform.localScale = Vector3.one * size * 2.6f;
            soulHalo.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f + 0.25f * full);
            // Kuon's shape gathers inside the soul as it fills.
            wolf.Form(heart, 0.9f * full);
            if (t >= nextRumble)
            {
                nextRumble = t + 0.15f;
                CameraShake.Add(0.02f + 0.06f * full);
            }
            yield return null;
        }

        // Any wisp still a frame short of the soul when time runs out merges with the rest.
        for (int i = 0; i < Souls; i++)
        {
            Drop(wisps[i]);
            Drop(wispGlows[i]);
            wisps[i] = wispGlows[i] = null;
        }

        // All have merged: the soul bulges once, then crushes to a white-hot point.
        Vector3 from = soulCore.transform.localScale;
        float halo = soulHalo.transform.localScale.x;
        for (float t = 0; t < 0.35f; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            float squeeze = k < 0.3f ? 1f + 0.2f * (k / 0.3f) : Mathf.Lerp(1.2f, 0.1f, Mathf.SmoothStep(0, 1, (k - 0.3f) / 0.7f));
            soulCore.transform.localScale = from * squeeze;
            soulHalo.transform.localScale = Vector3.one * halo * squeeze;
            soulHalo.color = new Color(Frost.r, Frost.g, Frost.b, 0.4f + 0.5f * k);
            yield return null;
        }
        Fx.Play(Fx.Impact, heart, Color.white, 1.6f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 0.7f);
        Dim(0.5f, 0.3f);
    }

    void RemoveSoulCore()
    {
        Drop(soulCore);
        Drop(soulHalo);
        soulCore = soulHalo = null;
    }

    // Blue spirit flame licking up from her feet and a cold glow pulsing behind her.
    void AddAura()
    {
        Vector3 feet = new Vector3(Centre.x, sprite.bounds.min.y + sprite.bounds.size.y * 0.12f, 0);
        auraFlames = Fx.Play(Fx.Aura(Element.Water), feet, new Color(0.5f, 0.9f, 1f, 0.9f), 3.2f, transform, sprite.sortingOrder - 1);
        auraGlow = Kunai.CreateGlow(sprite.transform, 2.2f, sprite.sortingOrder - 2, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
        StartCoroutine(PulseAura(auraGlow));
    }

    // Enraged, the glow burns hotter and quicker, flaring from spirit blue to blood red; it goes out when she kneels.
    IEnumerator PulseAura(SpriteRenderer glow)
    {
        while (glow != null && !IsDead && !Down)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (enraged ? 7f : 3.5f));
            Color colour = enraged ? Color.Lerp(Cyan, Rage, pulse) : Cyan;
            glow.color = new Color(colour.r, colour.g, colour.b, (enraged ? 0.4f : 0.25f) + 0.3f * pulse);
            glow.transform.localScale = Vector3.one * ((enraged ? 2.4f : 2f) + 0.4f * pulse);
            yield return null;
        }
        if (glow != null) Fader.Fade(glow, 0.8f);
    }

    // Her fall: the swords drop from her hands, she sinks to a knee and her flames fade; the wolf comes to stand
    // by her and she rests a hand on its head; it dissolves into light, she speaks her last, and follows it.
    protected override IEnumerator Fall()
    {
        StopRoaming();
        StopHunting();
        if (wolf != null) wolf.Disarm();
        ultimate = false;
        exposed = false;
        if (auraFlames != null) auraFlames.Stop();
        if (auraGlow != null) Destroy(auraGlow.gameObject);
        CameraShake.Add(0.6f);
        Debris.BurstSprites(Centre, art.deathParticles, Cyan, 24, 1f, 8f);
        if (bladeGlow != null) Fader.Fade(bladeGlow, 1.5f);
        // If she went down before Kuon fell she is already kneeling, blades dropped, and the scene picks up from there.
        if (!Down)
        {
            Sfx.Play(Sfx.BladeRing, 1f, 0.6f);
            DropBlade(Hands + Vector3.left * 0.6f, -1f);
            DropBlade(Hands + Vector3.right * 0.6f, 1f);
            // The defeat clip plays down to her knee and holds there.
            if (art.death != null && art.death.Length > 0) animator.Play(art.death, false, null, art.actionFps * 0.6f);
            else sprite.transform.localRotation = Quaternion.Euler(0, 0, -10f);
            Vector3 from = anchor;
            for (float t = 0; t < 1f; t += Time.deltaTime)
            {
                anchor = from + Vector3.down * (0.25f * t);
                body.MovePosition(anchor);
                yield return null;
            }
        }
        else yield return new WaitForSeconds(1f);

        if (wolf != null)
        {
            wolf.SetPresence(SpiritWolf.Presence.Present);
            if (!wolf.Visible) yield return wolf.AppearAt(WolfRest);
            yield return wolf.WalkTo(anchor + new Vector3(anchor.x < CameraShake.Center.x ? 1.6f : -1.6f, -0.1f, 0f), 1.6f);
        }
        // Her Defeat line was spoken as she fell; this is the beat she rests her hand on its head.
        yield return new WaitForSeconds(2.6f);
        if (wolf != null)
        {
            Debris.BurstSprites(wolf.transform.position, art.deathParticles, Cyan, 30, 1.1f, 5f);
            Sfx.Play(Sfx.SoulCollect, 0.8f, 0.8f);
            yield return wolf.Dissolve(1.2f);
        }
        Speak(BossLine.DefeatFinal);
        yield return new WaitForSeconds(2.4f);

        Sfx.Play(Sfx.PlayerDeath, 0.8f, 0.9f);
        for (float t = 0; t < 1.8f; t += Time.deltaTime)
        {
            float k = t / 1.8f;
            Color c = bodyTint;
            c.a = bodyTint.a * (1f - k);
            sprite.color = c;
            if (Random.value < 0.3f) Debris.BurstSprites(Centre + (Vector3)Random.insideUnitCircle * 0.6f, art.deathParticles, Cyan, 2, 0.9f, 2.5f);
            yield return null;
        }
        Destroy(gameObject);
    }

    // A dropped katana: tumbles to the ground and fades.
    void DropBlade(Vector3 from, float spin)
    {
        SpriteRenderer blade = SwordPicture(from, Random.Range(-20f, 20f));
        transient.Remove(blade.gameObject);
        StartCoroutine(Tumble(blade, spin));
    }

    IEnumerator Tumble(SpriteRenderer blade, float spin)
    {
        Vector3 velocity = new Vector3(spin * 1.2f, 1.5f, 0f);
        float ground = GroundY + 0.4f;
        while (blade != null && blade.transform.position.y > ground)
        {
            velocity += Vector3.down * 12f * Time.deltaTime;
            blade.transform.position += velocity * Time.deltaTime;
            blade.transform.Rotate(0, 0, spin * 400f * Time.deltaTime);
            yield return null;
        }
        if (blade == null) yield break;
        Sfx.Play(Sfx.Deflect, 0.5f, 0.8f);
        Fader.Fade(blade, 2.5f);
    }
}

// Kuon, Shirogane's spirit wolf, the guardian of her family's shrine: a translucent body of blue spirit flame with
// burning cyan eyes. A faint silhouette behind her in her first phase; once she calls it, it keeps its own ground on
// her row, padding from spot to spot, and vanishes into mist to attack from wherever it reappears. Everything it does
// to the ronin is an Arrow it throws, but from her phase break it can be hit while it stands in sight, its health
// sharing her bar. If she falls first it fights on enraged, bigger and burning red; broken, it fades to a shadow at
// her side until her fall.
[RequireComponent(typeof(Rigidbody))]
public class SpiritWolf : MonoBehaviour, IHittable
{
    public enum Presence { Silhouette, Present }

    // Scaled by Boss.HealthScale (the oaths) when it is armed, like the warlords.
    public const int StartingHealth = Shirogane.SecondPhaseShare;
    // A parried Fang of the Oath does no damage by Damage.Of; on Kuon it lands as a heavy blow and dazes it.
    const int ReflectedDamage = 20;
    const float DazeSeconds = 2f;
    const int SamuraiLayer = 7;

    static readonly Color Body = new Color(0.6f, 0.9f, 1f);
    static readonly Color EyeColour = new Color(0.4f, 1f, 1f);

    public event System.Action<Vector3, KillSource> Damaged;
    public event System.Action Broken;

    public int MaxHealth { get; private set; } = StartingHealth;
    public int Health { get; private set; } = StartingHealth;
    public float HealthFraction => (float)Health / MaxHealth;
    // In the fight as a foe of its own, from her phase break until her fall.
    public bool Armed { get; private set; }
    public bool IsBroken { get; private set; }
    public bool Dazed => Time.time < dazedUntil;
    // She has fallen and it fights on for both of them.
    public bool Enraged { get; private set; }
    // Only while it stands in plain sight: not in mist, not as the charge coming down a lane.
    public bool Targetable => Armed && !IsBroken && !hidden && shown > 0.5f;
    public Vector3 Centre => sprite.bounds.center;
    float dazedUntil;
    // Half points from shadow slashes that haven't yet made a whole one.
    float shadowChip;
    Collider hitbox;

    Shirogane owner;
    SpriteRenderer sprite;
    SpriteAnimator animator;
    SpriteRenderer glow;
    SpriteRenderer[] eyes;
    Sprite[] idle, howl, charge, vanish, maw;
    float scale;
    Presence presence;
    float shown;
    float shownTarget;
    float grown = 1f;
    float glare;
    Coroutine following;
    bool hidden;
    // Held, it stays where it stands instead of padding about (mid-howl, mid-lance).
    bool held;
    // Taking shape inside the soul at her phase break: its presence is set from outside, frame by frame.
    bool forming;
    // Its own spot on her row, and when it next moves on from it.
    Vector3 den;
    float nextProwl;
    // Eases in from 0 to 1 as it enrages: bigger and redder.
    float rage;

    static readonly Color RageColour = new Color(1f, 0.4f, 0.5f);

    // Where it keeps to between moves: its own ground while it fights, else her side.
    public Vector3 Home => Armed && !IsBroken ? den : owner.WolfRest;

    public bool Visible => shown > 0.05f && !hidden;
    // Where its howls and beams come from: the jaws, a little below the top of the body.
    public Vector3 Mouth => sprite != null && sprite.sprite != null
        ? new Vector3(sprite.bounds.center.x, sprite.bounds.center.y + sprite.bounds.extents.y * 0.3f, 0)
        : transform.position + Vector3.up * 0.4f;

    public static SpiritWolf Create(Shirogane owner, Sprite[] idle, Sprite[] howl, Sprite[] charge, Sprite[] vanish, Sprite[] maw, float scale, int order)
    {
        var go = new GameObject("SpiritWolf") { layer = SamuraiLayer };
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var wolf = go.AddComponent<SpiritWolf>();
        wolf.owner = owner;
        wolf.idle = idle;
        wolf.howl = howl;
        wolf.charge = charge;
        wolf.vanish = vanish;
        wolf.maw = maw;
        wolf.scale = scale;
        go.transform.position = owner.WolfRest;
        go.transform.localScale = Vector3.one * scale;

        var view = new GameObject("Sprite");
        view.transform.SetParent(go.transform, false);
        wolf.sprite = view.AddComponent<SpriteRenderer>();
        wolf.sprite.sortingOrder = order;
        wolf.animator = view.AddComponent<SpriteAnimator>();
        if (idle != null)
        {
            wolf.animator.Play(idle, true, null, 6f);
            view.transform.localPosition = -wolf.sprite.sprite.bounds.center;
        }
        else
        {
            // Without art: a long soft body of light with two eyes.
            wolf.sprite.sprite = ProceduralSprites.Glow;
            view.transform.localScale = new Vector3(2.4f / scale, 1.4f / scale, 1f);
            wolf.eyes = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var eye = new GameObject("Eye").AddComponent<SpriteRenderer>();
                eye.transform.SetParent(go.transform, false);
                eye.transform.localPosition = new Vector3((i - 0.5f) * 0.5f / scale, 0.25f / scale, -0.05f);
                eye.transform.localScale = Vector3.one * 0.16f / scale;
                eye.sprite = ProceduralSprites.Disc;
                eye.sortingOrder = order + 1;
                wolf.eyes[i] = eye;
            }
        }
        wolf.glow = Kunai.CreateGlow(go.transform, 2.6f / scale, order - 1, new Color(0.3f, 0.9f, 1f, 0.3f));
        // A box over the body, inside the art's padding; off until it is armed and in sight.
        Bounds frame = wolf.sprite.bounds;
        var box = go.AddComponent<BoxCollider>();
        box.center = (frame.center - go.transform.position) / scale;
        box.size = new Vector3(frame.size.x * 0.7f / scale, frame.size.y * 0.6f / scale, 1f);
        box.enabled = false;
        wolf.hitbox = box;
        wolf.Apply();
        return wolf;
    }

    // Takes the field with a full bar.
    public void Arm()
    {
        Health = MaxHealth = Mathf.RoundToInt(StartingHealth * Boss.HealthScale);
        IsBroken = false;
        Armed = true;
        den = transform.position;
        nextProwl = Time.time + 2f;
    }

    // She has fallen first: it howls and swells, burning red, and fights on harder.
    public void Enrage()
    {
        if (!Armed || IsBroken) return;
        Enraged = true;
        PlayHowl();
        Glare(1.2f);
        Sfx.Play(Sfx.WolfHowl, 1f, 0.7f);
        Fx.Play(Fx.Explosion, Centre, RageColour, 2.2f);
        CameraShake.Add(0.4f);
    }

    // Reeling where it stands, every blow landing twice as hard (its spent ultimate).
    public void Daze(float seconds)
    {
        dazedUntil = Mathf.Max(dazedUntil, Time.time + seconds);
    }

    // Stays put for a move fired from where it stands, then pads on again.
    public void Hold() => held = true;
    public void Release() => held = false;

    // Melts away and takes shape on fresh ground of its own.
    public IEnumerator ReturnHome()
    {
        if (Armed && !IsBroken) den = owner.ProwlSpot();
        nextProwl = Time.time + Random.Range(1.5f, 3f);
        yield return AppearAt(Home);
    }

    // Out of the fight for good (her fall): nothing can hit it any more.
    public void Disarm()
    {
        Armed = false;
        hitbox.enabled = false;
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (!Armed || IsBroken) return;
        if (!Targetable && source != KillSource.Reflected) return;
        if (source == KillSource.Reflected)
        {
            damage = ReflectedDamage;
            dazedUntil = Time.time + DazeSeconds;
            Sfx.Play(Sfx.WolfGrowl, 1f, 1.6f);
        }
        // Dazed, it can't guard itself: every other blow lands twice as hard.
        else if (Dazed) damage *= 2;
        damage = Damage.Lessen(damage, source, ref shadowChip);
        Health = Mathf.Max(0, Health - damage);
        HitFlash.Flash(sprite, 0.1f);
        Sfx.Play(Sfx.BossHit, 0.8f, 1.25f);
        Damaged?.Invoke(Centre, source);
        if (Health <= 0 && !IsBroken) Break();
    }

    // Its bar is empty: it cries out, bursts into mist and fades to a shadow that keeps to her side.
    void Break()
    {
        IsBroken = true;
        Enraged = false;
        held = false;
        hitbox.enabled = false;
        StopAllCoroutines();
        following = null;
        hidden = false;
        grown = 1f;
        glare = 0f;
        if (Fx.Play(Fx.ShiroMist, Centre, Color.white, 3f) == null) Fx.Play(Fx.SmokePuff, Centre, new Color(0.55f, 0.85f, 1f, 0.8f), 3f);
        Fx.Play(Fx.Explosion, Centre, Body, 2f);
        Sfx.Play(Sfx.WolfHowl, 1f, 1.5f);
        CameraShake.Add(0.4f);
        shownTarget = 0.18f;
        Follow();
        Broken?.Invoke();
    }

    public void SetPresence(Presence value)
    {
        presence = value;
        shownTarget = value == Presence.Present ? 1f : 0.22f;
        if (IsBroken && Armed) shownTarget = 0.18f;
        if (value == Presence.Present) Follow();
        else StartCoroutine(Haunt());
    }

    // In her first phase it only flickers into sight now and then, a shadow at her back.
    IEnumerator Haunt()
    {
        while (presence == Presence.Silhouette && this != null)
        {
            shownTarget = 0f;
            yield return new WaitForSeconds(Random.Range(8f, 14f));
            if (presence != Presence.Silhouette) yield break;
            transform.position = owner.WolfRest;
            shownTarget = 0.22f;
            yield return new WaitForSeconds(1.2f);
        }
    }

    // Keeps to its home: its own ground while it fights, padding to a fresh spot now and then; her side otherwise.
    void Follow()
    {
        if (following == null) following = StartCoroutine(Following());
    }

    IEnumerator Following()
    {
        while (true)
        {
            if (!hidden && !held && presence == Presence.Present)
            {
                if (Armed && !IsBroken && Time.time >= nextProwl)
                {
                    den = owner.ProwlSpot();
                    nextProwl = Time.time + Random.Range(2f, 3.5f);
                }
                transform.position = Vector3.Lerp(transform.position, Home, 1f - Mathf.Exp(-Time.deltaTime * 3f));
            }
            yield return null;
        }
    }

    void StopFollowing()
    {
        if (following != null) StopCoroutine(following);
        following = null;
    }

    // Melts into mist where it stands.
    public IEnumerator Vanish()
    {
        StopFollowing();
        hidden = true;
        if (vanish != null) animator.Play(vanish, false, null, 12f);
        Fx.Play(Fx.SmokePuff, transform.position, new Color(0.55f, 0.85f, 1f, 0.7f), 1.8f);
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            shown = Mathf.Lerp(shownTarget, 0f, t / 0.25f);
            Apply();
            yield return null;
        }
        shown = 0f;
        Apply();
    }

    // Takes shape again at a spot. Held, it stays there (a perch over a lane) instead of drifting back to her side.
    public IEnumerator AppearAt(Vector3 at, bool hold = false)
    {
        StopFollowing();
        forming = false;
        transform.position = at;
        hidden = false;
        if (idle != null) animator.Play(idle, true, null, 6f);
        Fx.Play(Fx.SmokePuff, at, new Color(0.55f, 0.85f, 1f, 0.7f), 1.8f);
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            shown = Mathf.Lerp(0f, shownTarget, t / 0.25f);
            Apply();
            yield return null;
        }
        shown = shownTarget;
        Apply();
        if (presence == Presence.Present && !hold) Follow();
    }

    // Takes shape inside the merging soul at her phase break: drawn at `amount` of its full presence, held at `at`
    // until it appears for real.
    public void Form(Vector3 at, float amount)
    {
        StopFollowing();
        forming = true;
        hidden = false;
        transform.position = at;
        shown = amount;
        Apply();
    }

    // Beside her at once if it isn't already, for a move that fires from its rest.
    public IEnumerator Settle(Vector3 rest)
    {
        if (!Visible) yield return AppearAt(rest);
    }

    // Gone in an instant: it has become the charge coming down the lane.
    public void Hide()
    {
        StopFollowing();
        hidden = true;
        shown = 0f;
        Apply();
    }

    // Back home without ceremony, when a stagger cuts a move of theirs short.
    public void Recall()
    {
        StopAllCoroutines();
        following = null;
        hidden = false;
        held = false;
        shown = shownTarget;
        grown = 1f;
        transform.position = Home;
        Apply();
        if (presence == Presence.Present) Follow();
        else StartCoroutine(Haunt());
    }

    // Eyes and jaws burning bright for a moment: the warning before a howl or the beam.
    public void Glare(float seconds)
    {
        glare = seconds;
        StartCoroutine(Glaring(seconds));
    }

    IEnumerator Glaring(float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            glare = 1f - t / seconds;
            Apply();
            yield return null;
        }
        glare = 0f;
        Apply();
    }

    // Its Maw: it rears, opens its jaws round the gathering orb and gulps it down over `seconds`, then holds the
    // last frame, head lowered, until the beam is spent. Without the clip it howls instead.
    public void PlayMaw(float seconds)
    {
        if (maw == null)
        {
            PlayHowl();
            return;
        }
        animator.Play(maw, false, () => animator.Play(new[] { maw[maw.Length - 1] }, true), maw.Length / Mathf.Max(0.1f, seconds));
    }

    public void PlayIdle()
    {
        if (idle != null) animator.Play(idle, true, null, 6f);
    }

    public void PlayHowl()
    {
        if (howl != null) animator.Play(howl, false, () => { if (idle != null) animator.Play(idle, true, null, 6f); }, 12f);
    }

    public IEnumerator Grow(float to, float seconds)
    {
        float from = grown;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            grown = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
            Apply();
            yield return null;
        }
        grown = to;
        Apply();
    }

    // A gulp: it swells for an instant around what it has swallowed, then settles back.
    public IEnumerator Gulp()
    {
        float from = grown;
        yield return Grow(from * 1.15f, 0.08f);
        yield return Grow(from, 0.25f);
    }

    // Its own Maw burst in its jaws: a heavy blow, and it reels.
    public void Backfire(int damage)
    {
        if (!Armed || IsBroken) return;
        TakeHit(damage, KillSource.Effect);
        if (!IsBroken) dazedUntil = Time.time + DazeSeconds;
    }

    // Two eyes open in the dark behind her, before the body takes shape.
    public IEnumerator OpenEyes(Vector3 at, float seconds)
    {
        transform.position = at;
        var pair = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            pair[i] = new GameObject("Eye").AddComponent<SpriteRenderer>();
            pair[i].transform.position = at + new Vector3((i - 0.5f) * 0.5f, 0.3f, -0.1f);
            pair[i].transform.localScale = Vector3.one * 0.2f;
            pair[i].sprite = ProceduralSprites.Glow;
            pair[i].sortingOrder = 45;
        }
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            foreach (SpriteRenderer eye in pair) eye.color = new Color(EyeColour.r, EyeColour.g, EyeColour.b, t / seconds);
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        foreach (SpriteRenderer eye in pair) Destroy(eye.gameObject);
    }

    // Pads over to a spot beside her at her fall.
    public IEnumerator WalkTo(Vector3 at, float seconds)
    {
        StopFollowing();
        Vector3 from = transform.position;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / seconds);
            transform.position = Vector3.Lerp(from, at, k) + Vector3.up * (0.12f * Mathf.Abs(Mathf.Sin(t * 9f)) * (1f - k));
            yield return null;
        }
        transform.position = at;
    }

    // Comes apart into light.
    public IEnumerator Dissolve(float seconds)
    {
        StopFollowing();
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            shown = shownTarget * (1f - t / seconds);
            Apply();
            yield return null;
        }
        Destroy(gameObject);
    }

    // A small ghost of the wolf (one of its pack) hanging over a lane; the caller owns it through the given list.
    public SpriteRenderer Silhouette(Vector3 at, float size, List<GameObject> owner)
    {
        var pup = new GameObject("WolfPup").AddComponent<SpriteRenderer>();
        pup.transform.position = at + Vector3.back * 0.2f;
        if (idle != null)
        {
            pup.sprite = idle[0];
            pup.transform.localScale = Vector3.one * scale * size;
            pup.gameObject.AddComponent<SpriteAnimator>().Play(idle, true, null, 6f);
        }
        else
        {
            pup.sprite = ProceduralSprites.Glow;
            pup.transform.localScale = new Vector3(1.6f * size, 1f * size, 1f);
        }
        pup.sortingOrder = sprite.sortingOrder;
        pup.color = new Color(Body.r, Body.g, Body.b, 0.6f);
        owner.Add(pup.gameObject);
        return pup;
    }

    void Update()
    {
        hitbox.enabled = Targetable;
        rage = Mathf.MoveTowards(rage, Enraged ? 1f : 0f, Time.deltaTime * 1.5f);
        // A dazed wolf reels where it stands.
        sprite.transform.localRotation = Dazed && !IsBroken ? Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 18f) * 8f) : Quaternion.identity;
        if (hidden || forming) return;
        shown = Mathf.MoveTowards(shown, shownTarget, Time.deltaTime * 1.5f);
        Apply();
    }

    void Apply()
    {
        float a = hidden ? 0f : shown;
        float flicker = 0.9f + 0.1f * Mathf.Sin(Time.time * 7f);
        // Enraged, its flame runs hot: a red heart pulsing through the blue, and its glow and eyes burn red.
        float heat = rage * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
        Color body = Color.Lerp(Body, RageColour, 0.35f * heat);
        sprite.color = new Color(body.r, body.g, body.b, a * 0.78f * flicker);
        transform.localScale = Vector3.one * scale * grown * (1f + 0.25f * rage);
        Color halo = Color.Lerp(new Color(0.3f + 0.5f * glare, 0.9f, 1f), RageColour, 0.7f * rage);
        glow.color = new Color(halo.r, halo.g, halo.b, a * (0.3f + 0.4f * glare + 0.2f * rage));
        if (eyes == null) return;
        Color eye = Color.Lerp(EyeColour, RageColour, rage);
        foreach (SpriteRenderer e in eyes) e.color = new Color(eye.r, eye.g, eye.b, Mathf.Min(1f, a * 1.6f + glare));
    }
}
