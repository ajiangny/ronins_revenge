using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Its lasers: the Beam drawn every frame, the rocks that shelter from the Hyper Beam, and the rockfall that brings them.
public partial class Utsusemi
{
    // A laser redrawn every frame: a soft glow under its body, which is its beam art tiled along its length and
    // scrolling (red, or molten orange and wilder once its core opens), or without the art a flickering red bar over a
    // white-hot core. A flare at the source, sparks where it lands and a trail of fire it leaves burning on the ground.
    class Beam
    {
        public SpriteRenderer glow, body, core;
        public Sprite[] art;
        // The phase 2 beam: molten orange and wild.
        public bool molten;
        public Color colour;
        public FxPlayer flare, hit;
        public float nextFire, nextBurn, nextSpark, lastFireX;
        // Fired from the slit-eye rather than the core.
        public bool fromEye;
        // How many times its usual breadth it's drawn (the Hyper Beam's is several).
        public float width = 1f;
    }

    Beam MakeBeam() => MakeBeam(Core, 1.6f);

    Beam MakeBeam(Vector3 from, float flareSize) => MakeBeam(from, flareSize, secondPhase);

    Beam MakeBeam(Vector3 from, float flareSize, bool molten)
    {
        Color colour = molten ? Molten : CoreRed;
        var beam = new Beam
        {
            molten = molten,
            colour = colour,
            art = Fx.Frames(molten ? Fx.UtsuBeamMolten : Fx.UtsuBeamRed),
            glow = Bar(from, from, 1f, new Color(colour.r, colour.g, colour.b, 0.35f), 53),
            // The flare at its source sits under the beam itself, so the laser reads on top of the glow. The molten
            // beam's is drawn in its own orange and gold, a little larger; without that art the red one is tinted.
            flare = molten && Fx.Frames(Fx.UtsuBeamFlareP2) != null
                ? Fx.Play(Fx.UtsuBeamFlareP2, from, Color.white, flareSize * 1.3f, transform, 54)
                : Fx.Play(Fx.UtsuBeamFlare, from, molten ? MoltenTint : Color.white, flareSize, transform, 54),
            hit = Fx.Play(Fx.UtsuBeamHit, from, molten ? MoltenTint : Color.white, 1.3f, null, 57),
        };
        if (beam.art != null)
        {
            beam.body = new GameObject("BeamBody").AddComponent<SpriteRenderer>();
            beam.body.sprite = beam.art[0];
            beam.body.drawMode = SpriteDrawMode.Tiled;
            beam.body.sortingOrder = 55;
            transient.Add(beam.body.gameObject);
        }
        else
        {
            beam.body = Bar(from, from, 1f, new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.9f), 55);
            beam.core = Bar(from, from, 1f, new Color(1f, 0.92f, 0.88f), 56);
        }
        beam.glow.sprite = ProceduralSprites.Glow;
        // Cleared with the rest of the attack if it's cut short.
        if (beam.flare != null) transient.Add(beam.flare.gameObject);
        if (beam.hit != null) transient.Add(beam.hit.gameObject);
        beams.Add(beam);
        return beam;
    }

    // An interrupted laser (a stagger, its defeat, the ronin falling) loses its bars with the attack; its hum goes too.
    void LateUpdate()
    {
        if (beams.Count == 0) return;
        beams.RemoveAll(b => b.body == null);
        if (beams.Count == 0) Sfx.Stop(Sfx.LanternBeam);
    }

    void DrawBeam(Beam beam, Vector3 from, Vector3 to, bool flash, bool onGround)
    {
        float flicker = 1f + 0.18f * Mathf.Sin(Time.time * 47f) + 0.08f * Mathf.Sin(Time.time * 113f);
        // The molten beam thrashes: its width jumps about every frame on top of the flicker.
        if (beam.molten) flicker *= Random.Range(0.85f, 1.2f);
        Stretch(beam.glow, from, to, (beam.molten ? 2f : 1.6f) * flicker * beam.width);
        beam.glow.color = flash ? new Color(1f, 1f, 1f, 0.5f) : new Color(beam.colour.r, beam.colour.g, beam.colour.b, 0.35f);
        if (beam.art != null)
        {
            beam.body.sprite = beam.art[(int)(Time.time * BeamFps) % beam.art.Length];
            // Flipped now and then, so the molten plasma never settles into a pattern.
            if (beam.molten && Random.value < 0.15f) beam.body.flipY = !beam.body.flipY;
            LayTiled(beam.body, from, to, (beam.molten ? 0.95f : 0.7f) * flicker * (flash ? 1.3f : 1f) * beam.width);
            if (beam.molten && Time.time >= beam.nextSpark)
            {
                // Embers spit off its length.
                beam.nextSpark = Time.time + 0.06f;
                Vector3 along = Vector3.Lerp(from, to, Random.value);
                Fx.Play(Fx.Impact, along, Molten, Random.Range(0.3f, 0.55f));
            }
        }
        else
        {
            Stretch(beam.body, from, to, 0.42f * flicker * beam.width);
            Stretch(beam.core, from, to, 0.14f * beam.width);
            beam.body.color = flash ? Color.white : new Color(CoreRed.r, CoreRed.g, CoreRed.b, 0.9f);
        }
        if (beam.hit != null) beam.hit.transform.position = to + Vector3.back * 0.3f;
        else if (beam.art == null && Time.time >= beam.nextSpark)
        {
            beam.nextSpark = Time.time + 0.12f;
            Fx.Play(Fx.Impact, to, CoreRed, 0.7f);
        }
        // A trail of fire along the ground behind it: a new patch every half unit it drags, or every so often if
        // it holds still, so the burn reads as one unbroken line.
        if (onGround && (Mathf.Abs(to.x - beam.lastFireX) >= 0.5f || Time.time >= beam.nextFire))
        {
            beam.nextFire = Time.time + 0.25f;
            beam.lastFireX = to.x;
            GroundFire.Scorch(to);
        }
    }

    // The beam art scrolls at this many frames a second.
    const float BeamFps = 18f;
    // Its laser's colour: red, and molten orange once its core opens.
    static readonly Color MoltenTint = new Color(1f, 0.75f, 0.45f);
    Color BeamColour => secondPhase ? Molten : CoreRed;

    // Lays tiled beam art from one point to another: scaled to the width and tiled along the length, so the art
    // doesn't smear.
    static void LayTiled(SpriteRenderer strip, Vector3 from, Vector3 to, float width)
    {
        Vector3 delta = to - from;
        float art = strip.sprite.bounds.size.y;
        float scale = width / art;
        strip.transform.position = (from + to) / 2f;
        strip.transform.rotation = delta.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.right, delta) : Quaternion.identity;
        strip.transform.localScale = Vector3.one * scale;
        strip.size = new Vector2(delta.magnitude / scale, art);
    }

    void BurnCover(Beam beam, Blocker cover, Vector3 at)
    {
        if (Time.time < beam.nextBurn) return;
        beam.nextBurn = Time.time + 0.25f;
        // The sword can't burn: the beam only strikes sparks off its steel.
        if (cover == plantedSword)
        {
            Fx.Play(Fx.Impact, at, Color.white, 0.7f);
            Sfx.Play(Sfx.ParryClang, 0.25f, 1.8f);
            return;
        }
        cover.Hit(at);
        Sfx.Play(Sfx.BlockerHit, 0.5f, 1.6f);
    }

    void EndBeam(Beam beam)
    {
        beams.Remove(beam);
        RemoveTransient(beam.glow);
        RemoveTransient(beam.body);
        RemoveTransient(beam.core);
        if (beam.flare != null) beam.flare.Stop();
        if (beam.hit != null) beam.hit.Stop();
    }

    // Where a beam from the core aimed at x on the ground stops: on the nearest blocker in the way, or the ground.
    Vector3 BeamEnd(float x, out Blocker cover) => BeamEnd(Core, x, out cover);

    Vector3 BeamEnd(Vector3 from, float x, out Blocker cover) => BeamEnd(from, new Vector3(x, GroundY, 0), out cover);

    // The height of the cover row its entrance razed, the lowest the rocks it shakes loose come to rest; just above the
    // ronin's head if the entrance never measured it.
    float coverRowY = float.NaN;
    float CoverRowY => float.IsNaN(coverRowY) ? ronin.TopY + 0.8f : coverRowY;

    // A rock lying on the field. Blows only flash off it; only the Hyper Beam breaks it, and only once the whole
    // beam has passed it.
    class Rock
    {
        public Blocker cover;
        public SpriteRenderer look;
        public Sprite[] states;
        public float halfWidth, height, heat;
        public bool scorched;
    }
    readonly List<Rock> rocks = new List<Rock>();
    // Set by the phase break: the Hyper Beam opens the second phase.
    bool hyperNext;

    // How tall a rock that stays stands, and how many can lie on the field at once.
    const float RockHeight = 1.5f;
    const int MaxRocks = 3;
    // They come to rest anywhere from the old cover row to this far above it, this far apart in height, so they never
    // read as one line.
    const float RockRowSpread = 3.5f, RockRowGap = 1.2f;
    // How far apart their shades lie on the ronin's row, so each is a different place to run to.
    const float RockShadeSpacing = 5f;
    // How far in from the edge the middle of a shade can lie. A shade lands further out than its rock, the more so the
    // higher the rock, so spots are chosen by their shade: a rock anywhere else could cast it past where he can walk,
    // and the Hyper Beam couldn't be dodged.
    const float RockShadeMargin = 2.3f;
    // How long the move's overload shakes the hall and rains rocks.
    const float RockfallSeconds = 2.6f;
    // The Hyper Beam: its wind-up, its sweep from edge to edge, how many times a laser's breadth it's drawn, and how
    // far either side of where its middle crosses the ronin's height it burns.
    const float HyperChargeSeconds = 2.2f, HyperSweepSeconds = 3f, HyperWidth = 3.2f, HyperReach = 1.1f;
    // Only the sweep itself is the Hyper Beam (the one a rock saves him from, Poguru's rock roasts): its overload's
    // wild lasers and falling rocks have names of their own.
    const string HyperBeamAttack = "Utsusemi_HyperBeam";

    // The Hyper Beam (second phase only). In super armour it overloads as at its phase break, lasers firing wild from
    // its eye and core (these ones burn) while the hall shakes and rocks rain down, one or two coming to rest and
    // staying. The armour drops, Poguru yells to get behind a rock and hovers in its shade, and it winds up a vast
    // beam that sweeps the field from one edge to the other. Only a rock saves the ronin: the beam can't get through
    // one, and a rock it burns crumbles only once the whole beam has passed it. Straight after the phase break it skips
    // the overload: the break shook the rocks down already.
    IEnumerator HyperBeam(bool overload)
    {
        yield return Stride(CameraShake.Center.x);
        if (overload)
        {
            armored = true;
            SpriteRenderer[] shell = ArmorShell();
            Sfx.Play(Sfx.GearGrind, 0.8f, 0.7f);
            StartCoroutine(Tremble(RockfallSeconds, 0.06f, 1.2f));
            bool fallen = false;
            StartCoroutine(Rockfall(RockfallSeconds, Random.Range(1, 3), true, () => fallen = true));
            float t = 0, nextShot = 0.15f;
            yield return HoldArmor(shell, RockfallSeconds, () =>
            {
                t += Time.deltaTime;
                if (t < nextShot) return;
                float k = t / RockfallSeconds;
                StartCoroutine(WildLaser(true, Mathf.Lerp(0.45f, 0.3f, k), true));
                nextShot = t + Mathf.Lerp(0.4f, 0.18f, k) * Random.Range(0.7f, 1.3f);
            });
            while (!fallen) yield return HoldArmor(shell, Time.deltaTime);
            Armour(shell, false);
            foreach (SpriteRenderer part in shell) RemoveTransient(part);
        }

        rocks.RemoveAll(r => r.cover == null);
        Rock nearest = NearestRock();
        if (nearest != null) CallPoguru("Poguru_Utsu_Rock", new Vector3(ShadeX(nearest), ronin.TopY + 0.3f, 0));
        // The sweep is laid out on the ronin's row, so its breadth reaches both edges there and every rock's shade;
        // aimed at the ground, its ends lie a little beyond the view.
        float dir = Random.value < 0.5f ? 1f : -1f;
        Vector3 aim = Core;
        float row = ronin.Bounds.center.y;
        float GroundFor(float rowX) => aim.x + (rowX - aim.x) * (aim.y - GroundY) / Mathf.Max(0.01f, aim.y - row);
        float from = GroundFor(dir > 0 ? LeftEdge - HyperReach : RightEdge + HyperReach);
        float to = GroundFor(dir > 0 ? RightEdge + HyperReach : LeftEdge - HyperReach);
        SunrayFan fan = SunrayFan.Create(sprite.sharedMaterial);
        transient.Add(fan.gameObject);
        yield return HyperCharge(from, to, fan);

        Beam beam = MakeBeam(Core, 2.6f);
        beam.width = HyperWidth;
        Sfx.PlayLoop(Sfx.LanternBeam, 1f, 0.6f);
        Sfx.Play(Sfx.LanternCharge, 1f, 0.45f);
        CameraShake.Add(0.6f);
        float lastX = from;
        for (float t = 0; t < HyperSweepSeconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.2f))
        {
            Beat();
            float x = Mathf.Lerp(from, to, t / HyperSweepSeconds);
            Vector3 end = BeamEnd(x, out Blocker cover);
            DrawBeam(beam, Core, end, false, cover == null);
            CameraShake.Add(2.4f * Time.deltaTime);
            Rock struck = rocks.Find(r => r.cover != null && r.cover == cover);
            if (struck != null) Scorch(struck, end);
            else if (cover != null) BurnCover(beam, cover, end);
            HyperHit(lastX, x);
            CrumblePassed(x, dir);
            lastX = x;
            // The shade still matters while the beam is on its way, so the fan stays, faint, over what's left to sweep.
            DrawHyperFan(fan, from, to, t / HyperSweepSeconds, 1f, FanSweepAlpha, 0f);
            yield return null;
        }
        EndBeam(beam);
        transient.Remove(fan.gameObject);
        Destroy(fan.gameObject);
        Sfx.Stop(Sfx.LanternBeam);
        foreach (Rock rock in rocks.ToArray())
        {
            if (rock.scorched) Crumble(rock);
        }
        RestPoguru();
        PlayIdle();
        yield return Wait(0.4f);
    }

    // Its wind-up: arms flung back as its core swells (its attack pose without that clip), streaks of light pulled
    // into its heart from all round, a molten orb growing there, the whine climbing and the ticks racing, the hall
    // shaking harder, a guide line to the edge the beam will start from, and the sunray fan pouring out over everything
    // it will sweep, with each rock's shade left pale. Full, it flashes white with a ripple.
    IEnumerator HyperCharge(float from, float to, SunrayFan fan)
    {
        // The clip winds up once, then its last two frames (the blaze at full power) flicker until it fires.
        if (hyperChargeFrames != null)
        {
            Sprite[] blaze = { hyperChargeFrames[hyperChargeFrames.Length - 2], hyperChargeFrames[hyperChargeFrames.Length - 1] };
            animator.Play(hyperChargeFrames, false, () => animator.Play(blaze, true, null, 10f), hyperChargeFrames.Length / (HyperChargeSeconds * 0.6f));
        }
        else PlayAction(art.attack);
        Sfx.Play(Sfx.LanternCharge, 1f, 0.55f);
        SpriteRenderer orb = Bar(Core, Core, 1f, Color.clear, sprite.sortingOrder + 2);
        orb.sprite = ProceduralSprites.Glow;
        SpriteRenderer heart = Bar(Core, Core, 1f, Color.clear, sprite.sortingOrder + 3);
        heart.sprite = ProceduralSprites.Disc;
        SpriteRenderer guide = Bar(Core, Core, 0.05f, Color.clear);
        float nextStreak = 0, nextTick = 0, tick = 0.3f;
        bool whined = false;
        for (float t = 0; t < HyperChargeSeconds; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            Beat();
            float k = t / HyperChargeSeconds;
            Vector3 core = Core;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * (10f + 30f * k));
            orb.transform.SetPositionAndRotation(core + Vector3.back * 0.3f, Quaternion.identity);
            orb.transform.localScale = Vector3.one * (Mathf.Lerp(0.5f, 3.2f, k) * pulse / ProceduralSprites.Glow.bounds.size.x);
            orb.color = new Color(Molten.r, Molten.g, Molten.b, Mathf.Lerp(0.3f, 0.9f, k));
            heart.transform.SetPositionAndRotation(core + Vector3.back * 0.35f, Quaternion.identity);
            heart.transform.localScale = Vector3.one * (Mathf.Lerp(0.1f, 0.75f, k) * pulse / ProceduralSprites.Disc.bounds.size.x);
            heart.color = Color.Lerp(new Color(1f, 0.8f, 0.5f), Color.white, k);
            if (t >= nextStreak)
            {
                nextStreak = t + Mathf.Lerp(0.07f, 0.02f, k);
                StartCoroutine(Inflow(core, k));
            }
            if (t >= nextTick)
            {
                Sfx.Play(Sfx.ClockTick, 0.6f, 1f + k);
                nextTick = t + tick;
                tick = Mathf.Max(0.06f, tick * 0.85f);
            }
            if (!whined && k > 0.55f)
            {
                whined = true;
                Sfx.Play(Sfx.LanternCharge, 1f, 0.8f);
            }
            CameraShake.Add(Time.deltaTime * (1.5f + 1.5f * k));
            Stretch(guide, core, BeamEnd(from, out _), 0.04f + 0.08f * k);
            guide.color = new Color(Molten.r, Molten.g, Molten.b, (0.2f + 0.5f * k) * (0.7f + 0.3f * Mathf.Sin(Time.time * 40f)));
            // In its last moments the fan flickers white: the beam is coming now.
            float white = k > 0.9f ? Mathf.PingPong(Time.time * 12f, 1f) * 0.7f : 0f;
            DrawHyperFan(fan, from, to, 0f, Mathf.SmoothStep(0f, 1f, k / FanPourShare), Mathf.Lerp(FanChargeAlpha.x, FanChargeAlpha.y, k), white);
            yield return null;
        }
        RemoveTransient(orb);
        RemoveTransient(heart);
        RemoveTransient(guide);
        ElementFx.Glow(Core, Color.white, 1.2f, 4f, 0.35f, transform);
        TimeRipple.Emit(Core, 10f, 1.2f, 0.6f);
        HitFlash.Flash(sprite, 0.12f);
    }

    // How much of the wind-up its warning takes to pour out from the core, and how bright it is across the wind-up and
    // during the sweep.
    const float FanPourShare = 0.3f, FanSweepAlpha = 0.09f;
    static readonly Vector2 FanChargeAlpha = new Vector2(0.1f, 0.26f);
    // The rocks' shade: the same pale colour as the safe strip under Kagetsune's beam.
    static readonly Color FanShade = new Color(1f, 0.95f, 0.8f);

    // Lays the warning over the ground from `from` to `to`. Every ray is measured as the beam's own will be, so the pale
    // shade behind a rock is exactly where HyperHit finds him covered.
    void DrawHyperFan(SunrayFan fan, float from, float to, float swept, float pour, float alpha, float white)
    {
        Vector3 core = Core;
        fan.Draw(core, GroundY, from, to, swept, pour, alpha, Color.Lerp(Molten, Color.white, white), FanShade, x =>
            (BeamEnd(core, x, out _) - core).magnitude / Mathf.Max(0.01f, (new Vector3(x, GroundY, 0) - core).magnitude));
    }

    // A streak of light pulled in from the air to its core.
    IEnumerator Inflow(Vector3 core, float k)
    {
        Vector3 heading = Random.insideUnitCircle.normalized;
        float far = Random.Range(2.5f, 4.5f);
        SpriteRenderer streak = Bar(core, core + heading * 0.01f, 0.06f, Color.clear, 54);
        const float seconds = 0.28f;
        for (float t = 0; t < seconds && streak != null; t += Time.deltaTime)
        {
            float u = t / seconds;
            float near = 1f - u * u;
            Stretch(streak, core + heading * far * near, core + heading * far * Mathf.Min(1f, near + 0.35f), 0.05f + 0.05f * k);
            streak.color = new Color(1f, Mathf.Lerp(0.85f, 0.6f, u), 0.4f, Mathf.Sin(u * Mathf.PI) * 0.9f);
            yield return null;
        }
        RemoveTransient(streak);
    }

    // Within the beam's breadth on the ronin's row (counting the ground it swept since last frame), it burns him
    // unless a rock (or the sword) stands between his middle and its core.
    void HyperHit(float lastX, float x)
    {
        Vector3 core = Core;
        Vector3 middle = ronin.Bounds.center;
        float a = RowCross(core, lastX, middle.y), b = RowCross(core, x, middle.y);
        if (middle.x < Mathf.Min(a, b) - HyperReach || middle.x > Mathf.Max(a, b) + HyperReach) return;
        BeamEnd(core, middle, out Blocker cover);
        if (cover == null && !InSwordWard(middle.x)) Strike(middle, HyperBeamAttack);
    }

    // Where a ray from `from` to x on the ground crosses height y.
    float RowCross(Vector3 from, float x, float y) => from.x + (x - from.x) * (from.y - y) / (from.y - GroundY);

    // Where a ray from `from` through a point crosses height y.
    static float Project(Vector3 from, Vector3 through, float y) => from.x + (through.x - from.x) * (from.y - y) / Mathf.Max(0.01f, from.y - through.y);

    // A burnt rock crumbles only once the beam's trailing edge on the ronin's row has left its whole shade behind,
    // so whoever sheltered there is never caught by the beam's breadth as the rock goes.
    void CrumblePassed(float x, float dir)
    {
        Vector3 core = Core;
        float rowY = ronin.Bounds.center.y;
        float trailing = RowCross(core, x, rowY) - dir * HyperReach;
        foreach (Rock rock in rocks.ToArray())
        {
            if (!rock.scorched || rock.cover == null) continue;
            Vector3 foot = rock.cover.transform.position;
            Vector3 farFoot = foot + Vector3.right * dir * rock.halfWidth;
            float shadeFar = dir > 0
                ? Mathf.Max(Project(core, farFoot, rowY), Project(core, farFoot + Vector3.up * rock.height, rowY))
                : Mathf.Min(Project(core, farFoot, rowY), Project(core, farFoot + Vector3.up * rock.height, rowY));
            if (dir * (trailing - shadeFar) > 0f) Crumble(rock);
        }
    }

    // The beam bearing down on a rock: it glows hotter, cracks through its damage states and spits sparks.
    void Scorch(Rock rock, Vector3 at)
    {
        rock.scorched = true;
        rock.heat += Time.deltaTime;
        if (rock.states != null) rock.look.sprite = rock.states[rock.heat > 0.4f ? 2 : rock.heat > 0.15f ? 1 : 0];
        rock.look.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.35f), Mathf.Clamp01(rock.heat * 1.5f));
        if (Random.value < 0.3f) Fx.Play(Fx.Impact, at, Molten, Random.Range(0.5f, 0.9f));
        if (Random.value < 0.05f) Sfx.Play(Sfx.BlockerHit, 0.5f, Random.Range(0.6f, 0.8f));
    }

    void Crumble(Rock rock)
    {
        rocks.Remove(rock);
        if (rock.cover == null) return;
        Vector3 middle = rock.cover.transform.position + Vector3.up * rock.height * 0.5f;
        Debris.BurstSprites(middle, rock.states, new Color(0.8f, 0.72f, 0.65f), 8, 0.3f, 6f);
        Fx.Play(Fx.SmokePuff, middle, new Color(0.8f, 0.75f, 0.7f, 0.9f), 2f);
        rock.cover.Demolish();
        CameraShake.Add(0.2f);
    }

    Rock NearestRock()
    {
        Rock best = null;
        foreach (Rock rock in rocks)
        {
            if (rock.cover == null) continue;
            if (best == null || Mathf.Abs(ShadeX(rock) - ronin.Bounds.center.x) < Mathf.Abs(ShadeX(best) - ronin.Bounds.center.x)) best = rock;
        }
        return best;
    }

    // The middle of a rock's shade on the ronin's row, cast from its core.
    float ShadeX(Rock rock) => Project(Core, rock.cover.transform.position + Vector3.up * rock.height * 0.5f, ronin.Bounds.center.y);

    // The hall shakes and rocks rain down across it for `seconds`: most smash on the ground (on the ronin, if
    // `harmful` and he's still under it), and `keep` of them land and stay there as cover, making room first if the
    // field already holds as many as it can.
    IEnumerator Rockfall(float seconds, int keep, bool harmful, System.Action done = null)
    {
        rocks.RemoveAll(r => r.cover == null);
        while (rocks.Count > 0 && rocks.Count + keep > MaxRocks) Crumble(rocks[0]);
        List<Vector2> spots = RockSpots(keep);
        Sfx.Play(Sfx.GearGrind, 0.7f, 0.5f);
        CameraShake.Add(0.4f);
        float nextRock = 0.1f;
        int dropped = 0, landed = 0;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            // A steady rumble: the shake topped up as fast as it fades.
            CameraShake.Add(2.3f * Time.deltaTime);
            if (t >= nextRock)
            {
                nextRock = t + Random.Range(0.12f, 0.24f);
                StartCoroutine(FallingRock(Random.Range(LeftEdge + 0.5f, RightEdge - 0.5f), harmful, null));
            }
            // The ones that stay come down through the middle of it.
            if (dropped < spots.Count && t >= seconds * (0.3f + 0.4f * dropped / spots.Count))
            {
                StartCoroutine(FallingRock(spots[dropped].x, false, () => landed++, spots[dropped].y));
                dropped++;
            }
            yield return null;
        }
        for (; dropped < spots.Count; dropped++) StartCoroutine(FallingRock(spots[dropped].x, false, () => landed++, spots[dropped].y));
        while (landed < spots.Count) yield return null;
        done?.Invoke();
    }

    // Where new rocks come to rest (x, and the height of their foot): in different rows, with their shades on the ronin's
    // row well apart from each other and from those already there. Each is worked back from where its shade should fall,
    // with its core at the middle of the field, where it stands to fire.
    List<Vector2> RockSpots(int count)
    {
        var spots = new List<Vector2>();
        var shades = new List<float>();
        float centre = CameraShake.Center.x, reach = HalfWidth - RockShadeMargin;
        float low = CoverRowY, coreY = Core.y, row = ronin.Bounds.center.y;
        Vector2 Spot(float shade, float y) => new Vector2(centre + (shade - centre) * (coreY - (y + RockHeight * 0.5f)) / Mathf.Max(0.01f, coreY - row), y);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                float shade = Random.Range(centre - reach, centre + reach);
                float y = Random.Range(low, low + RockRowSpread);
                bool clear = shades.TrueForAll(other => Mathf.Abs(other - shade) >= RockShadeSpacing)
                    && spots.TrueForAll(other => Mathf.Abs(other.y - y) >= RockRowGap)
                    && rocks.TrueForAll(r => r.cover == null
                        || (Mathf.Abs(ShadeX(r) - shade) >= RockShadeSpacing && Mathf.Abs(r.cover.transform.position.y - y) >= RockRowGap));
                if (!clear) continue;
                spots.Add(Spot(shade, y));
                shades.Add(shade);
                break;
            }
        }
        if (spots.Count == 0 && rocks.Count == 0) spots.Add(Spot(Random.Range(centre - reach, centre + reach), low));
        return spots;
    }

    // One rock falling from above the screen onto its shadow, which darkens as it nears. A rock that `stayed` lands
    // at `restY` and stays (calling it once it has); the rest smash on the ground.
    IEnumerator FallingRock(float x, bool harmful, System.Action stayed, float restY = 0f)
    {
        bool stays = stayed != null;
        Sprite[] frames = Fx.Frames(Fx.UtsuRock);
        Sprite[] states = null;
        if (frames != null && frames.Length >= 3)
        {
            int first = Random.Range(0, frames.Length / 3) * 3;
            states = new[] { frames[first], frames[first + 1], frames[first + 2] };
        }
        float height = stays ? RockHeight : Random.Range(0.45f, 0.85f);
        float landY = stays ? restY : GroundY + 0.15f;

        // The rock stands on its own foot: the object's origin is the bottom of its art. Launched, so a stagger leaves
        // it falling (and lying there); only a halt or its fall clears it.
        var root = new GameObject(stays ? "Rock" : "FallingRock");
        Detach(root);
        var look = new GameObject("Look").AddComponent<SpriteRenderer>();
        look.transform.SetParent(root.transform, false);
        look.sprite = states != null ? states[0] : ProceduralSprites.Disc;
        if (states == null) look.color = new Color(0.45f, 0.42f, 0.4f);
        float scale = height / look.sprite.bounds.size.y;
        look.transform.localScale = Vector3.one * scale;
        look.transform.localPosition = Vector3.up * (-look.sprite.bounds.min.y * scale);
        look.sortingOrder = stays ? 6 : 52;
        float halfWidth = look.sprite.bounds.size.x * scale / 2f;

        var shadow = new GameObject("RockShadow").AddComponent<SpriteRenderer>();
        shadow.sprite = ProceduralSprites.SoftGlow;
        shadow.sortingOrder = 3;
        Detach(shadow.gameObject);

        float top = TopOfScreen + height, fall = stays ? 0.9f : Random.Range(0.6f, 0.85f);
        float spin = stays ? 0f : Random.Range(-360f, 360f);
        for (float t = 0; t < fall && root != null; t += Time.deltaTime)
        {
            float k = t / fall;
            root.transform.SetPositionAndRotation(new Vector3(x, Mathf.Lerp(top, landY, k * k), 0), Quaternion.Euler(0, 0, spin * t));
            shadow.transform.position = new Vector3(x, landY, 0.3f);
            shadow.transform.localScale = new Vector3(halfWidth * 2.4f, halfWidth * 0.7f, 1f) * (0.5f + 0.5f * k) / ProceduralSprites.SoftGlow.bounds.size.x;
            shadow.color = new Color(0f, 0f, 0f, 0.15f + 0.4f * k);
            yield return null;
        }
        Destroy(shadow.gameObject);
        if (root == null) yield break;
        Vector3 foot = new Vector3(x, landY, 0);
        root.transform.SetPositionAndRotation(foot, Quaternion.identity);

        if (!stays)
        {
            Debris.BurstSprites(foot + Vector3.up * height * 0.4f, states, new Color(0.8f, 0.72f, 0.65f), 5, 0.2f, 4f);
            Fx.Play(Fx.SmokePuff, foot + Vector3.up * 0.2f, new Color(0.8f, 0.75f, 0.7f, 0.8f), 0.9f);
            Sfx.Play(Sfx.BlockerBreak, 0.35f, Random.Range(0.8f, 1.1f));
            CameraShake.Add(0.06f);
            if (harmful && Mathf.Abs(ronin.Bounds.center.x - x) < halfWidth + 0.3f) Strike(new Vector3(x, ronin.Bounds.center.y, 0), "Utsusemi_Rockfall");
            Destroy(root);
            yield break;
        }

        // It thuds down and stays: cover that only the Hyper Beam will break.
        Fx.Play(Fx.SmokePuff, foot + Vector3.up * 0.3f, new Color(0.8f, 0.75f, 0.7f, 0.9f), 2f);
        Debris.BurstSprites(foot + Vector3.up * 0.3f, states, new Color(0.8f, 0.72f, 0.65f), 6, 0.2f, 4f);
        Sfx.Play(Sfx.HammerSlam, 0.8f, 0.7f);
        CameraShake.Add(0.35f);
        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(halfWidth * 2f * 0.85f, height * 0.85f, 0.5f);
        box.center = Vector3.up * height / 2f;
        Blocker cover = root.AddComponent<Blocker>();
        cover.Init(int.MaxValue, look, states ?? new[] { look.sprite }, null);
        rocks.Add(new Rock { cover = cover, look = look, states = states, halfWidth = halfWidth, height = height });
        stayed();
    }

    // Fire burns holes in the lace.
    public override void Burn(Vector3 point, float radius)
    {
        foreach (Arrow diamond in lattice)
        {
            if (diamond != null && Vector2.Distance(diamond.transform.position, point) < radius * 1.5f) Destroy(diamond.gameObject);
        }
        lattice.RemoveAll(a => a == null);
    }
}
