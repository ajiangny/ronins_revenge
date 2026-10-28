using System.Collections.Generic;

public sealed class EquippedBoon
{
    public readonly BoonDefinition Boon;
    public Rarity Rarity;

    public EquippedBoon(BoonDefinition boon, Rarity rarity)
    {
        Boon = boon;
        Rarity = rarity;
    }

    public float Value => Boon.ValueAt(Rarity);
}

// The boons the ronin holds this run: one per core slot plus any passives, and the stats they add up to.
public sealed class RoninLoadout
{
    readonly EquippedBoon[] slots = new EquippedBoon[4];
    readonly List<EquippedBoon> passives = new List<EquippedBoon>();
    readonly int[] elementCounts = new int[3];

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
        return held?.Boon.ExtraValues != null ? held.Boon.ExtraValues[(int)held.Rarity] : 0f;
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
        // A Spirit Bead's raise is the same boon, so it doesn't count toward its element again.
        if (offer.IsUpgrade)
        {
            EquippedBoon raised = Find(offer.Boon.Id);
            if (raised != null) raised.Rarity = offer.Rarity;
            return;
        }
        // A Sword Art sits with the passives on the HUD but belongs to no element, so it doesn't steer souls.
        if (offer.Boon.Kind == BoonKind.Art)
        {
            passives.Add(new EquippedBoon(offer.Boon, offer.Rarity));
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
        }
        else if (offer.Boon.Slot == BoonSlot.Passive)
        {
            passives.Add(new EquippedBoon(offer.Boon, offer.Rarity));
        }
        else
        {
            if (offer.Replaces != null) elementCounts[(int)offer.Replaces.Boon.Element]--;
            slots[(int)offer.Boon.Slot] = new EquippedBoon(offer.Boon, offer.Rarity);
        }
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

}
