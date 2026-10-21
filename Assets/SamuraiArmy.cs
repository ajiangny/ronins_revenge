using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SamuraiArmy : MonoBehaviour
{
    [Header("Inscribed")]
    [FormerlySerializedAs("alienPrefab")] public Samurai samuraiPrefab;
    [FormerlySerializedAs("alienCounter")] public SamuraiCounter samuraiCounter;
    [Tooltip("Samurai type per row from the top; rows past the end reuse the last entry. Matches PointsForRow.")]
    public CharacterArt[] samuraiByRow;

    [SerializeField] float maxSpacingX = 2f;
    [SerializeField] float spacingY = 1.6f;
    [SerializeField] float edgeMargin = 1f;
    [SerializeField] float dropDistance = 0.5f;
    [SerializeField] float dropSpeed = 8f;
    [SerializeField, FormerlySerializedAs("alienRadius")] float samuraiRadius = 0.45f;

    public event Action<Samurai> SamuraiKilled;
    public event Action ArmyDefeated;
    public event Action ArmyReachedRonin;

    readonly List<Samurai> soldiers = new List<Samurai>();
    readonly Collider[] overlapBuffer = new Collider[8];

    LevelConfig level;
    Transform target;
    Camera cam;
    int startingCount;
    float direction = 1f;
    float dropRemaining;
    float nextShotTime;
    float landingY;
    float blockerTopY;
    bool advancing;

    public int Remaining => soldiers.Count;

    void Awake()
    {
        cam = Camera.main;
    }

    // Builds a fresh stage. The army stays still until Advance() is called.
    public void SpawnStage(LevelConfig config, Transform player, float landingLineY, float blockerTop)
    {
        Clear();
        level = config;
        target = player;
        landingY = landingLineY;
        blockerTopY = blockerTop;
        direction = 1f;
        dropRemaining = 0f;

        float spacingX = Mathf.Min(maxSpacingX, 0.6f * 2f * HalfWidth() / Mathf.Max(1, config.columns - 1));
        for (int col = 0; col < config.columns; col++)
        {
            for (int row = 0; row < config.rows; row++)
            {
                Vector3 position = new Vector3((col - (config.columns - 1) / 2f) * spacingX, config.topRowY - row * spacingY, 0);
                Samurai soldier = Instantiate(samuraiPrefab, position, Quaternion.identity, transform);
                soldier.Init(this, col, PointsForRow(row), config.samuraiColor, samuraiByRow[Mathf.Min(row, samuraiByRow.Length - 1)]);
                soldiers.Add(soldier);
            }
        }
        startingCount = soldiers.Count;
        samuraiCounter.SetCount(soldiers.Count);
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
        foreach (Samurai soldier in soldiers)
        {
            Destroy(soldier.gameObject);
        }
        soldiers.Clear();
        advancing = false;
    }

    static int PointsForRow(int row) => row == 0 ? 30 : row < 3 ? 20 : 10;

    float HalfWidth() => cam.orthographicSize * cam.aspect;

    // Speeds up linearly from baseSpeed to baseSpeed * maxSpeedMultiplier as soldiers fall.
    float CurrentSpeed()
    {
        float killedFraction = startingCount > 1 ? (float)(startingCount - soldiers.Count) / (startingCount - 1) : 1f;
        return level.baseSpeed * Mathf.Lerp(1f, level.maxSpeedMultiplier, killedFraction);
    }

    void FixedUpdate()
    {
        if (!advancing || soldiers.Count == 0) return;

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

        // Move every soldier and track the army's extents in a single pass.
        float left = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.PositiveInfinity;
        foreach (Samurai soldier in soldiers)
        {
            Vector3 position = soldier.Body.position + step;
            soldier.Body.MovePosition(position);
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

        if (bottom - samuraiRadius <= blockerTopY)
        {
            ErodeBlockers();
        }

        // Once the army reaches the ronin's height it can no longer be shot down.
        if (bottom - samuraiRadius <= landingY)
        {
            advancing = false;
            ArmyReachedRonin?.Invoke();
            return;
        }

        if (Time.time >= nextShotTime)
        {
            FireShot();
        }
    }

    // Samurai trample any blockers they move into.
    void ErodeBlockers()
    {
        foreach (Samurai soldier in soldiers)
        {
            int hits = Physics.OverlapSphereNonAlloc(soldier.Body.position, samuraiRadius, overlapBuffer);
            for (int i = 0; i < hits; i++)
            {
                if (overlapBuffer[i].TryGetComponent(out Blocker block))
                {
                    block.Demolish();
                }
            }
        }
    }

    // Only the front soldier of a column fires, so shots never come from behind the front line.
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
            column = soldiers[UnityEngine.Random.Range(0, soldiers.Count)].Column;
        }

        Samurai shooter = null;
        foreach (Samurai soldier in soldiers)
        {
            if (soldier.Column == column && (shooter == null || soldier.Body.position.y < shooter.Body.position.y))
            {
                shooter = soldier;
            }
        }
        shooter.Fire(level.arrowSpeed);
    }

    int ClosestColumnTo(float x)
    {
        Samurai closest = soldiers[0];
        foreach (Samurai soldier in soldiers)
        {
            if (Mathf.Abs(soldier.Body.position.x - x) < Mathf.Abs(closest.Body.position.x - x))
            {
                closest = soldier;
            }
        }
        return closest.Column;
    }

    public void OnSamuraiKilled(Samurai soldier)
    {
        if (!soldiers.Remove(soldier)) return;

        samuraiCounter.SetCount(soldiers.Count);
        SamuraiKilled?.Invoke(soldier);
        if (soldiers.Count == 0)
        {
            advancing = false;
            ArmyDefeated?.Invoke();
        }
    }
}
