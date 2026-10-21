using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the HUD (score, level, lives) and the full-screen overlay used for the title/level/end screens.
[RequireComponent(typeof(Canvas))]
public class GameUI : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] TMP_FontAsset font;
    [Tooltip("Ronin head shown once per remaining life.")]
    [SerializeField] Sprite lifeIcon;
    [Tooltip("9-sliced panel drawn behind the title/stage/end screen text.")]
    [SerializeField] Sprite overlayBanner;
    [Tooltip("Screen pixels per art pixel for the banner's border, so the pixel art stays chunky and even.")]
    [SerializeField] float overlayBannerPixelScale = 2f;
    [Tooltip("Hand-lettered font for a boss's name on her title card. Without it the card uses the HUD font.")]
    [SerializeField] Font cardNameFont;
    [Tooltip("Chunky outlined font for a boss's epithet on her title card. Without it the card uses the HUD font.")]
    [SerializeField] Font cardTagFont;

    public TMP_FontAsset Font => font;

    TextMeshProUGUI scoreText;
    TextMeshProUGUI levelText;
    RectTransform livesRow;
    readonly List<GameObject> lifeIcons = new List<GameObject>();

    GameObject overlay;
    TextMeshProUGUI overlayTitle;
    TextMeshProUGUI overlayBody;

    GameObject bossBar;
    TextMeshProUGUI bossName;
    RectTransform bossFill;
    Image bossFillImage;
    float shownBossHealth = 1f;
    float bossHealth = 1f;

    TextMeshProUGUI subtitle;
    float subtitleUntil;

    TMP_FontAsset cardNameAsset;
    TMP_FontAsset cardTagAsset;

    void Awake()
    {
        scoreText = CreateText("Score", new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(800, 50), 36, TextAlignmentOptions.Top);
        levelText = CreateText("Level", new Vector2(1f, 1f), new Vector2(-30, -20), new Vector2(600, 50), 36, TextAlignmentOptions.TopRight);

        TextMeshProUGUI livesLabel = CreateText("LivesLabel", new Vector2(0f, 0f), new Vector2(30, 20), new Vector2(150, 50), 36, TextAlignmentOptions.BottomLeft);
        livesLabel.text = "Lives";
        livesRow = CreateRect("LivesIcons", transform, new Vector2(0f, 0f), new Vector2(140, 26), new Vector2(400, 40));

        BuildBossBar();
        subtitle = CreateText("Subtitle", new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1400, 90), 34, TextAlignmentOptions.Bottom);
        subtitle.fontStyle = FontStyles.Italic;
        subtitle.gameObject.SetActive(false);
        BuildOverlay();
    }

    void Update()
    {
        if (subtitle.gameObject.activeSelf && Time.unscaledTime > subtitleUntil) subtitle.gameObject.SetActive(false);

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

    // A spoken line shown along the bottom of the screen for `seconds`.
    public void ShowSubtitle(string text, Color colour, float seconds)
    {
        subtitle.text = text;
        subtitle.color = Color.Lerp(colour, Color.white, 0.35f);
        subtitleUntil = Time.unscaledTime + seconds;
        subtitle.gameObject.SetActive(true);
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
        TextMeshProUGUI said = CreateText("Line", new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(1600, 110), 32, TextAlignmentOptions.Center, card);
        said.text = lineText ?? "";
        said.fontStyle = FontStyles.Italic;
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

    // The title card's fonts are built from their TTFs on first use; without one the card uses the HUD font.
    TMP_FontAsset CardFont(Font source, ref TMP_FontAsset asset)
    {
        if (asset == null && source != null) asset = TMP_FontAsset.CreateFontAsset(source, 90, 16, GlyphRenderMode.SDFAA, 1024, 1024);
        return asset != null ? asset : font;
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
        RectTransform frame = CreateRect("BossBar", transform, new Vector2(0.5f, 1f), new Vector2(0, -115), new Vector2(904, 30));
        frame.gameObject.AddComponent<Image>().color = new Color(0.9f, 0.88f, 0.85f, 0.9f);
        RectTransform back = CreateRect("Back", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(896, 22));
        back.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.06f, 0.95f);
        bossTrail = Stretch(CreateRect("Trail", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossTrail.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.85f);
        bossFill = Stretch(CreateRect("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero));
        bossFillImage = bossFill.gameObject.AddComponent<Image>();
        bossName = CreateText("BossName", new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(904, 44), 34, TextAlignmentOptions.Bottom, frame);
        bossName.fontStyle = FontStyles.Bold;
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

    public void SetScore(int score, int highScore)
    {
        scoreText.text = $"Score {score:000000}      High {highScore:000000}";
    }

    public void SetStage(int stage, int stageCount, int round, int roundCount)
    {
        levelText.text = $"Stage {stage}/{stageCount}   Round {round}/{roundCount}";
    }

    // One ronin head icon per remaining life.
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

    public void ShowOverlay(string title, string body, Color titleColor)
    {
        overlayTitle.text = title;
        overlayTitle.color = titleColor;
        overlayBody.text = body;
        overlay.SetActive(true);
    }

    public void HideOverlay()
    {
        overlay.SetActive(false);
    }

    void BuildOverlay()
    {
        RectTransform panel = CreateRect("Overlay", transform, Vector2.zero, Vector2.zero, Vector2.zero);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0.05f, 0.75f);
        overlay = panel.gameObject;

        if (overlayBanner != null)
        {
            RectTransform banner = CreateRect("Banner", panel, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1560, 500));
            Image image = banner.gameObject.AddComponent<Image>();
            image.sprite = overlayBanner;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = GetComponent<Canvas>().referencePixelsPerUnit / overlayBanner.pixelsPerUnit / overlayBannerPixelScale;
        }

        overlayTitle = CreateText("Title", new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1600, 180), 110, TextAlignmentOptions.Center, panel);
        overlayTitle.fontStyle = FontStyles.Bold;
        overlayBody = CreateText("Body", new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(1400, 360), 34, TextAlignmentOptions.Top, panel);
    }

    GameObject CreateLifeIcon(int index)
    {
        RectTransform icon = CreateRect("Life" + index, livesRow, new Vector2(0f, 0.5f), new Vector2(index * 54 + 24, 0), new Vector2(48, 48));
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
