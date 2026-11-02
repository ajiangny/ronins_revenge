using UnityEngine;

// Where each soldier's slot is in the round's formation (LevelConfig.shape).
public partial class SamuraiArmy
{
    // Sideways gap between columns: the formation fills 60% of the view, never wider apart than maxSpacingX.
    float SpacingFor(LevelConfig c)
    {
        return Mathf.Min(maxSpacingX, 0.6f * 2f * HalfWidth() / Mathf.Max(1, c.columns - 1));
    }

    // Whether this column and row hold a soldier at all.
    bool HasSlot(LevelConfig c, int col, int row)
    {
        return true;
    }

    // A slot's x, centred on the view.
    float SlotX(LevelConfig c, int col, int row, float spacing)
    {
        return (col - (c.columns - 1) / 2f) * spacing;
    }
}
