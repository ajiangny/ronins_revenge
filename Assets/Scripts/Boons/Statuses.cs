using UnityEngine;

// The five elements' statuses, one each like Hades' gods' curses, so whichever slot a boon sits in it carries its
// element's mark. Most samurai fall to one hit, so the army's statuses work on its arrows rather than its health,
// and none of them takes a samurai out of the fight (a samurai that can't shoot is as good as dead): Burning ones'
// shots go wild, Chilled ones loose slow arrows, a Jolted one's next shot fizzles, and Soaked ones (Asagi's) take
// one more from every boon effect. On a warlord each gives an edge instead: Burning breaks her stance faster,
// Chilled lengthens her next freeze, Jolted adds a point to the ronin's next blow. Cracked (Oliver's) gives no edge on
// a foe at all: it only pays in cover, as scrap shed by Oni's own hit.
// The Infusions (Ember Heart, Hoarfrost, Static Field, Deep Current, Tamahagane) deepen them once an element is held three times.
public static class Statuses
{
    public const float BurnSeconds = 3f;
    public const float ChillSeconds = 4f;
    public const float JoltSeconds = 4f;
    public const float CrackSeconds = 4f;
    // A cracked warlord's next own hit knocks loose this much scrap, at most once per this many seconds, however many
    // warlords stand: the payout is cover, and there is one wall.
    public const float BossCrackCooldown = 5f;
    public const int BossCrackScrap = 3;

    // A Burning samurai's shots fly off up to this many degrees from straight down.
    public const float WildAngle = 35f;
    // A Chilled samurai's arrows fly at this share of their speed; Hoarfrost slows them further.
    const float ChillArrowSpeed = 0.6f;
    const float HoarfrostArrowSpeed = 0.35f;
    // A Burning warlord's stance fills this much faster under the ronin's blows; Ember Heart, faster still.
    const float BurnStanceBoost = 1.5f;
    const float EmberHeartStanceBoost = 2f;
    // A Chilled warlord's next freeze lasts this much longer.
    public const float ChillFreeze = 1.5f;
    // A Jolted warlord's discharge adds this much to the ronin's next blow; Static Field, more.
    const int JoltBonus = 1;
    const int StaticFieldJoltBonus = 2;
    // Samurai this close beside a struck one, in its rank, are jolted (Static Field jolts the whole rank).
    public const float JoltReach = 2.2f;

    public static readonly Color BurnColor = new Color(1f, 0.5f, 0.2f);
    public static readonly Color ChillColor = new Color(0.75f, 0.92f, 1f);
    public static readonly Color JoltColor = new Color(0.78f, 0.45f, 1f);
    // Dull iron with bright seams: never flat gold on the field, where bronze means a parry that opens a way.
    public static readonly Color CrackColor = new Color32(0x6F, 0x6A, 0x63, 255);
    public static readonly Color SeamColor = new Color32(0xFF, 0xE0, 0x8A, 255);

    // Set from the loadout whenever it changes (GameManager), like Samurai.BlackIce.
    public static float BurnScale { get; private set; } = 1f;
    public static float ChillScale { get; private set; } = 1f;
    public static float SoakScale { get; private set; } = 1f;
    public static float CrackScale { get; private set; } = 1f;
    public static int BossCrackScrapNow { get; private set; } = BossCrackScrap;
    public static float PushScale { get; private set; } = 1f;
    public static float ChillArrow { get; private set; } = ChillArrowSpeed;
    public static float BossBurnStance { get; private set; } = BurnStanceBoost;
    public static int BossJoltBonus { get; private set; } = JoltBonus;
    public static bool JoltWholeRank { get; private set; }
    public static bool SpearBreaker { get; private set; }

    public static void From(RoninLoadout loadout)
    {
        bool ember = loadout.Find(BoonId.EmberHeart) != null;
        bool hoarfrost = loadout.Find(BoonId.Hoarfrost) != null;
        bool field = loadout.Find(BoonId.StaticField) != null;
        bool current = loadout.Find(BoonId.DeepCurrent) != null;
        BurnScale = ember ? loadout.Value(BoonId.EmberHeart) : 1f;
        BossBurnStance = ember ? EmberHeartStanceBoost : BurnStanceBoost;
        ChillScale = hoarfrost ? loadout.Value(BoonId.Hoarfrost) : 1f;
        ChillArrow = hoarfrost ? HoarfrostArrowSpeed : ChillArrowSpeed;
        JoltWholeRank = field;
        BossJoltBonus = field ? StaticFieldJoltBonus : JoltBonus;
        SoakScale = current ? loadout.Value(BoonId.DeepCurrent) : 1f;
        PushScale = current ? 1.5f : 1f;
        SpearBreaker = loadout.Find(BoonId.SpearBreaker) != null;
    }
}
