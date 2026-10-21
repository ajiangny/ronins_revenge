using System.Collections;
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
    public Sprite kunai;
    public Sprite shuriken;
}

// Owns the game flow: title screen -> levels -> win/lose screen, plus score, lives, souls and boons.
public class GameManager : MonoBehaviour
{
    enum State { Title, Transition, Playing, Choosing, GameOver }

    const string HighScoreKey = "HighScore";
    static bool skipTitle;

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
    [Tooltip("One entry per element: Fire, Water, Lightning.")]
    [SerializeField] ElementArt[] elementArt = new ElementArt[3];

    [SerializeField] LevelConfig[] levels =
    {
        new LevelConfig { columns = 10, rows = 4, topRowY = 8.5f, baseSpeed = 2.5f, maxSpeedMultiplier = 4f,   fireInterval = 0.9f,  aimedShotChance = 0.25f, arrowSpeed = 7f,   samuraiColor = new Color(0f, 0.98f, 0.6f) },
        new LevelConfig { columns = 11, rows = 5, topRowY = 8.1f, baseSpeed = 2.8f, maxSpeedMultiplier = 4.5f, fireInterval = 0.7f,  aimedShotChance = 0.4f,  arrowSpeed = 8.5f, samuraiColor = new Color(1f, 0.85f, 0.1f) },
        new LevelConfig { columns = 11, rows = 5, topRowY = 7.5f, baseSpeed = 3.2f, maxSpeedMultiplier = 5f,   fireInterval = 0.5f,  aimedShotChance = 0.55f, arrowSpeed = 10f,  samuraiColor = new Color(1f, 0.25f, 0.35f) },
    };

    State state;
    int levelIndex;
    int lives;
    int score;
    int highScore;

    RoninLoadout loadout;
    BoonEffects effects;
    CharacterArt baseRoninArt;
    Element? shownElement;
    Soul activeSoul;
    int killsSinceSoul;

    void OnEnable()
    {
        army.SamuraiKilled += OnSamuraiKilled;
        army.ArmyDefeated += OnArmyDefeated;
        army.ArmyReachedRonin += OnArmyReachedRonin;
        ronin.Hit += OnRoninHit;
        ronin.ShurikenThrown += OnShurikenThrown;
        ronin.Parried += OnParried;
    }

    void OnDisable()
    {
        army.SamuraiKilled -= OnSamuraiKilled;
        army.ArmyDefeated -= OnArmyDefeated;
        army.ArmyReachedRonin -= OnArmyReachedRonin;
        ronin.Hit -= OnRoninHit;
        ronin.ShurikenThrown -= OnShurikenThrown;
        ronin.Parried -= OnParried;
    }

    void Start()
    {
        Time.timeScale = 1f;
        if (boonChoice == null) boonChoice = ui.gameObject.AddComponent<BoonChoiceUI>();
        boonChoice.Bind(ronin, ui.Font);
        baseRoninArt = ronin.art;
        ResetLoadout();

        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        ui.SetScore(0, highScore);
        ui.SetLevel(1, levels.Length);
        ui.SetLives(startingLives);
        blockers.Rebuild();

        if (skipTitle)
        {
            skipTitle = false;
            StartGame();
        }
        else
        {
            state = State.Title;
            ui.ShowOverlay("SHADOW BLADE",
                "LEFT / RIGHT or A / D to move      SPACE to throw kunai\n" +
                "E or UP for the Spirit Shuriken      SHIFT or DOWN to parry arrows\n\n" +
                $"Defeat all {levels.Length} stages. Each samurai army is faster and fires more arrows.\n" +
                "Fallen samurai may drop spirit souls. Catch one to choose a boon.\n\n" +
                "Press ENTER to start",
                new Color(0.4f, 0.85f, 1f));
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;

        if (state == State.Title && confirm)
        {
            StartGame();
        }
        else if (state == State.GameOver && confirm)
        {
            // Reload for a clean slate (arrows, debris, ronin), then jump straight into play.
            skipTitle = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    void StartGame()
    {
        lives = startingLives;
        score = 0;
        levelIndex = 0;
        killsSinceSoul = 0;
        ResetLoadout();
        ui.SetLives(lives);
        ui.SetScore(score, highScore);
        StartCoroutine(BeginLevel());
    }

    IEnumerator BeginLevel()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ClearProjectiles();
        blockers.Rebuild(loadout.BlockerHealthMultiplier);

        LevelConfig level = levels[levelIndex];
        army.SpawnStage(level, ronin.transform, ronin.TopY, blockers.TopY);
        ui.SetLevel(levelIndex + 1, levels.Length);
        ui.ShowOverlay($"STAGE {levelIndex + 1}",
            levelIndex == 0 ? "Get ready!" : "Stage cleared! The next army is bigger, faster and angrier.",
            level.samuraiColor);

        yield return new WaitForSeconds(levelIntroTime);

        ui.HideOverlay();
        state = State.Playing;
        ronin.ControlsEnabled = true;
        army.Advance();
    }

    void OnSamuraiKilled(Samurai samurai, KillSource source)
    {
        score += samurai.Points * (levelIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score, highScore);

        if (state != State.Playing) return;
        effects.OnKill(samurai, source);
        MaybeDropSoul(samurai.transform.position);
    }

    void OnShurikenThrown(Vector3 muzzle)
    {
        if (state == State.Playing) effects.OnShurikenThrown(muzzle);
    }

    void OnParried()
    {
        if (state == State.Playing) effects.OnParried();
    }

    void ResetLoadout()
    {
        loadout = new RoninLoadout();
        ronin.Loadout = loadout;
        effects = new BoonEffects(army, ronin, loadout);
        shownElement = null;
        UpdateRoninLook();
        boonChoice.RefreshHud(loadout);
    }

    // One soul at a time, never from the stage's last samurai (the stage ends before it could be caught).
    void MaybeDropSoul(Vector3 position)
    {
        if (activeSoul != null || army.Remaining == 0) return;
        killsSinceSoul++;
        if (Random.value >= soulDropChance && killsSinceSoul < soulPityKills) return;

        killsSinceSoul = 0;
        var element = (Element)Random.Range(0, 3);
        activeSoul = Soul.Spawn(position, element, ArtFor(element)?.soulFrames, ronin, OnSoulCollected);
    }

    // Pauses the game and offers three boons of the soul's element, like a god's boon in Hades.
    void OnSoulCollected(Soul soul)
    {
        if (state != State.Playing) return;
        var offers = Boons.Draw(soul.Element, 3, loadout, lives);
        if (offers.Count == 0) return;

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        boonChoice.Show(soul.Element, offers, ArtFor(soul.Element)?.soulFrames, index =>
        {
            TakeBoon(offers[index]);
            Time.timeScale = 1f;
            state = State.Playing;
            ronin.ControlsEnabled = true;
        });
    }

    void TakeBoon(BoonOffer offer)
    {
        loadout.Take(offer);
        float value = offer.Boon.ValueAt(offer.Rarity);
        switch (offer.Boon.Id)
        {
            case BoonId.TidalRenewal:
                lives = Mathf.Min(lives + (int)value, Boons.MaxLives);
                ui.SetLives(lives);
                break;
            case BoonId.GlacierWall:
                blockers.Rebuild(loadout.BlockerHealthMultiplier);
                break;
            case BoonId.FrozenRiposte:
                loadout.GainWard();
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

    // Flames follow the element he has the most boons of; each projectile follows the element in its slot.
    void UpdateRoninLook()
    {
        Element? dominant = loadout.DominantElement;
        CharacterArt look = dominant is Element d && ArtFor(d)?.ronin != null ? ArtFor(d).ronin : baseRoninArt;

        Element? attack = loadout.SlotElement(BoonSlot.Attack);
        Element? special = loadout.SlotElement(BoonSlot.Special);
        Element? parry = loadout.SlotElement(BoonSlot.Parry);
        Sprite kunai = attack is Element a ? ArtFor(a)?.kunai : null;
        Sprite shuriken = special is Element s ? ArtFor(s)?.shuriken : null;

        Color aura = dominant is Element glow ? Boons.Info(glow).Color * new Color(1f, 1f, 1f, 0.45f) : Color.clear;
        ronin.SetLook(look, aura,
            kunai, StandInTint(attack, kunai),
            shuriken, StandInTint(special, shuriken),
            parry is Element p ? Boons.Info(p).Color : Color.white);

        if (dominant != shownElement && dominant is Element changed)
        {
            ElementFx.Glow(ronin.Bounds.center, Boons.Info(changed).Color, 1f, 4f, 0.5f);
            Debris.BurstSprites(ronin.Bounds.center, ronin.art.deathParticles, Boons.Info(changed).Color, 14, 1f, 6f);
        }
        shownElement = dominant;
    }

    // Recoloured art needs no tint; without it the default sprite is tinted in the element's colour.
    static Color StandInTint(Element? element, Sprite elementSprite)
    {
        if (element is not Element e || elementSprite != null) return Color.white;
        return Color.Lerp(Color.white, Boons.Info(e).Color, 0.75f);
    }

    void OnArmyDefeated()
    {
        if (state != State.Playing) return;

        if (levelIndex + 1 < levels.Length)
        {
            levelIndex++;
            StartCoroutine(BeginLevel());
        }
        else
        {
            EndGame("VICTORY", "Every samurai army has fallen. The temple is safe.", new Color(0.4f, 1f, 0.5f));
        }
    }

    void OnArmyReachedRonin()
    {
        if (state != State.Playing) return;
        ronin.Explode();
        EndGame("DEFEATED", "The samurai reached you - there's no stopping them now.", new Color(1f, 0.3f, 0.3f));
    }

    void OnRoninHit()
    {
        if (state != State.Playing) return;

        lives--;
        ui.SetLives(lives);
        if (lives <= 0)
        {
            ronin.Explode();
            EndGame("DEFEATED", "The ronin has fallen.", new Color(1f, 0.3f, 0.3f));
        }
    }

    void EndGame(string title, string message, Color color)
    {
        state = State.GameOver;
        Time.timeScale = 1f;
        army.Halt();
        ronin.ControlsEnabled = false;
        PlayerPrefs.SetInt(HighScoreKey, highScore);
        PlayerPrefs.Save();

        ui.ShowOverlay(title,
            $"{message}\n\nScore {score}      High score {highScore}\nReached stage {levelIndex + 1} of {levels.Length}\n\nPress ENTER to play again",
            color);
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
    }
}
