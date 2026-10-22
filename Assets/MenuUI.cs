using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

// A full-screen menu page: a title, an optional block of text, and a vertical list of buttons and sliders.
// Used for the title screen, pause, settings, controls and game over. Keyboard, mouse and gamepad all drive it,
// and it runs on unscaled time so it works while the game is paused.
public class MenuUI : MonoBehaviour
{
    public struct Item
    {
        public string label;
        public Action chosen;
        public Func<float> get;
        public Action<float> set;

        public bool IsSlider => set != null;

        public static Item Button(string label, Action chosen) => new Item { label = label, chosen = chosen };
        public static Item Slider(string label, Func<float> get, Action<float> set) => new Item { label = label, get = get, set = set };
    }

    // Keys can't be read for this long after a page opens, so the press that opened it doesn't also act on it.
    const float InputDelay = 0.2f;
    const float RowHeight = 84f;
    const float RowWidth = 760f;
    const float SliderStep = 0.1f;
    static readonly Color Accent = new Color(1f, 0.36f, 0.3f);
    static readonly Color Idle = new Color(0.86f, 0.83f, 0.78f);

    class Row
    {
        public RectTransform root;
        public Image bar;
        public RectTransform marker;
        public TextMeshProUGUI label;
        public RectTransform track;
        public RectTransform fill;
        public Image fillImage;
        public TextMeshProUGUI value;
        public float shownSelect;
    }

    GameUI ui;
    RectTransform page;
    Image dim;
    RectTransform titleReveal;
    TextMeshProUGUI titleText;
    TextMeshProUGUI bodyText;
    TextMeshProUGUI hintText;
    RectTransform rowsRoot;
    readonly List<Row> rows = new List<Row>();
    Item[] items = Array.Empty<Item>();
    Action back;
    bool logo;
    int selected;
    int heardSelection;
    float shownAt;

    public bool IsOpen => page != null && page.gameObject.activeSelf;

    public void Bind(GameUI gameUI)
    {
        ui = gameUI;
        Build();
    }

    // Opens a page, replacing whatever page was showing. `back` runs on Esc / gamepad B; null means Esc does nothing.
    // A `logo` page has a big brushed-on title over a lighter dim, so the scene shows through. The title is white
    // unless given a colour.
    public void Show(string title, string body, Item[] pageItems, Action onBack, bool isLogo = false, int startIndex = 0,
        Color titleColour = default)
    {
        items = pageItems;
        back = onBack;
        logo = isLogo;
        selected = heardSelection = Mathf.Clamp(startIndex, 0, items.Length - 1);
        shownAt = Time.unscaledTime;

        titleText.text = title;
        titleText.color = titleColour.a > 0f ? titleColour : Color.white;
        titleText.fontSize = logo ? 170 : 104;
        bodyText.text = body ?? "";
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        hintText.text = Hint("↑ ↓", "Select")
            + (Array.Exists(items, item => item.IsSlider) ? Hint("← →", "Adjust") : "")
            + Hint("Enter", "Confirm")
            + (onBack != null ? Hint("Esc", "Back") : "");

        // Stack title, body and rows down from the top, then centre the whole block on screen.
        float titleHeight = logo ? 230f : 150f;
        float bodyHeight = bodyText.gameObject.activeSelf ? bodyText.GetPreferredValues(bodyText.text, 1200f, 0f).y : 0f;
        float rowsHeight = items.Length * RowHeight;
        float gap = 36f;
        float total = titleHeight + (bodyHeight > 0f ? bodyHeight + gap : 0f) + gap + rowsHeight;
        float y = total / 2f + 30f;

        titleReveal.anchoredPosition = new Vector2(0, y - titleHeight / 2f);
        titleReveal.sizeDelta = new Vector2(logo ? 0f : 1800f, titleHeight + 60f);
        titleText.rectTransform.sizeDelta = new Vector2(1800f, titleHeight + 60f);
        y -= titleHeight + gap;
        if (bodyHeight > 0f)
        {
            bodyText.rectTransform.anchoredPosition = new Vector2(0, y);
            bodyText.rectTransform.sizeDelta = new Vector2(1200f, bodyHeight + 10f);
            y -= bodyHeight + gap;
        }
        rowsRoot.anchoredPosition = new Vector2(0, y);

        // Active before the rows are built, so their labels can be measured to place the markers.
        dim.color = new Color(0.02f, 0.01f, 0.04f, logo ? 0.45f : 0.8f);
        page.SetAsLastSibling();
        page.gameObject.SetActive(true);
        BuildRows();
        Animate(0f);
    }

    static string Hint(string key, string action) => $"<mark=#FFFFFF26 padding=\"12,12,2,2\">{key}</mark>  {action}       ";

    public void Hide()
    {
        if (page != null) page.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen) return;

        if (selected != heardSelection) Sfx.Play(Sfx.UiMove);
        heardSelection = selected;

        float elapsed = Time.unscaledTime - shownAt;
        Animate(elapsed);
        if (elapsed < InputDelay || items.Length == 0) return;

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        if (Pressed(keyboard?.upArrowKey, keyboard?.wKey, gamepad?.dpad.up, gamepad?.leftStick.up)) selected = (selected + items.Length - 1) % items.Length;
        if (Pressed(keyboard?.downArrowKey, keyboard?.sKey, gamepad?.dpad.down, gamepad?.leftStick.down)) selected = (selected + 1) % items.Length;
        if (Pressed(keyboard?.leftArrowKey, keyboard?.aKey, gamepad?.dpad.left, gamepad?.leftStick.left)) Nudge(-SliderStep);
        if (Pressed(keyboard?.rightArrowKey, keyboard?.dKey, gamepad?.dpad.right, gamepad?.leftStick.right)) Nudge(SliderStep);
        if (Pressed(keyboard?.enterKey, keyboard?.numpadEnterKey, keyboard?.spaceKey, gamepad?.buttonSouth))
        {
            Choose(selected);
            return;
        }
        if (back != null && Pressed(keyboard?.escapeKey, keyboard?.backspaceKey, gamepad?.buttonEast, gamepad?.startButton))
        {
            Sfx.Play(Sfx.UiConfirm);
            back();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        for (int i = 0; i < rows.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(rows[i].root, pointer, null)) continue;
            if (mouse.delta.ReadValue() != Vector2.zero) selected = i;
            if (items[i].IsSlider && mouse.leftButton.isPressed
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(rows[i].track, pointer, null, out Vector2 local)
                && local.x > -24f)
            {
                // Click or drag along the track to set the value directly.
                float value = Mathf.Clamp01(local.x / rows[i].track.rect.width);
                items[i].set(Mathf.Round(value / SliderStep) * SliderStep);
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                Choose(i);
                return;
            }
        }
    }

    static bool Pressed(params ButtonControl[] buttons)
    {
        foreach (ButtonControl button in buttons)
        {
            if (button != null && button.wasPressedThisFrame) return true;
        }
        return false;
    }

    void Nudge(float amount)
    {
        Item item = items[selected];
        if (!item.IsSlider) return;
        float before = item.get();
        float after = Mathf.Clamp01(Mathf.Round((before + amount) / SliderStep) * SliderStep);
        if (Mathf.Approximately(before, after)) return;
        item.set(after);
        Sfx.Play(Sfx.UiMove);
    }

    void Choose(int index)
    {
        Item item = items[index];
        if (item.chosen == null) return;
        Sfx.Play(Sfx.UiConfirm);
        item.chosen();
    }

    // Rows slide up into place one after another; the selected one gets a red brush bar, a marker and a nudge
    // forward, so it reads by shape and position as well as colour. A logo title is wiped on from the centre.
    void Animate(float elapsed)
    {
        float dt = Time.unscaledDeltaTime;
        if (logo) titleReveal.sizeDelta = new Vector2(1800f * EaseOut(Mathf.Clamp01((elapsed - 0.1f) / 0.7f)), titleReveal.sizeDelta.y);

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            float enter = EaseOut(Mathf.Clamp01((elapsed - 0.05f * i) / 0.25f));
            row.root.anchoredPosition = new Vector2(0, -RowHeight * (i + 0.5f) - 24f * (1f - enter));
            row.root.GetComponent<CanvasGroup>().alpha = enter;

            float target = i == selected ? 1f : 0f;
            row.shownSelect = Mathf.MoveTowards(row.shownSelect, target, dt / 0.12f);
            float k = EaseOut(row.shownSelect);
            row.root.localScale = Vector3.one * (1f + 0.08f * k);
            row.bar.rectTransform.sizeDelta = new Vector2(RowWidth * k, row.bar.rectTransform.sizeDelta.y);
            row.marker.localScale = Vector3.one * k;
            row.label.color = Color.Lerp(Idle, Color.white, k);
            // On the red selection bar a red fill would vanish, so it turns white.
            if (row.fillImage != null) row.fillImage.color = Color.Lerp(Accent, Color.white, k);

            if (items[i].IsSlider)
            {
                float value = items[i].get();
                row.fill.anchorMax = new Vector2(value, 1f);
                row.value.SetText("{0}%", Mathf.RoundToInt(value * 100f));
            }
        }
    }

    void BuildRows()
    {
        foreach (Row row in rows) Destroy(row.root.gameObject);
        rows.Clear();

        for (int i = 0; i < items.Length; i++)
        {
            var row = new Row();
            row.root = GameUI.CreateRect("Item" + i, rowsRoot, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(RowWidth, RowHeight - 8f));
            row.root.pivot = new Vector2(0.5f, 0.5f);
            row.root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            row.bar = GameUI.CreateRect("Bar", row.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, RowHeight - 14f)).gameObject.AddComponent<Image>();
            row.bar.sprite = ProceduralSprites.White;
            row.bar.color = new Color(Accent.r, Accent.g, Accent.b, 0.32f);
            row.bar.raycastTarget = false;

            // A small diamond to the left of the selected label.
            row.marker = GameUI.CreateRect("Marker", row.root, new Vector2(0.5f, 0.5f), new Vector2(-RowWidth / 2f + 46f, 0), new Vector2(20, 20));
            row.marker.localRotation = Quaternion.Euler(0, 0, 45f);
            Image marker = row.marker.gameObject.AddComponent<Image>();
            marker.color = Accent;
            marker.raycastTarget = false;

            bool slider = items[i].IsSlider;
            row.label = GameUI.CreateText(row.root, ui.BodyFont, "Label", new Vector2(slider ? 0f : 0.5f, 0.5f),
                slider ? new Vector2(76f, 0) : Vector2.zero, new Vector2(slider ? 300f : RowWidth, RowHeight), 44,
                slider ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            row.label.text = items[i].label;
            row.label.characterSpacing = 6;
            ui.Style(row.label, TextStyle.Heading);
            // A button's marker sits just left of its centred label.
            if (!slider)
            {
                row.label.ForceMeshUpdate(true);
                row.marker.anchoredPosition = new Vector2(row.label.textBounds.min.x - 34f, 0);
            }

            if (slider)
            {
                row.track = GameUI.CreateRect("Track", row.root, new Vector2(0f, 0.5f), new Vector2(330f, 0), new Vector2(280f, 16f));
                Image track = row.track.gameObject.AddComponent<Image>();
                track.color = new Color(0f, 0f, 0f, 0.45f);
                track.raycastTarget = false;
                row.fill = GameUI.CreateRect("Fill", row.track, Vector2.zero, Vector2.zero, Vector2.zero);
                row.fill.anchorMin = Vector2.zero;
                row.fill.anchorMax = Vector2.one;
                row.fill.sizeDelta = Vector2.zero;
                row.fillImage = row.fill.gameObject.AddComponent<Image>();
                row.fillImage.color = Accent;
                row.fillImage.raycastTarget = false;
                row.value = GameUI.CreateText(row.root, ui.BodyFont, "Value", new Vector2(1f, 0.5f), new Vector2(-24f, 0),
                    new Vector2(130f, RowHeight), 36, TextAlignmentOptions.Right);
                ui.Style(row.value, TextStyle.Hud);
            }
            rows.Add(row);
        }
    }

    void Build()
    {
        page = GameUI.CreateRect("Menu", transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        dim = page.gameObject.AddComponent<Image>();
        dim.raycastTarget = false;

        // The title sits in a mask that a logo page grows out from the centre, like a brushstroke spreading.
        titleReveal = GameUI.CreateRect("TitleReveal", page, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 260f));
        titleReveal.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(120, 0);
        titleText = GameUI.CreateText(titleReveal, ui.TitleFont, "Title", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(1800f, 260f), 104, TextAlignmentOptions.Center);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.color = Color.white;
        ui.Style(titleText, TextStyle.Title);

        bodyText = GameUI.CreateText(page, ui.BodyFont, "Body", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 200f),
            36, TextAlignmentOptions.Top);
        bodyText.rectTransform.pivot = new Vector2(0.5f, 1f);
        bodyText.richText = true;
        ui.Style(bodyText, TextStyle.Body);

        rowsRoot = GameUI.CreateRect("Items", page, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RowWidth, 0f));
        rowsRoot.pivot = new Vector2(0.5f, 1f);

        hintText = GameUI.CreateText(page, ui.BodyFont, "Hint", new Vector2(0.5f, 0f), new Vector2(0, 40f), new Vector2(1600f, 50f),
            28, TextAlignmentOptions.Bottom);
        hintText.color = GameUI.MutedColour;
        ui.Style(hintText, TextStyle.Hint);

        page.gameObject.SetActive(false);
    }

    static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
}
