using System;
using System.Collections;
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
    [Tooltip("The spiked ice shell drawn over ice statues (samurai killed while held in ice). Wired by Ninja/Apply Boon Art; empty keeps just the icy tint and halo.")]
    [SerializeField] Sprite iceShell;

    public Sprite IceShell => iceShell;

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
    [Tooltip("Shieldbearers take this many hits to fall.")]
    [SerializeField] int shieldHealth = 2;
    [Tooltip("How far above the top of the view the army's front rank starts before it marches in.")]
    [SerializeField] float entryMargin = 1f;

    [Header("Divers")]
    [Tooltip("Seconds a diver flashes in place before it breaks formation.")]
    [SerializeField] float diveWarning = 0.6f;
    [Tooltip("Seconds the swoop takes from its slot to below the screen.")]
    [SerializeField] float diveDuration = 1.7f;
    [Tooltip("Seconds a diver takes to glide back into its slot from above the screen.")]
    [SerializeField] float diveReturn = 1.2f;

    public event Action<Samurai, KillSource> SamuraiKilled;
    public event Action ArmyDefeated;
    public event Action ArmyReachedRonin;

    readonly List<Samurai> soldiers = new List<Samurai>();
    readonly Collider[] overlapBuffer = new Collider[8];
    readonly List<Dive> dives = new List<Dive>();

    // A samurai out of formation. Its slot keeps marching with the army so it can fly back into place.
    class Dive
    {
        public Samurai soldier;
        public Vector3 slot;
        public Vector3 p0, p1, p2, p3;
        public float elapsed;
        public int phase;
        public bool hitRonin;
        public readonly HashSet<Blocker> trampled = new HashSet<Blocker>();
    }

    LevelConfig level;
    Transform target;
    Camera cam;
    int startingCount;
    float direction = 1f;
    float dropRemaining;
    float nextShotTime;
    float nextSpearTime;
    float nextDiveTime;
    float landingY;
    float blockerTopY;
    float frozenUntil;
    float shownFrost;
    float marchTravel;
    float entryOffset;
    int marchBeat;
    bool advancing;

    // The classic four-note march, falling a step each beat.
    static readonly float[] MarchPitches = { 1f, 0.94f, 0.89f, 0.84f };

    public int Remaining => soldiers.Count;
    // An army still marching into the field can't be hit or targeted, so it reads as empty to everything outside.
    public IReadOnlyList<Samurai> Soldiers => Entering ? (IReadOnlyList<Samurai>)Array.Empty<Samurai>() : soldiers;
    // True from SpawnStage until Advance: the army is marching in and untouchable.
    public bool Entering { get; private set; }

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
    public bool Frozen => Time.time < frozenUntil;
    // How much of the speed-up as the army thins it actually gets (Still Water halves it).
    public float SpeedUpScale { get; set; } = 1f;

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
        frozenUntil = 0f;
        shownFrost = 0f;

        float spacingX = Mathf.Min(maxSpacingX, 0.6f * 2f * HalfWidth() / Mathf.Max(1, config.columns - 1));
        // The formation starts with its front rank just above the view, then MarchIn brings it down to its slots.
        float frontRowY = config.topRowY - (config.rows - 1) * spacingY;
        entryOffset = Mathf.Max(0f, CameraShake.Center.y + cam.orthographicSize + entryMargin + samuraiRadius - frontRowY);
        Entering = true;
        for (int col = 0; col < config.columns; col++)
        {
            for (int row = 0; row < config.rows; row++)
            {
                Vector3 position = new Vector3((col - (config.columns - 1) / 2f) * spacingX, config.topRowY - row * spacingY + entryOffset, 0);
                Samurai soldier = Instantiate(samuraiPrefab, position, Quaternion.identity, transform);
                SamuraiKind kind = KindForRow(config, row);
                soldier.Init(this, col, PointsFor(kind, row), config.samuraiColor, ArtFor(kind), kind,
                    kind == SamuraiKind.Taisho ? taishoHealth : kind == SamuraiKind.Shieldbearer ? shieldHealth : 1);
                soldier.SetTouchable(false);
                soldiers.Add(soldier);
            }
        }
        startingCount = soldiers.Count;
        samuraiCounter.SetCount(soldiers.Count);
    }

    // Marches the new formation down from above the view into its slots, to the drum. Nothing can hit it on the way.
    public IEnumerator MarchIn(float seconds)
    {
        float travelled = 0f;
        for (float t = 0f; t < seconds && soldiers.Count > 0;)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            // Eases out so the ranks slow into place rather than stopping dead.
            float u = Mathf.Clamp01(t / seconds);
            float target = entryOffset * (1f - (1f - u) * (1f - u));
            float step = target - travelled;
            travelled = target;
            MarchBeat(step);
            foreach (Samurai soldier in soldiers) soldier.Body.MovePosition(soldier.Body.position + Vector3.down * step);
        }
    }

    public void Advance()
    {
        if (Entering)
        {
            Entering = false;
            foreach (Samurai soldier in soldiers) soldier.SetTouchable(true);
        }
        advancing = true;
        nextShotTime = Time.time + level.fireInterval;
        nextSpearTime = Time.time + spearInterval;
        nextDiveTime = Time.time + level.diveInterval;
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
        dives.Clear();
        advancing = false;
        Entering = false;
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
        SamuraiKind.Shieldbearer => 40,
        SamuraiKind.Yurei => 35,
        SamuraiKind.Archer => 30,
        _ => row < 2 ? 20 : 10,
    };

    // Taisho wear the commander's look, archers the Bushi's and Ashigaru the foot soldier's, so each kind reads at a glance.
    // Yurei are Bushi ghosts: the Bushi art, drawn translucent and cyan by the samurai itself.
    CharacterArt ArtFor(SamuraiKind kind) => kind switch
    {
        SamuraiKind.Taisho => samuraiByRow[0],
        SamuraiKind.Archer or SamuraiKind.Shieldbearer or SamuraiKind.Yurei => samuraiByRow[Mathf.Min(1, samuraiByRow.Length - 1)],
        _ => samuraiByRow[samuraiByRow.Length - 1],
    };

    float HalfWidth() => cam.orthographicSize * cam.aspect;

    // Speeds up linearly from baseSpeed to baseSpeed * maxSpeedMultiplier as soldiers fall.
    float CurrentSpeed()
    {
        float killedFraction = startingCount > 1 ? (float)(startingCount - soldiers.Count) / (startingCount - 1) : 1f;
        return level.baseSpeed * Mathf.Lerp(1f, level.maxSpeedMultiplier, killedFraction * SpeedUpScale);
    }

    void FixedUpdate()
    {
        if (!advancing || soldiers.Count == 0) return;

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
        foreach (Dive dive in dives) dive.slot += step;
        foreach (Samurai soldier in soldiers)
        {
            if (IsDiving(soldier)) continue;
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
        if (level.diveInterval > 0 && Time.time >= nextDiveTime)
        {
            StartDive();
        }
        UpdateDives(dt);
    }

    bool IsDiving(Samurai soldier) => dives.Exists(d => d.soldier == soldier);

    // A random samurai from behind the front rank breaks formation; the front stays put so the wall and the shooters hold.
    void StartDive()
    {
        nextDiveTime = Time.time + level.diveInterval * UnityEngine.Random.Range(0.7f, 1.3f);
        if (target == null || dives.Count > 0) return;
        List<Samurai> candidates = soldiers.FindAll(s => !s.Encased && FrontSamuraiInColumn(s.Column) != s);
        if (candidates.Count == 0) candidates = soldiers.FindAll(s => !s.Encased);
        if (candidates.Count == 0) return;

        Samurai diver = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        diver.WarnDive();
        dives.Add(new Dive { soldier = diver, slot = diver.Body.position });
    }

    void UpdateDives(float dt)
    {
        for (int i = dives.Count - 1; i >= 0; i--)
        {
            Dive dive = dives[i];
            dive.elapsed += dt;
            switch (dive.phase)
            {
                case 0:
                    // Shudders in place as a warning, then picks its line at the ronin as it is right now.
                    float shake = Mathf.Sin(dive.elapsed * 60f) * 0.08f;
                    dive.soldier.Body.MovePosition(dive.slot + Vector3.right * shake);
                    if (dive.elapsed < diveWarning) break;
                    float side = dive.slot.x < target.position.x ? -1f : 1f;
                    float bottom = CameraShake.Center.y - cam.orthographicSize - 1.5f;
                    dive.p0 = dive.slot;
                    dive.p1 = dive.slot + new Vector3(side * 3.5f, 0.5f, 0);
                    dive.p2 = new Vector3(target.position.x, target.position.y + 3f, 0);
                    dive.p3 = new Vector3(target.position.x - side * 1.5f, bottom, 0);
                    dive.phase = 1;
                    dive.elapsed = 0f;
                    break;
                case 1:
                    float t = Mathf.Clamp01(dive.elapsed / diveDuration);
                    Vector3 position = Bezier(dive.p0, dive.p1, dive.p2, dive.p3, t * t * (1.6f - 0.6f * t));
                    dive.soldier.Body.MovePosition(position);
                    DiveContact(dive, position);
                    if (t < 1f) break;
                    dive.phase = 2;
                    dive.elapsed = 0f;
                    break;
                default:
                    // Drops back in from above the screen and settles into its marching slot.
                    float top = CameraShake.Center.y + cam.orthographicSize + 1.5f;
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dive.elapsed / diveReturn));
                    dive.soldier.Body.MovePosition(Vector3.Lerp(new Vector3(dive.slot.x, top, 0), dive.slot, u));
                    if (u >= 1f) dives.RemoveAt(i);
                    break;
            }
        }
    }

    // A diver barges through blockers and costs the ronin a life if it reaches him.
    void DiveContact(Dive dive, Vector3 position)
    {
        int hits = Physics.OverlapSphereNonAlloc(position, samuraiRadius, overlapBuffer);
        for (int i = 0; i < hits; i++)
        {
            if (overlapBuffer[i].TryGetComponent(out Blocker block) && dive.trampled.Add(block)) block.Hit(position);
        }
        if (!dive.hitRonin && target.TryGetComponent(out Ronin ronin) && (target.position - position).sqrMagnitude < 0.8f * 0.8f)
        {
            dive.hitRonin = true;
            ronin.TakeHit(position);
        }
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    void MarchBeat(float distance)
    {
        marchTravel += distance;
        if (marchTravel < marchBeatDistance) return;
        marchTravel -= marchBeatDistance;
        Sfx.Play(Sfx.MarchStep, 0.8f, MarchPitches[marchBeat]);
        marchBeat = (marchBeat + 1) % MarchPitches.Length;
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
        float frost = Frozen ? 1f : 0f;
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
            if (IsDiving(soldier)) continue;
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

        // Shieldbearers never fire, so the column's front shooter looses over their heads. A front shooter encased in ice
        // holds its whole column's fire.
        Samurai shooter = FrontShooterInColumn(column);
        if (shooter != null && shooter.CanFire) shooter.Fire(level.arrowSpeed);
    }

    // A random living Taisho throws; arrows pass through samurai, so it doesn't need a clear line.
    void ThrowSpear()
    {
        nextSpearTime = Time.time + spearInterval * UnityEngine.Random.Range(0.7f, 1.3f);
        List<Samurai> taisho = soldiers.FindAll(s => s.Kind == SamuraiKind.Taisho && !s.Encased);
        if (taisho.Count == 0) return;
        taisho[UnityEngine.Random.Range(0, taisho.Count)].Fire(level.arrowSpeed);
    }

    public int ClosestColumnTo(float x)
    {
        if (soldiers.Count == 0) return 0;
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

    // The lowest samurai in a column that can shoot, skipping shieldbearers and divers.
    Samurai FrontShooterInColumn(int column)
    {
        Samurai front = null;
        foreach (Samurai soldier in soldiers)
        {
            if (soldier.Column == column && soldier.Kind != SamuraiKind.Shieldbearer && !IsDiving(soldier)
                && (front == null || soldier.Body.position.y < front.Body.position.y))
            {
                front = soldier;
            }
        }
        return front;
    }

    // The lowest living samurai in formation in a column, or null if the column is empty.
    public Samurai FrontSamuraiInColumn(int column)
    {
        Samurai front = null;
        if (Entering) return null;
        foreach (Samurai soldier in soldiers)
        {
            if (soldier.Column == column && !IsDiving(soldier) && (front == null || soldier.Body.position.y < front.Body.position.y))
            {
                front = soldier;
            }
        }
        return front;
    }

    public Samurai NearestSamurai(Vector3 position, float maxDistance)
    {
        Samurai nearest = null;
        if (Entering) return null;
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
        if (Entering) return found;
        foreach (Samurai soldier in soldiers)
        {
            if ((soldier.Body.position - position).sqrMagnitude <= radius * radius) found.Add(soldier);
        }
        return found;
    }

    public void OnSamuraiKilled(Samurai soldier, KillSource source)
    {
        if (!soldiers.Remove(soldier)) return;
        dives.RemoveAll(d => d.soldier == soldier);

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
