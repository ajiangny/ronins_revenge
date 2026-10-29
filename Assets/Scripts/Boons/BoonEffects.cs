using System.Collections.Generic;
using UnityEngine;

// Carries out the elemental boons while a stage is played: blasts on kills, ice and freeze, lightning strikes,
// and the tide's waves. Kills made here count as KillSource.Effect, which never triggers another effect. A soaked
// target takes one more from every effect, added once in Samurai.TakeHit and Boss.TakeHit.
public class BoonEffects
{
    const float ChainRange = 3f;
    // Storm Blade's arc leaps to a random samurai this far away, not just the one beside the kill.
    const float StormBladeRange = 6.5f;
    // Thunder Shuriken: how close a samurai must come to the flying blade for the lightning to reach it.
    const float ShurikenArcRadius = 2.6f;
    // Samurai stand 2 apart across and 1.75 apart down, so this reaches the ones beside, above and below.
    const float IceRadius = 2.1f;
    // Frost Blade: each samurai in reach of a slash kill has this chance to be encased, and at most this many are,
    // nearest first, so ice can't lock down a whole packed rank from one kill.
    const float FrostEncaseChance = 0.5f;
    const int FrostEncaseTargets = 2;
    // Most samurai one fire blast can burn, nearest first; Wildfire's blasts burn more.
    const int BlastTargets = 2;
    const int WildfireBlastTargets = 4;
    // Flame Blade on a boss (or Kuon): every hit lands, so it bursts at this share of its kill chance.
    const float BossFlameShare = 0.6f;
    const float ShardFreezeSeconds = 1.5f;
    const float BossShardFreezeSeconds = 0.5f;
    // Frost Blade freezes the boss for a quarter of its freeze time, and no more often than this.
    const float BossFrostCooldown = 4f;
    // Raijin's Drum strikes this many columns (or the boss this many times) on each beat.
    const int DrumStrikes = 3;
    // Kindling's fireball bursts wide enough to reach the diagonals and a ring beyond.
    const float KindlingRadius = 2.7f;
    // Riptide Blade's wave takes the whole column: this far either side of it, and the arrows in that lane with it.
    const float ColumnHalfWidth = 0.9f;
    // Tidewake Shuriken: how close a samurai or arrow must come to the blade to be caught in its wake.
    const float WakeRadius = 1.2f;
    // Tide Shadow's great wave pushes the army back this far.
    const float TideShadowPush = 0.6f;
    // Riptide Blade soaks the boss on this share of its kill chance, like Flame Blade's bursts.
    const float BossRiptideShare = 0.6f;

    readonly SamuraiArmy army;
    readonly Ronin ronin;
    readonly RoninLoadout loadout;
    readonly Material boltMaterial;
    float nextBossFrost;
    int drumHits;
    int springKills;

    // Healing Spring: raised when enough of the ronin's own kills have added up; the GameManager heals or pays.
    public event System.Action SpringHeal;

    // The boss being fought, if any. Effects that would hit samurai hit the boss instead while she stands alone.
    public Boss Boss { get; set; }
    bool BossFight => Boss != null && !Boss.IsDead;
    // The souls Kagetsune has summoned to fight beside him: bosses in their own right, hit by the same effects.
    public readonly List<Boss> Summons = new List<Boss>();
    // Every boss standing: the one being fought and any souls fighting beside him.
    IEnumerable<Boss> Bosses
    {
        get
        {
            if (BossFight) yield return Boss;
            foreach (Boss summon in Summons) if (summon != null && !summon.IsDead) yield return summon;
        }
    }
    // A boss that's still standing, when the thing struck is one.
    static Boss StandingBoss(IHittable target) => target is Boss boss && boss != null && !boss.IsDead ? boss : null;
    // Shirogane's wolf, Kuon, while it can be fought: the damage boons land on it too, but frost doesn't hold a spirit.
    public SpiritWolf Wolf { get; set; }
    bool WolfFight => Wolf != null && Wolf.Armed && !Wolf.IsBroken;

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
    static Color Water => Boons.Info(Element.Tide).Color;

    // Flame Blade's roll. Kindling's fireball counts as a slash for the other blades, but brings its own blast, so it
    // never sets off Flame Blade's as well.
    bool FlameBursts(float share)
    {
        if (Projectile.Striking != null && Projectile.Striking.Kindled) return false;
        EquippedBoon flame = loadout.Find(BoonId.FlameBlade);
        return flame != null && Random.value < flame.Extra * share;
    }

    public void OnKill(Samurai samurai, KillSource source)
    {
        Vector3 position = samurai.transform.position;
        float value;
        switch (source)
        {
            case KillSource.Slash:
                if (loadout.Has(BoonId.FlameBlade, out value) && FlameBursts(1f)) Blast(position, value, samurai);
                if (loadout.Has(BoonId.RiptideBlade, out value) && Random.value < loadout.Extra(BoonId.RiptideBlade)) ColumnWave(samurai.Column, position, value);
                if (loadout.Has(BoonId.FrostBlade, out value))
                {
                    int encased = 0;
                    foreach (Samurai target in army.SamuraiWithin(position, IceRadius))
                    {
                        if (encased >= FrostEncaseTargets) break;
                        if (Random.value >= FrostEncaseChance) continue;
                        target.Encase(value * loadout.FreezeMultiplier);
                        encased++;
                    }
                    Fx.Play(Fx.IceBurst, position, Color.white, 1.2f);
                    Sfx.Play(Sfx.FrostChill);
                }
                if (loadout.Has(BoonId.StormBlade, out value) && Random.value < value)
                {
                    List<Samurai> reach = army.SamuraiWithin(position, StormBladeRange);
                    reach.RemoveAll(s => s.IsDead || s == samurai);
                    if (reach.Count > 0) Chain(position, reach[Random.Range(0, reach.Count)]);
                }
                break;
            case KillSource.Shuriken:
                if (loadout.Has(BoonId.InfernoShuriken, out value)) Blast(position, value, samurai);
                break;
            case KillSource.Clone:
                OnCloneKill(samurai, position);
                break;
        }
        if (!Damage.Procs(source)) return;
        BeatDrum();
        // Healing Spring counts only his own kills, never an effect's or a lesser blade's.
        if (loadout.Has(BoonId.HealingSpring, out value) && ++springKills >= (int)value)
        {
            springKills = 0;
            SpringHeal?.Invoke();
        }
    }

    // The Ultimate slot's boon, on a kill by the Shadow Clone: its element takes the samurai's neighbours.
    void OnCloneKill(Samurai samurai, Vector3 position)
    {
        float value;
        if (loadout.Has(BoonId.PyreShadow, out value)) Blast(position, value, samurai);
        if (loadout.Has(BoonId.TideShadow, out value))
        {
            foreach (Samurai target in army.SamuraiWithin(position, IceRadius))
            {
                if (target != samurai) target.Soak(value);
            }
        }
        if (loadout.Has(BoonId.FrostShadow, out value))
        {
            int encased = 0;
            foreach (Samurai target in army.SamuraiWithin(position, IceRadius))
            {
                if (encased >= FrostEncaseTargets) break;
                if (Random.value >= FrostEncaseChance) continue;
                target.Encase(value * loadout.FreezeMultiplier);
                encased++;
            }
            Fx.Play(Fx.IceBurst, position, Color.white, 1f);
        }
        if (loadout.Has(BoonId.StormShadow, out value) && Random.value < value)
        {
            List<Samurai> reach = army.SamuraiWithin(position, StormBladeRange);
            reach.RemoveAll(s => s.IsDead || s == samurai);
            if (reach.Count > 0) Chain(position, reach[Random.Range(0, reach.Count)]);
        }
    }

    // On a boss or Kuon every clone hit lands, so the Ultimate boons fire on a share of them.
    const float CloneBossShare = 0.25f;

    // Frost Shadow: the clone comes out in a wave of frost that freezes the army (or holds the boss a moment).
    // Tide Shadow: a great wave that washes every arrow away and rolls the army back (or soaks the boss).
    public void OnUltimateStarted(Vector3 at)
    {
        if (loadout.Has(BoonId.TideShadow, out float soak))
        {
            if (BossFight) foreach (Boss boss in Bosses) boss.Soak(soak);
            else if (army.Remaining > 0) army.PushBack(TideShadowPush);
            WashArrows(at, float.MaxValue);
            GreatWave(at, BossFight ? Boss.Centre : army.Centre);
        }
        if (!loadout.Has(BoonId.FrostShadow, out float seconds)) return;
        if (BossFight) foreach (Boss boss in Bosses) boss.Freeze(seconds * 0.5f * loadout.FreezeMultiplier);
        else if (army.Remaining > 0) army.Freeze(seconds * loadout.FreezeMultiplier);
        else return;
        if (Fx.Play(Fx.FrostWave, at, Color.white, 3.5f) == null) ElementFx.Glow(at, Frost, 1f, 6f, 0.4f);
        CameraShake.Add(0.2f);
        Sfx.Play(Sfx.FrostChill, 1f, 0.8f);
    }

    // Pyre Shadow: the clone goes out in a blast wider than its kills' (on a boss, a boss blast).
    public void OnUltimateEnded(Vector3 at)
    {
        if (!loadout.Has(BoonId.PyreShadow, out float radius)) return;
        if (BossFight) BossBlast(at, radius + 1f);
        else Blast(at, radius + 1f);
    }

    // The kill effects, triggered by hits on a boss (the one fought, or a soul summoned beside him): fire bursts on
    // her (and burns her smoke), frost slows her, lightning arcs into her.
    public void OnBossHit(Boss hit, Vector3 point, KillSource source)
    {
        if (!BossFight) return;
        Boss boss = hit != null && !hit.IsDead ? hit : Boss;
        float value;
        switch (source)
        {
            case KillSource.Slash:
                if (loadout.Has(BoonId.FlameBlade, out value) && FlameBursts(BossFlameShare)) BossBlast(point, value, boss);
                if (loadout.Has(BoonId.RiptideBlade, out value) && Random.value < loadout.Extra(BoonId.RiptideBlade) * BossRiptideShare) SoakBoss(boss, point, value);
                if (loadout.Has(BoonId.FrostBlade, out value))
                {
                    // A blow on the frozen boss shatters the ice for an extra point of damage; otherwise she freezes.
                    if (boss.Frozen)
                    {
                        ShatterFx(point, 1.4f);
                        boss.TakeHit(1, KillSource.Effect);
                    }
                    else if (Time.time >= nextBossFrost)
                    {
                        nextBossFrost = Time.time + BossFrostCooldown;
                        boss.Freeze(value * 0.25f * loadout.FreezeMultiplier);
                        Fx.Play(Fx.IceBurst, point, Color.white, 1.4f);
                        Sfx.Play(Sfx.FrostChill);
                    }
                }
                if (loadout.Has(BoonId.StormBlade, out value) && Random.value < value) Strike(point + Vector3.up * 5f, boss.Centre, boss);
                break;
            case KillSource.Shuriken:
                if (loadout.Has(BoonId.InfernoShuriken, out value)) BossBlast(point, value, boss);
                break;
            case KillSource.Clone:
                if (Random.value >= CloneBossShare) break;
                if (loadout.Has(BoonId.PyreShadow, out value)) BossBlast(point, value, boss);
                if (loadout.Has(BoonId.TideShadow, out value)) SoakBoss(boss, point, value);
                if (loadout.Has(BoonId.FrostShadow, out value) && !boss.Frozen && Time.time >= nextBossFrost)
                {
                    nextBossFrost = Time.time + BossFrostCooldown;
                    boss.Freeze(value * 0.25f * loadout.FreezeMultiplier);
                    Fx.Play(Fx.IceBurst, point, Color.white, 1.2f);
                }
                if (loadout.Has(BoonId.StormShadow, out value) && Random.value < value) Strike(point + Vector3.up * 5f, boss.Centre, boss);
                break;
        }
        if (Damage.Procs(source)) BeatDrum();
    }

    // A fireball or ice shard sent back by a parry has struck something.
    public void OnRiposteImpact(Vector3 point, IHittable target)
    {
        float value;
        if (loadout.Has(BoonId.BurningRiposte, out value))
        {
            if (BossFight && StandingBoss(target) is Boss struck) BossBlast(point, value, struck);
            else if (WolfFight && ReferenceEquals(target, Wolf)) WolfBlast(point, value);
            else if (!BossFight) Blast(point, value);
        }
        // Frozen Riposte shards encase the samurai they hit themselves (Projectile.FreezeOnHit); only the boss is left.
        if (loadout.Has(BoonId.FrozenRiposte, out _))
        {
            if (BossFight && StandingBoss(target) is Boss struck)
            {
                struck.Freeze(BossShardFreezeSeconds * loadout.FreezeMultiplier);
                Fx.Play(Fx.IceBurst, point, Color.white, 1f);
            }
        }
    }

    public void OnShurikenThrown(Vector3 muzzle, Projectile shuriken)
    {
        // Thunder Shuriken: lightning arcs off the blade into what it flies past, boss included.
        if (loadout.Has(BoonId.ThunderShuriken, out float arcChance) && shuriken != null)
        {
            shuriken.gameObject.AddComponent<ShurikenStorm>().Begin(army, () => Bosses, ShurikenArcRadius, arcChance,
                (from, target, point) => Strike(from, point, target, chain: true));
        }
        // Tidewake Shuriken: a wake that soaks what the blade passes and washes the arrows in its lane.
        if (loadout.Has(BoonId.TidewakeShuriken, out float soak) && shuriken != null)
        {
            shuriken.gameObject.AddComponent<ShurikenWake>().Begin(army, () => Bosses, WakeRadius, soak, Water);
        }
        if (BossFight)
        {
            OnShurikenThrownAtBoss();
            return;
        }
        if (army.Remaining == 0) return;
        float value;
        if (loadout.Has(BoonId.GlacialShuriken, out value))
        {
            army.Freeze(value * loadout.FreezeMultiplier);
            // A wave of frost rolls across the army, and every samurai's armour cracks with ice.
            if (Fx.Play(Fx.FrostWave, army.Centre, Color.white, 3.5f) == null) ElementFx.Glow(muzzle, Frost, 1f, 6f, 0.4f);
            foreach (Samurai soldier in army.Soldiers)
            {
                if (Random.value < 0.5f) Fx.Play(Fx.IceBurst, soldier.transform.position, Color.white, 0.8f);
            }
            CameraShake.Add(0.2f);
            Sfx.Play(Sfx.FrostChill, 1f, 0.8f);
        }
    }

    void OnShurikenThrownAtBoss()
    {
        float value;
        if (loadout.Has(BoonId.GlacialShuriken, out value))
        {
            foreach (Boss boss in Bosses) boss.Freeze(value * loadout.FreezeMultiplier);
            if (Fx.Play(Fx.FrostWave, Boss.Centre, Color.white, 3f) == null) ElementFx.Glow(Boss.Centre, Frost, 1f, 5f, 0.4f);
            CameraShake.Add(0.2f);
            Sfx.Play(Sfx.FrostChill, 1f, 0.8f);
        }
    }

    public void OnParried()
    {
        float value;
        if (BossFight)
        {
            if (loadout.Has(BoonId.ThunderRiposte, out value))
            {
                for (int i = 0; i < (int)value; i++) Strike(ronin.Bounds.center, Boss.Centre + (Vector3)Random.insideUnitCircle * 0.5f, Boss, chain: true);
            }
            if (loadout.Has(BoonId.UndertowRiposte, out _))
            {
                foreach (Boss boss in Bosses) SoakBoss(boss, boss.Centre, UndertowSoakSeconds);
                GreatWave(ronin.Bounds.center, Boss.Centre);
            }
            return;
        }
        // Undertow Riposte: a wave rolls up from him, pushes the army back and soaks its front rank.
        if (loadout.Has(BoonId.UndertowRiposte, out value) && army.Remaining > 0)
        {
            army.PushBack(value);
            foreach (Samurai front in army.FrontRank()) front.Soak(UndertowSoakSeconds);
            GreatWave(ronin.Bounds.center, army.Centre);
        }
        if (loadout.Has(BoonId.ThunderRiposte, out value))
        {
            Vector3 origin = ronin.Bounds.center;
            var targets = new List<Samurai>(army.Soldiers);
            targets.Sort((a, b) => (a.Body.position - origin).sqrMagnitude.CompareTo((b.Body.position - origin).sqrMagnitude));
            for (int i = 0; i < targets.Count && i < (int)value; i++)
            {
                Chain(origin, targets[i]);
            }
        }
    }

    // The ronin broke an ice statue: it bursts into shards (more with a better Frost Blade), each homing on one of the
    // nearest samurai. A shard stops at the samurai it hits, and holds it in ice first, so a kill freezes into a statue.
    public void OnStatueShattered(Vector3 position, bool soaked = false)
    {
        if (BossFight || army.Remaining == 0) return;
        EquippedBoon blade = loadout.Find(BoonId.FrostBlade);
        int count = blade != null ? (int)blade.Extra : Boons.IceShardsAt(Rarity.Common);
        // Black Ice: a statue that froze soaked bursts into twice the shards.
        if (soaked && loadout.Has(BoonId.BlackIce, out float blackIce)) count = (int)(count * blackIce);

        var targets = new List<Samurai>(army.Soldiers);
        targets.RemoveAll(s => s.IsDead);
        targets.Sort((a, b) => (a.Body.position - position).sqrMagnitude.CompareTo((b.Body.position - position).sqrMagnitude));
        if (targets.Count == 0) return;
        float freeze = ShardFreezeSeconds * loadout.FreezeMultiplier;
        for (int i = 0; i < count; i++)
        {
            // More shards than samurai nearby: the spares go round again.
            Samurai target = targets[i % Mathf.Min(count, targets.Count)];
            ronin.LaunchIceShard(position, () => ShardTarget(target, position), freeze);
        }
    }

    // A shard keeps after its samurai; if another shard got there first, it turns on the nearest one left.
    Transform ShardTarget(Samurai target, Vector3 from)
    {
        if (target != null && !target.IsDead) return target.transform;
        Samurai next = army.NearestSamurai(from, float.MaxValue);
        return next != null ? next.transform : null;
    }

    void ShatterFx(Vector3 position, float scale)
    {
        Fx.Play(Fx.IceBurst, position, Color.white, scale);
        Debris.BurstSprites(position, ronin.art.deathParticles, Frost, 10, 0.8f, 7f);
        CameraShake.Add(0.12f);
        Sfx.Play(Sfx.WardShatter, 0.8f, 1.2f);
    }

    // Flame Blade, Inferno Shuriken and Burning Riposte: everything within the radius burns. `centre` is the samurai
    // the blast is centred on, when there is one: Scalding Steam boils wider off a soaked one and washes the arrows
    // inside.
    void Blast(Vector3 position, float radius, Samurai centre = null)
    {
        radius += loadout.Value(BoonId.Wildfire);
        bool steam = centre != null && centre.Soaked && loadout.Find(BoonId.ScaldingSteam) != null;
        if (steam) radius += loadout.Value(BoonId.ScaldingSteam);
        // The explosion art is about 0.9 units across at full size.
        if (Fx.Play(Fx.Explosion, position, Color.white, radius / 0.9f) == null) ElementFx.Blast(position, radius, Fire, ronin.art.deathParticles);
        else Debris.BurstSprites(position, ronin.art.deathParticles, Fire, 8, 0.9f, 4f + 4f * radius);
        CameraShake.Add(0.12f);
        Sfx.Play(Sfx.FireBlast);
        // Thermal Shock: ice cracks under the heat.
        bool thermal = loadout.Has(BoonId.ThermalShock, out float iceDamage);
        // A blast burns only the few samurai nearest its heart, so a packed army can't be wiped by chains of them
        // (the play log had boon effects making about 60% of the kills). Wildfire burns more.
        int burned = 0, most = loadout.Find(BoonId.Wildfire) != null ? WildfireBlastTargets : BlastTargets;
        foreach (Samurai target in army.SamuraiWithin(position, radius))
        {
            if (burned++ >= most) break;
            target.TakeHit(thermal && (target.Encased || army.Frozen) ? (int)iceDamage : 1, KillSource.Effect);
        }
        BurnArrows(position, radius);
        if (steam) WashArrows(position, radius);
    }

    // Wildfire: breakable enemy arrows inside a blast burn away. Spears, and the boss slashes only a parry
    // answers, keep flying.
    void BurnArrows(Vector3 position, float radius)
    {
        if (loadout.Find(BoonId.Wildfire) == null) return;
        foreach (Arrow arrow in Object.FindObjectsByType<Arrow>())
        {
            if (arrow.Unbreakable || arrow.Reflectable) continue;
            if ((arrow.transform.position - position).sqrMagnitude <= radius * radius) arrow.Burn(Fire);
        }
    }

    // Asagi's waves: breakable enemy arrows within the radius wash away. Spears, and the boss slashes only a parry
    // answers, keep flying. float.MaxValue takes the whole field.
    void WashArrows(Vector3 position, float radius)
    {
        foreach (Arrow arrow in Object.FindObjectsByType<Arrow>())
        {
            if (arrow.Unbreakable || arrow.Reflectable) continue;
            if (radius == float.MaxValue || (arrow.transform.position - position).sqrMagnitude <= radius * radius) arrow.Wash(Water);
        }
    }

    // Riptide Blade: a wave runs up the victim's column from where it fell, soaking every samurai in the column and
    // washing away the arrows in that lane.
    void ColumnWave(int column, Vector3 from, float soak)
    {
        if (army.Remaining == 0) return;
        float top = from.y;
        foreach (Samurai soldier in army.Soldiers)
        {
            if (soldier.Column != column || soldier.IsDead) continue;
            soldier.Soak(soak);
            top = Mathf.Max(top, soldier.Body.position.y);
        }
        foreach (Arrow arrow in Object.FindObjectsByType<Arrow>())
        {
            if (arrow.Unbreakable || arrow.Reflectable) continue;
            if (Mathf.Abs(arrow.transform.position.x - from.x) <= ColumnHalfWidth && arrow.transform.position.y >= from.y - 1f) arrow.Wash(Water);
        }
        Vector3 crest = new Vector3(from.x, top + 1f, 0f);
        if (Fx.Play(Fx.TideWave, from, Color.white, 2f) == null) ElementFx.Wave(from, crest, Water, 2f, 0.45f, ronin.art.deathParticles);
        CameraShake.Add(0.06f);
        Sfx.Play(Sfx.TideWave, 0.8f, 1.1f);
    }

    // Undertow Riposte and Tide Shadow: a wave the width of the field rolls up from `from` toward `to`.
    void GreatWave(Vector3 from, Vector3 to)
    {
        if (Fx.Play(Fx.TideWave, from, Color.white, 4f) == null) ElementFx.Wave(from, to, Water, 7f, 0.6f, ronin.art.deathParticles);
        CameraShake.Add(0.15f);
        Sfx.Play(Sfx.TideWave);
    }

    // Undertow Riposte's wave and Tide Shadow's boss soak have no time of their own on the card: a short, fixed one.
    const float UndertowSoakSeconds = 3f;

    void SoakBoss(Boss boss, Vector3 point, float seconds)
    {
        if (boss == null || boss.IsDead) return;
        boss.Soak(seconds);
        Fx.Play(Fx.Impact, point, Color.Lerp(Color.white, Water, 0.6f), 1f);
    }

    // Cold Compress: a blow on the ronin freezes the army (or the standing boss for half as long).
    public void OnRoninHit()
    {
        if (!loadout.Has(BoonId.ColdCompress, out float seconds)) return;
        Vector3 at = ronin.Bounds.center;
        if (BossFight) foreach (Boss boss in Bosses) boss.Freeze(seconds * 0.5f * loadout.FreezeMultiplier);
        else if (army.Remaining > 0) army.Freeze(seconds * loadout.FreezeMultiplier);
        else return;
        if (Fx.Play(Fx.FrostWave, at, Color.white, 3f) == null) ElementFx.Glow(at, Frost, 1f, 5f, 0.4f);
        Sfx.Play(Sfx.FrostChill, 1f, 0.9f);
    }

    // Fire on the boss: the burst, plus one point of damage and a hole burnt in any smoke around her.
    void BossBlast(Vector3 position, float radius, Boss target = null)
    {
        Boss boss = target != null && !target.IsDead ? target : Boss;
        radius += loadout.Value(BoonId.Wildfire);
        if (Fx.Play(Fx.Explosion, position, Color.white, radius / 0.9f) == null) ElementFx.Blast(position, radius, Fire, ronin.art.deathParticles);
        CameraShake.Add(0.12f);
        Sfx.Play(Sfx.FireBlast);
        boss.Burn(position, radius);
        BurnArrows(position, radius);
        boss.TakeHit(boss.Frozen && loadout.Find(BoonId.ThermalShock) != null ? 2 : 1, KillSource.Effect);
    }

    // Fire on Kuon: the burst and one point of damage.
    void WolfBlast(Vector3 position, float radius)
    {
        radius += loadout.Value(BoonId.Wildfire);
        if (Fx.Play(Fx.Explosion, position, Color.white, radius / 0.9f) == null) ElementFx.Blast(position, radius, Fire, ronin.art.deathParticles);
        CameraShake.Add(0.12f);
        Sfx.Play(Sfx.FireBlast);
        BurnArrows(position, radius);
        Wolf.TakeHit(1, KillSource.Effect);
    }

    // The ronin's own hits on Kuon: the blade and shuriken fire bursts, and the blade's lightning.
    public void OnWolfHit(Vector3 point, KillSource source)
    {
        if (!WolfFight) return;
        float value;
        switch (source)
        {
            case KillSource.Slash:
                if (loadout.Has(BoonId.FlameBlade, out value) && FlameBursts(BossFlameShare)) WolfBlast(point, value);
                if (WolfFight && loadout.Has(BoonId.StormBlade, out value) && Random.value < value) Strike(point + Vector3.up * 5f, Wolf.Centre, Wolf);
                break;
            case KillSource.Shuriken:
                if (loadout.Has(BoonId.InfernoShuriken, out value)) WolfBlast(point, value);
                break;
            case KillSource.Clone:
                if (Random.value >= CloneBossShare) break;
                if (loadout.Has(BoonId.PyreShadow, out value)) WolfBlast(point, value);
                if (WolfFight && loadout.Has(BoonId.StormShadow, out value) && Random.value < value) Strike(point + Vector3.up * 5f, Wolf.Centre, Wolf);
                break;
        }
        if (Damage.Procs(source)) BeatDrum();
    }

    // A bolt from the source to the target, which the lightning strike art finishes with a flash and shockwave.
    void Strike(Vector3 from, Samurai target)
    {
        Strike(from, target.transform.position, target);
    }

    // Lightning that jumps sideways from one samurai to the next (Storm Blade, Thunder Riposte, Superconductor),
    // drawn as an arc between them rather than a bolt from the sky.
    void Chain(Vector3 from, Samurai target)
    {
        Strike(from, target.transform.position, target, chain: true);
    }

    // arcs is false for Superconductor's own arcs, so they never arc again. chain draws a crackling arc from
    // `from` instead of the strike from the sky.
    void Strike(Vector3 from, Vector3 to, IHittable target, bool arcs = true, bool chain = false)
    {
        if (chain)
        {
            ElementFx.Chain(from, to, Storm, boltMaterial);
            Fx.Play(Fx.Impact, to, Color.Lerp(Color.white, Storm, 0.5f), 1f);
            CameraShake.Add(0.06f);
            Sfx.Play(Sfx.LightningStrike, 0.7f, 1.3f);
        }
        else
        {
            ElementFx.Bolt(from, to, Storm, boltMaterial);
            Fx.Play(Fx.LightningStrike, to + Vector3.down * 0.3f, Color.white, 1f);
            CameraShake.Add(0.1f);
            Sfx.Play(Sfx.LightningStrike);
        }

        Boss struck = BossFight ? StandingBoss(target) : null;
        if (struck != null)
        {
            // Plasma Arc burns as it strikes; Superconductor doubles through a frozen boss.
            int damage = loadout.Find(BoonId.PlasmaArc) != null ? 2 : 1;
            if (struck.Frozen && loadout.Find(BoonId.Superconductor) != null) damage *= 2;
            target.TakeHit(damage, KillSource.Effect);
            return;
        }

        Samurai samurai = target as Samurai;
        bool iced = samurai != null && samurai.Encased;
        bool soaked = samurai != null && samurai.Soaked;
        target.TakeHit(1, KillSource.Effect);
        if (loadout.Has(BoonId.PlasmaArc, out float plasma)) Blast(to, plasma, samurai);
        // Thunder Rain: the strike runs along the water to every other soaked samurai in the rank, once.
        if (arcs && soaked && loadout.Find(BoonId.ThunderRain) != null)
        {
            foreach (Samurai next in army.SamuraiInRank(samurai))
            {
                if (next.IsDead || !next.Soaked) continue;
                Strike(to, next.transform.position, next, false, chain: true);
            }
        }
        if (arcs && iced && loadout.Has(BoonId.Superconductor, out float arcCount))
        {
            int arced = 0;
            foreach (Samurai next in army.SamuraiWithin(to, ChainRange))
            {
                if (arced >= (int)arcCount) break;
                if (ReferenceEquals(next, target) || next.IsDead || !next.Encased) continue;
                Strike(to, next.transform.position, next, false, chain: true);
                arced++;
            }
        }
    }

    // Kindling's fireball has struck something: it bursts in a great blast (on the boss, a boss blast).
    public void OnFireballImpact(Vector3 point, IHittable target)
    {
        if (BossFight && StandingBoss(target) is Boss struck) BossBlast(point, KindlingRadius, struck);
        else if (WolfFight && ReferenceEquals(target, Wolf)) WolfBlast(point, KindlingRadius);
        else if (!BossFight) Blast(point, KindlingRadius);
    }

    // Raijin's Drum: every few of the ronin's own hits, lightning falls on the front of the nearest columns.
    void BeatDrum()
    {
        if (!loadout.Has(BoonId.RaijinsDrum, out float hitsPerBeat)) return;
        if (++drumHits < (int)hitsPerBeat) return;
        drumHits = 0;
        if (BossFight)
        {
            for (int i = 0; i < DrumStrikes; i++) Strike(Boss.Centre + new Vector3((i - 1) * 1.5f, 6f, 0), Boss.Centre, Boss);
            return;
        }
        if (army.Remaining == 0) return;
        int own = army.ClosestColumnTo(ronin.transform.position.x);
        var columns = NearestColumns(own, DrumStrikes - 1);
        columns.Insert(0, own);
        foreach (int column in columns)
        {
            Samurai front = army.FrontSamuraiInColumn(column);
            if (front != null && !front.IsDead) Strike(front.transform.position + Vector3.up * 6f, front);
        }
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
