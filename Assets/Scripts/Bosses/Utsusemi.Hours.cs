using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hour Strikes: the laser hours, the lock-on dashes and chops with their numerals, the hour's cleave, and the armour it wears while it speaks.
public partial class Utsusemi
{
    // Hour Strikes: one hour at a time, a thin red sight line from its eye tracks the ronin, then locks on where he
    // stands, and a moment later its eye lasers the spot, burning him if he hasn't stepped off. Then it counts the
    // hours again with its blade: for each, the sight line hunts him down and locks on, the hour's numeral (I, II,
    // III...) rises on the spot, and after a short window it dashes down out of its row onto it, chops it with a
    // ground-cracking blow and dashes back up. Then, back in its row, it strikes the next hour: that numeral rises under
    // the ronin and follows him while its core glows and the blade draws back over three ticks, and a giant crescent
    // cleave flies at him. It hunts him, breaks his shots and cuts through cover, so only a parry answers it: sent
    // back, it jams the gears and staggers the automaton. In the second phase there are five hours, faster.
    IEnumerator HourStrikes()
    {
        int count = secondPhase ? 4 : 3;
        int lasers = secondPhase ? 3 : 2;
        // The dashes come fast: a short tick before each, and a quick blow.
        float beat = secondPhase ? 0.18f : 0.24f;
        Vector3 row = anchor;

        // The announcement, in super armour: it calls the move and stands its ground in a gold shell while it lasers
        // the hours in one at a time. Nothing staggers or freezes it until the shell drops.
        Speak(BossLine.HourStrikes);
        armored = true;
        SpriteRenderer[] shell = ArmorShell();
        Sfx.Play(Sfx.GearGrind, 0.6f, 1.4f);
        yield return HoldArmor(shell, 0.3f);
        // The shell drops once the move is called: it lasers the hours open to the blade.
        Armour(shell, false);
        for (int i = 0; i < lasers; i++) yield return LaserHour(shell);
        yield return HoldArmor(shell, 0.3f);
        Tick(0.4f);
        yield return HoldArmor(shell, 0.25f);

        // The dashes: each one locks onto wherever the ronin stands now. It keeps its shell while it takes aim, so the
        // hours give him an opening only while it is down on his ground.
        for (int hour = 1; hour <= count; hour++)
        {
            Beat();
            Tick(0.9f);
            yield return HoldArmor(shell, beat);
            yield return LockOnDash(hour, row, shell, hour == count ? RewindSeconds : 0f);
            PlayIdle();
        }
        armored = false;
        foreach (SpriteRenderer part in shell) RemoveTransient(part);

        Beat();
        yield return StrikeTheHour(count + 1);
        PlayIdle();
    }

    // One hour lasered where the ronin stands: a thin sight line from its eye follows him, then locks on and
    // brightens. He has a moment to step off before its eye's laser strikes the spot; it burns him if he's still there.
    IEnumerator LaserHour(SpriteRenderer[] shell)
    {
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        SpriteRenderer sight = Bar(Eye, Spot(x), 0.05f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.4f));
        yield return HoldArmor(shell, secondPhase ? 0.45f : 0.6f, () =>
        {
            x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
            Stretch(sight, Eye, Spot(x), 0.05f);
        });

        // Locked: the sight line brightens, and the dodge window opens.
        Tick(0.7f);
        Vector3 spot = Spot(x);
        sight.color = new Color(1f, 0.55f, 0.5f, 0.85f);
        yield return HoldArmor(shell, secondPhase ? 0.3f : 0.35f, () => Stretch(sight, Eye, spot, 0.07f));
        RemoveTransient(sight);

        Beam beam = MakeBeam(Eye, 0.8f);
        // The strike lands hard: a broad blaze where it hits, and a full-size burn left on the spot.
        if (beam.hit != null) beam.hit.transform.localScale *= 1.8f;
        Sfx.Play(Sfx.LanternCharge, 0.4f, 2f);
        CameraShake.Add(0.1f);
        GroundFire.Scorch(spot, 2f);
        Fx.Play(Fx.Impact, spot, BeamColour, 1.4f);
        yield return HoldArmor(shell, 0.3f, () =>
        {
            if (beam.body == null) return;
            DrawBeam(beam, Eye, spot, false, false);
            HitIfCrossed(Eye, spot);
        });
        EndBeam(beam);
        yield return HoldArmor(shell, 0.1f);
    }

    // One hour struck where the ronin stands: a red sight line from its eye follows him, then locks on, and the
    // hour's numeral rises there. After a short window to step off, it dashes down onto the spot and chops it.
    // After the last hour it stays down on his ground this long, winding itself back up: the opening.
    const float RewindSeconds = 1.2f;

    IEnumerator LockOnDash(int hour, Vector3 row, SpriteRenderer[] shell, float linger)
    {
        float lo = LeftEdge + 1.2f, hi = RightEdge - 1.2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        SpriteRenderer sight = Bar(Eye, Spot(x), 0.05f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.4f));
        // Armoured while it aims from its row.
        Armour(shell, true);
        yield return HoldArmor(shell, secondPhase ? 0.4f : 0.5f, () =>
        {
            x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
            Stretch(sight, Eye, Spot(x), 0.05f);
        });

        // Locked: the hour's numeral rises where he stood, and the dodge window opens.
        var mark = new HourMark(this, hour, x);
        mark.Show();
        Tick(0.7f);
        Vector3 spot = Spot(x);
        sight.color = new Color(1f, 0.55f, 0.5f, 0.85f);
        yield return HoldArmor(shell, secondPhase ? 0.3f : 0.4f, () => Stretch(sight, Eye, spot, 0.07f));
        RemoveTransient(sight);

        // The shell drops as it leaves its row: down on his ground it can be struck.
        Armour(shell, false);
        yield return Dash(new Vector3(x, Landing(), row.z), HourDashTime);
        yield return Chop(x, mark, 36f, 0.08f);
        if (linger > 0f)
        {
            // The hours spent, it winds itself back up where it landed: gears grinding, slow ticks, open to the blade.
            Sfx.Play(Sfx.GearGrind, 0.6f, 0.8f);
            if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 1.6f) == null) Fx.Play(Fx.Impact, Centre, Gold, 1.4f);
            PlayIdle();
            for (float t = 0; t < linger; t += 0.3f)
            {
                Beat();
                Sfx.Play(Sfx.ClockTick, 0.5f, 0.6f);
                yield return Wait(0.15f);
            }
        }
        yield return Dash(row, HourDashTime);
    }

    Vector3 Spot(float x) => new Vector3(x, GroundY + 0.15f, 0);

    void Armour(SpriteRenderer[] shell, bool on)
    {
        armored = on;
        foreach (SpriteRenderer part in shell)
        {
            if (part != null) part.enabled = on;
        }
    }

    bool armored;
    protected override bool SuperArmor => armored;
    // Its armour shrugs off 95% of every blow.
    protected override float ArmorDamageTaken => 0.05f;

    // The armour's look: a gold outline, made of solid gold silhouettes of its sprite nudged one sprite pixel out in
    // eight directions and drawn just behind it, so only a thin gold edge shows around the figure.
    static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1),
    };

    SpriteRenderer[] ArmorShell()
    {
        Material flat = Fx.Library != null ? Fx.Library.flashMaterial : null;
        if (flat == null) return new SpriteRenderer[0];
        float pixel = 1f / (sprite.sprite != null ? sprite.sprite.pixelsPerUnit : 48f);
        var shell = new SpriteRenderer[OutlineOffsets.Length];
        for (int i = 0; i < shell.Length; i++)
        {
            shell[i] = new GameObject("ArmorOutline").AddComponent<SpriteRenderer>();
            shell[i].transform.SetParent(sprite.transform, false);
            shell[i].transform.localPosition = (Vector3)(OutlineOffsets[i] * pixel);
            shell[i].sharedMaterial = flat;
            shell[i].sortingOrder = sprite.sortingOrder - 1;
            transient.Add(shell[i].gameObject);
        }
        ElementFx.Glow(Centre, Gold, 1f, 3.5f, 0.4f, transform);
        return shell;
    }

    // Made on first use: Unity won't build one in a field initializer.
    MaterialPropertyBlock outlineColour;
    static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

    // Real time, not boss time: the armour ignores a freeze.
    IEnumerator HoldArmor(SpriteRenderer[] shell, float seconds, System.Action eachFrame = null)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            Beat();
            eachFrame?.Invoke();
            // A shimmer between deep gold and pale gold.
            float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            if (outlineColour == null) outlineColour = new MaterialPropertyBlock();
            outlineColour.SetColor(FlashColor, Color.Lerp(new Color(1f, 0.7f, 0.15f), new Color(1f, 0.93f, 0.6f), shimmer));
            foreach (SpriteRenderer edge in shell)
            {
                if (edge == null) continue;
                edge.sprite = sprite.sprite;
                edge.flipX = sprite.flipX;
                edge.SetPropertyBlock(outlineColour);
            }
            yield return null;
        }
    }

    // The feet in its frames stand on this row of the 116-px canvas.
    const float FeetPx = 98f;

    // Where its anchor sits when its feet stand on the ronin's ground.
    float Landing() => anchor.y + (GroundY + 0.15f) - OnSprite(0, FeetPx).y;

    // A point given in the sprite frame's own pixels (from its top-left), so effects meet the art.
    Vector3 OnSprite(float px, float py)
    {
        Bounds b = sprite.bounds;
        Rect frame = sprite.sprite != null ? sprite.sprite.rect : new Rect(0, 0, 116, 116);
        float u = px / frame.width, v = 1f - py / frame.height;
        if (sprite.flipX) u = 1f - u;
        return new Vector3(b.min.x + u * b.size.x, b.min.y + v * b.size.y, 0);
    }

    // A straight clockwork dash, eased hard into a dead stop, leaving digital afterimages along the way.
    // How long each Hour Strikes dash takes, down to an hour or back up to the row.
    const float HourDashTime = 0.11f;

    IEnumerator Dash(Vector3 to, float seconds = 0.2f)
    {
        Vector3 from = anchor;
        PlayFrames(dashFrames, 1, -1, true, 30f);
        Sfx.Play(Sfx.ParrySwing, 0.6f, 1.4f);
        int ghosts = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            if (k >= ghosts / 3f && ghosts < 3)
            {
                DigitalGhost(0.38f - ghosts * 0.08f);
                ghosts++;
            }
            anchor = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k) * (1f - k));
            yield return null;
        }
        anchor = to;
        Tick(0.6f);
    }

    // A digital afterimage where it stands now: a flat silhouette split into steel blue and gold a few pixels apart,
    // nudged sideways a pixel or two like a bad signal, that blinks and fades out in hard steps. Without the flash
    // material a plain blue-tinted copy stands in.
    void DigitalGhost(float alpha)
    {
        Material flat = Fx.Library != null ? Fx.Library.flashMaterial : null;
        if (flat == null || sprite.sprite == null)
        {
            Afterimage(new Color(GhostBlue.r, GhostBlue.g, GhostBlue.b, alpha));
            return;
        }
        float pixel = sprite.transform.lossyScale.x / sprite.sprite.pixelsPerUnit;
        float jitter = Random.Range(-2, 3) * pixel;
        // Its second form leaves burning ghosts: molten and ivory instead of steel blue and gold.
        SpawnGhost(flat, SecondPhase ? Molten : GhostBlue, alpha, jitter - 3f * pixel, pixel);
        SpawnGhost(flat, SecondPhase ? Lace : GhostGold, alpha * 0.7f, jitter + 3f * pixel, pixel);
    }

    void SpawnGhost(Material flat, Color color, float alpha, float offsetX, float pixel)
    {
        var ghost = new GameObject("DigitalGhost").AddComponent<SpriteRenderer>();
        ghost.transform.SetPositionAndRotation(sprite.transform.position + Vector3.right * offsetX, sprite.transform.rotation);
        ghost.transform.localScale = sprite.transform.lossyScale;
        ghost.sprite = sprite.sprite;
        ghost.flipX = sprite.flipX;
        ghost.sharedMaterial = flat;
        ghost.sortingOrder = sprite.sortingOrder - 1;
        ghost.gameObject.AddComponent<GlitchFade>().Begin(color, alpha, 0.35f, pixel);
    }

    // Fades a digital afterimage in three hard steps, dropping out for a frame now and then and tearing a couple of
    // pixels sideways, like a bad signal.
    class GlitchFade : MonoBehaviour
    {
        SpriteRenderer target;
        MaterialPropertyBlock block;
        Color color;
        float alpha, seconds, age, pixel;
        Vector3 home;

        public void Begin(Color tint, float startAlpha, float lifetime, float pixelSize)
        {
            target = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
            color = tint;
            alpha = startAlpha;
            seconds = lifetime;
            pixel = pixelSize;
            home = transform.position;
            Apply();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        void Apply()
        {
            float stepped = Mathf.Ceil((1f - age / seconds) * 3f) / 3f;
            bool dropped = Random.value < 0.15f;
            transform.position = home + Vector3.right * (Random.value < 0.25f ? Random.Range(-2, 3) * pixel : 0f);
            block.SetColor(FlashColor, new Color(color.r, color.g, color.b, dropped ? 0f : alpha * stepped));
            target.SetPropertyBlock(block);
        }
    }

    const float ChopReach = 0.95f;
    // The chop's blade meets the ground on this frame.
    const int ChopImpactFrame = 5;

    // fps is how fast the chop clip plays, and so how soon the blow lands; recover is the pause after it.
    // A `parryable` chop (the sword reclaim's) clangs off a parry: no damage, but a sliver on its stance meter.
    IEnumerator Chop(float x, HourMark mark, float fps = 20f, float recover = 0.18f, bool parryable = false)
    {
        PlayFrames(chopFrames, 1, -1, true, fps);
        yield return Wait(ChopWindUp(fps));

        Vector3 point = new Vector3(x, GroundY + 0.15f, 0);
        if (Mathf.Abs(ronin.Bounds.center.x - x) < ChopReach)
        {
            if (parryable && ronin.Parrying)
            {
                Vector3 clash = new Vector3(x, ronin.Bounds.center.y + 0.4f, 0);
                ronin.ParryBody(clash);
                Fx.Play(Fx.Impact, clash, Gold, 1.6f);
                ElementFx.Glow(clash, Gold, 0.8f, 2.5f, 0.25f);
                HitStop?.Invoke(0.08f);
                AddStance(ParriedChopStance);
            }
            else Strike(new Vector3(x, ronin.Bounds.center.y, 0));
        }
        if (Fx.Play(Fx.UtsuChop, point, Color.white, 2.4f) == null) Fx.Play(Fx.Impact, point, CoreRed, 1.2f);
        ElementFx.Glow(point, Color.white, 0.6f, 1.8f, 0.15f);
        Sfx.Play(Sfx.BladeChop, 1f, Random.Range(0.92f, 1.08f));
        CameraShake.Add(0.2f);
        mark?.Struck();
        yield return Wait(recover);
    }

    // From the start of the chop clip to the blade striking the ground.
    float ChopWindUp(float fps = 20f) => chopFrames != null ? (ChopImpactFrame - 1) / fps : 0.2f;

    // How much of its stance meter a parried reclaim chop fills: a sliver, like a parried shot.
    const float ParriedChopStance = 0.03f;

    // The parry tell for the reclaim chop: a gold ring on the ground round where the blade will strike, closing in
    // over `seconds` with a quickening tick, and a gold "!" over the ronin's head (the army's lunge tell); both snap
    // white `flashBefore` seconds before it lands.
    IEnumerator ChopTell(float x, float seconds, float flashBefore)
    {
        Vector3 spot = new Vector3(x, GroundY + 0.15f, -0.3f);
        float pixel = ronin.ArtPixelSize;
        var ring = new GameObject("ChopTell").AddComponent<SpriteRenderer>();
        ring.sortingOrder = 58;
        transient.Add(ring.gameObject);
        var glow = Bar(spot, spot + Vector3.right * 0.01f, 1f, Color.clear, 57);
        glow.sprite = ProceduralSprites.SoftGlow;
        // The "!" stands out even against its own pale robe as it lands on him: a dark edge and a gold glow behind it.
        var alert = new GameObject("ChopAlert").AddComponent<SpriteRenderer>();
        alert.sprite = ProceduralSprites.Exclaim;
        alert.sortingOrder = 63;
        transient.Add(alert.gameObject);
        var edge = new GameObject("Edge").AddComponent<SpriteRenderer>();
        edge.sprite = ProceduralSprites.Exclaim;
        edge.color = new Color(0.08f, 0.06f, 0.05f);
        edge.sortingOrder = 62;
        edge.transform.SetParent(alert.transform, false);
        edge.transform.localScale = new Vector3(1.45f, 1.25f, 1f);
        var halo = new GameObject("Glow").AddComponent<SpriteRenderer>();
        halo.sprite = ProceduralSprites.Glow;
        halo.color = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
        halo.sortingOrder = 61;
        halo.transform.SetParent(alert.transform, false);
        halo.transform.localScale = Vector3.one * (1.6f * ProceduralSprites.Exclaim.bounds.size.y / ProceduralSprites.Glow.bounds.size.x);
        float alertSize = 0.95f / ProceduralSprites.Exclaim.bounds.size.y;
        Vector3 OverHead() => new Vector3(ronin.Bounds.center.x, ronin.TopY + 0.75f, -0.4f);
        float closeIn = Mathf.Max(0.05f, seconds - flashBefore), ticked = 0;
        for (float t = 0; t < closeIn && ring != null; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            float k = t / closeIn;
            float radius = Mathf.Lerp(ChopReach * 3f, ChopReach, Mathf.SmoothStep(0, 1, k));
            ring.sprite = ProceduralSprites.Ring(Mathf.RoundToInt(radius / pixel));
            ring.transform.SetPositionAndRotation(spot, Quaternion.identity);
            ring.transform.localScale = Vector3.one * pixel;
            ring.color = new Color(Gold.r, Gold.g, Gold.b, 0.4f + 0.6f * k);
            glow.transform.SetPositionAndRotation(spot, Quaternion.identity);
            glow.transform.localScale = new Vector3(radius * 2f, radius * 0.9f, 1f) / ProceduralSprites.SoftGlow.bounds.size.x;
            glow.color = new Color(Gold.r, Gold.g, Gold.b, 0.25f * k);
            alert.transform.position = OverHead() + Vector3.up * 0.06f * Mathf.Sin(t * 30f);
            alert.transform.localScale = Vector3.one * alertSize * Mathf.Min(1f, k * 4f);
            alert.color = Gold;
            if (t - ticked > Mathf.Lerp(0.12f, 0.05f, k))
            {
                ticked = t;
                Sfx.Play(Sfx.BladeRing, 0.25f, 1.3f + k);
            }
            yield return null;
        }
        if (ring == null) yield break;
        // The lock: tight and white. This is the beat to parry on.
        ring.sprite = ProceduralSprites.Ring(Mathf.RoundToInt(ChopReach * 1.3f / pixel));
        ring.color = Color.white;
        alert.transform.position = OverHead();
        alert.transform.localScale = Vector3.one * alertSize * 1.35f;
        alert.color = Color.white;
        halo.color = new Color(1f, 1f, 1f, 0.85f);
        ElementFx.Glow(OverHead(), Color.white, 0.5f, 1.5f, 0.2f);
        glow.color = new Color(1f, 1f, 1f, 0.45f);
        Fx.Play(Fx.Impact, spot + Vector3.up * 0.3f, Color.white, 0.8f);
        Sfx.Play(Sfx.BladeRing, 0.6f, 2f);
        yield return Wait(flashBefore + 0.1f);
        RemoveTransient(ring);
        RemoveTransient(glow);
        RemoveTransient(alert);
    }

    // The hour slash draws the blade back by this frame, holds it through the ticks, and lets the cleave go on the
    // release frame, as its swing passes the front.
    const int SlashDrawnFrame = 5;
    const int SlashReleaseFrame = 7;

    // The hour: its numeral under the ronin follows him for three ticks while the core glows and the blade is drawn
    // back, then flares red as the swing flings a giant crescent cleave at him.
    IEnumerator StrikeTheHour(int hour)
    {
        var mark = new HourMark(this, hour, ronin.Bounds.center.x);
        mark.Show();
        Sfx.Play(Sfx.LanternCharge, 0.8f, 1.3f);
        PlayFrames(slashFrames, 1, SlashDrawnFrame, true, 14f);

        float tickTime = secondPhase ? 0.32f : 0.42f;
        for (int i = 0; i < 3; i++)
        {
            Tick(1f);
            ElementFx.Glow(Core, i == 2 ? Color.white : CoreRed, 0.7f, 1.6f + i * 0.6f, tickTime * 0.8f, transform);
            for (float t = 0; t < tickTime; t += Time.deltaTime * Tempo)
            {
                mark.MoveTo(ronin.Bounds.center.x);
                yield return null;
            }
        }
        mark.Struck();

        const float swingFps = 18f;
        PlayFrames(slashFrames, SlashDrawnFrame + 1, -1, false, swingFps);
        yield return Wait(slashFrames != null ? (SlashReleaseFrame - SlashDrawnFrame - 1) / swingFps : 0.05f);

        Vector3 from = OnSprite(58f, FeetPx) + Vector3.down * 0.6f;
        Arrow cleave = Own(Shoot(from, (ronin.Bounds.center - from).normalized * HourSpeed));
        Sprite[] frames = Fx.Frames(Fx.UtsuHourSlash);
        if (frames != null) cleave.Restyle(frames, 14f, Color.white, new Vector2(5.6f, 3.7f), 1.1f, new Color(1f, 0.2f, 0.18f, 0.6f), 4.5f);
        else cleave.Restyle(ProceduralSprites.White, new Color(0.9f, 0.93f, 1f), new Vector2(5.4f, 0.5f), 1.1f, new Color(1f, 0.2f, 0.18f, 0.6f), 4.5f);
        cleave.MakeUnbreakable();
        cleave.MakeReflectable(transform, setPiece: true);
        StartCoroutine(Pursue(cleave, HourSpeed));
        Sfx.Play(Sfx.HourSlash, 1f);
        Sfx.Play(Sfx.MidnightChime, 0.6f, 1.4f);
        CameraShake.Add(0.35f);
        ElementFx.Glow(from, Color.white, 0.8f, 3f, 0.25f);
        yield return Wait(0.2f);
    }

    // Fast, but it turns as fast as the ronin runs: there's no outrunning it, only parrying it.
    const float HourSpeed = 11f;

    // Its Hour Strikes frames; without them it uses its thrust frames.
    Sprite[] dashFrames, chopFrames, slashFrames;

    public void SetSwordFrames(Sprite[] dash, Sprite[] chop, Sprite[] slash)
    {
        dashFrames = dash != null && dash.Length > 1 ? dash : null;
        chopFrames = chop != null && chop.Length > ChopImpactFrame ? chop : null;
        slashFrames = slash != null && slash.Length > SlashReleaseFrame ? slash : null;
    }

    // Plays frames first..last of a clip (last -1 for the end), then holds its final frame or returns to the idle.
    void PlayFrames(Sprite[] clip, int first, int last, bool hold, float fps)
    {
        if (clip == null)
        {
            PlayAction(art.attack);
            return;
        }
        if (last < 0 || last >= clip.Length) last = clip.Length - 1;
        var frames = new Sprite[last - first + 1];
        System.Array.Copy(clip, first, frames, 0, frames.Length);
        if (hold) animator.Play(frames, false, () => animator.Play(new[] { frames[frames.Length - 1] }, true), fps);
        else animator.Play(frames, false, PlayIdle, fps);
    }

    // The warning where a dash or a thrown sword will land: a brass Roman numeral (I to V) standing on a brass ring on
    // the ground, glowing red-hot at the edges, which rises in with a flash and flares red when the blow lands. Without
    // the numeral art it's drawn from bars over a soft pool of light: I and V strokes are all the first six hours need.
    class HourMark
    {
        const float Height = 0.8f;
        // The mark is drawn this many world units wide, about the reach of the blow it marks.
        const float ArtWidth = 2.1f;
        public float x { get; private set; }
        readonly Utsusemi owner;
        readonly int hour;
        readonly List<SpriteRenderer> strokes = new List<SpriteRenderer>();
        SpriteRenderer pool;

        public HourMark(Utsusemi owner, int hour, float x)
        {
            this.owner = owner;
            this.hour = hour;
            this.x = x;
        }

        public void Show() => Show(Color.white);

        // Slides the numeral along the ground to follow the ronin.
        public void MoveTo(float newX)
        {
            Vector3 shift = Vector3.right * (newX - x);
            x = newX;
            foreach (SpriteRenderer part in strokes)
            {
                if (part != null) part.transform.position += shift;
            }
            if (pool != null) pool.transform.position += shift;
        }

        void Show(Color tint)
        {
            Hide(0f);
            Vector3 foot = new Vector3(x, owner.GroundY + 0.15f, 0);
            Sprite[] numerals = Fx.Frames(Fx.UtsuNumerals);
            if (numerals != null)
            {
                // A gold clock rim lying flat on the ground line with the hour inside, like the clock marks.
                var art = new GameObject("HourNumeral").AddComponent<SpriteRenderer>();
                art.sprite = numerals[Mathf.Clamp(hour - 1, 0, numerals.Length - 1)];
                art.transform.position = new Vector3(x, foot.y - 0.1f, 0);
                art.transform.localScale = Vector3.one * (ArtWidth / art.sprite.bounds.size.x);
                art.color = tint;
                art.sortingOrder = 8;
                owner.transient.Add(art.gameObject);
                strokes.Add(art);
                ElementFx.Glow(foot, tint == Color.white ? Gold : tint, 0.5f, 1.6f, 0.3f);
                return;
            }

            Color colour = tint == Color.white ? Gold : tint;
            pool = owner.Bar(foot, foot, 1f, new Color(colour.r, colour.g, colour.b, 0.45f), 6);
            pool.sprite = ProceduralSprites.Glow;
            pool.transform.rotation = Quaternion.identity;
            pool.transform.localScale = new Vector3(ChopReach * 2.2f, 0.45f, 1f);

            string glyphs = Roman(hour);
            float stroke = Height * 0.16f, vWidth = Height * 0.6f, gap = Height * 0.18f;
            float width = 0;
            foreach (char c in glyphs) width += (c == 'V' ? vWidth : stroke) + gap;
            width -= gap;
            float left = x - width / 2f;
            float bottom = foot.y + 0.25f, top = bottom + Height;
            foreach (char c in glyphs)
            {
                if (c == 'V')
                {
                    Stroke(new Vector3(left, top, 0), new Vector3(left + vWidth / 2f, bottom, 0), stroke, colour);
                    Stroke(new Vector3(left + vWidth, top, 0), new Vector3(left + vWidth / 2f, bottom, 0), stroke, colour);
                    left += vWidth + gap;
                }
                else
                {
                    Stroke(new Vector3(left + stroke / 2f, top, 0), new Vector3(left + stroke / 2f, bottom, 0), stroke, colour);
                    left += stroke + gap;
                }
            }
            ElementFx.Glow(new Vector3(x, bottom + Height / 2f, 0), colour, 0.4f, 1.4f, 0.3f);
        }

        static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "VI" };

        // A gold stroke on a dark outline, so it reads against the grass.
        void Stroke(Vector3 from, Vector3 to, float width, Color colour)
        {
            Vector3 along = (to - from).normalized * width * 0.5f;
            strokes.Add(owner.Bar(from - along, to + along, width * 2f, new Color(0.08f, 0.06f, 0.04f, 0.9f), 7));
            strokes.Add(owner.Bar(from, to, width, colour, 8));
        }

        // The hour it was: it flares red under the blow, then fades.
        public void Struck()
        {
            Show(new Color(1f, 0.45f, 0.35f));
            Hide(0.5f);
        }

        public void Hide(float seconds)
        {
            foreach (SpriteRenderer part in strokes) Fade(part, seconds);
            Fade(pool, seconds);
            strokes.Clear();
            pool = null;
        }

        void Fade(SpriteRenderer part, float seconds)
        {
            if (part == null) return;
            owner.transient.Remove(part.gameObject);
            if (seconds > 0) Fader.Fade(part, seconds);
            else Destroy(part.gameObject);
        }
    }
}
