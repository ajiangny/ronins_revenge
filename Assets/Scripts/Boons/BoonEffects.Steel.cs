using System.Collections.Generic;
using UnityEngine;

// Steel (Oliver): attacking rebuilds the cover. Scrap is what carries it, and only Oni's own hits shed it, since a kill
// by an effect must not trigger another effect. An effect may still apply Cracked, as blasts apply Burning.
public partial class BoonEffects
{
    // How far sideways hurt cover draws a falling scrap, in a round. In a warlord's fight it reaches any on the field.
    const float ScrapReach = 2.5f;
    const float ScrapSpread = 0.5f;
    const int MaxPlates = 4;
    // How far from him Hammer Riposte's ring reaches, in a round.
    const float HammerReach = 3f;
    // Past this many in the air a scrap lands at once.
    const int MaxScrapLive = 12;

    // One for every warlord and Kuon: the payout is cover and there is one wall, so a soul at his side must not double it.
    float nextBossCrack;
    float nextRod;
    // Lightning Rod strikes a warlord less often than the army's front.
    const float RodBossSeconds = 8f;

    // RunLog's tallies, per segment: hit-free rounds are the main risk with Steel, and cover never showed in the log.
    int scrapShed;
    int pointsMended;
    int platesFormed;
    int spareScrap;
    int arrowsStopped;

    static Color Steel => Boons.Info(Element.Steel).Color;

    // One line for the segment just played, when Steel did anything in it.
    public void LogSteel()
    {
        if (scrapShed + pointsMended + platesFormed + spareScrap + arrowsStopped == 0) return;
        RunLog.Note($"steel: {scrapShed} scrap, {pointsMended} mended, {platesFormed} plates, {spareScrap} spare, {arrowsStopped} arrows stopped by cover");
        scrapShed = pointsMended = platesFormed = spareScrap = arrowsStopped = 0;
    }

    // Scrap from `from`, each piece drawn to the hurt cover nearest it as it falls. What it does is settled on landing,
    // so a target gone by then isn't a bug.
    void ShedScrap(Vector3 from, int count = 1, int points = 1)
    {
        scrapShed += count;
        for (int i = 0; i < count; i++)
        {
            Vector3 at = from + Vector3.right * Random.Range(-ScrapSpread, ScrapSpread);
            Blocker hurt = wall.NearestHurt(at.x, BossFight ? float.MaxValue : ScrapReach);
            float toX = hurt != null ? hurt.transform.position.x : at.x;
            if (Scrap.Live >= MaxScrapLive) Settle(toX, points);
            else Scrap.Drop(at, toX, wall.TopY, wall.transform, x => Settle(x, points), loadout.Find(BoonId.RedHotIron) != null, Fire);
        }
    }

    // Mend, temper, plate, spare, so a scrap is never dead, even after Utsusemi razes the wall.
    void Settle(float x, int points)
    {
        Blocker hurt = wall.NearestHurt(x, BossFight ? float.MaxValue : ScrapReach);
        if (hurt != null)
        {
            hurt.Mend(points);
            pointsMended += points;
            Fx.Play(Fx.Impact, hurt.transform.position, Statuses.SeamColor, 0.7f);
            Sfx.Play(Sfx.BlockerHit, 0.5f, 1.4f);
            return;
        }
        // Tamahagane: whole cover tempers beyond new, before a plate, which is only better if nothing can be tempered.
        if (loadout.Find(BoonId.Tamahagane) != null)
        {
            Blocker whole = wall.NearestUntempered(x, ScrapReach, Statuses.TemperBound);
            if (whole != null)
            {
                whole.Temper(points, Statuses.TemperBound);
                Fx.Play(Fx.Impact, whole.transform.position, Statuses.SeamColor, 0.9f);
                Sfx.Play(Sfx.BlockerHit, 0.5f, 1.6f);
                return;
            }
        }
        // No new Blocker in a warlord's fight: it would be a beam stop no warning showed.
        if (!BossFight && army.Remaining > 0 && wall.PlateCount < MaxPlates && wall.TryPlate(x, ScrapReach))
        {
            platesFormed++;
            Sfx.Play(Sfx.BlockerHit, 0.6f, 0.8f);
            return;
        }
        spareScrap++;
        ronin.ChargeSpare();
    }

    // Anvil Blade's proc on a slash kill: scrap at once, so it pays before anything cracked is cut down, and the samurai
    // nearest in the victim's rank crack.
    void RingAnvil(Samurai samurai, Vector3 position, int cracks)
    {
        ShedScrap(position);
        List<Samurai> rank = army.SamuraiInRank(samurai);
        rank.Sort((a, b) => (a.Body.position - position).sqrMagnitude.CompareTo((b.Body.position - position).sqrMagnitude));
        for (int i = 0; i < rank.Count && i < cracks; i++) rank[i].Crack(Statuses.CrackSeconds);
        Fx.Play(Fx.Impact, position, Steel, 1f);
        Sfx.Play(Sfx.Deflect, 0.5f, 1.3f);
    }

    // Hammer Riposte: the parry rings out. On a warlord or Kuon it always cracks, and the points go to the nearest hurt
    // cover anywhere; in a round to hurt cover near him, or a plate above him when there's none.
    void RingHammer(int points)
    {
        Vector3 at = ronin.Bounds.center;
        if (BossFight)
        {
            foreach (Boss boss in Bosses) boss.Crack(Statuses.CrackSeconds);
            if (WolfFight) Wolf.Crack(Statuses.CrackSeconds);
        }
        Blocker hurt = wall.NearestHurt(at.x, BossFight ? float.MaxValue : HammerReach);
        if (hurt != null)
        {
            hurt.Mend(points);
            pointsMended += points;
        }
        else if (!BossFight && army.Remaining > 0 && wall.PlateCount < MaxPlates && wall.TryPlate(at.x, ScrapReach)) platesFormed++;
        Fx.Play(Fx.Impact, at, Steel, 1.2f);
        Sfx.Play(Sfx.BlockerHit, 0.7f, 0.7f);
    }

    // Steel Shadow: the wall comes back whole. Fallen cover is raised only in a round, since in a warlord's fight a new
    // Blocker would be a beam stop no warning showed; there the unleashing cracks instead.
    void UnleashSteelShadow(Vector3 at, float crack)
    {
        wall.Restore(raise: !BossFight);
        if (BossFight)
        {
            foreach (Boss boss in Bosses) boss.Crack(crack);
            if (WolfFight) Wolf.Crack(crack);
        }
        Fx.Play(Fx.Impact, at, Steel, 1.6f);
        CameraShake.Add(0.15f);
        Sfx.Play(Sfx.BlockerBreak, 0.8f, 0.7f);
    }

    // Shrapnel: his cover broke under a hit, so it bursts. Halved against a warlord or Kuon, rounded up, since Benibana
    // has only 70 health. Blocker.Broken doesn't fire for what Steel built, so this can't feed itself.
    public void OnCoverBroken(Vector3 position)
    {
        if (!loadout.Has(BoonId.Shrapnel, out float shards)) return;
        int count = BossFight ? ((int)shards + 1) / 2 : (int)shards;
        for (int i = 0; i < count; i++) ronin.LaunchSteelShard(position, ronin.RiposteTarget);
        Fx.Play(Fx.Impact, position, Statuses.SeamColor, 1.2f);
    }

    // Spear-Breaker: a Taisho's yari cut down falls as scrap.
    public void OnSpearBroken(Vector3 position)
    {
        if (!loadout.Has(BoonId.SpearBreaker, out float scrap)) return;
        ShedScrap(position, (int)scrap);
        Sfx.Play(Sfx.BlockerBreak, 0.6f, 1.3f);
    }

    // Quenching: a freeze on a warlord cracks her, and the cold reaches Kuon as it does for Chilled. Not routed through
    // FreezeBoss from Frozen Riposte's shards, because that also Chills, which would change them for every build.
    void QuenchBoss(Boss boss)
    {
        if (loadout.Find(BoonId.Quenching) == null) return;
        boss.Crack(Statuses.CrackSeconds);
        if (WolfFight) Wolf.Crack(Statuses.CrackSeconds);
    }

    // Lightning Rod: an arrow chipping the cover strikes up its lane. Arrows don't record who shot them, so it takes the
    // front of the nearest column. The limit is the guard against the effect kill share of §4 coming back.
    public void OnCoverChipped(Vector3 position)
    {
        arrowsStopped++;
        if (!loadout.Has(BoonId.LightningRod, out float seconds) || Time.time < nextRod) return;
        if (BossFight)
        {
            nextRod = Time.time + RodBossSeconds;
            Strike(Boss.Centre + Vector3.up * 6f, Boss.Centre, Boss);
            return;
        }
        if (army.Remaining == 0) return;
        Samurai front = army.FrontSamuraiInColumn(army.ClosestColumnTo(position.x));
        if (front == null || front.IsDead) return;
        nextRod = Time.time + seconds;
        Strike(front.transform.position + Vector3.up * 6f, front);
    }

    // Slashing every 0.45 s would refill the wall without limit, so a warlord's crack is spent by his next own blow and
    // pays at most once per BossCrackCooldown.
    void PayBossCrack(Boss boss, Vector3 point, KillSource source)
    {
        if (!Damage.Procs(source) || !boss.Cracked || Time.time < nextBossCrack) return;
        nextBossCrack = Time.time + Statuses.BossCrackCooldown;
        boss.SpendCrack();
        ShedScrap(point, Statuses.BossCrackScrapNow);
    }

    void PayWolfCrack(Vector3 point, KillSource source)
    {
        if (!Damage.Procs(source) || !Wolf.Cracked || Time.time < nextBossCrack) return;
        nextBossCrack = Time.time + Statuses.BossCrackCooldown;
        Wolf.SpendCrack();
        ShedScrap(point, Statuses.BossCrackScrapNow);
    }

    // Anvil Blade on a warlord or Kuon: every hit lands, so it rings on a share of its chance, like Flame Blade's bursts.
    bool AnvilRings => loadout.Find(BoonId.AnvilBlade) != null && Random.value < loadout.Extra(BoonId.AnvilBlade) * BossRiptideShare;
}
