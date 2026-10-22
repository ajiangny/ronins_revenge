using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// The art for one element: its soul, and how the ronin and his projectiles look once he leans into it.
// Any field left empty falls back to the default art tinted in the element's colour.
[System.Serializable]
public class ElementArt
{
    public Element element;
    public Sprite[] soulFrames;
    public CharacterArt ronin;
    public Sprite slash;
    public Sprite kunai;
    public Sprite shuriken;
    [Tooltip("The boon screen's ronin portrait with his spirit wisps in this element's colour.")]
    public Sprite portrait;
}

// Owns the game flow: title menu -> stages of rounds -> win/lose menu, plus pause, score, lives, souls and boons.
public class GameManager : MonoBehaviour
{
    enum State { Title, Transition, Playing, Choosing, Paused, GameOver }

    const string HighScoreKey = "HighScore";
    static bool skipTitle;
    // Dev menu testing aids. Statics, so god mode and a pending jump survive the scene reload that starts the run.
    static bool godMode;
    static int devJumpStage = -1;
    static bool devJumpBoss;

    // The project enters Play mode without a domain reload, so these would otherwise carry over from the last
    // session: god mode left on, or a run that skips the title.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        skipTitle = false;
        godMode = false;
        devJumpStage = -1;
        devJumpBoss = false;
    }

    [Header("Inscribed")]
    [SerializeField, FormerlySerializedAs("fleet")] SamuraiArmy army;
    [SerializeField, FormerlySerializedAs("ship")] Ronin ronin;
    [SerializeField, FormerlySerializedAs("shields")] BlockerWall blockers;
    [SerializeField] GameUI ui;
    [SerializeField] int startingLives = 3;
    [SerializeField] float levelIntroTime = 2.5f;

    [Header("Souls and boons")]
    [Tooltip("Built on the Canvas at runtime if left empty.")]
    [SerializeField] BoonChoiceUI boonChoice;
    [Tooltip("Chance that a fallen samurai drops a soul.")]
    [SerializeField, Range(0, 1)] float soulDropChance = 0.07f;
    [Tooltip("A soul always drops after this many kills without one.")]
    [SerializeField] int soulPityKills = 15;
    [Tooltip("Arrow bobbing over the run's souls until the first one is caught. Empty draws a plain gold arrow.")]
    [SerializeField] Sprite soulHintArrow;
    [Tooltip("The ronin sitting on his row playing his flute, looped on the title screen. Empty shows his idle.")]
    [SerializeField] Sprite[] titleFluteFrames;
    [Tooltip("Frames per second for the title screen's flute loop.")]
    [SerializeField] float titleFluteFps = 4f;
    [Tooltip("One entry per element: Fire, Water, Lightning.")]
    [SerializeField] ElementArt[] elementArt = new ElementArt[3];

    [Header("Effects")]
    [Tooltip("Animated effects; built by Ninja/Apply VFX. Effects fall back to simple glows when it's empty.")]
    [SerializeField] FxLibrary fxLibrary;
    [Tooltip("ElevenLabs sound effects and music; built by Ninja/Apply Audio. The game runs silent when it's empty.")]
    [SerializeField] SfxLibrary sfxLibrary;
    [Tooltip("Real seconds the action freezes when a parry lands, for weight.")]
    [SerializeField] float parryHitStop = 0.06f;
    [Tooltip("How far above the ronin's feet his element aura's flames start, in world units.")]
    [SerializeField] float auraLift = 0.3f;

    [Header("Bosses")]
    [Tooltip("Benibana's sprite frames. Leave empty to use the Taisho art, tinted crimson, as a stand-in.")]
    [SerializeField] CharacterArt benibanaArt;
    [SerializeField] float benibanaScale = 3.4f;
    [Tooltip("Her full-body illustration and the close-up used in her intro. Without them she gets the plain title card.")]
    [SerializeField] Sprite benibanaPortrait;
    [SerializeField] Sprite benibanaCloseup;
    [SerializeField] AudioClip benibanaIntroLine;
    [SerializeField] AudioClip benibanaPhaseLine;
    [SerializeField] AudioClip benibanaDefeatLine;
    [Tooltip("Her short mid-fight barks: a gasp when her stance breaks and a callout for each move.")]
    [SerializeField] AudioClip benibanaStaggerLine;
    [SerializeField] AudioClip benibanaSmokeRingsLine;
    [SerializeField] AudioClip benibanaRosaryLashLine;
    [SerializeField] AudioClip benibanaIaidoLine;
    [SerializeField] AudioClip benibanaRosaryBloomLine;
    [Tooltip("Pained gasps when she takes a hit; one is picked at random.")]
    [SerializeField] AudioClip[] benibanaHurtLines;
    [Tooltip("Utsusemi's sprite frames. Leave empty to use the Taisho art, tinted steel blue, as a stand-in.")]
    [SerializeField] CharacterArt utsusemiArt;
    [SerializeField] float utsusemiScale = 3.6f;
    [Tooltip("Utsusemi's idle with its hat off, played while the hat is thrown.")]
    [SerializeField] Sprite[] utsusemiBareIdle;
    [Tooltip("Utsusemi's Hour Strikes frames: the dash between its row and the marks, the ground chop, and the hour slash.")]
    [SerializeField] Sprite[] utsusemiDash;
    [SerializeField] Sprite[] utsusemiChop;
    [SerializeField] Sprite[] utsusemiHourSlash;
    [Tooltip("Utsusemi raising its sword to throw it before the Heart Lantern, and its stance with the sword planted.")]
    [SerializeField] Sprite[] utsusemiSwordThrow;
    [SerializeField] Sprite[] utsusemiUnarmedIdle;
    [SerializeField] Sprite utsusemiPortrait;
    [SerializeField] Sprite utsusemiCloseup;
    [SerializeField] AudioClip utsusemiIntroLine;
    [SerializeField] AudioClip utsusemiPhaseLine;
    [SerializeField] AudioClip utsusemiDefeatLine;
    [SerializeField] AudioClip utsusemiStaggerLine;
    [SerializeField] AudioClip utsusemiKasaLine;
    [FormerlySerializedAs("utsusemiEscapementLine")]
    [SerializeField] AudioClip utsusemiHourStrikesLine;
    [SerializeField] AudioClip[] utsusemiHurtLines;
    [SerializeField, Range(0, 1)] float voiceVolume = 1f;
    [Tooltip("Where bosses stand, in world units from the top of the screen.")]
    [SerializeField] float bossDepth = 3f;

    [Tooltip("Intro time for the second and later rounds of a stage; the first round uses levelIntroTime.")]
    [SerializeField] float roundIntroTime = 1.2f;
    [Tooltip("Seconds a new army takes to march down into the field. The round starts once it is in place and the intro time is up.")]
    [SerializeField] float armyMarchInTime = 1.6f;
    [Tooltip("How fast the ronin walks off the field and into the next map.")]
    [SerializeField] float travelWalkSpeed = 9f;
    [Tooltip("Seconds the screen takes to fade to black, and back, between maps.")]
    [SerializeField] float travelFadeTime = 0.6f;
    [Tooltip("Pause after a stage's last army falls, before its boon is offered.")]
    [SerializeField] float stageClearPause = 1f;

    [SerializeField] StageConfig[] stages =
    {
        new StageConfig
        {
            name = "The Bamboo Road",
            rounds = new[]
            {
                new LevelConfig { columns = 8,  rows = 3, topRowY = 8.5f, baseSpeed = 2.3f, maxSpeedMultiplier = 3.5f, fireInterval = 1f,   aimedShotChance = 0.2f,  arrowSpeed = 7f,   samuraiColor = new Color(0f, 0.98f, 0.6f) },
                new LevelConfig { columns = 9,  rows = 4, topRowY = 8.4f, baseSpeed = 2.5f, maxSpeedMultiplier = 4f,   fireInterval = 0.9f, aimedShotChance = 0.25f, arrowSpeed = 7.5f, samuraiColor = new Color(0f, 0.98f, 0.6f),
                    rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru } },
                new LevelConfig { columns = 10, rows = 4, topRowY = 8.2f, baseSpeed = 2.7f, maxSpeedMultiplier = 4.2f, fireInterval = 0.8f, aimedShotChance = 0.3f,  arrowSpeed = 8f,   samuraiColor = new Color(0f, 0.98f, 0.6f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Ashigaru }, scouts = true },
            },
            boss = BossKind.Benibana,
        },
        new StageConfig
        {
            name = "The Pilgrim's Pass",
            rounds = new[]
            {
                new LevelConfig { columns = 10, rows = 4, topRowY = 8.3f, baseSpeed = 2.6f, maxSpeedMultiplier = 4f,   fireInterval = 0.85f, aimedShotChance = 0.3f,  arrowSpeed = 8f,   samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer } },
                new LevelConfig { columns = 11, rows = 4, topRowY = 8.3f, baseSpeed = 2.8f, maxSpeedMultiplier = 4.2f, fireInterval = 0.8f,  aimedShotChance = 0.3f,  arrowSpeed = 8f,   samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru }, diveInterval = 6f },
                new LevelConfig { columns = 11, rows = 5, topRowY = 8.5f, baseSpeed = 3f,   maxSpeedMultiplier = 4.4f, fireInterval = 0.75f, aimedShotChance = 0.35f, arrowSpeed = 8.5f, samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer },
                    diveInterval = 5f, scouts = true },
            },
            boss = BossKind.Utsusemi,
        },
    };

    State state;
    int stageIndex;
    int roundIndex;
    int lives;
    int score;
    int highScore;
    // The best score before this run, so the end screen can tell a new record apart.
    int highScoreAtStart;
    MenuUI menu;

    RoninLoadout loadout;
    BoonEffects effects;
    CharacterArt baseRoninArt;
    Element? shownElement;
    Soul activeSoul;
    int killsSinceSoul;
    // Souls carry a hint arrow until the player has caught one this run.
    bool soulCaught;
    FxPlayer aura;
    bool hitStopping;
    Scout activeScout;
    float nextScoutTime;
    Boss activeBoss;
    AudioSource voice;
    // Utsusemi's machine voice: a doubled, ringing, slightly gritty layer over the recorded lines.
    Behaviour[] machineVoice;
    // Whether the voice source is playing a full line rather than a bark.
    bool speakingFullLine;
    Backdrop backdrop;

    void Awake()
    {
        // Added before anything measures the view, so CameraShake.Center is the camera's resting spot.
        if (Camera.main.GetComponent<CameraShake>() == null) Camera.main.gameObject.AddComponent<CameraShake>();
        // Bosses' voicelines: a 2D source of their own, so they're never throttled like sound effects. It sits on its
        // own child so the machine-voice filters only ever touch it.
        backdrop = FindAnyObjectByType<Backdrop>();
        var voiceObject = new GameObject("BossVoice");
        voiceObject.transform.SetParent(transform, false);
        voice = voiceObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        machineVoice = CreateMachineVoice(voiceObject);
    }

    // The metallic ring: a chorus doubles the voice, a very short echo gives it a comb-filtered hollow ring, and a
    // touch of distortion adds grit. Off until a mechanical boss speaks.
    static Behaviour[] CreateMachineVoice(GameObject target)
    {
        var chorus = target.AddComponent<AudioChorusFilter>();
        chorus.dryMix = 0.7f;
        chorus.wetMix1 = 0.45f;
        chorus.wetMix2 = 0.45f;
        chorus.wetMix3 = 0f;
        chorus.delay = 14f;
        chorus.rate = 0.35f;
        chorus.depth = 0.06f;
        var ring = target.AddComponent<AudioEchoFilter>();
        ring.delay = 16f;
        ring.decayRatio = 0.4f;
        ring.wetMix = 0.35f;
        ring.dryMix = 1f;
        var grit = target.AddComponent<AudioDistortionFilter>();
        grit.distortionLevel = 0.12f;
        var filters = new Behaviour[] { chorus, ring, grit };
        foreach (Behaviour filter in filters) filter.enabled = false;
        return filters;
    }

    // OnEnable also runs after the Editor reloads scripts mid-play, which clears statics like the effect library.
    void OnEnable()
    {
        Fx.Library = fxLibrary;
        Sfx.Library = sfxLibrary;
        army.SamuraiKilled += OnSamuraiKilled;
        army.ArmyDefeated += OnArmyDefeated;
        army.ArmyReachedRonin += OnArmyReachedRonin;
        ronin.Hit += OnRoninHit;
        ronin.ShurikenThrown += OnShurikenThrown;
        ronin.Parried += OnParried;
        ronin.RiposteImpact += OnRiposteImpact;
        ronin.RiposteTarget = RandomEnemy;
    }

    void OnDisable()
    {
        army.SamuraiKilled -= OnSamuraiKilled;
        army.ArmyDefeated -= OnArmyDefeated;
        army.ArmyReachedRonin -= OnArmyReachedRonin;
        ronin.Hit -= OnRoninHit;
        ronin.ShurikenThrown -= OnShurikenThrown;
        ronin.Parried -= OnParried;
        ronin.RiposteImpact -= OnRiposteImpact;
    }

    void Start()
    {
        Time.timeScale = 1f;
        if (boonChoice == null) boonChoice = ui.gameObject.AddComponent<BoonChoiceUI>();
        boonChoice.Bind(ronin, ui);
        menu = ui.gameObject.AddComponent<MenuUI>();
        menu.Bind(ui);
        baseRoninArt = ronin.art;
        ResetLoadout();

        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        ui.SetScore(0);
        ui.SetStage(1, stages.Length, 1, stages[0].rounds.Length);
        ui.SetLives(startingLives);
        // The run opens on the first stage's own ground and cover, behind the title menu too.
        if (backdrop != null) backdrop.Show(stages[0].roundsMap);
        blockers.Use(stages[0].roundsMap?.blockers, stages[0].roundsMap?.blockerPattern);
        blockers.Rebuild();
        SetGodMode(godMode);

        if (skipTitle)
        {
            skipTitle = false;
            StartGame();
        }
        else
        {
            state = State.Title;
            ronin.ControlsEnabled = false;
            SetHudVisible(false);
            if (sfxLibrary != null) Music.Play(sfxLibrary.titleMusic, sfxLibrary.musicVolume);
            ShowTitleMenu();
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool pause = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        if (state == State.Playing && pause) Pause();
        if (Debug.isDebugBuild && keyboard != null) DevHotkeys(keyboard);
        if (state == State.Playing) UpdateScouts();
    }

    // The menu pages. Each sub-page's Back returns to whichever page opened it.
    void ShowTitleMenu()
    {
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("START", () =>
            {
                menu.Hide();
                SetHudVisible(true);
                StartGame();
            }),
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowTitleMenu)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowTitleMenu)),
        };
        if (Debug.isDebugBuild) items.Add(MenuUI.Item.Button("DEV TOOLS", () => ShowDevMenu(ShowTitleMenu)));
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
        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 seat = new Vector3(CameraShake.Center.x - halfWidth + 1.8f, ronin.ArtBounds.min.y, 0f);
        var canvas = (RectTransform)ui.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, cam.WorldToScreenPoint(seat), null, out Vector2 feet);
        float canvasPerWorld = canvas.rect.height / (2f * cam.orthographicSize);
        // Notes rise from the flute's tip, beside his mouth, when he's playing it.
        bool flute = titleFluteFrames != null && titleFluteFrames.Length > 0;
        menu.ShowFigure(frames, titleFluteFps, feet, ronin.ArtPixelSize * canvasPerWorld, flute ? FluteTip : (Vector2?)null);
    }

    // The title screen keeps the playfield clear of score, lives and ability slots.
    void SetHudVisible(bool visible)
    {
        ui.SetHudVisible(visible);
        boonChoice.SetHudVisible(visible);
    }

    // Pages opened from the title screen stay on its white page; from the pause menu they sit over the dimmed fight.
    bool OnPaper => state == State.Title;

    void ShowControls(System.Action back)
    {
        bool paper = OnPaper;
        string muted = MenuUI.MutedHex(paper);
        string Key(string key) => MenuUI.KeyCap(key, paper);
        string Row(string action, string keys) => $"<pos=20%><color=#{muted}>{action}</color><pos=50%>{keys}\n";
        menu.Show("CONTROLS",
            "<align=left>" +
            Row("Move", Key("A") + "  " + Key("D") + "    or    " + Key("←") + "  " + Key("→")) +
            Row("Slash", Key("SPACE")) +
            Row("Spirit Shuriken", Key("E") + "    or    " + Key("↑")) +
            Row("Parry arrows", Key("SHIFT") + "    or    " + Key("↓")) +
            Row("Pause", Key("ESC")) +
            "</align>\n" +
            $"Clear three samurai armies in each of the {stages.Length} stages.\nA warlord waits at the end of every stage.\n" +
            "Fallen samurai may drop spirit souls. Catch one to choose a boon.",
            new[] { MenuUI.Item.Button("BACK", back) }, back, paper: paper);
    }

    void ShowSettings(System.Action back)
    {
        menu.Show("SETTINGS", null, new[]
        {
            MenuUI.Item.Slider("MUSIC", () => Music.Volume, value => Music.Volume = value),
            MenuUI.Item.Slider("SOUND", () => Sfx.Volume, value =>
            {
                Sfx.Volume = value;
                Sfx.Play(Sfx.UiMove);
            }),
            MenuUI.Item.Button("BACK", back),
        }, back, paper: OnPaper);
    }

    // Freezes the fight under the pause menu. Only offered mid-play, never during a boon pick or a boss intro.
    void Pause()
    {
        state = State.Paused;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        Music.Duck(0.35f, float.MaxValue);
        ShowPauseMenu();
    }

    void ShowPauseMenu()
    {
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("RESUME", Resume),
            MenuUI.Item.Button("RESTART", () => Reload(true)),
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowPauseMenu)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowPauseMenu)),
        };
        if (Debug.isDebugBuild) items.Add(MenuUI.Item.Button("DEV TOOLS", () => ShowDevMenu(ShowPauseMenu)));
        items.Add(MenuUI.Item.Button("QUIT TO TITLE", () => Reload(false)));
        menu.Show("PAUSED", null, items.ToArray(), Resume);
    }

    // Testing tools, only in the Editor and development builds: start any stage's first round or boss fight
    // directly, and toggle god mode.
    void ShowDevMenu(System.Action back, int startIndex = 0)
    {
        var items = new List<MenuUI.Item>();
        for (int i = 0; i < stages.Length; i++)
        {
            int stage = i;
            items.Add(MenuUI.Item.Button($"STAGE {i + 1}  ·  ROUND 1", () => DevJump(stage, false)));
            if (stages[i].boss != BossKind.None)
                items.Add(MenuUI.Item.Button($"STAGE {i + 1}  ·  {stages[i].boss.ToString().ToUpper()}", () => DevJump(stage, true)));
        }
        int godModeRow = items.Count;
        items.Add(MenuUI.Item.Button($"GOD MODE  {(godMode ? "ON" : "OFF")}", () =>
        {
            SetGodMode(!godMode);
            ShowDevMenu(back, godModeRow);
        }));
        items.Add(MenuUI.Item.Button("BACK", back));
        string muted = MenuUI.MutedHex(OnPaper);
        menu.Show("DEV TOOLS", $"<size=30><color=#{muted}>F1 kill the army  ·  F2 last boss  ·  F3 this menu  ·  F4 god mode</color></size>",
            items.ToArray(), back, startIndex, paper: OnPaper);
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
        skipTitle = intoPlay;
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

    // One scout at a time, every 15-25 seconds in rounds that have them.
    void UpdateScouts()
    {
        if (!Round.scouts || activeScout != null || activeBoss != null || Time.time < nextScoutTime) return;
        nextScoutTime = Time.time + Random.Range(15f, 25f);
        activeScout = Scout.Spawn(army.samuraiByRow[army.samuraiByRow.Length - 1], Round.topRowY + 1.4f, OnScoutKilled);
    }

    void OnScoutKilled(Scout scout)
    {
        score += scout.Points * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        if (state == State.Playing) MaybeDropSoul(scout.transform.position, true);
    }

    // Testing shortcuts (Editor and development builds): F1 wipes out the current army, F2 skips to the last
    // stage's boss, F3 opens the dev menu, F4 toggles god mode.
    void DevHotkeys(Keyboard keyboard)
    {
        if (keyboard.f4Key.wasPressedThisFrame) SetGodMode(!godMode);
        if (keyboard.f3Key.wasPressedThisFrame)
        {
            if (state == State.Title) ShowDevMenu(ShowTitleMenu);
            else if (state == State.Playing)
            {
                Pause();
                ShowDevMenu(ShowPauseMenu);
            }
        }
        if (state != State.Playing) return;
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            KillArmy();
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

    // Drops into a stage's boss fight, or its rounds, on that fight's own map. A boss jump counts as the last round.
    void StartAt(int stage, bool boss)
    {
        stageIndex = stage;
        boss &= Stage.boss != BossKind.None;
        roundIndex = boss ? Stage.rounds.Length - 1 : 0;
        ShowMap(boss ? BossMap : Stage.roundsMap);
        StartCoroutine(boss ? BeginBoss() : BeginRound());
    }

    // Swaps the ground and builds that map's fresh cover.
    void ShowMap(Backdrop.Map map)
    {
        if (backdrop != null) backdrop.Show(map);
        blockers.Use(map?.blockers, map?.blockerPattern);
        blockers.Rebuild(loadout.BlockerHealthMultiplier);
    }

    StageConfig Stage => stages[stageIndex];
    LevelConfig Round => Stage.rounds[roundIndex];
    // The boss waits on its own ground; without art for it the duel is fought where the rounds were.
    Backdrop.Map BossMap => Stage.bossMap?.ground != null ? Stage.bossMap : Stage.roundsMap;

    void StartGame()
    {
        lives = startingLives;
        score = 0;
        highScoreAtStart = highScore;
        stageIndex = 0;
        roundIndex = 0;
        killsSinceSoul = 0;
        ResetLoadout();
        ui.SetLives(lives);
        ui.SetScore(score);
        if (devJumpStage >= 0)
        {
            int stage = Mathf.Min(devJumpStage, stages.Length - 1);
            devJumpStage = -1;
            StartCoroutine(DevJumpTo(stage, devJumpBoss));
            return;
        }
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
        // Blockers carry their damage from round to round; a new stage brings fresh ones.
        if (roundIndex == 0) blockers.Rebuild(loadout.BlockerHealthMultiplier);

        LevelConfig round = Round;
        army.SpawnStage(round, ronin.transform, ronin.TopY, blockers.TopY);
        ui.SetStage(stageIndex + 1, stages.Length, roundIndex + 1, Stage.rounds.Length);
        string body = roundIndex > 0 ? "Another army marches in."
            : stageIndex == 0 ? "Get ready!"
            : "Bigger, faster, angrier armies.";
        ui.ShowBanner($"Stage {Roman(stageIndex + 1)}  ·  Round {roundIndex + 1}", Stage.name, body, round.samuraiColor);
        Sfx.Play(Sfx.StageStart);
        if (sfxLibrary != null) Music.Play(sfxLibrary.battleMusic, sfxLibrary.musicVolume);

        // The new army marches in under the banner; the round starts once both are done.
        float introEnds = Time.time + (roundIndex == 0 ? levelIntroTime : roundIntroTime);
        yield return army.MarchIn(armyMarchInTime);
        if (Time.time < introEnds) yield return new WaitForSeconds(introEnds - Time.time);

        ui.HideBanner();
        state = State.Playing;
        ronin.ControlsEnabled = true;
        army.Advance();
        nextScoutTime = Time.time + Random.Range(6f, 10f);
    }

    void OnSamuraiKilled(Samurai samurai, KillSource source)
    {
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
        MaybeDropSoul(samurai.transform.position);
    }

    void OnShurikenThrown(Vector3 muzzle)
    {
        if (state == State.Playing) effects.OnShurikenThrown(muzzle);
    }

    void OnParried()
    {
        if (state != State.Playing) return;
        effects.OnParried();
        // Thunder Riposte can strike down the army's last samurai, which already moved on to the next stage
        // or the victory screen; freezing then would stall the stage intro.
        if (state == State.Playing && !hitStopping) StartCoroutine(HitStop(parryHitStop));
    }

    void OnRiposteImpact(Vector3 point, IHittable target)
    {
        if (state == State.Playing) effects.OnRiposteImpact(point, target);
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
        if (activeBoss != null && !activeBoss.IsDead) enemies.Add(activeBoss.transform);
        return enemies.Count > 0 ? enemies[Random.Range(0, enemies.Count)] : null;
    }

    void ResetLoadout()
    {
        loadout = new RoninLoadout();
        soulCaught = false;
        ronin.Loadout = loadout;
        effects = new BoonEffects(army, ronin, loadout);
        shownElement = null;
        UpdateRoninLook();
        boonChoice.RefreshHud(loadout);
    }

    // One soul at a time, never from the round's last samurai (the round ends before it could be caught).
    // A guaranteed drop (a scout's) still waits for the soul already falling.
    void MaybeDropSoul(Vector3 position, bool guaranteed = false)
    {
        if (activeSoul != null || army.Remaining == 0) return;
        killsSinceSoul++;
        if (!guaranteed && Random.value >= soulDropChance && killsSinceSoul < soulPityKills) return;

        killsSinceSoul = 0;
        var element = (Element)Random.Range(0, 3);
        activeSoul = Soul.Spawn(position, element, ArtFor(element)?.soulFrames, ronin, OnSoulCollected);
        if (!soulCaught) activeSoul.ShowHint(soulHintArrow);
    }

    // Pauses the game and offers three boons of the soul's element, like a god's boon in Hades.
    void OnSoulCollected(Soul soul)
    {
        if (state != State.Playing) return;
        if (!OfferBoons(soul.Element, ResumePlay)) return;
        soulCaught = true;
        Fx.Play(Fx.Impact, soul.transform.position, Boons.Info(soul.Element).Color, 1.6f);
        Sfx.Play(Sfx.SoulCollect);
    }

    void ResumePlay()
    {
        state = State.Playing;
        ronin.ControlsEnabled = true;
    }

    // Pauses on the boon screen and runs afterPick once a boon is taken. False (and nothing shown) when the element
    // has nothing left to offer.
    bool OfferBoons(Element element, System.Action afterPick)
    {
        var offers = Boons.Draw(element, 3, loadout, lives);
        if (offers.Count == 0) return false;

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        ElementArt art = ArtFor(element);
        boonChoice.Show(element, offers, art?.soulFrames, art?.portrait, index =>
        {
            TakeBoon(offers[index]);
            Time.timeScale = 1f;
            afterPick();
        });
        return true;
    }

    void TakeBoon(BoonOffer offer)
    {
        loadout.Take(offer);
        float value = offer.Boon.ValueAt(offer.Rarity);
        Color colour = Boons.Info(offer.Boon.Element).Color;
        Color pillar = Color.Lerp(Color.white, colour, 0.7f);
        Fx.Play(Fx.BoonPillar, ronin.Feet, new Color(pillar.r, pillar.g, pillar.b, 0.75f), offer.Boon.Id == BoonId.TidalRenewal ? 1.4f : 1f);
        switch (offer.Boon.Id)
        {
            case BoonId.TidalRenewal:
                lives = Mathf.Min(lives + (int)value, Boons.MaxLives);
                ui.SetLives(lives);
                break;
            case BoonId.GlacierWall:
                blockers.Rebuild(loadout.BlockerHealthMultiplier);
                foreach (Vector3 spot in blockers.BlockerPositions)
                {
                    Fx.Play(Fx.IceBurst, spot, Color.white, 1.3f);
                }
                break;
        }
        UpdateRoninLook();
        boonChoice.RefreshHud(loadout);
    }

    ElementArt ArtFor(Element element)
    {
        foreach (ElementArt art in elementArt)
        {
            if (art != null && art.element == element) return art;
        }
        return null;
    }

    // His flames and aura follow his Blade boon's element (or, before he has one, the element he has the most boons
    // of); each projectile follows the element in its slot.
    void UpdateRoninLook()
    {
        Element? attack = loadout.SlotElement(BoonSlot.Attack);
        Element? primary = attack ?? loadout.DominantElement;
        CharacterArt look = primary is Element d && ArtFor(d)?.ronin != null ? ArtFor(d).ronin : baseRoninArt;

        Element? special = loadout.SlotElement(BoonSlot.Special);
        Element? parry = loadout.SlotElement(BoonSlot.Parry);
        Sprite slash = attack is Element a ? ArtFor(a)?.slash : null;
        Sprite kunai = attack is Element a1 ? ArtFor(a1)?.kunai : null;
        Sprite shuriken = special is Element s ? ArtFor(s)?.shuriken : null;

        // The animated aura replaces the plain glow when its art exists.
        bool animatedAura = primary is Element a2 && Fx.Has(Fx.Aura(a2));
        Color glowAura = primary is Element glow && !animatedAura ? Boons.Info(glow).Color * new Color(1f, 1f, 1f, 0.45f) : Color.clear;
        ronin.SetLook(look, glowAura,
            slash, kunai, StandInTint(attack, slash), ElementColor(attack),
            shuriken, StandInTint(special, shuriken), ElementColor(special),
            parry is Element p ? Boons.Info(p).Color : Color.white);

        if (primary != shownElement)
        {
            if (aura != null) aura.Stop();
            aura = null;
            if (primary is Element changed)
            {
                // Behind the ronin and wider than him so the flames show round his silhouette; faded so he stays readable.
                aura = Fx.Play(Fx.Aura(changed), ronin.Feet + Vector3.up * auraLift, new Color(1f, 1f, 1f, 0.45f), 1.35f, ronin.transform, ronin.SpriteOrder - 1);
                Color burst = Boons.Info(changed).Color;
                if (Fx.Play(Fx.Impact, ronin.Bounds.center, new Color(burst.r, burst.g, burst.b, 0.7f), 1.3f) == null)
                {
                    ElementFx.Glow(ronin.Bounds.center, Boons.Info(changed).Color, 1f, 4f, 0.5f);
                }
                Debris.BurstSprites(ronin.Bounds.center, ronin.art.deathParticles, Boons.Info(changed).Color, 14, 1f, 6f);
            }
        }
        shownElement = primary;
    }

    static Color? ElementColor(Element? element) => element is Element e ? Boons.Info(e).Color : (Color?)null;

    // Recoloured art needs no tint; without it the default sprite is tinted in the element's colour.
    static Color StandInTint(Element? element, Sprite elementSprite)
    {
        if (element is not Element e || elementSprite != null) return Color.white;
        return Color.Lerp(Color.white, Boons.Info(e).Color, 0.75f);
    }

    void OnArmyDefeated()
    {
        if (state != State.Playing) return;

        if (roundIndex + 1 < Stage.rounds.Length)
        {
            roundIndex++;
            StartCoroutine(BeginRound());
        }
        else
        {
            StartCoroutine(StageCleared());
        }
    }

    // Every cleared stage earns a boon of a random element, then the next stage begins.
    IEnumerator StageCleared()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        yield return new WaitForSeconds(stageClearPause);

        if (Stage.boss != BossKind.None)
        {
            yield return TravelTo(BossMap);
            StartCoroutine(BeginBoss());
        }
        else
        {
            OfferStageBoon();
        }
    }

    void OfferStageBoon()
    {
        var element = (Element)Random.Range(0, 3);
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(element).Color, 1.4f);
        Sfx.Play(Sfx.SoulCollect);
        if (!OfferBoons(element, NextStage)) NextStage();
    }

    // The music cuts out, smoke rolls in across the top of the screen, and the stage's warlord descends.
    IEnumerator BeginBoss()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        army.Clear();
        ClearProjectiles();
        ui.HideBanner();
        ui.SetStage(stageIndex + 1, stages.Length, Stage.rounds.Length, Stage.rounds.Length);
        Music.Stop(0.8f);
        // A moon rises over the duel: blood red for Benibana, cold steel for Utsusemi.
        if (backdrop != null) backdrop.SetBossMoon(true, Stage.boss == BossKind.Utsusemi ? Backdrop.MoonLook.Steel : Backdrop.MoonLook.Blood);
        yield return new WaitForSeconds(1f);

        Camera cam = Camera.main;
        float top = CameraShake.Center.y + cam.orthographicSize;
        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 post = new Vector3(CameraShake.Center.x, top - bossDepth, 0);
        for (int i = 0; i < 7; i++)
        {
            Vector3 puff = new Vector3(post.x + Mathf.Lerp(-halfWidth, halfWidth, i / 6f), post.y + Random.Range(-0.5f, 1.5f), 0);
            Fx.Play(Fx.SmokePuff, puff, new Color(0.7f, 0.68f, 0.72f, 0.8f), 3f);
        }
        Sfx.Play(Sfx.FrostChill, 0.8f, 0.5f);

        activeBoss = CreateBoss(Stage.boss, post + Vector3.up * 5f);
        foreach (Behaviour filter in machineVoice) filter.enabled = Stage.boss == BossKind.Utsusemi;
        activeBoss.Damaged += OnBossDamaged;
        activeBoss.Defeated += OnBossDefeated;
        activeBoss.Spoke += OnBossSpoke;
        effects.Boss = activeBoss;
        yield return activeBoss.Enter(post, 1.6f);
        yield return activeBoss.Entrance();

        Fx.Play(Fx.SlashArc, post, activeBoss.Colour, 6f);
        if (Stage.boss == BossKind.Benibana) Fx.Play(Fx.BeniPetals, post, Color.white, 3f);
        CameraShake.Add(0.4f);
        Sfx.Play(Sfx.StageStart, 1f, 0.75f);
        BossVoice lines = VoiceFor(Stage.boss);
        if (lines.portrait != null && lines.closeup != null)
        {
            voice.volume = voiceVolume * Sfx.Volume;
            yield return ui.PlayBossIntro(lines.portrait, lines.closeup, lines.closeupHeight, lines.closeupFocus, lines.closeupAt,
                activeBoss.Title, activeBoss.Epithet, activeBoss.Colour, voice, lines.intro, activeBoss.Line(BossLine.Intro));
        }
        else
        {
            ui.ShowBanner(activeBoss.Epithet, System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(activeBoss.Title.ToLowerInvariant()), "", activeBoss.Colour);
            yield return new WaitForSeconds(2.2f);
            ui.HideBanner();
        }

        ui.ShowBossBar($"{activeBoss.Title}, {activeBoss.Epithet}", activeBoss.Colour);
        if (sfxLibrary != null) Music.Play(sfxLibrary.battleMusic, sfxLibrary.musicVolume);
        state = State.Playing;
        ronin.ControlsEnabled = true;
        activeBoss.Begin(ronin, army.samuraiPrefab.arrowPrefab);
    }

    Boss CreateBoss(BossKind kind, Vector3 position)
    {
        switch (kind)
        {
            case BossKind.Utsusemi:
                bool noArt = utsusemiArt == null;
                Utsusemi automaton = Boss.Create<Utsusemi>(noArt ? army.samuraiByRow[0] : utsusemiArt, utsusemiScale, Utsusemi.StartingHealth,
                    position, noArt ? new Color(0.6f, 0.7f, 0.95f) : Color.white);
                if (!noArt)
                {
                    automaton.SetBareIdle(utsusemiBareIdle);
                    automaton.SetSwordFrames(utsusemiDash, utsusemiChop, utsusemiHourSlash);
                    automaton.SetThrowFrames(utsusemiSwordThrow, utsusemiUnarmedIdle);
                }
                return automaton;
            default:
                bool standIn = benibanaArt == null;
                CharacterArt art = standIn ? army.samuraiByRow[0] : benibanaArt;
                Color tint = standIn ? new Color(1f, 0.55f, 0.6f) : Color.white;
                return Boss.Create<Benibana>(art, benibanaScale, Benibana.StartingHealth, position, tint);
        }
    }

    struct BossVoice
    {
        public Sprite portrait, closeup;
        // How the intro frames the close-up: its height on screen, the point of the art to centre on (0-1 across and
        // down), and where that point sits on screen.
        public float closeupHeight;
        public Vector2 closeupFocus, closeupAt;
        public AudioClip intro, phase, defeat, stagger, smokeRings, rosaryLash, iaido, rosaryBloom, kasaWheel, hourStrikes;
        public AudioClip[] hurt;

        public AudioClip Clip(BossLine line) => line switch
        {
            BossLine.Intro => intro,
            BossLine.Phase => phase,
            BossLine.Defeat => defeat,
            BossLine.Hurt => hurt != null && hurt.Length > 0 ? hurt[Random.Range(0, hurt.Length)] : null,
            BossLine.Stagger => stagger,
            BossLine.SmokeRings => smokeRings,
            BossLine.RosaryLash => rosaryLash,
            BossLine.Iaido => iaido,
            BossLine.RosaryBloom => rosaryBloom,
            BossLine.KasaWheel => kasaWheel,
            BossLine.HourStrikes => hourStrikes,
            _ => null,
        };
    }

    BossVoice VoiceFor(BossKind kind) => kind switch
    {
        BossKind.Benibana => new BossVoice
        {
            portrait = benibanaPortrait, closeup = benibanaCloseup,
            // Her face, high and right of centre.
            closeupHeight = 1500f, closeupFocus = new Vector2(0.48f, 0.25f), closeupAt = new Vector2(420f, 200f),
            intro = benibanaIntroLine, phase = benibanaPhaseLine, defeat = benibanaDefeatLine,
            stagger = benibanaStaggerLine, smokeRings = benibanaSmokeRingsLine, rosaryLash = benibanaRosaryLashLine,
            iaido = benibanaIaidoLine, rosaryBloom = benibanaRosaryBloomLine, hurt = benibanaHurtLines,
        },
        BossKind.Utsusemi => new BossVoice
        {
            portrait = utsusemiPortrait, closeup = utsusemiCloseup,
            // Framed like Benibana: filling the right half, its hat near the top edge, the brim running just off the
            // right side and the body off the bottom.
            closeupHeight = 2700f, closeupFocus = new Vector2(0.47f, 0.21f), closeupAt = new Vector2(460f, 290f),
            intro = utsusemiIntroLine, phase = utsusemiPhaseLine, defeat = utsusemiDefeatLine,
            stagger = utsusemiStaggerLine, kasaWheel = utsusemiKasaLine, hourStrikes = utsusemiHourStrikesLine, hurt = utsusemiHurtLines,
        },
        _ => default,
    };

    // Mid-fight lines (her phase break, her defeat) play over the music, which dips under them. Barks (move
    // callouts, gasps) never cut off a full line, and a hurt gasp never cuts off anything.
    void OnBossSpoke(Boss boss, BossLine line)
    {
        bool full = line == BossLine.Intro || line == BossLine.Phase || line == BossLine.Defeat;
        if (!full && voice.isPlaying && (speakingFullLine || line == BossLine.Hurt)) return;

        AudioClip clip = VoiceFor(Stage.boss).Clip(line);
        string text = boss.Line(line);
        float seconds = clip != null ? clip.length : full ? 2.5f : 1.2f;
        if (clip != null)
        {
            voice.Stop();
            voice.volume = voiceVolume * Sfx.Volume;
            voice.clip = clip;
            voice.Play();
            speakingFullLine = full;
            if (full) Music.Duck(0.3f, seconds + 0.3f);
        }
        if (!string.IsNullOrEmpty(text)) ui.ShowSubtitle(boss.Title, text, boss.Colour, seconds + 0.6f);
    }

    void OnBossDamaged(Vector3 point, KillSource source)
    {
        score += 10 * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        ui.SetBossHealth(activeBoss.HealthFraction);
        if (state != State.Playing || source == KillSource.Effect) return;

        if (source == KillSource.Reflected)
        {
            // Her own slash breaks her stance: a heavy hit-stop and a burst of red petals.
            Debris.BurstSprites(point, ronin.art.deathParticles, activeBoss.Colour, 24, 1.2f, 8f);
            Fx.Play(Fx.BeniPetals, point, Color.white, 2.2f);
            Fx.Play(Fx.Impact, point, activeBoss.Colour, 2.5f);
            CameraShake.Add(0.45f);
            if (!hitStopping) StartCoroutine(HitStop(0.15f));
        }
        else
        {
            Color spark = source == KillSource.Shuriken ? ronin.ShurikenGlow : source == KillSource.Deflected ? ronin.ParryColor : ronin.AttackGlow;
            Fx.Play(Fx.Impact, point + (Vector3)Random.insideUnitCircle * 0.6f, Color.Lerp(Color.white, spark, 0.6f), 1.2f);
        }
        effects.OnBossHit(point, source);
    }

    void OnBossDefeated()
    {
        if (state != State.Playing) return;
        score += 2000 * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        StartCoroutine(BossFell());
    }

    // She kneels and fades; her soul rises and becomes the stage's boon.
    IEnumerator BossFell()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ui.HideBossBar();
        ClearProjectiles();
        Music.Stop(1.5f);
        if (!hitStopping) StartCoroutine(HitStop(0.25f));
        Vector3 where = activeBoss.Centre;
        // Cleared only after a frame: this can run inside the boss's own hit handling, with boon effects still
        // working through the blow that felled her.
        yield return new WaitForSeconds(2.2f);
        effects.Boss = null;
        activeBoss = null;
        // Her fall lifts the blood moon.
        if (backdrop != null) backdrop.SetBossMoon(false);

        var element = (Element)Random.Range(0, 3);
        ElementFx.Glow(where, Boons.Info(element).Color, 1f, 5f, 1f);
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(element).Color, 1.4f);
        Sfx.Play(Sfx.SoulCollect);
        yield return new WaitForSeconds(0.6f);
        if (!OfferBoons(element, NextStage)) NextStage();
    }

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
            EndGame("VICTORY", "Every samurai army has fallen. The temple is safe.", new Color(0.4f, 1f, 0.5f), Sfx.VictorySting);
        }
    }

    IEnumerator EnterStage()
    {
        yield return TravelTo(Stage.roundsMap);
        yield return BeginRound();
    }

    // The ronin walks off the top of the field and the screen fades to black; the next map fades in with fresh cover
    // and he walks up into place from below.
    IEnumerator TravelTo(Backdrop.Map map)
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
        EndGame("DEFEATED", "The samurai reached you - there's no stopping them now.", new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting);
    }

    void OnRoninHit()
    {
        if (state != State.Playing) return;

        lives--;
        ui.SetLives(lives);
        if (lives <= 0)
        {
            ronin.Explode();
            EndGame("DEFEATED", "The ronin has fallen.", new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting);
        }
    }

    void EndGame(string title, string message, Color color, string sting)
    {
        state = State.GameOver;
        if (activeBoss != null) activeBoss.Halt();
        Music.Stop(0.6f);
        Sfx.Play(sting);
        Time.timeScale = 1f;
        army.Halt();
        ronin.ControlsEnabled = false;
        PlayerPrefs.SetInt(HighScoreKey, highScore);
        PlayerPrefs.Save();

        StartCoroutine(ShowEndMenu(title, message, color));
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
        menu.Show(title, body, new[]
        {
            MenuUI.Item.Button("PLAY AGAIN", () => Reload(true)),
            MenuUI.Item.Button("TITLE", () => Reload(false)),
        }, null, 0, color);
    }

    static void ClearProjectiles()
    {
        foreach (Kunai kunai in FindObjectsByType<Kunai>())
        {
            Destroy(kunai.gameObject);
        }
        foreach (Arrow arrow in FindObjectsByType<Arrow>())
        {
            Destroy(arrow.gameObject);
        }
        foreach (Soul soul in FindObjectsByType<Soul>())
        {
            Destroy(soul.gameObject);
        }
        foreach (Scout scout in FindObjectsByType<Scout>())
        {
            Destroy(scout.gameObject);
        }
    }
}
