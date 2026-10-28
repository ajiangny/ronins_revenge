using System;
using UnityEngine;

// The Oaths of the Ronin, sworn at the oath stone in the temple (Hades' Pact of Punishment): once he has seen a dawn,
// each oath makes the next runs harder, and every rank sworn is a point of Heat that raises the Spirit Notes he
// earns. The first dawn at a higher Heat than ever before pays a bounty. Saves index ranks by this enum, so new oaths
// go on the end.
public enum OathId { HastenedMarch, RainOfArrows, IronGenerals, LongRoad, FadedSpirits, FrailBody }

public sealed class Oath
{
    public OathId Id;
    public string Name;
    public int MaxRank;
    // What the oath does at a rank, for the page's note line.
    public Func<int, string> Effect;
}

public static class Oaths
{
    public const float MarchPerRank = 0.1f;
    public const float ArrowIntervalPerRank = 0.1f;
    public const float ArrowSpeedPerRank = 0.05f;
    public const float BossHealthPerRank = 0.15f;
    // Each point of Heat adds this much to the notes earned.
    public const float NotesPerHeat = 0.1f;
    // The first dawn at a new highest Heat pays this many notes per point.
    public const int BountyPerHeat = 150;

    public static readonly Oath[] All =
    {
        new Oath { Id = OathId.HastenedMarch, Name = "Hastened March", MaxRank = 2,
            Effect = r => $"The armies march  +{r * MarchPerRank * 100:0}% faster" },
        new Oath { Id = OathId.RainOfArrows, Name = "Rain of Arrows", MaxRank = 2,
            Effect = r => $"Arrows loosed  {r * ArrowIntervalPerRank * 100:0}% more often, and  +{r * ArrowSpeedPerRank * 100:0}% faster" },
        new Oath { Id = OathId.IronGenerals, Name = "Iron Generals", MaxRank = 2,
            Effect = r => $"Every warlord has  +{r * BossHealthPerRank * 100:0}% health" },
        new Oath { Id = OathId.LongRoad, Name = "The Long Road", MaxRank = 2,
            Effect = r => $"The warlord's portal opens  {r} round{(r == 1 ? "" : "s")} later" },
        new Oath { Id = OathId.FadedSpirits, Name = "Faded Spirits", MaxRank = 2,
            Effect = r => r >= 2 ? "No Spirit Beads behind the portals, and a warlord's boon may be Common"
                : "No Spirit Beads behind the portals" },
        new Oath { Id = OathId.FrailBody, Name = "Frail Body", MaxRank = 1,
            Effect = r => "Begin every run with one life fewer" },
    };

    public static Oath Get(OathId id) => All[(int)id];

    static int[] Ranks
    {
        get
        {
            SaveData save = Save.Data;
            if (save.oaths == null || save.oaths.Length < All.Length) Array.Resize(ref save.oaths, All.Length);
            return save.oaths;
        }
    }

    // The stone answers only once Kagetsune has fallen.
    public static bool Unlocked => Save.Data.runsCleared > 0;

    public static int Rank(OathId id) => Unlocked ? Ranks[(int)id] : 0;

    public static int Heat
    {
        get
        {
            if (!Unlocked) return 0;
            int heat = 0;
            foreach (int rank in Ranks) heat += rank;
            return heat;
        }
    }

    public static float NoteMultiplier => 1f + Heat * NotesPerHeat;

    // Each press swears one rank more, and past the last rank releases the oath.
    public static void Cycle(OathId id)
    {
        Ranks[(int)id] = (Ranks[(int)id] + 1) % (Get(id).MaxRank + 1);
        Save.Write();
    }

    // Called on a dawn: the bounty for a Heat higher than any cleared before, or 0.
    public static int ClaimBounty()
    {
        int heat = Heat;
        if (heat <= Save.Data.bestHeatCleared) return 0;
        int bounty = 0;
        for (int h = Save.Data.bestHeatCleared + 1; h <= heat; h++) bounty += BountyPerHeat * h;
        Save.Data.bestHeatCleared = heat;
        Save.Data.embers += bounty;
        Save.Write();
        return bounty;
    }

    public static Color HeatColour => new Color(1f, 0.42f, 0.25f);
}
