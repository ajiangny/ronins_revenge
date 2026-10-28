using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Poguru's first-run tutorial, in place of Stage I, Round 1 (Story/Poguru.md). He pops out of Oni's wisp and offers
// it; taken, five soldiers find Oni before the army (the drills: an archer for the slash, two chargers for the
// shuriken, a spearman for the parry, and a coward who runs with him for the army). Each lesson holds the field still
// at its moment, points at its slot with its keys and lets only that move go on. The army then comes; Poguru bonks
// every arrow at Oni (who can't be hurt until the spear), crosser by the bonk, and if it reaches the cover his rage
// bursts and wipes it out. A spear he can't stop then goes through him, he hands over a heart, and he explains the
// reward, the boon cards and the paths as each first comes up. It's done once round 2 starts. GameManager calls the
// hooks below; a death or a dev jump aborts it, and since nothing is settled until it's finished or skipped, the next
// run offers it again.
public class Tutorial : MonoBehaviour
{
    const float SpearSpeed = 9f;
    // A spear that hasn't landed by now is landed for it, so the beat can't stall.
    const float SpearTimeout = 2.5f;
    const float SlowMotion = 0.35f;
    const float PointerSize = 96f;
    // His annoyance barks, by arrows bonked; past the last, one every RepeatEvery more. He never stops guarding.
    static readonly int[] BlockBarks = { 1, 3, 6, 10, 15, 20, 30, 45 };
    const int RepeatEvery = 15;
    // Blocks at which he's as cross as he gets.
    const float FullAnger = 45f;
    const float RageSpeed = 12f;

    GameManager game;
    bool armed;
    bool guarding;
    bool raged;
    bool spearDone, rewardDone, boonsDone, pathsDone;
    float barkingUntil;
    readonly List<TrainingSoldier> soldiers = new List<TrainingSoldier>();

    // The layer the lessons' UI sits on, over the HUD (DialogueScene.Overlay only shows during a talk).
    RectTransform lessonLayer;
    // The pointer beside a HUD slot while he talks about it, and the glow behind that slot.
    RectTransform pointer;
    Image pointerImage;
    Image slotGlow;
    Sprite[] pointFrames;
    RectTransform pointedSlot;
    // How wide what he points at is drawn (a slot, or the row of lives), so he hovers just past it.
    float pointedWidth;
    // The keys for what he points at, shown beside his pointer so he never has to say them.
    RectTransform keyRow;
    // His yell and the move's keys, hung beside Oni while the lesson holds the field.
    RectTransform prompt;
    bool promptOnRight;

    // A test harness's press for a held lesson (synthetic keys don't reach an unfocused Game view), like
    // DialogueScene.Advance.
    Moves pressed;
    public void Press(Moves move) => pressed = move;

    public bool Armed => armed;
    public bool Active { get; private set; }

    // A new journey is offered it; so is any run after the REPLAY switch at his nest, or a death mid-tutorial.
    public static bool ShouldOffer(SaveData d) => d.tutorialReplay || (!d.tutorialSettled && d.runsStarted == 0);

    public void Bind(GameManager manager) => game = manager;

    // At the start of a run: whether its first round opens on the offer.
    public void Arm(bool offer)
    {
        armed = offer;
        Active = guarding = raged = false;
        spearDone = rewardDone = boonsDone = pathsDone = false;
    }

    static void Settle()
    {
        Save.Data.tutorialSettled = true;
        Save.Data.tutorialReplay = false;
        Save.Write();
    }

    static IEnumerator Wait(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
    }

    // Beat 1: before anything arrives, he pops out and asks. Back (Esc) skips.
    public IEnumerator Offer()
    {
        armed = false;
        Poguru poguru = game.Poguru;
        bool again = Save.Data.tutorialReplay;
        poguru.PopOut();
        yield return new WaitForSeconds(0.7f);
        yield return game.PlayTalk(Dialogue.Find(again ? "Poguru_Tut_Again" : "Poguru_Tut_Wake"), default);

        bool? take = null;
        string muted = MenuUI.MutedHex(false);
        game.Menu.Show("DO YOU REMEMBER?", $"<size=30><color=#{muted}>Poguru can walk you through the first round.</color></size>", new[]
        {
            MenuUI.Item.Button("TUTORIAL", () => take = true, "Practise on a few soldiers first, with Poguru beside you."),
            MenuUI.Item.Button("SKIP", () => take = false, "Straight into the fight. He'll be in your wisp."),
        }, () => take = false);
        yield return new WaitUntil(() => take.HasValue);
        game.Menu.Hide();

        if (take.Value)
        {
            Active = true;
            // He keeps Oni from harm until the lesson's spear.
            game.Ronin.Shielded = true;
            // A death before the end offers it again.
            Save.Data.tutorialReplay = true;
            Save.Write();
            yield return Wait(PoguruSays("Poguru_Tut_Yay"));
            poguru.Hover();
        }
        else
        {
            Settle();
            yield return Wait(PoguruSays("Poguru_Tut_Skip"));
            poguru.ReturnToWisp();
        }
    }

    // Beat 2, before the army: the five soldiers, one lesson each.
    public IEnumerator Drills()
    {
        if (!Active) yield break;
        Ronin ronin = game.Ronin;
        Poguru poguru = game.Poguru;
        Camera cam = Camera.main;
        Vector3 centre = CameraShake.Center;
        float top = centre.y + cam.orthographicSize + 1.5f;
        // Their line, well above the cover, and the coward a step behind it.
        float line = centre.y + cam.orthographicSize * 0.3f;
        Moves learned = Moves.None;
        ronin.Allowed = Moves.None;
        ronin.ControlsEnabled = false;

        // Spread across the road, so each one's lines and lesson read on their own.
        float c = centre.x;
        TrainingSoldier archer = Recruit(TrainingSoldier.Kind.Archer, game.FootSoldierArt, new Vector3(c - 5f, top), new Vector3(c - 5f, line));
        TrainingSoldier front = Recruit(TrainingSoldier.Kind.Charger, game.FootSoldierArt, new Vector3(c - 2f, top), new Vector3(c - 2f, line));
        TrainingSoldier back = Recruit(TrainingSoldier.Kind.Charger, game.FootSoldierArt, new Vector3(c + 0.5f, top), new Vector3(c + 0.5f, line));
        TrainingSoldier spearman = Recruit(TrainingSoldier.Kind.Spearman, game.SpearmanArt, new Vector3(c + 3.5f, top), new Vector3(c + 3.5f, line));
        TrainingSoldier coward = Recruit(TrainingSoldier.Kind.Coward, game.FootSoldierArt, new Vector3(c + 6f, top), new Vector3(c + 6f, line + 1.6f));
        Sfx.Play(Sfx.MarchStep);
        yield return new WaitUntil(() => soldiers.TrueForAll(s => s == null || !s.Moving));
        coward.Tremble(true);
        yield return Line("Tut_Soldiers", 0, archer);
        yield return Line("Tut_Soldiers", 1, archer);
        yield return Line("Tut_Soldiers", 2, null, ronin.Bounds.center);
        yield return Line("Tut_Soldiers", 3, null, poguru.Centre);

        // The slash: the archer steps into Oni's column and shoots straight down. Held mid-flight, Oni's cut breaks
        // the arrow; then he's to cut the archer down, who keeps shooting (and Poguru bonking) till he falls.
        archer.MoveTo(new Vector3(ronin.Bounds.center.x, line));
        yield return new WaitUntil(() => !archer.Moving);
        // Only the soldier of the lesson at hand can be hit: a wide slash mustn't take the chargers early.
        archer.Untouchable = false;
        yield return Line("Tut_Slash_Taunt", 0, archer);
        Arrow arrow = null;
        Vector3 archerAt = archer.transform.position;
        yield return archer.Shoot(game.ArrowPrefab, new Vector3(archerAt.x, ronin.Bounds.center.y), shot => arrow = shot);
        float midway = (archerAt.y + ronin.TopY) / 2f;
        yield return Lesson(Moves.Slash, BoonSlot.Attack, "Poguru_Tut_SlashIt", () => arrow == null || arrow.transform.position.y < midway, archerAt.x);
        learned |= Moves.Slash;
        ronin.Allowed = learned;
        yield return Wait(PoguruSays("Poguru_Tut_TakeHim") - 0.6f);
        ronin.ControlsEnabled = true;
        poguru.Guard(true);
        Vector3 fell = archerAt;
        float nextShot = Time.time + 2.5f;
        while (archer != null && !archer.Dead)
        {
            fell = archer.transform.position;
            if (Time.time >= nextShot)
            {
                nextShot = Time.time + 4f;
                StartCoroutine(archer.Shoot(game.ArrowPrefab, ronin.Bounds.center, null));
            }
            yield return null;
        }
        poguru.Guard(false);
        ronin.ControlsEnabled = false;
        game.SayLine(Dialogue.Find("Tut_Slash_Fall", false), 0, Beside(fell + Vector3.up * SoldierHead));
        yield return new WaitForSeconds(1.4f);

        // The shuriken: the chargers line up in Oni's column, one behind the other, and run at him. Held while
        // they're still well up the field, one shuriken goes through both. One that gets through is bonked back up
        // by Poguru and they come again.
        yield return Line("Tut_Charge_Taunt", 0, front);
        front.Untouchable = back.Untouchable = false;
        bool charged = false;
        while (!charged)
        {
            float cx = ronin.Bounds.center.x;
            front.MoveTo(new Vector3(cx, line), 6f);
            back.MoveTo(new Vector3(cx, line + 1.3f), 6f);
            yield return new WaitUntil(() => !front.Moving && !back.Moving);
            float stop = ronin.TopY + 1.6f;
            front.MoveTo(new Vector3(cx, stop), 2.2f);
            back.MoveTo(new Vector3(cx, stop + 1.3f), 2.2f);
            float holdAt = line - 1.3f;
            yield return Lesson(Moves.Shuriken, BoonSlot.Special, "Poguru_Tut_ShurikenIt", () => front.Dead || front.transform.position.y < holdAt, cx);
            learned |= Moves.Shuriken;
            ronin.Allowed = learned;
            yield return new WaitUntil(() => (front.Dead || !front.Moving) && (back.Dead || !back.Moving));
            charged = front.Dead && back.Dead;
            if (charged) break;
            // Through his shuriken: Poguru headbutts the survivors back up the field to try again.
            poguru.Play(game.PoguruClips.bonk);
            Sfx.Play(Sfx.Deflect, 0.6f, 1.4f);
            if (!front.Dead) front.Knockback(new Vector3(cx, line));
            if (!back.Dead) back.Knockback(new Vector3(cx, line + 1.3f));
            yield return new WaitForSeconds(0.8f);
            // Whoever's left of the pair comes again alone.
            if (front.Dead) front = back;
            if (back.Dead) back = front;
        }
        yield return Wait(PoguruSays("Poguru_Tut_Stopped"));

        // The parry: the spearman throws a cursed yari, unbreakable, that only a parry turns. His riposte skims past
        // the spearman's head on purpose, so he lives to run.
        spearman.MoveTo(new Vector3(ronin.Bounds.center.x + 2.5f, line));
        yield return new WaitUntil(() => !spearman.Moving);
        yield return Line("Tut_Spear_Taunt", 0, spearman);
        spearman.Untouchable = true;
        coward.Untouchable = true;
        var near = new GameObject("NearMiss").transform;
        Func<Transform> aimed = ronin.RiposteTarget;
        ronin.RiposteTarget = () => near;
        bool turned = false;
        Action landed = () => turned = true;
        ronin.Parried += landed;
        while (!turned)
        {
            near.position = spearman.transform.position + new Vector3(1.1f, 1.4f, 0f);
            Arrow spear = null;
            yield return spearman.Shoot(game.ArrowPrefab, ronin.Bounds.center, shot => spear = shot, game.SpearSprite);
            float reach = ronin.ParryRadius + 0.7f;
            yield return Lesson(Moves.Parry, BoonSlot.Parry, "Poguru_Tut_ParryIt",
                () => spear == null || Vector2.Distance(spear.transform.position, ronin.Bounds.center) < reach, spearman.transform.position.x);
            float swung = Time.time;
            while (!turned && Time.time - swung < 0.6f) yield return null;
            if (!turned && spear != null) spear.Pop(Color.white);
        }
        ronin.Parried -= landed;
        learned |= Moves.Parry;
        ronin.Allowed = learned;
        yield return new WaitForSeconds(0.9f);
        ronin.RiposteTarget = aimed;
        Destroy(near.gameObject);

        // They've seen enough: the two left run for the army.
        float said = 0f;
        if (spearman != null && !spearman.Dead)
        {
            said = game.SayLine(Dialogue.Find("Tut_Flee", false), 0, Beside(spearman.transform.position + Vector3.up * SoldierHead));
            spearman.Flee();
            yield return new WaitForSeconds(Mathf.Min(said, 1.2f));
        }
        if (coward != null && !coward.Dead)
        {
            coward.Tremble(false);
            said = game.SayLine(Dialogue.Find("Tut_Flee", false), 1, Beside(coward.transform.position + Vector3.up * SoldierHead));
            coward.Flee(9f);
        }
        yield return Wait(said);
        poguru.Play(game.PoguruClips.cheer);
        yield return Line("Tut_Flee", 2, null, poguru.Centre);
        soldiers.Clear();
        ronin.Allowed = Moves.All;
    }

    TrainingSoldier Recruit(TrainingSoldier.Kind kind, CharacterArt art, Vector3 from, Vector3 to)
    {
        TrainingSoldier soldier = TrainingSoldier.Spawn(art, kind, from);
        soldier.Untouchable = true;
        soldier.MoveTo(to);
        soldiers.Add(soldier);
        return soldier;
    }

    // One line in the field, hung over whoever says it (a soldier, or a point: Oni, Poguru), and its length waited.
    IEnumerator Line(string id, int line, TrainingSoldier over, Vector3? at = null)
    {
        // A soldier's head stands higher than his middle, and he bobs while he talks.
        Vector3 point = over != null ? over.transform.position + Vector3.up * SoldierHead : at ?? CameraShake.Center;
        float said = game.SayLine(Dialogue.Find(id, false), line, Beside(point));
        if (over != null) over.Speak(said);
        yield return Wait(said);
    }

    // One of Poguru's lines, hung over him (not across the bottom of the screen, where he hovers by Oni).
    float PoguruSays(string id) => game.SayLine(Dialogue.Find(id, false), 0, Beside(game.Poguru.Centre));

    const float SoldierHead = 0.35f;

    // The top of the head of whoever stands at this point (a soldier, Oni, Poguru), where his line hangs.
    static Vector3 Beside(Vector3 world) => world + Vector3.up * 0.8f;

    // A held lesson: once `moment` comes the field stops, Poguru's yell and the move's keys hang beside Oni (on the
    // side away from the shot, `threatX`), he points at the move's slot, and only that move lets play go on, fired by
    // the real press (Ronin reads input with time stopped) or a harness's Press.
    IEnumerator Lesson(Moves move, BoonSlot slot, string yellId, Func<bool> moment, float threatX)
    {
        yield return new WaitUntil(moment);
        Ronin ronin = game.Ronin;
        bool wasEnabled = ronin.ControlsEnabled;
        Moves wasAllowed = ronin.Allowed;
        game.HoldForLesson(true);
        ronin.ResetCooldowns();
        ronin.Allowed = move;
        ronin.ControlsEnabled = true;
        game.Poguru.Play(game.PoguruClips.gasp, true);
        Conversation yell = Dialogue.Find(yellId, false);
        game.SayLine(yell, 0, null, false);
        ShowLesson(slot, yell, PromptOnRight(threatX < ronin.Bounds.center.x));

        bool done = false;
        Action fired = () => done = true;
        Action<Vector3, Kunai> thrown = (_, __) => done = true;
        ronin.Slashed += fired;
        ronin.ShurikenThrown += thrown;
        ronin.ParryStarted += fired;
        pressed = Moves.None;
        yield return new WaitUntil(() => done || pressed == move);
        ronin.Slashed -= fired;
        ronin.ShurikenThrown -= thrown;
        ronin.ParryStarted -= fired;
        if (!done) ronin.Perform(move);

        HideLesson();
        game.Poguru.Play(game.PoguruClips.idle, true);
        ronin.Allowed = wasAllowed;
        ronin.ControlsEnabled = wasEnabled;
        game.HoldForLesson(false);
    }

    // The prompt goes on Oni's side away from the shot, unless that side lacks room (the HUD slots and their keys are
    // on the left), when it takes the other.
    bool PromptOnRight(bool awayIsRight)
    {
        RectTransform canvas = (RectTransform)game.Ui.transform;
        float oni = game.Ui.CanvasPoint(game.Ronin.Bounds.center).x + canvas.rect.width / 2f;
        bool roomLeft = oni - PromptWidth - 60f > HudReach;
        bool roomRight = oni + PromptWidth + 60f < canvas.rect.width - 30f;
        return awayIsRight ? roomRight || !roomLeft : !roomLeft;
    }

    const float PromptWidth = 560f;
    // How far right of the screen's left edge the slots, Poguru's pointer and its keys reach.
    const float HudReach = 520f;

    RectTransform LessonLayer()
    {
        if (lessonLayer == null)
        {
            lessonLayer = GameUI.CreateRect("TutorialLayer", game.Ui.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            lessonLayer.anchorMin = Vector2.zero;
            lessonLayer.anchorMax = Vector2.one;
            lessonLayer.pivot = new Vector2(0.5f, 0.5f);
            lessonLayer.offsetMin = lessonLayer.offsetMax = Vector2.zero;
        }
        lessonLayer.SetAsLastSibling();
        return lessonLayer;
    }

    void ShowLesson(BoonSlot slot, Conversation yell, bool onRight)
    {
        RectTransform layer = LessonLayer();
        game.BoonChoice.LiftSlots(layer);
        BuildPointer(layer);
        Point(slot);

        HidePrompt();
        GameUI ui = game.Ui;
        promptOnRight = onRight;
        prompt = GameUI.CreateRect("LessonPrompt", layer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
        prompt.pivot = new Vector2(onRight ? 0f : 1f, 0.5f);
        TextMeshProUGUI words = GameUI.CreateText(prompt, ui.BodyFont, "Yell", Vector2.zero, Vector2.zero, new Vector2(PromptWidth, 50f), 32,
            onRight ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
        words.rectTransform.anchorMin = words.rectTransform.anchorMax = new Vector2(onRight ? 0f : 1f, 0.5f);
        words.rectTransform.pivot = new Vector2(onRight ? 0f : 1f, 0f);
        words.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        words.textWrappingMode = TextWrappingModes.Normal;
        words.text = yell != null && yell.Lines.Length > 0 ? yell.Lines[0].Text : "";
        words.color = GameManager.PoguruColour;
        ui.Style(words, TextStyle.Hud);
        RectTransform keys = ui.CreateKeyRow(prompt, 30, Keys(slot));
        keys.anchorMin = keys.anchorMax = new Vector2(onRight ? 0f : 1f, 0.5f);
        keys.pivot = new Vector2(onRight ? 0f : 1f, 1f);
        keys.anchoredPosition = new Vector2(0f, -6f);
    }

    void HideLesson()
    {
        ClearPointer();
        game.BoonChoice.DropSlots();
        HidePrompt();
    }

    void HidePrompt()
    {
        if (prompt != null) Destroy(prompt.gameObject);
        prompt = null;
    }

    // What each move is pressed with: its keys, and the mouse button drawn as a mouse.
    static KeyGlyph[] Keys(BoonSlot slot) => slot switch
    {
        BoonSlot.Attack => new[] { KeyGlyph.Key("SPACE"), KeyGlyph.MouseLeft },
        BoonSlot.Special => new[] { KeyGlyph.Key("W"), KeyGlyph.Key("↑") },
        _ => new[] { KeyGlyph.Key("SHIFT"), KeyGlyph.Key("↓"), KeyGlyph.MouseRight },
    };

    // Beat 3: the army the coward fetched has marched in. Poguru's had a look.
    public IEnumerator Backup()
    {
        if (!Active) yield break;
        game.Poguru.Hover();
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Backup"), default);
    }

    // Beat 4: the round itself, with him on guard to the end, and his rage waiting for the army to reach the cover.
    public void RoundStarted()
    {
        if (!Active || spearDone) return;
        guarding = true;
        raged = false;
        barkingUntil = 0f;
        Poguru poguru = game.Poguru;
        poguru.ResetBlocks();
        poguru.Anger = 0f;
        poguru.Blocked -= OnBlocked;
        poguru.Blocked += OnBlocked;
        poguru.Guard(true);
        game.Army.Holding -= OnHolding;
        game.Army.Holding += OnHolding;
    }

    void OnBlocked(int count)
    {
        if (!guarding) return;
        game.Poguru.Anger = Mathf.Clamp01(count / FullAnger);
        string id = Array.IndexOf(BlockBarks, count) >= 0 ? $"Poguru_Block_{count}"
            : count > BlockBarks[BlockBarks.Length - 1] && (count - BlockBarks[BlockBarks.Length - 1]) % RepeatEvery == 0 ? "Poguru_Block_More"
            : null;
        // A bark waits for the last to finish, so a hail of arrows doesn't talk over itself.
        // Hung over him, not across the bottom where he hovers.
        if (id != null && Time.unscaledTime >= barkingUntil)
            barkingUntil = Time.unscaledTime + game.SayLine(Dialogue.Find(id, false), 0, Beside(game.Poguru.Centre));
    }

    void OnHolding()
    {
        if (guarding && !raged) StartCoroutine(Rage());
    }

    // Left to Poguru all round, the army reaches the cover and his rage bursts: he rises over Oni shaking and red, and
    // a ring of his soul-fire sweeps the field, cutting down every samurai it passes (a boon-effect kill, no chains).
    IEnumerator Rage()
    {
        raged = true;
        StopGuarding();
        Ronin ronin = game.Ronin;
        Poguru poguru = game.Poguru;
        ronin.ControlsEnabled = false;
        poguru.Anger = 1f;
        Vector3 over = ronin.Bounds.center + Vector3.up * 2.4f;
        poguru.DashTo(new Vector3(over.x, over.y, 0f), null, RageSpeed);
        poguru.Play(game.PoguruClips.annoyed, true);
        float charge = game.SayLine(Dialogue.Find("Poguru_Rage_Charge", false), 0, Beside(over));
        float until = Time.time + Mathf.Max(1.5f, charge - 0.6f);
        while (Time.time < until)
        {
            CameraShake.Add(Time.deltaTime * 0.6f);
            yield return null;
        }

        Vector3 from = poguru.Centre;
        Color fire = GameManager.PoguruColour;
        Sfx.Play(Sfx.LightningStrike, 1f, 0.7f);
        Sfx.Play(Sfx.FireBlast, 1f, 0.8f);
        CameraShake.Add(0.7f);
        ElementFx.Glow(from, new Color(fire.r, fire.g, fire.b, 0.9f), 1f, 14f, 0.6f);
        // The ring grows past the farthest samurai; whoever it passes falls.
        var ring = new GameObject("PoguruRage").AddComponent<SpriteRenderer>();
        ring.sprite = ProceduralSprites.Ring(48);
        ring.sortingOrder = 45;
        ring.transform.position = new Vector3(from.x, from.y, 0f);
        float reach = 4f;
        foreach (Samurai samurai in game.Army.Soldiers) reach = Mathf.Max(reach, Vector2.Distance(samurai.transform.position, from) + 1.5f);
        const float Sweep = 0.9f;
        float started = Time.time;
        while (Time.time - started < Sweep)
        {
            float k = (Time.time - started) / Sweep;
            float radius = Mathf.Lerp(0.3f, reach, 1f - (1f - k) * (1f - k));
            ring.transform.localScale = Vector3.one * (radius * 2f / ring.sprite.bounds.size.x);
            ring.color = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1f, 0.45f, 0.35f, 0f), k);
            foreach (Samurai samurai in new List<Samurai>(game.Army.Soldiers))
                if (Vector2.Distance(samurai.transform.position, from) <= radius) samurai.Kill(KillSource.Effect);
            foreach (Arrow arrow in FindObjectsByType<Arrow>())
                if (Vector2.Distance(arrow.transform.position, from) <= radius) arrow.Pop(fire);
            yield return null;
        }
        Destroy(ring.gameObject);
        foreach (Samurai samurai in new List<Samurai>(game.Army.Soldiers)) samurai.Kill(KillSource.Effect);
        poguru.Anger = 0.7f;
        ronin.ControlsEnabled = true;
    }

    void StopGuarding()
    {
        guarding = false;
        if (game.Army != null) game.Army.Holding -= OnHolding;
        game.Poguru.Blocked -= OnBlocked;
        game.Poguru.Guard(false);
    }

    // Beat 5, as the army falls (still in play, so the hit counts): he celebrates (or, if his rage did the work, yells
    // at Oni), then a spear he can't stop goes through him and takes a life, and he hands over a heart to win it back.
    // Without a life to spare the spear is left out.
    public IEnumerator SpearAndHeart()
    {
        if (!Active || spearDone) yield break;
        spearDone = true;
        StopGuarding();
        Ronin ronin = game.Ronin;
        Poguru poguru = game.Poguru;
        poguru.Hover();
        // Nothing else may hit him: only the spear's blow counts.
        foreach (Arrow arrow in FindObjectsByType<Arrow>()) arrow.Pop(Color.white);
        if (raged)
        {
            Conversation scold = Dialogue.Find("Poguru_Tut_Raged");
            for (int i = 0; i < scold.Lines.Length; i++)
            {
                bool oni = scold.Lines[i].Speaker == "Oni";
                yield return Wait(game.SayLine(scold, i, Beside(oni ? ronin.Bounds.center : poguru.Centre)));
            }
        }
        else
        {
            poguru.Play(game.PoguruClips.cheer, true);
            yield return Wait(PoguruSays("Poguru_Tut_Win") - 0.8f);
        }
        poguru.Anger = 0f;
        ronin.ControlsEnabled = true;
        ronin.Shielded = false;
        if (game.MaxLives < 2) yield break;
        game.EnsureLives(2);
        yield return new WaitForSeconds(0.6f);

        Camera cam = Camera.main;
        float half = cam.orthographicSize * cam.aspect;
        Vector3 centre = CameraShake.Center;
        // From the top corner farther from him, just off screen.
        float side = ronin.Bounds.center.x < centre.x ? 1f : -1f;
        Vector3 from = new Vector3(centre.x + side * (half + 1f), centre.y + cam.orthographicSize + 1f, 0f);
        int livesBefore = game.Lives;
        Arrow spear = game.SpawnSpear(from, SpearSpeed);
        // He throws himself into its path, between it and Oni.
        Vector3 block = Vector3.Lerp(ronin.Bounds.center, from, 0.3f);
        poguru.DashTo(new Vector3(block.x, block.y, 0f), null, 20f);
        poguru.Play(game.PoguruClips.gasp, true);
        PoguruSays("Poguru_Tut_LookOut");
        Time.timeScale = SlowMotion;
        float slowUntil = Time.unscaledTime + 1.1f;
        float launched = Time.time;
        bool ghosted = false;
        while (spear != null && game.Lives == livesBefore && Time.time - launched < SpearTimeout)
        {
            // It homes in on him, so it always lands.
            Vector3 to = ronin.Bounds.center - spear.transform.position;
            to.z = 0f;
            if (to.sqrMagnitude > 0.01f) spear.Turn(to.normalized * SpearSpeed);
            if (!ghosted && Vector2.Distance(spear.transform.position, poguru.Centre) < 1f)
            {
                ghosted = true;
                poguru.Ghost(1f);
            }
            // Only its own slow-down is undone: a pause opened meanwhile keeps the clock stopped.
            if (Time.unscaledTime > slowUntil && Mathf.Approximately(Time.timeScale, SlowMotion)) Time.timeScale = 1f;
            yield return null;
        }
        if (Mathf.Approximately(Time.timeScale, SlowMotion)) Time.timeScale = 1f;
        // God mode, or his blink after an arrow, would turn it away; the lesson needs the blow.
        if (game.Lives == livesBefore) game.ForceHit();
        if (spear != null) Destroy(spear.gameObject);
        yield return new WaitForSeconds(1f);

        poguru.Hover();
        // The lives are lifted out of the dark and pointed at on the line about them.
        RectTransform lives = game.Ui.LivesRow;
        Transform livesHome = lives.parent;
        int livesIndex = lives.GetSiblingIndex();
        lives.SetParent(game.Scene.Overlay, false);
        BuildPointer(game.Scene.Overlay);
        float livesWidth = game.MaxLives * (lives.childCount > 0 ? ((RectTransform)lives.GetChild(0)).sizeDelta.x + 6f : 40f);
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Hearts"), new GameManager.TalkOptions
        {
            keepHud = true,
            poguruRight = true,
            onLine = line => PointAt(line == 2 ? lives : null, livesWidth),
        });
        ClearPointer();
        lives.SetParent(livesHome, false);
        lives.SetSiblingIndex(livesIndex);

        // The heart goes down on his row, on its emptier side.
        float x = ronin.Bounds.center.x < centre.x ? centre.x + half * 0.45f : centre.x - half * 0.45f;
        Vector3 spot = new Vector3(x, ronin.Bounds.center.y + 1.2f, 0f);
        bool there = false;
        poguru.DashTo(spot + Vector3.up * 0.6f, () => there = true, 8f);
        float flying = Time.time;
        while (!there && Time.time - flying < 3f) yield return null;
        Heart heart = game.SpawnHeart(spot);
        poguru.Play(game.PoguruClips.point, true);
        PoguruSays("Poguru_Tut_Collect");
        // A heart he can't use (lives somehow full) waits on his row for the round's end instead of holding the lesson.
        yield return new WaitUntil(() => heart == null || game.Lives >= game.MaxLives);
        poguru.Hover();
        poguru.Play(game.PoguruClips.cheer);
        yield return Wait(PoguruSays("Poguru_Tut_Collected"));
    }

    // Beat 6: the round's soul hangs mid-field; he half-recognises it.
    public IEnumerator RewardShown()
    {
        if (!Active || rewardDone) yield break;
        rewardDone = true;
        // Into its hover (RewardSpirit's materialize time), so the talk holds it still on show.
        yield return new WaitForSeconds(0.75f);
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Reward"), default);
    }

    // Beat 7: over the first boon screen, lightly dimmed so the cards stay readable; nothing can be picked meanwhile.
    public IEnumerator BoonsShown()
    {
        if (!Active || boonsDone) yield break;
        boonsDone = true;
        game.BoonChoice.InputLocked = true;
        yield return null;
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Boons"), new GameManager.TalkOptions { keepHud = true, dim = 0.25f });
        game.BoonChoice.InputLocked = false;
    }

    // Beat 8: the first paths.
    public IEnumerator PathsShown()
    {
        if (!Active || pathsDone) yield break;
        pathsDone = true;
        // Once they've formed, and lightly dimmed, so the talk is about paths he can see.
        yield return new WaitForSeconds(1f);
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Paths"), new GameManager.TalkOptions { keepHud = true, dim = 0.3f });
    }

    // Beat 9: round 2's army has marched in; he's satisfied and goes back in the wisp.
    public IEnumerator Finish()
    {
        if (!Active) yield break;
        Active = false;
        game.Poguru.Hover();
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Done"), default);
        Settle();
        game.Poguru.ReturnToWisp();
    }

    // A death or a jump elsewhere: he goes back in the wisp and nothing is settled, so it's offered again.
    public void Abort()
    {
        armed = false;
        if (!Active && !guarding) return;
        Active = false;
        StopGuarding();
        game.Ronin.Shielded = false;
        game.Ronin.Allowed = Moves.All;
        game.Poguru.Anger = 0f;
        game.BoonChoice.InputLocked = false;
        HideLesson();
        foreach (TrainingSoldier soldier in soldiers) if (soldier != null) Destroy(soldier.gameObject);
        soldiers.Clear();
        game.Poguru.ReturnToWisp();
    }

    void BuildPointer(RectTransform layer)
    {
        ClearPointer();
        pointFrames = game.Poguru.PointFrames;
        slotGlow = GameUI.CreateRect("SlotGlow", layer, Vector2.zero, Vector2.zero, Vector2.one * 170f).gameObject.AddComponent<Image>();
        slotGlow.sprite = ProceduralSprites.SoftGlow;
        slotGlow.raycastTarget = false;
        slotGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        slotGlow.transform.SetAsFirstSibling();
        pointer = GameUI.CreateRect("PoguruPointer", layer, Vector2.zero, Vector2.zero, Vector2.one * PointerSize);
        pointer.pivot = new Vector2(0f, 0.5f);
        pointerImage = pointer.gameObject.AddComponent<Image>();
        pointerImage.raycastTarget = false;
        pointerImage.preserveAspect = true;
        if (pointFrames != null)
        {
            // His frames face right; he points left, at the slot.
            pointerImage.sprite = pointFrames[0];
            pointer.localScale = new Vector3(-1f, 1f, 1f);
            pointer.pivot = new Vector2(1f, 0.5f);
        }
        else
        {
            // Without his frames, an arrow in his colour.
            pointerImage.sprite = ProceduralSprites.ArrowDown;
            pointerImage.color = GameManager.PoguruColour;
            pointer.sizeDelta = Vector2.one * 56f;
            pointer.localRotation = Quaternion.Euler(0f, 0f, -90f);
            // Pivoted on its tip, which turns to face the slot.
            pointer.pivot = new Vector2(0.5f, 0f);
        }
        PointAt(null, 0f);
    }

    void Point(BoonSlot slot)
    {
        RectTransform rect = game.BoonChoice.SlotRect(slot);
        PointAt(rect, rect.sizeDelta.x, Keys(slot));
    }

    void PointAt(RectTransform target, float width, KeyGlyph[] keys = null)
    {
        pointedSlot = target;
        pointedWidth = width;
        if (pointer != null) pointer.gameObject.SetActive(pointedSlot != null);
        if (slotGlow != null) slotGlow.gameObject.SetActive(pointedSlot != null);
        if (keyRow != null) Destroy(keyRow.gameObject);
        keyRow = null;
        if (pointer != null && pointedSlot != null && keys != null)
        {
            keyRow = game.Ui.CreateKeyRow(pointer.parent, 30, keys);
            keyRow.anchorMin = keyRow.anchorMax = Vector2.zero;
            keyRow.pivot = new Vector2(0f, 0.5f);
        }
        if (pointedSlot != null) Sfx.Play(Sfx.UiMove, 0.5f, 1.4f);
    }

    void ClearPointer()
    {
        if (pointer != null) Destroy(pointer.gameObject);
        if (slotGlow != null) Destroy(slotGlow.gameObject);
        if (keyRow != null) Destroy(keyRow.gameObject);
        keyRow = null;
        pointer = null;
        slotGlow = null;
        pointedSlot = null;
    }

    // The pointer bobs toward the slot, its frames cycling, and the slot's glow pulses; the lesson's prompt rides
    // beside Oni. Unscaled: lessons and talks hold time.
    void Update()
    {
        float now = Time.unscaledTime;
        if (prompt != null)
        {
            prompt.anchoredPosition = game.Ui.CanvasPoint(game.Ronin.Bounds.center + Vector3.right * (promptOnRight ? 1.3f : -1.3f));
            prompt.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(now * 8f));
        }
        if (pointer == null || pointedSlot == null) return;
        // Both are anchored and pivoted at their bottom-left (the lives row at its left middle).
        Vector2 size = new Vector2(pointedWidth, pointedSlot.sizeDelta.y);
        Vector2 corner = pointedSlot.anchoredPosition - Vector2.Scale(pointedSlot.pivot, pointedSlot.sizeDelta);
        Vector2 middle = corner + size / 2f;
        float bob = Mathf.Abs(Mathf.Sin(now * 4f)) * 14f;
        pointer.anchoredPosition = middle + new Vector2(size.x / 2f + 18f + bob, 0f);
        // The keys sit just past where the pointer reaches, still, so they read easily.
        if (keyRow != null) keyRow.anchoredPosition = middle + new Vector2(size.x / 2f + 18f + 14f + PointerSize + 16f, 0f);
        if (pointFrames != null) pointerImage.sprite = pointFrames[(int)(now * 8f) % pointFrames.Length];
        slotGlow.rectTransform.anchoredPosition = middle;
        Color glow = GameManager.PoguruColour;
        slotGlow.color = new Color(glow.r, glow.g, glow.b, 0.45f + 0.25f * Mathf.Sin(now * 5f));
    }
}
