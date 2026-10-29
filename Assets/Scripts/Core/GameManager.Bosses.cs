using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// The boss fights: the entrance, phase cards, each boss's voice, the shared bar, its fall and the ending at dawn.
public partial class GameManager
{
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

        ClearSummons();
        activeBoss = CreateBoss(Stage.boss, post + Vector3.up * 5f);
        TuneVoice(Stage.boss);
        Boss boss = activeBoss;
        boss.Damaged += (point, source) => OnBossDamaged(boss, point, source);
        boss.Healed += () => ShowBossHealth(boss);
        boss.PoiseChanged += ui.SetBossPoise;
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

    // A soul Kagetsune drags out of the coffin comes back with this share of its health.
    const float SummonHealth = 0.3f;
    // The souls fighting beside the Emperor; they take the usual boss row, above where he stands.
    readonly List<Boss> summons = new List<Boss>();

    // Raises a boss's soul to fight beside the Emperor (Boss.Summon): its first phase only, at a share of its health,
    // gliding down to the boss row at x. It shares his bar while it stands and hits back like any boss; its fall
    // isn't the stage's.
    Boss SummonSoul(BossKind kind, float x)
    {
        if (state != State.Playing || activeBoss == null) return null;
        Camera cam = Camera.main;
        Vector3 post = new Vector3(x, CameraShake.Center.y + cam.orthographicSize - bossDepth, 0);
        Boss soul = CreateBoss(kind, post + Vector3.up * 4f, SummonHealth);
        soul.Summoned = true;
        soul.Damaged += (point, source) => OnBossDamaged(soul, point, source);
        soul.Spoke += OnBossSpoke;
        soul.Defeated += () =>
        {
            score += 500 * (stageIndex + 1);
            highScore = Mathf.Max(highScore, score);
            ui.SetScore(score);
            if (!hitStopping) StartCoroutine(HitStop(0.12f));
            ShowBossHealth(activeBoss);
        };
        soul.Banished += () => ShowBossHealth(activeBoss);
        summons.Add(soul);
        effects.Summons.Add(soul);
        ShowBossHealth(activeBoss);
        StartCoroutine(RaiseSoul(soul, post));
        return soul;
    }

    IEnumerator RaiseSoul(Boss soul, Vector3 post)
    {
        yield return soul.Enter(post, 1f);
        if (soul != null && !soul.IsDead && state == State.Playing) soul.Begin(ronin, army.samuraiPrefab.arrowPrefab);
    }

    // The soul standing beside the boss, if any.
    Boss LiveSummon()
    {
        summons.RemoveAll(s => s == null || s.IsDead);
        effects.Summons.RemoveAll(s => s == null || s.IsDead);
        return summons.Count > 0 ? summons[0] : null;
    }

    // Kagetsune's levy (Boss.Muster): his formation in the stage's colours and under the run's oaths, flying out of
    // `from` into its slots. One at a time, and only mid-fight.
    bool MusterLevy(LevelConfig levy, Vector3 from, Color soul)
    {
        if (state != State.Playing || activeBoss == null || army.Remaining > 0) return false;
        levy = Sworn(levy);
        levy.samuraiColor = Stage.rounds[Stage.rounds.Length - 1].samuraiColor;
        army.Muster(levy, ronin.transform, ronin.TopY, blockers.TopY, from, levy.rows * levy.columns > 20 ? 2.4f : 1.6f, soul);
        return true;
    }

    void ClearSummons()
    {
        foreach (Boss soul in summons) if (soul != null) soul.Banish();
        summons.Clear();
        effects.Summons.Clear();
    }

    // Which boss a speaker is, for its own voice clips.
    static BossKind KindOf(Boss boss) => boss switch
    {
        Utsusemi => BossKind.Utsusemi,
        Shirogane => BossKind.Shirogane,
        Kagetsune => BossKind.Kagetsune,
        _ => BossKind.Benibana,
    };

    Boss CreateBoss(BossKind kind, Vector3 position, float healthScale = 1f)
    {
        int Health(int full) => Mathf.Max(1, Mathf.RoundToInt(full * healthScale));
        switch (kind)
        {
            case BossKind.Utsusemi:
                bool noArt = utsusemiArt == null;
                Utsusemi automaton = Boss.Create<Utsusemi>(noArt ? army.samuraiByRow[0] : utsusemiArt, utsusemiScale, Health(Utsusemi.StartingHealth),
                    position, noArt ? new Color(0.6f, 0.7f, 0.95f) : Color.white);
                if (!noArt)
                {
                    automaton.SetBareIdle(utsusemiBareIdle);
                    automaton.SetSwordFrames(utsusemiDash, utsusemiChop, utsusemiHourSlash);
                    automaton.SetThrowFrames(utsusemiSwordThrow, utsusemiUnarmedIdle);
                    automaton.SetPhaseArt(utsusemiPhaseArt);
                    automaton.SetPhaseFrames(utsusemiPhaseBareIdle, utsusemiPhaseUnarmedIdle, utsusemiPhaseDash, utsusemiPhaseChop,
                        utsusemiPhaseHourSlash, utsusemiPhaseSwordThrow, utsusemiPhaseStop);
                    automaton.SetHyperCharge(utsusemiPhaseHyperCharge);
                }
                return automaton;
            case BossKind.Kagetsune:
                bool hollow = kagetsuneArt == null;
                Kagetsune emperor = Boss.Create<Kagetsune>(hollow ? army.samuraiByRow[0] : kagetsuneArt, kagetsuneScale, Health(Kagetsune.StartingHealth),
                    position, hollow ? new Color(0.55f, 0.42f, 0.32f) : Color.white);
                if (!hollow) emperor.SetPhaseArt(kagetsunePhaseArt);
                return emperor;
            case BossKind.Shirogane:
                bool ghostly = shiroganeArt == null;
                Shirogane blade = Boss.Create<Shirogane>(ghostly ? army.samuraiByRow[Mathf.Min(1, army.samuraiByRow.Length - 1)] : shiroganeArt,
                    shiroganeScale, Health(Shirogane.StartingHealth), position, ghostly ? new Color(0.6f, 0.9f, 1f) : Color.white);
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
                Benibana warlord = Boss.Create<Benibana>(art, benibanaScale, Health(Benibana.StartingHealth), position, tint);
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
            summonBenibana, summonUtsusemi, summonShirogane, echoBenibana, echoUtsusemi, echoShirogane,
            curtain, river, rend, pendulum, harvest, absorbed, levy;
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
                BossLine.RosaryCurtain => curtain,
                BossLine.SoulRiver => river,
                BossLine.Rend => rend,
                BossLine.Pendulum => pendulum,
                BossLine.Harvest => harvest,
                BossLine.Levy => levy,
                BossLine.SoulAbsorbed => absorbed,
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
            curtain = kagetsuneCurtainLine, river = kagetsuneRiverLine, rend = kagetsuneRendLine, pendulum = kagetsunePendulumLine,
            harvest = kagetsuneHarvestLine, absorbed = kagetsuneAbsorbedLine, levy = kagetsuneLevyLine,
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

        AudioClip clip = VoiceFor(KindOf(boss)).Take(line, lastTakes.TryGetValue(line, out int last) ? last : -1, out int take);
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

    void OnBossDamaged(Boss hit, Vector3 point, KillSource source)
    {
        score += 10 * (stageIndex + 1);
        highScore = Mathf.Max(highScore, score);
        ui.SetScore(score);
        ShowBossHealth(activeBoss);
        if (state != State.Playing || source == KillSource.Effect) return;

        if (source == KillSource.Reflected)
        {
            // Her own slash breaks her stance: a heavy hit-stop and a burst of red petals.
            Debris.BurstSprites(point, ronin.art.deathParticles, hit.Colour, 24, 1.2f, 8f);
            Fx.Play(Fx.BeniPetals, point, Color.white, 2.2f);
            Fx.Play(Fx.Impact, point, hit.Colour, 2.5f);
            CameraShake.Add(0.45f);
            if (!hitStopping) StartCoroutine(HitStop(0.15f));
        }
        else
        {
            Color spark = source == KillSource.Shuriken ? ronin.ShurikenGlow : source == KillSource.Deflected ? ronin.ParryColor : ronin.AttackGlow;
            Fx.Play(Fx.Impact, point + (Vector3)Random.insideUnitCircle * 0.6f, Color.Lerp(Color.white, spark, 0.6f), 1.2f);
        }
        effects.OnBossHit(hit, point, source);
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
        if (boss == null) return;
        // A soul summoned beside the Emperor takes the right half of his bar while it stands.
        if (boss == activeBoss && LiveSummon() is Boss soul)
        {
            ui.SetBossHealthSplit(boss.BarFraction, soul.HealthFraction, boss.Title, soul.Title, soul.Colour);
            return;
        }
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
}
