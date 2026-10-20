using UnityEngine;

// Tuning values for one wave of aliens. GameManager holds one of these per level.
[System.Serializable]
public class LevelConfig
{
    public int columns = 10;
    public int rows = 4;
    public float topRowY = 9f;

    [Tooltip("Sideways speed (units/sec) with the full fleet alive.")]
    public float baseSpeed = 2.5f;
    [Tooltip("Speed multiplier reached when only one alien is left.")]
    public float maxSpeedMultiplier = 4f;

    [Tooltip("Average seconds between alien shots.")]
    public float fireInterval = 0.9f;
    [Tooltip("Chance that a shot comes from the column above the player.")]
    [Range(0, 1)] public float aimedShotChance = 0.25f;
    public float bulletSpeed = 7f;

    public Color alienColor = new Color(0f, 0.98f, 0.6f);
}
