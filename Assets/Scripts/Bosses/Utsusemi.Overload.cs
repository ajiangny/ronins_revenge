using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stagger, the phase break (the overload, wild lasers and the break open), the burning shroud and embers of its second form, and its fall.
public partial class Utsusemi
{
    // A parried thrust jams the gears: sparks and a grinding shudder.
    protected override void OnStaggered()
    {
        StopPacing();
        if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 2f) == null) Fx.Play(Fx.Impact, Centre, Gold, 2f);
        Debris.BurstSprites(Centre, art.deathParticles, Gold, 12, 0.8f, 7f);
        Sfx.Play(Sfx.GearGrind);
    }

    protected override bool HasSecondPhase => true;

    // When its first bar empties the mainspring snaps: it overloads, lasering wildly in every direction as it cracks
    // open from the core out, then the shroud tears, the chest opens on its glowing core, and the clock runs fast from
    // then on.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        RestPoguru();
        // A break mid-Hour Strikes drops the armour with the rest of the move.
        armored = false;
        ShowEye(false);
        // It breaks open in its row, not wherever a cut-short dash left it.
        yield return BackToRow();

        // The mainspring slips: it seizes with a jolt.
        animator.Play(art.hit, false, null, art.actionFps * 0.5f);
        HitFlash.Flash(sprite, 0.15f);
        CameraShake.Add(0.3f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.75f);
        Music.Duck(0.15f, 8f);
        yield return new WaitForSeconds(0.4f);
        // The whole hall shakes with it and rocks rain down; one or two come to rest in the cover row and stay, which
        // its first move once it has broken open (the Hyper Beam) will need.
        StartCoroutine(Rockfall(OverloadSeconds + 0.6f, Random.Range(1, 3), false));
        yield return Overload();

        // It stops dead. A bell tolls once, and the air ripples with it.
        Sfx.Play(Sfx.MidnightChime, 1f, 0.7f);
        TimeRipple.Emit(Centre, 14f, 0.8f, 1.2f);
        yield return new WaitForSeconds(0.55f);

        // Then it breaks open: gears and parts burst off it, the hat shatters into a halo and the core blazes out.
        CameraShake.Add(0.6f);
        HitFlash.Flash(sprite, 0.2f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.6f);
        Sfx.Play(Sfx.StageStart, 1f, 0.8f);
        Sfx.Play(Sfx.HatClang, 0.9f, 0.7f);
        if (Fx.Play(Fx.UtsuGears, Centre, Color.white, 3f) == null) Fx.Play(Fx.Explosion, Centre, Gold, 2.5f);
        if (Fx.Play(Fx.UtsuParts, Head, Color.white, 2.5f) == null) Fx.Play(Fx.Impact, Head, Gold, 2.5f);
        Debris.BurstSprites(Centre, art.deathParticles, Lace, 30, 1.2f, 8f);
        WearPhaseArt();
        // The painting-drawn second form (152 px frames) carries its own burnt look; only the old recolour needs dimming.
        painted = art.idle != null && art.idle.Length > 0 && art.idle[0].rect.width > 140f;
        if (painted)
        {
            eyePixel = new Vector2(76.5f, 44.5f);
            corePixel = new Vector2(79.5f, 59.5f);
        }
        else bodyTint = Color.Lerp(bodyTint, new Color(0.75f, 0.7f, 0.8f), 0.5f);
        if (phaseBareIdle != null) bareIdle = phaseBareIdle;
        if (phaseUnarmedIdle != null) unarmedIdle = phaseUnarmedIdle;
        if (phaseDash != null) dashFrames = phaseDash;
        if (phaseChop != null) chopFrames = phaseChop;
        if (phaseSlash != null) slashFrames = phaseSlash;
        if (phaseSwordThrow != null) swordThrowFrames = phaseSwordThrow;
        // The patched bare-and-unarmed stance is built from the idles, so it is rebuilt from the new ones.
        bareUnarmedIdle = null;
        PlayIdle();
        AddCore();
        StartCoroutine(BurnShroud());
        // The core opens with a radial burst of light: phase 2 fights with lasers.
        for (int i = 0; i < 8; i++)
        {
            Vector3 ray = Quaternion.Euler(0, 0, i * 45f + 22.5f) * Vector3.right * 22f;
            SpriteRenderer flash = Bar(Core, Core + ray, 0.25f, new Color(1f, 0.85f, 0.8f, 0.9f), 57);
            transient.Remove(flash.gameObject);
            Fader.Fade(flash, 0.35f);
        }
        Sfx.Play(Sfx.LanternCharge, 1f, 1.3f);
        ElementFx.Glow(Core, new Color(1f, 0.45f, 0.12f), 0.8f, 4f, 0.9f, transform);
        // Overdriven: it hangs there, burning, then a title card of its broken form, over which it speaks; without
        // its art, a subtitle.
        if (PhaseCard != null)
        {
            yield return new WaitForSeconds(1f);
            yield return PhaseCard(this);
        }
        else
        {
            Speak(BossLine.Phase);
            yield return new WaitForSeconds(1.8f);
        }
        hyperNext = true;
        breaking = false;
    }

    // How long it overloads before it stops dead, and how far apart its wild lasers fire at the start and the end.
    const float OverloadSeconds = 2.4f;
    const float WildLaserGapStart = 0.45f, WildLaserGapEnd = 0.07f;

    // It overloads: it shudders in place while cracks spread from its core over its body and hat, its clock racing,
    // each tick closer on the last, and its eye and core loose lasers in every direction, ever faster, turning molten
    // as the cracks open. The lasers are wild, not aimed: they scorch the ground but can't hurt the ronin. Without the
    // cracking art it shudders in its hit pose, sparks spitting from its joints.
    IEnumerator Overload()
    {
        Sprite[] cracks = Fx.Frames(Fx.UtsuCracking);
        StartCoroutine(Tremble(OverloadSeconds, 0.08f, 1.5f));
        int stage = -1;
        float nextTick = 0, tick = 0.3f, nextShot = 0.2f;
        for (float t = 0; t < OverloadSeconds; t += Time.deltaTime)
        {
            float k = t / OverloadSeconds;
            int now = cracks != null ? Mathf.Min(cracks.Length - 1, (int)(k * cracks.Length)) : (int)(k * 6);
            if (now != stage)
            {
                // Another crack splits open with a snap, spitting sparks.
                stage = now;
                if (cracks != null) animator.Play(new[] { cracks[stage] }, true);
                Vector3 split = Core + (Vector3)(Random.insideUnitCircle * sprite.bounds.extents.x * 0.5f);
                Fx.Play(Fx.Impact, split, stage < 3 ? Gold : Molten, 0.8f);
                Sfx.Play(Sfx.ParryClang, 0.35f, Random.Range(0.6f, 0.8f));
                ElementFx.Glow(Core, Color.Lerp(CoreRed, Molten, k), 0.5f, 1.5f + k * 2f, 0.3f, transform);
                CameraShake.Add(0.12f + 0.1f * k);
            }
            if (t >= nextTick)
            {
                Sfx.Play(Sfx.ClockTick, 0.7f, 1f + k * 0.8f);
                nextTick = t + tick;
                tick = Mathf.Max(0.05f, tick * 0.85f);
            }
            if (t >= nextShot)
            {
                StartCoroutine(WildLaser(k > 0.45f, Mathf.Lerp(0.4f, 0.2f, k)));
                nextShot = t + Mathf.Lerp(WildLaserGapStart, WildLaserGapEnd, k * k) * Random.Range(0.7f, 1.3f);
            }
            yield return null;
        }
    }

    // One wild laser from its eye or core: mostly raking the ground, some flung up and out to the sides, swinging as
    // it fires. It burns the ground where it lands; only a `harmful` one (the Hyper Beam's overload, not the phase
    // break's) hits the ronin, and cover stops that one.
    IEnumerator WildLaser(bool molten, float preview, bool harmful = false)
    {
        bool fromEye = Random.value < 0.4f;
        float angle = Random.value < 0.75f ? Random.Range(-160f, -20f) : Random.Range(-15f, 195f);
        float swing = Random.Range(-40f, 40f);
        float seconds = Random.Range(0.18f, 0.32f);

        // Previewed first: a thin flickering line where it will open fire, a fainter one where its swing will end, and
        // a glow growing on the ground where each lands, so even a wild shot reads before it comes.
        Color tint = molten ? Molten : CoreRed;
        Vector3 Reach(Vector3 from, float a, out bool ground)
        {
            Vector3 heading = Quaternion.Euler(0, 0, a) * Vector3.right;
            ground = heading.y < -0.05f;
            return ground ? from + heading * ((GroundY - from.y) / heading.y) : from + heading * 30f;
        }
        SpriteRenderer startLine = Bar(Core, Core, 0.04f, Color.clear, 54);
        SpriteRenderer endLine = Bar(Core, Core, 0.03f, Color.clear, 54);
        SpriteRenderer startMark = GroundMark(tint), endMark = GroundMark(tint);
        for (float t = 0; t < preview; t += Time.deltaTime)
        {
            float k = t / preview;
            float flicker = 0.6f + 0.4f * Mathf.Sin(Time.time * 60f);
            Vector3 from = fromEye ? Eye : Core;
            Vector3 start = Reach(from, angle, out bool startGround), end = Reach(from, angle + swing, out bool endGround);
            Stretch(startLine, from, start, 0.03f + 0.03f * k);
            Stretch(endLine, from, end, 0.02f);
            startLine.color = new Color(tint.r, tint.g, tint.b, (0.25f + 0.45f * k) * flicker);
            endLine.color = new Color(tint.r, tint.g, tint.b, 0.18f * flicker);
            PlaceMark(startMark, start, startGround, tint, k, 1f);
            PlaceMark(endMark, end, endGround, tint, k, 0.6f);
            yield return null;
        }
        RemoveTransient(startLine);
        RemoveTransient(endLine);
        RemoveTransient(startMark);
        RemoveTransient(endMark);

        Beam beam = MakeBeam(fromEye ? Eye : Core, 0.7f, molten);
        Sfx.Play(Sfx.LanternCharge, 0.3f, Random.Range(1.9f, 2.5f));
        CameraShake.Add(0.06f);
        for (float t = 0; t < seconds && beam.body != null; t += Time.deltaTime)
        {
            Vector3 from = fromEye ? Eye : Core;
            Vector3 heading = Quaternion.Euler(0, 0, angle + swing * t / seconds) * Vector3.right;
            bool ground = heading.y < -0.05f;
            Vector3 to = ground ? from + heading * ((GroundY - from.y) / heading.y) : from + heading * 30f;
            if (harmful && ground)
            {
                to = BeamEnd(from, to, out Blocker cover);
                if (cover == null) BeamHit(from, to);
                ground = cover == null;
            }
            DrawBeam(beam, from, to, t < 0.04f, ground);
            yield return null;
        }
        EndBeam(beam);
    }

    // A soft glow on the ground where a previewed laser will land.
    SpriteRenderer GroundMark(Color tint)
    {
        var mark = new GameObject("LaserMark").AddComponent<SpriteRenderer>();
        mark.sprite = ProceduralSprites.SoftGlow;
        mark.sortingOrder = 3;
        mark.color = Color.clear;
        transient.Add(mark.gameObject);
        return mark;
    }

    static void PlaceMark(SpriteRenderer mark, Vector3 at, bool onGround, Color tint, float k, float strength)
    {
        if (mark == null) return;
        mark.enabled = onGround;
        mark.transform.position = new Vector3(at.x, at.y, 0.3f);
        mark.transform.localScale = new Vector3(1.6f, 0.5f, 1f) * (0.5f + 0.7f * k);
        mark.color = new Color(tint.r, tint.g, tint.b, (0.2f + 0.45f * k) * strength);
    }

    void AddCore()
    {
        coreGlow = Projectile.CreateGlow(sprite.transform, 1.4f, sprite.sortingOrder + 1, CoreMolten);
        coreGlow.transform.position = Core + Vector3.back * 0.3f;
        StartCoroutine(PulseCore());
    }

    // The opened core burns molten orange, like the beams it fires, not the first form's red.
    static readonly Color CoreMolten = new Color(1f, 0.5f, 0.14f);

    const float EmberGap = 0.022f;
    const int MaxEmbers = 90;
    // Embers cool through these in hard steps, never blending: white-hot, gold, orange, ember red, a dying char.
    static readonly Color[] EmberSteps =
    {
        new Color(1f, 1f, 0.92f), new Color(1f, 0.86f, 0.36f), new Color(0.95f, 0.36f, 0.05f),
        new Color(0.62f, 0.12f, 0.04f), new Color(0.3f, 0.06f, 0.04f),
    };

    class Ember
    {
        public SpriteRenderer r;
        public Vector3 at, velocity;
        public float age, life, nextKick, drag;
    }
    readonly List<Ember> embers = new List<Ember>();

    // Embers in the air when the fight is torn down go with it.
    protected override void OnCleared()
    {
        foreach (Ember e in embers) if (e.r != null) Destroy(e.r.gameObject);
        embers.Clear();
        ClearStoppedTime();
        TimeRipple.Clear();
        plumbsOut = false;
        RestPoguru();
        foreach (Rock rock in rocks) if (rock.cover != null) Destroy(rock.cover.gameObject);
        rocks.Clear();
        hyperNext = false;
    }

    // One world pixel of its sprite, so its fire is drawn on its own pixel grid.
    float BodyPixel => sprite.sprite != null ? sprite.transform.lossyScale.x / sprite.sprite.pixelsPerUnit : 0.05f;

    Vector3 SnapToPixels(Vector3 at)
    {
        float pixel = BodyPixel;
        return new Vector3(Mathf.Round(at.x / pixel) * pixel, Mathf.Round(at.y / pixel) * pixel, at.z);
    }

    void SpawnEmber(Vector3 from, Vector3 velocity, float life)
    {
        if (embers.Count >= MaxEmbers) return;
        var ember = new GameObject("Ember").AddComponent<SpriteRenderer>();
        ember.sprite = ProceduralSprites.White;
        ember.sortingOrder = sprite.sortingOrder + 2;
        // Tiny specks: one of its own pixels, now and then two, so the sparks read as pixel art beside it.
        ember.transform.localScale = Vector3.one * BodyPixel * (Random.value < 0.85f ? 1f : 2f);
        embers.Add(new Ember { r = ember, at = from + Vector3.back * 0.25f, velocity = velocity, life = life, drag = Random.Range(0.5f, 2.5f) });
    }

    // Its second form burns, as in its painting: embers boil off the shroud in fits, jerked about by gusts and kicks,
    // some spat out sideways in bursts, cooling in hard steps on its own pixel grid.
    IEnumerator BurnShroud()
    {
        // Heat glowing under its feet, beneath the body.
        var heat = new GameObject("Heat").AddComponent<SpriteRenderer>();
        heat.transform.SetParent(sprite.transform, false);
        heat.sprite = ProceduralSprites.SoftGlow;
        heat.sortingOrder = sprite.sortingOrder - 2;
        float nextEmber = 0f, nextBurst = Time.time + 0.5f, gustUntil = 0f, nextGust = Time.time + 1f;
        Vector3 gust = Vector3.zero;
        while (!IsDead && sprite != null)
        {
            // Its fire stands still while it holds time.
            if (timeHeld)
            {
                yield return null;
                continue;
            }
            float throb = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            heat.transform.position = new Vector3(sprite.bounds.center.x, sprite.bounds.min.y + sprite.bounds.size.y * 0.12f, 0.1f);
            heat.transform.localScale = new Vector3(3.2f, 1.2f, 1f) / sprite.transform.lossyScale.x;
            heat.color = new Color(Molten.r, Molten.g * 0.8f, Molten.b, 0.18f + 0.08f * throb);

            // Gusts: every so often every ember is shoved one way together.
            if (Time.time >= nextGust)
            {
                gust = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(-0.3f, 0.8f), 0);
                gustUntil = Time.time + Random.Range(0.15f, 0.35f);
                nextGust = Time.time + Random.Range(0.6f, 1.6f);
            }
            Vector3 gustNow = Time.time < gustUntil ? gust : Vector3.zero;

            Bounds b = sprite.bounds;
            if (Time.time >= nextEmber)
            {
                nextEmber = Time.time + EmberGap * Random.Range(0.3f, 1.7f);
                // Off the lower shroud, where the painting's hem burns.
                Vector3 from = new Vector3(Random.Range(b.min.x + b.size.x * 0.08f, b.max.x - b.size.x * 0.08f),
                    b.min.y + b.size.y * Random.Range(0.05f, 0.55f), 0);
                SpawnEmber(from, new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 2.4f), 0), Random.Range(0.4f, 1.2f));
            }
            // Bursts: the shroud spits a fistful of sparks out sideways from one ragged end.
            if (Time.time >= nextBurst)
            {
                nextBurst = Time.time + Random.Range(0.25f, 0.8f);
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 from = new Vector3(side < 0 ? b.min.x + b.size.x * 0.15f : b.max.x - b.size.x * 0.15f,
                    b.min.y + b.size.y * Random.Range(0.1f, 0.45f), 0);
                int count = Random.Range(6, 13);
                for (int n = 0; n < count; n++)
                {
                    SpawnEmber(from, new Vector3(side * Random.Range(1.5f, 4.5f), Random.Range(-0.5f, 2.5f), 0), Random.Range(0.25f, 0.6f));
                }
            }

            for (int i = embers.Count - 1; i >= 0; i--)
            {
                Ember e = embers[i];
                e.age += Time.deltaTime;
                if (e.age >= e.life || e.r == null)
                {
                    if (e.r != null) Destroy(e.r.gameObject);
                    embers.RemoveAt(i);
                    continue;
                }
                // Kicked about at random, rising as they go, slowed by the air.
                if (Time.time >= e.nextKick)
                {
                    e.velocity += new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-0.4f, 1.2f), 0);
                    e.nextKick = Time.time + Random.Range(0.04f, 0.16f);
                }
                e.velocity = Vector3.Lerp(e.velocity, new Vector3(0, 1.2f, 0), e.drag * Time.deltaTime);
                e.at += (e.velocity + gustNow) * Time.deltaTime;
                e.r.transform.position = SnapToPixels(e.at);
                // Cooling in hard steps, and guttering: some frames it blinks out.
                float k = e.age / e.life;
                Color c = EmberSteps[Mathf.Min(EmberSteps.Length - 1, (int)(k * EmberSteps.Length))];
                e.r.color = new Color(c.r, c.g, c.b, Random.value < 0.12f * k ? 0f : 1f);
            }
            yield return null;
        }
        if (heat != null) Destroy(heat.gameObject);
        OnCleared();
    }

    // The heart beats in time with the clock.
    IEnumerator PulseCore()
    {
        while (coreGlow != null && !IsDead)
        {
            float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 4f)), 4f);
            // On the heart in whatever pose it's in.
            coreGlow.transform.position = Core + Vector3.back * 0.3f;
            coreGlow.color = new Color(CoreMolten.r, CoreMolten.g, CoreMolten.b, 0.45f + 0.45f * pulse);
            coreGlow.transform.localScale = Vector3.one * (1.2f + 0.5f * pulse) / transform.lossyScale.x;
            yield return null;
        }
    }

    // It locks up mid-tick and comes apart: hat, arm and shroud fall away and the heart gutters out.
    protected override IEnumerator Fall()
    {
        ShowEye(false);
        foreach (Arrow diamond in lattice)
        {
            if (diamond != null) Destroy(diamond.gameObject);
        }
        if (coreGlow != null) Destroy(coreGlow.gameObject);
        if (Fx.Play(Fx.UtsuParts, Centre, Color.white, 3f) == null) Fx.Play(Fx.Impact, Centre, Gold, 3f);
        Debris.BurstSprites(Centre, art.deathParticles, Gold, 20, 1f, 8f);
        Debris.BurstSprites(Centre, art.deathParticles, Lace, 16, 0.9f, 6f);
        Sfx.Play(Sfx.GearGrind, 1f, 0.6f);
        return base.Fall();
    }
}
