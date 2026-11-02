using UnityEngine;
using UnityEngine.Serialization;

// Tuning values for one round of samurai. Each StageConfig holds one of these per round.
[System.Serializable]
public class LevelConfig
{
    public int columns = 10;
    public int rows = 4;
    public float topRowY = 9f;
    [Tooltip("Kind of samurai per row from the top; rows past the end reuse the last entry. Empty means all Ashigaru.")]
    public SamuraiKind[] rowKinds;
    [Tooltip("Scouts fly across the top of the screen during this round.")]
    public bool scouts;
    [Tooltip("Average seconds between samurai breaking formation to swoop at the ronin. 0 means no divers.")]
    public float diveInterval;

    [Tooltip("Sideways speed (units/sec) with the full army alive.")]
    public float baseSpeed = 2.5f;
    [Tooltip("Speed multiplier reached when only one samurai is left.")]
    public float maxSpeedMultiplier = 4f;

    [Tooltip("Average seconds between samurai arrows.")]
    public float fireInterval = 0.9f;
    [Tooltip("Chance that a shot comes from the column above the ronin.")]
    [Range(0, 1)] public float aimedShotChance = 0.25f;
    [FormerlySerializedAs("bulletSpeed")] public float arrowSpeed = 7f;

    [Tooltip("Extra army shots: this many shooters from different columns loose together. 1 means one at a time.")]
    public int volley = 1;
    [Tooltip("Chance that an army arrow flies faster than arrowSpeed.")]
    [Range(0, 1)] public float fastArrowChance;
    [Tooltip("Chance that an army arrow is shielded and takes an extra cut to break.")]
    [Range(0, 1)] public float toughArrowChance;
    [Tooltip("Chance that an army arrow is guarded: it can't be cut, only parried, dodged or stopped by cover.")]
    [Range(0, 1)] public float guardedArrowChance;

    [Tooltip("How the formation is laid out.")]
    public FormationShape shape = FormationShape.Grid;
    [Tooltip("Squads shape: how many separate blocks the columns split into.")]
    public int squads = 1;
    [Tooltip("Squads shape: extra gap between blocks, in column spacings.")]
    public float squadGap = 1f;
    [Tooltip("Hit points per row from the top; 0 or rows past the end keep the kind's own.")]
    public int[] rowHealth;
    [Tooltip("Hit points of the round's one Elite. 0 means no Elite.")]
    public int eliteHealth;

    [FormerlySerializedAs("alienColor")] public Color samuraiColor = new Color(0f, 0.98f, 0.6f);

    // A copy to tune for one round (the oaths) without touching the stage's own; rowKinds and rowHealth are shared, read-only.
    public LevelConfig Clone() => (LevelConfig)MemberwiseClone();
}

// How SamuraiArmy lays out a round's slots (SamuraiArmy.Formation.cs).
public enum FormationShape { Grid, Staggered, Squads, Wedge }
