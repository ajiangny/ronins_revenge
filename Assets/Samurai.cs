using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Samurai : MonoBehaviour
{
    static readonly Color FrostColor = new Color(0.55f, 0.8f, 1f);

    [Header("Inscribed")]
    [FormerlySerializedAs("bulletPrefab")] public Arrow arrowPrefab;
    [Tooltip("How strongly the stage's clan colour tints the samurai sprite.")]
    [SerializeField, Range(0, 1)] float tintStrength = 0.2f;

    public Rigidbody Body { get; private set; }
    public int Column { get; private set; }
    public int Points { get; private set; }
    public bool IsDead { get; private set; }

    SamuraiArmy army;
    SpriteAnimator animator;
    CharacterArt art;
    Color clanTint;

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<SpriteAnimator>();
    }

    public void Init(SamuraiArmy owner, int column, int points, Color tint, CharacterArt characterArt)
    {
        army = owner;
        Column = column;
        Points = points;
        art = characterArt;
        clanTint = tint;

        SetFrost(0);
        animator.Play(art.idle, true, null, art.idleFps);
    }

    public void Fire(float arrowSpeed)
    {
        // Spawn just below the samurai's body (its collider radius is 0.5 in local space), whatever its scale.
        Vector3 muzzle = transform.position + Vector3.down * (0.5f * transform.lossyScale.y + 0.1f);
        Arrow arrow = Instantiate(arrowPrefab, muzzle, Quaternion.identity);
        arrow.Launch(arrowSpeed);
    }

    // Tints the samurai towards ice blue while the army is chilled or frozen (0 = normal, 1 = fully frozen).
    public void SetFrost(float amount)
    {
        if (IsDead) return;
        Color clan = Color.Lerp(Color.white, clanTint, tintStrength);
        animator.Renderer.color = Color.Lerp(clan, FrostColor, amount * 0.7f);
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
