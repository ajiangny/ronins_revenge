using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The souls he drags out of the coffin to fight beside him.
public partial class Kagetsune
{
    // --- The summoned souls ---

    // A soul he drags out of the coffin: a translucent teal ghost of the boss, in its own idle, on a chain from the
    // coffin-hammer. Without its art, a teal wisp stands in.
    class Echo
    {
        public SpriteRenderer body;
        public SpriteRenderer chain;
        public SpriteRenderer aura;
        public Vector3 Centre => body.bounds.center;
        public float Height => body.bounds.size.y;
    }

    IEnumerator Summon(BossKind kind, BossLine call, System.Action<Echo> ready)
    {
        Speak(call);
        PlayAction(art.parry);
        Toll(0.8f, 0.65f);
        float side = anchor.x > CameraShake.Center.x ? -1f : 1f;
        float x = Mathf.Clamp(anchor.x + side * 6.5f, LeftEdge + 3f, RightEdge - 3f);
        Vector3 at = new Vector3(x, anchor.y - 0.6f, 0);

        var echo = new Echo();
        var body = new GameObject("Echo_" + kind).AddComponent<SpriteRenderer>();
        body.sortingOrder = 4;
        if (echoes.TryGetValue(kind, out var look) && look.art.idle != null && look.art.idle.Length > 0)
        {
            body.transform.localScale = Vector3.one * look.scale;
            var animator = body.gameObject.AddComponent<SpriteAnimator>();
            animator.Play(look.art.idle, true, null, look.art.idleFps);
            body.sprite = look.art.idle[0];
            body.transform.position = at - (body.bounds.center - body.transform.position);
        }
        else
        {
            body.sprite = ProceduralSprites.Glow;
            body.transform.position = at;
            body.transform.localScale = Vector3.one * 3f;
        }
        body.color = new Color(0.55f, 1f, 0.9f, 0f);
        transient.Add(body.gameObject);
        echo.body = body;
        echo.aura = Glowing(at, new Color(Teal.r, Teal.g, Teal.b, 0f), echo.Height * 1.1f, 3);
        echo.chain = Tiled(Fx.KageChain, HammerHead, at, 0.3f, new Color(Bronze.r, Bronze.g, Bronze.b, 0f), 3);
        echo.chain.color = ChainColour(echo.chain, 0f);

        activeEcho = echo;
        Fx.Play(Fx.SmokePuff, at, new Color(Teal.r, Teal.g, Teal.b, 0.7f), 2.5f);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 0.8f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime * Tempo)
        {
            float k = t / 0.8f;
            body.color = new Color(0.55f, 1f, 0.9f, 0.7f * k);
            echo.aura.color = new Color(Teal.r, Teal.g, Teal.b, 0.3f * k);
            echo.chain.color = ChainColour(echo.chain, 0.8f * k);
            yield return null;
        }
        ready(echo);
    }

    IEnumerator Dismiss(Echo echo)
    {
        if (echo == null || echo.body == null) yield break;
        for (float t = 0; t < 0.6f && echo.body != null; t += Time.deltaTime)
        {
            float k = 1f - t / 0.6f;
            echo.body.color = new Color(0.55f, 1f, 0.9f, 0.7f * k);
            if (echo.aura != null) echo.aura.color = new Color(Teal.r, Teal.g, Teal.b, 0.3f * k);
            if (echo.chain != null) echo.chain.color = ChainColour(echo.chain, 0.8f * k);
            yield return null;
        }
        Drop(echo.body);
        Drop(echo.aura);
        Drop(echo.chain);
        if (activeEcho == echo) activeEcho = null;
    }

    // A soul whose ghost has been cleared away (a stagger, his fall) stops where it is.
    static bool Gone(Echo echo) => echo == null || echo.body == null;

    // The chain from the coffin-hammer follows the ghost while the soul works.
    void HoldChain(Echo echo)
    {
        if (echo?.chain != null && echo.body != null) Lay(echo.chain, HammerHead, echo.Centre, echo.chain.drawMode == SpriteDrawMode.Tiled ? 0.3f : 0.1f);
    }

    // Benibana's soul: her smoke rings in offset fans, then her Crimson Iaido, a crescent only a parry answers.
    // Parried, it flies back at the Emperor, not at her.
    IEnumerator EchoBenibana()
    {
        Echo echo = null;
        yield return Summon(BossKind.Benibana, BossLine.SummonBenibana, e => echo = e);
        if (Gone(echo)) yield break;
        Vector3 mouth = echo.Centre + Vector3.up * (echo.Height * 0.25f);
        for (int volley = 0; volley < 3; volley++)
        {
            HoldChain(echo);
            Fx.Play(Fx.SmokePuff, mouth, new Color(0.8f, 0.75f, 0.8f, 0.8f), 1.6f);
            Sfx.Play(Sfx.ParrySwing, 0.6f, 0.6f);
            Vector3 aim = (ronin.Bounds.center - mouth).normalized;
            var rings = new List<Arrow>();
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(-40f, 40f, i / 4f) + (volley % 2 == 1 ? 10f : 0f);
                Arrow ring = Shoot(mouth, Quaternion.Euler(0, 0, angle) * aim * 5.5f);
                Sprite[] frames = Fx.Frames(Fx.BeniSmokeRing);
                if (frames != null) ring.Restyle(frames, 10f, new Color(0.75f, 1f, 0.9f, 0.85f), new Vector2(1.15f, 1.15f), 0.3f, new Color(Teal.r, Teal.g, Teal.b, 0.3f), 1.4f);
                else ring.Restyle(ProceduralSprites.Disc, new Color(0.7f, 0.9f, 0.85f, 0.8f), new Vector2(0.75f, 0.75f), 0.3f, new Color(Teal.r, Teal.g, Teal.b, 0.35f), 1.4f);
                ring.gameObject.AddComponent<Swell>().Begin(1f, 1.9f, 2.2f);
                rings.Add(ring);
            }
            IgnoreEachOther(rings);
            // She answers once his summons has been heard.
            if (volley == 0) Speak(BossLine.EchoBenibana);
            yield return Wait(0.6f);
            if (Gone(echo)) yield break;
        }
        // The draw.
        HoldChain(echo);
        yield return LockOn(0.8f, new Color(1f, 0.3f, 0.35f));
        if (Gone(echo)) yield break;
        Vector3 from = echo.Centre;
        Arrow wave = Shoot(from, (ronin.Bounds.center - from).normalized * 10f);
        Sprite[] crescent = Fx.Frames(Fx.BeniCrescent);
        if (crescent != null) wave.Restyle(crescent, 14f, new Color(0.7f, 1f, 0.9f), new Vector2(3.2f, 2f), 0.55f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 3.5f);
        else wave.Restyle(ProceduralSprites.Glow, new Color(0.6f, 1f, 0.9f), new Vector2(3.2f, 1f), 0.55f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 3.5f);
        wave.MakeReflectable(transform);
        Fx.Play(Fx.SlashArc, from, Teal, 3f);
        Sfx.Play(Sfx.SwordSlash, 0.9f, 0.7f);
        yield return Wait(0.8f);
        if (Gone(echo)) yield break;
        yield return Dismiss(echo);
    }

    // Utsusemi's soul: three hour marks, each laid where the ronin stands and struck by its eye-laser a beat later,
    // then its Hour Strike, a giant cleave steered onto him that only a parry turns back (onto the Emperor).
    IEnumerator EchoUtsusemi()
    {
        Echo echo = null;
        yield return Summon(BossKind.Utsusemi, BossLine.SummonUtsusemi, e => echo = e);
        if (Gone(echo)) yield break;
        for (int i = 0; i < 3; i++)
        {
            HoldChain(echo);
            Vector3 eye = echo.Centre + Vector3.up * (echo.Height * 0.3f);
            Vector3 spot = new Vector3(ronin.Bounds.center.x, ronin.Feet.y, 0);
            FxPlayer mark = Fx.Play(Fx.UtsuClockMark, spot, new Color(0.6f, 1f, 0.9f), 3f, null, 6);
            SpriteRenderer ring = mark == null ? Glowing(spot, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 2.2f, 6) : null;
            if (mark != null) transient.Add(mark.gameObject);
            SpriteRenderer sight = Bar(eye, spot, 0.05f, new Color(Teal.r, Teal.g, Teal.b, 0.5f));
            Sfx.Play(Sfx.ClockTick, 0.8f, 0.8f);
            if (i == 0) Speak(BossLine.EchoUtsusemi);
            yield return Wait(0.7f);
            if (Gone(echo)) yield break;
            var laser = Bar(eye, spot, 0.3f, new Color(0.85f, 1f, 0.95f, 0.95f), 56);
            Sfx.Play(Sfx.LanternBeam, 0.5f, 1.3f);
            if (Mathf.Abs(ronin.Bounds.center.x - spot.x) < 0.9f) ronin.TakeHit(spot + Vector3.up * 0.5f);
            Fx.Play(Fx.Impact, spot, Teal, 1.4f);
            GroundFire.Light(spot);
            yield return Wait(0.15f);
            if (Gone(echo)) yield break;
            Drop(laser);
            Drop(sight);
            Drop(ring);
            if (mark != null) { transient.Remove(mark.gameObject); mark.Stop(); }
            yield return Wait(0.2f);
            if (Gone(echo)) yield break;
        }
        HoldChain(echo);
        yield return LockOn(0.8f, Teal);
        if (Gone(echo)) yield break;
        Sfx.Play(Sfx.MidnightChime, 0.8f, 0.7f);
        Vector3 from = echo.Centre;
        Arrow cleave = Shoot(from, (ronin.Bounds.center - from).normalized * 11f);
        Sprite[] frames = Fx.Frames(Fx.UtsuHourSlash);
        if (frames != null) cleave.Restyle(frames, 14f, new Color(0.7f, 1f, 0.9f), new Vector2(3.4f, 2.2f), 0.6f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 3.5f);
        else cleave.Restyle(ProceduralSprites.Glow, new Color(0.6f, 1f, 0.9f), new Vector2(3.4f, 1.1f), 0.6f, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 3.5f);
        cleave.MakeUnbreakable();
        cleave.MakeReflectable(transform);
        StartCoroutine(Pursue(cleave, 11f));
        Sfx.Play(Sfx.HourSlash, 0.9f);
        yield return Wait(0.9f);
        if (Gone(echo)) yield break;
        yield return Dismiss(echo);
    }

    // Shirogane's soul: her Phantom Blade Rain, two waves of ghost swords down every lane but two. Bound to his will,
    // she can only warn Oni before her blades fall.
    IEnumerator EchoShirogane()
    {
        Echo echo = null;
        yield return Summon(BossKind.Shirogane, BossLine.SummonShirogane, e => echo = e);
        if (Gone(echo)) yield break;
        const int LaneCount = 8;
        List<float> lanes = Lanes(LaneCount);
        int gap = Random.Range(0, LaneCount - 1);
        for (int wave = 0; wave < 2; wave++)
        {
            HoldChain(echo);
            var tells = new List<SpriteRenderer>();
            var xs = new List<float>();
            for (int i = 0; i < LaneCount; i++)
            {
                if (i == gap || i == gap + 1) continue;
                xs.Add(lanes[i]);
                tells.Add(Bar(new Vector3(lanes[i], TopOfScreen, 0), new Vector3(lanes[i], GroundY, 0), 0.1f, new Color(0.6f, 0.95f, 1f, 0.3f)));
            }
            Sfx.Play(Sfx.BladeRing, 0.7f);
            if (wave == 0) Speak(BossLine.EchoShirogane);
            yield return Wait(0.8f);
            if (Gone(echo)) yield break;
            foreach (SpriteRenderer tell in tells) Drop(tell);
            foreach (float x in xs)
            {
                Arrow blade = Shoot(new Vector3(x, TopOfScreen, 0), Vector3.down * 12f);
                Sprite[] frames = Fx.Frames(Fx.ShiroSword);
                if (frames != null) blade.Restyle(frames, 10f, Color.white, new Vector2(0.7f, 1.9f), 0.3f, new Color(0.4f, 0.9f, 1f, 0.4f), 1.4f);
                else blade.Restyle(ProceduralSprites.White, new Color(0.85f, 0.97f, 1f), new Vector2(0.16f, 1.6f), 0.3f, new Color(0.4f, 0.9f, 1f, 0.45f), 1.4f);
                blade.PassesCover = true;
                blade.PassesShots = true;
            }
            Sfx.Play(Sfx.GhostSwordFall, 0.9f);
            // The next wave's gap is one lane over, so one lane carries through.
            gap = Mathf.Clamp(gap + (Random.value < 0.5f ? -1 : 1), 0, LaneCount - 2);
            yield return Wait(0.7f);
            if (Gone(echo)) yield break;
        }
        yield return Dismiss(echo);
    }
}
