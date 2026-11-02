using UnityEngine;

// The round's one Elite (LevelConfig.eliteHealth): picked at spawn, firing its kind's shot on its own timer.
public partial class SamuraiArmy
{
    Samurai elite;

    public Samurai Elite => elite;

    // Makes one soldier the round's Elite.
    void Promote(LevelConfig c) { }

    // The Elite's own shot timer, run from FixedUpdate.
    void UpdateElite() { }
}
