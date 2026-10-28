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
public class GameManager : MonoBehaviour
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
    [SerializeField] int startingLives = 3;
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

    // The stages as written in code. The scene keeps its own serialized copy, which wins at runtime; Ninja/Apply Stages
    // copies these into it (maps are wired separately by Ninja/Apply Maps).
    // The temple on the hill: its gate at the top leads out, its spirit pool at the bottom is where he wakes, and the
    // flute altar, memorial stones and weapon rack stand on the open flagstones between. Spots match Temple_Hub.png.
    public static SafeArea.Config[] DefaultSafeAreas() => new[]
    {
        new SafeArea.Config
        {
            name = "The Temple",
            floor = Rect.MinMaxRect(0.17f, 0.08f, 0.86f, 0.69f),
            wakeSpot = new Vector2(0.4985f, 0.17f),
            // The water, in map pixels (TempleLayers cuts the same ellipse out of the map).
            poolCentre = new Vector2(343f, 319f),
            poolRadii = new Vector2(39f, 30f),
            stations = new[]
            {
                new SafeArea.Station { kind = StationKind.Exit, label = "SET OUT", spot = new Vector2(0.5f, 0.68f), onMap = true, reach = 2.2f },
                // On the stone dais to the left, used from the top of its steps.
                new SafeArea.Station { kind = StationKind.FluteSongs, label = "FLUTE SONGS", spot = new Vector2(0.2689f, 0.4219f), propAt = new Vector2(185f, 180f),
                    footprint = new Vector2(2.4f, 0.6f), fx = StationFx.Incense, reach = 2f },
                // In the kerbed grave nook to the upper right, used from in front of its offerings.
                new SafeArea.Station { kind = StationKind.Memorial, label = "MEMORIAL STONES", spot = new Vector2(0.6933f, 0.4401f), propAt = new Vector2(477f, 170f),
                    placeholder = new Color(0.4f, 0.42f, 0.45f), footprint = new Vector2(4.2f, 0.6f), fx = StationFx.Glow, reach = 2f },
                // On the plank platform under the broken eave, used from its left end.
                new SafeArea.Station { kind = StationKind.WeaponRack, label = "WEAPON RACK", spot = new Vector2(0.7849f, 0.0885f), propAt = new Vector2(562f, 340f),
                    footprint = new Vector2(2.6f, 0.5f), fx = StationFx.Glint, reach = 2.2f },
                // On the flagstones to the lower left, between the dais and the pool, used from its right side.
                new SafeArea.Station { kind = StationKind.Oaths, label = "OATH STONE", spot = new Vector2(0.3125f, 0.1f), propAt = new Vector2(190f, 334f),
                    placeholder = new Color(0.32f, 0.36f, 0.3f), footprint = new Vector2(2f, 0.5f), fx = StationFx.Glow, glowColour = Oaths.HeatColour, reach = 2f },
                // Poguru's nest, an old offering bowl on the flagstones right of the pool, used from in front of it.
                new SafeArea.Station { kind = StationKind.Poguru, label = "POGURU", spot = new Vector2(0.6105f, 0.215f), propAt = new Vector2(452f, 292f),
                    placeholder = new Color(0.3f, 0.45f, 0.55f), footprint = new Vector2(1.2f, 0.4f), fx = StationFx.Glow, glowColour = PoguruColour, reach = 1.8f },
            },
            // The ghost-fires at either side, and the two stone lanterns at the dais, lit.
            lamps = new[]
            {
                new SafeArea.Lamp { px = new Vector2(100f, 147f), colour = new Color(0.45f, 1f, 0.9f, 0.2f), radius = 2.6f },
                new SafeArea.Lamp { px = new Vector2(562f, 178f), colour = new Color(0.45f, 1f, 0.9f, 0.2f), radius = 2.6f },
                new SafeArea.Lamp { px = new Vector2(120f, 224f), colour = new Color(1f, 0.65f, 0.35f, 0.28f), radius = 2.2f },
                new SafeArea.Lamp { px = new Vector2(242f, 224f), colour = new Color(1f, 0.65f, 0.35f, 0.28f), radius = 2.2f },
            },
            // The ghost-fires at either side, lifted off the map so they flicker. The pool is left to the souls.
            spirits = new[] { new Vector2(100f, 154f), new Vector2(562f, 186f) },
            // The gate's doorway, whose leaves swing open as he comes up; past its threshold he's under its beam and
            // roof (raised off the map's top, so he stays hidden walking out of view) and between its posts.
            gateDoors = new Rect(309f, 58f, 70f, 50f),
            gateOver = new[] { new Rect(284f, -300f, 118f, 358f), new Rect(284f, 58f, 25f, 52f), new Rect(379f, 58f, 23f, 52f) },
            // He takes the steps up to the altar's dais and the side step onto the rack's platform; the toro lanterns,
            // the dais's raised edges, the platform's edges and the grave nook's kerb stand in his way.
            walls = new[]
            {
                new Rect(106f, 198f, 54f, 54f), new Rect(209f, 198f, 45f, 54f),
                new Rect(126f, 126f, 12f, 72f), new Rect(233f, 126f, 12f, 72f), new Rect(126f, 122f, 119f, 12f),
                new Rect(416f, 110f, 124f, 90f),
                new Rect(508f, 206f, 126f, 12f), new Rect(508f, 212f, 12f, 51f), new Rect(508f, 320f, 12f, 26f), new Rect(508f, 334f, 126f, 12f),
            },
        },
    };

    public static StageConfig[] DefaultStages() => new[]
    {
        new StageConfig
        {
            name = "The Bamboo Road",
            rounds = new[]
            {
                new LevelConfig { columns = 8,  rows = 3, topRowY = 8.5f, baseSpeed = 2.3f, maxSpeedMultiplier = 3.5f, fireInterval = 0.8f,   aimedShotChance = 0.3f,  arrowSpeed = 7f,   samuraiColor = new Color(0f, 0.98f, 0.6f) },
                new LevelConfig { columns = 9,  rows = 4, topRowY = 8.4f, baseSpeed = 2.5f, maxSpeedMultiplier = 4f,   fireInterval = 0.72f, aimedShotChance = 0.35f, arrowSpeed = 7.5f, samuraiColor = new Color(0f, 0.98f, 0.6f),
                    rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru }, diveInterval = 9f },
                new LevelConfig { columns = 10, rows = 4, topRowY = 8.2f, baseSpeed = 2.7f, maxSpeedMultiplier = 4.2f, fireInterval = 0.64f, aimedShotChance = 0.4f,  arrowSpeed = 8f,   samuraiColor = new Color(0f, 0.98f, 0.6f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Ashigaru }, scouts = true, diveInterval = 7f },
            },
            boss = BossKind.Benibana,
        },
        new StageConfig
        {
            name = "The Pilgrim's Pass",
            rounds = new[]
            {
                new LevelConfig { columns = 10, rows = 4, topRowY = 8.3f, baseSpeed = 2.6f, maxSpeedMultiplier = 4f,   fireInterval = 0.68f, aimedShotChance = 0.4f,  arrowSpeed = 8f,   samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer }, diveInterval = 7f },
                new LevelConfig { columns = 11, rows = 4, topRowY = 8.3f, baseSpeed = 2.8f, maxSpeedMultiplier = 4.2f, fireInterval = 0.64f,  aimedShotChance = 0.4f,  arrowSpeed = 8f,   samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru }, diveInterval = 5.1f },
                new LevelConfig { columns = 11, rows = 5, topRowY = 8.5f, baseSpeed = 3f,   maxSpeedMultiplier = 4.4f, fireInterval = 0.6f, aimedShotChance = 0.45f, arrowSpeed = 8.5f, samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer },
                    diveInterval = 4.2f, scouts = true },
            },
            boss = BossKind.Utsusemi,
        },
        new StageConfig
        {
            name = "The Frozen Shrine",
            rounds = new[]
            {
                new LevelConfig { columns = 11, rows = 5, topRowY = 8.5f, baseSpeed = 3f,   maxSpeedMultiplier = 4.4f, fireInterval = 0.6f, aimedShotChance = 0.45f, arrowSpeed = 8.5f, samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer }, diveInterval = 5.1f },
                new LevelConfig { columns = 12, rows = 5, topRowY = 8.6f, baseSpeed = 3.2f, maxSpeedMultiplier = 4.6f, fireInterval = 0.56f,  aimedShotChance = 0.5f,  arrowSpeed = 9f,   samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Yurei, SamuraiKind.Shieldbearer }, diveInterval = 4.2f, scouts = true },
                new LevelConfig { columns = 12, rows = 6, topRowY = 8.8f, baseSpeed = 3.4f, maxSpeedMultiplier = 4.8f, fireInterval = 0.52f, aimedShotChance = 0.55f, arrowSpeed = 9.5f, samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Taisho, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer },
                    diveInterval = 3.4f, scouts = true },
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
                new LevelConfig { columns = 12, rows = 5, topRowY = 8.6f, baseSpeed = 3.3f, maxSpeedMultiplier = 4.6f, fireInterval = 0.56f,  aimedShotChance = 0.5f,  arrowSpeed = 9f,   samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Bellringer, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer }, diveInterval = 4.2f },
                new LevelConfig { columns = 12, rows = 6, topRowY = 8.8f, baseSpeed = 3.5f, maxSpeedMultiplier = 4.8f, fireInterval = 0.53f, aimedShotChance = 0.55f, arrowSpeed = 9.5f, samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Bellringer, SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Bellringer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer },
                    diveInterval = 3.8f, scouts = true },
                new LevelConfig { columns = 13, rows = 6, topRowY = 9f,   baseSpeed = 3.6f, maxSpeedMultiplier = 5f,   fireInterval = 0.5f, aimedShotChance = 0.6f,  arrowSpeed = 10f,  samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Bellringer, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Bellringer, SamuraiKind.Shieldbearer },
                    diveInterval = 3f, scouts = true },
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
    // Spirit Notes earned this run (already added to the save), for the end screen.
    int notesThisRun;
    float noteFraction;
    // Second Dawn and Fate's Thread work once a run.
    bool dawnUsed;
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
        // The temple's resources, Hades-style in the corner. A new currency is one more line here.
        resources = ResourceTray.Build(ui);
        resources.Add("SpiritNotes", () => Save.Data.embers, NoteIcon);
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
    IEnumerator HubReturn()
    {
        if (!Save.Data.hubPending) yield break;
        Save.Data.hubPending = false;
        Conversation said = Dialogue.Pick("Oni", DialogueEvent.HubReturn, Dialogue.Context());
        if (said == null || said.Lines.Length == 0) yield break;
        yield return new WaitForSeconds(1.5f);
        yield return PlayConversation(said, OniColour);
    }

    // Oni's line as he climbs out of the pool, then Poguru's, after a run that ended.
    IEnumerator WakeTalk()
    {
        bool pending = Save.Data.hubPending;
        yield return HubReturn();
        yield return new WaitForSeconds(pending ? 0.6f : 1f);
        if (pending) yield return PoguruBark(DialogueEvent.PoguruWake);
        // Out of the wisp (if his word didn't already bring him) and off to his nest.
        if (state == State.Hub && safeArea != null && poguru.Current != Poguru.Mode.Perch) poguru.PerchAt(NestPoint);
    }

    // Poguru pops out of the wisp for a line and goes back in. Nothing is picked (or marked heard) unless it can play:
    // not in the tutorial, never over a full line or while he's already out, and only in play, between beats or in
    // the temple.
    IEnumerator PoguruBark(DialogueEvent evt, float chance = 1f, BossKind? boss = null)
    {
        if (tutorial.Active || poguru.IsOut || FullLinePlaying) yield break;
        if (state != State.Playing && state != State.Transition && state != State.Hub) yield break;
        if (Random.value >= chance) yield break;
        Conversation said = Dialogue.Pick("Poguru", evt, Dialogue.Context(loadout, null, lives, false, boss));
        if (said == null || said.Lines.Length == 0) yield break;
        poguru.PopOut();
        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForSecondsRealtime(SayAsPoguru(said));
        // In the temple he goes to his nest instead.
        if (state == State.Hub && safeArea != null) poguru.PerchAt(NestPoint);
        else poguru.ReturnToWisp();
    }

    bool FullLinePlaying => dialogueScene.IsPlaying || (speakingFullLine && (voice.isPlaying || oniVoice.isPlaying || soulVoice.isPlaying));

    IEnumerator SoulBark()
    {
        yield return new WaitForSecondsRealtime(0.8f);
        yield return PoguruBark(DialogueEvent.PoguruSoul);
    }

    bool DevToolsOn => Debug.isDebugBuild || devToolsInRelease;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool pause = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        if (state == State.Playing && pause) Pause();
        if (state == State.Hub) UpdateSafeArea(keyboard, gamepad, pause);
        resources.SetVisible(state == State.Hub);
        if (DevToolsOn && keyboard != null) DevHotkeys(keyboard);
        if (state == State.Playing)
        {
            UpdateScouts();
            if (RunLog.Enabled) RunLog.Tick(Time.deltaTime, army.Frozen, ArmyGap());
        }
        ronin.UltimateLocked = tutorial.Active;
        ui.SetUltimate(UltimateName(ronin.CurrentUltimate), ronin.UltimateFill, ronin.UltimateReady, ronin.UltimateActive);
    }

    // The ultimates the temple offers, by Ronin.Ultimate: their names and what they do.
    static string UltimateName(Ronin.Ultimate ultimate) => ultimate switch
    {
        _ => "SHADOW CLONE",
    };

    static string UltimateNote(Ronin.Ultimate ultimate) => ultimate switch
    {
        _ => "Fill the bar by striking foes, then press Q: your shadow slashes beside you at half strength until it drains.",
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
        foreach (EquippedBoon boon in loadout.Held) held.Add(boon.Boon.Kind == BoonKind.Normal ? $"{boon.Boon.Name} ({boon.Rarity})" : boon.Boon.Name);
        return string.Join(", ", held);
    }

    // The menu pages. Each sub-page's Back returns to whichever page opened it.
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
            string price = maxed ? (Upgrades.IsOff(id) ? $"<color=#{muted}>OFF</color>" : "") : $"<color=#{(affordable ? noteHex : muted)}>{Upgrades.Price(id)}</color>";
            string note = maxed
                ? $"{song.Effect(song.MaxRank)}   <color=#{muted}>·  Enter switches it {(Upgrades.IsOff(id) ? "on" : "off")}</color>"
                : $"{song.Effect(bought + 1)}   <color=#{muted}>·  {(Upgrades.CanBuy(id) ? "Enter to learn" : "not enough notes")}</color>";
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
        }, $"Forget every song and take back {Upgrades.Spent} notes"));
        items.Add(MenuUI.Item.Button("BACK", back, ""));
        menu.Show("FLUTE SONGS", null, items.ToArray(), back, startIndex, paper: paper, gridColumns: 2);
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
        bool fresh = !Save.Exists(slot);
        Save.Use(slot);
        StartCoroutine(fresh ? TitleToGame() : TitleToSafeArea());
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
        // The oath stone stands in chains until the first dawn.
        foreach (SafeArea.Station station in area.stations)
        {
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
            ronin.PlaceAt(BodyAt(safeArea.WakePoint));
            yield return ui.FadeTo(1f, 0f);
            yield return new WaitForSeconds(0.4f);
            StartCoroutine(ui.FadeTo(0f, 1.4f));
            yield return new WaitForSeconds(0.6f);
            Fx.Play(Fx.BoonPillar, ronin.Feet, OniColour, 1.4f);
            ElementFx.Glow(ronin.Bounds.center, OniColour, 1f, 6f, 1.2f);
            Sfx.Play(Sfx.SoulCollect);
            yield return new WaitForSeconds(0.7f);
            yield return ronin.WalkTo(BodyAt(safeArea.PoolExit), travelWalkSpeed * 0.3f);
            if (lastRunNotes > 0) StartCoroutine(NoteTally(lastRunNotes));
            lastRunNotes = 0;
            StartCoroutine(WakeTalk());
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

    void ShowTips(System.Action back)
    {
        string muted = MenuUI.MutedHex(false);
        string teal = ColorUtility.ToHtmlStringRGB(PoguruColour);
        string Row(string topic, string text) => $"<color=#{teal}>{topic}</color>  <color=#{muted}>·</color>  {text}\n";
        string body = "<size=28><align=left>" +
            Row("SLASH (SPACE)", "A cut flies up the field. Cover stops it, so step out first!") +
            Row("SHURIKEN (W)", "Big and sparkly, slow to come back. Save it for crowds.") +
            Row("PARRY (SHIFT)", "Just before an arrow lands. A good parry is ready again straight away! Crimson blows can't be parried. Dodge those!") +
            Row("SPEARS", "The glowing red ones go right through cover. Move!") +
            Row("HEARTS", "From broken cover, and rarely a soldier. At full lives they wait on your row.") +
            Row("THE ARMY", "If it reaches your row, it's over. Thin out the bottom rank!") +
            Row("BOONS", "One per soul. Hold TAB in a fight to see what your icons do.") +
            Row("PATHS", "Walk under, press E. The boss's path opens after round three.") +
            Row("SPIRIT NOTES", "They stay even when you fall. Spend them at the flute altar.") +
            "</align></size>";
        menu.Show("TIPS", body, new[] { MenuUI.Item.Button("BACK", back) }, back);
    }

    // The weapon rack (Hades' Infernal Arms): the blade he carries out, kept in the save.
    void ShowRack(System.Action back, int startIndex = -1)
    {
        string muted = MenuUI.MutedHex(false);
        var carried = (Ronin.Weapon)Save.Data.weapon;
        var blades = new[]
        {
            (Ronin.Weapon.Sword, "KATANA", "Slash waves that cut through the ranks. The technique scrolls reshape them."),
            (Ronin.Weapon.Kunai, "KUNAI", "Thrown blades that strike the first samurai they meet. The elements' blade boons still burn, freeze and arc."),
        };
        var items = new List<MenuUI.Item>();
        foreach (var (weapon, name, note) in blades)
        {
            int row = items.Count;
            Ronin.Weapon pick = weapon;
            items.Add(MenuUI.Item.Button(weapon == carried ? $"{name}  <color=#{muted}>·  CARRIED</color>" : name, () =>
            {
                Save.Data.weapon = (int)pick;
                Save.Write();
                ronin.CurrentWeapon = pick;
                Sfx.Play(Sfx.UiConfirm);
                ShowRack(back, row);
            }, note));
        }
        // The ultimate he carries out (only one so far): its bar fills as he strikes, Q unleashes it.
        var ultimate = (Ronin.Ultimate)Save.Data.ultimate;
        foreach (Ronin.Ultimate each in (Ronin.Ultimate[])System.Enum.GetValues(typeof(Ronin.Ultimate)))
        {
            int row = items.Count;
            Ronin.Ultimate pick = each;
            string name = $"ULTIMATE: {UltimateName(each)}";
            items.Add(MenuUI.Item.Button(each == ultimate ? $"{name}  <color=#{muted}>·  CARRIED</color>" : name, () =>
            {
                Save.Data.ultimate = (int)pick;
                Save.Write();
                ronin.CurrentUltimate = pick;
                Sfx.Play(Sfx.UiConfirm);
                ShowRack(back, row);
            }, UltimateNote(each)));
        }
        items.Add(MenuUI.Item.Button("BACK", back, ""));
        menu.Show("WEAPON RACK", $"<size=30><color=#{muted}>What he takes through the gate.</color></size>", items.ToArray(), back,
            startIndex >= 0 ? startIndex : (int)carried);
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
        menu.Show("DEV TOOLS", $"<size=30><color=#{muted}>F1 kill the army  ·  F2 last boss  ·  F3 this menu  ·  F4 god mode  ·  F5 soul  ·  F6 scroll  ·  F7 dialogue</color></size>",
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

    // One scout at a time, every 15-25 seconds in rounds that have them.
    void UpdateScouts()
    {
        if (!Round.scouts || activeScout != null || activeBoss != null || Time.time < nextScoutTime) return;
        nextScoutTime = Time.time + Random.Range(15f, 25f);
        activeScout = Scout.Spawn(army.samuraiByRow[army.samuraiByRow.Length - 1], Round.topRowY + 1.4f, OnScoutKilled);
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
    }

    // Testing shortcuts (Editor and development builds): F1 wipes out the current army, F2 skips to the last
    // stage's boss, F3 opens the dev menu, F4 toggles god mode, F5 drops a soul (Fire, Frost, Storm in turn) at the
    // ronin's feet.
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
        ronin.CurrentWeapon = (Ronin.Weapon)Save.Data.weapon;
        ronin.CurrentUltimate = (Ronin.Ultimate)Save.Data.ultimate;
        lives = maxLives = Mathf.Clamp(startingLives + Upgrades.Rank(UpgradeId.IronWill) - Oaths.Rank(OathId.FrailBody), 1, Boons.MaxLives);
        Boss.HealthScale = 1f + Oaths.Rank(OathId.IronGenerals) * Oaths.BossHealthPerRank;
        noteFraction = 0f;
        notesThisRun = 0;
        dawnUsed = false;
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
        RunLog.BeginRun($"Heat {Oaths.Heat}  lives {maxLives}  {(Ronin.Weapon)Save.Data.weapon}{(godMode ? "  GOD MODE" : "")}  songs: {(songs.Count > 0 ? string.Join(", ", songs) : "none")}");
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
        // Blockers carry their damage from round to round; a new stage brings fresh ones.
        if (roundIndex == 0) blockers.Rebuild();
        // A stage's first round always ends in a soul; later rounds carry what their path showed.
        if (roundIndex == 0)
        {
            killSoulsThisStage = 0;
            killHeartThisStage = false;
            pendingReward = PathReward.Soul;
            pendingElement = PickSoulElement();
        }

        LevelConfig round = Sworn(Round);
        army.SpawnStage(round, ronin.transform, ronin.TopY, blockers.TopY);
        ui.SetStage(stageIndex + 1, stages.Length, roundIndex + 1, 0);
        string body = roundIndex >= Stage.rounds.Length + 1 ? "Their finest march."
            : roundIndex >= Stage.rounds.Length ? "The army regroups."
            : roundIndex > 0 ? "Another army marches in."
            : stageIndex == 0 ? "Get ready!"
            : stageIndex == 1 ? "Bigger, faster, angrier armies."
            : "The dead march too.";
        Color rewardColour = RewardColour(pendingReward, pendingElement);
        body += $"\n<color=#{ColorUtility.ToHtmlStringRGB(rewardColour)}>Reward: {RewardLabel(pendingReward, pendingElement)}</color>";
        ui.ShowBanner($"Stage {Roman(stageIndex + 1)}  ·  Round {roundIndex + 1}", Stage.name, body, round.samuraiColor);
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
        if (!tutorial.Active) StartCoroutine(PoguruBark(DialogueEvent.PoguruRound, 0.2f));
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

    void OnUltimateStarted()
    {
        RunLog.Note($"ultimate: {UltimateName(ronin.CurrentUltimate)}");
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
        firstBoonTaken = false;
        scrollsDropped = 0;
        gatheredScroll = false;
        ronin.Loadout = loadout;
        effects = new BoonEffects(army, ronin, loadout);
        army.SpeedUpScale = loadout.MarchSpeedUpScale;
        shownElement = null;
        UpdateRoninLook();
        boonChoice.RefreshHud(loadout);
    }

    // Most souls come from the portals; a scout's or a rare samurai's waits for one already falling, and never comes
    // once the round's army is gone.
    void MaybeDropSoul(Vector3 position)
    {
        // Nothing drops in Poguru's tutorial: its one heart and soul are scripted.
        if (activeSoul != null || army.Remaining == 0 || tutorial.Active) return;
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
        if (activeScroll != null || scrollsDropped >= maxScrollsPerRun || army.Remaining == 0 || tutorial.Active) return;
        if (Random.value >= chance || Boons.DrawArts(loadout, 1).Count == 0) return;
        scrollsDropped++;
        SpawnScroll(position);
    }

    void SpawnScroll(Vector3 position)
    {
        activeScroll = TechniqueScroll.Spawn(position, scrollFrames, ronin, OnScrollCollected);
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

    // A Spirit Bead raises one held boon a rarity, like Hades' Pom of Power. With nothing left to raise it pays
    // Spirit Notes instead, and false (nothing shown).
    bool OfferBead(System.Action afterPick)
    {
        var offers = Boons.DrawBead(loadout, 3);
        if (offers.Count == 0)
        {
            Earn(BeadNotes * (stageIndex + 1));
            return false;
        }

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.BeadColor, 1.2f);
        Sfx.Play(Sfx.SoulCollect, 1f, 1.15f);
        boonChoice.ShowBead(offers, Held(beadPainted, beadFrames), index =>
        {
            TakeOrReject(offers, index);
            Time.timeScale = 1f;
            afterPick();
        });
        return true;
    }

    // The Sword Arts screen: up to three arts the ronin doesn't hold, like Daedalus's hammer in Hades.
    bool OfferArts(System.Action afterPick)
    {
        var offers = Boons.DrawArts(loadout, 3);
        if (offers.Count == 0) return false;

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        boonChoice.ShowArts(offers, Held(scrollPainted, scrollFrames), index =>
        {
            TakeOrReject(offers, index);
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
        if (state != State.Playing || tutorial.Active || Random.value >= heartDropChance) return;
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

    // At the end of a round every soul still out, and every heart he can use, flies to the ronin. Hearts heal him up
    // to full; any left over fade away into points, so none linger into the next round. A soul caught on the way
    // offers its boons before the game moves on.
    IEnumerator GatherDrops()
    {
        Pickup.GatherAll();
        float giveUp = Time.time + 2f;
        while (Pickup.Gathering > 0 && Time.time < giveUp) yield return null;
        Pickup.StopGathering();

        // Disabled ones are already fading.
        Heart[] spare = System.Array.FindAll(FindObjectsByType<Heart>(), h => h.enabled);
        foreach (Heart heart in spare)
        {
            int points = SpareHeartPoints * (stageIndex + 1);
            score += points;
            heart.Dissipate(points, ui.BodyFont);
        }
        if (spare.Length > 0)
        {
            highScore = Mathf.Max(highScore, score);
            ui.SetScore(score);
            Sfx.Play(Sfx.SoulCollect, 0.6f, 1.5f);
            yield return new WaitForSeconds(Heart.DissipateTime);
        }

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
        var offers = Boons.Draw(element, 3, loadout, maxLives, ensureCapstone, minRarity, banished);
        if (offers.Count == 0) return false;

        // The first screen of each soul in a run opens with that soul talking to Oni, as Hades' gods do.
        Conversation greeting = null;
        if (metThisRun.Add(element))
        {
            bool offering = offers.Exists(o => o.Boon.Kind == BoonKind.Offering);
            greeting = Dialogue.Pick(SoulOf(element), DialogueEvent.SoulPickup, Dialogue.Context(loadout, element, lives, offering));
        }

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        if (greeting != null) StartCoroutine(GreetThenChoose(greeting, element, offers, afterPick, ensureCapstone, minRarity));
        else ShowBoons(element, offers, afterPick, ensureCapstone, minRarity);
        return true;
    }

    IEnumerator GreetThenChoose(Conversation greeting, Element element, List<BoonOffer> offers, System.Action afterPick,
        bool ensureCapstone, Rarity minRarity)
    {
        yield return PlayScene(greeting);
        ShowBoons(element, offers, afterPick, ensureCapstone, minRarity);
    }

    void ShowBoons(Element element, List<BoonOffer> offers, System.Action afterPick, bool ensureCapstone, Rarity minRarity)
    {
        ElementArt art = ArtFor(element);
        Sprite[] heldSoul = art?.heldSoul != null ? new[] { art.heldSoul } : art?.soulFrames;
        boonChoice.Show(element, offers, heldSoul, art?.portrait, index =>
        {
            TakeOrReject(offers, index);
            Time.timeScale = 1f;
            afterPick();
        });
        // The tutorial explains the cards over the first boon screen.
        if (tutorial.Active) StartCoroutine(tutorial.BoonsShown());
        // Fate's Thread and Banishing Bell. The new cards replace these in the same list, so the pick reads them.
        List<BoonOffer> Refill()
        {
            var fresh = Boons.Draw(element, 3, loadout, maxLives, ensureCapstone, minRarity, banished);
            if (fresh.Count == 0) return null;
            offers.Clear();
            offers.AddRange(fresh);
            OfferRerolls();
            return offers;
        }
        void OfferRerolls()
        {
            if (redrawsLeft > 0) boonChoice.OfferRedraw(() =>
            {
                redrawsLeft--;
                return Refill();
            });
            if (banishesLeft > 0) boonChoice.OfferBanish(selected =>
            {
                banishesLeft--;
                banished.Add(offers[selected].Boon.Id);
                return Refill();
            });
        }
        OfferRerolls();
    }

    // The soul who catches each element's dead and hands them to Oni (Story/Souls.md).
    static string SoulOf(Element element) => element switch
    {
        Element.Fire => "Akagane",
        Element.Water => "Mizore",
        _ => "Hibiki",
    };

    // The boon screen's pick, or nothing when every offer was rejected (X twice); the run goes on either way.
    void TakeOrReject(List<BoonOffer> offers, int index)
    {
        if (index == BoonChoiceUI.Rejected) RunLog.Note($"rejected {string.Join(", ", offers.ConvertAll(o => o.Boon.Name))}");
        else TakeBoon(offers[index]);
    }

    void TakeBoon(BoonOffer offer)
    {
        loadout.Take(offer);
        // The run's first boon gets a word from Poguru once the screen has closed.
        if (!firstBoonTaken && offer.Boon.Kind != BoonKind.Art && !offer.IsUpgrade)
        {
            firstBoonTaken = true;
            StartCoroutine(SoulBark());
        }
        RunLog.Note(offer.IsUpgrade ? $"bead: {offer.Boon.Name} -> {offer.Rarity}"
            : offer.Boon.Kind == BoonKind.Normal ? $"took {offer.Boon.Name} ({offer.Rarity})" : $"took {offer.Boon.Name}");
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
        if (tutorial.Active && stageIndex == 0 && roundIndex == 0)
        {
            StartCoroutine(TutorialRoundEnd());
            return;
        }
        StartCoroutine(EndRound());
    }

    // The tutorial's first round ends on its spear and heart, still in play so the blow lands, then as any round does.
    IEnumerator TutorialRoundEnd()
    {
        yield return tutorial.SpearAndHeart();
        // A pause opened on the last beat must not skip the round's end; a death (GameOver) does.
        while (state == State.Paused) yield return null;
        if (state == State.Playing) yield return EndRound();
    }

    IEnumerator EndRound()
    {
        RunLog.EndSegment(lives, maxLives, null);
        state = State.Transition;
        ronin.ControlsEnabled = false;
        IceStatue.CrumbleAll();
        yield return GatherDrops();
        yield return GrantReward();

        // The last round leaves only the boss; before that the ronin picks the next round's reward on a path.
        if (roundIndex + 1 >= maxRounds)
        {
            StartCoroutine(StageCleared());
            yield break;
        }
        RewardPath chosen = null;
        yield return ChoosePath(p => chosen = p);
        if (chosen.Reward == PathReward.Boss)
        {
            StartCoroutine(StageCleared());
            yield break;
        }
        pendingReward = chosen.Reward;
        pendingElement = chosen.Element;
        roundIndex++;
        StartCoroutine(BeginRound());
    }

    // The Long Road holds the boss's path back a round per rank.
    int BossPortalAfter => Mathf.Min(bossPortalAfterRound + Oaths.Rank(OathId.LongRoad), maxRounds);

    // The cleared round's reward, given before the paths open: it gathers out of the air in the middle of the field,
    // where the army stood, and is pulled into the ronin before it's given.
    IEnumerator GrantReward()
    {
        PathReward reward = pendingReward;
        // A spring at full health, or a bead or scroll with nothing left to give, falls back to a Lost Melody.
        if (reward == PathReward.Spring && lives >= maxLives) reward = PathReward.Notes;
        if (reward == PathReward.Scroll && Boons.DrawArts(loadout, 1).Count == 0) reward = PathReward.Notes;
        if (reward == PathReward.Bead && Boons.DrawBead(loadout, 1).Count == 0) reward = PathReward.Notes;

        RewardSpirit spirit = RewardSpirit.Spawn(CameraShake.Center, RewardIcon(reward, pendingElement),
            RewardColour(reward, pendingElement), ronin);
        if (tutorial.Active) yield return tutorial.RewardShown();
        yield return new WaitUntil(() => spirit == null);

        bool picked = false;
        switch (reward)
        {
            case PathReward.Soul:
                Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(pendingElement).Color, 1.4f);
                Sfx.Play(Sfx.SoulCollect);
                soulCaught = true;
                // Ancestral Lore: each stage's first soul is Rare or better.
                Rarity floor = roundIndex == 0 && Upgrades.Rank(UpgradeId.AncestralLore) > 0 ? Rarity.Rare : Rarity.Common;
                if (OfferBoons(pendingElement, () => picked = true, false, floor)) yield return new WaitUntil(() => picked);
                break;
            case PathReward.Bead:
                Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.BeadColor, 1.2f);
                Sfx.Play(Sfx.SoulCollect, 1f, 1.1f);
                if (OfferBead(() => picked = true)) yield return new WaitUntil(() => picked);
                break;
            case PathReward.Scroll:
                scrollsDropped++;
                Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.ArtColor, 1.2f);
                Sfx.Play(Sfx.SoulCollect, 1f, 0.8f);
                if (OfferArts(() => picked = true)) yield return new WaitUntil(() => picked);
                break;
            case PathReward.Spring:
                lives = Mathf.Min(lives + 1, maxLives);
                ui.SetLives(lives, maxLives);
                Fx.Play(Fx.BoonPillar, ronin.Feet, RewardColour(reward, pendingElement), 1.2f);
                Sfx.Play(Sfx.SoulCollect, 0.8f, 1.3f);
                yield return new WaitForSeconds(0.6f);
                break;
            case PathReward.Notes:
                int notes = noteCache * (stageIndex + 1);
                Earn(notes);
                Fx.Play(Fx.BoonPillar, ronin.Feet, RewardColour(reward, pendingElement), 1.2f);
                Sfx.Play(Sfx.SoulCollect, 0.9f, 1.2f);
                ui.ShowSubtitle(null, $"+{notes} Spirit Notes", RewardColour(reward, pendingElement), 1.6f);
                yield return new WaitForSeconds(0.6f);
                break;
        }
        state = State.Transition;
        ronin.ControlsEnabled = false;
    }

    const string PathHint = "E / ENTER  to choose";

    // The paths open where the army stood. He walks his row (no attacks); the one he's under lights and a tooltip says
    // what it gives, and E or Enter picks it: its reward floats away up the field and
    // he follows it to the next round, or to the boss.
    IEnumerator ChoosePath(System.Action<RewardPath> onChosen)
    {
        ClearProjectiles();
        var rewards = RollPaths();
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        float y = CameraShake.Center.y + cam.orthographicSize * 0.35f;
        var paths = new List<RewardPath>();
        for (int i = 0; i < rewards.Count; i++)
        {
            float x = CameraShake.Center.x + halfWidth * 1.2f * ((i + 0.5f) / rewards.Count - 0.5f);
            (PathReward reward, Element element) = rewards[i];
            paths.Add(RewardPath.Spawn(new Vector3(x, y, 0f), reward, element, RewardColour(reward, element),
                RewardLabel(reward, element), RewardIcon(reward, element), ui.BodyFont));
        }
        Sfx.Play(Sfx.SoulSpawn, 1f, 0.7f);
        if (tutorial.Active) yield return tutorial.PathsShown();

        ronin.MoveOnly = true;
        ronin.ControlsEnabled = true;
        float readyAt = Time.time + 0.6f;
        RewardPath chosen = null;
        RewardPath described = null;
        bool shownOnce = false;
        while (chosen == null)
        {
            // Only a path he's standing under lights; between them nothing does, and the choose key does nothing.
            RewardPath under = null;
            float best = RewardPath.Reach;
            foreach (RewardPath path in paths)
            {
                float gap = Mathf.Abs(path.transform.position.x - ronin.Bounds.center.x);
                if (gap < best)
                {
                    best = gap;
                    under = path;
                }
            }
            foreach (RewardPath path in paths) path.SetLit(path == under);
            // Between paths, a quiet prompt rides over his head instead.
            Vector3 overHead = new Vector3(ronin.Bounds.center.x, ronin.TopY + 1.4f, 0f);
            if (under != described || !shownOnce)
            {
                described = under;
                shownOnce = true;
                if (under != null) ui.ShowPathInfo(under.TooltipAnchor, RewardDescription(under.Reward), under.Colour, PathHint);
                else ui.ShowPathInfo(overHead, "Walk under a path.", GameUI.MutedColour, "");
            }
            if (under == null) ui.MovePathInfo(overHead);
            if (under != null && Time.time >= readyAt && state == State.Transition && ChooseKey()) chosen = under;
            yield return null;
        }
        ui.HidePathInfo();
        ronin.ControlsEnabled = false;
        ronin.MoveOnly = false;
        var shown = new List<string>();
        foreach (RewardPath path in paths) shown.Add(RewardLabel(path.Reward, path.Element));
        RunLog.Note($"path: {RewardLabel(chosen.Reward, chosen.Element)}  (of {string.Join(" / ", shown)})");
        foreach (RewardPath path in paths)
        {
            if (path != chosen) path.Close();
        }
        // The pick floats away up the field, to come back as the next round's reward; he sets off after it, on to the
        // next round on the same ground, or the boss's path goes on through StageCleared's own travel.
        chosen.Release();
        Sfx.Play(Sfx.SoulSpawn, 1f, 1.2f);
        yield return new WaitForSeconds(0.35f);
        if (chosen.Reward != PathReward.Boss) yield return WalkOn(chosen.transform.position.x);
        onChosen(chosen);
    }

    // What each path gives, for the HUD while he chooses.
    string RewardDescription(PathReward reward) => reward switch
    {
        PathReward.Soul => "Receive a boon from a lost soul.",
        PathReward.Bead => "Raise one of your boons to the next rarity.",
        PathReward.Scroll => "Learn a Sword Art that reshapes your blade.",
        PathReward.Spring => "Restore one lost life.",
        PathReward.Notes => $"Gain {noteCache * (stageIndex + 1)} Spirit Notes to spend at the temple.",
        PathReward.Boss => Stage.boss == BossKind.None ? "Leave this stage behind."
            : "Face the stage's warlord. Felling them grants a rare boon and a Spirit Bead.",
        _ => "",
    };

    // E or Enter (pad A) chooses a path. Not Space or a click: they slash, and a player still swinging as the army
    // falls shouldn't take a path unread.
    static bool ChooseKey()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }

    // After the reward up the field into the dark, and out of it at his place on the row, on the same ground: the cover
    // keeps its damage and the stage's music plays on, unlike TravelTo between maps.
    IEnumerator WalkOn(float towardX)
    {
        Camera cam = Camera.main;
        float top = CameraShake.Center.y + cam.orthographicSize;
        float bottom = CameraShake.Center.y - cam.orthographicSize;
        Vector3 from = ronin.transform.position;
        Coroutine leaving = StartCoroutine(ronin.WalkTo(new Vector3(towardX, top + 2f, from.z), travelWalkSpeed));
        // The fade starts as he nears the top, so he's gone into the dark just as it closes.
        yield return new WaitUntil(() => ronin.transform.position.y > top - 4f);
        yield return ui.FadeTo(1f, travelFadeTime);
        yield return leaving;
        ronin.PlaceAt(new Vector3(ronin.Home.x, bottom - 2f, ronin.Home.z));
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(ui.FadeTo(0f, travelFadeTime));
        yield return ronin.WalkTo(ronin.Home, travelWalkSpeed);
    }

    // The next round's rewards to choose between: no two of a kind, and the boss's path once enough rounds are
    // cleared. Souls are the common pick; springs only show while he's hurt, beads and scrolls only with a use.
    List<(PathReward, Element)> RollPaths()
    {
        var weights = new List<(PathReward reward, float weight)> { (PathReward.Soul, 45f), (PathReward.Notes, 20f) };
        if (Boons.DrawBead(loadout, 1).Count > 0 && Oaths.Rank(OathId.FadedSpirits) == 0) weights.Add((PathReward.Bead, 20f));
        if (lives < maxLives) weights.Add((PathReward.Spring, 10f));
        if (scrollsDropped < maxScrollsPerRun && Boons.DrawArts(loadout, 1).Count > 0) weights.Add((PathReward.Scroll, 5f));

        var picks = new List<(PathReward, Element)>();
        // Third Path opens one more.
        int choices = portalChoices + Upgrades.Rank(UpgradeId.ThirdPath);
        while (picks.Count < choices && weights.Count > 0)
        {
            float total = 0f;
            foreach (var entry in weights) total += entry.weight;
            float roll = Random.value * total;
            int index = 0;
            while (index < weights.Count - 1 && (roll -= weights[index].weight) >= 0f) index++;
            PathReward reward = weights[index].reward;
            weights.RemoveAt(index);
            picks.Add((reward, reward == PathReward.Soul ? PickSoulElement() : Element.Fire));
        }
        if (roundIndex + 1 >= BossPortalAfter) picks.Add((PathReward.Boss, Element.Fire));
        return picks;
    }

    static Color NoteColour => OniColour;
    // The Spirit Notes' icon, looped: the PixelLab art, or a drawn note without it.
    Sprite[] NoteIcon => noteFrames != null && noteFrames.Length > 0 ? noteFrames : new[] { ProceduralSprites.Note(1) };
    static readonly Color SpringColour = new Color(1f, 0.4f, 0.45f);
    static readonly Color BossPortalColour = new Color(0.85f, 0.15f, 0.2f);

    static Color RewardColour(PathReward reward, Element element) => reward switch
    {
        PathReward.Soul => Boons.Info(element).Color,
        PathReward.Bead => Boons.BeadColor,
        PathReward.Scroll => Boons.ArtColor,
        PathReward.Spring => SpringColour,
        PathReward.Boss => BossPortalColour,
        _ => NoteColour,
    };

    string RewardLabel(PathReward reward, Element element) => reward switch
    {
        PathReward.Soul => $"{Boons.Info(element).Name} Soul",
        PathReward.Bead => "Spirit Bead",
        PathReward.Scroll => "Technique Scroll",
        PathReward.Spring => "Healing Spring",
        PathReward.Boss => Stage.boss == BossKind.None ? "Onward" : Stage.boss.ToString(),
        _ => "Lost Melody",
    };

    Sprite[] RewardIcon(PathReward reward, Element element) => reward switch
    {
        PathReward.Soul => ArtFor(element)?.soulFrames,
        PathReward.Scroll => scrollFrames,
        PathReward.Bead => beadFrames,
        PathReward.Notes => NoteIcon,
        PathReward.Spring => springFrames != null && springFrames.Length > 0 ? springFrames : ui.LifeIcon != null ? new[] { ui.LifeIcon } : null,
        PathReward.Boss => BossIconOf(stages[stageIndex].boss),
        _ => null,
    };

    Sprite[] BossIconOf(BossKind boss)
    {
        if (bossIcons == null) return null;
        foreach (BossIcon icon in bossIcons)
            if (icon.boss == boss) return icon.frames;
        return null;
    }

    // What a boon screen holds out: the painting, or the pixel loop without one.
    static Sprite[] Held(Sprite painted, Sprite[] frames) => painted != null ? new[] { painted } : frames;

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
        Rarity floor = Oaths.Rank(OathId.FadedSpirits) >= 2 ? Rarity.Common : Rarity.Rare;
        if (!OfferBoons(element, BeadThenNextStage, true, floor)) BeadThenNextStage();
    }

    // A fallen boss also leaves a Spirit Bead, so the stage's reward raises the build as well as widening it.
    // Pond's Blessing adds a second after the first boss.
    void BeadThenNextStage()
    {
        state = State.Transition;
        bool blessed = stageIndex == 0 && Upgrades.Rank(UpgradeId.PondsBlessing) > 0;
        System.Action then = blessed ? () =>
        {
            state = State.Transition;
            if (!OfferBead(NextStage)) NextStage();
        } : NextStage;
        if (!OfferBead(then)) then();
    }

    // The music cuts out, smoke rolls in across the top of the screen, and the stage's warlord descends.
    IEnumerator BeginBoss()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        army.Clear();
        ClearProjectiles();
        ui.HideBanner();
        ui.SetStage(stageIndex + 1, stages.Length, roundIndex + 1, 0);
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
        boss.Healed += () => ShowBossHealth(boss);
        activeBoss.Defeated += OnBossDefeated;
        // A boss with no phase card still changes music when its second phase starts; with one, this is a no-op.
        activeBoss.SecondPhaseBegan += PlayPhaseMusic;
        activeBoss.Spoke += OnBossSpoke;
        effects.Boss = activeBoss;
        yield return activeBoss.Enter(post, 1.6f);
        yield return activeBoss.Entrance();

        Fx.Play(Fx.SlashArc, post, activeBoss.Colour, 6f);
        if (Stage.boss == BossKind.Benibana) Fx.Play(Fx.BeniPetals, post, Color.white, 3f);
        CameraShake.Add(0.4f);
        Sfx.Play(Sfx.StageStart, 1f, 0.75f);
        BossVoice lines = VoiceFor(Stage.boss);
        // What the boss says depends on every fight before this one; with nothing written it's its own intro line.
        Conversation intro = Dialogue.Pick(Stage.boss.ToString(), DialogueEvent.BossIntro, Dialogue.Context(loadout))
            ?? Unwritten(BossLine.Intro);
        // The card only names the boss; the talking happens in the scene after it.
        if (lines.portrait != null && lines.closeup != null)
        {
            yield return ui.PlayBossIntro(lines.portrait, lines.closeup, lines.closeupHeight, lines.closeupFocus, lines.closeupAt,
                activeBoss.Title, activeBoss.Epithet, activeBoss.Colour, voice, null, null);
        }
        else
        {
            ui.ShowBanner(activeBoss.Epithet, System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(activeBoss.Title.ToLowerInvariant()), "", activeBoss.Colour);
            yield return new WaitForSeconds(2.2f);
            ui.HideBanner();
        }
        yield return PlayScene(intro);

        ui.ShowBossBar($"{activeBoss.Title}, {activeBoss.Epithet}", activeBoss.Colour);
        // A boss with second-phase portraits gets a card for its arrival, played in its phase break.
        Boss.PhaseCard = lines.phasePortrait != null && lines.phaseCloseup != null ? PlayPhaseCard : null;
        if (sfxLibrary != null) Music.Play(sfxLibrary.FightMusic(Stage.boss), sfxLibrary.musicVolume);
        state = State.Playing;
        ronin.ControlsEnabled = true;
        RunLog.Note($"held: {HeldSummary()}");
        RunLog.BeginSegment($"S{stageIndex + 1} BOSS {Stage.boss} ({activeBoss.MaxHealth} hp)");
        activeBoss.Begin(ronin, army.samuraiPrefab.arrowPrefab);
    }

    // The second-phase title card: the same cinematic as the intro with the phase art, epithet and line. The music
    // ducks under it as it would under the spoken line.
    // When Oni has something to answer (a phase exchange not heard yet), the card only names the new form and the
    // exchange plays as a scene after it; otherwise the card speaks the line itself.
    IEnumerator PlayPhaseCard(Boss boss)
    {
        BossVoice lines = VoiceFor(Stage.boss);
        Conversation exchange = Dialogue.Pick(Stage.boss.ToString(), DialogueEvent.BossPhase, Dialogue.Context(loadout));
        AudioClip clip = exchange == null ? lines.phase : null;
        Music.Duck(0.3f, (clip != null ? clip.length : 3f) + 2.5f);
        // The new form brings its own music, rising in under the card.
        PlayPhaseMusic();
        voice.volume = voiceVolume * Sfx.Volume;
        // The wash behind the art follows the transformed painting where its colours change (Utsusemi's molten core).
        Color wash = lines.phaseColour.a > 0f ? lines.phaseColour : boss.Colour;
        yield return ui.PlayBossIntro(lines.phasePortrait, lines.phaseCloseup, lines.phaseCloseupHeight, lines.phaseCloseupFocus,
            lines.phaseCloseupAt, boss.Title, lines.phaseEpithet, wash, voice, clip, exchange == null ? boss.Line(BossLine.Phase) : null);
        // The bar carries the name the card just announced.
        ui.RenameBossBar($"{boss.PhaseTitle}, {lines.phaseEpithet}");
        if (exchange != null) yield return PlayScene(exchange);
    }

    void PlayPhaseMusic()
    {
        if (sfxLibrary != null) Music.Play(sfxLibrary.PhaseMusic(Stage.boss), sfxLibrary.musicVolume, 1.5f);
    }

    // The boss's own line alone, for a moment nothing is written for.
    Conversation Unwritten(BossLine line) =>
        new Conversation { Id = $"{Stage.boss}_{line}", Speaker = Stage.boss.ToString(), BuiltIn = line };

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
            // The close-ups are transparent dialogue portraits, 1024x1536 and cut off at the thigh, shown 1500 tall
            // with each boss's eyes (the focus) on the centre line at the same point on screen, so the set lines up and
            // the cut-off bottom sits off screen. Kagetsune's is a wider 1536x1024 bust, sized to match.
            // Hers: the kiseru raised by her face, its smoke curling up to the left.
            closeupHeight = 1500f, closeupFocus = new Vector2(0.46f, 0.19f), closeupAt = new Vector2(0f, 255f),
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
            // Framed on its slit-eye, the whole hat brim and its pendants on screen.
            closeupHeight = 1500f, closeupFocus = new Vector2(0.45f, 0.19f), closeupAt = new Vector2(0f, 255f),
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
            // A spike of ice forming over her raised fingertips, her sheathed blades held upright at her side.
            closeupHeight = 1500f, closeupFocus = new Vector2(0.419f, 0.207f), closeupAt = new Vector2(0f, 255f),
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
            // The horned helm over the coffin-hammer on his shoulder. Kept just wider than the screen so the painting's
            // cut-off sides stay off it, which puts his horn tips at the top edge: he's the biggest of them.
            closeupHeight = 1300f, closeupFocus = new Vector2(0.51f, 0.245f), closeupAt = new Vector2(0f, 255f),
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
        // The last words are said in the defeat scene (BossFell), with Oni's answer.
        if (line == BossLine.Defeat) return;
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
        ShowBossHealth(activeBoss);
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
        if (activeBoss != null) ShowBossHealth(activeBoss);
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
        if (activeBoss != null) ShowBossHealth(activeBoss);
        if (!hitStopping) StartCoroutine(HitStop(0.2f));
        CameraShake.Add(0.5f);
        effects.Wolf = null;
        activeWolf = null;
    }

    // The boss bar, in two halves while a boss fights beside a second foe (Shirogane and Kuon).
    // Kuon's half is a spirit violet, apart from Shirogane's ice blue and the white of the damage trail.
    static readonly Color KuonBarColour = new Color(0.72f, 0.6f, 1f);

    void ShowBossHealth(Boss boss)
    {
        if (boss.SplitBar(out float left, out float right)) ui.SetBossHealthSplit(left, right, "SHIROGANE", "KUON", KuonBarColour);
        else ui.SetBossHealth(boss.BarFraction);
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
        RunLog.EndSegment(lives, maxLives, null);
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ui.HideBossBar();
        ClearProjectiles();
        Music.Stop(1.5f);
        if (!hitStopping) StartCoroutine(HitStop(0.25f));
        Vector3 where = activeBoss.Centre;
        // Her last words, and Oni's answer, with the field held still at the killing blow.
        yield return PlayScene(Dialogue.Pick(Stage.boss.ToString(), DialogueEvent.BossDefeat, Dialogue.Context(loadout))
            ?? Unwritten(BossLine.Defeat));
        // Cleared only after a frame: this can run inside the boss's own hit handling, with boon effects still
        // working through the blow that felled her. Each boss's fall takes its own time (Shirogane's is a scene).
        yield return new WaitForSeconds(activeBoss.DefeatSeconds);
        Earn(Upgrades.ForBoss(Stage.boss));
        Save.BossFelled(Stage.boss);
        // Poguru has a word for a fallen warlord; the Emperor's fall has the dawn instead.
        if (Stage.boss != BossKind.Kagetsune) yield return PoguruBark(DialogueEvent.PoguruBoss, 1f, Stage.boss);
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

        ElementFx.Glow(where, Color.white, 1f, 5f, 1f);
        yield return new WaitForSeconds(0.6f);
        // Her soul is the stage's boon: a capstone when one is in reach, Rare or better, then a Spirit Bead.
        OfferStageBoon();
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
            new Color(1f, 0.72f, 0.45f), Sfx.VictorySting, true);
    }

    // Her silver, as herself rather than the Emperor's teal.
    static readonly Color FreedColour = new Color(0.72f, 0.9f, 1f);

    // How a talk plays: over the HUD or with it left as it is (keepHud: the tutorial's pointing, a temple talk, a talk
    // over the boon screen), how dark the field goes, Poguru on the right instead of the left (and Oni on the left), and
    // a call as each line comes up.
    public struct TalkOptions
    {
        public bool keepHud;
        public float dim;
        public bool poguruRight;
        public System.Action<int> onLine;
    }

    // A conversation as a talk scene (DialogueScene) over the frozen field. A boss's built-in line comes first in its
    // own voice, then the written lines, each voiced when its clip is there. The field, the clock and the ronin's
    // controls are put back as they were afterwards.
    IEnumerator PlayScene(Conversation said) => PlayTalk(said, default);

    // A conversation's lines for the scene, each with its speaker, filled-in text and clip.
    List<DialogueScene.Line> LinesOf(Conversation said, TalkOptions options)
    {
        var lines = new List<DialogueScene.Line>();
        int index = 0;
        if (said.BuiltIn is BossLine built && activeBoss != null)
        {
            AudioClip clip = VoiceFor(Stage.boss).Take(built, -1, out int take);
            lines.Add(new DialogueScene.Line { who = SpeakerFor(said.Speaker), text = activeBoss.Line(built, take), clip = clip });
            index = 1;
        }
        DialogueContext ctx = Dialogue.Context();
        foreach (DialogueLine line in said.Lines)
        {
            AudioClip clip = voiceLibrary != null ? voiceLibrary.Find(said.VoiceId(index)) : null;
            DialogueScene.Speaker who = SpeakerFor(line.Speaker);
            // Poguru on the right puts Oni on the left, so the two still face each other.
            if (options.poguruRight && line.Speaker == "Poguru") who.right = true;
            else if (options.poguruRight && line.Speaker == "Oni") who.right = false;
            int shown = lines.Count;
            lines.Add(new DialogueScene.Line { who = who, text = Dialogue.Fill(line.Text, ctx), clip = clip,
                source = VoiceOf(line.Speaker), shown = options.onLine != null ? () => options.onLine(shown) : null });
            index++;
        }
        return lines;
    }

    public IEnumerator PlayTalk(Conversation said, TalkOptions options)
    {
        if (said == null) yield break;
        // A killing blow's hit-stop would hand time back mid-scene.
        while (hitStopping) yield return null;
        List<DialogueScene.Line> lines = LinesOf(said, options);
        if (lines.Count == 0) yield break;

        State was = state;
        float timeScale = Time.timeScale;
        bool controls = ronin.ControlsEnabled;
        state = State.Dialogue;
        Time.timeScale = 0f;
        ronin.ControlsEnabled = false;
        speakingFullLine = true;
        // Nothing of the HUD sits over the scene, unless the talk is about it.
        if (!options.keepHud)
        {
            ui.HideForScene(true);
            boonChoice.SetHudVisible(false);
        }
        yield return dialogueScene.Play(lines, voice, voiceVolume * Sfx.Volume, options.dim > 0f ? options.dim : DialogueScene.DarkDim);
        if (!options.keepHud)
        {
            ui.HideForScene(false);
            boonChoice.SetHudVisible(true);
        }
        speakingFullLine = false;
        state = was;
        Time.timeScale = timeScale;
        ronin.ControlsEnabled = controls;
    }

    // Poguru's tutorial (Tutorial.cs) drives the first round through these.
    public Poguru Poguru => poguru;
    public PoguruArt PoguruClips => poguruArt;
    public Ronin Ronin => ronin;
    public MenuUI Menu => menu;
    public BoonChoiceUI BoonChoice => boonChoice;
    public DialogueScene Scene => dialogueScene;
    public GameUI Ui => ui;
    public int Lives => lives;
    public int MaxLives => maxLives;

    // One of Poguru's lines as a bark: voiced when its clip is there, subtitled, his mouth moving. Returns how long it
    // takes to say; 0 when it isn't written.
    public float Bark(string id, bool subtitled = true)
    {
        Conversation said = Dialogue.Find(id);
        return said != null && said.Lines.Length > 0 ? SayAsPoguru(said, subtitled) : 0f;
    }

    // Unsubtitled when the words are shown elsewhere (the tutorial's parry prompt), so the plate covers nothing.
    // Hung over him when he's out (his barks come as he pops from the wisp), so it's clear the line is his.
    float SayAsPoguru(Conversation said, bool subtitled = true) =>
        SayLine(said, 0, poguru.IsOut ? poguru.Centre + Vector3.up * 0.8f : (Vector3?)null, subtitled);

    // The tutorial's soldiers, in the dull red of the army's arrows.
    static readonly Color SoldierColour = new Color(0.9f, 0.45f, 0.4f);

    // One line of a conversation said in the field, not in a scene: voiced when its clip is there and subtitled, hung
    // over the speaker when `over` (the top of their head, in the world) is given. Returns how long it takes to say.
    public float SayLine(Conversation said, int line, Vector3? over = null, bool subtitled = true)
    {
        if (said == null || line >= said.Lines.Length) return 0f;
        string speaker = said.Lines[line].Speaker;
        AudioClip clip = voiceLibrary != null ? voiceLibrary.Find(said.VoiceId(line)) : null;
        string text = Dialogue.Fill(said.Lines[line].Text, Dialogue.Context());
        float seconds = clip != null ? clip.length : Mathf.Clamp(text.Length * 0.06f, 1.4f, 3.5f);
        // Soldiers speak through Oni's plain source; they never talk over him.
        AudioSource source = speaker == "Poguru" ? poguruVoice : oniVoice;
        if (clip != null)
        {
            source.Stop();
            source.volume = voiceVolume * Sfx.Volume;
            source.clip = clip;
            source.Play();
        }
        Color colour = speaker == "Poguru" ? PoguruColour : speaker == "Oni" ? OniColour : SoldierColour;
        if (subtitled && over is Vector3 head)
            ui.ShowSubtitleOver(speaker, text, colour, seconds + 0.6f, ui.CanvasPoint(head) + ((RectTransform)ui.transform).rect.size / 2f);
        else if (subtitled) ui.ShowSubtitle(speaker, text, colour, seconds + 0.6f);
        if (speaker == "Poguru") poguru.Talk(seconds);
        return seconds + 0.3f;
    }

    // The tutorial's spear has to land: through god mode and the blink after an earlier hit.
    public void ForceHit()
    {
        bool invincible = ronin.Invincible;
        ronin.Invincible = false;
        ronin.ClearInvulnerable();
        ronin.TakeHit(ronin.Bounds.center);
        ronin.Invincible = invincible;
    }

    public Heart SpawnHeart(Vector3 at) => Heart.Spawn(at, ui.LifeIcon, ronin, CanHeal, OnHeartCollected);

    // A Taisho's spear thrown at him from `from`: it cuts through cover and can't be parried.
    public Arrow SpawnSpear(Vector3 from, float speed)
    {
        Arrow spear = Instantiate(army.samuraiPrefab.arrowPrefab, from, Quaternion.identity);
        spear.MakeUnbreakable(army.samuraiPrefab.SpearSprite);
        spear.Unparryable = true;
        Vector3 to = ronin.Bounds.center - from;
        to.z = 0f;
        spear.Launch(to.normalized * speed, 2f);
        return spear;
    }

    // The tutorial's last straggler: a foot soldier's art and an ordinary arrow.
    public CharacterArt FootSoldierArt => army.samuraiByRow[army.samuraiByRow.Length - 1];
    // The tutorial's spearman wears the Taisho's art and throws his yari.
    public CharacterArt SpearmanArt => army.samuraiByRow[0];
    public Sprite SpearSprite => army.samuraiPrefab.SpearSprite;
    public Arrow ArrowPrefab => army.samuraiPrefab.arrowPrefab;
    public SamuraiArmy Army => army;

    // Holds the field still for a lesson that waits on the player (the tutorial's parry): no pause menu meanwhile,
    // since its Resume would let time run on.
    public void HoldForLesson(bool hold)
    {
        if (hold && state != State.Dialogue) heldFrom = state;
        state = hold ? State.Dialogue : heldFrom;
        Time.timeScale = hold ? 0f : 1f;
    }

    // The state a lesson's hold hands back (the drills run in Transition, the army round in Playing).
    State heldFrom = State.Playing;

    // At least this many lives, so a lesson's blow can't end the run.
    public void EnsureLives(int count)
    {
        if (lives >= count) return;
        lives = Mathf.Min(count, maxLives);
        ui.SetLives(lives, maxLives);
    }

    // Who speaks through which source: Oni clean, the souls with their coffin echo, a boss (null) through the scene's
    // own, retuned per boss.
    AudioSource VoiceOf(string speaker) => speaker switch
    {
        "Oni" => oniVoice,
        "Akagane" or "Mizore" or "Hibiki" => soulVoice,
        "Poguru" => poguruVoice,
        _ => null,
    };

    // The boss cards draw close-ups this much larger than a scene does, where a figure stands about screen height.
    const float SceneArtScale = 0.72f;
    // Oni's and the souls' close-ups (1024x1536, cut at the hips) drawn that tall.
    const float ScenePortraitHeight = 1080f;

    // How a speaker stands in a talk scene: Oni on the right, the boss or soul on the left. A boss in its second
    // phase wears that phase's painting and epithet. Anyone without a painting shows their soul-fire, or a glow.
    DialogueScene.Speaker SpeakerFor(string name)
    {
        switch (name)
        {
            case "Oni":
                // Drawn a little taller than the bosses (1080) so the hero holds his own against them on screen.
                return new DialogueScene.Speaker { name = "Oni", epithet = "The Ronin", colour = OniColour, art = oniCloseup,
                    focus = new Vector2(0.46f, 0.236f), height = ScenePortraitHeight * 1.1f, right = true };
            // The souls' paintings fill their canvases edge to edge, so they're drawn smaller than Oni's to match faces.
            case "Akagane": return SoulSpeaker(name, "Captain of the Vanguard", Element.Fire, akaganeCloseup, new Vector2(0.62f, 0.24f));
            case "Mizore": return SoulSpeaker(name, "The Court Physician", Element.Water, mizoreCloseup, new Vector2(0.62f, 0.23f));
            case "Hibiki": return SoulSpeaker(name, "The Village Drummer", Element.Lightning, hibikiCloseup, new Vector2(0.67f, 0.22f));
            // He's small, so his painting is drawn a little smaller than the souls'; his pixel frames stand in without it.
            case "Poguru":
                return new DialogueScene.Speaker { name = "Poguru", epithet = "The Lost Dragon", colour = PoguruColour, art = poguruCloseup,
                    focus = new Vector2(0.5f, 0.32f), height = ScenePortraitHeight * 0.85f, soulFrames = poguruArt?.idle };
        }
        if (activeBoss != null && name == Stage.boss.ToString())
        {
            BossVoice lines = VoiceFor(Stage.boss);
            bool phase = activeBoss.SecondPhase && lines.phaseCloseup != null;
            return new DialogueScene.Speaker
            {
                name = phase ? activeBoss.PhaseTitle : activeBoss.Title,
                epithet = phase ? lines.phaseEpithet : activeBoss.Epithet,
                colour = activeBoss.Colour,
                art = phase ? lines.phaseCloseup : lines.closeup,
                focus = phase ? lines.phaseCloseupFocus : lines.closeupFocus,
                height = (phase ? lines.phaseCloseupHeight : lines.closeupHeight) * SceneArtScale,
            };
        }
        return new DialogueScene.Speaker { name = name, colour = Color.white };
    }

    // `eyes` is where the soul's eyes sit in their close-up, 0-1 across and down.
    DialogueScene.Speaker SoulSpeaker(string name, string epithet, Element element, Sprite closeup, Vector2 eyes) => new DialogueScene.Speaker
    {
        name = name,
        epithet = epithet,
        colour = Boons.Info(element).Color,
        art = closeup,
        focus = eyes,
        height = ScenePortraitHeight * 0.9f,
        soulFrames = ArtFor(element)?.soulFrames,
    };

    // A written conversation's lines from `from` on, each voiced when its clip is there, subtitled and waited out.
    // Oni speaks in his teal, anyone else in `colour`.
    IEnumerator PlayConversation(Conversation said, Color colour, int from = 0)
    {
        DialogueContext ctx = Dialogue.Context();
        for (int i = from; i < said.Lines.Length; i++)
        {
            DialogueLine line = said.Lines[i];
            AudioClip clip = voiceLibrary != null ? voiceLibrary.Find(said.VoiceId(i)) : null;
            yield return Say(line.Speaker, line.Speaker == "Oni" ? OniColour : colour, clip, Dialogue.Fill(line.Text, ctx));
        }
    }

    // A line spoken outside a fight (the ending): voiced when the clip is there, always subtitled, waited out.
    IEnumerator Say(string speaker, Color colour, AudioClip clip, string text)
    {
        float seconds = clip != null ? clip.length : 2.5f;
        if (clip != null)
        {
            AudioSource source = VoiceOf(speaker) ?? voice;
            voice.Stop();
            oniVoice.Stop();
            soulVoice.Stop();
            source.volume = voiceVolume * Sfx.Volume;
            source.clip = clip;
            source.Play();
            // A conversation is never talked over: barks wait it out.
            speakingFullLine = true;
            Music.Duck(0.3f, seconds + 0.3f);
        }
        // On the title page Oni speaks from his corner, clear of the menu, his portrait standing on the plate; in play
        // a line is just words.
        bool hill = state == State.Title;
        ui.ShowSubtitle(speaker, text, colour, seconds + 0.6f, hill ? HillSpeech : null, hill ? PortraitFor(speaker) : null);
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
        EndGame("DEFEATED", "The samurai reached you - there's no stopping them now.", new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting, false);
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
            EndGame("DEFEATED", message, new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting, false, activeBoss != null ? Stage.boss : BossKind.None);
        }
    }

    // A run that ends in a clear or a death is remembered for the next one; the killer is the boss he fell to, or
    // None for the army.
    void EndGame(string title, string message, Color color, string sting, bool cleared, BossKind killer = BossKind.None)
    {
        state = State.GameOver;
        tutorial.Abort();
        // Before EndRun counts the dawn, so the first one (before the stone unseals) pays nothing.
        if (cleared) notesThisRun += Oaths.ClaimBounty();
        RunLog.EndSegment(lives, maxLives, null);
        RunLog.EndRun($"{title}  stage {stageIndex + 1} round {roundIndex + 1}  notes {notesThisRun}  held: {HeldSummary()}");
        Save.EndRun(cleared, killer);
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
        menu.Show(title, body, new[]
        {
            MenuUI.Item.Button("RETURN TO TEMPLE", () => ReloadToSafeArea(true)),
            MenuUI.Item.Button("TITLE", () => Reload(false)),
        }, null, 0, color);
    }

    // Hearts are kept for GatherDrops, which heals him with them or fades them into points.
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
