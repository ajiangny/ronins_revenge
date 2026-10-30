using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kuon, Shirogane's spirit wolf, the guardian of her family's shrine: a translucent body of blue spirit flame with
// burning cyan eyes. A faint silhouette behind her in her first phase; once she calls it, it keeps its own ground on
// her row, padding from spot to spot, and vanishes into mist to attack from wherever it reappears. Everything it does
// to the ronin is an Arrow it throws, but from her phase break it can be hit while it stands in sight, its health
// sharing her bar. If she falls first it fights on enraged, bigger and burning red; broken, it fades to a shadow at
// her side until her fall.
[RequireComponent(typeof(Rigidbody))]
public class SpiritWolf : MonoBehaviour, IHittable
{
    public enum Presence { Silhouette, Present }

    // Scaled by Boss.HealthScale (the oaths) when it is armed, like the warlords.
    public const int StartingHealth = Shirogane.SecondPhaseShare;
    // A parried Fang of the Oath does no damage by Damage.Of; on Kuon it lands as a heavy blow and dazes it.
    const int ReflectedDamage = 20;
    const float DazeSeconds = 2f;
    const int SamuraiLayer = 7;

    static readonly Color Body = new Color(0.6f, 0.9f, 1f);
    static readonly Color EyeColour = new Color(0.4f, 1f, 1f);

    public event System.Action<Vector3, KillSource> Damaged;
    public event System.Action Broken;

    public int MaxHealth { get; private set; } = StartingHealth;
    public int Health { get; private set; } = StartingHealth;
    public float HealthFraction => (float)Health / MaxHealth;
    // In the fight as a foe of its own, from her phase break until her fall.
    public bool Armed { get; private set; }
    public bool IsBroken { get; private set; }
    public bool Dazed => Time.time < dazedUntil;
    // Soaked by Asagi's water, as a boss is: every boon effect lands one point harder (TakeHit).
    public bool Soaked => Time.time < soakedUntil;
    // She has fallen and it fights on for both of them.
    public bool Enraged { get; private set; }
    // Only while it stands in plain sight: not in mist, not as the charge coming down a lane.
    public bool Targetable => Armed && !IsBroken && !hidden && shown > 0.5f;
    public Vector3 Centre => sprite.bounds.center;
    float dazedUntil;
    float soakedUntil;
    static readonly Color SoakTint = new Color(0.18f, 0.48f, 1f);
    // Half points from shadow slashes that haven't yet made a whole one.
    float shadowChip;
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
    // Held, it stays where it stands instead of padding about (mid-howl, mid-lance).
    bool held;
    // Taking shape inside the soul at her phase break: its presence is set from outside, frame by frame.
    bool forming;
    // Its own spot on her row, and when it next moves on from it.
    Vector3 den;
    float nextProwl;
    // Eases in from 0 to 1 as it enrages: bigger and redder.
    float rage;

    static readonly Color RageColour = new Color(1f, 0.4f, 0.5f);

    // Where it keeps to between moves: its own ground while it fights, else her side.
    public Vector3 Home => Armed && !IsBroken ? den : owner.WolfRest;

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
        wolf.glow = Projectile.CreateGlow(go.transform, 2.6f / scale, order - 1, new Color(0.3f, 0.9f, 1f, 0.3f));
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
        Health = MaxHealth = Mathf.RoundToInt(StartingHealth * Boss.HealthScale);
        IsBroken = false;
        Armed = true;
        den = transform.position;
        nextProwl = Time.time + 2f;
    }

    // She has fallen first: it howls and swells, burning red, and fights on harder.
    public void Enrage()
    {
        if (!Armed || IsBroken) return;
        Enraged = true;
        PlayHowl();
        Glare(1.2f);
        Sfx.Play(Sfx.WolfHowl, 1f, 0.7f);
        Fx.Play(Fx.Explosion, Centre, RageColour, 2.2f);
        CameraShake.Add(0.4f);
    }

    // Reeling where it stands, every blow landing twice as hard (its spent ultimate).
    public void Daze(float seconds)
    {
        dazedUntil = Mathf.Max(dazedUntil, Time.time + seconds);
    }

    // Stays put for a move fired from where it stands, then pads on again.
    public void Hold() => held = true;
    public void Release() => held = false;

    // Melts away and takes shape on fresh ground of its own.
    public IEnumerator ReturnHome()
    {
        if (Armed && !IsBroken) den = owner.ProwlSpot();
        nextProwl = Time.time + Random.Range(1.5f, 3f);
        yield return AppearAt(Home);
    }

    // Out of the fight for good (her fall): nothing can hit it any more.
    public void Disarm()
    {
        Armed = false;
        hitbox.enabled = false;
    }

    // Soaks the wolf: a splash, and every boon effect on it lands harder until it dries.
    public void Soak(float seconds)
    {
        if (!Armed || IsBroken || seconds <= 0f) return;
        bool fresh = !Soaked;
        soakedUntil = Mathf.Max(soakedUntil, Time.time + seconds * Statuses.SoakScale);
        if (!fresh) return;
        if (Fx.Play(Fx.Splash, Centre, Color.white, 1.2f) == null) ElementFx.Glow(Centre, SoakTint, 0.7f, 2f, 0.35f);
        Sfx.Play(Sfx.Splash, 0.8f, 0.9f);
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (!Armed || IsBroken) return;
        if (!Targetable && source != KillSource.Reflected) return;
        if (Soaked && source == KillSource.Effect) damage++;
        if (source == KillSource.Reflected)
        {
            damage = ReflectedDamage;
            dazedUntil = Time.time + DazeSeconds;
            Sfx.Play(Sfx.WolfGrowl, 1f, 1.6f);
        }
        // Dazed, it can't guard itself: every other blow lands twice as hard.
        else if (Dazed) damage *= 2;
        damage = Damage.Lessen(damage, source, ref shadowChip);
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
        Enraged = false;
        held = false;
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

    // Keeps to its home: its own ground while it fights, padding to a fresh spot now and then; her side otherwise.
    void Follow()
    {
        if (following == null) following = StartCoroutine(Following());
    }

    IEnumerator Following()
    {
        while (true)
        {
            if (!hidden && !held && presence == Presence.Present)
            {
                if (Armed && !IsBroken && Time.time >= nextProwl)
                {
                    den = owner.ProwlSpot();
                    nextProwl = Time.time + Random.Range(2f, 3.5f);
                }
                transform.position = Vector3.Lerp(transform.position, Home, 1f - Mathf.Exp(-Time.deltaTime * 3f));
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
        forming = false;
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

    // Takes shape inside the merging soul at her phase break: drawn at `amount` of its full presence, held at `at`
    // until it appears for real.
    public void Form(Vector3 at, float amount)
    {
        StopFollowing();
        forming = true;
        hidden = false;
        transform.position = at;
        shown = amount;
        Apply();
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

    // Back home without ceremony, when a stagger cuts a move of theirs short.
    public void Recall()
    {
        StopAllCoroutines();
        following = null;
        hidden = false;
        held = false;
        shown = shownTarget;
        grown = 1f;
        transform.position = Home;
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
        rage = Mathf.MoveTowards(rage, Enraged ? 1f : 0f, Time.deltaTime * 1.5f);
        // A dazed wolf reels where it stands.
        sprite.transform.localRotation = Dazed && !IsBroken ? Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 18f) * 8f) : Quaternion.identity;
        if (hidden || forming) return;
        shown = Mathf.MoveTowards(shown, shownTarget, Time.deltaTime * 1.5f);
        Apply();
    }

    void Apply()
    {
        float a = hidden ? 0f : shown;
        float flicker = 0.9f + 0.1f * Mathf.Sin(Time.time * 7f);
        // Enraged, its flame runs hot: a red heart pulsing through the blue, and its glow and eyes burn red.
        float heat = rage * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
        Color body = Color.Lerp(Body, RageColour, 0.35f * heat);
        if (Soaked) body = Color.Lerp(body, SoakTint, 0.35f);
        sprite.color = new Color(body.r, body.g, body.b, a * 0.78f * flicker);
        transform.localScale = Vector3.one * scale * grown * (1f + 0.25f * rage);
        Color halo = Color.Lerp(new Color(0.3f + 0.5f * glare, 0.9f, 1f), RageColour, 0.7f * rage);
        glow.color = new Color(halo.r, halo.g, halo.b, a * (0.3f + 0.4f * glare + 0.2f * rage));
        if (eyes == null) return;
        Color eye = Color.Lerp(EyeColour, RageColour, rage);
        foreach (SpriteRenderer e in eyes) e.color = new Color(eye.r, eye.g, eye.b, Mathf.Min(1f, a * 1.6f + glare));
    }
}
