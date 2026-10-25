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

    public TMP_FontAsset Font => font;
    public Sprite LifeIcon => lifeIcon;
    public TMP_FontAsset BodyFont => BuildFont(bodyFont, ref bodyAsset, 72, 12);
    public TMP_FontAsset TitleFont => BuildFont(headerFont, ref headerAsset, 90, 16);
    public Sprite TitleLogo => titleLogo;

    public static readonly Color MutedColour = new Color(0.8f, 0.77f, 0.72f);
    public static readonly Color HighScoreColour = new Color(1f, 0.84f, 0.42f);

    TextMeshProUGUI scoreText;
    TextMeshProUGUI levelText;
    RectTransform livesRow;
    readonly List<GameObject> lifeIcons = new List<GameObject>();
    Sprite emptyLifeIcon;
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
    RectTransform bossFrame;
    RectTransform bossBack;
    // The boss bar's width alone, and while it shares the top line with a companion's bar (Shirogane's Kuon), which
    // sits to its right: hers narrows and slides left so both fit where hers stood.
    const float BossBarY = -80f;
    const float BossBarWidth = 904f;
    const float SharedBossBarWidth = 580f;
    const float WolfBarWidth = 300f;
    const float BarGap = 48f;

    GameObject wolfBar;
    TextMeshProUGUI wolfName;
    RectTransform wolfFill;
    RectTransform wolfTrail;
    Image wolfFillImage;
    float shownWolfHealth = 1f;
    float wolfHealth = 1f;

    RectTransform subtitlePlate;
    TextMeshProUGUI subtitleSpeaker;
    TextMeshProUGUI subtitle;
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
        BuildWolfBar();
        BuildSubtitle();
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

    // Arcade layout: score top left, stage and samurai left top right, lives in a thin strip along the very bottom
    // left, below the ronin's feet so he's never hidden behind them. The top centre stays clear
    // for the boss bar; the high score is shown on the title and end menus instead. Small dim labels sit over big
    // bright numbers, on a shade that fades out before the army's top row.
    void BuildHud()
    {
        RectTransform shade = CreateRect("HudShade", transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 150));
        shade.anchorMin = new Vector2(0f, 1f);
        shade.anchorMax = new Vector2(1f, 1f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.sprite = ProceduralSprites.FadeDown;
        shadeImage.color = new Color(0.02f, 0.01f, 0.04f, 0.7f);
        shadeImage.raycastTarget = false;
        hudParts.Add(shade.gameObject);

        HudLabel("ScoreLabel", "SCORE", new Vector2(0f, 1f), new Vector2(36, -14), TextAlignmentOptions.TopLeft);
        scoreText = HudValue("Score", new Vector2(0f, 1f), new Vector2(36, -42), TextAlignmentOptions.TopLeft, Color.white);
        levelText = HudValue("Level", new Vector2(1f, 1f), new Vector2(-36, -14), TextAlignmentOptions.TopRight, Color.white);
        levelText.fontSize = 36;

        // The samurai counter is the one HUD text authored in the scene; it joins the stage block on the right.
        SamuraiCounter counter = FindAnyObjectByType<SamuraiCounter>();
        if (counter != null && counter.TryGetComponent(out TextMeshProUGUI counterText))
        {
            RectTransform rect = counterText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-36, -62);
            rect.sizeDelta = new Vector2(500, 50);
            counterText.alignment = TextAlignmentOptions.TopRight;
            counterText.fontSize = 30;
            counterText.color = MutedColour;
            Style(counterText, TextStyle.Hud);
            hudParts.Add(counterText.gameObject);
        }

        livesRow = CreateRect("LivesIcons", transform, new Vector2(0f, 0f), new Vector2(20, 6), new Vector2(400, LifeIconSize));
        hudParts.Add(livesRow.gameObject);
    }

    // Score, stage and lives; hidden behind the title menu.
    public void SetHudVisible(bool visible)
    {
        foreach (GameObject part in hudParts) part.SetActive(visible);
    }

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

    TextMeshProUGUI HudLabel(string name, string label, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = CreateText(name, anchor, position, new Vector2(500, 36), 28, alignment);
        text.text = label;
        text.color = MutedColour;
        text.characterSpacing = 8;
        Style(text, TextStyle.Hint);
        hudParts.Add(text.gameObject);
        return text;
    }

    TextMeshProUGUI HudValue(string name, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment, Color colour)
    {
        TextMeshProUGUI text = CreateText(name, anchor, position, new Vector2(600, 64), 50, alignment);
        text.color = colour;
        Style(text, TextStyle.Hud);
        hudParts.Add(text.gameObject);
        return text;
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
        subtitlePlate.gameObject.SetActive(false);
    }

    void Update()
    {
        if (subtitlePlate.gameObject.activeSelf && Time.unscaledTime > subtitleUntil) subtitlePlate.gameObject.SetActive(false);
        UpdateBanner();

        // The trailing white chunk of the boss bar catches up to the red, so big hits read as big.
        if (bossBar == null || !bossBar.activeSelf) return;
        shownBossHealth = Mathf.MoveTowards(shownBossHealth, bossHealth, Time.unscaledDeltaTime * 0.6f);
        bossFill.anchorMax = new Vector2(bossHealth, 1f);
        bossTrail.anchorMax = new Vector2(shownBossHealth, 1f);
        // The boss bar narrows to the left while a companion's bar shares the line, and widens back once it's gone.
        float width = wolfBar.activeSelf ? SharedBossBarWidth : BossBarWidth;
        float x = wolfBar.activeSelf ? -(WolfBarWidth + BarGap) / 2f : 0f;
        float ease = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
        bossFrame.sizeDelta = new Vector2(Mathf.Lerp(bossFrame.sizeDelta.x, width, ease), bossFrame.sizeDelta.y);
        bossFrame.anchoredPosition = new Vector2(Mathf.Lerp(bossFrame.anchoredPosition.x, x, ease), BossBarY);
        bossBack.sizeDelta = new Vector2(bossFrame.sizeDelta.x - 8f, bossBack.sizeDelta.y);
        bossName.rectTransform.sizeDelta = new Vector2(bossFrame.sizeDelta.x - (wolfBar.activeSelf ? 24f : 0f), bossName.rectTransform.sizeDelta.y);

        if (!wolfBar.activeSelf) return;
        shownWolfHealth = Mathf.MoveTowards(shownWolfHealth, wolfHealth, Time.unscaledDeltaTime * 0.6f);
        wolfFill.anchorMax = new Vector2(wolfHealth, 1f);
        wolfTrail.anchorMax = new Vector2(shownWolfHealth, 1f);
    }

    RectTransform bossTrail;

    public void ShowBossBar(string name, Color colour)
    {
        bossName.text = name;
        bossName.color = colour;
        bossFillImage.color = colour;
        bossHealth = shownBossHealth = 1f;
        bossBar.SetActive(true);
    }

    // A new name for the bar when the boss changes form, leaving its health and colour as they are.
    public void RenameBossBar(string name)
    {
        bossName.text = name;
    }

    public void SetBossHealth(float fraction)
    {
        bossHealth = Mathf.Clamp01(fraction);
    }

    // A second, thinner bar above the boss's for a companion fought alongside her (Shirogane's wolf, Kuon).
    public void ShowWolfBar(string name, Color colour)
    {
        wolfName.text = name;
        wolfName.color = colour;
        wolfFillImage.color = colour;
        wolfHealth = shownWolfHealth = 1f;
        wolfFill.anchorMax = wolfTrail.anchorMax = Vector2.one;
        wolfBar.SetActive(true);
    }

    public void SetWolfHealth(float fraction)
    {
        wolfHealth = Mathf.Clamp01(fraction);
    }

    public void HideWolfBar()
    {
        wolfBar.SetActive(false);
    }

    // A spoken line shown along the bottom of the screen for `seconds`, with the speaker named in their colour.
    public void ShowSubtitle(string speaker, string text, Color colour, float seconds)
    {
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
        subtitlePlate.gameObject.SetActive(true);
    }

    // A boss's entrance, in two beats, like a Final Fantasy XIV boss card. Her full-body art fades up on a dark stage
    // lit in her colour; then it cross-fades to her close-up while a lower-third title rises in: her epithet over a
    // thin rule, her name under it, in Roman capitals, and her spoken line beneath. The close-up is `closeUpHeight`
    // tall with its `focus` point (0-1 across and down the art) at `focusAt` on screen. Runs on unscaled time; ENTER
    // or SPACE skips to the fight.
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
        float lineLength = line != null ? line.length : 3f;
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

    const string PrintableCharacters =
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
        wolfBar.SetActive(false);
        bossFrame.anchoredPosition = new Vector2(0, BossBarY);
        bossFrame.sizeDelta = new Vector2(BossBarWidth, bossFrame.sizeDelta.y);
        bossBack.sizeDelta = new Vector2(BossBarWidth - 8f, bossBack.sizeDelta.y);
    }

    void BuildBossBar()
    {
        RectTransform frame = CreateRect("BossBar", transform, new Vector2(0.5f, 1f), new Vector2(0, BossBarY), new Vector2(904, 30));
        bossFrame = frame;
        frame.gameObject.AddComponent<Image>().color = new Color(0.9f, 0.88f, 0.85f, 0.9f);
        RectTransform back = CreateRect("Back", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BossBarWidth - 8f, 22));
        back.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.06f, 0.95f);
        bossBack = back;
        bossTrail = Stretch(CreateRect("Trail", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossTrail.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.85f);
        bossFill = Stretch(CreateRect("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossFillImage = bossFill.gameObject.AddComponent<Image>();
        bossName = CreateText("BossName", new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(904, 48), 38, TextAlignmentOptions.Bottom, frame);
        bossName.characterSpacing = 4;
        // Shrinks to fit when the bar narrows to share its line.
        bossName.enableAutoSizing = true;
        bossName.fontSizeMin = 22;
        bossName.fontSizeMax = 38;
        Style(bossName, TextStyle.Heading);
        bossBar = frame.gameObject;
        bossBar.SetActive(false);
    }

    // The companion's bar, to the right of the boss's on the same line: the same look, shorter and thinner, with a
    // smaller name over it.
    void BuildWolfBar()
    {
        float x = (SharedBossBarWidth + BarGap) / 2f;
        RectTransform frame = CreateRect("WolfBar", transform, new Vector2(0.5f, 1f), new Vector2(x, BossBarY), new Vector2(WolfBarWidth, 22));
        frame.gameObject.AddComponent<Image>().color = new Color(0.9f, 0.88f, 0.85f, 0.9f);
        RectTransform back = CreateRect("Back", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WolfBarWidth - 6f, 16));
        back.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.06f, 0.95f);
        wolfTrail = Stretch(CreateRect("Trail", back, Vector2.zero, Vector2.zero, Vector2.zero));
        wolfTrail.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.85f);
        wolfFill = Stretch(CreateRect("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero));
        wolfFillImage = wolfFill.gameObject.AddComponent<Image>();
        wolfName = CreateText("WolfName", new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(WolfBarWidth, 40), 30, TextAlignmentOptions.Bottom, frame);
        wolfName.characterSpacing = 4;
        wolfName.enableAutoSizing = true;
        wolfName.fontSizeMin = 20;
        wolfName.fontSizeMax = 30;
        Style(wolfName, TextStyle.Heading);
        wolfBar = frame.gameObject;
        wolfBar.SetActive(false);
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
        scoreText.SetText("{0:000000}", score);
    }

    public void SetStage(int stage, int stageCount, int round, int roundCount)
    {
        levelText.text = $"STAGE {stage}/{stageCount}  ·  ROUND {round}/{roundCount}";
    }

    // A full heart per remaining life and an empty outlined one per life lost, up to his maximum.
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
            image.sprite = full ? lifeIcon : EmptyLifeIcon;
            image.color = full ? Color.white : new Color(1f, 1f, 1f, 0.8f);
        }
    }

    // The life heart's outline alone, built once from its art.
    Sprite EmptyLifeIcon => emptyLifeIcon != null || lifeIcon == null ? emptyLifeIcon : emptyLifeIcon = ProceduralSprites.Hollow(lifeIcon, new Color(0f, 0f, 0f, 0.35f));

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
