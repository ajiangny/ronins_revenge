using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The things a boss says out loud; GameManager plays the matching voice clip and subtitle. Intro, Phase and
// Defeat are full lines; the rest are short barks that never cut off a line still playing (bar Stagger).
public enum BossLine { Intro, Phase, Defeat, Hurt, Stagger, SmokeRings, RosaryLash, Iaido, RosaryBloom,
    KasaWheel, Lattice, HourStrikes, PlumbLines, HeartLantern, Midnight,
    CrescentSlash, BladeRain, MoonlitDash, BladeBarrage, WolfCharge, Howl, TwinAssault, WolfPack, EclipseFang,
    // Shirogane's last words, after her wolf has gone; a full line like Defeat.
    DefeatFinal,
    // Shirogane's later barks: Kuon broken, and her mirrored and parry-only moves.
    WolfFallen, MirrorMoon, Moonsplitter, OathFang,
    // Kagetsune's move callouts, his summons, and the summoned souls' own lines (spoken in their voices).
    CofferSlam, TollOfFealty, OfudaDecree, SoulWail, LastToll, WraithSwarm, BrokenChains, SoulCoffin, ThousandBells,
    SummonBenibana, SummonUtsusemi, SummonShirogane, EchoBenibana, EchoUtsusemi, EchoShirogane,
    // His line over the ronin when he falls to him.
    KilledOni,
    // Shirogane's word to Kuon when she falls before it does.
    KuonEnraged }

// A warlord fought at the end of a stage: one big target with an HP bar that stands in the top band and attacks
// in telegraphed patterns. Subclasses write the fight as a coroutine; this class handles the body, damage,
// staggers, frost and movement. Built in code, so a boss needs no prefab.
[RequireComponent(typeof(Rigidbody))]
public abstract class Boss : MonoBehaviour, IHittable
{
    const int SamuraiLayer = 7;
    const float BobHeight = 0.18f;
    const float BlinkTime = 0.2f;
    // A fight that hasn't started a new move in this long has stalled and is restarted.
    const float StallSeconds = 8f;
    // The shortest gap between her pained gasps, so a flurry of hits doesn't become a stream of them.
    const float HurtLineGap = 2.5f;

    public event Action<Vector3, KillSource> Damaged;
    // Raised as the bar refills during a phase break.
    public event Action Healed;
    public event Action Defeated;
    // Raised once the phase break is over and the second phase starts fighting.
    public event Action SecondPhaseBegan;
    public event Action<Boss, BossLine> Spoke;
    // Raised when a summoned soul is taken off the field without being cut down.
    public event Action Banished;

    // Set by the GameManager for a boss with phase-two portraits: plays a title card for the new phase (its own
    // art, epithet and the Phase line) and finishes when the card has. Null leaves the phase line as a subtitle.
    public static System.Func<Boss, IEnumerator> PhaseCard;
    // Set by the GameManager: a brief freeze of the whole game, for a blow that deserves weight (a parry-only move
    // sent home).
    public static System.Action<float> HitStop;
    // Set by the GameManager: renames the boss bar mid-fight (a partner joins, or one of a pair falls).
    public static System.Action<string> RenameBar;
    // Set by the GameManager: Poguru pops out of the ronin's wisp to call a warning (one of his lines by id, or none)
    // and hovers over a spot to point it out, or at the ronin's shoulder without one; PoguruRest sends him back in.
    public static System.Action<string, Vector3?> PoguruWarn;
    public static System.Action PoguruRest;
    // Set by the GameManager: raises a boss's soul out of Kagetsune's coffin at the given x, at a share of its health,
    // to fight beside him. It enters and begins on its own; null when it can't be raised.
    public static System.Func<BossKind, float, Boss> Summon;

    public abstract string Title { get; }
    // The name on the bar from the phase card on (Shirogane shares hers with Kuon).
    public virtual string PhaseTitle => Title;
    public abstract string Epithet { get; }
    public abstract Color Colour { get; }

    // The subtitle for a line, or null when the boss has nothing to say there.
    public virtual string Line(BossLine line) => null;

    // The subtitle for one take of a line recorded several ways; take 0 is the line's own.
    public virtual string Line(BossLine line, int take) => Line(line);

    // Who says a line, for its subtitle: the boss, or a soul it has summoned to speak for itself.
    public virtual string Speaker(BossLine line) => Title;
    public virtual Color SpeakerColour(BossLine line) => Colour;

    protected void Speak(BossLine line)
    {
        Spoke?.Invoke(this, line);
    }

    readonly Dictionary<BossLine, float> lastBarked = new Dictionary<BossLine, float>();

    // A move callout that stays quiet if the same line was called within cooldown seconds, so a move she
    // repeats doesn't become a refrain. The first call of the fight always speaks.
    protected void Bark(BossLine line, float cooldown)
    {
        if (lastBarked.TryGetValue(line, out float at) && Time.time - at < cooldown) return;
        lastBarked[line] = Time.time;
        Speak(line);
    }

    public int Health { get; private set; }
    // Iron Generals: every warlord's bars are this much longer (Oaths).
    public static float HealthScale = 1f;
    public int MaxHealth { get; private set; }
    // Damage let through super armour that hasn't yet added up to a whole hit point.
    float armorChip;
    // The shadow slashes' half points, likewise.
    float shadowChip;
    public float HealthFraction => (float)Health / MaxHealth;
    // What the HUD bar shows: her own health, unless she shares the bar with a partner (Shirogane and Kuon).
    public virtual float BarFraction => HealthFraction;
    // A bar shown in two halves (Shirogane and Kuon): each half's fraction, or false for one plain bar.
    public virtual bool SplitBar(out float left, out float right)
    {
        left = right = 0f;
        return false;
    }
    // What the bar refills to at the phase break; most bosses come back at full strength.
    protected virtual int SecondPhaseHealth => MaxHealth;
    public bool IsDead { get; private set; }
    // Beaten while a partner fights on: out of the fight, can't be hit or targeted, but not yet fallen.
    public bool Down { get; private set; }
    // Bosses with a second phase have two bars: emptying the first starts a phase break that refills it.
    protected virtual bool HasSecondPhase => false;
    public bool SecondPhase { get; private set; }
    // A soul Kagetsune has dragged out of the coffin: it fights with its first phase only (an empty bar is its end,
    // not a phase break) and falls as a soul, without last words or its own defeat scene.
    public bool Summoned { get; set; }
    // True from the moment the first bar empties until phase 2 begins; nothing hurts the boss meanwhile.
    protected bool PhaseBreaking { get; private set; }
    // How long the bar takes to refill, after the break's first beat.
    const float RefillDelay = 0.4f;
    const float RefillSeconds = 1.2f;
    public Vector3 Centre => sprite.bounds.center;

    protected Rigidbody body;
    protected SpriteRenderer sprite;
    protected SpriteAnimator animator;
    protected CharacterArt art;
    // Its transformed frames, worn from the phase break on; null keeps the first art.
    CharacterArt phaseArt;
    protected Ronin ronin;
    protected Arrow arrowPrefab;
    protected Vector3 anchor;
    protected Color bodyTint = Color.white;
    // Telegraphs and hazards the current attack has out; cleared when the attack is interrupted.
    protected readonly List<GameObject> transient = new List<GameObject>();

    float frozenUntil;
    float staggeredUntil;
    float nextHurtLine;
    float age;
    Coroutine fight;
    bool fighting;
    float lastBeat;

    // The stance meter: every hit fills it a little, and a full meter breaks her stance. Left alone it drains.
    public float Poise { get; private set; }
    // Raised whenever the stance meter moves, for the HUD.
    public event Action<float> PoiseChanged;
    float lastPoiseAt;
    const float PoiseDecayDelay = 3f;
    const float PoiseDecayRate = 0.2f;

    protected bool Staggered => Time.time < staggeredUntil;
    public bool Frozen => Time.time < frozenUntil;
    // How fast the boss's clock runs: a freeze or a stagger stops her.
    protected float Tempo => Frozen || Staggered ? 0f : 1f;
    protected float HalfWidth => Camera.main.orthographicSize * Camera.main.aspect;

    public static T Create<T>(CharacterArt art, float scale, int health, Vector3 position, Color tint) where T : Boss
    {
        var go = new GameObject(typeof(T).Name) { layer = SamuraiLayer };
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var hitbox = go.AddComponent<CapsuleCollider>();
        hitbox.direction = 1;

        var view = new GameObject("Sprite") { layer = SamuraiLayer };
        view.transform.SetParent(go.transform, false);
        var renderer = view.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 5;
        var animator = view.AddComponent<SpriteAnimator>();

        T boss = go.AddComponent<T>();
        boss.body = body;
        boss.sprite = renderer;
        boss.animator = animator;
        boss.art = art;
        boss.anchor = position;
        boss.bodyTint = tint;
        boss.Health = boss.MaxHealth = Mathf.RoundToInt(health * HealthScale);
        renderer.color = tint;
        animator.Play(art.idle, true, null, art.idleFps);
        // Centre the art on the body, and fit an upright capsule to the figure inside the canvas padding.
        Bounds frame = renderer.sprite.bounds;
        view.transform.localPosition = -frame.center;
        hitbox.radius = 0.45f * frame.extents.x;
        hitbox.height = 1.4f * frame.extents.y;
        return boss;
    }

    // Glides down from where she was created to her post, then waits for Begin.
    public IEnumerator Enter(Vector3 post, float seconds)
    {
        Vector3 from = anchor;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            anchor = Vector3.Lerp(from, post, Mathf.SmoothStep(0, 1, t / seconds));
            yield return null;
        }
        anchor = post;
    }

    // A set piece played once she has descended, before her title card and before the fight begins.
    public virtual IEnumerator Entrance()
    {
        yield break;
    }

    public void Begin(Ronin target, Arrow arrows)
    {
        ronin = target;
        arrowPrefab = arrows;
        fighting = true;
        RestartFight();
    }

    // Stops the fight where it stands (the ronin fell).
    public void Halt()
    {
        fighting = false;
        StopAllCoroutines();
        ClearTransient();
        ClearLaunched();
        OnCleared();
    }

    protected abstract IEnumerator Fight();

    // Called when the fight is torn down for good (a halt or her fall), for anything a subclass keeps outside
    // `transient`.
    protected virtual void OnCleared() { }

    // In her second phase, an empty bar puts her Down instead of killing her while this is true (a partner still
    // fights); she is finished later with Defeat.
    protected virtual bool HoldOnAtZero => false;

    // She has just gone Down: her fight is stopped and her hitbox is off.
    protected virtual void OnDowned() { }

    // Called after every hit that leaves the boss standing.
    protected virtual void OnDamaged() { }

    // The phase-break cutscene, played while the bar refills; the boss is invincible until it ends.
    protected virtual IEnumerator PhaseBreak()
    {
        yield return new WaitForSeconds(RefillDelay + RefillSeconds);
    }

    // Called when a reflected strike breaks the boss's stance, for its own reaction on top of the wobble.
    protected virtual void OnStaggered() { }

    protected void RestartFight()
    {
        if (fight != null) StopCoroutine(fight);
        lastBeat = Time.time;
        fight = fighting && !IsDead && !Down ? StartCoroutine(Fight()) : null;
    }

    // Fight loops call this each time they start a move, to show the watchdog they're alive.
    protected void Beat()
    {
        lastBeat = Time.time;
    }

    // While true, nothing breaks its stance: no stagger, no freeze. Hits deal only ArmorDamageTaken of their damage.
    protected virtual bool SuperArmor => false;

    // The share of each hit's damage that gets through super armour.
    protected virtual float ArmorDamageTaken => 1f;

    // The spark a hit throws off super armour: Utsusemi's gold, Shirogane's spirit ward.
    protected virtual Color ArmorSpark => new Color(1f, 0.85f, 0.4f);

    // True while a subclass is in a set piece (a phase break) and isn't expected to beat.
    protected virtual bool Busy => false;

    // Extra damage taken while a weak spot is open (Utsusemi with its hat off).
    protected virtual int DamageMultiplier => 1;

    // How long a broken stance lasts.
    protected virtual float StaggerSeconds => 2f;

    // How much of each hit's stance damage the boss feels; below 1 she takes longer to stagger. Second forms hold firmer.
    protected virtual float StanceResistance => SecondPhase ? 0.85f : 1f;

    // How long the fall takes before the stage's boon is offered over it.
    public virtual float DefeatSeconds => 2.2f;

    // An exception inside an attack silently ends its coroutine; rather than stand idle for the rest of the
    // fight, the boss notices it has stopped moving and starts over.
    void Update()
    {
        if (Poise > 0f && Time.time - lastPoiseAt > PoiseDecayDelay)
            SetPoise(Poise - PoiseDecayRate * Time.deltaTime);

        if (!fighting || IsDead || Down || PhaseBreaking || Busy || Staggered || Frozen)
        {
            lastBeat = Time.time;
            return;
        }
        if (Time.time - lastBeat > StallSeconds)
        {
            Debug.LogWarning($"{name}'s fight stalled; restarting it.");
            ClearTransient();
            RestartFight();
        }
    }

    // Cuts the current attack short; the fight starts over once the boss can act again.
    protected void Interrupt()
    {
        if (fight != null) StopCoroutine(fight);
        fight = null;
        ClearTransient();
        if (!IsDead) PlayIdle();
    }

    // An attack's parts that have already left the boss (weights swinging, a chain slammed down): a stagger breaks
    // its stance but not these, which play out on their own. Only a halt or its fall clears them.
    readonly List<GameObject> launched = new List<GameObject>();

    // Moves something out of `transient`, so a stagger no longer clears it.
    protected void Detach(GameObject thing)
    {
        if (thing == null) return;
        transient.Remove(thing);
        launched.Add(thing);
    }

    // Runs the rest of an attack that has already been launched on its own, outside the fight loop, so a stagger
    // doesn't cut it short. Yield on it to wait for it while nothing interrupts. Launched parts keep real time (not
    // boss time): the daze is the boss's, not its attack's. Detach whatever they draw.
    protected Coroutine Launch(IEnumerator rest) => StartCoroutine(rest);

    void ClearLaunched()
    {
        foreach (GameObject thing in launched)
        {
            if (thing != null) Destroy(thing);
        }
        launched.Clear();
    }

    protected void ClearTransient()
    {
        foreach (GameObject thing in transient)
        {
            if (thing != null) Destroy(thing);
        }
        transient.Clear();
    }

    // Waits in boss time, so a stagger or freeze holds it.
    protected IEnumerator Wait(float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            yield return null;
        }
    }

    // A one-shot clip (a slash, a puff of the pipe) that hands back to the idle loop.
    protected void PlayAction(Sprite[] clip)
    {
        if (IsDead || clip == null || clip.Length == 0) return;
        animator.Play(clip, false, PlayIdle, art.actionFps);
    }

    // The loop the boss rests in; a subclass can swap it for a different look (Utsusemi without its hat).
    protected virtual Sprite[] IdleClip => art.idle;

    public void SetPhaseArt(CharacterArt awakened)
    {
        phaseArt = awakened;
    }

    // Puts on the second-phase art, if there is one. Phase breaks call it before their first PlayIdle.
    protected void WearPhaseArt()
    {
        if (phaseArt != null) art = phaseArt;
    }

    protected void PlayIdle()
    {
        animator.Play(IdleClip, true, null, art.idleFps);
    }

    void FixedUpdate()
    {
        if (IsDead) return;
        age += Time.fixedDeltaTime * Mathf.Max(Tempo, 0.2f);
        body.MovePosition(anchor + Vector3.up * (Mathf.Sin(age * 2.4f) * BobHeight));
        sprite.color = Frozen ? Color.Lerp(bodyTint, new Color(0.55f, 0.8f, 1f), 0.6f) : bodyTint;
    }

    // A dash to a new spot that leaves a trail of fading afterimages along the way.
    protected IEnumerator BlinkTo(float x, Color trail)
    {
        Vector3 from = anchor;
        Vector3 to = new Vector3(x, anchor.y, anchor.z);
        Sfx.Play(Sfx.ParrySwing, 0.5f, 1.5f);
        int ghosts = 0;
        for (float t = 0; t < BlinkTime; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            float k = t / BlinkTime;
            if (k >= ghosts / 3f && ghosts < 3)
            {
                Color fading = trail;
                fading.a *= 1f - ghosts * 0.25f;
                Afterimage(fading);
                ghosts++;
            }
            anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, k));
            yield return null;
        }
        anchor = to;
    }

    // Shakes the body in place, building to `strength` (world units) and easing off at the end: a stagger, a seizing
    // machine. Rotation and offset are restored after, so the art lands back where it stood.
    protected IEnumerator Tremble(float seconds, float strength, float twist = 4f)
    {
        Transform view = sprite.transform;
        Vector3 rest = view.localPosition;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.PI * t / seconds);
            Vector2 jolt = UnityEngine.Random.insideUnitCircle * strength * k;
            view.localPosition = rest + (Vector3)jolt / transform.lossyScale.x;
            view.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-twist, twist) * k);
            yield return null;
        }
        view.localPosition = rest;
        view.localRotation = Quaternion.identity;
    }

    protected void Afterimage(Color color)
    {
        Afterimage(sprite, color);
    }

    // A fading copy of any sprite where it stands now: dash trails, a falling weight's streak.
    protected static void Afterimage(SpriteRenderer source, Color color)
    {
        var ghost = new GameObject("Afterimage").AddComponent<SpriteRenderer>();
        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;
        ghost.sprite = source.sprite;
        ghost.flipX = source.flipX;
        ghost.sortingOrder = source.sortingOrder - 1;
        ghost.color = color;
        Fader.Fade(ghost, 0.4f);
    }

    // A random x in the top band, at least minStep away from where the boss stands now.
    protected float RandomX(float minStep)
    {
        float centre = CameraShake.Center.x;
        float range = HalfWidth - 2.5f;
        for (int i = 0; i < 8; i++)
        {
            float x = centre + UnityEngine.Random.Range(-range, range);
            if (Mathf.Abs(x - anchor.x) >= minStep) return x;
        }
        return Mathf.Clamp(centre * 2f - anchor.x, centre - range, centre + range);
    }

    // The field's edges, measured from the camera's resting spot so screen shake doesn't move them.
    protected float TopOfScreen => CameraShake.Center.y + Camera.main.orthographicSize + 0.5f;
    protected float GroundY => ronin.Feet.y - 0.3f;
    protected float LeftEdge => CameraShake.Center.x - HalfWidth;
    protected float RightEdge => CameraShake.Center.x + HalfWidth;

    // A spot minStep to maxStep from where the boss stands, kept inside the top band; it turns back from an edge.
    protected float NearX(float minStep, float maxStep)
    {
        float range = HalfWidth - 2.5f;
        float lo = CameraShake.Center.x - range, hi = CameraShake.Center.x + range;
        float step = UnityEngine.Random.Range(minStep, maxStep) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        float x = anchor.x + step;
        if (x < lo || x > hi) x = anchor.x - step;
        return Mathf.Clamp(x, lo, hi);
    }

    // Positions of n lanes spread evenly across the field, clear of the edges.
    protected List<float> Lanes(int count)
    {
        var lanes = new List<float>(count);
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        for (int i = 0; i < count; i++) lanes.Add(Mathf.Lerp(lo, hi, (i + 0.5f) / count));
        return lanes;
    }

    // Steers a shot onto the ronin's lane as fast as he can run: it can't be outrun, only parried. A blade (`face`)
    // turns to point where it's going as it curves, so a crescent always leads with its edge; a bell or coffin
    // doesn't tilt.
    protected IEnumerator Pursue(Arrow shot, float speed, bool face = true)
    {
        while (shot != null && ronin != null && shot.transform.position.y > ronin.Bounds.center.y)
        {
            float dx = ronin.Bounds.center.x - shot.transform.position.x;
            Vector3 velocity = new Vector3(Mathf.Clamp(dx * 6f, -ronin.speed * 1.1f, ronin.speed * 1.1f), -speed, 0);
            if (face) shot.Turn(velocity);
            else shot.Steer(velocity);
            yield return new WaitForFixedUpdate();
        }
    }

    // Beams are measured every frame they burn, so their hits go into one shared buffer.
    static readonly RaycastHit[] BeamHits = new RaycastHit[32];

    // Where a beam from `from` towards `ground` stops: on the nearest blocker in the way, or the ground.
    protected static Vector3 BeamEnd(Vector3 from, Vector3 ground, out Blocker cover)
    {
        cover = null;
        Vector3 delta = ground - from;
        float nearest = delta.magnitude;
        int count = Physics.RaycastNonAlloc(from, delta.normalized, BeamHits, delta.magnitude);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = BeamHits[i];
            if (hit.distance < nearest && hit.collider.TryGetComponent(out Blocker blocker))
            {
                nearest = hit.distance;
                cover = blocker;
            }
        }
        return from + delta.normalized * nearest;
    }

    // Where a beam from `from` to `end` crosses the ronin's height, it burns him (unless cover stopped it above him).
    protected void HitIfCrossed(Vector3 from, Vector3 end)
    {
        float y = ronin.Bounds.center.y;
        if (end.y > y + 0.2f) return;
        float beamX = Mathf.Lerp(from.x, end.x, Mathf.InverseLerp(from.y, end.y, y));
        if (Mathf.Abs(ronin.Bounds.center.x - beamX) < 0.45f) ronin.TakeHit(new Vector3(beamX, y, 0));
    }

    // Lays a bar along from-to at the given width (beams redrawn every frame).
    protected static void Stretch(SpriteRenderer bar, Vector3 from, Vector3 to, float width)
    {
        if (bar == null) return;
        Vector3 delta = to - from;
        bar.transform.position = (from + to) / 2f + Vector3.back * 0.2f;
        bar.transform.rotation = delta.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.right, delta) : Quaternion.identity;
        bar.transform.localScale = new Vector3(delta.magnitude, width, 1f);
    }

    // A thin bar between two points, drawn above the action, for telegraphs and chains.
    protected SpriteRenderer Bar(Vector3 from, Vector3 to, float width, Color color, int order = 55)
    {
        var bar = new GameObject("Bar").AddComponent<SpriteRenderer>();
        bar.sprite = ProceduralSprites.White;
        bar.color = color;
        bar.sortingOrder = order;
        Vector3 delta = to - from;
        bar.transform.position = (from + to) / 2f + Vector3.back * 0.2f;
        bar.transform.rotation = Quaternion.FromToRotation(Vector3.right, delta);
        bar.transform.localScale = new Vector3(delta.magnitude, width, 1f);
        transient.Add(bar.gameObject);
        return bar;
    }

    // Fires one of the arrow prefab's shots from position with the given velocity.
    protected Arrow Shoot(Vector3 position, Vector3 velocity)
    {
        Arrow arrow = Instantiate(arrowPrefab, position, Quaternion.identity);
        arrow.Launch(velocity, 2.5f);
        // A summoned soul's shots and the Emperor's skulls share the field: they fly through each other.
        if (Summoned) arrow.PassesShots = true;
        return arrow;
    }

    // Shots fired together start out overlapping, and arrows collide with each other, so without this a volley
    // destroys itself the moment it's fired.
    protected static void IgnoreEachOther(List<Arrow> volley)
    {
        for (int i = 0; i < volley.Count; i++)
        {
            if (volley[i] == null) continue;
            for (int j = 0; j < i; j++) volley[i].IgnoreCollisionsWith(volley[j]);
        }
    }

    public void Freeze(float seconds)
    {
        if (SuperArmor || PhaseBreaking || Down) return;
        frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
    }

    // Broken stance: the boss stops dead and takes double damage for a moment.
    // Without the cry, for a stagger the boss has her own line for (Shirogane when her wolf falls).
    public void Stagger(float seconds, bool cry = true)
    {
        if (IsDead || Down || SuperArmor || PhaseBreaking) return;
        Interrupt();
        SetPoise(0f);
        staggeredUntil = Time.time + seconds;
        PlayAction(art.hit);
        Sfx.Play(Sfx.BossStagger);
        if (cry) Speak(BossLine.Stagger);
        OnStaggered();
        nextHurtLine = Time.time + seconds;
        StartCoroutine(RecoverFromStagger(seconds));
    }

    // A blow that lands on her stance but not her health (Utsusemi's parried sword chop): a full meter still staggers.
    protected void AddStance(float amount)
    {
        if (AddPoise(amount)) Stagger(StaggerSeconds);
    }

    // Fills the stance meter by `amount` (scaled by StanceResistance); true once it's full and her stance should break.
    // Super armour, a stagger already underway, a phase break or going down hold it where it is.
    bool AddPoise(float amount)
    {
        if (amount <= 0f || SuperArmor || Staggered || PhaseBreaking || Down || IsDead) return false;
        lastPoiseAt = Time.time;
        SetPoise(Poise + amount * StanceResistance);
        return Poise >= 1f;
    }

    void SetPoise(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, Poise)) return;
        Poise = value;
        PoiseChanged?.Invoke(Poise);
    }

    IEnumerator RecoverFromStagger(float seconds)
    {
        float tilt = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            // A phase break cuts the daze short and restarts the fight itself.
            if (PhaseBreaking)
            {
                sprite.transform.localRotation = Quaternion.identity;
                yield break;
            }
            // A dazed wobble while her stance is broken.
            tilt = Mathf.Sin(t * 18f) * 6f * (1f - t / seconds);
            sprite.transform.localRotation = Quaternion.Euler(0, 0, tilt);
            yield return null;
        }
        sprite.transform.localRotation = Quaternion.identity;
        RestartFight();
    }

    // Lets subclasses react to boon effects on the battlefield (Benibana's smoke burns away under fire).
    public virtual void Burn(Vector3 point, float radius) { }

    public void TakeHit(int damage, KillSource source)
    {
        if (IsDead || Down || PhaseBreaking) return;
        // Read before the hit lands: a boon's nested hit would clear Projectile.Striking.
        bool setPiece = source == KillSource.Reflected && Projectile.Striking != null && Projectile.Striking.SetPiece;
        float stance = setPiece ? Damage.SetPieceStance : Damage.Stance(source);
        if (ronin != null) stance *= ronin.StanceBoost(source);
        if (Staggered) damage *= 2;
        damage *= DamageMultiplier;
        damage = Damage.Lessen(damage, source, ref shadowChip);
        if (SuperArmor)
        {
            // Only a sliver gets through; slivers add up until they're worth a whole hit point.
            armorChip += damage * ArmorDamageTaken;
            damage = Mathf.FloorToInt(armorChip);
            armorChip -= damage;
        }
        Health = Mathf.Max(0, Health - damage);

        Vector3 point = Centre;
        HitFlash.Flash(sprite, 0.1f);
        Sfx.Play(Sfx.BossHit);
        // Armoured hits barely hurt, and glance off with a ring of steel instead of breaking its stance.
        if (SuperArmor)
        {
            Fx.Play(Fx.Impact, point, ArmorSpark, 1.2f);
            Sfx.Play(Sfx.ParryClang, 0.5f, 1.3f);
        }
        Damaged?.Invoke(point, source);
        // A boon's on-hit effect lands its own TakeHit from inside Damaged; if that nested hit already broke the
        // phase (or killed), this one must not act on the empty bar again and kill outright.
        if (IsDead || Down || PhaseBreaking) return;

        if (Health <= 0)
        {
            if (HasSecondPhase && !SecondPhase && !Summoned) StartCoroutine(BreakPhase());
            else if (HoldOnAtZero) GoDown();
            else Die();
            return;
        }
        if (AddPoise(stance)) Stagger(StaggerSeconds);
        else if (Time.time >= nextHurtLine)
        {
            nextHurtLine = Time.time + HurtLineGap;
            Speak(BossLine.Hurt);
        }
        OnDamaged();
    }

    // The first bar is empty: the fight stops, the cutscene plays while the bar fills back up, then phase 2 begins.
    IEnumerator BreakPhase()
    {
        MaxHealth = SecondPhaseHealth;
        SecondPhase = true;
        PhaseBreaking = true;
        armorChip = 0;
        staggeredUntil = frozenUntil = 0;
        SetPoise(0f);
        Interrupt();
        IEnumerator scene = PhaseBreak();
        bool sceneDone = false;
        StartCoroutine(Run(scene, () => sceneDone = true));

        yield return new WaitForSeconds(RefillDelay);
        for (float t = 0; t < RefillSeconds; t += Time.deltaTime)
        {
            Health = Mathf.Max(Health, Mathf.CeilToInt(MaxHealth * Mathf.SmoothStep(0, 1, t / RefillSeconds)));
            Healed?.Invoke();
            yield return null;
        }
        Health = MaxHealth;
        Healed?.Invoke();

        while (!sceneDone) yield return null;
        PhaseBreaking = false;
        SecondPhaseBegan?.Invoke();
        RestartFight();
    }

    IEnumerator Run(IEnumerator routine, Action done)
    {
        yield return routine;
        done();
    }

    // Out of the fight but not fallen: her moves stop and nothing can hit her until Defeat finishes her.
    void GoDown()
    {
        Down = true;
        staggeredUntil = frozenUntil = 0;
        SetPoise(0f);
        Interrupt();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        OnDowned();
    }

    // Finishes a boss that went Down, once whatever kept her in the fight has fallen too.
    protected void Defeat()
    {
        if (!IsDead) Die();
    }

    void Die()
    {
        IsDead = true;
        fighting = false;
        StopAllCoroutines();
        ClearTransient();
        ClearLaunched();
        OnCleared();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Defeated?.Invoke();
        if (Summoned)
        {
            StartCoroutine(SoulFall(false));
            return;
        }
        Speak(BossLine.Defeat);
        StartCoroutine(Fall());
    }

    // Takes a summoned soul off the field without a defeat: the Emperor drags it back into the coffin, or his own fall
    // takes it with him. Defeated is not raised.
    public void Banish()
    {
        if (IsDead) return;
        IsDead = true;
        fighting = false;
        StopAllCoroutines();
        ClearTransient();
        ClearLaunched();
        OnCleared();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Banished?.Invoke();
        StartCoroutine(SoulFall(true));
    }

    // A summoned soul's end: cut down, it flares white and dissolves upward; pulled back, it thins into teal and is
    // drawn down toward the coffin.
    IEnumerator SoulFall(bool pulledBack)
    {
        Color soul = new Color(0.3f, 1f, 0.82f);
        sprite.transform.localRotation = Quaternion.identity;
        Fx.Play(pulledBack ? Fx.SmokePuff : Fx.Impact, Centre, soul, 2.2f);
        if (!pulledBack) Debris.BurstSprites(Centre, art.deathParticles, soul, 24, 1.1f, 7f);
        Vector3 from = anchor;
        const float Seconds = 1.1f;
        for (float t = 0; t < Seconds; t += Time.deltaTime)
        {
            float k = t / Seconds;
            anchor = from + (pulledBack ? Vector3.down : Vector3.up) * (0.8f * k * k);
            body.MovePosition(anchor);
            Color c = Color.Lerp(bodyTint, pulledBack ? soul : Color.white, Mathf.Min(1f, k * 2f));
            c.a = 1f - k;
            sprite.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }

    // Overridable defeat: by default she staggers, kneels and fades out in a burst of petals.
    protected virtual IEnumerator Fall()
    {
        CameraShake.Add(0.8f);
        Fx.Play(Fx.Explosion, Centre, Color.white, 2.5f);
        Debris.BurstSprites(Centre, art.deathParticles, Colour, 40, 1.4f, 9f);
        Sfx.Play(Sfx.PlayerDeath, 1f, 0.8f);
        // With a defeat clip she sinks down in her own animation; without one the sprite tips over.
        bool clip = art.death != null && art.death.Length > 0;
        if (clip) animator.Play(art.death, false, null, art.actionFps * 0.6f);
        Vector3 from = anchor;
        for (float t = 0; t < 2.5f; t += Time.deltaTime)
        {
            float k = t / 2.5f;
            if (!clip) sprite.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -14f, Mathf.Min(1f, k * 3f)));
            anchor = from + Vector3.down * Mathf.Min(clip ? 0.2f : 0.6f, k * 2f);
            // Hold her kneeling pose fully visible until the clip has played, then fade.
            k = clip ? Mathf.Clamp01((k - 0.45f) / 0.55f) : k;
            body.MovePosition(anchor);
            Color c = bodyTint;
            c.a = 1f - k;
            sprite.color = c;
            if (UnityEngine.Random.value < 0.15f) Debris.BurstSprites(Centre, art.deathParticles, Colour, 3, 1f, 3f);
            yield return null;
        }
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        ClearTransient();
    }
}
