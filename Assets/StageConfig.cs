// One stage: a few rounds of samurai armies, cleared one after another. Blockers are rebuilt only when a stage
// starts, so they wear down across its rounds.
[System.Serializable]
public class StageConfig
{
    public string name = "Stage";
    public LevelConfig[] rounds;
}
