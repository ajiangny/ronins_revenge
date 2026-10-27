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
    // A Spirit Offering taken at full health gives these points, times the stage number.
    const int OfferingPoints = 500;
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
    [Tooltip("Keep DEV TOOLS and the F1-F4 hotkeys in release builds, for friends play-testing. Development builds always have them.")]
    [SerializeField] bool devToolsInRelease = true;

    [Header("Souls and boons")]
    [Tooltip("Built on the Canvas at runtime if left empty.")]
    [SerializeField] BoonChoiceUI boonChoice;
    [Tooltip("Chance that a fallen samurai drops a soul.")]
    [SerializeField, Range(0, 1)] float soulDropChance = 0.07f;
    [Tooltip("A soul always drops after this many kills without one.")]
    [SerializeField] int soulPityKills = 15;
    [Tooltip("Once the ronin holds a boon, the chance a soul takes one of his elements (weighted by how many boons of each he holds) instead of a random one.")]
    [SerializeField, Range(0, 1)] float soulElementBias = 0.5f;
    [Tooltip("Arrow bobbing over the run's souls until the first one is caught. Empty draws a plain gold arrow.")]
    [SerializeField] Sprite soulHintArrow;

    [Header("Technique scrolls (Sword Arts)")]
    [Tooltip("Chance a downed kite scout also drops a technique scroll.")]
    [SerializeField, Range(0, 1)] float scrollChanceFromScout = 0.4f;
    [Tooltip("Chance any fallen samurai drops a technique scroll.")]
    [SerializeField, Range(0, 1)] float scrollChancePerKill = 0.005f;
    [Tooltip("Most technique scrolls that can drop in one run, like Hades' two Daedalus Hammers.")]
    [SerializeField] int maxScrollsPerRun = 2;
    [Tooltip("The technique scroll pickup. Wired by Ninja/Apply Boon Art; empty draws a parchment bar on a gold glow.")]
    [SerializeField] Sprite scrollSprite;
    [Tooltip("Chance that a blocker worn down by hits drops a heart, which heals a lost life.")]
    [SerializeField, Range(0, 1)] float heartDropChance = 0.2f;
    [Tooltip("The ronin sitting on his row playing his flute, looped on the title screen. Empty shows his idle.")]
    [SerializeField] Sprite[] titleFluteFrames;
    [Tooltip("Frames per second for the title screen's flute loop.")]
    [SerializeField] float titleFluteFps = 4f;
    [Tooltip("The ronin putting his flute away and getting up, played once when START is chosen. Empty skips to the walk.")]
    [SerializeField] Sprite[] titleStandFrames;
    [Tooltip("Frames per second for the stand-up.")]
    [SerializeField] float titleStandFps = 8f;
    [Tooltip("How fast the ronin walks from his seat to the middle of his row after START, in world units a second.")]
    [SerializeField] float titleWalkSpeed = 6f;
    [Tooltip("Seconds the title page takes to fade into the first stage's field as he walks.")]
    [SerializeField] float titleFadeTime = 2.2f;
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
    [Tooltip("Her unsealed frames, worn from her phase break on. Leave empty to keep her first art.")]
    [SerializeField] CharacterArt benibanaPhaseArt;
    [SerializeField] float benibanaScale = 3.4f;
    [Tooltip("Her full-body illustration and the close-up used in her intro. Without them she gets the plain title card.")]
    [SerializeField] Sprite benibanaPortrait;
    [SerializeField] Sprite benibanaCloseup;
    [Tooltip("Her unsealed second-phase illustrations: the card that plays in her phase break. Without them her phase line is a subtitle.")]
    [SerializeField] Sprite benibanaPhasePortrait;
    [SerializeField] Sprite benibanaPhaseCloseup;
    [SerializeField] AudioClip benibanaIntroLine;
    [SerializeField] AudioClip benibanaPhaseLine;
    [SerializeField] AudioClip benibanaDefeatLine;
    [Tooltip("Her cries when her stance breaks, a few takes; one is picked at random, in the order of Benibana.Line's subtitles.")]
    [SerializeField] AudioClip[] benibanaStaggerLines;
    [Tooltip("Her callouts for each move, a few takes each; one is picked at random, in the order of Benibana.Line's subtitles.")]
    [SerializeField] AudioClip[] benibanaSmokeRingsLines;
    [SerializeField] AudioClip[] benibanaRosaryLashLines;
    [SerializeField] AudioClip[] benibanaIaidoLines;
    [SerializeField] AudioClip[] benibanaRosaryBloomLines;
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
    [Tooltip("Its overdriven frames, worn from its phase break on, with its extra clips in that form. Leave empty to keep its first art.")]
    [SerializeField] CharacterArt utsusemiPhaseArt;
    [SerializeField] Sprite[] utsusemiPhaseBareIdle;
    [SerializeField] Sprite[] utsusemiPhaseUnarmedIdle;
    [SerializeField] Sprite[] utsusemiPhaseDash;
    [SerializeField] Sprite[] utsusemiPhaseChop;
    [SerializeField] Sprite[] utsusemiPhaseHourSlash;
    [SerializeField] Sprite[] utsusemiPhaseSwordThrow;
    [SerializeField] Sprite utsusemiPortrait;
    [SerializeField] Sprite utsusemiCloseup;
    [Tooltip("Its overdriven second-phase illustrations: the card that plays in its phase break. Without them its phase line is a subtitle.")]
    [SerializeField] Sprite utsusemiPhasePortrait;
    [SerializeField] Sprite utsusemiPhaseCloseup;
    [SerializeField] AudioClip utsusemiIntroLine;
    [SerializeField] AudioClip utsusemiPhaseLine;
    [SerializeField] AudioClip utsusemiDefeatLine;
    [SerializeField] AudioClip utsusemiStaggerLine;
    [SerializeField] AudioClip utsusemiKasaLine;
    [FormerlySerializedAs("utsusemiEscapementLine")]
    [SerializeField] AudioClip utsusemiHourStrikesLine;
    [SerializeField] AudioClip[] utsusemiHurtLines;
    [Tooltip("Shirogane's sprite frames. Leave empty to use the Bushi art, tinted spirit blue, as a stand-in.")]
    [SerializeField] CharacterArt shiroganeArt;
    [SerializeField] float shiroganeScale = 3.4f;
    [Tooltip("Shirogane's dash frames for the Moonlit Dash.")]
    [SerializeField] Sprite[] shiroganeDash;
    [Tooltip("Her awakened frames, spirit flame on her blades, worn from her phase break on. Leave empty to keep her first art.")]
    [SerializeField] CharacterArt shiroganePhaseArt;
    [SerializeField] Sprite[] shiroganePhaseDash;
    [Tooltip("Her freed soul in the ending, drawn at Oni's size: sinking to her knees (reversed, rising), a bow, an idle.")]
    [SerializeField] Sprite[] freedKneel;
    [SerializeField] Sprite[] freedBow;
    [SerializeField] Sprite[] freedIdle;
    [Tooltip("Her spirit wolf's frames: resting beside her, howling, charging down a lane and dissolving into mist.")]
    [SerializeField] Sprite[] shiroWolfIdle;
    [SerializeField] Sprite[] shiroWolfHowl;
    [SerializeField] Sprite[] shiroWolfCharge;
    [SerializeField] Sprite[] shiroWolfVanish;
    [Tooltip("Kuon's Maw: it rears, gathers the orb in its jaws and swallows it. Without it, it howls.")]
    [SerializeField] Sprite[] shiroWolfMaw;
    [SerializeField] Sprite shiroganePortrait;
    [SerializeField] Sprite shiroganeCloseup;
    [Tooltip("Her second-phase illustrations, with the wolf: the card that plays as it arrives. Without them her phase line is a subtitle.")]
    [FormerlySerializedAs("shiroganeWolfPortrait")]
    [SerializeField] Sprite shiroganePhasePortrait;
    [FormerlySerializedAs("shiroganeWolfCloseup")]
    [SerializeField] Sprite shiroganePhaseCloseup;
    [SerializeField] AudioClip shiroganeIntroLine;
    [SerializeField] AudioClip shiroganePhaseLine;
    [SerializeField] AudioClip shiroganeDefeatLine;
    [SerializeField] AudioClip shiroganeDefeatFinalLine;
    [SerializeField] AudioClip shiroganeStaggerLine;
    [SerializeField] AudioClip shiroganeCrescentLine;
    [SerializeField] AudioClip shiroganeBladeRainLine;
    [SerializeField] AudioClip shiroganeEclipseLine;
    [Tooltip("Her word to Kuon as she falls before it, sending it on enraged.")]
    [SerializeField] AudioClip shiroganeKuonEnragedLine;
    [SerializeField] AudioClip[] shiroganeHurtLines;
    [Tooltip("Kagetsune's sprite frames. Leave empty to use the Taisho art, darkened to blackened bronze, as a stand-in.")]
    [SerializeField] CharacterArt kagetsuneArt;
    [Tooltip("The Unchained Wraith's frames, worn from his phase break on. Leave empty to keep his first art.")]
    [SerializeField] CharacterArt kagetsunePhaseArt;
    [Tooltip("He is drawn bigger than the other bosses: a colossus over the field.")]
    [SerializeField] float kagetsuneScale = 4.6f;
    [Tooltip("How much lower than the other bosses he stands, so his horns and the Wraith's ghost-fire clear the boss bar.")]
    [SerializeField] float kagetsuneDrop = 2.1f;
    [SerializeField] Sprite kagetsunePortrait;
    [SerializeField] Sprite kagetsuneCloseup;
    [Tooltip("The Unchained Wraith's illustrations: the card that plays in his phase break. Without them his phase line is a subtitle.")]
    [SerializeField] Sprite kagetsunePhasePortrait;
    [SerializeField] Sprite kagetsunePhaseCloseup;
    [SerializeField] AudioClip kagetsuneIntroLine;
    [SerializeField] AudioClip kagetsunePhaseLine;
    [SerializeField] AudioClip kagetsuneDefeatLine;
    [SerializeField] AudioClip kagetsuneStaggerLine;
    [SerializeField] AudioClip kagetsuneKilledOniLine;
    [Tooltip("His move callouts and summons, and the summoned souls' lines in their own voices. Missing clips just show the subtitle.")]
    [SerializeField] AudioClip kagetsuneCofferSlamLine;
    [SerializeField] AudioClip kagetsuneTollLine;
    [SerializeField] AudioClip kagetsuneOfudaLine;
    [SerializeField] AudioClip kagetsuneWailLine;
    [SerializeField] AudioClip kagetsuneLastTollLine;
    [SerializeField] AudioClip kagetsuneSwarmLine;
    [SerializeField] AudioClip kagetsuneChainsLine;
    [SerializeField] AudioClip kagetsuneCoffinLine;
    [SerializeField] AudioClip kagetsuneThousandBellsLine;
    [SerializeField] AudioClip kagetsuneSummonBenibanaLine;
    [SerializeField] AudioClip kagetsuneSummonUtsusemiLine;
    [SerializeField] AudioClip kagetsuneSummonShiroganeLine;
    [SerializeField] AudioClip echoBenibanaLine;
    [SerializeField] AudioClip echoUtsusemiLine;
    [SerializeField] AudioClip echoShiroganeLine;
    [Tooltip("Shirogane in the ending, once Oni cuts her chain, and her farewell at dawn.")]
    [SerializeField] AudioClip shiroganeFreedLine;
    [SerializeField] AudioClip shiroganeFarewellLine;
    [SerializeField] AudioClip[] kagetsuneHurtLines;
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

    [SerializeField] StageConfig[] stages = DefaultStages();

    // The stages as written in code. The scene keeps its own serialized copy, which wins at runtime; Ninja/Apply Stages
    // copies these into it (maps are wired separately by Ninja/Apply Maps).
    public static StageConfig[] DefaultStages() => new[]
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
        new StageConfig
        {
            name = "The Frozen Shrine",
            rounds = new[]
            {
                new LevelConfig { columns = 11, rows = 5, topRowY = 8.5f, baseSpeed = 3f,   maxSpeedMultiplier = 4.4f, fireInterval = 0.75f, aimedShotChance = 0.35f, arrowSpeed = 8.5f, samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer }, diveInterval = 6f },
                new LevelConfig { columns = 12, rows = 5, topRowY = 8.6f, baseSpeed = 3.2f, maxSpeedMultiplier = 4.6f, fireInterval = 0.7f,  aimedShotChance = 0.4f,  arrowSpeed = 9f,   samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Yurei, SamuraiKind.Shieldbearer }, diveInterval = 5f, scouts = true },
                new LevelConfig { columns = 12, rows = 6, topRowY = 8.8f, baseSpeed = 3.4f, maxSpeedMultiplier = 4.8f, fireInterval = 0.65f, aimedShotChance = 0.45f, arrowSpeed = 9.5f, samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Taisho, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer },
                    diveInterval = 4f, scouts = true },
            },
            boss = BossKind.Shirogane,
        },
        // The mountain shrine where the Night of a Thousand Bells was held: every kind of soldier he has, and his
        // bellringers, whose tolls rouse the ranks. The army wears crimson and bronze, the court's colours.
        new StageConfig
        {
            name = "The Mountain Shrine",
            rounds = new[]
            {
                new LevelConfig { columns = 12, rows = 5, topRowY = 8.6f, baseSpeed = 3.3f, maxSpeedMultiplier = 4.6f, fireInterval = 0.7f,  aimedShotChance = 0.4f,  arrowSpeed = 9f,   samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Bellringer, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer }, diveInterval = 5f },
                new LevelConfig { columns = 12, rows = 6, topRowY = 8.8f, baseSpeed = 3.5f, maxSpeedMultiplier = 4.8f, fireInterval = 0.66f, aimedShotChance = 0.45f, arrowSpeed = 9.5f, samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Bellringer, SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Bellringer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer },
                    diveInterval = 4.5f, scouts = true },
                new LevelConfig { columns = 13, rows = 6, topRowY = 9f,   baseSpeed = 3.6f, maxSpeedMultiplier = 5f,   fireInterval = 0.62f, aimedShotChance = 0.5f,  arrowSpeed = 10f,  samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Bellringer, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Bellringer, SamuraiKind.Shieldbearer },
                    diveInterval = 3.5f, scouts = true },
            },
            boss = BossKind.Kagetsune,
        },
    };

    State state;
    int stageIndex;
    int roundIndex;
    int lives;
    // Lives he can heal back up to; only Tidal Renewal raises it.
    int maxLives;
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
    // A soul that reached the ronin while the round was wrapping up; its boons are offered before the next round.
    Element? gatheredSoul;
    TechniqueScroll activeScroll;
    // A scroll that reached the ronin while the round was wrapping up; its arts are offered before the next round.
    bool gatheredScroll;
    int scrollsDropped;
    int killsSinceSoul;
    // The element F5's next soul brings.
    int devSoulElement;
    // Souls carry a hint arrow until the player has caught one this run.
    bool soulCaught;
    FxPlayer aura;
    bool hitStopping;
    // The crimson-shot hint shows once a session.
    bool toldUnparryable;
    Scout activeScout;
    float nextScoutTime;
    Boss activeBoss;
    // Shirogane's wolf, Kuon, while it is in the fight with a bar of its own.
    SpiritWolf activeWolf;
    AudioSource voice;
    // The voice filters: Utsusemi's machine voice (a doubled, ringing, slightly gritty layer over the recorded
    // lines) and Shirogane's spirit voice (a soft, hollow echo, as if from across water) retune the same three.
    Behaviour[] machineVoice;
    // Whether the voice source is playing a full line rather than a bark.
    bool speakingFullLine;
    // The take each line played last, so the next call of it picks another.
    readonly Dictionary<BossLine, int> lastTakes = new Dictionary<BossLine, int>();
    Backdrop backdrop;

    void Awake()
    {
        // Added before anything measures the view, so CameraShake.Center is the camera's resting spot.
        if (Camera.main.GetComponent<CameraShake>() == null) Camera.main.gameObject.AddComponent<CameraShake>();
        Boss.HitStop = seconds =>
        {
            if (state == State.Playing && !hitStopping) StartCoroutine(HitStop(seconds));
        };
        Boss.RenameBar = name => ui.RenameBossBar(name);
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

    // Unity allows one of each filter per source, so each boss's voice is a retuning of the same three: the
    // machine's tight metallic ring, or the ghost's long, quiet echo and slow drift with no grit. Off for Benibana.
    void TuneVoice(BossKind boss)
    {
        var chorus = (AudioChorusFilter)machineVoice[0];
        var ring = (AudioEchoFilter)machineVoice[1];
        bool spirit = boss == BossKind.Shirogane;
        // The Emperor: a choir of the souls inside him, a wide slow chorus that drifts deeper, under a hall's echo.
        bool choir = boss == BossKind.Kagetsune;
        chorus.dryMix = spirit ? 0.8f : choir ? 0.75f : 0.7f;
        chorus.wetMix1 = chorus.wetMix2 = spirit ? 0.3f : choir ? 0.55f : 0.45f;
        chorus.delay = spirit ? 30f : choir ? 45f : 14f;
        chorus.rate = spirit ? 0.2f : choir ? 0.12f : 0.35f;
        chorus.depth = spirit ? 0.1f : choir ? 0.3f : 0.06f;
        ring.delay = spirit ? 140f : choir ? 70f : 16f;
        ring.decayRatio = spirit ? 0.3f : choir ? 0.35f : 0.4f;
        ring.wetMix = spirit ? 0.28f : choir ? 0.3f : 0.35f;
        bool on = boss == BossKind.Utsusemi || spirit || choir;
        chorus.enabled = ring.enabled = on;
        machineVoice[2].enabled = boss == BossKind.Utsusemi;
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
        ronin.ParryFizzled += OnParryFizzled;
        ronin.RiposteImpact += OnRiposteImpact;
        ronin.FireballImpact += OnFireballImpact;
        ronin.RiposteTarget = RandomEnemy;
        Blocker.Broken += OnBlockerBroken;
        IceStatue.Shattered += OnStatueShattered;
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
        ronin.FireballImpact -= OnFireballImpact;
        Blocker.Broken -= OnBlockerBroken;
        IceStatue.Shattered -= OnStatueShattered;
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
        ui.SetLives(startingLives, startingLives);
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

    bool DevToolsOn => Debug.isDebugBuild || devToolsInRelease;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool pause = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        if (state == State.Playing && pause) Pause();
        if (DevToolsOn && keyboard != null) DevHotkeys(keyboard);
        if (state == State.Playing) UpdateScouts();
    }

    // The menu pages. Each sub-page's Back returns to whichever page opened it.
    void ShowTitleMenu()
    {
        var items = new List<MenuUI.Item>
        {
            MenuUI.Item.Button("START", () => StartCoroutine(TitleToGame())),
            MenuUI.Item.Button("CONTROLS", () => ShowControls(ShowTitleMenu)),
            MenuUI.Item.Button("SETTINGS", () => ShowSettings(ShowTitleMenu)),
        };
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

    void ShowControls(System.Action back)
    {
        bool paper = OnPaper;
        string muted = MenuUI.MutedHex(paper);
        string Key(string key) => MenuUI.KeyCap(key, paper);
        string Row(string action, string keys) => $"<pos=20%><color=#{muted}>{action}</color><pos=50%>{keys}\n";
        menu.Show("CONTROLS",
            "<align=left>" +
            Row("Move", Key("A") + "  " + Key("D") + "    or    " + Key("←") + "  " + Key("→")) +
            Row("Slash", Key("SPACE") + "    or    " + Key("LEFT CLICK")) +
            Row("Spirit Shuriken", Key("W") + "    or    " + Key("↑")) +
            Row("Parry arrows", Key("SHIFT") + "  " + Key("↓") + "    or    " + Key("RIGHT CLICK")) +
            Row("Show ability names", "hold " + Key("TAB")) +
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
        if (DevToolsOn) items.Add(MenuUI.Item.Button("DEV TOOLS", () => ShowDevMenu(ShowPauseMenu)));
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
        menu.Show("DEV TOOLS", $"<size=30><color=#{muted}>F1 kill the army  ·  F2 last boss  ·  F3 this menu  ·  F4 god mode  ·  F5 soul  ·  F6 scroll</color></size>",
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
        // Scouts are where scrolls usually come from; it lands beside the soul rather than on it.
        if (state == State.Playing) MaybeDropScroll(scout.transform.position + Vector3.right * 0.9f, scrollChanceFromScout);
    }

    // Testing shortcuts (Editor and development builds): F1 wipes out the current army, F2 skips to the last
    // stage's boss, F3 opens the dev menu, F4 toggles god mode, F5 drops a soul (Fire, Frost, Storm in turn) at the
    // ronin's feet.
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
        else if (keyboard.f5Key.wasPressedThisFrame && activeSoul == null)
        {
            SpawnSoul(ronin.Feet + Vector3.up * 2f, (Element)(devSoulElement++ % 3));
        }
        else if (keyboard.f6Key.wasPressedThisFrame && activeScroll == null && Boons.DrawArts(loadout, 1).Count > 0)
        {
            SpawnScroll(ronin.Feet + Vector3.up * 2f);
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
        lives = maxLives = startingLives;
        score = 0;
        highScoreAtStart = highScore;
        stageIndex = 0;
        roundIndex = 0;
        killsSinceSoul = 0;
        ResetLoadout();
        ui.SetLives(lives, maxLives);
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
            : stageIndex == 1 ? "Bigger, faster, angrier armies."
            : "The dead march too.";
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
        MaybeDropScroll(samurai.transform.position, scrollChancePerKill);
    }

    void OnShurikenThrown(Vector3 muzzle, Kunai shuriken)
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

    void OnStatueShattered(Vector3 position)
    {
        if (state == State.Playing) effects.OnStatueShattered(position);
    }

    void OnRiposteImpact(Vector3 point, IHittable target)
    {
        if (state == State.Playing) effects.OnRiposteImpact(point, target);
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
        return enemies.Count > 0 ? enemies[Random.Range(0, enemies.Count)] : null;
    }

    void ResetLoadout()
    {
        loadout = new RoninLoadout();
        soulCaught = false;
        scrollsDropped = 0;
        gatheredScroll = false;
        ronin.Loadout = loadout;
        effects = new BoonEffects(army, ronin, loadout);
        army.SpeedUpScale = loadout.MarchSpeedUpScale;
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
        SpawnSoul(position, PickSoulElement());
    }

    void SpawnSoul(Vector3 position, Element element)
    {
        activeSoul = Soul.Spawn(position, element, ArtFor(element)?.soulFrames, ronin, OnSoulCollected);
        if (!soulCaught) activeSoul.ShowHint(soulHintArrow);
    }

    // Like the gods a Hades run keeps meeting, souls lean toward the elements the ronin already carries.
    Element PickSoulElement()
    {
        if (loadout.BoonCount == 0 || Random.value >= soulElementBias) return (Element)Random.Range(0, 3);
        float roll = Random.value * loadout.BoonCount;
        for (int i = 0; i < 3; i++)
        {
            roll -= loadout.ElementCount((Element)i);
            if (roll < 0f) return (Element)i;
        }
        return loadout.DominantElement ?? Element.Fire;
    }

    // Rare, and at most maxScrollsPerRun a run; one on the field at a time, and none once every art is held. Never from
    // the round's last samurai, like souls.
    void MaybeDropScroll(Vector3 position, float chance)
    {
        if (activeScroll != null || scrollsDropped >= maxScrollsPerRun || army.Remaining == 0) return;
        if (Random.value >= chance || Boons.DrawArts(loadout, 1).Count == 0) return;
        scrollsDropped++;
        SpawnScroll(position);
    }

    void SpawnScroll(Vector3 position)
    {
        activeScroll = TechniqueScroll.Spawn(position, scrollSprite, ronin, OnScrollCollected);
        // The first scroll of a run carries the gold arrow, like the first soul.
        if (scrollsDropped <= 1) activeScroll.ShowHint(soulHintArrow);
    }

    void OnScrollCollected(TechniqueScroll scroll)
    {
        if (state == State.Transition)
        {
            gatheredScroll = true;
        }
        else if (state != State.Playing || !OfferArts(ResumePlay))
        {
            return;
        }
        Fx.Play(Fx.Impact, scroll.transform.position, Boons.ArtColor, 1.8f);
        Sfx.Play(Sfx.SoulCollect, 1f, 0.8f);
    }

    // The Sword Arts screen: up to three arts the ronin doesn't hold, like Daedalus's hammer in Hades.
    bool OfferArts(System.Action afterPick)
    {
        var offers = Boons.DrawArts(loadout, 3);
        if (offers.Count == 0) return false;

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        boonChoice.ShowArts(offers, scrollSprite, index =>
        {
            TakeBoon(offers[index]);
            Time.timeScale = 1f;
            afterPick();
        });
        return true;
    }

    // Pauses the game and offers three boons of the soul's element, like a god's boon in Hades. One gathered at the
    // end of a round waits for GatherDrops to offer it.
    void OnSoulCollected(Soul soul)
    {
        if (state == State.Transition)
        {
            gatheredSoul = soul.Element;
        }
        else if (state != State.Playing || !OfferBoons(soul.Element, ResumePlay))
        {
            return;
        }
        soulCaught = true;
        Fx.Play(Fx.Impact, soul.transform.position, Boons.Info(soul.Element).Color, 1.6f);
        Sfx.Play(Sfx.SoulCollect);
    }

    void OnBlockerBroken(Vector3 position)
    {
        if (state != State.Playing || Random.value >= heartDropChance) return;
        Heart.Spawn(position, ui.LifeIcon, ronin, CanHeal, OnHeartCollected);
    }

    // Hearts only heal: at full health they wait on his row until he's lost a life.
    bool CanHeal() => state != State.GameOver && lives < maxLives;

    void OnHeartCollected(Heart heart)
    {
        lives++;
        ui.SetLives(lives, maxLives);
        Fx.Play(Fx.Impact, heart.transform.position, new Color(1f, 0.35f, 0.4f), 1.4f);
        Sfx.Play(Sfx.SoulCollect, 0.8f, 1.3f);
    }

    // At the end of a round every soul still out, and every heart he can use, flies to the ronin. A soul caught on
    // the way offers its boons before the game moves on; spare hearts stay on his row for later.
    IEnumerator GatherDrops()
    {
        Pickup.GatherAll();
        float giveUp = Time.time + 2f;
        while (Pickup.Gathering > 0 && Time.time < giveUp) yield return null;
        Pickup.StopGathering();

        bool picked = false;
        if (gatheredSoul is Element element)
        {
            gatheredSoul = null;
            if (OfferBoons(element, () => picked = true))
            {
                yield return new WaitUntil(() => picked);
                state = State.Transition;
            }
        }
        if (gatheredScroll)
        {
            gatheredScroll = false;
            picked = false;
            if (OfferArts(() => picked = true))
            {
                yield return new WaitUntil(() => picked);
                state = State.Transition;
            }
        }
    }

    void ResumePlay()
    {
        state = State.Playing;
        ronin.ControlsEnabled = true;
    }

    // Pauses on the boon screen and runs afterPick once a boon is taken. An element with nothing left to give
    // offers a Spirit Offering instead. False (and nothing shown) only when nothing could be drawn.
    bool OfferBoons(Element element, System.Action afterPick, bool ensureCapstone = false, Rarity minRarity = Rarity.Common)
    {
        var offers = Boons.Draw(element, 3, loadout, maxLives, ensureCapstone, minRarity);
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
        Color colour = offer.Boon.Kind == BoonKind.Art ? Boons.ArtColor : Boons.Info(offer.Boon.Element).Color;
        Color pillar = Color.Lerp(Color.white, colour, 0.7f);
        Fx.Play(Fx.BoonPillar, ronin.Feet, new Color(pillar.r, pillar.g, pillar.b, 0.75f), offer.Boon.Id == BoonId.TidalRenewal ? 1.4f : 1f);
        switch (offer.Boon.Id)
        {
            case BoonId.TidalRenewal:
                // New hearts arrive full; the ones already lost stay empty.
                int gained = Mathf.Min((int)value, Boons.MaxLives - maxLives);
                maxLives += gained;
                lives += gained;
                // From Rare, the pond also mends every life already lost.
                if (offer.Rarity >= Rarity.Rare) lives = maxLives;
                ui.SetLives(lives, maxLives);
                break;
            case BoonId.SpiritOffering:
                if (lives < maxLives)
                {
                    lives++;
                    ui.SetLives(lives, maxLives);
                }
                else
                {
                    score += OfferingPoints * (stageIndex + 1);
                    highScore = Mathf.Max(highScore, score);
                    ui.SetScore(score);
                }
                break;
            case BoonId.GlacierWall:
                blockers.Rebuild(loadout.BlockerHealthMultiplier);
                foreach (Vector3 spot in blockers.BlockerPositions)
                {
                    Fx.Play(Fx.IceBurst, spot, Color.white, 1.3f);
                }
                break;
        }
        army.SpeedUpScale = loadout.MarchSpeedUpScale;
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
        StartCoroutine(EndRound());
    }

    IEnumerator EndRound()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        IceStatue.CrumbleAll();
        yield return GatherDrops();

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

    // The stage's reward: a Duo or Legendary when one is within reach, like a boss chest evolving a weapon, and
    // never below Rare.
    void OfferStageBoon()
    {
        List<BoonDefinition> capstones = Boons.EligibleCapstones(loadout);
        Element element = PickSoulElement();
        if (capstones.Count > 0)
        {
            BoonDefinition capstone = capstones[Random.Range(0, capstones.Count)];
            element = capstone.Partner is Element partner && Random.value < 0.5f ? partner : capstone.Element;
        }
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(element).Color, 1.4f);
        Sfx.Play(Sfx.SoulCollect);
        if (!OfferBoons(element, NextStage, true, Rarity.Rare)) NextStage();
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
        // A moon rises over the duel: blood red for Benibana, cold steel for Utsusemi, spirit blue for Shirogane; for the
        // Emperor none, only a starless dark lit by his soul-fire.
        if (backdrop != null) backdrop.SetBossMoon(true, Stage.boss switch
        {
            BossKind.Utsusemi => Backdrop.MoonLook.Steel,
            BossKind.Shirogane => Backdrop.MoonLook.Spirit,
            BossKind.Kagetsune => Backdrop.MoonLook.Soul,
            _ => Backdrop.MoonLook.Blood,
        });
        yield return new WaitForSeconds(1f);

        Camera cam = Camera.main;
        float top = CameraShake.Center.y + cam.orthographicSize;
        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 post = new Vector3(CameraShake.Center.x, top - bossDepth - (Stage.boss == BossKind.Kagetsune ? kagetsuneDrop : 0f), 0);
        for (int i = 0; i < 7; i++)
        {
            Vector3 puff = new Vector3(post.x + Mathf.Lerp(-halfWidth, halfWidth, i / 6f), post.y + Random.Range(-0.5f, 1.5f), 0);
            Fx.Play(Fx.SmokePuff, puff, new Color(0.7f, 0.68f, 0.72f, 0.8f), 3f);
        }
        Sfx.Play(Sfx.FrostChill, 0.8f, 0.5f);

        activeBoss = CreateBoss(Stage.boss, post + Vector3.up * 5f);
        TuneVoice(Stage.boss);
        activeBoss.Damaged += OnBossDamaged;
        Boss boss = activeBoss;
        boss.Healed += () => ui.SetBossHealth(boss.BarFraction);
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
        // A boss with second-phase portraits gets a card for its arrival, played in its phase break.
        Boss.PhaseCard = lines.phasePortrait != null && lines.phaseCloseup != null ? PlayPhaseCard : null;
        if (sfxLibrary != null) Music.Play(sfxLibrary.battleMusic, sfxLibrary.musicVolume);
        state = State.Playing;
        ronin.ControlsEnabled = true;
        activeBoss.Begin(ronin, army.samuraiPrefab.arrowPrefab);
    }

    // The second-phase title card: the same cinematic as the intro with the phase art, epithet and line. The music
    // ducks under it as it would under the spoken line.
    IEnumerator PlayPhaseCard(Boss boss)
    {
        BossVoice lines = VoiceFor(Stage.boss);
        AudioClip clip = lines.phase;
        Music.Duck(0.3f, (clip != null ? clip.length : 3f) + 2.5f);
        voice.volume = voiceVolume * Sfx.Volume;
        // The wash behind the art follows the transformed painting where its colours change (Utsusemi's molten core).
        Color wash = lines.phaseColour.a > 0f ? lines.phaseColour : boss.Colour;
        yield return ui.PlayBossIntro(lines.phasePortrait, lines.phaseCloseup, lines.phaseCloseupHeight, lines.phaseCloseupFocus,
            lines.phaseCloseupAt, boss.Title, lines.phaseEpithet, wash, voice, clip, boss.Line(BossLine.Phase));
        // The bar carries the name the card just announced.
        ui.RenameBossBar($"{boss.PhaseTitle}, {lines.phaseEpithet}");
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
                    automaton.SetPhaseArt(utsusemiPhaseArt);
                    automaton.SetPhaseFrames(utsusemiPhaseBareIdle, utsusemiPhaseUnarmedIdle, utsusemiPhaseDash, utsusemiPhaseChop,
                        utsusemiPhaseHourSlash, utsusemiPhaseSwordThrow);
                }
                return automaton;
            case BossKind.Kagetsune:
                bool hollow = kagetsuneArt == null;
                Kagetsune emperor = Boss.Create<Kagetsune>(hollow ? army.samuraiByRow[0] : kagetsuneArt, kagetsuneScale, Kagetsune.StartingHealth,
                    position, hollow ? new Color(0.55f, 0.42f, 0.32f) : Color.white);
                if (!hollow) emperor.SetPhaseArt(kagetsunePhaseArt);
                // The souls he can drag out of the coffin, drawn in their own art at their own size.
                emperor.SetEcho(BossKind.Benibana, benibanaPhaseArt != null ? benibanaPhaseArt : benibanaArt, benibanaScale);
                emperor.SetEcho(BossKind.Utsusemi, utsusemiArt, utsusemiScale);
                emperor.SetEcho(BossKind.Shirogane, shiroganeArt, shiroganeScale);
                return emperor;
            case BossKind.Shirogane:
                bool ghostly = shiroganeArt == null;
                Shirogane blade = Boss.Create<Shirogane>(ghostly ? army.samuraiByRow[Mathf.Min(1, army.samuraiByRow.Length - 1)] : shiroganeArt,
                    shiroganeScale, Shirogane.StartingHealth, position, ghostly ? new Color(0.6f, 0.9f, 1f) : Color.white);
                if (!ghostly)
                {
                    blade.SetDashFrames(shiroganeDash);
                    blade.SetPhaseArt(shiroganePhaseArt);
                    blade.SetPhaseDash(shiroganePhaseDash);
                }
                blade.SetWolfFrames(shiroWolfIdle, shiroWolfHowl, shiroWolfCharge, shiroWolfVanish, shiroWolfMaw);
                blade.WolfJoined += OnWolfJoined;
                return blade;
            default:
                bool standIn = benibanaArt == null;
                CharacterArt art = standIn ? army.samuraiByRow[0] : benibanaArt;
                Color tint = standIn ? new Color(1f, 0.55f, 0.6f) : Color.white;
                Benibana warlord = Boss.Create<Benibana>(art, benibanaScale, Benibana.StartingHealth, position, tint);
                if (!standIn) warlord.SetPhaseArt(benibanaPhaseArt);
                return warlord;
        }
    }

    struct BossVoice
    {
        public Sprite portrait, closeup;
        // How the intro frames the close-up: its height on screen, the point of the art to centre on (0-1 across and
        // down), and where that point sits on screen.
        public float closeupHeight;
        public Vector2 closeupFocus, closeupAt;
        // A second card for the boss's second phase, framed the same way; both sprites or none.
        public Sprite phasePortrait, phaseCloseup;
        public string phaseEpithet;
        // The phase card's colour wash, when the transformed art needs a different one; clear keeps the boss's colour.
        public Color phaseColour;
        public float phaseCloseupHeight;
        public Vector2 phaseCloseupFocus, phaseCloseupAt;
        public AudioClip intro, phase, defeat, defeatFinal, stagger, kasaWheel, hourStrikes, crescentSlash, bladeRain, eclipseFang, kuonEnraged;
        // Kagetsune's callouts and summons, and the summoned souls' lines.
        public AudioClip killedOni, cofferSlam, toll, ofuda, wail, lastToll, swarm, chains, coffin, thousandBells,
            summonBenibana, summonUtsusemi, summonShirogane, echoBenibana, echoUtsusemi, echoShirogane;
        // Lines with several takes: one is picked at random, never the one played last time.
        // A boss with stagger takes uses them instead of its single stagger clip.
        public AudioClip[] hurt, staggers, smokeRings, rosaryLash, iaido, rosaryBloom;

        // The clip for a line and which take it is (0 for a single clip), so the subtitle can match it.
        public AudioClip Take(BossLine line, int last, out int take)
        {
            take = 0;
            AudioClip[] takes = line switch
            {
                BossLine.Hurt => hurt,
                BossLine.Stagger => staggers,
                BossLine.SmokeRings => smokeRings,
                BossLine.RosaryLash => rosaryLash,
                BossLine.Iaido => iaido,
                BossLine.RosaryBloom => rosaryBloom,
                _ => null,
            };
            if (takes == null) return line switch
            {
                BossLine.Intro => intro,
                BossLine.Phase => phase,
                BossLine.Defeat => defeat,
                BossLine.Stagger => stagger,
                BossLine.KasaWheel => kasaWheel,
                BossLine.HourStrikes => hourStrikes,
                BossLine.DefeatFinal => defeatFinal,
                BossLine.CrescentSlash => crescentSlash,
                BossLine.BladeRain => bladeRain,
                BossLine.EclipseFang => eclipseFang,
                BossLine.KuonEnraged => kuonEnraged,
                BossLine.KilledOni => killedOni,
                BossLine.CofferSlam => cofferSlam,
                BossLine.TollOfFealty => toll,
                BossLine.OfudaDecree => ofuda,
                BossLine.SoulWail => wail,
                BossLine.LastToll => lastToll,
                BossLine.WraithSwarm => swarm,
                BossLine.BrokenChains => chains,
                BossLine.SoulCoffin => coffin,
                BossLine.ThousandBells => thousandBells,
                BossLine.SummonBenibana => summonBenibana,
                BossLine.SummonUtsusemi => summonUtsusemi,
                BossLine.SummonShirogane => summonShirogane,
                BossLine.EchoBenibana => echoBenibana,
                BossLine.EchoUtsusemi => echoUtsusemi,
                BossLine.EchoShirogane => echoShirogane,
                _ => null,
            };
            if (takes.Length == 0) return null;
            take = Random.Range(0, takes.Length);
            if (takes.Length > 1 && take == last) take = (take + 1 + Random.Range(0, takes.Length - 1)) % takes.Length;
            return takes[take];
        }
    }

    BossVoice VoiceFor(BossKind kind) => kind switch
    {
        BossKind.Benibana => new BossVoice
        {
            portrait = benibanaPortrait, closeup = benibanaCloseup,
            // Each close-up is framed on the face, on the centre line and high enough to clear the title block, big
            // enough that the painting's edges sit off screen.
            closeupHeight = 1500f, closeupFocus = new Vector2(0.47f, 0.27f), closeupAt = new Vector2(0f, 200f),
            // Unsealed: framed on her face with her loose hair and the drawn blade around her, held back far enough to
            // see her shoulders; raised a touch so the painting's top edge stays off screen.
            phasePortrait = benibanaPhasePortrait, phaseCloseup = benibanaPhaseCloseup, phaseEpithet = Benibana.PhaseEpithet,
            phaseCloseupHeight = 1440f, phaseCloseupFocus = new Vector2(0.46f, 0.22f), phaseCloseupAt = new Vector2(-10f, 265f),
            intro = benibanaIntroLine, phase = benibanaPhaseLine, defeat = benibanaDefeatLine,
            staggers = benibanaStaggerLines, smokeRings = benibanaSmokeRingsLines, rosaryLash = benibanaRosaryLashLines,
            iaido = benibanaIaidoLines, rosaryBloom = benibanaRosaryBloomLines, hurt = benibanaHurtLines,
        },
        BossKind.Utsusemi => new BossVoice
        {
            portrait = utsusemiPortrait, closeup = utsusemiCloseup,
            // Framed on its slit-eye under the hat brim, the brim running off the top right.
            closeupHeight = 1600f, closeupFocus = new Vector2(0.52f, 0.27f), closeupAt = new Vector2(0f, 230f),
            // Overdriven: framed between its slit-eye and the open core, the broken hat ringing the top.
            phasePortrait = utsusemiPhasePortrait, phaseCloseup = utsusemiPhaseCloseup, phaseEpithet = Utsusemi.PhaseEpithet,
            // Pulled back as far as the painting allows: its left edge limits it, so the slit-eye sits a little left.
            phaseCloseupHeight = 1380f, phaseCloseupFocus = new Vector2(0.45f, 0.25f), phaseCloseupAt = new Vector2(-70f, 235f),
            phaseColour = new Color(1f, 0.45f, 0.12f),
            intro = utsusemiIntroLine, phase = utsusemiPhaseLine, defeat = utsusemiDefeatLine,
            stagger = utsusemiStaggerLine, kasaWheel = utsusemiKasaLine, hourStrikes = utsusemiHourStrikesLine, hurt = utsusemiHurtLines,
        },
        BossKind.Shirogane => new BossVoice
        {
            portrait = shiroganePortrait, closeup = shiroganeCloseup,
            // Framed on her face, her hair spilling off the top and right.
            // Taller than the others, since her face sits so high in the painting, so its top edge stays off screen.
            closeupHeight = 1350f, closeupFocus = new Vector2(0.48f, 0.41f), closeupAt = new Vector2(0f, 170f),
            // The oath card: the pair centred, her face left of the middle and the wolf's head burning to the right.
            phasePortrait = shiroganePhasePortrait, phaseCloseup = shiroganePhaseCloseup, phaseEpithet = Shirogane.OathEpithet,
            phaseCloseupHeight = 1340f, phaseCloseupFocus = new Vector2(0.52f, 0.3f), phaseCloseupAt = new Vector2(40f, 180f),
            intro = shiroganeIntroLine, phase = shiroganePhaseLine, defeat = shiroganeDefeatLine, defeatFinal = shiroganeDefeatFinalLine,
            stagger = shiroganeStaggerLine, crescentSlash = shiroganeCrescentLine, bladeRain = shiroganeBladeRainLine,
            eclipseFang = shiroganeEclipseLine, kuonEnraged = shiroganeKuonEnragedLine, hurt = shiroganeHurtLines,
        },
        BossKind.Kagetsune => new BossVoice
        {
            portrait = kagetsunePortrait, closeup = kagetsuneCloseup,
            // Framed on the horned helm and its visor slit, the coffin-hammer's head running off to the right.
            closeupHeight = 1500f, closeupFocus = new Vector2(0.48f, 0.3f), closeupAt = new Vector2(0f, 200f),
            // The Wraith: framed on the helm and the howling skull fire, washed in the souls' teal.
            phasePortrait = kagetsunePhasePortrait, phaseCloseup = kagetsunePhaseCloseup, phaseEpithet = Kagetsune.PhaseEpithet,
            phaseCloseupHeight = 1400f, phaseCloseupFocus = new Vector2(0.5f, 0.32f), phaseCloseupAt = new Vector2(0f, 190f),
            phaseColour = new Color(0.3f, 1f, 0.82f),
            intro = kagetsuneIntroLine, phase = kagetsunePhaseLine, defeat = kagetsuneDefeatLine, stagger = kagetsuneStaggerLine,
            killedOni = kagetsuneKilledOniLine, cofferSlam = kagetsuneCofferSlamLine, toll = kagetsuneTollLine, ofuda = kagetsuneOfudaLine,
            wail = kagetsuneWailLine, lastToll = kagetsuneLastTollLine, swarm = kagetsuneSwarmLine, chains = kagetsuneChainsLine,
            coffin = kagetsuneCoffinLine, thousandBells = kagetsuneThousandBellsLine, summonBenibana = kagetsuneSummonBenibanaLine,
            summonUtsusemi = kagetsuneSummonUtsusemiLine, summonShirogane = kagetsuneSummonShiroganeLine,
            echoBenibana = echoBenibanaLine, echoUtsusemi = echoUtsusemiLine, echoShirogane = echoShiroganeLine,
            hurt = kagetsuneHurtLines,
        },
        _ => default,
    };

    // Mid-fight lines (her phase break, her defeat) play over the music, which dips under them. A bark (move
    // callout, gasp) never cuts off anything still playing: it's dropped, since a callout played late would name
    // the wrong move. Only a stagger cries over another bark.
    void OnBossSpoke(Boss boss, BossLine line)
    {
        // The Emperor's summons and the souls' answers are story, not barks: a gasp mustn't swallow them.
        bool full = line == BossLine.Intro || line == BossLine.Phase || line == BossLine.Defeat || line == BossLine.DefeatFinal
            || line == BossLine.KuonEnraged
            || line is BossLine.SummonBenibana or BossLine.SummonUtsusemi or BossLine.SummonShirogane
                or BossLine.EchoBenibana or BossLine.EchoUtsusemi or BossLine.EchoShirogane;
        if (!full && voice.isPlaying && (speakingFullLine || line != BossLine.Stagger)) return;

        AudioClip clip = VoiceFor(Stage.boss).Take(line, lastTakes.TryGetValue(line, out int last) ? last : -1, out int take);
        lastTakes[line] = take;
        string text = boss.Line(line, take);
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
        if (!string.IsNullOrEmpty(text)) ui.ShowSubtitle(boss.Speaker(line), text, boss.SpeakerColour(line), seconds + 0.6f);
    }

    void OnBossDamaged(Vector3 point, KillSource source)
    {
        score += 10 * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        ui.SetBossHealth(activeBoss.BarFraction);
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

    // Kuon takes the field at her phase break: it shares her bar, and it can be hit until it breaks.
    void OnWolfJoined(SpiritWolf wolf)
    {
        activeWolf = wolf;
        effects.Wolf = wolf;
        wolf.Damaged += OnWolfDamaged;
        wolf.Broken += OnWolfBroken;
    }

    void OnWolfDamaged(Vector3 point, KillSource source)
    {
        score += 10 * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        if (activeBoss != null) ui.SetBossHealth(activeBoss.BarFraction);
        if (state != State.Playing || source == KillSource.Effect) return;

        if (source == KillSource.Reflected)
        {
            // The Fang of the Oath sent home: a heavy hit-stop as it slams into the wolf.
            Debris.BurstSprites(point, ronin.art.deathParticles, activeBoss.Colour, 24, 1.2f, 8f);
            Fx.Play(Fx.Impact, point, activeBoss.Colour, 2.5f);
            CameraShake.Add(0.45f);
            if (!hitStopping) StartCoroutine(HitStop(0.15f));
        }
        else
        {
            Color spark = source == KillSource.Shuriken ? ronin.ShurikenGlow : source == KillSource.Deflected ? ronin.ParryColor : ronin.AttackGlow;
            Fx.Play(Fx.Impact, point + (Vector3)Random.insideUnitCircle * 0.6f, Color.Lerp(Color.white, spark, 0.6f), 1.2f);
        }
        effects.OnWolfHit(point, source);
    }

    // Kuon is broken: its share of the bar is gone, and she fights on enraged (or, if she was already down, falls).
    void OnWolfBroken()
    {
        score += 750 * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        if (activeBoss != null) ui.SetBossHealth(activeBoss.BarFraction);
        if (!hitStopping) StartCoroutine(HitStop(0.2f));
        CameraShake.Add(0.5f);
        effects.Wolf = null;
        activeWolf = null;
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
        // working through the blow that felled her. Each boss's fall takes its own time (Shirogane's is a scene).
        yield return new WaitForSeconds(activeBoss.DefeatSeconds);
        // The Emperor is the end: no boon, the dawn.
        if (Stage.boss == BossKind.Kagetsune && stageIndex == stages.Length - 1)
        {
            yield return PlayDawn(where);
            yield break;
        }
        yield return GatherDrops();
        effects.Boss = null;
        effects.Wolf = null;
        activeBoss = null;
        activeWolf = null;
        Boss.PhaseCard = null;
        // Her fall lifts the blood moon.
        if (backdrop != null) backdrop.SetBossMoon(false);

        var element = (Element)Random.Range(0, 3);
        ElementFx.Glow(where, Boons.Info(element).Color, 1f, 5f, 1f);
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(element).Color, 1.4f);
        Sfx.Play(Sfx.SoulCollect);
        yield return new WaitForSeconds(0.6f);
        if (!OfferBoons(element, NextStage)) NextStage();
    }

    // The true ending. The bells are silent and the stopped hour moves: the sky warms over the field for the first
    // time in the game, Oni walks out into it and plays the bone flute, and the second spirit wisp beside him is
    // Shirogane's. Then the end screen.
    // The ending. Her bell, the one that never rang, lies where the Emperor fell, and Shirogane's soul is still
    // chained to it. Oni walks up and cuts the chain, as she once cut the tether dragging him into the armor; her
    // bell rings once and goes silent, she rises as herself, and the stopped hour moves. Dawn comes up while he
    // plays the flute beside her, and she says goodbye and becomes the wisp at his shoulder.
    IEnumerator PlayDawn(Vector3 throne)
    {
        effects.Boss = null;
        activeBoss = null;
        Boss.PhaseCard = null;
        if (backdrop != null) backdrop.SetBossMoon(false);

        Vector3 herFeet = new Vector3(CameraShake.Center.x, Mathf.Lerp(ronin.Home.y, throne.y, 0.4f), 0f);
        // The bell lies up the field toward where he fell, close enough that the chain reads as hers.
        Vector3 bellAt = new Vector3(herFeet.x + Mathf.Clamp(throne.x - herFeet.x, -2.5f, 2.5f), herFeet.y + 4f, 0f);
        var her = ShiroganeFreed.Create(herFeet, bellAt, ronin.ArtPixelSize, ronin.SpriteOrder, freedKneel, freedBow, freedIdle, shiroganeArt);
        yield return her.Appear(1.6f);
        yield return new WaitForSeconds(0.8f);

        // He walks up beside her and cuts the chain.
        float feetToBody = ronin.transform.position.y - ronin.ArtBounds.min.y;
        Vector3 beside = new Vector3(herFeet.x - 1.6f, herFeet.y + feetToBody, ronin.Home.z);
        yield return ronin.WalkTo(beside, travelWalkSpeed * 0.35f);
        yield return new WaitForSeconds(0.4f);
        ronin.Swing();
        yield return new WaitForSeconds(0.12f);
        her.Snap();
        if (!hitStopping) StartCoroutine(HitStop(0.12f));
        yield return new WaitForSeconds(1.4f);

        // She rises as herself and speaks in her own voice, not the Emperor's choir.
        TuneVoice(BossKind.Shirogane);
        yield return her.Rise(1.6f);
        yield return Say("Shirogane", FreedColour, shiroganeFreedLine, "You cut it... the way I cut yours.");

        // The clock that held the hour starts again, and the sun comes up.
        if (backdrop != null) backdrop.SetDawn(true);
        for (int i = 0; i < 3; i++)
        {
            Sfx.Play(Sfx.ClockTick, 0.7f, 0.8f);
            yield return new WaitForSeconds(0.6f);
        }
        if (Sfx.Has(Sfx.DawnChime)) Sfx.Play(Sfx.DawnChime);
        else Sfx.Play(Sfx.VictorySting, 0.8f, 0.8f);
        if (sfxLibrary != null) Music.Play(sfxLibrary.titleMusic, sfxLibrary.musicVolume);
        yield return new WaitForSeconds(1.5f);
        yield return Say("Shirogane", FreedColour, shiroganeFarewellLine, "The oath is kept. Play for me, Oni... one last time.");
        ronin.Pose(titleFluteFrames, titleFluteFps);
        yield return new WaitForSeconds(1f);
        yield return her.Bow();
        Vector3 wispFrom = her.Centre;
        StartCoroutine(her.Dissolve(1.6f));
        StartCoroutine(SecondWisp(wispFrom));
        yield return new WaitForSeconds(2.5f);

        ui.ShowBanner("The bells are silent", "Dawn", "The stopped hour moves, and the souls go home.", new Color(1f, 0.72f, 0.45f));
        yield return new WaitForSeconds(5f);
        ui.HideBanner();
        yield return new WaitForSeconds(1f);
        EndGame("DAWN", "Kagetsune has fallen and his bells are silent. The sun rises over the hill,\nand Oni plays the flute one last time, with Shirogane beside him.",
            new Color(1f, 0.72f, 0.45f), Sfx.VictorySting);
    }

    // Her silver, as herself rather than the Emperor's teal.
    static readonly Color FreedColour = new Color(0.72f, 0.9f, 1f);

    // A line spoken outside a fight (the ending): voiced when the clip is there, always subtitled, waited out.
    IEnumerator Say(string speaker, Color colour, AudioClip clip, string text)
    {
        float seconds = clip != null ? clip.length : 2.5f;
        if (clip != null)
        {
            voice.Stop();
            voice.volume = voiceVolume * Sfx.Volume;
            voice.clip = clip;
            voice.Play();
            Music.Duck(0.3f, seconds + 0.3f);
        }
        ui.ShowSubtitle(speaker, text, colour, seconds + 0.6f);
        yield return new WaitForSeconds(seconds + 0.5f);
    }

    // Shirogane's soul, free at last: her spirit wisp rises from where she stood and settles at his shoulder.
    IEnumerator SecondWisp(Vector3 from)
    {
        Sprite[] frames = Fx.Frames(Fx.ShiroSpiritWisp);
        var wisp = new GameObject("ShiroganeWisp").AddComponent<SpriteRenderer>();
        wisp.sprite = frames != null ? frames[0] : ProceduralSprites.Glow;
        wisp.sortingOrder = ronin.SpriteOrder + 2;
        if (frames != null) wisp.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 10f);
        var glow = new GameObject("ShiroganeWispGlow").AddComponent<SpriteRenderer>();
        glow.sprite = ProceduralSprites.SoftGlow;
        glow.sortingOrder = ronin.SpriteOrder + 1;
        glow.transform.SetParent(wisp.transform, false);
        glow.transform.localScale = Vector3.one * 1.4f;
        float size = frames != null ? 1.2f : 0.9f;
        for (float t = 0; wisp != null; t += Time.deltaTime)
        {
            float arrive = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 2.5f));
            Vector3 shoulder = ronin.Bounds.center + new Vector3(0.9f, 0.6f + Mathf.Sin(t * 2.2f) * 0.15f, -0.2f);
            // It rises a little first, then curves down onto his shoulder.
            Vector3 lift = from + Vector3.up * 1.2f;
            wisp.transform.position = Vector3.Lerp(Vector3.Lerp(from, lift, arrive), Vector3.Lerp(lift, shoulder, arrive), arrive);
            wisp.transform.localScale = Vector3.one * size * (0.9f + 0.1f * Mathf.Sin(t * 5f));
            wisp.color = new Color(0.85f, 0.95f, 1f, 0.9f * Mathf.Clamp01(t / 0.6f));
            glow.color = new Color(0.7f, 0.9f, 1f, 0.3f * Mathf.Clamp01(t / 0.6f));
            yield return null;
        }
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
        ui.SetLives(lives, maxLives);
        if (lives <= 0)
        {
            ronin.Explode();
            // The Emperor has the last word over him.
            string message = "The ronin has fallen.";
            if (activeBoss is Kagetsune emperor)
            {
                OnBossSpoke(emperor, BossLine.KilledOni);
                message = $"\"{emperor.Line(BossLine.KilledOni)}\"";
            }
            EndGame("DEFEATED", message, new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting);
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

    // Hearts are kept: they wait on the ronin's row until he needs one.
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
