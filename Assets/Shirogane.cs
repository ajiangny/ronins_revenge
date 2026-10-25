using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 3's warlord: Shirogane, the Forsaken Blade, a ghostly swordswoman in black, white and deep-blue armour
// whose twin katanas burn with blue spirit flame. She fights alone in her first phase, gliding about the top of
// the field and raining spectral blades; once her first bar empties her spirit wolf answers her and the two
// fight as one. Kuon is a foe of its own: while it stands it wards her, so only a sliver of each blow gets through,
// and it hunts the ronin alongside her. Break it and she fights on alone, faster in her grief. Everything she throws
// is a readable pattern with a lane or a gap left open, and twice a phase-long tell asks for a parry.
public class Shirogane : Boss
{
    // Per bar: she fights two, the second with her wolf.
    public const int StartingHealth = 140;

    static readonly Color Spirit = new Color(0.45f, 0.85f, 1f);
    static readonly Color Cyan = new Color(0.3f, 0.92f, 1f);
    static readonly Color Frost = new Color(0.85f, 0.97f, 1f);
    static readonly Color MistColour = new Color(0.55f, 0.85f, 1f, 0.7f);
    static readonly Color Trail = new Color(0.45f, 0.85f, 1f, 0.5f);

    enum Move { CrescentSlash, BladeRain, MoonlitDash, BladeBarrage, MirrorMoon, Moonsplitter, OathFang, Moonfall,
        WolfCharge, Howl, TwinAssault, WolfPack, EclipseFang, SpiritMaw }

    // Each value is (phase 1, phase 2 with Kuon, phase 2 alone): with the wolf at her side she gives no quarter, and
    // without it she gives less still.
    float Recovery => alone ? 0.35f : secondPhase ? 0.5f : 0.8f;
    float ComboChance => alone ? 0.8f : secondPhase ? 0.75f : 0.6f;
    int MaxCombo => alone ? 4 : secondPhase ? 3 : 2;
    float GlideSpeed => alone ? 9f : secondPhase ? 7.5f : 5f;
    float CrescentSpeed => alone ? 10f : secondPhase ? 9f : 8f;
    float RainWarning => alone ? 0.6f : secondPhase ? 0.7f : 0.9f;
    // The parry-only draws: how fast they come, and how long the sight line tracks the ronin before they do.
    float DrawSpeed => alone ? 11f : 10f;
    float DrawAim => alone ? 0.65f : secondPhase ? 0.8f : 0.9f;

    // While Kuon stands, her ward lets this share of each blow through.
    const float WardDamageTaken = 0.1f;
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
        _ => null,
    };

    // Raised when Kuon takes the field at her phase break, so the HUD can show its bar.
    public event System.Action<SpiritWolf> WolfJoined;

    bool secondPhase => SecondPhase;
    // Kuon stands beside her and wards her.
    bool Guarded => secondPhase && wolf != null && wolf.Armed && !wolf.IsBroken;
    // Kuon has fallen: the Lone Blade.
    bool alone => secondPhase && wolf != null && wolf.IsBroken;
    bool breaking;
    Move? lastMove;
    // How many times running the last move has come up: never the same major move three times in a row.
    int repeats;
    Move? lastWolfMove;
    int wolfRepeats;
    int movesSinceEclipse;
    // Counts toward the next parry-only draw; starts part way so one comes within her first few moves.
    int movesSinceParry = 1;
    // Kuon's own attack loop, run beside hers while it stands.
    Coroutine hunting;
    // True while Kuon is mid-attack, and while she holds it back for a move of their own or her densest pattern.
    bool wolfBusy;
    bool holdWolf;
    // A cold shimmer around her while Kuon wards her.
    SpriteRenderer wardGlow;
    // True for the recovery after the Eclipse Fang, when she stands open and takes double damage.
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
    // Kuon's ward: only a sliver of each blow gets through, except while she stands spent after the Eclipse Fang.
    protected override bool SuperArmor => Guarded && !exposed && !breaking;
    protected override float ArmorDamageTaken => WardDamageTaken;
    protected override Color ArmorSpark => Cyan;
    float TopOfScreen => CameraShake.Center.y + Camera.main.orthographicSize + 0.5f;
    float GroundY => ronin.Feet.y - 0.3f;
    float LeftEdge => CameraShake.Center.x - HalfWidth;
    float RightEdge => CameraShake.Center.x + HalfWidth;
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
            float lit = exposed || breaking ? 0.08f : 0.25f + 0.2f * pulse;
            bladeGlow.color = new Color(Cyan.r, Cyan.g, Cyan.b, lit);
            bladeGlow.transform.localScale = Vector3.one * ((1.5f + 0.3f * pulse) / transform.lossyScale.x);
            yield return null;
        }
    }

    protected override IEnumerator Fight()
    {
        exposed = false;
        holdWolf = false;
        while (true)
        {
            while (breaking) yield return null;
            // Kuon hunts on its own clock beside hers for as long as it stands.
            if (Guarded && hunting == null) hunting = StartCoroutine(Hunt());
            // She glides on between combos: the opening is there, but she never stands still for it.
            yield return Roam(Recovery);

            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                Move move = NextMove();
                // Their joint moves need Kuon free, and her blade rain is dense enough without its charges on top.
                if (Guarded && (move == Move.TwinAssault || move == Move.EclipseFang || move == Move.OathFang || move == Move.BladeRain))
                {
                    holdWolf = true;
                    while (wolfBusy) yield return null;
                }
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
                    case Move.TwinAssault: yield return TwinAssault(); break;
                    case Move.EclipseFang: yield return EclipseFang(); break;
                }
                holdWolf = false;
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
            // Kuon's ultimate needs Kuon; alone she brings the moon down herself.
            return alone ? Move.Moonfall : Move.EclipseFang;
        }
        else if (alone)
        {
            options = new List<Move> { Move.CrescentSlash, Move.CrescentSlash, Move.BladeRain, Move.MoonlitDash, Move.BladeBarrage, Move.MirrorMoon };
        }
        else
        {
            // Kuon's own moves run in Hunt; hers are the swords and the attacks they make together.
            options = new List<Move> { Move.CrescentSlash, Move.BladeRain, Move.MoonlitDash, Move.MirrorMoon, Move.TwinAssault, Move.TwinAssault };
        }
        if (movesSinceParry >= ParryEvery) options.Add(parry);
        if (lastMove is Move last && repeats >= 2) options.RemoveAll(m => m == last);
        return options[Random.Range(0, options.Count)];
    }

    // Kuon's attack loop while it stands: a charge, a howl, the pack or its Maw, then a breath, then another. It holds back
    // while she makes a move of their own, while it is dazed, and through her phase break.
    IEnumerator Hunt()
    {
        yield return Wait(1f);
        while (Guarded)
        {
            while (holdWolf || ultimate || breaking || wolf.Dazed) yield return null;
            if (!Guarded) break;
            wolfBusy = true;
            Move move = NextWolfMove();
            wolfRepeats = move == lastWolfMove ? wolfRepeats + 1 : 1;
            lastWolfMove = move;
            switch (move)
            {
                case Move.WolfCharge: yield return WolfCharge(); break;
                case Move.Howl: yield return Howl(); break;
                case Move.WolfPack: yield return WolfPack(); break;
                case Move.SpiritMaw: yield return SpiritMaw(); break;
            }
            wolfBusy = false;
            yield return Wait(Random.Range(1.2f, 1.8f));
        }
        wolfBusy = false;
        hunting = null;
    }

    Move NextWolfMove()
    {
        var options = new List<Move> { Move.WolfCharge, Move.WolfCharge, Move.Howl, Move.WolfPack, Move.SpiritMaw };
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
    }

    // Kuon is broken: her ward shatters and grief staggers her, the one long opening she gives, and she fights on
    // alone. Her farewell to it is said here; her stagger cry would talk over it.
    void OnWolfBroken()
    {
        StopHunting();
        holdWolf = false;
        ultimate = false;
        exposed = false;
        // The ultimate waits a full cycle, so her grief isn't followed straight away by Moonfall.
        movesSinceEclipse = 0;
        Fx.Play(Fx.Impact, Centre, Cyan, 3f);
        ElementFx.Glow(Centre, Frost, 1f, 4f, 0.5f);
        Sfx.Play(Sfx.WardShatter, 1f, 0.8f);
        CameraShake.Add(0.5f);
        Speak(BossLine.WolfFallen);
        Stagger(GriefStagger, cry: false);
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

    float NearX(float minStep, float maxStep)
    {
        float range = HalfWidth - 2.5f;
        float lo = CameraShake.Center.x - range, hi = CameraShake.Center.x + range;
        float step = Random.Range(minStep, maxStep) * (Random.value < 0.5f ? -1f : 1f);
        float x = anchor.x + step;
        if (x < lo || x > hi) x = anchor.x - step;
        return Mathf.Clamp(x, lo, hi);
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

    protected override void OnStaggered()
    {
        StopRoaming();
        StopHunting();
        holdWolf = false;
        exposed = false;
        ultimate = false;
        Sfx.Stop(Sfx.SpiritBeam);
        RemoveOrb();
        if (wolf != null) wolf.Recall();
    }

    // --- Shots ---

    // Positions of n lanes spread evenly across the field, clear of the edges.
    List<float> Lanes(int count)
    {
        var lanes = new List<float>(count);
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        for (int i = 0; i < count; i++) lanes.Add(Mathf.Lerp(lo, hi, (i + 0.5f) / count));
        return lanes;
    }

    // A blade of blue spirit flame cast from her katana; it can be cut down by a slash.
    Arrow Crescent(Vector3 from, Vector3 velocity)
    {
        Arrow wave = Shoot(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroCrescent);
        if (frames != null) wave.Restyle(frames, 14f, Color.white, new Vector2(2.2f, 1.4f), 0.4f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 2.4f);
        else wave.Restyle(ProceduralSprites.Glow, Cyan, new Vector2(2f, 0.8f), 0.4f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), 2.4f);
        return wave;
    }

    // A ghostly katana that falls point first and drifts straight through cover.
    Arrow GhostSword(Vector3 from, Vector3 velocity)
    {
        Arrow blade = Shoot(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroSword);
        if (frames != null) blade.Restyle(frames, 10f, Color.white, new Vector2(0.7f, 1.9f), 0.3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f), 1.4f);
        else blade.Restyle(ProceduralSprites.White, Frost, new Vector2(0.16f, 1.6f), 0.3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 1.4f);
        blade.PassesCover = true;
        return blade;
    }

    // A slow orb of the wolf's spirit energy, big enough to see coming and easy to read between.
    Arrow Orb(Vector3 from, Vector3 velocity)
    {
        Arrow orb = Shoot(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroOrb);
        if (frames != null) orb.Restyle(frames, 12f, Color.white, new Vector2(1.1f, 1.1f), 0.35f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 1.8f);
        else orb.Restyle(ProceduralSprites.Glow, new Color(0.6f, 0.95f, 1f), new Vector2(1f, 1f), 0.35f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), 1.8f);
        return orb;
    }

    // The wolf (or one of its pack) as a charge down a lane: unbreakable, it stops only on the ronin, and dissolves
    // into mist where it meets the ground. Drawn from the charge art, or as a streak of spirit light.
    Arrow Charge(Vector3 from, float speed, float size)
    {
        Arrow charge = Shoot(from, Vector3.down * speed);
        charge.MakeUnbreakable();
        Sprite[] frames = Fx.Frames(Fx.ShiroWolfCharge) ?? wolfCharge;
        if (frames != null) charge.Restyle(frames, 14f, new Color(0.75f, 0.95f, 1f, 0.9f), new Vector2(size * 1.4f, size * 1.9f), size * 0.45f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f), size * 2.2f);
        else charge.Restyle(ProceduralSprites.Glow, new Color(0.7f, 0.95f, 1f, 0.95f), new Vector2(size * 1.1f, size * 2.2f), size * 0.45f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), size * 2.2f);
        StartCoroutine(DissolveAtGround(charge));
        return charge;
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
        if (Fx.Play(Fx.SlashArc, Hands, Cyan, 2.6f) == null) ElementFx.Glow(Hands, Cyan, 1f, 3f, 0.2f);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        // Past the middle of her first bar she has his measure, and aims.
        bool aimed = secondPhase || HealthFraction < 0.5f;
        Vector3 aim = aimed ? (ronin.Bounds.center - Hands).normalized : Vector3.down;
        var fan = new List<Arrow>();
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(-36f, 36f, i / 4f);
            fan.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
        yield return Wait(0.3f);
    }

    // Phantom Blade Rain: ghost swords hang over six lanes with a thin warning line under each, then fall. At least
    // one lane stays empty. In her second phase there are seven lanes, two empty, and the swords fall in a ripple;
    // alone, eight.
    IEnumerator BladeRain()
    {
        if (Random.value < 0.5f) Speak(BossLine.BladeRain);
        int laneCount = alone ? 8 : secondPhase ? 7 : 6;
        int empty = secondPhase ? 2 : 1;
        List<float> lanes = Lanes(laneCount);
        // The empty lanes are picked at random; the ronin's own lane is never made the only way out on purpose.
        for (int i = 0; i < empty; i++) lanes.RemoveAt(Random.Range(0, lanes.Count));

        var tells = new List<SpriteRenderer>();
        var swords = new List<SpriteRenderer>();
        float hang = TopOfScreen - 1.4f;
        foreach (float x in lanes)
        {
            tells.Add(LaneWarning(x));
            swords.Add(SwordPicture(new Vector3(x, hang, 0), 0f));
            Puff(new Vector3(x, hang, 0), 1f);
        }
        Sfx.Play(Sfx.MistSwirl, 0.6f, 1f);
        RoamMeanwhile(RainWarning);
        yield return Brighten(tells, RainWarning);
        StopRoaming();

        for (int i = 0; i < lanes.Count; i++)
        {
            if (swords[i] != null)
            {
                transient.Remove(swords[i].gameObject);
                Destroy(swords[i].gameObject);
            }
            GhostSword(new Vector3(lanes[i], hang, 0), Vector3.down * 11f);
            if (secondPhase) yield return Wait(0.08f);
        }
        RemoveTells(tells);
        Sfx.Play(Sfx.GhostSwordFall, 0.9f, 1f);
        yield return Wait(0.4f);
    }

    // Moonlit Dash: a blue trail shows the way, she vanishes into mist and streaks across the field, and three
    // afterimages left along her path each loose a ghost sword straight down, one after the other.
    IEnumerator MoonlitDash()
    {
        if (Random.value < 0.4f) Speak(BossLine.MoonlitDash);
        float range = HalfWidth - 2f;
        float far = anchor.x < CameraShake.Center.x ? CameraShake.Center.x + range : CameraShake.Center.x - range;
        Vector3 from = anchor;
        Vector3 to = new Vector3(far, anchor.y, anchor.z);
        SpriteRenderer trail = Bar(from, to, 0.08f, Trail);
        for (float t = 0; t < 0.5f; t += Time.deltaTime * Tempo)
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
            GhostSword(at, Vector3.down * 10f);
            Sfx.Play(Sfx.GhostSwordFall, 0.6f, 1.2f);
            transient.Remove(ghost.gameObject);
            Fader.Fade(ghost, 0.3f);
            yield return Wait(0.25f);
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
    // the left and the outer row down and to the right, crossing in the middle of the field with gaps between.
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
            volley.Add(GhostSword(at, velocity));
        }
        IgnoreEachOther(volley);
    }

    // Mirror Moon: she takes one side of the field and leaves a reflection of herself standing on the other, mirrored
    // across the middle. Each looses an aimed fan of three crescents a beat apart, a crossfire with gaps between.
    IEnumerator MirrorMoon()
    {
        if (Random.value < 0.5f) Speak(BossLine.MirrorMoon);
        float side = anchor.x < CameraShake.Center.x ? -1f : 1f;
        yield return Glide(CameraShake.Center.x + side * HalfWidth * 0.45f);
        SpriteRenderer reflection = Linger();
        float mirrorX = CameraShake.Center.x * 2f - sprite.transform.position.x;
        reflection.transform.position = new Vector3(mirrorX, reflection.transform.position.y, reflection.transform.position.z);
        reflection.flipX = !sprite.flipX;
        Puff(reflection.bounds.center, 2f);
        Sfx.Play(Sfx.MistSwirl, 0.7f, 1.3f);
        Vector3 reflectionHands = reflection.bounds.center + Vector3.down * (reflection.bounds.size.y * 0.1f);
        ElementFx.Glow(Hands, Cyan, 0.7f, 2.4f, 0.6f, transform);
        ElementFx.Glow(reflectionHands, Cyan, 0.7f, 2.4f, 0.6f);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.25f);
        yield return Wait(alone ? 0.45f : 0.6f);

        PlayAction(art.attack);
        AimedFan(Hands);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.75f);
        yield return Wait(0.35f);
        if (reflection != null)
        {
            AimedFan(reflectionHands);
            Sfx.Play(Sfx.SwordSlash, 0.8f, 0.85f);
            transient.Remove(reflection.gameObject);
            Fader.Fade(reflection, 0.4f);
        }
        yield return Wait(0.3f);
    }

    // Three crescents from a point, the middle one at the ronin.
    void AimedFan(Vector3 from)
    {
        if (Fx.Play(Fx.SlashArc, from, Cyan, 2.2f) == null) ElementFx.Glow(from, Cyan, 1f, 2.6f, 0.2f);
        Vector3 aim = (ronin.Bounds.center - from).normalized;
        var fan = new List<Arrow>();
        for (int i = 0; i < 3; i++)
        {
            float angle = Mathf.Lerp(-16f, 16f, i / 2f);
            fan.Add(Crescent(from, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
    }

    // --- Parry-only draws ---

    // Moonsplitter Draw: she sheathes both blades and her flame drains into them while a sight line holds on the
    // ronin, then one great crescent is drawn that turns with him and cuts through cover and slashes. There is no
    // outrunning it: only a parry sends it back, and it breaks her stance.
    IEnumerator Moonsplitter()
    {
        Speak(BossLine.Moonsplitter);
        yield return SightLine(Hands, DrawAim, Frost);

        PlayAction(art.attack);
        Vector3 from = Hands;
        if (Fx.Play(Fx.SlashArc, from, Frost, 4f) == null) ElementFx.Glow(from, Frost, 1f, 4f, 0.25f);
        Sfx.Play(Sfx.SwordSlash, 1f, 0.55f);
        Sfx.Play(Sfx.BladeRing, 0.9f, 0.7f);
        CameraShake.Add(0.3f);
        Arrow cut = Shoot(from, (ronin.Bounds.center - from).normalized * DrawSpeed);
        Sprite[] frames = Fx.Frames(Fx.ShiroCrescent);
        Color halo = new Color(Frost.r, Frost.g, Frost.b, 0.6f);
        if (frames != null) cut.Restyle(frames, 14f, Color.white, new Vector2(4.4f, 2.8f), 0.9f, halo, 4.4f);
        else cut.Restyle(ProceduralSprites.Glow, Frost, new Vector2(4f, 1.4f), 0.9f, halo, 4.4f);
        cut.MakeUnbreakable();
        cut.MakeReflectable(transform);
        StartCoroutine(Pursue(cut, DrawSpeed));
        yield return Wait(0.5f);
    }

    // Fang of the Oath: Kuon howls and she crosses her blades, and their spirit leaves as one bolt in the shape of a
    // wolf's head that turns with the ronin. Parried, it goes back at Kuon, a heavy blow that dazes it; once Kuon
    // has fallen she casts it alone, and parried it breaks her stance instead.
    IEnumerator OathFang()
    {
        Speak(BossLine.OathFang);
        bool together = Guarded;
        if (together)
        {
            yield return wolf.Settle(WolfRest);
            wolf.PlayHowl();
            wolf.Glare(DrawAim);
            Sfx.Play(Sfx.WolfHowl, 0.8f, 1.1f);
        }
        PlayAction(art.parry);
        Vector3 Source() => together ? Vector3.Lerp(Hands, WolfMouth, 0.5f) : Hands;
        yield return SightLine(Source(), DrawAim, Cyan, together ? (System.Func<Vector3>)Source : null);

        Vector3 from = Source();
        Fx.Play(Fx.Impact, from, Cyan, 2.4f);
        Sfx.Play(Sfx.WolfGrowl, 1f, 0.9f);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.6f);
        CameraShake.Add(0.3f);
        Arrow fang = Shoot(from, (ronin.Bounds.center - from).normalized * DrawSpeed);
        Sprite[] frames = Fx.Frames(Fx.ShiroWolfCharge) ?? wolfCharge;
        Color halo = new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f);
        if (frames != null) fang.Restyle(frames, 14f, new Color(0.8f, 0.97f, 1f, 0.95f), new Vector2(2.2f, 3f), 0.8f, halo, 3.6f);
        else fang.Restyle(ProceduralSprites.Glow, new Color(0.7f, 0.95f, 1f, 0.95f), new Vector2(1.8f, 2.6f), 0.8f, halo, 3.6f);
        fang.MakeUnbreakable();
        fang.MakeReflectable(together ? wolf.transform : transform);
        StartCoroutine(Pursue(fang, DrawSpeed));

        // Kuon holds its ground at her side until the fang has struck and anything parried back has reached it.
        if (together)
        {
            for (float t = 0; fang != null && t < 3f; t += Time.deltaTime) yield return null;
            yield return Wait(1.4f);
        }
        else yield return Wait(0.5f);
    }

    // A sight line from her to the ronin that follows him, brightening and quickening, then holds still a moment
    // before the draw: the tell for a blow that must be parried.
    IEnumerator SightLine(Vector3 from, float seconds, Color colour, System.Func<Vector3> source = null)
    {
        ElementFx.Glow(from, colour, 0.9f, 3f, seconds, transform);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.8f);
        SpriteRenderer line = Bar(from, ronin.Bounds.center, 0.06f, new Color(colour.r, colour.g, colour.b, 0.3f));
        float rang = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            Vector3 start = source != null ? source() : from;
            Vector3 end = ronin.Bounds.center;
            Vector3 delta = end - start;
            line.transform.position = (start + end) / 2f + Vector3.back * 0.2f;
            line.transform.rotation = Quaternion.FromToRotation(Vector3.right, delta);
            line.transform.localScale = new Vector3(delta.magnitude, 0.06f + 0.08f * k, 1f);
            line.color = new Color(colour.r, colour.g, colour.b, 0.3f + 0.5f * k);
            if (k > 0.6f && t - rang > 0.12f)
            {
                rang = t;
                Sfx.Play(Sfx.BladeRing, 0.25f, 1.2f + k);
            }
            yield return null;
        }
        // The aim locks with a flash: this is the beat to parry on.
        line.color = Color.white;
        yield return Wait(0.18f);
        transient.Remove(line.gameObject);
        Destroy(line.gameObject);
    }

    // Steers a shot onto the ronin's lane as fast as he can run: it can't be outrun, only parried.
    IEnumerator Pursue(Arrow shot, float speed)
    {
        while (shot != null && ronin != null && shot.transform.position.y > ronin.Bounds.center.y)
        {
            float dx = ronin.Bounds.center.x - shot.transform.position.x;
            shot.Steer(new Vector3(Mathf.Clamp(dx * 6f, -ronin.speed * 1.1f, ronin.speed * 1.1f), -speed, 0));
            yield return new WaitForFixedUpdate();
        }
    }

    // Moonfall, her ultimate once Kuon has fallen: under the spirit moon three waves of ghost swords ripple down
    // across eight lanes, each leaving a pair empty, and each wave's pair one lane over from the last so one lane
    // always carries through. Then she stands spent, open to double damage.
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
        float hang = TopOfScreen - 1.4f;
        for (int wave = 0; wave < 3; wave++)
        {
            if (wave > 0)
            {
                int step = Random.value < 0.5f ? -1 : 1;
                if (gap + step < 0 || gap + step > lanes.Count - 2) step = -step;
                gap += step;
            }
            var tells = new List<SpriteRenderer>();
            var swords = new List<SpriteRenderer>();
            var falling = new List<float>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (i == gap || i == gap + 1) continue;
                falling.Add(lanes[i]);
                tells.Add(LaneWarning(lanes[i]));
                swords.Add(SwordPicture(new Vector3(lanes[i], hang, 0), 0f));
            }
            PlayAction(art.parry);
            Sfx.Play(Sfx.BladeRing, 0.7f, 0.9f + wave * 0.1f);
            yield return Brighten(tells, wave == 0 ? 0.8f : 0.6f);
            for (int i = 0; i < falling.Count; i++)
            {
                if (swords[i] != null)
                {
                    transient.Remove(swords[i].gameObject);
                    Destroy(swords[i].gameObject);
                }
                GhostSword(new Vector3(falling[i], hang, 0), Vector3.down * 13f);
            }
            RemoveTells(tells);
            Sfx.Play(Sfx.GhostSwordFall, 1f, 0.9f);
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


    // --- Phase 2: the wolf ---
    // Kuon's own moves (charge, howl, pack, Maw) run in Hunt beside hers, so they leave her movement to her.

    // Spirit Wolf Charge: the wolf melts into mist and reappears over the ronin's lane, growling, a warning line
    // under it; then it comes down the lane like a bolt and dissolves where it lands, and returns to her.
    IEnumerator WolfCharge()
    {
        if (Random.value < 0.5f) Speak(BossLine.WolfCharge);
        yield return wolf.Vanish();
        yield return Wait(0.25f);
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.2f, RightEdge - 1.2f);
        Vector3 perch = new Vector3(x, TopOfScreen - 1.6f, 0);
        yield return wolf.AppearAt(perch, true);
        Sfx.Play(Sfx.WolfGrowl, 0.9f, 1f);
        var tells = new List<SpriteRenderer> { LaneWarning(x) };
        yield return Brighten(tells, 0.9f);
        RemoveTells(tells);

        wolf.Hide();
        Charge(perch, 14f, 1.6f);
        Puff(perch, 1.6f);
        Sfx.Play(Sfx.MistSwirl, 0.7f, 0.8f);
        yield return Wait(0.5f);
        yield return wolf.AppearAt(WolfRest);
    }

    // Howl of the Departed: the wolf's eyes and jaws burn bright, then its howl sends seven orbs down in a wide
    // fan, and a second wave a beat later between the first's paths. Near her end a third wave follows.
    IEnumerator Howl()
    {
        if (Random.value < 0.5f) Speak(BossLine.Howl);
        yield return wolf.Settle(WolfRest);
        wolf.Glare(0.8f);
        ElementFx.Glow(WolfMouth, Cyan, 0.6f, 2.4f, 0.8f, wolf.transform);
        yield return Wait(0.8f);

        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 1f, 1f);
        CameraShake.Add(0.15f);
        int waves = HealthFraction < 0.3f ? 3 : 2;
        for (int wave = 0; wave < waves; wave++)
        {
            if (wave > 0) yield return Wait(0.6f);
            OrbFan(7, 60f, 4.5f, wave % 2 == 1);
        }
        yield return Wait(0.3f);
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
        yield return wolf.AppearAt(WolfRest);
    }

    // Phantom Wolf Pack: three smaller ghost wolves form over three of six lanes, each with its warning line,
    // and charge down one after another. Never the same three lanes twice running.
    IEnumerator WolfPack()
    {
        if (Random.value < 0.5f) Speak(BossLine.WolfPack);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 0.6f, 1.4f);
        List<float> lanes = Lanes(6);
        var picks = new List<int>();
        while (picks.Count < 3)
        {
            int lane = Random.Range(0, lanes.Count);
            if (!picks.Contains(lane) && !(lastPackLanes.Contains(lane) && Random.value < 0.7f)) picks.Add(lane);
        }
        lastPackLanes.Clear();
        foreach (int lane in picks) lastPackLanes.Add(lane);

        float perchY = TopOfScreen - 1.5f;
        var tells = new List<SpriteRenderer>();
        var pups = new List<SpriteRenderer>();
        foreach (int lane in picks)
        {
            Vector3 perch = new Vector3(lanes[lane], perchY, 0);
            tells.Add(LaneWarning(lanes[lane]));
            pups.Add(wolf.Silhouette(perch, 0.6f, transient));
            Puff(perch, 1.2f);
        }
        yield return Brighten(tells, 0.9f);

        for (int i = 0; i < picks.Count; i++)
        {
            if (pups[i] != null)
            {
                transient.Remove(pups[i].gameObject);
                Destroy(pups[i].gameObject);
            }
            if (tells[i] != null)
            {
                transient.Remove(tells[i].gameObject);
                Destroy(tells[i].gameObject);
            }
            Charge(new Vector3(lanes[picks[i]], perchY, 0), 12f, 1.1f);
            Sfx.Play(Sfx.WolfGrowl, 0.6f, 1.3f);
            yield return Wait(0.35f);
        }
        yield return Wait(0.3f);
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
        SpriteRenderer warning = Bar(new Vector3(x, WolfMouth.y, 0), new Vector3(x, GroundY, 0), 2.2f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f));
        wolf.Glare(1.4f);
        yield return GatherOrb(() => WolfMouth, 0.8f, () => false, warning);
        yield return SwallowOrb(warning, 0.2f);
        Drop(warning);
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
        yield return wolf.AppearAt(WolfRest);
    }

    // --- Kuon's Maw ---

    // Kuon's own ultimate, drawn like a tailed beast's bomb. It plants itself over the field and rears back, and motes
    // of spirit light, pale and deep, stream in from all round and spiral into an orb at its jaws that swells and
    // shakes the ground. The lane locks where the ronin stands as the orb is crushed to a white-hot point and
    // swallowed; a beat of silence, then the beam comes down that lane. Hit Kuon hard enough while it gathers and
    // the orb bursts in its own jaws.
    IEnumerator SpiritMaw()
    {
        yield return wolf.Vanish();
        // It stands on her row, clear of the HUD, over the ronin's side of the field.
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 2f, RightEdge - 2f);
        yield return wolf.AppearAt(new Vector3(x, anchor.y + 0.2f, 0), true);
        wolf.PlayMaw(MawGather + 0.3f);
        wolf.Glare(1.8f);
        Sfx.Play(Sfx.WolfGrowl, 1f, 0.7f);
        wolf.StartCoroutine(wolf.Grow(1.3f, 0.5f));
        int before = wolf.Health;
        yield return GatherOrb(() => WolfMouth, MawGather, () => wolf.IsBroken || before - wolf.Health >= MawBreakDamage, null);
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
            if (wolf.IsBroken) yield break;
            yield return wolf.Vanish();
            yield return wolf.AppearAt(WolfRest);
            yield break;
        }

        float lane = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.4f, RightEdge - 1.4f);
        SpriteRenderer warning = Bar(new Vector3(lane, WolfMouth.y, 0), new Vector3(lane, GroundY, 0), 2.8f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.2f));
        yield return SwallowOrb(warning, 0.35f);
        Drop(warning);
        yield return MawBeam(lane, 1.4f, 0.8f);
        Fx.Play(Fx.Explosion, new Vector3(lane, GroundY, 0), Cyan, 3f);
        CameraShake.Add(0.4f);
        wolf.PlayIdle();
        wolf.StartCoroutine(wolf.Grow(1f, 0.4f));
        yield return Wait(0.4f);
        yield return wolf.Vanish();
        yield return wolf.AppearAt(WolfRest);
    }

    // How long Kuon's Maw gathers, how much damage on Kuon meanwhile bursts the orb, and what the burst costs it.
    const float MawGather = 1.6f;
    const int MawBreakDamage = 6;
    const int MawBackfireDamage = 10;

    // The orb at Kuon's jaws while it gathers; null when none is out.
    SpriteRenderer orb, orbCore;
    // Set when the gather was cut short by burst() returning true, the orb bursting where it hung.
    bool orbBurst;

    // Motes of light and shadow spiral in from all round to an orb at `at` that swells as they arrive. A warning
    // bar, if given, flickers brighter as it grows.
    IEnumerator GatherOrb(System.Func<Vector3> at, float seconds, System.Func<bool> burst, SpriteRenderer warning)
    {
        orbBurst = false;
        RemoveOrb();
        orb = Glowing("SpiritOrb", ProceduralSprites.SoftGlow, 57);
        orbCore = Glowing("OrbCore", ProceduralSprites.Glow, 58);
        const int count = 28;
        var motes = new SpriteRenderer[count];
        var offsets = new Vector3[count];
        var delays = new float[count];
        var spins = new float[count];
        for (int i = 0; i < count; i++)
        {
            motes[i] = Glowing("Mote", ProceduralSprites.Glow, 56);
            motes[i].color = i % 2 == 0 ? new Color(Frost.r, Frost.g, Frost.b, 0f) : new Color(0.15f, 0.3f, 0.75f, 0f);
            motes[i].transform.localScale = Vector3.one * Random.Range(0.45f, 0.85f);
            offsets[i] = Random.insideUnitCircle.normalized * Random.Range(3.5f, 7f);
            delays[i] = Random.Range(0f, seconds * 0.6f);
            spins[i] = Random.Range(120f, 240f) * (Random.value < 0.5f ? -1f : 1f);
        }
        Sfx.Play(Sfx.LanternCharge, 0.9f, 0.8f);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 0.5f);
        float nextRumble = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            Vector3 centre = at() + Vector3.back * 0.4f;
            for (int i = 0; i < count; i++)
            {
                float m = Mathf.Clamp01((t - delays[i]) / (seconds * 0.4f));
                Color c = motes[i].color;
                c.a = m > 0f && m < 1f ? 0.9f : 0f;
                motes[i].color = c;
                if (c.a == 0f) continue;
                // Each mote spirals in, quickening as it nears the orb.
                motes[i].transform.position = centre + Quaternion.Euler(0, 0, spins[i] * m) * offsets[i] * (1f - m * m);
            }
            float size = Mathf.Lerp(0.3f, 2.8f, k) * (1f + 0.08f * Mathf.Sin(t * 40f));
            orb.transform.position = centre;
            orb.transform.localScale = Vector3.one * size * 1.6f;
            orb.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f);
            orbCore.transform.position = centre + Vector3.back * 0.05f;
            orbCore.transform.localScale = Vector3.one * size * 0.7f;
            orbCore.color = new Color(0.9f, 1f, 1f, 0.9f);
            if (warning != null) warning.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f + 0.25f * k * Mathf.PingPong(t * 6f, 1f));
            if (t >= nextRumble)
            {
                nextRumble = t + 0.12f;
                CameraShake.Add(0.03f + 0.05f * k);
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
            Fx.Play(Fx.Explosion, orb.transform.position, Cyan, 2.4f);
            RemoveOrb();
        }
    }

    // The orb crushes down to a white-hot point and is swallowed, and for a beat everything goes quiet.
    IEnumerator SwallowOrb(SpriteRenderer warning, float beat)
    {
        if (orb == null) yield break;
        Vector3 at = orb.transform.position;
        float from = orb.transform.localScale.x;
        Color start = orb.color;
        for (float t = 0; t < 0.3f; t += Time.deltaTime * Tempo)
        {
            float k = t / 0.3f;
            orb.transform.localScale = Vector3.one * Mathf.Lerp(from, 0.4f, k * k);
            orb.color = Color.Lerp(start, Color.white, k);
            orbCore.transform.localScale = Vector3.one * Mathf.Lerp(from * 0.45f, 0.3f, k * k);
            if (warning != null) warning.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f + 0.3f * k);
            yield return null;
        }
        RemoveOrb();
        Fx.Play(Fx.Impact, at, Color.white, 1.6f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 0.6f);
        wolf.Glare(beat + 0.3f);
        wolf.StartCoroutine(wolf.Gulp());
        yield return Wait(beat);
    }

    // The beam from Kuon's jaws down a lane; it burns the ronin while he stands within halfWidth of it.
    IEnumerator MawBeam(float x, float halfWidth, float seconds)
    {
        Vector3 from = WolfMouth;
        Vector3 to = new Vector3(x, GroundY, 0);
        float w = halfWidth / 1.1f;
        SpriteRenderer glow = Bar(from, to, 2.4f * w, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), 53);
        glow.sprite = ProceduralSprites.Glow;
        SpriteRenderer body = Bar(from, to, 1.1f * w, new Color(0.6f, 0.95f, 1f, 0.9f), 55);
        SpriteRenderer core = Bar(from, to, 0.35f * w, Color.white, 56);
        FxPlayer flare = Fx.Play(Fx.ShiroBeamFlare, from, Color.white, 2f * w, wolf.transform, 57);
        if (flare != null) transient.Add(flare.gameObject);
        Sfx.PlayLoop(Sfx.SpiritBeam, 0.8f, 1f);
        CameraShake.Add(0.4f * w);
        float nextSpark = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float flicker = 1f + 0.15f * Mathf.Sin(Time.time * 50f);
            if (body != null) body.transform.localScale = new Vector3(body.transform.localScale.x, 1.1f * w * flicker, 1f);
            if (Mathf.Abs(ronin.Bounds.center.x - x) < halfWidth) ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.1f;
                Fx.Play(Fx.Impact, to, Cyan, 0.9f * w);
            }
            yield return null;
        }
        Sfx.Stop(Sfx.SpiritBeam);
        foreach (SpriteRenderer part in new[] { glow, body, core }) Drop(part);
        if (flare != null) flare.Stop();
    }

    // A code-drawn glow the current attack owns.
    SpriteRenderer Glowing(string name, Sprite shape, int order)
    {
        var glow = new GameObject(name).AddComponent<SpriteRenderer>();
        glow.sprite = shape;
        glow.sortingOrder = order;
        glow.color = Color.clear;
        transient.Add(glow.gameObject);
        return glow;
    }

    void Drop(SpriteRenderer thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    void RemoveOrb()
    {
        Drop(orb);
        Drop(orbCore);
        orb = orbCore = null;
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

    // When her first bar empties she staggers to a knee and her blades go dark; mist spreads over the field, a
    // howl sounds far off, two eyes open behind her and the wolf takes shape in a burst of blue flame. She rises
    // wreathed in it, with her second bar.
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
        yield return new WaitForSeconds(0.9f);

        yield return wolf.OpenEyes(WolfRest, 0.6f);
        CameraShake.Add(0.6f);
        Fx.Play(Fx.Explosion, WolfRest, Cyan, 2.4f);
        Debris.BurstSprites(WolfRest, art.deathParticles, Cyan, 24, 1.2f, 8f);
        Sfx.Play(Sfx.WolfHowl, 1f, 1f);
        Sfx.Play(Sfx.FireBlast, 0.8f, 1.3f);
        wolf.SetPresence(SpiritWolf.Presence.Present);
        yield return wolf.AppearAt(WolfRest);

        bodyTint = new Color(0.85f, 0.96f, 1f);
        WearPhaseArt();
        if (phaseDash != null) dashFrames = phaseDash;
        AddAura();
        PlayIdle();
        // Kuon takes the field as a foe of its own, with its own bar, and wards her while it stands.
        wolf.Arm();
        AddWard();
        WolfJoined?.Invoke(wolf);
        movesSinceParry = 1;
        movesSinceEclipse = 0;
        // Her oath: a title card with her and the wolf, over which she speaks; without its art, a subtitle.
        if (PhaseCard != null) yield return PhaseCard(this);
        else
        {
            Speak(BossLine.Phase);
            yield return new WaitForSeconds(2.2f);
        }
        breaking = false;
    }

    // Blue spirit flame licking up from her feet and a cold glow pulsing behind her.
    void AddAura()
    {
        Vector3 feet = new Vector3(Centre.x, sprite.bounds.min.y + sprite.bounds.size.y * 0.12f, 0);
        auraFlames = Fx.Play(Fx.Aura(Element.Water), feet, new Color(0.5f, 0.9f, 1f, 0.9f), 3.2f, transform, sprite.sortingOrder - 1);
        auraGlow = Kunai.CreateGlow(sprite.transform, 2.2f, sprite.sortingOrder - 2, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
        StartCoroutine(PulseAura(auraGlow));
    }

    IEnumerator PulseAura(SpriteRenderer glow)
    {
        while (glow != null && !IsDead)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.5f);
            glow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.25f + 0.3f * pulse);
            glow.transform.localScale = Vector3.one * (2f + 0.4f * pulse);
            yield return null;
        }
        if (glow != null) Destroy(glow.gameObject);
    }

    // Kuon's ward: a pale shimmer behind her, wider than her aura, that flickers while it holds and is gone once
    // Kuon falls.
    void AddWard()
    {
        wardGlow = Kunai.CreateGlow(sprite.transform, 2.8f, sprite.sortingOrder - 3, Color.clear);
        StartCoroutine(PulseWard());
    }

    IEnumerator PulseWard()
    {
        while (wardGlow != null && !IsDead)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            float a = SuperArmor ? 0.2f + 0.15f * pulse : 0f;
            wardGlow.color = new Color(Frost.r, Frost.g, Frost.b, a);
            wardGlow.transform.localScale = Vector3.one * (2.9f + 0.3f * pulse);
            yield return null;
        }
        if (wardGlow != null) Destroy(wardGlow.gameObject);
    }

    // Her fall: the swords drop from her hands, she sinks to a knee and her flames fade; the wolf comes to stand
    // by her and she rests a hand on its head; it dissolves into light, she speaks her last, and follows it.
    protected override IEnumerator Fall()
    {
        StopRoaming();
        StopHunting();
        if (wolf != null) wolf.Disarm();
        if (wardGlow != null) Destroy(wardGlow.gameObject);
        ultimate = false;
        exposed = false;
        Sfx.Stop(Sfx.SpiritBeam);
        if (auraFlames != null) auraFlames.Stop();
        if (auraGlow != null) Destroy(auraGlow.gameObject);
        CameraShake.Add(0.6f);
        Debris.BurstSprites(Centre, art.deathParticles, Cyan, 24, 1f, 8f);
        Sfx.Play(Sfx.BladeRing, 1f, 0.6f);
        DropBlade(Hands + Vector3.left * 0.6f, -1f);
        DropBlade(Hands + Vector3.right * 0.6f, 1f);
        if (bladeGlow != null) Fader.Fade(bladeGlow, 1.5f);
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
            c.a = 1f - k;
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
// burning cyan eyes. A faint silhouette behind her in her first phase; once she calls it, it rests beside her and
// vanishes into mist to attack from wherever it reappears. Everything it does to the ronin is an Arrow it throws,
// but from her phase break it can be hit while it stands in sight, with a bar of its own; broken, it fades to a
// shadow at her side until her fall.
[RequireComponent(typeof(Rigidbody))]
public class SpiritWolf : MonoBehaviour, IHittable
{
    public enum Presence { Silhouette, Present }

    public const string Title = "KUON";
    public const string Epithet = "The Shrine Guardian";
    public const int StartingHealth = 80;
    // A parried Fang of the Oath does no damage by Damage.Of; on Kuon it lands as a heavy blow and dazes it.
    const int ReflectedDamage = 20;
    const float DazeSeconds = 2f;
    const int SamuraiLayer = 7;

    static readonly Color Body = new Color(0.6f, 0.9f, 1f);
    static readonly Color EyeColour = new Color(0.4f, 1f, 1f);

    public event System.Action<Vector3, KillSource> Damaged;
    public event System.Action Broken;

    public int Health { get; private set; } = StartingHealth;
    public float HealthFraction => (float)Health / StartingHealth;
    // In the fight as a foe of its own, from her phase break until her fall.
    public bool Armed { get; private set; }
    public bool IsBroken { get; private set; }
    public bool Dazed => Time.time < dazedUntil;
    // Only while it stands in plain sight: not in mist, not as the charge coming down a lane.
    public bool Targetable => Armed && !IsBroken && !hidden && shown > 0.5f;
    public Vector3 Centre => sprite.bounds.center;
    float dazedUntil;
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
        Health = StartingHealth;
        IsBroken = false;
        Armed = true;
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

    // Keeps to its rest beside her as she glides.
    void Follow()
    {
        if (following == null) following = StartCoroutine(Following());
    }

    IEnumerator Following()
    {
        while (true)
        {
            if (!hidden && presence == Presence.Present)
            {
                Vector3 rest = owner.WolfRest;
                transform.position = Vector3.Lerp(transform.position, rest, 1f - Mathf.Exp(-Time.deltaTime * 4f));
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

    // Back beside her without ceremony, when a stagger cuts a move short.
    public void Recall()
    {
        StopAllCoroutines();
        following = null;
        hidden = false;
        shown = shownTarget;
        grown = 1f;
        transform.position = owner.WolfRest;
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
        // A dazed wolf reels where it stands.
        sprite.transform.localRotation = Dazed && !IsBroken ? Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 18f) * 8f) : Quaternion.identity;
        if (hidden) return;
        shown = Mathf.MoveTowards(shown, shownTarget, Time.deltaTime * 1.5f);
        Apply();
    }

    void Apply()
    {
        float a = hidden ? 0f : shown;
        float flicker = 0.9f + 0.1f * Mathf.Sin(Time.time * 7f);
        sprite.color = new Color(Body.r, Body.g, Body.b, a * 0.78f * flicker);
        transform.localScale = Vector3.one * scale * grown;
        glow.color = new Color(0.3f + 0.5f * glare, 0.9f, 1f, a * (0.3f + 0.4f * glare));
        if (eyes == null) return;
        foreach (SpriteRenderer eye in eyes) eye.color = new Color(EyeColour.r, EyeColour.g, EyeColour.b, Mathf.Min(1f, a * 1.6f + glare));
    }
}
