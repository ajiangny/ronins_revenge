using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Serialization;
using UnityEngine.UI;

// How a piece of text is drawn: Title in the brush font, the rest in the clean body font, all outlined and shadowed.
public enum TextStyle { Title, Heading, Hud, Body, Hint }

// Builds the HUD (score, stage, lives, boss bar, subtitles), the boss intro and the stage banner, and owns
// the text styles every other screen uses.
[RequireComponent(typeof(Canvas))]
public class GameUI : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] TMP_FontAsset font;
    [Tooltip("Heart shown once per remaining life.")]
    [SerializeField] Sprite lifeIcon;
    [Tooltip("Roman-capital font for titles and headers: menus, banners and boss cards. Without it they use the pixel font.")]
    [SerializeField, FormerlySerializedAs("cardNameFont")] Font headerFont;
    [Tooltip("The game's logo for the title menu. Without it the menu sets the name in the header font.")]
    [SerializeField] Sprite titleLogo;
    [Tooltip("Clean, readable font for the HUD, menus and body text. Without it they use the pixel font.")]
    [SerializeField] Font bodyFont;
    [Tooltip("The body and header fonts baked into static TMP fonts by Ninja/Apply UI Fonts, so a build needn't ship " +
             "the TTFs or rasterise them at startup. Empty falls back to building them from the TTFs above.")]
    [SerializeField] TMP_FontAsset bodyFontBaked;
    [SerializeField] TMP_FontAsset headerFontBaked;

    public TMP_FontAsset Font => font;
    public Sprite LifeIcon => lifeIcon;
    public TMP_FontAsset BodyFont => bodyFontBaked != null ? bodyFontBaked : BuildFont(bodyFont, ref bodyAsset, BodySampling, BodyPadding);
    public TMP_FontAsset TitleFont => headerFontBaked != null ? headerFontBaked : BuildFont(headerFont, ref headerAsset, HeaderSampling, HeaderPadding);
    public const int BodySampling = 72, BodyPadding = 12, HeaderSampling = 90, HeaderPadding = 16;
    public Sprite TitleLogo => titleLogo;

    public static readonly Color MutedColour = new Color(0.8f, 0.77f, 0.72f);
    public static readonly Color HighScoreColour = new Color(1f, 0.84f, 0.42f);

    TextMeshProUGUI scoreText;
    TextMeshProUGUI levelText;
    RectTransform livesRow;
    readonly List<GameObject> lifeIcons = new List<GameObject>();
    readonly List<GameObject> hudParts = new List<GameObject>();

    CanvasGroup banner;
    Image bannerInk;
    TextMeshProUGUI bannerOverline;
    TextMeshProUGUI bannerTitle;
    RectTransform bannerRule;
    TextMeshProUGUI bannerBody;
    bool bannerShown;
    float bannerShownAt;

    GameObject bossBar;
    TextMeshProUGUI bossName;
    RectTransform bossFill;
    Image bossFillImage;
    float shownBossHealth = 1f;
    float bossHealth = 1f;
    // The bar's second half (Kuon's), shown only while the bar is split.
    RectTransform bossFill2;
    Image bossFill2Image;
    RectTransform bossTrail2;
    RectTransform bossDivider;
    TextMeshProUGUI bossLabelLeft;
    TextMeshProUGUI bossLabelRight;
    bool bossSplit;
    float shownBossHealth2 = 1f;
    float bossHealth2 = 1f;

    RectTransform pathPlate;
    CanvasGroup pathGroup;
    Image pathShade;
    RectTransform pathRule;
    float pathRuleWidth;
    TextMeshProUGUI pathText;
    TextMeshProUGUI pathHint;
    Vector3 pathAnchor;
    float pathShownAt;
    bool pathShown;

    RectTransform subtitlePlate;
    TextMeshProUGUI subtitleSpeaker;
    TextMeshProUGUI subtitle;
    CanvasGroup subtitleGroup;
    RectTransform portraitFrame;
    Image portraitEdge;
    Image portraitGlow;
    RectTransform portraitArt;
    Image portraitImage;
    TextMeshProUGUI godModeTag;
    float subtitleUntil;
    Image fade;

    TMP_FontAsset headerAsset;
    TMP_FontAsset bodyAsset;
    readonly Dictionary<TextStyle, Material> styleMaterials = new Dictionary<TextStyle, Material>();

    void Awake()
    {
        BuildHud();
        BuildBossBar();
        BuildSubtitle();
        BuildPathInfo();
        BuildBanner();
        BuildFade();
    }

    // A black cover over the whole screen for moving between maps, raised over the rest of the canvas while it fades.
    void BuildFade()
    {
        fade = Stretch(CreateRect("Fade", transform, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        fade.color = Color.clear;
        fade.raycastTarget = false;
    }

    // Fades the black cover to the given opacity, in real time so a hit-stop can't stall it.
    public IEnumerator FadeTo(float alpha, float seconds)
    {
        // The boon slots are added to the canvas after the HUD, so bring the cover back over them.
        fade.transform.SetAsLastSibling();
        float from = fade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, alpha, t / seconds));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, alpha);
    }

    // Arcade layout, kept minimal: one row each side of the boss bar, score top left and stage/round top right, each a
    // small dim label beside a bright number; lives in a thin strip along the very bottom left, below the ronin's feet
    // so he's never hidden behind them. The high score is shown on the title and end menus instead. Both rows sit on
    // a shade that fades out before the army's top row.
    void BuildHud()
    {
        RectTransform shade = CreateRect("HudShade", transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 120));
        shade.anchorMin = new Vector2(0f, 1f);
        shade.anchorMax = new Vector2(1f, 1f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.sprite = ProceduralSprites.FadeDown;
        shadeImage.color = new Color(0.02f, 0.01f, 0.04f, 0.7f);
        shadeImage.raycastTarget = false;
        hudParts.Add(shade.gameObject);

        scoreText = HudValue("Score", new Vector2(0f, 1f), new Vector2(36, -14), TextAlignmentOptions.TopLeft, Color.white);
        levelText = HudValue("Level", new Vector2(1f, 1f), new Vector2(-36, -14), TextAlignmentOptions.TopRight, Color.white);
        levelText.rectTransform.sizeDelta = new Vector2(700, 100);
        levelText.lineSpacing = -12;

        // The army still counts into the scene's samurai counter, but the HUD no longer shows it.
        SamuraiCounter counter = FindAnyObjectByType<SamuraiCounter>();
        if (counter != null && counter.TryGetComponent(out TextMeshProUGUI counterText)) counterText.enabled = false;

        livesRow = CreateRect("LivesIcons", transform, new Vector2(0f, 0f), new Vector2(20, 6), new Vector2(400, LifeIconSize));
        hudParts.Add(livesRow.gameObject);
    }

    // Score, stage and lives; hidden behind the title menu.
    public void SetHudVisible(bool visible)
    {
        foreach (GameObject part in hudParts) part.SetActive(visible);
    }

    // Hides the whole HUD, boss bar too, while a talk scene plays over the field, and brings back what was up before.
    public void HideForScene(bool hide)
    {
        SetHudVisible(!hide);
        if (hide) bossBarBeforeScene = bossBar.activeSelf;
        bossBar.SetActive(!hide && bossBarBeforeScene);
    }

    bool bossBarBeforeScene;

    // A reminder over the lives while the dev menu's god mode is on. Built the first time it's needed.
    public void SetGodMode(bool on)
    {
        if (godModeTag == null)
        {
            if (!on) return;
            godModeTag = CreateText("GodMode", new Vector2(0f, 0f), new Vector2(24, 46), new Vector2(500, 36), 28, TextAlignmentOptions.BottomLeft);
            godModeTag.text = "GOD MODE";
            godModeTag.color = HighScoreColour;
            godModeTag.characterSpacing = 8;
            Style(godModeTag, TextStyle.Hint);
        }
        godModeTag.gameObject.SetActive(on);
    }

    TextMeshProUGUI HudValue(string name, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment, Color colour)
    {
        TextMeshProUGUI text = CreateText(name, anchor, position, new Vector2(700, 56), 40, alignment);
        text.color = colour;
        Style(text, TextStyle.Hud);
        hudParts.Add(text.gameObject);
        return text;
    }

    // While the ronin chooses his next path, a tooltip hangs under the one he's lined up with, below its name: a
    // brush rule in the reward's colour, what it gives, and how to take it in small dim text, over a soft shadow
    // rather than a box, so it reads as part of the path and not as another panel on the HUD.
    void BuildPathInfo()
    {
        pathPlate = CreateRect("PathTooltip", transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PathTooltipWidth, 120f));
        pathPlate.pivot = new Vector2(0.5f, 1f);

        pathShade = CreateRect("Shade", pathPlate, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        pathShade.sprite = ProceduralSprites.SoftGlow;
        pathShade.color = new Color(0.02f, 0.01f, 0.04f, 0.75f);
        pathShade.raycastTarget = false;

        pathRule = CreateRule(pathPlate, new Vector2(0.5f, 1f), new Vector2(0f, -6f), Color.white);
        pathRule.GetChild(0).localScale = Vector3.zero;

        pathText = CreateText("Description", new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(PathTooltipWidth, 80f), 26, TextAlignmentOptions.Top, pathPlate);
        pathText.rectTransform.pivot = new Vector2(0.5f, 1f);
        Style(pathText, TextStyle.Body);
        pathText.lineSpacing = 0f;
        pathText.color = new Color(0.93f, 0.9f, 0.85f);

        pathHint = CreateText("Hint", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PathTooltipWidth, 30f), 19, TextAlignmentOptions.Top, pathPlate);
        pathHint.rectTransform.pivot = new Vector2(0.5f, 1f);
        pathHint.color = MutedColour;
        pathHint.characterSpacing = 4;
        Style(pathHint, TextStyle.Hint);

        pathGroup = pathPlate.gameObject.AddComponent<CanvasGroup>();
        pathGroup.blocksRaycasts = false;
        pathPlate.gameObject.SetActive(false);
    }

    const float PathTooltipWidth = 460f;

    // Hangs the tooltip under `anchor` (a point in the world, just below the path's name) and redraws it for the path
    // he's now lined up with: the rule draws out and the words rise in.
    public void ShowPathInfo(Vector3 anchor, string description, Color colour, string hint)
    {
        pathAnchor = anchor;
        Color accent = Color.Lerp(colour, Color.white, 0.3f);
        foreach (Image mark in pathRule.GetComponentsInChildren<Image>()) mark.color = accent;
        pathText.text = description;
        pathHint.text = hint;
        float textHeight = pathText.GetPreferredValues(description, PathTooltipWidth, 0f).y;
        float textWidth = Mathf.Min(pathText.GetPreferredValues(description, PathTooltipWidth, 0f).x, PathTooltipWidth);
        pathText.rectTransform.sizeDelta = new Vector2(PathTooltipWidth, textHeight);
        pathHint.rectTransform.anchoredPosition = new Vector2(0f, -22f - textHeight - 8f);
        pathRuleWidth = Mathf.Max(textWidth * 0.8f, 140f);
        float height = 22f + textHeight + 8f + 26f;
        pathPlate.sizeDelta = new Vector2(PathTooltipWidth, height);
        // The shadow spreads past the words so its soft edge never cuts across them.
        RectTransform shade = pathShade.rectTransform;
        shade.anchoredPosition = new Vector2(0f, 10f);
        shade.sizeDelta = new Vector2(textWidth + 220f, height + 110f);
        pathShownAt = Time.unscaledTime;
        if (!pathPlate.gameObject.activeSelf) pathGroup.alpha = 0f;
        pathPlate.gameObject.SetActive(true);
        pathShown = true;
        PlacePathInfo();
    }

    public void HidePathInfo()
    {
        pathShown = false;
    }

    // Keeps the tooltip as it is but hangs it from a new point, for one that follows him.
    public void MovePathInfo(Vector3 anchor) => pathAnchor = anchor;

    void PlacePathInfo()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Canvas canvas = GetComponent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = cam.WorldToScreenPoint(pathAnchor);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, uiCamera, out Vector2 local))
        {
            pathPlate.anchoredPosition = local;
        }
    }

    void UpdatePathInfo()
    {
        if (!pathPlate.gameObject.activeSelf) return;
        pathGroup.alpha = Mathf.MoveTowards(pathGroup.alpha, pathShown ? 1f : 0f, Time.unscaledDeltaTime / SubtitleFadeIn);
        if (!pathShown && pathGroup.alpha <= 0f)
        {
            pathPlate.gameObject.SetActive(false);
            return;
        }
        PlacePathInfo();
        float since = Time.unscaledTime - pathShownAt;
        AnimateRule(pathRule, pathRuleWidth, EaseOut(Mathf.Clamp01(since / 0.35f)));
        float k = EaseOut(Mathf.Clamp01((since - 0.08f) / 0.3f));
        pathText.alpha = k;
        pathText.rectTransform.anchoredPosition = new Vector2(0f, -22f - 8f * (1f - k));
        pathHint.alpha = EaseOut(Mathf.Clamp01((since - 0.2f) / 0.3f));
    }

    // A spoken line sits on a dark plate sized to fit it, with the speaker's name above.
    void BuildSubtitle()
    {
        subtitlePlate = CreateRect("Subtitle", transform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1200, 100));
        subtitlePlate.pivot = new Vector2(0.5f, 0f);
        Image plate = subtitlePlate.gameObject.AddComponent<Image>();
        plate.color = new Color(0.02f, 0.01f, 0.04f, 0.68f);
        plate.raycastTarget = false;

        subtitleSpeaker = CreateText("Speaker", new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(1200, 40), 30, TextAlignmentOptions.Bottom, subtitlePlate);
        subtitleSpeaker.characterSpacing = 6;
        Style(subtitleSpeaker, TextStyle.Heading);
        subtitle = CreateText("Line", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 100), 34, TextAlignmentOptions.Center, subtitlePlate);
        Style(subtitle, TextStyle.Body);
        subtitleGroup = subtitlePlate.gameObject.AddComponent<CanvasGroup>();
        subtitleGroup.blocksRaycasts = false;
        BuildSpeakerPortrait();
        subtitlePlate.gameObject.SetActive(false);
    }

    // The frame a speaker's close-up is cropped into, standing by the plate like a Hades portrait (placed per line): a thin
    // edge in their colour around a night-dark window, lit from behind in their colour so the art never sits on paper.
    void BuildSpeakerPortrait()
    {
        portraitFrame = CreateRect("Portrait", subtitlePlate, Vector2.zero, Vector2.zero, PortraitSize);
        portraitEdge = portraitFrame.gameObject.AddComponent<Image>();
        portraitEdge.raycastTarget = false;
        RectTransform window = Stretch(CreateRect("Window", portraitFrame, Vector2.zero, Vector2.zero, Vector2.zero));
        window.offsetMin = new Vector2(PortraitEdge, PortraitEdge);
        window.offsetMax = new Vector2(-PortraitEdge, -PortraitEdge);
        Image night = window.gameObject.AddComponent<Image>();
        night.color = new Color(0.03f, 0.025f, 0.06f, 1f);
        night.raycastTarget = false;
        window.gameObject.AddComponent<RectMask2D>();
        portraitGlow = CreateRect("Glow", window, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), PortraitSize * 1.5f).gameObject.AddComponent<Image>();
        portraitGlow.sprite = ProceduralSprites.SoftGlow;
        portraitGlow.raycastTarget = false;
        portraitArt = CreateRect("Art", window, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
        portraitImage = portraitArt.gameObject.AddComponent<Image>();
        portraitImage.raycastTarget = false;
        // Darkness rising from the frame's foot, so the cut-off art settles into the plate beside it.
        RectTransform foot = CreateRect("Foot", window, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 90f));
        foot.anchorMin = Vector2.zero;
        foot.anchorMax = new Vector2(1f, 0f);
        foot.pivot = new Vector2(0.5f, 1f);
        foot.localScale = new Vector3(1f, -1f, 1f);
        Image footImage = foot.gameObject.AddComponent<Image>();
        footImage.sprite = ProceduralSprites.FadeDown;
        footImage.color = new Color(0.03f, 0.025f, 0.06f, 0.85f);
        footImage.raycastTarget = false;
        portraitFrame.gameObject.SetActive(false);
    }

    static readonly Vector2 PortraitSize = new Vector2(230f, 270f);
    const float PortraitEdge = 3f, PortraitGap = 14f;
    // Where the art's focus (the eyes) sits in the frame, 0-1 across and down.
    static readonly Vector2 PortraitEyes = new Vector2(0.5f, 0.36f);
    // How long a line takes to fade in, and out once it's done.
    const float SubtitleFadeIn = 0.2f, SubtitleFadeOut = 0.35f;

    void Update()
    {
        if (subtitlePlate.gameObject.activeSelf)
        {
            bool showing = Time.unscaledTime <= subtitleUntil;
            subtitleGroup.alpha = Mathf.MoveTowards(subtitleGroup.alpha, showing ? 1f : 0f,
                Time.unscaledDeltaTime / (showing ? SubtitleFadeIn : SubtitleFadeOut));
            if (!showing && subtitleGroup.alpha <= 0f) subtitlePlate.gameObject.SetActive(false);
        }
        UpdateBanner();
        UpdatePathInfo();

        // The trailing white chunk of the boss bar catches up to the red, so big hits read as big.
        if (bossBar == null || !bossBar.activeSelf) return;
        shownBossHealth = Mathf.MoveTowards(shownBossHealth, bossHealth, Time.unscaledDeltaTime * 0.6f);
        float half = bossSplit ? 0.5f : 1f;
        bossFill.anchorMax = new Vector2(bossHealth * half, 1f);
        bossTrail.anchorMax = new Vector2(shownBossHealth * half, 1f);
        bossFill2.gameObject.SetActive(bossSplit);
        bossTrail2.gameObject.SetActive(bossSplit);
        bossDivider.gameObject.SetActive(bossSplit);
        bossLabelLeft.gameObject.SetActive(bossSplit);
        bossLabelRight.gameObject.SetActive(bossSplit);
        if (!bossSplit) return;
        shownBossHealth2 = Mathf.MoveTowards(shownBossHealth2, bossHealth2, Time.unscaledDeltaTime * 0.6f);
        bossFill2.anchorMax = new Vector2(0.5f + bossHealth2 * 0.5f, 1f);
        bossTrail2.anchorMax = new Vector2(0.5f + shownBossHealth2 * 0.5f, 1f);
    }

    RectTransform bossTrail;

    public void ShowBossBar(string name, Color colour)
    {
        bossName.text = name;
        bossName.color = colour;
        bossFillImage.color = colour;
        bossHealth = shownBossHealth = 1f;
        bossSplit = false;
        bossBar.SetActive(true);
    }

    // A new name for the bar when the boss changes form, leaving its health and colour as they are.
    public void RenameBossBar(string name)
    {
        bossName.text = name;
    }

    public void SetBossHealth(float fraction)
    {
        bossSplit = false;
        bossHealth = Mathf.Clamp01(fraction);
    }

    // Two foes on one bar: the left half is the first, the right half the second, in its own colour, with a notch
    // between them. The second half starts full when the split first appears.
    public void SetBossHealthSplit(float left, float right, string leftName, string rightName, Color rightColour)
    {
        bossLabelLeft.text = leftName;
        bossLabelLeft.color = bossFillImage.color;
        bossLabelRight.text = rightName;
        bossLabelRight.color = rightColour;
        if (!bossSplit) shownBossHealth = Mathf.Min(shownBossHealth * 2f, 1f);
        if (!bossSplit) shownBossHealth2 = 1f;
        bossSplit = true;
        bossHealth = Mathf.Clamp01(left);
        bossHealth2 = Mathf.Clamp01(right);
        bossFill2Image.color = rightColour;
    }

    // A speaker's dialogue portrait: their close-up, the point of it to centre on (0-1 across and down, their eyes),
    // and how tall the whole art is drawn, so the face fills the frame.
    public struct SpeakerPortrait
    {
        public Sprite art;
        public Vector2 focus;
        public float height;
    }

    // A spoken line shown along the bottom of the screen for `seconds`, with the speaker named in their colour. Given
    // `beside`, the plate's bottom-left corner sits at that point from the screen's bottom-left instead, next to
    // whoever is talking (Oni on the title hill). Given a `portrait`, their close-up stands framed beside the plate, or
    // on top of it when `beside` is set; without one the plate shows alone.
    public void ShowSubtitle(string speaker, string text, Color colour, float seconds, Vector2? beside = null,
        SpeakerPortrait? portrait = null)
    {
        bool framed = portrait.HasValue && portrait.Value.art != null;
        Vector2 anchor = beside.HasValue ? Vector2.zero : new Vector2(0.5f, 0f);
        subtitlePlate.anchorMin = subtitlePlate.anchorMax = anchor;
        subtitlePlate.pivot = anchor;
        // Centred, the portrait stands at the plate's left and the two are centred together. Beside someone (the title
        // page, where the menu column is just to the right) the plate keeps its place and the portrait stands on it.
        float room = framed && !beside.HasValue ? PortraitSize.x + PortraitGap : 0f;
        subtitlePlate.anchoredPosition = (beside ?? new Vector2(0, 150)) + new Vector2(room / 2f, 0f);
        portraitFrame.gameObject.SetActive(framed);
        if (framed)
        {
            portraitFrame.anchorMin = portraitFrame.anchorMax = beside.HasValue ? new Vector2(0f, 1f) : Vector2.zero;
            portraitFrame.pivot = beside.HasValue ? Vector2.zero : new Vector2(1f, 0f);
            portraitFrame.anchoredPosition = beside.HasValue ? new Vector2(0f, PortraitGap) : new Vector2(-PortraitGap, 0f);
            FramePortrait(portrait.Value, colour);
        }
        // With the portrait standing on the plate, the name moves out from under it, just right of the frame.
        bool nameAside = framed && beside.HasValue;
        Vector2 nameAnchor = nameAside ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
        subtitleSpeaker.rectTransform.anchorMin = subtitleSpeaker.rectTransform.anchorMax = nameAnchor;
        subtitleSpeaker.rectTransform.pivot = new Vector2(nameAside ? 0f : 0.5f, 0.5f);
        subtitleSpeaker.rectTransform.anchoredPosition = new Vector2(nameAside ? PortraitSize.x + PortraitGap : 0f, 40f);
        subtitleSpeaker.alignment = nameAside ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.Bottom;
        subtitleSpeaker.text = speaker != null ? speaker.ToUpperInvariant() : "";
        subtitleSpeaker.color = Color.Lerp(colour, Color.white, 0.25f);
        subtitle.text = text;
        subtitle.color = Color.white;
        // Fit the plate to the line, wrapping past 1100 wide.
        Vector2 size = subtitle.GetPreferredValues(text, 1100f, 0f);
        size = new Vector2(Mathf.Min(size.x, 1100f) + 64f, size.y + 28f);
        subtitlePlate.sizeDelta = size;
        subtitle.rectTransform.sizeDelta = size;
        subtitleUntil = Time.unscaledTime + seconds;
        // Over any menu page too: Oni speaks on the title hill.
        subtitlePlate.SetAsLastSibling();
        // A new line fades in unless one is already up.
        if (!subtitlePlate.gameObject.activeSelf) subtitleGroup.alpha = 0f;
        subtitlePlate.gameObject.SetActive(true);
    }

    void FramePortrait(SpeakerPortrait portrait, Color colour)
    {
        portraitEdge.color = Color.Lerp(colour, new Color(0.1f, 0.09f, 0.12f), 0.45f);
        portraitGlow.color = new Color(colour.r, colour.g, colour.b, 0.3f);
        portraitImage.sprite = portrait.art;
        Rect art = portrait.art.rect;
        Vector2 size = new Vector2(portrait.height * art.width / art.height, portrait.height);
        Vector2 window = PortraitSize - 2f * PortraitEdge * Vector2.one;
        Vector2 eyes = new Vector2((PortraitEyes.x - 0.5f) * window.x, (0.5f - PortraitEyes.y) * window.y);
        portraitArt.sizeDelta = size;
        portraitArt.anchoredPosition = eyes - new Vector2((portrait.focus.x - 0.5f) * size.x, (0.5f - portrait.focus.y) * size.y);
    }

    // A boss's entrance, in two beats, like a Final Fantasy XIV boss card. Her full-body art fades up on a dark stage
    // lit in her colour; then it cross-fades to her close-up while a lower-third title rises in: her epithet over a
    // thin rule, her name under it, in Roman capitals, and her spoken line beneath. The close-up is `closeUpHeight`
    // tall with its `focus` point (0-1 across and down the art) at `focusAt` on screen. Runs on unscaled time; ENTER
    // or SPACE skips to the fight. A null `lineText` makes it a name reveal only, for a talk scene to follow.
    public IEnumerator PlayBossIntro(Sprite full, Sprite closeUp, float closeUpHeight, Vector2 focus, Vector2 focusAt,
        string title, string epithet, Color colour, AudioSource voice, AudioClip line, string lineText)
    {
        RectTransform root = Stretch(CreateRect("BossIntro", transform, Vector2.zero, Vector2.zero, Vector2.zero));
        root.pivot = new Vector2(0.5f, 0.5f);
        root.SetAsLastSibling();
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        // A near-black stage with a wash of her colour behind wherever she stands.
        Image stage = Stretch(CreateRect("Stage", root, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        stage.color = new Color(0.03f, 0.02f, 0.04f, 1f);
        Image glow = CreateRect("Glow", root, new Vector2(0.5f, 0.5f), new Vector2(260f, 60f), new Vector2(1700f, 1700f)).gameObject.AddComponent<Image>();
        glow.sprite = ProceduralSprites.SoftGlow;
        glow.color = new Color(colour.r, colour.g, colour.b, 0.5f);

        // Beat one: her full-body art, sized to the screen's height, centred, drifting in from the right.
        RectTransform art = CreateRect("FullArt", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f * full.rect.width / full.rect.height, 1000f));
        Image artImage = art.gameObject.AddComponent<Image>();
        artImage.sprite = full;
        artImage.preserveAspect = true;
        artImage.color = Color.clear;
        const float artRestX = 0f, artStartX = 90f;

        // Beat two: her close-up, framed per boss so the art's cut-off edges sit off screen.
        Vector2 portraitSize = new Vector2(closeUpHeight * closeUp.rect.width / closeUp.rect.height, closeUpHeight);
        Vector2 portraitRest = focusAt - new Vector2((focus.x - 0.5f) * portraitSize.x, (0.5f - focus.y) * portraitSize.y);
        RectTransform portrait = CreateRect("Portrait", root, new Vector2(0.5f, 0.5f), portraitRest, portraitSize);
        Image portraitImage = portrait.gameObject.AddComponent<Image>();
        portraitImage.sprite = closeUp;
        portraitImage.color = Color.clear;

        // Darkness rising from the bottom, so the title reads over her art.
        RectTransform shade = CreateRect("Shade", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 560f));
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = new Vector2(1f, 0f);
        shade.localScale = new Vector3(1f, -1f, 1f);
        shade.pivot = new Vector2(0.5f, 1f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.sprite = ProceduralSprites.FadeDown;
        shadeImage.color = new Color(0.02f, 0.01f, 0.03f, 0f);

        // A soft dark wash right behind the title block, feathered on every side so it reads as shadow, not a box.
        Image titleWash = CreateRect("TitleWash", root, new Vector2(0.5f, 0f), new Vector2(0, 20f), new Vector2(1900f, 480f)).gameObject.AddComponent<Image>();
        titleWash.sprite = ProceduralSprites.Shadow;
        titleWash.color = Color.clear;

        // The lower third: epithet, a rule that draws out from the centre, then her name a size down.
        TextMeshProUGUI epithetText = CreateText("Epithet", new Vector2(0.5f, 0f), new Vector2(0, 262f), new Vector2(1800f, 120f), 88, TextAlignmentOptions.Bottom, root);
        epithetText.textWrappingMode = TextWrappingModes.NoWrap;
        epithetText.text = epithet;
        Style(epithetText, TextStyle.Title);
        epithetText.alpha = 0f;
        RectTransform rule = CreateRect("Rule", root, new Vector2(0.5f, 0f), new Vector2(0, 248f), new Vector2(0f, 5f));
        Image ruleImage = rule.gameObject.AddComponent<Image>();
        // The glow sprite stretched thin gives a rule that's brightest in the middle and fades out at the ends.
        ruleImage.sprite = ProceduralSprites.SoftGlow;
        Color ruleColour = Color.Lerp(colour, Color.white, 0.55f);
        TextMeshProUGUI nameText = CreateText("Name", new Vector2(0.5f, 0f), new Vector2(0, 154f), new Vector2(1800f, 90f), 56, TextAlignmentOptions.Top, root);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        // Title case, so the Roman capitals set the rest of the name in small caps.
        nameText.text = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.ToLowerInvariant());
        Style(nameText, TextStyle.Title);
        nameText.alpha = 0f;

        TextMeshProUGUI said = CreateText("Line", new Vector2(0.5f, 0f), new Vector2(0, 26f), new Vector2(1640f, 112f), 32, TextAlignmentOptions.Center, root);
        said.text = lineText ?? "";
        said.color = new Color(0.93f, 0.9f, 0.85f);
        Style(said, TextStyle.Body);
        said.maxVisibleCharacters = 0;

        const float cardAt = 1.6f;
        const float crossFade = 0.35f;
        // An unvoiced line (one written for the reactive dialogue but not recorded yet) stays up long enough to read; with
        // no line at all the card only names her, and holds just long enough for the title to settle.
        float lineLength = line != null ? line.length : lineText == null ? 1.4f : Mathf.Max(3f, lineText.Length / 15f);
        float end = cardAt + Mathf.Max(lineLength + 0.6f, 2.6f);
        bool cut = false;
        for (float t = 0; t < end; t += Time.unscaledDeltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            if (t > 0.3f && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) break;

            group.alpha = Mathf.Clamp01(t / 0.3f);

            // Beat one: she fades up and drifts in, then keeps creeping closer until the cut.
            float rise = EaseOut(Mathf.Clamp01((t - 0.15f) / 0.6f));
            float fadeToCard = Mathf.Clamp01((t - cardAt) / crossFade);
            artImage.color = new Color(1f, 1f, 1f, rise * (1f - fadeToCard));
            art.anchoredPosition = new Vector2(Mathf.Lerp(artStartX, artRestX, rise), -20f);
            art.localScale = Vector3.one * (1f + 0.04f * Mathf.Clamp01(t / cardAt) + 0.06f * fadeToCard);

            // The cut lands with her line.
            if (!cut && t >= cardAt)
            {
                cut = true;
                if (voice != null && line != null)
                {
                    voice.clip = line;
                    voice.Play();
                }
                Sfx.Play(Sfx.ParrySwing, 0.8f, 0.55f);
            }
            if (!cut)
            {
                yield return null;
                continue;
            }

            // Beat two: the close-up settles in from slightly too close, then drifts closer while she talks.
            float since = t - cardAt;
            float hold = Mathf.Clamp01(since / (end - cardAt));
            portraitImage.color = new Color(1f, 1f, 1f, fadeToCard);
            portrait.localScale = Vector3.one * (1.06f - 0.06f * EaseOut(fadeToCard) + 0.04f * hold);
            shadeImage.color = new Color(0.02f, 0.01f, 0.03f, 0.9f * fadeToCard);
            titleWash.color = new Color(0.01f, 0f, 0.02f, 0.6f * fadeToCard);

            float ruleIn = EaseOut(Mathf.Clamp01((since - 0.2f) / 0.5f));
            rule.sizeDelta = new Vector2(1180f * ruleIn, 5f);
            ruleImage.color = new Color(ruleColour.r, ruleColour.g, ruleColour.b, ruleIn);
            Reveal(epithetText, since - 0.4f, 262f, 1f);
            Reveal(nameText, since - 0.6f, 154f, -1f);
            said.maxVisibleCharacters = Mathf.RoundToInt(said.text.Length * Mathf.Clamp01((since - 0.3f) / (lineLength * 0.85f)));
            yield return null;
        }
        if (voice != null && voice.isPlaying) voice.Stop();

        // Fade out, drifting slightly closer.
        for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.4f;
            group.alpha = 1f - k;
            root.localScale = Vector3.one * (1f + 0.03f * k);
            yield return null;
        }
        Destroy(root.gameObject);
    }

    // A title line fades in, sliding onto its resting height `y` from `side` (1 above, -1 below) as its letter
    // spacing closes up.
    internal static void Reveal(TMP_Text text, float since, float y, float side, float spacing = 6f)
    {
        float k = EaseOut(Mathf.Clamp01(since / 0.6f));
        text.alpha = k;
        text.characterSpacing = Mathf.Lerp(spacing + 20f, spacing, k);
        text.rectTransform.anchoredPosition = new Vector2(text.rectTransform.anchoredPosition.x, y + side * 14f * (1f - k));
    }

    // The game's divider: a brush line that tapers at both ends, with a small diamond (the menus' selection marker)
    // at its middle. AnimateRule draws it out from the centre.
    internal static RectTransform CreateRule(Transform parent, Vector2 anchor, Vector2 position, Color colour)
    {
        RectTransform rule = CreateRect("Rule", parent, anchor, position, new Vector2(0f, 6f));
        Image line = rule.gameObject.AddComponent<Image>();
        line.sprite = ProceduralSprites.BrushLine;
        line.color = colour;
        line.raycastTarget = false;
        RectTransform diamond = CreateRect("Diamond", rule, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        diamond.localRotation = Quaternion.Euler(0, 0, 45f);
        Image mark = diamond.gameObject.AddComponent<Image>();
        mark.color = colour;
        mark.raycastTarget = false;
        return rule;
    }

    // `k` runs from 0 (not drawn) to 1 (drawn out to `width`).
    internal static void AnimateRule(RectTransform rule, float width, float k)
    {
        rule.sizeDelta = new Vector2(width * k, rule.sizeDelta.y);
        rule.GetChild(0).localScale = Vector3.one * k;
    }

    // A brush stroke of ink behind text. It is a horizontal fill, so raising fillAmount paints it on from the left.
    internal static Image CreateInk(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
    {
        Image ink = CreateRect("Ink", parent, anchor, position, size).gameObject.AddComponent<Image>();
        ink.sprite = ProceduralSprites.InkStroke;
        ink.type = Image.Type.Filled;
        ink.fillMethod = Image.FillMethod.Horizontal;
        ink.fillOrigin = (int)Image.OriginHorizontal.Left;
        ink.color = colour;
        ink.raycastTarget = false;
        return ink;
    }

    // TTF fonts are turned into TMP fonts on first use; without one the text uses the pixel font. The printable
    // ASCII set is added up front so the atlas is real before any material is copied from it.
    TMP_FontAsset BuildFont(Font source, ref TMP_FontAsset asset, int samplingSize, int padding)
    {
        if (asset == null && source != null)
        {
            asset = TMP_FontAsset.CreateFontAsset(source, samplingSize, padding, GlyphRenderMode.SDFAA, 1024, 1024);
            if (asset != null) asset.TryAddCharacters(PrintableCharacters);
        }
        return asset != null ? asset : font;
    }

    public const string PrintableCharacters =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·•–—…←↑→↓";

    // Gives text its font and a shared outline-and-shadow material, so it stays readable over the busy playfield.
    // Titles use the Roman-capital header font; everything else the clean body font. Colour and size are left to
    // the caller.
    public void Style(TMP_Text text, TextStyle style)
    {
        TMP_FontAsset styleFont = style == TextStyle.Title ? TitleFont : BodyFont;
        text.font = styleFont;
        text.fontStyle &= ~FontStyles.Italic;
        text.fontSharedMaterial = StyleMaterial(style, styleFont);
        if (style == TextStyle.Body) text.lineSpacing = 12f;
    }

    Material StyleMaterial(TextStyle style, TMP_FontAsset styleFont)
    {
        if (styleMaterials.TryGetValue(style, out Material material) && material != null)
        {
            // A dynamic font can grow a new atlas; keep the copy pointing at the live one.
            if (material.mainTexture != styleFont.atlasTexture) material.mainTexture = styleFont.atlasTexture;
            return material;
        }

        material = new Material(styleFont.material) { name = styleFont.name + " " + style };
        // Titles are fine Roman capitals, so they get a hairline edge and lean on a soft shadow; hints stay light so
        // they read as secondary.
        float outline = style == TextStyle.Title ? 0.08f : style == TextStyle.Hint ? 0.12f : 0.18f;
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(14, 6, 12, 255));
        // The outline eats half into each letter, so fatten the face back up.
        material.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.6f);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, style == TextStyle.Title ? 0.35f : outline);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, style == TextStyle.Title ? 0.45f : 0.15f);
        styleMaterials[style] = material;
        return material;
    }

    internal static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

    public void HideBossBar()
    {
        bossBar.SetActive(false);
    }

    // A compact strip hugging the top edge, name over a thin bar, so the bosses' heads and raised weapons (the
    // Emperor's hammer reaches past the old bar's bottom) stay clear of it.
    void BuildBossBar()
    {
        RectTransform frame = CreateRect("BossBar", transform, new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(904, 20));
        frame.gameObject.AddComponent<Image>().color = new Color(0.9f, 0.88f, 0.85f, 0.9f);
        RectTransform back = CreateRect("Back", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(898, 14));
        back.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.06f, 0.95f);
        bossTrail = Stretch(CreateRect("Trail", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossTrail.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.85f);
        bossFill = Stretch(CreateRect("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossFillImage = bossFill.gameObject.AddComponent<Image>();
        bossTrail2 = Stretch(CreateRect("Trail2", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossTrail2.anchorMin = new Vector2(0.5f, 0f);
        bossTrail2.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.85f);
        bossFill2 = Stretch(CreateRect("Fill2", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossFill2.anchorMin = new Vector2(0.5f, 0f);
        bossFill2Image = bossFill2.gameObject.AddComponent<Image>();
        // The notch between the halves, in the frame's colour.
        bossDivider = CreateRect("Divider", back, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 18));
        bossDivider.gameObject.AddComponent<Image>().color = new Color(0.9f, 0.88f, 0.85f, 1f);
        // Each half's name under it, small, so the split reads without a legend.
        bossLabelLeft = CreateText("LabelLeft", new Vector2(0.25f, 0f), new Vector2(0, -14), new Vector2(440, 26), 18, TextAlignmentOptions.Top, frame);
        bossLabelRight = CreateText("LabelRight", new Vector2(0.75f, 0f), new Vector2(0, -14), new Vector2(440, 26), 18, TextAlignmentOptions.Top, frame);
        foreach (TextMeshProUGUI label in new[] { bossLabelLeft, bossLabelRight })
        {
            label.characterSpacing = 3;
            Style(label, TextStyle.Heading);
            label.gameObject.SetActive(false);
        }
        bossTrail2.gameObject.SetActive(false);
        bossFill2.gameObject.SetActive(false);
        bossDivider.gameObject.SetActive(false);
        bossName = CreateText("BossName", new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(904, 40), 30, TextAlignmentOptions.Bottom, frame);
        bossName.characterSpacing = 3;
        Style(bossName, TextStyle.Heading);
        bossBar = frame.gameObject;
        bossBar.SetActive(false);
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    public void SetScore(int score)
    {
        scoreText.SetText(ScoreFormat, score);
    }

    public void SetStage(int stage, int stageCount, int round, int roundCount)
    {
        // The stage on the HUD row, with the round as a smaller line under it scaled to the stage line's width, so
        // the two sit as one right-aligned block.
        string stageLine = $"{HudLabel("STAGE")}{stage}";
        float stageWidth = levelText.GetPreferredValues(stageLine).x;
        float roundWidth = levelText.GetPreferredValues(RoundLine(round, roundCount, 1f)).x;
        float scale = roundWidth > 0f ? Mathf.Min(1f, stageWidth / roundWidth) : 1f;
        levelText.text = $"{stageLine}\n{RoundLine(round, roundCount, scale)}";
    }

    // A roundCount of 0 leaves the total off: with portals a stage runs as many rounds as the player chooses.
    string RoundLine(int round, int roundCount, float scale) =>
        $"{HudLabel("ROUND", scale)}<size={levelText.fontSize * scale:0.#}>{round}{(roundCount > 0 ? HudDim($"/{roundCount}") : "")}</size>";

    // A HUD row's small dim label, set in the same text as its number so the two share a baseline.
    static string HudLabel(string label, float scale = 1f) =>
        $"<size={26f * scale:0.#}><cspace={6f * scale:0.#}>{HudDim(label)}</cspace>  </size>";

    static string HudDim(string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(MutedColour)}>{text}</color>";

    // Built once, as the score changes many times a second.
    static readonly string ScoreFormat = HudLabel("SCORE") + "{0:000000}";

    // A full heart per remaining life and a burnt-out one per life lost, up to his maximum: the same heart, its fire
    // gone dark, so the row keeps one silhouette (an outline of the flame-wreathed heart came out scraggly).
    public void SetLives(int lives, int maxLives)
    {
        while (lifeIcons.Count < maxLives)
        {
            lifeIcons.Add(CreateLifeIcon(lifeIcons.Count));
        }
        for (int i = 0; i < lifeIcons.Count; i++)
        {
            lifeIcons[i].SetActive(i < maxLives);
            bool full = i < lives;
            Image image = lifeIcons[i].GetComponent<Image>();
            image.sprite = lifeIcon;
            image.color = full ? Color.white : SpentLife;
        }
    }

    static readonly Color SpentLife = new Color(0.22f, 0.24f, 0.3f, 0.75f);

    // A stage or round card across the middle of the screen: a stroke of ink paints on from the left, then a small
    // overline, a large title, a brush rule in the accent colour and a line of body text rise in one after another,
    // like the boss cards' lower third. The playfield stays visible above and below, and it all fades out together.
    public void ShowBanner(string overline, string title, string body, Color accent)
    {
        bannerOverline.text = overline;
        bannerTitle.text = title;
        bannerBody.text = body;
        bannerBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
        foreach (Image mark in bannerRule.GetComponentsInChildren<Image>()) mark.color = Color.Lerp(accent, Color.white, 0.35f);
        bannerShown = true;
        bannerShownAt = Time.unscaledTime;
        banner.alpha = 1f;
        banner.gameObject.SetActive(true);
        UpdateBanner();
    }

    public void HideBanner()
    {
        bannerShown = false;
    }

    const float BannerRuleWidth = 820f;

    void BuildBanner()
    {
        RectTransform panel = CreateRect("Banner", transform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1700, 340));
        banner = panel.gameObject.AddComponent<CanvasGroup>();
        banner.blocksRaycasts = false;
        // The stroke's dry tail is on the right, so it sits a little right of centre to look centred.
        bannerInk = CreateInk(panel, new Vector2(0.5f, 0.5f), new Vector2(70, 0), new Vector2(1760, 340), new Color(0.03f, 0.02f, 0.04f, 0.9f));

        bannerOverline = CreateText("Overline", new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(1300, 50), 34, TextAlignmentOptions.Center, panel);
        bannerOverline.textWrappingMode = TextWrappingModes.NoWrap;
        bannerOverline.color = MutedColour;
        Style(bannerOverline, TextStyle.Title);
        bannerTitle = CreateText("Title", new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(1500, 120), 96, TextAlignmentOptions.Center, panel);
        bannerTitle.textWrappingMode = TextWrappingModes.NoWrap;
        bannerTitle.color = new Color(0.97f, 0.94f, 0.88f);
        Style(bannerTitle, TextStyle.Title);
        bannerRule = CreateRule(panel, new Vector2(0.5f, 0.5f), new Vector2(0, -40), Color.white);
        bannerBody = CreateText("Body", new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(1300, 60), 34, TextAlignmentOptions.Center, panel);
        bannerBody.color = new Color(0.93f, 0.9f, 0.85f);
        Style(bannerBody, TextStyle.Body);
        panel.gameObject.SetActive(false);
    }

    // Paints the card in on unscaled time, then fades it out once hidden.
    void UpdateBanner()
    {
        if (!banner.gameObject.activeSelf) return;
        float since = Time.unscaledTime - bannerShownAt;
        bannerInk.fillAmount = EaseOut(Mathf.Clamp01(since / 0.45f));
        Reveal(bannerOverline, since - 0.15f, 100f, -1f, 12f);
        Reveal(bannerTitle, since - 0.25f, 28f, -1f);
        AnimateRule(bannerRule, BannerRuleWidth, EaseOut(Mathf.Clamp01((since - 0.35f) / 0.5f)));
        Reveal(bannerBody, since - 0.5f, -88f, -1f, 0f);
        if (bannerShown) return;
        banner.alpha = Mathf.MoveTowards(banner.alpha, 0f, Time.unscaledDeltaTime / 0.3f);
        if (banner.alpha <= 0f) banner.gameObject.SetActive(false);
    }

    const float LifeIconSize = 34f;

    GameObject CreateLifeIcon(int index)
    {
        RectTransform icon = CreateRect("Life" + index, livesRow, new Vector2(0f, 0.5f), new Vector2(index * (LifeIconSize + 6f), 0), new Vector2(LifeIconSize, LifeIconSize));
        Image image = icon.gameObject.AddComponent<Image>();
        image.sprite = lifeIcon;
        image.preserveAspect = true;
        return icon.gameObject;
    }

    TextMeshProUGUI CreateText(string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment, Transform parent = null)
    {
        return CreateText(parent != null ? parent : transform, font, name, anchor, position, size, fontSize, alignment);
    }

    // Shared with BoonChoiceUI, which builds its panels the same way.
    internal static TextMeshProUGUI CreateText(Transform parent, TMP_FontAsset font, string name, Vector2 anchor, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, anchor, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    internal static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
