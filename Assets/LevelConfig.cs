using UnityEngine;
using UnityEngine.Serialization;

// Tuning values for one stage of samurai. GameManager holds one of these per level.
[System.Serializable]
public class LevelConfig
{
    public int columns = 10;
    public int rows = 4;
    public float topRowY = 9f;

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
