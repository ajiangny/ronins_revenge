using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The souls he drags out of the coffin. Each is the boss itself, raised at 30% of its health and fighting with its
// own first-phase moves beside him, on a chain from the coffin-hammer. Cut it down and the soul breaks free: it flies
// home into him and hammers his stance. Leave it standing too long and he reels it back in and wears its power on
// his next blows.
public partial class Kagetsune
{
    // How long a soul fights before he drags it back into the coffin, in his time (a stagger holds the count).
    const float AbsorbAfter = 20f;
    // The stance a freed soul knocks out of him.
    const float FreedStance = 0.35f;
    // How many of his blows wear an absorbed soul's power.
    const int BorrowedBlows = 2;

    static readonly BossKind[] SoulOrder = { BossKind.Benibana, BossKind.Utsusemi, BossKind.Shirogane };

    // The soul standing beside him, the chain that binds it and the soul-fire round it.
    Boss soul;
    BossKind soulKind;
    float soulAge;
    bool reeling;
    SpriteRenderer soulChain;
    SpriteRenderer soulAura;
    // The soul he last swallowed, and how many of his blows still carry it.
    BossKind borrowed;
    int borrowedLeft;
    // The souls take their turns in order the first time round, then come at random, never the same twice running.
    int nextSoul;
    BossKind? lastSoul;
    // Freed souls he has already called this fight: nothing came, and he doesn't call them again.
    readonly List<BossKind> unanswered = new List<BossKind>();

    bool SoulOut => soul != null && !soul.IsDead;

    // A freed soul (SaveData.freed) can be called once, to find it gone; after that only the bound ones answer.
    bool Callable(BossKind kind) => !Save.Data.Freed(kind) || !unanswered.Contains(kind);
    bool CanSummon => System.Array.Exists(SoulOrder, Callable);

    BossKind NextSoul()
    {
        while (nextSoul < SoulOrder.Length && !Callable(SoulOrder[nextSoul])) nextSoul++;
        if (nextSoul < SoulOrder.Length)
        {
            lastSoul = SoulOrder[nextSoul++];
            return lastSoul.Value;
        }
        var open = new List<BossKind>(System.Array.FindAll(SoulOrder, Callable));
        BossKind pick = open[Random.Range(0, open.Count)];
        if (pick == lastSoul && open.Count > 1) pick = open[(open.IndexOf(pick) + 1) % open.Count];
        lastSoul = pick;
        return pick;
    }

    static BossLine SummonLine(BossKind kind) => kind switch
    {
        BossKind.Benibana => BossLine.SummonBenibana,
        BossKind.Utsusemi => BossLine.SummonUtsusemi,
        _ => BossLine.SummonShirogane,
    };

    static BossLine AnswerLine(BossKind kind) => kind switch
    {
        BossKind.Benibana => BossLine.EchoBenibana,
        BossKind.Utsusemi => BossLine.EchoUtsusemi,
        _ => BossLine.EchoShirogane,
    };

    // He tolls, the coffin-hammer opens and a boss's soul is dragged out on its chain to the far side of the field.
    IEnumerator CallSoul(BossKind kind)
    {
        // A freed soul's bell is silent: he calls, and nothing comes.
        if (Save.Data.Freed(kind))
        {
            unanswered.Add(kind);
            Say($"Kagetsune_Summon_{kind}Gone");
            PlayAction(art.parry);
            Toll(0.9f, 0.6f);
            yield return Wait(2.2f);
            yield break;
        }
        Speak(SummonLine(kind));
        PlayAction(art.parry);
        Toll(0.9f, 0.6f);
        float side = anchor.x > CameraShake.Center.x ? -1f : 1f;
        float x = Mathf.Clamp(anchor.x + side * 6.5f, LeftEdge + 3f, RightEdge - 3f);
        // The coffin cracks open: soul-fire spills from it.
        ElementFx.Glow(HammerHead, Teal, 1f, 7f, 0.6f);
        Fx.Play(Fx.SmokePuff, HammerHead, new Color(Teal.r, Teal.g, Teal.b, 0.8f), 2.4f);
        HitFlash.Flash(sprite, 0.15f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.6f, 0.7f);
        yield return Wait(0.35f);
        // Only one soul stands beside him at a time.
        if (SoulOut) yield break;

        Boss raised = Summon?.Invoke(kind, x);
        if (raised == null) yield break;
        soul = raised;
        soulKind = kind;
        soulAge = 0f;
        raised.Haunt(new Color(0.78f, 1f, 0.95f, 0.92f), new Color(Teal.r, Teal.g, Teal.b, 0.9f), new Color(0.85f, 1f, 0.97f, 1f));
        raised.Defeated += () => OnSoulFreed(raised);

        soulChain = Tiled(Fx.KageChain, HammerHead, HammerHead, 0.32f, Bronze, 4);
        transient.Remove(soulChain.gameObject);
        soulAura = Glowing(raised.Centre, new Color(Teal.r, Teal.g, Teal.b, 0f), 4f, 3, false);
        Sfx.Play(Sfx.MistSwirl, 0.8f, 0.8f);
        Fx.Play(Fx.SmokePuff, new Vector3(x, TopOfScreen - 1f, 0), new Color(Teal.r, Teal.g, Teal.b, 0.7f), 3f);
        yield return Wait(0.9f);
        // The soul speaks for itself, in his choir: it knows what it's been made to do.
        // Once it has told Oni how to free it, it fights its chain out loud.
        if (SoulOut)
        {
            string freeing = Dialogue.FreeingLine(kind);
            if (freeing != null && Save.Data.SeenIn(freeing) >= 0) Say($"{kind}_Echo_Defiant");
            else Speak(AnswerLine(kind));
        }
    }

    // The chain follows the soul every frame, taut from the coffin-hammer; the soul-fire round it breathes. Its time
    // runs on his clock: once it's fought long enough, he reels it in.
    void UpdateSoul()
    {
        if (soul == null || (soul.IsDead && !reeling))
        {
            if (soulChain != null || soulAura != null) ReleaseChain();
            return;
        }
        Vector3 to = soul.Centre;
        if (soulChain != null)
        {
            Lay(soulChain, HammerHead, to, soulChain.drawMode == SpriteDrawMode.Tiled ? 0.32f : 0.1f);
            soulChain.color = ChainColour(soulChain, 0.85f + 0.15f * Mathf.Sin(Time.time * 7f));
        }
        if (soulAura != null)
        {
            soulAura.transform.position = to + Vector3.forward * 0.2f;
            soulAura.color = new Color(Teal.r, Teal.g, Teal.b, 0.22f + 0.08f * Mathf.Sin(Time.time * 3.1f));
        }
        if (reeling || breaking || IsDead || soul.IsDead) return;
        soulAge += Time.deltaTime * Tempo;
        if (soulAge >= AbsorbAfter) StartCoroutine(Absorb());
    }

    void ReleaseChain()
    {
        if (soulChain != null) Destroy(soulChain.gameObject);
        if (soulAura != null) Destroy(soulAura.gameObject);
        soulChain = soulAura = null;
    }

    // Cut down, the soul breaks its chain: the links burst, and it flies home into his chest like a stone through
    // a bell, knocking his stance.
    void OnSoulFreed(Boss freed)
    {
        if (freed != soul) return;
        Vector3 from = freed.Centre;
        soul = null;
        if (soulChain != null)
        {
            Vector3 mid = soulChain.transform.position;
            if (Fx.Play(Fx.KageChainSnap, mid, Color.white, 2f) == null) Fx.Play(Fx.Impact, mid, Bronze, 1.8f);
            Debris.BurstSprites(mid, art.deathParticles, Bronze, 10, 0.7f, 6f);
        }
        if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 1f);
        else Sfx.Play(Sfx.ParryClang, 0.9f, 0.7f);
        ReleaseChain();
        if (!IsDead) StartCoroutine(SoulComesHome(from));
    }

    IEnumerator SoulComesHome(Vector3 from)
    {
        var mote = new GameObject("FreedSoul").AddComponent<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.KageSoulRise);
        mote.sprite = frames != null ? frames[0] : ProceduralSprites.Glow;
        mote.sortingOrder = 58;
        float size = frames != null ? 1.4f / frames[0].bounds.size.y : 1.4f;
        Vector3 lift = Vector3.up * 2.5f;
        const float Seconds = 0.7f;
        for (float t = 0; t < Seconds && !IsDead; t += Time.deltaTime)
        {
            float k = t / Seconds;
            // An arc up and over into the chest, the last stretch fastest.
            Vector3 to = Chest;
            Vector3 at = Vector3.Lerp(Vector3.Lerp(from, from + lift, k), Vector3.Lerp(from + lift, to, k), k * k);
            mote.transform.position = at + Vector3.back * 0.4f;
            mote.transform.localScale = Vector3.one * size * (1f + 0.5f * Mathf.Sin(t * 30f) * 0.1f);
            mote.color = new Color(0.85f, 1f, 0.97f, 0.95f);
            if (frames != null) mote.sprite = frames[(int)(t * 12f) % frames.Length];
            if (Random.value < 0.5f) Afterimage(mote, new Color(Teal.r, Teal.g, Teal.b, 0.5f));
            yield return null;
        }
        Destroy(mote.gameObject);
        if (IsDead) yield break;
        // It strikes him like a clapper: the bell he's made of rings against him.
        Toll(1f, 0.85f);
        Fx.Play(Fx.Impact, Chest, Teal, 3f);
        ElementFx.Glow(Chest, Teal, 1f, 9f, 0.5f);
        HitFlash.Flash(sprite, 0.12f);
        CameraShake.Add(0.4f);
        TimeRipple.Emit(Chest, 4f, 0.5f, 0.6f);
        StartCoroutine(Tremble(0.4f, 0.1f));
        AddStance(FreedStance);
    }

    // Left standing, the soul is dragged back into the coffin, and his next blows wear its power.
    IEnumerator Absorb()
    {
        reeling = true;
        Boss taken = soul;
        BossKind kind = soulKind;
        Speak(BossLine.SoulAbsorbed);
        if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.7f, 0.6f);
        Toll(0.8f, 0.55f);
        // The chain draws taut and glows as he hauls.
        for (float t = 0; t < 0.6f && taken != null; t += Time.deltaTime)
        {
            if (soulChain != null) soulChain.color = Color.Lerp(ChainColour(soulChain, 1f), Teal, t / 0.6f);
            yield return null;
        }
        Vector3 from = taken != null ? taken.Centre : Chest;
        if (taken != null) taken.Banish();
        soul = null;
        // The soul streams back along the chain into the coffin.
        for (int i = 0; i < 6; i++)
        {
            float k = i / 5f;
            ElementFx.Glow(Vector3.Lerp(from, HammerHead, k), Teal, 0.6f, 2.5f, 0.35f);
            yield return new WaitForSeconds(0.05f);
        }
        ReleaseChain();
        reeling = false;
        if (IsDead) yield break;
        Fx.Play(Fx.Explosion, HammerHead, Teal, 2f);
        ElementFx.Glow(Chest, Teal, 2f, 10f, 0.6f);
        HitFlash.Flash(sprite, 0.15f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.9f, 0.75f);
        CameraShake.Add(0.35f);
        borrowed = kind;
        borrowedLeft = BorrowedBlows;
    }

    // Takes the soul off the field with no reward and no power gained: his phase break, his fall, or the fight ending.
    void BanishSoul()
    {
        if (soul != null && !soul.IsDead) soul.Banish();
        soul = null;
        reeling = false;
        ReleaseChain();
    }

    // --- The power of a soul he has swallowed ---

    // A blow that can carry a borrowed power: only his plainer moves, so a borrowed pattern never closes the gap a
    // curtain or the ultimate leaves.
    static bool CarriesBorrowed(Move move) => move is Move.CofferSlam or Move.Toll or Move.Swarm or Move.Rend;

    void Borrow(Move move)
    {
        if (borrowedLeft <= 0 || !CarriesBorrowed(move)) return;
        borrowedLeft--;
        StartCoroutine(borrowed switch
        {
            BossKind.Benibana => BorrowedSmoke(),
            BossKind.Utsusemi => BorrowedHours(),
            _ => BorrowedBlades(),
        });
    }

    // Benibana's smoke rings, from his bells: two fans of swelling rings, aimed at the ronin.
    IEnumerator BorrowedSmoke()
    {
        yield return Wait(0.5f);
        for (int volley = 0; volley < 2 && !IsDead; volley++)
        {
            Vector3 mouth = Waist;
            Fx.Play(Fx.SmokePuff, mouth, new Color(Teal.r, Teal.g, Teal.b, 0.7f), 1.8f);
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
                ring.PassesShots = true;
                rings.Add(ring);
            }
            IgnoreEachOther(rings);
            yield return Wait(0.7f);
        }
    }

    // Utsusemi's hour marks: two struck where the ronin stands, each a beat after it's laid.
    IEnumerator BorrowedHours()
    {
        for (int i = 0; i < 2 && !IsDead; i++)
        {
            Vector3 spot = new Vector3(ronin.Bounds.center.x, ronin.Feet.y, 0);
            FxPlayer mark = Fx.Play(Fx.UtsuClockMark, spot, new Color(0.6f, 1f, 0.9f), 3f, null, 6);
            SpriteRenderer ring = mark == null ? Glowing(spot, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 2.2f, 6, false) : null;
            Sfx.Play(Sfx.ClockTick, 0.8f, 0.8f);
            yield return Wait(0.75f);
            Vector3 top = new Vector3(spot.x, TopOfScreen, 0);
            SpriteRenderer laser = Bar(top, spot, 0.35f, new Color(0.85f, 1f, 0.95f, 0.95f), 56);
            transient.Remove(laser.gameObject);
            Sfx.Play(Sfx.LanternBeam, 0.5f, 1.3f);
            if (ronin != null && Mathf.Abs(ronin.Bounds.center.x - spot.x) < 0.9f) Strike(spot + Vector3.up * 0.5f);
            Fx.Play(Fx.Impact, spot, Teal, 1.6f);
            GroundFire.Scorch(spot, soul: true);
            yield return new WaitForSeconds(0.15f);
            Destroy(laser.gameObject);
            if (ring != null) Destroy(ring.gameObject);
            if (mark != null) mark.Stop();
            yield return Wait(0.35f);
        }
    }

    // Shirogane's blade rain: one wave of ghost swords down every lane but the two round where the ronin isn't.
    IEnumerator BorrowedBlades()
    {
        const int LaneCount = 8;
        List<float> lanes = Lanes(LaneCount);
        int mine = 0;
        for (int i = 1; i < LaneCount; i++) if (Mathf.Abs(lanes[i] - ronin.Bounds.center.x) < Mathf.Abs(lanes[mine] - ronin.Bounds.center.x)) mine = i;
        // The gap is a lane or two from him: he has to move, but never far.
        int gap = Mathf.Clamp(mine + (Random.value < 0.5f ? -1 : 1), 0, LaneCount - 2);
        var tells = new List<SpriteRenderer>();
        var xs = new List<float>();
        for (int i = 0; i < LaneCount; i++)
        {
            if (i == gap || i == gap + 1) continue;
            xs.Add(lanes[i]);
            SpriteRenderer tell = Bar(new Vector3(lanes[i], TopOfScreen, 0), new Vector3(lanes[i], GroundY, 0), 0.12f, new Color(0.6f, 0.95f, 1f, 0.35f));
            transient.Remove(tell.gameObject);
            tells.Add(tell);
        }
        Sfx.Play(Sfx.BladeRing, 0.7f);
        yield return Wait(0.9f);
        foreach (SpriteRenderer tell in tells) if (tell != null) Destroy(tell.gameObject);
        if (IsDead) yield break;
        foreach (float x in xs)
        {
            Arrow blade = Shoot(new Vector3(x, TopOfScreen, 0), Vector3.down * 12f);
            Sprite[] frames = Fx.Frames(Fx.ShiroSword);
            if (frames != null) blade.Restyle(frames, 10f, new Color(0.8f, 1f, 0.95f), new Vector2(0.7f, 1.9f), 0.3f, new Color(Teal.r, Teal.g, Teal.b, 0.4f), 1.4f);
            else blade.Restyle(ProceduralSprites.White, new Color(0.85f, 0.97f, 1f), new Vector2(0.16f, 1.6f), 0.3f, new Color(Teal.r, Teal.g, Teal.b, 0.45f), 1.4f);
            blade.PassesCover = true;
            blade.PassesShots = true;
        }
        Sfx.Play(Sfx.GhostSwordFall, 0.9f);
    }
}
