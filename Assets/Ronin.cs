using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// The moves his controls can fire, as flags for Ronin.Allowed.
[Flags]
public enum Moves { None = 0, Slash = 1, Shuriken = 2, Parry = 4, Ultimate = 8, All = Slash | Shuriken | Parry | Ultimate }

[RequireComponent(typeof(Rigidbody))]
public class Ronin : MonoBehaviour
{
    // The main attack on Space. The kunai is kept for a later weapon-select screen.
    public enum Weapon { Sword, Kunai }
    // The ultimate chosen at the temple, kept in the save by its int: new ones go on the end.
    public enum Ultimate { ShadowClone }

    // The teal of his spirit flames, before any element takes over.
    static readonly Color DefaultGlow = new Color(0.55f, 1f, 0.85f);
    // Burning and Frozen Riposte shots: drawn sizes in world units, and the shards' fan either side of straight up.
    const float FireballSize = 1.3f;
    const float ShardSize = 0.8f;
    const float ShardSpread = 25f;
    const float ThunderShurikenSpeed = 1.9f;
    // Kindling's fireball: hitbox scale, drawn size against a Burning Riposte fireball, and speed against a slash.
    const float KindlingScale = 2.2f;
    const float KindlingDrawScale = 2f;
    const float KindlingSpeed = 0.75f;
    // Split Slash forks this long into a slash's flight, into side slashes this much smaller.
    const float SplitDelay = 0.22f;
    const float SplitScale = 0.8f;
    // Echo Slash's homing ghost is drawn a little smaller than the slash it echoes.
    const float EchoScale = 0.9f;
    // Shadow Clone: the shadow's look, and the look of the slashes it sends.
    static readonly Color ShadowTint = new Color(0.32f, 0.22f, 0.5f, 0.75f);
    static readonly Color ShadowGlow = new Color(0.55f, 0.35f, 0.9f);
    // How long a Frozen Riposte shard holds the samurai it hits in ice.
    const float RiposteShardFreeze = 1.5f;

    [Header("Inscribed")]
    [Tooltip("Thrown kunai, and the Spirit Shuriken and parry ripostes, which reuse the same projectile.")]
    [FormerlySerializedAs("dartPrefab")] public GameObject kunaiPrefab;
    [Tooltip("The crescent wave each katana swing sends up the screen.")]
    public GameObject slashPrefab;
    [SerializeField] Weapon weapon = Weapon.Sword;
    public CharacterArt art;
    public float speed = 10f;
    [Tooltip("Seconds between swings. Kept slow enough that every slash is a choice, as in Space Invaders.")]
    [SerializeField] float shotCooldown = 0.45f;
    [SerializeField] float invulnerableTime = 2f;
    [SerializeField] float screenMargin = 0.2f;
    [Tooltip("Degrees off its heading that each side slash of Split Slash peels away at.")]
    [SerializeField] float splitAngle = 18f;
    [Tooltip("Art pixels the spirit wisps bob up and down.")]
    [SerializeField] float wispBobPixels = 1.5f;
    [Tooltip("Seconds per bob of the spirit wisps.")]
    [SerializeField] float wispPeriod = 1.8f;

    [Header("Spirit Shuriken")]
    [Tooltip("Leave empty to throw a big spinning kunai until the shuriken art is in.")]
    [SerializeField] Sprite shurikenSprite;
    [SerializeField] float shurikenCooldown = 6f;
    [Tooltip("Scales the whole shuriken, hitbox included (the art is also drawn larger by Kunai.artScale).")]
    [SerializeField] float shurikenScale = 2.6f;
    [SerializeField] float shurikenSpeed = 0.75f;

    [Header("Parry")]
    [SerializeField] float parryWindow = 0.25f;
    [Tooltip("Cooldown after a parry that deflects nothing. A parry that lands is ready again at once, so parries chain.")]
    [SerializeField] float parryMissCooldown = 2f;
    [Tooltip("Arrows inside this radius around the ronin's upper body are deflected during the parry.")]
    [SerializeField] float parryRadius = 1.1f;

    [Header("Ultimate")]
    [Tooltip("Hits on samurai, scouts or a boss (his own blows, not boon effects or shadow slashes) to fill the bar.")]
    [SerializeField] int ultimateHits = 40;
    [Tooltip("Seconds a full bar lasts once the ultimate is unleashed.")]
    [SerializeField] float ultimateDuration = 10f;
    [Tooltip("How far beside him the Shadow Clone stands, in world units.")]
    [SerializeField] float cloneOffset = 1.6f;
    [Tooltip("How fast the Shadow Clone glides to keep its place beside him (and to cross to his other side at a wall), in units per second.")]
    [SerializeField] float cloneGlide = 16f;

    public event Action Hit;
    // Raised on every swing (a slash or Kindling's fireball).
    public event Action Slashed;
    public event Action<Vector3, Kunai> ShurikenThrown;
    // Raised as a parry starts, landed or not.
    public event Action ParryStarted;
    // Raised once per parry, on the first arrow it deflects.
    public event Action Parried;
    // Raised when a parry meets a shot it can't turn (a crimson seal-dart), once per parry.
    public event Action ParryFizzled;

    public bool ControlsEnabled { get; set; }
    public Ultimate CurrentUltimate { get; set; }
    // 0 empty, 1 full; while the ultimate runs it drains back to 0.
    public float UltimateFill { get; private set; }
    public bool UltimateActive { get; private set; }
    public bool UltimateReady => !UltimateActive && UltimateFill >= 1f;
    // The tutorial keeps the bar still, so its drills can't hand him a full one.
    public bool UltimateLocked { get; set; }
    public event Action UltimateFilled;
    public event Action UltimateStarted;
    public event Action UltimateEnded;
    // He walks his row but can't attack: choosing a portal between rounds.
    public bool MoveOnly { get; set; }
    // Held in a boss's stopped time (Utsusemi's Stopped Hour): he can't move, attack or parry, and stands frozen
    // mid-frame until time runs again.
    public bool TimeHeld
    {
        get => timeHeld;
        set
        {
            timeHeld = value;
            if (animator != null) animator.enabled = !value;
        }
    }
    bool timeHeld;
    // In a safe area (the temple) he walks anywhere inside RoamArea, up and down too, and can't fight. Nowhere else
    // does he leave his row.
    public bool Roaming
    {
        get => roaming;
        set
        {
            // He comes into a safe area facing us (climbing out of the pool, or loaded in).
            if (value && !roaming) facing = South;
            roaming = value;
            if (!value && sprite != null) sprite.transform.localPosition = spriteRest;
        }
    }
    public Rect RoamArea { get; set; }
    // Ground in a safe area he walks around (the props' footprints), in the same body space as RoamArea.
    public Rect[] RoamBlocks { get; set; }
    public Weapon CurrentWeapon { get => weapon; set => weapon = value; }
    // God mode from the dev menu: nothing can hurt him.
    public bool Invincible { get; set; }
    // Poguru's tutorial keeps him unhurt until its spear, apart from god mode.
    public bool Shielded { get; set; }
    // Which of his moves the controls may fire; the tutorial's drills allow only the one they teach.
    public Moves Allowed { get; set; } = Moves.All;
    // Picks the enemy a parried arrow homes in on; set by the GameManager, which knows who's on the field.
    public Func<Transform> RiposteTarget { get; set; }
    // True while he stands where a parry is forbidden (inside a lane sealed by the Emperor's ofuda); set by that boss.
    public Func<bool> ParrySealed { get; set; }
    // A fireball or ice shard riposte struck something.
    public event Action<Vector3, IHittable> RiposteImpact;
    // Kindling's fireball has hit something.
    public event Action<Vector3, IHittable> FireballImpact;
    public RoninLoadout Loadout { get; set; } = new RoninLoadout();

    // Each upgrade he holds for an attack makes its hits break a boss's stance a quarter faster.
    static readonly BoonId[] SlashUpgrades = { BoonId.FlameBlade, BoonId.FrostBlade, BoonId.StormBlade, BoonId.SplitSlash, BoonId.PiercingSlash, BoonId.EchoSlash };
    static readonly BoonId[] ShurikenUpgrades = { BoonId.InfernoShuriken, BoonId.GlacialShuriken, BoonId.ThunderShuriken };
    static readonly BoonId[] RiposteUpgrades = { BoonId.BurningRiposte, BoonId.FrozenRiposte, BoonId.ThunderRiposte };
    const float StancePerUpgrade = 0.25f;

    public float StanceBoost(KillSource source)
    {
        BoonId[] upgrades = source switch
        {
            KillSource.Shuriken => ShurikenUpgrades,
            KillSource.Deflected or KillSource.Reflected => RiposteUpgrades,
            KillSource.Effect => null,
            _ => SlashUpgrades,
        };
        if (upgrades == null) return 1f;
        int held = 0;
        foreach (BoonId id in upgrades)
            if (Loadout.Find(id) != null) held++;
        return 1f + StancePerUpgrade * held;
    }
    // World-space height of the top of the ronin; samurai reaching it have landed.
    public float TopY => transform.position.y + topOffset;
    public Bounds Bounds => sprite.bounds;
    // Where his feet touch the ground: the art is drawn standing 7px above the bottom of its canvas, which is the
    // sprite's pivot, so effects anchored here (aura, frost wave, boon pillar) line up with him.
    // The drawn sprite's extent, and the world size of one of its art pixels, for drawing him elsewhere at the same scale.
    public Bounds ArtBounds => sprite.bounds;
    public Sprite CurrentFrame => sprite.sprite;
    public float ArtPixelSize => sprite.transform.lossyScale.y / sprite.sprite.pixelsPerUnit;
    public Vector3 Feet => sprite.transform.position + Vector3.up * (FeetAbovePivotPx / sprite.sprite.pixelsPerUnit * sprite.transform.lossyScale.y);

    const float FeetAbovePivotPx = 7f;
    // 0 right after use, 1 when ready. Shown on the HUD.
    public float ShurikenCharge => Charge(lastShuriken, shurikenCooldown);
    public float ParryCharge => Charge(lastParry, parryMissCooldown);
    // The colours his attacks spark in, for effects elsewhere (hit sparks on kills).
    public Color AttackGlow => attackGlow;
    public Color ShurikenGlow => shurikenGlow;
    public Color ParryColor => parryColor;
    public int SpriteOrder => sprite.sortingOrder;
    // Where he stands when a run starts; he walks back here after crossing to a new map.
    public Vector3 Home { get; private set; }

    Rigidbody body;
    SpriteRenderer sprite;
    bool hurry;
    SpriteRenderer shadow;
    SpriteRenderer shadowGlow;
    // Where the clone stands (its pivot's x, like his), which side of him it keeps to, and its own parry's end.
    float cloneX;
    float cloneSide = 1f;
    float cloneParryUntil;
    SpriteRenderer aura;
    readonly System.Collections.Generic.List<SpriteRenderer> wisps = new System.Collections.Generic.List<SpriteRenderer>();
    SpriteAnimator animator;
    Camera cam;
    // Room for a bullet-hell curtain's worth of shots inside the parry at once.
    readonly Collider[] parryBuffer = new Collider[48];
    // A parry sends back at most this many of a curtain's bullets (Arrow.CappedRiposte); the rest burst harmlessly.
    const int RiposteCap = 3;
    int cappedRipostes;
    bool parryFizzled;
    float minOffsetX, maxOffsetX, centerOffsetX, topOffset;
    float lastShot = float.NegativeInfinity;
    // Swings since Kindling's last fireball.
    int swingsSinceFireball;
    float lastShuriken = float.NegativeInfinity;
    float lastParry = float.NegativeInfinity;
    float invulnerableUntil;
    float parryUntil;
    bool parryLanded;
    float moveInput;
    float moveInputY;
    bool roaming;
    Vector3 spriteRest;
    float roamTime;
    Sprite[] loopClip;
    bool actionPlaying;
    bool walking;

    // The current elemental look: art set, projectile sprites and colours.
    Sprite kunaiSprite;
    Sprite slashSprite;
    Color attackTint = Color.white;
    Color attackGlow = DefaultGlow;
    Color shurikenTint = Color.white;
    Color shurikenGlow = DefaultGlow;
    Color parryColor = Color.white;

    // The one ronin, for things that follow him every frame (arrow shadows) without searching the scene.
    public static Ronin Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        body = GetComponent<Rigidbody>();
        Home = transform.position;
        sprite = GetComponentInChildren<SpriteRenderer>();
        animator = sprite.GetComponent<SpriteAnimator>();
        spriteRest = sprite.transform.localPosition;
        cam = Camera.main;

        // The sprite isn't centred on the pivot, so measure it once and clamp/shoot from the real extents.
        Bounds bounds = sprite.bounds;
        Vector3 pivot = transform.position;
        minOffsetX = bounds.min.x - pivot.x;
        maxOffsetX = bounds.max.x - pivot.x;
        centerOffsetX = bounds.center.x - pivot.x;
        topOffset = bounds.max.y - pivot.y;

        // A soft glow behind the ronin in his main element's colour; his idle frames have no spirit flames to recolour.
        aura = new GameObject("Aura").AddComponent<SpriteRenderer>();
        aura.transform.SetParent(transform, false);
        aura.transform.position = bounds.center + Vector3.forward * 0.1f;
        aura.transform.localScale = Vector3.one * (bounds.size.y * 1.3f / transform.lossyScale.y);
        aura.sprite = ProceduralSprites.Glow;
        aura.sortingOrder = sprite.sortingOrder - 1;
        aura.color = Color.clear;
    }

    void Start()
    {
        UpdateLoopClip();
    }

    void OnEnable() => Kunai.Landed += ChargeUltimate;
    void OnDisable() => Kunai.Landed -= ChargeUltimate;

    // Each of his own blows on an enemy fills the bar a notch; boon effects and the shadow's slashes don't.
    void ChargeUltimate(KillSource source, IHittable target)
    {
        if (UltimateActive || UltimateLocked || UltimateFill >= 1f || !Damage.Procs(source)) return;
        if (!(target is Samurai || target is Scout || target is Boss || target is SpiritWolf)) return;
        // Storm Shadow fills it faster.
        float step = (1f + Loadout.Extra(BoonId.StormShadow)) / Mathf.Max(1, ultimateHits);
        UltimateFill = Mathf.Min(1f, UltimateFill + step);
        if (UltimateFill >= 1f) UltimateFilled?.Invoke();
    }

    // Glacier Shell: a bubble of ice that takes one blow for him, bursts into homing shards and grows back after the
    // boon's time (shorter at each rarity, so a Spirit Bead quickens it).
    const int ShellShards = 5;
    const float ShellGrace = 1f;
    static readonly Color ShellColour = new Color(0.65f, 0.9f, 1f);
    float shellBrokenAt = float.NegativeInfinity;
    SpriteRenderer shell;

    bool ShellUp => Loadout.Has(BoonId.GlacierShell, out float regrow) && Time.time - shellBrokenAt >= regrow;
    // 0 right after it breaks, 1 while it's up; the HUD dims the boon's icon until then.
    public float ShellCharge => Loadout.Has(BoonId.GlacierShell, out float regrow) ? Mathf.Clamp01((Time.time - shellBrokenAt) / regrow) : 1f;

    void BreakShell(Vector3 contact)
    {
        shellBrokenAt = Time.time;
        invulnerableUntil = Time.time + ShellGrace;
        Vector3 at = Bounds.center;
        Fx.Play(Fx.IceBurst, at, Color.white, 1.8f);
        Debris.BurstSprites(contact, art.deathParticles, ShellColour, 12, 1f, 6f);
        Sfx.Play(Sfx.WardShatter, 1f, 1.1f);
        Sfx.Play(Sfx.FrostChill);
        CameraShake.Add(0.2f);
        for (int i = 0; i < ShellShards; i++) LaunchIceShard(at, RiposteTarget, ShellShardFreeze);
    }

    // As long as a shard from a shattered statue holds what it hits.
    const float ShellShardFreeze = 1.5f;

    // The shell's bubble: a pale glow round him that breathes while it's up and is gone while it regrows.
    void UpdateShell()
    {
        bool up = ShellUp && ControlsEnabled && !Roaming;
        if (shell == null)
        {
            if (!up) return;
            shell = new GameObject("GlacierShell").AddComponent<SpriteRenderer>();
            shell.transform.SetParent(transform, false);
            shell.sprite = ProceduralSprites.Glow;
        }
        if (up && !shell.enabled)
        {
            // Grows back with a shimmer.
            Fx.Play(Fx.IceBurst, Bounds.center, Color.white, 1.1f);
            Sfx.Play(Sfx.FrostChill, 0.5f, 1.4f);
        }
        shell.enabled = up;
        if (!up) return;
        float breathe = Mathf.Sin(Time.time * 3f);
        shell.transform.position = Bounds.center + Vector3.forward * 0.05f;
        shell.transform.localScale = Vector3.one * (Bounds.size.y * (1.5f + 0.04f * breathe) / transform.lossyScale.y);
        shell.sortingOrder = sprite.sortingOrder + 1;
        shell.color = new Color(ShellColour.r, ShellColour.g, ShellColour.b, 0.3f + 0.06f * breathe);
    }

    // Dev tools (F9): a full bar at once.
    public void FillUltimate()
    {
        if (UltimateActive || UltimateFill >= 1f) return;
        UltimateFill = 1f;
        UltimateFilled?.Invoke();
    }

    // Unleashed: the shadow steps out across the field and swings with him until the bar runs dry.
    void StartUltimate()
    {
        UltimateActive = true;
        Sfx.Play(Sfx.MistSwirl, 0.9f, 1.2f);
        CameraShake.Add(0.15f);
        UltimateStarted?.Invoke();
    }

    // The bar drains only while he's fighting, so a talk, a pause or the walk between rounds doesn't waste it.
    void UpdateUltimate()
    {
        if (!UltimateActive || !ControlsEnabled || MoveOnly || Roaming) return;
        UltimateFill = Mathf.Max(0f, UltimateFill - Time.deltaTime / Mathf.Max(0.1f, ultimateDuration));
        if (UltimateFill > 0f) return;
        UltimateActive = false;
        if (CloneOut) Fx.Play(Fx.SmokePuff, shadow.bounds.center, CloneGlow, 1.4f);
        UltimateEnded?.Invoke();
    }

    bool songsLearned;

    // The Flute Songs bought on the hill, added on top of the scene's tuning. The scene reloads for every run, so
    // this runs once on fresh values.
    public void LearnSongs()
    {
        if (songsLearned) return;
        songsLearned = true;
        parryWindow += Upgrades.Rank(UpgradeId.KeenEye) * Upgrades.ParryWindowPerRank;
        speed += Upgrades.Rank(UpgradeId.SwiftFeet) * Upgrades.SpeedPerRank;
        shotCooldown *= 1f - Upgrades.Rank(UpgradeId.QuickDraw) * Upgrades.SwingCutPerRank;
        shurikenCooldown -= Upgrades.Rank(UpgradeId.SpiritShuriken) * Upgrades.ShurikenCutPerRank;
        parryMissCooldown -= Upgrades.Rank(UpgradeId.SteadyBreath) * Upgrades.MissCutPerRank;
        invulnerableTime += Upgrades.Rank(UpgradeId.LongShadow) * Upgrades.InvulnerablePerRank;
    }

    void Update()
    {
        moveInput = 0;
        moveInputY = 0;
        bool invulnerable = Time.time < invulnerableUntil;
        sprite.enabled = !invulnerable || Mathf.Repeat(Time.time, 0.2f) < 0.1f;
        sprite.color = Parrying ? Color.Lerp(Color.white, parryColor, 0.5f) : Color.white;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (ControlsEnabled && Roaming && keyboard != null)
        {
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveInput += 1;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveInput -= 1;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) moveInputY += 1;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) moveInputY -= 1;
        }
        else if (ControlsEnabled && !timeHeld && keyboard != null)
        {
            moveInput = Controls.MoveX(keyboard);
        }
        // The moves' buttons are the control scheme's (Controls); using the other scheme's switches to it.
        if (ControlsEnabled && !Roaming && !MoveOnly && !timeHeld && keyboard != null)
        {
            Controls.Detect(keyboard, mouse);
            if (Controls.Slash(keyboard, mouse) && (Allowed & Moves.Slash) != 0 && Time.time - lastShot >= shotCooldown * Loadout.AttackCooldownMultiplier)
            {
                Attack();
            }
            if (Controls.Shuriken(keyboard, mouse) && (Allowed & Moves.Shuriken) != 0 && ShurikenCharge >= 1f)
            {
                ThrowShuriken();
            }
            if (Controls.Parry(keyboard, mouse) && (Allowed & Moves.Parry) != 0 && ParryCharge >= 1f)
            {
                if (ParrySealed != null && ParrySealed()) SealedParry();
                else StartParry();
            }
            if (Controls.Ultimate(keyboard, mouse) && (Allowed & Moves.Ultimate) != 0 && UltimateReady) StartUltimate();
        }
        UpdateUltimate();
        UpdateShell();
        UpdateLoopClip();
    }

    // After the animator has picked this frame's sprite.
    void LateUpdate()
    {
        UpdateWisps();
        UpdateShadowClone();
    }

    // The Shadow Clone ultimate: a shadow of him stands at his side, drawn from his own frame darkened, and does
    // whatever he does. It keeps to the side with more room and glides round to his other side when a wall
    // (the view's edge, measured from CameraShake.Center so a shake doesn't move it) would crowd it. It's only a
    // picture: nothing collides with it. It shows while the ultimate runs and he's in control, and puffs into being
    // each time it's unleashed.
    void UpdateShadowClone()
    {
        bool show = ControlsEnabled && UltimateActive && CurrentUltimate == Ultimate.ShadowClone;
        if (shadow == null)
        {
            if (!show) return;
            shadow = new GameObject("ShadowClone").AddComponent<SpriteRenderer>();
            shadowGlow = Kunai.CreateGlow(shadow.transform, 1.6f, sprite.sortingOrder - 2, Color.clear);
        }
        float x = transform.position.x;
        float halfWidth = cam.orthographicSize * cam.aspect;
        float centerX = CameraShake.Center.x;
        float min = centerX - halfWidth + screenMargin - minOffsetX;
        float max = centerX + halfWidth - screenMargin - maxOffsetX;
        bool appearing = show && !shadow.enabled;
        if (appearing) cloneSide = x < centerX ? 1f : -1f;
        float wanted = x + cloneSide * cloneOffset;
        if (wanted < min || wanted > max)
        {
            cloneSide = -cloneSide;
            wanted = x + cloneSide * cloneOffset;
        }
        cloneX = appearing ? wanted : Mathf.MoveTowards(cloneX, wanted, cloneGlide * Time.deltaTime);
        cloneX = Mathf.Clamp(cloneX, min, max);
        if (appearing) Fx.Play(Fx.SmokePuff, CloneCentre, CloneGlow, 1.4f);

        shadow.enabled = shadowGlow.enabled = show;
        if (!show) return;
        shadow.sprite = sprite.sprite;
        shadow.flipX = sprite.flipX;
        shadow.color = CloneTint;
        shadow.sortingOrder = sprite.sortingOrder - 1;
        shadowGlow.color = new Color(CloneGlow.r, CloneGlow.g, CloneGlow.b, 0.35f);
        shadow.transform.position = sprite.transform.position + Vector3.right * (cloneX - x);
        shadow.transform.localScale = sprite.transform.lossyScale;
    }

    bool CloneOut => shadow != null && shadow.enabled;
    Vector3 CloneMuzzle => new Vector3(cloneX + centerOffsetX, transform.position.y, 0f);
    Vector3 CloneCentre => Bounds.center + Vector3.right * (cloneX - transform.position.x);
    // Where the clone stands while it's out, for the Ultimate boons' blasts.
    public bool CloneShown => CloneOut;
    public Vector3 ClonePoint => CloneCentre;
    // The clone's colours, and the look of everything it sends: its violet, leaning to the element of the Ultimate
    // slot's boon.
    Color CloneTint
    {
        get
        {
            Element? element = Loadout.SlotElement(BoonSlot.Ultimate);
            if (element == null) return ShadowTint;
            Color c = Color.Lerp(ShadowTint, Boons.Info(element.Value).Color, 0.35f);
            return new Color(c.r, c.g, c.b, ShadowTint.a);
        }
    }
    Color CloneGlow => Loadout.SlotElement(BoonSlot.Ultimate) is Element e ? Color.Lerp(ShadowGlow, Boons.Info(e).Color, 0.6f) : ShadowGlow;

    // A clone's projectile: its own colours over his attack's.
    void Shade(Kunai projectile, Color tint) => projectile.Tint(Color.Lerp(tint, CloneGlow, 0.6f), CloneGlow);

    void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    // Some of his clips (idle, parry, walk) were drawn without the spirit wisps from his base art, so they're layered
    // on here, each bobbing out of step with the other, snapped to whole art pixels. Frames that draw their own skip them.
    void UpdateWisps()
    {
        Sprite[] frames = art.wisps ?? Array.Empty<Sprite>();
        bool drawnInFrame = art.framesWithWisps != null && Array.IndexOf(art.framesWithWisps, sprite.sprite) >= 0;
        while (wisps.Count < frames.Length)
        {
            var wisp = new GameObject("Wisp").AddComponent<SpriteRenderer>();
            wisp.transform.SetParent(sprite.transform, false);
            wisp.sortingOrder = sprite.sortingOrder + 1;
            wisps.Add(wisp);
        }
        for (int i = 0; i < wisps.Count; i++)
        {
            SpriteRenderer wisp = wisps[i];
            wisp.sprite = i < frames.Length ? frames[i] : null;
            wisp.enabled = sprite.enabled && wisp.sprite != null && !drawnInFrame && !(i < wispHidden.Length && wispHidden[i]);
            wisp.color = sprite.color;
            if (wisp.sprite == null) continue;
            float bob = Mathf.Round(Mathf.Sin((Time.time / wispPeriod + i * 0.5f) * Mathf.PI * 2f) * wispBobPixels);
            wisp.transform.localPosition = Vector3.up * (bob / wisp.sprite.pixelsPerUnit);
        }
    }

    // Poguru lives in the left wisp (0): while he's out of it, it's hidden.
    readonly bool[] wispHidden = new bool[2];

    public void SetWispHidden(int index, bool hidden)
    {
        if (index >= 0 && index < wispHidden.Length) wispHidden[index] = hidden;
    }

    // Where each wisp's flame sits on his 64-pixel canvas, in art pixels from the top-left (Ronin_Wisp_<n>.png).
    static readonly Vector2[] WispPixels = { new Vector2(16.5f, 17f), new Vector2(49f, 28f) };

    // A wisp's flame in the world, on whatever frame he's showing.
    public Vector3 WispPoint(int index)
    {
        Sprite frame = sprite.sprite;
        if (frame == null) return Bounds.center;
        Vector2 px = WispPixels[Mathf.Clamp(index, 0, WispPixels.Length - 1)];
        float ppu = frame.pixelsPerUnit;
        var local = new Vector3((px.x - frame.pivot.x) / ppu, (frame.rect.height - px.y - frame.pivot.y) / ppu, 0f);
        return sprite.transform.TransformPoint(local);
    }

    // Ends his hit flash's invulnerability at once, so a scripted blow (the tutorial's spear) always lands.
    public void ClearInvulnerable() => invulnerableUntil = 0f;

    public float ParryRadius => parryRadius;

    // Every move ready at once, for the tutorial's drills.
    public void ResetCooldowns() => lastShot = lastShuriken = lastParry = float.NegativeInfinity;

    // A drill's move fired for a test harness (synthetic keys don't reach an unfocused Game view).
    public void Perform(Moves move)
    {
        ResetCooldowns();
        if (move == Moves.Slash) Attack();
        else if (move == Moves.Shuriken) ThrowShuriken();
        else if (move == Moves.Parry) StartParry();
        else if (move == Moves.Ultimate && !UltimateActive)
        {
            FillUltimate();
            StartUltimate();
        }
    }

    public bool Parrying => Time.time < parryUntil;

    // A samurai lunging or diving at him met the parry: the clash lands like a parried arrow (the parry is ready
    // again at once), and the samurai is flung away by the army.
    public void ParryBody(Vector3 at)
    {
        Debris.BurstSprites(at, art.deathParticles, parryColor, 6, 0.7f, 5f);
        Fx.Play(Fx.Impact, at, Color.Lerp(Color.white, parryColor, 0.6f), 1.3f);
        Sfx.Play(Sfx.Deflect);
        CameraShake.Add(0.2f);
        LandParry();
    }

    float Charge(float lastUse, float cooldown) => Mathf.Clamp01((Time.time - lastUse) / (cooldown * Loadout.AbilityCooldownMultiplier));

    Vector3 Muzzle => new Vector3(transform.position.x + centerOffsetX, transform.position.y, 0);

    void Attack()
    {
        Slashed?.Invoke();
        // Kindling: every few swings a great fireball goes up instead of the slash.
        if (Loadout.Has(BoonId.Kindling, out float swingsPerFireball) && ++swingsSinceFireball >= (int)swingsPerFireball)
        {
            swingsSinceFireball = 0;
            ThrowFireball(Muzzle, false);
            if (CloneOut) ThrowFireball(CloneMuzzle, true);
        }
        // Without the slash prefab (art not applied yet) the sword falls back to throwing kunai.
        else if (weapon == Weapon.Sword && slashPrefab != null) Slash();
        else ThrowKunai();
        lastShot = Time.time;
    }

    // Kindling's fireball: the Burning Riposte fireball drawn at twice its size, slower than a slash. It burns through
    // arrows, and its hit counts as a slash (so Frost and Storm Blade trigger, though not Flame Blade's blast) before
    // it bursts in a great blast. The clone's is a lesser one in its colours, with no blast.
    void ThrowFireball(Vector3 from, bool clone)
    {
        Color fire = Boons.Info(Element.Fire).Color;
        Kunai fireball = SpawnProjectile(kunaiPrefab, from);
        Sprite[] frames = Fx.Frames(Fx.Fireball);
        bool hasArt = frames != null && frames.Length > 0;
        fireball.Launch(Vector3.up, KindlingSpeed, 0, clone ? KillSource.Clone : KillSource.Slash, hasArt ? null : kunaiSprite,
            scale: KindlingScale, lifetimeMultiplier: 2f);
        if (hasArt)
        {
            fireball.Animate(frames, 14f, FireballSize * KindlingDrawScale);
            fireball.Tint(Color.white, fire);
        }
        else
        {
            fireball.Tint(Color.Lerp(Color.white, fire, 0.7f), fire);
        }
        fireball.CutsArrows = true;
        if (clone)
        {
            Shade(fireball, hasArt ? Color.white : fire);
            fireball.ParryAura(CloneGlow);
            Fx.Play(Fx.SlashArc, CloneCentre + Vector3.up * 0.35f, CloneGlow, 1f);
            return;
        }
        fireball.ParryAura(Color.Lerp(fire, Color.yellow, 0.3f));
        fireball.Kindled = true;
        fireball.Impacted += (point, target) => FireballImpact?.Invoke(point, target);
        Sfx.Play(Sfx.FireBlast, 0.8f, 1.3f);
        Fx.Play(Fx.SlashArc, Bounds.center + Vector3.up * 0.35f, fire, 1f, transform);
        PlayAction(art.slash != null && art.slash.Length > 0 ? art.slash : art.parry);
    }

    // Each swing of the katana sends a crescent of spirit energy up the screen.
    void Slash()
    {
        Fan(slashPrefab, KillSource.Slash, slashSprite);
        Sfx.Play(Sfx.SwordSlash);
        Fx.Play(Fx.SlashArc, Bounds.center + Vector3.up * 0.35f, Color.Lerp(Color.white, attackGlow, 0.5f), 0.8f, transform);
        PlayAction(art.slash != null && art.slash.Length > 0 ? art.slash : art.parry);
    }

    // One cut with no crescent, for a cutscene (Oni cutting the chain on Shirogane's soul).
    public void Swing()
    {
        Sfx.Play(Sfx.SwordSlash);
        Fx.Play(Fx.SlashArc, Bounds.center + Vector3.up * 0.35f, Color.white, 0.8f, transform);
        PlayAction(art.slash != null && art.slash.Length > 0 ? art.slash : art.parry);
    }

    void ThrowKunai()
    {
        Fan(kunaiPrefab, KillSource.Kunai, kunaiSprite);
        Sfx.Play(Sfx.KunaiThrow);
        PlayAction(art.attack);
    }

    // One slash (or kunai) per swing; the Sword Arts reshape it. Split Slash forks it mid-flight, Echo Slash may add a
    // homing ghost, Piercing Slash applies to every one of them, and the Shadow Clone swings with him: the same fan
    // from beside him, every blade of it a lesser one (KillSource.Clone) in its colours.
    void Fan(GameObject prefab, KillSource source, Sprite projectileSprite)
    {
        FanFrom(prefab, source, projectileSprite, Muzzle, false);
        if (!CloneOut) return;
        FanFrom(prefab, KillSource.Clone, projectileSprite, CloneMuzzle, true);
        Fx.Play(Fx.SlashArc, CloneCentre + Vector3.up * 0.35f, CloneGlow, 0.8f);
    }

    void FanFrom(GameObject prefab, KillSource source, Sprite projectileSprite, Vector3 from, bool clone)
    {
        Kunai slash = LaunchSlash(prefab, from, Vector3.up, source, projectileSprite, 1f, clone);
        if (Loadout.Has(BoonId.SplitSlash, out _)) slash.SplitAfter(SplitDelay, parent => SplitOff(prefab, projectileSprite, parent, clone));
        if (Loadout.Has(BoonId.EchoSlash, out float echo) && UnityEngine.Random.value < echo)
        {
            Kunai ghost = LaunchSlash(prefab, from, Vector3.up, source, projectileSprite, EchoScale, clone);
            ghost.Home(RiposteTarget);
            ghost.ParryAura(clone ? CloneGlow : Color.Lerp(Color.white, attackGlow, 0.5f));
        }
    }

    Kunai LaunchSlash(GameObject prefab, Vector3 position, Vector3 direction, KillSource source, Sprite projectileSprite, float scale, bool clone = false)
    {
        Kunai projectile = SpawnProjectile(prefab, position);
        bool pierces = Loadout.Has(BoonId.PiercingSlash, out float passes);
        projectile.Launch(direction, 1f, pierces ? (int)passes : 0, source, projectileSprite, scale: scale);
        projectile.CutsArrows = pierces;
        if (clone) Shade(projectile, attackTint);
        else projectile.Tint(attackTint, attackGlow);
        return projectile;
    }

    // Split Slash: two smaller, lesser slashes (half damage to bosses, no boon effects) peel off either side of the
    // one in flight, which flies on. A clone's slash forks into the clone's own.
    void SplitOff(GameObject prefab, Sprite projectileSprite, Kunai parent, bool clone)
    {
        Vector3 at = parent.transform.position;
        Vector3 heading = parent.transform.up;
        KillSource source = clone ? KillSource.Clone : KillSource.Shadow;
        Kunai left = LaunchSlash(prefab, at, Quaternion.Euler(0, 0, splitAngle) * heading, source, projectileSprite, SplitScale, clone);
        Kunai right = LaunchSlash(prefab, at, Quaternion.Euler(0, 0, -splitAngle) * heading, source, projectileSprite, SplitScale, clone);
        left.IgnoreCollisionsWith(parent);
        right.IgnoreCollisionsWith(parent);
        left.IgnoreCollisionsWith(right);
        Fx.Play(Fx.Impact, at, Color.Lerp(Color.white, attackGlow, 0.6f), 0.7f);
    }

    // A big spinning blade that flies straight up through every samurai in its column.
    void ThrowShuriken()
    {
        Kunai shuriken = SpawnProjectile(kunaiPrefab, Muzzle);
        bool hasArt = shurikenSprite != null;
        // Thunder Shuriken hurls it far faster.
        float speed = shurikenSpeed * (Loadout.Has(BoonId.ThunderShuriken, out _) ? ThunderShurikenSpeed : 1f);
        shuriken.Launch(Vector3.up, speed, int.MaxValue, KillSource.Shuriken,
            hasArt ? shurikenSprite : kunaiSprite, scale: hasArt ? shurikenScale : shurikenScale * 0.6f, spin: true, lifetimeMultiplier: 2f);
        shuriken.Tint(shurikenTint, shurikenGlow);
        lastShuriken = Time.time;
        Sfx.Play(Sfx.ShurikenThrow);
        PlayAction(art.attack);
        ShurikenThrown?.Invoke(Muzzle, shuriken);
        if (CloneOut) CloneShuriken(speed, hasArt);
    }

    // The clone's own shuriken flies up its column beside his: as fast and as far, but each hit a lesser one, and it
    // sets off none of the shuriken boons (it raises no ShurikenThrown).
    void CloneShuriken(float speed, bool hasArt)
    {
        Kunai shuriken = SpawnProjectile(kunaiPrefab, CloneMuzzle);
        shuriken.Launch(Vector3.up, speed, int.MaxValue, KillSource.Clone,
            hasArt ? shurikenSprite : kunaiSprite, scale: hasArt ? shurikenScale : shurikenScale * 0.6f, spin: true, lifetimeMultiplier: 2f);
        shuriken.Spinning = true;
        Shade(shuriken, shurikenTint);
    }

    // The seal holds his blade: a dull red spark and no parry, and the cooldown isn't spent.
    void SealedParry()
    {
        if (Time.time - lastSealedParry < 0.3f) return;
        lastSealedParry = Time.time;
        Fx.Play(Fx.Impact, Bounds.center + Vector3.up * 0.35f, new Color(0.9f, 0.15f, 0.15f), 0.8f);
        Sfx.Play(Sfx.Deflect, 0.4f, 0.55f);
    }

    float lastSealedParry = float.NegativeInfinity;

    void StartParry()
    {
        lastParry = Time.time;
        parryUntil = Time.time + parryWindow;
        parryLanded = false;
        cappedRipostes = 0;
        parryFizzled = false;
        Sfx.Play(Sfx.ParrySwing);
        ParryStarted?.Invoke();
        // The slash sweeps over his head, where the arrows come from.
        if (Fx.Play(Fx.SlashArc, Bounds.center + Vector3.up * 0.35f, Color.Lerp(Color.white, parryColor, 0.7f), 1.15f, transform) == null)
        {
            ElementFx.Glow(Bounds.center, parryColor, 0.8f, parryRadius * 2.4f, parryWindow, transform);
        }
        PlayAction(art.parry != null && art.parry.Length > 0 ? art.parry : art.attack);
        if (CloneOut)
        {
            cloneParryUntil = parryUntil;
            Fx.Play(Fx.SlashArc, CloneCentre + Vector3.up * 0.35f, CloneGlow, 1.15f);
        }
    }

    Kunai SpawnProjectile(GameObject prefab, Vector3 position)
    {
        return Instantiate(prefab, position, Quaternion.identity).GetComponent<Kunai>();
    }

    // An elemental riposte: animated fireball or ice shard art when it exists, otherwise the parried arrow in the
    // element's colour.
    Kunai SpawnRiposte(Vector3 position, Vector3 direction, int pierce, float scale, string fxId, float drawSize, SpriteRenderer arrowArt, Color color)
    {
        Kunai riposte = SpawnProjectile(kunaiPrefab, position);
        Sprite[] frames = Fx.Frames(fxId);
        bool hasArt = frames != null && frames.Length > 0;
        riposte.Launch(direction, 1.1f, pierce, KillSource.Deflected, flipSprite: !hasArt, scale: scale, lifetimeMultiplier: 2f);
        riposte.Home(RiposteTarget);
        if (hasArt)
        {
            riposte.Animate(frames, 14f, drawSize);
            riposte.Tint(Color.white, color);
        }
        else
        {
            riposte.Mimic(arrowArt);
            riposte.Tint(Color.Lerp(Color.white, color, 0.6f), color);
        }
        riposte.ParryAura(Color.Lerp(Color.white, color, 0.7f));
        return riposte;
    }

    void OnRiposteImpact(Vector3 point, IHittable target) => RiposteImpact?.Invoke(point, target);

    // A shard of a shattered ice statue, homing on whatever picker returns. It hits one samurai and stops there,
    // holding it in ice first so a shard that cuts it down freezes it into a statue too.
    public void LaunchIceShard(Vector3 from, Func<Transform> picker, float freezeSeconds)
    {
        Color frost = Boons.Info(Element.Water).Color;
        Transform first = picker();
        Vector3 direction = first != null ? (first.position - from).normalized : Vector3.up;
        Kunai shard = SpawnProjectile(kunaiPrefab, from);
        shard.Launch(direction, 0.8f, 0, KillSource.Effect, scale: 1.1f, lifetimeMultiplier: 2f);
        shard.Home(picker);
        Sprite[] frames = Fx.Frames(Fx.IceShard);
        if (frames != null && frames.Length > 0)
        {
            shard.Animate(frames, 14f, ShardSize);
            shard.Tint(Color.white, frost);
        }
        else
        {
            shard.Tint(Color.Lerp(Color.white, frost, 0.6f), frost);
        }
        // The parry aura's trail of afterimages makes each shard's path easy to follow.
        shard.ParryAura(Color.Lerp(Color.white, frost, 0.6f));
        shard.FreezeOnHit = freezeSeconds;
    }

    void FixedUpdate()
    {
        if (walking) return;
        if (Parrying) DeflectNearbyArrows();
        if (Time.time < cloneParryUntil && CloneOut) CloneDeflect();

        Vector3 position = body.position;
        if (Roaming)
        {
            Roam(position);
            return;
        }
        position.x += moveInput * speed * Loadout.MoveSpeedMultiplier * Time.fixedDeltaTime;

        // Keep the whole ronin inside the camera's view.
        float halfWidth = cam.orthographicSize * cam.aspect;
        float centerX = CameraShake.Center.x;
        position.x = Mathf.Clamp(position.x,
            centerX - halfWidth + screenMargin - minOffsetX,
            centerX + halfWidth - screenMargin - maxOffsetX);
        body.MovePosition(position);
    }

    // A slower stroll than the fight's sprint, diagonals no faster than straight lines.
    const float RoamSpeedScale = 0.55f;

    void Roam(Vector3 position)
    {
        var step = new Vector2(moveInput, moveInputY);
        if (step.sqrMagnitude > 1f) step.Normalize();
        Vector3 from = position;
        position += (Vector3)(step * speed * RoamSpeedScale * Time.fixedDeltaTime);
        Rect area = RoamArea;
        position.x = Mathf.Clamp(position.x, area.xMin, area.xMax);
        position.y = Mathf.Clamp(position.y, area.yMin, area.yMax);
        // Into a prop, he keeps whichever half of the step doesn't go in, so he slides along it.
        if (RoamBlocks != null)
        {
            foreach (Rect block in RoamBlocks)
            {
                if (!block.Contains(position)) continue;
                if (!block.Contains(new Vector2(position.x, from.y))) position.y = from.y;
                else if (!block.Contains(new Vector2(from.x, position.y))) position.x = from.x;
                else position = from;
            }
            // Sliding off one wall mustn't put him in another where they meet.
            foreach (Rect block in RoamBlocks)
                if (block.Contains(position)) position = from;
        }
        body.MovePosition(position);
        // A direction with no frames of its own walks on the idle, so he bobs on it, as on a walk without frames.
        roamTime += Time.fixedDeltaTime;
        bool bob = step != Vector2.zero && IsIdle(loopClip);
        sprite.transform.localPosition = spriteRest + (bob ? Vector3.up * (Mathf.Abs(Mathf.Sin(roamTime * 9f)) * 0.12f / transform.lossyScale.y) : Vector3.zero);
    }

    // Walks him to a spot on his own while his controls are off, to leave one map and enter the next. He passes
    // through cover, and nothing can hit him on the way.
    // He faces the way he's going.
    public IEnumerator WalkTo(Vector3 target, float walkSpeed)
    {
        Sprite[] frames = WalkClip(target - body.position);
        return WalkTo(target, walkSpeed, frames, ClipFps(frames));
    }

    // The same walk on a clip of the caller's choosing, e.g. his run frames slowed down to cross the screen sideways.
    public IEnumerator WalkTo(Vector3 target, float walkSpeed, Sprite[] frames, float fps)
    {
        walking = true;
        actionPlaying = true;
        SetTouchable(false);
        // Without walk frames he bobs along on his idle loop.
        bool bob = frames == null || frames.Length == 0;
        animator.Play(bob ? art.idle : frames, true, null, bob ? art.idleFps : fps);
        Transform look = sprite.transform;
        Vector3 rest = look.localPosition;
        Vector3 at = body.position;
        hurry = false;
        for (float t = 0f; at != target;)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            at = hurry ? target : Vector3.MoveTowards(at, target, walkSpeed * Time.fixedDeltaTime);
            body.MovePosition(at);
            if (bob) look.localPosition = rest + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 9f)) * 0.12f / transform.lossyScale.y);
        }
        look.localPosition = rest;
        SetTouchable(true);
        walking = false;
        EndAction();
    }

    // Ends a WalkTo on its next step, standing him on its target (a skipped cutscene).
    public void FinishWalk() => hurry = walking;

    // Holds him in a looping pose of his own (the flute at dawn) for the rest of the scene.
    public void Pose(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0) return;
        actionPlaying = true;
        animator.Play(frames, true, null, fps);
    }

    // Moves him at once, e.g. to just below the view before he walks into a new map.
    public void PlaceAt(Vector3 position)
    {
        body.position = position;
        transform.position = position;
    }

    // Standing in water this deep (world units), the water hides his feet: a mask over everything below the waterline
    // clips his sprite, and only his sprite (its range is his order alone). It follows his feet, bob and all, so he
    // stays in the water as he steps. 0 brings his feet back.
    public void Wade(float depth)
    {
        bool wading = depth > 0f;
        if (wading && waterline == null)
        {
            waterline = new GameObject("Waterline").AddComponent<SpriteMask>();
            waterline.transform.SetParent(transform, false);
            waterline.sprite = ProceduralSprites.White;
            waterline.isCustomRangeActive = true;
        }
        this.wading = wading;
        if (waterline == null) return;
        waterline.enabled = wading;
        Mask();
        if (!wading) return;
        waterline.frontSortingOrder = sprite.sortingOrder;
        waterline.backSortingOrder = sprite.sortingOrder - 1;
        // From well below his feet (the canvas pads them) up to the waterline.
        const float Below = 0.5f;
        float width = sprite.bounds.size.x * 1.4f;
        Vector3 feet = Feet;
        waterline.transform.position = new Vector3(feet.x, feet.y + (depth - Below) / 2f, feet.z);
        Vector2 native = waterline.sprite.bounds.size;
        Vector3 parent = transform.lossyScale;
        waterline.transform.localScale = new Vector3(width / native.x / parent.x, (depth + Below) / native.y / parent.y, 1f);
    }

    SpriteMask waterline;
    bool wading, covered;

    // Under something the map draws over him (the temple gate's beam), its masks hide him where it stands, his spirit
    // wisps with him (the masks reach his order and theirs, one above).
    public void Cover(bool on)
    {
        covered = on;
        Mask();
        foreach (SpriteRenderer wisp in wisps) wisp.maskInteraction = on ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;
    }

    void Mask() => sprite.maskInteraction = wading || covered ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;

    void SetTouchable(bool touchable)
    {
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = touchable;
    }

    void DeflectNearbyArrows()
    {
        int hits = Physics.OverlapSphereNonAlloc(Bounds.center, parryRadius, parryBuffer);
        for (int i = 0; i < hits; i++)
        {
            Arrow arrow = parryBuffer[i].GetComponentInParent<Arrow>();
            if (arrow == null) continue;
            // The blade meets a seal-dart and fizzles; the dart flies on.
            if (arrow.Unparryable)
            {
                SealedParry();
                if (!parryFizzled)
                {
                    parryFizzled = true;
                    ParryFizzled?.Invoke();
                }
            }
            else Deflect(arrow);
        }
    }

    // The Shadow Clone parries with him and turns the arrows that reach it: plain ones only (no spear, seal-dart, boss
    // slash or shot with a part to play when parried, like the Emperor's skulls), each sent back as a lesser homing
    // riposte in its colours. Its parries set off no riposte boons and don't count as his landing one.
    void CloneDeflect()
    {
        int hits = Physics.OverlapSphereNonAlloc(CloneCentre, parryRadius, parryBuffer);
        for (int i = 0; i < hits; i++)
        {
            Arrow arrow = parryBuffer[i].GetComponentInParent<Arrow>();
            if (arrow == null || arrow.Unparryable || arrow.Unbreakable || arrow.Reflectable || arrow.Parried != null) continue;
            if (arrow.CappedRiposte && ++cappedRipostes > RiposteCap)
            {
                arrow.Pop(CloneGlow);
                continue;
            }
            SpriteRenderer arrowArt = arrow.Art;
            Vector3 position = arrow.transform.position;
            if (!arrow.TryDeflect()) continue;
            Kunai riposte = SpawnProjectile(kunaiPrefab, position);
            riposte.Launch(Vector3.up, 1.1f, 0, KillSource.Clone, flipSprite: true, scale: 1.8f, lifetimeMultiplier: 2f);
            riposte.Home(RiposteTarget);
            riposte.Mimic(arrowArt);
            Shade(riposte, Color.white);
            riposte.ParryAura(CloneGlow);
            arrow.Riposte = riposte;
            Fx.Play(Fx.Impact, position, CloneGlow, 0.9f);
            Sfx.Play(Sfx.Deflect, 0.7f, 0.85f);
        }
    }

    // The arrow itself flies back, same art and all, homing in on a random enemy and wrapped in the parry's aura.
    // Burning Riposte sends a fireball instead and Frozen Riposte a spread of ice shards. A boss's reflectable
    // slash homes in on the boss instead.
    void Deflect(Arrow arrow)
    {
        bool reflect = arrow.Reflectable;
        Transform target = arrow.ReflectTarget;
        Color arrowColor = arrow.Color;
        SpriteRenderer arrowArt = arrow.Art;
        bool heavy = arrow.Unbreakable;
        // Past the cap, a curtain's bullets are cut down instead of sent back; the parry still counts as landed.
        if (arrow.CappedRiposte && !reflect && !heavy && ++cappedRipostes > RiposteCap)
        {
            arrow.Pop(Color.Lerp(Color.white, parryColor, 0.6f));
            LandParry();
            return;
        }
        if (!arrow.TryDeflect()) return;

        Vector3 position = arrow.transform.position;
        Kunai sent;
        // Homing shots curve, so they get longer to reach a far target. Boss slashes are drawn leading downwards, so
        // like a parried arrow the copy is flipped to lead the way back.
        if (reflect)
        {
            Kunai riposte = SpawnProjectile(kunaiPrefab, position);
            riposte.Launch(Vector3.up, 1.3f, 0, KillSource.Reflected, flipSprite: true, scale: 2.4f, lifetimeMultiplier: 2f);
            riposte.SetPiece = arrow.SetPiece;
            riposte.Tint(arrowColor, arrowColor);
            riposte.Home(() => target);
            riposte.Mimic(arrowArt);
            riposte.ParryAura(Color.Lerp(Color.white, parryColor, 0.7f));
            CameraShake.Add(0.25f);
            sent = riposte;
        }
        else if (Loadout.Has(BoonId.BurningRiposte, out _))
        {
            Color fire = Boons.Info(Element.Fire).Color;
            Kunai fireball = SpawnRiposte(position, Vector3.up, heavy ? 2 : 0, 2.2f, Fx.Fireball, FireballSize, arrowArt, fire);
            fireball.Impacted += OnRiposteImpact;
            sent = fireball;
        }
        else if (Loadout.Has(BoonId.FrozenRiposte, out float shards))
        {
            Color frost = Boons.Info(Element.Water).Color;
            int count = (int)shards;
            var spread = new Kunai[count];
            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? Mathf.Lerp(-ShardSpread, ShardSpread, i / (count - 1f)) : 0f;
                Kunai shard = SpawnRiposte(position, Quaternion.Euler(0, 0, angle) * Vector3.up, 0, 1.2f, Fx.IceShard, ShardSize, arrowArt, frost);
                shard.FreezeOnHit = RiposteShardFreeze * Loadout.FreezeMultiplier;
                shard.Impacted += OnRiposteImpact;
                for (int j = 0; j < i; j++) shard.IgnoreCollisionsWith(spread[j]);
                spread[i] = shard;
            }
            sent = count > 0 ? spread[0] : null;
        }
        else
        {
            // The scale only sizes the hitbox; Mimic draws the arrow at its own size. The arrow art points down,
            // so it's flipped to lead the way back up. A parried Taisho spear keeps its weight and runs through two samurai.
            Kunai riposte = SpawnProjectile(kunaiPrefab, position);
            riposte.Launch(Vector3.up, 1.1f, heavy ? 2 : 0, KillSource.Deflected, flipSprite: true, scale: 1.8f, lifetimeMultiplier: 2f);
            riposte.Tint(Color.white, parryColor);
            riposte.Home(RiposteTarget);
            riposte.Mimic(arrowArt);
            riposte.ParryAura(Color.Lerp(Color.white, parryColor, 0.7f));
            sent = riposte;
        }
        arrow.Riposte = sent;
        arrow.Parried?.Invoke(sent);
        Debris.BurstSprites(position, art.deathParticles, parryColor, 6, 0.7f, 5f);
        Fx.Play(Fx.Impact, position, Color.Lerp(Color.white, parryColor, 0.6f), 0.9f);
        Sfx.Play(Sfx.Deflect);
        LandParry();
    }

    void LandParry()
    {
        if (parryLanded) return;
        parryLanded = true;
        // A landed parry is ready again straight away, so a well-timed one can answer the next arrow too.
        lastParry = float.NegativeInfinity;
        Sfx.Play(Sfx.ParryClang);
        Parried?.Invoke();
    }

    void OnCollisionEnter(Collision coll)
    {
        Arrow arrow = coll.collider.GetComponentInParent<Arrow>();
        if (!ControlsEnabled || arrow == null) return;
        if (Parrying && !arrow.Unparryable)
        {
            Deflect(arrow);
            return;
        }
        TakeHit(coll.GetContact(0).point);
    }

    // Damage that can't be parried, like a boss's chain lash. Invulnerability still applies.
    public void TakeHit(Vector3 contact)
    {
        if (!ControlsEnabled || Invincible || Shielded || Time.time < invulnerableUntil) return;
        if (ShellUp)
        {
            BreakShell(contact);
            return;
        }

        invulnerableUntil = Time.time + invulnerableTime;
        HitFlash.Flash(sprite);
        Debris.BurstSprites(contact, art.deathParticles, Color.white, 10, 1f, 6f);
        Fx.Play(Fx.Impact, contact, new Color(1f, 0.45f, 0.4f), 1.5f);
        CameraShake.Add(0.35f);
        Sfx.Play(Sfx.PlayerHit);
        Sfx.Play(Sfx.PlayerHurt);
        PlayAction(art.hit);
        Hit?.Invoke();
    }

    public void Explode()
    {
        Debris.BurstSprites(sprite.bounds.center, art.deathParticles, Color.white, 30, 1.4f, 9f);
        Fx.Play(Fx.Explosion, sprite.bounds.center, Color.white, 1.6f);
        Fx.Play(Fx.SmokePuff, sprite.bounds.center, Color.white, 2f);
        CameraShake.Add(0.8f);
        Sfx.Play(Sfx.PlayerDeath);
        gameObject.SetActive(false);
    }

    // Switches to an elemental look. Null sprites keep the prefab's art; the tints stand in until recoloured art exists.
    // A null glow colour keeps the default spirit-flame teal.
    public void SetLook(CharacterArt lookArt, Color auraColor, Sprite slash, Sprite kunai, Color attackColor, Color? attackHalo,
        Sprite shuriken, Color shurikenColor, Color? shurikenHalo, Color parry)
    {
        attackGlow = attackHalo ?? DefaultGlow;
        shurikenGlow = shurikenHalo ?? DefaultGlow;
        aura.color = auraColor;
        if (lookArt != null && lookArt != art)
        {
            art = lookArt;
            loopClip = null;
            if (!actionPlaying) UpdateLoopClip();
        }
        slashSprite = slash;
        kunaiSprite = kunai;
        attackTint = attackColor;
        if (shuriken != null) shurikenSprite = shuriken;
        shurikenTint = shurikenColor;
        parryColor = parry;
    }

    // Sprint while moving, idle while still. Slash, throw, parry and hit clips play over this and hand back when done.
    void UpdateLoopClip()
    {
        if (actionPlaying) return;
        // In a safe area he walks all eight ways; in a fight he only sprints along his row.
        var step = new Vector2(moveInput, moveInputY);
        if (Roaming && step != Vector2.zero) facing = Compass(step);
        Sprite[] run = Roaming ? WalkClip(step)
            : moveInput < 0 ? art.runLeft : moveInput > 0 ? art.runRight : null;
        // Standing still in a safe area he keeps facing the way he last walked; in a fight, up the field at the army.
        Sprite[] still = Roaming ? StillClip(facing) : art.idle;
        Sprite[] clip = run != null && run.Length > 0 ? run : still;
        if (clip == loopClip) return;
        loopClip = clip;
        animator.Play(clip, true, null, ClipFps(clip));
    }

    // His clip for a way across the screen, by the nearest of the eight compass points: sideways is his sprint,
    // the rest his walks. A diagonal without frames of its own falls back to the sprint, and a missing walk up or
    // down to null, which the callers play as the idle bobbing along.
    Sprite[] WalkClip(Vector2 direction)
    {
        if (direction == Vector2.zero) return null;
        switch (Compass(direction))
        {
            case 0: return FirstOf(art.runRight);
            case 1: return FirstOf(art.walkUpRight, art.runRight);
            case 2: return FirstOf(art.walkUp);
            case 3: return FirstOf(art.walkUpLeft, art.runLeft);
            case 4: return FirstOf(art.runLeft);
            case 5: return FirstOf(art.walkDownLeft, art.runLeft);
            case 6: return FirstOf(art.walkDown);
            default: return FirstOf(art.walkDownRight, art.runRight);
        }
    }

    // The nearest of the eight compass points: 0 is east, counting an eighth of a turn anticlockwise (2 north, 6 south).
    static int Compass(Vector2 direction) => (Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI / 4f)) + 8) % 8;

    const int South = 6;
    // The compass point he last walked toward in a safe area, for his still pose.
    int facing = South;

    // Standing still facing a compass point: that way's idle, else facing us, else his back.
    Sprite[] StillClip(int point)
    {
        Sprite[] own = point switch
        {
            0 => art.idleRight,
            1 => art.idleUpRight,
            2 => art.idle,
            3 => art.idleUpLeft,
            4 => art.idleLeft,
            5 => art.idleDownLeft,
            7 => art.idleDownRight,
            _ => art.idleDown,
        };
        return FirstOf(own, art.idleDown, art.idle);
    }

    bool IsIdle(Sprite[] clip) => clip == art.idle || clip == art.idleDown || clip == art.idleLeft || clip == art.idleRight
        || clip == art.idleUpLeft || clip == art.idleUpRight || clip == art.idleDownLeft || clip == art.idleDownRight;

    static Sprite[] FirstOf(params Sprite[][] clips)
    {
        foreach (Sprite[] clip in clips) if (clip != null && clip.Length > 0) return clip;
        return null;
    }

    float ClipFps(Sprite[] clip)
    {
        if (IsIdle(clip)) return art.idleFps;
        if (clip == art.runLeft || clip == art.runRight) return art.runFps;
        return art.walkFps;
    }

    void PlayAction(Sprite[] clip)
    {
        if (clip == null || clip.Length == 0) return;
        actionPlaying = true;
        animator.Play(clip, false, EndAction, art.actionFps);
    }

    void EndAction()
    {
        actionPlaying = false;
        loopClip = null;
        UpdateLoopClip();
    }
}
