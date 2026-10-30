using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// The temple between runs: loading into it, waking in its pool, its stations and their pages, and setting out.
public partial class GameManager
{
    IEnumerator HubReturn()
    {
        if (!Save.Data.hubPending) yield break;
        Save.Data.hubPending = false;
        Conversation said = Dialogue.Pick("Oni", DialogueEvent.HubReturn, Dialogue.Context());
        Color colour = OniColour;
        // With nothing of his own to say, the soul he leaned on most last run may speak instead.
        if ((said == null || said.Lines.Length == 0) && Save.Data.lastRunElement >= 0)
        {
            var element = (Element)Save.Data.lastRunElement;
            var ctx = Dialogue.Context(null, element);
            said = Dialogue.Pick(SoulOf(element), DialogueEvent.SoulHub, ctx, Tier.Priority, Tier.Reactive);
            if (said == null && Random.value < 0.4f) said = Dialogue.Pick(SoulOf(element), DialogueEvent.SoulHub, ctx, Tier.Repeatable);
            colour = Boons.Info(element).Color;
        }
        if (said == null || said.Lines.Length == 0) yield break;
        yield return new WaitForSeconds(1.5f);
        yield return PlayConversation(said, colour);
    }

    // Oni's line as he climbs out of the pool, then Poguru's, after a run that ended. On the wake he meets Asagi, her
    // scene (played by EnterSafeArea) takes the place of his line.
    IEnumerator WakeTalk(bool meetAsagi)
    {
        bool pending = Save.Data.hubPending;
        // The meeting itself has already played (EnterSafeArea); it stands in for his own line.
        if (!meetAsagi) yield return HubReturn();
        yield return new WaitForSeconds(pending ? 0.6f : 1f);
        if (pending) yield return PoguruBark(DialogueEvent.PoguruWake);
        // Out of the wisp (if his word didn't already bring him) and off to his nest.
        if (state == State.Hub && safeArea != null && poguru.Current != Poguru.Mode.Perch) poguru.PerchAt(NestPoint);
    }

    // The wake after his tenth death, so her "caught you eleven times" has been earned: Asagi's hands under his arms
    // as he comes out of the pool, and she's at the rim from then on (Story/Asagi.md, "The meeting"). Her scene
    // answers the run instead of his own line. Asagi_Meet_Pool asks for the same count.
    const int AsagiDeaths = 10;
    bool AsagiDue => Save.Data.deaths >= AsagiDeaths && !Save.Data.asagiMet;

    IEnumerator MeetAsagi()
    {
        Save.Data.hubPending = false;
        Conversation said = Dialogue.Find("Asagi_Meet_Pool");
        Save.Data.asagiMet = true;
        Save.Data.asagiSoulOwed = true;
        Save.Write();
        if (said == null) yield break;
        yield return new WaitForSeconds(0.8f);
        Asagi asagi = safeArea != null ? safeArea.AsagiFigure : null;
        if (asagi != null) asagi.Play(Asagi.Clip.Talk, true);
        yield return PlayTalk(said, new TalkOptions { keepHud = true });
        if (asagi != null) asagi.Play(Asagi.Clip.Idle);
    }

    // Loading a journey: no flute intro, the page just fades off Oni already standing in the temple.
    IEnumerator TitleToSafeArea()
    {
        state = State.Transition;
        menu.LeaveTitle();
        yield return null;
        menu.HideFigure();
        ShowSafeArea();
        ronin.PlaceAt(BodyAt(safeArea.PoolExit));
        yield return menu.FadePaper(1.2f);
        menu.Hide();
        yield return EnterSafeArea(false, true);
    }

    SafeArea.Config CurrentSafeArea => safeAreas != null && safeAreas.Length > 0 ? safeAreas[0] : DefaultSafeAreas()[0];

    // Swaps in the safe area's map with no cover, and builds its stations.
    void ShowSafeArea()
    {
        SafeArea.Config area = CurrentSafeArea;
        if (backdrop != null) backdrop.Show(area.map);
        blockers.Clear();
        if (safeArea != null) Destroy(safeArea.gameObject);
        // The oath stone stands in chains until the first dawn; Asagi isn't at the rim until he's met her.
        foreach (SafeArea.Station station in area.stations)
        {
            if (station.kind == StationKind.Asagi) station.hidden = !Save.Data.asagiMet;
            if (station.kind != StationKind.Oaths) continue;
            station.isSealed = !(Oaths.Unlocked && Save.Data.oathsUnsealed);
            station.label = station.isSealed ? "SEALED STONE" : "OATH STONE";
        }
        safeArea = SafeArea.Build(area, ui.BodyFont, ronin, backdrop != null ? backdrop.GroundBounds : null);
    }

    // Safe area spots are where his feet go; he's placed and walked by his body, which stands above them.
    Vector3 BodyAt(Vector3 feet) => feet + (ronin.transform.position - ronin.Feet);

    // Between runs. After a death he wakes in the spirit pool as the dark lifts (Zagreus out of the Styx), climbs out,
    // hears what the run earned and maybe says something; then he walks the temple freely until he sets out.
    IEnumerator EnterSafeArea(bool wake, bool shown = false)
    {
        state = State.Transition;
        SetHudVisible(false);
        ronin.ControlsEnabled = false;
        poguruTalked = false;
        if (!shown)
        {
            ShowSafeArea();
            ronin.PlaceAt(BodyAt(safeArea.PoolExit));
        }
        if (sfxLibrary != null) Music.Play(sfxLibrary.TempleMusic, sfxLibrary.musicVolume);
        if (wake)
        {
            bool meetAsagi = AsagiDue;
            ronin.PlaceAt(BodyAt(safeArea.WakePoint));
            yield return ui.FadeTo(1f, 0f);
            yield return new WaitForSeconds(0.4f);
            StartCoroutine(ui.FadeTo(0f, 1.4f));
            yield return new WaitForSeconds(0.6f);
            Fx.Play(Fx.BoonPillar, ronin.Feet, OniColour, 1.4f);
            ElementFx.Glow(ronin.Bounds.center, OniColour, 1f, 6f, 1.2f);
            Sfx.Play(Sfx.SoulCollect);
            yield return new WaitForSeconds(0.7f);
            // The first time she's seen, she's already at the rim with her hands out, lifting him.
            if (meetAsagi)
            {
                safeArea.Reveal(StationKind.Asagi);
                safeArea.AsagiFigure?.Play(Asagi.Clip.Lift);
            }
            yield return ronin.WalkTo(BodyAt(safeArea.PoolExit), travelWalkSpeed * 0.3f);
            if (meetAsagi) safeArea.AsagiFigure?.Play(Asagi.Clip.Idle);
            if (lastRunNotes > 0) StartCoroutine(NoteTally(lastRunNotes));
            lastRunNotes = 0;
            // Her scene plays before he gets his controls back, so a press at her station can't open a second one.
            if (meetAsagi) yield return MeetAsagi();
            StartCoroutine(WakeTalk(meetAsagi));
        }
        // The floor is where his feet may go; the clamp is on his body.
        Vector3 lift = BodyAt(Vector3.zero);
        Rect floor = safeArea.WalkArea;
        ronin.RoamArea = new Rect(floor.x + lift.x, floor.y + lift.y, floor.width, floor.height);
        Rect[] blocks = safeArea.Blocks;
        for (int i = 0; i < blocks.Length; i++) blocks[i].position += (Vector2)lift;
        ronin.RoamBlocks = blocks;
        ronin.Roaming = true;
        ronin.ControlsEnabled = true;
        state = State.Hub;
        // Standing by the pool, he's already on his nest; waking, he flies to it after his word (WakeTalk).
        if (!wake) poguru.PerchAt(NestPoint, true);
        if (Oaths.Unlocked && !Save.Data.oathsUnsealed) StartCoroutine(UnsealOaths());
    }

    // After the first dawn the oath stone's chains break, once, and the stone answers from then on.
    IEnumerator UnsealOaths()
    {
        Save.Data.oathsUnsealed = true;
        Save.Write();
        yield return new WaitForSeconds(3.2f);
        Vector3 stone = safeArea.PointOf(StationKind.Oaths) + Vector3.up * 1.2f;
        Sfx.Play(Sfx.ChainSnap);
        Fx.Play(Fx.Impact, stone, Oaths.HeatColour, 2.2f);
        ElementFx.Glow(stone, Oaths.HeatColour, 1f, 5f, 1.2f);
        safeArea.Unseal(StationKind.Oaths);
        foreach (SafeArea.Station station in CurrentSafeArea.stations)
        {
            if (station.kind == StationKind.Oaths) station.label = "OATH STONE";
        }
        ui.ShowSubtitle(null, "The oath stone's chains fall away. Harder oaths may be sworn there now.", Oaths.HeatColour, 3.5f);
    }

    // The oath stone (Hades' Pact of Punishment): each oath cycles through its ranks; the Heat they add up to raises
    // the Spirit Notes earned, and a dawn at a new highest Heat pays a bounty.
    void ShowOaths(System.Action back, int startIndex = 0)
    {
        string muted = MenuUI.MutedHex(false);
        string heat = ColorUtility.ToHtmlStringRGB(Oaths.HeatColour);
        var items = new List<MenuUI.Item>();
        foreach (Oath oath in Oaths.All)
        {
            OathId id = oath.Id;
            int row = items.Count;
            int rank = Oaths.Rank(id);
            string note = rank > 0 ? $"{oath.Effect(rank)}   <color=#{muted}>·  Enter to {(rank < oath.MaxRank ? "swear deeper" : "release it")}</color>"
                : $"{oath.Effect(1)}   <color=#{muted}>·  Enter to swear</color>";
            MenuUI.Item item = MenuUI.Item.Button(oath.Name.ToUpperInvariant(), () =>
            {
                Oaths.Cycle(id);
                Sfx.Play(Oaths.Rank(id) > 0 ? Sfx.ChainSnap : Sfx.UiMove);
                ShowOaths(back, row);
            }, note);
            // A table row like the Flute Songs': its ranks as pips, and the Heat it adds.
            item.pips = rank;
            item.pipsMax = oath.MaxRank;
            item.detail = rank > 0 ? $"<color=#{heat}>+{rank}</color>" : "";
            items.Add(item);
        }
        items.Add(MenuUI.Item.Button("BACK", back, ""));
        int nextBounty = 0;
        for (int h = Save.Data.bestHeatCleared + 1; h <= Oaths.Heat; h++) nextBounty += Oaths.BountyPerHeat * h;
        string bounty = nextBounty > 0 ? $"   <color=#{muted}>·</color>   dawn bounty  <color=#{heat}>{nextBounty}</color>" : "";
        menu.Show("OATH STONE",
            $"<color=#{heat}>HEAT {Oaths.Heat}</color>   <color=#{muted}>·</color>   notes  x{Oaths.NoteMultiplier:0.0}{bounty}\n<size=30><color=#{muted}>Vows cut into the stone by ronin who wanted the road harder. Highest Heat at dawn: {Save.Data.bestHeatCleared}.</color></size>",
            items.ToArray(), back, startIndex, gridColumns: 2);
    }

    // What the run he just died on was worth, for a moment, under the stations.
    IEnumerator NoteTally(int earned)
    {
        ui.ShowBanner("Back in the temple", $"+{earned} Spirit Notes", $"{Save.Data.embers} to spend at the flute altar", OniColour);
        yield return new WaitForSeconds(2.8f);
        ui.HideBanner();
    }

    // Walking the safe area: the station he's beside shows its prompt; E (or Enter, Space, gamepad A) uses it and Esc
    // pauses. He stands still while a station's page is open.
    void UpdateSafeArea(Keyboard keyboard, Gamepad gamepad, bool pause)
    {
        if (stationOpen || safeArea == null) return;
        SafeArea.Station near = safeArea.Near(ronin.Feet);
        safeArea.ShowPrompt(near);
        if (pause)
        {
            OpenStation(ShowSafeAreaPause);
            return;
        }
        bool use = (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        // The press that closed a page mustn't open it again.
        if (!use || near == null || Time.unscaledTime - stationClosedAt < 0.25f) return;
        switch (near.kind)
        {
            case StationKind.Exit: StartCoroutine(SetOut()); break;
            case StationKind.FluteSongs: OpenStation(() => ShowSongs(CloseStation)); break;
            case StationKind.Memorial: OpenStation(() => ShowRecords(CloseStation)); break;
            case StationKind.WeaponRack: OpenStation(() => ShowRack(CloseStation)); break;
            case StationKind.Poguru: OpenStation(() => ShowPoguru(CloseStation)); break;
            case StationKind.Asagi: OpenStation(() => StartCoroutine(TalkToAsagi())); break;
            case StationKind.Oaths:
                if (Oaths.Unlocked) OpenStation(() => ShowOaths(CloseStation));
                else ui.ShowSubtitle("Oni", "Bound in chains. Not until the Emperor falls.", OniColour, 2.5f);
                break;
        }
    }

    void OpenStation(System.Action show)
    {
        stationOpen = true;
        ronin.ControlsEnabled = false;
        safeArea.ShowPrompt(null);
        show();
    }

    void CloseStation()
    {
        menu.Hide();
        stationOpen = false;
        stationClosedAt = Time.unscaledTime;
        if (state == State.Hub) ronin.ControlsEnabled = true;
    }

    void ShowSafeAreaPause()
    {
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("RESUME", CloseStation),
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowSafeAreaPause)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowSafeAreaPause)),
        };
        if (DevToolsOn) items.Add(MenuUI.Item.Button("DEV TOOLS", () => ShowDevMenu(ShowSafeAreaPause)));
        items.Add(MenuUI.Item.Button("QUIT TO TITLE", () => Reload(false)));
        menu.Show(CurrentSafeArea.name.ToUpperInvariant(), null, items.ToArray(), CloseStation);
    }

    // The memorial stones (Hades' Codex, cut down): how the runs went, and each warlord he has met by how often they
    // have killed him and he them. Ones he hasn't met stay worn smooth.
    void ShowRecords(System.Action back)
    {
        SaveData d = Save.Data;
        string muted = MenuUI.MutedHex(false);
        string body = $"<color=#{muted}>RUNS</color>  {d.runsStarted}        <color=#{muted}>DAWNS</color>  {d.runsCleared}        <color=#{muted}>DEATHS</color>  {d.deaths}";
        if (d.bestHeatCleared > 0) body += $"        <color=#{muted}>HIGHEST HEAT</color>  {d.bestHeatCleared}";
        body += "\n\n<align=left>";
        string Row(string name, string fell, string felled) => $"<pos=14%>{name}<pos=52%><color=#{muted}>killed him</color>  {fell}<pos=76%><color=#{muted}>fell to him</color>  {felled}\n";
        body += Row("THE ARMY", d.deathsTo[(int)BossKind.None].ToString(), "-");
        var listed = new HashSet<BossKind>();
        foreach (StageConfig stage in stages)
        {
            BossKind boss = stage.boss;
            if (boss == BossKind.None || !listed.Add(boss)) continue;
            int fell = d.deathsTo[(int)boss], felled = d.winsVs[(int)boss];
            bool met = fell + felled > 0 || d.SeenIn($"{boss}_Intro") >= 0 || d.seen.Exists(id => id.StartsWith(boss + "_"));
            body += met ? Row(boss.ToString().ToUpperInvariant(), fell.ToString(), felled.ToString()) : Row($"<color=#{muted}>· · ·</color>", "-", "-");
        }
        body += $"</align>\n<size=30><color=#{muted}>Names worn into the old stones. Fewer read clearly every time he wakes.</color></size>";
        menu.Show("MEMORIAL STONES", body, new[] { MenuUI.Item.Button("BACK", back) }, back);
    }

    // Where Poguru sits on his nest.
    Vector3 NestPoint => safeArea.TopOf(StationKind.Poguru) + Vector3.up * 0.35f;

    // Poguru's nest: a talk once a visit, what he remembers about fighting, and the switch that has the next run open
    // with his lesson again.
    void ShowPoguru(System.Action back, int startIndex = 0)
    {
        string muted = MenuUI.MutedHex(false);
        bool replay = Save.Data.tutorialReplay;
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button(poguruTalked ? $"TALK  <color=#{muted}>·  NAPPING</color>" : "TALK", () =>
            {
                if (poguruTalked)
                {
                    Sfx.Play(Sfx.UiMove);
                    return;
                }
                menu.Hide();
                StartCoroutine(TalkToPoguru());
            }, poguruTalked ? "He's said his piece. Come back after the next run." : "Hear what's on his mind."),
            MenuUI.Item.Button("TIPS", () => ShowTips(() => ShowPoguru(back, 1)), "What he remembers about fighting."),
            MenuUI.Item.Button($"REPLAY TUTORIAL  <color=#{muted}>·  {(replay ? "ON" : "OFF")}</color>", () =>
            {
                Save.Data.tutorialReplay = !replay;
                Save.Write();
                Sfx.Play(Sfx.UiConfirm);
                ShowPoguru(back, 2);
            }, replay ? "The next run opens with his lesson. Enter to switch it off." : "Enter to have the next run open with his lesson again."),
            MenuUI.Item.Button("BACK", back, ""),
        };
        menu.Show("POGURU", $"<size=30><color=#{muted}>The Lost Dragon, on his nest. He lives in your left wisp.</color></size>", items.ToArray(), back, startIndex);
    }

    // Once a visit, with the temple left as it is behind the talk.
    IEnumerator TalkToPoguru()
    {
        poguruTalked = true;
        yield return PlayTalk(Dialogue.Pick("Poguru", DialogueEvent.PoguruTalk, Dialogue.Context(loadout)), new TalkOptions { keepHud = true });
        CloseStation();
    }

    // Asagi at the rim: her pond talk (AsagiTalk), a scene over the temple as it stands, her hands up while she speaks.
    IEnumerator TalkToAsagi()
    {
        Conversation said = Dialogue.Pick("Asagi", DialogueEvent.AsagiTalk, Dialogue.Context(loadout));
        Asagi asagi = safeArea.AsagiFigure;
        if (said != null && asagi != null) asagi.Play(Asagi.Clip.Talk, true);
        yield return PlayTalk(said, new TalkOptions { keepHud = true });
        if (asagi != null) asagi.Play(Asagi.Clip.Idle);
        CloseStation();
    }

    void ShowTips(System.Action back)
    {
        string muted = MenuUI.MutedHex(false);
        string teal = ColorUtility.ToHtmlStringRGB(PoguruColour);
        string Row(string topic, string text) => $"<color=#{teal}>{topic}</color>  <color=#{muted}>·</color>  {text}\n";
        // The keys are the current control scheme's.
        string Keyed(string topic, Moves move) => $"{topic} ({Controls.Label(move).ToUpperInvariant()})";
        string body = "<size=28><align=left>" +
            Row(Keyed("SLASH", Moves.Slash), "A cut flies up the field. Cover stops it, so step out first!") +
            Row(Keyed("SHURIKEN", Moves.Shuriken), "Big and sparkly, slow to come back. Save it for crowds.") +
            Row(Keyed("PARRY", Moves.Parry), "Just before an arrow lands. A good parry is ready again straight away! Crimson blows can't be parried. Dodge those!") +
            Row("SPEARS", "The glowing red ones go right through cover. Move!") +
            Row("HEARTS", "From broken cover, and rarely a soldier. At full lives they wait on your row.") +
            Row("THE ARMY", "If it reaches your row, it's over. Thin out the bottom rank!") +
            Row(Keyed("ULTIMATE", Moves.Ultimate), "Hits fill the bottom medallion. Your shadow fights beside you!") +
            Row("BOONS", "One per soul. Hold TAB in a fight to see what your icons do.") +
            Row("PATHS", "Walk under, press E. The boss's path opens after round three.") +
            Row("SPIRIT NOTES", "They stay even when you fall. Spend them at the flute altar.") +
            "</align></size>";
        menu.Show("TIPS", body, new[] { MenuUI.Item.Button("BACK", back) }, back);
    }

    // The weapon rack (Hades' Infernal Arms): the katana is the only weapon so far, and the kunai a skin for it that
    // looks and sounds different but plays the same. The pick is kept in the save.
    void ShowRack(System.Action back, int startIndex = -1)
    {
        string muted = MenuUI.MutedHex(false);
        var carried = CarriedBlade;
        var blades = new[]
        {
            (Ronin.Blade.Katana, "KATANA", "Slash waves that cut through the ranks. The technique scrolls reshape them."),
            (Ronin.Blade.Kunai, "KUNAI", "A skin for the katana: thrown kunai that fly and hit exactly like its slash waves."),
        };
        var items = new List<MenuUI.Item>();
        foreach (var (blade, name, note) in blades)
        {
            int row = items.Count;
            Ronin.Blade pick = blade;
            items.Add(MenuUI.Item.Button(blade == carried ? $"{name}  <color=#{muted}>·  CARRIED</color>" : name, () =>
            {
                Save.Data.weapon = (int)pick;
                Save.Write();
                ronin.Skin = pick;
                Sfx.Play(Sfx.UiConfirm);
                ShowRack(back, row);
            }, note));
        }
        // The ultimate (SaveData.ultimate) isn't offered here while Shadow Clone is the only one; it comes back once
        // there are more to choose between.
        items.Add(MenuUI.Item.Button("BACK", back, ""));
        menu.Show("WEAPON RACK", $"<size=30><color=#{muted}>What he takes through the gate.</color></size>", items.ToArray(), back,
            startIndex >= 0 ? startIndex : (int)carried);
    }

    // The save's weapon, falling back to the katana for anything this build doesn't know.
    static Ronin.Blade CarriedBlade
    {
        get
        {
            var blade = (Ronin.Blade)Save.Data.weapon;
            return System.Enum.IsDefined(typeof(Ronin.Blade), blade) ? blade : Ronin.Blade.Katana;
        }
    }

    // Through the gate: he walks up to it, its doors swing open, he goes out under its beam, the dark comes down, and
    // the first stage's road opens.
    IEnumerator SetOut()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ronin.Roaming = false;
        ronin.RoamBlocks = null;
        safeArea.ShowPrompt(null);
        safeArea.HoldGateOpen = true;
        poguru.ReturnToWisp();
        yield return ronin.WalkTo(BodyAt(safeArea.PointOf(StationKind.Exit)), travelWalkSpeed * 0.6f);
        yield return new WaitUntil(() => safeArea.GateOpened);
        stageIndex = 0;
        yield return TravelTo(stages[0].roundsMap, () =>
        {
            Destroy(safeArea.gameObject);
            safeArea = null;
        });
        SetHudVisible(true);
        StartGame();
    }
}
