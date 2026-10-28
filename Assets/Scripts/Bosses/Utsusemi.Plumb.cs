using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plumb Lines: clockwork portals, their threads and the weights that swing through the lane.
public partial class Utsusemi
{
    // Plumb Lines: gold clockwork portals open in its row, one by one, behind it. Threads show their full reach, then
    // a gold weight drops from each and swings as a pendulum through the ronin's lane, the swings rippling along the
    // row like a pendulum-wave toy. One hangs right above him. A hit on a weight cuts it loose; when the time is up
    // the swings die down and the rest are reeled back up into their portals, which close behind them.
    IEnumerator PlumbLines()
    {
        if (Random.value < 0.5f) Speak(BossLine.PlumbLines);
        int count = secondPhase ? 5 : Random.Range(3, 5);
        // The portals hang just above its hat, whose brim tops out near this row of its frames.
        float pivotY = OnSprite(58f, 28f).y + 0.35f;
        float roninX = ronin.Bounds.center.x;
        const float margin = 2.5f;
        // The first hangs over the ronin; the rest spread out either side of him. Left to right, for the wave.
        // It doesn't stand and watch: it strides to fresh spots under the portals from the moment they open until the
        // swings die down.
        float swing = secondPhase ? 3.6f : 3.2f;
        const float drop = 0.6f, fadeIn = 0.8f, damp = 0.6f, reel = 0.5f;
        pacing = StartCoroutine(Pace(count * 0.12f + 0.6f + drop + swing + damp));
        var xs = new List<float>();
        for (int i = 0; i < count; i++)
        {
            float offset = i == 0 ? 0f : ((i + 1) / 2) * 4.5f * (i % 2 == 1 ? 1f : -1f) + Random.Range(-0.7f, 0.7f);
            xs.Add(Mathf.Clamp(roninX + offset, LeftEdge + margin, RightEdge - margin));
        }
        xs.Sort();
        var pivots = new Vector3[count];
        for (int i = 0; i < count; i++) pivots[i] = new Vector3(xs[i], pivotY, 0);

        // The portals open in turn, behind it.
        var portals = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            portals[i] = OpenPortal(pivots[i]);
            Tick(0.5f);
            yield return Wait(0.12f);
        }

        // Every thread is the same length, bottoming out just below the ronin's middle, so each swing crosses his
        // height over most of its arc.
        float length = pivotY - (ronin.Bounds.center.y - 0.6f);
        var threads = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            threads[i] = Bar(pivots[i], pivots[i] + Vector3.down * length, 0.06f, new Color(Gold.r, Gold.g, Gold.b, 0.3f), PortalOrder);
        }
        Tick(0.6f);
        yield return Wait(0.3f);
        Tick(0.6f);
        yield return Wait(0.3f);

        var weights = new Arrow[count];
        Sprite[] frames = Fx.Frames(Fx.UtsuPlumb);
        for (int i = 0; i < count; i++)
        {
            weights[i] = Own(Shoot(pivots[i], Vector3.down * 0.1f));
            // A landing shadow reads a straight fall; a pendulum's swing makes it lie, so the weights go without.
            Destroy(weights[i].GetComponent<ArrowShadow>());
            if (frames != null) weights[i].Restyle(frames, 8f, Color.white, new Vector2(0.9f, 1.07f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.35f), 1.1f);
            else weights[i].Restyle(ProceduralSprites.Disc, Gold, new Vector2(0.6f, 0.8f), 0.3f, new Color(Gold.r, Gold.g, Gold.b, 0.4f), 1.1f);
            threads[i].color = new Color(0.95f, 0.95f, 1f, 0.6f);
        }
        // Neighbours' arcs cross, and shots break each other, so the weights must pass through one another.
        IgnoreEachOther(new List<Arrow>(weights));
        Sfx.Play(Sfx.ShurikenThrow, 0.7f, 0.4f);

        // The weights are away: a stagger now breaks its stance but not the swing, which plays out on its own.
        for (int i = 0; i < count; i++)
        {
            if (threads[i] != null) Detach(threads[i].gameObject);
            if (portals[i] != null) Detach(portals[i].gameObject);
        }
        plumbsOut = true;
        yield return Launch(SwingPlumbs(weights, threads, portals, pivots, length, drop, swing, damp, reel, fadeIn));
    }

    // Plumb Lines has weights swinging: it won't open more portals until they're reeled in.
    bool plumbsOut;

    // The Plumb Lines' swing, launched: the drop, the pendulum wave, the damping and the reel, in real time whatever
    // happens to the automaton meanwhile, then the portals close.
    IEnumerator SwingPlumbs(Arrow[] weights, SpriteRenderer[] threads, Transform[] portals, Vector3[] pivots, float length,
        float drop, float swing, float damp, float reel, float fadeIn)
    {
        int count = weights.Length;

        // Where each weight hung last, so a cut thread snaps where it was.
        var lastHang = new Vector3[count];
        const float amplitude = 28f * Mathf.Deg2Rad;
        // Each swings this far behind its left-hand neighbour, so a ripple runs along the row.
        const float waveStep = 0.7f;
        float omega = Mathf.PI * 2f / (secondPhase ? 2f : 2.4f);
        float halfPeriod = Mathf.PI / omega;
        float nextTick = drop + halfPeriod * 0.5f;
        float total = drop + swing + damp + reel;
        // Steered once per physics step: a velocity worked out from a render frame's delta overshoots the next step.
        for (float t = 0; t < total; t += Time.fixedDeltaTime)
        {
            Beat();
            float swingT = t - drop;
            // Faded in smoothly after the drop, and damped to a stop before the reel.
            float envelope = Mathf.SmoothStep(0, 1, swingT / fadeIn) * (1f - Mathf.SmoothStep(0, 1, (swingT - swing) / damp));
            // Out with a slight settle at the bottom; wound back in at the end.
            float reach;
            if (t < drop) reach = DropEase(t / drop);
            else if (swingT < swing + damp) reach = 1f;
            else reach = 1f - Mathf.Pow(Mathf.Clamp01((swingT - swing - damp) / reel), 2f);
            // The lead weight's tick at each end of its swing keeps the clock's time.
            if (swingT > 0 && t >= nextTick && swingT < swing)
            {
                nextTick += halfPeriod;
                Tick(0.3f);
            }
            for (int i = 0; i < count; i++)
            {
                float angle = t < drop ? 0f : amplitude * envelope * Mathf.Sin(omega * swingT - i * waveStep);
                Vector3 hang = new Vector3(Mathf.Sin(angle), -Mathf.Cos(angle), 0) * length * reach;
                if (weights[i] == null)
                {
                    // Cut loose: the thread snaps and whips back up into its portal, which closes after it.
                    if (threads[i] == null) continue;
                    if (portals[i] != null) StartCoroutine(SnapThread(threads[i], pivots[i], lastHang[i], portals[i]));
                    else if (threads[i] != null) Destroy(threads[i].gameObject);
                    threads[i] = null;
                    portals[i] = null;
                    continue;
                }
                lastHang[i] = weights[i].transform.position;
                Vector3 target = pivots[i] + hang;
                weights[i].Steer((target - weights[i].transform.position) / Time.fixedDeltaTime);
                weights[i].Art.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                Stretch(threads[i], pivots[i], weights[i].transform.position, 0.06f);
                // A gold streak trails each weight as it drops.
                if (t < drop && Random.value < 0.5f) Afterimage(weights[i].Art, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            }
            yield return new WaitForFixedUpdate();
        }

        StopPacing();
        // Back in: each weight vanishes into its portal with a glint, and the portals close.
        for (int i = 0; i < count; i++)
        {
            if (weights[i] != null)
            {
                ElementFx.Glow(pivots[i], Gold, 0.6f, 1.4f, 0.25f);
                Destroy(weights[i].gameObject);
            }
            RemoveTransient(threads[i]);
        }
        Sfx.Play(Sfx.HatClang, 0.35f, 1.6f);
        plumbsOut = false;
        for (int i = 0; i < count; i++)
        {
            if (portals[i] != null) StartCoroutine(ClosePortal(portals[i]));
            yield return new WaitForSeconds(0.06f);
        }
    }

    // A cut thread: it parts at `at` with a spray of gold and a high ting, the loose end drops away tumbling, and the
    // rest whips back up into its portal, wriggling as it goes, and the portal flares as it swallows it and closes.
    IEnumerator SnapThread(SpriteRenderer thread, Vector3 pivot, Vector3 at, Transform portal)
    {
        Fx.Play(Fx.Impact, at, Gold, 0.7f);
        ElementFx.Glow(at, Gold, 0.3f, 1.2f, 0.2f);
        Debris.BurstSprites(at, art.deathParticles, Gold, 5, 0.4f, 3.5f);
        Sfx.Play(Sfx.ParryClang, 0.3f, 2f);
        StartCoroutine(DropLooseEnd(at, at - pivot));

        Vector3 reach = at - pivot;
        Vector3 across = new Vector3(-reach.y, reach.x, 0).normalized;
        const float seconds = 0.35f;
        for (float t = 0; t < seconds && thread != null; t += Time.deltaTime)
        {
            float k = t / seconds;
            float left = (1f - k) * (1f - k);
            // A dying wriggle sideways, like a cut string recoiling.
            Vector3 tip = pivot + reach * left + across * Mathf.Sin(k * Mathf.PI * 4f) * 0.3f * (1f - k);
            Stretch(thread, pivot, tip, 0.05f);
            if (Random.value < 0.5f) Afterimage(thread, new Color(Gold.r, Gold.g, Gold.b, 0.3f));
            yield return null;
        }
        RemoveTransient(thread);
        ElementFx.Glow(pivot, Gold, 0.4f, 1.6f, 0.25f);
        Fx.Play(Fx.Impact, pivot, Gold, 0.5f);
        Tick(0.35f);
        yield return ClosePortal(portal);
    }

    // The thread's loose end below the cut: a short gold strand that tumbles down and fades.
    IEnumerator DropLooseEnd(Vector3 at, Vector3 along)
    {
        Vector3 piece = along.normalized * 0.6f;
        SpriteRenderer end = Bar(at, at - piece, 0.05f, new Color(0.95f, 0.95f, 1f, 0.6f), PortalOrder);
        Vector3 velocity = new Vector3(Random.Range(-1.5f, 1.5f), 1.5f, 0);
        float spin = Random.Range(-540f, 540f);
        const float seconds = 0.5f;
        for (float t = 0; t < seconds && end != null; t += Time.deltaTime)
        {
            velocity += Vector3.down * 18f * Time.deltaTime;
            end.transform.position += velocity * Time.deltaTime;
            end.transform.Rotate(0, 0, spin * Time.deltaTime);
            Color c = end.color;
            end.color = new Color(c.r, c.g, c.b, 0.6f * (1f - t / seconds));
            yield return null;
        }
        RemoveTransient(end);
    }

    // The portals and their threads sit just behind the automaton's own sprite.
    const int PortalOrder = 4;

    Coroutine pacing;

    // Strides about while an attack plays out by itself, so it isn't a still target. Started beside the fight, so a
    // stagger (OnStaggered) or the attack's end stops it.
    IEnumerator Pace(float seconds)
    {
        float until = Time.time + seconds;
        yield return Wait(0.2f);
        while (Time.time < until - 0.3f)
        {
            yield return Stride(RandomX(2.5f));
            yield return Wait(Random.Range(0.25f, 0.5f));
        }
        pacing = null;
    }

    void StopPacing()
    {
        if (pacing != null) StopCoroutine(pacing);
        pacing = null;
    }
    const float PortalSize = 1.3f;

    // Out fast, a touch past full length, then settling back.
    static float DropEase(float k)
    {
        k = Mathf.Clamp01(k);
        float overshoot = 1.06f;
        return k < 0.8f ? overshoot * (1f - Mathf.Pow(1f - k / 0.8f, 3f)) : Mathf.Lerp(overshoot, 1f, (k - 0.8f) / 0.2f);
    }

    // A gold clockwork portal opening in the air: it grows from a point with a small flash.
    Transform OpenPortal(Vector3 at)
    {
        FxPlayer portal = Fx.Play(Fx.UtsuPlumbPortal, at, Color.white, PortalSize, null, PortalOrder);
        Transform shape;
        if (portal != null) shape = portal.transform;
        else
        {
            SpriteRenderer disc = new GameObject("Portal").AddComponent<SpriteRenderer>();
            disc.sprite = ProceduralSprites.Glow;
            disc.color = new Color(Gold.r, Gold.g, Gold.b, 0.8f);
            disc.sortingOrder = PortalOrder;
            disc.transform.position = new Vector3(at.x, at.y, -1f);
            shape = disc.transform;
        }
        transient.Add(shape.gameObject);
        ElementFx.Glow(at, Gold, 0.5f, 1.6f, 0.3f);
        StartCoroutine(ScalePortal(shape, 0f, shape.localScale.x, 0.2f, false));
        return shape;
    }

    IEnumerator ClosePortal(Transform portal)
    {
        if (portal == null) yield break;
        yield return ScalePortal(portal, portal.localScale.x, 0f, 0.2f, true);
    }

    IEnumerator ScalePortal(Transform portal, float from, float to, float seconds, bool remove)
    {
        for (float t = 0; t < seconds && portal != null; t += Time.deltaTime)
        {
            portal.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
            yield return null;
        }
        if (portal == null) yield break;
        portal.localScale = Vector3.one * to;
        if (remove)
        {
            transient.Remove(portal.gameObject);
            Destroy(portal.gameObject);
        }
    }
}
