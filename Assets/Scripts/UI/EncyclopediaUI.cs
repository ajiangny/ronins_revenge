using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Every foe the memorial stones' encyclopedia can list, in the order it lists them: the dead army's kinds and the
// kite scout, then the warlords with Kuon beside Shirogane. Ids are the SamuraiKind and BossKind names (the save keeps
// them), so those enum values are never renamed.
public static class Foes
{
    public const string Scout = "Scout";
    public const string Kuon = "Kuon";

    public static string Of(SamuraiKind kind) => kind.ToString();
    public static string Of(BossKind boss) => boss.ToString();

    public struct Foe
    {
        public string id, name, epithet, line;
        public bool warlord;
        public Color accent;
        // The warlord it is (None for the army, the scout and Kuon), for the boss tallies.
        public BossKind boss;
    }

    // Names are plain ASCII: the title face may lack the macrons.
    public static readonly Foe[] All =
    {
        Army(SamuraiKind.Ashigaru, "ASHIGARU", "The Levied Spear",
            "The bulk of the vanguard, still marching in the rotted straw hats they died in. One arrow at a time, as ordered."),
        Army(SamuraiKind.Archer, "ARCHER", "The Court's Bow",
            "The court's bowmen, grey skin pulled tight to the skull. Three arrows at once, in a fan, as they were drilled."),
        Army(SamuraiKind.Taisho, "TAISHO", "The Revenant General",
            "Akagane's officers, a cracked mask over bare bone. They still hurl the Emperor's cursed spears, which no blade can cut."),
        Army(SamuraiKind.Shieldbearer, "SHIELDBEARER", "The Door of the Hall",
            "They tore the shrine hall's doors from their hinges to shield the line, and never put them down."),
        Army(SamuraiKind.Yurei, "YUREI", "The Bodiless",
            "Souls whose bodies are gone entirely, so the coffin doesn't bother giving them one. Their arrows pass through cover."),
        Army(SamuraiKind.Bellringer, "BELLRINGER", "The Shrine Watch",
            "Akagane's watchmen, dead at their posts with their jaws bound shut. Every toll drives the army to fire faster."),
        new Foe { id = Scout, name = "KITE SCOUT", epithet = "The Hanged Kite", accent = MenuUI.Accent,
            line = "A shrouded corpse lashed to a torn signal kite, drifting across the sky like a hanged man. Souls slip out of the ones that fall." },
        Warlord(BossKind.Benibana, "BENIBANA", "The Crimson Smoke", Benibana.Crimson,
            "The court's sword-dancer and its secret assassin. She kills Oni for a promise the Emperor has never kept."),
        Warlord(BossKind.Utsusemi, "UTSUSEMI", "The Hollow Pilgrim", Utsusemi.Steel,
            "The monk who once sheltered Oni and made him tea, wound into clockwork under his own hat. It winds the clock that keeps the hour still."),
        Warlord(BossKind.Shirogane, "SHIROGANE", "The Forsaken Blade", Shirogane.Spirit,
            "His sworn partner, who cut him free of the coffin and fell to the Emperor's hammer. She would not kneel: her bell has never rung."),
        new Foe { id = Kuon, name = "KUON", epithet = "The Shrine Guardian", warlord = true, accent = GameManager.KuonBarColour,
            line = "The wolf of blue spirit flame that guarded her family's shrine. The Emperor bound it to her as a weapon, and gave her a friend." },
        Warlord(BossKind.Kagetsune, "KAGETSUNE", "The Thousand-Soul Throne", Kagetsune.Crimson,
            "The Emperor, deathless inside an armour of a thousand sealed souls. Every bell on it is a soul that knelt to him."),
    };

    static Foe Army(SamuraiKind kind, string name, string epithet, string line) =>
        new Foe { id = Of(kind), name = name, epithet = epithet, line = line, accent = MenuUI.Accent };

    static Foe Warlord(BossKind boss, string name, string epithet, Color accent, string line) =>
        new Foe { id = Of(boss), name = name, epithet = epithet, line = line, warlord = true, accent = accent, boss = boss };
}

// The memorial stones' encyclopedia (Hades' Codex): a strip of tiles along the bottom, one per foe, and the selected
// one large above it, its figure looping beside its name, epithet, a line of its story and Oni's tally against it.
// Foes he hasn't met are a dark silhouette and "???". Built in code over the dimmed temple; keyboard, mouse and
// gamepad drive it on unscaled time, like MenuUI.
public class EncyclopediaUI : MonoBehaviour
{
    public struct Entry
    {
        public string name, epithet, line, stats;
        public Sprite[] frames;
        public float fps;
        public Color tint, accent;
        public bool met, warlord;
    }

    // Keys can't be read for this long after the page opens, so the press that opened it doesn't also act on it.
    const float InputDelay = 0.2f;
    // The tile strip: small enough that all twelve, and the gap between the army and the warlords, fit a 4:3 screen.
    const float TileSize = 116f;
    const float TileGap = 12f;
    const float GroupGap = 48f;
    const float StripY = -330f;
    // A tile's figure may fill the whole tile: the art's own transparent margins keep it off the frame.
    const float TileFigure = TileSize;
    // The selected foe's figure stands in this box, its feet on the box's bottom edge.
    const float BoxSize = 420f;
    static readonly Vector2 BoxFeet = new Vector2(-560f, -150f);
    // The left edge and width of the text beside it.
    const float TextLeft = -240f;
    const float TextWidth = 900f;
    // A foe with no art at all is a soft disc this big.
    const float DiscSize = 160f;
    const float TileDisc = 64f;
    static readonly Color Silhouette = new Color(0.04f, 0.03f, 0.06f, 0.92f);
    static readonly Color TileFrame = new Color(0.86f, 0.83f, 0.78f, 0.22f);

    class Tile
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image frame;
        public Image figure;
        public SpriteAnimatorUI animator;
        public float shownSelect;
    }

    GameUI ui;
    RectTransform page;
    TextMeshProUGUI titleText;
    TextMeshProUGUI countText;
    RectTransform rule;
    RectTransform detail;
    CanvasGroup detailGroup;
    Image glow;
    Image shadow;
    Image figure;
    SpriteAnimatorUI figureAnimator;
    Image nameInk;
    TextMeshProUGUI nameText;
    TextMeshProUGUI epithetText;
    RectTransform smallRule;
    TextMeshProUGUI lineText;
    TextMeshProUGUI statsText;
    TextMeshProUGUI armyLabel;
    TextMeshProUGUI warlordLabel;
    TextMeshProUGUI backText;
    TextMeshProUGUI hintText;
    RectTransform tilesRoot;
    readonly List<Tile> tiles = new List<Tile>();
    Entry[] entries = Array.Empty<Entry>();
    Action<int> back;
    int selected;
    int heardSelection;
    int shownEntry = -1;
    float shownAt;
    float selectedAt;
    bool backHovered;

    public bool IsOpen => page != null && page.gameObject.activeSelf;

    public void Bind(GameUI gameUI)
    {
        ui = gameUI;
        Build();
    }

    // Opens the encyclopedia on `startIndex`; Esc or BACK hides it and hands `onBack` the entry last selected, so
    // the next visit opens where this one left off.
    public void Show(Entry[] pageEntries, int startIndex, Action<int> onBack)
    {
        entries = pageEntries ?? Array.Empty<Entry>();
        back = onBack;
        selected = heardSelection = Mathf.Clamp(startIndex, 0, Mathf.Max(0, entries.Length - 1));
        shownEntry = -1;
        shownAt = Time.unscaledTime;

        int met = Array.FindAll(entries, e => e.met).Length;
        string muted = MenuUI.MutedHex(false);
        countText.text = $"<color=#{muted}>Names worn into the old stones:</color>  {met}<color=#{muted}> of {entries.Length} still read.</color>";
        hintText.text = Hint("← →", "Select") + Hint("↑ ↓", "Army / Warlords")
            // In a browser Esc can be taken by fullscreen, so the web build names Backspace too.
            + Hint(Application.platform == RuntimePlatform.WebGLPlayer ? "Esc / Bksp" : "Esc", "Back");

        page.SetAsLastSibling();
        page.gameObject.SetActive(true);
        BuildTiles();
        Animate(0f);
    }

    public void Hide()
    {
        if (page != null) page.gameObject.SetActive(false);
        if (figureAnimator != null) figureAnimator.Play(null, 0f);
    }

    static string Hint(string key, string action) => $"{MenuUI.KeyCap(key, false)}  {action}       ";

    void Update()
    {
        if (!IsOpen || entries.Length == 0) return;
        if (selected != heardSelection) Sfx.Play(Sfx.UiMove);
        heardSelection = selected;
        if (selected != shownEntry) ShowDetail(selected);

        float elapsed = Time.unscaledTime - shownAt;
        Animate(elapsed);
        if (elapsed < InputDelay) return;

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        if (MenuUI.Pressed(keyboard?.leftArrowKey, keyboard?.aKey, gamepad?.dpad.left, gamepad?.leftStick.left)) Step(-1);
        if (MenuUI.Pressed(keyboard?.rightArrowKey, keyboard?.dKey, gamepad?.dpad.right, gamepad?.leftStick.right)) Step(1);
        if (MenuUI.Pressed(keyboard?.upArrowKey, keyboard?.wKey, gamepad?.dpad.up, gamepad?.leftStick.up,
                keyboard?.downArrowKey, keyboard?.sKey, gamepad?.dpad.down, gamepad?.leftStick.down))
            SwitchGroup();
        if (MenuUI.Pressed(keyboard?.escapeKey, keyboard?.backspaceKey, gamepad?.buttonEast, gamepad?.startButton))
        {
            Leave();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        bool moved = mouse.delta.ReadValue() != Vector2.zero;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(tiles[i].root, pointer, null)) continue;
            if (moved || mouse.leftButton.wasPressedThisFrame) selected = i;
        }
        backHovered = RectTransformUtility.RectangleContainsScreenPoint(backText.rectTransform, pointer, null);
        if (backHovered && mouse.leftButton.wasPressedThisFrame)
        {
            Leave();
            return;
        }
        float wheel = mouse.scroll.ReadValue().y;
        if (wheel > 0f) Step(-1);
        else if (wheel < 0f) Step(1);
    }

    void Leave()
    {
        Sfx.Play(Sfx.UiConfirm);
        Hide();
        back?.Invoke(selected);
    }

    void Step(int direction) => selected = (selected + direction + entries.Length) % entries.Length;

    // Up and down cross between the army and the warlords, keeping the place in the row where they can.
    void SwitchGroup()
    {
        int firstWarlord = FirstWarlord;
        int armyCount = firstWarlord, warlordCount = entries.Length - firstWarlord;
        if (armyCount == 0 || warlordCount == 0) return;
        selected = selected < firstWarlord
            ? firstWarlord + Mathf.Min(selected, warlordCount - 1)
            : Mathf.Min(selected - firstWarlord, armyCount - 1);
    }

    int FirstWarlord
    {
        get
        {
            int i = Array.FindIndex(entries, e => e.warlord);
            return i < 0 ? entries.Length : i;
        }
    }

    // The big figure and the text beside it, for one entry. Unmet foes give away only their shape.
    void ShowDetail(int index)
    {
        shownEntry = index;
        selectedAt = Time.unscaledTime;
        Entry entry = entries[index];
        string muted = MenuUI.MutedHex(false);

        PlaceFigure(figure, figureAnimator, entry, BoxSize, DiscSize, true);
        glow.gameObject.SetActive(true);
        // A met foe glows in its own colour; an unmet one has only a faint pale haze, so its silhouette still reads.
        glow.color = entry.met ? new Color(entry.accent.r, entry.accent.g, entry.accent.b, 0.25f) : new Color(1f, 1f, 1f, 0.07f);
        nameInk.color = new Color(entry.accent.r, entry.accent.g, entry.accent.b, entry.met ? 0.38f : 0.12f);

        nameText.text = entry.met ? entry.name : $"<color=#{muted}>???</color>";
        epithetText.text = entry.met ? entry.epithet : "";
        lineText.text = entry.met ? entry.line : $"<color=#{muted}>The name on this stone is worn smooth. Meet it in the field to read it again.</color>";
        statsText.text = entry.met ? entry.stats : "";
        smallRule.gameObject.SetActive(entry.met);
    }

    // Stands a figure in a box of `box` units, its feet on the box's bottom: whole-number scales when the art fits
    // at least once, and halves, thirds and so on when it doesn't, so pixels stay even. With no frames it's a soft
    // disc in the entry's colour.
    static void PlaceFigure(Image image, SpriteAnimatorUI animator, Entry entry, float box, float disc, bool animate)
    {
        bool art = entry.frames != null && entry.frames.Length > 0 && entry.frames[0] != null;
        if (!art)
        {
            animator.Play(null, 0f);
            image.sprite = ProceduralSprites.SoftGlow;
            image.rectTransform.sizeDelta = new Vector2(disc, disc);
            image.color = entry.met ? new Color(entry.accent.r, entry.accent.g, entry.accent.b, 0.85f) : Silhouette;
            return;
        }
        Sprite first = entry.frames[0];
        Vector2 size = first.rect.size;
        float scale = Mathf.Min(box / size.x, box / size.y);
        scale = scale >= 1f ? Mathf.Floor(scale) : 1f / Mathf.Ceil(1f / scale);
        image.sprite = first;
        image.rectTransform.sizeDelta = size * scale;
        if (entry.met)
        {
            image.color = entry.tint;
            animator.Play(animate ? entry.frames : null, entry.fps);
        }
        else
        {
            animator.Play(null, 0f);
            image.color = Silhouette;
        }
    }

    // Tiles slide up into the strip one after another; the selected one lifts, grows a touch and its frame turns to
    // the foe's colour. The detail fades in on each change of selection while the ink under the name paints on.
    void Animate(float elapsed)
    {
        float dt = Time.unscaledDeltaTime;
        GameUI.Reveal(titleText, elapsed, 440f, 1f);
        countText.alpha = GameUI.EaseOut(Mathf.Clamp01((elapsed - 0.15f) / 0.4f));
        GameUI.AnimateRule(rule, 1100f, GameUI.EaseOut(Mathf.Clamp01(elapsed / 0.5f)));

        float since = Time.unscaledTime - selectedAt;
        detailGroup.alpha = GameUI.EaseOut(Mathf.Clamp01(since / 0.2f));
        nameInk.fillAmount = GameUI.EaseOut(Mathf.Clamp01(since / 0.25f));
        GameUI.AnimateRule(smallRule, 240f, GameUI.EaseOut(Mathf.Clamp01((since - 0.05f) / 0.3f)));
        // The figure breathes up a little as it appears.
        figure.rectTransform.anchoredPosition = BoxFeet + new Vector2(0f, -12f * (1f - detailGroup.alpha));

        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            float enter = GameUI.EaseOut(Mathf.Clamp01((elapsed - 0.04f * i) / 0.25f));
            float target = i == selected ? 1f : 0f;
            tile.shownSelect = Mathf.MoveTowards(tile.shownSelect, target, dt / 0.12f);
            float k = GameUI.EaseOut(tile.shownSelect);
            tile.root.anchoredPosition = TilePosition(i) + new Vector2(0f, -24f * (1f - enter) + 10f * k);
            tile.root.localScale = Vector3.one * (1f + 0.06f * k);
            tile.group.alpha = enter;
            Color accent = entries[i].met ? entries[i].accent : MenuUI.Accent;
            tile.frame.color = Color.Lerp(TileFrame, accent, k);
        }
        backText.color = backHovered ? Color.white : MenuUI.Idle;
    }

    // Where tile i sits in the strip: the army's then the warlords', a wider gap between the two groups.
    Vector2 TilePosition(int i)
    {
        int firstWarlord = FirstWarlord;
        bool split = firstWarlord > 0 && firstWarlord < entries.Length;
        float total = entries.Length * TileSize + (entries.Length - 1) * TileGap + (split ? GroupGap : 0f);
        float x = -total / 2f + TileSize / 2f + i * (TileSize + TileGap) + (split && i >= firstWarlord ? GroupGap : 0f);
        return new Vector2(x, StripY);
    }

    void BuildTiles()
    {
        foreach (Tile tile in tiles) Destroy(tile.root.gameObject);
        tiles.Clear();
        for (int i = 0; i < entries.Length; i++)
        {
            var tile = new Tile();
            tile.root = GameUI.CreateRect("Tile" + i, tilesRoot, new Vector2(0.5f, 0.5f), TilePosition(i), new Vector2(TileSize, TileSize));
            tile.group = tile.root.gameObject.AddComponent<CanvasGroup>();
            tile.group.blocksRaycasts = false;
            // A thin frame around a dark panel, like MenuUI's cards.
            tile.frame = Fill(GameUI.CreateRect("Frame", tile.root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), ProceduralSprites.White);
            Image panel = Fill(GameUI.CreateRect("Panel", tile.root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), ProceduralSprites.White);
            panel.rectTransform.offsetMin = new Vector2(3f, 3f);
            panel.rectTransform.offsetMax = new Vector2(-3f, -3f);
            panel.color = new Color(0f, 0f, 0f, 0.6f);
            // A pale haze behind each figure, so even a silhouette reads on the dark panel.
            Image haze = GameUI.CreateRect("Haze", tile.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileSize - 10f, TileSize - 10f))
                .gameObject.AddComponent<Image>();
            haze.sprite = ProceduralSprites.SoftGlow;
            haze.raycastTarget = false;
            Color accent = entries[i].met ? entries[i].accent : Color.white;
            haze.color = new Color(accent.r, accent.g, accent.b, entries[i].met ? 0.18f : 0.07f);

            // Big art can reach past the frame; the tile clips it.
            tile.root.gameObject.AddComponent<RectMask2D>();
            tile.figure = GameUI.CreateRect("Figure", tile.root, new Vector2(0.5f, 0f), new Vector2(0f, 3f), Vector2.zero)
                .gameObject.AddComponent<Image>();
            tile.figure.raycastTarget = false;
            tile.animator = tile.figure.gameObject.AddComponent<SpriteAnimatorUI>();
            PlaceFigure(tile.figure, tile.animator, entries[i], TileFigure, TileDisc, true);
            // A disc floats in the middle of the tile rather than standing on its floor.
            if (tile.figure.sprite == ProceduralSprites.SoftGlow) tile.figure.rectTransform.anchoredPosition = new Vector2(0f, (TileSize - TileDisc) / 2f);
            tiles.Add(tile);
        }

        // Each group's label sits over the middle of its tiles.
        int firstWarlord = FirstWarlord;
        armyLabel.gameObject.SetActive(firstWarlord > 0);
        warlordLabel.gameObject.SetActive(firstWarlord < entries.Length);
        if (firstWarlord > 0)
            armyLabel.rectTransform.anchoredPosition = new Vector2((TilePosition(0).x + TilePosition(firstWarlord - 1).x) / 2f, StripY + 92f);
        if (firstWarlord < entries.Length)
            warlordLabel.rectTransform.anchoredPosition = new Vector2((TilePosition(firstWarlord).x + TilePosition(entries.Length - 1).x) / 2f, StripY + 92f);
    }

    static Image Fill(RectTransform rect, Sprite sprite)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    void Build()
    {
        page = GameUI.CreateRect("Encyclopedia", transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        Image dim = page.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.01f, 0.04f, 0.8f);
        dim.raycastTarget = false;
        string muted = MenuUI.MutedHex(false);

        titleText = GameUI.CreateText(page, ui.TitleFont, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 440f), new Vector2(1600f, 140f),
            96, TextAlignmentOptions.Center);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.text = "ENCYCLOPEDIA";
        titleText.color = Color.white;
        ui.Style(titleText, TextStyle.Title);
        countText = GameUI.CreateText(page, ui.BodyFont, "Count", new Vector2(0.5f, 0.5f), new Vector2(0f, 372f), new Vector2(1400f, 50f),
            30, TextAlignmentOptions.Center);
        countText.richText = true;
        ui.Style(countText, TextStyle.Body);
        rule = GameUI.CreateRule(page, new Vector2(0.5f, 0.5f), new Vector2(0f, 340f), new Color(1f, 1f, 1f, 0.35f));

        // Everything about the selected foe, so it can fade in as one when the selection changes.
        detail = GameUI.CreateRect("Detail", page, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        detailGroup = detail.gameObject.AddComponent<CanvasGroup>();
        detailGroup.blocksRaycasts = false;
        glow = GameUI.CreateRect("Glow", detail, new Vector2(0.5f, 0.5f), BoxFeet + new Vector2(0f, BoxSize * 0.45f), new Vector2(BoxSize * 1.2f, BoxSize * 1.2f))
            .gameObject.AddComponent<Image>();
        glow.sprite = ProceduralSprites.SoftGlow;
        glow.raycastTarget = false;
        shadow = GameUI.CreateRect("Shadow", detail, new Vector2(0.5f, 0.5f), BoxFeet, new Vector2(260f, 44f)).gameObject.AddComponent<Image>();
        shadow.sprite = ProceduralSprites.Shadow;
        shadow.color = new Color(0f, 0f, 0f, 0.6f);
        shadow.raycastTarget = false;
        figure = GameUI.CreateRect("Figure", detail, new Vector2(0.5f, 0.5f), BoxFeet, Vector2.zero).gameObject.AddComponent<Image>();
        figure.rectTransform.pivot = new Vector2(0.5f, 0f);
        figure.raycastTarget = false;
        figureAnimator = figure.gameObject.AddComponent<SpriteAnimatorUI>();

        nameInk = GameUI.CreateInk(detail, new Vector2(0.5f, 0.5f), new Vector2(TextLeft + 300f, 250f), new Vector2(720f, 110f), MenuUI.Accent);
        nameText = LeftText("Name", ui.TitleFont, 72, new Vector2(TextLeft, 250f), 90f, TextStyle.Title);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.characterSpacing = 4;
        epithetText = LeftText("Epithet", ui.BodyFont, 34, new Vector2(TextLeft, 185f), 50f, TextStyle.Body);
        epithetText.color = GameUI.MutedColour;
        smallRule = GameUI.CreateRule(detail, new Vector2(0.5f, 0.5f), new Vector2(TextLeft + 120f, 150f), new Color(1f, 1f, 1f, 0.35f));
        lineText = LeftText("Line", ui.BodyFont, 36, new Vector2(TextLeft, 120f), 240f, TextStyle.Body);
        // The story line hangs from its top edge, so a longer one grows down towards the tally.
        lineText.rectTransform.pivot = new Vector2(0f, 1f);
        lineText.alignment = TextAlignmentOptions.TopLeft;
        statsText = LeftText("Stats", ui.BodyFont, 36, new Vector2(TextLeft, -150f), 50f, TextStyle.Heading);
        statsText.textWrappingMode = TextWrappingModes.NoWrap;
        statsText.characterSpacing = 3;

        tilesRoot = GameUI.CreateRect("Tiles", page, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        armyLabel = GroupLabel("THE DEAD ARMY");
        warlordLabel = GroupLabel("THE WARLORDS");

        backText = GameUI.CreateText(page, ui.BodyFont, "Back", new Vector2(0f, 0f), new Vector2(110f, 50f), new Vector2(160f, 60f),
            32, TextAlignmentOptions.Center);
        backText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        backText.text = "BACK";
        backText.characterSpacing = 6;
        ui.Style(backText, TextStyle.Heading);
        hintText = GameUI.CreateText(page, ui.BodyFont, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1400f, 50f),
            28, TextAlignmentOptions.Bottom);
        hintText.richText = true;
        hintText.color = GameUI.MutedColour;
        ui.Style(hintText, TextStyle.Hint);

        page.gameObject.SetActive(false);
    }

    TextMeshProUGUI LeftText(string name, TMP_FontAsset font, float size, Vector2 position, float height, TextStyle style)
    {
        TextMeshProUGUI text = GameUI.CreateText(detail, font, name, new Vector2(0.5f, 0.5f), position, new Vector2(TextWidth, height),
            size, TextAlignmentOptions.Left);
        text.rectTransform.pivot = new Vector2(0f, 0.5f);
        text.richText = true;
        text.color = Color.white;
        ui.Style(text, style);
        return text;
    }

    TextMeshProUGUI GroupLabel(string label)
    {
        TextMeshProUGUI text = GameUI.CreateText(page, ui.BodyFont, label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 40f),
            28, TextAlignmentOptions.Center);
        text.text = label;
        text.characterSpacing = 6;
        text.color = GameUI.MutedColour;
        ui.Style(text, TextStyle.Heading);
        return text;
    }
}
