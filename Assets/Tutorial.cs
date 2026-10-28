using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Poguru's first-run tutorial, in place of Stage I, Round 1 (Story/Poguru.md). He pops out of Oni's wisp before the
// army arrives and offers it; taken, he points out the ability slots, guards the round by bonking arrows, lets a
// spear through to teach hearts, and explains the reward, the boon cards and the paths as each first comes up. It's
// done once round 2 starts. GameManager calls the hooks below at those moments; a death or a dev jump aborts it,
// and since nothing is settled until it's finished or skipped, the next run offers it again.
public class Tutorial : MonoBehaviour
{
    const float SpearSpeed = 9f;
    // A spear that hasn't landed by now is landed for it, so the beat can't stall.
    const float SpearTimeout = 2.5f;
    const float SlowMotion = 0.35f;
    const float PointerSize = 96f;
    // How far the lifted ability slots rise during the controls talk, so the lowest (parry) and its keys clear the
    // talk's plate.
    const float SlotRise = 130f;
    // His annoyance barks, by arrows bonked; at the break he sulks for a few seconds, then comes back counting from 10.
    static readonly int[] BlockBarks = { 1, 3, 6, 10 };
    const int BreakAt = 15;
    const float BreakTime = 5f;

    GameManager game;
    bool armed;
    bool guarding;
    bool spearDone, rewardDone, boonsDone, pathsDone;
    Coroutine dragonBreak;

    // The pointer beside a HUD slot while he talks about it, and the glow behind that slot.
    RectTransform pointer;
    Image pointerImage;
    Image slotGlow;
    Sprite[] pointFrames;
    RectTransform pointedSlot;
    // How wide what he points at is drawn (a slot, or the row of lives), so he hovers just past it.
    float pointedWidth;
    // The keys for what he points at, shown beside his pointer so he never has to say them.
    TextMeshProUGUI keyLabel;
    // "Parry!" with its keys, hung beside Oni while the straggler's arrow hangs in the air.
    TextMeshProUGUI parryPrompt;

    // A test harness's parry press (synthetic keys don't reach an unfocused Game view), like DialogueScene.Advance.
    bool parryPressed;
    public void PressParry() => parryPressed = true;

    public bool Armed => armed;
    public bool Active { get; private set; }

    // A new journey is offered it; so is any run after the REPLAY switch at his nest, or a death mid-tutorial.
    public static bool ShouldOffer(SaveData d) => d.tutorialReplay || (!d.tutorialSettled && d.runsStarted == 0);

    public void Bind(GameManager manager) => game = manager;

    // At the start of a run: whether its first round opens on the offer.
    public void Arm(bool offer)
    {
        armed = offer;
        Active = guarding = false;
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

    // Beat 1: before the army, he pops out and asks. Back (Esc) skips.
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
            MenuUI.Item.Button("TUTORIAL", () => take = true, "Slash, shuriken and parry, with Poguru beside you."),
            MenuUI.Item.Button("SKIP", () => take = false, "Straight into the fight. He'll be in your wisp."),
        }, () => take = false);
        yield return new WaitUntil(() => take.HasValue);
        game.Menu.Hide();

        if (take.Value)
        {
            Active = true;
            // A death before the end offers it again.
            Save.Data.tutorialReplay = true;
            Save.Write();
            yield return Wait(game.Bark("Poguru_Tut_Yay"));
            poguru.Hover();
        }
        else
        {
            Settle();
            yield return Wait(game.Bark("Poguru_Tut_Skip"));
            poguru.ReturnToWisp();
        }
    }

    // Beat 2: the army stands ready; he stands on the right of a talk with the ability slots lifted out of the dark,
    // pointing at each as he names it.
    public IEnumerator ArmyArrived()
    {
        if (!Active) yield break;
        BoonChoiceUI boons = game.BoonChoice;
        boons.LiftSlots(game.Scene.Overlay);
        RaiseSlots(true);
        BuildPointer(game.Scene.Overlay);
        var options = new GameManager.TalkOptions
        {
            keepHud = true,
            poguruRight = true,
            onLine = line => Point(line == 1 ? BoonSlot.Attack : line == 2 ? BoonSlot.Special : line == 3 ? BoonSlot.Parry : (BoonSlot?)null),
        };
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Basics"), options);
        Point(null);
        ClearPointer();
        RaiseSlots(false);
        boons.DropSlots();
    }

    bool slotsRaised;

    void RaiseSlots(bool raise)
    {
        if (slotsRaised == raise) return;
        slotsRaised = raise;
        foreach (BoonSlot slot in new[] { BoonSlot.Attack, BoonSlot.Special, BoonSlot.Parry })
            game.BoonChoice.SlotRect(slot).anchoredPosition += Vector2.up * (raise ? SlotRise : -SlotRise);
    }

    // Beat 3: the round itself, with him on guard.
    public void RoundStarted()
    {
        if (!Active || spearDone) return;
        guarding = true;
        Poguru poguru = game.Poguru;
        poguru.ResetBlocks();
        poguru.Blocked -= OnBlocked;
        poguru.Blocked += OnBlocked;
        poguru.Guard(true);
    }

    void OnBlocked(int count)
    {
        if (!guarding) return;
        if (System.Array.IndexOf(BlockBarks, count) >= 0) game.Bark($"Poguru_Block_{count}");
        if (count >= BreakAt && dragonBreak == null) dragonBreak = StartCoroutine(DragonBreak());
    }

    IEnumerator DragonBreak()
    {
        Poguru poguru = game.Poguru;
        poguru.Guard(false);
        float said = game.Bark("Poguru_Block_Break");
        yield return new WaitForSeconds(Mathf.Min(said, BreakTime));
        poguru.Play(game.PoguruClips.annoyed, true);
        yield return new WaitForSeconds(Mathf.Max(0f, BreakTime - said));
        dragonBreak = null;
        if (!guarding) yield break;
        poguru.ResetBlocks(10);
        poguru.Play(game.PoguruClips.idle, true);
        poguru.Guard(true);
        game.Bark("Poguru_Block_Back");
    }

    void StopGuarding()
    {
        guarding = false;
        if (dragonBreak != null) StopCoroutine(dragonBreak);
        dragonBreak = null;
        game.Poguru.Blocked -= OnBlocked;
        game.Poguru.Guard(false);
    }

    // Beat 4, as the army falls (still in play, so a hit counts). While he celebrates, a straggler runs in and shoots;
    // the field holds still with the arrow at Oni until he parries it. Then a spear he can't stop goes through him and
    // takes a life, and he hands over a heart to win it back. Without a life to spare the spear is left out.
    public IEnumerator SpearAndHeart()
    {
        if (!Active || spearDone) yield break;
        spearDone = true;
        StopGuarding();
        Poguru poguru = game.Poguru;
        poguru.Hover();
        // Nothing else may hit him: only the lesson's shots count.
        foreach (Arrow arrow in FindObjectsByType<Arrow>()) arrow.Pop(Color.white);
        yield return ParryLesson();
        poguru.Hover();
        if (game.MaxLives < 2) yield break;
        game.EnsureLives(2);
        yield return new WaitForSeconds(0.9f);

        Ronin ronin = game.Ronin;
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
        game.Bark("Poguru_Tut_LookOut");
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
        game.Bark("Poguru_Tut_Collect");
        // A heart he can't use (lives somehow full) waits on his row for the round's end instead of holding the lesson.
        yield return new WaitUntil(() => heart == null || game.Lives >= game.MaxLives);
        poguru.Hover();
        poguru.Play(game.PoguruClips.cheer);
        yield return Wait(game.Bark("Poguru_Tut_Collected"));
    }

    // The straggler's shot. Poguru is busy with his victory wiggle, so it's Oni's to parry: once the arrow is in reach
    // the field holds still, he yells, and only a parry lets it go on. A shot that's dodged or cut down is shot again.
    IEnumerator ParryLesson()
    {
        Poguru poguru = game.Poguru;
        Ronin ronin = game.Ronin;
        Camera cam = Camera.main;
        float half = cam.orthographicSize * cam.aspect;
        Vector3 centre = CameraShake.Center;
        poguru.Play(game.PoguruClips.cheer, true);
        float cheering = game.Bark("Poguru_Tut_Win");
        yield return new WaitForSeconds(0.8f);

        // A little to one side of him, so the arrow comes in at an angle he can read.
        float x = ronin.Bounds.center.x + (ronin.Bounds.center.x < centre.x ? 2.5f : -2.5f);
        x = Mathf.Clamp(x, centre.x - half + 1.5f, centre.x + half - 1.5f);
        Straggler straggler = Straggler.Spawn(game.FootSoldierArt, x, centre.y + cam.orthographicSize * 0.4f);
        System.Func<Transform> aimed = ronin.RiposteTarget;
        ronin.RiposteTarget = () => straggler != null && !straggler.Dead ? straggler.transform : null;
        bool parried = false;
        System.Action landed = () => parried = true;
        ronin.Parried += landed;
        yield return new WaitUntil(() => straggler == null || straggler.Standing);
        yield return new WaitForSeconds(Mathf.Max(0.3f, cheering - 1.6f));

        while (!parried && straggler != null && !straggler.Dead)
        {
            Arrow arrow = null;
            yield return straggler.Shoot(game.ArrowPrefab, ronin.Bounds.center, shot => arrow = shot);
            if (arrow == null) break;
            float reach = ronin.ParryRadius + 0.7f;
            while (arrow != null && Vector2.Distance(arrow.transform.position, ronin.Bounds.center) > reach) yield return null;
            if (arrow == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Hold everything with the arrow in the air; only a parry press lets go.
            game.HoldForLesson(true);
            ronin.ControlsEnabled = false;
            poguru.Play(game.PoguruClips.gasp, true);
            // His yell is in the prompt, beside Oni on the far side from the arrow, so nothing hides the shot.
            game.Bark("Poguru_Tut_Parry", false);
            ShowParryPrompt(Dialogue.Find("Poguru_Tut_Parry", false), arrow.transform.position.x < ronin.Bounds.center.x);
            parryPressed = false;
            yield return new WaitUntil(() => parryPressed || ParryPressed());
            HideParryPrompt();
            ronin.ControlsEnabled = true;
            game.HoldForLesson(false);
            ronin.ParryNow();
            float pressed = Time.time;
            while (!parried && Time.time - pressed < 0.6f) yield return null;
            if (!parried && arrow != null) arrow.Pop(Color.white);
        }
        ronin.Parried -= landed;

        // His own arrow comes back for him; if cover took it instead, he falls all the same.
        float flying = Time.time;
        while (straggler != null && !straggler.Dead && Time.time - flying < 1.5f) yield return null;
        if (straggler != null && !straggler.Dead) straggler.Fall();
        ronin.RiposteTarget = aimed;
        poguru.Play(game.PoguruClips.cheer, true);
        yield return Wait(game.Bark("Poguru_Tut_Parried"));
    }

    // The same keys Ronin parries on.
    static bool ParryPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame))
            || (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }

    // Poguru's yell over the parry keys, hung beside Oni on the side the arrow isn't coming from.
    void ShowParryPrompt(Conversation yell, bool onRight)
    {
        HideParryPrompt();
        GameUI ui = game.Ui;
        promptOnRight = onRight;
        parryPrompt = GameUI.CreateText(ui.transform, ui.BodyFont, "ParryPrompt", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 120f),
            34, onRight ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
        parryPrompt.rectTransform.pivot = new Vector2(onRight ? 0f : 1f, 0.5f);
        parryPrompt.textWrappingMode = TextWrappingModes.NoWrap;
        string said = yell != null && yell.Lines.Length > 0 ? yell.Lines[0].Text : "PARRY IT!!";
        parryPrompt.text = $"<color=#{ColorUtility.ToHtmlStringRGB(GameManager.PoguruColour)}>{said}</color>\n{Keys(BoonSlot.Parry)}";
        ui.Style(parryPrompt, TextStyle.Hud);
    }

    bool promptOnRight;

    void HideParryPrompt()
    {
        if (parryPrompt != null) Destroy(parryPrompt.gameObject);
        parryPrompt = null;
    }

    // What each ability is pressed with, as key caps.
    static string Keys(BoonSlot slot) => slot switch
    {
        BoonSlot.Attack => $"{MenuUI.KeyCap("SPACE", false)}  {MenuUI.KeyCap("LEFT CLICK", false)}",
        BoonSlot.Special => $"{MenuUI.KeyCap("W", false)}  {MenuUI.KeyCap("↑", false)}",
        _ => $"{MenuUI.KeyCap("SHIFT", false)}  {MenuUI.KeyCap("↓", false)}  {MenuUI.KeyCap("RIGHT CLICK", false)}",
    };

    // Beat 5: the round's soul hangs mid-field; he half-recognises it.
    public IEnumerator RewardShown()
    {
        if (!Active || rewardDone) yield break;
        rewardDone = true;
        // Into its hover (RewardSpirit's materialize time), so the talk holds it still on show.
        yield return new WaitForSeconds(0.75f);
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Reward"), default);
    }

    // Beat 6: over the first boon screen, lightly dimmed so the cards stay readable; nothing can be picked meanwhile.
    public IEnumerator BoonsShown()
    {
        if (!Active || boonsDone) yield break;
        boonsDone = true;
        game.BoonChoice.InputLocked = true;
        yield return null;
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Boons"), new GameManager.TalkOptions { keepHud = true, dim = 0.25f });
        game.BoonChoice.InputLocked = false;
    }

    // Beat 7: the first paths.
    public IEnumerator PathsShown()
    {
        if (!Active || pathsDone) yield break;
        pathsDone = true;
        // Once they've formed, and lightly dimmed, so the talk is about paths he can see.
        yield return new WaitForSeconds(1f);
        yield return game.PlayTalk(Dialogue.Find("Poguru_Tut_Paths"), new GameManager.TalkOptions { keepHud = true, dim = 0.3f });
    }

    // Beat 8: round 2's army has marched in; he's satisfied and goes back in the wisp.
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
        game.BoonChoice.InputLocked = false;
        RaiseSlots(false);
        game.BoonChoice.DropSlots();
        ClearPointer();
        HideParryPrompt();
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
        keyLabel = GameUI.CreateText(layer, game.Ui.BodyFont, "PointerKeys", Vector2.zero, Vector2.zero, new Vector2(600f, 56f), 30, TextAlignmentOptions.Left);
        keyLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        keyLabel.textWrappingMode = TextWrappingModes.NoWrap;
        game.Ui.Style(keyLabel, TextStyle.Hud);
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
        Point(null);
    }

    void Point(BoonSlot? slot)
    {
        RectTransform rect = slot is BoonSlot s ? game.BoonChoice.SlotRect(s) : null;
        PointAt(rect, rect != null ? rect.sizeDelta.x : 0f, slot is BoonSlot k ? Keys(k) : null);
    }

    void PointAt(RectTransform target, float width, string keys = null)
    {
        pointedSlot = target;
        pointedWidth = width;
        if (pointer != null) pointer.gameObject.SetActive(pointedSlot != null);
        if (slotGlow != null) slotGlow.gameObject.SetActive(pointedSlot != null);
        if (keyLabel != null)
        {
            keyLabel.text = keys ?? "";
            keyLabel.gameObject.SetActive(pointedSlot != null && keys != null);
        }
        if (pointedSlot != null) Sfx.Play(Sfx.UiMove, 0.5f, 1.4f);
    }

    void ClearPointer()
    {
        if (pointer != null) Destroy(pointer.gameObject);
        if (slotGlow != null) Destroy(slotGlow.gameObject);
        if (keyLabel != null) Destroy(keyLabel.gameObject);
        keyLabel = null;
        pointer = null;
        slotGlow = null;
        pointedSlot = null;
    }

    // The pointer bobs toward the slot, its frames cycling, and the slot's glow pulses. Unscaled: the talk holds time.
    void Update()
    {
        // The parry prompt rides beside Oni and swells in time, held or not.
        if (parryPrompt != null)
        {
            parryPrompt.rectTransform.anchoredPosition = game.Ui.CanvasPoint(game.Ronin.Bounds.center + Vector3.right * (promptOnRight ? 1.3f : -1.3f));
            parryPrompt.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 8f));
        }
        if (pointer == null || pointedSlot == null) return;
        float now = Time.unscaledTime;
        // Both are anchored and pivoted at their bottom-left (the lives row at its left middle).
        Vector2 size = new Vector2(pointedWidth, pointedSlot.sizeDelta.y);
        Vector2 corner = pointedSlot.anchoredPosition - Vector2.Scale(pointedSlot.pivot, pointedSlot.sizeDelta);
        Vector2 middle = corner + size / 2f;
        float bob = Mathf.Abs(Mathf.Sin(now * 4f)) * 14f;
        pointer.anchoredPosition = middle + new Vector2(size.x / 2f + 18f + bob, 0f);
        // The keys sit just past where the pointer reaches, still, so they read easily.
        if (keyLabel != null) keyLabel.rectTransform.anchoredPosition = middle + new Vector2(size.x / 2f + 18f + 14f + PointerSize + 16f, 0f);
        if (pointFrames != null) pointerImage.sprite = pointFrames[(int)(now * 8f) % pointFrames.Length];
        slotGlow.rectTransform.anchoredPosition = middle;
        Color glow = GameManager.PoguruColour;
        slotGlow.color = new Color(glow.r, glow.g, glow.b, 0.45f + 0.25f * Mathf.Sin(now * 5f));
    }
}
