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

    [FormerlySerializedAs("alienColor")] public Color samuraiColor = new Color(0f, 0.98f, 0.6f);
}
