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
    [Tooltip("The painted soul he holds on the boon screen, drawn for the soul who caught it. Empty uses soulFrames.")]
    public Sprite heldSoul;
}

// The icon a boss's portal shows: something of theirs (Benibana's kiseru, Utsusemi's hat, Kuon's head, Kagetsune's helm).
[System.Serializable]
public class BossIcon
{
    public BossKind boss;
    public Sprite[] frames;
}

// Owns the game flow: title menu -> stages of rounds -> win/lose menu, plus pause, score, lives, souls and boons.
public partial class GameManager : MonoBehaviour
{
    enum State { Title, Transition, Playing, Choosing, Paused, GameOver, Dialogue, Hub }

    const string HighScoreKey = "HighScore";
    // A Spirit Offering taken at full health gives these points, times the stage number.
    const int OfferingPoints = 500;
    // A spare heart left at a round's end fades into these points, times the stage number.
    const int SpareHeartPoints = 250;
    // Spirit Notes a Spirit Bead gives per stage when every boon held is already Epic.
    const int BeadNotes = 20;
    static bool skipTitle;
    // The next scene load opens in the safe area instead of the title, after a death (he wakes in the pool) or a load.
    static bool intoSafeArea;
    static bool wakeInPool;
    // What the run just ended earned, shown as he wakes.
    static int lastRunNotes;
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
        intoSafeArea = wakeInPool = false;
        lastRunNotes = 0;
        godMode = false;
        devJumpStage = -1;
        devJumpBoss = false;
    }

    [Header("Inscribed")]
    [SerializeField, FormerlySerializedAs("fleet")] SamuraiArmy army;
    [SerializeField, FormerlySerializedAs("ship")] Ronin ronin;
    [SerializeField, FormerlySerializedAs("shields")] BlockerWall blockers;
    [SerializeField] GameUI ui;
    [SerializeField] int startingLives = 2;
    [SerializeField] float levelIntroTime = 2.5f;
    [Tooltip("Keep DEV TOOLS and the F1-F4 hotkeys in release builds, for friends play-testing. Development builds always have them.")]
    [SerializeField] bool devToolsInRelease = true;

    [Header("Souls and boons")]
    [Tooltip("Built on the Canvas at runtime if left empty.")]
    [SerializeField] BoonChoiceUI boonChoice;
    [Tooltip("Chance a downed kite scout drops a soul. Most souls come from the portals' rewards.")]
    [SerializeField, Range(0, 1)] float scoutSoulChance = 0.2f;
    [Tooltip("Chance any fallen samurai drops a soul: a rare stroke of luck on top of the portals.")]
    [SerializeField, Range(0, 1)] float killSoulChance = 0.01f;
    [Tooltip("Most souls that can drop from samurai kills in one stage.")]
    [SerializeField] int killSoulsPerStage = 1;
    [Tooltip("Once the ronin holds a boon, the chance a soul takes one of his elements (weighted by how many boons of each he holds) instead of a random one.")]
    [SerializeField, Range(0, 1)] float soulElementBias = 0.5f;
    [Tooltip("Arrow bobbing over the run's souls until the first one is caught. Empty draws a plain gold arrow.")]
    [SerializeField] Sprite soulHintArrow;

    [Header("Portals (Hades' chamber doors)")]
    [Tooltip("Most rounds a stage can run to; after the last only the boss is left.")]
    [SerializeField] int maxRounds = 5;
    [Tooltip("The boss portal opens among the others once this many rounds of the stage are cleared.")]
    [SerializeField] int bossPortalAfterRound = 3;
    [Tooltip("Portals to choose from after each round, besides the boss's.")]
    [SerializeField] int portalChoices = 2;
    [Tooltip("Spirit Notes a Lost Melody portal gives, times the stage number.")]
    [FormerlySerializedAs("emberCache")]
    [SerializeField] int noteCache = 30;
    [Tooltip("Rounds past a stage's last configured one repeat it, this much faster (speed) per extra round.")]
    [SerializeField] float extraRoundSpeedUp = 0.06f;
    [Tooltip("...and with this much less time between arrows per extra round.")]
    [SerializeField] float extraRoundFireCut = 0.08f;

    [Header("Technique scrolls (Sword Arts)")]
    [Tooltip("Chance a downed kite scout also drops a technique scroll.")]
    [SerializeField, Range(0, 1)] float scrollChanceFromScout = 0.25f;
    [Tooltip("Chance a downed kite scout leaves a Kite Silk, which the upper ranks of some Flute Songs ask for.")]
    [SerializeField, Range(0, 1)] float silkChanceFromScout = 0.35f;
    [Tooltip("Chance any fallen samurai drops a technique scroll.")]
    [SerializeField, Range(0, 1)] float scrollChancePerKill = 0f;
    [Tooltip("Most technique scrolls that can drop in one run, like Hades' two Daedalus Hammers.")]
    [SerializeField] int maxScrollsPerRun = 2;
    [Tooltip("The technique scroll pickup's loop, also its portal's icon. Wired by Ninja/Apply Boon Art; empty draws a parchment bar on a gold glow.")]
    [SerializeField] Sprite[] scrollFrames;
    [Tooltip("The painted scroll held out on the Sword Arts screen. Empty uses scrollFrames.")]
    [SerializeField] Sprite scrollPainted;
    [Tooltip("The Spirit Bead portal's icon, looped. Empty: a glow in the bead's colour.")]
    [SerializeField] Sprite[] beadFrames;
    [Tooltip("The painted bead held out on the Spirit Bead screen. Empty uses beadFrames.")]
    [SerializeField] Sprite beadPainted;
    [Tooltip("The Spirit Thread portal's icon and the thing held out on its screen, looped. Empty: a glow in the thread's colour.")]
    [SerializeField] Sprite[] threadFrames;
    [Tooltip("The Lost Melody portal's icon, looped.")]
    [FormerlySerializedAs("emberFrames")]
    [SerializeField] Sprite[] noteFrames;
    [Tooltip("The healing spring portal's icon, looped. Empty uses the life icon.")]
    [SerializeField] Sprite[] springFrames;
    [Tooltip("Each boss portal's icon, by the boss waiting behind it.")]
    [SerializeField] BossIcon[] bossIcons;
    [Tooltip("Chance that a blocker worn down by hits drops a heart, which heals a lost life.")]
    [SerializeField, Range(0, 1)] float heartDropChance = 0.2f;
    [Tooltip("Chance a fallen samurai drops a heart while the ronin is hurt (never from boon effects); at most one a stage.")]
    [SerializeField, Range(0, 1)] float killHeartChance = 0.005f;
    [Tooltip("The ronin sitting on his row playing his flute, looped on the title screen. Empty shows his idle.")]
    [SerializeField] Sprite[] titleFluteFrames;
    [Tooltip("Frames per second for the title screen's flute loop.")]
    [SerializeField] float titleFluteFps = 4f;
    [Tooltip("The ronin putting his flute away and getting up, played once when START is chosen. Empty skips to the walk.")]
    [SerializeField] Sprite[] titleStandFrames;
    [Tooltip("Frames per second for the stand-up.")]
    [SerializeField] float titleStandFps = 8f;
    [Tooltip("Oni's painted close-up, framed beside his lines (set by Ninja/Apply UI Fonts). Empty shows his lines alone.")]
    [SerializeField] Sprite oniCloseup;
    [Tooltip("The souls' painted close-ups for their dialogue scenes (set by Ninja/Apply UI Fonts). Empty shows the soul-fire instead.")]
    [SerializeField] Sprite akaganeCloseup;
    [SerializeField] Sprite mizoreCloseup;
    [SerializeField] Sprite hibikiCloseup;
    [Tooltip("Poguru's pixel clips (set by Ninja/Apply Boss Art). Empty draws him as a teal glow with eyes.")]
    [SerializeField] PoguruArt poguruArt = new PoguruArt();
    [Tooltip("Poguru's painted close-up for his talk scenes (set by Ninja/Apply UI Fonts). Empty shows his pixel frames instead.")]
    [SerializeField] Sprite poguruCloseup;
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
    [Tooltip("Every clip in Audio/Voice by name, for the reactive dialogue's lines; built by Ninja/Apply Voice Lines. Lines without a clip are subtitles only.")]
    [SerializeField] VoiceLibrary voiceLibrary;
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
    [SerializeField] Sprite[] utsusemiPhaseStop;
    [Tooltip("Its Hyper Beam wind-up in phase 2 (set by Ninja/Apply Boss Art). Empty holds its attack pose instead.")]
    [SerializeField] Sprite[] utsusemiPhaseHyperCharge;
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
    [Tooltip("His Spirit Hyper Beam pose: the hammer slammed down at his side, then a braced stance held while the beam burns. Wired by Ninja/Apply Boss Art; empty keeps his idle.")]
    [SerializeField] Sprite[] kagetsuneBeam;
    [Tooltip("The Unchained Wraith's Spirit Hyper Beam pose. Empty falls back to his first form's.")]
    [SerializeField] Sprite[] kagetsunePhaseBeam;
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
    [SerializeField] AudioClip kagetsuneCoffinLine;
    [SerializeField] AudioClip kagetsuneThousandBellsLine;
    [SerializeField] AudioClip kagetsuneSummonBenibanaLine;
    [SerializeField] AudioClip kagetsuneSummonUtsusemiLine;
    [SerializeField] AudioClip kagetsuneSummonShiroganeLine;
    [Tooltip("His callouts for the Rosary Curtain, the Soul River, the Reliquary Rend, the Coffin Pendulum and the Soul Harvest, and his word as he drags a soul back into the coffin.")]
    [SerializeField] AudioClip kagetsuneCurtainLine;
    [SerializeField] AudioClip kagetsuneRiverLine;
    [SerializeField] AudioClip kagetsuneRendLine;
    [SerializeField] AudioClip kagetsunePendulumLine;
    [SerializeField] AudioClip kagetsuneHarvestLine;
    [SerializeField] AudioClip kagetsuneLevyLine;
    [SerializeField] AudioClip kagetsuneAbsorbedLine;
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
    [Header("Safe areas")]
    [Tooltip("Where Oni lives between runs, walking in every direction. The first is the temple he wakes in; maps, props and spots are set by Ninja/Apply Maps.")]
    [SerializeField] SafeArea.Config[] safeAreas = DefaultSafeAreas();
    [Tooltip("Seconds after a death before the temple calls him back.")]
    [SerializeField] float deathToTempleTime = 2.4f;

    [Tooltip("How fast the ronin walks off the field and into the next map.")]
    [SerializeField] float travelWalkSpeed = 9f;
    [Tooltip("Seconds the screen takes to fade to black, and back, between maps.")]
    [SerializeField] float travelFadeTime = 0.6f;
    [Tooltip("Pause after a stage's last army falls, before its boon is offered.")]
    [SerializeField] float stageClearPause = 1f;

    [SerializeField] StageConfig[] stages = DefaultStages();

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
    // Spirit Notes earned this run (already added to the save), for the end screen.
    int notesThisRun;
    // Seals and silk gained this run, by Relic, for the end screen.
    readonly int[] relicsThisRun = new int[2];
    float noteFraction;
    // Second Dawn and Fate's Thread work once a run.
    bool dawnUsed;
    // Poguru's last-heart warning, once a run.
    bool lowLifeWarned;
    // Fate's Thread redraws and Banishing Bell banishes left this run, and the boons banished.
    int redrawsLeft;
    int banishesLeft;
    readonly HashSet<BoonId> banished = new HashSet<BoonId>();
    MenuUI menu;
    ResourceTray resources;

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
    // Souls dropped by samurai this stage, for killSoulsPerStage; and whether one has dropped a heart.
    int killSoulsThisStage;
    bool killHeartThisStage;
    Tutorial tutorial;
    // What clearing the current round gives, picked at the path that led to it.
    PathReward pendingReward;
    Element pendingElement;
    // The escalated config for rounds past a stage's configured ones, built once per round.
    LevelConfig extraRound;
    int extraRoundFor = -1;
    // The element F5's next soul brings.
    int devSoulElement;
    // Souls carry a hint arrow until the player has caught one this run.
    bool soulCaught;
    bool firstBoonTaken;
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
    AudioSource oniVoice;
    AudioSource soulVoice;
    AudioSource poguruVoice;
    Poguru poguru;
    // The voice filters: Utsusemi's machine voice (a doubled, ringing, slightly gritty layer over the recorded
    // lines) and Shirogane's spirit voice (a soft, hollow echo, as if from across water) retune the same three.
    Behaviour[] machineVoice;
    // Whether the voice source is playing a full line rather than a bark.
    bool speakingFullLine;
    DialogueScene dialogueScene;
    // The souls who have already talked this run: only the first boon screen of each element opens a scene.
    readonly HashSet<Element> metThisRun = new HashSet<Element>();
    // The take each line played last, so the next call of it picks another.
    readonly Dictionary<BossLine, int> lastTakes = new Dictionary<BossLine, int>();
    Backdrop backdrop;
    SafeArea safeArea;
    // A station's page is open over the safe area; he stands still under it.
    bool stationOpen;
    float stationClosedAt;
    // Poguru talks once a visit to the temple.
    bool poguruTalked;

    void Awake()
    {
        // Added before anything measures the view, so CameraShake.Center is the camera's resting spot.
        if (Camera.main.GetComponent<CameraShake>() == null) Camera.main.gameObject.AddComponent<CameraShake>();
        Boss.HitStop = seconds =>
        {
            if (state == State.Playing && !hitStopping) StartCoroutine(HitStop(seconds));
        };
        Boss.RenameBar = name => ui.RenameBossBar(name);
        Boss.Summon = SummonSoul;
        Boss.Muster = MusterLevy;
        Boss.LevyRemaining = () => army.Remaining;
        Boss.Disband = army.Disband;
        Boss.PoguruWarn = PoguruWarns;
        Boss.PoguruRest = () =>
        {
            if (!poguruWarning) return;
            poguruWarning = false;
            poguru.ReturnToWisp();
        };
        // Bosses' voicelines: a 2D source of their own, so they're never throttled like sound effects. It sits on its
        // own child so the machine-voice filters only ever touch it.
        backdrop = FindAnyObjectByType<Backdrop>();
        var voiceObject = new GameObject("BossVoice");
        voiceObject.transform.SetParent(transform, false);
        voice = voiceObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        machineVoice = CreateMachineVoice(voiceObject);
        // Oni's own source, so a boss's voice filters never colour his lines.
        var oniObject = new GameObject("OniVoice");
        oniObject.transform.SetParent(transform, false);
        oniVoice = oniObject.AddComponent<AudioSource>();
        oniVoice.playOnAwake = false;
        oniVoice.spatialBlend = 0f;
        soulVoice = CreateSoulVoice();
        poguruVoice = CreatePoguruVoice();
    }

    // Poguru's own source: he's soul-fire too, so a light, quick chorus, but no coffin echo.
    AudioSource CreatePoguruVoice()
    {
        var poguruObject = new GameObject("PoguruVoice");
        poguruObject.transform.SetParent(transform, false);
        var source = poguruObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var chorus = poguruObject.AddComponent<AudioChorusFilter>();
        chorus.dryMix = 0.9f;
        chorus.wetMix1 = chorus.wetMix2 = 0.12f;
        chorus.wetMix3 = 0f;
        chorus.delay = 20f;
        chorus.rate = 0.6f;
        chorus.depth = 0.08f;
        return source;
    }

    // The three souls share a source of their own: they're half in the coffin, so a faint, slow version of the
    // Emperor's choir (a light chorus under a short hall echo), the same for all three so they read as one family.
    AudioSource CreateSoulVoice()
    {
        var soulObject = new GameObject("SoulVoice");
        soulObject.transform.SetParent(transform, false);
        var source = soulObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var chorus = soulObject.AddComponent<AudioChorusFilter>();
        chorus.dryMix = 0.85f;
        chorus.wetMix1 = chorus.wetMix2 = 0.2f;
        chorus.wetMix3 = 0f;
        chorus.delay = 35f;
        chorus.rate = 0.15f;
        chorus.depth = 0.15f;
        var echo = soulObject.AddComponent<AudioEchoFilter>();
        echo.delay = 90f;
        echo.decayRatio = 0.25f;
        echo.wetMix = 0.18f;
        echo.dryMix = 1f;
        return source;
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
        ronin.UltimateStarted += OnUltimateStarted;
        ronin.UltimateEnded += OnUltimateEnded;
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
        ronin.UltimateStarted -= OnUltimateStarted;
        ronin.UltimateEnded -= OnUltimateEnded;
        Blocker.Broken -= OnBlockerBroken;
        IceStatue.Shattered -= OnStatueShattered;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void LockEscapeInFullscreen();
#endif

    // Fullscreen last frame, so leaving it mid-fight (Esc in a browser that won't hand the key over) pauses the game.
    bool wasFullScreen;

    void Start()
    {
        Time.timeScale = 1f;
#if UNITY_WEBGL && !UNITY_EDITOR
        LockEscapeInFullscreen();
#endif
        wasFullScreen = Screen.fullScreen;
        if (boonChoice == null) boonChoice = ui.gameObject.AddComponent<BoonChoiceUI>();
        boonChoice.Bind(ronin, ui);
        menu = ui.gameObject.AddComponent<MenuUI>();
        menu.Bind(ui);
        // The temple's resources, Hades-style in the corner. A new currency is one more line here.
        resources = ResourceTray.Build(ui);
        resources.Add("SpiritNotes", () => Save.Data.embers, NoteIcon);
        resources.Add("WarlordSeals", () => Upgrades.Held(Relic.Seal), new[] { ProceduralSprites.Seal });
        resources.Add("KiteSilk", () => Upgrades.Held(Relic.Silk), new[] { ProceduralSprites.Silk });
        dialogueScene = ui.gameObject.AddComponent<DialogueScene>();
        dialogueScene.Bind(ui);
        baseRoninArt = ronin.art;
        ResetLoadout();
        poguru = Poguru.Create(poguruArt, ronin);
        tutorial = gameObject.AddComponent<Tutorial>();
        tutorial.Bind(this);

        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        ui.SetScore(0);
        ui.SetStage(1, stages.Length, 1, 0);
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
        else if (intoSafeArea)
        {
            intoSafeArea = false;
            bool wake = wakeInPool;
            wakeInPool = false;
            StartCoroutine(EnterSafeArea(wake));
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

    // Where Oni's words sit on the title page: just right of him, above his row.
    static readonly Vector2 HillSpeech = new Vector2(250f, 90f);

    // Oni's teal, for the few words he says.
    static readonly Color OniColour = new Color(0.45f, 1f, 0.9f);
    // Poguru's soul-fire, a bluer teal than Oni's so the two read apart.
    public static readonly Color PoguruColour = new Color(0.45f, 0.85f, 1f);

    // The close-up framed beside Oni's words on the title hill. Lines in play show no portrait; conversations get a
    // full talk scene instead (SpeakerFor).
    GameUI.SpeakerPortrait? PortraitFor(string speaker) => speaker switch
    {
        // His close-up is 1024x1536, cut at the hips; drawn this tall his head and shoulders fill the frame.
        "Oni" when oniCloseup != null => new GameUI.SpeakerPortrait { art = oniCloseup, focus = new Vector2(0.46f, 0.236f), height = 780f },
        _ => null,
    };

    // Back in the temple after a run, Oni sometimes says something as he climbs out of the pool.

    bool DevToolsOn => Debug.isDebugBuild || devToolsInRelease;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool pause = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        // In a browser Esc may only leave fullscreen without the game seeing it, so leaving fullscreen pauses too.
        bool leftFullScreen = Application.platform == RuntimePlatform.WebGLPlayer && wasFullScreen && !Screen.fullScreen;
        wasFullScreen = Screen.fullScreen;
        // Poguru's tutorial can be paused through its drills and talks too, so it can be skipped from anywhere in it.
        bool canPause = state == State.Playing
            || (tutorial.Active && (state == State.Transition || state == State.Dialogue) && !menu.IsOpen);
        if (canPause && (pause || leftFullScreen)) Pause();
        if (state == State.Hub) UpdateSafeArea(keyboard, gamepad, pause);
        resources.SetVisible(state == State.Hub);
        if (DevToolsOn && keyboard != null) DevHotkeys(keyboard);
        if (state == State.Playing)
        {
            UpdateScouts();
            if (RunLog.Enabled) RunLog.Tick(Time.deltaTime, army.Frozen, ArmyGap());
        }
        ronin.UltimateLocked = tutorial.Active;
        boonChoice.UltimateShown = !tutorial.Active || tutorial.ShowsUltimate;
    }

    // The ultimates, by Ronin.Ultimate, named for the run log.
    static string UltimateName(Ronin.Ultimate ultimate) => ultimate switch
    {
        _ => "SHADOW CLONE",
    };

    // How far the army's lowest rank stands above the ronin's head, for the run log.
    float ArmyGap()
    {
        float lowest = float.MaxValue;
        // Divers leave the ranks on purpose, so only the formation counts.
        foreach (Samurai samurai in army.Soldiers)
        {
            if (!army.IsDiving(samurai)) lowest = Mathf.Min(lowest, samurai.transform.position.y);
        }
        return lowest == float.MaxValue ? float.MaxValue : lowest - ronin.TopY;
    }

    // The boons held, for the run log.
    string HeldSummary()
    {
        var held = new List<string>();
        foreach (EquippedBoon boon in loadout.Held) held.Add(boon.Boon.Kind == BoonKind.Normal ? $"{boon.Boon.Name} ({boon.Rarity} Lv {boon.Level})" : boon.Boon.Name);
        return string.Join(", ", held);
    }

    // The menu pages. Each sub-page's Back returns to whichever page opened it.

}
