using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The resources held, stacked in the bottom-right corner like Hades' House: each a count with its looping icon to the
// right. Each resource is one Add; one at zero stays hidden until it's found, so the column only grows as the game
// opens up. It has its own canvas sorted over the menu pages, so it stays up while a station's page is open.
public class ResourceTray : MonoBehaviour
{
    const float RowStep = 58f;
    const float IconSize = 50f;
    const float FontSize = 42f;
    const float RightMargin = 30f;
    const float BottomMargin = 26f;
    // A count that changes swells for a moment, so a purchase reads at a glance.
    const float PunchSeconds = 0.3f;

    class Entry
    {
        public Func<int> count;
        public RectTransform root;
        public TextMeshProUGUI text;
        public int shown = -1;
        public float changedAt = -1f;
    }

    readonly List<Entry> entries = new List<Entry>();
    GameUI ui;

    public static ResourceTray Build(GameUI gameUI)
    {
        RectTransform rect = GameUI.CreateRect("ResourceTray", gameUI.transform, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        Canvas canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        var tray = rect.gameObject.AddComponent<ResourceTray>();
        tray.ui = gameUI;
        rect.gameObject.SetActive(false);
        return tray;
    }

    public void Add(string name, Func<int> count, Sprite[] icon)
    {
        var entry = new Entry { count = count };
        entry.root = GameUI.CreateRect(name, transform, new Vector2(1f, 0f), Vector2.zero, new Vector2(400f, RowStep));
        Image image = GameUI.CreateRect("Icon", entry.root, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize))
            .gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (icon != null && icon.Length > 0)
        {
            image.sprite = icon[0];
            image.gameObject.AddComponent<SpriteAnimatorUI>().Play(icon, 8f);
        }
        entry.text = GameUI.CreateText(entry.root, ui.BodyFont, "Count", new Vector2(1f, 0.5f), new Vector2(-IconSize - 12f, 0),
            new Vector2(320f, RowStep), FontSize, TextAlignmentOptions.Right);
        entry.text.textWrappingMode = TextWrappingModes.NoWrap;
        entry.text.color = Color.white;
        ui.Style(entry.text, TextStyle.Hud);
        entries.Add(entry);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible) return;
        gameObject.SetActive(visible);
        // Opening on a value already held shouldn't swell it.
        foreach (Entry entry in entries) entry.shown = -1;
    }

    void Update()
    {
        // Stacked up from the corner in the order added, the first on top, skipping any not yet found.
        int visible = 0;
        foreach (Entry entry in entries) if (entry.count() > 0) visible++;
        int row = 0;
        foreach (Entry entry in entries)
        {
            int count = entry.count();
            entry.root.gameObject.SetActive(count > 0);
            if (count <= 0) continue;
            if (count != entry.shown)
            {
                if (entry.shown >= 0) entry.changedAt = Time.unscaledTime;
                entry.shown = count;
                entry.text.SetText("{0}", count);
            }
            entry.root.anchoredPosition = new Vector2(-RightMargin, BottomMargin + RowStep * (visible - 1 - row + 0.5f));
            float t = entry.changedAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - entry.changedAt) / PunchSeconds);
            entry.text.rectTransform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t * Mathf.PI));
            row++;
        }
    }
}
