using System;
using UnityEngine;

// Poguru's pixel clips (Art/Sprites/Characters/Poguru/Poguru_<Clip>_<i>.png, wired by Ninja/Apply Boss Art). Any clip
// left empty plays his idle; with no idle at all he's a teal glow with two eyes.
[Serializable]
public class PoguruArt
{
    public Sprite[] idle;
    public Sprite[] talk;
    [Tooltip("Swelling out of the wisp.")]
    public Sprite[] pop;
    [Tooltip("Headbutting an arrow aside.")]
    public Sprite[] bonk;
    public Sprite[] annoyed;
    [Tooltip("Pointing; also the HUD pointer in the tutorial.")]
    public Sprite[] point;
    public Sprite[] gasp;
    public Sprite[] cheer;
    public float fps = 8f;
}

// Poguru, the Lost Dragon (Story/Poguru.md): a baby dragon of soul-fire who lives in Oni's left wisp. He pops out of
// it to talk, hovers at Oni's shoulder, perches on his nest in the temple, and in the tutorial bonks arrows aside.
// He never fights and can't be hit: he has no collider. Code-built, drawn at Oni's pixel size, and animated on
// unscaled time so he keeps moving while a talk scene holds the field still.
public class Poguru : MonoBehaviour
{
    public enum Mode { Hidden, Popping, Hover, Guard, Returning, Perch }

    const int Wisp = 0;
    const float PopTime = 0.45f;
    const float DashSpeed = 14f;
    const float ReturnSpeed = 9f;
    // How soon an arrow must be due to land on Oni for him to go after it, and how close counts as a bonk.
    const float LookAhead = 0.35f;
    const float BonkReach = 0.55f;
    // Without art: the glow's width, in world units.
    const float FallbackSize = 0.75f;
    // How red he goes, fully angry.
    static readonly Color AngryTint = new Color(1f, 0.42f, 0.36f);

    PoguruArt art;
    Ronin ronin;
    SpriteRenderer body;
    SpriteRenderer glow;
    SpriteRenderer[] eyes;
    bool hasArt;
    Vector3 bodyScale;

    Mode mode = Mode.Hidden;
    Sprite[] clip;
    bool clipLoop;
    float clipAt;
    float talkUntil;
    float ghostUntil;
    float poppedAt;
    float grow;
    // Where he hovers when not following Oni (a DashTo's end), and the nest he perches on.
    Vector3? holdAt;
    float dashSpeed = DashSpeed;
    Action arrived;
    Vector3 perch;
    Arrow chasing;
    int blocks;
    Vector3 lastPosition;

    // How cross he is, 0 to 1 (the tutorial's guarded round): he reddens, shakes and steams.
    public float Anger { get; set; }
    float nextSteam;

    public Mode Current => mode;
    public bool IsOut => mode != Mode.Hidden;
    public int Blocks => blocks;
    public Vector3 Centre => transform.position;
    public Sprite[] PointFrames => Has(art?.point) ? art.point : Has(art?.idle) ? art.idle : null;
    // Each arrow bonked away while guarding, with the count so far.
    public event Action<int> Blocked;

    static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;

    public static Poguru Create(PoguruArt art, Ronin ronin)
    {
        var poguru = new GameObject("Poguru").AddComponent<Poguru>();
        poguru.art = art ?? new PoguruArt();
        poguru.ronin = ronin;
        poguru.Build();
        poguru.SetVisible(false);
        return poguru;
    }

    void Build()
    {
        hasArt = Has(art.idle);
        int order = ronin.SpriteOrder + 2;
        glow = Layer("Glow", ProceduralSprites.SoftGlow, order - 1);
        glow.transform.localScale = Vector3.one * (1.9f / ProceduralSprites.SoftGlow.bounds.size.x);
        glow.color = new Color(GameManager.PoguruColour.r, GameManager.PoguruColour.g, GameManager.PoguruColour.b, 0.35f);
        if (hasArt)
        {
            body = Layer("Body", art.idle[0], order);
            // His frames are drawn at Oni's pixel size and stand on their bottom edge; the body is shifted so his
            // middle sits on this object, which is the point he flies by.
            bodyScale = Vector3.one * (ronin.ArtPixelSize * art.idle[0].pixelsPerUnit);
            body.transform.localScale = bodyScale;
            body.transform.localPosition = -Vector3.Scale(art.idle[0].bounds.center, bodyScale);
            Play(art.idle, true);
            return;
        }
        // No art yet: a ball of his soul-fire with two white eyes.
        body = Layer("Body", ProceduralSprites.Glow, order);
        bodyScale = Vector3.one * (FallbackSize / ProceduralSprites.Glow.bounds.size.x);
        body.transform.localScale = bodyScale;
        body.color = Color.Lerp(GameManager.PoguruColour, Color.white, 0.2f);
        eyes = new SpriteRenderer[4];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            eyes[i] = Layer("Eye", ProceduralSprites.Disc, order + 1);
            eyes[i].transform.localScale = Vector3.one * (0.17f / ProceduralSprites.Disc.bounds.size.x);
            eyes[i].transform.localPosition = new Vector3(side * 0.11f, 0.06f, 0f);
            eyes[i + 2] = Layer("Pupil", ProceduralSprites.Disc, order + 2);
            eyes[i + 2].transform.localScale = Vector3.one * (0.08f / ProceduralSprites.Disc.bounds.size.x);
            eyes[i + 2].transform.localPosition = new Vector3(side * 0.11f + 0.02f, 0.05f, 0f);
            eyes[i + 2].color = new Color(0.05f, 0.08f, 0.12f);
        }
    }

    SpriteRenderer Layer(string name, Sprite sprite, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = sprite;
        r.sortingOrder = order;
        return r;
    }

    void SetVisible(bool on)
    {
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = on;
    }

    void SetOrder(int order)
    {
        glow.sortingOrder = order - 1;
        body.sortingOrder = order;
        if (eyes == null) return;
        for (int i = 0; i < eyes.Length; i++) eyes[i].sortingOrder = order + (i < 2 ? 1 : 2);
    }

    // A clip, looped or once (then back to his idle). An empty clip plays the idle.
    public void Play(Sprite[] frames, bool loop = false)
    {
        if (!Has(frames)) frames = art.idle;
        clip = frames;
        clipLoop = loop || frames == art.idle;
        clipAt = Time.unscaledTime;
    }

    // Moves his mouth for this long, then goes back to his idle.
    public void Talk(float seconds)
    {
        talkUntil = Time.unscaledTime + seconds;
        Play(art.talk, true);
    }

    // Cut short: his line was stopped, so he stops mouthing it.
    public void Hush()
    {
        if (talkUntil <= 0f) return;
        talkUntil = 0f;
        Play(art.idle, true);
    }

    // Something passes right through him (the tutorial's spear): he goes see-through and flickers for a moment.
    public void Ghost(float seconds) => ghostUntil = Time.unscaledTime + seconds;

    public void ResetBlocks(int count = 0) => blocks = count;

    // Out of the wisp at Oni's shoulder: he swells out of its flame and floats up beside him.
    public void PopOut()
    {
        if (mode != Mode.Hidden && mode != Mode.Returning) return;
        bool fromWisp = mode == Mode.Hidden;
        mode = Mode.Popping;
        holdAt = null;
        poppedAt = Time.unscaledTime;
        if (fromWisp)
        {
            transform.position = ronin.WispPoint(Wisp);
            grow = 0.15f;
        }
        ronin.SetWispHidden(Wisp, true);
        SetVisible(true);
        Play(art.pop);
        Fx.Play(Fx.Impact, transform.position, GameManager.PoguruColour, 0.6f);
        Sfx.Play(Sfx.SoulSpawn, 0.5f, 1.6f);
    }

    public void ReturnToWisp()
    {
        if (mode == Mode.Hidden) return;
        mode = Mode.Returning;
        holdAt = null;
        chasing = null;
        talkUntil = 0f;
    }

    // Straight back into the wisp, unseen (a run that ends, a scene that reloads).
    public void Hide()
    {
        mode = Mode.Hidden;
        holdAt = null;
        chasing = null;
        SetVisible(false);
        ronin.SetWispHidden(Wisp, false);
    }

    // Follows Oni at his shoulder.
    public void Hover()
    {
        if (mode == Mode.Hidden) PopOut();
        else if (mode != Mode.Popping) mode = Mode.Hover;
        holdAt = null;
        chasing = null;
    }

    // Hovers over Oni and bonks away the arrows about to hit him; off, he just hovers.
    public void Guard(bool on)
    {
        if (on && mode == Mode.Hidden) PopOut();
        if (on) mode = mode == Mode.Popping ? Mode.Popping : Mode.Guard;
        else if (mode == Mode.Guard) mode = Mode.Hover;
        guarding = on;
        holdAt = null;
        chasing = null;
    }

    bool guarding;

    // Flies to a point and holds there (a spear's path, a spot on Oni's row); `onArrive` once he's there.
    public void DashTo(Vector3 point, Action onArrive = null, float speed = DashSpeed)
    {
        if (mode == Mode.Hidden) PopOut();
        if (mode != Mode.Popping) mode = Mode.Hover;
        guarding = false;
        holdAt = point;
        dashSpeed = speed;
        arrived = onArrive;
    }

    // Settles on his nest in the temple, flying there from wherever he is (out of the wisp if he's in it).
    public void PerchAt(Vector3 nest, bool instantly = false)
    {
        perch = nest;
        if (instantly)
        {
            transform.position = nest;
            grow = 1f;
            SetVisible(true);
            ronin.SetWispHidden(Wisp, true);
            Play(art.idle, true);
            mode = Mode.Perch;
            return;
        }
        if (mode == Mode.Hidden) PopOut();
        holdAt = null;
        if (mode != Mode.Popping) mode = Mode.Perch;
        else perchAfterPop = true;
    }

    bool perchAfterPop;

    // Beside Oni's shoulder, kept inside the view.
    Vector3 HoverSpot()
    {
        Bounds b = ronin.Bounds;
        Vector3 spot = b.center + new Vector3(-1.2f, 0.9f, 0f);
        Camera cam = Camera.main;
        float half = cam.orthographicSize * cam.aspect;
        // Near the left edge he takes the other shoulder.
        if (spot.x < CameraShake.Center.x - half + 0.6f) spot.x = b.center.x + 1.2f;
        return spot;
    }

    Vector3 GuardSpot() => new Vector3(ronin.Bounds.center.x - 0.5f, ronin.TopY + 1.2f, 0f);

    void Update()
    {
        if (ronin == null || mode == Mode.Hidden) return;
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;
        float bob = Mathf.Sin(now * 3.1f) * 0.08f;
        Vector3 at = transform.position;

        switch (mode)
        {
            case Mode.Popping:
                float k = Mathf.Clamp01((now - poppedAt) / PopTime);
                grow = Mathf.Max(grow, Mathf.Lerp(0.15f, 1f, k));
                at = Vector3.Lerp(at, HoverSpot(), 1f - Mathf.Exp(-8f * dt));
                if (k >= 1f)
                {
                    mode = perchAfterPop ? Mode.Perch : holdAt.HasValue ? Mode.Hover : guarding ? Mode.Guard : Mode.Hover;
                    perchAfterPop = false;
                }
                break;
            case Mode.Hover:
                if (holdAt is Vector3 hold)
                {
                    at = Vector3.MoveTowards(at, hold, dashSpeed * dt);
                    if (arrived != null && (at - hold).sqrMagnitude < 0.0025f)
                    {
                        Action done = arrived;
                        arrived = null;
                        done();
                    }
                }
                else at = Vector3.Lerp(at, HoverSpot() + Vector3.up * bob, 1f - Mathf.Exp(-6f * dt));
                break;
            case Mode.Guard:
                at = UpdateGuard(at, dt, bob);
                break;
            case Mode.Returning:
                Vector3 wisp = ronin.WispPoint(Wisp);
                at = Vector3.MoveTowards(at, wisp, ReturnSpeed * dt);
                float left = Vector3.Distance(at, wisp);
                grow = Mathf.Min(grow, Mathf.Clamp01(left / 1.2f) * 0.85f + 0.15f);
                if (left < 0.05f)
                {
                    transform.position = at;
                    Hide();
                    return;
                }
                break;
            case Mode.Perch:
                at = Vector3.Lerp(at, perch + Vector3.up * (bob * 0.6f), 1f - Mathf.Exp(-5f * dt));
                break;
        }
        // Fuming, he shakes on the spot.
        Vector3 shake = Anger > 0.3f ? new Vector3(Mathf.Sin(now * 70f), Mathf.Cos(now * 53f), 0f) * (0.05f * (Anger - 0.3f)) : Vector3.zero;
        transform.position = new Vector3(at.x, at.y, -0.3f) + shake;
        transform.localScale = Vector3.one * grow;
        if (mode != Mode.Popping && mode != Mode.Returning) grow = Mathf.MoveTowards(grow, 1f, dt * 3f);

        // On the nest he's drawn over its prop, which stands over Oni when he's behind it.
        SetOrder(ronin.SpriteOrder + (mode == Mode.Perch ? 6 : 2));
        Face(at);
        Animate(now);
        lastPosition = at;
    }

    // Faces the way he's flying, or Oni when still.
    void Face(Vector3 at)
    {
        float dx = at.x - lastPosition.x;
        bool left = Mathf.Abs(dx) > 0.002f ? dx < 0f : ronin.Bounds.center.x < at.x;
        body.flipX = left;
    }

    void Animate(float now)
    {
        if (talkUntil > 0f && now > talkUntil)
        {
            talkUntil = 0f;
            Play(art.idle, true);
        }
        float alpha = now < ghostUntil ? 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(now * 30f)) : 1f;
        Color soul = Color.Lerp(GameManager.PoguruColour, AngryTint, Anger);
        glow.color = new Color(soul.r, soul.g, soul.b, 0.3f * alpha + 0.06f * Mathf.Sin(now * 4f));
        Color tint = Color.Lerp(Color.white, AngryTint, Anger * 0.75f);
        // Past half angry, puffs of steam off his head, quicker the crosser he gets.
        if (Anger > 0.5f && now >= nextSteam)
        {
            nextSteam = now + Mathf.Lerp(0.7f, 0.2f, Anger);
            ElementFx.Glow(transform.position + new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), 0.45f, 0f), new Color(1f, 1f, 1f, 0.5f), 0.15f, 0.5f, 0.5f);
        }
        if (hasArt)
        {
            int frame = (int)((now - clipAt) * Mathf.Max(1f, art.fps));
            if (!clipLoop && frame >= clip.Length) Play(talkUntil > 0f ? art.talk : art.idle, true);
            else body.sprite = clip[clipLoop ? frame % clip.Length : frame];
            body.color = new Color(tint.r, tint.g, tint.b, alpha);
            return;
        }
        // The stand-in breathes, and squashes as he talks.
        float squash = talkUntil > 0f ? 1f + 0.12f * Mathf.Sin(now * 22f) : 1f + 0.04f * Mathf.Sin(now * 3f);
        body.transform.localScale = new Vector3(bodyScale.x * (2f - squash), bodyScale.y * squash, 1f);
        body.color = new Color(body.color.r, body.color.g, body.color.b, alpha);
        foreach (SpriteRenderer eye in eyes) eye.color = new Color(eye.color.r, eye.color.g, eye.color.b, alpha);
    }

    // Each frame: the arrow due to hit Oni soonest, if he can reach it; dash to it and bonk it away. He leaves them
    // all alone while Oni parries, so the parry can be practised, and never touches spears or a boss's reflectable
    // slashes.
    Vector3 UpdateGuard(Vector3 at, float dt, float bob)
    {
        if (chasing == null || !chasing.isActiveAndEnabled || chasing.Velocity.y >= 0f || chasing.transform.position.y < ronin.TopY - 0.4f)
            chasing = Threat();
        if (chasing == null)
            return Vector3.Lerp(at, GuardSpot() + Vector3.up * bob, 1f - Mathf.Exp(-6f * dt));
        Vector3 aim = chasing.transform.position + chasing.Velocity * 0.04f;
        aim.z = at.z;
        at = Vector3.MoveTowards(at, aim, DashSpeed * dt);
        if (Vector2.Distance(at, chasing.transform.position) <= BonkReach)
        {
            chasing.Pop(GameManager.PoguruColour);
            chasing = null;
            blocks++;
            Play(art.bonk);
            Sfx.Play(Sfx.Deflect, 0.6f, 1.4f);
            Blocked?.Invoke(blocks);
        }
        return at;
    }

    Arrow Threat()
    {
        if (ronin.Parrying || !ronin.ControlsEnabled) return null;
        Bounds b = ronin.Bounds;
        float half = b.extents.x + 0.2f;
        float top = ronin.TopY;
        Arrow best = null;
        float soonest = LookAhead;
        foreach (Arrow arrow in FindObjectsByType<Arrow>())
        {
            if (!arrow.isActiveAndEnabled || arrow.Unbreakable || arrow.Reflectable) continue;
            Vector3 v = arrow.Velocity;
            if (v.y >= -0.01f) continue;
            Vector3 p = arrow.transform.position;
            float t = (p.y - top) / -v.y;
            if (t < 0f || t > soonest) continue;
            if (Mathf.Abs(p.x + v.x * t - b.center.x) > half) continue;
            best = arrow;
            soonest = t;
        }
        return best;
    }

    void OnDestroy()
    {
        if (ronin != null) ronin.SetWispHidden(Wisp, false);
    }
}
