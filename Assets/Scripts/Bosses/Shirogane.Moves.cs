using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Her first-phase moves and the parry-only draws.
public partial class Shirogane
{
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
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        // Past the middle of her first bar she has his measure, and aims.
        bool aimed = secondPhase || HealthFraction < 0.5f;
        Vector3 aim = aimed ? (ronin.Bounds.center - Hands).normalized : Vector3.down;
        WolfSlash(Hands, aim, 2.6f);
        CrescentFan(aim, 0f);
        // Enraged, a second swing follows, its blades between the first's and aimed afresh.
        if (enraged)
        {
            yield return Wait(0.25f);
            PlayAction(art.attack);
            Sfx.Play(Sfx.SwordSlash, 0.9f, 0.8f);
            Vector3 again = (ronin.Bounds.center - Hands).normalized;
            WolfSlash(Hands, again, 2.6f);
            CrescentFan(again, 9f);
        }
        yield return Wait(0.3f);
    }

    // Her sword slash: a frost arc sweeps out from `at` toward `aim` and an ice wolf's head lunges along it, jaws
    // open, and fades. Without the art, the plain slash arc and a glow.
    void WolfSlash(Vector3 at, Vector3 aim, float size)
    {
        aim.z = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.down;
        FxPlayer arc = Fx.Play(Fx.ShiroIceSlash, at, Color.white, size * 0.6f);
        if (arc != null) arc.transform.rotation = Quaternion.FromToRotation(Vector3.right, aim);
        else if (Fx.Play(Fx.SlashArc, at, Cyan, size) == null) ElementFx.Glow(at, Cyan, 1f, size + 0.4f, 0.2f);
        Sprite[] head = Fx.Frames(Fx.ShiroWolfSlash);
        if (head != null) StartCoroutine(Lunge(head[0], at, aim, size));
    }

    // The ice wolf's head leaping out along a slash: it swells in, lunges past the arc and fades, leaving a ghost.
    IEnumerator Lunge(Sprite head, Vector3 at, Vector3 aim, float size)
    {
        SpriteRenderer wolfHead = Glowing("IceWolf", head, 57);
        wolfHead.transform.rotation = Quaternion.FromToRotation(Vector3.right, aim);
        // Drawn facing right; flipped so it never lunges upside down.
        wolfHead.flipY = aim.x < 0f;
        float scale = size * 1.3f / head.bounds.size.x;
        const float Seconds = 0.4f;
        for (float t = 0; t < Seconds && wolfHead != null; t += Time.deltaTime)
        {
            float k = t / Seconds;
            wolfHead.transform.position = at + aim * (size * (0.15f + 0.9f * Mathf.Sqrt(k))) + Vector3.back * 0.3f;
            wolfHead.transform.localScale = Vector3.one * scale * (0.7f + 0.4f * k);
            wolfHead.color = new Color(1f, 1f, 1f, k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f);
            if (k > 0.3f && Random.value < 0.35f) Afterimage(wolfHead, new Color(Frost.r, Frost.g, Frost.b, 0.35f));
            yield return null;
        }
        Drop(wolfHead);
    }

    // Five crescents in a fan about `aim`, turned by `offset` degrees.
    void CrescentFan(Vector3 aim, float offset)
    {
        var fan = new List<Arrow>();
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(-36f, 36f, i / 4f) + offset;
            fan.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
    }

    // Phantom Blade Rain: pillars of ice form at the top of six lanes, a thin warning line and a ring of frost under
    // each, then fall. At least one lane stays empty. In her second phase there are seven lanes, two empty, the
    // pillars fall in a ripple, and one lane (never beside an open one) brings down a glacier spire that must be
    // dodged; enraged, eight lanes with only one empty.
    IEnumerator BladeRain()
    {
        if (Random.value < 0.5f) Speak(BossLine.BladeRain);
        int laneCount = enraged ? 8 : secondPhase ? 7 : 6;
        int empty = secondPhase && !enraged ? 2 : 1;
        List<float> lanes = Lanes(laneCount);
        // The empty lanes are picked at random; the ronin's own lane is never made the only way out on purpose.
        var open = new HashSet<int>();
        while (open.Count < empty) open.Add(Random.Range(0, lanes.Count));
        int spire = -1;
        if (secondPhase)
        {
            var away = new List<int>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!open.Contains(i) && !open.Contains(i - 1) && !open.Contains(i + 1)) away.Add(i);
            }
            if (away.Count > 0) spire = away[Random.Range(0, away.Count)];
        }

        var tells = new List<SpriteRenderer>();
        var pillars = new List<SpriteRenderer>();
        var falling = new List<float>();
        float hang = PillarHang;
        for (int i = 0; i < lanes.Count; i++)
        {
            if (open.Contains(i)) continue;
            if (i == spire)
            {
                StartCoroutine(Spire(lanes[i], RainWarning));
                continue;
            }
            float x = lanes[i];
            falling.Add(x);
            tells.Add(LaneWarning(x));
            tells.Add(FrostMark(x));
            pillars.Add(PillarPicture(new Vector3(x, hang, 0)));
            Puff(new Vector3(x, hang, 0), 1f);
        }
        Sfx.Play(Sfx.MistSwirl, 0.6f, 1f);
        Sfx.Play(Sfx.FrostChill, 0.5f, 1.1f);
        RoamMeanwhile(RainWarning);
        yield return Brighten(tells, RainWarning);
        StopRoaming();

        for (int i = 0; i < falling.Count; i++)
        {
            Drop(pillars[i]);
            IcePillar(new Vector3(falling[i], hang, 0), 11f);
            if (secondPhase) yield return Wait(0.08f);
        }
        RemoveTells(tells);
        Sfx.Play(Sfx.GhostSwordFall, 0.9f, 0.8f);
        yield return Wait(0.25f);
        RingingFence();
        yield return Wait(0.4f);
    }

    // After the rain, a short fence of ghost swords falls over the ronin with one bronze-edged ringing blade at its
    // heart. He can walk out round the end, or parry the ringing blade: it rings, and the swords beside it burst.
    // It's the first bronze shot, so the Emperor's bells read as a parry when they come.
    void RingingFence()
    {
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 5f, RightEdge - 5f);
        float hang = TopOfScreen - 1.4f;
        BulletHell.Shooter sword = (from, velocity) => BulletHell.Enlist(GhostSword(from, velocity));
        BulletHell.Shooter ringing = (from, velocity) =>
        {
            Arrow blade = BulletHell.MakeResonant(BulletHell.Enlist(GhostSword(from, velocity)));
            blade.Tint(Color.Lerp(Color.white, BulletHell.Bronze, 0.7f), new Color(BulletHell.Bronze.r, BulletHell.Bronze.g, BulletHell.Bronze.b, 0.7f));
            return blade;
        };
        BulletHell.Curtain(sword, hang, x - 4.2f, x + 4.2f, 1.4f, null, BulletHell.MinGap, 7f, i => i == 3 ? ringing : null);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.8f);
    }

    // Moonlit Dash: a blue trail shows the way, she vanishes into mist and streaks across the field, and three
    // afterimages left along her path each drop a pillar of ice straight down, one after the other. Enraged, she
    // streaks straight back the way she came and leaves three more.
    IEnumerator MoonlitDash()
    {
        if (Random.value < 0.4f) Speak(BossLine.MoonlitDash);
        float range = HalfWidth - 2f;
        int passes = enraged ? 2 : 1;
        for (int pass = 0; pass < passes; pass++)
        {
            float far = anchor.x < CameraShake.Center.x ? CameraShake.Center.x + range : CameraShake.Center.x - range;
            Vector3 from = anchor;
            Vector3 to = new Vector3(far, anchor.y, anchor.z);
            SpriteRenderer trail = Bar(from, to, 0.08f, Trail);
            float tell = pass == 0 ? 0.5f : 0.3f;
            for (float t = 0; t < tell; t += Time.deltaTime * Tempo)
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
                IcePillar(at, 10f);
                Sfx.Play(Sfx.GhostSwordFall, 0.6f, 1.2f);
                transient.Remove(ghost.gameObject);
                Fader.Fade(ghost, 0.3f);
                yield return Wait(pass < passes - 1 ? 0.18f : 0.25f);
            }
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
    // the left and the outer row down and to the right, crossing in the middle of the field. Each sword curves after
    // the ronin as it falls, then flies straight for the last stretch, so standing still is a hit but a late step
    // aside still slips it.
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
            Arrow blade = GhostSword(at, velocity);
            volley.Add(blade);
            StartCoroutine(Seek(blade));
        }
        IgnoreEachOther(volley);
    }

    // How fast a barrage sword turns after the ronin, in degrees a second, and how far above him it gives up and
    // flies straight.
    float SeekTurn => enraged ? 240f : secondPhase ? 170f : 140f;
    const float SeekStops = 3.5f;
    const float SeekSeconds = 1.4f;

    IEnumerator Seek(Arrow blade)
    {
        for (float t = 0; t < SeekSeconds; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            if (blade == null || ronin == null) yield break;
            Vector3 at = blade.transform.position;
            Vector3 target = ronin.Bounds.center;
            if (at.y - target.y < SeekStops) yield break;
            Vector3 velocity = blade.GetComponent<Rigidbody>().linearVelocity;
            Vector3 toward = target - at;
            toward.z = 0;
            Vector3 turned = Vector3.RotateTowards(velocity, toward.normalized * velocity.magnitude, SeekTurn * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            blade.Turn(turned);
        }
    }

    // Mirror Moon: she takes one side of the field and leaves a reflection of herself standing on the other, mirrored
    // across the middle. Each looses an aimed fan of three crescents a beat apart, a crossfire with gaps between.
    // Enraged, a second reflection stands in the middle and fires last.
    IEnumerator MirrorMoon()
    {
        if (Random.value < 0.5f) Speak(BossLine.MirrorMoon);
        float side = anchor.x < CameraShake.Center.x ? -1f : 1f;
        yield return Glide(CameraShake.Center.x + side * HalfWidth * 0.45f);
        var reflections = new List<SpriteRenderer>();
        float mirrorX = CameraShake.Center.x * 2f - sprite.transform.position.x;
        var spots = new List<float> { mirrorX };
        if (enraged) spots.Add(CameraShake.Center.x);
        foreach (float x in spots)
        {
            SpriteRenderer reflection = Linger();
            reflection.transform.position = new Vector3(x, reflection.transform.position.y, reflection.transform.position.z);
            reflection.flipX = !sprite.flipX;
            Puff(reflection.bounds.center, 2f);
            ElementFx.Glow(ReflectionHands(reflection), Cyan, 0.7f, 2.4f, 0.6f);
            reflections.Add(reflection);
        }
        Sfx.Play(Sfx.MistSwirl, 0.7f, 1.3f);
        ElementFx.Glow(Hands, Cyan, 0.7f, 2.4f, 0.6f, transform);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.25f);
        yield return Wait(enraged ? 0.45f : 0.6f);

        PlayAction(art.attack);
        AimedFan(Hands);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.75f);
        foreach (SpriteRenderer reflection in reflections)
        {
            yield return Wait(enraged ? 0.3f : 0.35f);
            if (reflection == null) continue;
            AimedFan(ReflectionHands(reflection));
            Sfx.Play(Sfx.SwordSlash, 0.8f, 0.85f);
            transient.Remove(reflection.gameObject);
            Fader.Fade(reflection, 0.4f);
        }
        yield return Wait(0.3f);
    }

    static Vector3 ReflectionHands(SpriteRenderer reflection)
    {
        return reflection.bounds.center + Vector3.down * (reflection.bounds.size.y * 0.1f);
    }

    // Three crescents from a point, the middle one at the ronin.
    void AimedFan(Vector3 from)
    {
        Vector3 aim = (ronin.Bounds.center - from).normalized;
        WolfSlash(from, aim, 2.2f);
        var fan = new List<Arrow>();
        for (int i = 0; i < 3; i++)
        {
            float angle = Mathf.Lerp(-16f, 16f, i / 2f);
            fan.Add(Crescent(from, Quaternion.Euler(0, 0, angle) * aim * CrescentSpeed));
        }
        IgnoreEachOther(fan);
    }

    // --- Parry-only draws ---

    // Crossing Moons, her parry-only draw: she crosses her twin blades and a ghost crescent forms on each, brightening
    // while motes trace the curves they will take and a reticle closes on him, then flashing as it locks. She
    // flings them out to either side and they curve back in low along the ground like sickles, closing like scissors,
    // their curves bending to follow him and trailing afterimages, and meet on him together: there is nowhere to
    // step, so the answer is a parry as they cross. Parried, they fly back and break her stance.
    IEnumerator Moonsplitter()
    {
        Speak(BossLine.Moonsplitter);
        PlayAction(art.parry);
        ElementFx.Glow(Hands, Frost, 0.9f, 3f, DrawAim, transform);
        Sfx.Play(Sfx.BladeRing, 0.8f, 0.8f);

        float height = ronin.Bounds.center.y;
        // Each moon leaves one of her blades, flies out wide to its own side and curves back in low along the ground
        // to him: a sickle's curve, bending past the edge of the field at about his height.
        Vector3[] starts = { Hands + Vector3.left * 0.6f, Hands + Vector3.right * 0.6f };
        Vector3[] bends = { new Vector3(LeftEdge - 1.5f, height + MoonBend, 0), new Vector3(RightEdge + 1.5f, height + MoonBend, 0) };
        var ghosts = new List<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.ShiroCrescent);
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer ghost = Glowing("MoonGhost", frames != null ? frames[0] : ProceduralSprites.Glow, 54);
            ghost.transform.position = starts[i];
            // Drawn facing down, the way it sets off.
            ghost.transform.rotation = Quaternion.FromToRotation(Vector3.down, bends[i] - starts[i]);
            float art = frames != null ? frames[0].bounds.size.x : 1f;
            ghost.transform.localScale = Vector3.one * (2.4f / art);
            ghosts.Add(ghost);
            Puff(starts[i], 1.6f);
        }
        Sfx.Play(Sfx.MistSwirl, 0.7f, 1.2f);
        // The ghosts brighten and their paths are traced in motes while the reticle closes on him.
        StartCoroutine(Brighten(ghosts, DrawAim));
        Coroutine tracing = StartCoroutine(TraceMoonPaths(starts, bends, DrawAim + 0.18f));
        yield return LockOn(Hands, DrawAim, Frost);
        if (tracing != null) StopCoroutine(tracing);
        foreach (SpriteRenderer ghost in ghosts) Drop(ghost);

        PlayAction(art.attack);
        Sfx.Play(Sfx.SwordSlash, 1f, 0.55f);
        Sfx.Play(Sfx.BladeRing, 0.9f, 0.7f);
        CameraShake.Add(0.3f);
        var pair = new List<Arrow>();
        float speed = DrawSpeed * ShotSpeed * 0.9f;
        // Both take the same time, so they meet on him together; the curve is a little shorter than its corners.
        float seconds = 0.85f * ((bends[0] - starts[0]).magnitude + (ronin.Bounds.center - bends[0]).magnitude) / speed;
        for (int i = 0; i < 2; i++)
        {
            Arrow moon = Fire(starts[i], (bends[i] - starts[i]).normalized * speed / ShotSpeed);
            Color halo = new Color(Frost.r, Frost.g, Frost.b, 0.6f);
            if (frames != null) moon.Restyle(frames, 14f, Color.white, new Vector2(3.2f, 2.2f), 0.8f, halo, 3.6f);
            else moon.Restyle(ProceduralSprites.Glow, Frost, new Vector2(1.4f, 3f), 0.8f, halo, 3.6f);
            moon.MakeUnbreakable();
            moon.MakeReflectable(transform);
            WolfSlash(starts[i], bends[i] - starts[i], 2.6f);
            pair.Add(moon);
            StartCoroutine(Close(moon, starts[i], bends[i], seconds));
        }
        IgnoreEachOther(pair);
        yield return Wait(0.6f);
    }

    // How far above the ronin the crossing moons' curves bend back in.
    const float MoonBend = 1.2f;

    // A point on the sickle curve from `start`, bending at `bend`, ending at `end`, `u` of the way along.
    static Vector3 MoonPath(Vector3 start, Vector3 bend, Vector3 end, float u)
    {
        float v = 1f - u;
        return v * v * start + 2f * v * u * bend + u * u * end;
    }

    // Dots of light along both moons' paths to the ronin, following him, so the curve they will take reads before
    // they fly.
    IEnumerator TraceMoonPaths(Vector3[] starts, Vector3[] bends, float seconds)
    {
        const int Dots = 12;
        var dots = new List<SpriteRenderer>();
        for (int i = 0; i < Dots * 2; i++)
        {
            SpriteRenderer dot = Glowing("MoonPath", ProceduralSprites.Glow, 53);
            dot.transform.localScale = Vector3.one * 0.35f;
            dots.Add(dot);
        }
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = Mathf.Clamp01(t / seconds);
            for (int side = 0; side < 2; side++)
            {
                for (int d = 0; d < Dots; d++)
                {
                    // The dots run along the path toward him, so it reads as the way they will come.
                    float u = Mathf.Repeat((d + t * 6f) / Dots, 1f);
                    SpriteRenderer dot = dots[side * Dots + d];
                    if (dot == null) continue;
                    dot.transform.position = MoonPath(starts[side], bends[side], ronin.Bounds.center, u) + Vector3.back * 0.2f;
                    dot.color = new Color(Frost.r, Frost.g, Frost.b, (0.15f + 0.45f * k) * Mathf.Sin(Mathf.PI * u));
                }
            }
            yield return null;
        }
        foreach (SpriteRenderer dot in dots) Drop(dot);
    }

    // Flies a crossing moon along its sickle curve onto the ronin, the curve's end following him so it can't be
    // stepped out of, trailing pale afterimages; once there it flies on straight.
    IEnumerator Close(Arrow moon, Vector3 start, Vector3 bend, float seconds)
    {
        float nextGhost = 0f;
        for (float t = 0; moon != null && ronin != null && t < seconds; t += Time.fixedDeltaTime)
        {
            float u = Mathf.Clamp01((t + Time.fixedDeltaTime) / seconds);
            Vector3 next = MoonPath(start, bend, ronin.Bounds.center, u);
            Vector3 step = next - moon.transform.position;
            step.z = 0;
            if (step.sqrMagnitude > 0.0001f) moon.Turn(step / Time.fixedDeltaTime);
            if (Time.time >= nextGhost)
            {
                nextGhost = Time.time + 0.04f;
                Afterimage(moon.Art, new Color(Frost.r, Frost.g, Frost.b, 0.45f));
            }
            yield return new WaitForFixedUpdate();
        }
    }

    // Fang of the Oath: Kuon howls and she crosses her blades, and their spirit leaves as one bolt in the shape of a
    // wolf's head that turns with the ronin. Parried, it goes back at Kuon, a heavy blow that dazes it. Cast while Kuon
    // is busy with a move of its own, or once it has fallen, she casts it alone, and parried it breaks her stance.
    IEnumerator OathFang()
    {
        Speak(BossLine.OathFang);
        // Kuon joins in only if she could call it from its hunt.
        bool together = borrowed;
        if (together)
        {
            yield return wolf.Settle(wolf.Home);
            wolf.PlayHowl();
            wolf.Glare(DrawAim);
            Sfx.Play(Sfx.WolfHowl, 0.8f, 1.1f);
        }
        PlayAction(art.parry);
        Vector3 Source() => together ? Vector3.Lerp(Hands, WolfMouth, 0.5f) : Hands;
        yield return LockOn(Source(), DrawAim, Cyan);

        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.6f);
        Arrow fang = LooseFang(Source(), together ? wolf.transform : transform);

        // Kuon holds its ground at her side until the fang has struck and anything parried back has reached it.
        if (together)
        {
            for (float t = 0; fang != null && t < 3f; t += Time.deltaTime) yield return null;
            yield return Wait(1.4f);
        }
        else yield return Wait(0.5f);
    }

    // The Fang itself: a great spirit wolf lunging at the ronin from `from`, bright as their oath and trailing ghosts
    // of itself, steered onto him so it can only be parried; parried, it flies home to `home`.
    Arrow LooseFang(Vector3 from, Transform home)
    {
        Fx.Play(Fx.Impact, from, Cyan, 2.4f);
        Sfx.Play(Sfx.WolfGrowl, 1f, 0.9f);
        CameraShake.Add(0.3f);
        Arrow fang = Fire(from, (ronin.Bounds.center - from).normalized * DrawSpeed);
        WolfShot(fang, 2.2f, Color.white, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
        fang.MakeUnbreakable();
        fang.MakeReflectable(home, setPiece: true);
        StartCoroutine(Pursue(fang, DrawSpeed * ShotSpeed));
        return fang;
    }

    // Kuon's own Fang, once she is down and it fights enraged: the oath bolt from its jaws alone, behind the same
    // lock-on. Parried, it goes home to Kuon, a heavy blow that dazes it.
    IEnumerator WolfFang()
    {
        yield return wolf.Settle(wolf.Home);
        wolf.Hold();
        wolf.PlayHowl();
        wolf.Glare(DrawAim + 0.3f);
        Sfx.Play(Sfx.WolfHowl, 0.9f, 1.2f);
        yield return LockOn(WolfMouth, DrawAim, Cyan);
        Arrow fang = LooseFang(WolfMouth, wolf.transform);
        // It holds its ground until the fang has struck, so a parry sent home finds it.
        for (float t = 0; fang != null && t < 3f; t += Time.deltaTime) yield return null;
        yield return WolfWait(1f);
        wolf.Release();
    }

    // A lock-on reticle that settles over the ronin from wide, following him, turning and brightening with a
    // quickening ring, then flashes white and holds still a moment before the draw: the tell for a blow that must be
    // parried. A spark at `from` shows where the blow will come from.
    IEnumerator LockOn(Vector3 from, float seconds, Color colour)
    {
        ElementFx.Glow(from, colour, 0.9f, 3f, seconds, transform);
        Sfx.Play(Sfx.BladeRing, 0.7f, 0.8f);
        Sprite[] frames = Fx.Frames(Fx.ShiroLockOn);
        SpriteRenderer sight = Glowing("LockOn", frames != null ? frames[0] : ProceduralSprites.Glow, 58);
        if (frames != null) sight.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 12f);
        // The reticle art is this many world units across at scale 1 (64 px at 48 px a unit).
        float art = frames != null ? 64f / 48f : 1f;
        float rang = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime * Tempo)
        {
            float k = t / seconds;
            sight.transform.position = ronin.Bounds.center + Vector3.back * 0.3f;
            sight.transform.localScale = Vector3.one * Mathf.Lerp(4.5f, 2.2f, Mathf.SmoothStep(0, 1, k)) / art;
            sight.transform.rotation = Quaternion.Euler(0, 0, -90f * k);
            sight.color = new Color(colour.r, colour.g, colour.b, 0.35f + 0.6f * k);
            if (k > 0.4f && t - rang > Mathf.Lerp(0.2f, 0.08f, k))
            {
                rang = t;
                Sfx.Play(Sfx.BladeRing, 0.25f, 1.2f + k);
            }
            yield return null;
        }
        // The lock: it snaps tight and flashes white; this is the beat to parry on.
        sight.transform.localScale = Vector3.one * 1.9f / art;
        sight.color = Color.white;
        Fx.Play(Fx.Impact, ronin.Bounds.center, Color.white, 0.9f);
        Sfx.Play(Sfx.BladeRing, 0.6f, 2f);
        yield return Wait(0.18f);
        Drop(sight);
    }

    // Moonfall, her ultimate once Kuon has fallen (or while it's too busy for the Eclipse Fang): under the spirit moon
    // three waves of ice pillars ripple down across eight lanes, four enraged, each leaving a pair empty, and each
    // wave's pair one lane over from the last so one lane always carries through. Enraged, each wave also brings down
    // a glacier spire well clear of the gap. Then she stands spent, open to double damage.
    IEnumerator Moonfall()
    {
        Speak(BossLine.BladeRain);
        ultimate = true;
        yield return Glide(CameraShake.Center.x);
        SpriteRenderer moon = Moon(anchor + Vector3.up * 1.2f);
        Sfx.Play(Sfx.MistSwirl, 1f, 0.6f);
        CameraShake.Add(0.3f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            moon.color = new Color(0.6f, 0.9f, 1f, 0.55f * t / 0.8f);
            yield return null;
        }

        List<float> lanes = Lanes(8);
        int gap = Random.Range(0, lanes.Count - 1);
        float hang = PillarHang;
        int waves = enraged ? 4 : 3;
        for (int wave = 0; wave < waves; wave++)
        {
            if (wave > 0)
            {
                int step = Random.value < 0.5f ? -1 : 1;
                if (gap + step < 0 || gap + step > lanes.Count - 2) step = -step;
                gap += step;
            }
            float warning = wave == 0 ? 0.8f : 0.6f;
            int spire = -1;
            if (enraged)
            {
                var away = new List<int>();
                for (int i = 0; i < lanes.Count; i++)
                {
                    if (i < gap - 1 || i > gap + 2) away.Add(i);
                }
                if (away.Count > 0) spire = away[Random.Range(0, away.Count)];
            }
            var tells = new List<SpriteRenderer>();
            var pillars = new List<SpriteRenderer>();
            var falling = new List<float>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (i == gap || i == gap + 1) continue;
                if (i == spire)
                {
                    StartCoroutine(Spire(lanes[i], warning));
                    continue;
                }
                falling.Add(lanes[i]);
                tells.Add(LaneWarning(lanes[i]));
                pillars.Add(PillarPicture(new Vector3(lanes[i], hang, 0)));
            }
            PlayAction(art.parry);
            Sfx.Play(Sfx.BladeRing, 0.7f, 0.9f + wave * 0.1f);
            yield return Brighten(tells, warning);
            for (int i = 0; i < falling.Count; i++)
            {
                Drop(pillars[i]);
                IcePillar(new Vector3(falling[i], hang, 0), 13f);
            }
            RemoveTells(tells);
            Sfx.Play(Sfx.GhostSwordFall, 1f, 0.8f);
            yield return Wait(0.35f);
        }
        transient.Remove(moon.gameObject);
        Fader.Fade(moon, 0.8f);
        ultimate = false;

        exposed = true;
        PlayIdle();
        yield return Wait(2f);
        exposed = false;
    }

    // Glacier, from her second phase: she raises her blades and two glacier spires, far too big to parry, come down
    // on the ronin's lane and one beside it under long dark warnings. Enraged, a third follows him to wherever he
    // stepped.
    IEnumerator Glacier()
    {
        PlayAction(art.parry);
        ElementFx.Glow(Hands, Frost, 0.9f, 3f, 0.8f, transform);
        Sfx.Play(Sfx.BladeRing, 0.8f, 0.7f);
        float tell = enraged ? 0.95f : 1.15f;
        float lo = LeftEdge + 2f, hi = RightEdge - 2f;
        float x = Mathf.Clamp(ronin.Bounds.center.x, lo, hi);
        float beside = x < CameraShake.Center.x ? x + 4.2f : x - 4.2f;
        StartCoroutine(Spire(x, tell));
        StartCoroutine(Spire(Mathf.Clamp(beside, lo, hi), tell));
        yield return Wait(tell + 0.3f);
        if (enraged) yield return Spire(Mathf.Clamp(ronin.Bounds.center.x, lo, hi), 0.8f);
        yield return Wait(0.4f);
    }

}
