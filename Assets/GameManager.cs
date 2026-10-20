using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Owns the game flow: title screen -> levels -> win/lose screen, plus score and lives.
public class GameManager : MonoBehaviour
{
    enum State { Title, Transition, Playing, GameOver }

    const string HighScoreKey = "HighScore";
    static bool skipTitle;

    [Header("Inscribed")]
    [SerializeField] AlienFleet fleet;
    [SerializeField] Spaceship ship;
    [SerializeField] ShieldWall shields;
    [SerializeField] GameUI ui;
    [SerializeField] int startingLives = 3;
    [SerializeField] float levelIntroTime = 2.5f;

    [SerializeField] LevelConfig[] levels =
    {
        new LevelConfig { columns = 10, rows = 4, topRowY = 9f,   baseSpeed = 2.5f, maxSpeedMultiplier = 4f,   fireInterval = 0.9f,  aimedShotChance = 0.25f, bulletSpeed = 7f,   alienColor = new Color(0f, 0.98f, 0.6f) },
        new LevelConfig { columns = 11, rows = 5, topRowY = 8.6f, baseSpeed = 2.8f, maxSpeedMultiplier = 4.5f, fireInterval = 0.7f,  aimedShotChance = 0.4f,  bulletSpeed = 8.5f, alienColor = new Color(1f, 0.85f, 0.1f) },
        new LevelConfig { columns = 11, rows = 5, topRowY = 8f,   baseSpeed = 3.2f, maxSpeedMultiplier = 5f,   fireInterval = 0.5f,  aimedShotChance = 0.55f, bulletSpeed = 10f,  alienColor = new Color(1f, 0.25f, 0.35f) },
    };

    State state;
    int levelIndex;
    int lives;
    int score;
    int highScore;

    void OnEnable()
    {
        fleet.AlienDestroyed += OnAlienDestroyed;
        fleet.FleetCleared += OnFleetCleared;
        fleet.FleetLanded += OnFleetLanded;
        ship.Hit += OnShipHit;
    }

    void OnDisable()
    {
        fleet.AlienDestroyed -= OnAlienDestroyed;
        fleet.FleetCleared -= OnFleetCleared;
        fleet.FleetLanded -= OnFleetLanded;
        ship.Hit -= OnShipHit;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        ui.SetScore(0, highScore);
        ui.SetLevel(1, levels.Length);
        ui.SetLives(startingLives);
        shields.Rebuild();

        if (skipTitle)
        {
            skipTitle = false;
            StartGame();
        }
        else
        {
            state = State.Title;
            ui.ShowOverlay("SPACE INVADERS 3D",
                "LEFT / RIGHT or A / D to move      SPACE to fire\n\n" +
                $"Clear all {levels.Length} waves. Each wave is faster and shoots more.\n" +
                "Hide behind the shields - they won't last forever.\n\n" +
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
            // Reload for a clean slate (bullets, debris, ship), then jump straight into play.
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
        ship.ControlsEnabled = false;
        ClearProjectiles();
        shields.Rebuild();

        LevelConfig level = levels[levelIndex];
        fleet.SpawnWave(level, ship.transform, ship.TopY, shields.TopY);
        ui.SetLevel(levelIndex + 1, levels.Length);
        ui.ShowOverlay($"WAVE {levelIndex + 1}",
            levelIndex == 0 ? "Get ready!" : "Wave cleared! The next fleet is bigger, faster and angrier.",
            level.alienColor);

        yield return new WaitForSeconds(levelIntroTime);

        ui.HideOverlay();
        state = State.Playing;
        ship.ControlsEnabled = true;
        fleet.Advance();
    }

    void OnAlienDestroyed(Alien alien)
    {
        score += alien.Points * (levelIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score, highScore);
    }

    void OnFleetCleared()
    {
        if (state != State.Playing) return;

        if (levelIndex + 1 < levels.Length)
        {
            levelIndex++;
            StartCoroutine(BeginLevel());
        }
        else
        {
            EndGame("YOU WIN!", "Every wave has been destroyed. Earth is safe.", new Color(0.4f, 1f, 0.5f));
        }
    }

    void OnFleetLanded()
    {
        if (state != State.Playing) return;
        ship.Explode();
        EndGame("GAME OVER", "The aliens reached your ship - there's no stopping them now.", new Color(1f, 0.3f, 0.3f));
    }

    void OnShipHit()
    {
        if (state != State.Playing) return;

        lives--;
        ui.SetLives(lives);
        if (lives <= 0)
        {
            ship.Explode();
            EndGame("GAME OVER", "Your ship was destroyed.", new Color(1f, 0.3f, 0.3f));
        }
    }

    void EndGame(string title, string message, Color color)
    {
        state = State.GameOver;
        fleet.Halt();
        ship.ControlsEnabled = false;
        PlayerPrefs.SetInt(HighScoreKey, highScore);
        PlayerPrefs.Save();

        ui.ShowOverlay(title,
            $"{message}\n\nScore {score}      High score {highScore}\nReached wave {levelIndex + 1} of {levels.Length}\n\nPress ENTER to play again",
            color);
    }

    static void ClearProjectiles()
    {
        foreach (Dart dart in FindObjectsByType<Dart>())
        {
            Destroy(dart.gameObject);
        }
        foreach (AlienBullet bullet in FindObjectsByType<AlienBullet>())
        {
            Destroy(bullet.gameObject);
        }
    }
}
