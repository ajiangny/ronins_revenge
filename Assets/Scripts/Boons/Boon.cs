using System;
using System.Collections.Generic;
using UnityEngine;

// The five spirit elements a soul can carry. Like the gods in Hades, each has a theme:
// Fire burns (explosions), Water chills (slows and freezes), Lightning jolts (chains), Tide soaks and heals (the
// pond's water: it amplifies the others and rolls the army back), Steel rebuilds the cover by attacking. Water is
// Frost on screen and Tide is Water: Tide and Steel came last and are appended so every saved Element index and
// Frost asset keeps meaning what it did.
public enum Element { Fire, Water, Lightning, Tide, Steel }

// Attack, Special, Parry and Ultimate each hold one boon; taking another element's boon for that slot replaces it.
// The Ultimate slot is Hades' Call: its boon gives the Shadow Clone its element, shown on the ultimate's medallion.
// Passives don't take a slot and are kept alongside each other.
public enum BoonSlot { Attack, Special, Parry, Ultimate, Passive }

public enum Rarity { Common, Rare, Epic }

// Duo and Legendary boons are capstones, offered once their prerequisite boons are held and never upgraded. An
// Infusion (Hades II's) is a capstone too: it wakes once three boons of its element are held, passives included,
// and deepens that element's status.
// An Offering stands in when a soul's element has nothing left to give. A Sword Art (from a Technique Scroll, like
// Hades' Daedalus Hammer) reshapes the katana's slash itself; it belongs to no element and is never upgraded.
public enum BoonKind { Normal, Duo, Legendary, Offering, Art, Infusion }

// Indexes the icon array, so keep the order in step with BoonChoiceUI.boonIcons.
public enum BoonId
{
    FlameBlade, FrostBlade, StormBlade,
    InfernoShuriken, GlacialShuriken, ThunderShuriken,
    BurningRiposte, FrozenRiposte, ThunderRiposte,
    BlazingHands, Kindling,
    TidalRenewal, GlacierShell,
    StormStep, StaticCharge,
    ThermalShock, PlasmaArc, Superconductor,
    Wildfire, StillWater, RaijinsDrum,
    SplitSlash, PiercingSlash, EchoSlash, ShadowClone,
    SpiritOffering,
    PyreShadow, FrostShadow, StormShadow,
    // Water (Asagi), and Frost's Cold Compress, which took Tidal Renewal's place.
    RiptideBlade, TidewakeShuriken, UndertowRiposte, HealingSpring, TideShadow, DragonGate,
    ScaldingSteam, BlackIce, ThunderRain, ColdCompress,
    // Infusions, one per element: each deepens its element's status (Statuses.cs).
    EmberHeart, Hoarfrost, StaticField, DeepCurrent,
    // Sword Arts for the shuriken and the parry.
    ScatterStar, MirrorStance,
    // Steel (Oliver): the four slots, two passives, the Legendary, the Infusion and the four Duos.
    AnvilBlade, LodestoneShuriken, HammerRiposte, SteelShadow, Whetstone, Shrapnel, SpearBreaker, Tamahagane,
    RedHotIron, Quenching, LightningRod, Rust,
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
    // An optional second stat on the same line (Frost Blade's ice shards), indexed by Rarity and shown as a count
    // unless ExtraFormat says otherwise (Flame Blade's blast chance).
    public string ExtraLabel;
    public float[] ExtraValues;
    public Func<float, string> ExtraFormat;
    // Applied once when taken, instead of being kept in the loadout (Tidal Renewal).
    public bool Instant;
    public BoonKind Kind;
    // A Duo's second element: a soul of either element can offer it.
    public Element? Partner;
    // What the ronin must hold before a capstone is offered.
    public Func<RoninLoadout, bool> Requires;

    // What a Spirit Bead's level adds, like Hades' Pom of Power: the first level adds LevelStep, each after it
    // LevelFalloff of the one before, so the early levels count most. Rarity sets where the stat starts; levels add
    // on top. A whole-number stat (strikes, shards, swings) only moves once the levels add up to a whole one, and
    // Min/Max keep a stat in bounds however high it climbs. A boon with no step (capstones, arts) has no levels.
    public float LevelStep;
    public float ExtraLevelStep;
    public bool Whole;
    public bool ExtraWhole;
    public float Min = float.MinValue;
    public float Max = float.MaxValue;
    public float ExtraMax = float.MaxValue;
    public const float LevelFalloff = 0.8f;

    public bool IsCapstone => Kind == BoonKind.Duo || Kind == BoonKind.Legendary || Kind == BoonKind.Infusion;
    public bool Offers(Element element) => Element == element || Partner == element;
    public bool HasLevels => LevelStep != 0f;

    public float ValueAt(Rarity rarity, int level = 1) => Levelled(Values[(int)rarity], LevelStep, level, Whole, Min, Max);

    public float ExtraAt(Rarity rarity, int level = 1) => ExtraValues == null ? 0f
        : Levelled(ExtraValues[(int)rarity], ExtraLevelStep, level, ExtraWhole, float.MinValue, ExtraMax);

    static float Levelled(float value, float step, int level, bool whole, float min, float max)
    {
        float bonus = 0f, add = step;
        for (int i = 1; i < level; i++)
        {
            bonus += add;
            add *= LevelFalloff;
        }
        // Cut toward zero, so a falling stat (swings, regrow time) only drops once a whole step is earned too.
        if (whole) bonus = (int)bonus;
        return Mathf.Clamp(value + bonus, min, max);
    }
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

// What a card does: a new boon, a Spirit Bead's level on a held one, or a Spirit Thread's rarity on a held one.
public enum OfferKind { New, Level, Rarity }

// Rarity is rolled per card. Souls only offer boons the ronin doesn't hold; a Bead or a Thread raises a held one instead.
public readonly struct BoonOffer
{
    public readonly BoonDefinition Boon;
    public readonly Rarity Rarity;
    // The level the boon will be at once taken (1 for a fresh one).
    public readonly int Level;
    public readonly OfferKind Kind;
    // The other element's boon this one would push out of its slot, if any.
    public readonly EquippedBoon Replaces;

    // Set when this card raises a boon the ronin already holds.
    public bool IsUpgrade => Kind != OfferKind.New;

    public BoonOffer(BoonDefinition boon, Rarity rarity, OfferKind kind, EquippedBoon replaces, int level = 1)
    {
        Boon = boon;
        Rarity = rarity;
        Kind = kind;
        Replaces = replaces;
        Level = level;
    }
}

public static class Boons
{
    public const int MaxLives = 5;

    static readonly float[] RarityWeights = { 70f, 25f, 5f };
    // Slay the Spire's hidden offset: every Common card rolled raises the Rare and Epic odds a little, until a Rare
    // or better comes up and resets it, so a long dry spell can't last. Per run (ResetPity).
    // Simulated: overall odds move from 70 / 25 / 5 to about 67 / 28 / 6, and dry spells of ten Commons halve.
    const float PityRare = 2f;
    const float PityEpic = 0.5f;
    const int MaxPity = 30;
    public static int Pity { get; private set; }
    public static void ResetPity() => Pity = 0;

    static readonly ElementInfo[] Elements =
    {
        new ElementInfo("Fire", "the ember spirits of the burned shrine lend you their fury.", new Color(1f, 0.38f, 0.2f)),
        new ElementInfo("Frost", "the physician's cold hands of the fallen palace lend you their calm.", new Color(0.35f, 0.75f, 1f)),
        new ElementInfo("Storm", "the thunder over the mountain pass lends you its speed.", new Color(0.78f, 0.45f, 1f)),
        new ElementInfo("Water", "the koi of the temple pond lends you its tide.", new Color32(0x2F, 0x7B, 0xFF, 255)),
        new ElementInfo("Steel", "the smith's cat of the Emperor's forge lends you his master's hammer.", new Color32(0xE0, 0xA8, 0x3A, 255)),
    };

    public static ElementInfo Info(Element element) => Elements[(int)element];

    // How many shards an ice statue bursts into: Frost Blade's shard count, or its Common count without it.
    public static int IceShardsAt(Rarity rarity, int level = 1) => (int)Get(BoonId.FrostBlade).ExtraAt(rarity, level);

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
        BoonKind.Infusion => InfusionColor,
        _ => RarityColor(rarity),
    };

    // Prayer-bead amber, for the Spirit Bead's screen and pickup.
    public static readonly Color BeadColor = new Color(1f, 0.72f, 0.38f);

    // Pale moon-silver, for the Spirit Thread's screen and path.
    public static readonly Color ThreadColor = new Color(0.75f, 0.95f, 0.88f);

    // Hades II's infusion pink-gold, apart from the Duo green and the Legendary orange.
    public static readonly Color InfusionColor = new Color(1f, 0.55f, 0.75f);

    // How many boons of its element an Infusion asks for: any of them, passives and capstones included.
    public const int InfusionCount = 3;

    // Polished steel: the Sword Arts belong to the blade, not to any spirit.
    public static readonly Color ArtColor = new Color(0.82f, 0.86f, 0.92f);

    // The core slot boons each capstone builds on: fire blasts, ice, lightning strikes, and the tide's soaking waves.
    static readonly BoonId[] BlastBoons = { BoonId.FlameBlade, BoonId.InfernoShuriken, BoonId.BurningRiposte, BoonId.PyreShadow };
    static readonly BoonId[] IceBoons = { BoonId.FrostBlade, BoonId.GlacialShuriken, BoonId.FrozenRiposte, BoonId.FrostShadow };
    static readonly BoonId[] StrikeBoons = { BoonId.StormBlade, BoonId.ThunderShuriken, BoonId.ThunderRiposte, BoonId.StormShadow };
    static readonly BoonId[] TideBoons = { BoonId.RiptideBlade, BoonId.TidewakeShuriken, BoonId.UndertowRiposte, BoonId.TideShadow };
    static readonly BoonId[] SteelBoons = { BoonId.AnvilBlade, BoonId.LodestoneShuriken, BoonId.HammerRiposte, BoonId.SteelShadow };

    static bool HoldsAny(RoninLoadout loadout, BoonId[] ids) => Array.Exists(ids, id => loadout.Find(id) != null);

    static bool TwoSlotsOf(RoninLoadout loadout, Element element)
    {
        int count = 0;
        foreach (BoonSlot slot in new[] { BoonSlot.Attack, BoonSlot.Special, BoonSlot.Parry, BoonSlot.Ultimate })
        {
            if (loadout.SlotElement(slot) == element) count++;
        }
        return count >= 2;
    }

    static string Plus(float v) => $"+{v:0.##}";
    static string Percent(float v) => $"+{v * 100:0}%";
    static string MinusPercent(float v) => $"-{v * 100:0}%";
    static string Seconds(float v) => $"{v:0.##}s";

    public static readonly BoonDefinition[] All =
    {
        // Fire: burn. Kills explode. A blast's reach is fixed (BoonEffects.BlastRadius: the samurai above, below and
        // beside, never the diagonals, which are Wildfire's); what rarity and levels raise is how many in reach it
        // burns, nearest first. While the reach grew instead, the burn cap of 2 left most of a bead's radius unused.
        // Flame Blade only bursts on some kills: on every one, with the slash as fast as it is, it cleared whole waves
        // by itself.
        new BoonDefinition { Id = BoonId.FlameBlade, Element = Element.Fire, Slot = BoonSlot.Attack, Name = "Flame Blade",
            Description = "A <b>Slash</b> kill may burst into flame, burning nearby samurai.",
            StatLabel = "Samurai burned:", Values = new[] { 2f, 2f, 3f }, Format = Plus,
            ExtraLabel = "Chance:", ExtraValues = new[] { 0.4f, 0.55f, 0.7f }, ExtraFormat = Percent,
            LevelStep = 1f, Whole = true, Max = 4f, ExtraLevelStep = 0.08f, ExtraMax = 0.9f },
        new BoonDefinition { Id = BoonId.InfernoShuriken, Element = Element.Fire, Slot = BoonSlot.Special, Name = "Inferno Shuriken",
            Description = "Every samurai your <b>Shuriken</b> cuts down explodes.",
            StatLabel = "Samurai burned:", Values = new[] { 2f, 3f, 4f }, Format = Plus,
            LevelStep = 1f, Whole = true, Max = 5f },
        new BoonDefinition { Id = BoonId.BurningRiposte, Element = Element.Fire, Slot = BoonSlot.Parry, Name = "Burning Riposte",
            Description = "Arrows you <b>Parry</b> come back as a fireball that explodes on impact.",
            StatLabel = "Samurai burned:", Values = new[] { 2f, 3f, 4f }, Format = Plus,
            LevelStep = 1f, Whole = true, Max = 5f },
        // Once a flat cut to the slash's cooldown; now it builds while he keeps swinging, and pays at full heat.
        new BoonDefinition { Id = BoonId.BlazingHands, Element = Element.Fire, Slot = BoonSlot.Passive, Name = "Blazing Hands",
            Description = "Keep swinging and your hands heat up: each quick <b>Slash</b> comes faster. At full heat, a slash kill sets the samurai beside it <b>Burning</b>.",
            StatLabel = "Slash cooldown at full heat:", Values = new[] { 0.12f, 0.2f, 0.28f }, Format = MinusPercent,
            LevelStep = 0.04f, Max = 0.4f },
        new BoonDefinition { Id = BoonId.Kindling, Element = Element.Fire, Slot = BoonSlot.Passive, Name = "Kindling",
            Description = "Every few swings, you hurl a great <b>Fireball</b> instead of a <b>Slash</b>. It burns through arrows and bursts in a wide blast.",
            StatLabel = "Swings per fireball:", Values = new[] { 6f, 5f, 4f }, Format = v => $"{v:0}",
            LevelStep = -1f, Whole = true, Min = 3f },

        // Water: ice. Encases, freezes, shatters, protects.
        new BoonDefinition { Id = BoonId.FrostBlade, Element = Element.Water, Slot = BoonSlot.Attack, Name = "Frost Blade",
            Description = "A <b>Slash</b> kill may encase up to two nearby samurai in ice. Cut one down and it freezes where it stands; strike the ice to <b>Shatter</b> it into shards.",
            StatLabel = "Freeze time:", Values = new[] { 1.5f, 2f, 2.5f }, Format = Seconds,
            ExtraLabel = "Shards:", ExtraValues = new[] { 1f, 2f, 3f },
            LevelStep = 0.28f, ExtraLevelStep = 1f, ExtraWhole = true, ExtraMax = 5f },
        new BoonDefinition { Id = BoonId.GlacialShuriken, Element = Element.Water, Slot = BoonSlot.Special, Name = "Glacial Shuriken",
            Description = "Your <b>Shuriken</b> freezes the whole army: no marching, no arrows.",
            StatLabel = "Freeze time:", Values = new[] { 0.6f, 0.9f, 1.2f }, Format = Seconds,
            LevelStep = 0.17f },
        new BoonDefinition { Id = BoonId.FrozenRiposte, Element = Element.Water, Slot = BoonSlot.Parry, Name = "Frozen Riposte",
            Description = "Arrows you <b>Parry</b> burst into homing ice shards that freeze what they hit.",
            StatLabel = "Ice shards:", Values = new[] { 2f, 3f, 4f }, Format = Plus,
            LevelStep = 1f, Whole = true, Max = 6f },
        // Frost's defensive passive, in the place Tidal Renewal held before it became Asagi's.
        new BoonDefinition { Id = BoonId.ColdCompress, Element = Element.Water, Slot = BoonSlot.Passive, Name = "Cold Compress",
            Description = "When a blow lands on you, the cold answers: the army <b>Freezes</b>, or the warlord for half as long.",
            StatLabel = "Freeze time:", Values = new[] { 1f, 1.5f, 2f }, Format = Seconds,
            LevelStep = 0.28f, Max = 4f },
        // Once Glacier Wall (tougher cover, which only stopped his own slashes the more); now a shell on him alone.
        new BoonDefinition { Id = BoonId.GlacierShell, Element = Element.Water, Slot = BoonSlot.Passive, Name = "Glacier Shell",
            Description = "A shell of ice takes one blow for you, then <b>Shatters</b> into homing shards. It grows back over time.",
            StatLabel = "Regrows in:", Values = new[] { 18f, 13f, 9f }, Format = Seconds,
            LevelStep = -2.8f, Min = 6f },

        // Lightning: jolt. Chains and speed.
        new BoonDefinition { Id = BoonId.StormBlade, Element = Element.Lightning, Slot = BoonSlot.Attack, Name = "Storm Blade",
            Description = "A <b>Slash</b> kill may arc lightning to a samurai further off in the ranks.",
            StatLabel = "Chain chance:", Values = new[] { 0.45f, 0.6f, 0.8f }, Format = Percent,
            LevelStep = 0.08f, Max = 0.95f },
        new BoonDefinition { Id = BoonId.ThunderShuriken, Element = Element.Lightning, Slot = BoonSlot.Special, Name = "Thunder Shuriken",
            Description = "Your <b>Shuriken</b> flies much faster, and may arc lightning into each samurai it passes near.",
            StatLabel = "Chain chance:", Values = new[] { 0.4f, 0.55f, 0.7f }, Format = Percent,
            LevelStep = 0.08f, Max = 0.95f },
        new BoonDefinition { Id = BoonId.ThunderRiposte, Element = Element.Lightning, Slot = BoonSlot.Parry, Name = "Thunder Riposte",
            Description = "A successful <b>Parry</b> strikes the samurai nearest above you with lightning.",
            StatLabel = "Strikes:", Values = new[] { 1f, 2f, 3f }, Format = Plus,
            LevelStep = 1f, Whole = true, Max = 5f },
        // Once only speed: now the static he builds on the move breaks arrows out of the air around him.
        new BoonDefinition { Id = BoonId.StormStep, Element = Element.Lightning, Slot = BoonSlot.Passive, Name = "Storm Step",
            Description = "You move faster, and while you're on the move, static leaps from you to break an enemy arrow every few seconds.",
            StatLabel = "Move speed:", Values = new[] { 0.08f, 0.12f, 0.16f }, Format = Percent,
            ExtraLabel = "Arrow broken every:", ExtraValues = new[] { 2.5f, 2f, 1.5f }, ExtraFormat = Seconds,
            LevelStep = 0.03f, Max = 0.3f, ExtraLevelStep = -0.25f },
        // Once only shorter cooldowns: now a landed parry also charges the next shuriken.
        new BoonDefinition { Id = BoonId.StaticCharge, Element = Element.Lightning, Slot = BoonSlot.Passive, Name = "Static Charge",
            Description = "Your <b>Shuriken</b> and <b>Parry</b> recharge faster, and a landed <b>Parry</b> charges your next <b>Shuriken</b> to <b>Jolt</b> everything it passes.",
            StatLabel = "Cooldowns:", Values = new[] { 0.1f, 0.15f, 0.2f }, Format = MinusPercent,
            LevelStep = 0.03f, Max = 0.35f },

        // Ultimate (Hades' Calls): one per element, held one at a time (Tide Shadow sits with Water's above). Each
        // lends the Shadow Clone its element; the clone's own hits (KillSource.Clone) set off nothing else.
        new BoonDefinition { Id = BoonId.PyreShadow, Element = Element.Fire, Slot = BoonSlot.Ultimate, Name = "Pyre Shadow",
            Description = "Your <b>Shadow Clone</b>'s kills burst into flame, and it goes out in a great blast when your ultimate ends.",
            StatLabel = "Samurai burned:", Values = new[] { 1f, 2f, 3f }, Format = Plus,
            LevelStep = 1f, Whole = true, Max = 4f },
        new BoonDefinition { Id = BoonId.FrostShadow, Element = Element.Water, Slot = BoonSlot.Ultimate, Name = "Frost Shadow",
            Description = "Unleashing your <b>Shadow Clone</b> freezes the whole army, and its kills may encase nearby samurai in ice.",
            StatLabel = "Freeze time:", Values = new[] { 1f, 1.5f, 2f }, Format = Seconds,
            LevelStep = 0.28f },
        new BoonDefinition { Id = BoonId.StormShadow, Element = Element.Lightning, Slot = BoonSlot.Ultimate, Name = "Storm Shadow",
            Description = "Your <b>Shadow Clone</b>'s kills may arc lightning, and your <b>Ultimate</b> fills faster.",
            StatLabel = "Chain chance:", Values = new[] { 0.4f, 0.55f, 0.7f }, Format = Percent,
            ExtraLabel = "Fill speed:", ExtraValues = new[] { 0.15f, 0.25f, 0.35f }, ExtraFormat = Percent,
            LevelStep = 0.08f, Max = 0.95f, ExtraLevelStep = 0.055f, ExtraMax = 0.6f },

        // Water: tide. Soaks (so every other element's effect hits harder), rolls the army back, washes arrows away,
        // and heals. Asagi's boons make few kills of their own; the Soaked status is what they're for.
        new BoonDefinition { Id = BoonId.RiptideBlade, Element = Element.Tide, Slot = BoonSlot.Attack, Name = "Riptide Blade",
            Description = "A <b>Slash</b> kill may send a wave up its column: every samurai in it is <b>Soaked</b> and its arrows wash away.",
            StatLabel = "Soak time:", Values = new[] { 4f, 4f, 4f }, Format = Seconds,
            ExtraLabel = "Chance:", ExtraValues = new[] { 0.4f, 0.55f, 0.7f }, ExtraFormat = Percent,
            LevelStep = 0.4f, Max = 8f, ExtraLevelStep = 0.08f, ExtraMax = 0.9f },
        new BoonDefinition { Id = BoonId.TidewakeShuriken, Element = Element.Tide, Slot = BoonSlot.Special, Name = "Tidewake Shuriken",
            Description = "Your <b>Shuriken</b> leaves a wake: arrows it passes wash away, and samurai it passes are <b>Soaked</b>.",
            StatLabel = "Soak time:", Values = new[] { 3f, 4f, 5f }, Format = Seconds,
            LevelStep = 0.4f, Max = 8f },
        new BoonDefinition { Id = BoonId.UndertowRiposte, Element = Element.Tide, Slot = BoonSlot.Parry, Name = "Undertow Riposte",
            Description = "A successful <b>Parry</b> sends a wave that pushes the army back up the field and <b>Soaks</b> its front rank.",
            StatLabel = "Push back:", Values = new[] { 0.3f, 0.45f, 0.6f }, Format = Plus,
            LevelStep = 0.08f, Max = 0.9f },
        new BoonDefinition { Id = BoonId.TidalRenewal, Element = Element.Tide, Slot = BoonSlot.Passive, Name = "Tidal Renewal",
            Description = "The temple pond deepens your spirit. Gain <b>Max Lives</b>, filled at once (up to 5).",
            StatLabel = "Max lives:", Values = new[] { 1f, 1f, 2f }, Format = Plus, Instant = true },
        new BoonDefinition { Id = BoonId.HealingSpring, Element = Element.Tide, Slot = BoonSlot.Passive, Name = "Healing Spring",
            Description = "Every so many samurai cut down by your own hand (or three blows on a warlord for each), a lost life is restored (or points, at full health).",
            StatLabel = "Kills per life:", Values = new[] { 45f, 35f, 25f }, Format = v => $"{v:0}",
            LevelStep = -5f, Whole = true, Min = 15f },

        new BoonDefinition { Id = BoonId.TideShadow, Element = Element.Tide, Slot = BoonSlot.Ultimate, Name = "Tide Shadow",
            Description = "Unleashing your <b>Shadow Clone</b> sends a great wave: every arrow washes away and the army is pushed back. Its kills <b>Soak</b> their neighbours.",
            StatLabel = "Soak time:", Values = new[] { 3f, 4f, 5f }, Format = Seconds,
            LevelStep = 0.4f, Max = 8f },

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

        new BoonDefinition { Id = BoonId.ScaldingSteam, Element = Element.Fire, Partner = Element.Tide, Slot = BoonSlot.Passive, Kind = BoonKind.Duo,
            Name = "Scalding Steam",
            Description = "A fire <b>Blast</b> on a <b>Soaked</b> samurai boils wider and washes away the arrows inside it.",
            StatLabel = "Blast radius:", Values = new[] { 0.5f, 0.5f, 0.5f }, Format = Plus,
            Requires = l => HoldsAny(l, BlastBoons) && HoldsAny(l, TideBoons) },
        new BoonDefinition { Id = BoonId.BlackIce, Element = Element.Water, Partner = Element.Tide, Slot = BoonSlot.Passive, Kind = BoonKind.Duo,
            Name = "Black Ice",
            Description = "A <b>Soaked</b> samurai held in ice stays held twice as long, and its statue <b>Shatters</b> into double shards.",
            StatLabel = "Ice on soaked:", Values = new[] { 2f, 2f, 2f }, Format = v => $"x{v:0}",
            Requires = l => HoldsAny(l, IceBoons) && HoldsAny(l, TideBoons) },
        new BoonDefinition { Id = BoonId.ThunderRain, Element = Element.Lightning, Partner = Element.Tide, Slot = BoonSlot.Passive, Kind = BoonKind.Duo,
            Name = "Thunder Rain",
            Description = "<b>Lightning</b> that strikes a <b>Soaked</b> samurai leaps to every other <b>Soaked</b> samurai in its rank.",
            StatLabel = "Rank struck:", Values = new[] { 1f, 1f, 1f }, Format = v => "all",
            Requires = l => HoldsAny(l, StrikeBoons) && HoldsAny(l, TideBoons) },

        // Legendaries: two core slots of one element.
        new BoonDefinition { Id = BoonId.Wildfire, Element = Element.Fire, Slot = BoonSlot.Passive, Kind = BoonKind.Legendary,
            Name = "Wildfire",
            Description = "Your fire <b>Blasts</b> grow wider, burn two more samurai and burn away the enemy arrows inside them.",
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
            StatLabel = "Hits per beat:", Values = new[] { 6f, 6f, 6f }, Format = v => $"{v:0}",
            Requires = l => TwoSlotsOf(l, Element.Lightning) },
        new BoonDefinition { Id = BoonId.DragonGate, Element = Element.Tide, Slot = BoonSlot.Passive, Kind = BoonKind.Legendary,
            Name = "Dragon Gate",
            Description = "Once a stage, a blow that would take your last life is turned aside, and the koi climbs the falls: 2 lives are healed.",
            StatLabel = "Lives healed:", Values = new[] { 2f, 2f, 2f }, Format = Plus,
            Requires = l => TwoSlotsOf(l, Element.Tide) },

        // Infusions: three boons of one element, any slot. Each deepens its element's status, so committing to an
        // element pays through its passives too.
        new BoonDefinition { Id = BoonId.EmberHeart, Element = Element.Fire, Slot = BoonSlot.Passive, Kind = BoonKind.Infusion,
            Name = "Ember Heart",
            Description = "<b>Burning</b> lasts twice as long, and a burning warlord's stance breaks twice as fast under your blows.",
            StatLabel = "Burn time:", Values = new[] { 2f, 2f, 2f }, Format = v => $"x{v:0}",
            Requires = l => l.ElementCount(Element.Fire) >= InfusionCount },
        new BoonDefinition { Id = BoonId.Hoarfrost, Element = Element.Water, Slot = BoonSlot.Passive, Kind = BoonKind.Infusion,
            Name = "Hoarfrost",
            Description = "<b>Chilled</b> lasts twice as long, and chilled samurai's arrows crawl at a third of their speed.",
            StatLabel = "Chill time:", Values = new[] { 2f, 2f, 2f }, Format = v => $"x{v:0}",
            Requires = l => l.ElementCount(Element.Water) >= InfusionCount },
        new BoonDefinition { Id = BoonId.StaticField, Element = Element.Lightning, Slot = BoonSlot.Passive, Kind = BoonKind.Infusion,
            Name = "Static Field",
            Description = "<b>Lightning</b> <b>Jolts</b> its target's whole rank, and a jolted warlord's discharge deals 2 more.",
            StatLabel = "Rank jolted:", Values = new[] { 1f, 1f, 1f }, Format = v => "all",
            Requires = l => l.ElementCount(Element.Lightning) >= InfusionCount },
        new BoonDefinition { Id = BoonId.DeepCurrent, Element = Element.Tide, Slot = BoonSlot.Passive, Kind = BoonKind.Infusion,
            Name = "Deep Current",
            Description = "<b>Soaked</b> lasts twice as long, and every tide push rolls the army back half again as far.",
            StatLabel = "Soak time:", Values = new[] { 2f, 2f, 2f }, Format = v => $"x{v:0}",
            Requires = l => l.ElementCount(Element.Tide) >= InfusionCount },

        // Sword Arts: dropped as Technique Scrolls (mostly by scouts), at most two a run. Element is unused.
        new BoonDefinition { Id = BoonId.SplitSlash, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Split Slash",
            Description = "A moment into its flight, every <b>Slash</b> splits: two smaller, weaker slashes peel off to either side.",
            StatLabel = "Side slashes:", Values = new[] { 2f, 2f, 2f }, Format = Plus },
        new BoonDefinition { Id = BoonId.PiercingSlash, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Piercing Slash",
            Description = "Your <b>Slash</b> cuts through samurai and arrows instead of stopping at the first.",
            StatLabel = "Samurai pierced:", Values = new[] { 2f, 2f, 2f }, Format = v => $"{v:0}" },
        new BoonDefinition { Id = BoonId.EchoSlash, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Echo Slash",
            Description = "A swing may send a ghostly second <b>Slash</b> that hunts down an enemy.",
            StatLabel = "Echo chance:", Values = new[] { 0.25f, 0.25f, 0.25f }, Format = Percent },
        // The shuriken's and the parry's arts, so a scroll reshapes more than the slash.
        new BoonDefinition { Id = BoonId.ScatterStar, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Scattering Star",
            Description = "Your <b>Shuriken</b> splits as it leaves your hand: two lesser blades fly off at an angle to either side.",
            StatLabel = "Side blades:", Values = new[] { 2f, 2f, 2f }, Format = Plus },
        new BoonDefinition { Id = BoonId.MirrorStance, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Mirror Stance",
            Description = "A landed <b>Parry</b> holds its stance a moment longer, turning back every arrow that reaches you.",
            StatLabel = "Stance held:", Values = new[] { 0.5f, 0.5f, 0.5f }, Format = Seconds },
        // No longer drawn from scrolls: it's the Shadow Clone ultimate now (Ronin.Ultimate), kept here for its id and icon.
        new BoonDefinition { Id = BoonId.ShadowClone, Slot = BoonSlot.Passive, Kind = BoonKind.Art, Name = "Shadow Clone",
            Description = "Your shadow fights at your side, copying your <b>Slash</b>, <b>Shuriken</b> and <b>Parry</b> at reduced strength. Nothing can touch it.",
            StatLabel = "Shadows:", Values = new[] { 1f, 1f, 1f }, Format = Plus },
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
            if (boon.Kind == BoonKind.Art && boon.Id != BoonId.ShadowClone && loadout.Find(boon.Id) == null) pool.Add(boon);
        }
        var offers = new List<BoonOffer>();
        while (offers.Count < count && pool.Count > 0)
        {
            BoonDefinition art = pool[UnityEngine.Random.Range(0, pool.Count)];
            pool.Remove(art);
            offers.Add(new BoonOffer(art, Rarity.Common, OfferKind.New, null));
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

    // Up to count held boons a Spirit Bead could raise one level, like Hades' Pom of Power. Capstones, arts and
    // instant boons have no levels, and one whose stats are already at their bounds has nothing left to gain.
    public static List<BoonOffer> DrawBead(RoninLoadout loadout, int count)
    {
        var pool = new List<EquippedBoon>();
        foreach (EquippedBoon held in loadout.Held)
        {
            if (Levels(held.Boon) && Gains(held.Boon, held.Rarity, held.Level, held.Rarity, held.Level + 1)) pool.Add(held);
        }
        var offers = new List<BoonOffer>();
        while (offers.Count < count && pool.Count > 0)
        {
            EquippedBoon held = pool[UnityEngine.Random.Range(0, pool.Count)];
            pool.Remove(held);
            offers.Add(new BoonOffer(held.Boon, held.Rarity, OfferKind.Level, null, held.Level + 1));
        }
        return offers;
    }

    // Up to count held boons a Spirit Thread could raise one rarity, like Hades II's Yarn of Ariadne. The level stays.
    public static List<BoonOffer> DrawThread(RoninLoadout loadout, int count)
    {
        var pool = new List<EquippedBoon>();
        foreach (EquippedBoon held in loadout.Held)
        {
            if (Levels(held.Boon) && held.Rarity < Rarity.Epic) pool.Add(held);
        }
        var offers = new List<BoonOffer>();
        while (offers.Count < count && pool.Count > 0)
        {
            EquippedBoon held = pool[UnityEngine.Random.Range(0, pool.Count)];
            pool.Remove(held);
            offers.Add(new BoonOffer(held.Boon, held.Rarity + 1, OfferKind.Rarity, null, held.Level));
        }
        return offers;
    }

    static bool Levels(BoonDefinition boon) => boon.Kind == BoonKind.Normal && !boon.Instant && boon.HasLevels;

    // Whether going from one rarity and level to another changes either stat.
    static bool Gains(BoonDefinition boon, Rarity fromRarity, int fromLevel, Rarity toRarity, int toLevel) =>
        boon.ValueAt(toRarity, toLevel) != boon.ValueAt(fromRarity, fromLevel)
        || boon.ExtraAt(toRarity, toLevel) != boon.ExtraAt(fromRarity, fromLevel);

    // Up to count distinct boons of one element. Like Hades, the first boon of a run is always a core slot boon.
    // ensureCapstone puts an eligible Duo or Legendary in the offer (the stage boon), and no card rolls below minRarity.
    // The boons in `avoid` (the screen just shown) are drawn only once nothing else is left, so a redraw or the next
    // screen doesn't hand back the same cards.
    public static List<BoonOffer> Draw(Element element, int count, RoninLoadout loadout, int maxLives,
        bool ensureCapstone = false, Rarity minRarity = Rarity.Common, ICollection<BoonId> banished = null,
        ICollection<BoonId> avoid = null)
    {
        var pool = new List<BoonDefinition>();
        var capstones = new List<BoonDefinition>();
        foreach (BoonDefinition boon in All)
        {
            if (boon.Kind == BoonKind.Art || !boon.Offers(element)) continue;
            if (banished != null && banished.Contains(boon.Id)) continue;
            if (boon.IsCapstone)
            {
                if (loadout.Find(boon.Id) == null && boon.Requires(loadout)) capstones.Add(boon);
                continue;
            }
            if (loadout.BoonCount == 0 && (boon.Slot == BoonSlot.Passive || boon.Slot == BoonSlot.Ultimate)) continue;
            // Held boons are raised by Spirit Beads, not offered again.
            if (loadout.Find(boon.Id) != null) continue;
            if (boon.Id == BoonId.TidalRenewal && maxLives >= MaxLives) continue;
            pool.Add(boon);
        }

        var offers = new List<BoonOffer>();
        if (ensureCapstone && capstones.Count > 0)
        {
            BoonDefinition capstone = capstones[UnityEngine.Random.Range(0, capstones.Count)];
            capstones.Remove(capstone);
            offers.Add(new BoonOffer(capstone, Rarity.Common, OfferKind.New, null));
        }
        pool.AddRange(capstones);

        while (offers.Count < count && pool.Count > 0)
        {
            List<BoonDefinition> fresh = avoid == null ? pool : pool.FindAll(b => !avoid.Contains(b.Id));
            if (fresh.Count == 0) fresh = pool;
            BoonDefinition boon = fresh[UnityEngine.Random.Range(0, fresh.Count)];
            pool.Remove(boon);

            if (boon.IsCapstone)
            {
                offers.Add(new BoonOffer(boon, Rarity.Common, OfferKind.New, null));
                continue;
            }
            EquippedBoon replaces = boon.Slot == BoonSlot.Passive ? null : loadout.InSlot(boon.Slot);
            Rarity rolled = RollRarity();
            if (rolled < minRarity) rolled = minRarity;
            // Hades' Exchange: a boon that takes another element's slot keeps its level and comes one rarity higher.
            int level = 1;
            if (replaces != null && Levels(replaces.Boon))
            {
                level = replaces.Level;
                Rarity raised = replaces.Rarity < Rarity.Epic ? replaces.Rarity + 1 : Rarity.Epic;
                if (raised > rolled) rolled = raised;
            }
            Pity = rolled == Rarity.Common ? Mathf.Min(Pity + 1, MaxPity) : 0;
            offers.Add(new BoonOffer(boon, rolled, OfferKind.New, replaces, level));
        }
        if (offers.Count == 0) offers.Add(new BoonOffer(Offering(element), Rarity.Common, OfferKind.New, null));
        return offers;
    }

    static Rarity RollRarity()
    {
        float epic = RarityWeights[2] + Pity * PityEpic;
        float rare = RarityWeights[1] + Pity * PityRare;
        float roll = UnityEngine.Random.value * (RarityWeights[0] + rare + epic);
        if (roll < epic) return Rarity.Epic;
        if (roll < epic + rare) return Rarity.Rare;
        return Rarity.Common;
    }
}
