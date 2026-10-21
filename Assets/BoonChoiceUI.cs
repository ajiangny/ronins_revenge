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

    const float PortraitPixelScale = 6f;

    [Header("Inscribed")]
    [Tooltip("Full-body art of the ronin on the left of the screen, like Zagreus in Hades. Empty shows the soul instead.")]
    [SerializeField] Sprite roninPortrait;
    [Tooltip("Where the caught soul floats over the ronin's open palm, in portrait art pixels from its top-left corner.")]
    [SerializeField] Vector2 portraitSoulPixel = new Vector2(84, -2);
    [Tooltip("Size of the soul in his hand, relative to the portrait's pixels.")]
    [SerializeField] float heldSoulScale = 0.67f;
    [Tooltip("One icon per boon, in BoonId order.")]
    [SerializeField] Sprite[] boonIcons;
    [Tooltip("9-sliced dark ink panel behind each boon card.")]
    [SerializeField] Sprite cardSprite;
    [Tooltip("9-sliced white border drawn around the selected card, tinted in the element's colour.")]
    [SerializeField] Sprite cardGlowSprite;
    [Tooltip("Screen pixels per art pixel for the card borders.")]
    [SerializeField] float cardPixelScale = 2f;
    [Tooltip("Default icons for the Attack (sword), Shuriken and Parry slots before they hold a boon.")]
    [SerializeField] Sprite[] abilityIcons = new Sprite[3];

    Ronin ronin;
    TMP_FontAsset font;
    bool built;

    GameObject panel;
    Image portrait;
    Vector2 portraitHome;
    Image portraitGlow;
    SpriteAnimatorUI portraitAnimator;
    Image heldSoul;
    Image heldSoulGlow;
    SpriteAnimatorUI heldSoulAnimator;
    TextMeshProUGUI title;
    TextMeshProUGUI subtitle;
    Image divider;
    readonly Card[] cards = new Card[3];

    List<BoonOffer> offers;
    Action<int> chosen;
    Color elementColor;
    int selected;
    int heardSelection;
    float shownAt;

    readonly HudSlot[] hudSlots = new HudSlot[3];
    RectTransform passiveRow;
    readonly List<Image> passiveIcons = new List<Image>();

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

    // elementPortrait is the ronin with his wisps in the soul's colour; empty uses the default portrait.
    public void Show(Element element, List<BoonOffer> boonOffers, Sprite[] soulFrames, Sprite elementPortrait, Action<int> onChosen)
    {
        offers = boonOffers;
        chosen = onChosen;
        ElementInfo info = Boons.Info(element);
        elementColor = info.Color;
        selected = heardSelection = 0;
        shownAt = Time.unscaledTime;

        title.text = $"Boons of {info.Name}";
        title.color = elementColor;
        subtitle.text = info.Subtitle;
        divider.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.7f);

        bool hasSoul = soulFrames != null && soulFrames.Length > 0;
        if (roninPortrait != null)
        {
            portrait.sprite = elementPortrait != null ? elementPortrait : roninPortrait;
            portrait.color = Color.white;
            portraitAnimator.Play(null, 0f);
            // The soul he caught floats over his palm, so it takes the element's colours.
            heldSoul.gameObject.SetActive(hasSoul);
            if (hasSoul)
            {
                heldSoul.sprite = soulFrames[0];
                heldSoulAnimator.Play(soulFrames, 8f);
            }
            heldSoulGlow.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.8f);
        }
        else
        {
            portrait.sprite = hasSoul ? soulFrames[0] : ProceduralSprites.Glow;
            portrait.color = hasSoul ? Color.white : Color.Lerp(elementColor, Color.white, 0.5f);
            portraitAnimator.Play(hasSoul ? soulFrames : null, 8f);
            heldSoul.gameObject.SetActive(false);
            heldSoulGlow.color = Color.clear;
        }
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
        // Hidden until AnimatePanel slides it in, so it never flashes at full opacity on the first frame.
        card.group.alpha = 0f;
        card.glow.color = Color.clear;
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

        if (selected != heardSelection) Sfx.Play(Sfx.UiMove);
        heardSelection = selected;

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
        Sfx.Play(Sfx.UiConfirm);
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
        // The ronin slides in from the left, then breathes gently while the player chooses.
        float enter = Mathf.Clamp01(elapsed / 0.3f);
        enter = 1f - (1f - enter) * (1f - enter);
        // The breath moves in whole art pixels so the big portrait doesn't shimmer.
        float breath = Mathf.Round(Mathf.Sin(Time.unscaledTime * 1.8f)) * PortraitPixelScale;
        portrait.rectTransform.anchoredPosition = portraitHome + new Vector2((enter - 1f) * 160f, breath);
        portrait.color = new Color(portrait.color.r, portrait.color.g, portrait.color.b, enter);
        heldSoul.color = new Color(1f, 1f, 1f, enter);
        heldSoul.rectTransform.anchoredPosition = heldSoulGlow.rectTransform.anchoredPosition + new Vector2(0f, 6f * Mathf.Sin(Time.unscaledTime * 3f));
        heldSoulGlow.rectTransform.localScale = Vector3.one * (0.9f + 0.15f * pulse);

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
                slot.letter.text = ability != null ? "" : new[] { "B", "S", "P" }[i];
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
    // Without art the soul floats on its own left of the cards; the ronin's portrait is huge and cropped by the
    // bottom-left corner of the screen, raising the soul up into the top-left like Zagreus in Hades.
    static readonly Vector2 SoulOnlyPosition = new Vector2(-690, 40);
    static readonly Vector2 PortraitCorner = new Vector2(-80, -128);

    void BuildPanel()
    {
        RectTransform root = GameUI.CreateRect("BoonChoice", transform, Vector2.zero, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.04f, 0.72f);
        panel = root.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);
        // The ronin's art is drawn at a whole-number scale so its pixels stay square.
        bool hasArt = roninPortrait != null;
        Vector2 portraitSize = hasArt ? roninPortrait.rect.size * PortraitPixelScale : new Vector2(360, 360);
        Vector2 portraitAnchor = hasArt ? Vector2.zero : center;
        portraitHome = hasArt ? PortraitCorner : SoulOnlyPosition;
        Vector2 palm = hasArt
            ? new Vector2(portraitSoulPixel.x - roninPortrait.rect.width / 2f, roninPortrait.rect.height / 2f - portraitSoulPixel.y) * PortraitPixelScale
            : Vector2.zero;

        // The element's glow sits behind the soul in his hand.
        portraitGlow = CreateImage("PortraitGlow", root, portraitAnchor, portraitHome + (hasArt ? portraitSize / 2f + palm : Vector2.zero),
            hasArt ? new Vector2(1100, 1100) : new Vector2(640, 640), ProceduralSprites.Glow);
        portraitGlow.rectTransform.pivot = center;
        portrait = CreateImage("Portrait", root, portraitAnchor, portraitHome, portraitSize, null);
        portrait.preserveAspect = true;
        portraitAnimator = portrait.gameObject.AddComponent<SpriteAnimatorUI>();

        Vector2 soulSize = new Vector2(48, 48) * PortraitPixelScale * heldSoulScale;
        heldSoulGlow = CreateImage("HeldSoulGlow", portrait.rectTransform, center, palm, soulSize * 2.2f, ProceduralSprites.Glow);
        heldSoul = CreateImage("HeldSoul", portrait.rectTransform, center, palm, soulSize, null);
        heldSoul.preserveAspect = true;
        heldSoulAnimator = heldSoul.gameObject.AddComponent<SpriteAnimatorUI>();

        title = GameUI.CreateText(root, font, "Title", center, new Vector2(CardX, 425), new Vector2(1300, 90), 66, TextAlignmentOptions.Center);
        title.characterSpacing = 38f;
        title.fontStyle = FontStyles.UpperCase;
        subtitle = GameUI.CreateText(root, font, "Subtitle", center, new Vector2(CardX, 362), new Vector2(1300, 50), 28, TextAlignmentOptions.Center);
        subtitle.fontStyle = FontStyles.Italic;
        subtitle.color = MutedText;
        divider = CreateImage("Divider", root, center, new Vector2(CardX, 326), new Vector2(760, 3), null);

        float[] rows = { 175f, -40f, -255f };
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
        Vector2 size = new Vector2(1180, 200);
        card.root = GameUI.CreateRect("Card" + index, parent, center, position, size);
        card.group = card.root.gameObject.AddComponent<CanvasGroup>();

        // A white silhouette of the card, so the selection glow follows its ragged ink edge.
        card.glow = CreateImage("Glow", card.root, center, Vector2.zero, size + new Vector2(24, 24), cardGlowSprite);
        Image background = CreateImage("Panel", card.root, center, Vector2.zero, size, cardSprite);
        if (cardSprite == null) background.color = CardColor;

        // Round medallion on the left, like the god boon icons in Hades.
        Image frame = CreateImage("IconFrame", card.root, center, new Vector2(-498, 0), new Vector2(142, 142), ProceduralSprites.Disc);
        frame.color = new Color(0.12f, 0.09f, 0.16f);
        card.icon = CreateImage("Icon", frame.rectTransform, center, Vector2.zero, new Vector2(124, 124), null);
        card.icon.preserveAspect = true;
        card.letter = GameUI.CreateText(frame.rectTransform, font, "Letter", center, Vector2.zero, new Vector2(128, 128), 64, TextAlignmentOptions.Center);
        card.slot = GameUI.CreateText(card.root, font, "Slot", center, new Vector2(400, 14), new Vector2(300, 28), 20, TextAlignmentOptions.Right);
        card.slot.color = MutedText;
        card.slot.characterSpacing = 8f;

        card.name = GameUI.CreateText(card.root, font, "Name", center, new Vector2(-90, 46), new Vector2(640, 52), 42, TextAlignmentOptions.Left);
        card.name.fontStyle = FontStyles.SmallCaps;
        card.rarity = GameUI.CreateText(card.root, font, "Rarity", center, new Vector2(400, 46), new Vector2(300, 52), 32, TextAlignmentOptions.Right);
        card.rarity.fontStyle = FontStyles.SmallCaps;
        // Room for two lines under the name; the stat line sits below them.
        card.description = GameUI.CreateText(card.root, font, "Description", center, new Vector2(-15, -10), new Vector2(790, 60), 26, TextAlignmentOptions.TopLeft);
        card.description.color = new Color(0.9f, 0.88f, 0.84f);
        card.stat = GameUI.CreateText(card.root, font, "Stat", center, new Vector2(70, -47), new Vector2(960, 32), 26, TextAlignmentOptions.Left);
        card.note = GameUI.CreateText(card.root, font, "Note", center, new Vector2(330, -47), new Vector2(440, 32), 22, TextAlignmentOptions.Right);
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

        passiveRow = GameUI.CreateRect("Passives", transform, corner, new Vector2(-30, 192), new Vector2(400, 50));
        passiveRow.pivot = new Vector2(1f, 0.5f);
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
