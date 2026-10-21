using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SamuraiArmy : MonoBehaviour
{
    [Header("Inscribed")]
    [FormerlySerializedAs("alienPrefab")] public Samurai samuraiPrefab;
    [FormerlySerializedAs("alienCounter")] public SamuraiCounter samuraiCounter;
    [Tooltip("Samurai art from the top rank down: Taisho, Bushi, Bushi, Ashigaru. Each SamuraiKind picks its look from here.")]
    public CharacterArt[] samuraiByRow;

    [SerializeField] float maxSpacingX = 2f;
    [SerializeField] float spacingY = 1.6f;
    [SerializeField] float edgeMargin = 1f;
    [SerializeField] float dropDistance = 0.5f;
    [SerializeField] float dropSpeed = 8f;
    [SerializeField, FormerlySerializedAs("alienRadius")] float samuraiRadius = 0.45f;
    [Tooltip("Distance the army marches per drum beat, so the beat quickens as the army speeds up.")]
    [SerializeField] float marchBeatDistance = 1.2f;
    [Tooltip("Average seconds between Taisho spears. Taisho throw on their own timer, over the heads of the ranks in front.")]
    [SerializeField] float spearInterval = 4.5f;
    [Tooltip("Taisho take this many hits to fall.")]
    [SerializeField] int taishoHealth = 2;

    public event Action<Samurai, KillSource> SamuraiKilled;
    public event Action ArmyDefeated;
    public event Action ArmyReachedRonin;

    readonly List<Samurai> soldiers = new List<Samurai>();
    readonly Collider[] overlapBuffer = new Collider[8];
    // Expiry time of each Frost Kunai chill stack.
    readonly List<float> chillStacks = new List<float>();

    LevelConfig level;
    Transform target;
    Camera cam;
    int startingCount;
    float direction = 1f;
    float dropRemaining;
    float nextShotTime;
    float nextSpearTime;
    float landingY;
    float blockerTopY;
    float chillPerStack;
    float frozenUntil;
    float shownFrost;
    float marchTravel;
    int marchBeat;
    bool advancing;

    // The classic four-note march, falling a step each beat.
    static readonly float[] MarchPitches = { 1f, 0.94f, 0.89f, 0.84f };

    public int Remaining => soldiers.Count;
    public IReadOnlyList<Samurai> Soldiers => soldiers;

    // Middle of the living army, for effects that wash over all of it.
    public Vector3 Centre
    {
        get
        {
            if (soldiers.Count == 0) return transform.position;
            Vector3 sum = Vector3.zero;
            foreach (Samurai soldier in soldiers) sum += soldier.Body.position;
            return sum / soldiers.Count;
        }
    }
    bool Frozen => Time.time < frozenUntil;

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
        chillStacks.Clear();
        frozenUntil = 0f;
        shownFrost = 0f;

        float spacingX = Mathf.Min(maxSpacingX, 0.6f * 2f * HalfWidth() / Mathf.Max(1, config.columns - 1));
        for (int col = 0; col < config.columns; col++)
        {
            for (int row = 0; row < config.rows; row++)
            {
                Vector3 position = new Vector3((col - (config.columns - 1) / 2f) * spacingX, config.topRowY - row * spacingY, 0);
                Samurai soldier = Instantiate(samuraiPrefab, position, Quaternion.identity, transform);
                SamuraiKind kind = KindForRow(config, row);
                soldier.Init(this, col, PointsFor(kind, row), config.samuraiColor, ArtFor(kind), kind,
                    kind == SamuraiKind.Taisho ? taishoHealth : 1);
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
        nextSpearTime = Time.time + spearInterval;
    }

    public void Halt()
    {
        advancing = false;
    }

    public void Clear()
    {
        foreach (Samurai soldier in soldiers)
        {
            Destroy(soldier.gameObject);
        }
        soldiers.Clear();
        advancing = false;
        samuraiCounter.SetCount(0);
    }

    static SamuraiKind KindForRow(LevelConfig config, int row)
    {
        if (config.rowKinds == null || config.rowKinds.Length == 0) return SamuraiKind.Ashigaru;
        return config.rowKinds[Mathf.Min(row, config.rowKinds.Length - 1)];
    }

    // Higher ranks are worth more; archers are worth the most of the rank and file.
    static int PointsFor(SamuraiKind kind, int row) => kind switch
    {
        SamuraiKind.Taisho => 60,
        SamuraiKind.Archer => 30,
        _ => row < 2 ? 20 : 10,
    };

    // Taisho wear the commander's look, archers the Bushi's and Ashigaru the foot soldier's, so each kind reads at a glance.
    CharacterArt ArtFor(SamuraiKind kind) => kind switch
    {
        SamuraiKind.Taisho => samuraiByRow[0],
        SamuraiKind.Archer => samuraiByRow[Mathf.Min(1, samuraiByRow.Length - 1)],
        _ => samuraiByRow[samuraiByRow.Length - 1],
    };

    float HalfWidth() => cam.orthographicSize * cam.aspect;

    // Speeds up linearly from baseSpeed to baseSpeed * maxSpeedMultiplier as soldiers fall.
    float CurrentSpeed()
    {
        float killedFraction = startingCount > 1 ? (float)(startingCount - soldiers.Count) / (startingCount - 1) : 1f;
        float chill = 1f - chillPerStack * chillStacks.Count;
        return level.baseSpeed * Mathf.Lerp(1f, level.maxSpeedMultiplier, killedFraction) * chill;
    }

    void FixedUpdate()
    {
        if (!advancing || soldiers.Count == 0) return;

        chillStacks.RemoveAll(expiry => expiry <= Time.time);
        UpdateFrostTint();
        if (Frozen) return;

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
        MarchBeat(step.magnitude);

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

        // The resting camera position: a screen shake mustn't move the army's turning points.
        float centerX = CameraShake.Center.x;
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
        if (Time.time >= nextSpearTime)
        {
            ThrowSpear();
        }
    }

    void MarchBeat(float distance)
    {
        marchTravel += distance;
        if (marchTravel < marchBeatDistance) return;
        marchTravel -= marchBeatDistance;
        Sfx.Play(Sfx.MarchStep, 0.8f, MarchPitches[marchBeat]);
        marchBeat = (marchBeat + 1) % MarchPitches.Length;
    }

    // Frost Kunai: each hit slows the march for a while. Stacks up to maxStacks; a new hit refreshes the oldest stack.
    public void Chill(float percentPerStack, float seconds, int maxStacks)
    {
        chillPerStack = percentPerStack;
        if (chillStacks.Count >= maxStacks) chillStacks.RemoveAt(0);
        chillStacks.Add(Time.time + seconds);
        UpdateFrostTint();
    }

    // Glacial Shuriken: no marching or shooting until the ice thaws.
    public void Freeze(float seconds)
    {
        frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
        nextShotTime = Mathf.Max(nextShotTime, frozenUntil + 0.3f);
        nextSpearTime = Mathf.Max(nextSpearTime, frozenUntil + 0.3f);
        UpdateFrostTint();
    }

    // Only retints the army when the amount of frost actually changes.
    void UpdateFrostTint()
    {
        float frost = Frozen ? 1f : chillStacks.Count / 3f;
        if (Mathf.Approximately(frost, shownFrost)) return;
        shownFrost = frost;
        foreach (Samurai soldier in soldiers)
        {
            soldier.SetFrost(frost);
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

        Samurai shooter = FrontSamuraiInColumn(column);
        shooter.Fire(level.arrowSpeed);
    }

    // A random living Taisho throws; arrows pass through samurai, so it doesn't need a clear line.
    void ThrowSpear()
    {
        nextSpearTime = Time.time + spearInterval * UnityEngine.Random.Range(0.7f, 1.3f);
        List<Samurai> taisho = soldiers.FindAll(s => s.Kind == SamuraiKind.Taisho);
        if (taisho.Count == 0) return;
        taisho[UnityEngine.Random.Range(0, taisho.Count)].Fire(level.arrowSpeed);
    }

    public int ClosestColumnTo(float x)
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

    // The lowest living samurai in a column, or null if the column is empty.
    public Samurai FrontSamuraiInColumn(int column)
    {
        Samurai front = null;
        foreach (Samurai soldier in soldiers)
        {
            if (soldier.Column == column && (front == null || soldier.Body.position.y < front.Body.position.y))
            {
                front = soldier;
            }
        }
        return front;
    }

    public Samurai NearestSamurai(Vector3 position, float maxDistance)
    {
        Samurai nearest = null;
        float best = maxDistance * maxDistance;
        foreach (Samurai soldier in soldiers)
        {
            float d = (soldier.Body.position - position).sqrMagnitude;
            if (d <= best)
            {
                best = d;
                nearest = soldier;
            }
        }
        return nearest;
    }

    // Copies the matches so callers can kill them while the army list changes.
    public List<Samurai> SamuraiWithin(Vector3 position, float radius)
    {
        var found = new List<Samurai>();
        foreach (Samurai soldier in soldiers)
        {
            if ((soldier.Body.position - position).sqrMagnitude <= radius * radius) found.Add(soldier);
        }
        return found;
    }

    public void OnSamuraiKilled(Samurai soldier, KillSource source)
    {
        if (!soldiers.Remove(soldier)) return;

        samuraiCounter.SetCount(soldiers.Count);
        SamuraiKilled?.Invoke(soldier, source);
        // A boon effect inside the event may already have killed the last samurai and ended the stage.
        if (soldiers.Count == 0 && advancing)
        {
            advancing = false;
            ArmyDefeated?.Invoke();
        }
    }
}
