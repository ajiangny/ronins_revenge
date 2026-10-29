using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Phase 2, the Unchained Wraith: the Bloom, the Soul River, the Broken Chains, the parry-only Soul Coffin and the
// Night of a Thousand Bells.
public partial class Kagetsune
{
    // --- Phase 2 ---

    // Thousand-Soul Bloom (the Wraith Swarm): a flower of skulls blooms out of his chest, stops dead in the air, then
    // turns on the ronin in two volleys a beat apart. Aimed shots meet where he stood, so a step clears the first;
    // the second is aimed where he stepped to.
    IEnumerator WraithSwarm()
    {
        Bark(BossLine.WraithSwarm, 7f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.8f);
        int blooms = lastBell ? 3 : 2;
        int count = lastBell ? 30 : 24;
        for (int wave = 0; wave < blooms; wave++)
        {
            yield return BulletHell.Room(count);
            List<Arrow> bloom = BulletHell.Ring(Skull, Chest, count, 4f / ShotSpeed, wave * 180f / count);
            Fx.Play(Fx.Impact, Chest, Teal, 2f);
            ElementFx.Glow(Chest, Teal, 1f, 6f, 0.4f);
            StartCoroutine(BulletHell.HaltAndAim(bloom, () => ronin.Bounds.center, 0.55f, 0.45f, 2, 0.55f, 7.5f, Wait));
            yield return Wait(1.7f);
        }
    }

    // Soul River: rows of skulls pour down with one channel through them that snakes from side to side, never faster
    // than he can follow. Now and then a bronze bell rides the channel's bank: parry it, and the river widens.
    IEnumerator SoulRiver()
    {
        Bark(BossLine.WraithSwarm, 7f);
        const float Spacing = 1.6f;
        const float Speed = 6.5f;
        const float Between = 0.45f;
        const float Width = 3.2f;
        float lo = FieldLeft + Width, hi = FieldRight - Width;
        float centre = (lo + hi) / 2f;
        float sway = Mathf.Min((hi - lo) / 2f, 7f);
        // The channel's speed along his row is sway × rate: kept under the dodge limit.
        float rate = (BulletHell.MaxDodgeSpeed * 0.8f) / sway;
        float phase = Mathf.Asin(Mathf.Clamp((ronin.Bounds.center.x - centre) / sway, -1f, 1f));
        int rows = lastBell ? 14 : 11;
        float last = centre + sway * Mathf.Sin(phase);
        for (int row = 0; row < rows; row++)
        {
            yield return BulletHell.Room(24);
            float gap = centre + sway * Mathf.Sin(phase + rate * row * Between);
            BulletHell.GapOk(last, gap, Between, Width, "Soul River");
            last = gap;
            // Every fourth row, the skull on one bank is a bell.
            float bank = gap + (row % 8 == 3 ? -1f : 1f) * (Width / 2f + Spacing * 0.75f);
            float left = FieldLeft;
            bool bellRow = row % 4 == 3 && !SealActive;
            BulletHell.Curtain(Skull, TopOfScreen, FieldLeft, FieldRight, Spacing, new[] { gap }, Width, Speed,
                i => bellRow && Mathf.Abs(left + i * Spacing - bank) < Spacing / 2f ? BellShot : null);
            if (row % 2 == 0) Toll(0.3f, 1.1f + 0.03f * row);
            yield return Wait(Between);
        }
    }

    // Broken Chains: the snapped chains lash from his shoulders, each sweeping a stretch of the ground. A thin
    // line marks where each lash starts and ends.
    IEnumerator BrokenChains()
    {
        Bark(BossLine.BrokenChains, 7f);
        yield return ChainCage();
        int lashes = 2;
        for (int i = 0; i < lashes; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 root = Shoulder(side);
            float centre = ClampX(ronin.Bounds.center.x + Random.Range(-1f, 1f));
            float dir = Random.value < 0.5f ? -1f : 1f;
            float a = ClampX(centre - dir * 4.5f), b = ClampX(centre + dir * 4.5f);
            SpriteRenderer startTell = Bar(root, new Vector3(a, GroundY, 0), 0.05f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.6f));
            SpriteRenderer endTell = Bar(root, new Vector3(b, GroundY, 0), 0.05f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.3f));
            yield return Wait(0.65f);
            Drop(startTell);
            Drop(endTell);

            var chain = Tiled(Fx.KageChain, root, root, 0.5f, Bronze, 56);
            var glow = Bar(root, root, 0.8f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 54);
            glow.sprite = ProceduralSprites.Glow;
            if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.7f, 0.8f);
            else Sfx.Play(Sfx.ParrySwing, 0.8f, 0.6f);
            float nextBurn = 0;
            for (float t = 0; t < 0.45f; t += Time.deltaTime * Tempo)
            {
                root = Shoulder(side);
                float x = Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, t / 0.45f));
                Vector3 end = BeamEnd(root, new Vector3(x, GroundY, 0), out Blocker cover);
                Lay(chain, root, end, chain.drawMode == SpriteDrawMode.Tiled ? 0.5f : 0.22f);
                Stretch(glow, root, end, 0.8f);
                HitIfCrossed(root, end);
                if (cover != null && Time.time >= nextBurn)
                {
                    nextBurn = Time.time + 0.15f;
                    cover.Hit(end);
                }
                yield return null;
            }
            CameraShake.Add(0.2f);
            Drop(chain);
            Drop(glow);
            yield return Wait(0.2f);
        }
    }

    // Chain Cage: both chains slam into the ground either side of the ronin and stay there as walls, and while he's
    // caged a two-arm spiral of skulls winds down on him. Touching a chain burns.
    IEnumerator ChainCage()
    {
        const float HalfWidth = 3f;
        const float Hold = 3f;
        float centre = Mathf.Clamp(ronin.Bounds.center.x, FieldLeft + HalfWidth + 0.5f, FieldRight - HalfWidth - 0.5f);
        float[] xs = { centre - HalfWidth, centre + HalfWidth };
        var tells = new List<SpriteRenderer>();
        for (int i = 0; i < 2; i++)
        {
            tells.Add(Bar(Shoulder(i == 0 ? -1f : 1f), new Vector3(xs[i], GroundY, 0), 0.05f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.6f)));
        }
        yield return Wait(0.6f);
        foreach (SpriteRenderer tell in tells) Drop(tell);

        var chains = new List<SpriteRenderer>();
        var glows = new List<SpriteRenderer>();
        for (int i = 0; i < 2; i++)
        {
            chains.Add(Tiled(Fx.KageChain, Shoulder(i == 0 ? -1f : 1f), new Vector3(xs[i], GroundY, 0), 0.5f, Bronze, 56));
            SpriteRenderer glow = Bar(Shoulder(i == 0 ? -1f : 1f), new Vector3(xs[i], GroundY, 0), 0.8f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 54);
            glow.sprite = ProceduralSprites.Glow;
            glows.Add(glow);
            Fx.Play(Fx.Impact, new Vector3(xs[i], GroundY, 0), Bronze, 1.6f);
        }
        if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.9f, 0.7f);
        Slam(0.6f);
        CameraShake.Add(0.3f);
        cageSpiral = StartCoroutine(BulletHell.Spiral(Skull, () => Waist, Row, 2, Random.value < 0.5f ? 40f : -40f, Hold - 0.6f, 5f, 0.32f, Wait, Random.Range(0f, 360f)));
        for (float t = 0; t < Hold; t += Time.deltaTime * Tempo)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 root = Shoulder(i == 0 ? -1f : 1f);
                Vector3 end = new Vector3(xs[i], GroundY, 0);
                Lay(chains[i], root, end, chains[i].drawMode == SpriteDrawMode.Tiled ? 0.5f : 0.22f);
                Stretch(glows[i], root, end, 0.8f * (1f + 0.15f * Mathf.Sin(Time.time * 9f)));
                HitIfCrossed(root, end);
            }
            yield return null;
        }
        foreach (SpriteRenderer chain in chains) StartCoroutine(FadeOut(chain, 0.25f));
        foreach (SpriteRenderer glow in glows) StartCoroutine(FadeOut(glow, 0.25f));
        yield return Wait(0.3f);
    }

    // Soul Coffin, parry-only: he hurls the coffin-hammer down on its broken chain. Parried, it bursts against his
    // own armor, and he stands open.
    IEnumerator SoulCoffin()
    {
        while (SealActive) yield return Wait(0.2f);
        Bark(BossLine.SoulCoffin, 5f);
        PlayAction(art.attack);
        SpriteRenderer raised = Glowing(HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.6f), 3f);
        // Bronze, like the Last Toll's: every blow only a parry answers rings in the same colour.
        yield return LockOn(1f, Bronze);
        Drop(raised);
        Vector3 from = HammerHead;
        Arrow coffin = Shoot(from, (ronin.Bounds.center - from).normalized * 9f);
        Sprite[] frames = Fx.Frames(Fx.KageCoffin);
        if (frames != null) coffin.Restyle(frames, 8f, Color.white, new Vector2(1.6f, 2.4f), 0.7f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 3f);
        else coffin.Restyle(ProceduralSprites.White, new Color(0.22f, 0.2f, 0.22f), new Vector2(1.2f, 2f), 0.7f, new Color(Teal.r, Teal.g, Teal.b, 0.6f), 3f);
        coffin.MakeUnbreakable();
        coffin.MakeReflectable(transform, setPiece: true);
        coffinThrownAt = Time.time;
        StartCoroutine(Pursue(coffin, 9f, face: false));
        // Seal-darts fall either side of him, so running wide only trades the coffin for the darts.
        foreach (float offset in new[] { -2.6f, 2.6f, -5.2f, 5.2f })
        {
            float x = ronin.Bounds.center.x + offset;
            if (x > FieldLeft && x < FieldRight) SealDart(new Vector3(x, TopOfScreen, 0), Vector3.down * 8f);
        }
        Slam(0.6f);
        // The chain pays out behind it.
        SpriteRenderer chain = Tiled(Fx.KageChain, from, from, 0.4f, Bronze, 54);
        for (float t = 0; t < 1.6f && coffin != null; t += Time.deltaTime)
        {
            Lay(chain, HammerHead, coffin.transform.position, chain.drawMode == SpriteDrawMode.Tiled ? 0.4f : 0.12f);
            yield return null;
        }
        Drop(chain);
        yield return Wait(0.4f);
    }

    // His stance breaks (the coffin or the Last Toll sent home, or a meter filled by blows).
    protected override void OnStaggered()
    {
        ultimate = false;
        RecallEcho();
        // His own bell or coffin sent home: the whole field holds its breath for a moment.
        HitStop?.Invoke(0.12f);
        ElementFx.Glow(Centre, new Color(Bronze.r, Bronze.g, Bronze.b, 0.8f), 1f, 12f, 0.5f);
        Debris.BurstSprites(Centre, art.deathParticles, Teal, 40, 1.1f, 9f);
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        Toll(1f, 0.7f);
        Fx.Play(Fx.Impact, Centre, Bronze, 3f);
        // Opened by the coffin a moment ago: the opening waits out the stagger, which already doubles damage.
        if (Time.time < exposedUntil) exposedUntil += StaggerSeconds;
    }

    // The coffin bursting on him leaves him open whether or not it fills his stance meter. Damaged fires before
    // the stance breaks, so OnStaggered can push the opening back past the stagger.
    void OnHitBy(Vector3 point, KillSource source)
    {
        if (source != KillSource.Reflected || Time.time - coffinThrownAt >= 4f) return;
        coffinThrownAt = float.NegativeInfinity;
        exposedUntil = Time.time + ExposedSeconds;
        Fx.Play(Fx.Explosion, Chest, Teal, 2.5f);
        Debris.BurstSprites(Chest, art.deathParticles, Teal, 24, 1f, 8f);
    }

    // The Night of a Thousand Bells, his ultimate: the field goes dark, bells hang across the top, and soul-fire
    // falls down every lane but one in waves, the safe lane stepping aside each time. Then he kneels, spent.
    IEnumerator NightOfAThousandBells()
    {
        while (SealActive) yield return Wait(0.2f);
        ultimate = true;
        Bark(BossLine.ThousandBells, 5f);
        yield return Stride(CameraShake.Center.x);
        SpriteRenderer dark = Darkness();
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            dark.color = new Color(0, 0, 0, 0.55f * t / 0.8f);
            yield return null;
        }
        const int LaneCount = 8;
        List<float> lanes = Lanes(LaneCount);
        float halfWidth = (RightEdge - LeftEdge - 2.4f) / (LaneCount * 2f);
        var bells = new List<SpriteRenderer>();
        foreach (float x in lanes) bells.Add(HangBell(new Vector3(x, TopOfScreen - 0.6f, 0)));

        int safe = 0;
        for (int i = 1; i < LaneCount; i++) if (Mathf.Abs(lanes[i] - ronin.Bounds.center.x) < Mathf.Abs(lanes[safe] - ronin.Bounds.center.x)) safe = i;
        safe = Mathf.Clamp(safe + (Random.value < 0.5f ? -1 : 1), 0, LaneCount - 1);
        const int Waves = 6;
        for (int wave = 0; wave < Waves; wave++)
        {
            // The last toll has no safe lane: a wall of skulls with a bronze bell over him. Only the parry gets through.
            if (wave == Waves - 1)
            {
                yield return BulletHell.Room(26);
                float bellAt = ronin.Bounds.center.x;
                float left = FieldLeft;
                Toll(1f, 0.6f);
                foreach (SpriteRenderer bell in bells) if (bell != null) bell.color = BellColour(bell, 1f);
                BulletHell.Curtain(Skull, TopOfScreen, FieldLeft, FieldRight, 1.5f, null, BulletHell.MinGap, 4.5f,
                    i => Mathf.Abs(left + i * 1.5f - bellAt) < 0.75f ? BellShot : null);
                yield return Wait((TopOfScreen - Row) / (4.5f * ShotSpeed));
                break;
            }
            var bands = new List<SpriteRenderer>();
            var xs = new List<float>();
            for (int i = 0; i < LaneCount; i++)
            {
                if (i == safe) continue;
                xs.Add(lanes[i]);
                bands.Add(Bar(new Vector3(lanes[i], TopOfScreen, 0), new Vector3(lanes[i], GroundY, 0), halfWidth * 2f, new Color(Teal.r, Teal.g, Teal.b, 0.1f)));
                if (bells[i] != null) bells[i].color = BellColour(bells[i], 0.9f);
            }
            if (bells[safe] != null) bells[safe].color = BellColour(bells[safe], 0.2f);
            yield return CloseLanes(bands, xs, halfWidth * 1.6f, halfWidth * 0.5f, Teal, wave == 0 ? 0.9f : 0.7f, 2, 0.2f);
            foreach (SpriteRenderer band in bands) Drop(band);

            Toll(1f, 0.7f + 0.05f * wave);
            CameraShake.Add(0.3f);
            for (int i = 0; i < LaneCount; i++) if (i != safe && bells[i] != null) StartCoroutine(Swing(bells[i]));
            var columns = new List<SpriteRenderer>();
            foreach (float x in xs)
            {
                // A soul-fire column: a broad soft glow round a narrow white-hot core.
                SpriteRenderer halo = Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), halfWidth * 2.2f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 55);
                halo.sprite = ProceduralSprites.Glow;
                columns.Add(halo);
                columns.Add(Tiled(Fx.KageSoulPillar, new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), halfWidth * 1.3f, new Color(0.85f, 1f, 0.96f, 0.8f), 56));
                GroundFire.Light(new Vector3(x, GroundY, 0));
            }
            // The columns burn for their whole flash, not a single frame.
            for (float t = 0; t < ColumnSeconds; t += Time.deltaTime * Tempo)
            {
                if (Mathf.Abs(ronin.Bounds.center.x - lanes[safe]) > halfWidth * 0.95f) ronin.TakeHit(ronin.Bounds.center);
                yield return null;
            }
            foreach (SpriteRenderer column in columns) StartCoroutine(FadeOut(column, 0.35f));
            yield return Wait(0.3f - ColumnSeconds);
            // The safe lane steps one over, never off the field.
            int stepTo = safe + (Random.value < 0.5f ? -1 : 1);
            if (stepTo < 0 || stepTo >= LaneCount) stepTo = safe - (stepTo - safe);
            safe = stepTo;
        }
        foreach (SpriteRenderer bell in bells) Drop(bell);
        for (float t = 0; t < 0.6f && dark != null; t += Time.deltaTime)
        {
            dark.color = new Color(0, 0, 0, 0.55f * (1f - t / 0.6f));
            yield return null;
        }
        Drop(dark);
        ultimate = false;
        // Spent: he sags, and the soul-fire in his chest gutters open.
        exposedUntil = Time.time + ExposedSeconds + 0.6f;
        PlayAction(art.hit);
        yield return Tremble(0.6f, 0.08f);
        yield return Wait(ExposedSeconds);
    }

    const float ColumnSeconds = 0.25f;

    // A bell hung across the top for the ultimate: the art on its cord, or a bronze glow without it.
    SpriteRenderer HangBell(Vector3 at)
    {
        Sprite[] frames = Fx.Frames(Fx.KageBell);
        if (frames == null) return Glowing(at + Vector3.down * 0.6f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.35f), 1.2f, 57);
        var bell = new GameObject("Bell").AddComponent<SpriteRenderer>();
        bell.sprite = frames[0];
        bell.sortingOrder = 57;
        bell.transform.position = at;
        bell.transform.localScale = Vector3.one * (1.3f / frames[0].bounds.size.y);
        bell.color = BellColour(bell, 0.5f);
        transient.Add(bell.gameObject);
        return bell;
    }

    Color BellColour(SpriteRenderer bell, float alpha) =>
        bell.sprite == ProceduralSprites.Glow ? new Color(Bronze.r, Bronze.g, Bronze.b, alpha * 0.9f) : new Color(1f, 1f, 1f, alpha);

    // A rung bell swings on its cord and settles.
    IEnumerator Swing(SpriteRenderer bell)
    {
        for (float t = 0; t < 0.6f && bell != null; t += Time.deltaTime)
        {
            bell.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 22f) * 18f * (1f - t / 0.6f));
            yield return null;
        }
        if (bell != null) bell.transform.rotation = Quaternion.identity;
    }

    // A full-screen dark under the action, for his ultimate.
    SpriteRenderer Darkness()
    {
        var dark = new GameObject("Darkness").AddComponent<SpriteRenderer>();
        dark.sprite = ProceduralSprites.White;
        dark.color = Color.clear;
        dark.sortingOrder = 1;
        Camera cam = Camera.main;
        dark.transform.position = new Vector3(CameraShake.Center.x, CameraShake.Center.y, 0.5f);
        dark.transform.localScale = new Vector3(cam.orthographicSize * cam.aspect * 2.4f, cam.orthographicSize * 2.4f, 1f);
        transient.Add(dark.gameObject);
        return dark;
    }
}
