using System.Collections.Generic;
using UnityEngine;

// Carries out the elemental boons while a stage is played: blasts on kills, chill and freeze, lightning strikes.
// Kills made here count as KillSource.Effect, which never triggers another effect.
public class BoonEffects
{
    const float ChainRange = 3f;
    const float ChillSeconds = 3f;
    const int ChillStacks = 3;

    readonly SamuraiArmy army;
    readonly Ronin ronin;
    readonly RoninLoadout loadout;
    readonly Material boltMaterial;

    // The boss being fought, if any. Effects that would hit samurai hit the boss instead while she stands alone.
    public Boss Boss { get; set; }
    bool BossFight => Boss != null && !Boss.IsDead;

    public BoonEffects(SamuraiArmy army, Ronin ronin, RoninLoadout loadout)
    {
        this.army = army;
        this.ronin = ronin;
        this.loadout = loadout;
        // Any sprite material draws vertex colours, which is all a bolt needs.
        boltMaterial = ronin.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
    }

    static Color Fire => Boons.Info(Element.Fire).Color;
    static Color Frost => Boons.Info(Element.Water).Color;
    static Color Storm => Boons.Info(Element.Lightning).Color;

    public void OnKill(Samurai samurai, KillSource source)
    {
        Vector3 position = samurai.transform.position;
        float value;
        switch (source)
        {
            case KillSource.Kunai:
                if (loadout.Has(BoonId.FlameKunai, out value)) Blast(position, value);
                if (loadout.Has(BoonId.FrostKunai, out value))
                {
                    army.Chill(value, ChillSeconds, ChillStacks);
                    Fx.Play(Fx.IceBurst, position, Color.white, 1.2f);
                    Sfx.Play(Sfx.FrostChill);
                }
                if (loadout.Has(BoonId.StormKunai, out value) && Random.value < value)
                {
                    Samurai next = army.NearestSamurai(position, ChainRange);
                    if (next != null) Strike(position, next);
                }
                break;
            case KillSource.Shuriken:
                if (loadout.Has(BoonId.InfernoShuriken, out value)) Blast(position, value);
                break;
            case KillSource.Deflected:
                if (loadout.Has(BoonId.BurningRiposte, out value)) Blast(position, value);
                break;
        }
    }

    // The kill effects, triggered by hits on the boss: fire bursts on her (and burns her smoke), frost slows her,
    // lightning arcs into her.
    public void OnBossHit(Vector3 point, KillSource source)
    {
        if (!BossFight) return;
        float value;
        switch (source)
        {
            case KillSource.Kunai:
                if (loadout.Has(BoonId.FlameKunai, out value)) BossBlast(point, value);
                if (loadout.Has(BoonId.FrostKunai, out value))
                {
                    Boss.Chill(value, ChillSeconds, ChillStacks);
                    Fx.Play(Fx.IceBurst, point, Color.white, 1.4f);
                    Sfx.Play(Sfx.FrostChill);
                }
                if (loadout.Has(BoonId.StormKunai, out value) && Random.value < value) Strike(point + Vector3.up * 5f, Boss.Centre, Boss);
                break;
            case KillSource.Shuriken:
                if (loadout.Has(BoonId.InfernoShuriken, out value)) BossBlast(point, value);
                break;
            case KillSource.Deflected:
            case KillSource.Reflected:
                if (loadout.Has(BoonId.BurningRiposte, out value)) BossBlast(point, value);
                break;
        }
    }

    public void OnShurikenThrown(Vector3 muzzle)
    {
        if (BossFight)
        {
            OnShurikenThrownAtBoss();
            return;
        }
        if (army.Remaining == 0) return;
        float value;
        if (loadout.Has(BoonId.GlacialShuriken, out value))
        {
            army.Freeze(value);
            // A wave of frost rolls across the army, and every samurai's armour cracks with ice.
            if (Fx.Play(Fx.FrostWave, army.Centre, Color.white, 3.5f) == null) ElementFx.Glow(muzzle, Frost, 1f, 6f, 0.4f);
            foreach (Samurai soldier in army.Soldiers)
            {
                if (Random.value < 0.5f) Fx.Play(Fx.IceBurst, soldier.transform.position, Color.white, 0.8f);
            }
            CameraShake.Add(0.2f);
            Sfx.Play(Sfx.FrostChill, 1f, 0.8f);
        }
        if (loadout.Has(BoonId.ThunderShuriken, out value))
        {
            int own = army.ClosestColumnTo(muzzle.x);
            foreach (int column in NearestColumns(own, (int)value))
            {
                Samurai front = army.FrontSamuraiInColumn(column);
                if (front != null) Strike(front.transform.position + Vector3.up * 6f, front);
            }
        }
    }

    void OnShurikenThrownAtBoss()
    {
        float value;
        if (loadout.Has(BoonId.GlacialShuriken, out value))
        {
            Boss.Freeze(value);
            if (Fx.Play(Fx.FrostWave, Boss.Centre, Color.white, 3f) == null) ElementFx.Glow(Boss.Centre, Frost, 1f, 5f, 0.4f);
            CameraShake.Add(0.2f);
            Sfx.Play(Sfx.FrostChill, 1f, 0.8f);
        }
        if (loadout.Has(BoonId.ThunderShuriken, out value))
        {
            for (int i = 0; i < (int)value; i++) Strike(Boss.Centre + new Vector3((i - (value - 1) / 2f) * 1.5f, 6f, 0), Boss.Centre, Boss);
        }
    }

    public void OnParried()
    {
        float value;
        if (loadout.Has(BoonId.FrozenRiposte, out _)) loadout.GainWard();
        if (BossFight)
        {
            if (loadout.Has(BoonId.ThunderRiposte, out _)) Strike(ronin.Bounds.center, Boss.Centre, Boss);
            return;
        }
        if (loadout.Has(BoonId.ThunderRiposte, out value))
        {
            Vector3 origin = ronin.Bounds.center;
            var targets = new List<Samurai>(army.Soldiers);
            targets.Sort((a, b) => (a.Body.position - origin).sqrMagnitude.CompareTo((b.Body.position - origin).sqrMagnitude));
            for (int i = 0; i < targets.Count && i < (int)value; i++)
            {
                Strike(origin, targets[i]);
            }
        }
    }

    // Flame Kunai, Inferno Shuriken and Burning Riposte: everything within the radius burns.
    void Blast(Vector3 position, float radius)
    {
        // The explosion art is about 0.9 units across at full size.
        if (Fx.Play(Fx.Explosion, position, Color.white, radius / 0.9f) == null) ElementFx.Blast(position, radius, Fire, ronin.art.deathParticles);
        else Debris.BurstSprites(position, ronin.art.deathParticles, Fire, 8, 0.9f, 4f + 4f * radius);
        CameraShake.Add(0.12f);
        Sfx.Play(Sfx.FireBlast);
        foreach (Samurai target in army.SamuraiWithin(position, radius))
        {
            target.TakeHit(1, KillSource.Effect);
        }
    }

    // Fire on the boss: the burst, plus one point of damage and a hole burnt in any smoke around her.
    void BossBlast(Vector3 position, float radius)
    {
        if (Fx.Play(Fx.Explosion, position, Color.white, radius / 0.9f) == null) ElementFx.Blast(position, radius, Fire, ronin.art.deathParticles);
        CameraShake.Add(0.12f);
        Sfx.Play(Sfx.FireBlast);
        Boss.Burn(position, radius);
        Boss.TakeHit(1, KillSource.Effect);
    }

    // A bolt from the source to the target, which the lightning strike art finishes with a flash and shockwave.
    void Strike(Vector3 from, Samurai target)
    {
        Strike(from, target.transform.position, target);
    }

    void Strike(Vector3 from, Vector3 to, IHittable target)
    {
        ElementFx.Bolt(from, to, Storm, boltMaterial);
        Fx.Play(Fx.LightningStrike, to + Vector3.down * 0.3f, Color.white, 1f);
        CameraShake.Add(0.1f);
        Sfx.Play(Sfx.LightningStrike);
        target.TakeHit(1, KillSource.Effect);
    }

    // The other occupied columns closest to `column`, nearest first.
    List<int> NearestColumns(int column, int count)
    {
        var columns = new List<int>();
        foreach (Samurai soldier in army.Soldiers)
        {
            if (soldier.Column != column && !columns.Contains(soldier.Column)) columns.Add(soldier.Column);
        }
        columns.Sort((a, b) => Mathf.Abs(a - column).CompareTo(Mathf.Abs(b - column)));
        if (columns.Count > count) columns.RemoveRange(count, columns.Count - count);
        return columns;
    }
}
