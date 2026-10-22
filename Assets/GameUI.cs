using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
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
    [Tooltip("9-sliced panel drawn behind the stage and round announcements.")]
    [SerializeField] Sprite overlayBanner;
    [Tooltip("Screen pixels per art pixel for the banner's border, so the pixel art stays chunky and even.")]
    [SerializeField] float overlayBannerPixelScale = 2f;
    [Tooltip("Hand-lettered font for a boss's name on her title card. Without it the card uses the HUD font.")]
    [SerializeField] Font cardNameFont;
    [Tooltip("Chunky outlined font for a boss's epithet on her title card. Without it the card uses the HUD font.")]
    [SerializeField] Font cardTagFont;
    [Tooltip("Clean, readable font for the HUD, menus and body text. Without it they use the pixel font.")]
    [SerializeField] Font bodyFont;

    public TMP_FontAsset Font => font;
    public TMP_FontAsset BodyFont => BuildFont(bodyFont, ref bodyAsset, 72, 12);
    public TMP_FontAsset TitleFont => BuildFont(cardNameFont, ref cardNameAsset, 90, 16);

    public static readonly Color MutedColour = new Color(0.8f, 0.77f, 0.72f);
    public static readonly Color HighScoreColour = new Color(1f, 0.84f, 0.42f);

    TextMeshProUGUI scoreText;
    TextMeshProUGUI levelText;
    RectTransform livesRow;
    readonly List<GameObject> lifeIcons = new List<GameObject>();
    readonly List<GameObject> hudParts = new List<GameObject>();

    CanvasGroup banner;
    TextMeshProUGUI bannerTitle;
    TextMeshProUGUI bannerBody;
    bool bannerShown;

    GameObject bossBar;
    TextMeshProUGUI bossName;
    RectTransform bossFill;
    Image bossFillImage;
    float shownBossHealth = 1f;
    float bossHealth = 1f;

    RectTransform subtitlePlate;
    TextMeshProUGUI subtitleSpeaker;
    TextMeshProUGUI subtitle;
    float subtitleUntil;
    Image fade;

    TMP_FontAsset cardNameAsset;
    TMP_FontAsset cardTagAsset;
    TMP_FontAsset bodyAsset;
    readonly Dictionary<TextStyle, Material> styleMaterials = new Dictionary<TextStyle, Material>();

    void Awake()
    {
        BuildHud();
        BuildBossBar();
        BuildSubtitle();
        BuildBanner();
        BuildFade();
    }

    // A black cover over the whole screen for moving between maps. Built last so it sits over the HUD; the boss intro
    // and menus are built later and stay above it.
    void BuildFade()
    {
        fade = Stretch(CreateRect("Fade", transform, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        fade.color = Color.clear;
        fade.raycastTarget = false;
    }

    // Fades the black cover to the given opacity, in real time so a hit-stop can't stall it.
    public IEnumerator FadeTo(float alpha, float seconds)
    {
        float from = fade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, alpha, t / seconds));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, alpha);
    }

    // Arcade layout: score top left, stage and samurai left top right, lives bottom left. The top centre stays clear
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

        TextMeshProUGUI livesLabel = HudLabel("LivesLabel", "LIVES", new Vector2(0f, 0f), new Vector2(36, 76), TextAlignmentOptions.BottomLeft);
        livesLabel.rectTransform.sizeDelta = new Vector2(200, 36);
        livesRow = CreateRect("LivesIcons", transform, new Vector2(0f, 0f), new Vector2(24, 24), new Vector2(400, 52));
        hudParts.Add(livesRow.gameObject);
    }

    // Score, stage and lives; hidden behind the title menu.
    public void SetHudVisible(bool visible)
    {
        foreach (GameObject part in hudParts) part.SetActive(visible);
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

    public void SetBossHealth(float fraction)
    {
        bossHealth = Mathf.Clamp01(fraction);
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

    // A boss's entrance, in two beats. The screen darkens under a slash of her colour and her full-body art slides
    // in; then it cuts to a Borderlands-style title card (her close-up on painted paper, her name brushed across it,
    // letterboxed) while she speaks her line. Runs on unscaled time; ENTER or SPACE skips to the fight.
    public IEnumerator PlayBossIntro(Sprite full, Sprite closeUp, string title, string epithet, Color colour,
        AudioSource voice, AudioClip line, string lineText)
    {
        RectTransform root = Stretch(CreateRect("BossIntro", transform, Vector2.zero, Vector2.zero, Vector2.zero));
        root.pivot = new Vector2(0.5f, 0.5f);
        root.SetAsLastSibling();
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        Image dim = Stretch(CreateRect("Dim", root, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        dim.color = Color.clear;

        RectTransform slash = CreateRect("Slash", root, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(0, 44));
        slash.localRotation = Quaternion.Euler(0, 0, -16f);
        Image slashImage = slash.gameObject.AddComponent<Image>();
        slashImage.color = colour;

        // Her full-body art on the right, sized to the screen's height.
        RectTransform art = CreateRect("FullArt", root, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1060f * full.rect.width / full.rect.height, 1060f));
        Image artImage = art.gameObject.AddComponent<Image>();
        artImage.sprite = full;
        artImage.preserveAspect = true;
        const float artRestX = -140f, artStartX = 760f;

        // The title card, built up front and hidden until the cut.
        RectTransform card = Stretch(CreateRect("TitleCard", root, Vector2.zero, Vector2.zero, Vector2.zero));
        CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        RawImage paper = Stretch(CreateRect("Paper", card, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<RawImage>();
        paper.texture = TitleCardArt.Backdrop(colour);
        paper.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // Her close-up fills the right of the card, face high and right of centre, over a hard drop shadow.
        const float portraitHeight = 1500f;
        Vector2 portraitSize = new Vector2(portraitHeight * closeUp.rect.width / closeUp.rect.height, portraitHeight);
        // Her face sits about 48% across and 25% down the close-up.
        Vector2 portraitRest = new Vector2(430f, 150f) - new Vector2((0.48f - 0.5f) * portraitSize.x, (0.5f - 0.25f) * portraitSize.y);
        RectTransform portrait = CreateRect("Portrait", card, new Vector2(0.5f, 0.5f), portraitRest, portraitSize);
        Image shadow = CreateRect("Shadow", portrait, new Vector2(0.5f, 0.5f), new Vector2(-22f, -14f), portraitSize).gameObject.AddComponent<Image>();
        shadow.sprite = closeUp;
        shadow.color = new Color(0.05f, 0f, 0.02f, 0.55f);
        CreateRect("Art", portrait, new Vector2(0.5f, 0.5f), Vector2.zero, portraitSize).gameObject.AddComponent<Image>().sprite = closeUp;

        // Her name, hand-lettered at a rising angle and wiped on left to right as if brushed in. The mask isn't
        // rotated, so a RectMask2D can do the wipe.
        RectTransform nameReveal = CreateRect("NameReveal", card, new Vector2(0f, 0.5f), new Vector2(70f, 170f), new Vector2(0f, 800f));
        nameReveal.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(80, 0);
        TextMeshProUGUI name = CreateText(nameReveal, CardFont(cardNameFont, ref cardNameAsset), "Name", new Vector2(0f, 0.5f),
            new Vector2(40f, 0f), new Vector2(1150f, 260f), 150, TextAlignmentOptions.Left);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.rectTransform.localRotation = Quaternion.Euler(0, 0, 9f);
        name.text = title;
        name.color = colour;
        Ink(name, 0.22f);
        ScatterLetters(name, title.GetHashCode());

        TextMeshProUGUI epithetText = CreateText(card, CardFont(cardTagFont, ref cardTagAsset), "Epithet", new Vector2(0f, 0.5f),
            new Vector2(260f, -70f), new Vector2(1000f, 120f), 84, TextAlignmentOptions.Left);
        epithetText.textWrappingMode = TextWrappingModes.NoWrap;
        epithetText.rectTransform.localRotation = Quaternion.Euler(0, 0, 6f);
        epithetText.fontStyle = FontStyles.UpperCase;
        epithetText.text = epithet;
        Ink(epithetText, 0.3f);
        epithetText.color = Color.clear;

        // Letterbox bars, with her line subtitled in the lower one.
        CreateRect("TopBar", card, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, 70f)).gameObject.AddComponent<Image>().color = Color.black;
        CreateRect("BottomBar", card, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 130f)).gameObject.AddComponent<Image>().color = Color.black;
        TextMeshProUGUI said = CreateText("Line", new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(1600, 110), 34, TextAlignmentOptions.Center, card);
        said.text = lineText ?? "";
        Style(said, TextStyle.Body);
        said.maxVisibleCharacters = 0;

        Image flash = Stretch(CreateRect("Flash", root, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        flash.color = Color.clear;

        const float cardAt = 1.3f;
        float lineLength = line != null ? line.length : 3f;
        float end = cardAt + lineLength + 0.8f;
        bool cut = false;
        for (float t = 0; t < end; t += Time.unscaledDeltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            if (t > 0.3f && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) break;

            if (!cut)
            {
                dim.color = new Color(0.03f, 0f, 0.02f, 0.85f * Mathf.Clamp01(t / 0.25f));
                slash.sizeDelta = new Vector2(Mathf.Lerp(0, 2700, EaseOut(Mathf.Clamp01(t / 0.22f))), 44);
                slashImage.color = new Color(colour.r, colour.g, colour.b, 1f - Mathf.Clamp01((t - 0.6f) / 0.5f));
                float slide = EaseOut(Mathf.Clamp01((t - 0.2f) / 0.4f));
                art.anchoredPosition = new Vector2(Mathf.Lerp(artStartX, artRestX, slide), -10f);
                art.localScale = Vector3.one * (1f + 0.04f * Mathf.Clamp01((t - 0.6f) / (cardAt - 0.6f)));
            }

            // The cut to the card lands with her line.
            if (!cut && t >= cardAt)
            {
                cut = true;
                cardGroup.alpha = 1f;
                art.gameObject.SetActive(false);
                slash.gameObject.SetActive(false);
                if (voice != null && line != null)
                {
                    voice.clip = line;
                    voice.Play();
                }
                CameraShake.Add(0.35f);
                Sfx.Play(Sfx.ParrySwing, 1f, 0.55f);
            }
            if (!cut)
            {
                yield return null;
                continue;
            }

            float since = t - cardAt;
            float hold = Mathf.Clamp01(since / (end - cardAt));
            // The card punches in, then everything drifts slowly closer while she talks.
            paper.rectTransform.localScale = Vector3.one * (1.12f - 0.12f * EaseOut(Mathf.Clamp01(since / 0.25f)) + 0.03f * hold);
            portrait.anchoredPosition = Vector2.Lerp(portraitRest + Vector2.right * 260f, portraitRest, EaseOut(Mathf.Clamp01(since / 0.3f)));
            portrait.localScale = Vector3.one * (1f + 0.05f * hold);
            nameReveal.sizeDelta = new Vector2(1500f * EaseOut(Mathf.Clamp01((since - 0.15f) / 0.45f)), nameReveal.sizeDelta.y);
            float slam = Mathf.Clamp01((since - 0.65f) / 0.15f);
            epithetText.color = new Color(1f, 1f, 1f, slam);
            epithetText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, EaseOut(slam));
            flash.color = new Color(1f, 1f, 1f, 0.85f * (1f - Mathf.Clamp01(since / 0.25f)));
            said.maxVisibleCharacters = Mathf.RoundToInt(said.text.Length * Mathf.Clamp01((since - 0.2f) / (lineLength * 0.85f)));
            yield return null;
        }
        if (voice != null && voice.isPlaying) voice.Stop();

        // Wipe everything off to the left.
        for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.35f;
            root.anchoredPosition = new Vector2(-2200f * k * k, 0);
            group.alpha = 1f - k;
            yield return null;
        }
        Destroy(root.gameObject);
    }

    TMP_FontAsset CardFont(Font source, ref TMP_FontAsset asset) => BuildFont(source, ref asset, 90, 16);

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
    // Titles use the brush font; everything else the clean body font. Colour and size are left to the caller.
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
        // Headings and titles get a heavier ink edge; hints stay light so they read as secondary.
        float outline = style == TextStyle.Title ? 0.24f : style == TextStyle.Hint ? 0.12f : 0.18f;
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(14, 6, 12, 255));
        // The outline eats half into each letter, so fatten the face back up.
        material.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.6f);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, outline);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        styleMaterials[style] = material;
        return material;
    }

    // A dark ink outline and a hard drop shadow, like the lettering on a Borderlands card.
    static void Ink(TMP_Text text, float width)
    {
        // Lay the text out first: a fresh dynamic font only gets its real atlas once it adds the glyphs, and the
        // material instance below would otherwise keep the placeholder texture.
        text.ForceMeshUpdate(true);
        text.outlineWidth = width;
        text.outlineColor = new Color32(22, 6, 10, 255);
        Material material = text.fontMaterial;
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.7f));
        material.SetFloat("_UnderlayOffsetX", 0.7f);
        material.SetFloat("_UnderlayOffsetY", -0.7f);
        material.SetFloat("_UnderlaySoftness", 0.1f);
    }

    // Knocks each letter a little off the line (lifted, tilted, resized) so the name reads as hand-lettered.
    // Seeded, so a boss's name is lettered the same way every time.
    static void ScatterLetters(TMP_Text text, int seed)
    {
        text.ForceMeshUpdate(true);
        var rng = new System.Random(seed);
        TMP_TextInfo info = text.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo letter = info.characterInfo[i];
            if (!letter.isVisible) continue;
            Vector3[] vertices = info.meshInfo[letter.materialReferenceIndex].vertices;
            Vector3 mid = (vertices[letter.vertexIndex] + vertices[letter.vertexIndex + 2]) / 2f;
            Quaternion tilt = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 14f - 7f);
            Vector3 lift = Vector3.up * ((float)rng.NextDouble() * 26f - 13f);
            float scale = 0.9f + (float)rng.NextDouble() * 0.22f;
            for (int v = 0; v < 4; v++)
            {
                vertices[letter.vertexIndex + v] = mid + lift + tilt * ((vertices[letter.vertexIndex + v] - mid) * scale);
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

    public void HideBossBar()
    {
        bossBar.SetActive(false);
    }

    void BuildBossBar()
    {
        RectTransform frame = CreateRect("BossBar", transform, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(904, 30));
        frame.gameObject.AddComponent<Image>().color = new Color(0.9f, 0.88f, 0.85f, 0.9f);
        RectTransform back = CreateRect("Back", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(896, 22));
        back.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.06f, 0.95f);
        bossTrail = Stretch(CreateRect("Trail", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossTrail.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.85f);
        bossFill = Stretch(CreateRect("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossFillImage = bossFill.gameObject.AddComponent<Image>();
        bossName = CreateText("BossName", new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(904, 48), 38, TextAlignmentOptions.Bottom, frame);
        bossName.characterSpacing = 4;
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
        scoreText.SetText("{0:000000}", score);
    }

    public void SetStage(int stage, int stageCount, int round, int roundCount)
    {
        levelText.text = $"STAGE {stage}/{stageCount}  ·  ROUND {round}/{roundCount}";
    }

    // One heart icon per remaining life.
    public void SetLives(int lives)
    {
        while (lifeIcons.Count < lives)
        {
            lifeIcons.Add(CreateLifeIcon(lifeIcons.Count));
        }
        for (int i = 0; i < lifeIcons.Count; i++)
        {
            lifeIcons[i].SetActive(i < lives);
        }
    }

    // A stage or round announcement across the middle of the screen. Unlike a full-screen overlay it leaves the
    // playfield visible above and below, and it fades in and out rather than popping.
    public void ShowBanner(string title, string body, Color titleColor)
    {
        bannerTitle.text = title;
        bannerTitle.color = titleColor;
        bannerBody.text = body;
        bannerBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
        bannerShown = true;
        banner.gameObject.SetActive(true);
    }

    public void HideBanner()
    {
        bannerShown = false;
    }

    void BuildBanner()
    {
        RectTransform panel = CreateRect("Banner", transform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1400, 300));
        banner = panel.gameObject.AddComponent<CanvasGroup>();
        banner.alpha = 0f;
        banner.blocksRaycasts = false;
        Image image = panel.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (overlayBanner != null)
        {
            image.sprite = overlayBanner;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = GetComponent<Canvas>().referencePixelsPerUnit / overlayBanner.pixelsPerUnit / overlayBannerPixelScale;
        }
        else
        {
            image.color = new Color(0.02f, 0.01f, 0.04f, 0.8f);
        }

        bannerTitle = CreateText("Title", new Vector2(0.5f, 0.5f), new Vector2(0, 45), new Vector2(1300, 140), 96, TextAlignmentOptions.Center, panel);
        bannerTitle.textWrappingMode = TextWrappingModes.NoWrap;
        Style(bannerTitle, TextStyle.Title);
        bannerBody = CreateText("Body", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(1200, 100), 38, TextAlignmentOptions.Center, panel);
        bannerBody.color = new Color(0.95f, 0.92f, 0.86f);
        Style(bannerBody, TextStyle.Body);
        panel.gameObject.SetActive(false);
    }

    // Fades the banner, with a slight scale-in so it lands rather than appears.
    void UpdateBanner()
    {
        if (!banner.gameObject.activeSelf) return;
        banner.alpha = Mathf.MoveTowards(banner.alpha, bannerShown ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
        banner.transform.localScale = Vector3.one * (1.06f - 0.06f * EaseOut(banner.alpha));
        if (!bannerShown && banner.alpha <= 0f) banner.gameObject.SetActive(false);
    }

    GameObject CreateLifeIcon(int index)
    {
        RectTransform icon = CreateRect("Life" + index, livesRow, new Vector2(0f, 0.5f), new Vector2(index * 58 + 12, 0), new Vector2(52, 52));
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
