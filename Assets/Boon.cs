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

// Duo and Legendary boons are capstones, offered once their prerequisite boons are held and never upgraded.
// An Offering stands in when a soul's element has nothing left to give. A Sword Art (from a Technique Scroll, like
// Hades' Daedalus Hammer) reshapes the katana's slash itself; it belongs to no element and is never upgraded.
public enum BoonKind { Normal, Duo, Legendary, Offering, Art }

// Indexes the icon array, so keep the order in step with BoonChoiceUI.boonIcons.
public enum BoonId
{
    FlameBlade, FrostBlade, StormBlade,
    InfernoShuriken, GlacialShuriken, ThunderShuriken,
    BurningRiposte, FrozenRiposte, ThunderRiposte,
    BlazingHands, Kindling,
    TidalRenewal, GlacierWall,
    StormStep, StaticCharge,
    ThermalShock, PlasmaArc, Superconductor,
    Wildfire, StillWater, RaijinsDrum,
    SplitSlash, PiercingSlash, EchoSlash, CrescentMoon,
    SpiritOffering,
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
    // An optional second stat on the same line (Frost Blade's ice shards), indexed by Rarity and shown as a count.
    public string ExtraLabel;
    public float[] ExtraValues;
    // Applied once when taken, instead of being kept in the loadout (Tidal Renewal).
    public bool Instant;
    public BoonKind Kind;
    // A Duo's second element: a soul of either element can offer it.
    public Element? Partner;
    // What the ronin must hold before a capstone is offered.
    public Func<RoninLoadout, bool> Requires;

    public bool IsCapstone => Kind == BoonKind.Duo || Kind == BoonKind.Legendary;
    public bool Offers(Element element) => Element == element || Partner == element;

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

    // How many shards an ice statue bursts into: Frost Blade's shard count, or its Common count without it.
    public static int IceShardsAt(Rarity rarity) => (int)Get(BoonId.FrostBlade).ExtraValues[(int)rarity];

    public static Color RarityColor(Rarity rarity) => rarity switch
    {
        Rarity.Rare => new Color(0.35f, 0.65f, 1f),
        Rarity.Epic => new Color(0.75f, 0.4f, 1f),
        _ => new Color(0.95f, 0.95f, 0.95f),
    };

    // Hades' colours: green for a Duo, orange for a Legendary.
    public static Color KindColor(BoonKind kind, Rarity rarity) => kind switch
    {
        BoonKind.Duo => new Color(0.55f, 0.95f, 0.45f),
        BoonKind.Legendary => new Color(1f, 0.6f, 0.2f),
        BoonKind.Offering => new Color(1f, 0.85f, 0.5f),
        BoonKind.Art => ArtColor,
        _ => RarityColor(rarity),
    };

    // Polished steel: the Sword Arts belong to the blade, not to any spirit.
    public static readonly Color ArtColor = new Color(0.82f, 0.86f, 0.92f);

    // The core slot boons each capstone builds on: fire blasts, ice, and lightning strikes.
    static readonly BoonId[] BlastBoons = { BoonId.FlameBlade, BoonId.InfernoShuriken, BoonId.BurningRiposte };
    static readonly BoonId[] IceBoons = { BoonId.FrostBlade, BoonId.GlacialShuriken, BoonId.FrozenRiposte };
    static readonly BoonId[] StrikeBoons = { BoonId.StormBlade, BoonId.ThunderShuriken, BoonId.ThunderRiposte };

    static bool HoldsAny(RoninLoadout loadout, BoonId[] ids) => Array.Exists(ids, id => loadout.Find(id) != null);

    static bool TwoSlotsOf(RoninLoadout loadout, Element element)
    {
        int count = 0;
        foreach (BoonSlot slot in new[] { BoonSlot.Attack, BoonSlot.Special, BoonSlot.Parry })
        {
            if (loadout.SlotElement(slot) == element) count++;
        }
        return count >= 2;
    }

    static string Plus(float v) => $"+{v:0.#}";
    static string Percent(float v) => $"+{v * 100:0}%";
    static string MinusPercent(float v) => $"-{v * 100:0}%";
    static string Seconds(float v) => $"{v:0.#}s";

    public static readonly BoonDefinition[] All =
    {
        // Fire: burn. Kills explode. Samurai stand 2 apart across and 1.75 apart down, so 1.8 reaches the samurai
    // above and below, 2.1 adds the ones beside it and 2.7 the diagonals.
        new BoonDefinition { Id = BoonId.FlameBlade, Element = Element.Fire, Slot = BoonSlot.Attack, Name = "Flame Blade",
            Description = "Your <b>Slashes</b> burst into flame on a kill, burning nearby samurai.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 2.1f, 2.7f }, Format = Plus },
        new BoonDefinition { Id = BoonId.InfernoShuriken, Element = Element.Fire, Slot = BoonSlot.Special, Name = "Inferno Shuriken",
            Description = "Every samurai your <b>Shuriken</b> cuts down explodes.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 2.1f, 2.7f }, Format = Plus },
        new BoonDefinition { Id = BoonId.BurningRiposte, Element = Element.Fire, Slot = BoonSlot.Parry, Name = "Burning Riposte",
            Description = "Arrows you <b>Parry</b> come back as a fireball that explodes on impact.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 2.1f, 2.7f }, Format = Plus },
        new BoonDefinition { Id = BoonId.BlazingHands, Element = Element.Fire, Slot = BoonSlot.Passive, Name = "Blazing Hands",
            Description = "You <b>Slash</b> faster.",
            StatLabel = "Slash cooldown:", Values = new[] { 0.15f, 0.25f, 0.35f }, Format = MinusPercent },
        new BoonDefinition { Id = BoonId.Kindling, Element = Element.Fire, Slot = BoonSlot.Passive, Name = "Kindling",
            Description = "Every few <b>Slash</b> kills, the embers catch: a great fire blast bursts from the fallen samurai.",
            StatLabel = "Kills per blast:", Values = new[] { 5f, 4f, 3f }, Format = v => $"{v:0}" },

        // Water: ice. Encases, freezes, shatters, protects.
        new BoonDefinition { Id = BoonId.FrostBlade, Element = Element.Water, Slot = BoonSlot.Attack, Name = "Frost Blade",
            Description = "A <b>Slash</b> kill encases nearby samurai in ice. Cut one down and it freezes where it stands; strike the ice to <b>Shatter</b> it into shards.",
            StatLabel = "Freeze time:", Values = new[] { 2f, 3f, 4f }, Format = Seconds,
            ExtraLabel = "Shards:", ExtraValues = new[] { 2f, 3f, 4f } },
        new BoonDefinition { Id = BoonId.GlacialShuriken, Element = Element.Water, Slot = BoonSlot.Special, Name = "Glacial Shuriken",
            Description = "Your <b>Shuriken</b> freezes the whole army: no marching, no arrows.",
            StatLabel = "Freeze time:", Values = new[] { 1f, 1.5f, 2f }, Format = Seconds },
        new BoonDefinition { Id = BoonId.FrozenRiposte, Element = Element.Water, Slot = BoonSlot.Parry, Name = "Frozen Riposte",
            Description = "Arrows you <b>Parry</b> burst into homing ice shards that freeze what they hit.",
            StatLabel = "Ice shards:", Values = new[] { 2f, 3f, 4f }, Format = Plus },
        new BoonDefinition { Id = BoonId.TidalRenewal, Element = Element.Water, Slot = BoonSlot.Passive, Name = "Tidal Renewal",
            Description = "The temple pond deepens your spirit. Gain <b>Max Lives</b>, filled at once (up to 5). From Rare, every lost life heals too.",
            StatLabel = "Max lives:", Values = new[] { 1f, 1f, 2f }, Format = Plus, Instant = true },
        new BoonDefinition { Id = BoonId.GlacierWall, Element = Element.Water, Slot = BoonSlot.Passive, Name = "Glacier Wall",
            Description = "Rebuild every <b>Blocker</b> now, sheathed in ice, and for every stage after.",
            StatLabel = "Blocker health:", Values = new[] { 0.25f, 0.5f, 1f }, Format = Percent },

        // Lightning: jolt. Chains and speed.
        new BoonDefinition { Id = BoonId.StormBlade, Element = Element.Lightning, Slot = BoonSlot.Attack, Name = "Storm Blade",
            Description = "A <b>Slash</b> kill may arc lightning to a samurai further off in the ranks.",
            StatLabel = "Chain chance:", Values = new[] { 0.35f, 0.5f, 0.7f }, Format = Percent },
        new BoonDefinition { Id = BoonId.ThunderShuriken, Element = Element.Lightning, Slot = BoonSlot.Special, Name = "Thunder Shuriken",
            Description = "Your <b>Shuriken</b> flies much faster, and may arc lightning into each samurai it passes near.",
            StatLabel = "Chain chance:", Values = new[] { 0.3f, 0.45f, 0.6f }, Format = Percent },
        new BoonDefinition { Id = BoonId.ThunderRiposte, Element = Element.Lightning, Slot = BoonSlot.Parry, Name = "Thunder Riposte",
            Description = "A successful <b>Parry</b> strikes the samurai nearest above you with lightning.",
            StatLabel = "Strikes:", Values = new[] { 1f, 2f, 3f }, Format = Plus },
        new BoonDefinition { Id = BoonId.StormStep, Element = Element.Lightning, Slot = BoonSlot.Passive, Name = "Storm Step",
            Description = "You move faster.",
            StatLabel = "Move speed:", Values = new[] { 0.15f, 0.25f, 0.35f }, Format = Percent },
        new BoonDefinition { Id = BoonId.StaticCharge, Element = Element.Lightning, Slot = BoonSlot.Passive, Name = "Static Charge",
            Description = "Your <b>Shuriken</b> and <b>Parry</b> recharge faster.",
            StatLabel = "Cooldowns:", Values = new[] { 0.15f, 0.25f, 0.35f }, Format = MinusPercent },

        // Duos: one core boon from each of two elements.
        new BoonDefinition { Id = BoonId.ThermalShock, Element = Element.Fire, Partner = Element.Water, Slot = BoonSlot.Passive, Kind = BoonKind.Duo,
            Name = "Thermal Shock",
            Description = "Your fire <b>Blasts</b> deal double damage to samurai held in ice, or to the whole army while it's frozen.",
            StatLabel = "Blast damage on ice:", Values = new[] { 2f, 2f, 2f }, Format = v => $"x{v:0}",
            Requires = l => HoldsAny(l, BlastBoons) && HoldsAny(l, IceBoons) },
        new BoonDefinition { Id = BoonId.PlasmaArc, Element = Element.Fire, Partner = Element.Lightning, Slot = BoonSlot.Passive, Kind = BoonKind.Duo,
            Name = "Plasma Arc",
            Description = "Every <b>Lightning</b> strike bursts into a fire blast around its target.",
            StatLabel = "Blast radius:", Values = new[] { 1.8f, 1.8f, 1.8f }, Format = Plus,
            Requires = l => HoldsAny(l, BlastBoons) && HoldsAny(l, StrikeBoons) },
        new BoonDefinition { Id = BoonId.Superconductor, Element = Element.Water, Partner = Element.Lightning, Slot = BoonSlot.Passive, Kind = BoonKind.Duo,
            Name = "Superconductor",
            Description = "<b>Lightning</b> that strikes a samurai held in ice arcs on to others held in ice. Strikes on a frozen boss deal double.",
            StatLabel = "Extra arcs:", Values = new[] { 3f, 3f, 3f }, Format = Plus,
            Requires = l => HoldsAny(l, IceBoons) && HoldsAny(l, StrikeBoons) },

        // Legendaries: two core slots of one element.
        new BoonDefinition { Id = BoonId.Wildfire, Element = Element.Fire, Slot = BoonSlot.Passive, Kind = BoonKind.Legendary,
            Name = "Wildfire",
            Description = "Your fire <b>Blasts</b> grow wider and burn away the enemy arrows inside them.",
            StatLabel = "Blast radius:", Values = new[] { 0.6f, 0.6f, 0.6f }, Format = Plus,
            Requires = l => TwoSlotsOf(l, Element.Fire) },
        new BoonDefinition { Id = BoonId.StillWater, Element = Element.Water, Slot = BoonSlot.Passive, Kind = BoonKind.Legendary,
            Name = "Still Water",
            Description = "The army quickens only half as much as its ranks thin, and your <b>Freezes</b> last longer.",
            StatLabel = "Freeze time:", Values = new[] { 0.5f, 0.5f, 0.5f }, Format = Percent,
            Requires = l => TwoSlotsOf(l, Element.Water) },
        new BoonDefinition { Id = BoonId.RaijinsDrum, Element = Element.Lightning, Slot = BoonSlot.Passive, Kind = BoonKind.Legendary,
            Name = "Raijin's Drum",
            Description = "Every few hits beat the thunder drum: <b>Lightning</b> strikes the front samurai of the 3 columns nearest you.",
            StatLabel = "Hits per beat:", Values = new[] { 8f, 8f, 8f }, Format = v => $"{v:0}",
            Requires = l => TwoSlotsOf(l, Element.Lightning) },

        // Sword Arts: dropped as Technique Scrolls (mostly by scouts), at most two a run. Element is unused.
        new BoonDefinition { Id = BoonId.SplitSlash, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Split Slash",
            Description = "A moment into its flight, every <b>Slash</b> splits: two smaller slashes peel off to either side.",
            StatLabel = "Side slashes:", Values = new[] { 2f, 2f, 2f }, Format = Plus },
        new BoonDefinition { Id = BoonId.PiercingSlash, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Piercing Slash",
            Description = "Your <b>Slash</b> cuts through samurai and arrows instead of stopping at the first.",
            StatLabel = "Samurai pierced:", Values = new[] { 2f, 2f, 2f }, Format = v => $"{v:0}" },
        new BoonDefinition { Id = BoonId.EchoSlash, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Echo Slash",
            Description = "A swing may send a ghostly second <b>Slash</b> that hunts down an enemy.",
            StatLabel = "Echo chance:", Values = new[] { 0.25f, 0.25f, 0.25f }, Format = Percent },
        new BoonDefinition { Id = BoonId.CrescentMoon, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Crescent Moon",
            Description = "Your <b>Slash</b> is a great crescent, wide enough to cut two columns and every arrow across them.",
            StatLabel = "Slash width:", Values = new[] { 1.7f, 1.7f, 1.7f }, Format = v => $"x{v:0.#}" },
    };

    // Stands in when a soul's element has nothing left to offer, so a caught soul is never wasted.
    static BoonDefinition Offering(Element element) => new BoonDefinition
    {
        Id = BoonId.SpiritOffering, Element = element, Slot = BoonSlot.Passive, Kind = BoonKind.Offering, Instant = true,
        Name = "Spirit Offering",
        Description = "This spirit has no more boons to give. It mends you instead: heal a life, or take points at full health.",
        StatLabel = "Heal:", Values = new[] { 1f, 1f, 1f }, Format = v => $"+{v:0} life",
    };

    // Up to count Sword Arts the ronin doesn't hold yet, for a Technique Scroll.
    public static List<BoonOffer> DrawArts(RoninLoadout loadout, int count)
    {
        var pool = new List<BoonDefinition>();
        foreach (BoonDefinition boon in All)
        {
            if (boon.Kind == BoonKind.Art && loadout.Find(boon.Id) == null) pool.Add(boon);
        }
        var offers = new List<BoonOffer>();
        while (offers.Count < count && pool.Count > 0)
        {
            BoonDefinition art = pool[UnityEngine.Random.Range(0, pool.Count)];
            pool.Remove(art);
            offers.Add(new BoonOffer(art, Rarity.Common, false, null));
        }
        return offers;
    }

    // Capstones the ronin could take right now, of any element.
    public static List<BoonDefinition> EligibleCapstones(RoninLoadout loadout)
    {
        var eligible = new List<BoonDefinition>();
        foreach (BoonDefinition boon in All)
        {
            if (boon.IsCapstone && loadout.Find(boon.Id) == null && boon.Requires(loadout)) eligible.Add(boon);
        }
        return eligible;
    }

    public static BoonDefinition Get(BoonId id) => All[Array.FindIndex(All, b => b.Id == id)];

    // Up to count distinct boons of one element. Like Hades, the first boon of a run is always a core slot boon.
    // ensureCapstone puts an eligible Duo or Legendary in the offer (the stage boon), and no card rolls below minRarity.
    public static List<BoonOffer> Draw(Element element, int count, RoninLoadout loadout, int maxLives,
        bool ensureCapstone = false, Rarity minRarity = Rarity.Common)
    {
        var pool = new List<BoonDefinition>();
        var capstones = new List<BoonDefinition>();
        foreach (BoonDefinition boon in All)
        {
            if (boon.Kind == BoonKind.Art || !boon.Offers(element)) continue;
            if (boon.IsCapstone)
            {
                if (loadout.Find(boon.Id) == null && boon.Requires(loadout)) capstones.Add(boon);
                continue;
            }
            if (loadout.BoonCount == 0 && boon.Slot == BoonSlot.Passive) continue;
            if (loadout.Find(boon.Id) is { Rarity: Rarity.Epic }) continue;
            if (boon.Id == BoonId.TidalRenewal && maxLives >= MaxLives) continue;
            pool.Add(boon);
        }

        var offers = new List<BoonOffer>();
        if (ensureCapstone && capstones.Count > 0)
        {
            BoonDefinition capstone = capstones[UnityEngine.Random.Range(0, capstones.Count)];
            capstones.Remove(capstone);
            offers.Add(new BoonOffer(capstone, Rarity.Common, false, null));
        }
        pool.AddRange(capstones);

        while (offers.Count < count && pool.Count > 0)
        {
            BoonDefinition boon = pool[UnityEngine.Random.Range(0, pool.Count)];
            pool.Remove(boon);

            if (boon.IsCapstone)
            {
                offers.Add(new BoonOffer(boon, Rarity.Common, false, null));
                continue;
            }
            EquippedBoon held = loadout.Find(boon.Id);
            EquippedBoon replaces = boon.Slot == BoonSlot.Passive || held != null ? null : loadout.InSlot(boon.Slot);
            Rarity rolled = RollRarity();
            Rarity rarity = held != null ? held.Rarity + 1 : rolled > minRarity ? rolled : minRarity;
            offers.Add(new BoonOffer(boon, rarity, held != null, replaces));
        }
        if (offers.Count == 0) offers.Add(new BoonOffer(Offering(element), Rarity.Common, false, null));
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
