using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Samurai : MonoBehaviour, IHittable
{
    static readonly Color FrostColor = new Color(0.55f, 0.8f, 1f);
    const float ArcherSpread = 15f;
    // The spear is heavy: slower than an arrow, so there's time to see it and dodge or parry.
    const float SpearSpeedMultiplier = 0.8f;

    [Header("Inscribed")]
    [FormerlySerializedAs("bulletPrefab")] public Arrow arrowPrefab;
    [Tooltip("The Taisho's yari. Wired by Ninja/Apply Sprite Art; without it the spear is a darkened arrow.")]
    [SerializeField] Sprite spearSprite;
    [Tooltip("How strongly the stage's clan colour tints the samurai sprite.")]
    [SerializeField, Range(0, 1)] float tintStrength = 0.2f;

    public Rigidbody Body { get; private set; }
    public int Column { get; private set; }
    public int Points { get; private set; }
    public SamuraiKind Kind { get; private set; }
    public bool IsDead { get; private set; }

    SamuraiArmy army;
    SpriteAnimator animator;
    CharacterArt art;
    Color clanTint;
    int health;
    int maxHealth;
    float frost;

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

        SetFrost(0);
        animator.Play(art.idle, true, null, art.idleFps);
    }

    public void Fire(float arrowSpeed)
    {
        // Spawn just below the samurai's body (its collider radius is 0.5 in local space), whatever its scale.
        Vector3 muzzle = transform.position + Vector3.down * (0.5f * transform.lossyScale.y + 0.1f);
        if (Kind == SamuraiKind.Archer)
        {
            Arrow.Fan(arrowPrefab, muzzle, Vector3.down, 3, ArcherSpread, arrowSpeed * 0.9f);
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

    // Tints the samurai towards ice blue while the army is chilled or frozen (0 = normal, 1 = fully frozen).
    public void SetFrost(float amount)
    {
        if (IsDead) return;
        frost = amount;
        Color clan = Color.Lerp(Color.white, clanTint, tintStrength);
        // Armour that has taken hits darkens, so wounded samurai stand out.
        float wounds = 1f - (float)health / maxHealth;
        clan = Color.Lerp(clan, new Color(0.45f, 0.3f, 0.3f), wounds * 0.6f);
        animator.Renderer.color = Color.Lerp(clan, FrostColor, frost * 0.7f);
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (IsDead) return;
        health -= damage;
        if (health <= 0)
        {
            Kill(source);
            return;
        }
        HitFlash.Flash(animator.Renderer, 0.1f);
        Fx.Play(Fx.Impact, transform.position, new Color(1f, 0.9f, 0.7f), 0.7f);
        Debris.BurstSprites(transform.position, art.deathParticles, Color.white, 3, 0.7f, 4f);
        Sfx.Play(Sfx.BlockerHit, 0.8f, 1.3f);
        SetFrost(frost);
    }

    public void Kill(KillSource source)
    {
        if (IsDead) return;

        // Leave the army straight away so the corpse can't be hit, shoot or land while it falls.
        IsDead = true;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }
        Debris.BurstSprites(transform.position, art.deathParticles, Color.white, 8, 1f, 6f);
        Fx.Play(Fx.SmokePuff, transform.position + Vector3.up * 0.2f, new Color(1f, 1f, 1f, 0.8f), 0.9f);
        HitFlash.Flash(animator.Renderer, 0.12f);
        Sfx.Play(Sfx.SamuraiDeath);
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
