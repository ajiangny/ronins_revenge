using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Samurai : MonoBehaviour, IHittable
{
    static readonly Color FrostColor = new Color(0.55f, 0.8f, 1f);
    // Encased samurai are drawn fully icy with a pale halo, so they stand out from the army's lighter frost tint.
    static readonly Color IceColor = new Color(0.45f, 0.85f, 1f);
    static readonly Color IceGlow = new Color(0.6f, 0.9f, 1f, 0.7f);
    // Soaked samurai are washed sea-blue, layered under the ice tint the way the army's frost is.
    static readonly Color SoakColor = new Color(0.18f, 0.48f, 1f);
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
    // Soaked by Asagi's water: every boon effect deals it one more (TakeHit), and the Duos build on it.
    public bool Soaked => Time.time < soakedUntil;
    // The other elements' statuses (Statuses.cs): Burning sends its shots wild, Chilled slows its arrows, and Jolted
    // makes its next shot fizzle.
    public bool Burning => Time.time < burningUntil;
    public bool Chilled => Time.time < chilledUntil;
    public bool Jolted => Time.time < joltedUntil;
    // Steel: dull iron with bright seams. Oni's own blow on a cracked samurai sheds scrap (BoonEffects.OnKill).
    public bool Cracked => Time.time < crackedUntil;
    // Black Ice: how much longer a soaked samurai stays encased. Set from the loadout by the GameManager.
    public static float BlackIce = 1f;

    SamuraiArmy army;
    SpriteAnimator animator;
    CharacterArt art;
    Color clanTint;
    int health;
    int maxHealth;
    float frost;
    float encasedUntil;
    float soakedUntil;
    float burningUntil;
    float chilledUntil;
    float joltedUntil;
    float crackedUntil;
    // The statuses last tinted in, so a status wearing off retints once.
    int shownStatus;
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
        if (Jolted)
        {
            Fizzle();
            return;
        }
        // Chilled, its arrows crawl, frost-pale, and live long enough to still reach the ground.
        bool chilled = Chilled;
        float life = 1f;
        if (chilled)
        {
            arrowSpeed *= Statuses.ChillArrow;
            life = 1f / Statuses.ChillArrow;
        }
        // Burning, it can't hold its aim: the shot flies off at an angle, ember-red.
        bool burning = Burning;
        Vector3 aim = burning ? Quaternion.Euler(0, 0, Random.Range(-Statuses.WildAngle, Statuses.WildAngle)) * Vector3.down : Vector3.down;
        // Spawn just below the samurai's body (its collider radius is 0.5 in local space), whatever its scale.
        Vector3 muzzle = transform.position + Vector3.down * (0.5f * transform.lossyScale.y + 0.1f);
        if (Kind == SamuraiKind.Archer)
        {
            foreach (Arrow arrow in Arrow.Fan(arrowPrefab, muzzle, aim, 3, ArcherSpread, arrowSpeed * 0.9f, life)) Mark(arrow, chilled, burning);
        }
        else if (Kind == SamuraiKind.Yurei)
        {
            // Spirit arrows: a parted pair that drifts through the blockers, so cover only shelters from the living.
            foreach (Arrow arrow in Arrow.Fan(arrowPrefab, muzzle, aim, 2, YureiSpread, arrowSpeed * 0.85f, life))
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
            spear.CursedSpear = true;
            spear.Launch(aim * (arrowSpeed * SpearSpeedMultiplier), 1.5f * life);
            Sfx.Play(Sfx.ArrowFire, 0.9f, 0.65f);
            return;
        }
        else
        {
            Arrow arrow = Instantiate(arrowPrefab, muzzle, Quaternion.identity);
            arrow.Launch(aim * arrowSpeed, life);
            Mark(arrow, chilled, burning);
        }
        Sfx.Play(Sfx.ArrowFire, 0.6f, Kind == SamuraiKind.Archer ? 1.15f : 1f);
    }

    static readonly Color ChilledArrowGlow = new Color(0.6f, 0.9f, 1f, 0.5f);
    static readonly Color WildArrowGlow = new Color(1f, 0.45f, 0.15f, 0.5f);

    // A status shows on the arrows it changes: frost-pale for a Chilled one's, ember for a Burning one's wild shots.
    static void Mark(Arrow arrow, bool chilled, bool burning)
    {
        if (chilled) arrow.Tint(Statuses.ChillColor, ChilledArrowGlow);
        else if (burning) arrow.Tint(Color.Lerp(Color.white, Statuses.BurnColor, 0.5f), WildArrowGlow);
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

    // Soaks the samurai for a while: a sea-blue wash and a splash. Nothing on its own; it's what the other effects
    // hit harder, and what Black Ice, Scalding Steam and Thunder Rain look for.
    public void Soak(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        bool fresh = !Soaked;
        soakedUntil = Mathf.Max(soakedUntil, Time.time + seconds * Statuses.SoakScale);
        if (Statuses.Rust) Crack(Statuses.CrackSeconds);
        if (fresh)
        {
            if (Fx.Play(Fx.Splash, transform.position, Color.white, 0.9f) == null) ElementFx.Glow(transform.position, SoakColor, 0.5f, 1.4f, 0.3f);
            Sfx.Play(Sfx.Splash, 0.6f, Random.Range(0.9f, 1.1f));
        }
        SetFrost(frost);
    }

    // Fire: it beats at the flames instead of shooting.
    public void Ignite(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        burningUntil = Mathf.Max(burningUntil, Time.time + seconds * Statuses.BurnScale);
        SetFrost(frost);
    }

    // Frost: its arrows crawl.
    public void Chill(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        chilledUntil = Mathf.Max(chilledUntil, Time.time + seconds * Statuses.ChillScale);
        SetFrost(frost);
    }

    // Storm: the charge waits in its armour until it draws a bow, then spoils the shot.
    public void Jolt(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        joltedUntil = Mathf.Max(joltedUntil, Time.time + seconds);
        SetFrost(frost);
    }

    // Steel: its armour splits along the seams. It changes nothing until Oni's own blow cuts it down.
    public void Crack(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        crackedUntil = Mathf.Max(crackedUntil, Time.time + seconds * Statuses.CrackScale);
        SetFrost(frost);
    }

    // A Jolted samurai tried to shoot: the charge snaps through its bow and the shot is lost. It costs the charge,
    // not the samurai: blowing it up made the status as good as a kill.
    void Fizzle()
    {
        joltedUntil = 0f;
        Fx.Play(Fx.Impact, transform.position + Vector3.down * 0.4f, Color.Lerp(Color.white, Statuses.JoltColor, 0.5f), 1.1f);
        Sfx.Play(Sfx.LightningStrike, 0.4f, 1.6f);
        SetFrost(frost);
    }

    int StatusMask => (Soaked ? 1 : 0) | (Burning ? 2 : 0) | (Chilled ? 4 : 0) | (Jolted ? 8 : 0) | (Cracked ? 16 : 0);

    // Encases the samurai in ice for a while: full ice tint and a frozen pose. Black Ice holds a soaked one longer.
    public void Encase(float seconds)
    {
        if (IsDead) return;
        if (Soaked) seconds *= BlackIce;
        encasedUntil = Mathf.Max(encasedUntil, Time.time + seconds);
        animator.enabled = false;
        if (iceGlow == null) iceGlow = Projectile.CreateGlow(transform, 1.6f / transform.lossyScale.x, animator.Renderer.sortingOrder - 1, IceGlow);
        Fx.Play(Fx.IceBurst, transform.position, Color.white, 0.9f);
        SetFrost(frost);
    }

    void Update()
    {
        UpdateAlert();
        // A status wearing off retints once (Burning and Jolted ones flicker while they last); a ghost's body breathes in and out
        // of sight, slowly, each on its own beat.
        if ((shownStatus != StatusMask || Jolted || Burning || Cracked) && !IsDead) SetFrost(frost);
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
        shownStatus = StatusMask;
        if (Soaked) clan = Color.Lerp(clan, SoakColor, 0.45f);
        if (Burning) clan = Color.Lerp(clan, Statuses.BurnColor, 0.4f + 0.1f * Mathf.Sin(Time.time * 9f));
        if (Chilled) clan = Color.Lerp(clan, Statuses.ChillColor, 0.5f);
        if (Jolted) clan = Color.Lerp(clan, Statuses.JoltColor, Mathf.Sin(Time.time * 30f) > 0.3f ? 0.6f : 0.2f);
        if (Cracked)
        {
            clan = Color.Lerp(clan, Statuses.CrackColor, 0.5f);
            // The seams catch the light now and then.
            if (Mathf.Sin(Time.time * 7f + transform.position.x) > 0.75f) clan = Color.Lerp(clan, Statuses.SeamColor, 0.5f);
        }
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
        // Soaked, every boon effect (blast, strike, shard, wave, drum) lands one point harder: the one place it's added.
        if (Soaked && source == KillSource.Effect) damage++;
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
            IceStatue.Create(animator.Renderer, gameObject.layer, art.deathParticles, army.IceShell).Soaked = Soaked;
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
