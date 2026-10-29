using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Samurai : MonoBehaviour, IHittable
{
    static readonly Color FrostColor = new Color(0.55f, 0.8f, 1f);
    // Encased samurai are drawn fully icy with a pale halo, so they stand out from the army's lighter frost tint.
    static readonly Color IceColor = new Color(0.45f, 0.85f, 1f);
    static readonly Color IceGlow = new Color(0.6f, 0.9f, 1f, 0.7f);
    // Without their own art, shieldbearers wear steel-blue armour so the front wall reads apart from the archers.
    static readonly Color ShieldColor = new Color(0.6f, 0.72f, 0.95f);
    // Yurei are bodiless souls: a pale cyan, see-through body over a soft spirit glow, so they read apart from the
    // walking dead at a glance. On their own onryo art the cyan is lighter, so the white burial kimono still reads.
    static readonly Color GhostColor = new Color(0.6f, 0.95f, 1f);
    static readonly Color GhostGlow = new Color(0.4f, 0.9f, 1f, 0.45f);
    static readonly Color GhostArrow = new Color(0.55f, 0.95f, 1f);
    // Bellringers wear bronze until they have their own art, so the bell-bearers read apart from the ranks.
    static readonly Color BronzeColor = new Color(1f, 0.72f, 0.42f);
    // A bell's toll is the souls answering: teal, the colour of soul-fire.
    static readonly Color TollColor = new Color(0.3f, 1f, 0.8f, 0.8f);
    const float ArcherSpread = 15f;
    // A yurei's pair of arrows part just enough that one lane sits between them.
    const float YureiSpread = 12f;
    // The spear is heavy: slower than an arrow, so there's time to see it and dodge or parry.
    const float SpearSpeedMultiplier = 0.8f;

    [Header("Inscribed")]
    [FormerlySerializedAs("bulletPrefab")] public Arrow arrowPrefab;
    [Tooltip("The Taisho's yari. Wired by Ninja/Apply Sprite Art; without it the spear is a darkened arrow.")]
    [SerializeField] Sprite spearSprite;
    public Sprite SpearSprite => spearSprite;
    [Tooltip("How strongly the stage's clan colour tints the samurai sprite.")]
    [SerializeField, Range(0, 1)] float tintStrength = 0.2f;

    public Rigidbody Body { get; private set; }
    public int Column { get; private set; }
    public int Points { get; private set; }
    public SamuraiKind Kind { get; private set; }
    public bool IsDead { get; private set; }
    // Frozen solid by Frost Blade or an ice shard: holds its fire, and the ronin's next hit shatters it.
    public bool Encased => Time.time < encasedUntil;

    SamuraiArmy army;
    SpriteAnimator animator;
    CharacterArt art;
    Color clanTint;
    int health;
    int maxHealth;
    float frost;
    float encasedUntil;
    SpriteRenderer iceGlow;
    SpriteRenderer ghostGlow;
    float ghostPhase;
    SpriteRenderer alert;
    SpriteRenderer alertGlow;
    SpriteRenderer alertEdge;
    float alertUntil;
    float alertSince;
    Color? knockTint;

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<SpriteAnimator>();
    }

    public void Init(SamuraiArmy owner, int column, int points, Color tint, CharacterArt characterArt, SamuraiKind kind, int hitPoints = 1)
    {
        army = owner;
        Column = column;
        Points = points;
        art = characterArt;
        clanTint = tint;
        Kind = kind;
        health = maxHealth = Mathf.Max(1, hitPoints);

        if (kind == SamuraiKind.Yurei)
        {
            ghostGlow = Projectile.CreateGlow(transform, 1.5f / transform.lossyScale.x, animator.Renderer.sortingOrder - 1, GhostGlow);
            ghostPhase = Random.value * Mathf.PI * 2f;
        }
        SetFrost(0);
        animator.Play(art.idle, true, null, art.idleFps);
    }

    // The body's sprite, for effects laid over it (a levy forming out of soul energy).
    public SpriteRenderer Look => animator.Renderer;

    public bool CanFire => Kind != SamuraiKind.Shieldbearer && Kind != SamuraiKind.Bellringer && !Encased;

    // Shieldbearers and bellringers never shoot, so the rank behind them looses over their heads.
    public bool CanFireKind => Kind != SamuraiKind.Shieldbearer && Kind != SamuraiKind.Bellringer;

    // Wearing the Bushi art because its kind has none of its own yet: a shieldbearer is then tinted steel, a
    // bellringer bronze and a yurei a stronger cyan, so each still reads apart from the archers.
    public bool StandIn { get; set; }

    public void Fire(float arrowSpeed)
    {
        if (Kind == SamuraiKind.Shieldbearer || Kind == SamuraiKind.Bellringer) return;
        // Spawn just below the samurai's body (its collider radius is 0.5 in local space), whatever its scale.
        Vector3 muzzle = transform.position + Vector3.down * (0.5f * transform.lossyScale.y + 0.1f);
        if (Kind == SamuraiKind.Archer)
        {
            Arrow.Fan(arrowPrefab, muzzle, Vector3.down, 3, ArcherSpread, arrowSpeed * 0.9f);
        }
        else if (Kind == SamuraiKind.Yurei)
        {
            // Spirit arrows: a parted pair that drifts through the blockers, so cover only shelters from the living.
            foreach (Arrow arrow in Arrow.Fan(arrowPrefab, muzzle, Vector3.down, 2, YureiSpread, arrowSpeed * 0.85f))
            {
                arrow.PassesCover = true;
                arrow.Cause = DeathCause.GhostArrow;
                arrow.Tint(GhostArrow, GhostGlow);
            }
            Sfx.Play(Sfx.ArrowFire, 0.5f, 1.3f);
            return;
        }
        else if (Kind == SamuraiKind.Taisho)
        {
            Arrow spear = Instantiate(arrowPrefab, muzzle, Quaternion.identity);
            spear.MakeUnbreakable(spearSprite);
            spear.Launch(Vector3.down * (arrowSpeed * SpearSpeedMultiplier), 1.5f);
            Sfx.Play(Sfx.ArrowFire, 0.9f, 0.65f);
            return;
        }
        else
        {
            Instantiate(arrowPrefab, muzzle, Quaternion.identity).Launch(arrowSpeed);
        }
        Sfx.Play(Sfx.ArrowFire, 0.6f, Kind == SamuraiKind.Archer ? 1.15f : 1f);
    }

    // A bellringer lifts its bell: it shudders and glows for a beat before the toll, so the haste can be seen coming.
    public void WarnToll(float seconds)
    {
        HitFlash.Flash(animator.Renderer, 0.15f);
        ElementFx.Glow(transform.position, TollColor, 0.6f, 1.6f, seconds, transform);
    }

    // The toll: a ring of soul-fire spreads from the bell.
    public void Toll()
    {
        ElementFx.Glow(transform.position, TollColor, 1f, 9f, 0.7f);
        Fx.Play(Fx.Impact, transform.position, TollColor, 1.4f);
    }

    static readonly Color AlertColor = new Color(1f, 0.35f, 0.2f);

    // A flash, a battle cry and a big red "!" edged in white over its head just before it breaks formation to dive or
    // lunge at the ronin, for as long as the warning lasts. It pops in oversized so it catches the eye.
    public void WarnDive(float seconds)
    {
        HitFlash.Flash(animator.Renderer, 0.18f);
        Fx.Play(Fx.Impact, transform.position, new Color(1f, 0.5f, 0.4f), 0.8f);
        Sfx.Play(Sfx.SamuraiCry, 0.7f, 1.2f);
        if (alert == null)
        {
            alert = new GameObject("Alert").AddComponent<SpriteRenderer>();
            alert.sprite = ProceduralSprites.Exclaim;
            alert.color = AlertColor;
            alert.sortingOrder = animator.Renderer.sortingOrder + 5;
            alertEdge = new GameObject("AlertEdge").AddComponent<SpriteRenderer>();
            alertEdge.sprite = ProceduralSprites.Exclaim;
            alertEdge.color = Color.white;
            alertEdge.sortingOrder = alert.sortingOrder - 1;
            alertGlow = new GameObject("AlertGlow").AddComponent<SpriteRenderer>();
            alertGlow.sprite = ProceduralSprites.Glow;
            alertGlow.color = new Color(AlertColor.r, AlertColor.g, AlertColor.b, 0.75f);
            alertGlow.sortingOrder = alert.sortingOrder - 2;
        }
        alertSince = Time.time;
        alertUntil = Time.time + seconds;
    }

    // Parried: it takes the parry's colour as it's flung away, before it dies.
    public void Knock(Color colour)
    {
        knockTint = colour;
        HitFlash.Flash(animator.Renderer, 0.15f);
        animator.Renderer.color = colour;
        HideAlert();
    }

    // Bobs and pulses over its head, above the sprite's top whatever its size.
    void OnDestroy() => HideAlert();

    void UpdateAlert()
    {
        if (alert == null) return;
        if (Time.time >= alertUntil || IsDead)
        {
            HideAlert();
            return;
        }
        float top = animator.Renderer.bounds.max.y;
        float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 20f);
        // Pops in at half again its size and settles over the first fifth of a second. The mark is a unit tall at
        // scale 1, so its foot is lifted clear of the head whatever its size.
        float pop = 1f + 0.5f * (1f - Mathf.Clamp01((Time.time - alertSince) / 0.2f));
        float size = 1.1f * pulse * pop;
        alert.transform.position = new Vector3(transform.position.x, top + 0.25f + size * 0.5f + 0.08f * Mathf.Sin(Time.time * 14f), -0.2f);
        alert.transform.localScale = Vector3.one * size;
        alertEdge.transform.position = alert.transform.position + Vector3.forward * 0.005f;
        alertEdge.transform.localScale = new Vector3(size * 1.35f, size * 1.15f, 1f);
        alertGlow.transform.position = alert.transform.position + Vector3.forward * 0.01f;
        alertGlow.transform.localScale = Vector3.one * (1.6f * pulse * pop / ProceduralSprites.Glow.bounds.size.x);
    }

    void HideAlert()
    {
        if (alert != null) Destroy(alert.gameObject);
        if (alertGlow != null) Destroy(alertGlow.gameObject);
        if (alertEdge != null) Destroy(alertEdge.gameObject);
        alert = null;
        alertGlow = null;
        alertEdge = null;
    }

    // A spiked shell of icicles drawn over the statue a samurai leaves when it dies frozen. Living samurai held in ice
    // only wear the ice tint and halo, so the spikes always mean "dead, strike it". The art's 48px frame is centred
    // while samurai frames stand on their feet, so it's lifted half a unit and a little more. Null without art.
    public static SpriteRenderer AddIceShell(SpriteRenderer body, Sprite shell)
    {
        if (shell == null) return null;
        var ice = new GameObject("IceShell").AddComponent<SpriteRenderer>();
        ice.transform.SetParent(body.transform, false);
        ice.transform.localPosition = new Vector3(0f, 0.56f, -0.01f);
        ice.transform.localScale = new Vector3(1.05f, 1.02f, 1f);
        ice.sprite = shell;
        ice.sortingOrder = body.sortingOrder + 1;
        return ice;
    }

    // Encases the samurai in ice for a while: full ice tint and a frozen pose.
    public void Encase(float seconds)
    {
        if (IsDead) return;
        encasedUntil = Mathf.Max(encasedUntil, Time.time + seconds);
        animator.enabled = false;
        if (iceGlow == null) iceGlow = Projectile.CreateGlow(transform, 1.6f / transform.lossyScale.x, animator.Renderer.sortingOrder - 1, IceGlow);
        Fx.Play(Fx.IceBurst, transform.position, Color.white, 0.9f);
        SetFrost(frost);
    }

    void Update()
    {
        UpdateAlert();
        // A ghost's body breathes in and out of sight, slowly, each on its own beat.
        if (ghostGlow != null && !Encased && !IsDead) SetFrost(frost);
        if (iceGlow == null || Encased) return;
        Thaw();
        SetFrost(frost);
    }

    void Thaw()
    {
        animator.enabled = true;
        if (iceGlow != null) Destroy(iceGlow.gameObject);
        iceGlow = null;
    }

    // Tints the samurai towards ice blue while the army is frozen (0 = normal, 1 = fully frozen); being encased
    // in ice always shows full frost.
    public void SetFrost(float amount)
    {
        if (IsDead) return;
        if (knockTint is Color knocked)
        {
            animator.Renderer.color = knocked;
            return;
        }
        frost = amount;
        Color clan = Color.Lerp(Color.white, clanTint, tintStrength);
        if (StandIn && Kind == SamuraiKind.Shieldbearer) clan = Color.Lerp(clan, ShieldColor, 0.6f);
        if (StandIn && Kind == SamuraiKind.Bellringer) clan = Color.Lerp(clan, BronzeColor, 0.65f);
        // Armour that has taken hits darkens, so wounded samurai stand out.
        float wounds = 1f - (float)health / maxHealth;
        clan = Color.Lerp(clan, new Color(0.45f, 0.3f, 0.3f), wounds * 0.6f);
        Color body = Encased ? IceColor : Color.Lerp(clan, FrostColor, frost * 0.7f);
        if (Kind == SamuraiKind.Yurei && !Encased)
        {
            body = Color.Lerp(body, GhostColor, StandIn ? 0.7f : 0.35f);
            body.a = 0.65f + 0.15f * Mathf.Sin(Time.time * 2.2f + ghostPhase);
        }
        animator.Renderer.color = body;
    }

    // Off while the army marches in: shots and blasts pass straight through until the round starts.
    public void SetTouchable(bool touchable)
    {
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = touchable;
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (IsDead || army.Entering) return;
        // Ice is brittle: any of the ronin's own hits shatters an encased samurai outright.
        if (Encased && source != KillSource.Effect) damage = health;
        health -= damage;
        if (health <= 0)
        {
            Kill(source);
            return;
        }
        HitFlash.Flash(animator.Renderer, 0.1f);
        Debris.BurstSprites(transform.position, art.deathParticles, Color.white, 3, 0.7f, 4f);
        // A shield turns the blow with a bright spark and a clang instead of a thud.
        if (Kind == SamuraiKind.Shieldbearer)
        {
            Fx.Play(Fx.Impact, transform.position + Vector3.down * 0.3f, new Color(0.8f, 0.9f, 1f), 1f);
            Sfx.Play(Sfx.Deflect, 0.7f, 1.2f);
        }
        else
        {
            Fx.Play(Fx.Impact, transform.position, new Color(1f, 0.9f, 0.7f), 0.7f);
            Sfx.Play(Sfx.BlockerHit, 0.8f, 1.3f);
        }
        SetFrost(frost);
    }

    public void Kill(KillSource source)
    {
        if (IsDead || army.Entering) return;

        // Leave the army straight away so the corpse can't be hit, shoot or land while it falls.
        IsDead = true;
        // Cut down while held in ice, it freezes solid where it stood instead of falling.
        bool frozen = Encased;
        Thaw();
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }
        if (frozen)
        {
            IceStatue.Create(animator.Renderer, gameObject.layer, art.deathParticles, army.IceShell);
            Sfx.Play(Sfx.SamuraiDeath);
            army.OnSamuraiKilled(this, source);
            Destroy(gameObject);
            return;
        }
        Debris.BurstSprites(transform.position, art.deathParticles, Color.white, 8, 1f, 6f);
        Fx.Play(Fx.SmokePuff, transform.position + Vector3.up * 0.2f, new Color(1f, 1f, 1f, 0.8f), 0.9f);
        HitFlash.Flash(animator.Renderer, 0.12f);
        Sfx.Play(Sfx.SamuraiDeath);
        Sfx.Play(Sfx.SamuraiCry);
        army.OnSamuraiKilled(this, source);
        if (art.death != null && art.death.Length > 0)
        {
            animator.Play(art.death, false, () => Destroy(gameObject), art.actionFps);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
