using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The things a boss says out loud; GameManager plays the matching voice clip and subtitle. Intro, Phase and
// Defeat are full lines; the rest are short barks that never cut one off.
public enum BossLine { Intro, Phase, Defeat, Hurt, Stagger, SmokeRings, RosaryLash, Iaido, RosaryBloom,
    KasaWheel, Lattice, Escapement, PlumbLines, HeartLantern, Midnight }

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
    public event Action Defeated;
    public event Action<Boss, BossLine> Spoke;

    public abstract string Title { get; }
    public abstract string Epithet { get; }
    public abstract Color Colour { get; }

    // The subtitle for a line, or null when the boss has nothing to say there.
    public virtual string Line(BossLine line) => null;

    protected void Speak(BossLine line)
    {
        Spoke?.Invoke(this, line);
    }

    public int Health { get; private set; }
    public int MaxHealth { get; private set; }
    public float HealthFraction => (float)Health / MaxHealth;
    public bool IsDead { get; private set; }
    public Vector3 Centre => sprite.bounds.center;

    protected Rigidbody body;
    protected SpriteRenderer sprite;
    protected SpriteAnimator animator;
    protected CharacterArt art;
    protected Ronin ronin;
    protected Arrow arrowPrefab;
    protected Vector3 anchor;
    protected Color bodyTint = Color.white;
    // Telegraphs and hazards the current attack has out; cleared when the attack is interrupted.
    protected readonly List<GameObject> transient = new List<GameObject>();

    float frozenUntil;
    float staggeredUntil;
    float invulnerableUntil;
    float nextHurtLine;
    float age;
    Coroutine fight;
    bool fighting;
    float lastBeat;

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
        boss.Health = boss.MaxHealth = health;
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
    }

    protected abstract IEnumerator Fight();

    // Called after every hit that leaves the boss standing, for phase changes.
    protected virtual void OnDamaged() { }

    // Called when a reflected strike breaks the boss's stance, for its own reaction on top of the wobble.
    protected virtual void OnStaggered() { }

    protected void RestartFight()
    {
        if (fight != null) StopCoroutine(fight);
        lastBeat = Time.time;
        fight = fighting && !IsDead ? StartCoroutine(Fight()) : null;
    }

    // Fight loops call this each time they start a move, to show the watchdog they're alive.
    protected void Beat()
    {
        lastBeat = Time.time;
    }

    // True while a subclass is in a set piece (a phase break) and isn't expected to beat.
    protected virtual bool Busy => false;

    // Extra damage taken while a weak spot is open (Utsusemi with its hat off).
    protected virtual int DamageMultiplier => 1;

    // How long a reflected strike breaks the boss's stance.
    protected virtual float StaggerSeconds => 2f;

    // An exception inside an attack silently ends its coroutine; rather than stand idle for the rest of the
    // fight, the boss notices it has stopped moving and starts over.
    void Update()
    {
        if (!fighting || IsDead || Busy || Staggered || Frozen)
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

    protected void PlayIdle()
    {
        animator.Play(IdleClip, true, null, art.idleFps);
    }

    protected void SetInvulnerable(float seconds)
    {
        invulnerableUntil = Time.time + seconds;
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
        return arrow;
    }

    // Shots fired together start out overlapping, and arrows collide with each other, so without this a volley
    // destroys itself the moment it's fired.
    protected static void IgnoreEachOther(List<Arrow> volley)
    {
        for (int i = 0; i < volley.Count; i++)
        {
            for (int j = 0; j < i; j++) volley[i].IgnoreCollisionsWith(volley[j]);
        }
    }

    public void Freeze(float seconds)
    {
        frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
    }

    // Broken stance: the boss stops dead and takes double damage for a moment.
    public void Stagger(float seconds)
    {
        if (IsDead) return;
        Interrupt();
        staggeredUntil = Time.time + seconds;
        PlayAction(art.hit);
        Sfx.Play(Sfx.BossStagger);
        Speak(BossLine.Stagger);
        OnStaggered();
        nextHurtLine = Time.time + seconds;
        StartCoroutine(RecoverFromStagger(seconds));
    }

    IEnumerator RecoverFromStagger(float seconds)
    {
        float tilt = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
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
        if (IsDead || Time.time < invulnerableUntil) return;
        if (Staggered) damage *= 2;
        damage *= DamageMultiplier;
        Health = Mathf.Max(0, Health - damage);

        Vector3 point = Centre;
        HitFlash.Flash(sprite, 0.1f);
        Sfx.Play(Sfx.BossHit);
        Damaged?.Invoke(point, source);
        if (IsDead) return;

        if (Health <= 0)
        {
            Die();
            return;
        }
        if (source == KillSource.Reflected) Stagger(StaggerSeconds);
        else if (Time.time >= nextHurtLine)
        {
            nextHurtLine = Time.time + HurtLineGap;
            Speak(BossLine.Hurt);
        }
        OnDamaged();
    }

    void Die()
    {
        IsDead = true;
        fighting = false;
        StopAllCoroutines();
        ClearTransient();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Defeated?.Invoke();
        Speak(BossLine.Defeat);
        StartCoroutine(Fall());
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

    void OnDestroy()
    {
        ClearTransient();
    }
}

// Fades a sprite out and removes it.
public class Fader : MonoBehaviour
{
    SpriteRenderer target;
    float seconds;
    float startAlpha;
    float age;

    public static void Fade(SpriteRenderer renderer, float seconds)
    {
        var fader = renderer.gameObject.AddComponent<Fader>();
        fader.target = renderer;
        fader.seconds = seconds;
        fader.startAlpha = renderer.color.a;
    }

    void Update()
    {
        age += Time.deltaTime;
        Color c = target.color;
        c.a = startAlpha * (1f - age / seconds);
        target.color = c;
        if (age >= seconds) Destroy(gameObject);
    }
}
