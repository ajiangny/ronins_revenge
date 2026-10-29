using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Phase 1, the Thousand-Soul Throne: the Coffer Slam, the Toll of Fealty, the Rosary Curtain, the Ofuda Decree, the
// Spirit Hyper Beam and the parry-only Last Toll.
public partial class Kagetsune
{
    // --- Phase 1 ---

    // Coffer Slam: he rears back with the coffin-hammer high over his head, trembling with its weight while the coffin
    // blazes, then slams it down at his feet. The courtyard splits in a cone: cracks fan out from the blow down to the
    // ronin's row, soul-fire columns bursting up along each as it opens, and every piece of cover a crack runs through
    // breaks. Where a crack reaches his row its column burns him. The second fan of cracks lands between the first's, so
    // he steps into where a column has just burnt out; hardened, a third fan follows the first's lines.
    IEnumerator CofferSlam()
    {
        Bark(BossLine.CofferSlam, 7f);
        float tell = secondPhase ? 0.75f : 0.95f;
        SpriteRenderer raised = Glowing(HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.2f), 2f);
        RaiseHammer(tell);
        Transform view = sprite.transform;
        int rung = 0;
        for (float t = 0; t < tell; t += Time.deltaTime * Tempo)
        {
            float k = t / tell;
            // He rears up and stretches tall under the weight, shaking harder the longer he holds it.
            float rear = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f));
            Vector2 shake = Random.insideUnitCircle * 0.04f * k;
            view.localPosition = viewHome + (Vector3.up * (0.3f * rear) + (Vector3)shake) / transform.lossyScale.x;
            view.localScale = new Vector3(viewScale.x * (1f - 0.03f * rear), viewScale.y * (1f + 0.05f * rear), viewScale.z);
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

        // The slam: the hammer comes down on its impact frame, and he drops and squashes into the blow.
        StrikeHammer();
        Vector3 impact = new Vector3(Centre.x + Extents.x * 0.2f, sprite.bounds.min.y + Extents.y * 0.32f, 0);
        view.localPosition = viewHome + Vector3.down * (0.25f / transform.lossyScale.x);
        view.localScale = new Vector3(viewScale.x * 1.07f, viewScale.y * 0.9f, viewScale.z);
        StartCoroutine(SettleView(0.25f));
        Slam(1f);
        CameraShake.Add(0.65f);
        HitStop?.Invoke(0.06f);
        Fx.Play(Fx.Impact, impact, Teal, 2.8f);
        Shockwave(impact, 4.5f);
        TimeRipple.Emit(impact, 5f, 0.5f, 0.5f);
        Debris.BurstSprites(impact, art.deathParticles, Bronze, 16, 1f, 9f);
        // The crater: short cracks split out all round where the hammer landed.
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f + Random.Range(-20f, 20f);
            Vector3 reach = Quaternion.Euler(0, 0, angle) * Vector3.right * Random.Range(0.8f, 1.5f);
            Split(impact, impact + new Vector3(reach.x, reach.y * 0.55f, 0), 24f, 0.75f);
        }

        // The cone: each fan's cracks spread from the blow to the row, `ConeHalfAngle` either side of straight down.
        float ground = GroundY + 0.25f;
        float spread = (impact.y - ground) * Mathf.Tan(ConeHalfAngle * Mathf.Deg2Rad);
        float[][] fans = { new[] { -1f, 0f, 1f }, new[] { -0.5f, 0.5f } };
        int waves = Hardened ? 3 : 2;
        for (int wave = 0; wave < waves; wave++)
        {
            foreach (float f in fans[wave % 2])
            {
                Vector3 end = new Vector3(Mathf.Clamp(impact.x + f * spread, FieldLeft, FieldRight), ground, 0);
                StartCoroutine(OpenCrack(impact, end));
            }
            if (wave < waves - 1) yield return Wait(secondPhase ? 0.75f : 0.9f);
        }
        yield return Wait(1.4f);
    }

    // How wide the cone of cracks opens, how fast a crack runs, how far apart its columns rise, how long a column's
    // spot glows before it erupts, and how it burns.
    const float ConeHalfAngle = 34f;
    const float FissureSpeed = 16f;
    const float ColumnStep = 2.2f;
    const float ColumnTell = 0.55f;
    const float EruptSeconds = 0.3f;
    const float EruptHeight = 6f;
    const float EruptWidth = 1.4f;

    // One crack of the cone: the fissure opens from the blow toward the row, and a column erupts every ColumnStep
    // along it as it gets there (its stretch of crack flaring first), short ones on the way and a full one where it reaches the row (the one that burns).
    IEnumerator OpenCrack(Vector3 from, Vector3 to)
    {
        SoulFissure crack = Split(from, to, FissureSpeed);
        float length = Vector3.Distance(from, to);
        for (float d = ColumnStep; d < length - ColumnStep * 0.5f; d += ColumnStep)
        {
            Vector3 spot = Vector3.Lerp(from, to, d / length);
            StartCoroutine(EruptColumn(spot, d / FissureSpeed, 2.5f, false, crack));
        }
        StartCoroutine(EruptColumn(to, length / FissureSpeed, EruptHeight, true, crack));
        yield return new WaitForSeconds(crack.OpenSeconds);
    }

    // One column out of a crack: its spot glows and swells for the tell, then soul-fire bursts up from it. On the
    // ronin's row (`burns`) it burns him if he's in it; anywhere it cracks the cover it rises through. Once the
    // crack is open it runs in real time: a stagger doesn't hold columns already glowing.
    IEnumerator EruptColumn(Vector3 spot, float delay, float height, bool burns, SoulFissure crack)
    {
        yield return new WaitForSeconds(delay);
        if (IsDead) yield break;
        // The crack itself flares where the column is coming.
        if (crack != null) crack.Flare(spot, ColumnTell);
        float width = burns ? EruptWidth : EruptWidth * 0.7f;
        SpriteRenderer mark = Glowing(spot + Vector3.up * 0.2f, new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 4);
        Detach(mark.gameObject);
        for (float t = 0; t < ColumnTell && mark != null; t += Time.deltaTime)
        {
            float k = t / ColumnTell;
            mark.transform.localScale = new Vector3(Mathf.Lerp(1f, width * 1.8f, k), Mathf.Lerp(0.4f, 0.9f, k), 1f);
            mark.color = new Color(Teal.r, Teal.g, Teal.b, (burns ? 0.15f : 0.08f) + (burns ? 0.6f : 0.35f) * k * k);
            yield return null;
        }
        if (mark != null) Destroy(mark.gameObject);
        if (IsDead) yield break;
        Vector3 top = spot + Vector3.up * height;
        if (SoulColumn(top, spot, width) == null)
        {
            SpriteRenderer bar = Bar(spot, top, width * 0.5f, new Color(0.85f, 1f, 0.96f, 0.9f), 56);
            Detach(bar.gameObject);
            Fader.Fade(bar, EruptSeconds + 0.2f);
        }
        Fx.Play(Fx.SmokePuff, spot, new Color(0.25f, 0.27f, 0.28f, 0.7f), burns ? 1.4f : 0.9f);
        if (burns)
        {
            if (Sfx.Has(Sfx.SoulWail)) Sfx.Play(Sfx.SoulWail, 0.25f, 1.6f);
            else Sfx.Play(Sfx.LanternBeam, 0.25f, 1.4f);
        }
        BeamEnd(top, spot, out Blocker cover);
        if (cover != null) cover.Hit(spot);
        for (float t = 0; t < EruptSeconds && burns; t += Time.deltaTime)
        {
            if (ronin != null && Mathf.Abs(ronin.Bounds.center.x - spot.x) < width / 2f) ronin.TakeHit(new Vector3(spot.x, ronin.Bounds.center.y, 0));
            yield return null;
        }
        if (burns) GroundFire.Light(spot, soul: true);
    }

    // A fissure splitting the courtyard from one point to another at `speed` (a SoulFissure, drawn in pixels the size
    // of the ronin's art). As each piece opens, the cover over it breaks and stone chips and dust kick up from it.
    SoulFissure Split(Vector3 from, Vector3 to, float speed, float width = 1f)
    {
        float pixel = ronin != null ? Mathf.Clamp(ronin.ArtPixelSize, 1f / 64f, 1f / 24f) : 1f / 48f;
        SoulFissure crack = SoulFissure.Open(from, to, speed, pixel, width);
        crack.Opened += (a, b) =>
        {
            BreakCoverAlong(a, b);
            Debris.BurstSprites(b, art.deathParticles, new Color(0.55f, 0.52f, 0.5f), 3, 0.5f * width, 3.5f);
            if (Random.value < 0.5f) Fx.Play(Fx.SmokePuff, b, new Color(0.3f, 0.3f, 0.32f, 0.55f), 0.9f * width);
        };
        return crack;
    }

    // The ground splits under the cover a crack runs through: every blocker on the way breaks.
    static readonly RaycastHit[] CrackHits = new RaycastHit[16];

    static void BreakCoverAlong(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        int count = Physics.SphereCastNonAlloc(from, 0.45f, delta.normalized, CrackHits, delta.magnitude);
        for (int i = 0; i < count; i++)
        {
            if (CrackHits[i].collider.GetComponentInParent<Blocker>() is Blocker cover) cover.Demolish();
        }
    }

    // His slam on his own art: the wind-up (everything before the impact frame) played over the first half of the
    // tell so the hammer is held high through the rest, and StrikeHammer plays the impact and follow-through on the
    // blow. Without enough frames the whole clip plays at the blow.
    int ImpactFrame => art.attack != null && art.attack.Length >= 4 ? Mathf.RoundToInt(art.attack.Length * 0.625f) : -1;

    void RaiseHammer(float seconds)
    {
        int impact = ImpactFrame;
        if (impact <= 0 || IsDead) return;
        var windup = new Sprite[impact];
        System.Array.Copy(art.attack, windup, impact);
        animator.Play(windup, false, null, impact / (seconds * 0.5f));
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
        animator.Play(blow, false, PlayIdle, art.actionFps);
    }

    // Where his art rests on his body, so a rear-back or a squash can always be put back.
    Vector3 viewHome, viewScale;

    // Springs his art back from the slam's squash.
    IEnumerator SettleView(float seconds)
    {
        Transform view = sprite.transform;
        Vector3 fromPos = view.localPosition, fromScale = view.localScale;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            view.localPosition = Vector3.Lerp(fromPos, viewHome, k);
            view.localScale = Vector3.Lerp(fromScale, viewScale, k);
            yield return null;
        }
        ResetView();
    }

    void ResetView()
    {
        if (sprite == null) return;
        sprite.transform.localPosition = viewHome;
        sprite.transform.localScale = viewScale;
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
            GroundFire.Light(end, soul: true);
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

    // The Spirit Hyper Beam's footprint: it opens this wide at the edge Oni stands nearer and pinches out this far
    // short of the far edge, leaving him a strip to stand in while it dies.
    float HyperOpenWidth => secondPhase ? 12f : 11f;
    const float HyperSafe = 3f;
    // Seconds the beam takes to cross. Its leading edge covers the field less the opening and the safe strip in this
    // time, well under Oni's run, so he can always stay ahead of it.
    float HyperSweep => secondPhase ? 3.8f : Hardened ? 4.3f : 4.8f;
    // Long enough to run out of the whole opening from the edge.
    float HyperTell => secondPhase ? 1.5f : 1.6f;
    static readonly Color HyperSafeColour = new Color(1f, 0.95f, 0.8f);
    static readonly Collider[] HyperCover = new Collider[32];

    // The Spirit Hyper Beam: every soul in him screams out of his open chest at once, a torrent of soul-fire that slams
    // into the ground across a third of the field at the edge Oni stands nearer. It drives across to the far edge,
    // burning through any cover it passes over, and thins as it goes, its leading edge slower than Oni can run, until
    // it narrows to a thread short of the far edge and dissipates there. The strip past where it dies is safe, and is
    // lit pale from the tell on.
    IEnumerator SpiritHyperBeam()
    {
        Bark(BossLine.SoulWail, 7f);
        float dir = ronin.Bounds.center.x < CameraShake.Center.x ? 1f : -1f;
        float start = dir > 0 ? LeftEdge : RightEdge;
        float far = dir > 0 ? RightEdge : LeftEdge;
        float stop = far - dir * HyperSafe;
        float open = HyperOpenWidth;
        float ground = GroundY - 0.3f;
        float band = GroundY + 0.9f;

        // The tell: the chest gathers, the ground it will open on pulses teal, two guides run down to its edges, and the
        // safe strip at the far edge glows pale.
        SpriteRenderer gather = Glowing(Chest, new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 57);
        FxPlayer flare = Fx.Play(Fx.KageWailFlare, Chest, new Color(1f, 1f, 1f, 0f), 1f, null, 58);
        if (flare != null) transient.Add(flare.gameObject);
        SpriteRenderer danger = Bar(new Vector3(start, band, 0), new Vector3(start + dir * open, band, 0), 2.4f, Color.clear, 2);
        SpriteRenderer safe = Bar(new Vector3(stop, band, 0), new Vector3(far, band, 0), 2.4f, Color.clear, 2);
        SpriteRenderer guideA = Bar(Chest, new Vector3(start, ground, 0), 0.05f, Color.clear, 54);
        SpriteRenderer guideB = Bar(Chest, new Vector3(start + dir * open, ground, 0), 0.05f, Color.clear, 54);
        if (Sfx.Has(Sfx.SoulWail)) Sfx.PlayLoop(Sfx.SoulWail, 0.45f, 0.7f);
        else Sfx.Play(Sfx.LanternCharge, 0.9f, 0.6f);
        SpriteRenderer dim = Darkness();
        HitFlash.Flash(sprite, 0.1f);
        float tell = HyperTell;
        for (float t = 0; t < tell; t += Time.deltaTime * Tempo)
        {
            float k = t / tell;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(8f, 22f, k));
            gather.transform.position = Chest + Vector3.back * 0.1f;
            gather.transform.localScale = Vector3.one * Mathf.Lerp(1f, 6f, k);
            gather.color = new Color(Teal.r, Teal.g, Teal.b, 0.8f * k);
            if (flare != null)
            {
                flare.transform.position = Chest + Vector3.back * 0.3f;
                flare.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3f, k * k);
                flare.Color = new Color(1f, 1f, 1f, k);
            }
            danger.color = new Color(Teal.r, Teal.g, Teal.b, 0.12f + 0.3f * k * pulse);
            safe.color = new Color(HyperSafeColour.r, HyperSafeColour.g, HyperSafeColour.b, 0.18f + 0.14f * pulse);
            guideA.color = guideB.color = new Color(Teal.r, Teal.g, Teal.b, 0.6f * k);
            dim.color = new Color(0f, 0.02f, 0.02f, 0.35f * k);
            yield return null;
        }
        Drop(gather);
        Drop(danger);
        Drop(guideA);
        Drop(guideB);
        Sfx.Stop(Sfx.SoulWail);

        // Drawn as pixel art in code (Kagetsune.Beam.cs): the stepped cylinder, its coil and the souls riding it.
        var beam = new SpiritBeamView(this, secondPhase);
        FxPlayer splash = Fx.Play(Fx.KageWailFlare, new Vector3(start + dir * open * 0.5f, ground, 0), Color.white, 2f, null, 58);
        if (splash != null) transient.Add(splash.gameObject);
        TimeRipple.Emit(Chest, 7f, 0.6f, 0.6f);
        HitFlash.Flash(sprite, 0.15f);
        CameraShake.Add(0.6f);
        Slam(0.8f);
        if (Sfx.Has(Sfx.SoulWail)) Sfx.PlayLoop(Sfx.SoulWail, 1f, 0.85f);
        Sfx.PlayLoop(Sfx.LanternBeam, 0.8f, 0.6f);

        float sweep = HyperSweep;
        float nextBurn = 0, nextRumble = 0, lastFire = start;
        for (float t = 0; t < sweep; t += Time.deltaTime * Tempo)
        {
            float k = t / sweep;
            float lead = Mathf.Lerp(start + dir * open, stop, k);
            float trail = Mathf.Lerp(start - dir * 0.5f, stop, k);
            float centre = (lead + trail) * 0.5f;
            // It bursts open past its full width for a moment, then thins as it drives on.
            float burst = t < 0.2f ? Mathf.Lerp(1.2f, 1f, t / 0.2f) : 1f;
            float flicker = 1f + 0.06f * Mathf.Sin(Time.time * 47f) + 0.03f * Mathf.Sin(Time.time * 113f);
            float half = Mathf.Abs(lead - trail) * 0.5f * burst;
            float chestHalf = (0.4f + 2.2f * (1f - k)) * burst * flicker;
            Vector3 top = Chest, foot = new Vector3(centre, ground, 0);
            beam.Draw(top, chestHalf, foot, half);
            if (flare != null)
            {
                flare.transform.position = Chest + Vector3.back * 0.3f;
                flare.transform.localScale = Vector3.one * Mathf.Lerp(3f, 1.4f, k);
            }
            if (splash != null)
            {
                splash.transform.position = foot + Vector3.back * 0.3f;
                splash.transform.localScale = Vector3.one * Mathf.Max(0.8f, half * 0.45f);
            }
            safe.color = new Color(HyperSafeColour.r, HyperSafeColour.g, HyperSafeColour.b, 0.24f + 0.08f * Mathf.Sin(Time.time * 6f));
            dim.color = new Color(0f, 0.02f, 0.02f, 0.35f);

            // Oni burns wherever the beam spans his row.
            float at = Mathf.InverseLerp(top.y, ground, Row);
            float rowCentre = Mathf.Lerp(top.x, centre, at), rowHalf = Mathf.Lerp(chestHalf, half, at);
            float x = ronin.Bounds.center.x;
            if (Mathf.Abs(x - rowCentre) < rowHalf + 0.2f) ronin.TakeHit(new Vector3(Mathf.Clamp(x, rowCentre - rowHalf, rowCentre + rowHalf), Row, 0));
            // It doesn't stop at cover: every piece it stands on burns through.
            if (Time.time >= nextBurn)
            {
                nextBurn = Time.time + 0.2f;
                int count = Physics.OverlapBoxNonAlloc(new Vector3(centre, GroundY + 3f, 0), new Vector3(half, 3f, 5f), HyperCover);
                for (int i = 0; i < count; i++)
                {
                    if (!HyperCover[i].TryGetComponent(out Blocker cover)) continue;
                    cover.Hit(cover.transform.position);
                    if (Random.value < 0.3f) Sfx.Play(Sfx.BlockerHit, 0.5f, 1.4f);
                }
            }
            // The ground it has crossed burns behind it, short of the safe strip.
            if (Mathf.Abs(trail - lastFire) >= 0.9f && Mathf.Abs(stop - trail) > 2.5f)
            {
                lastFire = trail;
                GroundFire.Light(new Vector3(trail, GroundY, 0), soul: true);
            }
            if (Random.value < 0.5f) Fx.Play(Fx.Impact, new Vector3(Mathf.Lerp(trail, lead, Random.value), ground + 0.3f, 0), Teal, 0.9f);
            if (Time.time >= nextRumble)
            {
                nextRumble = Time.time + 0.1f;
                CameraShake.Add(0.07f + 0.08f * (1f - k));
            }
            yield return null;
        }

        // It thins to a thread where it stopped and dissipates in a scatter of freed souls.
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        Sfx.Play(Sfx.LanternCharge, 0.5f, 1.6f);
        Vector3 end = new Vector3(stop, ground, 0);
        ElementFx.Glow(end + Vector3.up * 0.5f, Teal, 0.5f, 4f, 0.6f);
        for (int i = 0; i < 6; i++) Fx.Play(Fx.Impact, end + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0f, 2.5f), 0), Teal, 0.8f);
        if (splash != null) Drop(splash);
        for (float t = 0; t < 0.45f; t += Time.deltaTime * Tempo)
        {
            float fade = 1f - t / 0.45f;
            // Down to a thread a pixel or two wide, the coil drawing in with it and the souls flung off.
            float thread = Mathf.Max(1f / BeamPixel, 0.3f * fade);
            beam.Draw(Chest, thread, end, thread, 1f - fade);
            if (flare != null) flare.Color = new Color(1f, 1f, 1f, fade);
            safe.color = new Color(HyperSafeColour.r, HyperSafeColour.g, HyperSafeColour.b, 0.24f * fade);
            dim.color = new Color(0f, 0.02f, 0.02f, 0.35f * fade);
            yield return null;
        }
        beam.Remove();
        if (flare != null) Drop(flare);
        Drop(safe);
        Drop(dim);
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
