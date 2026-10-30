using UnityEngine;

// The time bonus: a clock that runs only while the ronin fights, a bonus for each round and each stage cleared under
// par, banked through the run and added to the score when it ends (any way it ends).
public partial class GameManager
{
    // Seconds of play this round (or boss fight), this stage and this run.
    float roundTime;
    float stageTime;
    float runTime;
    int roundsThisStage;
    // Banked bonus, not yet in the score; the totals are for the end screen.
    int timeBonus;
    int roundBonusTotal;
    int stageBonusTotal;

    float SegmentPar => activeBoss != null ? bossParSeconds : roundParSeconds;

    // Banners, boon screens, talk scenes and pauses aren't Playing, so only the fighting is timed. Poguru's drills
    // aren't a race. Utsusemi's Stopped Hour holds the clock with the world: Oni can't act through it.
    void TickClock()
    {
        if (tutorial.Active || ronin.TimeHeld) return;
        roundTime += Time.deltaTime;
        stageTime += Time.deltaTime;
        runTime += Time.deltaTime;
        ui.SetClock(roundTime, SegmentPar);
    }

    void StartRunClock()
    {
        runTime = 0f;
        timeBonus = roundBonusTotal = stageBonusTotal = 0;
        StartStageClock();
    }

    void StartStageClock()
    {
        stageTime = 0f;
        roundsThisStage = 0;
    }

    void StartRoundClock()
    {
        roundTime = 0f;
        if (!tutorial.Active) ui.SetClock(0f, SegmentPar);
    }

    // Whole seconds under par, so a clear just inside it still pays something.
    int Bonus(float time, float par, int perSecond) =>
        Mathf.CeilToInt(Mathf.Max(0f, par - time)) * perSecond * (stageIndex + 1);

    void PayRoundBonus()
    {
        if (tutorial.Active) return;
        roundsThisStage++;
        int bonus = Bonus(roundTime, roundParSeconds, roundBonusPerSecond);
        timeBonus += bonus;
        roundBonusTotal += bonus;
        RunLog.Note($"round time {Clock(roundTime)}  bonus +{bonus}");
        ui.ShowClockResult("ROUND", roundTime, bonus);
    }

    // The stage's par grows with the rounds fought in it, so a longer road through portals isn't punished.
    void PayStageBonus()
    {
        float par = roundsThisStage * roundParSeconds + (Stage.boss != BossKind.None ? bossParSeconds : 0f);
        int bonus = Bonus(stageTime, par, stageBonusPerSecond);
        timeBonus += bonus;
        stageBonusTotal += bonus;
        RunLog.Note($"stage time {Clock(stageTime)}  bonus +{bonus}");
        ui.ShowClockResult("STAGE", stageTime, bonus);
    }

    // At the run's end, before the high score is saved. Paid once.
    void BankTimeBonus()
    {
        if (timeBonus <= 0) return;
        score += timeBonus;
        timeBonus = 0;
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
    }

    static string Clock(float seconds)
    {
        int whole = Mathf.FloorToInt(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }
}
