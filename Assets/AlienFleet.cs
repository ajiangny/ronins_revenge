using System;
using System.Collections.Generic;
using UnityEngine;

public class AlienFleet : MonoBehaviour
{
    [Header("Inscribed")]
    public Alien alienPrefab;
    public AlienCounter alienCounter;
    [Tooltip("Samurai type per row from the top; rows past the end reuse the last entry. Matches PointsForRow.")]
    public CharacterArt[] samuraiByRow;

    [SerializeField] float maxSpacingX = 2f;
    [SerializeField] float spacingY = 1.6f;
    [SerializeField] float edgeMargin = 1f;
    [SerializeField] float dropDistance = 0.5f;
    [SerializeField] float dropSpeed = 8f;
    [SerializeField] float alienRadius = 0.45f;

    public event Action<Alien> AlienDestroyed;
    public event Action FleetCleared;
    public event Action FleetLanded;

    readonly List<Alien> aliens = new List<Alien>();
    readonly Collider[] overlapBuffer = new Collider[8];

    LevelConfig level;
    Transform target;
    Camera cam;
    int startingCount;
    float direction = 1f;
    float dropRemaining;
    float nextShotTime;
    float landingY;
    float shieldTopY;
    bool advancing;

    public int Remaining => aliens.Count;

    void Awake()
    {
        cam = Camera.main;
    }

    // Builds a fresh wave. The fleet stays still until Advance() is called.
    public void SpawnWave(LevelConfig config, Transform player, float landingLineY, float shieldTop)
    {
        Clear();
        level = config;
        target = player;
        landingY = landingLineY;
        shieldTopY = shieldTop;
        direction = 1f;
        dropRemaining = 0f;

        float spacingX = Mathf.Min(maxSpacingX, 0.6f * 2f * HalfWidth() / Mathf.Max(1, config.columns - 1));
        for (int col = 0; col < config.columns; col++)
        {
            for (int row = 0; row < config.rows; row++)
            {
                Vector3 position = new Vector3((col - (config.columns - 1) / 2f) * spacingX, config.topRowY - row * spacingY, 0);
                Alien alien = Instantiate(alienPrefab, position, Quaternion.identity, transform);
                alien.Init(this, col, PointsForRow(row), config.alienColor, samuraiByRow[Mathf.Min(row, samuraiByRow.Length - 1)]);
                aliens.Add(alien);
            }
        }
        startingCount = aliens.Count;
        alienCounter.SetCount(aliens.Count);
    }

    public void Advance()
    {
        advancing = true;
        nextShotTime = Time.time + level.fireInterval;
    }

    public void Halt()
    {
        advancing = false;
    }

    void Clear()
    {
        foreach (Alien alien in aliens)
        {
            Destroy(alien.gameObject);
        }
        aliens.Clear();
        advancing = false;
    }

    static int PointsForRow(int row) => row == 0 ? 30 : row < 3 ? 20 : 10;

    float HalfWidth() => cam.orthographicSize * cam.aspect;

    // Speeds up linearly from baseSpeed to baseSpeed * maxSpeedMultiplier as aliens die.
    float CurrentSpeed()
    {
        float killedFraction = startingCount > 1 ? (float)(startingCount - aliens.Count) / (startingCount - 1) : 1f;
        return level.baseSpeed * Mathf.Lerp(1f, level.maxSpeedMultiplier, killedFraction);
    }

    void FixedUpdate()
    {
        if (!advancing || aliens.Count == 0) return;

        float dt = Time.fixedDeltaTime;
        Vector3 step;
        if (dropRemaining > 0)
        {
            float drop = Mathf.Min(dropSpeed * dt, dropRemaining);
            dropRemaining -= drop;
            step = Vector3.down * drop;
        }
        else
        {
            step = Vector3.right * (direction * CurrentSpeed() * dt);
        }

        // Move every alien and track the fleet's extents in a single pass.
        float left = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.PositiveInfinity;
        foreach (Alien alien in aliens)
        {
            Vector3 position = alien.Body.position + step;
            alien.Body.MovePosition(position);
            left = Mathf.Min(left, position.x);
            right = Mathf.Max(right, position.x);
            bottom = Mathf.Min(bottom, position.y);
        }

        float centerX = cam.transform.position.x;
        float bound = HalfWidth() - edgeMargin;
        if (dropRemaining <= 0 && (direction > 0 && right >= centerX + bound || direction < 0 && left <= centerX - bound))
        {
            direction = -direction;
            dropRemaining = dropDistance;
        }

        if (bottom - alienRadius <= shieldTopY)
        {
            ErodeShields();
        }

        // Once the fleet reaches the ship's height it can no longer be shot down.
        if (bottom - alienRadius <= landingY)
        {
            advancing = false;
            FleetLanded?.Invoke();
            return;
        }

        if (Time.time >= nextShotTime)
        {
            FireShot();
        }
    }

    // Aliens chew through any shield blocks they move into.
    void ErodeShields()
    {
        foreach (Alien alien in aliens)
        {
            int hits = Physics.OverlapSphereNonAlloc(alien.Body.position, alienRadius, overlapBuffer);
            for (int i = 0; i < hits; i++)
            {
                if (overlapBuffer[i].TryGetComponent(out ShieldBlock block))
                {
                    block.Demolish();
                }
            }
        }
    }

    // Only the bottom alien of a column fires, so shots never come from behind the front line.
    void FireShot()
    {
        nextShotTime = Time.time + level.fireInterval * UnityEngine.Random.Range(0.5f, 1.5f);

        int column;
        if (target != null && UnityEngine.Random.value < level.aimedShotChance)
        {
            column = ClosestColumnTo(target.position.x);
        }
        else
        {
            column = aliens[UnityEngine.Random.Range(0, aliens.Count)].Column;
        }

        Alien shooter = null;
        foreach (Alien alien in aliens)
        {
            if (alien.Column == column && (shooter == null || alien.Body.position.y < shooter.Body.position.y))
            {
                shooter = alien;
            }
        }
        shooter.Fire(level.bulletSpeed);
    }

    int ClosestColumnTo(float x)
    {
        Alien closest = aliens[0];
        foreach (Alien alien in aliens)
        {
            if (Mathf.Abs(alien.Body.position.x - x) < Mathf.Abs(closest.Body.position.x - x))
            {
                closest = alien;
            }
        }
        return closest.Column;
    }

    public void OnAlienKilled(Alien alien)
    {
        if (!aliens.Remove(alien)) return;

        alienCounter.SetCount(aliens.Count);
        AlienDestroyed?.Invoke(alien);
        if (aliens.Count == 0)
        {
            advancing = false;
            FleetCleared?.Invoke();
        }
    }
}
