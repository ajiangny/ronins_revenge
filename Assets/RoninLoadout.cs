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
    readonly EquippedBoon[] slots = new EquippedBoon[3];
    readonly List<EquippedBoon> passives = new List<EquippedBoon>();
    readonly int[] elementCounts = new int[3];

    public int BoonCount { get; private set; }
    public Element? LastElement { get; private set; }
    public int WardCharges { get; private set; }

    public IReadOnlyList<EquippedBoon> Passives => passives;

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

    public bool Has(BoonId id, out float value)
    {
        EquippedBoon held = Find(id);
        value = held?.Value ?? 0f;
        return held != null;
    }

    // Takes the offered boon: upgrades it if held, otherwise fills (or replaces) its slot or joins the passives.
    public void Take(BoonOffer offer)
    {
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
        WardCharges = System.Math.Min(WardCharges, MaxWards);
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

    public Element? SlotElement(BoonSlot slot) => InSlot(slot)?.Boon.Element;

    public float AttackCooldownMultiplier => 1f - Value(BoonId.BlazingHands);
    public float MoveSpeedMultiplier => 1f + Value(BoonId.StormStep);
    public float AbilityCooldownMultiplier => 1f - Value(BoonId.StaticCharge);
    public int ExtraProjectiles => (int)Value(BoonId.InfernoFan);
    public float BlockerHealthMultiplier => 1f + Value(BoonId.GlacierWall);
    public int MaxWards => (int)Value(BoonId.FrozenRiposte);

    public void GainWard()
    {
        if (WardCharges < MaxWards) WardCharges++;
    }

    public bool TryUseWard()
    {
        if (WardCharges <= 0) return false;
        WardCharges--;
        return true;
    }
}
