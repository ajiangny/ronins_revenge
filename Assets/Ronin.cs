using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Ronin : MonoBehaviour
{
    // The main attack on Space. The kunai is kept for a later weapon-select screen.
    public enum Weapon { Sword, Kunai }

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
    [SerializeField] float shotCooldown = 0.35f;
    [SerializeField] float invulnerableTime = 2f;
    [SerializeField] float screenMargin = 0.2f;
    [Tooltip("Degrees off its heading that each side slash of Split Slash peels away at.")]
    [SerializeField] float splitAngle = 18f;
    [Tooltip("Art pixels the spirit wisps bob up and down.")]
    [SerializeField] float wispBobPixels = 1.5f;
    [Tooltip("Seconds per bob of the spirit wisps.")]
    [SerializeField] float wispPeriod = 1.8f;

    [Header("Spirit Shuriken (W / Up)")]
    [Tooltip("Leave empty to throw a big spinning kunai until the shuriken art is in.")]
    [SerializeField] Sprite shurikenSprite;
    [SerializeField] float shurikenCooldown = 6f;
    [Tooltip("Scales the whole shuriken, hitbox included (the art is also drawn larger by Kunai.artScale).")]
    [SerializeField] float shurikenScale = 2.6f;
    [SerializeField] float shurikenSpeed = 0.75f;

    [Header("Parry (Shift / Down / Right Click)")]
    [SerializeField] float parryWindow = 0.25f;
    [Tooltip("Cooldown after a parry that deflects nothing. A parry that lands is ready again at once, so parries chain.")]
    [SerializeField] float parryMissCooldown = 2f;
    [Tooltip("Arrows inside this radius around the ronin's upper body are deflected during the parry.")]
    [SerializeField] float parryRadius = 1.1f;

    public event Action Hit;
    public event Action<Vector3, Kunai> ShurikenThrown;
    // Raised once per parry, on the first arrow it deflects.
    public event Action Parried;
    // Raised when a parry meets a shot it can't turn (a crimson seal-dart), once per parry.
    public event Action ParryFizzled;

    public bool ControlsEnabled { get; set; }
    // In a safe area (the temple) he walks anywhere inside RoamArea, up and down too, and can't fight. Nowhere else
    // does he leave his row.
    public bool Roaming
    {
        get => roaming;
        set
        {
            roaming = value;
            if (!value && sprite != null) sprite.transform.localPosition = spriteRest;
        }
    }
    public Rect RoamArea { get; set; }
    public Weapon CurrentWeapon { get => weapon; set => weapon = value; }
    // God mode from the dev menu: nothing can hurt him.
    public bool Invincible { get; set; }
    // Picks the enemy a parried arrow homes in on; set by the GameManager, which knows who's on the field.
    public Func<Transform> RiposteTarget { get; set; }
    // True while he stands where a parry is forbidden (inside a lane sealed by the Emperor's ofuda); set by that boss.
    public Func<bool> ParrySealed { get; set; }
    // A fireball or ice shard riposte struck something.
    public event Action<Vector3, IHittable> RiposteImpact;
    // Kindling's fireball has hit something.
    public event Action<Vector3, IHittable> FireballImpact;
    public RoninLoadout Loadout { get; set; } = new RoninLoadout();
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
        else if (ControlsEnabled && keyboard != null)
        {
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveInput += 1;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveInput -= 1;

            bool slash = keyboard.spaceKey.isPressed || (mouse != null && mouse.leftButton.isPressed);
            bool parry = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (slash && Time.time - lastShot >= shotCooldown * Loadout.AttackCooldownMultiplier)
            {
                Attack();
            }
            if ((keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) && ShurikenCharge >= 1f)
            {
                ThrowShuriken();
            }
            if (parry && ParryCharge >= 1f)
            {
                if (ParrySealed != null && ParrySealed()) SealedParry();
                else StartParry();
            }
        }
        UpdateLoopClip();
    }

    // After the animator has picked this frame's sprite.
    void LateUpdate()
    {
        UpdateWisps();
        UpdateShadowClone();
    }

    // Shadow Clone: the shadow mirrors him across the middle of the view (CameraShake.Center, so a shake doesn't move
    // it), drawn from his own frame flipped and darkened. It's only a picture: nothing collides with it. It shows while
    // he's in control, and puffs into being the first time.
    void UpdateShadowClone()
    {
        bool show = ControlsEnabled && Loadout.Has(BoonId.ShadowClone, out _);
        if (shadow == null)
        {
            if (!show) return;
            shadow = new GameObject("ShadowClone").AddComponent<SpriteRenderer>();
            shadowGlow = Kunai.CreateGlow(shadow.transform, 1.6f, sprite.sortingOrder - 2, new Color(ShadowGlow.r, ShadowGlow.g, ShadowGlow.b, 0.35f));
            Fx.Play(Fx.SmokePuff, new Vector3(MirrorX(Bounds.center.x), Bounds.center.y, 0f), ShadowGlow, 1.4f);
        }
        shadow.enabled = shadowGlow.enabled = show;
        if (!show) return;
        shadow.sprite = sprite.sprite;
        shadow.flipX = !sprite.flipX;
        shadow.color = ShadowTint;
        shadow.sortingOrder = sprite.sortingOrder - 1;
        Vector3 at = sprite.transform.position;
        shadow.transform.position = new Vector3(MirrorX(at.x), at.y, at.z);
        shadow.transform.localScale = sprite.transform.lossyScale;
    }

    float MirrorX(float x) => 2f * CameraShake.Center.x - x;

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
            wisp.enabled = sprite.enabled && wisp.sprite != null && !drawnInFrame;
            wisp.color = sprite.color;
            if (wisp.sprite == null) continue;
            float bob = Mathf.Round(Mathf.Sin((Time.time / wispPeriod + i * 0.5f) * Mathf.PI * 2f) * wispBobPixels);
            wisp.transform.localPosition = Vector3.up * (bob / wisp.sprite.pixelsPerUnit);
        }
    }

    bool Parrying => Time.time < parryUntil;

    float Charge(float lastUse, float cooldown) => Mathf.Clamp01((Time.time - lastUse) / (cooldown * Loadout.AbilityCooldownMultiplier));

    Vector3 Muzzle => new Vector3(transform.position.x + centerOffsetX, transform.position.y, 0);

    void Attack()
    {
        // Kindling: every few swings a great fireball goes up instead of the slash.
        if (Loadout.Has(BoonId.Kindling, out float swingsPerFireball) && ++swingsSinceFireball >= (int)swingsPerFireball)
        {
            swingsSinceFireball = 0;
            ThrowFireball();
        }
        // Without the slash prefab (art not applied yet) the sword falls back to throwing kunai.
        else if (weapon == Weapon.Sword && slashPrefab != null) Slash();
        else ThrowKunai();
        lastShot = Time.time;
    }

    // Kindling's fireball: the Burning Riposte fireball drawn at twice its size, slower than a slash. It burns through
    // arrows, and its hit counts as a slash (so Blade boons trigger) before it bursts in a great blast.
    void ThrowFireball()
    {
        Color fire = Boons.Info(Element.Fire).Color;
        Kunai fireball = SpawnProjectile(kunaiPrefab, Muzzle);
        Sprite[] frames = Fx.Frames(Fx.Fireball);
        bool hasArt = frames != null && frames.Length > 0;
        fireball.Launch(Vector3.up, KindlingSpeed, 0, KillSource.Slash, hasArt ? null : kunaiSprite, scale: KindlingScale, lifetimeMultiplier: 2f);
        if (hasArt)
        {
            fireball.Animate(frames, 14f, FireballSize * KindlingDrawScale);
            fireball.Tint(Color.white, fire);
        }
        else
        {
            fireball.Tint(Color.Lerp(Color.white, fire, 0.7f), fire);
        }
        fireball.ParryAura(Color.Lerp(fire, Color.yellow, 0.3f));
        fireball.CutsArrows = true;
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
    // homing ghost, Piercing Slash applies to every one of them, and Shadow Clone's shadow swings with him.
    void Fan(GameObject prefab, KillSource source, Sprite projectileSprite)
    {
        Kunai slash = LaunchSlash(prefab, Muzzle, Vector3.up, source, projectileSprite, 1f);
        if (Loadout.Has(BoonId.SplitSlash, out _)) slash.SplitAfter(SplitDelay, parent => SplitOff(prefab, source, projectileSprite, parent));
        if (shadow != null && shadow.enabled)
        {
            // The shadow's slash is its own (it splits and pierces too) but never echoes, and wears the shadow's colours.
            Kunai shade = LaunchSlash(prefab, new Vector3(MirrorX(Muzzle.x), Muzzle.y, 0f), Vector3.up, source, projectileSprite, 1f);
            shade.Tint(Color.Lerp(attackTint, ShadowGlow, 0.6f), ShadowGlow);
            if (Loadout.Has(BoonId.SplitSlash, out _)) shade.SplitAfter(SplitDelay, parent => SplitOff(prefab, source, projectileSprite, parent));
            Fx.Play(Fx.SlashArc, new Vector3(MirrorX(Bounds.center.x), Bounds.center.y + 0.35f, 0f), ShadowGlow, 0.8f);
        }
        if (Loadout.Has(BoonId.EchoSlash, out float echo) && UnityEngine.Random.value < echo)
        {
            Kunai ghost = LaunchSlash(prefab, Muzzle, Vector3.up, source, projectileSprite, EchoScale);
            ghost.Home(RiposteTarget);
            ghost.ParryAura(Color.Lerp(Color.white, attackGlow, 0.5f));
        }
    }

    Kunai LaunchSlash(GameObject prefab, Vector3 position, Vector3 direction, KillSource source, Sprite projectileSprite, float scale)
    {
        Kunai projectile = SpawnProjectile(prefab, position);
        bool pierces = Loadout.Has(BoonId.PiercingSlash, out float passes);
        projectile.Launch(direction, 1f, pierces ? (int)passes : 0, source, projectileSprite, scale: scale);
        projectile.CutsArrows = pierces;
        projectile.Tint(attackTint, attackGlow);
        return projectile;
    }

    // Split Slash: two smaller slashes peel off either side of the one in flight, which flies on.
    void SplitOff(GameObject prefab, KillSource source, Sprite projectileSprite, Kunai parent)
    {
        Vector3 at = parent.transform.position;
        Vector3 heading = parent.transform.up;
        Kunai left = LaunchSlash(prefab, at, Quaternion.Euler(0, 0, splitAngle) * heading, source, projectileSprite, SplitScale);
        Kunai right = LaunchSlash(prefab, at, Quaternion.Euler(0, 0, -splitAngle) * heading, source, projectileSprite, SplitScale);
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
        // The slash sweeps over his head, where the arrows come from.
        if (Fx.Play(Fx.SlashArc, Bounds.center + Vector3.up * 0.35f, Color.Lerp(Color.white, parryColor, 0.7f), 1.15f, transform) == null)
        {
            ElementFx.Glow(Bounds.center, parryColor, 0.8f, parryRadius * 2.4f, parryWindow, transform);
        }
        PlayAction(art.parry != null && art.parry.Length > 0 ? art.parry : art.attack);
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
        position += (Vector3)(step * speed * RoamSpeedScale * Time.fixedDeltaTime);
        Rect area = RoamArea;
        position.x = Mathf.Clamp(position.x, area.xMin, area.xMax);
        position.y = Mathf.Clamp(position.y, area.yMin, area.yMax);
        body.MovePosition(position);
        // A direction with no frames of its own walks on the idle, so he bobs on it, as on a walk without frames.
        roamTime += Time.fixedDeltaTime;
        bool bob = step != Vector2.zero && loopClip == art.idle;
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
        if (!ControlsEnabled || Invincible || Time.time < invulnerableUntil) return;

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
        Sprite[] run = Roaming ? WalkClip(new Vector2(moveInput, moveInputY))
            : moveInput < 0 ? art.runLeft : moveInput > 0 ? art.runRight : null;
        Sprite[] clip = run != null && run.Length > 0 ? run : art.idle;
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
        // 0 is east, counting a quarter turn per two steps anticlockwise.
        int point = (Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI / 4f)) + 8) % 8;
        switch (point)
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

    static Sprite[] FirstOf(params Sprite[][] clips)
    {
        foreach (Sprite[] clip in clips) if (clip != null && clip.Length > 0) return clip;
        return null;
    }

    float ClipFps(Sprite[] clip)
    {
        if (clip == art.idle) return art.idleFps;
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
