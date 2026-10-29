using System.Collections;
using UnityEngine;

// His levy of the dead: he rings the bells on his armour and the souls sealed in it pour out of his chest, each one a
// soldier of the dead army that flies down and falls in to its slot in rank and file below him, then marches, drops
// and fires like any army while he fights on above it. A small company of foot soldiers on the Throne; on the Wraith a
// host, commanders and all.
public partial class Kagetsune
{
    // A levy is called at least this many moves after the last one was cut down (or, the first, after the fight opens).
    int LevyAtLatest => secondPhase ? 4 : 5;
    // Moves since the field was last clear of his levy.
    int movesSinceLevy = 3;

    bool LevyOut => LevyRemaining != null && LevyRemaining() > 0;

    // While he calls them he stands in super armour: no stagger, and only a fifth of each blow gets through, edged in
    // teal soul-fire.
    bool levyArmour;
    SpriteOutline armourEdge;
    protected override bool SuperArmor => levyArmour;
    protected override float ArmorDamageTaken => 0.2f;
    protected override Color ArmorSpark => Teal;

    void Armour(bool on)
    {
        levyArmour = on;
        if (on && armourEdge == null) armourEdge = SpriteOutline.Wrap(sprite, new Color(Teal.r, Teal.g, Teal.b, 0.95f), Color.white);
        if (!on && armourEdge != null)
        {
            Destroy(armourEdge);
            armourEdge = null;
        }
    }

    // The formation's second rank stands just under his feet, so its front rank starts above the cover; a host's back
    // rank flanks his legs.
    float LevyTop(int rows) => sprite.bounds.min.y + Extents.y * LevyFeet + Mathf.Max(0, rows - 2) * LevyRankSpacing;
    // The army's own rank spacing (SamuraiArmy.spacingY).
    const float LevyRankSpacing = 1.6f;
    // Where his feet are on the square canvas, from its bottom, as a share of the half-height.
    const float LevyFeet = 0.2f;

    LevelConfig LevyConfig() => secondPhase
        ? new LevelConfig
        {
            columns = lastBell ? 14 : 13, rows = 3, topRowY = LevyTop(3), baseSpeed = 2.2f, maxSpeedMultiplier = 3.2f,
            fireInterval = 1f, aimedShotChance = 0.35f, arrowSpeed = 8f, diveInterval = 6f,
            rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Yurei },
        }
        : new LevelConfig
        {
            columns = 8, rows = 2, topRowY = LevyTop(2), baseSpeed = 1.8f, maxSpeedMultiplier = 2.6f,
            fireInterval = 1.5f, aimedShotChance = 0.25f, arrowSpeed = 7.5f,
            rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru },
        };

    // He tolls his bells and his chest flares open; the souls stream out of it into formation, and he goes on with
    // his own moves while they fall in.
    IEnumerator RaiseLevy()
    {
        Armour(true);
        Bark(BossLine.Levy, 8f);
        PlayAction(art.parry);
        SpriteRenderer gather = Glowing(Chest, new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 57);
        for (int i = 0; i < 3; i++)
        {
            Toll(0.7f + 0.1f * i, 0.6f + 0.08f * i);
            TollRing(Waist, 5f + 2f * i, 0.8f);
            for (float t = 0; t < 0.3f; t += Time.deltaTime * Tempo)
            {
                float k = (i + t / 0.3f) / 3f;
                gather.transform.position = Chest + Vector3.back * 0.1f;
                gather.transform.localScale = Vector3.one * Mathf.Lerp(1f, 5f, k);
                gather.color = new Color(Teal.r, Teal.g, Teal.b, 0.75f * k);
                yield return null;
            }
        }
        Drop(gather);
        if (Muster == null || !Muster(LevyConfig(), Chest, Teal))
        {
            Armour(false);
            yield break;
        }
        CameraShake.Add(0.3f);
        ElementFx.Glow(Chest, Teal, 0.4f, 7f, 0.6f);
        if (Sfx.Has(Sfx.SkullShriek)) Sfx.Play(Sfx.SkullShriek, 0.6f, 0.8f);
        Sfx.Play(Sfx.SamuraiCry, 0.9f, 0.8f);
        // He holds, armoured, until the last of them has fallen in (GameManager.MusterLevy's flight time and the
        // soul-fire burning off them).
        float pour = secondPhase ? 2.9f : 2.1f;
        for (float t = 0; t < pour; t += Time.deltaTime * Tempo)
        {
            if (Random.value < 0.15f) ElementFx.Glow(Chest, new Color(Teal.r, Teal.g, Teal.b, 0.5f), 1f, 3f, 0.25f);
            yield return null;
        }
        Armour(false);
    }

    // His break and his fall send the levy back into him, and take off the armour he called it in.
    void DisbandLevy()
    {
        Armour(false);
        if (LevyOut) Disband?.Invoke(Teal);
    }
}
