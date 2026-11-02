using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The phase break that gathers the souls of her shrine into Kuon, and her fall.
public partial class Shirogane
{
    // When her first bar empties she staggers to a knee and her blades go dark; mist spreads over the field and a
    // howl sounds far off. The souls of her shrine answer: wisps of spirit light rise all over the court and spiral
    // into one soul beside her, which swells as each merges, Kuon's shape gathering inside it. The soul crushes to a
    // point, two eyes open in it, and it bursts: the wolf steps out of the merge in blue flame. She rises wreathed in
    // it, with her second bar.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        StopRoaming();
        StopHunting();
        ultimate = false;
        exposed = false;
        animator.Play(art.hit, false, null, art.actionFps * 0.7f);
        Sfx.Play(Sfx.BossStagger, 0.8f, 0.9f);
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < 7; i++)
        {
            float x = CameraShake.Center.x + Mathf.Lerp(-HalfWidth, HalfWidth, i / 6f);
            Puff(new Vector3(x, GroundY + Random.Range(0.5f, 2.5f), 0), 2.6f);
        }
        Sfx.Play(Sfx.MistSwirl, 1f, 0.6f);
        yield return new WaitForSeconds(0.6f);
        Sfx.Play(Sfx.WolfHowl, 0.5f, 0.85f);

        Vector3 heart = WolfRest + Vector3.up * 0.3f;
        yield return GatherSouls(heart, SoulGather);
        yield return wolf.OpenEyes(heart, 0.5f);

        // The merged soul bursts and Kuon steps out of it.
        CameraShake.Add(0.7f);
        if (Fx.Play(Fx.ShiroMawBurst, heart, Color.white, 3.2f, null, 58) == null) Fx.Play(Fx.Explosion, heart, Cyan, 3f);
        Fx.Play(Fx.Explosion, heart, Cyan, 2.4f);
        ElementFx.Glow(heart, Frost, 1f, 6f, 0.5f);
        Debris.BurstSprites(heart, art.deathParticles, Cyan, 30, 1.2f, 9f);
        Sfx.Play(Sfx.WolfHowl, 1f, 1f);
        Sfx.Play(Sfx.FireBlast, 0.8f, 1.3f);
        RemoveSoulCore();
        Dim(0f, 0.8f);
        wolf.SetPresence(SpiritWolf.Presence.Present);
        yield return wolf.AppearAt(WolfRest);

        bodyTint = new Color(0.85f, 0.96f, 1f);
        WearPhaseArt();
        if (phaseDash != null) dashFrames = phaseDash;
        AddAura();
        PlayIdle();
        // Kuon takes the field as a foe of its own, sharing her bar.
        wolf.Arm();
        WolfJoined?.Invoke(wolf);
        movesSinceParry = 1;
        movesSinceEclipse = 0;
        // Her oath: a title card with her and the wolf, over which she speaks; without its art, a subtitle.
        if (PhaseCard != null) yield return PhaseCard(this);
        else
        {
            Speak(BossLine.Phase);
            RenameBar?.Invoke($"{PhaseTitle}, {OathEpithet}");
            yield return new WaitForSeconds(2.2f);
        }
        breaking = false;
    }

    // How long the shrine's souls take to gather into Kuon, and how many come.
    const float SoulGather = 3.6f;
    const int Souls = 26;

    // The merging soul and the bloom behind it, while the souls gather.
    SpriteRenderer soulCore, soulHalo;
    // Each gathering wisp's soft glow.
    readonly SpriteRenderer[] wispGlows = new SpriteRenderer[Souls];

    // Wisps of spirit light rise from all over the court, near the ground and up the walls, and spiral in to `heart`,
    // each arriving on its own beat. Every one that merges swells the soul there with a rising chime, and Kuon's
    // shape grows inside it. The arena darkens and the ground shakes harder as it fills; once all have merged the
    // soul crushes to a white-hot point.
    IEnumerator GatherSouls(Vector3 heart, float seconds)
    {
        Dim(0.5f, 0.8f);
        soulHalo = Glowing("SoulHalo", ProceduralSprites.SoftGlow, 56);
        Sprite[] coreFrames = Fx.Frames(Fx.ShiroMawOrb);
        soulCore = Glowing("SoulCore", coreFrames != null ? coreFrames[0] : ProceduralSprites.SoftGlow, 57);
        if (coreFrames != null) soulCore.gameObject.AddComponent<SpriteAnimator>().Play(coreFrames, true, null, 10f);
        Sprite[] wispFrames = Fx.Frames(Fx.ShiroSpiritWisp);
        Sfx.Play(Sfx.MistSwirl, 0.9f, 0.8f);
        Sfx.Play(Sfx.LanternCharge, 0.6f, 0.6f);

        var wisps = new SpriteRenderer[Souls];
        var starts = new Vector3[Souls];
        var delays = new float[Souls];
        var travels = new float[Souls];
        var spins = new float[Souls];
        var last = new Vector3[Souls];
        for (int i = 0; i < Souls; i++)
        {
            // Spread across the whole court, most rising from the ground, a few from higher up the walls.
            float x = CameraShake.Center.x + Mathf.Lerp(-HalfWidth + 1f, HalfWidth - 1f, (i + Random.value) / Souls);
            float y = i % 3 == 0 ? Random.Range(GroundY + 3f, heart.y - 1f) : GroundY + Random.Range(0.3f, 1.2f);
            starts[i] = new Vector3(x, y, 0);
            // Their arrivals are spread over the whole gather, a trickle that becomes a flood.
            delays[i] = seconds * 0.55f * Mathf.Sqrt(Random.value);
            travels[i] = Random.Range(1.6f, 2.3f);
            spins[i] = Random.Range(90f, 200f) * (x < heart.x ? -1f : 1f);
            last[i] = starts[i];
        }

        int merged = 0;
        float nextRumble = 0f;
        float end = seconds;
        for (int i = 0; i < Souls; i++) end = Mathf.Max(end, delays[i] + travels[i]);
        for (float t = 0; t < end; t += Time.deltaTime)
        {
            for (int i = 0; i < Souls; i++)
            {
                float u = (t - delays[i]) / travels[i];
                if (u < 0f || (u >= 1f && wisps[i] == null)) continue;
                if (wisps[i] == null)
                {
                    // It rises out of a breath of mist.
                    wisps[i] = Glowing("Wisp", wispFrames != null ? wispFrames[0] : ProceduralSprites.Glow, 58);
                    if (wispFrames != null) wisps[i].gameObject.AddComponent<SpriteAnimator>().Play(wispFrames, true, null, 12f);
                    wisps[i].transform.localScale = Vector3.one * (wispFrames != null ? Random.Range(1.5f, 2.1f) : 0.7f);
                    wispGlows[i] = Glowing("WispGlow", ProceduralSprites.SoftGlow, 57);
                    wispGlows[i].transform.localScale = Vector3.one * 1.6f;
                    Puff(starts[i], 1.2f);
                }
                if (u >= 1f)
                {
                    // It merges: the soul swells, rings and flashes.
                    Drop(wisps[i]);
                    Drop(wispGlows[i]);
                    wisps[i] = null;
                    delays[i] = float.MaxValue;
                    merged++;
                    Fx.Play(Fx.Impact, heart, Frost, 0.8f + 0.04f * merged);
                    Sfx.Play(Sfx.SoulCollect, 0.35f, 0.8f + 0.05f * merged);
                    continue;
                }
                // Up off the ground first, then a spiral that tightens onto the soul.
                float pull = u * u;
                Vector3 offset = starts[i] - heart;
                Vector3 at = heart + Quaternion.Euler(0, 0, spins[i] * u) * offset * (1f - pull) + Vector3.up * (Mathf.Sin(Mathf.PI * u) * 1.2f);
                Vector3 step = at - last[i];
                last[i] = at;
                wisps[i].transform.position = at + Vector3.back * 0.3f;
                // Drawn flying right, so it turns to face where it goes.
                if (step.sqrMagnitude > 0.00001f) wisps[i].transform.rotation = Quaternion.FromToRotation(Vector3.right, step);
                float fade = Mathf.Clamp01(u / 0.2f);
                wisps[i].color = wispFrames != null ? new Color(1f, 1f, 1f, fade) : new Color(Frost.r, Frost.g, Frost.b, 0.9f * fade);
                wispGlows[i].transform.position = at + Vector3.back * 0.25f;
                wispGlows[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f * fade);
                if (Random.value < 0.15f) Afterimage(wisps[i], new Color(Spirit.r, Spirit.g, Spirit.b, 0.3f * fade));
            }

            float full = (float)merged / Souls;
            float size = Mathf.Lerp(0.5f, 3.6f, full) * (1f + 0.08f * Mathf.Sin(t * 30f));
            soulCore.transform.position = heart + Vector3.back * 0.4f;
            soulCore.transform.localScale = Vector3.one * size / (coreFrames != null ? OrbArtSize : 0.6f);
            // Kept see-through, so the wolf taking shape inside shows.
            soulCore.color = coreFrames != null ? new Color(1f, 1f, 1f, 0.3f + 0.3f * full) : new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f + 0.3f * full);
            soulHalo.transform.position = heart + Vector3.back * 0.35f;
            soulHalo.transform.localScale = Vector3.one * size * 2.6f;
            soulHalo.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f + 0.25f * full);
            // Kuon's shape gathers inside the soul as it fills.
            wolf.Form(heart, 0.9f * full);
            if (t >= nextRumble)
            {
                nextRumble = t + 0.15f;
                CameraShake.Add(0.02f + 0.06f * full);
            }
            yield return null;
        }

        // Any wisp still a frame short of the soul when time runs out merges with the rest.
        for (int i = 0; i < Souls; i++)
        {
            Drop(wisps[i]);
            Drop(wispGlows[i]);
            wisps[i] = wispGlows[i] = null;
        }

        // All have merged: the soul bulges once, then crushes to a white-hot point.
        Vector3 from = soulCore.transform.localScale;
        float halo = soulHalo.transform.localScale.x;
        for (float t = 0; t < 0.35f; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            float squeeze = k < 0.3f ? 1f + 0.2f * (k / 0.3f) : Mathf.Lerp(1.2f, 0.1f, Mathf.SmoothStep(0, 1, (k - 0.3f) / 0.7f));
            soulCore.transform.localScale = from * squeeze;
            soulHalo.transform.localScale = Vector3.one * halo * squeeze;
            soulHalo.color = new Color(Frost.r, Frost.g, Frost.b, 0.4f + 0.5f * k);
            yield return null;
        }
        Fx.Play(Fx.Impact, heart, Color.white, 1.6f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 0.7f);
        Dim(0.5f, 0.3f);
    }

    void RemoveSoulCore()
    {
        Drop(soulCore);
        Drop(soulHalo);
        soulCore = soulHalo = null;
    }

    // Blue spirit flame licking up from her feet and a cold glow pulsing behind her.
    void AddAura()
    {
        Vector3 feet = new Vector3(Centre.x, sprite.bounds.min.y + sprite.bounds.size.y * 0.12f, 0);
        auraFlames = Fx.Play(Fx.ShiroAura, feet, new Color(0.5f, 0.9f, 1f, 0.9f), 3.2f, transform, sprite.sortingOrder - 1);
        auraGlow = Projectile.CreateGlow(sprite.transform, 2.2f, sprite.sortingOrder - 2, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
        StartCoroutine(PulseAura(auraGlow));
    }

    // Enraged, the glow burns hotter and quicker, flaring from spirit blue to blood red; it goes out when she kneels.
    IEnumerator PulseAura(SpriteRenderer glow)
    {
        while (glow != null && !IsDead && !Down)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (enraged ? 7f : 3.5f));
            Color colour = enraged ? Color.Lerp(Cyan, Rage, pulse) : Cyan;
            glow.color = new Color(colour.r, colour.g, colour.b, (enraged ? 0.4f : 0.25f) + 0.3f * pulse);
            glow.transform.localScale = Vector3.one * ((enraged ? 2.4f : 2f) + 0.4f * pulse);
            yield return null;
        }
        if (glow != null) Fader.Fade(glow, 0.8f);
    }

    // Her fall: the swords drop from her hands, she sinks to a knee and her flames fade; the wolf comes to stand
    // by her and she rests a hand on its head; it dissolves into light, she speaks her last, and follows it.
    protected override IEnumerator Fall()
    {
        StopRoaming();
        StopHunting();
        if (wolf != null) wolf.Disarm();
        ultimate = false;
        exposed = false;
        if (auraFlames != null) auraFlames.Stop();
        if (auraGlow != null) Destroy(auraGlow.gameObject);
        CameraShake.Add(0.6f);
        Debris.BurstSprites(Centre, art.deathParticles, Cyan, 24, 1f, 8f);
        if (bladeGlow != null) Fader.Fade(bladeGlow, 1.5f);
        // If she went down before Kuon fell she is already kneeling, blades dropped, and the scene picks up from there.
        if (!Down)
        {
            Sfx.Play(Sfx.BladeRing, 1f, 0.6f);
            DropBlade(Hands + Vector3.left * 0.6f, -1f);
            DropBlade(Hands + Vector3.right * 0.6f, 1f);
            // The defeat clip plays down to her knee and holds there.
            if (art.death != null && art.death.Length > 0) animator.Play(art.death, false, null, art.actionFps * 0.6f);
            else sprite.transform.localRotation = Quaternion.Euler(0, 0, -10f);
            Vector3 from = anchor;
            for (float t = 0; t < 1f; t += Time.deltaTime)
            {
                anchor = from + Vector3.down * (0.25f * t);
                body.MovePosition(anchor);
                yield return null;
            }
        }
        else yield return new WaitForSeconds(1f);

        if (wolf != null)
        {
            wolf.SetPresence(SpiritWolf.Presence.Present);
            if (!wolf.Visible) yield return wolf.AppearAt(WolfRest);
            yield return wolf.WalkTo(anchor + new Vector3(anchor.x < CameraShake.Center.x ? 1.6f : -1.6f, -0.1f, 0f), 1.6f);
        }
        // Her Defeat line was spoken as she fell; this is the beat she rests her hand on its head.
        yield return new WaitForSeconds(2.6f);
        if (wolf != null)
        {
            Debris.BurstSprites(wolf.transform.position, art.deathParticles, Cyan, 30, 1.1f, 5f);
            Sfx.Play(Sfx.SoulCollect, 0.8f, 0.8f);
            yield return wolf.Dissolve(1.2f);
        }
        Speak(BossLine.DefeatFinal);
        yield return new WaitForSeconds(2.4f);

        Sfx.Play(Sfx.PlayerDeath, 0.8f, 0.9f);
        for (float t = 0; t < 1.8f; t += Time.deltaTime)
        {
            float k = t / 1.8f;
            Color c = bodyTint;
            c.a = bodyTint.a * (1f - k);
            sprite.color = c;
            if (Random.value < 0.3f) Debris.BurstSprites(Centre + (Vector3)Random.insideUnitCircle * 0.6f, art.deathParticles, Cyan, 2, 0.9f, 2.5f);
            yield return null;
        }
        Destroy(gameObject);
    }

    // A dropped katana: tumbles to the ground and fades.
    void DropBlade(Vector3 from, float spin)
    {
        SpriteRenderer blade = SwordPicture(from, Random.Range(-20f, 20f));
        transient.Remove(blade.gameObject);
        TumblingBlade.Drop(blade, spin, GroundY + 0.4f);
    }
}
