using UnityEngine;

// Pushing single soldiers back up the field (Water's knockback, the Undertow wave).
public partial class SamuraiArmy
{
    // Will knock one soldier back up the field by `up`, capped by its slot height (Samurai.HomeY); it stays where it
    // lands, so the formation goes ragged. Returns whether it moved.
    public bool Shove(Samurai soldier, float up)
    {
        return false;
    }
}
