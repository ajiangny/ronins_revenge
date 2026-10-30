using System.Collections.Generic;

public sealed class EquippedBoon
{
    public readonly BoonDefinition Boon;
    public Rarity Rarity;
    // Raised by Spirit Beads; kept when a Thread raises the rarity.
    public int Level;

    public EquippedBoon(BoonDefinition boon, Rarity rarity, int level = 1)
    {
        Boon = boon;
        Rarity = rarity;
        Level = level;
    }

    public float Value => Boon.ValueAt(Rarity, Level);
    public float Extra => Boon.ExtraAt(Rarity, Level);
}

// The boons the ronin holds this run: one per core slot plus any passives, and the stats they add up to.
public sealed class RoninLoadout
{
    readonly EquippedBoon[] slots = new EquippedBoon[4];
    readonly List<EquippedBoon> passives = new List<EquippedBoon>();
    readonly int[] elementCounts = new int[System.Enum.GetValues(typeof(Element)).Length];

    public int BoonCount { get; private set; }
    public Element? LastElement { get; private set; }

    public IReadOnlyList<EquippedBoon> Passives => passives;

    // Every boon held, slots first.
    public IEnumerable<EquippedBoon> Held
    {
        get
        {
            foreach (EquippedBoon held in slots)
            {
                if (held != null) yield return held;
            }
            foreach (EquippedBoon held in passives) yield return held;
        }
    }

    public EquippedBoon InSlot(BoonSlot slot) => slot == BoonSlot.Passive ? null : slots[(int)slot];

    public EquippedBoon Find(BoonId id)
    {
        foreach (EquippedBoon held in slots)
        {
            if (held != null && held.Boon.Id == id) return held;
        }
        return passives.Find(p => p.Boon.Id == id);
    }

    // The boon's value at its current rarity, or 0 if the ronin doesn't have it.
    public float Value(BoonId id) => Find(id)?.Value ?? 0f;

    // A held boon's second stat (Storm Shadow's fill speed), or 0.
    public float Extra(BoonId id)
    {
        EquippedBoon held = Find(id);
        return held?.Extra ?? 0f;
    }

    public bool Has(BoonId id, out float value)
    {
        EquippedBoon held = Find(id);
        value = held?.Value ?? 0f;
        return held != null;
    }

    // Takes the offered boon: raises it if it's a Bead's upgrade, otherwise fills (or replaces) its slot or joins the passives.
    public void Take(BoonOffer offer)
    {
        // An Offering is a heal, not a boon: it doesn't count toward the ronin's elements.
        if (offer.Boon.Kind == BoonKind.Offering) return;
        // A Bead's level or a Thread's rarity is the same boon, so it doesn't count toward its element again.
        if (offer.IsUpgrade)
        {
            EquippedBoon raised = Find(offer.Boon.Id);
            if (raised == null) return;
            raised.Rarity = offer.Rarity;
            raised.Level = offer.Level;
            return;
        }
        // A Sword Art sits with the passives on the HUD but belongs to no element, so it doesn't steer souls.
        if (offer.Boon.Kind == BoonKind.Art)
        {
            passives.Add(new EquippedBoon(offer.Boon, offer.Rarity, offer.Level));
            return;
        }
        BoonCount++;
        elementCounts[(int)offer.Boon.Element]++;
        LastElement = offer.Boon.Element;
        if (offer.Boon.Instant) return;

        EquippedBoon held = Find(offer.Boon.Id);
        if (held != null)
        {
            held.Rarity = offer.Rarity;
            held.Level = offer.Level;
        }
        else if (offer.Boon.Slot == BoonSlot.Passive)
        {
            passives.Add(new EquippedBoon(offer.Boon, offer.Rarity, offer.Level));
        }
        else
        {
            if (offer.Replaces != null) elementCounts[(int)offer.Replaces.Boon.Element]--;
            slots[(int)offer.Boon.Slot] = new EquippedBoon(offer.Boon, offer.Rarity, offer.Level);
        }
    }

    // A copy to try an offer on (the cards' "Opens" hint) without touching the real loadout.
    public RoninLoadout Copy()
    {
        var copy = new RoninLoadout { BoonCount = BoonCount, LastElement = LastElement, DragonGateUsed = DragonGateUsed };
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) copy.slots[i] = new EquippedBoon(slots[i].Boon, slots[i].Rarity, slots[i].Level);
        }
        foreach (EquippedBoon held in passives) copy.passives.Add(new EquippedBoon(held.Boon, held.Rarity, held.Level));
        elementCounts.CopyTo(copy.elementCounts, 0);
        return copy;
    }

    // The element the ronin has invested in most; ties go to the latest pick. Null before any boon.
    public Element? DominantElement
    {
        get
        {
            if (LastElement == null) return null;
            Element best = LastElement.Value;
            for (int i = 0; i < elementCounts.Length; i++)
            {
                if (elementCounts[i] > elementCounts[(int)best]) best = (Element)i;
            }
            return best;
        }
    }

    public int ElementCount(Element element) => elementCounts[(int)element];

    public Element? SlotElement(BoonSlot slot) => InSlot(slot)?.Boon.Element;

    public float AttackCooldownMultiplier => 1f - Value(BoonId.BlazingHands);
    public float MoveSpeedMultiplier => 1f + Value(BoonId.StormStep);
    public float AbilityCooldownMultiplier => 1f - Value(BoonId.StaticCharge);
    // Still Water: freezes last longer and the army's march quickens half as much as it thins.
    public float FreezeMultiplier => 1f + Value(BoonId.StillWater);
    public float MarchSpeedUpScale => Find(BoonId.StillWater) != null ? 0.5f : 1f;

    // Dragon Gate turns one lethal blow aside a stage; BeginStage opens the gate again.
    public bool DragonGateUsed { get; set; }

    public void BeginStage()
    {
        DragonGateUsed = false;
    }
}
