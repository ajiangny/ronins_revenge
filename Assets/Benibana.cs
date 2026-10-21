using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 1's warlord: a red-haired ronin in a torn black kimono who fights with her kiseru pipe, a rosary chain
// and her katana. She duels alone. Her signature move, the Crimson Iaido, can be parried straight back at her
// to break her stance.
public class Benibana : Boss
{
    public const int StartingHealth = 45;

    static readonly Color Crimson = new Color(0.95f, 0.15f, 0.22f);
    static readonly Color Silver = new Color(0.85f, 0.88f, 0.95f);
    static readonly Color Smoke = new Color(0.6f, 0.58f, 0.62f, 0.8f);
    static readonly Color VeilColour = new Color(0.16f, 0.14f, 0.17f, 0.72f);

    enum Move { SmokeRings, RosaryLash, Iaido }

    public override string Title => "BENIBANA";
    public override string Epithet => "The Crimson Smoke";
    public override Color Colour => Crimson;

    bool secondPhase;
    bool breaking;
    Move? lastMove;
    int movesSinceIaido;
    SpriteRenderer eyes;
    readonly List<VeilPuff> veil = new List<VeilPuff>();

    protected override bool Busy => breaking;
    float GroundY => ronin.Feet.y - 0.3f;
    Vector3 Mouth => Centre + Vector3.up * 0.5f;

    protected override IEnumerator Fight()
    {
        while (true)
        {
            while (breaking) yield return null;
            Beat();
            yield return Wait(secondPhase ? 1.1f : 1.6f);
            yield return BlinkTo(RandomX(3f), new Color(Crimson.r, Crimson.g, Crimson.b, 0.55f));
            yield return Wait(0.25f);

            Move move = NextMove();
            lastMove = move;
            movesSinceIaido = move == Move.Iaido ? 0 : movesSinceIaido + 1;
            switch (move)
            {
                case Move.SmokeRings: yield return SmokeRings(); break;
                case Move.RosaryLash: yield return RosaryLash(); break;
                case Move.Iaido: yield return Iaido(); break;
            }
        }
    }

    // Never the same move twice in a row, and the Iaido at least every third move so the duel keeps its rhythm.
    Move NextMove()
    {
        if (movesSinceIaido >= 2 && lastMove != Move.Iaido) return Move.Iaido;
        var options = new List<Move> { Move.SmokeRings, Move.RosaryLash, Move.Iaido };
        if (lastMove is Move last) options.Remove(last);
        return options[Random.Range(0, options.Count)];
    }

    // Kiseru Smoke Rings: she draws on the pipe, then puffs a fan of slow rings that swell as they fall.
    // In her second phase silver crosses hide inside them.
    IEnumerator SmokeRings()
    {
        PlayAction(art.parry);
        Fx.Play(Fx.SmokePuff, Mouth, Smoke, 1.2f);
        Sfx.Play(Sfx.FrostChill, 0.6f, 0.55f);
        yield return Wait(0.55f);

        int count = secondPhase ? 5 : 4;
        float spread = 32f;
        Vector3 aim = (ronin.Bounds.center - Mouth).normalized;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-spread, spread, i / (count - 1f));
            Vector3 velocity = Quaternion.Euler(0, 0, angle) * aim * 3.4f;
            Arrow ring = Shoot(Mouth, velocity);
            Sprite[] ringFrames = Fx.Frames(Fx.BeniSmokeRing);
            if (ringFrames != null) ring.Restyle(ringFrames, 10f, new Color(1f, 1f, 1f, 0.9f), new Vector2(1.15f, 1.15f), 0.3f, new Color(0.5f, 0.5f, 0.55f, 0.3f), 1.4f);
            else ring.Restyle(ProceduralSprites.Disc, Smoke, new Vector2(0.75f, 0.75f), 0.3f, new Color(0.5f, 0.5f, 0.55f, 0.35f), 1.4f);
            ring.gameObject.AddComponent<Swell>().Begin(1f, 1.9f, 2.2f);
            if (secondPhase && i % 2 == 1) Cross(Mouth, velocity * 1.02f);
        }
        Fx.Play(Fx.SmokePuff, Mouth, Smoke, 1.8f);
        Sfx.Play(Sfx.ParrySwing, 0.7f, 0.6f);
    }

    // Rosary Lash: a silver line marks a column, then her cross-chain slams down it, smashing blockers. It hangs
    // there as a wall for a moment before its crosses break off and fall. Two columns in her second phase.
    IEnumerator RosaryLash()
    {
        var columns = new List<float> { ronin.Bounds.center.x };
        if (secondPhase)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float other = columns[0] + side * 3.5f;
            if (Mathf.Abs(other - CameraShake.Center.x) > HalfWidth - 1f) other = columns[0] - side * 3.5f;
            columns.Add(other);
        }

        var tells = new List<SpriteRenderer>();
        foreach (float x in columns)
        {
            tells.Add(Bar(new Vector3(x, Centre.y, 0), new Vector3(x, GroundY, 0), 0.08f, new Color(Silver.r, Silver.g, Silver.b, 0.4f)));
        }
        for (int beat = 0; beat < 3; beat++)
        {
            Sfx.Play(Sfx.BlockerHit, 0.45f, 1.9f);
            yield return Wait(0.25f);
        }

        var chains = new List<GameObject>();
        foreach (float x in columns)
        {
            Vector3 top = new Vector3(x, Centre.y, 0);
            Vector3 bottom = new Vector3(x, GroundY, 0);
            chains.Add(Chain(top, bottom));
            if (Fx.Play(Fx.BeniChainImpact, bottom + Vector3.up * 0.3f, Color.white, 1.8f) == null)
            {
                Fx.Play(Fx.Impact, bottom + Vector3.up * 0.4f, Color.white, 1.6f);
            }
            Debris.BurstSprites(bottom, art.deathParticles, Silver, 10, 0.8f, 7f);
            SmashBlockers(x, top.y, bottom.y);
        }
        foreach (SpriteRenderer tell in tells) Destroy(tell.gameObject);
        CameraShake.Add(0.35f);
        Sfx.Play(Sfx.BlockerBreak, 1f, 0.8f);

        // The chain is a wall while it hangs: touching it costs a life, and parrying does nothing.
        for (float t = 0; t < 1f; t += Time.deltaTime * Mathf.Max(Tempo, 0.5f))
        {
            foreach (float x in columns)
            {
                if (Mathf.Abs(ronin.Bounds.center.x - x) < 0.55f) ronin.TakeHit(new Vector3(x, ronin.Bounds.center.y, 0));
            }
            yield return null;
        }

        foreach (float x in columns)
        {
            for (int i = 0; i < 4; i++)
            {
                float y = Mathf.Lerp(Centre.y - 1f, GroundY + 3.5f, i / 3f);
                Cross(new Vector3(x, y, 0), Quaternion.Euler(0, 0, Random.Range(-22f, 22f)) * Vector3.down * 5.5f);
            }
        }
        foreach (GameObject chain in chains)
        {
            transient.Remove(chain);
            foreach (SpriteRenderer link in chain.GetComponentsInChildren<SpriteRenderer>()) Fader.Fade(link, 0.25f);
            Destroy(chain, 0.3f);
        }
        Sfx.Play(Sfx.WardShatter, 0.6f, 1.4f);
    }

    // Crimson Iaido: she sheathes her blade while a red line flashes at her height, then draws, and a crescent of
    // red light sweeps down at the ronin. Parried on time, it flies back and breaks her stance. In her second
    // phase she cuts an X: two crescents that cross over the ronin.
    IEnumerator Iaido()
    {
        float left = CameraShake.Center.x - HalfWidth;
        float right = CameraShake.Center.x + HalfWidth;
        SpriteRenderer line = Bar(new Vector3(left, Centre.y, 0), new Vector3(right, Centre.y, 0), 0.06f, Crimson);
        ElementFx.Glow(Centre, Crimson, 1f, 3f, 0.6f, transform);
        float windUp = secondPhase ? 0.65f : 0.8f;
        for (float t = 0; t < windUp; t += Time.deltaTime * Mathf.Max(Tempo, 0.3f))
        {
            Color c = Crimson;
            c.a = Mathf.PingPong(t * 6f, 1f) * 0.8f + 0.2f;
            line.color = c;
            yield return null;
        }
        transient.Remove(line.gameObject);
        Destroy(line.gameObject);

        PlayAction(art.attack);
        if (Fx.Play(Fx.SlashArc, Centre, Crimson, 3f) == null) ElementFx.Glow(Centre, Crimson, 1f, 4f, 0.25f);
        // The draw streak cuts through her at sword height, from her left to well past her right.
        Fx.Play(Fx.BeniIaidoFlash, Centre + new Vector3(-2.2f, -0.5f, 0), Color.white, 3f);
        Sfx.Play(Sfx.ParrySwing, 1f, 0.55f);
        CameraShake.Add(0.2f);

        Vector3 target = ronin.Bounds.center;
        if (secondPhase)
        {
            Crescent(Centre + Vector3.left * 2.5f, target);
            Crescent(Centre + Vector3.right * 2.5f, target);
        }
        else
        {
            Crescent(Centre, target);
        }
        yield return Wait(0.3f);
    }

    void Crescent(Vector3 from, Vector3 target)
    {
        Vector3 velocity = (target - from).normalized * 10f;
        Arrow wave = Shoot(from, velocity);
        Sprite[] frames = Fx.Frames(Fx.BeniCrescent);
        if (frames != null) wave.Restyle(frames, 14f, Color.white, new Vector2(3.2f, 2f), 0.55f, new Color(1f, 0.1f, 0.15f, 0.45f), 3.5f);
        else wave.Restyle(ProceduralSprites.Glow, new Color(1f, 0.25f, 0.3f), new Vector2(3.2f, 1f), 0.55f, new Color(1f, 0.1f, 0.15f, 0.5f), 3.5f);
        wave.MakeReflectable(transform);
    }

    // A small silver cross off her rosary.
    void Cross(Vector3 position, Vector3 velocity)
    {
        Arrow cross = Shoot(position, velocity);
        Sprite[] frames = Fx.Frames(Fx.BeniCross);
        if (frames != null) cross.Restyle(frames, 10f, Color.white, new Vector2(1.1f, 1.3f), 0.16f, new Color(0.8f, 0.85f, 1f, 0.35f), 0.8f);
        else cross.Restyle(cross.Sprite, Silver, new Vector2(0.45f, 0.9f), 0.12f, new Color(0.8f, 0.85f, 1f, 0.4f), 0.8f);
    }

    // Her rosary chain hanging from top to bottom: a column of silver links, or a plain bar without the art.
    GameObject Chain(Vector3 top, Vector3 bottom)
    {
        Sprite[] link = Fx.Frames(Fx.BeniChain);
        if (link == null) return Bar(top, bottom, 0.32f, Silver, 54).gameObject;

        var chain = new GameObject("Chain");
        transient.Add(chain);
        // Drawn at twice the art's size so the links read as the chain from her sheet.
        const float scale = 2f;
        float step = link[0].bounds.size.y * scale;
        for (float y = top.y; y > bottom.y - step * 0.5f; y -= step)
        {
            var piece = new GameObject("Link").AddComponent<SpriteRenderer>();
            piece.transform.SetParent(chain.transform, false);
            piece.transform.position = new Vector3(top.x, y, -0.2f);
            piece.transform.localScale = Vector3.one * scale;
            piece.sprite = link[0];
            piece.sortingOrder = 54;
        }
        return chain;
    }

    void SmashBlockers(float x, float top, float bottom)
    {
        Vector3 centre = new Vector3(x, (top + bottom) / 2f, 0);
        Vector3 halfExtents = new Vector3(0.35f, (top - bottom) / 2f, 2f);
        foreach (Collider hit in Physics.OverlapBox(centre, halfExtents))
        {
            if (hit.TryGetComponent(out Blocker blocker)) blocker.Demolish();
        }
    }

    protected override void OnDamaged()
    {
        if (!secondPhase && HealthFraction <= 0.5f) StartCoroutine(PhaseBreak());
    }

    // At half health her kimono tears and her hair comes loose; she exhales a veil of smoke over the top of the
    // screen and fights on from inside it, only her eyes showing.
    IEnumerator PhaseBreak()
    {
        secondPhase = true;
        breaking = true;
        Interrupt();
        SetInvulnerable(1.8f);
        CameraShake.Add(0.5f);
        Sfx.Play(Sfx.StageStart, 1f, 0.7f);
        Fx.Play(Fx.SlashArc, Centre, Crimson, 4f);
        Fx.Play(Fx.BeniPetals, Centre, Color.white, 2.5f);
        Debris.BurstSprites(Centre, art.deathParticles, Crimson, 30, 1.2f, 8f);
        for (int i = 0; i < 6; i++)
        {
            Fx.Play(Fx.SmokePuff, Centre + (Vector3)Random.insideUnitCircle * 2f, Smoke, 2.5f);
        }
        bodyTint = new Color(0.8f, 0.45f, 0.5f);
        AddEyes();
        yield return new WaitForSeconds(0.6f);
        SpawnVeil();
        yield return new WaitForSeconds(1.2f);
        breaking = false;
        RestartFight();
    }

    // Glowing red eyes drawn above the smoke, so she can always be found inside it.
    void AddEyes()
    {
        eyes = new GameObject("Eyes").AddComponent<SpriteRenderer>();
        eyes.transform.SetParent(sprite.transform, false);
        eyes.transform.position = sprite.bounds.center + Vector3.up * sprite.bounds.extents.y * 0.55f + Vector3.back * 0.3f;
        eyes.transform.localScale = Vector3.one * 0.35f / transform.lossyScale.x;
        eyes.sprite = ProceduralSprites.Glow;
        eyes.color = new Color(1f, 0.2f, 0.25f, 0.9f);
        eyes.sortingOrder = 45;
    }

    void SpawnVeil()
    {
        float centre = CameraShake.Center.x;
        for (int i = 0; i < 9; i++)
        {
            float x = centre + Mathf.Lerp(-HalfWidth, HalfWidth, i / 8f);
            float y = anchor.y + Random.Range(-1.2f, 1.6f);
            veil.Add(VeilPuff.Create(new Vector3(x, y, 0), Random.Range(5f, 7f), VeilColour, centre, HalfWidth + 3f, Fx.Frames(Fx.BeniSmokeVeil)));
        }
    }

    // Fire burns holes in the smoke veil.
    public override void Burn(Vector3 point, float radius)
    {
        foreach (VeilPuff puff in veil)
        {
            if (puff != null) puff.Burn(point, radius * 2.5f);
        }
    }

    protected override IEnumerator Fall()
    {
        foreach (VeilPuff puff in veil)
        {
            if (puff != null) puff.Dissolve();
        }
        if (eyes != null) Destroy(eyes.gameObject);
        // The pipe falls and the blade snaps.
        Debris.BurstSprites(Centre, art.deathParticles, Silver, 16, 0.9f, 8f);
        Fx.Play(Fx.BeniPetals, Centre, Color.white, 3f);
        Sfx.Play(Sfx.WardShatter, 1f, 0.8f);
        return base.Fall();
    }
}

// Grows a shot's whole transform (hitbox included) as it flies.
public class Swell : MonoBehaviour
{
    float from, to, seconds, age;
    Vector3 baseScale;

    public void Begin(float fromScale, float toScale, float duration)
    {
        from = fromScale;
        to = toScale;
        seconds = duration;
        baseScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.localScale = baseScale * Mathf.Lerp(from, to, Mathf.Clamp01(age / seconds));
    }
}

// One drifting cloud of Benibana's smoke veil. It wraps around the screen and thins where fire burns it.
public class VeilPuff : MonoBehaviour
{
    SpriteRenderer sprite;
    Color colour;
    float drift;
    float centre;
    float wrap;
    float burnt;
    float fadeIn;
    bool dissolving;

    // frames, when the veil art exists, replace the soft glow with a drifting cloud drawn at about the same size.
    public static VeilPuff Create(Vector3 position, float size, Color colour, float centreX, float wrapDistance, Sprite[] frames = null)
    {
        var puff = new GameObject("Veil").AddComponent<VeilPuff>();
        puff.transform.position = position + Vector3.back * 0.4f;
        puff.transform.localScale = Vector3.one * size;
        puff.sprite = puff.gameObject.AddComponent<SpriteRenderer>();
        puff.sprite.sprite = ProceduralSprites.Glow;
        puff.sprite.sortingOrder = 40;
        if (frames != null && frames.Length > 0)
        {
            puff.transform.localScale = Vector3.one * (size * 0.8f / frames[0].bounds.size.x);
            puff.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
            // The cloud art is already dark; only its alpha comes from the colour.
            colour = new Color(1f, 1f, 1f, 0.85f);
        }
        puff.colour = colour;
        puff.drift = Random.Range(0.3f, 0.7f) * (Random.value < 0.5f ? -1f : 1f);
        puff.centre = centreX;
        puff.wrap = wrapDistance;
        puff.sprite.color = Color.clear;
        return puff;
    }

    public void Burn(Vector3 point, float radius)
    {
        if (Vector2.Distance(point, transform.position) < radius + transform.localScale.x * 0.3f) burnt = 1f;
    }

    public void Dissolve()
    {
        dissolving = true;
    }

    void Update()
    {
        Vector3 position = transform.position;
        position.x += drift * Time.deltaTime;
        if (position.x > centre + wrap) position.x -= wrap * 2f;
        else if (position.x < centre - wrap) position.x += wrap * 2f;
        transform.position = position;

        fadeIn = Mathf.MoveTowards(fadeIn, dissolving ? 0f : 1f, Time.deltaTime * (dissolving ? 0.6f : 1.2f));
        // Burnt smoke clears at once and creeps back over a few seconds.
        burnt = Mathf.MoveTowards(burnt, 0f, Time.deltaTime / 4f);
        Color c = colour;
        c.a *= fadeIn * (1f - burnt * 0.85f);
        sprite.color = c;
        if (dissolving && fadeIn <= 0f) Destroy(gameObject);
    }
}
