using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Hades-style "Boons of ..." screen shown when the ronin catches a soul, and the HUD medallions for his
// three ability slots and passives. Everything is built in code on the GameUI canvas; any sprite left empty
// falls back to a plain shape so the screen works before the art is wired up.
[RequireComponent(typeof(Canvas))]
public class BoonChoiceUI : MonoBehaviour
{
    const float InputDelay = 0.3f;
    const float CardSlide = 34f;
    static readonly Color CardColor = new Color(0.05f, 0.03f, 0.08f, 0.94f);
    static readonly Color MutedText = new Color(0.78f, 0.74f, 0.68f);
    static readonly string StatGreen = "#8CF55A";

    class Card
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image glow;
        public Image icon;
        public TextMeshProUGUI letter;
        public TextMeshProUGUI slot;
        public TextMeshProUGUI name;
        public TextMeshProUGUI rarity;
        public TextMeshProUGUI description;
        public TextMeshProUGUI stat;
        public TextMeshProUGUI note;
        public float slide;
    }

    class HudSlot
    {
        public Image ring;
        public Image icon;
        public TextMeshProUGUI letter;
        public Image cooldown;
    }

    [Header("Inscribed")]
    [Tooltip("One icon per boon, in BoonId order.")]
    [SerializeField] Sprite[] boonIcons;
    [Tooltip("9-sliced dark ink panel behind each boon card.")]
    [SerializeField] Sprite cardSprite;
    [Tooltip("9-sliced white border drawn around the selected card, tinted in the element's colour.")]
    [SerializeField] Sprite cardGlowSprite;
    [Tooltip("Screen pixels per art pixel for the card borders.")]
    [SerializeField] float cardPixelScale = 2f;
    [Tooltip("Default icons for the Kunai, Shuriken and Parry slots before they hold a boon.")]
    [SerializeField] Sprite[] abilityIcons = new Sprite[3];

    Ronin ronin;
    TMP_FontAsset font;
    bool built;

    GameObject panel;
    Image portrait;
    Image portraitGlow;
    SpriteAnimatorUI portraitAnimator;
    TextMeshProUGUI title;
    TextMeshProUGUI subtitle;
    Image divider;
    readonly Card[] cards = new Card[3];

    List<BoonOffer> offers;
    Action<int> chosen;
    Color elementColor;
    int selected;
    float shownAt;

    readonly HudSlot[] hudSlots = new HudSlot[3];
    RectTransform passiveRow;
    readonly List<Image> passiveIcons = new List<Image>();
    TextMeshProUGUI wardText;

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Bind(Ronin owner, TMP_FontAsset uiFont)
    {
        ronin = owner;
        font = uiFont;
        if (built) return;
        built = true;
        BuildHud();
        BuildPanel();
        panel.SetActive(false);
    }

    public void Show(Element element, List<BoonOffer> boonOffers, Sprite[] soulFrames, Action<int> onChosen)
    {
        offers = boonOffers;
        chosen = onChosen;
        ElementInfo info = Boons.Info(element);
        elementColor = info.Color;
        selected = 0;
        shownAt = Time.unscaledTime;

        title.text = $"Boons of {info.Name}";
        title.color = elementColor;
        subtitle.text = info.Subtitle;
        divider.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.7f);

        bool hasPortrait = soulFrames != null && soulFrames.Length > 0;
        portrait.sprite = hasPortrait ? soulFrames[0] : ProceduralSprites.Glow;
        portrait.color = hasPortrait ? Color.white : Color.Lerp(elementColor, Color.white, 0.5f);
        portraitAnimator.Play(hasPortrait ? soulFrames : null, 8f);
        portraitGlow.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.6f);

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].root.gameObject.SetActive(i < offers.Count);
            if (i < offers.Count) Fill(cards[i], offers[i]);
        }
        panel.SetActive(true);
    }

    void Fill(Card card, BoonOffer offer)
    {
        BoonDefinition boon = offer.Boon;
        Color rarityColor = Boons.RarityColor(offer.Rarity);
        SetIcon(card.icon, card.letter, boon, Boons.Info(boon.Element).Color);
        card.slot.text = boon.Slot.ToString().ToUpperInvariant();
        card.name.text = boon.Name;
        card.name.color = rarityColor;
        card.rarity.text = offer.IsUpgrade ? $"Upgrade  {offer.Rarity}" : offer.Rarity.ToString();
        card.rarity.color = rarityColor;
        card.description.text = boon.Description;

        string value = boon.Format(boon.ValueAt(offer.Rarity));
        if (offer.IsUpgrade) value += $"  <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>(was {boon.Format(boon.ValueAt(offer.Rarity - 1))})</color>";
        card.stat.text = $"<color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>- {boon.StatLabel}</color>   <color={StatGreen}>{value}</color>";
        card.note.text = offer.Replaces != null ? $"Replaces {offer.Replaces.Boon.Name}" : "";
        card.slide = 0f;
    }

    void SetIcon(Image icon, TextMeshProUGUI letter, BoonDefinition boon, Color color)
    {
        Sprite sprite = boonIcons != null && (int)boon.Id < boonIcons.Length ? boonIcons[(int)boon.Id] : null;
        icon.sprite = sprite != null ? sprite : ProceduralSprites.Disc;
        icon.color = sprite != null ? Color.white : color * 0.7f;
        letter.text = sprite != null ? "" : boon.Name.Substring(0, 1);
    }

    void Update()
    {
        if (ronin != null) UpdateHud();
        if (!IsOpen) return;

        float elapsed = Time.unscaledTime - shownAt;
        AnimatePanel(elapsed);
        if (elapsed < InputDelay) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) selected = (selected + offers.Count - 1) % offers.Count;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) selected = (selected + 1) % offers.Count;
            if (keyboard.digit1Key.wasPressedThisFrame) selected = 0;
            if (keyboard.digit2Key.wasPressedThisFrame && offers.Count > 1) selected = 1;
            if (keyboard.digit3Key.wasPressedThisFrame && offers.Count > 2) selected = 2;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Choose(selected);
                return;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        for (int i = 0; i < offers.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(cards[i].root, pointer, null)) continue;
            if (mouse.delta.ReadValue() != Vector2.zero) selected = i;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Choose(i);
                return;
            }
        }
    }

    void Choose(int index)
    {
        panel.SetActive(false);
        Action<int> callback = chosen;
        chosen = null;
        callback?.Invoke(index);
    }

    // Cards slide in one after another; the selected one glows and sits slightly forward.
    void AnimatePanel(float elapsed)
    {
        float dt = Time.unscaledDeltaTime;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        portraitGlow.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 2.5f));

        for (int i = 0; i < offers.Count; i++)
        {
            Card card = cards[i];
            float appear = Mathf.Clamp01((elapsed - i * 0.07f) / 0.25f);
            float ease = 1f - (1f - appear) * (1f - appear);
            card.slide = Mathf.MoveTowards(card.slide, i == selected ? 1f : 0f, dt * 8f);
            card.root.anchoredPosition = new Vector2(CardX + (1f - ease) * 260f + card.slide * CardSlide, card.root.anchoredPosition.y);
            card.group.alpha = ease;
            card.glow.color = new Color(elementColor.r, elementColor.g, elementColor.b, i == selected ? pulse : 0f);
        }
    }

    void UpdateHud()
    {
        hudSlots[(int)BoonSlot.Special].cooldown.fillAmount = 1f - ronin.ShurikenCharge;
        hudSlots[(int)BoonSlot.Parry].cooldown.fillAmount = 1f - ronin.ParryCharge;
        int wards = ronin.Loadout.WardCharges;
        wardText.text = wards > 0 ? $"Frost Ward x{wards}" : "";
    }

    // Called whenever the loadout changes: slot medallions take the boon's icon and its element's colour.
    public void RefreshHud(RoninLoadout loadout)
    {
        if (!built) return;
        for (int i = 0; i < hudSlots.Length; i++)
        {
            HudSlot slot = hudSlots[i];
            EquippedBoon held = loadout.InSlot((BoonSlot)i);
            if (held != null)
            {
                Color color = Boons.Info(held.Boon.Element).Color;
                SetIcon(slot.icon, slot.letter, held.Boon, color);
                slot.ring.color = color;
            }
            else
            {
                Sprite ability = abilityIcons != null && i < abilityIcons.Length ? abilityIcons[i] : null;
                slot.icon.sprite = ability != null ? ability : ProceduralSprites.Disc;
                slot.icon.color = ability != null ? Color.white : new Color(0.25f, 0.25f, 0.3f);
                slot.letter.text = ability != null ? "" : new[] { "K", "S", "P" }[i];
                slot.ring.color = new Color(0.55f, 0.55f, 0.6f);
            }
        }

        while (passiveIcons.Count < loadout.Passives.Count)
        {
            RectTransform rect = GameUI.CreateRect("Passive" + passiveIcons.Count, passiveRow, new Vector2(1f, 0.5f),
                new Vector2(-passiveIcons.Count * 52f, 0), new Vector2(46, 46));
            passiveIcons.Add(rect.gameObject.AddComponent<Image>());
            GameUI.CreateText(rect, font, "Letter", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), 26, TextAlignmentOptions.Center);
        }
        for (int i = 0; i < passiveIcons.Count; i++)
        {
            bool shown = i < loadout.Passives.Count;
            passiveIcons[i].gameObject.SetActive(shown);
            if (!shown) continue;
            BoonDefinition boon = loadout.Passives[i].Boon;
            SetIcon(passiveIcons[i], passiveIcons[i].GetComponentInChildren<TextMeshProUGUI>(), boon, Boons.Info(boon.Element).Color);
        }
    }

    const float CardX = 170f;

    void BuildPanel()
    {
        RectTransform root = GameUI.CreateRect("BoonChoice", transform, Vector2.zero, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.04f, 0.72f);
        panel = root.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);
        portraitGlow = CreateImage("PortraitGlow", root, center, new Vector2(-690, 40), new Vector2(640, 640), ProceduralSprites.Glow);
        portrait = CreateImage("Portrait", root, center, new Vector2(-690, 40), new Vector2(360, 360), null);
        portrait.preserveAspect = true;
        portraitAnimator = portrait.gameObject.AddComponent<SpriteAnimatorUI>();

        title = GameUI.CreateText(root, font, "Title", center, new Vector2(CardX, 425), new Vector2(1300, 90), 66, TextAlignmentOptions.Center);
        title.characterSpacing = 38f;
        title.fontStyle = FontStyles.UpperCase;
        subtitle = GameUI.CreateText(root, font, "Subtitle", center, new Vector2(CardX, 362), new Vector2(1300, 50), 28, TextAlignmentOptions.Center);
        subtitle.fontStyle = FontStyles.Italic;
        subtitle.color = MutedText;
        divider = CreateImage("Divider", root, center, new Vector2(CardX, 326), new Vector2(760, 3), null);

        float[] rows = { 170f, -40f, -250f };
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = BuildCard(root, new Vector2(CardX, rows[i]), i);
        }

        RectTransform footer = GameUI.CreateRect("Footer", root, new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(0, 76));
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        TextMeshProUGUI quick = GameUI.CreateText(footer, font, "QuickPick", new Vector2(0f, 0.5f), new Vector2(40, 0), new Vector2(700, 60), 30, TextAlignmentOptions.Left);
        quick.rectTransform.pivot = new Vector2(0f, 0.5f);
        quick.text = "1  2  3   Quick pick";
        quick.color = MutedText;
        TextMeshProUGUI hint = GameUI.CreateText(footer, font, "Hint", new Vector2(1f, 0.5f), new Vector2(-40, 0), new Vector2(900, 60), 30, TextAlignmentOptions.Right);
        hint.text = $"<color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>Up / Down</color>  Select        <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>Enter</color>  Choose";
    }

    Card BuildCard(RectTransform parent, Vector2 position, int index)
    {
        var card = new Card();
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 size = new Vector2(1180, 186);
        card.root = GameUI.CreateRect("Card" + index, parent, center, position, size);
        card.group = card.root.gameObject.AddComponent<CanvasGroup>();

        card.glow = CreateImage("Glow", card.root, center, Vector2.zero, size + new Vector2(22, 22), cardGlowSprite);
        Image background = CreateImage("Panel", card.root, center, Vector2.zero, size, cardSprite);
        if (cardSprite == null) background.color = CardColor;

        // Round medallion on the left, like the god boon icons in Hades.
        Image frame = CreateImage("IconFrame", card.root, center, new Vector2(-500, 8), new Vector2(150, 150), ProceduralSprites.Disc);
        frame.color = new Color(0.12f, 0.09f, 0.16f);
        card.icon = CreateImage("Icon", frame.rectTransform, center, Vector2.zero, new Vector2(128, 128), null);
        card.icon.preserveAspect = true;
        card.letter = GameUI.CreateText(frame.rectTransform, font, "Letter", center, Vector2.zero, new Vector2(128, 128), 64, TextAlignmentOptions.Center);
        card.slot = GameUI.CreateText(card.root, font, "Slot", center, new Vector2(-500, -76), new Vector2(170, 30), 20, TextAlignmentOptions.Center);
        card.slot.color = MutedText;
        card.slot.characterSpacing = 8f;

        card.name = GameUI.CreateText(card.root, font, "Name", center, new Vector2(-90, 50), new Vector2(640, 56), 44, TextAlignmentOptions.Left);
        card.name.fontStyle = FontStyles.SmallCaps;
        card.rarity = GameUI.CreateText(card.root, font, "Rarity", center, new Vector2(400, 50), new Vector2(300, 56), 34, TextAlignmentOptions.Right);
        card.rarity.fontStyle = FontStyles.SmallCaps;
        card.description = GameUI.CreateText(card.root, font, "Description", center, new Vector2(70, -2), new Vector2(960, 46), 28, TextAlignmentOptions.Left);
        card.description.color = new Color(0.9f, 0.88f, 0.84f);
        card.stat = GameUI.CreateText(card.root, font, "Stat", center, new Vector2(70, -50), new Vector2(960, 40), 28, TextAlignmentOptions.Left);
        card.note = GameUI.CreateText(card.root, font, "Note", center, new Vector2(330, -50), new Vector2(440, 36), 22, TextAlignmentOptions.Right);
        card.note.color = new Color(1f, 0.75f, 0.55f, 0.85f);
        card.note.fontStyle = FontStyles.Italic;
        return card;
    }

    // Bottom-right: the three ability slots (with cooldown sweeps) and a row of passive icons above them.
    void BuildHud()
    {
        Vector2 corner = new Vector2(1f, 0f);
        string[] keys = { "Space", "E", "Shift" };
        for (int i = 0; i < hudSlots.Length; i++)
        {
            Vector2 position = new Vector2(-250 + i * 100, 64);
            var slot = new HudSlot();
            slot.ring = CreateImage("Slot" + i, transform, corner, position, new Vector2(88, 88), ProceduralSprites.Disc);
            Image inner = CreateImage("Inner", slot.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78), ProceduralSprites.Disc);
            inner.color = new Color(0.06f, 0.04f, 0.09f);
            slot.icon = CreateImage("Icon", slot.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70), null);
            slot.icon.preserveAspect = true;
            slot.letter = GameUI.CreateText(slot.ring.rectTransform, font, "Letter", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70), 36, TextAlignmentOptions.Center);
            slot.cooldown = CreateImage("Cooldown", slot.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88, 88), ProceduralSprites.Disc);
            slot.cooldown.color = new Color(0f, 0f, 0f, 0.65f);
            slot.cooldown.type = Image.Type.Filled;
            slot.cooldown.fillMethod = Image.FillMethod.Radial360;
            slot.cooldown.fillOrigin = (int)Image.Origin360.Top;
            slot.cooldown.fillClockwise = false;
            slot.cooldown.fillAmount = 0f;
            TextMeshProUGUI key = GameUI.CreateText(slot.ring.rectTransform, font, "Key", new Vector2(0.5f, 0f), new Vector2(0, -22), new Vector2(120, 30), 20, TextAlignmentOptions.Center);
            key.text = keys[i];
            key.color = MutedText;
            hudSlots[i] = slot;
        }

        passiveRow = GameUI.CreateRect("Passives", transform, corner, new Vector2(-30, 150), new Vector2(400, 50));
        passiveRow.pivot = new Vector2(1f, 0.5f);
        wardText = GameUI.CreateText(transform, font, "Ward", corner, new Vector2(-30, 196), new Vector2(400, 40), 26, TextAlignmentOptions.Right);
        wardText.rectTransform.pivot = new Vector2(1f, 0.5f);
        wardText.color = Boons.Info(Element.Water).Color;
    }

    Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite)
    {
        Image image = GameUI.CreateRect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        if (sprite != null && sprite.border != Vector4.zero)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = GetComponent<Canvas>().referencePixelsPerUnit / sprite.pixelsPerUnit / cardPixelScale;
        }
        return image;
    }
}
