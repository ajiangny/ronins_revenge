using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Ronin : MonoBehaviour
{
    static readonly Color WardColor = new Color(0.7f, 0.9f, 1f);
    // The teal of his spirit flames, before any element takes over.
    static readonly Color DefaultGlow = new Color(0.55f, 1f, 0.85f);

    [Header("Inscribed")]
    [FormerlySerializedAs("dartPrefab")] public GameObject kunaiPrefab;
    public CharacterArt art;
    public float speed = 10f;
    [SerializeField] float shotCooldown = 0.35f;
    [SerializeField] float invulnerableTime = 2f;
    [SerializeField] float screenMargin = 0.2f;
    [Tooltip("Degrees between kunai when Inferno Fan throws several at once.")]
    [SerializeField] float fanAngle = 9f;

    [Header("Spirit Shuriken (E / Up)")]
    [Tooltip("Leave empty to throw a big spinning kunai until the shuriken art is in.")]
    [SerializeField] Sprite shurikenSprite;
    [SerializeField] float shurikenCooldown = 6f;
    [Tooltip("Scales the whole shuriken, hitbox included (the art is also drawn larger by Kunai.artScale).")]
    [SerializeField] float shurikenScale = 2.6f;
    [SerializeField] float shurikenSpeed = 0.75f;

    [Header("Parry (Shift / Down)")]
    [SerializeField] float parryWindow = 0.25f;
    [SerializeField] float parryCooldown = 1.5f;
    [Tooltip("Arrows inside this radius around the ronin's upper body are deflected during the parry.")]
    [SerializeField] float parryRadius = 1.1f;

    public event Action Hit;
    public event Action WardBroken;
    public event Action<Vector3> ShurikenThrown;
    // Raised once per parry, on the first arrow it deflects.
    public event Action Parried;

    public bool ControlsEnabled { get; set; }
    public RoninLoadout Loadout { get; set; } = new RoninLoadout();
    // World-space height of the top of the ronin; samurai reaching it have landed.
    public float TopY => transform.position.y + topOffset;
    public Bounds Bounds => sprite.bounds;
    // 0 right after use, 1 when ready. Shown on the HUD.
    public float ShurikenCharge => Charge(lastShuriken, shurikenCooldown);
    public float ParryCharge => Charge(lastParry, parryCooldown);
    // The colours his attacks spark in, for effects elsewhere (hit sparks on kills).
    public Color KunaiGlow => kunaiGlow;
    public Color ShurikenGlow => shurikenGlow;
    public Color ParryColor => parryColor;
    public int SpriteOrder => sprite.sortingOrder;

    Rigidbody body;
    SpriteRenderer sprite;
    SpriteRenderer aura;
    FxPlayer wardBubble;
    SpriteAnimator animator;
    Camera cam;
    readonly Collider[] parryBuffer = new Collider[16];
    float minOffsetX, maxOffsetX, centerOffsetX, topOffset;
    float lastShot = float.NegativeInfinity;
    float lastShuriken = float.NegativeInfinity;
    float lastParry = float.NegativeInfinity;
    float invulnerableUntil;
    float parryUntil;
    bool parryLanded;
    float moveInput;
    Sprite[] loopClip;
    bool actionPlaying;

    // The current elemental look: art set, projectile sprites and colours.
    Sprite kunaiSprite;
    Color kunaiTint = Color.white;
    Color kunaiGlow = DefaultGlow;
    Color shurikenTint = Color.white;
    Color shurikenGlow = DefaultGlow;
    Color parryColor = Color.white;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        animator = sprite.GetComponent<SpriteAnimator>();
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

    void Update()
    {
        moveInput = 0;
        bool invulnerable = Time.time < invulnerableUntil;
        sprite.enabled = !invulnerable || Mathf.Repeat(Time.time, 0.2f) < 0.1f;
        UpdateWardBubble();
        bool tintForWard = Loadout.WardCharges > 0 && wardBubble == null;
        sprite.color = Parrying ? Color.Lerp(Color.white, parryColor, 0.5f) : tintForWard ? WardColor : Color.white;

        Keyboard keyboard = Keyboard.current;
        if (ControlsEnabled && keyboard != null)
        {
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveInput += 1;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveInput -= 1;

            if (keyboard.spaceKey.isPressed && Time.time - lastShot >= shotCooldown * Loadout.ThrowCooldownMultiplier)
            {
                ThrowKunai();
            }
            if ((keyboard.eKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) && ShurikenCharge >= 1f)
            {
                ThrowShuriken();
            }
            if ((keyboard.leftShiftKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) && ParryCharge >= 1f)
            {
                StartParry();
            }
        }
        UpdateLoopClip();
    }

    bool Parrying => Time.time < parryUntil;

    // An icy bubble shows each Frost Ward the ronin is carrying (the plain blue tint is the fallback).
    void UpdateWardBubble()
    {
        bool warded = Loadout.WardCharges > 0 && gameObject.activeSelf;
        if (warded && wardBubble == null)
        {
            wardBubble = Fx.Play(Fx.WardBubble, Bounds.center, new Color(1f, 1f, 1f, 0.85f), 1.1f, transform);
        }
        else if (!warded && wardBubble != null)
        {
            wardBubble.Stop();
            wardBubble = null;
        }
    }

    float Charge(float lastUse, float cooldown) => Mathf.Clamp01((Time.time - lastUse) / (cooldown * Loadout.AbilityCooldownMultiplier));

    Vector3 Muzzle => new Vector3(transform.position.x + centerOffsetX, transform.position.y, 0);

    // Inferno Fan adds kunai either side of the straight throw.
    void ThrowKunai()
    {
        int count = 1 + Loadout.ExtraKunai;
        var thrown = new Kunai[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) / 2f) * fanAngle;
            Kunai kunai = SpawnProjectile(Muzzle);
            kunai.Launch(Quaternion.Euler(0, 0, angle) * Vector3.up, 1f, 0, KillSource.Kunai, kunaiSprite);
            kunai.Tint(kunaiTint, kunaiGlow);
            for (int j = 0; j < i; j++) kunai.IgnoreCollisionsWith(thrown[j]);
            thrown[i] = kunai;
        }
        lastShot = Time.time;
        PlayAction(art.attack);
    }

    // A big spinning blade that flies straight up through every samurai in its column.
    void ThrowShuriken()
    {
        Kunai shuriken = SpawnProjectile(Muzzle);
        bool hasArt = shurikenSprite != null;
        shuriken.Launch(Vector3.up, shurikenSpeed, int.MaxValue, KillSource.Shuriken,
            hasArt ? shurikenSprite : kunaiSprite, scale: hasArt ? shurikenScale : shurikenScale * 0.6f, spin: true, lifetimeMultiplier: 2f);
        shuriken.Tint(shurikenTint, shurikenGlow);
        lastShuriken = Time.time;
        PlayAction(art.attack);
        ShurikenThrown?.Invoke(Muzzle);
    }

    void StartParry()
    {
        lastParry = Time.time;
        parryUntil = Time.time + parryWindow;
        parryLanded = false;
        // The slash sweeps over his head, where the arrows come from.
        if (Fx.Play(Fx.SlashArc, Bounds.center + Vector3.up * 0.35f, Color.Lerp(Color.white, parryColor, 0.7f), 1.15f, transform) == null)
        {
            ElementFx.Glow(Bounds.center, parryColor, 0.8f, parryRadius * 2.4f, parryWindow, transform);
        }
        PlayAction(art.parry != null && art.parry.Length > 0 ? art.parry : art.attack);
    }

    Kunai SpawnProjectile(Vector3 position)
    {
        return Instantiate(kunaiPrefab, position, Quaternion.identity).GetComponent<Kunai>();
    }

    void FixedUpdate()
    {
        if (Parrying) DeflectNearbyArrows();

        Vector3 position = body.position;
        position.x += moveInput * speed * Loadout.MoveSpeedMultiplier * Time.fixedDeltaTime;

        // Keep the whole ronin inside the camera's view.
        float halfWidth = cam.orthographicSize * cam.aspect;
        float centerX = CameraShake.Center.x;
        position.x = Mathf.Clamp(position.x,
            centerX - halfWidth + screenMargin - minOffsetX,
            centerX + halfWidth - screenMargin - maxOffsetX);
        body.MovePosition(position);
    }

    void DeflectNearbyArrows()
    {
        int hits = Physics.OverlapSphereNonAlloc(Bounds.center, parryRadius, parryBuffer);
        for (int i = 0; i < hits; i++)
        {
            Arrow arrow = parryBuffer[i].GetComponentInParent<Arrow>();
            if (arrow != null) Deflect(arrow);
        }
    }

    // The arrow is knocked back up as a projectile of the ronin's that kills the first samurai it reaches.
    void Deflect(Arrow arrow)
    {
        if (!arrow.TryDeflect()) return;

        Vector3 position = arrow.transform.position;
        Kunai riposte = SpawnProjectile(position);
        // Kunai art is drawn at half size in the prefab; scale the riposte so it matches the arrow it came from.
        riposte.Launch(Vector3.up, 1.1f, 0, KillSource.Deflected, arrow.Sprite, flipSprite: true, scale: 1.8f);
        riposte.Tint(Color.Lerp(Color.white, parryColor, 0.6f), parryColor);
        Debris.BurstSprites(position, art.deathParticles, parryColor, 6, 0.7f, 5f);
        Fx.Play(Fx.Impact, position, Color.Lerp(Color.white, parryColor, 0.6f), 0.9f);

        if (parryLanded) return;
        parryLanded = true;
        Parried?.Invoke();
    }

    void OnCollisionEnter(Collision coll)
    {
        Arrow arrow = coll.collider.GetComponentInParent<Arrow>();
        if (!ControlsEnabled || arrow == null) return;
        if (Parrying)
        {
            Deflect(arrow);
            return;
        }
        if (Time.time < invulnerableUntil) return;

        invulnerableUntil = Time.time + invulnerableTime;
        Vector3 contact = coll.GetContact(0).point;
        HitFlash.Flash(sprite);
        if (Loadout.TryUseWard())
        {
            // The Frost Ward shatters instead of the ronin taking the hit.
            Debris.BurstSprites(contact, art.deathParticles, WardColor, 12, 1f, 6f);
            Fx.Play(Fx.IceBurst, contact, Color.white, 1.4f);
            Fx.Play(Fx.FrostWave, new Vector3(Bounds.center.x, Bounds.min.y, 0f), Color.white, 0.9f);
            CameraShake.Add(0.2f);
            WardBroken?.Invoke();
            return;
        }
        Debris.BurstSprites(contact, art.deathParticles, Color.white, 10, 1f, 6f);
        Fx.Play(Fx.Impact, contact, new Color(1f, 0.45f, 0.4f), 1.5f);
        CameraShake.Add(0.35f);
        PlayAction(art.hit);
        Hit?.Invoke();
    }

    public void Explode()
    {
        Debris.BurstSprites(sprite.bounds.center, art.deathParticles, Color.white, 30, 1.4f, 9f);
        Fx.Play(Fx.Explosion, sprite.bounds.center, Color.white, 1.6f);
        Fx.Play(Fx.SmokePuff, sprite.bounds.center, Color.white, 2f);
        CameraShake.Add(0.8f);
        if (wardBubble != null) wardBubble.Stop();
        gameObject.SetActive(false);
    }

    // Switches to an elemental look. Null sprites keep the prefab's art; the tints stand in until recoloured art exists.
    // A null glow colour keeps the default spirit-flame teal.
    public void SetLook(CharacterArt lookArt, Color auraColor, Sprite kunai, Color kunaiColor, Color? kunaiHalo,
        Sprite shuriken, Color shurikenColor, Color? shurikenHalo, Color parry)
    {
        kunaiGlow = kunaiHalo ?? DefaultGlow;
        shurikenGlow = shurikenHalo ?? DefaultGlow;
        aura.color = auraColor;
        if (lookArt != null && lookArt != art)
        {
            art = lookArt;
            loopClip = null;
            if (!actionPlaying) UpdateLoopClip();
        }
        kunaiSprite = kunai;
        kunaiTint = kunaiColor;
        if (shuriken != null) shurikenSprite = shuriken;
        shurikenTint = shurikenColor;
        parryColor = parry;
    }

    // Sprint while moving, idle while still. Throw, parry and hit clips play over this and hand back when done.
    void UpdateLoopClip()
    {
        if (actionPlaying) return;
        Sprite[] run = moveInput < 0 ? art.runLeft : moveInput > 0 ? art.runRight : null;
        Sprite[] clip = run != null && run.Length > 0 ? run : art.idle;
        if (clip == loopClip) return;
        loopClip = clip;
        animator.Play(clip, true, null, clip == art.idle ? art.idleFps : art.runFps);
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
