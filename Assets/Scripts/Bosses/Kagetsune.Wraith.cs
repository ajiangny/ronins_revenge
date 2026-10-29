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
        Bark(BossLine.SoulRiver, 7f);
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
            bool bellRow = row % 4 == 3;
            BulletHell.Curtain(Skull, TopOfScreen, FieldLeft, FieldRight, Spacing, new[] { gap }, Width, Speed,
                i => bellRow && Mathf.Abs(left + i * Spacing - bank) < Spacing / 2f ? BellShot : null);
            if (row % 2 == 0) Toll(0.3f, 1.1f + 0.03f * row);
            yield return Wait(Between);
        }
    }

    // Broken Chains: the snapped chains lash from his shoulders, each sweeping a stretch of the ground. Before each
    // lash the chain hangs taut over where it starts and the stretch glows crimson, chevrons running the way it
    // will sweep; the tell is long enough to step out of the stretch at either end.
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
            float x0 = ronin.Bounds.center.x;
            float escape = Mathf.Min(Mathf.Abs(x0 - a), Mathf.Abs(x0 - b)) + 0.6f;
            float tell = Mathf.Max(0.65f, escape / BulletHell.MaxDodgeSpeed + 0.3f);
            var chain = Tiled(Fx.KageChain, root, new Vector3(a, GroundY, 0), 0.5f, Bronze, 56);
            yield return LashTell(chain, side, a, b, tell);

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

    // A lash's tell: the chain hangs taut and trembling from his shoulder over the start of the stretch, and the
    // stretch glows crimson under a row of chevrons that ripple toward its end.
    IEnumerator LashTell(SpriteRenderer chain, float side, float a, float b, float seconds)
    {
        float band = GroundY + 0.9f;
        float dir = Mathf.Sign(b - a);
        SpriteRenderer span = Bar(new Vector3(a, band, 0), new Vector3(b, band, 0), 2.4f, new Color(Crimson.r, Crimson.g, Crimson.b, 0f), 2);
        var chevrons = new List<SpriteRenderer>();
        for (float d = 0.8f; d < Mathf.Abs(b - a) - 0.4f; d += 1.3f)
        {
            Vector3 tip = new Vector3(a + dir * d, band, 0);
            for (int arm = -1; arm <= 1; arm += 2)
            {
                chevrons.Add(Bar(tip, tip + new Vector3(-dir * 0.35f, arm * 0.35f, 0), 0.1f, Color.clear, 3));
            }
        }
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            Vector3 shake = new Vector3(Mathf.Sin(t * 50f) * 0.1f, 0, 0);
            Lay(chain, Shoulder(side), new Vector3(a, GroundY, 0) + shake, chain.drawMode == SpriteDrawMode.Tiled ? 0.5f : 0.22f);
            span.color = new Color(Crimson.r, Crimson.g, Crimson.b, 0.1f + 0.3f * k);
            for (int i = 0; i < chevrons.Count; i++)
            {
                float wave = Mathf.Max(0f, Mathf.Sin(t * 10f - (i / 2) * 0.9f));
                chevrons[i].color = new Color(1f, 0.6f, 0.5f, (0.25f + 0.6f * wave) * Mathf.Min(1f, k * 4f));
            }
            yield return null;
        }
        Drop(span);
        foreach (SpriteRenderer chevron in chevrons) Drop(chevron);
    }

    // Chain Cage: both chains slam into the ground either side of the ronin and stay there as walls, and while he's
    // caged a two-arm spiral of skulls winds down on him. Touching a chain burns.
    IEnumerator ChainCage()
    {
        const float HalfWidth = 3f;
        const float Hold = 3f;
        float centre = Mathf.Clamp(ronin.Bounds.center.x, FieldLeft + HalfWidth + 0.5f, FieldRight - HalfWidth - 0.5f);
        float[] xs = { centre - HalfWidth, centre + HalfWidth };
        // The tell: each chain hangs faint over where it will land, trembling, over a bronze glow on the ground.
        var chains = new List<SpriteRenderer>();
        var marks = new List<SpriteRenderer>();
        for (int i = 0; i < 2; i++)
        {
            chains.Add(Tiled(Fx.KageChain, Shoulder(i == 0 ? -1f : 1f), new Vector3(xs[i], GroundY, 0), 0.5f, Bronze, 56));
            marks.Add(Glowing(new Vector3(xs[i], GroundY + 0.1f, 0), new Color(Bronze.r, Bronze.g, Bronze.b, 0f), 1.2f, 5));
        }
        for (float t = 0; t < 0.6f; t += Time.deltaTime * Tempo)
        {
            float k = t / 0.6f;
            for (int i = 0; i < 2; i++)
            {
                Vector3 shake = new Vector3(Mathf.Sin(t * 50f + i) * 0.08f, 0, 0);
                Lay(chains[i], Shoulder(i == 0 ? -1f : 1f), new Vector3(xs[i], GroundY, 0) + shake, chains[i].drawMode == SpriteDrawMode.Tiled ? 0.5f : 0.22f);
                chains[i].color = ChainColour(chains[i], 0.25f + 0.25f * k);
                marks[i].color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.3f + 0.4f * k);
                marks[i].transform.localScale = new Vector3(Mathf.Lerp(1.2f, 2f, k), 0.7f, 1f);
            }
            yield return null;
        }
        foreach (SpriteRenderer mark in marks) Drop(mark);

        var glows = new List<SpriteRenderer>();
        for (int i = 0; i < 2; i++)
        {
            chains[i].color = ChainColour(chains[i], 1f);
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

    // Coffin Pendulum: he swings the coffin-hammer out on its broken chain like a pendulum, each pass scything the
    // ground down one side of the field, the other side then the first, each faster than the last. The last pass
    // sheds skulls that fall where it passed. The far side is always clear, and each tell lasts long enough to cross to it;
    // the last pass he brings the coffin round overhead and down on the ronin's lane.
    IEnumerator CoffinPendulum()
    {
        Bark(BossLine.Pendulum, 7f);
        PlayAction(art.attack);
        float mid = CameraShake.Center.x;
        float side = ronin.Bounds.center.x < mid ? -1f : 1f;
        SpriteRenderer coffin = CoffinOnChain(HammerHead);
        SpriteRenderer chain = Tiled(Fx.KageChain, HammerHead, HammerHead, 0.4f, Bronze, 54);
        const int Passes = 3;
        for (int pass = 0; pass < Passes; pass++)
        {
            // The stretch this pass scythes: from just past the middle out to the edge, on one side.
            float near = mid + side * 1.2f, far = side > 0 ? FieldRight : FieldLeft;
            float lo = Mathf.Min(near, far), hi = Mathf.Max(near, far);
            SpriteRenderer band = Bar(new Vector3(lo, Row, 0), new Vector3(hi, Row, 0), 1.6f, new Color(Crimson.r, Crimson.g, Crimson.b, 0f), 4);
            // Long enough to cross to safety from wherever he stands.
            float need = 0f;
            float x0 = ronin.Bounds.center.x;
            if (x0 >= lo - 0.6f && x0 <= hi + 0.6f) need = Mathf.Abs(x0 - (near - side * 1.6f)) / BulletHell.MaxDodgeSpeed;
            float tell = Mathf.Max(Mathf.Lerp(1.1f, 0.75f, pass / (Passes - 1f)), need + 0.3f);
            // The wind-up: the coffin swings back out to his other side, low and in plain view, the ground band
            // glowing in.
            Vector3 windFrom = coffin != null ? coffin.transform.position : HammerHead;
            Vector3 start = new Vector3(Centre.x - side * 4.5f, Centre.y - Extents.y * 0.2f, 0);
            for (float t = 0; t < tell; t += Time.deltaTime * Tempo)
            {
                float k = t / tell;
                Vector3 winding = Vector3.Lerp(windFrom, start, Mathf.SmoothStep(0, 1, k)) + Vector3.up * Mathf.Sin(t * 9f) * 0.15f;
                SetCoffin(coffin, chain, winding, HangTilt(winding));
                if (band != null) band.color = new Color(Crimson.r, Crimson.g, Crimson.b, 0.1f + 0.35f * k);
                if (k > 0.5f && Mathf.Repeat(t, 0.25f) < Time.deltaTime) Toll(0.3f, 0.9f + 0.1f * pass);
                yield return null;
            }
            Drop(band);
            if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.8f, 0.6f + 0.1f * pass);
            else Sfx.Play(Sfx.ParrySwing, 0.9f, 0.5f);
            // The swing: down out of the wind-up, along the ground through the stretch, and up past its end.
            float swing = 0.55f * Mathf.Pow(0.8f, pass);
            // Through the stretch it swings in a shallow arc, lowest in the middle, as a weight on a chain would.
            Vector3 dip = new Vector3(near, Row + 1.2f, 0), outer = new Vector3(far, Row + 1.2f, 0);
            Vector3 lift = new Vector3(far + side * 1.5f, Row + 4f, 0);
            bool shed = false;
            float nextGhost = 0f, lastDust = near;
            for (float t = 0; t < swing; t += Time.deltaTime * Tempo)
            {
                float k = t / swing;
                Vector3 at = k < 0.3f ? Vector3.Lerp(start, dip, Mathf.SmoothStep(0, 1, k / 0.3f))
                    : k < 0.8f ? ScytheArc(dip, outer, (k - 0.3f) / 0.5f)
                    : Vector3.Lerp(outer, lift, (k - 0.8f) / 0.2f);
                SetCoffin(coffin, chain, at, HangTilt(at));
                // A few faint ghosts of the coffin mark its sweep, not a solid smear.
                if (coffin != null && Time.time >= nextGhost)
                {
                    nextGhost = Time.time + 0.05f;
                    Afterimage(coffin, new Color(Teal.r, Teal.g, Teal.b, 0.22f));
                }
                // Along the ground it burns whatever it passes.
                if (k >= 0.3f && k < 0.8f)
                {
                    if (Mathf.Abs(ronin.Bounds.center.x - at.x) < 1f) ronin.TakeHit(new Vector3(at.x, Row, 0));
                    BeamEnd(at + Vector3.up * 2f, at, out Blocker cover);
                    if (cover != null) cover.Hit(at);
                    // Where it scrapes past, dust kicks up and the ground catches soul-fire, every step or so.
                    if (Mathf.Abs(at.x - lastDust) >= 1.4f)
                    {
                        lastDust = at.x;
                        Fx.Play(Fx.SmokePuff, new Vector3(at.x, GroundY + 0.2f, 0), new Color(0.3f, 0.32f, 0.33f, 0.6f), 1.3f);
                        GroundFire.Scorch(new Vector3(at.x, GroundY, 0), soul: true);
                    }
                }
                // On the last upswing it sheds a row of skulls over the stretch it scythed, spaced wide enough to pass
                // between; earlier passes don't, as he has to cross back through that side.
                if (k >= 0.8f && !shed && pass == Passes - 1)
                {
                    shed = true;
                    for (float x = lo + Random.Range(0.3f, 1.5f); x <= hi; x += 3f) Skull(new Vector3(x, at.y + 1f, 0), Vector3.down * 3.2f);
                }
                yield return null;
            }
            CameraShake.Add(0.25f);
            side = -side;
            yield return Wait(0.1f);
        }
        // The last swing comes round overhead and down on him.
        var xs = new List<float> { ClampX(ronin.Bounds.center.x) };
        var lane = new List<SpriteRenderer> { Bar(new Vector3(xs[0], TopOfScreen, 0), new Vector3(xs[0], GroundY, 0), 2.4f, new Color(Crimson.r, Crimson.g, Crimson.b, 0.12f)) };
        Vector3 over = new Vector3(xs[0], TopOfScreen - 1.5f, 0);
        Vector3 from = coffin != null ? coffin.transform.position : HammerHead;
        for (float t = 0; t < 0.7f; t += Time.deltaTime * Tempo)
        {
            float k = t / 0.7f;
            Vector3 rising = Vector3.Lerp(from, over, Mathf.SmoothStep(0, 1, k));
            SetCoffin(coffin, chain, rising, HangTilt(rising));
            yield return null;
        }
        yield return CloseLanes(lane, xs, 2.4f, 1.5f, Crimson, 0.45f, 2);
        Drop(lane[0]);
        Drop(coffin);
        Drop(chain);
        yield return SlamDown(xs, true, 1.1f);
        yield return Wait(0.4f);
    }

    // The coffin-hammer's head as a swung weight: its art, or a dark slab without it.
    SpriteRenderer CoffinOnChain(Vector3 at)
    {
        var coffin = new GameObject("Coffin").AddComponent<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.KageCoffin);
        coffin.sprite = frames != null ? frames[0] : ProceduralSprites.White;
        coffin.color = frames != null ? Color.white : new Color(0.22f, 0.2f, 0.22f);
        coffin.sortingOrder = 57;
        coffin.transform.position = at;
        coffin.transform.localScale = frames != null ? Vector3.one * (2.2f / frames[0].bounds.size.y) : new Vector3(1.2f, 2f, 1f);
        transient.Add(coffin.gameObject);
        return coffin;
    }

    // The swing's path through the stretch: a shallow arc between its ends, a little lower in the middle.
    Vector3 ScytheArc(Vector3 from, Vector3 to, float k)
    {
        Vector3 at = Vector3.Lerp(from, to, k);
        at.y -= Mathf.Sin(k * Mathf.PI) * 1f;
        return at;
    }

    // The coffin hangs off the end of its chain: its long axis points along the chain, away from the hammer.
    float HangTilt(Vector3 at)
    {
        Vector3 pull = at - HammerHead;
        return pull.sqrMagnitude < 0.01f ? 0f : Vector3.SignedAngle(Vector3.down, pull, Vector3.forward);
    }

    void SetCoffin(SpriteRenderer coffin, SpriteRenderer chain, Vector3 at, float tilt)
    {
        if (coffin != null)
        {
            coffin.transform.position = at + Vector3.back * 0.3f;
            coffin.transform.rotation = Quaternion.Euler(0, 0, tilt);
        }
        if (chain != null) Lay(chain, HammerHead, at, chain.drawMode == SpriteDrawMode.Tiled ? 0.4f : 0.12f);
    }

    // Soul Harvest: he calls the souls home. They well up out of the ground in rows, a glow marking each before it
    // rises, and stream up into his chest, every row with one gap a few steps from the last. The souls cross the
    // ronin's row from below, the one direction nothing else comes from. The last row in, his chest-fire swells.
    IEnumerator SoulHarvest()
    {
        Bark(BossLine.Harvest, 7f);
        PlayAction(art.parry);
        const int Rows = 5;
        const float Spacing = 1.5f;
        const float Width = 3f;
        const float Between = 0.65f;
        float lo = FieldLeft + Width / 2f, hi = FieldRight - Width / 2f;
        float gap = Mathf.Clamp(ronin.Bounds.center.x + (Random.value < 0.5f ? -1f : 1f) * Random.Range(2f, 4f), lo, hi);
        for (int row = 0; row < Rows; row++)
        {
            yield return BulletHell.Room(20);
            var spots = new List<Vector3>();
            for (float x = FieldLeft; x <= FieldRight; x += Spacing)
            {
                if (Mathf.Abs(x - gap) < Width / 2f) continue;
                spots.Add(new Vector3(x, GroundY - 0.4f, 0));
            }
            // The souls gather under the floor: a glow over each spot, so the gap reads before anything rises.
            var glows = new List<SpriteRenderer>();
            foreach (Vector3 spot in spots) glows.Add(Glowing(spot + Vector3.up * 0.4f, new Color(Teal.r, Teal.g, Teal.b, 0f), 0.9f, 6));
            for (float t = 0; t < 0.55f; t += Time.deltaTime * Tempo)
            {
                float k = t / 0.55f;
                foreach (SpriteRenderer glow in glows)
                {
                    if (glow == null) continue;
                    glow.color = new Color(Teal.r, Teal.g, Teal.b, 0.6f * k);
                    glow.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, k);
                }
                yield return null;
            }
            foreach (SpriteRenderer glow in glows) Drop(glow);
            Toll(0.35f, 1.2f + 0.05f * row);
            foreach (Vector3 spot in spots)
            {
                // Each rises straight up through his row, then bends toward the chest; a straight aim at the chest
                // would slant across the row and close the gap.
                Arrow skull = Skull(spot, Vector3.up * 5f);
                if (skull != null) StartCoroutine(HomeToChest(skull, spot.y + 3f));
            }
            yield return Wait(Between);
            float want = gap + (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.5f, 3.5f);
            float next = BulletHell.NextGap(gap, Mathf.Clamp(want, lo, hi), Between + 0.55f, lo, hi);
            BulletHell.GapOk(gap, next, Between + 0.55f, Width, "Soul Harvest");
            gap = next;
        }
        yield return Wait(0.6f);
        // Fed, the chest-fire flares and spits a last slow ring.
        ElementFx.Glow(Chest, Teal, 2f, 12f, 0.7f);
        Fx.Play(Fx.Explosion, Chest, Teal, 2f);
        HitFlash.Flash(sprite, 0.12f);
        TimeRipple.Emit(Chest, 5f, 0.4f, 0.6f, true);
        BulletHell.Ring(Skull, Chest, 14, 3f, Random.Range(0f, 25f));
        yield return Wait(0.6f);
    }

    // A rising soul turns toward his chest once it's above the ronin's row, and is gone when it gets there.
    IEnumerator HomeToChest(Arrow skull, float turnAt)
    {
        while (skull != null && skull.transform.position.y < turnAt) yield return new WaitForFixedUpdate();
        while (skull != null)
        {
            Vector3 to = Chest - skull.transform.position;
            if (to.sqrMagnitude < 0.8f)
            {
                Destroy(skull.gameObject);
                yield break;
            }
            skull.Steer(to.normalized * 6.5f * ShotSpeed);
            yield return new WaitForFixedUpdate();
        }
    }

    // Soul Coffin, parry-only: he hurls the coffin-hammer down on its broken chain. Parried, it bursts against his
    // own armor, and he stands open.
    IEnumerator SoulCoffin()
    {
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
        StartCoroutine(Streak(coffin, new Color(Teal.r, Teal.g, Teal.b, 0.4f)));
        HitFlash.Flash(sprite, 0.08f);
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
        // The soul beside him fights on through his stagger; only his own chain cage stops.
        StopCage();
        // A slam cut off mid-wind-up leaves his art reared up; put it back.
        ResetView();
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
        // Under the Last Bell one wave's safe lane leaps two lanes instead of one; its tell runs longer to match.
        bool leapt = false;
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
            float tell = wave == 0 ? 0.9f : leapt ? 0.7f + halfWidth * 2f / BulletHell.MaxDodgeSpeed : 0.7f;
            leapt = false;
            // The lane to stand in glows pale on the ground, the same way the beam marks its safe strip.
            float strip = GroundY + 0.9f;
            SpriteRenderer haven = Bar(new Vector3(lanes[safe] - halfWidth, strip, 0), new Vector3(lanes[safe] + halfWidth, strip, 0), 2.4f, new Color(HyperSafeColour.r, HyperSafeColour.g, HyperSafeColour.b, 0.24f), 2);
            yield return CloseLanes(bands, xs, halfWidth * 1.6f, halfWidth * 0.5f, Teal, tell, 2, 0.2f);
            foreach (SpriteRenderer band in bands) Drop(band);
            StartCoroutine(FadeOut(haven, 0.3f));

            Toll(1f, 0.7f + 0.05f * wave);
            CameraShake.Add(0.3f);
            TimeRipple.Emit(new Vector3(CameraShake.Center.x, TopOfScreen - 1f, 0), 10f, 0.4f, 0.5f);
            for (int i = 0; i < LaneCount; i++) if (i != safe && bells[i] != null) TollRing(bells[i].transform.position + Vector3.down * HangingBellHeight * 0.55f, 3.6f, 0.7f);
            for (int i = 0; i < LaneCount; i++) if (i != safe && bells[i] != null) StartCoroutine(Swing(bells[i]));
            var columns = new List<SpriteRenderer>();
            foreach (float x in xs)
            {
                // A soul-fire column slamming down the lane in a soft glow (a white-hot bar without its art).
                Vector3 top = new Vector3(x, TopOfScreen, 0), ground = new Vector3(x, GroundY, 0);
                SpriteRenderer halo = Bar(top, ground, halfWidth * 2.2f, new Color(Teal.r, Teal.g, Teal.b, 0.3f), 55);
                halo.sprite = ProceduralSprites.Glow;
                columns.Add(halo);
                if (SoulColumn(top, ground, halfWidth * 1.8f) == null) columns.Add(Bar(top, ground, halfWidth * 0.6f, new Color(0.85f, 1f, 0.96f, 0.8f), 56));
                GroundFire.Scorch(new Vector3(x, GroundY, 0), soul: true);
            }
            // The columns burn for their whole flash, not a single frame.
            for (float t = 0; t < ColumnSeconds; t += Time.deltaTime * Tempo)
            {
                if (Mathf.Abs(ronin.Bounds.center.x - lanes[safe]) > halfWidth * 0.95f) ronin.TakeHit(ronin.Bounds.center);
                yield return null;
            }
            foreach (SpriteRenderer column in columns) StartCoroutine(FadeOut(column, 0.35f));
            yield return Wait(0.3f - ColumnSeconds);
            // The safe lane steps one over (two on the Last Bell's leap), never off the field.
            int stride = lastBell && wave == 2 ? 2 : 1;
            int stepTo = safe + (Random.value < 0.5f ? -stride : stride);
            if (stepTo < 0 || stepTo >= LaneCount) stepTo = safe - (stepTo - safe);
            leapt = stride == 2;
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

    // How tall the ultimate's hanging bells are drawn: big enough to read as bells from the bottom of the field.
    const float HangingBellHeight = 2.3f;

    // A bell hung across the top for the ultimate: the art on its cord, or a bronze glow without it.
    SpriteRenderer HangBell(Vector3 at)
    {
        Sprite[] frames = Fx.Frames(Fx.KageBell);
        if (frames == null) return Glowing(at + Vector3.down * HangingBellHeight * 0.5f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.35f), HangingBellHeight, 57);
        var bell = new GameObject("Bell").AddComponent<SpriteRenderer>();
        bell.sprite = frames[0];
        bell.sortingOrder = 57;
        bell.transform.position = at;
        bell.transform.localScale = Vector3.one * (HangingBellHeight / frames[0].bounds.size.y);
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
