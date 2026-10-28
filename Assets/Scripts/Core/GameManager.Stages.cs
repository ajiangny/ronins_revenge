using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Between stages and at the end: travel between maps, the army reaching the ronin, his hits, game over and the
// fall back to the temple.
public partial class GameManager
{
    void NextStage()
    {
        if (stageIndex + 1 < stages.Length)
        {
            stageIndex++;
            roundIndex = 0;
            StartCoroutine(EnterStage());
        }
        else
        {
            EndGame("VICTORY", "Every samurai army has fallen. The temple is safe.", new Color(0.4f, 1f, 0.5f), Sfx.VictorySting, true);
        }
    }

    IEnumerator EnterStage()
    {
        yield return TravelTo(Stage.roundsMap);
        yield return BeginRound();
    }

    // The ronin walks off the top of the field and the screen fades to black; the next map fades in with fresh cover
    // and he walks up into place from below.
    IEnumerator TravelTo(Backdrop.Map map, System.Action inDark = null)
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ClearProjectiles();
        Music.Stop(1.5f);
        Camera cam = Camera.main;
        float top = CameraShake.Center.y + cam.orthographicSize;
        float bottom = CameraShake.Center.y - cam.orthographicSize;
        Vector3 from = ronin.transform.position;

        Coroutine leaving = StartCoroutine(ronin.WalkTo(new Vector3(from.x, top + 2f, from.z), travelWalkSpeed));
        // The fade starts as he nears the top, so he's gone into the dark just as it closes.
        yield return new WaitUntil(() => ronin.transform.position.y > top - 4f);
        yield return ui.FadeTo(1f, travelFadeTime);
        yield return leaving;

        inDark?.Invoke();
        ShowMap(map);
        ronin.PlaceAt(new Vector3(ronin.Home.x, bottom - 2f, ronin.Home.z));
        yield return new WaitForSeconds(0.3f);

        StartCoroutine(ui.FadeTo(0f, travelFadeTime));
        yield return ronin.WalkTo(ronin.Home, travelWalkSpeed);
    }

    void OnArmyReachedRonin()
    {
        if (state != State.Playing) return;
        if (godMode)
        {
            KillArmy();
            return;
        }
        CameraShake.Add(0.6f);
        ronin.Explode();
        EndGame("DEFEATED", "The dead reached you - there's no stopping them now.", new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting, false,
            BossKind.None, DeathCause.Invasion);
    }

    void OnRoninHit()
    {
        if (state != State.Playing) return;
        RunLog.Hit();

        lives--;
        ui.SetLives(lives, maxLives);
        // Second Dawn: once a run, the last blow doesn't take.
        if (lives <= 0 && !dawnUsed && Upgrades.Rank(UpgradeId.SecondDawn) > 0)
        {
            dawnUsed = true;
            lives = Mathf.Min(Upgrades.DawnLives, maxLives);
            ui.SetLives(lives, maxLives);
            ElementFx.Glow(ronin.Bounds.center, OniColour, 1f, 6f, 1.2f);
            Fx.Play(Fx.BoonPillar, ronin.Feet, OniColour, 1.4f);
            Sfx.Play(Sfx.SoulCollect);
            AudioClip notYet = voiceLibrary != null ? voiceLibrary.Find("Oni_NotYet") : null;
            if (notYet != null)
            {
                oniVoice.volume = voiceVolume * Sfx.Volume;
                oniVoice.PlayOneShot(notYet);
            }
            ui.ShowSubtitle("Oni", "...Not yet.", OniColour, notYet != null ? notYet.length + 0.6f : 2f);
            return;
        }
        if (lives <= 0)
        {
            ronin.Explode();
            // The boss that killed him has the last word over him.
            string message = "The ronin has fallen.";
            Conversation last = activeBoss != null ? Dialogue.Pick(Stage.boss.ToString(), DialogueEvent.KilledOni, Dialogue.Context(loadout)) : null;
            if (last?.BuiltIn is BossLine line)
            {
                OnBossSpoke(activeBoss, line);
                message = $"\"{activeBoss.Line(line)}\"";
            }
            else if (last != null && last.Lines.Length > 0)
            {
                StartCoroutine(PlayConversation(last, activeBoss.Colour));
                message = $"\"{Dialogue.Fill(last.Lines[0].Text, Dialogue.Context())}\"";
            }
            EndGame("DEFEATED", message, new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting, false, activeBoss != null ? Stage.boss : BossKind.None,
                ronin.LastHitCause);
        }
    }

    // A run that ends in a clear or a death is remembered for the next one; the killer is the boss he fell to, or
    // None for the army.
    void EndGame(string title, string message, Color color, string sting, bool cleared, BossKind killer = BossKind.None,
        DeathCause cause = DeathCause.None)
    {
        state = State.GameOver;
        tutorial.Abort();
        // Before EndRun counts the dawn, so the first one (before the stone unseals) pays nothing.
        if (cleared) notesThisRun += Oaths.ClaimBounty();
        RunLog.EndSegment(lives, maxLives, null);
        RunLog.EndRun($"{title}  stage {stageIndex + 1} round {roundIndex + 1}  notes {notesThisRun}  held: {HeldSummary()}");
        Save.EndRun(cleared, killer, cause, loadout.DominantElement);
        lastRunNotes = notesThisRun;
        if (activeBoss != null) activeBoss.Halt();
        Music.Stop(0.6f);
        Sfx.Play(sting);
        Time.timeScale = 1f;
        army.Halt();
        ronin.ControlsEnabled = false;
        PlayerPrefs.SetInt(HighScoreKey, highScore);
        PlayerPrefs.Save();

        // A death has no end screen: like Zagreus, he's pulled back to the temple and wakes there. A clear still
        // gets its results.
        StartCoroutine(cleared ? ShowEndMenu(title, message, color) : FallToTemple());
    }

    // The killer's last word, then the dark comes down and the temple calls him back.
    IEnumerator FallToTemple()
    {
        yield return new WaitForSecondsRealtime(deathToTempleTime);
        yield return ui.FadeTo(1f, 1f);
        ReloadToSafeArea(true);
    }

    // Hearts are kept for GatherDrops, which heals him with them or fades them into points.
    static void ClearProjectiles()
    {
        foreach (Projectile projectile in FindObjectsByType<Projectile>())
        {
            Destroy(projectile.gameObject);
        }
        foreach (Arrow arrow in FindObjectsByType<Arrow>())
        {
            Destroy(arrow.gameObject);
        }
        foreach (IceStatue statue in FindObjectsByType<IceStatue>())
        {
            Destroy(statue.gameObject);
        }
        foreach (Soul soul in FindObjectsByType<Soul>())
        {
            Destroy(soul.gameObject);
        }
        foreach (TechniqueScroll scroll in FindObjectsByType<TechniqueScroll>())
        {
            Destroy(scroll.gameObject);
        }
        foreach (Scout scout in FindObjectsByType<Scout>())
        {
            Destroy(scout.gameObject);
        }
    }
}
