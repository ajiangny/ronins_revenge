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

    [Header("Effects")]
    [Tooltip("Animated effects; built by Ninja/Apply VFX. Effects fall back to simple glows when it's empty.")]
    [SerializeField] FxLibrary fxLibrary;
    [Tooltip("ElevenLabs sound effects and music; built by Ninja/Apply Audio. The game runs silent when it's empty.")]
    [SerializeField] SfxLibrary sfxLibrary;
    [Tooltip("Real seconds the action freezes when a parry lands, for weight.")]
    [SerializeField] float parryHitStop = 0.06f;
    [Tooltip("How far above the ronin's feet his element aura's flames start, in world units.")]
    [SerializeField] float auraLift = 0.3f;

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
    FxPlayer aura;
    bool hitStopping;

    void Awake()
    {
        // Added before anything measures the view, so CameraShake.Center is the camera's resting spot.
        if (Camera.main.GetComponent<CameraShake>() == null) Camera.main.gameObject.AddComponent<CameraShake>();
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
            if (sfxLibrary != null) Music.Play(sfxLibrary.titleMusic, sfxLibrary.musicVolume);
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
        Sfx.Play(Sfx.StageStart);
        if (sfxLibrary != null) Music.Play(sfxLibrary.battleMusic, sfxLibrary.musicVolume);

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
        // Boon effects bring their own blasts and bolts; the ronin's own hits spark in his projectile's colour.
        if (source != KillSource.Effect)
        {
            Color spark = source == KillSource.Shuriken ? ronin.ShurikenGlow : source == KillSource.Deflected ? ronin.ParryColor : ronin.KunaiGlow;
            Fx.Play(Fx.Impact, samurai.transform.position, Color.Lerp(Color.white, spark, 0.6f), source == KillSource.Shuriken ? 1.4f : 1f);
        }
        effects.OnKill(samurai, source);
        MaybeDropSoul(samurai.transform.position);
    }

    void OnShurikenThrown(Vector3 muzzle)
    {
        if (state == State.Playing) effects.OnShurikenThrown(muzzle);
    }

    void OnParried()
    {
        if (state != State.Playing) return;
        effects.OnParried();
        // Thunder Riposte can strike down the army's last samurai, which already moved on to the next stage
        // or the victory screen; freezing then would stall the stage intro.
        if (state == State.Playing && !hitStopping) StartCoroutine(HitStop(parryHitStop));
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
        Fx.Play(Fx.Impact, soul.transform.position, Boons.Info(soul.Element).Color, 1.6f);
        Sfx.Play(Sfx.SoulCollect);

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
        Color colour = Boons.Info(offer.Boon.Element).Color;
        Color pillar = Color.Lerp(Color.white, colour, 0.7f);
        Fx.Play(Fx.BoonPillar, ronin.Feet, new Color(pillar.r, pillar.g, pillar.b, 0.75f), offer.Boon.Id == BoonId.TidalRenewal ? 1.4f : 1f);
        switch (offer.Boon.Id)
        {
            case BoonId.TidalRenewal:
                lives = Mathf.Min(lives + (int)value, Boons.MaxLives);
                ui.SetLives(lives);
                break;
            case BoonId.GlacierWall:
                blockers.Rebuild(loadout.BlockerHealthMultiplier);
                foreach (Vector3 spot in blockers.BlockerPositions)
                {
                    Fx.Play(Fx.IceBurst, spot, Color.white, 1.3f);
                }
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

        // The animated aura replaces the plain glow when its art exists.
        bool animatedAura = dominant is Element a2 && Fx.Has(Fx.Aura(a2));
        Color glowAura = dominant is Element glow && !animatedAura ? Boons.Info(glow).Color * new Color(1f, 1f, 1f, 0.45f) : Color.clear;
        ronin.SetLook(look, glowAura,
            kunai, StandInTint(attack, kunai), ElementColor(attack),
            shuriken, StandInTint(special, shuriken), ElementColor(special),
            parry is Element p ? Boons.Info(p).Color : Color.white);

        if (dominant != shownElement)
        {
            if (aura != null) aura.Stop();
            aura = null;
            if (dominant is Element changed)
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
        shownElement = dominant;
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

        if (levelIndex + 1 < levels.Length)
        {
            levelIndex++;
            StartCoroutine(BeginLevel());
        }
        else
        {
            EndGame("VICTORY", "Every samurai army has fallen. The temple is safe.", new Color(0.4f, 1f, 0.5f), Sfx.VictorySting);
        }
    }

    void OnArmyReachedRonin()
    {
        if (state != State.Playing) return;
        CameraShake.Add(0.6f);
        ronin.Explode();
        EndGame("DEFEATED", "The samurai reached you - there's no stopping them now.", new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting);
    }

    void OnRoninHit()
    {
        if (state != State.Playing) return;

        lives--;
        ui.SetLives(lives);
        if (lives <= 0)
        {
            ronin.Explode();
            EndGame("DEFEATED", "The ronin has fallen.", new Color(1f, 0.3f, 0.3f), Sfx.DefeatSting);
        }
    }

    void EndGame(string title, string message, Color color, string sting)
    {
        state = State.GameOver;
        Music.Stop(0.6f);
        Sfx.Play(sting);
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
