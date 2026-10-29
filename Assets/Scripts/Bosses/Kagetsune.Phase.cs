using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// His phase break (the coffin's chains snap) and his fall (the bells go silent one by one).
public partial class Kagetsune
{
    // --- Phase break ---

    // Faint: the fire is the souls round him, not a wall in front of the field.
    const float SoulFireAlpha = 0.32f;

    // The coffin's chains snap one by one as the bells clamour faster, the souls tear loose in a swarm of skulls,
    // and his eyes go from red to teal: the souls are looking out now.
    protected override IEnumerator PhaseBreak()
    {
        breaking = true;
        ultimate = false;
        BanishSoul();
        DisbandLevy();
        // The Wraith's host comes early: his second move after the break.
        movesSinceLevy = LevyAtLatest - 1;
        StopCage();
        ResetView();
        exposedUntil = 0;
        seals.Clear();
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        PlayAction(art.hit);
        Music.Duck(0.25f, 9f);

        var chains = new List<SpriteRenderer>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = HammerHead + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.5f, 0.5f), 0);
            Vector3 b = Centre + new Vector3((i - 1.5f) * Extents.x * 0.4f, -Extents.y * 0.1f, 0);
            chains.Add(Tiled(Fx.KageChain, a, b, 0.45f, Bronze, 57));
        }
        StartCoroutine(Tremble(2.4f, 0.16f));
        // The bells: slow at first, then a clamour.
        for (int i = 0; i < 10; i++)
        {
            Toll(0.5f + 0.04f * i, 0.8f + 0.06f * i);
            TollRing(Waist, 3f + 0.3f * i, 0.5f + 0.05f * i);
            yield return new WaitForSeconds(Mathf.Lerp(0.38f, 0.08f, i / 9f));
        }
        foreach (SpriteRenderer chain in chains)
        {
            Vector3 at = chain.transform.position;
            if (Sfx.Has(Sfx.ChainSnap)) Sfx.Play(Sfx.ChainSnap, 0.9f);
            else Sfx.Play(Sfx.ParryClang, 0.8f, 0.7f);
            if (Fx.Play(Fx.KageChainSnap, at, Color.white, 2.2f) == null) Fx.Play(Fx.Impact, at, Bronze, 1.5f);
            HitFlash.Flash(sprite, 0.06f);
            Debris.BurstSprites(at, art.deathParticles, Bronze, 6, 0.6f, 6f);
            CameraShake.Add(0.2f);
            Drop(chain);
            yield return new WaitForSeconds(0.22f);
        }
        // The souls tear loose.
        CameraShake.Add(0.7f);
        Fx.Play(Fx.Explosion, Chest, Teal, 3f);
        Debris.BurstSprites(Chest, art.deathParticles, Teal, 40, 1.2f, 10f);
        ElementFx.Glow(Chest, Teal, 2f, 14f, 1f);
        HitFlash.Flash(sprite, 0.2f);
        TimeRipple.Emit(Chest, 8f, 0.7f, 0.9f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 1f, 0.8f);
        WearPhaseArt();
        PlayIdle();
        // Two teal points behind the visor, where the red was.
        eyes = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            eyes[i] = Glowing(Eye(i), new Color(Teal.r, Teal.g, Teal.b, 0.9f), 0.45f, 7, false);
            eyes[i].sprite = ProceduralSprites.Glint;
            eyes[i].transform.SetParent(transform, true);
        }
        // The souls burn round him from now on: soul-fire at his feet, behind him.
        Sprite[] fire = Fx.Frames(Fx.KageSoulFire);
        if (fire != null)
        {
            Vector3 feet = new Vector3(Centre.x, sprite.bounds.min.y + Extents.y * 0.32f, 0);
            soulFire = Fx.Play(Fx.KageSoulFire, feet + Vector3.forward * 0.2f, new Color(0.7f, 1f, 0.92f, SoulFireAlpha),
                Extents.x * 0.85f / fire[0].bounds.size.x, transform, sprite.sortingOrder - 2);
        }
        yield return new WaitForSeconds(0.6f);

        if (PhaseCard != null) yield return PhaseCard(this);
        else
        {
            Speak(BossLine.Phase);
            yield return new WaitForSeconds(2.2f);
        }
        // The first soul comes within two moves of the chains snapping.
        movesSinceSoul = SoulAtLatest - 2;
        movesSinceUltimate = 0;
        movesSinceParry = 0;
        breaking = false;
    }

    // --- Defeat ---

    // The coffin bursts. One by one his bells fall silent, each toll lower and quieter than the last, a soul rising
    // free with each, until there is silence and the armor stands hollow, then fades.
    protected override IEnumerator Fall()
    {
        seals.Clear();
        Sfx.Stop(Sfx.SoulWail);
        Sfx.Stop(Sfx.LanternBeam);
        if (ronin != null && ronin.ParrySealed != null) ronin.ParrySealed = null;
        PlayAction(art.hit);
        CameraShake.Add(0.9f);
        Fx.Play(Fx.Explosion, HammerHead, Teal, 3.5f);
        Debris.BurstSprites(HammerHead, art.deathParticles, Teal, 50, 1.3f, 10f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 1f, 0.7f);
        ElementFx.Glow(Centre, Teal, 3f, 16f, 1.2f);
        yield return new WaitForSeconds(1f);
        if (art.death != null && art.death.Length > 0) animator.Play(art.death, false, null, art.actionFps * 0.5f);

        const int Bells = 7;
        for (int i = 0; i < Bells; i++)
        {
            float k = i / (Bells - 1f);
            Toll(Mathf.Lerp(0.9f, 0.25f, k), Mathf.Lerp(0.9f, 0.5f, k));
            TollRing(Waist, Mathf.Lerp(6f, 3f, k), Mathf.Lerp(0.9f, 0.3f, k));
            RisingSoul(Centre + new Vector3(Random.Range(-Extents.x, Extents.x) * 0.6f, Random.Range(-0.3f, 0.5f) * Extents.y, 0));
            yield return new WaitForSeconds(Mathf.Lerp(0.7f, 1.05f, k));
        }
        // Silence. The armor sinks hollow and fades.
        yield return new WaitForSeconds(0.6f);
        Vector3 from = anchor;
        for (float t = 0; t < 2.2f; t += Time.deltaTime)
        {
            float k = t / 2.2f;
            anchor = from + Vector3.down * (0.5f * k);
            body.MovePosition(anchor);
            Color c = Color.Lerp(bodyTint, new Color(0.3f, 0.3f, 0.32f), k);
            c.a = 1f - k * k;
            sprite.color = c;
            if (chestGlow != null) chestGlow.color = new Color(Teal.r, Teal.g, Teal.b, 0.4f * (1f - k));
            if (eyes != null) foreach (SpriteRenderer eye in eyes) if (eye != null) eye.color = new Color(Teal.r, Teal.g, Teal.b, 0.75f * (1f - k));
            if (soulFire != null) soulFire.Color = new Color(0.7f, 1f, 0.92f, SoulFireAlpha * (1f - k));
            if (Random.value < 0.1f) RisingSoul(Centre + (Vector3)Random.insideUnitCircle * Extents.x * 0.5f);
            yield return null;
        }
        sprite.color = Color.clear;
        if (chestGlow != null) chestGlow.color = Color.clear;
        if (eyes != null) foreach (SpriteRenderer eye in eyes) if (eye != null) eye.color = Color.clear;
        if (soulFire != null) soulFire.Stop();
        // The last souls finish rising before he's gone (they rise on his clock).
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    // A freed soul: a teal wisp drifting up and away, its fire leaving with it.
    // A freed soul rising and fading. It drives itself, so it finishes its climb after he is gone.
    public static void RisingSoul(Vector3 from)
    {
        var soul = new GameObject("FreedSoul").AddComponent<SpriteRenderer>();
        Sprite[] frames = Fx.Frames(Fx.KageSoulRise);
        soul.sortingOrder = 58;
        soul.transform.position = from + Vector3.back * 0.3f;
        if (frames == null)
        {
            soul.sprite = ProceduralSprites.Glow;
            soul.gameObject.AddComponent<FreedSoul>().Begin(from, Teal);
            return;
        }
        // A little ghost of its own, drawn in its own colours.
        soul.sprite = frames[0];
        soul.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 12f);
        soul.gameObject.AddComponent<FreedSoul>().Begin(from, new Color(0.9f, 1f, 0.97f));
    }
}
