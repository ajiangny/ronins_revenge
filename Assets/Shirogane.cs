using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 3's warlord: Shirogane, the Forsaken Blade, a ghostly swordswoman in black, white and deep-blue armour
// whose twin katanas burn with blue spirit flame. She fights alone in her first phase, gliding about the top of
// the field and raining spectral blades; once her first bar empties her spirit wolf answers her and the two
// fight as one. Everything she throws is a readable pattern with a lane or a gap left open.
public class Shirogane : Boss
{
    // Per bar: she fights two, the second with her wolf.
    public const int StartingHealth = 140;

    static readonly Color Spirit = new Color(0.45f, 0.85f, 1f);
    static readonly Color Cyan = new Color(0.3f, 0.92f, 1f);
    static readonly Color Frost = new Color(0.85f, 0.97f, 1f);
    static readonly Color MistColour = new Color(0.55f, 0.85f, 1f, 0.7f);
    static readonly Color Trail = new Color(0.45f, 0.85f, 1f, 0.5f);

    enum Move { CrescentSlash, BladeRain, MoonlitDash, BladeBarrage, WolfCharge, Howl, TwinAssault, WolfPack, EclipseFang }

    // Each value is (phase 1, phase 2): with the wolf at her side she gives no quarter.
    float Recovery => secondPhase ? 0.5f : 1f;
    float ComboChance => secondPhase ? 0.75f : 0.5f;
    int MaxCombo => secondPhase ? 3 : 2;
    float GlideSpeed => secondPhase ? 7.5f : 5f;
    float CrescentSpeed => secondPhase ? 9f : 8f;
    float RainWarning => secondPhase ? 0.7f : 0.9f;

    public override string Title => "SHIROGANE";
    public override string Epithet => "The Forsaken Blade";
    public override Color Colour => Spirit;

    public override string Line(BossLine line) => line switch
    {
        BossLine.Intro => "I remember your blade. Do you remember mine?",
        BossLine.Phase => "You still fight as you did before... Then let us remind you what you've forgotten.",
        BossLine.Defeat => "Even after all this time... you still wield the blade I remember.",
        BossLine.DefeatFinal => "Go. Finish what we started.",
        BossLine.Stagger => "My stance...",
        BossLine.CrescentSlash => "Crescent Moon.",
        BossLine.BladeRain => "Blades... fall.",
        BossLine.MoonlitDash => "Too slow.",
        BossLine.BladeBarrage => "Scatter.",
        BossLine.WolfCharge => "Go.",
        BossLine.Howl => "Sing for them.",
        BossLine.TwinAssault => "Together.",
        BossLine.WolfPack => "Hunt.",
        BossLine.EclipseFang => "Eclipse... Fang.",
        _ => null,
    };

    bool secondPhase => SecondPhase;
    bool breaking;
    Move? lastMove;
    // How many times running the last move has come up: never the same major move three times in a row.
    int repeats;
    Move? lastWolfMove;
    bool lastWasWolf;
    int movesSinceEclipse;
    // True for the recovery after the Eclipse Fang, when she stands open and takes double damage.
    bool exposed;
    Coroutine roaming;
    // Her blades' spirit flame, lit at her hands; dark while she is spent.
    SpriteRenderer bladeGlow;
    // The dash frames from the GameManager, with her slash as the fallback.
    Sprite[] dashFrames;

    protected override bool Busy => breaking;
    protected override float StaggerSeconds => secondPhase ? 1.6f : 2.2f;
    protected override int DamageMultiplier => exposed ? 2 : 1;
    float TopOfScreen => CameraShake.Center.y + Camera.main.orthographicSize + 0.5f;
    float GroundY => ronin.Feet.y - 0.3f;
    float LeftEdge => CameraShake.Center.x - HalfWidth;
    float RightEdge => CameraShake.Center.x + HalfWidth;
    Vector3 Hands => Centre + Vector3.down * (sprite.bounds.size.y * 0.1f);

    public void SetDashFrames(Sprite[] dash)
    {
        dashFrames = dash != null && dash.Length > 0 ? dash : null;
    }

    // The wolf's clips; any that are missing leave it drawn in code.
    Sprite[] wolfIdle, wolfHowl, wolfCharge, wolfVanish;

    public void SetWolfFrames(Sprite[] idle, Sprite[] howl, Sprite[] charge, Sprite[] vanish)
    {
        wolfIdle = idle != null && idle.Length > 0 ? idle : null;
        wolfHowl = howl != null && howl.Length > 0 ? howl : null;
        wolfCharge = charge != null && charge.Length > 0 ? charge : null;
        wolfVanish = vanish != null && vanish.Length > 0 ? vanish : null;
    }

    void Start()
    {
        bladeGlow = Kunai.CreateGlow(sprite.transform, 1.6f / transform.lossyScale.x, sprite.sortingOrder - 1, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        bladeGlow.transform.localPosition = new Vector3(0f, -0.1f, 0.05f);
        StartCoroutine(PulseBlades());
    }

    IEnumerator PulseBlades()
    {
        while (bladeGlow != null && !IsDead)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            float lit = exposed || breaking ? 0.08f : 0.25f + 0.2f * pulse;
            bladeGlow.color = new Color(Cyan.r, Cyan.g, Cyan.b, lit);
            bladeGlow.transform.localScale = Vector3.one * ((1.5f + 0.3f * pulse) / transform.lossyScale.x);
            yield return null;
        }
    }

    protected override IEnumerator Fight()
    {
        exposed = false;
        while (true)
        {
            while (breaking) yield return null;
            // She glides on between combos: the opening is there, but she never stands still for it.
            yield return Roam(Recovery);

            int combo = 1;
            while (combo < MaxCombo && Random.value < ComboChance) combo++;
            for (int i = 0; i < combo; i++)
            {
                Beat();
                Move move = NextMove();
                bool wolf = move >= Move.WolfCharge;
                if (move == Move.EclipseFang)
                {
                    // The ultimate ends every combo: nothing follows it but her recovery.
                    i = combo;
                }
                else if (!wolf && move != Move.MoonlitDash)
                {
                    yield return Glide(NearX(1.5f, 3.5f));
                }
                repeats = move == lastMove ? repeats + 1 : 1;
                lastMove = move;
                lastWasWolf = wolf;
                if (wolf) lastWolfMove = move;
                movesSinceEclipse = move == Move.EclipseFang ? 0 : movesSinceEclipse + 1;
                switch (move)
                {
                    case Move.CrescentSlash: yield return CrescentSlash(); break;
                    case Move.BladeRain: yield return BladeRain(); break;
                    case Move.MoonlitDash: yield return MoonlitDash(); break;
                    case Move.BladeBarrage: yield return BladeBarrage(); break;
                }
                yield return Wait(secondPhase ? 0.15f : 0.25f);
            }
        }
    }

    // Weighted at random: the phase's signature counts twice, the same move never comes three times running, and
    // once the wolf is out her sword moves and its own alternate so the two never pile on at once.
    Move NextMove()
    {
        var options = new List<Move> { Move.CrescentSlash, Move.BladeRain, Move.BladeRain, Move.MoonlitDash, Move.BladeBarrage };
        if (lastMove is Move last && repeats >= 2) options.RemoveAll(m => m == last);
        return options[Random.Range(0, options.Count)];
    }

    // --- Movement ---

    // A smooth glide along her row to x, trailing a little mist; never a blink, so she can always be followed.
    IEnumerator Glide(float x)
    {
        Vector3 from = anchor;
        Vector3 to = new Vector3(x, from.y, from.z);
        float seconds = Mathf.Max(0.15f, Mathf.Abs(to.x - from.x) / GlideSpeed);
        float nextGhost = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
            if (t >= nextGhost)
            {
                nextGhost = t + 0.08f;
                Afterimage(new Color(Spirit.r, Spirit.g, Spirit.b, 0.18f));
            }
            yield return null;
        }
        anchor = to;
    }

    float NearX(float minStep, float maxStep)
    {
        float range = HalfWidth - 2.5f;
        float lo = CameraShake.Center.x - range, hi = CameraShake.Center.x + range;
        float step = Random.Range(minStep, maxStep) * (Random.value < 0.5f ? -1f : 1f);
        float x = anchor.x + step;
        if (x < lo || x > hi) x = anchor.x - step;
        return Mathf.Clamp(x, lo, hi);
    }

    // Drifts from spot to spot for a while, stopping short of its end so an attack never starts mid-glide.
    IEnumerator Roam(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until)
        {
            float x = NearX(1f, 2.5f);
            float trip = Mathf.Abs(x - anchor.x) / GlideSpeed;
            if (Time.time + trip > until) break;
            yield return Glide(x);
            yield return Wait(0.1f);
        }
        yield return Wait(Mathf.Max(0f, until - Time.time));
    }

    // Roams beside an attack that plays out on its own, so she isn't a still target while it does.
    void RoamMeanwhile(float seconds)
    {
        StopRoaming();
        roaming = StartCoroutine(Roam(seconds));
    }

    void StopRoaming()
    {
        if (roaming != null) StopCoroutine(roaming);
        roaming = null;
    }

    protected override void OnStaggered()
    {
        StopRoaming();
        exposed = false;
    }

    // --- Shots ---

    // Positions of n lanes spread evenly across the field, clear of the edges.
    List<float> Lanes(int count)
    {
        var lanes = new List<float>(count);
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        for (int i = 0; i < count; i++) lanes.Add(Mathf.Lerp(lo, hi, (i + 0.5f) / count));
        return lanes;
    }

    // A blade of blue spirit flame cast from her katana; it can be cut down by a slash.
    Arrow Crescent(Vector3 from, Vector3 velocity)
    {
        Arrow wave = Shoot(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroCrescent);
        if (frames != null) wave.Restyle(frames, 14f, Color.white, new Vector2(2.2f, 1.4f), 0.4f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 2.4f);
        else wave.Restyle(ProceduralSprites.Glow, Cyan, new Vector2(2f, 0.8f), 0.4f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), 2.4f);
        return wave;
    }

    // A ghostly katana that falls point first and drifts straight through cover.
    Arrow GhostSword(Vector3 from, Vector3 velocity)
    {
        Arrow blade = Shoot(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.ShiroSword);
        if (frames != null) blade.Restyle(frames, 10f, Color.white, new Vector2(0.7f, 1.9f), 0.3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f), 1.4f);
        else blade.Restyle(ProceduralSprites.White, Frost, new Vector2(0.16f, 1.6f), 0.3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 1.4f);
        blade.PassesCover = true;
        return blade;
    }

    // A ghost sword's picture, hanging where it will fall from; drawn in code when the art is missing.
    SpriteRenderer SwordPicture(Vector3 at, float angle)
    {
        var picture = new GameObject("GhostSword").AddComponent<SpriteRenderer>();
        picture.transform.position = at + Vector3.back * 0.3f;
        picture.transform.rotation = Quaternion.Euler(0, 0, angle);
        picture.sortingOrder = 54;
        Sprite[] frames = Fx.Frames(Fx.ShiroSword);
        if (frames != null)
        {
            picture.sprite = frames[0];
            picture.transform.localScale = Vector3.one * (1.9f / frames[0].bounds.size.y);
            picture.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 10f);
            picture.color = new Color(1f, 1f, 1f, 0.85f);
        }
        else
        {
            picture.sprite = ProceduralSprites.White;
            picture.transform.localScale = new Vector3(0.16f, 1.6f, 1f);
            picture.color = Frost;
        }
        transient.Add(picture.gameObject);
        return picture;
    }

    // A thin lane warning from the top of the field to the ground that brightens as the blow comes.
    SpriteRenderer LaneWarning(float x)
    {
        return Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), 0.1f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));
    }

    IEnumerator Brighten(List<SpriteRenderer> tells, float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            float flicker = k > 0.7f ? 0.5f + 0.5f * Mathf.PingPong(t * 16f, 1f) : 1f;
            foreach (SpriteRenderer tell in tells)
            {
                if (tell != null) tell.color = new Color(Cyan.r, Cyan.g, Cyan.b, (0.3f + 0.6f * k) * flicker);
            }
            yield return null;
        }
    }

    void RemoveTells(List<SpriteRenderer> tells)
    {
        foreach (SpriteRenderer tell in tells)
        {
            if (tell == null) continue;
            transient.Remove(tell.gameObject);
            Destroy(tell.gameObject);
        }
        tells.Clear();
    }

    void Puff(Vector3 at, float size = 1.6f)
    {
        if (Fx.Play(Fx.ShiroMist, at, Color.white, size) == null) Fx.Play(Fx.SmokePuff, at, MistColour, size);
    }

    // --- Phase 1 moves ---

    // Crescent Moon Slash: her blades glow bright cyan, then a swing casts five crescents in a fan beneath her.
    // In her second phase the fan is aimed, its centre blade at the ronin.
    IEnumerator CrescentSlash()
    {
        if (Random.value < 0.5f) Speak(BossLine.CrescentSlash);
        ElementFx.Glow(Hands, Cyan, 0.8f, 2.6f, 0.6f, transform);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.1f);
        yield return Wait(secondPhase ? 0.5f : 0.6f);

        PlayAction(art.attack);
        if (Fx.Play(Fx.SlashArc, Hands, Cyan, 2.6f) == null) ElementFx.Glow(Hands, Cyan, 1f, 3f, 0.2f);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        Vector3 aim = secondPhase ? (ronin.Bounds.center - Hands).normalized : Vector3.down;
        var fan = new List<Arrow>();
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(-36f, 36f, i / 4f);
            fan.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
        yield return Wait(0.3f);
    }

    // Phantom Blade Rain: ghost swords hang over six lanes with a thin warning line under each, then fall. At least
    // one lane stays empty. In her second phase there are seven lanes, two empty, and the swords fall in a ripple.
    IEnumerator BladeRain()
    {
        if (Random.value < 0.5f) Speak(BossLine.BladeRain);
        int laneCount = secondPhase ? 7 : 6;
        int empty = secondPhase ? 2 : 1;
        List<float> lanes = Lanes(laneCount);
        // The empty lanes are picked at random; the ronin's own lane is never made the only way out on purpose.
        for (int i = 0; i < empty; i++) lanes.RemoveAt(Random.Range(0, lanes.Count));

        var tells = new List<SpriteRenderer>();
        var swords = new List<SpriteRenderer>();
        float hang = TopOfScreen - 1.4f;
        foreach (float x in lanes)
        {
            tells.Add(LaneWarning(x));
            swords.Add(SwordPicture(new Vector3(x, hang, 0), 0f));
            Puff(new Vector3(x, hang, 0), 1f);
        }
        Sfx.Play(Sfx.MistSwirl, 0.6f, 1f);
        RoamMeanwhile(RainWarning);
        yield return Brighten(tells, RainWarning);
        StopRoaming();

        for (int i = 0; i < lanes.Count; i++)
        {
            if (swords[i] != null)
            {
                transient.Remove(swords[i].gameObject);
                Destroy(swords[i].gameObject);
            }
            GhostSword(new Vector3(lanes[i], hang, 0), Vector3.down * 11f);
            if (secondPhase) yield return Wait(0.08f);
        }
        RemoveTells(tells);
        Sfx.Play(Sfx.GhostSwordFall, 0.9f, 1f);
        yield return Wait(0.4f);
    }

    // Moonlit Dash: a blue trail shows the way, she vanishes into mist and streaks across the field, and three
    // afterimages left along her path each loose a ghost sword straight down, one after the other.
    IEnumerator MoonlitDash()
    {
        if (Random.value < 0.4f) Speak(BossLine.MoonlitDash);
        float range = HalfWidth - 2f;
        float far = anchor.x < CameraShake.Center.x ? CameraShake.Center.x + range : CameraShake.Center.x - range;
        Vector3 from = anchor;
        Vector3 to = new Vector3(far, anchor.y, anchor.z);
        SpriteRenderer trail = Bar(from, to, 0.08f, Trail);
        for (float t = 0; t < 0.5f; t += Time.deltaTime * Tempo)
        {
            trail.color = new Color(Trail.r, Trail.g, Trail.b, 0.25f + 0.35f * Mathf.PingPong(t * 6f, 1f));
            yield return null;
        }
        transient.Remove(trail.gameObject);
        Destroy(trail.gameObject);

        Puff(Centre, 2.2f);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 1.2f);
        if (dashFrames != null) animator.Play(dashFrames, true, null, art.actionFps);
        sprite.flipX = to.x < from.x;
        var ghosts = new List<SpriteRenderer>();
        float dash = 0.4f;
        for (float t = 0; t < dash; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            float k = t / dash;
            anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, k));
            if (ghosts.Count < 3 && k >= (ghosts.Count + 1) / 4f) ghosts.Add(Linger());
            yield return null;
        }
        anchor = to;
        sprite.flipX = false;
        PlayIdle();
        Puff(Centre, 2.2f);

        // The afterimages let fly in the order she left them, so the pattern walks across the field.
        foreach (SpriteRenderer ghost in ghosts)
        {
            if (ghost == null) continue;
            Vector3 at = ghost.bounds.center + Vector3.down * 0.4f;
            GhostSword(at, Vector3.down * 10f);
            Sfx.Play(Sfx.GhostSwordFall, 0.6f, 1.2f);
            transient.Remove(ghost.gameObject);
            Fader.Fade(ghost, 0.3f);
            yield return Wait(0.25f);
        }
    }

    // A spectral copy of her that stays where she stood until it has struck.
    SpriteRenderer Linger()
    {
        var ghost = new GameObject("Afterimage").AddComponent<SpriteRenderer>();
        ghost.transform.SetPositionAndRotation(sprite.transform.position, sprite.transform.rotation);
        ghost.transform.localScale = sprite.transform.lossyScale;
        ghost.sprite = sprite.sprite;
        ghost.flipX = sprite.flipX;
        ghost.sortingOrder = sprite.sortingOrder - 1;
        ghost.color = new Color(Spirit.r, Spirit.g, Spirit.b, 0.55f);
        transient.Add(ghost.gameObject);
        return ghost;
    }

    // Spirit Blade Barrage: two rows of floating katanas wheel about her, then the inner row flies down and to
    // the left and the outer row down and to the right, crossing in the middle of the field with gaps between.
    IEnumerator BladeBarrage()
    {
        if (Random.value < 0.5f) Speak(BossLine.BladeBarrage);
        PlayAction(art.parry);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.9f);
        const int perRow = 4;
        var inner = new List<SpriteRenderer>();
        var outer = new List<SpriteRenderer>();
        for (int i = 0; i < perRow; i++)
        {
            inner.Add(SwordPicture(Centre, 0f));
            outer.Add(SwordPicture(Centre, 0f));
        }
        // They wheel for a second, slowing into their firing spread as the second ends.
        float spin = secondPhase ? 0.8f : 1f;
        for (float t = 0; t < spin; t += Time.deltaTime * Tempo)
        {
            float k = t / spin;
            float turn = t * 240f * (1f - k * 0.6f);
            for (int i = 0; i < perRow; i++)
            {
                PlaceOrbiting(inner[i], 1.3f, turn + i * 90f, -30f, k);
                PlaceOrbiting(outer[i], 2.1f, -turn + i * 90f + 45f, 30f, k);
            }
            yield return null;
        }

        // Rows fire a beat apart so the crossing paths read as two separate waves.
        LooseRow(inner, -30f);
        Sfx.Play(Sfx.GhostSwordFall, 0.8f, 0.9f);
        yield return Wait(0.4f);
        LooseRow(outer, 30f);
        Sfx.Play(Sfx.GhostSwordFall, 0.8f, 1.1f);
        yield return Wait(0.3f);
    }

    // Eases a wheeling sword from its orbit into a line above her, spaced for its row's dive angle.
    void PlaceOrbiting(SpriteRenderer sword, float radius, float angle, float dive, float settle)
    {
        if (sword == null) return;
        int index = sword.transform.GetSiblingIndex();
        Vector3 orbit = Centre + Quaternion.Euler(0, 0, angle) * Vector3.right * radius;
        float slot = (index % 4 - 1.5f) * 1.6f;
        Vector3 rest = Centre + new Vector3(slot, radius * 0.5f + 0.4f, 0);
        float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.5f, 1f, settle));
        sword.transform.position = Vector3.Lerp(orbit, rest, k) + Vector3.back * 0.3f;
        sword.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(angle + 90f, dive, k));
    }

    void LooseRow(List<SpriteRenderer> row, float dive)
    {
        var volley = new List<Arrow>();
        Vector3 velocity = Quaternion.Euler(0, 0, dive) * Vector3.down * 7f;
        foreach (SpriteRenderer sword in row)
        {
            if (sword == null) continue;
            Vector3 at = sword.transform.position;
            at.z = 0;
            transient.Remove(sword.gameObject);
            Destroy(sword.gameObject);
            volley.Add(GhostSword(at, velocity));
        }
        IgnoreEachOther(volley);
    }

    protected override IEnumerator Fall()
    {
        StopRoaming();
        if (bladeGlow != null) Destroy(bladeGlow.gameObject);
        Debris.BurstSprites(Centre, art.deathParticles, Cyan, 16, 0.9f, 8f);
        Sfx.Play(Sfx.BladeRing, 1f, 0.6f);
        return base.Fall();
    }
}
