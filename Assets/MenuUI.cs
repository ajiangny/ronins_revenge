using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

// A full-screen menu page: a title, an optional block of text, and a vertical list (or a grid) of buttons and sliders.
// Used for pause, settings, controls and game over; the title screen has its own light layout (ShowTitle). Keyboard, mouse and gamepad all drive it,
// and it runs on unscaled time so it works while the game is paused.
public class MenuUI : MonoBehaviour
{
    public struct Item
    {
        public string label;
        public Action chosen;
        public Func<float> get;
        public Action<float> set;
        // A line shown under the rows while this item is selected (the Flute Songs' effects).
        public string note;
        // Table rows (the Flute Songs): a right-aligned value, and rank pips.
        public string detail;
        public int pips;
        public int pipsMax;
        // A card's text under its heading (a page of cards side by side, like the new journey's control choice).
        public string card;

        public bool IsSlider => set != null;
        public bool IsTableRow => detail != null || pipsMax > 0;
        public bool IsCard => card != null;

        public static Item Button(string label, Action chosen, string note = null) => new Item { label = label, chosen = chosen, note = note };
        public static Item Card(string label, string card, Action chosen) => new Item { label = label, card = card, chosen = chosen };
        public static Item Slider(string label, Func<float> get, Action<float> set) => new Item { label = label, get = get, set = set };
    }

    // Keys can't be read for this long after a page opens, so the press that opened it doesn't also act on it.
    const float InputDelay = 0.2f;
    const float RowHeight = 84f;
    const float RowWidth = 760f;
    // Grid pages (more rows than fit in a column) use narrower cells side by side.
    const float GridCellWidth = 620f;
    // A table page (any table row) is denser and wider, so its columns line up and the page fits with its note line.
    const float TableRowHeight = 66f;
    const float TableCellWidth = 740f;
    const float TableFontSize = 36f;
    // Right-hand columns of a table row, from the cell's right edge: the value, then the pips.
    const float DetailWidth = 110f;
    const float PipSize = 13f;
    const float PipSpacing = 24f;
    const float GridGap = 70f;
    // A card page's cells: tall panels side by side, one per choice.
    const float CardWidth = 580f;
    const float CardHeight = 480f;
    const float SliderStep = 0.1f;
    static readonly Color Accent = new Color(1f, 0.36f, 0.3f);
    static readonly Color Idle = new Color(0.86f, 0.83f, 0.78f);
    // The title screen is ink on paper, like a Final Fantasy title: dark labels, a deep red for the selection.
    const float LightRowWidth = 300f;
    const float LightRowHeight = 66f;
    static readonly Color Paper = new Color(0.975f, 0.963f, 0.945f);
    static readonly Color Ink = new Color(0.17f, 0.13f, 0.15f);
    static readonly Color InkAccent = new Color(0.72f, 0.1f, 0.08f);

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
        public TextMeshProUGUI detail;
        public Image frame;
        public float shownSelect;
    }

    GameUI ui;
    RectTransform page;
    Image dim;
    RectTransform titleReveal;
    TextMeshProUGUI titleText;
    TextMeshProUGUI bodyText;
    TextMeshProUGUI hintText;
    TextMeshProUGUI noteText;
    RectTransform rowsRoot;
    readonly List<Row> rows = new List<Row>();
    Item[] items = Array.Empty<Item>();
    Action back;
    bool light;
    // A regular page drawn ink-on-paper, for pages opened from the title screen.
    bool onPaper;
    Image paper;
    Image titleArt;
    Image figure;
    SpriteAnimatorUI figureAnimator;
    // Music notes drifting up from the figure's flute.
    static readonly Color NoteColour = new Color(0.25f, 0.72f, 0.56f);
    const float NoteLife = 2.8f;
    readonly List<(Image image, float born, Vector2 from, float drift)> liveNotes = new List<(Image, float, Vector2, float)>();
    readonly List<Image> spareNotes = new List<Image>();
    Vector2 noteSource;
    float notePixel;
    float nextNoteAt;
    int nextNoteKind;
    float figurePixel;
    // The title page's figure stays on every paper page opened from it, so START can stand him up from any of them.
    bool figureWanted;
    // Set once START is chosen: the title page stops taking input while its logo and rows fade, then the paper
    // itself fades to show the field.
    bool leaving;
    float leftAt;
    int columns = 1;
    // Set while the page has table rows: shorter, wider cells with left-aligned labels.
    bool table;
    // Set while every item is a card: one row of panels, picked with left and right.
    bool cards;
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
    // The title is white (ink on paper) unless given a colour. A `paper` page sits on the title screen's white page
    // instead of over the dimmed game. With more than one column the items fill a grid row by row.
    public void Show(string title, string body, Item[] pageItems, Action onBack, int startIndex = 0, Color titleColour = default,
        bool paper = false, int gridColumns = 1)
    {
        items = pageItems;
        cards = items.Length > 0 && Array.TrueForAll(items, item => item.IsCard);
        columns = cards ? items.Length : Mathf.Max(1, gridColumns);
        table = Array.Exists(items, item => item.IsTableRow);
        back = onBack;
        SetLight(false);
        SetPaper(paper);
        figure.gameObject.SetActive(paper && figureWanted);
        figure.color = Color.white;
        selected = heardSelection = Mathf.Clamp(startIndex, 0, items.Length - 1);
        shownAt = Time.unscaledTime;

        titleText.text = title;
        titleText.color = titleColour.a > 0f ? titleColour : paper ? Ink : Color.white;
        titleText.fontSize = 96;
        bodyText.text = body ?? "";
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        noteText.gameObject.SetActive(Array.Exists(items, item => item.note != null));
        noteText.color = paper ? Ink : Color.white;
        if (paper) noteText.fontSharedMaterial = noteText.font.material;
        else ui.Style(noteText, TextStyle.Body);
        hintText.text = Hint(cards ? "← →" : columns > 1 ? "↑ ↓ ← →" : "↑ ↓", "Select")
            + (Array.Exists(items, item => item.IsSlider) ? Hint("← →", "Adjust") : "")
            + Hint("Enter", "Confirm")
            // In a browser Esc can be taken by fullscreen, so the web build names Backspace too.
            + (onBack != null ? Hint(Application.platform == RuntimePlatform.WebGLPlayer ? "Esc / Bksp" : "Esc", "Back") : "");

        // Stack title, body and rows down from the top, then centre the whole block on screen.
        const float titleHeight = 150f;
        float bodyHeight = bodyText.gameObject.activeSelf ? bodyText.GetPreferredValues(bodyText.text, 1200f, 0f).y : 0f;
        float rowsHeight = GridRows * PageRowHeight;
        float gap = 36f;
        // The selected item's note sits just under the rows, so it can never run into the last one.
        const float noteHeight = 60f;
        bool hasNote = noteText.gameObject.activeSelf;
        float total = titleHeight + (bodyHeight > 0f ? bodyHeight + gap : 0f) + gap + rowsHeight + (hasNote ? gap / 2f + noteHeight : 0f);
        float y = total / 2f + 30f;

        titleReveal.anchoredPosition = new Vector2(0, y - titleHeight / 2f);
        titleReveal.sizeDelta = new Vector2(1800f, titleHeight + 60f);
        titleText.rectTransform.sizeDelta = new Vector2(1800f, titleHeight + 60f);
        y -= titleHeight + gap;
        if (bodyHeight > 0f)
        {
            bodyText.rectTransform.anchoredPosition = new Vector2(0, y);
            bodyText.rectTransform.sizeDelta = new Vector2(1200f, bodyHeight + 10f);
            y -= bodyHeight + gap;
        }
        rowsRoot.anchoredPosition = new Vector2(0, y);
        noteText.rectTransform.anchoredPosition = new Vector2(0, y - rowsHeight - gap / 2f);
        noteText.rectTransform.sizeDelta = new Vector2(1600f, noteHeight);

        // Active before the rows are built, so their labels can be measured to place the markers.
        dim.color = paper ? Color.clear : new Color(0.02f, 0.01f, 0.04f, 0.8f);
        page.SetAsLastSibling();
        page.gameObject.SetActive(true);
        BuildRows();
        Animate(0f);
    }

    // The title screen, laid out like a Final Fantasy title: the game's logo centred high on a paper-white page and
    // a short left-aligned menu column under it, the selection marked in red. Without logo art the title is set
    // in the header font.
    public void ShowTitle(string title, Item[] pageItems)
    {
        items = pageItems;
        columns = 1;
        table = false;
        cards = false;
        back = null;
        selected = heardSelection = 0;
        shownAt = Time.unscaledTime;
        leaving = false;
        SetLight(true);

        bool art = ui.TitleLogo != null;
        titleArt.gameObject.SetActive(art);
        titleReveal.gameObject.SetActive(!art);
        titleReveal.anchoredPosition = LogoCentre;
        titleReveal.sizeDelta = new Vector2(1100f, 300f);
        titleText.rectTransform.sizeDelta = new Vector2(1100f, 300f);
        titleText.text = title;
        titleText.fontSize = 120;
        titleText.color = Ink;
        titleText.fontSharedMaterial = titleText.font.material;
        hintText.text = $"↑ ↓  Select      Enter  Confirm";

        // The column is centred under the logo by its text, not its box, so short labels don't drift left.
        rowsRoot.anchoredPosition = new Vector2(MenuColumnX, -40f);
        dim.color = Color.clear;
        page.SetAsLastSibling();
        page.gameObject.SetActive(true);
        BuildRows();
        Animate(0f);
    }

    // A figure standing (or sitting) on the title page, its feet at `feet` in page coordinates and each art pixel
    // `pixelSize` page units across, looping `frames`. Null or empty frames hide it. With a `notesFrom` art pixel
    // (from the frame's top-left), music notes float up from there.
    public void ShowFigure(Sprite[] frames, float fps, Vector2 feet, float pixelSize, Vector2? notesFrom = null)
    {
        foreach (var note in liveNotes) Release(note.image);
        liveNotes.Clear();
        notePixel = notesFrom.HasValue ? pixelSize : 0f;
        figurePixel = pixelSize;
        bool shown = frames != null && frames.Length > 0;
        figureWanted = shown;
        figure.gameObject.SetActive(shown);
        if (!shown) return;
        figure.sprite = frames[0];
        figure.rectTransform.anchoredPosition = feet;
        figure.rectTransform.sizeDelta = frames[0].rect.size * pixelSize;
        figureAnimator.Play(frames, fps);
        if (notesFrom.HasValue)
        {
            Vector2 size = frames[0].rect.size;
            noteSource = feet + new Vector2(notesFrom.Value.x - size.x / 2f, size.y - notesFrom.Value.y) * pixelSize;
            nextNoteAt = Time.unscaledTime + 0.6f;
        }
    }

    // Plays `frames` once on the title figure (the ronin getting up), holding the last frame, then calls `onDone`.
    // The figure keeps its feet where they were, so clips on a different canvas size still stand on the same row.
    public void PlayFigureOnce(Sprite[] frames, float fps, Action onDone)
    {
        notePixel = 0f;
        if (frames == null || frames.Length == 0 || !figure.gameObject.activeSelf)
        {
            onDone?.Invoke();
            return;
        }
        figure.sprite = frames[0];
        figure.rectTransform.sizeDelta = frames[0].rect.size * figurePixel;
        figureAnimator.PlayOnce(frames, fps, onDone);
    }

    public void HideFigure()
    {
        figureWanted = false;
        figureAnimator.Play(null, 0f);
        figure.gameObject.SetActive(false);
    }

    // Fades the title page's logo, rows and hint out and stops taking input; the paper stays until FadePaper.
    public void LeaveTitle()
    {
        leaving = true;
        leftAt = Time.unscaledTime;
    }

    // Fades the paper away over `seconds` of unscaled time to reveal the field under it, then closes the page.
    public System.Collections.IEnumerator FadePaper(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            paper.color = new Color(Paper.r, Paper.g, Paper.b, 1f - k * k * (3f - 2f * k));
            yield return null;
        }
        Hide();
    }

    // Shows one frame on the title figure with its feet at `feet`, for when it follows the real ronin.
    public void PlaceFigure(Sprite frame, Vector2 feet)
    {
        if (frame == null) return;
        figureAnimator.Play(null, 0f);
        figure.sprite = frame;
        figure.rectTransform.sizeDelta = frame.rect.size * figurePixel;
        figure.rectTransform.anchoredPosition = feet;
    }

    static readonly Vector2 LogoCentre = new Vector2(0f, 250f);
    const float MenuColumnX = 40f;

    // Switches between the dark overlay pages and the light title page.
    void SetLight(bool on)
    {
        light = on;
        onPaper = false;
        paper.gameObject.SetActive(on);
        if (!on)
        {
            titleArt.gameObject.SetActive(false);
            figure.gameObject.SetActive(false);
            titleReveal.gameObject.SetActive(true);
            ui.Style(titleText, TextStyle.Title);
            rowsRoot.anchoredPosition = new Vector2(0, rowsRoot.anchoredPosition.y);
        }
        if (on)
        {
            bodyText.gameObject.SetActive(false);
            noteText.gameObject.SetActive(false);
        }
        hintText.color = on ? new Color(Ink.r, Ink.g, Ink.b, 0.55f) : GameUI.MutedColour;
        hintText.fontSharedMaterial = on ? hintText.font.material : null;
        if (!on) ui.Style(hintText, TextStyle.Hint);
        hintText.rectTransform.anchoredPosition = new Vector2(0, 40f);
        hintText.rectTransform.sizeDelta = new Vector2(1600f, 50f);
    }

    // Ink on paper needs no outline or shadow, so paper pages draw their text with the font's plain material.
    void SetPaper(bool on)
    {
        onPaper = on;
        paper.gameObject.SetActive(on);
        bodyText.color = on ? Ink : Color.white;
        hintText.color = on ? new Color(Ink.r, Ink.g, Ink.b, 0.55f) : GameUI.MutedColour;
        if (on)
        {
            titleText.fontSharedMaterial = titleText.font.material;
            bodyText.fontSharedMaterial = bodyText.font.material;
            hintText.fontSharedMaterial = hintText.font.material;
        }
        else
        {
            ui.Style(bodyText, TextStyle.Body);
        }
    }

    // Colours for rich text on a page: a muted label colour and a key-cap tint that read on dark or on paper.
    public static string MutedHex(bool paper) => paper ? "6E6268" : ColorUtility.ToHtmlStringRGB(GameUI.MutedColour);
    public static string KeyCap(string key, bool paper) => $"<mark=#{(paper ? "0000001A" : "FFFFFF2E")} padding=\"10,10,2,2\">{key}</mark>";

    string Hint(string key, string action) => $"<mark=#{(onPaper || light ? "00000014" : "FFFFFF26")} padding=\"12,12,2,2\">{key}</mark>  {action}       ";

    public void Hide()
    {
        if (page != null) page.gameObject.SetActive(false);
        leaving = false;
        if (paper != null) paper.color = Paper;
    }

    void Update()
    {
        if (!IsOpen) return;
        UpdateNotes();

        if (selected != heardSelection) Sfx.Play(Sfx.UiMove);
        heardSelection = selected;
        if (noteText.gameObject.activeSelf && items.Length > 0) noteText.text = items[selected].note ?? "";

        float elapsed = Time.unscaledTime - shownAt;
        Animate(elapsed);
        if (leaving || elapsed < InputDelay || items.Length == 0) return;

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        if (Pressed(keyboard?.upArrowKey, keyboard?.wKey, gamepad?.dpad.up, gamepad?.leftStick.up)) selected = StepVertical(-1);
        if (Pressed(keyboard?.downArrowKey, keyboard?.sKey, gamepad?.dpad.down, gamepad?.leftStick.down)) selected = StepVertical(1);
        if (Pressed(keyboard?.leftArrowKey, keyboard?.aKey, gamepad?.dpad.left, gamepad?.leftStick.left)) StepSideways(-1);
        if (Pressed(keyboard?.rightArrowKey, keyboard?.dKey, gamepad?.dpad.right, gamepad?.leftStick.right)) StepSideways(1);
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

    int GridRows => (items.Length + columns - 1) / columns;

    // Up and down move within the selection's column, wrapping top to bottom. A short last row has no cell under
    // some columns, so those wrap past it.
    int StepVertical(int direction)
    {
        int column = selected % columns;
        int row = selected / columns;
        for (int tries = 0; tries < GridRows; tries++)
        {
            row = (row + direction + GridRows) % GridRows;
            int next = row * columns + column;
            if (next < items.Length) return next;
        }
        return selected;
    }

    // Left and right adjust a slider, or move along the selection's row on a grid page, wrapping at its ends.
    void StepSideways(int direction)
    {
        if (items[selected].IsSlider || columns == 1)
        {
            Nudge(direction * SliderStep);
            return;
        }
        int rowStart = selected / columns * columns;
        int rowLength = Mathf.Min(columns, items.Length - rowStart);
        selected = rowStart + (selected - rowStart + direction + rowLength) % rowLength;
    }

    // Where a cell sits in the rows block: grid columns are centred as a group, and each grid row is one row tall.
    Vector2 CellPosition(int index, float rowHeight)
    {
        int column = index % columns;
        float x = (column - (columns - 1) / 2f) * (CellWidth + GridGap);
        return new Vector2(columns > 1 ? x : 0f, -rowHeight * (index / columns + 0.5f));
    }

    float CellWidth => cards ? CardWidth : light ? LightRowWidth : table ? TableCellWidth : columns > 1 ? GridCellWidth : RowWidth;
    float PageRowHeight => cards ? CardHeight : light ? LightRowHeight : table ? TableRowHeight : RowHeight;

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
    // forward, so it reads by shape and position as well as colour. On the title page the logo fades up instead.
    void Animate(float elapsed)
    {
        float dt = Time.unscaledDeltaTime;
        if (light)
        {
            // The logo settles in from slightly large while it fades up.
            float rise = EaseOut(Mathf.Clamp01(elapsed / 0.9f));
            titleArt.color = new Color(1f, 1f, 1f, rise * Chrome);
            titleArt.rectTransform.localScale = Vector3.one * (1.04f - 0.04f * rise);
            titleText.alpha = Chrome;
            hintText.alpha = 0.55f * Chrome;
            figure.color = new Color(1f, 1f, 1f, rise);
        }
        else if (onPaper)
        {
            // Leaving the title from one of its pages fades that page's words like the title's own.
            titleText.alpha = Chrome;
            bodyText.alpha = Chrome;
            noteText.alpha = Chrome;
            hintText.alpha = 0.55f * Chrome;
        }
        float rowHeight = PageRowHeight;

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            float enter = EaseOut(Mathf.Clamp01((elapsed - 0.05f * i) / 0.25f));
            row.root.anchoredPosition = CellPosition(i, rowHeight) + new Vector2(0, -24f * (1f - enter));
            row.root.GetComponent<CanvasGroup>().alpha = enter * Chrome;

            float target = i == selected ? 1f : 0f;
            row.shownSelect = Mathf.MoveTowards(row.shownSelect, target, dt / 0.12f);
            float k = EaseOut(row.shownSelect);
            if (cards)
            {
                // The chosen card lifts a touch, its frame and heading turning red.
                row.root.anchoredPosition += new Vector2(0f, 14f * k);
                row.root.localScale = Vector3.one * (1f + 0.04f * k);
                row.frame.color = Color.Lerp(new Color(Ink.r, Ink.g, Ink.b, 0.22f), InkAccent, k);
                row.label.color = Color.Lerp(Ink, InkAccent, k);
                continue;
            }
            row.marker.localScale = Vector3.one * k;
            if (light)
            {
                // Ink turns red and the label steps right a touch as the marker arrives beside it.
                row.label.color = Color.Lerp(Ink, InkAccent, k);
                row.label.rectTransform.anchoredPosition = new Vector2(8f * k, 0);
                continue;
            }
            // A table row barely grows, so its columns stay in line with the rows around it.
            row.root.localScale = Vector3.one * (1f + (table ? 0.02f : 0.08f) * k);
            if (row.bar.type == Image.Type.Filled) row.bar.fillAmount = k;
            else row.bar.rectTransform.sizeDelta = new Vector2(CellWidth * k, row.bar.rectTransform.sizeDelta.y);
            row.label.color = onPaper ? Color.Lerp(Ink, InkAccent, k) : Color.Lerp(Idle, Color.white, k);
            // On the red selection bar a red fill would vanish, so it turns white.
            // On paper the bar is a pale wash, so the fill stays red.
            if (row.fillImage != null) row.fillImage.color = onPaper ? InkAccent : Color.Lerp(Accent, Color.white, k);

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

        float rowWidth = CellWidth;
        float rowHeight = PageRowHeight;
        for (int i = 0; i < items.Length; i++)
        {
            var row = new Row();
            row.root = GameUI.CreateRect("Item" + i, rowsRoot, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(rowWidth, rowHeight - 8f));
            row.root.pivot = new Vector2(0.5f, 0.5f);
            row.root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            if (cards)
            {
                BuildCard(row, items[i], rowWidth, rowHeight - 8f);
                rows.Add(row);
                continue;
            }

            if (light || onPaper)
            {
                row.bar = GameUI.CreateRect("Bar", row.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, rowHeight - 14f)).gameObject.AddComponent<Image>();
                row.bar.sprite = ProceduralSprites.White;
                row.bar.color = new Color(Accent.r, Accent.g, Accent.b, light ? 0f : 0.14f);
                row.bar.raycastTarget = false;
            }
            else
            {
                // Over the dimmed game the selection is a stroke of red ink, like the stage and boon cards; it paints
                // on from the left as the marker arrives.
                row.bar = GameUI.CreateInk(row.root, new Vector2(0.5f, 0.5f), new Vector2(30f, 0), new Vector2(rowWidth + 60f, rowHeight + 10f),
                    new Color(Accent.r, Accent.g, Accent.b, 0.38f));
            }

            // A small diamond to the left of the selected label.
            row.marker = GameUI.CreateRect("Marker", row.root, new Vector2(0.5f, 0.5f), new Vector2(-rowWidth / 2f + 46f, 0), new Vector2(20, 20));
            row.marker.localRotation = Quaternion.Euler(0, 0, 45f);
            Image marker = row.marker.gameObject.AddComponent<Image>();
            marker.color = light || onPaper ? InkAccent : Accent;
            marker.raycastTarget = false;

            bool slider = items[i].IsSlider;
            // Title-page labels line up on their left edge, like a classic JRPG menu, and so do a table's, so its
            // columns read down the page; the rest are centred.
            bool left = slider || light || table;
            float labelWidth = slider ? 300f : table ? rowWidth - 76f - DetailWidth - 3 * PipSpacing - 40f : rowWidth;
            row.label = GameUI.CreateText(row.root, ui.BodyFont, "Label", new Vector2(left ? 0f : 0.5f, 0.5f),
                slider || table ? new Vector2(76f, 0) : Vector2.zero, new Vector2(labelWidth, rowHeight), light ? 40 : table ? TableFontSize : 44,
                left ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            if (table) row.label.textWrappingMode = TextWrappingModes.NoWrap;
            row.label.text = items[i].label;
            row.label.characterSpacing = 6;
            ui.Style(row.label, TextStyle.Heading);
            // Ink on paper needs no outline or shadow.
            if (light || onPaper) row.label.fontSharedMaterial = row.label.font.material;
            // A button's marker sits just left of its label.
            if (light)
            {
                row.marker.anchoredPosition = new Vector2(-rowWidth / 2f - 30f, 0);
            }
            else if (table)
            {
                row.marker.anchoredPosition = new Vector2(-rowWidth / 2f + 44f, 0);
                BuildTableColumns(row, items[i], rowHeight);
            }
            else if (!slider)
            {
                row.label.ForceMeshUpdate(true);
                row.marker.anchoredPosition = new Vector2(row.label.textBounds.min.x - 34f, 0);
            }

            if (slider)
            {
                row.track = GameUI.CreateRect("Track", row.root, new Vector2(0f, 0.5f), new Vector2(330f, 0), new Vector2(280f, 16f));
                Image track = row.track.gameObject.AddComponent<Image>();
                track.color = new Color(0f, 0f, 0f, onPaper ? 0.15f : 0.45f);
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
                if (onPaper)
                {
                    row.value.fontSharedMaterial = row.value.font.material;
                    row.value.color = Ink;
                }
            }
            rows.Add(row);
        }
    }

    // A card: a pale panel in a thin ink frame, its heading in the title face over a short rule, and its text below.
    void BuildCard(Row row, Item item, float width, float height)
    {
        Color ink = onPaper ? Ink : Idle;
        row.frame = Stretch(GameUI.CreateRect("Frame", row.root, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        row.frame.sprite = ProceduralSprites.White;
        row.frame.raycastTarget = false;
        RectTransform inside = Stretch(GameUI.CreateRect("Panel", row.root, Vector2.zero, Vector2.zero, Vector2.zero));
        inside.offsetMin = new Vector2(3f, 3f);
        inside.offsetMax = new Vector2(-3f, -3f);
        Image panel = inside.gameObject.AddComponent<Image>();
        panel.sprite = ProceduralSprites.White;
        panel.color = onPaper ? Color.Lerp(Paper, Ink, 0.035f) : new Color(0f, 0f, 0f, 0.6f);
        panel.raycastTarget = false;

        row.label = GameUI.CreateText(row.root, ui.TitleFont, "Heading", new Vector2(0.5f, 1f), new Vector2(0f, -62f),
            new Vector2(width - 40f, 70f), 46, TextAlignmentOptions.Center);
        row.label.textWrappingMode = TextWrappingModes.NoWrap;
        row.label.text = item.label;
        row.label.characterSpacing = 4;
        ui.Style(row.label, TextStyle.Title);
        if (onPaper) row.label.fontSharedMaterial = row.label.font.material;

        Image rule = GameUI.CreateRect("Rule", row.root, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(180f, 2f)).gameObject.AddComponent<Image>();
        rule.sprite = ProceduralSprites.White;
        rule.color = new Color(ink.r, ink.g, ink.b, 0.3f);
        rule.raycastTarget = false;

        TextMeshProUGUI body = GameUI.CreateText(row.root, ui.BodyFont, "Body", new Vector2(0.5f, 1f), new Vector2(0f, -140f),
            new Vector2(width - 70f, height - 160f), 30, TextAlignmentOptions.TopLeft);
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.richText = true;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.text = item.card;
        body.color = ink;
        ui.Style(body, TextStyle.Body);
        if (onPaper) body.fontSharedMaterial = body.font.material;

        // Cards have no bar or marker; Animate never reaches for them on a card page.
        row.marker = GameUI.CreateRect("Marker", row.root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        row.marker.gameObject.SetActive(false);
    }

    // A table row's right-hand columns: the value right-aligned at the edge, and the rank pips (small diamonds like the
    // marker, lit for each rank held) in a column of their own left of it.
    void BuildTableColumns(Row row, Item item, float rowHeight)
    {
        Color ink = onPaper ? Ink : Idle;
        if (item.detail != null)
        {
            row.detail = GameUI.CreateText(row.root, ui.BodyFont, "Detail", new Vector2(1f, 0.5f), new Vector2(-24f, 0),
                new Vector2(DetailWidth, rowHeight), TableFontSize, TextAlignmentOptions.Right);
            row.detail.textWrappingMode = TextWrappingModes.NoWrap;
            row.detail.richText = true;
            row.detail.text = item.detail;
            row.detail.color = ink;
            ui.Style(row.detail, TextStyle.Heading);
            if (onPaper) row.detail.fontSharedMaterial = row.detail.font.material;
        }
        float pipsRight = -24f - DetailWidth - 28f;
        for (int p = 0; p < item.pipsMax; p++)
        {
            float x = pipsRight - (item.pipsMax - 1 - p) * PipSpacing - PipSize / 2f;
            RectTransform pip = GameUI.CreateRect("Pip", row.root, new Vector2(1f, 0.5f), new Vector2(x, 0), new Vector2(PipSize, PipSize));
            pip.pivot = new Vector2(0.5f, 0.5f);
            pip.localRotation = Quaternion.Euler(0, 0, 45f);
            Image image = pip.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            bool lit = p < item.pips;
            image.color = new Color(ink.r, ink.g, ink.b, lit ? 0.9f : 0.3f);
        }
    }

    void Build()
    {
        page = GameUI.CreateRect("Menu", transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        dim = page.gameObject.AddComponent<Image>();
        dim.raycastTarget = false;

        paper = Stretch(GameUI.CreateRect("Paper", page, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<Image>();
        paper.color = Paper;
        paper.raycastTarget = false;
        titleArt = GameUI.CreateRect("TitleLogo", page, new Vector2(0.5f, 0.5f), LogoCentre, new Vector2(1000f, 563f)).gameObject.AddComponent<Image>();
        titleArt.sprite = ui.TitleLogo;
        titleArt.preserveAspect = true;
        titleArt.raycastTarget = false;
        figure = GameUI.CreateRect("Figure", page, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        figure.rectTransform.pivot = new Vector2(0.5f, 0f);
        figure.raycastTarget = false;
        figureAnimator = figure.gameObject.AddComponent<SpriteAnimatorUI>();
        figure.gameObject.SetActive(false);

        // The title sits in a mask, so it can be cropped cleanly.
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
        noteText = GameUI.CreateText(page, ui.BodyFont, "Note", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 60f),
            34, TextAlignmentOptions.Center);
        // Placed under the rows by Show, by its top edge.
        noteText.rectTransform.pivot = new Vector2(0.5f, 1f);
        noteText.richText = true;

        paper.gameObject.SetActive(false);
        titleArt.gameObject.SetActive(false);
        page.gameObject.SetActive(false);
    }

    // Every second or so a note leaves the flute, then rises, sways and drifts right as it fades, so the title
    // screen's tune seems to come from him.
    void UpdateNotes()
    {
        bool playing = (light || onPaper) && figure.gameObject.activeSelf && notePixel > 0f;
        bool shown = (light || onPaper) && figure.gameObject.activeSelf;
        float now = Time.unscaledTime;
        if (playing && now >= nextNoteAt)
        {
            nextNoteAt = now + UnityEngine.Random.Range(0.8f, 1.3f);
            Image image = spareNotes.Count > 0 ? spareNotes[spareNotes.Count - 1] : null;
            if (image != null) spareNotes.RemoveAt(spareNotes.Count - 1);
            else
            {
                image = GameUI.CreateRect("Note", page, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
                image.raycastTarget = false;
            }
            image.sprite = ProceduralSprites.Note(nextNoteKind++);
            // Notes are drawn at twice the figure's pixel size, so they read at a glance.
            image.rectTransform.sizeDelta = image.sprite.rect.size * notePixel * 2f;
            image.gameObject.SetActive(true);
            liveNotes.Add((image, now, noteSource, UnityEngine.Random.Range(30f, 90f)));
        }

        for (int i = liveNotes.Count - 1; i >= 0; i--)
        {
            var (image, born, from, drift) = liveNotes[i];
            float t = (now - born) / NoteLife;
            // Notes already in the air drift on and fade when he stops playing; they go at once if he's gone.
            if (t >= 1f || !shown)
            {
                Release(image);
                liveNotes.RemoveAt(i);
                continue;
            }
            float sway = Mathf.Sin((now - born) * 3f) * 10f;
            image.rectTransform.anchoredPosition = from + new Vector2(drift * t + sway, 150f * EaseOut(t));
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin((now - born) * 2.4f) * 12f);
            float alpha = Mathf.Clamp01(t / 0.12f) * (1f - Mathf.Clamp01((t - 0.6f) / 0.4f));
            image.color = new Color(NoteColour.r, NoteColour.g, NoteColour.b, alpha);
        }
    }

    void Release(Image image)
    {
        image.gameObject.SetActive(false);
        spareNotes.Add(image);
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    // How much of the title page's logo, rows and hint still shows while it is leaving.
    float Chrome => leaving ? 1f - Mathf.Clamp01((Time.unscaledTime - leftAt) / 0.4f) : 1f;

    static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
}
