using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Phase 1, the Thousand-Soul Throne: the Coffer Slam, the Toll of Fealty, the Rosary Curtain, the Ofuda Decree, the
// Thousand-Soul Wail and the parry-only Last Toll.
public partial class Kagetsune
{
    // --- Phase 1 ---

    // Coffer Slam: he heaves the coffin-hammer up over his shoulder and brings it down on the courtyard at his feet. The
    // ground splits: a fissure runs down the field to the ronin's row and races along it both ways, and soul-fire
    // columns erupt out of it in waves, each spot glowing before it bursts. The second wave fills the gaps the first
    // left, so he has to step into where a column has just burnt out. Cover over a column is split by it. Hardened, a
    // third wave erupts where the first did.
    IEnumerator CofferSlam()
    {
        Bark(BossLine.CofferSlam, 7f);
        float tell = secondPhase ? 0.75f : 0.95f;
        // The wind-up: the hammer rises on his shoulder and is held high while the coffin blazes, three tolls rising.
        SpriteRenderer raised = Glowing(HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.2f), 2f);
        RaiseHammer(tell);
        int rung = 0;
        for (float t = 0; t < tell; t += Time.deltaTime * Tempo)
        {
            float k = t / tell;
            raised.transform.position = HammerHead + Vector3.back * 0.1f;
            raised.transform.localScale = Vector3.one * Mathf.Lerp(2f, 4f, k);
            raised.color = new Color(Teal.r, Teal.g, Teal.b, 0.2f + 0.5f * k);
            if (k >= rung / 3f && rung < 3)
            {
                rung++;
                Toll(0.35f, 0.8f + 0.12f * rung);
            }
            yield return null;
        }
        Drop(raised);

        // The blow, on the frame the hammer meets the ground.
        StrikeHammer();
        Vector3 impact = new Vector3(Centre.x + Extents.x * 0.2f, sprite.bounds.min.y + Extents.y * 0.32f, 0);
        Slam(1f);
        CameraShake.Add(0.6f);
        HitStop?.Invoke(0.05f);
        Fx.Play(Fx.Impact, impact, Teal, 2.6f);
        Shockwave(impact, 4f);
        TimeRipple.Emit(impact, 5f, 0.5f, 0.5f);
        Crack(impact);
        Debris.BurstSprites(impact, art.deathParticles, Bronze, 14, 0.9f, 8f);

        // The fissure runs down the field to the ronin's row, then splits along it both ways.
        float ground = GroundY + 0.25f;
        Vector3 fork = new Vector3(impact.x, ground, 0);
        yield return Fissure(impact, fork, FissureSpeed);
        StartCoroutine(Fissure(fork, new Vector3(FieldLeft, ground, 0), FissureSpeed));
        StartCoroutine(Fissure(fork, new Vector3(FieldRight, ground, 0), FissureSpeed));

        // The columns: wave A on the fork and every Spacing out from it, wave B halfway between, then (hardened) A again.
        const float Spacing = 4f;
        int waves = Hardened ? 3 : 2;
        for (int wave = 0; wave < waves; wave++)
        {
            float offset = wave % 2 == 0 ? 0f : Spacing / 2f;
            var spots = new List<float>();
            for (float d = offset; fork.x - d >= FieldLeft || fork.x + d <= FieldRight; d += Spacing)
            {
                if (fork.x + d <= FieldRight) spots.Add(fork.x + d);
                if (d > 0f && fork.x - d >= FieldLeft) spots.Add(fork.x - d);
            }
            // Each erupts a fixed tell after the fissure reaches it, so the wave rolls out from the fork.
            foreach (float x in spots) StartCoroutine(EruptColumn(new Vector3(x, ground, 0), Mathf.Abs(x - fork.x) / FissureSpeed));
            yield return Wait(ColumnTell + (secondPhase ? 0.25f : 0.35f));
        }
        yield return Wait(0.4f);
    }

    // How fast a fissure splits the ground, how long a column's spot glows before it erupts, and how it burns.
    const float FissureSpeed = 16f;
    const float ColumnTell = 0.55f;
    const float EruptSeconds = 0.3f;
    const float EruptHeight = 6f;
    const float EruptWidth = 1.4f;

    // One column out of the fissure: its spot glows and swells for the tell, then soul-fire bursts up from it,
    // burning the ronin and any cover in it for a moment. Once the fissure is open it runs in real time: a stagger
    // doesn't hold columns already glowing.
    IEnumerator EruptColumn(Vector3 spot, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsDead) yield break;
        SpriteRenderer mark = Glowing(spot + Vector3.up * 0.2f, new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 4);
        Detach(mark.gameObject);
        for (float t = 0; t < ColumnTell && mark != null; t += Time.deltaTime)
        {
            float k = t / ColumnTell;
            mark.transform.localScale = new Vector3(Mathf.Lerp(1f, EruptWidth * 1.8f, k), Mathf.Lerp(0.4f, 0.9f, k), 1f);
            mark.color = new Color(Teal.r, Teal.g, Teal.b, 0.15f + 0.6f * k * k);
            yield return null;
        }
        if (mark != null) Destroy(mark.gameObject);
        if (IsDead) yield break;
        Vector3 top = spot + Vector3.up * EruptHeight;
        if (SoulColumn(top, spot, EruptWidth) == null)
        {
            SpriteRenderer bar = Bar(spot, top, EruptWidth * 0.5f, new Color(0.85f, 1f, 0.96f, 0.9f), 56);
            Detach(bar.gameObject);
            Fader.Fade(bar, EruptSeconds + 0.2f);
        }
        Fx.Play(Fx.SmokePuff, spot, new Color(0.25f, 0.27f, 0.28f, 0.7f), 1.4f);
        if (Sfx.Has(Sfx.SoulWail)) Sfx.Play(Sfx.SoulWail, 0.25f, 1.6f);
        else Sfx.Play(Sfx.LanternBeam, 0.25f, 1.4f);
        BeamEnd(top, spot, out Blocker cover);
        if (cover != null) cover.Hit(spot);
        for (float t = 0; t < EruptSeconds; t += Time.deltaTime)
        {
            if (ronin != null && Mathf.Abs(ronin.Bounds.center.x - spot.x) < EruptWidth / 2f) ronin.TakeHit(new Vector3(spot.x, ronin.Bounds.center.y, 0));
            yield return null;
        }
        GroundFire.Light(spot);
    }

    // A fissure splitting the ground from one point to another at `speed`: a dark rift with soul-fire glowing up out of
    // it, jagged along its length, drawn as it goes and cooling over a few seconds. Only for show; not transient.
    IEnumerator Fissure(Vector3 from, Vector3 to, float speed)
    {
        float length = Vector3.Distance(from, to);
        if (length < 0.1f) yield break;
        Vector3 dir = (to - from) / length;
        Vector3 side = new Vector3(-dir.y, dir.x, 0);
        int segments = Mathf.Max(2, Mathf.CeilToInt(length / 1.2f));
        Vector3 last = from;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = from + dir * (length * i / segments) + (i < segments ? side * Random.Range(-0.35f, 0.35f) : Vector3.zero);
            // A seam of soul-fire light with the dark crack down its middle; each piece overlaps the next a little so
            // the seam reads as one line, not a row of dashes.
            Vector3 a = last - dir * 0.06f, b = next + dir * 0.06f;
            for (int layer = 0; layer < 2; layer++)
            {
                var rift = new GameObject("Fissure").AddComponent<SpriteRenderer>();
                rift.sprite = ProceduralSprites.White;
                rift.color = layer == 0 ? new Color(Teal.r, Teal.g, Teal.b, 0.75f) : new Color(0.04f, 0.03f, 0.05f, 0.95f);
                rift.sortingOrder = 3 + layer;
                Stretch(rift, a, b, layer == 0 ? 0.24f : 0.09f);
                rift.transform.position += Vector3.forward * 0.5f;
                Fader.Fade(rift, 3.5f);
            }
            // Soul-fire glows up out of it here and there.
            if (Random.value < 0.4f) ElementFx.Glow(next, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 0.3f, 1.4f, 0.6f);
            last = next;
            yield return new WaitForSeconds(length / segments / speed);
        }
    }

    // His slam on his own art, timed to the blow: the wind-up (everything before the impact frame) spread over the
    // tell so the hammer is held high at its end, and StrikeHammer plays the impact and follow-through. Without enough
    // frames the whole clip plays at the blow.
    int ImpactFrame => art.attack != null && art.attack.Length >= 4 ? Mathf.RoundToInt(art.attack.Length * 0.625f) : -1;

    void RaiseHammer(float seconds)
    {
        int impact = ImpactFrame;
        if (impact <= 0 || IsDead) return;
        var windup = new Sprite[impact];
        System.Array.Copy(art.attack, windup, impact);
        // A little quicker than the tell, so the hammer hangs at its peak before it falls.
        animator.Play(windup, false, null, impact / (seconds * 0.8f));
    }

    void StrikeHammer()
    {
        int impact = ImpactFrame;
        if (IsDead) return;
        if (impact <= 0)
        {
            PlayAction(art.attack);
            return;
        }
        var blow = new Sprite[art.attack.Length - impact];
        System.Array.Copy(art.attack, impact, blow, 0, blow.Length);
        animator.Play(blow, false, PlayIdle, art.actionFps * 0.8f);
    }

    // The hammer comes down on the given lanes: a soul-fire shockwave runs down each to the ground (cover takes it
    // and breaks), and the ground cracks where it lands. With `ring`, skulls burst from each crack.
    IEnumerator SlamDown(List<float> xs, bool ring, float weight = 1f)
    {
        Slam(weight);
        CameraShake.Add(0.55f * weight);
        HitFlash.Flash(sprite, 0.08f);
        var waves = new List<SpriteRenderer>();
        var ends = new List<Vector3>();
        foreach (float x in xs)
        {
            Vector3 top = new Vector3(x, anchor.y - Extents.y * 0.5f, 0);
            Vector3 end = BeamEnd(top, new Vector3(x, GroundY, 0), out Blocker cover);
            ends.Add(end);
            // A soft soul-fire glow runs down the lane while the column itself slams onto the ground; without the
            // column's art a white-hot bar runs down inside the glow instead.
            SpriteRenderer halo = Bar(top, top, 1f, new Color(Teal.r, Teal.g, Teal.b, 0.3f), 55);
            halo.sprite = ProceduralSprites.Glow;
            waves.Add(halo);
            bool column = SoulColumn(top, end, 1.9f) != null;
            waves.Add(column ? null : Bar(top, top, 0.5f, new Color(0.9f, 1f, 0.96f, 0.9f), 56));
            if (cover != null) StartCoroutine(BreakAfter(cover, 0.18f));
        }
        bool hit = false;
        for (float t = 0; t < 0.22f; t += Time.deltaTime * Tempo)
        {
            float k = t / 0.22f;
            for (int i = 0; i < xs.Count; i++)
            {
                Vector3 top = new Vector3(xs[i], anchor.y - Extents.y * 0.5f, 0);
                Vector3 tip = Vector3.Lerp(top, ends[i], k);
                Stretch(waves[2 * i], top, tip, 2.6f);
                Stretch(waves[2 * i + 1], top, tip, 0.7f * (1f - 0.4f * k));
                if (!hit && Mathf.Abs(ronin.Bounds.center.x - xs[i]) < 0.95f && tip.y <= ronin.Bounds.center.y + 0.3f)
                {
                    hit = true;
                    ronin.TakeHit(new Vector3(xs[i], ronin.Bounds.center.y, 0));
                }
            }
            yield return null;
        }
        HitStop?.Invoke(0.04f * weight);
        foreach (Vector3 end in ends)
        {
            Fx.Play(Fx.Impact, end, Teal, 2.2f);
            Fx.Play(Fx.SmokePuff, end, new Color(0.25f, 0.25f, 0.28f, 0.8f), 2f);
            ElementFx.Glow(end, new Color(Teal.r, Teal.g, Teal.b, 0.6f), 0.5f, 7f, 0.4f);
            TimeRipple.Emit(end, 3.5f * weight, 0.35f * weight, 0.45f);
            Crack(end);
            Shockwave(end, 3.2f);
            GroundFire.Light(end);
            Debris.BurstSprites(end, art.deathParticles, Bronze, 10, 0.8f, 7f);
        }
        foreach (SpriteRenderer wave in waves) StartCoroutine(FadeOut(wave, 0.3f));
        yield return Wait(0.15f);
        if (ring) foreach (Vector3 end in ends) SlamRing(end);
    }

    // Reliquary Rend: three blows of the hammer. The first two come quick, each on the lane the ronin stands in; the
    // third he holds overhead for a breath that's never the same length twice, the coffin blazing brighter the longer
    // he holds, so it can't be timed by rhythm. A beat after it lands, the souls the first blow loosed erupt from its
    // crack again.
    IEnumerator ReliquaryRend()
    {
        Bark(BossLine.Rend, 7f);
        float tell = secondPhase ? 0.45f : 0.55f;
        float first = 0f;
        for (int blow = 0; blow < 3; blow++)
        {
            var xs = new List<float> { ClampX(ronin.Bounds.center.x) };
            if (blow == 0) first = xs[0];
            var bands = new List<SpriteRenderer> { Bar(new Vector3(xs[0], TopOfScreen, 0), new Vector3(xs[0], GroundY, 0), 2.4f, new Color(Crimson.r, Crimson.g, Crimson.b, 0.12f)) };
            PlayAction(art.attack);
            yield return CloseLanes(bands, xs, 2.4f, 1.5f, Crimson, tell, 2);
            if (blow == 2)
            {
                // The held blow: the mark stays closed and burning while the hammer gathers.
                SpriteRenderer raised = Glowing(HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.3f), 2f);
                float hold = Random.Range(0.4f, 1f);
                for (float t = 0; t < hold; t += Time.deltaTime * Tempo)
                {
                    float k = t / hold;
                    raised.transform.localScale = Vector3.one * Mathf.Lerp(2f, 4.5f, k);
                    raised.color = new Color(Teal.r, Teal.g, Teal.b, 0.3f + 0.5f * k);
                    if (bands[0] != null) bands[0].color = new Color(Crimson.r, Crimson.g, Crimson.b, 0.5f + 0.15f * Mathf.Sin(t * 40f));
                    yield return null;
                }
                Drop(raised);
            }
            Drop(bands[0]);
            yield return SlamDown(xs, blow == 2, blow == 2 ? 1.2f : 0.75f);
            yield return Wait(blow == 2 ? 0.1f : 0.15f);
        }
        // The aftershock: the first crack glows, then erupts.
        var shock = new List<float> { first };
        var mark = new List<SpriteRenderer> { Bar(new Vector3(first, TopOfScreen, 0), new Vector3(first, GroundY, 0), 2.2f, new Color(Teal.r, Teal.g, Teal.b, 0.12f)) };
        yield return CloseLanes(mark, shock, 2.2f, 1.4f, Teal, 0.6f, 2, 0.35f);
        Drop(mark[0]);
        yield return SlamDown(shock, false, 0.8f);
        yield return Wait(0.4f);
    }

    // A crack left in the courtyard where the hammer landed: dark rifts glowing teal from below, cooling over a few
    // seconds. Only for show, and not transient, so it outlasts a stagger.
    static void Crack(Vector3 at)
    {
        int rifts = Random.Range(3, 5);
        for (int i = 0; i < rifts; i++)
        {
            float angle = Random.Range(-35f, 35f) + (i % 2 == 0 ? 0f : 180f);
            float length = Random.Range(0.9f, 1.8f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 mid = at + dir * (length / 2f) + Vector3.down * 0.1f;
            for (int layer = 0; layer < 2; layer++)
            {
                var rift = new GameObject("Crack").AddComponent<SpriteRenderer>();
                rift.sprite = layer == 0 ? ProceduralSprites.Glow : ProceduralSprites.White;
                rift.color = layer == 0 ? new Color(Teal.r, Teal.g, Teal.b, 0.45f) : new Color(0.04f, 0.03f, 0.05f, 0.9f);
                rift.sortingOrder = 3 + layer;
                rift.transform.position = mid + Vector3.forward * 0.3f;
                rift.transform.rotation = Quaternion.Euler(0, 0, angle);
                rift.transform.localScale = new Vector3(length, layer == 0 ? 0.5f : 0.08f, 1f);
                Fader.Fade(rift, 3f);
            }
        }
    }

    IEnumerator BreakAfter(Blocker cover, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (cover == null) yield break;
        cover.Demolish();
        Sfx.Play(Sfx.BlockerBreak, 0.8f, 0.8f);
    }

    IEnumerator FadeOut(SpriteRenderer thing, float seconds)
    {
        if (thing == null) yield break;
        Color from = thing.color;
        for (float t = 0; t < seconds && thing != null; t += Time.deltaTime)
        {
            thing.color = new Color(from.r, from.g, from.b, from.a * (1f - t / seconds));
            yield return null;
        }
        Drop(thing);
    }

    // Skulls burst from the crack in a slow ring a little above the ground; where it comes down round the crack it
    // closes off the ground nearby. Step clear of it, or parry through it.
    void SlamRing(Vector3 at)
    {
        int count = secondPhase ? 20 : Hardened ? 16 : 12;
        Vector3 from = new Vector3(at.x, Mathf.Max(at.y, Row) + 3.2f, 0);
        BulletHell.Ring(Skull, from, count, 3f, Random.Range(0f, 360f / count));
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.6f);
    }

    // Toll of Fealty: each toll winds a spiral of skulls out of the bells at his waist, turning the other way from the
    // last. Where the arms come down, their landings step sideways along the ronin's row a gap apart, so he reads
    // the shadows and steps between them. In phase 2 a small aimed fan rides each toll, so standing still won't do.
    IEnumerator TollOfFealty(int tolls)
    {
        Bark(BossLine.TollOfFealty, 7f);
        PlayAction(art.parry);
        for (int i = 0; i < tolls; i++)
        {
            SpriteRenderer bell = Glowing(Waist, new Color(Bronze.r, Bronze.g, Bronze.b, 0.7f), 1.6f, 56);
            yield return Wait(0.22f);
            Toll(0.75f, 1f - 0.05f * i);
            Fx.Play(Fx.Impact, Waist, Teal, 1.6f);
            TollRing(Waist, 5f);
            TimeRipple.Emit(Waist, 3f, 0.25f, 0.4f);
            Drop(bell);
            yield return BulletHell.Room(12);
            if (secondPhase) BulletHell.Fan(Skull, Waist, ronin.Bounds.center, 3, 10f, 6.5f);
            float spin = (i % 2 == 0 ? 1f : -1f) * (secondPhase ? 60f : 48f);
            yield return BulletHell.Spiral(Skull, () => Waist, Row, SpiralArms, spin, secondPhase ? 0.8f : 1f, 5.5f, 0.32f, Wait, Random.Range(0f, 360f));
            yield return Wait(secondPhase ? 0.15f : 0.25f);
        }
    }

    // Rosary Curtain: rows of skulls fall slowly from the top of the field, each with one gap a few steps from the
    // last: a staircase read off the shadows. Hardened, one row has no gap at all, only a bronze bell where the last
    // gap was: parry it, and its Resonance opens the way.
    IEnumerator RosaryCurtain()
    {
        Bark(BossLine.RosaryCurtain, 7f);
        PlayAction(art.parry);
        const int Rows = 4;
        const float Spacing = 1.5f;
        const float Speed = 4.5f;
        float between = secondPhase ? 0.6f : 0.75f;
        float width = secondPhase ? 2.8f : 3.2f;
        float lo = FieldLeft + width / 2f, hi = FieldRight - width / 2f;
        float side = Random.value < 0.5f ? -1f : 1f;
        float gap = Mathf.Clamp(ronin.Bounds.center.x + side * Random.Range(3f, 6f), lo, hi);
        // No gapless row while a seal holds: inside one he couldn't parry the bell.
        int bellRow = Hardened && !SealActive ? Random.Range(1, Rows - 1) : -1;
        for (int row = 0; row < Rows; row++)
        {
            yield return BulletHell.Room(26);
            Toll(0.45f, 0.9f + 0.06f * row);
            if (row == bellRow)
            {
                float bellAt = gap;
                float left = FieldLeft;
                BulletHell.Curtain(Skull, TopOfScreen, FieldLeft, FieldRight, Spacing, null, BulletHell.MinGap, Speed,
                    i => Mathf.Abs(left + i * Spacing - bellAt) < Spacing / 2f ? BellShot : null);
            }
            else BulletHell.Curtain(Skull, TopOfScreen, FieldLeft, FieldRight, Spacing, new[] { gap }, width, Speed);
            yield return Wait(between);
            float want = gap + (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.5f, 4f);
            float next = BulletHell.NextGap(gap, Mathf.Clamp(want, lo, hi), between, lo, hi);
            BulletHell.GapOk(gap, next, between, width, "Rosary Curtain");
            gap = next;
        }
    }

    // Ofuda Decree: talisman strips drift down and hang in two or three lanes, one always the ronin's. Inside a
    // sealed lane the law holds: no parry. Then a slam or a toll comes while he's caught in it.
    IEnumerator OfudaDecree()
    {
        Bark(BossLine.OfudaDecree, 7f);
        var lanes = Lanes(6);
        float halfWidth = (RightEdge - LeftEdge - 2.4f) / 12f;
        int mine = 0;
        for (int i = 1; i < lanes.Count; i++)
        {
            if (Mathf.Abs(lanes[i] - ronin.Bounds.center.x) < Mathf.Abs(lanes[mine] - ronin.Bounds.center.x)) mine = i;
        }
        var chosen = new List<float> { lanes[mine] };
        var others = new List<float>(lanes);
        others.RemoveAt(mine);
        for (int i = 0; i < (secondPhase ? 2 : 1); i++)
        {
            int pick = Random.Range(0, others.Count);
            chosen.Add(others[pick]);
            others.RemoveAt(pick);
        }
        if (Sfx.Has(Sfx.OfudaFlutter)) Sfx.Play(Sfx.OfudaFlutter, 0.8f);
        else Sfx.Play(Sfx.MistSwirl, 0.6f, 1.4f);

        // The strips fall in a loose row across each lane, swaying as they come.
        var strips = new List<(SpriteRenderer strip, float x, float sway)>();
        var curtains = new List<SpriteRenderer>();
        foreach (float x in chosen)
        {
            curtains.Add(Bar(new Vector3(x, TopOfScreen, 0), new Vector3(x, GroundY, 0), halfWidth * 2f, new Color(SealRed.r, SealRed.g, SealRed.b, 0f), 3));
            for (int s = 0; s < 3; s++)
            {
                float sx = x + Mathf.Lerp(-halfWidth * 0.7f, halfWidth * 0.7f, s / 2f);
                SpriteRenderer strip = Ofuda(new Vector3(sx, TopOfScreen + Random.Range(0f, 1.5f), 0));
                strips.Add((strip, sx, Random.value * 6f));
            }
        }
        float fall = 0.9f;
        float hang = ronin.Bounds.center.y + 1.6f;
        for (float t = 0; t < fall; t += Time.deltaTime * Tempo)
        {
            float k = Mathf.SmoothStep(0, 1, t / fall);
            foreach (var (strip, x, sway) in strips)
            {
                if (strip == null) continue;
                float y = Mathf.Lerp(TopOfScreen + 1f, hang + Mathf.Sin(sway) * 0.6f, k);
                strip.transform.position = new Vector3(x + Mathf.Sin(t * 4f + sway) * 0.25f, y, -0.2f);
                strip.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 5f + sway) * 12f);
            }
            foreach (SpriteRenderer curtain in curtains) curtain.color = new Color(SealRed.r, SealRed.g, SealRed.b, 0.08f * k);
            yield return null;
        }
        float seconds = secondPhase ? 5f : 4.5f;
        foreach (float x in chosen) seals.Add((x, halfWidth, Time.time + seconds));
        StartCoroutine(LiftSeals(strips.ConvertAll(s => s.strip), curtains, seconds));
        Fx.Play(Fx.Impact, new Vector3(lanes[mine], hang, 0), SealRed, 1.4f);

        // The law is laid down; now the blow that can't be parried inside it.
        yield return Wait(0.3f);
        float roll = Random.value;
        if (roll < (Hardened ? 0.5f : 0.34f)) yield return DecreeRain(chosen, halfWidth);
        else if (roll < 0.75f) yield return TollOfFealty(secondPhase ? 3 : 2);
        else yield return CofferSlam();
    }

    // The decree's sentence: crimson seal-darts rain everywhere the seals don't hang, while rows of skulls fall down
    // the sealed lanes. Inside a seal he can't parry the skulls; outside it no parry turns the darts. He has to
    // leave his lane and weave.
    IEnumerator DecreeRain(List<float> sealedLanes, float halfWidth)
    {
        const float DartSpacing = 2.7f;
        for (int wave = 0; wave < 3; wave++)
        {
            yield return BulletHell.Room(30);
            float offset = Random.Range(0f, DartSpacing);
            for (float x = FieldLeft + offset; x <= FieldRight; x += DartSpacing)
            {
                if (sealedLanes.Exists(lane => Mathf.Abs(x - lane) < halfWidth + 0.4f)) continue;
                SealDart(new Vector3(x, TopOfScreen, 0), Vector3.down * 6f);
            }
            foreach (float lane in sealedLanes)
            {
                BulletHell.Curtain(Skull, TopOfScreen, lane - halfWidth + 0.6f, lane + halfWidth - 0.6f, 1.5f, null, BulletHell.MinGap, 6f);
            }
            if (Sfx.Has(Sfx.OfudaFlutter)) Sfx.Play(Sfx.OfudaFlutter, 0.5f, 1.2f);
            yield return Wait(0.7f);
        }
    }

    SpriteRenderer Ofuda(Vector3 at)
    {
        var strip = new GameObject("Ofuda").AddComponent<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.KageOfuda);
        strip.sprite = frames != null ? frames[0] : ProceduralSprites.White;
        strip.color = frames != null ? Color.white : Paper;
        strip.sortingOrder = 45;
        strip.transform.position = at;
        strip.transform.localScale = frames != null ? Vector3.one * (1.3f / frames[0].bounds.size.y) : new Vector3(0.32f, 1.1f, 1f);
        if (frames == null)
        {
            // A red sigil down the paper.
            var sigil = new GameObject("Sigil").AddComponent<SpriteRenderer>();
            sigil.sprite = ProceduralSprites.White;
            sigil.color = SealRed;
            sigil.sortingOrder = 46;
            sigil.transform.SetParent(strip.transform, false);
            sigil.transform.localScale = new Vector3(0.3f, 0.7f, 1f);
        }
        transient.Add(strip.gameObject);
        return strip;
    }

    // The seals hold their time even if the attack that laid them is cut short; then the strips burn away.
    IEnumerator LiftSeals(List<SpriteRenderer> strips, List<SpriteRenderer> curtains, float seconds)
    {
        foreach (SpriteRenderer s in strips) if (s != null) transient.Remove(s.gameObject);
        foreach (SpriteRenderer c in curtains) if (c != null) transient.Remove(c.gameObject);
        yield return new WaitForSeconds(seconds - 0.5f);
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            foreach (SpriteRenderer s in strips) if (s != null) s.color = new Color(s.color.r, s.color.g, s.color.b, 1f - t / 0.5f);
            foreach (SpriteRenderer c in curtains) if (c != null) c.color = new Color(c.color.r, c.color.g, c.color.b, 0.08f * (1f - t / 0.5f));
            yield return null;
        }
        // Each strip burns away in seal-fire.
        foreach (SpriteRenderer s in strips)
        {
            if (s == null) continue;
            if (Fx.Play(Fx.KageOfudaBurn, s.transform.position, Color.white, 1.4f) == null) Fx.Play(Fx.Impact, s.transform.position, SealRed, 0.6f);
            Destroy(s.gameObject);
        }
        foreach (SpriteRenderer c in curtains) if (c != null) Destroy(c.gameObject);
    }

    bool InSealedLane()
    {
        if (ronin == null) return false;
        float x = ronin.Bounds.center.x;
        foreach (var (lane, halfWidth, until) in seals)
        {
            if (Time.time < until && Mathf.Abs(x - lane) < halfWidth) return true;
        }
        return false;
    }

    const float GutterTell = 0.35f;
    const float GutterSeconds = 0.4f;

    // The Thousand-Soul Wail: a beam of soul-fire from his open chest sweeps the field, edge to edge (and back in
    // phase 2). Cover stops it and burns; it lights the ground as it drags.
    IEnumerator ThousandSoulWail()
    {
        Bark(BossLine.SoulWail, 7f);
        float side = ronin.Bounds.center.x < CameraShake.Center.x ? 1f : -1f;
        float from = side > 0 ? RightEdge - 0.5f : LeftEdge + 0.5f;
        float to = side > 0 ? LeftEdge + 0.5f : RightEdge - 0.5f;

        // The chest gathers, and a thin guide shows where the beam will start.
        SpriteRenderer gather = Glowing(Chest, new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 57);
        SpriteRenderer guide = Bar(Chest, BeamEnd(Chest, new Vector3(from, GroundY, 0), out _), 0.06f, new Color(Teal.r, Teal.g, Teal.b, 0.5f));
        if (Sfx.Has(Sfx.SoulWail)) Sfx.PlayLoop(Sfx.SoulWail, 0.4f, 0.8f);
        else Sfx.Play(Sfx.LanternCharge, 0.8f, 0.7f);
        // The flare at his chest: the souls' fire gathering, then pouring out of it while the beam burns.
        FxPlayer flare = Fx.Play(Fx.KageWailFlare, Chest, new Color(1f, 1f, 1f, 0f), 1f, null, 58);
        if (flare != null) transient.Add(flare.gameObject);
        SpriteRenderer dim = Darkness();
        HitFlash.Flash(sprite, 0.1f);
        for (float t = 0; t < 1f; t += Time.deltaTime * Tempo)
        {
            gather.transform.localScale = Vector3.one * Mathf.Lerp(1f, 4f, t);
            gather.color = new Color(Teal.r, Teal.g, Teal.b, 0.7f * t);
            if (flare != null)
            {
                flare.transform.position = Chest + Vector3.back * 0.3f;
                flare.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, t * t);
                flare.Color = new Color(1f, 1f, 1f, t);
            }
            dim.color = new Color(0f, 0.02f, 0.02f, 0.25f * t);
            yield return null;
        }
        Drop(gather);
        Drop(guide);
        TimeRipple.Emit(Chest, 5f, 0.5f, 0.5f);
        // A splash where the beam meets the ground.
        FxPlayer splash = Fx.Play(Fx.KageWailFlare, Chest, Color.white, 1.2f, null, 58);
        if (splash != null) transient.Add(splash.gameObject);

        var glow = Bar(Chest, Chest, 1f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 53);
        glow.sprite = ProceduralSprites.Glow;
        var body = Bar(Chest, Chest, 1f, new Color(Teal.r, Teal.g, Teal.b, 0.9f), 55);
        var core = Bar(Chest, Chest, 1f, new Color(0.9f, 1f, 0.97f), 56);
        if (Sfx.Has(Sfx.SoulWail)) Sfx.PlayLoop(Sfx.SoulWail, 1f, 1f);
        else Sfx.PlayLoop(Sfx.LanternBeam, 1f, 0.8f);
        CameraShake.Add(0.3f);
        int passes = secondPhase ? 2 : 1;
        float sweep = secondPhase ? 1.9f : 2.6f;
        float nextBurn = 0, lastFire = from, nextEmber = 0, nextRumble = 0;
        // The beam bursts open past its full width and settles, and on its last stretch pinches shut.
        const float OpenSeconds = 0.18f, CloseSeconds = 0.2f;
        float burned = 0f, total = passes * sweep;
        for (int pass = 0; pass < passes; pass++)
        {
            float a = pass % 2 == 0 ? from : to, b = pass % 2 == 0 ? to : from;
            // Once a sweep the souls falter and the beam gutters out for a moment, thinning and stuttering just
            // before, so even with every scrap of cover burnt away there's a way through it.
            float gutterAt = Random.Range(0.35f, 0.6f) * sweep;
            for (float t = 0; t < sweep; t += Time.deltaTime * Tempo)
            {
                float x = Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, t / sweep));
                Vector3 start = Chest;
                Vector3 end = BeamEnd(start, new Vector3(x, GroundY, 0), out Blocker cover);
                bool faltering = t > gutterAt - GutterTell && t < gutterAt;
                bool guttered = t >= gutterAt && t < gutterAt + GutterSeconds;
                float flicker = 1f + 0.18f * Mathf.Sin(Time.time * 47f) + 0.08f * Mathf.Sin(Time.time * 113f);
                if (faltering) flicker *= Random.value < 0.5f ? 0.25f : 0.7f;
                if (guttered) flicker = 0.06f;
                burned += Time.deltaTime * Tempo;
                float open = burned < OpenSeconds ? Mathf.Lerp(1.8f, 1f, burned / OpenSeconds)
                    : total - burned < CloseSeconds ? Mathf.Max(0f, (total - burned) / CloseSeconds) : 1f;
                Stretch(glow, start, end, 1.8f * flicker * open);
                Stretch(body, start, end, 0.5f * flicker * open);
                Stretch(core, start, end, (guttered ? 0.02f : 0.16f) * open);
                if (flare != null)
                {
                    flare.transform.position = start + Vector3.back * 0.3f;
                    flare.transform.localScale = Vector3.one * 2.2f * (guttered ? 0.4f : open);
                }
                if (splash != null)
                {
                    splash.transform.position = end + Vector3.back * 0.3f;
                    splash.Color = new Color(1f, 1f, 1f, guttered ? 0f : 1f);
                }
                // A low rumble the whole time it burns.
                if (Time.time >= nextRumble && !guttered)
                {
                    nextRumble = Time.time + 0.1f;
                    CameraShake.Add(0.05f);
                }
                if (!guttered) HitIfCrossed(start, end);
                // Hardened, the beam sheds embers as it drags: soul-skulls that drift down behind it.
                float emberY = Row + 3.5f;
                if (Hardened && !guttered && cover == null && end.y < emberY && Time.time >= nextEmber)
                {
                    nextEmber = Time.time + (secondPhase ? 0.18f : 0.25f);
                    float emberX = Mathf.Lerp(start.x, end.x, Mathf.InverseLerp(start.y, end.y, emberY));
                    Skull(new Vector3(emberX, emberY, 0), Vector3.down * 2.2f);
                }
                if (cover != null && Time.time >= nextBurn)
                {
                    nextBurn = Time.time + 0.25f;
                    cover.Hit(end);
                    Sfx.Play(Sfx.BlockerHit, 0.5f, 1.4f);
                }
                if (cover == null && Mathf.Abs(x - lastFire) >= 0.6f)
                {
                    lastFire = x;
                    GroundFire.Light(end);
                }
                if (Random.value < 0.2f) Fx.Play(Fx.Impact, end, Teal, 0.7f);
                yield return null;
            }
        }
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        Drop(glow);
        Drop(body);
        Drop(core);
        if (flare != null) Drop(flare);
        if (splash != null) Drop(splash);
        StartCoroutine(FadeOut(dim, 0.4f));
    }

    // The Last Toll, parry-only: a lock-on rings the ronin, then a great bronze bell drops on him, steered onto
    // his lane as fast as he runs. Parried, it flies back and rings against the Emperor's own armor.
    IEnumerator LastToll()
    {
        while (SealActive) yield return Wait(0.2f);
        Bark(BossLine.LastToll, 5f);
        PlayAction(art.parry);
        Vector3 drop = new Vector3(ronin.Bounds.center.x, TopOfScreen + 1f, 0);
        SpriteRenderer shadow = Glowing(drop + Vector3.down * 1.5f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.5f), 2.5f);
        Toll(0.9f, 0.6f);
        yield return LockOn(1.1f, Bronze);
        Drop(shadow);
        Arrow bell = Shoot(new Vector3(ronin.Bounds.center.x, TopOfScreen + 1f, 0), Vector3.down * 8f);
        Sprite[] frames = Fx.Frames(Fx.KageGreatBell);
        if (frames != null) bell.Restyle(frames, 8f, Color.white, new Vector2(2f, 2.2f), 0.7f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.5f), 3f);
        else bell.Restyle(ProceduralSprites.Disc, Bronze, new Vector2(1.7f, 2f), 0.7f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.55f), 3f);
        bell.MakeUnbreakable();
        bell.MakeReflectable(transform, setPiece: true);
        StartCoroutine(Pursue(bell, 8.5f, face: false));
        StartCoroutine(Streak(bell, new Color(Bronze.r, Bronze.g, Bronze.b, 0.4f)));
        Toll(1f, 0.5f);
        TollRing(bell.transform.position, 6f);
        if (Hardened) ClosingRing((bell.transform.position.y - Row) / 8.5f);
        yield return Wait(1.6f);
    }

    // Skulls in an arc over the ronin that close on where he stands, timed to arrive with the bell, so the parry that
    // turns the bell also has to cut through them.
    void ClosingRing(float arrival)
    {
        const int Count = 7;
        const float Radius = 6f;
        Vector3 centre = ronin.Bounds.center;
        float speed = Mathf.Min(BulletHell.AimedSpeedCap, Radius / Mathf.Max(0.5f, arrival));
        for (int i = 0; i < Count; i++)
        {
            float angle = Mathf.Lerp(32f, 148f, i / (Count - 1f)) * Mathf.Deg2Rad;
            Vector3 from = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Radius;
            Skull(from, (centre - from).normalized * speed / ShotSpeed);
        }
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.5f, 1.2f);
    }
}
