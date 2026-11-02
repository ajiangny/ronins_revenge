using UnityEngine;

// A soldier's rank in the formation: the round's Elite, a veteran row, and the slot it marched into.
public partial class Samurai
{
    public bool IsElite { get; private set; }
    public bool IsVeteran { get; private set; }
    // The formation row it was spawned in, 0 at the top.
    public int Row { get; set; }
    // The slot's height once marched in; the most a shove lifts it to.
    public float HomeY { get; set; }

    public void MakeElite(int hitPoints, Color clan) { }

    public void MakeVeteran(int hitPoints) { }

    // Burning wears down a soldier with more than one hit point, a tick at a time.
    void UpdateBurn() { }
}
