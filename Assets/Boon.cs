using System;
using System.Collections.Generic;
using UnityEngine;

// The three spirit elements a soul can carry. Like the gods in Hades, each has a theme:
// Fire burns (explosions), Water chills (slows and freezes), Lightning jolts (chains).
public enum Element { Fire, Water, Lightning }

// Attack, Special and Parry each hold one boon; taking another element's boon for that slot replaces it.
// Passives don't take a slot and are kept alongside each other.
public enum BoonSlot { Attack, Special, Parry, Passive }

public enum Rarity { Common, Rare, Epic }

// Indexes the icon array, so keep the order in step with BoonChoiceUI.boonIcons.
public enum BoonId
{
    FlameKunai, FrostKunai, StormKunai,
    InfernoShuriken, GlacialShuriken, ThunderShuriken,
    BurningRiposte, FrozenRiposte, ThunderRiposte,
    BlazingHands, InfernoFan,
    TidalRenewal, GlacierWall,
    StormStep, StaticCharge,
}

public sealed class BoonDefinition
{
    public BoonId Id;
    public Element Element;
    public BoonSlot Slot;
    public string Name;
    public string Description;
    public string StatLabel;
    // Indexed by Rarity.
    public float[] Values;
    // Formats one value for the stat line, e.g. v => $"+{v:0.#}".
    public Func<float, string> Format;
    // Applied once when taken, instead of being kept in the loadout (Tidal Renewal).
    public bool Instant;

    public float ValueAt(Rarity rarity) => Values[(int)rarity];
}

public readonly struct ElementInfo
{
    public readonly string Name;
    public readonly string Subtitle;
    public readonly Color Color;

    public ElementInfo(string name, string subtitle, Color color)
    {
        Name = name;
        Subtitle = subtitle;
        Color = color;
    }
}

// Rarity is rolled per card; a boon the ronin already has comes back as an upgrade to the next rarity instead.
public readonly struct BoonOffer
{
    public readonly BoonDefinition Boon;
    public readonly Rarity Rarity;
    // Set when this card upgrades a boon the ronin already holds.
    public readonly bool IsUpgrade;
    // The other element's boon this one would push out of its slot, if any.
    public readonly EquippedBoon Replaces;

    public BoonOffer(BoonDefinition boon, Rarity rarity, bool isUpgrade, EquippedBoon replaces)
    {
        Boon = boon;
        Rarity = rarity;
        IsUpgrade = isUpgrade;
        Replaces = replaces;
    }
}

public static class Boons
{
    public const int MaxLives = 5;

    static readonly float[] RarityWeights = { 60f, 30f, 10f };

    static readonly ElementInfo[] Elements =
    {
        new ElementInfo("Fire", "the ember spirits of the burned shrine lend you their fury.", new Color(1f, 0.38f, 0.2f)),
        new ElementInfo("Frost", "the still waters of the temple pond lend you their calm.", new Color(0.35f, 0.75f, 1f)),
        new ElementInfo("Storm", "the thunder over the mountain pass lends you its speed.", new Color(0.78f, 0.45f, 1f)),
    };

    public static ElementInfo Info(Element element) => Elements[(int)element];

    public static Color RarityColor(Rarity rarity) => rarity switch
    {
        Rarity.Rare => new Color(0.35f, 0.65f, 1f),
        Rarity.Epic => new Color(0.75f, 0.4f, 1f),
        _ => new Color(0.95f, 0.95f, 0.95f),
    };

    static string Plus(float v) => $"+{v:0.#}";
    static string Percent(float v) => $"+{v * 100:0}%";
    static string MinusPercent(float v) => $"-{v * 100:0}%";
    static string Seconds(float v) => $"{v:0.#}s";

    public static readonly BoonDefinition[] All =
    {
        // Fire: burn. Kills explode. Samurai stand 2 apart across and 1.75 apart down, so 1.8 reaches the samurai
    // above and below, 2.1 adds the ones beside it and 2.7 the diagonals.
        new BoonDefinition { Id = BoonId.FlameKunai, Element = Element.Fire, Slot = BoonSlot.Attack, Name = "Flame Kunai",
            Description = "Your <b>Kunai</b> burst into flame on a kill, burning nearby samurai.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 2.1f, 2.7f }, Format = Plus },
        new BoonDefinition { Id = BoonId.InfernoShuriken, Element = Element.Fire, Slot = BoonSlot.Special, Name = "Inferno Shuriken",
            Description = "Every samurai your <b>Shuriken</b> cuts down explodes.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 2.1f, 2.7f }, Format = Plus },
        new BoonDefinition { Id = BoonId.BurningRiposte, Element = Element.Fire, Slot = BoonSlot.Parry, Name = "Burning Riposte",
            Description = "Arrows you <b>Parry</b> come back as fire arrows that explode.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 2.1f, 2.7f }, Format = Plus },
        new BoonDefinition { Id = BoonId.BlazingHands, Element = Element.Fire, Slot = BoonSlot.Passive, Name = "Blazing Hands",
            Description = "You throw <b>Kunai</b> faster.",
            StatLabel = "Throw cooldown:", Values = new[] { 0.15f, 0.25f, 0.35f }, Format = MinusPercent },
        new BoonDefinition { Id = BoonId.InfernoFan, Element = Element.Fire, Slot = BoonSlot.Passive, Name = "Inferno Fan",
            Description = "You throw extra <b>Kunai</b> in a spread.",
            StatLabel = "Extra kunai:", Values = new[] { 1f, 2f, 3f }, Format = Plus },

        // Water: chill. Slows, freezes, protects.
        new BoonDefinition { Id = BoonId.FrostKunai, Element = Element.Water, Slot = BoonSlot.Attack, Name = "Frost Kunai",
            Description = "Each <b>Kunai</b> kill <b>Chills</b> the army, slowing its march. Stacks 3 times.",
            StatLabel = "Slow per stack:", Values = new[] { 0.05f, 0.07f, 0.1f }, Format = Percent },
        new BoonDefinition { Id = BoonId.GlacialShuriken, Element = Element.Water, Slot = BoonSlot.Special, Name = "Glacial Shuriken",
            Description = "Your <b>Shuriken</b> freezes the whole army: no marching, no arrows.",
            StatLabel = "Freeze time:", Values = new[] { 1f, 1.5f, 2f }, Format = Seconds },
        new BoonDefinition { Id = BoonId.FrozenRiposte, Element = Element.Water, Slot = BoonSlot.Parry, Name = "Frozen Riposte",
            Description = "A successful <b>Parry</b> grants a <b>Frost Ward</b> that absorbs the next hit.",
            StatLabel = "Max wards:", Values = new[] { 1f, 1f, 2f }, Format = Plus },
        new BoonDefinition { Id = BoonId.TidalRenewal, Element = Element.Water, Slot = BoonSlot.Passive, Name = "Tidal Renewal",
            Description = "The temple pond restores your spirit. Gain <b>Lives</b> now (up to 5).",
            StatLabel = "Lives:", Values = new[] { 1f, 1f, 2f }, Format = Plus, Instant = true },
        new BoonDefinition { Id = BoonId.GlacierWall, Element = Element.Water, Slot = BoonSlot.Passive, Name = "Glacier Wall",
            Description = "Rebuild every <b>Blocker</b> now, sheathed in ice, and for every stage after.",
            StatLabel = "Blocker health:", Values = new[] { 0.25f, 0.5f, 1f }, Format = Percent },

        // Lightning: jolt. Chains and speed.
        new BoonDefinition { Id = BoonId.StormKunai, Element = Element.Lightning, Slot = BoonSlot.Attack, Name = "Storm Kunai",
            Description = "A <b>Kunai</b> kill may arc lightning to the nearest samurai.",
            StatLabel = "Chain chance:", Values = new[] { 0.25f, 0.35f, 0.5f }, Format = Percent },
        new BoonDefinition { Id = BoonId.ThunderShuriken, Element = Element.Lightning, Slot = BoonSlot.Special, Name = "Thunder Shuriken",
            Description = "Your <b>Shuriken</b> calls lightning on the front samurai of nearby columns.",
            StatLabel = "Extra columns:", Values = new[] { 1f, 2f, 3f }, Format = Plus },
        new BoonDefinition { Id = BoonId.ThunderRiposte, Element = Element.Lightning, Slot = BoonSlot.Parry, Name = "Thunder Riposte",
            Description = "A successful <b>Parry</b> strikes the samurai nearest above you with lightning.",
            StatLabel = "Strikes:", Values = new[] { 1f, 2f, 3f }, Format = Plus },
        new BoonDefinition { Id = BoonId.StormStep, Element = Element.Lightning, Slot = BoonSlot.Passive, Name = "Storm Step",
            Description = "You move faster.",
            StatLabel = "Move speed:", Values = new[] { 0.15f, 0.25f, 0.35f }, Format = Percent },
        new BoonDefinition { Id = BoonId.StaticCharge, Element = Element.Lightning, Slot = BoonSlot.Passive, Name = "Static Charge",
            Description = "Your <b>Shuriken</b> and <b>Parry</b> recharge faster.",
            StatLabel = "Cooldowns:", Values = new[] { 0.15f, 0.25f, 0.35f }, Format = MinusPercent },
    };

    public static BoonDefinition Get(BoonId id) => All[Array.FindIndex(All, b => b.Id == id)];

    // Up to count distinct boons of one element. Like Hades, the first boon of a run is always a core slot boon.
    public static List<BoonOffer> Draw(Element element, int count, RoninLoadout loadout, int lives)
    {
        var pool = new List<BoonDefinition>();
        foreach (BoonDefinition boon in All)
        {
            if (boon.Element != element) continue;
            if (loadout.BoonCount == 0 && boon.Slot == BoonSlot.Passive) continue;
            if (loadout.Find(boon.Id) is { Rarity: Rarity.Epic }) continue;
            if (boon.Id == BoonId.TidalRenewal && lives >= MaxLives) continue;
            pool.Add(boon);
        }

        var offers = new List<BoonOffer>();
        while (offers.Count < count && pool.Count > 0)
        {
            BoonDefinition boon = pool[UnityEngine.Random.Range(0, pool.Count)];
            pool.Remove(boon);

            EquippedBoon held = loadout.Find(boon.Id);
            EquippedBoon replaces = boon.Slot == BoonSlot.Passive || held != null ? null : loadout.InSlot(boon.Slot);
            Rarity rarity = held != null ? held.Rarity + 1 : RollRarity();
            offers.Add(new BoonOffer(boon, rarity, held != null, replaces));
        }
        return offers;
    }

    static Rarity RollRarity()
    {
        float roll = UnityEngine.Random.value * (RarityWeights[0] + RarityWeights[1] + RarityWeights[2]);
        if (roll < RarityWeights[2]) return Rarity.Epic;
        if (roll < RarityWeights[2] + RarityWeights[1]) return Rarity.Rare;
        return Rarity.Common;
    }
}
