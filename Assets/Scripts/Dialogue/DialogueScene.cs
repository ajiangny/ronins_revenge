using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A Hades-style talk scene over the frozen field: the visitor (a boss or a soul) painted large at the left, Oni at the
// right, and the line typed into a dark plate between them under the speaker's name. The speaker is lit and the
// listener drops back into shadow. Built in code from GameUI's "minimal ink" parts and run on unscaled time; confirm
// finishes a line's typing, then moves on.
public class DialogueScene : MonoBehaviour
{
    // Who is talking. `art` is their transparent close-up, drawn `height` tall with its `focus` (their eyes, 0-1
    // across and down the art) at the slot's eye point; without it `soulFrames` burn large in their glow instead.
    public struct Speaker
    {
        public string name;
        public string epithet;
        public Color colour;
        public Sprite art;
        public Vector2 focus;
        public float height;
        public Sprite[] soulFrames;
        // Oni stands on the right; everyone else on the left.
        public bool right;
    }

    public struct Line
    {
        public Speaker who;
        public string text;
        public AudioClip clip;
        // The source to speak through, when not the scene's own (Oni's carries none of the bosses' voice filters).
        public AudioSource source;
        // Called as the line comes up (the tutorial points at a HUD slot on the line about it).
        public System.Action shown;
    }

    // One side of the stage: a glow in the speaker's colour and their painting, masked to that half of the screen.
    sealed class Slot
    {
        public RectTransform root;
        public CanvasGroup group;
        public RectTransform body;
        public Image glow;
        public Image art;
        public SpriteAnimatorUI animator;
        public string owner;
        public bool shown, lit;
        public float shift;
        public Vector2 home;
    }

    GameUI ui;
    RectTransform root;
    CanvasGroup group;
    Slot left, right;
    RectTransform plate;
    RectTransform tab;
    Image tabInk;
    TextMeshProUGUI nameText;
    TextMeshProUGUI epithetText;
    RectTransform rule;
    TextMeshProUGUI lineText;
    TextMeshProUGUI hint;
    Image dim;
    bool advance;
    // Held under the pause menu: the line stops typing, its voice pauses and no press advances it.
    bool held;
    float heldReleasedAt = float.MinValue;
    AudioSource voicing;

    public bool IsPlaying { get; private set; }

    public void Hold(bool hold)
    {
        if (held == hold) return;
        held = hold;
        if (voicing != null)
        {
            if (hold) voicing.Pause();
            else voicing.UnPause();
        }
        // The press that closed the pause menu mustn't also advance the line.
        if (!hold) heldReleasedAt = Time.unscaledTime;
    }
    // Over the plate and the figures: what a talk lifts out of the dark (the HUD's ability slots, Poguru's pointer).
    public RectTransform Overlay { get; private set; }

    public const float DarkDim = 0.55f;

    const float PlateWidth = 1180f, PlateHeight = 230f;
    const float RuleWidth = 1080f;
    // Where each speaker's eyes sit, in from their side of the screen and up from its bottom.
    static readonly Vector2 EyePoint = new Vector2(440f, 790f);
    const float CharactersPerSecond = 45f;
    // Mashed confirm from the fight can't skip the scene's opening.
    const float InputGuard = 0.3f;
    const float ListenerShift = 40f;
    static readonly Color ListenerTint = new Color(0.45f, 0.45f, 0.5f);
    const float GlowLit = 0.28f;
    // How far past the middle of the screen a figure may reach.
    const float MaskOverlap = 60f;
    // How far past the screen a painting's cut edge is pushed.
    const float EdgeHide = 12f;

    public void Bind(GameUI gameUI)
    {
        ui = gameUI;
        if (root != null) return;
        Build();
        root.gameObject.SetActive(false);
    }

    void Build()
    {
        root = GameUI.CreateRect("DialogueScene", transform, Vector2.zero, Vector2.zero, Vector2.zero);
        Stretch(root);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // The field stays visible, dimmed, with darkness rising from the bottom so the plate reads.
        dim = Stretch(GameUI.CreateRect("Dim", root, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.01f, 0.03f, DarkDim);
        dim.raycastTarget = false;

        left = BuildSlot("Left", false);
        right = BuildSlot("Right", true);

        RectTransform shade = GameUI.CreateRect("Shade", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 520f));
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = new Vector2(1f, 0f);
        shade.pivot = new Vector2(0.5f, 1f);
        shade.localScale = new Vector3(1f, -1f, 1f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.sprite = ProceduralSprites.FadeDown;
        shadeImage.color = new Color(0.02f, 0.01f, 0.03f, 0.85f);
        shadeImage.raycastTarget = false;

        // The plate: the subtitles' night-dark, a shade deeper, with the speaker's name tab on its top edge.
        plate = GameUI.CreateRect("Plate", root, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(PlateWidth, PlateHeight));
        plate.pivot = new Vector2(0.5f, 0f);
        Image plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.color = new Color(0.02f, 0.01f, 0.04f, 0.85f);
        plateImage.raycastTarget = false;

        rule = GameUI.CreateRule(plate, new Vector2(0.5f, 1f), Vector2.zero, Color.white);

        tab = GameUI.CreateRect("NameTab", plate, new Vector2(0f, 1f), Vector2.zero, new Vector2(520f, 104f));
        tab.pivot = new Vector2(0f, 0f);
        tabInk = GameUI.CreateInk(tab, new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(600f, 120f), new Color(0.03f, 0.02f, 0.04f, 0.92f));
        nameText = GameUI.CreateText(tab, ui.Font, "Name", new Vector2(0f, 0.5f), new Vector2(40f, 18f), new Vector2(560f, 60f), 50, TextAlignmentOptions.Left);
        nameText.rectTransform.pivot = new Vector2(0f, 0.5f);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        ui.Style(nameText, TextStyle.Title);
        epithetText = GameUI.CreateText(tab, ui.Font, "Epithet", new Vector2(0f, 0.5f), new Vector2(42f, -26f), new Vector2(560f, 36f), 26, TextAlignmentOptions.Left);
        epithetText.rectTransform.pivot = new Vector2(0f, 0.5f);
        epithetText.textWrappingMode = TextWrappingModes.NoWrap;
        epithetText.color = GameUI.MutedColour;
        ui.Style(epithetText, TextStyle.Hint);

        lineText = GameUI.CreateText(plate, ui.Font, "Line", new Vector2(0f, 1f), new Vector2(52f, -36f), new Vector2(PlateWidth - 104f, PlateHeight - 70f), 34, TextAlignmentOptions.TopLeft);
        lineText.rectTransform.pivot = new Vector2(0f, 1f);
        lineText.color = new Color(0.95f, 0.92f, 0.87f);
        ui.Style(lineText, TextStyle.Body);

        hint = GameUI.CreateText(plate, ui.Font, "Hint", new Vector2(1f, 0f), new Vector2(-30f, 22f), new Vector2(300f, 40f), 26, TextAlignmentOptions.BottomRight);
        hint.rectTransform.pivot = new Vector2(1f, 0f);
        hint.text = MenuUI.KeyCap("SPACE", false);
        hint.color = GameUI.MutedColour;
        ui.Style(hint, TextStyle.Hint);

        Overlay = Stretch(GameUI.CreateRect("Overlay", root, Vector2.zero, Vector2.zero, Vector2.zero));
    }

    // Each half of the screen holds one figure, masked a little past the centre with a feathered edge so a wide bust
    // never crosses into the other speaker's half.
    Slot BuildSlot(string name, bool onRight)
    {
        var slot = new Slot();
        Vector2 corner = onRight ? new Vector2(1f, 0f) : Vector2.zero;
        slot.root = GameUI.CreateRect(name, root, corner, Vector2.zero, Vector2.zero);
        slot.root.anchorMin = onRight ? new Vector2(0.5f, 0f) : Vector2.zero;
        slot.root.anchorMax = onRight ? Vector2.one : new Vector2(0.5f, 1f);
        // Past the screen's outer edges, so the feather only shows at the middle.
        slot.root.offsetMin = new Vector2(onRight ? -MaskOverlap : -200f, -200f);
        slot.root.offsetMax = new Vector2(onRight ? 200f : MaskOverlap, 200f);
        RectMask2D mask = slot.root.gameObject.AddComponent<RectMask2D>();
        mask.softness = new Vector2Int(200, 0);
        slot.group = slot.root.gameObject.AddComponent<CanvasGroup>();
        slot.group.blocksRaycasts = false;
        slot.group.alpha = 0f;

        // The body is pinned to the screen's bottom corner; the art and glow are placed in it per speaker.
        slot.home = onRight ? new Vector2(-200f, 200f) : new Vector2(200f, 200f);
        slot.body = GameUI.CreateRect("Body", slot.root, corner, slot.home, Vector2.zero);
        slot.glow = GameUI.CreateRect("Glow", slot.body, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1000f)).gameObject.AddComponent<Image>();
        slot.glow.sprite = ProceduralSprites.SoftGlow;
        slot.glow.raycastTarget = false;
        slot.art = GameUI.CreateRect("Art", slot.body, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        slot.art.raycastTarget = false;
        slot.animator = slot.art.gameObject.AddComponent<SpriteAnimatorUI>();
        return slot;
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // Puts a speaker's painting (or their soul-fire) into a slot, eyes on the slot's eye point.
    void Dress(Slot slot, Speaker who)
    {
        slot.owner = who.name;
        float side = who.right ? -1f : 1f;
        Vector2 eyes = new Vector2(side * EyePoint.x, EyePoint.y);
        slot.glow.color = new Color(who.colour.r, who.colour.g, who.colour.b, GlowLit);
        slot.glow.rectTransform.anchoredPosition = eyes + new Vector2(0f, -260f);
        RectTransform art = slot.art.rectTransform;
        if (who.art != null)
        {
            slot.animator.Play(null, 0f);
            slot.art.sprite = who.art;
            slot.art.preserveAspect = false;
            slot.art.color = Color.white;
            Rect r = who.art.rect;
            Vector2 size = new Vector2(who.height * r.width / r.height, who.height);
            art.sizeDelta = size;
            Vector2 at = eyes - new Vector2((who.focus.x - 0.5f) * size.x, (0.5f - who.focus.y) * size.y);
            // The paintings are cut at the hips and some fill their canvas to the sides, so the cut edges are kept off
            // screen: the foot below the bottom, the outer side past the screen's edge.
            at.y = Mathf.Min(at.y, size.y / 2f - EdgeHide);
            at.x = who.right ? Mathf.Max(at.x, EdgeHide - size.x / 2f) : Mathf.Min(at.x, size.x / 2f - EdgeHide);
            art.anchoredPosition = at;
            // A painting that runs to its canvas edge would end in a hard line, so the mask's feathered edge comes in to
            // meet it whenever it stops short of the middle of the screen.
            float inner = who.right ? art.anchoredPosition.x - size.x / 2f : art.anchoredPosition.x + size.x / 2f;
            Feather(slot, who.right, inner);
        }
        else if (who.soulFrames != null && who.soulFrames.Length > 0)
        {
            Feather(slot, who.right, float.NaN);
            // No painting yet: the soul itself, burning large where a face would be.
            slot.art.sprite = who.soulFrames[0];
            slot.art.preserveAspect = true;
            slot.art.color = Color.white;
            slot.animator.Play(who.soulFrames, 8f);
            art.sizeDelta = new Vector2(460f, 460f);
            art.anchoredPosition = eyes + new Vector2(0f, -120f);
        }
        else
        {
            Feather(slot, who.right, float.NaN);
            slot.animator.Play(null, 0f);
            slot.art.sprite = null;
            slot.art.color = Color.clear;
        }
    }

    // Moves the slot mask's inner edge to `inner` (from the speaker's screen edge, signed like the art's position), or
    // back to just past the middle when it's NaN or further out than that.
    void Feather(Slot slot, bool onRight, float inner)
    {
        float half = root.rect.width / 2f;
        float edge = float.IsNaN(inner) ? half + MaskOverlap : Mathf.Min(Mathf.Abs(inner) - EdgeHide, half + MaskOverlap);
        float offset = edge - half;
        if (onRight) slot.root.offsetMin = new Vector2(-offset, slot.root.offsetMin.y);
        else slot.root.offsetMax = new Vector2(offset, slot.root.offsetMax.y);
    }

    // Moves on to the next line as confirm would, typed or not (for test harnesses driving a scene).
    public void Advance() => advance = true;

    // Plays the lines in order, each waiting on confirm, and returns once the scene has faded out. `dimAlpha` is how
    // dark the field goes behind it; a lighter one leaves what's under it (a boon screen) readable.
    public IEnumerator Play(IList<Line> lines, AudioSource voice, float voiceVolume, float dimAlpha = DarkDim)
    {
        if (lines == null || lines.Count == 0) yield break;
        IsPlaying = true;
        advance = false;
        dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, dimAlpha);
        root.SetAsLastSibling();
        root.gameObject.SetActive(true);
        group.alpha = 0f;
        foreach (Slot s in new[] { left, right })
        {
            s.shown = s.lit = false;
            s.shift = 0f;
            s.owner = null;
            s.group.alpha = 0f;
        }
        float openedAt = Time.unscaledTime;
        AudioSource speaker = null;

        for (int i = 0; i < lines.Count; i++)
        {
            Line line = lines[i];
            Speaker who = line.who;
            Slot speaking = who.right ? right : left;
            Slot listening = who.right ? left : right;
            // A new face on the same side fades the last one out first.
            if (speaking.owner != who.name)
            {
                if (speaking.shown)
                {
                    for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime)
                    {
                        speaking.group.alpha = 1f - t / 0.15f;
                        yield return null;
                    }
                    speaking.group.alpha = 0f;
                }
                Dress(speaking, who);
            }
            speaking.shown = speaking.lit = true;
            listening.lit = false;
            ShowName(who);
            line.shown?.Invoke();
            lineText.text = line.text;
            lineText.maxVisibleCharacters = 0;
            int length = lineText.text.Length;
            if (speaker != null) speaker.Stop();
            speaker = line.source != null ? line.source : voice;
            voicing = speaker;
            if (speaker != null && line.clip != null)
            {
                speaker.volume = voiceVolume;
                speaker.clip = line.clip;
                speaker.Play();
                Music.Duck(0.3f, line.clip.length + 0.3f);
            }

            float lineAt = Time.unscaledTime;
            bool typed = false;
            while (true)
            {
                if (held)
                {
                    lineAt += Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }
                float since = Time.unscaledTime - lineAt;
                int shown = Mathf.Min(length, Mathf.FloorToInt(since * CharactersPerSecond));
                if (!typed && shown >= length) typed = true;
                lineText.maxVisibleCharacters = typed ? length : shown;
                hint.alpha = typed ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
                Animate(openedAt);
                if (advance)
                {
                    advance = false;
                    break;
                }
                if (Time.unscaledTime - openedAt > InputGuard && Time.unscaledTime - heldReleasedAt > InputGuard && Confirmed())
                {
                    if (!typed) typed = true;
                    else break;
                }
                yield return null;
            }
            Sfx.Play(Sfx.UiMove, 0.6f);
            if (speaker != null && speaker.isPlaying) speaker.Stop();
        }

        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.3f;
            yield return null;
        }
        root.gameObject.SetActive(false);
        voicing = null;
        IsPlaying = false;
    }

    // The name tab follows the speaker to their side of the plate, its ink painting on again.
    void ShowName(Speaker who)
    {
        nameText.text = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(who.name.ToLowerInvariant());
        nameText.color = Color.Lerp(who.colour, Color.white, 0.3f);
        epithetText.text = who.epithet ?? "";
        epithetText.gameObject.SetActive(!string.IsNullOrEmpty(who.epithet));
        // The name reads in from the plate's outer edge, on whichever side the speaker stands.
        Vector2 edge = who.right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        float inset = who.right ? -40f : 40f;
        TextAlignmentOptions align = who.right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        foreach (TextMeshProUGUI text in new[] { nameText, epithetText })
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = edge;
            text.alignment = align;
        }
        nameText.rectTransform.anchoredPosition = new Vector2(inset, string.IsNullOrEmpty(who.epithet) ? 0f : 18f);
        epithetText.rectTransform.anchoredPosition = new Vector2(inset * 1.05f, -26f);
        // The ink's dry tail trails toward the plate's middle.
        tabInk.fillOrigin = (int)(who.right ? Image.OriginHorizontal.Right : Image.OriginHorizontal.Left);
        tabInk.rectTransform.localScale = new Vector3(who.right ? -1f : 1f, 1f, 1f);
        tab.anchorMin = tab.anchorMax = who.right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        tab.pivot = who.right ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        tab.anchoredPosition = new Vector2(who.right ? -20f : 20f, -14f);
        tabShownAt = Time.unscaledTime;
        Color ruleColour = Color.Lerp(who.colour, Color.white, 0.35f);
        foreach (Image mark in rule.GetComponentsInChildren<Image>()) mark.color = ruleColour;
    }

    float tabShownAt;

    void Animate(float openedAt)
    {
        float open = Time.unscaledTime - openedAt;
        group.alpha = Mathf.Clamp01(open / 0.25f);
        GameUI.AnimateRule(rule, RuleWidth, GameUI.EaseOut(Mathf.Clamp01((open - 0.1f) / 0.5f)));
        float since = Time.unscaledTime - tabShownAt;
        tabInk.fillAmount = GameUI.EaseOut(Mathf.Clamp01(since / 0.35f));
        float nameIn = GameUI.EaseOut(Mathf.Clamp01((since - 0.08f) / 0.3f));
        nameText.alpha = nameIn;
        epithetText.alpha = nameIn;

        foreach (Slot s in new[] { left, right })
        {
            float step = Time.unscaledDeltaTime / 0.2f;
            s.group.alpha = Mathf.MoveTowards(s.group.alpha, s.shown ? 1f : 0f, step);
            s.shift = Mathf.MoveTowards(s.shift, s.lit ? 0f : 1f, step);
            // The listener darkens and slips back toward their edge of the screen.
            Color tint = Color.Lerp(Color.white, ListenerTint, s.shift);
            if (s.art.color.a > 0f) s.art.color = tint;
            s.glow.color = new Color(s.glow.color.r, s.glow.color.g, s.glow.color.b, Mathf.Lerp(GlowLit, 0.08f, s.shift));
            float outward = s == right ? 1f : -1f;
            s.body.anchoredPosition = s.home + new Vector2(outward * ListenerShift * s.shift, 0f);
        }
    }

    static bool Confirmed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }
}
