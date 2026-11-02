using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Talking: Poguru's barks and warnings, talk scenes and subtitled lines, the speakers' close-ups, and the hooks
// the tutorial drives.
public partial class GameManager
{
    // Poguru pops out of the wisp for a line and goes back in. Nothing is picked (or marked heard) unless it can play:
    // not in the tutorial, never over a full line or while he's already out, and only in play, between beats or in
    // the temple.
    IEnumerator PoguruBark(DialogueEvent evt, float chance = 1f, BossKind? boss = null, BossKind? nextBoss = null, string just = null)
    {
        if (tutorial.Active || poguru.IsOut || FullLinePlaying) yield break;
        if (state != State.Playing && state != State.Transition && state != State.Hub) yield break;
        if (Random.value >= chance) yield break;
        Conversation said = Dialogue.Pick("Poguru", evt, BarkContext(boss, nextBoss, just));
        if (said == null || said.Lines.Length == 0) yield break;
        poguru.PopOut();
        yield return new WaitForSecondsRealtime(0.5f);
        // A roast can draw a word back from Oni, said over his own head.
        for (int i = 0; i < said.Lines.Length; i++)
        {
            Vector3? over = said.Lines[i].Speaker == "Oni" ? new Vector3(ronin.Bounds.center.x, ronin.Bounds.max.y + 0.3f, 0f)
                : poguru.IsOut ? poguru.Centre + Vector3.up * 0.8f : (Vector3?)null;
            yield return new WaitForSecondsRealtime(SayLine(said, i, over));
        }
        // In the temple he goes to his nest instead.
        if (state == State.Hub && safeArea != null) poguru.PerchAt(NestPoint);
        else poguru.ReturnToWisp();
    }

    DialogueContext BarkContext(BossKind? boss = null, BossKind? nextBoss = null, string just = null) =>
        Dialogue.Context(loadout, null, lives, false, boss, nextBoss, null, state == State.Hub ? 0 : stageIndex + 1, state == State.Hub ? 0 : roundIndex + 1, just);

    // Whether a bark could start now: PoguruBark's own checks, without its dice.
    bool CanBark => !tutorial.Active && !poguru.IsOut && !FullLinePlaying
        && (state == State.Playing || state == State.Transition || state == State.Hub);

    bool HasFirstLine(string key) => Dialogue.CanPick("Poguru", DialogueEvent.PoguruFirst, BarkContext(null, null, key));

    // Records a first (Firsts) and, when it's news, Poguru pops out with his word on it (PoguruFirst). One he has a
    // word for but can't say yet (he's already out, a line is playing) stays unrecorded, so the next time it comes up
    // gets it. Nothing is recorded in the tutorial, which is its own introduction.
    void FirstBark(string key)
    {
        if (tutorial.Active || Save.Data.FirstIn(key) >= 0) return;
        bool line = HasFirstLine(key);
        if (line && !CanBark) return;
        Save.First(key);
        if (line) StartCoroutine(PoguruBark(DialogueEvent.PoguruFirst, 1f, null, null, key));
    }

    // The same for a thing whose screen freezes the field (a bead, a thread, the Sword Arts): his word comes as a short
    // talk over the frozen field, then `show` opens the screen.
    void FirstThen(string key, System.Action show)
    {
        Conversation said = !tutorial.Active && Save.First(key)
            ? Dialogue.Pick("Poguru", DialogueEvent.PoguruFirst, BarkContext(null, null, key)) : null;
        if (said == null) show();
        else StartCoroutine(TalkThen(said, show));
    }

    IEnumerator TalkThen(Conversation said, System.Action then)
    {
        yield return PlayTalk(said, new TalkOptions { keepHud = true, dim = 0.3f });
        then();
    }

    // The kinds of soldier in this army never met before. Those he has no word for are recorded; the first he has a
    // word for is recorded and returned for his bark, and any others wait for a later army, one word a round.
    string NewEnemies()
    {
        string news = null;
        foreach (Samurai s in army.Soldiers)
        {
            string key = Firsts.Enemy(s.Kind);
            if (Save.Data.FirstIn(key) >= 0) continue;
            if (!HasFirstLine(key)) Save.First(key);
            else if (news == null && CanBark)
            {
                Save.First(key);
                news = key;
            }
        }
        return news;
    }

    void OnDiveStarted()
    {
        if (state == State.Playing) FirstBark(Firsts.Divers);
    }

    bool FullLinePlaying => dialogueScene.IsPlaying || (speakingFullLine && (voice.isPlaying || oniVoice.isPlaying || soulVoice.isPlaying));

    IEnumerator SoulBark()
    {
        yield return new WaitForSecondsRealtime(0.8f);
        yield return PoguruBark(DialogueEvent.PoguruSoul);
    }

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

    // Out of the wisp mid-fight because a boss's move is coming (Utsusemi's lasers and sword): he calls it and hovers
    // over where Oni should go, or at his shoulder, until the boss sends him back with Boss.PoguruRest.
    bool poguruWarning;

    void PoguruWarns(string id, Vector3? over)
    {
        if (tutorial.Active || poguru == null) return;
        poguruWarning = true;
        if (over is Vector3 spot) poguru.DashTo(spot, null, 18f);
        else poguru.Hover();
        if (id != null && WarningSpoken()) Bark(WarningTake(id));
    }

    // His warnings are a reminder, not a running commentary. He speaks them in the first run they come up, then only
    // every two or three runs, and only two or three times in such a run; the rest of the time he just points.
    int warnRun = -1, warnsLeft;

    bool WarningSpoken()
    {
        if (warnRun != Save.Run)
        {
            warnRun = Save.Run;
            SaveData d = Save.Data;
            bool talks = d.poguruWarnRun == 0 || Save.Run - d.poguruWarnRun >= d.poguruWarnGap;
            warnsLeft = talks ? Random.Range(2, 4) : 0;
            if (talks)
            {
                d.poguruWarnRun = Save.Run;
                d.poguruWarnGap = Random.Range(2, 4);
                Save.Write();
            }
        }
        if (warnsLeft <= 0) return false;
        warnsLeft--;
        return true;
    }

    // A warning is written a few ways (`<id>`, `<id>_B`, `<id>_C`); one at random, never the one he said last.
    string lastWarning;

    string WarningTake(string id)
    {
        var takes = new List<string>();
        foreach (string take in new[] { id, id + "_B", id + "_C" })
        {
            if (take != lastWarning && Dialogue.Find(take, false) != null) takes.Add(take);
        }
        lastWarning = takes.Count > 0 ? takes[Random.Range(0, takes.Count)] : id;
        return lastWarning;
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
        // Out of the wisp, Poguru's line follows him as he flies.
        System.Func<Vector3> follow = speaker == "Poguru" && poguru.IsOut ? () => poguru.Centre + Vector3.up * 0.8f : (System.Func<Vector3>)null;
        if (subtitled && over is Vector3 head)
            ui.ShowSubtitleOver(speaker, text, colour, seconds + 0.6f, ui.CanvasPoint(head) + ((RectTransform)ui.transform).rect.size / 2f, follow);
        else if (subtitled) ui.ShowSubtitle(speaker, text, colour, seconds + 0.6f);
        if (speaker == "Poguru") poguru.Talk(seconds);
        return seconds + 0.3f;
    }

    // Stops a field line (SayLine) this speaker is still saying.
    public void CutLine(string speaker)
    {
        AudioSource source = speaker == "Poguru" ? poguruVoice : oniVoice;
        if (source.isPlaying) source.Stop();
        if (speaker == "Poguru") poguru.Hush();
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

    // Holds the field still for a lesson that waits on the player (the tutorial's parry). The tutorial's pause can
    // open over it and hands the hold back on resume.
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
        "Akagane" or "Mizore" or "Hibiki" or "Asagi" or "Oliver" => soulVoice,
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
            // The souls' paintings fill their canvases edge to edge, so they're drawn smaller than Oni's to match faces;
            // Mizore's crane crest towers over her eyes, so she's drawn a little larger to bring them level with Oni's,
            // and Asagi a little larger too, to stand her bowl up near his eyes. Akagane, the captain, stands screen-tall,
            // a little taller than Oni.
            case "Akagane": return SoulSpeaker(name, "Captain of the Vanguard", Element.Fire, akaganeCloseup, new Vector2(0.62f, 0.24f), 1f);
            case "Mizore": return SoulSpeaker(name, "The Court Physician", Element.Water, mizoreCloseup, new Vector2(0.72f, 0.18f), 0.95f);
            case "Hibiki": return SoulSpeaker(name, "The Village Drummer", Element.Lightning, hibikiCloseup, new Vector2(0.67f, 0.22f));
            case "Asagi": return SoulSpeaker(name, "The Pond Physician", Element.Tide, asagiCloseup, new Vector2(0.63f, 0.26f), 0.88f);
            case "Oliver": return SoulSpeaker(name, "The Smith's Cat", Element.Steel, oliverCloseup, new Vector2(0.38f, 0.19f));
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

    // `eyes` is where the soul's eyes sit in their close-up, 0-1 across and down; `scale` is their height as a share of
    // ScenePortraitHeight. Below 1 they stand shorter than the screen, their painting's top fading out rather than cut;
    // at 1 they're placed like Oni, drawn screen-tall with the top pushed just off screen.
    DialogueScene.Speaker SoulSpeaker(string name, string epithet, Element element, Sprite closeup, Vector2 eyes, float scale = 0.83f) => new DialogueScene.Speaker
    {
        name = name,
        epithet = epithet,
        colour = Boons.Info(element).Color,
        art = closeup,
        focus = eyes,
        height = ScenePortraitHeight * scale,
        mayStandShort = scale < 1f,
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
}
