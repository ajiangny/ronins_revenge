using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Owns the game flow: title screen -> levels -> win/lose screen, plus score and lives.
public class GameManager : MonoBehaviour
{
    enum State { Title, Transition, Playing, GameOver }

    const string HighScoreKey = "HighScore";
    static bool skipTitle;

    [Header("Inscribed")]
    [SerializeField, FormerlySerializedAs("fleet")] SamuraiArmy army;
    [SerializeField, FormerlySerializedAs("ship")] Ronin ronin;
    [SerializeField, FormerlySerializedAs("shields")] BlockerWall blockers;
    [SerializeField] GameUI ui;
    [SerializeField] int startingLives = 3;
    [SerializeField] float levelIntroTime = 2.5f;

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

    void OnEnable()
    {
        army.SamuraiKilled += OnSamuraiKilled;
        army.ArmyDefeated += OnArmyDefeated;
        army.ArmyReachedRonin += OnArmyReachedRonin;
        ronin.Hit += OnRoninHit;
    }

    void OnDisable()
    {
        army.SamuraiKilled -= OnSamuraiKilled;
        army.ArmyDefeated -= OnArmyDefeated;
        army.ArmyReachedRonin -= OnArmyReachedRonin;
        ronin.Hit -= OnRoninHit;
    }

    void Start()
    {
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
                "LEFT / RIGHT or A / D to move      SPACE to throw\n\n" +
                $"Defeat all {levels.Length} stages. Each samurai army is faster and fires more arrows.\n" +
                "Hide behind the bushes and stone lanterns - they won't last forever.\n\n" +
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
        ui.SetLives(lives);
        ui.SetScore(score, highScore);
        StartCoroutine(BeginLevel());
    }

    IEnumerator BeginLevel()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        ClearProjectiles();
        blockers.Rebuild();

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

    void OnSamuraiKilled(Samurai samurai)
    {
        score += samurai.Points * (levelIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score, highScore);
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
    }
}
