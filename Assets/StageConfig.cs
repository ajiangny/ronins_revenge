// The warlord fought after a stage's last round.
public enum BossKind { None, Benibana }

// One stage: a few rounds of samurai armies, cleared one after another, then its boss. Blockers are rebuilt only
// when a stage starts, so they wear down across its rounds.
[System.Serializable]
public class StageConfig
{
    public string name = "Stage";
    public LevelConfig[] rounds;
    public BossKind boss;
}
