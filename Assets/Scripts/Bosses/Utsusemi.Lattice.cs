using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lace Lattice: curtains of lace diamonds with a gap or two in each layer.
public partial class Utsusemi
{
    // Lace Lattice: the shroud flares and a curtain of lace diamonds drifts down with a gap or two in it. Each
    // diamond breaks in one hit, so the ronin can cut his own way through. Two offset layers in the second phase.
    IEnumerator LaceLattice()
    {
        if (Random.value < 0.5f) Speak(BossLine.Lattice);
        // Its attack clip has the sword in hand; with the sword wheeling it flares its shroud from its stance.
        if (!swordThrown) PlayAction(art.attack);
        ElementFx.Glow(Centre, Lace, 1f, 5f, 0.5f, transform);
        Sfx.Play(Sfx.FrostChill, 0.6f, 0.8f);
        yield return Wait(0.35f);

        // Two layers with two ways through (three layers with one, once unwound). Each layer's ways through step on
        // from the last's, never further than he can follow before it lands.
        int layers = secondPhase ? 3 : 2;
        float interval = secondPhase ? 0.6f : 0.8f;
        float lo = LeftEdge + 1.6f, hi = RightEdge - 1.6f;
        var gaps = new List<float>();
        for (int g = 0; g < (secondPhase ? 1 : 2); g++) gaps.Add(PickGap(gaps, lo, hi));
        for (int layer = 0; layer < layers; layer++)
        {
            if (layer > 0)
            {
                yield return Wait(interval);
                for (int g = 0; g < gaps.Count; g++)
                {
                    float want = gaps[g] + Random.Range(-1f, 1f) * BulletHell.MaxDodgeSpeed * interval;
                    float next = BulletHell.NextGap(gaps[g], want, interval * 0.9f, lo, hi);
                    BulletHell.GapOk(gaps[g], next, interval, LatticeGap, "Lace Lattice");
                    gaps[g] = next;
                }
            }
            yield return BulletHell.Room(60);
            SpinLattice((layer % 2) * 0.55f, gaps);
            Sfx.Play(Sfx.WardShatter, 0.5f, 1.8f);
        }
    }

    // The clear width of each way through the lace, edge to edge.
    const float LatticeGap = 2.4f;
    const float DiamondRadius = 0.22f;

    // A way through somewhere in the field, well apart from any already chosen.
    static float PickGap(List<float> taken, float lo, float hi)
    {
        float x = Random.Range(lo, hi);
        for (int tries = 0; tries < 12; tries++)
        {
            x = Random.Range(lo, hi);
            if (taken.TrueForAll(g => Mathf.Abs(g - x) >= 4f)) break;
        }
        return x;
    }

    void SpinLattice(float offset, List<float> gaps)
    {
        const float spacing = 1.1f;
        const int rows = 3;
        float speed = secondPhase ? 3f : 2.6f;
        float left = LeftEdge + 0.4f + offset;
        int columns = Mathf.FloorToInt((RightEdge - 0.4f - left) / spacing) + 1;
        // Once unwound its lace burns: dark steel cracked with molten light.
        Sprite[] frames = secondPhase ? Fx.Frames(Fx.UtsuLaceP2) ?? Fx.Frames(Fx.UtsuLace) : Fx.Frames(Fx.UtsuLace);
        Color glow = secondPhase ? new Color(Molten.r, Molten.g, Molten.b, 0.35f) : new Color(0.8f, 0.85f, 1f, 0.25f);
        float y0 = anchor.y - 1.6f;
        for (int row = 0; row < rows; row++)
        {
            for (int c = 0; c < columns; c++)
            {
                float x = left + c * spacing + (row % 2 == 1 ? spacing / 2f : 0f);
                // Every row leaves the gap clear by its own diamonds, so the offset rows can't close it.
                if (gaps.Exists(g => Mathf.Abs(x - g) < LatticeGap / 2f + DiamondRadius)) continue;
                Arrow diamond = BulletHell.Enlist(Shoot(new Vector3(x, y0 + row * 0.9f, 0), Vector3.down * speed));
                if (frames != null) diamond.Restyle(frames, 8f, Color.white, new Vector2(0.7f, 0.7f), 0.22f, glow, 0.7f);
                else
                {
                    diamond.Restyle(ProceduralSprites.White, Lace, new Vector2(0.42f, 0.42f), 0.22f, new Color(0.8f, 0.85f, 1f, 0.3f), 0.7f);
                    diamond.Art.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                }
                diamond.PassesCover = true;
                lattice.Add(diamond);
            }
        }
        lattice.RemoveAll(a => a == null);
    }
}
