using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// The menu pages: title, save slots, controls, settings, pause (and the tutorial's), reload/quit and the end screen.
public partial class GameManager
{
    void ShowTitleMenu()
    {
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("START", () => ShowSlots()),
        };
        items.AddRange(new[]
        {
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowTitleMenu)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowTitleMenu)),
        });
        if (DevToolsOn) items.Add(MenuUI.Item.Button("DEV TOOLS", () => ShowDevMenu(ShowTitleMenu)));
        if (Application.platform != RuntimePlatform.WebGLPlayer) items.Add(MenuUI.Item.Button("QUIT", Quit));
        menu.ShowTitle("RONIN'S REVENGE", items.ToArray());
        ShowTitleRonin();
    }

    // Where the notes leave the flute frames, just right of the flute so they clear his head, in art pixels from
    // the top-left.
    static readonly Vector2 FluteTip = new Vector2(50f, 36f);

    // The ronin sits in the bottom-left corner of the title page, on the row he fights from, drawn at the same pixel
    // size as in play.
    void ShowTitleRonin()
    {
        Sprite[] frames = titleFluteFrames != null && titleFluteFrames.Length > 0 ? titleFluteFrames : baseRoninArt != null ? baseRoninArt.idle : null;
        Camera cam = Camera.main;
        var canvas = (RectTransform)ui.transform;
        float canvasPerWorld = canvas.rect.height / (2f * cam.orthographicSize);
        // Notes rise from the flute's tip, beside his mouth, when he's playing it.
        bool flute = titleFluteFrames != null && titleFluteFrames.Length > 0;
        menu.ShowFigure(frames, titleFluteFps, CanvasPoint(TitleSeat()), ronin.ArtPixelSize * canvasPerWorld, flute ? FluteTip : (Vector2?)null);
    }

    // Where the title ronin's feet rest in the world: 1.8 units in from the left edge, on his own row.
    Vector3 TitleSeat()
    {
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        return new Vector3(CameraShake.Center.x - halfWidth + 1.8f, ronin.ArtBounds.min.y, 0f);
    }

    Vector2 CanvasPoint(Vector3 world)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ui.transform, Camera.main.WorldToScreenPoint(world), null, out Vector2 local);
        return local;
    }

    // START: the ronin puts his flute away and gets up, then walks to the middle of his row while the paper page
    // fades into the first stage's field, and the run begins. The title figure is a UI image over the paper, so it
    // follows the real ronin (frame and position) until the paper is gone, then hands over to him. Any key skips.
    IEnumerator TitleToGame()
    {
        state = State.Transition;
        menu.LeaveTitle();
        Music.Stop(1.5f);
        bool skipped = false;
        // The press that chose START mustn't also skip.
        yield return null;

        bool stood = false;
        menu.PlayFigureOnce(titleStandFrames, titleStandFps, () => stood = true);
        while (!stood && !(skipped = SkipPressed())) yield return null;

        Vector3 seat = TitleSeat();
        ronin.PlaceAt(new Vector3(seat.x, ronin.Home.y, ronin.Home.z));
        bool walked = false;
        StartCoroutine(Then(ronin.WalkTo(ronin.Home, titleWalkSpeed, baseRoninArt.runRight, baseRoninArt.runFps * 0.7f), () => walked = true));
        Coroutine fade = StartCoroutine(menu.FadePaper(skipped ? 0.3f : titleFadeTime));
        while (!walked || menu.IsOpen)
        {
            if (!skipped && SkipPressed())
            {
                skipped = true;
                StopCoroutine(fade);
                menu.Hide();
            }
            if (skipped) ronin.FinishWalk();
            Bounds art = ronin.ArtBounds;
            if (menu.IsOpen) menu.PlaceFigure(ronin.CurrentFrame, CanvasPoint(new Vector3(art.center.x, art.min.y, 0f)));
            yield return null;
        }
        menu.HideFigure();
        SetHudVisible(true);
        StartGame();
    }

    static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        Mouse mouse = Mouse.current;
        return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            || (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }

    static IEnumerator Then(IEnumerator routine, System.Action after)
    {
        yield return routine;
        after();
    }

    // The title screen keeps the playfield clear of score, lives and ability slots.
    void SetHudVisible(bool visible)
    {
        ui.SetHudVisible(visible);
        boonChoice.SetHudVisible(visible);
    }

    // Pages opened from the title screen stay on its white page; from the pause menu they sit over the dimmed fight.
    bool OnPaper => state == State.Title;

    // The current control scheme's keys, and a switch between the two schemes (Controls), which the page redraws on.
    void ShowControls(System.Action back, int startIndex = 0)
    {
        bool paper = OnPaper;
        string muted = MenuUI.MutedHex(paper);
        string Key(string key) => MenuUI.KeyCap(key, paper);
        string Keys(Moves move) => SchemeKeys(Controls.Current, move, paper, "    or    ");
        string Row(string action, string keys) => $"<pos=20%><color=#{muted}>{action}</color><pos=50%>{keys}\n";
        bool mouse = Controls.UsesMouse;
        menu.Show("CONTROLS",
            "<align=left>" +
            Row("Move", SchemeMoveKeys(Controls.Current, paper, "    or    ")) +
            Row("Slash (hold)", Keys(Moves.Slash)) +
            Row("Parry arrows", Keys(Moves.Parry)) +
            Row("Spirit Shuriken", Keys(Moves.Shuriken)) +
            Row("Ultimate (when full)", Keys(Moves.Ultimate)) +
            Row("Show ability names", "hold " + Key("TAB")) +
            Row("Pause", Key("ESC") + "    or    " + Key("P")) +
            "</align>",
            new[]
            {
                MenuUI.Item.Button($"PLAY WITH: {Controls.Name(Controls.Current)}", () =>
                {
                    Controls.Current = mouse ? Controls.Scheme.Keyboard : Controls.Scheme.KeyboardMouse;
                    Sfx.Play(Sfx.UiConfirm);
                    ShowControls(back, 0);
                }, ""),
                MenuUI.Item.Button("BACK", back),
            }, back, startIndex, paper: paper);
    }

    // A move's keys in a scheme as key caps in text, `or` between alternatives (the controls page).
    static string SchemeKeys(Controls.Scheme scheme, Moves move, bool paper, string or) =>
        string.Join(or, System.Array.ConvertAll(Controls.Bindings(move, scheme), key => MenuUI.KeyCap(key, paper)));

    static string SchemeMoveKeys(Controls.Scheme scheme, bool paper, string or)
    {
        string Key(string key) => MenuUI.KeyCap(key, paper);
        string ad = Key("A") + "  " + Key("D");
        return scheme == Controls.Scheme.KeyboardMouse ? ad : Key("←") + "  " + Key("→") + or + ad;
    }

    // A new journey asks how Oni will fight before the flute intro, so Poguru's lessons show the right keys from the
    // first one. Two cards, each listing its keys as drawn caps, the current scheme preselected; Esc goes back to the
    // journeys.
    void ShowSchemeChoice(int slot)
    {
        string muted = MenuUI.MutedHex(true);
        MenuUI.Item Card(Controls.Scheme scheme) => MenuUI.Item.Card(Controls.Name(scheme), new[]
        {
            new MenuUI.CardLine("Move", Controls.MoveGlyphs(scheme)),
            new MenuUI.CardLine("Slash", Controls.AllGlyphs(Moves.Slash, scheme)),
            new MenuUI.CardLine("Parry", Controls.AllGlyphs(Moves.Parry, scheme)),
            new MenuUI.CardLine("Shuriken", Controls.AllGlyphs(Moves.Shuriken, scheme)),
            new MenuUI.CardLine("Ultimate", Controls.AllGlyphs(Moves.Ultimate, scheme)),
        }, () =>
        {
            Controls.Current = scheme;
            Save.Use(slot);
            StartCoroutine(TitleToGame());
        });
        menu.Show("HOW WILL YOU FIGHT?", $"<size=30><color=#{muted}>You can change this any time under CONTROLS.</color></size>",
            new[] { Card(Controls.Scheme.Keyboard), Card(Controls.Scheme.KeyboardMouse) },
            () => ShowSlots(slot), (int)Controls.Current, paper: true);
    }

    // The shop on the hill, laid out as a table: each song's name, its ranks as pips and the price of the next in Spirit
    // Notes; the line under the rows says what the selected one does. A maxed song toggles on and off instead. The
    // notes held show in the temple's resource tray in the corner.
    void ShowSongs(System.Action back, int startIndex = 0)
    {
        bool paper = OnPaper;
        string muted = MenuUI.MutedHex(paper);
        string noteHex = paper ? "1E8C7E" : ColorUtility.ToHtmlStringRGB(OniColour);
        var items = new List<MenuUI.Item>();
        Upgrades.RefundRetired();
        foreach (Song song in Upgrades.All)
        {
            if (song.Retired) continue;
            UpgradeId id = song.Id;
            int row = items.Count;
            int bought = Upgrades.Bought(id);
            bool maxed = Upgrades.Maxed(id);
            bool affordable = Upgrades.CanBuy(id);
            // An upper rank that also asks for a seal or silk shows its price in that colour, and its note line says
            // how many it takes and how many he holds.
            (Relic relic, int needed) = Upgrades.Needs(id);
            string relicHex = ColorUtility.ToHtmlStringRGB(Upgrades.RelicColour(relic));
            string priceHex = !affordable ? muted : needed > 0 ? relicHex : noteHex;
            string price = maxed ? (Upgrades.IsOff(id) ? $"<color=#{muted}>OFF</color>" : "") : $"<color=#{priceHex}>{Upgrades.Price(id)}</color>";
            string needs = needed > 0
                ? $"   <color=#{relicHex}>+ {needed} {Upgrades.RelicName(relic, needed)}</color> <color=#{muted}>(you have {Upgrades.Held(relic)})</color>"
                : "";
            string verdict = Upgrades.CanBuy(id) ? "Enter to learn" : !Upgrades.HasRelicsFor(id) ? $"needs {Upgrades.RelicName(relic, needed)}" : "not enough notes";
            string note = maxed
                ? $"{song.Effect(song.MaxRank)}   <color=#{muted}>·  Enter switches it {(Upgrades.IsOff(id) ? "on" : "off")}</color>"
                : $"{song.Effect(bought + 1)}{needs}   <color=#{muted}>·  {verdict}</color>";
            MenuUI.Item item = MenuUI.Item.Button(song.Name.ToUpperInvariant(), () =>
            {
                if (maxed) Upgrades.Toggle(id);
                else if (Upgrades.Buy(id)) Sfx.Play(Sfx.SoulCollect);
                ShowSongs(back, row);
            }, note);
            item.detail = price;
            item.pips = bought;
            item.pipsMax = song.MaxRank;
            items.Add(item);
        }
        int refundRow = items.Count;
        items.Add(MenuUI.Item.Button("REFUND ALL", () =>
        {
            Upgrades.RefundAll();
            ShowSongs(back, refundRow);
        }, $"Forget every song and take back {Upgrades.Spent} notes{RelicRefund}"));
        items.Add(MenuUI.Item.Button("BACK", back, ""));
        menu.Show("FLUTE SONGS", null, items.ToArray(), back, startIndex, paper: paper, gridColumns: 2);
    }

    // The seals and silk a refund would also return, for its note line.
    static string RelicRefund
    {
        get
        {
            string text = "";
            foreach (Relic relic in System.Enum.GetValues(typeof(Relic)))
            {
                int spent = Upgrades.SpentOf(relic);
                if (spent > 0) text += $", {spent} {Upgrades.RelicName(relic, spent)}";
            }
            return text;
        }
    }

    // Three journeys side by side, like Hades' save profiles. An empty one is a new game, which starts on the hill
    // with the old flute intro and goes straight into the first run; a journey already begun opens in the temple.
    void ShowSlots(int startIndex = -1)
    {
        string muted = MenuUI.MutedHex(OnPaper);
        var items = new List<MenuUI.Item>();
        bool any = false;
        for (int i = 0; i < Save.Slots; i++)
        {
            int slot = i;
            SaveData peek = Save.Peek(i);
            any |= peek != null;
            string label = peek == null
                ? $"{Roman(i + 1)}   <color=#{muted}>·</color>   NEW JOURNEY"
                : $"{Roman(i + 1)}   <color=#{muted}>·</color>   {peek.runsStarted} {(peek.runsStarted == 1 ? "RUN" : "RUNS")}  <color=#{muted}>·</color>  {peek.embers} NOTES";
            string note = peek == null
                ? "An empty slot. It begins on the hill, with her flute."
                : $"{peek.deaths} {(peek.deaths == 1 ? "death" : "deaths")}  ·  {peek.runsCleared} {(peek.runsCleared == 1 ? "dawn" : "dawns")}  ·  wakes in the temple";
            items.Add(MenuUI.Item.Button(label, () => ChooseSlot(slot), note));
        }
        if (any) items.Add(MenuUI.Item.Button("ERASE A JOURNEY", () => ShowErase(), ""));
        items.Add(MenuUI.Item.Button("BACK", ShowTitleMenu, ""));
        menu.Show("CHOOSE A JOURNEY", null, items.ToArray(), ShowTitleMenu, startIndex >= 0 ? startIndex : Save.Slot, paper: OnPaper);
    }

    void ChooseSlot(int slot)
    {
        if (!Save.Exists(slot))
        {
            ShowSchemeChoice(slot);
            return;
        }
        Save.Use(slot);
        StartCoroutine(TitleToSafeArea());
    }

    void ShowErase()
    {
        var items = new List<MenuUI.Item>();
        for (int i = 0; i < Save.Slots; i++)
        {
            if (!Save.Exists(i)) continue;
            int slot = i;
            items.Add(MenuUI.Item.Button($"ERASE {Roman(i + 1)}", () => ConfirmErase(slot)));
        }
        items.Add(MenuUI.Item.Button("BACK", () => ShowSlots()));
        menu.Show("ERASE A JOURNEY", null, items.ToArray(), () => ShowSlots(), paper: OnPaper);
    }

    // Erasing can't be undone, so it asks first, with KEEP IT selected.
    void ConfirmErase(int slot)
    {
        string muted = MenuUI.MutedHex(OnPaper);
        menu.Show($"ERASE {Roman(slot + 1)}?", $"<size=30><color=#{muted}>Every run, song and word heard in this journey is forgotten.</color></size>", new[]
        {
            MenuUI.Item.Button("KEEP IT", ShowErase),
            MenuUI.Item.Button("ERASE", () =>
            {
                Save.Delete(slot);
                Sfx.Play(Sfx.WardShatter);
                ShowSlots(slot);
            }),
        }, ShowErase, paper: OnPaper);
    }

    void ShowSettings(System.Action back)
    {
        menu.Show("SETTINGS", null, new[]
        {
            MenuUI.Item.Button($"CONTROLS: {Controls.Name(Controls.Current)}", () =>
            {
                Controls.Current = Controls.UsesMouse ? Controls.Scheme.Keyboard : Controls.Scheme.KeyboardMouse;
                Sfx.Play(Sfx.UiConfirm);
                ShowSettings(back);
            }, "Keyboard only, or keyboard and mouse. Using the other in a fight switches to it."),
            MenuUI.Item.Slider("MUSIC", () => Music.Volume, value => Music.Volume = value),
            MenuUI.Item.Slider("SOUND", () => Sfx.Volume, value =>
            {
                Sfx.Volume = value;
                Sfx.Play(Sfx.UiMove);
            }),
            MenuUI.Item.Button("BACK", back),
        }, back, paper: OnPaper);
    }

    // Freezes the fight under the pause menu. Only offered mid-play, never during a boon pick or a boss intro, except
    // in the tutorial, which can be paused through its drills, lessons and talks.
    void Pause()
    {
        pausedFrom = state;
        pausedTimeScale = Time.timeScale;
        pausedControls = ronin.ControlsEnabled;
        state = State.Paused;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        Music.Duck(0.35f, float.MaxValue);
        if (!tutorial.Active)
        {
            ShowPauseMenu();
            return;
        }
        // Talks and lessons run on real time: their lines and voices are held too.
        HoldVoices(true);
        dialogueScene.Hold(true);
        ShowTutorialPause();
    }

    // What the tutorial's pause hands back: it can open in a lesson's hold or a talk as well as mid-play.
    State pausedFrom;
    float pausedTimeScale = 1f;
    bool pausedControls;
    readonly List<AudioSource> heldVoices = new List<AudioSource>();

    void ShowTutorialPause()
    {
        string muted = MenuUI.MutedHex(false);
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("RESUME", ResumeTutorial),
            MenuUI.Item.Button("SKIP TUTORIAL", SkipTutorial, "Straight into a regular Round 1. Poguru will be in your wisp."),
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowTutorialPause)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowTutorialPause)),
            MenuUI.Item.Button("QUIT TO TITLE", () => Reload(false)),
        };
        if (Application.platform != RuntimePlatform.WebGLPlayer) items.Add(MenuUI.Item.Button("QUIT GAME", Quit));
        menu.Show("PAUSED", $"<size=30><color=#{muted}>Poguru's tutorial</color></size>", items.ToArray(), ResumeTutorial);
    }

    void ResumeTutorial()
    {
        menu.Hide();
        Music.Duck(1f, 0f);
        HoldVoices(false);
        dialogueScene.Hold(false);
        state = pausedFrom;
        Time.timeScale = pausedTimeScale;
        StartCoroutine(GiveBackControls(pausedControls));
    }

    // A frame late, so the key that closed the menu (Space is also the slash) doesn't fire a held lesson's move.
    IEnumerator GiveBackControls(bool enabled)
    {
        yield return null;
        if (state != State.Paused) ronin.ControlsEnabled = enabled;
    }

    // Settles the tutorial, then fades to black and starts the run again at a regular Stage I, Round 1.
    void SkipTutorial()
    {
        menu.Hide();
        tutorial.Skip();
        StartCoroutine(SkipToRoundOne());
    }

    IEnumerator SkipToRoundOne()
    {
        yield return ui.FadeTo(1f, 0.8f);
        Reload(true);
    }

    void HoldVoices(bool hold)
    {
        if (hold)
        {
            heldVoices.Clear();
            foreach (AudioSource source in new[] { voice, oniVoice, soulVoice, poguruVoice })
            {
                if (source == null || !source.isPlaying) continue;
                source.Pause();
                heldVoices.Add(source);
            }
            return;
        }
        foreach (AudioSource source in heldVoices) if (source != null) source.UnPause();
        heldVoices.Clear();
    }

    void ShowPauseMenu()
    {
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("RESUME", Resume),
            // Gives up the run (it isn't counted as a death) and wakes in the temple with what it earned.
            MenuUI.Item.Button("RETURN TO TEMPLE", () =>
            {
                lastRunNotes = notesThisRun;
                ReloadToSafeArea(true);
            }),
            MenuUI.Item.Button("RESTART", () => Reload(true)),
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowPauseMenu)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowPauseMenu)),
        };
        if (DevToolsOn) items.Add(MenuUI.Item.Button("DEV TOOLS", () => ShowDevMenu(ShowPauseMenu)));
        items.Add(MenuUI.Item.Button("QUIT TO TITLE", () => Reload(false)));
        menu.Show("PAUSED", null, items.ToArray(), Resume);
    }

    // Always back to full speed: a parry's hit-stop that was cut off by the pause would otherwise never end.
    void Resume()
    {
        menu.Hide();
        Music.Duck(1f, 0f);
        Time.timeScale = 1f;
        ResumePlay();
    }

    // Reload for a clean slate (arrows, debris, ronin), straight into play or back to the title menu.
    static void Reload(bool intoPlay)
    {
        // A restart or quit mid-run still keeps the notes earned so far.
        Save.Write();
        skipTitle = intoPlay;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // A clean scene that opens in the safe area: waking in the pool after a death, or standing by it.
    static void ReloadToSafeArea(bool wake)
    {
        Save.Write();
        intoSafeArea = true;
        wakeInPool = wake;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // A beat to watch the ronin fall (or the last army scatter) before the results come up.
    IEnumerator ShowEndMenu(string title, string message, Color color)
    {
        yield return new WaitForSecondsRealtime(1.2f);
        string muted = ColorUtility.ToHtmlStringRGB(GameUI.MutedColour);
        string gold = ColorUtility.ToHtmlStringRGB(GameUI.HighScoreColour);
        string body = $"{message}\n\n" +
            $"<color=#{muted}>SCORE</color>  <size=48>{score:000000}</size>        <color=#{muted}>HIGH SCORE</color>  <size=48><color=#{gold}>{highScore:000000}</color></size>\n" +
            $"<color=#{muted}>Reached stage {stageIndex + 1} of {stages.Length}, round {roundIndex + 1}</color>";
        if (score > highScoreAtStart) body += $"\n\n<color=#{gold}><size=44>NEW HIGH SCORE!</size></color>";
        if (notesThisRun > 0)
            body += $"\n\n<color=#{ColorUtility.ToHtmlStringRGB(OniColour)}>+{notesThisRun} SPIRIT NOTES</color>  <color=#{muted}>({Save.Data.embers} to spend in the temple)</color>";
        foreach (Relic relic in System.Enum.GetValues(typeof(Relic)))
        {
            int gained = relicsThisRun[(int)relic];
            if (gained > 0)
                body += $"\n<color=#{ColorUtility.ToHtmlStringRGB(Upgrades.RelicColour(relic))}>+{gained} {Upgrades.RelicName(relic, gained).ToUpperInvariant()}</color>";
        }
        menu.Show(title, body, new[]
        {
            MenuUI.Item.Button("RETURN TO TEMPLE", () => ReloadToSafeArea(true)),
            MenuUI.Item.Button("TITLE", () => Reload(false)),
        }, null, 0, color);
    }
}
