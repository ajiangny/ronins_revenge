using System.Collections.Generic;
using UnityEngine;

// The round's one Elite (LevelConfig.eliteHealth): picked at spawn, firing its kind's shot on its own timer. Veteran
// rows (LevelConfig.rowHealth) are raised here too.
public partial class SamuraiArmy
{
    Samurai elite;
    float nextEliteShot;

    public Samurai Elite => elite;

    // Raises the veteran rows and makes one soldier the round's Elite, once the formation is built (SpawnStage).
    void Promote(LevelConfig c)
    {
        elite = null;
        foreach (Samurai soldier in soldiers)
        {
            int hp = c.rowHealth != null && soldier.Row < c.rowHealth.Length ? c.rowHealth[soldier.Row] : 0;
            // A row's health only ever toughens it: a shieldbearer's own two hits aren't lowered to a veteran's.
            if (hp > soldier.MaxHealth) soldier.MakeVeteran(hp);
        }

        if (c.eliteHealth <= 0 || soldiers.Count == 0) return;
        // It must be able to shoot, and the Taisho already has its spear; with no such soldier, anyone will do.
        List<Samurai> candidates = soldiers.FindAll(s => s.CanFireKind && s.Kind != SamuraiKind.Taisho);
        if (candidates.Count == 0) candidates = new List<Samurai>(soldiers);
        // Near the middle of the formation, so it stands at the heart of the ranks, with a little chance in it.
        float spacing = Mathf.Max(0.01f, SpacingFor(c));
        float middleRow = (c.rows - 1) / 2f;
        float best = float.PositiveInfinity;
        foreach (Samurai soldier in candidates)
        {
            float score = Mathf.Abs(soldier.Row - middleRow) * 2f + Mathf.Abs(soldier.transform.position.x) / spacing + Random.value * 1.5f;
            if (score < best)
            {
                best = score;
                elite = soldier;
            }
        }
        elite.MakeElite(c.eliteHealth, c.samuraiColor);
    }

    // The Elite's own shot timer, run from FixedUpdate: a slightly faster shot, shielded once the stage rolls shields,
    // and often violet (dodge-only), more so as the stages roll sealed shots for the ranks.
    void UpdateElite()
    {
        if (elite == null || elite.IsDead || IsDiving(elite) || !elite.CanFire || Time.time < nextEliteShot) return;
        // A bell's toll hastens it like the rest of the army.
        float haste = Time.time < hasteUntil ? tollFireScale : 1f;
        nextEliteShot = Time.time + eliteFireInterval * haste * Random.Range(0.7f, 1.3f);
        float sealedChance = Mathf.Min(eliteSealedChance + 2f * level.sealedArrowChance, MaxSealedChance);
        elite.Fire(level.arrowSpeed, new ShotMods
        {
            speed = 1.1f,
            toughness = level.toughArrowChance > 0f ? 2 : 1,
            sealedShot = Random.value < sealedChance,
        });
    }
}
