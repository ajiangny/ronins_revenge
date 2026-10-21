using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

    // A boss's entrance: the screen darkens under a crimson ink slash, her full-body art slides in beside her
    // name, then a close-up of her face slams across the screen while she speaks her line. Runs on unscaled
    // time; ENTER or SPACE skips to the fight.
    public IEnumerator PlayBossIntro(Sprite full, Sprite closeUp, string title, string epithet, Color colour,
        AudioSource voice, AudioClip line, string lineText)
    {
        var rims = new List<RectTransform>();
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

        TextMeshProUGUI name = CreateText("Name", new Vector2(0f, 0.5f), new Vector2(130, 150), new Vector2(1100, 190), 150, TextAlignmentOptions.Left, root);
        name.text = title;
        name.fontStyle = FontStyles.Bold;
        name.color = Color.clear;
        TextMeshProUGUI epithetText = CreateText("Epithet", new Vector2(0f, 0.5f), new Vector2(140, 40), new Vector2(1100, 70), 52, TextAlignmentOptions.Left, root);
        epithetText.text = epithet;
        epithetText.color = Color.clear;

        // The close-up: a tilted letterbox panel across the lower left, cropped to her eyes and pipe. A stencil
        // Mask, since RectMask2D can't clip a rotated rectangle.
        const float stripWidth = 1500f, stripHeight = 300f;
        RectTransform strip = CreateRect("CloseUp", root, new Vector2(0.5f, 0.5f), new Vector2(-180, -230), new Vector2(stripWidth, stripHeight));
        strip.localRotation = Quaternion.Euler(0, 0, -5f);
        strip.gameObject.AddComponent<Image>().color = new Color(0.06f, 0f, 0.02f, 1f);
        strip.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        const float faceWidth = stripWidth;
        float faceHeight = faceWidth * closeUp.rect.height / closeUp.rect.width;
        // Her eyes sit about 53% across and 30% down the close-up: centre the strip on them.
        Vector2 faceRest = new Vector2(-(0.53f - 0.5f) * faceWidth + 35f, -(0.5f - 0.30f) * faceHeight);
        RectTransform face = CreateRect("Face", strip, new Vector2(0.5f, 0.5f), faceRest, new Vector2(faceWidth, faceHeight));
        face.gameObject.AddComponent<Image>().sprite = closeUp;
        // Crimson rims drawn over the strip's edges, outside the mask so they stay crisp.
        foreach (float edge in new[] { 1f, -1f })
        {
            RectTransform rim = CreateRect("Rim", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(stripWidth + 40, 8));
            rim.localRotation = strip.localRotation;
            rim.anchoredPosition = strip.anchoredPosition + (Vector2)(strip.localRotation * new Vector3(0, edge * stripHeight / 2f, 0));
            rim.gameObject.AddComponent<Image>().color = colour;
            rims.Add(rim);
        }
        strip.localScale = new Vector3(1f, 0f, 1f);

        TextMeshProUGUI said = CreateText("Line", new Vector2(0.5f, 0.5f), new Vector2(-180, -478), new Vector2(1500, 100), 32, TextAlignmentOptions.Top, root);
        said.text = lineText ?? "";
        said.fontStyle = FontStyles.Italic;
        said.maxVisibleCharacters = 0;

        Image flash = Stretch(CreateRect("Flash", root, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        flash.color = Color.clear;

        const float closeUpAt = 1.4f;
        float lineLength = line != null ? line.length : 3f;
        float end = closeUpAt + lineLength + 0.6f;
        bool spoke = false;
        for (float t = 0; t < end; t += Time.unscaledDeltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            if (t > 0.3f && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) break;

            dim.color = new Color(0.03f, 0f, 0.02f, 0.85f * Mathf.Clamp01(t / 0.25f));
            slash.sizeDelta = new Vector2(Mathf.Lerp(0, 2700, EaseOut(Mathf.Clamp01(t / 0.22f))), 44);
            slashImage.color = new Color(colour.r, colour.g, colour.b, 1f - Mathf.Clamp01((t - 0.6f) / 0.5f));

            float slide = EaseOut(Mathf.Clamp01((t - 0.2f) / 0.4f));
            art.anchoredPosition = new Vector2(Mathf.Lerp(artStartX, artRestX, slide), -10f);
            art.localScale = Vector3.one * (1f + 0.05f * Mathf.Clamp01((t - 0.6f) / (end - 0.6f)));

            float stamp = Mathf.Clamp01((t - 0.35f) / 0.18f);
            name.color = new Color(colour.r, colour.g, colour.b, stamp);
            name.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, EaseOut(stamp));
            epithetText.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 0.6f) / 0.3f));

            if (!spoke && t >= closeUpAt)
            {
                spoke = true;
                if (voice != null && line != null)
                {
                    voice.clip = line;
                    voice.Play();
                }
                CameraShake.Add(0.35f);
                Sfx.Play(Sfx.ParrySwing, 1f, 0.55f);
            }
            float since = t - closeUpAt;
            float open = since < 0 ? 0f : EaseOut(Mathf.Clamp01(since / 0.12f));
            strip.localScale = new Vector3(1f, open, 1f);
            foreach (RectTransform rim in rims) rim.localScale = new Vector3(open, 1f, 1f);
            face.anchoredPosition = faceRest + Vector2.left * (70f * Mathf.Clamp01(since / lineLength));
            flash.color = new Color(1f, 1f, 1f, since < 0 ? 0f : 0.85f * (1f - Mathf.Clamp01(since / 0.25f)));
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
