using UnityEngine;

// The warlord fought after a stage's last round. The save keeps these names (the encyclopedia's Foes ids), so
// never rename a value.
public enum BossKind { None, Benibana, Utsusemi, Shirogane, Kagetsune }

// One stage: a few rounds of samurai armies, cleared one after another, then its boss. Blockers are rebuilt for
// every round.
[System.Serializable]
public class StageConfig
{
    public string name = "Stage";
    public LevelConfig[] rounds;
    public BossKind boss;
    [Tooltip("Ground the stage's rounds are fought on. Empty keeps the moonlit courtyard.")]
    public Backdrop.Map roundsMap;
    [Tooltip("Ground the ronin walks into for the boss fight. Empty keeps the stage's own ground.")]
    public Backdrop.Map bossMap;
}
