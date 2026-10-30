using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// A run's fights: starting a game or a jump, each round's army, scouts, the Spirit Notes earned and the
// combat callbacks the ronin and army raise.
public partial class GameManager
{
    // One scout at a time, every 15-25 seconds in rounds that have them.
    void UpdateScouts()
    {
        if (!Round.scouts || activeScout != null || activeBoss != null || Time.time < nextScoutTime) return;
        nextScoutTime = Time.time + Random.Range(15f, 25f);
        bool kiteArt = army.ScoutArt != null;
        CharacterArt scoutArt = kiteArt ? army.ScoutArt : army.samuraiByRow[army.samuraiByRow.Length - 1];
        activeScout = Scout.Spawn(scoutArt, kiteArt, Round.topRowY + 1.4f, OnScoutKilled);
        StartCoroutine(PoguruBark(DialogueEvent.PoguruScout, 0.35f));
    }

    void OnScoutKilled(Scout scout)
    {
        Earn(Upgrades.PerScout);
        score += scout.Points * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        if (state == State.Playing && Random.value < scoutSoulChance) MaybeDropSoul(scout.transform.position);
        // Scouts are where scrolls usually come from; it lands beside the soul rather than on it.
        if (state == State.Playing) MaybeDropScroll(scout.transform.position + Vector3.right * 0.9f, scrollChanceFromScout);
        if (Random.value < silkChanceFromScout) GainRelic(Relic.Silk, 1, scout.transform.position);
    }

    // A seal or silk goes into the save at once, like notes, so a death keeps it.
    void GainRelic(Relic relic, int count, Vector3 where)
    {
        Upgrades.AddRelic(relic, count);
        relicsThisRun[(int)relic] += count;
        RunLog.Note($"+{count} {Upgrades.RelicName(relic, count)}");
        // It forms where it was won and flies into him, like a round's reward, only smaller.
        RewardSpirit.Spawn(where, relic == Relic.Seal ? Looped(sealFrames, ProceduralSprites.Seal) : Looped(silkFrames, ProceduralSprites.Silk),
            Upgrades.RelicColour(relic), ronin, 1f);
    }

    // Drops into a stage's boss fight, or its rounds, on that fight's own map. A boss jump counts as the last round.
    void StartAt(int stage, bool boss)
    {
        tutorial.Abort();
        stageIndex = stage;
        boss &= Stage.boss != BossKind.None;
        roundIndex = boss ? BossPortalAfter - 1 : 0;
        ShowMap(boss ? BossMap : Stage.roundsMap);
        StartCoroutine(boss ? BeginBoss() : BeginRound());
    }

    // Swaps the ground and builds that map's fresh cover.
    void ShowMap(Backdrop.Map map)
    {
        if (backdrop != null) backdrop.Show(map);
        blockers.Use(map?.blockers, map?.blockerPattern);
        blockers.Rebuild();
    }

    StageConfig Stage => stages[stageIndex];
    LevelConfig Round => roundIndex < Stage.rounds.Length ? Stage.rounds[roundIndex] : ExtraRound();

    // The round as the oaths sworn make it: a faster march and more, faster arrows. A copy, so the stage keeps its own.
    static LevelConfig Sworn(LevelConfig round)
    {
        int march = Oaths.Rank(OathId.HastenedMarch), arrows = Oaths.Rank(OathId.RainOfArrows);
        if (march == 0 && arrows == 0) return round;
        var sworn = round.Clone();
        sworn.baseSpeed *= 1f + march * Oaths.MarchPerRank;
        sworn.fireInterval *= 1f - arrows * Oaths.ArrowIntervalPerRank;
        sworn.arrowSpeed *= 1f + arrows * Oaths.ArrowSpeedPerRank;
        return sworn;
    }

    // A round past the stage's configured ones: its last round again, marching faster and shooting more often, with
    // a commander in the back rank and, from the second extra round, one more rank (up to six).
    LevelConfig ExtraRound()
    {
        int key = stageIndex * 100 + roundIndex;
        if (extraRound != null && extraRoundFor == key) return extraRound;
        LevelConfig last = Stage.rounds[Stage.rounds.Length - 1];
        int extra = roundIndex - Stage.rounds.Length + 1;
        int rows = Mathf.Min(last.rows + (extra >= 2 ? 1 : 0), 6);
        var kinds = new SamuraiKind[rows];
        for (int i = 0; i < rows; i++)
        {
            SamuraiKind[] from = last.rowKinds;
            kinds[i] = from == null || from.Length == 0 ? SamuraiKind.Ashigaru : from[Mathf.Min(i, from.Length - 1)];
        }
        kinds[0] = SamuraiKind.Taisho;
        extraRound = new LevelConfig
        {
            columns = last.columns,
            rows = rows,
            topRowY = last.topRowY,
            rowKinds = kinds,
            scouts = true,
            diveInterval = last.diveInterval > 0f ? last.diveInterval * Mathf.Pow(0.8f, extra) : 6f,
            baseSpeed = last.baseSpeed * (1f + extraRoundSpeedUp * extra),
            maxSpeedMultiplier = last.maxSpeedMultiplier,
            fireInterval = last.fireInterval * Mathf.Pow(1f - extraRoundFireCut, extra),
            aimedShotChance = Mathf.Min(0.7f, last.aimedShotChance + 0.05f * extra),
            arrowSpeed = last.arrowSpeed,
            samuraiColor = last.samuraiColor,
        };
        extraRoundFor = key;
        return extraRound;
    }
    // The boss waits on its own ground; without art for it the duel is fought where the rounds were.
    Backdrop.Map BossMap => Stage.bossMap?.ground != null ? Stage.bossMap : Stage.roundsMap;

    void StartGame()
    {
        Upgrades.RefundRetired();
        ronin.LearnSongs();
        ronin.Skin = CarriedBlade;
        ronin.CurrentUltimate = (Ronin.Ultimate)Save.Data.ultimate;
        lives = maxLives = Mathf.Clamp(startingLives + Upgrades.Rank(UpgradeId.IronWill) - Oaths.Rank(OathId.FrailBody), 1, Boons.MaxLives);
        Boss.HealthScale = 1f + Oaths.Rank(OathId.IronGenerals) * Oaths.BossHealthPerRank;
        noteFraction = 0f;
        notesThisRun = 0;
        System.Array.Clear(relicsThisRun, 0, relicsThisRun.Length);
        dawnUsed = false;
        lowLifeWarned = false;
        redrawsLeft = Upgrades.Rank(UpgradeId.FatesThread);
        banishesLeft = Upgrades.Rank(UpgradeId.BanishingBell);
        banished.Clear();
        metThisRun.Clear();
        score = 0;
        highScoreAtStart = highScore;
        stageIndex = 0;
        roundIndex = 0;
        ResetLoadout();
        // Asked before the run is counted: a new journey's first run has none behind it.
        bool offerTutorial = Tutorial.ShouldOffer(Save.Data);
        tutorial.Arm(false);
        Save.BeginRun();
        RunLog.Enabled = DevToolsOn;
        var songs = new List<string>();
        foreach (Song song in Upgrades.All)
        {
            if (Upgrades.Rank(song.Id) > 0) songs.Add($"{song.Name} {Upgrades.Rank(song.Id)}");
        }
        RunLog.BeginRun($"Heat {Oaths.Heat}  lives {maxLives}  {CarriedBlade}{(godMode ? "  GOD MODE" : "")}  songs: {(songs.Count > 0 ? string.Join(", ", songs) : "none")}");
        ui.SetLives(lives, maxLives);
        ui.SetScore(score);
        if (devJumpStage >= 0)
        {
            int stage = Mathf.Min(devJumpStage, stages.Length - 1);
            devJumpStage = -1;
            StartCoroutine(DevJumpTo(stage, devJumpBoss));
            return;
        }
        tutorial.Arm(offerTutorial);
        StartCoroutine(BeginRound());
    }

    // A jump starts from this scene's Start, before the backdrop has built its ground, so it waits a frame
    // before swapping maps.
    IEnumerator DevJumpTo(int stage, bool boss)
    {
        yield return null;
        StartAt(stage, boss);
    }

    // Stage numbers in Roman numerals, to suit the Roman-capital titles.
    static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", _ => n.ToString() };

    IEnumerator BeginRound()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ClearProjectiles();
        // A new journey's first round opens on Poguru's offer, before the army arrives.
        if (stageIndex == 0 && roundIndex == 0 && tutorial.Armed) yield return tutorial.Offer();
        // Taken, five soldiers find Oni first: his practice before the army they run to fetch. The cover isn't up yet
        // (it's built just below), so nothing stands between his blows and them.
        if (stageIndex == 0 && roundIndex == 0 && tutorial.Active)
        {
            blockers.Clear();
            yield return tutorial.Drills();
        }
        // Every round is fought on fresh ground further along the road, so it brings fresh cover.
        blockers.Rebuild();
        // A stage's first round always ends in a soul; later rounds carry what their path showed.
        if (roundIndex == 0)
        {
            killSoulsThisStage = 0;
            killHeartThisStage = false;
            loadout.BeginStage();
            pendingReward = PathReward.Soul;
            pendingElement = PickSoulElement();
        }

        LevelConfig round = Sworn(Round);
        army.SpawnStage(round, ronin.transform, ronin.TopY, blockers.TopY);
        ui.SetStage(stageIndex + 1, stages.Length, roundIndex + 1, 0);
        string body = roundIndex >= Stage.rounds.Length + 1 ? "Their finest march."
            : roundIndex >= Stage.rounds.Length ? "The dead regroup."
            : roundIndex > 0 ? "Another army of the dead marches in."
            : stageIndex == 0 ? "Get ready!"
            : stageIndex == 1 ? "Bigger, faster, angrier armies."
            : "The dead march too.";
        // The round's reward shows as its icon under the line, or in words while it has no art.
        Sprite[] rewardIcon = RewardIcon(pendingReward, pendingElement);
        Color rewardColour = RewardColour(pendingReward, pendingElement);
        if (rewardIcon == null || rewardIcon.Length == 0)
            body += $"\n<color=#{ColorUtility.ToHtmlStringRGB(rewardColour)}>Reward: {RewardLabel(pendingReward, pendingElement)}</color>";
        ui.ShowBanner($"Stage {Roman(stageIndex + 1)}  ·  Round {roundIndex + 1}", Stage.name, body, round.samuraiColor, rewardIcon,
            "Reward:", rewardColour);
        Sfx.Play(Sfx.StageStart);
        if (sfxLibrary != null) Music.Play(sfxLibrary.RoundsMusic(Stage.boss), sfxLibrary.musicVolume);

        // The new army marches in under the banner; the round starts once both are done.
        float introEnds = Time.time + (roundIndex == 0 ? levelIntroTime : roundIntroTime);
        yield return army.MarchIn(armyMarchInTime);
        if (Time.time < introEnds) yield return new WaitForSeconds(introEnds - Time.time);

        ui.HideBanner();
        // The tutorial's lesson as the first army stands ready, and its goodbye as the second does.
        if (tutorial.Active && stageIndex == 0 && roundIndex == 0) yield return tutorial.Backup();
        else if (tutorial.Active && stageIndex == 0 && roundIndex == 1) yield return tutorial.Finish();
        state = State.Playing;
        ronin.ControlsEnabled = true;
        army.Advance();
        tutorial.RoundStarted();
        // His first look at a new stage comes before any round-start bark.
        if (!tutorial.Active && Dialogue.CanPick("Poguru", DialogueEvent.PoguruStage, BarkContext())) StartCoroutine(PoguruBark(DialogueEvent.PoguruStage));
        else if (!tutorial.Active) StartCoroutine(PoguruBark(DialogueEvent.PoguruRound, 0.2f));
        RunLog.BeginSegment($"S{stageIndex + 1} R{roundIndex + 1} ({RewardLabel(pendingReward, pendingElement)})");
        nextScoutTime = Time.time + Random.Range(6f, 10f);
    }

    // Spirit Notes go straight into the save, so a run that ends any way at all keeps them.
    // Heat raises them; the fractions carry over so single kills still add up.
    void Earn(int notes)
    {
        noteFraction += notes * Oaths.NoteMultiplier;
        int whole = Mathf.FloorToInt(noteFraction);
        noteFraction -= whole;
        notesThisRun += whole;
        Upgrades.AddNotes(whole);
    }

    void OnSamuraiKilled(Samurai samurai, KillSource source)
    {
        RunLog.Kill(source);
        Earn(Upgrades.PerSamurai);
        score += samurai.Points * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);

        if (state != State.Playing) return;
        // Boon effects bring their own blasts and bolts; the ronin's own hits spark in his projectile's colour.
        if (source != KillSource.Effect)
        {
            Color spark = source == KillSource.Shuriken ? ronin.ShurikenGlow : source == KillSource.Deflected ? ronin.ParryColor : ronin.AttackGlow;
            Fx.Play(Fx.Impact, samurai.transform.position, Color.Lerp(Color.white, spark, 0.6f), source == KillSource.Shuriken ? 1.4f : 1f);
        }
        effects.OnKill(samurai, source);
        // A boss's levy drops no souls or scrolls: a boon screen mid-fight would stop the duel.
        if (activeBoss != null) return;
        if (killSoulsThisStage < killSoulsPerStage && Random.value < killSoulChance && activeSoul == null && army.Remaining > 0)
        {
            killSoulsThisStage++;
            MaybeDropSoul(samurai.transform.position);
        }
        MaybeDropScroll(samurai.transform.position, scrollChancePerKill);
        // Rarely a soldier drops a heart, but only one he can use (Poguru promises as much in the tutorial).
        if (source != KillSource.Effect && !killHeartThisStage && !tutorial.Active && lives < maxLives && Random.value < killHeartChance)
        {
            killHeartThisStage = true;
            Heart.Spawn(samurai.transform.position, ui.LifeIcon, ronin, CanHeal, OnHeartCollected);
        }
    }

    void OnShurikenThrown(Vector3 muzzle, Projectile shuriken)
    {
        if (state == State.Playing) effects.OnShurikenThrown(muzzle, shuriken);
    }

    void OnParried()
    {
        if (state != State.Playing) return;
        effects.OnParried();
        // Thunder Riposte can strike down the army's last samurai, which already moved on to the next stage
        // or the victory screen; freezing then would stall the stage intro.
        if (state == State.Playing && !hitStopping) StartCoroutine(HitStop(parryHitStop));
    }

    // The first time a parry meets a crimson shot it can't turn, the game says so once.
    void OnParryFizzled()
    {
        if (toldUnparryable || state != State.Playing) return;
        toldUnparryable = true;
        ui.ShowSubtitle(null, "Some blows can't be turned. Dodge the crimson.", BulletHell.Crimson, 3f);
    }

    void OnStatueShattered(Vector3 position, bool soaked)
    {
        if (state == State.Playing) effects.OnStatueShattered(position, soaked);
    }

    void OnRiposteImpact(Vector3 point, IHittable target)
    {
        if (state == State.Playing) effects.OnRiposteImpact(point, target);
    }

    void OnUltimateStarted()
    {
        RunLog.Note($"ultimate: {UltimateName(ronin.CurrentUltimate)}");
        if (state == State.Playing) effects.OnUltimateStarted(ronin.Bounds.center);
    }

    // The clone is still out on the frame the bar runs dry, so its spot is where Pyre Shadow bursts.
    void OnUltimateEnded()
    {
        if (state == State.Playing && ronin.CloneShown) effects.OnUltimateEnded(ronin.ClonePoint);
    }

    void OnFireballImpact(Vector3 point, IHittable target)
    {
        if (state == State.Playing) effects.OnFireballImpact(point, target);
    }

    // A brief freeze when a parry lands. It always undoes its own slow-down, and leaves the time scale alone
    // if something else (the boon screen, game over) changed it in the meantime.
    IEnumerator HitStop(float seconds)
    {
        if (Time.timeScale != 1f) yield break;
        hitStopping = true;
        Time.timeScale = 0.02f;
        yield return new WaitForSecondsRealtime(seconds);
        if (Mathf.Approximately(Time.timeScale, 0.02f)) Time.timeScale = 1f;
        hitStopping = false;
    }

    // Any enemy still standing: a samurai, the scout or the boss. Parried arrows home in on one at random.
    Transform RandomEnemy()
    {
        var enemies = new List<Transform>();
        foreach (Samurai samurai in army.Soldiers)
        {
            if (!samurai.IsDead) enemies.Add(samurai.transform);
        }
        if (activeScout != null && activeScout.GetComponentInChildren<Collider>() is { enabled: true }) enemies.Add(activeScout.transform);
        if (activeBoss != null && !activeBoss.IsDead && !activeBoss.Down) enemies.Add(activeBoss.transform);
        if (activeWolf != null && activeWolf.Targetable) enemies.Add(activeWolf.transform);
        foreach (Boss soul in summons) if (soul != null && !soul.IsDead) enemies.Add(soul.transform);
        return enemies.Count > 0 ? enemies[Random.Range(0, enemies.Count)] : null;
    }
}
