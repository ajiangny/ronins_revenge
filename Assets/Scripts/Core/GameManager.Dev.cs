using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Dev tools: the DEV TOOLS page, stage/boss jumps, god mode and the F-key hotkeys.
public partial class GameManager
{
    // Testing tools, only in the Editor and development builds: start any stage's first round or boss fight
    // directly, and toggle god mode. A column per stage would run off the bottom of the screen, so it's a grid:
    // one row per stage, its first round on the left and its boss on the right.
    void ShowDevMenu(System.Action back, int startIndex = 0)
    {
        var items = new List<MenuUI.Item>();
        for (int i = 0; i < stages.Length; i++)
        {
            int stage = i;
            items.Add(MenuUI.Item.Button($"STAGE {Roman(i + 1)}  ·  ROUND 1", () => DevJump(stage, false)));
            // A stage without a boss jumps to its last round instead, so every row keeps two cells.
            items.Add(stages[i].boss != BossKind.None
                ? MenuUI.Item.Button($"STAGE {Roman(i + 1)}  ·  {stages[i].boss.ToString().ToUpper()}", () => DevJump(stage, true))
                : MenuUI.Item.Button($"STAGE {Roman(i + 1)}  ·  LAST ROUND", () => DevJump(stage, true)));
        }
        int godModeRow = items.Count;
        items.Add(MenuUI.Item.Button($"GOD MODE  {(godMode ? "ON" : "OFF")}", () =>
        {
            SetGodMode(!godMode);
            ShowDevMenu(back, godModeRow);
        }));
        // Enough notes to try the songs without playing for them.
        int notesRow = items.Count;
        items.Add(MenuUI.Item.Button($"+1000 NOTES  ({Save.Data.embers})", () =>
        {
            Upgrades.AddNotes(1000);
            Save.Write();
            ShowDevMenu(back, notesRow);
        }));
        // Forgets every run, line heard and song bought, so the game plays as on a first boot.
        int resetRow = items.Count;
        items.Add(MenuUI.Item.Button(Save.Data.runsStarted > 0 ? $"RESET SAVE  ({Save.Data.runsStarted} RUNS)" : "RESET SAVE  (FRESH)", () =>
        {
            Save.Reset();
            ShowDevMenu(back, resetRow);
        }));
        items.Add(MenuUI.Item.Button("BACK", back));
        string muted = MenuUI.MutedHex(OnPaper);
        menu.Show("DEV TOOLS", $"<size=30><color=#{muted}>F1 kill the army  ·  F2 last boss  ·  F3 this menu  ·  F4 god mode  ·  F5 soul  ·  F6 scroll  ·  F7 dialogue  ·  F10 bead  ·  F11 thread</color></size>",
            items.ToArray(), back, startIndex, paper: OnPaper, gridColumns: 2);
    }

    // A jump restarts the run from a clean scene, then StartGame drops it into the chosen fight.
    static void DevJump(int stage, bool boss)
    {
        devJumpStage = stage;
        devJumpBoss = boss;
        Reload(true);
    }

    // The ronin takes no hits, and an army that lands is swept away instead of ending the run.
    void SetGodMode(bool on)
    {
        godMode = on;
        ronin.Invincible = on;
        ui.SetGodMode(on);
    }

    // Testing shortcuts (Editor and development builds): F1 wipes out the current army, F2 skips to the last
    // stage's boss, F3 opens the dev menu, F4 toggles god mode, F5 drops a soul (Fire, Frost, Storm in turn) at the
    // ronin's feet, F10 opens a Spirit Bead's screen and F11 a Spirit Thread's.
    void DevHotkeys(Keyboard keyboard)
    {
        if (keyboard.f4Key.wasPressedThisFrame) SetGodMode(!godMode);
        // What each speaker could say now, and why the rest can't play.
        if (keyboard.f7Key.wasPressedThisFrame)
        {
            DialogueContext ctx = Dialogue.Context(loadout);
            string boss = Stage.boss.ToString();
            Debug.Log(Dialogue.Explain(boss, DialogueEvent.BossIntro, ctx) + Dialogue.Explain(boss, DialogueEvent.KilledOni, ctx)
                + Dialogue.Explain("Oni", DialogueEvent.HubReturn, ctx) + Dialogue.Explain("Poguru", DialogueEvent.PoguruWake, ctx)
                + Dialogue.Explain("Poguru", DialogueEvent.PoguruTalk, ctx));
        }
        if (keyboard.f3Key.wasPressedThisFrame)
        {
            if (state == State.Title) ShowDevMenu(ShowTitleMenu);
            else if (state == State.Hub && !stationOpen) OpenStation(() => ShowDevMenu(ShowSafeAreaPause));
            else if (state == State.Playing)
            {
                Pause();
                ShowDevMenu(ShowPauseMenu);
            }
        }
        if (keyboard.f8Key.wasPressedThisFrame) RunLog.Dump();
        if (keyboard.f9Key.wasPressedThisFrame) ronin.FillUltimate();
        if (state != State.Playing) return;
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            KillArmy();
        }
        else if (keyboard.f5Key.wasPressedThisFrame && activeSoul == null)
        {
            var element = (Element)(devSoulElement++ % System.Enum.GetValues(typeof(Element)).Length);
            // A Water soul dropped by hand stands in for meeting Asagi, so her souls keep coming after it.
            if (element == Element.Tide && !Save.Data.asagiMet)
            {
                Save.Data.asagiMet = true;
                // Count the meeting as heard too, or her station would open on it and her chain stay gated.
                Dialogue.Find("Asagi_Meet_Pool");
                Save.Write();
            }
            SpawnSoul(ronin.Feet + Vector3.up * 2f, element);
        }
        else if (keyboard.f6Key.wasPressedThisFrame && activeScroll == null && Boons.DrawArts(loadout, 1).Count > 0)
        {
            SpawnScroll(ronin.Feet + Vector3.up * 2f);
        }
        else if (keyboard.f10Key.wasPressedThisFrame)
        {
            OfferBead(ResumePlay);
        }
        else if (keyboard.f11Key.wasPressedThisFrame)
        {
            OfferThread(ResumePlay);
        }
        else if (keyboard.f2Key.wasPressedThisFrame && activeBoss == null)
        {
            // Straight to the last stage's boss, or its last round when it has none.
            StageConfig last = stages[stages.Length - 1];
            StartAt(stages.Length - 1, last.boss != BossKind.None);
        }
    }

    void KillArmy()
    {
        foreach (Samurai soldier in new List<Samurai>(army.Soldiers)) soldier.Kill(KillSource.Effect);
    }
}
