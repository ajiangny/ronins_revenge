using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The second phase with Kuon: the wolf's moves, their shared ones, and Kuon's Maw.
public partial class Shirogane
{
    // --- Phase 2: the wolf ---
    // Kuon's own moves (charge, howl, pack, lances, Maw) run in Hunt on its own clock, so they leave her to hers and
    // carry on through her stagger. Each has a harder version for when it fights enraged.

    // Where Kuon perches over a lane: on her row, clear of the HUD (lower while she kneels, so it stays by her).
    float WolfRowY => anchor.y + (Down ? 0.55f : 0.3f);

    // A spot on her row for Kuon to stand: its own ground, well apart from hers.
    public Vector3 ProwlSpot()
    {
        float range = HalfWidth - 2.5f;
        float centre = CameraShake.Center.x;
        float y = WolfRowY - 0.1f;
        for (int i = 0; i < 8; i++)
        {
            float x = centre + Random.Range(-range, range);
            if (Down || Mathf.Abs(x - anchor.x) >= 3.5f) return new Vector3(x, y, 0);
        }
        return new Vector3(Mathf.Clamp(centre * 2f - anchor.x, centre - range, centre + range), y, 0);
    }

    // Spirit Wolf Charge: the wolf melts into mist and reappears over the ronin's lane, growling, a warning line
    // under it; then it comes down the lane like a bolt and dissolves where it lands, and returns to its ground.
    // Enraged, it perches again at once over wherever he went and comes down a second time.
    IEnumerator WolfCharge()
    {
        Command(BossLine.WolfCharge, 0.5f);
        yield return wolf.Vanish();
        yield return WolfWait(0.25f);
        int charges = wolf.Enraged ? 2 : 1;
        for (int c = 0; c < charges; c++)
        {
            float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.2f, RightEdge - 1.2f);
            Vector3 perch = new Vector3(x, WolfRowY, 0);
            yield return wolf.AppearAt(perch, true);
            Sfx.Play(Sfx.WolfGrowl, 0.9f, c == 0 ? 1f : 1.2f);
            float tell = c == 0 ? 0.9f : 0.6f;
            BeamTell lane = MarkBeam(x, ChargeHalfWidth, tell, () => perch);
            yield return WolfWait(tell);
            lane.Remove();

            wolf.Hide();
            Charge(perch, wolf.Enraged ? 16f : 14f, 1.6f);
            Puff(perch, 1.6f);
            Sfx.Play(Sfx.MistSwirl, 0.7f, 0.8f);
            yield return WolfWait(c < charges - 1 ? 0.35f : 0.5f);
        }
        yield return wolf.ReturnHome();
    }

    // Howl of the Departed: the wolf's eyes and jaws burn bright, then its howl sends seven orbs down in a wide
    // fan, and a second wave a beat later between the first's paths. Low on health a third wave follows; enraged,
    // three waves of nine.
    IEnumerator Howl()
    {
        Command(BossLine.Howl, 0.5f);
        yield return wolf.Settle(wolf.Home);
        wolf.Hold();
        wolf.Glare(0.8f);
        ElementFx.Glow(WolfMouth, Cyan, 0.6f, 2.4f, 0.8f, wolf.transform);
        yield return WolfWait(wolf.Enraged ? 0.6f : 0.8f);

        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 1f, 1f);
        CameraShake.Add(0.15f);
        int waves = wolf.Enraged || wolf.HealthFraction < 0.3f ? 3 : 2;
        int orbs = wolf.Enraged ? 9 : 7;
        for (int wave = 0; wave < waves; wave++)
        {
            if (wave > 0) yield return WolfWait(wolf.Enraged ? 0.45f : 0.6f);
            OrbFan(orbs, wolf.Enraged ? 66f : 60f, 4.5f, wave % 2 == 1);
        }
        yield return WolfWait(0.3f);
        wolf.Release();
    }

    // One fan of orbs from the wolf's jaws, aimed straight down; an offset fan fills the last one's gaps.
    void OrbFan(int count, float spread, float speed, bool offset)
    {
        float step = 2f * spread / (count - 1f);
        var fan = new List<Arrow>();
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-spread, spread, i / (count - 1f)) + (offset ? step / 2f : 0f);
            fan.Add(Orb(WolfMouth, Quaternion.Euler(0, 0, angle) * Vector3.down * speed));
        }
        IgnoreEachOther(fan);
    }

    // Twin Spirit Assault: she takes the left of the field and the wolf the right; both glow, then her crescents
    // sweep down and to the right while its orbs sweep down and to the left, crossing in the middle with gaps.
    IEnumerator TwinAssault()
    {
        if (Random.value < 0.6f) Speak(BossLine.TwinAssault);
        float quarter = HalfWidth * 0.5f;
        bool herLeft = anchor.x <= CameraShake.Center.x;
        float herX = CameraShake.Center.x + (herLeft ? -quarter : quarter);
        Vector3 wolfSpot = new Vector3(CameraShake.Center.x + (herLeft ? quarter : -quarter), anchor.y + 0.2f, 0);
        yield return wolf.Vanish();
        yield return Glide(herX);
        yield return wolf.AppearAt(wolfSpot, true);

        ElementFx.Glow(Hands, Cyan, 0.8f, 2.6f, 0.6f, transform);
        wolf.Glare(0.6f);
        Sfx.Play(Sfx.BladeRing, 0.6f, 1.2f);
        yield return Wait(0.6f);

        PlayAction(art.attack);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 0.7f, 1.2f);
        // Positive angles turn a downward shot to the right, negative to the left.
        float inward = herLeft ? 1f : -1f;
        WolfSlash(Hands, Quaternion.Euler(0, 0, 40f * inward) * Vector3.down, 2.6f);
        var volley = new List<Arrow>();
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(20f, 60f, i / 4f);
            volley.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle * inward) * Vector3.down * 7.5f));
            volley.Add(Orb(WolfMouth, Quaternion.Euler(0, 0, -angle * inward) * Vector3.down * 6.5f));
            IgnoreEachOther(volley);
            yield return Wait(0.15f);
        }
        yield return Wait(0.3f);
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    // Phantom Wolf Pack: three smaller ghost wolves form over three of six lanes, each with its warning line,
    // and charge down one after another. Never the same three lanes twice running. Enraged, four pups over seven
    // lanes, quicker.
    IEnumerator WolfPack()
    {
        Command(BossLine.WolfPack, 0.5f);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 0.6f, 1.4f);
        List<float> lanes = Lanes(wolf.Enraged ? 7 : 6);
        int count = wolf.Enraged ? 4 : 3;
        var picks = new List<int>();
        while (picks.Count < count)
        {
            int lane = Random.Range(0, lanes.Count);
            if (!picks.Contains(lane) && !(lastPackLanes.Contains(lane) && Random.value < 0.7f)) picks.Add(lane);
        }
        lastPackLanes.Clear();
        foreach (int lane in picks) lastPackLanes.Add(lane);

        float perchY = WolfRowY;
        float tell = wolf.Enraged ? 0.8f : 0.9f;
        var tells = new List<BeamTell>();
        var pups = new List<SpriteRenderer>();
        foreach (int lane in picks)
        {
            Vector3 perch = new Vector3(lanes[lane], perchY, 0);
            tells.Add(MarkBeam(lanes[lane], PupHalfWidth, tell, () => perch));
            pups.Add(wolf.Silhouette(perch, 0.6f, wolfTransient));
            Puff(perch, 1.2f);
        }
        yield return WolfWait(tell);

        for (int i = 0; i < picks.Count; i++)
        {
            if (pups[i] != null)
            {
                wolfTransient.Remove(pups[i].gameObject);
                Destroy(pups[i].gameObject);
            }
            tells[i].Remove();
            Charge(new Vector3(lanes[picks[i]], perchY, 0), wolf.Enraged ? 13f : 12f, 1.1f);
            Sfx.Play(Sfx.WolfGrowl, 0.6f, 1.3f);
            yield return WolfWait(wolf.Enraged ? 0.25f : 0.35f);
        }
        yield return WolfWait(0.3f);
    }

    // Eclipse Fang, their strongest attack together: she takes the centre, the wolf looms up huge behind her under
    // a spectral blue moon; two crescent waves, then the wolf's beam straight down a marked lane, then a last
    // spread of swords. Afterwards she stands spent for two seconds, open to double damage.
    IEnumerator EclipseFang()
    {
        Speak(BossLine.EclipseFang);
        ultimate = true;
        yield return wolf.Vanish();
        yield return Glide(CameraShake.Center.x);
        yield return wolf.AppearAt(anchor + new Vector3(0f, 0.9f, 0f), true);
        SpriteRenderer moon = Moon(anchor + Vector3.up * 1.2f);
        wolf.StartCoroutine(wolf.Grow(1.8f, 0.8f));
        Sfx.Play(Sfx.WolfHowl, 1f, 0.8f);
        CameraShake.Add(0.3f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            moon.color = new Color(0.6f, 0.9f, 1f, 0.55f * t / 0.8f);
            yield return null;
        }

        // Two crescent waves, the second's blades between the first's, so the safe spot moves.
        for (int wave = 0; wave < 2; wave++)
        {
            PlayAction(art.attack);
            Sfx.Play(Sfx.SwordSlash, 0.9f, 0.65f);
            WolfSlash(Hands, Vector3.down, 3f);
            var fan = new List<Arrow>();
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(-44f, 44f, i / 4f) + (wave == 1 ? 11f : 0f);
                fan.Add(Crescent(Hands, Quaternion.Euler(0, 0, angle) * Vector3.down * 7.5f));
            }
            IgnoreEachOther(fan);
            yield return Wait(0.8f);
        }

        // The beam: the lane is marked where the ronin stands as Kuon starts to gather it, so stepping off it is the
        // dodge. Kuon draws the orb in and swallows it as in its own Maw, then looses the beam.
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.2f, RightEdge - 1.2f);
        BeamTell warning = MarkBeam(x, 1.1f, 1.3f);
        wolf.Glare(1.4f);
        yield return GatherOrb(() => WolfMouth, 0.8f, () => false);
        yield return SwallowOrb(0.2f);
        warning.Remove();
        yield return MawBeam(x, 1.1f, 0.5f);

        // The closing spread of swords, then she is spent.
        PlayAction(art.parry);
        Sfx.Play(Sfx.GhostSwordFall, 1f, 0.8f);
        var swords = new List<Arrow>();
        for (int i = 0; i < 7; i++)
        {
            float angle = Mathf.Lerp(-45f, 45f, i / 6f);
            swords.Add(GhostSword(Hands, Quaternion.Euler(0, 0, angle) * Vector3.down * 9f));
        }
        IgnoreEachOther(swords);
        wolf.StartCoroutine(wolf.Grow(1f, 0.6f));
        transient.Remove(moon.gameObject);
        Fader.Fade(moon, 0.8f);
        ultimate = false;

        exposed = true;
        PlayIdle();
        yield return Wait(2f);
        exposed = false;
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    // --- Kuon's Maw ---

    // Kuon's own ultimate, drawn like a tailed beast's bomb. It plants itself over the field and rears back, and motes
    // of spirit light, pale and deep, stream in from all round and spiral into an orb at its jaws that swells and
    // shakes the ground. The lane locks where the ronin stands as the orb is crushed to a white-hot point and
    // swallowed; a beat of silence, then the beam comes down that lane. Hit Kuon hard enough while it gathers and
    // the orb bursts in its own jaws. Enraged, the beam is wider and drags after him.
    IEnumerator SpiritMaw()
    {
        yield return wolf.Vanish();
        // It stands on her row, clear of the HUD, over the ronin's side of the field.
        float x = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 2f, RightEdge - 2f);
        yield return wolf.AppearAt(new Vector3(x, WolfRowY - 0.1f, 0), true);
        yield return MawStrike(MawGather, wolf.Enraged ? 1.8f : 1.4f, wolf.Enraged ? 1f : 0.8f, wolf.Enraged ? 2.2f : 0f);
        if (wolf.IsBroken) yield break;
        yield return WolfWait(0.4f);
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    // The Maw from where Kuon stands: the gather (which a hard enough beating bursts in its jaws), the lane locked on
    // the ronin, the swallow and the beam, `halfWidth` wide for `seconds`, dragging after him at `sweep`.
    IEnumerator MawStrike(float gather, float halfWidth, float seconds, float sweep)
    {
        wolf.PlayMaw(gather + 0.3f);
        wolf.Glare(gather + 0.2f);
        Sfx.Play(Sfx.WolfGrowl, 1f, 0.7f);
        wolf.StartCoroutine(wolf.Grow(1.3f, 0.5f));
        int before = wolf.Health;
        yield return GatherOrb(() => WolfMouth, gather, () => wolf.IsBroken || before - wolf.Health >= MawBreakDamage);
        if (orbBurst)
        {
            if (!wolf.IsBroken)
            {
                wolf.Backfire(MawBackfireDamage);
                wolf.StartCoroutine(wolf.Grow(1f, 0.4f));
            }
            CameraShake.Add(0.5f);
            Sfx.Play(Sfx.FireBlast, 0.9f, 0.8f);
            Sfx.Play(Sfx.WolfHowl, 0.8f, 1.6f);
            while (wolf.Dazed) yield return null;
            yield break;
        }

        float lane = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1.4f, RightEdge - 1.4f);
        BeamTell warning = MarkBeam(lane, halfWidth, 0.8f);
        yield return SwallowOrb(0.5f);
        warning.Remove();
        yield return MawBeam(lane, halfWidth, seconds, sweep: sweep);
        Fx.Play(Fx.Explosion, new Vector3(beamX, GroundY, 0), Cyan, 3f);
        CameraShake.Add(0.4f);
        wolf.PlayIdle();
        wolf.StartCoroutine(wolf.Grow(1f, 0.4f));
    }

    // Spirit Lances: Kuon's quick beams. It stays on its ground and spits thin beams one after another, each down the
    // lane where the ronin stands as its warning opens, so standing still is the one way to be hit; four near its
    // end, five enraged, with shorter warnings. Each is the Maw's beam in small: a short trapezoid warning, a burst
    // at the jaws and a splash.
    IEnumerator SpiritLances()
    {
        yield return wolf.Settle(wolf.Home);
        wolf.Hold();
        wolf.Glare(0.5f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 1.3f);
        int lances = wolf.Enraged ? 5 : wolf.HealthFraction < 0.5f ? 4 : 3;
        float tell = wolf.Enraged ? 0.4f : LanceTell;
        for (int i = 0; i < lances; i++)
        {
            float lane = Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1f, RightEdge - 1f);
            BeamTell warning = MarkBeam(lane, LanceHalfWidth, tell);
            ElementFx.Glow(WolfMouth, Cyan, 0.5f, 1.8f, tell, wolf.transform);
            yield return WolfWait(tell);
            warning.Remove();
            wolf.PlayHowl();
            yield return MawBeam(lane, LanceHalfWidth, LanceBurn, heavy: false);
            yield return WolfWait(0.12f);
        }
        wolf.Release();
    }

    // A lance's half width, how long its warning opens, and how long it burns.
    const float LanceHalfWidth = 0.7f;
    const float LanceTell = 0.5f;
    const float LanceBurn = 0.25f;

    // Frozen Howl, Kuon's ultimate once she is down and it fights for both of them: it takes the middle of the field
    // and gathers a longer Maw while two glacier spires come down on the ronin one after the other, then the beam
    // comes down his lane, wide, dragging after him. Afterwards it stands spent and reeling for two seconds, open to
    // double damage.
    IEnumerator FrozenHowl()
    {
        yield return wolf.Vanish();
        yield return wolf.AppearAt(new Vector3(CameraShake.Center.x, WolfRowY - 0.1f, 0), true);
        wolf.PlayHowl();
        Sfx.Play(Sfx.WolfHowl, 1f, 0.75f);
        CameraShake.Add(0.3f);
        yield return WolfWait(0.4f);
        StartCoroutine(SpireVolley(2, 0.9f, 0.8f));
        yield return MawStrike(FrozenGather, 2f, 1.2f, 2.6f);
        if (wolf.IsBroken) yield break;
        // Spent: it reels where it stands.
        wolf.Daze(2f);
        yield return WolfWait(2f);
        yield return wolf.Vanish();
        yield return wolf.ReturnHome();
    }

    const float FrozenGather = 2.2f;

    // Kuon's spires: `count` of them on wherever the ronin stands, `gap` seconds apart.
    IEnumerator SpireVolley(int count, float tell, float gap)
    {
        for (int i = 0; i < count && WolfFights; i++)
        {
            StartCoroutine(Spire(Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 2f, RightEdge - 2f), tell, wolfs: true));
            yield return WolfWait(gap);
        }
    }

    // How long Kuon's Maw gathers, how much damage on Kuon meanwhile bursts the orb, and what the burst costs it.
    const float MawGather = 1.6f;
    const int MawBreakDamage = 6;
    const int MawBackfireDamage = 10;

    // The orb at Kuon's jaws while it gathers (the pixel orb, or a code glow without it), the bloom behind it and,
    // drawn in code only, its white core; null when none is out.
    SpriteRenderer orb, orbHalo, orbCore;
    // Set when the gather was cut short by burst() returning true, the orb bursting where it hung.
    bool orbBurst;
    // The dark laid over the arena while Kuon gathers, so the orb and the beam blaze against it.
    SpriteRenderer dimmer;

    // The orb's pixel art is this many world units across at scale 1 (64 px at 48 px a unit).
    const float OrbArtSize = 64f / 48f;

    // Like a tailed beast's bomb: the arena darkens, streaks of light and shadow spiral in from all round to an
    // orb at `at` that swells and churns as they arrive, and the ground shakes harder as it grows.
    IEnumerator GatherOrb(System.Func<Vector3> at, float seconds, System.Func<bool> burst)
    {
        orbBurst = false;
        RemoveOrb();
        Dim(0.4f, 0.4f);
        orbHalo = Glowing("OrbHalo", ProceduralSprites.SoftGlow, 56, wolfs: true);
        Sprite[] frames = Fx.Frames(Fx.ShiroMawOrb);
        orb = Glowing("SpiritOrb", frames != null ? frames[0] : ProceduralSprites.SoftGlow, 58, wolfs: true);
        if (frames != null) orb.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 14f);
        else orbCore = Glowing("OrbCore", ProceduralSprites.Glow, 59, wolfs: true);

        const int count = 36;
        var motes = new SpriteRenderer[count];
        var offsets = new Vector3[count];
        var delays = new float[count];
        var spins = new float[count];
        for (int i = 0; i < count; i++)
        {
            // Streaks, pale and deep in turn: light and shadow drawn in together.
            motes[i] = Glowing("Mote", ProceduralSprites.White, 59, wolfs: true);
            motes[i].color = i % 2 == 0 ? new Color(Frost.r, Frost.g, Frost.b, 0f) : new Color(0.2f, 0.4f, 0.95f, 0f);
            offsets[i] = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
            delays[i] = Random.Range(0f, seconds * 0.65f);
            spins[i] = Random.Range(140f, 260f) * (Random.value < 0.5f ? -1f : 1f);
        }
        Sfx.Play(Sfx.LanternCharge, 0.9f, 0.8f);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 0.5f);
        float nextRumble = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            Vector3 centre = at() + Vector3.back * 0.4f;
            for (int i = 0; i < count; i++)
            {
                float m = Mathf.Clamp01((t - delays[i]) / (seconds * 0.35f));
                Color c = motes[i].color;
                c.a = m > 0f && m < 1f ? 0.95f : 0f;
                motes[i].color = c;
                if (c.a == 0f) continue;
                // Each streak spirals in, quickening and stretching as it nears the orb.
                Vector3 offset = Quaternion.Euler(0, 0, spins[i] * m) * offsets[i] * (1f - m * m);
                motes[i].transform.position = centre + offset;
                motes[i].transform.rotation = Quaternion.FromToRotation(Vector3.right, -offset);
                motes[i].transform.localScale = new Vector3(0.25f + 0.9f * m, 0.09f, 1f);
            }
            float size = Mathf.Lerp(0.5f, 3.6f, Mathf.SmoothStep(0, 1, k)) * (1f + 0.06f * Mathf.Sin(t * 40f));
            orbHalo.transform.position = centre + Vector3.forward * 0.05f;
            orbHalo.transform.localScale = Vector3.one * size * 2.8f;
            orbHalo.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.2f + 0.25f * k);
            orb.transform.position = centre;
            if (frames != null)
            {
                orb.transform.localScale = Vector3.one * size / OrbArtSize;
                orb.color = Color.white;
            }
            else
            {
                orb.transform.localScale = Vector3.one * size * 1.6f;
                orb.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f);
                orbCore.transform.position = centre + Vector3.back * 0.05f;
                orbCore.transform.localScale = Vector3.one * size * 0.7f;
                orbCore.color = new Color(0.9f, 1f, 1f, 0.9f);
            }
            if (t >= nextRumble)
            {
                nextRumble = t + 0.12f;
                CameraShake.Add(0.03f + 0.06f * k);
            }
            if (burst())
            {
                orbBurst = true;
                break;
            }
            yield return null;
        }
        foreach (SpriteRenderer mote in motes) Drop(mote);
        if (orbBurst)
        {
            Vector3 where = orb.transform.position;
            if (Fx.Play(Fx.ShiroMawBurst, where, Color.white, 2.4f, null, 58) == null) Fx.Play(Fx.Explosion, where, Cyan, 2.4f);
            Fx.Play(Fx.Explosion, where, Cyan, 2f);
            RemoveOrb();
            Dim(0f, 0.5f);
        }
    }

    // The orb crushes down to a white-hot point and is swallowed, and for a beat everything goes quiet.
    IEnumerator SwallowOrb(float beat)
    {
        if (orb == null) yield break;
        Vector3 at = orb.transform.position;
        Vector3 from = orb.transform.localScale;
        float halo = orbHalo.transform.localScale.x;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            // It bulges once before it gives, then crushes down fast.
            float squeeze = k < 0.25f ? 1f + 0.15f * (k / 0.25f) : Mathf.Lerp(1.15f, 0.12f, Mathf.SmoothStep(0, 1, (k - 0.25f) / 0.75f));
            orb.transform.localScale = from * squeeze;
            orbHalo.transform.localScale = Vector3.one * halo * squeeze;
            orbHalo.color = new Color(Frost.r, Frost.g, Frost.b, 0.45f + 0.4f * k);
            if (orbCore != null) orbCore.transform.localScale = Vector3.one * from.x * 0.45f * squeeze;
            yield return null;
        }
        RemoveOrb();
        Fx.Play(Fx.Impact, at, Color.white, 1.8f);
        Sfx.Play(Sfx.WolfGrowl, 0.8f, 0.6f);
        wolf.Glare(beat + 0.3f);
        wolf.StartCoroutine(wolf.Gulp());
        // The held breath: the arena goes darker still for the beat before the beam.
        Dim(0.55f, beat);
        yield return WolfWait(beat);
    }

    // The beam segment's pixel art is this many world units across at scale 1 (34 px at 48 px a unit).
    const float BeamArtWidth = 34f / 48f;

    // The beam from Kuon's jaws down a lane; it burns the ronin while he stands within halfWidth of it. It bursts
    // from the jaws with a shockwave, snaps open wide, burns pulsing with spirit light streaming down it and
    // splashing where it lands, then pinches shut and the arena's dark lifts. With a sweep its foot drags after him
    // at that speed, slower than he runs.
    // A light one (Kuon's quick lances) leaves the arena lit and shakes it less, and keeps its move's name; a heavy one
    // is the Maw, whichever move fires it (Spirit Maw, the Eclipse Fang, the Frozen Howl).
    IEnumerator MawBeam(float x, float halfWidth, float seconds, bool heavy = true, float sweep = 0f)
    {
        Vector3 from = WolfMouth;
        Vector3 to = new Vector3(x, GroundY, 0);
        float w = halfWidth / 1.1f;
        Sprite[] frames = Fx.Frames(Fx.ShiroMawBeam);

        if (Fx.Play(Fx.ShiroMawBurst, from, Color.white, 2.6f * w, null, 58) == null) Fx.Play(Fx.Explosion, from, Cyan, 2.4f * w);
        SpriteRenderer glow = Bar(from, to, 3.4f * w, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f), 53);
        glow.sprite = ProceduralSprites.Glow;
        WolfOwns(glow.gameObject);
        SpriteRenderer body = null, core = null, beam = null;
        float beamScale = 2.7f * w / BeamArtWidth;
        if (frames != null)
        {
            beam = Glowing("MawBeam", frames[0], 55, wolfs: true);
            beam.color = Color.white;
            beam.drawMode = SpriteDrawMode.Tiled;
            beam.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 16f);
        }
        else
        {
            body = Bar(from, to, 1.1f * w, new Color(0.6f, 0.95f, 1f, 0.9f), 55);
            core = Bar(from, to, 0.35f * w, Color.white, 56);
            WolfOwns(body.gameObject);
            WolfOwns(core.gameObject);
        }
        FxPlayer flare = Fx.Play(Fx.ShiroBeamFlare, from, Color.white, 2f * w, wolf.transform, 57);
        if (flare != null) wolfTransient.Add(flare.gameObject);
        FxPlayer splash = Fx.Play(Fx.ShiroMawHit, to, Color.white, 2.4f * w, null, 57);
        if (splash != null) wolfTransient.Add(splash.gameObject);
        Sfx.PlayLoop(Sfx.SpiritBeam, heavy ? 0.9f : 0.6f, heavy ? 0.9f : 1.3f);
        if (heavy)
        {
            Sfx.Play(Sfx.FireBlast, 0.8f, 0.7f);
            CameraShake.Add(0.6f * w);
            Dim(0.45f, 0.1f);
        }
        else CameraShake.Add(0.15f);

        float nextSpark = 0f, nextRumble = 0f;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            if (sweep > 0f) x = Mathf.MoveTowards(x, Mathf.Clamp(ronin.Bounds.center.x, LeftEdge + 1f, RightEdge - 1f), sweep * Time.deltaTime);
            to = new Vector3(x, GroundY, 0);
            Vector3 delta = to - from;
            // Snaps open past full width, settles, pulses, and pinches shut over its last moment.
            float open = t < 0.1f ? Mathf.Lerp(0.2f, 1.25f, t / 0.1f)
                : t < 0.2f ? Mathf.Lerp(1.25f, 1f, (t - 0.1f) / 0.1f)
                : 1f + 0.06f * Mathf.Sin(Time.time * 45f);
            open *= Mathf.Clamp01((seconds - t) / Mathf.Min(0.15f, seconds * 0.4f));
            if (beam != null)
            {
                beam.transform.position = (from + to) / 2f + Vector3.back * 0.2f;
                beam.transform.rotation = Quaternion.FromToRotation(Vector3.down, delta);
                beam.transform.localScale = new Vector3(beamScale * open, beamScale, 1f);
                beam.size = new Vector2(BeamArtWidth, delta.magnitude / beamScale);
            }
            Stretch(body, from, to, 1.1f * w * open);
            Stretch(core, from, to, 0.35f * w * open);
            Stretch(glow, from, to, 3.4f * w * open);
            if (splash != null) splash.transform.position = to;
            if (Mathf.Abs(ronin.Bounds.center.x - x) < halfWidth) Strike(new Vector3(x, ronin.Bounds.center.y, 0), heavy ? "Shirogane_KuonMaw" : null);
            if (Time.time >= nextSpark)
            {
                // Sparks thrown off the beam's edges along its length.
                nextSpark = Time.time + 0.06f;
                Vector3 along = Vector3.Lerp(from, to, Random.value);
                Vector3 side = Vector3.Cross(delta.normalized, Vector3.forward) * (halfWidth * 0.9f * (Random.value < 0.5f ? -1f : 1f));
                Fx.Play(Fx.Impact, along + side, Cyan, 0.5f * w);
            }
            if (heavy && Time.time >= nextRumble)
            {
                nextRumble = Time.time + 0.1f;
                CameraShake.Add(0.08f * w);
            }
            yield return null;
        }
        beamX = x;
        Sfx.Stop(Sfx.SpiritBeam);
        foreach (SpriteRenderer part in new[] { glow, body, core, beam }) Drop(part);
        if (flare != null) flare.Stop();
        if (splash != null) splash.Stop();
        if (heavy) Dim(0f, 0.6f);
    }

    // Where the last beam ended, after any sweep.
    float beamX;

    // The warning for a Maw beam: a trapezoid from Kuon's jaws, as wide there as the beam, to where the beam will
    // land, much wider there. Its outline shows at once; over three ringing beats its fill pours down from the jaws
    // to the ground, brightening on each, while the wide foot draws in until the shape is a rectangle, the beam's
    // own, flickering white as the beam fires into it. It follows the jaws as Kuon moves.
    class BeamTell
    {
        public GameObject shape;
        public Mesh mesh;
        public Coroutine opening;
        public Shirogane owner;

        public void Remove()
        {
            if (opening != null) owner.StopCoroutine(opening);
            if (shape == null) return;
            owner.wolfTransient.Remove(shape);
            UnityEngine.Object.Destroy(shape);
            UnityEngine.Object.Destroy(mesh);
        }
    }

    // The rectangle it ends as is a little wider than the beam's hit, so standing just outside it is safe; its foot
    // starts this many times as wide and shrinks to it.
    const float TellMargin = 1.15f;
    const float TellFoot = 2.2f;

    // From Kuon's jaws unless `from` says otherwise (a lane charge's perch, a pup's).
    BeamTell MarkBeam(float x, float halfWidth, float seconds, System.Func<Vector3> from = null)
    {
        var tell = new BeamTell { owner = this, shape = new GameObject("MawTell"), mesh = new Mesh() };
        tell.mesh.MarkDynamic();
        tell.shape.AddComponent<MeshFilter>().sharedMesh = tell.mesh;
        var renderer = tell.shape.AddComponent<MeshRenderer>();
        // Drawn with the sprites' own material, white, so the vertex colours give it its look.
        renderer.sharedMaterial = sprite.sharedMaterial;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", Texture2D.whiteTexture);
        renderer.SetPropertyBlock(block);
        renderer.sortingOrder = 52;
        wolfTransient.Add(tell.shape);
        tell.opening = StartCoroutine(OpenBeamTell(tell, x, halfWidth * TellMargin, seconds, from ?? (() => WolfMouth)));
        return tell;
    }

    IEnumerator OpenBeamTell(BeamTell tell, float x, float halfWidth, float seconds, System.Func<Vector3> source)
    {
        const float EdgeWidth = 0.12f;
        const int Quads = 5;
        float beatTime = seconds / 3f;
        int beats = 0;
        var vertices = new Vector3[Quads * 4];
        var colours = new Color[Quads * 4];
        var triangles = new int[Quads * 6];
        for (int q = 0; q < Quads; q++)
        {
            int v = q * 4, i = q * 6;
            triangles[i] = v; triangles[i + 1] = v + 1; triangles[i + 2] = v + 2;
            triangles[i + 3] = v; triangles[i + 4] = v + 2; triangles[i + 5] = v + 3;
        }
        for (float t = 0; tell.shape != null; t += Time.deltaTime)
        {
            if (t < seconds && t >= beats * beatTime)
            {
                beats++;
                Sfx.Play(Sfx.BladeRing, 0.45f, 1.5f + 0.2f * beats);
            }
            float filled = Mathf.Clamp01(t / seconds);
            float fill = 0.14f + 0.08f * beats;
            float edge = 0.4f + 0.2f * beats;
            // Full, it burns white and flickers: the beam is coming now.
            Color tint = filled >= 1f ? Color.Lerp(Cyan, Color.white, Mathf.PingPong(t * 12f, 1f)) : Cyan;

            Vector3 from = source();
            Vector3 to = new Vector3(x, GroundY, 0);
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.forward);
            // The jaws end is the beam's width throughout; the wide foot draws in over the beats until the shape is
            // a rectangle, the beam's own, as it fires.
            float top = halfWidth;
            float foot = halfWidth * Mathf.Lerp(TellFoot, 1f, Mathf.SmoothStep(0, 1, filled));
            Vector3 topLeft = from - side * top, topRight = from + side * top;
            Vector3 footLeft = to - side * foot, footRight = to + side * foot;
            // The fill pours down from the jaws, as far as it has got, widening as it goes.
            float pour = Mathf.SmoothStep(0, 1, filled);
            Vector3 frontLeft = Vector3.Lerp(topLeft, footLeft, pour), frontRight = Vector3.Lerp(topRight, footRight, pour);
            Quad(vertices, 0, topLeft, topRight, frontRight, frontLeft);
            // The outline: a strip down each side, one across the foot and one across the jaws, inside the shape.
            Vector3 inTop = side * Mathf.Min(EdgeWidth, top), inFoot = side * EdgeWidth;
            Vector3 down = (to - from).normalized * EdgeWidth;
            Quad(vertices, 4, topLeft, topLeft + inTop, footLeft + inFoot, footLeft);
            Quad(vertices, 8, topRight - inTop, topRight, footRight, footRight - inFoot);
            Quad(vertices, 12, footLeft - down, footRight - down, footRight, footLeft);
            Quad(vertices, 16, topLeft, topRight, topRight + down, topLeft + down);
            for (int i = 0; i < vertices.Length; i++)
            {
                Color c = tint;
                c.a = Mathf.Min(1f, i < 4 ? fill : edge);
                colours[i] = c;
            }
            tell.mesh.vertices = vertices;
            tell.mesh.colors = colours;
            if (tell.mesh.GetIndexCount(0) == 0) tell.mesh.triangles = triangles;
            tell.mesh.RecalculateBounds();
            yield return null;
        }
    }

    static void Quad(Vector3[] vertices, int at, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector3 back = Vector3.back * 0.2f;
        vertices[at] = a + back;
        vertices[at + 1] = b + back;
        vertices[at + 2] = c + back;
        vertices[at + 3] = d + back;
    }

    // Eases the dark over the arena to `alpha` over `seconds`; 0 lifts it and then removes it.
    void Dim(float alpha, float seconds)
    {
        if (dimmer == null)
        {
            if (alpha <= 0f) return;
            dimmer = new GameObject("MawDark").AddComponent<SpriteRenderer>();
            dimmer.sprite = ProceduralSprites.White;
            // Over the floor and its cover's shadows, under every character and shot.
            dimmer.sortingOrder = 1;
            dimmer.color = new Color(0.01f, 0.02f, 0.06f, 0f);
            dimmer.transform.position = new Vector3(CameraShake.Center.x, CameraShake.Center.y, 0f);
            float h = Camera.main.orthographicSize * 2f + 4f;
            dimmer.transform.localScale = new Vector3(h * Camera.main.aspect + 4f, h, 1f);
            wolfTransient.Add(dimmer.gameObject);
        }
        if (dimming != null) StopCoroutine(dimming);
        dimming = StartCoroutine(Dimming(alpha, seconds));
    }

    Coroutine dimming;

    IEnumerator Dimming(float alpha, float seconds)
    {
        float from = dimmer.color.a;
        for (float t = 0; t < seconds && dimmer != null; t += Time.deltaTime)
        {
            Color c = dimmer.color;
            c.a = Mathf.Lerp(from, alpha, t / seconds);
            dimmer.color = c;
            yield return null;
        }
        if (dimmer == null) yield break;
        if (alpha <= 0f) Drop(dimmer);
        else dimmer.color = new Color(dimmer.color.r, dimmer.color.g, dimmer.color.b, alpha);
        dimming = null;
    }

    // A code-drawn glow the current attack owns: hers, or Kuon's.
    SpriteRenderer Glowing(string name, Sprite shape, int order, bool wolfs = false)
    {
        var glow = new GameObject(name).AddComponent<SpriteRenderer>();
        glow.sprite = shape;
        glow.sortingOrder = order;
        glow.color = Color.clear;
        (wolfs ? wolfTransient : transient).Add(glow.gameObject);
        return glow;
    }

    void Drop(SpriteRenderer thing)
    {
        if (thing == null) return;
        transient.Remove(thing.gameObject);
        wolfTransient.Remove(thing.gameObject);
        Destroy(thing.gameObject);
    }

    void RemoveOrb()
    {
        Drop(orb);
        Drop(orbHalo);
        Drop(orbCore);
        orb = orbHalo = orbCore = null;
    }

    // The spectral moon that rises behind them for the Eclipse Fang.
    SpriteRenderer Moon(Vector3 at)
    {
        var moon = new GameObject("SpiritMoon").AddComponent<SpriteRenderer>();
        moon.transform.position = at + Vector3.forward * 0.5f;
        Sprite[] frames = Fx.Frames(Fx.ShiroMoon);
        if (frames != null)
        {
            moon.sprite = frames[0];
            moon.transform.localScale = Vector3.one * (6f / frames[0].bounds.size.x);
            moon.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
        }
        else
        {
            moon.sprite = ProceduralSprites.SoftGlow;
            moon.transform.localScale = Vector3.one * 7f;
        }
        moon.sortingOrder = 2;
        moon.color = Color.clear;
        transient.Add(moon.gameObject);
        return moon;
    }
}
