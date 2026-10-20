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
    [SerializeField] Color lifeIconColor = new Color(0.4f, 0.85f, 1f);

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

    public void SetLevel(int level, int levelCount)
    {
        levelText.text = $"Stage {level} / {levelCount}";
    }

    // One small ship icon per remaining life.
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

        overlayTitle = CreateText("Title", new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1600, 180), 110, TextAlignmentOptions.Center, panel);
        overlayTitle.fontStyle = FontStyles.Bold;
        overlayBody = CreateText("Body", new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(1400, 360), 40, TextAlignmentOptions.Top, panel);
    }

    GameObject CreateLifeIcon(int index)
    {
        RectTransform icon = CreateRect("Life" + index, livesRow, new Vector2(0f, 0.5f), new Vector2(index * 60 + 25, 0), new Vector2(48, 30));
        // A wide hull with a cockpit on top, roughly matching the ship's silhouette.
        RectTransform hull = CreateRect("Hull", icon, new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(48, 14));
        hull.gameObject.AddComponent<Image>().color = lifeIconColor;
        RectTransform cockpit = CreateRect("Cockpit", icon, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(16, 16));
        cockpit.gameObject.AddComponent<Image>().color = lifeIconColor;
        return icon.gameObject;
    }

    TextMeshProUGUI CreateText(string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment, Transform parent = null)
    {
        RectTransform rect = CreateRect(name, parent != null ? parent : transform, anchor, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
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
