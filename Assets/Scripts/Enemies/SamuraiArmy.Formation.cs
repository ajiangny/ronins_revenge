using UnityEngine;

// Where each soldier's slot is in the round's formation (LevelConfig.shape).
public partial class SamuraiArmy
{
    // Old scene data saved before the Squads shape has 0 here, which means one block with no gap.
    static int SquadCount(LevelConfig c) => Mathf.Clamp(c.squads <= 0 ? 1 : c.squads, 1, Mathf.Max(1, c.columns));
    static float SquadGap(LevelConfig c) => c.squadGap <= 0f ? 1f : c.squadGap;

    // How many column spacings the formation spans from its leftmost slot to its rightmost.
    static float Span(LevelConfig c)
    {
        float span = c.columns - 1;
        // Staggered rows reach a quarter slot further out each side.
        if (c.shape == FormationShape.Staggered) span += 0.5f;
        else if (c.shape == FormationShape.Squads) span += (SquadCount(c) - 1) * SquadGap(c);
        return span;
    }

    // Sideways gap between columns: the formation fills 60% of the view, never wider apart than maxSpacingX.
    float SpacingFor(LevelConfig c)
    {
        return Mathf.Min(maxSpacingX, 0.6f * 2f * HalfWidth() / Mathf.Max(1f, Span(c)));
    }

    // Whether this column and row hold a soldier at all.
    bool HasSlot(LevelConfig c, int col, int row)
    {
        // A wedge is widest at the top and loses half a column each side per row down, so its point faces the ronin.
        if (c.shape == FormationShape.Wedge)
        {
            float half = (c.columns - 1) / 2f;
            return Mathf.Abs(col - half) <= half - row * 0.5f;
        }
        return true;
    }

    // A slot's x, centred on the view.
    float SlotX(LevelConfig c, int col, int row, float spacing)
    {
        switch (c.shape)
        {
            // Every other row shifts half a slot, so the gaps of one rank sit over the soldiers of the next.
            case FormationShape.Staggered:
                return ((col - (c.columns - 1) / 2f) + (row % 2 == 0 ? -0.25f : 0.25f)) * spacing;
            // The columns split into blocks with open lanes between them.
            case FormationShape.Squads:
                int k = SquadCount(c);
                int squad = col * k / Mathf.Max(1, c.columns);
                float u = col + squad * SquadGap(c);
                return (u - Span(c) / 2f) * spacing;
            default:
                return (col - (c.columns - 1) / 2f) * spacing;
        }
    }
}
