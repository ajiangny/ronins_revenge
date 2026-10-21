using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    void Awake()
    {
        scoreText = CreateText("Score", new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(800, 50), 36, TextAlignmentOptions.Top);
        levelText = CreateText("Level", new Vector2(1f, 1f), new Vector2(-30, -20), new Vector2(400, 50), 36, TextAlignmentOptions.TopRight);

        TextMeshProUGUI livesLabel = CreateText("LivesLabel", new Vector2(0f, 0f), new Vector2(30, 20), new Vector2(150, 50), 36, TextAlignmentOptions.BottomLeft);
        livesLabel.text = "Lives";
        livesRow = CreateRect("LivesIcons", transform, new Vector2(0f, 0f), new Vector2(140, 26), new Vector2(400, 40));

        BuildOverlay();
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
