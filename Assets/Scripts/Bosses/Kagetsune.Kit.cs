using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kagetsune's kit: his heavy stride, his three bullets, and the glows, strips, lanes and lock-ons his moves draw with.
public partial class Kagetsune
{
    // --- Movement ---

    // He doesn't blink or glide: he takes a few heavy steps, and the ground shakes under each.
    IEnumerator Stride(float x)
    {
        float distance = x - anchor.x;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(distance) / 1.4f), 1, 3);
        float step = distance / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 from = anchor;
            Vector3 to = from + Vector3.right * step;
            for (float t = 0; t < 0.32f; t += Time.deltaTime * Tempo)
            {
                anchor = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 0.32f));
                yield return null;
            }
            anchor = to;
            CameraShake.Add(0.12f);
            Sfx.Play(Sfx.MarchStep, 0.9f, 0.45f);
            Fx.Play(Fx.SmokePuff, new Vector3(Centre.x, sprite.bounds.min.y + 0.3f, 0), new Color(0.3f, 0.3f, 0.32f, 0.6f), 1.6f);
            // The Wraith leaves a ghost of soul-fire where each step began.
            if (secondPhase) Afterimage(new Color(Teal.r, Teal.g, Teal.b, 0.35f));
            yield return Wait(0.1f);
        }
    }

    // --- Shots and telegraphs ---

    // A soul-skull: a breakable teal orb. Parried, it flies home at him as a freed soul.
    Arrow Skull(Vector3 from, Vector3 velocity)
    {
        Arrow skull = BulletHell.Enlist(Shoot(from, velocity * ShotSpeed));
        Sprite[] frames = Fx.Frames(Fx.KageSkull);
        if (frames != null) skull.Restyle(frames, 10f, Color.white, new Vector2(0.95f, 0.95f), 0.32f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 1.5f);
        else skull.Restyle(ProceduralSprites.Disc, new Color(0.75f, 1f, 0.92f), new Vector2(0.6f, 0.6f), 0.32f, new Color(Teal.r, Teal.g, Teal.b, 0.55f), 1.5f);
        skull.PassesShots = true;
        skull.Parried = FreeSoul;
        return skull;
    }

    // A parried skull is a soul stolen back: it flies home at him wrapped in its own teal fire, ringing as it goes.
    static void FreeSoul(Projectile riposte)
    {
        if (riposte == null) return;
        riposte.Tint(new Color(0.8f, 1f, 0.95f), Teal);
        if (Sfx.Has(Sfx.BellSilenced)) Sfx.Play(Sfx.BellSilenced, 0.45f, 1.3f);
        else Sfx.Play(Sfx.MidnightChime, 0.3f, 1.9f);
    }

    // His other two bullets: a bronze bell a parry rings into Resonance, and a violet seal-dart no parry turns.
    Arrow BellShot(Vector3 from, Vector3 velocity) => BulletHell.MakeBell(BulletHell.Enlist(Shoot(from, velocity * ShotSpeed), capped: false));
    Arrow SealDart(Vector3 from, Vector3 velocity) => BulletHell.MakeSealDart(BulletHell.Enlist(Shoot(from, velocity * ShotSpeed), capped: false), BulletHell.Violet);

    // The ronin's row, and the stretch of it a curtain spans.
    float Row => ronin.Bounds.center.y;
    float FieldLeft => LeftEdge + 0.6f;
    float FieldRight => RightEdge - 0.6f;
    // Past the middle of his first bar he hardens: a second slam lane, a faster Spirit Hyper Beam, a gapless curtain row,
    // and a ring of skulls closing with the Last Toll.
    bool Hardened => secondPhase || HealthFraction < 0.5f;
    int SpiralArms => lastBell ? 4 : 3;
    // Under 30% of his last bar the Last Bell rings: a fourth spiral arm, denser blooms, the ultimate every fourth
    // move, and the dark closes in.
    bool lastBell => secondPhase && HealthFraction < LastBellAt;
    const float LastBellAt = 0.3f;
    bool lastBellRung;
    int Ultimate => lastBell ? 4 : UltimateEvery;

    // A soft glow sprite, tracked with the attack unless told otherwise.
    SpriteRenderer Glowing(Vector3 at, Color colour, float size, int order = 55, bool track = true)
    {
        var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
        glow.sprite = ProceduralSprites.Glow;
        glow.color = colour;
        glow.sortingOrder = order;
        glow.transform.position = at + Vector3.back * 0.1f;
        glow.transform.localScale = Vector3.one * size;
        if (track) transient.Add(glow.gameObject);
        return glow;
    }

    void Drop(Component thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    // A chain or soul-fire column: its art tiled along the length when it exists, or a plain bar in the fallback colour.
    SpriteRenderer Tiled(string id, Vector3 from, Vector3 to, float width, Color fallback, int order)
    {
        Sprite[] frames = Fx.Frames(id);
        if (frames == null) return Bar(from, to, width, fallback, order);
        var strip = new GameObject(id).AddComponent<SpriteRenderer>();
        strip.sprite = frames[0];
        strip.drawMode = SpriteDrawMode.Tiled;
        strip.sortingOrder = order;
        transient.Add(strip.gameObject);
        Lay(strip, from, to, width);
        return strip;
    }

    // Stretch for both kinds: a tiled strip is scaled to the width and tiled to the length, so its links don't smear.
    public static void Lay(SpriteRenderer strip, Vector3 from, Vector3 to, float width)
    {
        if (strip == null) return;
        if (strip.drawMode != SpriteDrawMode.Tiled)
        {
            Stretch(strip, from, to, width);
            return;
        }
        Vector3 delta = to - from;
        float art = strip.sprite.bounds.size.y;
        float scale = width / art;
        strip.transform.position = (from + to) / 2f + Vector3.back * 0.2f;
        strip.transform.rotation = delta.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.right, delta) : Quaternion.identity;
        strip.transform.localScale = Vector3.one * scale;
        strip.size = new Vector2(delta.magnitude / scale, art);
    }

    // The colour of a chain at the given opacity: its own art's colours, or bronze for the bar.
    static Color ChainColour(SpriteRenderer chain, float alpha) =>
        chain.drawMode == SpriteDrawMode.Tiled ? new Color(1f, 1f, 1f, alpha) : new Color(Bronze.r, Bronze.g, Bronze.b, alpha);

    // The burst of stone and soul-fire under a slam: a ring racing out along the ground as it cracks, `size` wide.
    static void Shockwave(Vector3 at, float size)
    {
        Sprite[] frames = Fx.Frames(Fx.KageShockwave);
        if (frames == null) return;
        Fx.Play(Fx.KageShockwave, at + Vector3.back * 0.2f, Color.white, size / frames[0].bounds.size.x, null, 57);
    }

    // How much of the column art's canvas its beam fills at its widest.
    const float ColumnBeamShare = 0.4f;

    // A column of soul-fire slamming down from `top` onto `ground`, its beam `width` across (as wide as what it burns):
    // its ring sits on the ground and the beam stretches up to the top. Null without the art (the caller draws a plain
    // bar instead).
    FxPlayer SoulColumn(Vector3 top, Vector3 ground, float width)
    {
        Sprite[] frames = Fx.Frames(Fx.KageSoulColumn);
        if (frames == null) return null;
        FxPlayer column = Fx.Play(Fx.KageSoulColumn, ground + Vector3.back * 0.2f, Color.white, 1f, null, 56);
        if (column == null) return null;
        Vector2 art = frames[0].bounds.size;
        column.transform.localScale = new Vector3(width / ColumnBeamShare / art.x, Mathf.Max(0.1f, top.y - ground.y) / art.y, 1f);
        return column;
    }

    // A bell's toll seen as well as heard: a bronze ring of sound spreading from where it rang, `size` across.
    static void TollRing(Vector3 at, float size, float alpha = 1f)
    {
        Sprite[] frames = Fx.Frames(Fx.KageTollRing);
        if (frames == null)
        {
            ElementFx.Glow(at, new Color(Bronze.r, Bronze.g, Bronze.b, 0.5f * alpha), 0.5f, size, 0.35f);
            return;
        }
        Fx.Play(Fx.KageTollRing, at + Vector3.back * 0.3f, new Color(1f, 1f, 1f, alpha), size / frames[0].bounds.size.x, null, 58);
    }

    // A heavy thrown thing leaves a streak of fading copies of itself as it flies.
    IEnumerator Streak(Arrow shot, Color colour)
    {
        SpriteRenderer look = shot != null ? shot.GetComponentInChildren<SpriteRenderer>() : null;
        while (shot != null && look != null)
        {
            Afterimage(look, colour);
            yield return new WaitForSeconds(0.04f);
        }
    }

    // A wide faint band down a lane that narrows and brightens over the tell, so the blow lands as it closes.
    IEnumerator CloseLanes(List<SpriteRenderer> bands, List<float> xs, float fromWidth, float toWidth, Color colour, float seconds, int beats = 3, float peak = 0.45f)
    {
        int rung = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            if (k >= rung / (float)beats && rung < beats)
            {
                rung++;
                Toll(0.35f, 0.8f + 0.12f * rung);
            }
            float width = Mathf.Lerp(fromWidth, toWidth, Mathf.SmoothStep(0, 1, k));
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i] == null) continue;
                Stretch(bands[i], new Vector3(xs[i], TopOfScreen, 0), new Vector3(xs[i], GroundY, 0), width);
                bands[i].color = new Color(colour.r, colour.g, colour.b, 0.12f + peak * k);
            }
            yield return null;
        }
    }

    // His bell: the generated toll, or a low chime until it's made.
    static void Toll(float volume, float pitch)
    {
        if (Sfx.Has(Sfx.BellToll)) Sfx.Play(Sfx.BellToll, volume, pitch);
        else Sfx.Play(Sfx.MidnightChime, volume, pitch * 0.55f);
    }

    static void Slam(float volume)
    {
        if (Sfx.Has(Sfx.HammerSlam)) Sfx.Play(Sfx.HammerSlam, volume);
        else Sfx.Play(Sfx.BossStagger, volume, 0.5f);
    }

    float ClampX(float x) => Mathf.Clamp(x, LeftEdge + 1f, RightEdge - 1f);

    // Rings round the ronin and closes on him, then flashes as the blow comes: the parry beat.
    IEnumerator LockOn(float seconds, Color colour)
    {
        SpriteRenderer ring = Glowing(ronin.Bounds.center, new Color(colour.r, colour.g, colour.b, 0f), 5f, 58);
        SpriteRenderer core = Glowing(ronin.Bounds.center, new Color(1f, 1f, 1f, 0f), 1f, 59);
        // His reticle: a bronze seal turning as it closes in.
        FxPlayer reticle = Fx.Play(Fx.KageLockOn, ronin.Bounds.center, new Color(1f, 1f, 1f, 0f), 1f, null, 59);
        Sprite[] frames = Fx.Frames(Fx.KageLockOn);
        float reticleArt = frames != null ? frames[0].bounds.size.x : 1f;
        if (reticle != null) transient.Add(reticle.gameObject);
        int pings = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            Vector3 at = ronin.Bounds.center + Vector3.back * 0.1f;
            ring.transform.position = core.transform.position = at;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(5f, 1.8f, Mathf.SmoothStep(0, 1, k));
            ring.color = new Color(colour.r, colour.g, colour.b, 0.25f + 0.4f * k);
            core.color = new Color(1f, 1f, 1f, 0.5f * k);
            if (reticle != null)
            {
                reticle.transform.position = at + Vector3.back * 0.05f;
                reticle.transform.localScale = Vector3.one * (Mathf.Lerp(4.5f, 1.8f, Mathf.SmoothStep(0, 1, k)) / reticleArt);
                reticle.Color = new Color(1f, 1f, 1f, Mathf.Min(1f, k * 3f));
            }
            // It ticks faster as it closes.
            if (k >= 1f - Mathf.Pow(0.6f, pings + 1))
            {
                pings++;
                Sfx.Play(Sfx.ClockTick, 0.35f, 0.7f + 0.08f * pings);
            }
            yield return null;
        }
        ring.color = Color.white;
        Sfx.Play(Sfx.BladeRing, 0.6f, 0.7f);
        Fx.Play(Fx.Impact, ronin.Bounds.center, colour, 1.6f);
        yield return Wait(0.15f);
        Drop(ring);
        Drop(core);
        if (reticle != null)
        {
            transient.Remove(reticle.gameObject);
            Destroy(reticle.gameObject);
        }
    }
}
