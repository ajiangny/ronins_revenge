using System;
using UnityEngine;

// The Flute Songs: what Oni buys on the hill with Spirit Notes between runs (ECONOMY.md). A fresh save plays exactly
// as the game is tuned; every song only adds on top of the scene's values. The stat songs stay small next to the
// boons; the later ones give control over a run's rewards (more portals, redraws, banishes, beads) instead of power.
// Saves index ranks by this enum, so new songs go on the end and a retired one keeps its place.
public enum UpgradeId
{
    KeenEye, SwiftFeet, QuickDraw, SpiritShuriken, SteadyBreath, LongShadow, SoulPull, IronWill, FatesThread, SecondDawn,
    ThirdPath, BanishingBell, PondsBlessing, AncestralLore,
}

public sealed class Song
{
    public UpgradeId Id;
    public string Name;
    public int MaxRank;
    public int BaseCost;
    // What the song does at a rank, for the shop's note line.
    public Func<int, string> Effect;
    // No longer sold; ranks bought before are refunded (Soul Pull, once souls stopped dropping from kills).
    public bool Retired;
}

public static class Upgrades
{
    // Spirit Notes for each kill: samurai and scouts, and each boss by BossKind.
    public const int PerSamurai = 1;
    public const int PerScout = 5;
    static readonly int[] PerBoss = { 0, 25, 40, 60, 100 };
    public static int ForBoss(BossKind boss) => PerBoss[(int)boss];

    public const float ParryWindowPerRank = 0.03f;
    public const float SpeedPerRank = 0.5f;
    public const float SwingCutPerRank = 0.05f;
    public const float ShurikenCutPerRank = 0.5f;
    public const float MissCutPerRank = 0.25f;
    public const float InvulnerablePerRank = 0.5f;
    // Second Dawn raises him with this many lives.
    public const int DawnLives = 2;

    public static readonly Song[] All =
    {
        new Song { Id = UpgradeId.KeenEye, Name = "Keen Eye", MaxRank = 2, BaseCost = 60,
            Effect = r => $"Parry window  +{r * ParryWindowPerRank:0.00} s" },
        new Song { Id = UpgradeId.SwiftFeet, Name = "Swift Feet", MaxRank = 2, BaseCost = 60,
            Effect = r => $"Move speed  +{r * 5}%" },
        new Song { Id = UpgradeId.QuickDraw, Name = "Quick Draw", MaxRank = 2, BaseCost = 80,
            Effect = r => $"Swing cooldown  -{r * 5}%" },
        new Song { Id = UpgradeId.SpiritShuriken, Name = "Spirit Wind", MaxRank = 2, BaseCost = 80,
            Effect = r => $"Spirit Shuriken cooldown  -{r * ShurikenCutPerRank:0.0} s" },
        new Song { Id = UpgradeId.SteadyBreath, Name = "Steady Breath", MaxRank = 2, BaseCost = 80,
            Effect = r => $"Missed parry cooldown  -{r * MissCutPerRank:0.00} s" },
        new Song { Id = UpgradeId.LongShadow, Name = "Long Shadow", MaxRank = 1, BaseCost = 120,
            Effect = r => $"Untouchable after a hit  +{r * InvulnerablePerRank:0.0} s" },
        new Song { Id = UpgradeId.SoulPull, Name = "Soul Pull", MaxRank = 2, BaseCost = 100, Retired = true,
            Effect = r => "Souls now come from the portals" },
        new Song { Id = UpgradeId.IronWill, Name = "Iron Will", MaxRank = 1, BaseCost = 600,
            Effect = r => $"Start each run with  +{r} life" },
        new Song { Id = UpgradeId.FatesThread, Name = "Fate's Thread", MaxRank = 3, BaseCost = 300,
            Effect = r => $"Redraw a boon offer  {r} time{(r == 1 ? "" : "s")} a run  (R)" },
        new Song { Id = UpgradeId.SecondDawn, Name = "Second Dawn", MaxRank = 1, BaseCost = 1200,
            Effect = r => $"Once a run, rise again with {DawnLives} lives" },
        new Song { Id = UpgradeId.ThirdPath, Name = "Third Path", MaxRank = 1, BaseCost = 400,
            Effect = r => "Three portals open after each round, not two" },
        new Song { Id = UpgradeId.BanishingBell, Name = "Banishing Bell", MaxRank = 2, BaseCost = 350,
            Effect = r => $"Banish a boon from the run  {r} time{(r == 1 ? "" : "s")} a run  (B)" },
        new Song { Id = UpgradeId.PondsBlessing, Name = "Pond's Blessing", MaxRank = 1, BaseCost = 500,
            Effect = r => "A second Spirit Bead after the first boss" },
        new Song { Id = UpgradeId.AncestralLore, Name = "Ancestral Lore", MaxRank = 1, BaseCost = 600,
            Effect = r => "Each stage's first soul offers Rare or better" },
    };

    public static Song Get(UpgradeId id) => All[(int)id];

    static int[] Ranks
    {
        get
        {
            SaveData save = Save.Data;
            if (save.ranks == null || save.ranks.Length < All.Length) Array.Resize(ref save.ranks, All.Length);
            return save.ranks;
        }
    }

    public static int Bought(UpgradeId id) => Ranks[(int)id];
    public static bool IsOff(UpgradeId id) => Save.Data.songsOff.Contains((int)id);
    // The rank in play: a maxed song can be switched off for a harder run.
    public static int Rank(UpgradeId id) => IsOff(id) ? 0 : Bought(id);
    public static bool Maxed(UpgradeId id) => Bought(id) >= Get(id).MaxRank;

    public static int TotalBought
    {
        get
        {
            int total = 0;
            foreach (int rank in Ranks) total += rank;
            return total;
        }
    }

    // Vampire Survivors' formula at half its fee: the song's own price climbs with its rank, and every rank bought
    // anywhere adds a growing surcharge, so the first songs are cheap and finishing the set takes a while.
    public static int Price(UpgradeId id)
    {
        int total = TotalBought;
        int fee = total == 0 ? 0 : Mathf.FloorToInt(10f * Mathf.Pow(1.1f, total));
        return Get(id).BaseCost * (Bought(id) + 1) + fee;
    }

    public static bool CanBuy(UpgradeId id) => !Maxed(id) && Save.Data.embers >= Price(id);

    public static bool Buy(UpgradeId id)
    {
        if (!CanBuy(id)) return false;
        Save.Data.embers -= Price(id);
        Ranks[(int)id]++;
        Save.Write();
        return true;
    }

    // Everything spent comes back. Prices are worked out again in buying order, since the surcharge grows with it.
    public static int Spent
    {
        get
        {
            int spent = 0, total = 0;
            for (int i = 0; i < All.Length; i++)
            {
                for (int r = 0; r < Ranks[i]; r++)
                {
                    spent += All[i].BaseCost * (r + 1);
                    total++;
                }
            }
            // The surcharge on the nth rank bought was 10 × 1.1^(n-1), whatever the order.
            for (int n = 1; n < total; n++) spent += Mathf.FloorToInt(10f * Mathf.Pow(1.1f, n));
            return spent;
        }
    }

    public static void RefundAll()
    {
        Save.Data.embers += Spent;
        Array.Clear(Ranks, 0, Ranks.Length);
        Save.Data.songsOff.Clear();
        Save.Write();
    }

    public static void Toggle(UpgradeId id)
    {
        if (!Maxed(id)) return;
        if (!Save.Data.songsOff.Remove((int)id)) Save.Data.songsOff.Add((int)id);
        Save.Write();
    }

    public static void AddNotes(int notes) => Save.Data.embers += notes;

    // A retired song's ranks come back as notes at their base price, and stop counting toward the surcharge.
    public static void RefundRetired()
    {
        bool changed = false;
        foreach (Song song in All)
        {
            int bought = Ranks[(int)song.Id];
            if (!song.Retired || bought == 0) continue;
            for (int r = 0; r < bought; r++) Save.Data.embers += song.BaseCost * (r + 1);
            Ranks[(int)song.Id] = 0;
            Save.Data.songsOff.Remove((int)song.Id);
            changed = true;
        }
        if (changed) Save.Write();
    }
}
