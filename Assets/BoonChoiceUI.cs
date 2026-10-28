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
    static readonly Color CardColor = new Color(0.03f, 0.02f, 0.05f, 0.97f);
    // The dark well the held soul sits in, and how far its shadow falls below it.
    static readonly Color PocketColour = new Color(0.02f, 0.01f, 0.04f, 0.85f);
    static readonly Vector2 ShadowDrop = new Vector2(0f, -14f);
    static readonly Color MutedText = new Color(0.78f, 0.74f, 0.68f);
    static readonly string StatGreen = "#A8E07C";
    const float CardRuleWidth = 980f;

    class Card
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image ink;
        public Image selectInk;
        public RectTransform marker;
        public RectTransform rule;
        public CanvasGroup content;
        public Image ring;
        public Image icon;
        public TextMeshProUGUI letter;
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
        public TextMeshProUGUI tip;
    }

    [Header("Inscribed")]
    [Tooltip("Full-body painting of the ronin on the left of the screen, like Zagreus in Hades. Empty shows the soul instead.")]
    [SerializeField] Sprite roninPortrait;
    [Tooltip("On-screen height of the ronin's painting; it is cropped by the bottom of the screen.")]
    [SerializeField] float portraitHeight = 1350f;
    [Tooltip("Bottom-left corner of the painting, from the bottom-left of the screen.")]
    [SerializeField] Vector2 portraitCorner = new Vector2(-250, -601);
    [Tooltip("Where the caught soul floats over the ronin's open palm, as a fraction of the painting from its top-left corner.")]
    [SerializeField] Vector2 portraitSoulPoint = new Vector2(0.8f, -0.06f);
    [Tooltip("Shifts the held soul (and its swirl, pocket and shadow) from his palm, so the selected card's marker doesn't overlap it.")]
    [SerializeField] Vector2 heldSoulShift = new Vector2(-170f, 0f);
    [Tooltip("On-screen size of the soul in his hand.")]
    [SerializeField] float heldSoulSize = 500f;
    [Tooltip("One icon per boon, in BoonId order.")]
    [SerializeField] Sprite[] boonIcons;
    [Tooltip("Default icons for the Attack (sword), Shuriken and Parry slots before they hold a boon.")]
    [SerializeField] Sprite[] abilityIcons = new Sprite[3];

    Ronin ronin;
    GameUI ui;
    TMP_FontAsset font;
    bool built;

    GameObject panel;
    Image portrait;
    Vector2 portraitHome;
    Image portraitGlow;
    SwirlUI swirl;
    SpriteAnimatorUI portraitAnimator;
    Image heldSoul;
    Image heldSoulShadow;
    Image pocket;
    Image heldSoulGlow;
    SpriteAnimatorUI heldSoulAnimator;
    TextMeshProUGUI overline;
    TextMeshProUGUI title;
    TextMeshProUGUI subtitle;
    RectTransform divider;
    readonly Card[] cards = new Card[3];

    List<BoonOffer> offers;
    // Called with the card taken, or Rejected when every offer is turned down.
    Action<int> chosen;
    public const int Rejected = -1;
    // Rejecting takes two presses: the first arms it (the hint asks "Reject all?"), so a stray key never throws a soul away.
    bool rejectArmed;
    // Fate's Thread: draws a fresh set of offers, once.
    Func<List<BoonOffer>> redraw;
    // Banishing Bell: takes the selected card's boon out of the run and returns the fresh cards.
    Func<int, List<BoonOffer>> banish;
    TextMeshProUGUI hint;
    Color elementColor;
    int selected;
    int heardSelection;
    float shownAt;

    readonly HudSlot[] hudSlots = new HudSlot[3];
    RectTransform passiveRow;
    readonly List<Image> passiveIcons = new List<Image>();
    IReadOnlyList<EquippedBoon> hudPassives = new List<EquippedBoon>();
    readonly List<TextMeshProUGUI> passiveTips = new List<TextMeshProUGUI>();
    CanvasGroup tips;
    static readonly string[] SlotKeys = { "Space", "W", "Shift" };
    static readonly string[] SlotNames = { "Slash", "Spirit Shuriken", "Parry" };
    const float SlotSize = 68f;
    const float SlotGap = 12f;

    public bool IsOpen => panel != null && panel.activeSelf;
    // While Poguru explains the cards over the open screen, nothing can be picked.
    public bool InputLocked { get; set; }

    // A HUD ability slot's ring, for pointing at it.
    public RectTransform SlotRect(BoonSlot slot) => hudSlots[(int)slot].ring.rectTransform;

    // Moves the ability slots onto `layer` (a talk scene's overlay, which covers the screen as the canvas does, so
    // they stay put) to show them over the scene's dark; DropSlots puts them back.
    public void LiftSlots(RectTransform layer)
    {
        foreach (HudSlot slot in hudSlots)
        {
            slot.ring.rectTransform.SetParent(layer, false);
            slot.ring.gameObject.SetActive(true);
        }
    }

    public void DropSlots()
    {
        foreach (HudSlot slot in hudSlots)
        {
            if (slot.ring.transform.parent == transform) continue;
            slot.ring.rectTransform.SetParent(transform, false);
            // Back under the tooltips, where they were built.
            slot.ring.rectTransform.SetSiblingIndex(tips.transform.GetSiblingIndex());
        }
    }

    public void Bind(Ronin owner, GameUI gameUI)
    {
        ronin = owner;
        ui = gameUI;
        font = gameUI.BodyFont;
        if (built) return;
        built = true;
        BuildHud();
        BuildPanel();
        panel.SetActive(false);
    }

    // elementPortrait is the ronin with his wisps in the soul's colour; empty uses the default portrait.
    public void Show(Element element, List<BoonOffer> boonOffers, Sprite[] soulFrames, Sprite elementPortrait, Action<int> onChosen)
    {
        ElementInfo info = Boons.Info(element);
        Show($"Boons of {info.Name}", info.Subtitle, info.Color, boonOffers, soulFrames, elementPortrait, onChosen);
    }

    // The Sword Arts screen for a technique scroll: the same layout in polished steel, the scroll in the ronin's hand.
    public void ShowArts(List<BoonOffer> artOffers, Sprite[] scroll, Action<int> onChosen)
    {
        Show("Sword Arts", "a secret technique of the blade, written by a master long dead.", Boons.ArtColor, artOffers,
            scroll, null, onChosen);
    }

    // A Spirit Bead (Hades' Pom of Power): the held boons it could raise, each card showing the step up.
    public void ShowBead(List<BoonOffer> upgrades, Sprite[] bead, Action<int> onChosen)
    {
        Show("Spirit Bead", "a bead of the temple's prayer string, warm with old devotion.", Boons.BeadColor, upgrades,
            bead, null, onChosen);
    }

    void Show(string heading, string tagline, Color colour, List<BoonOffer> boonOffers, Sprite[] soulFrames, Sprite elementPortrait, Action<int> onChosen)
    {
        offers = boonOffers;
        chosen = onChosen;
        redraw = null;
        banish = null;
        rejectArmed = false;
        UpdateHints();
        elementColor = colour;
        selected = heardSelection = 0;
        shownAt = Time.unscaledTime;

        title.text = heading;
        title.color = elementColor;
        subtitle.text = tagline;
        Color ruleColour = Color.Lerp(elementColor, Color.white, 0.35f);
        foreach (Image mark in divider.GetComponentsInChildren<Image>()) mark.color = ruleColour;

        bool hasSoul = soulFrames != null && soulFrames.Length > 0;
        if (roninPortrait != null)
        {
            portrait.sprite = elementPortrait != null ? elementPortrait : roninPortrait;
            portrait.color = Color.white;
            portraitAnimator.Play(null, 0f);
            // The soul he caught floats over his palm, so it takes the element's colours.
            heldSoul.gameObject.SetActive(hasSoul);
            heldSoulShadow.gameObject.SetActive(hasSoul);
            if (hasSoul)
            {
                heldSoul.sprite = soulFrames[0];
                heldSoulAnimator.Play(soulFrames, 8f);
                heldSoulShadow.sprite = ProceduralSprites.ShadowOf(soulFrames[0], out float shadowScale);
                heldSoulShadow.rectTransform.sizeDelta = heldSoul.rectTransform.sizeDelta * shadowScale;
            }
            // Only a faint aura: a bright halo here would fill in the dark pocket the soul sits in.
            heldSoulGlow.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.25f);
        }
        else
        {
            portrait.sprite = hasSoul ? soulFrames[0] : ProceduralSprites.Glow;
            portrait.color = hasSoul ? Color.white : Color.Lerp(elementColor, Color.white, 0.5f);
            portraitAnimator.Play(hasSoul ? soulFrames : null, 8f);
            heldSoul.gameObject.SetActive(false);
            heldSoulShadow.gameObject.SetActive(false);
            heldSoulGlow.color = Color.clear;
        }
        // A soft wash under the swirl; any brighter and a pale element (the steel of Sword Arts) turns the corner white.
        portraitGlow.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.22f);
        swirl.Colour = elementColor;

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].root.gameObject.SetActive(i < offers.Count);
            if (i < offers.Count) Fill(cards[i], offers[i]);
        }
        // The ability slots would show through the footer's key hints.
        SetHudVisible(false);
        panel.SetActive(true);
    }

    static string Hints => $"{MenuUI.KeyCap("1 2 3", false)}  Quick pick       {MenuUI.KeyCap("↑ ↓", false)}  Select       {MenuUI.KeyCap("Enter", false)}  Choose";
    // Its own link, so a click on it rejects as the key does.
    string RejectHint => rejectArmed
        ? $"<link=\"reject\">{MenuUI.KeyCap("X", false)}  <color=#{ColorUtility.ToHtmlStringRGB(RejectRed)}>Reject all?</color></link>"
        : $"<link=\"reject\">{MenuUI.KeyCap("X", false)}  Reject</link>";
    static readonly Color RejectRed = new Color(1f, 0.42f, 0.36f);

    // Offers a redraw of this screen's cards (R), for the Fate's Thread song. The callback may offer another.
    public void OfferRedraw(Func<List<BoonOffer>> draw)
    {
        redraw = draw;
        UpdateHints();
    }

    // Offers to banish the selected card's boon for the rest of the run (B), for the Banishing Bell song.
    public void OfferBanish(Func<int, List<BoonOffer>> banishSelected)
    {
        banish = banishSelected;
        UpdateHints();
    }

    void UpdateHints()
    {
        hint.text = Hints;
        if (redraw != null) hint.text += $"       {MenuUI.KeyCap("R", false)}  Redraw";
        if (banish != null) hint.text += $"       {MenuUI.KeyCap("B", false)}  Banish";
        hint.text += $"       {RejectHint}";
    }

    // The first press arms it; the second turns every offer down and closes the screen with nothing taken.
    void Reject()
    {
        if (!rejectArmed)
        {
            rejectArmed = true;
            UpdateHints();
            Sfx.Play(Sfx.UiMove);
            return;
        }
        Choose(Rejected);
    }

    void Disarm()
    {
        if (!rejectArmed) return;
        rejectArmed = false;
        UpdateHints();
    }

    void Redraw()
    {
        Func<List<BoonOffer>> draw = redraw;
        redraw = null;
        rejectArmed = false;
        UpdateHints();
        Replace(draw());
    }

    void Banish()
    {
        Func<int, List<BoonOffer>> banishSelected = banish;
        banish = null;
        rejectArmed = false;
        UpdateHints();
        Replace(banishSelected(selected));
    }

    // Fresh cards in place of the shown ones.
    void Replace(List<BoonOffer> fresh)
    {
        if (fresh == null || fresh.Count == 0) return;
        offers = fresh;
        selected = heardSelection = 0;
        shownAt = Time.unscaledTime;
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].root.gameObject.SetActive(i < offers.Count);
            if (i < offers.Count) Fill(cards[i], offers[i]);
        }
        Sfx.Play(Sfx.SoulCollect);
    }

    void Fill(Card card, BoonOffer offer)
    {
        BoonDefinition boon = offer.Boon;
        Color rarityColor = Boons.KindColor(boon.Kind, offer.Rarity);
        Color element = ColorOf(boon);
        // A Duo's wash takes its partner's colour, so the card shows both elements.
        Color wash = boon.Partner is Element partner ? Boons.Info(partner).Color : element;
        SetIcon(card.icon, card.letter, boon, element);
        card.ring.color = new Color(element.r, element.g, element.b, 0.85f);
        card.selectInk.color = new Color(wash.r, wash.g, wash.b, 0.22f);
        card.marker.GetComponent<Image>().color = element;
        foreach (Image mark in card.rule.GetComponentsInChildren<Image>()) mark.color = Color.Lerp(element, Color.white, 0.35f);
        card.name.text = boon.Name;
        card.name.color = rarityColor;
        // The slot rides on the rarity line so the description can use the card's full width.
        string rarity = boon.Kind switch
        {
            BoonKind.Duo => $"Duo  ·  {Boons.Info(boon.Element).Name} + {Boons.Info(boon.Partner.Value).Name}",
            BoonKind.Legendary => "Legendary",
            BoonKind.Offering => "Offering",
            BoonKind.Art => "Sword Art",
            _ => offer.IsUpgrade ? $"Upgrade  {offer.Rarity}" : offer.Rarity.ToString(),
        };
        string slot = boon.Kind == BoonKind.Offering ? "" : $"  <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}><size=26>·  {boon.Slot.ToString().ToUpperInvariant()}</size></color>";
        card.rarity.text = rarity + slot;
        card.rarity.color = rarityColor;
        card.description.text = boon.Description;

        string value = boon.Format(boon.ValueAt(offer.Rarity));
        if (offer.IsUpgrade) value += $"  <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>(was {boon.Format(boon.ValueAt(offer.Rarity - 1))})</color>";
        card.stat.text = $"<color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>- {boon.StatLabel}</color>   <color={StatGreen}>{value}</color>";
        if (boon.ExtraValues != null)
        {
            System.Func<float, string> format = boon.ExtraFormat ?? (v => $"{v:0}");
            string extra = format(boon.ExtraValues[(int)offer.Rarity]);
            if (offer.IsUpgrade) extra += $"  <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>(was {format(boon.ExtraValues[(int)offer.Rarity - 1])})</color>";
            card.stat.text += $"     <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{boon.ExtraLabel}</color>   <color={StatGreen}>{extra}</color>";
        }
        card.note.text = offer.Replaces != null ? $"Replaces {offer.Replaces.Boon.Name}" : "";
        card.slide = 0f;
        // Hidden until AnimatePanel paints it in, so it never flashes at full opacity on the first frame.
        card.group.alpha = 0f;
        card.ink.fillAmount = 0f;
    }

    // A Sword Art is steel; every other boon wears its element's colour.
    static Color ColorOf(BoonDefinition boon) => boon.Kind == BoonKind.Art ? Boons.ArtColor : Boons.Info(boon.Element).Color;

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

        if (selected != heardSelection)
        {
            Sfx.Play(Sfx.UiMove);
            // Looking at another card means he's still choosing.
            Disarm();
        }
        heardSelection = selected;

        float elapsed = Time.unscaledTime - shownAt;
        AnimatePanel(elapsed);
        if (elapsed < InputDelay || InputLocked) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) selected = (selected + offers.Count - 1) % offers.Count;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) selected = (selected + 1) % offers.Count;
            if (keyboard.digit1Key.wasPressedThisFrame) selected = 0;
            if (keyboard.digit2Key.wasPressedThisFrame && offers.Count > 1) selected = 1;
            if (keyboard.digit3Key.wasPressedThisFrame && offers.Count > 2) selected = 2;
            if (keyboard.rKey.wasPressedThisFrame && redraw != null)
            {
                Redraw();
                return;
            }
            if (keyboard.bKey.wasPressedThisFrame && banish != null)
            {
                Banish();
                return;
            }
            if (keyboard.xKey.wasPressedThisFrame)
            {
                Reject();
                return;
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Choose(selected);
                return;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame && TMP_TextUtilities.FindIntersectingLink(hint, pointer, null) >= 0)
        {
            Reject();
            return;
        }
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
        Sfx.Play(index == Rejected ? Sfx.UiMove : Sfx.UiConfirm);
        rejectArmed = false;
        panel.SetActive(false);
        SetHudVisible(true);
        Action<int> callback = chosen;
        chosen = null;
        callback?.Invoke(index);
    }

    // Each card's ink paints on from the left one after another and its text rises in behind it. The selected card
    // gets the menus' diamond marker, a wash of the element's colour and a brush rule, and steps slightly forward.
    void AnimatePanel(float elapsed)
    {
        float dt = Time.unscaledDeltaTime;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        GameUI.Reveal(overline, elapsed - 0.05f, 488f, -1f, 10f);
        GameUI.Reveal(title, elapsed - 0.12f, 425f, -1f);
        GameUI.Reveal(subtitle, elapsed - 0.3f, 362f, -1f, 0f);
        GameUI.AnimateRule(divider, 760f, GameUI.EaseOut(Mathf.Clamp01((elapsed - 0.2f) / 0.5f)));
        portraitGlow.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 2.5f));
        // The ronin slides in from the left, then breathes gently while the player chooses.
        float enter = Mathf.Clamp01(elapsed / 0.3f);
        enter = 1f - (1f - enter) * (1f - enter);
        float breath = Mathf.Sin(Time.unscaledTime * 1.8f) * 5f;
        portrait.rectTransform.anchoredPosition = portraitHome + new Vector2((enter - 1f) * 160f, breath);
        portrait.color = new Color(portrait.color.r, portrait.color.g, portrait.color.b, enter);
        swirl.Strength = Mathf.Clamp01(elapsed / 0.6f);
        heldSoul.color = new Color(1f, 1f, 1f, enter);
        heldSoul.rectTransform.anchoredPosition = heldSoulGlow.rectTransform.anchoredPosition + new Vector2(0f, 6f * Mathf.Sin(Time.unscaledTime * 3f));
        // Its shadow falls a little below it and follows its bob, so it reads as floating off the swirl.
        heldSoulShadow.rectTransform.anchoredPosition = heldSoul.rectTransform.anchoredPosition + ShadowDrop;
        heldSoulShadow.rectTransform.localScale = heldSoul.rectTransform.localScale;
        heldSoulShadow.color = new Color(0f, 0f, 0f, 0.75f * enter);
        pocket.color = new Color(PocketColour.r, PocketColour.g, PocketColour.b, PocketColour.a * swirl.Strength);
        heldSoulGlow.rectTransform.localScale = Vector3.one * (0.9f + 0.15f * pulse);
        // The painted soul is a single frame, so it flickers by swelling a little out of step with the glow.
        heldSoul.rectTransform.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(Time.unscaledTime * 7.3f) + 0.02f * Mathf.Sin(Time.unscaledTime * 3.1f));

        for (int i = 0; i < offers.Count; i++)
        {
            Card card = cards[i];
            float since = elapsed - 0.1f - i * 0.09f;
            card.group.alpha = Mathf.Clamp01(since / 0.1f);
            card.ink.fillAmount = GameUI.EaseOut(Mathf.Clamp01(since / 0.4f));
            card.content.alpha = GameUI.EaseOut(Mathf.Clamp01((since - 0.15f) / 0.3f));
            card.slide = Mathf.MoveTowards(card.slide, i == selected ? 1f : 0f, dt * 8f);
            float k = GameUI.EaseOut(card.slide);
            card.root.anchoredPosition = new Vector2(CardX + k * CardSlide, card.root.anchoredPosition.y);
            card.selectInk.fillAmount = k;
            card.marker.localScale = Vector3.one * k;
            GameUI.AnimateRule(card.rule, CardRuleWidth, k);
        }
    }

    void UpdateHud()
    {
        hudSlots[(int)BoonSlot.Special].cooldown.fillAmount = 1f - ronin.ShurikenCharge;
        hudSlots[(int)BoonSlot.Parry].cooldown.fillAmount = 1f - ronin.ParryCharge;
        // Glacier Shell's icon stays dim while the shell grows back.
        for (int i = 0; i < hudPassives.Count && i < passiveIcons.Count; i++)
        {
            if (hudPassives[i].Boon.Id != BoonId.GlacierShell) continue;
            Color c = passiveIcons[i].color;
            passiveIcons[i].color = new Color(c.r, c.g, c.b, ronin.ShellCharge >= 1f ? 1f : 0.3f + 0.4f * ronin.ShellCharge);
        }

        // Names and keys stay hidden so the column is small; holding Tab (or the gamepad's View button) shows them.
        bool held = (Keyboard.current != null && Keyboard.current.tabKey.isPressed)
            || (Gamepad.current != null && Gamepad.current.selectButton.isPressed);
        tips.alpha = Mathf.MoveTowards(tips.alpha, held ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
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
                slot.tip.text = $"{MenuUI.KeyCap(SlotKeys[i], false)}  {held.Boon.Name}";
            }
            else
            {
                Sprite ability = abilityIcons != null && i < abilityIcons.Length ? abilityIcons[i] : null;
                slot.icon.sprite = ability != null ? ability : ProceduralSprites.Disc;
                slot.icon.color = ability != null ? Color.white : new Color(0.25f, 0.25f, 0.3f);
                slot.letter.text = ability != null ? "" : new[] { "B", "S", "P" }[i];
                slot.ring.color = new Color(0.55f, 0.55f, 0.6f);
                slot.tip.text = $"{MenuUI.KeyCap(SlotKeys[i], false)}  {SlotNames[i]}";
            }
        }

        hudPassives = loadout.Passives;
        while (passiveIcons.Count < loadout.Passives.Count)
        {
            // Passives stack up the column above the ability slots.
            RectTransform rect = GameUI.CreateRect("Passive" + passiveIcons.Count, passiveRow, new Vector2(0.5f, 0f),
                new Vector2(0, passiveIcons.Count * 52f), new Vector2(46, 46));
            passiveIcons.Add(rect.gameObject.AddComponent<Image>());
            ui.Style(GameUI.CreateText(rect, font, "Letter", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), 28, TextAlignmentOptions.Center), TextStyle.Hud);
            passiveTips.Add(CreateTip(passiveRow.anchoredPosition + new Vector2(60f, 23f + (passiveIcons.Count - 1) * 52f)));
        }
        for (int i = 0; i < passiveIcons.Count; i++)
        {
            bool shown = i < loadout.Passives.Count;
            passiveIcons[i].gameObject.SetActive(shown);
            passiveTips[i].gameObject.SetActive(shown);
            if (!shown) continue;
            BoonDefinition boon = loadout.Passives[i].Boon;
            SetIcon(passiveIcons[i], passiveIcons[i].GetComponentInChildren<TextMeshProUGUI>(), boon, ColorOf(boon));
            passiveTips[i].text = boon.Name;
        }
    }

    const float CardX = 170f;
    // Without art the soul floats on its own left of the cards; the ronin's portrait is huge and cropped by the
    // bottom-left corner of the screen, raising the soul up into the top-left like Zagreus in Hades.
    static readonly Vector2 SoulOnlyPosition = new Vector2(-690, 40);

    void BuildPanel()
    {
        RectTransform root = GameUI.CreateRect("BoonChoice", transform, Vector2.zero, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.04f, 0.82f);
        panel = root.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);
        bool hasArt = roninPortrait != null;
        Vector2 portraitSize = hasArt
            ? new Vector2(portraitHeight * roninPortrait.rect.width / roninPortrait.rect.height, portraitHeight)
            : new Vector2(360, 360);
        Vector2 portraitAnchor = hasArt ? Vector2.zero : center;
        portraitHome = hasArt ? portraitCorner : SoulOnlyPosition;
        Vector2 palm = hasArt
            ? new Vector2((portraitSoulPoint.x - 0.5f) * portraitSize.x, (0.5f - portraitSoulPoint.y) * portraitSize.y) + heldSoulShift
            : Vector2.zero;

        // A vortex of the element's light turns behind the soul in his hand, filling the corner above him.
        swirl = SwirlUI.Create("Swirl", root, portraitAnchor, portraitHome + (hasArt ? portraitSize / 2f + palm : Vector2.zero),
            hasArt ? 1400f : 820f);
        // The element's glow sits behind the soul in his hand.
        portraitGlow = CreateImage("PortraitGlow", root, portraitAnchor, portraitHome + (hasArt ? portraitSize / 2f + palm : Vector2.zero),
            hasArt ? new Vector2(1100, 1100) : new Vector2(640, 640), ProceduralSprites.SoftGlow);
        portraitGlow.rectTransform.pivot = center;
        // A well of shadow in the swirl's heart, behind Oni, so what he holds is the brightest thing in a dark frame.
        pocket = CreateImage("Pocket", root, portraitAnchor, portraitHome + (hasArt ? portraitSize / 2f + palm : Vector2.zero),
            Vector2.one * heldSoulSize * 1.9f, ProceduralSprites.SoftGlow);
        pocket.rectTransform.pivot = center;
        portrait = CreateImage("Portrait", root, portraitAnchor, portraitHome, portraitSize, null);
        portrait.preserveAspect = true;
        portraitAnimator = portrait.gameObject.AddComponent<SpriteAnimatorUI>();

        Vector2 soulSize = new Vector2(heldSoulSize, heldSoulSize);
        heldSoulGlow = CreateImage("HeldSoulGlow", portrait.rectTransform, center, palm, soulSize * 1.5f, ProceduralSprites.SoftGlow);
        // A soft shadow in the soul's own shape, just under it.
        heldSoulShadow = CreateImage("HeldSoulShadow", portrait.rectTransform, center, palm, soulSize, null);
        heldSoulShadow.preserveAspect = true;
        heldSoul = CreateImage("HeldSoul", portrait.rectTransform, center, palm, soulSize, null);
        heldSoul.preserveAspect = true;
        heldSoulAnimator = heldSoul.gameObject.AddComponent<SpriteAnimatorUI>();

        // The header follows the stage and boss cards: a small overline, a large title, then a brush rule.
        overline = GameUI.CreateText(root, font, "Overline", center, new Vector2(CardX, 488), new Vector2(1300, 44), 30, TextAlignmentOptions.Center);
        overline.text = "A soul offers its power";
        overline.color = MutedText;
        ui.Style(overline, TextStyle.Title);
        title = GameUI.CreateText(root, font, "Title", center, new Vector2(CardX, 425), new Vector2(1300, 100), 76, TextAlignmentOptions.Center);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        ui.Style(title, TextStyle.Title);
        subtitle = GameUI.CreateText(root, font, "Subtitle", center, new Vector2(CardX, 362), new Vector2(1300, 50), 32, TextAlignmentOptions.Center);
        subtitle.color = MutedText;
        ui.Style(subtitle, TextStyle.Body);
        divider = GameUI.CreateRule(root, center, new Vector2(CardX, 326), Color.white);

        float[] rows = { 175f, -40f, -255f };
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = BuildCard(root, new Vector2(CardX, rows[i]), i);
        }

        // Key hints in the menus' key caps, under the cards.
        hint = GameUI.CreateText(root, font, "Hint", new Vector2(0.5f, 0f), new Vector2(CardX, 40), new Vector2(1300, 50), 30, TextAlignmentOptions.Center);
        hint.text = Hints;
        hint.color = MutedText;
        ui.Style(hint, TextStyle.Hint);
    }

    Card BuildCard(RectTransform parent, Vector2 position, int index)
    {
        var card = new Card();
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 size = new Vector2(1180, 200);
        card.root = GameUI.CreateRect("Card" + index, parent, center, position, size);
        card.group = card.root.gameObject.AddComponent<CanvasGroup>();

        // A stroke of ink for the card, its dry tail trailing off past the text on the right. The selected card gets
        // a second, wider stroke in the element's colour behind it.
        card.selectInk = GameUI.CreateInk(card.root, center, new Vector2(60, 0), new Vector2(1340, 250), Color.clear);
        card.ink = GameUI.CreateInk(card.root, center, new Vector2(40, 0), new Vector2(1300, 220), CardColor);

        // The menus' diamond marker, just left of the selected card.
        card.marker = GameUI.CreateRect("Marker", card.root, center, new Vector2(-634, 0), new Vector2(20, 20));
        card.marker.localRotation = Quaternion.Euler(0, 0, 45f);
        card.marker.gameObject.AddComponent<Image>().raycastTarget = false;

        RectTransform content = GameUI.CreateRect("Content", card.root, center, Vector2.zero, size);
        card.content = content.gameObject.AddComponent<CanvasGroup>();

        // Round medallion on the left, like the god boon icons in Hades, ringed in the element's colour.
        card.ring = CreateImage("IconRing", content, center, new Vector2(-498, 0), new Vector2(150, 150), ProceduralSprites.Disc);
        Image frame = CreateImage("IconFrame", card.ring.rectTransform, center, Vector2.zero, new Vector2(140, 140), ProceduralSprites.Disc);
        frame.color = new Color(0.08f, 0.06f, 0.11f);
        card.icon = CreateImage("Icon", frame.rectTransform, center, Vector2.zero, new Vector2(124, 124), null);
        card.icon.preserveAspect = true;
        card.letter = GameUI.CreateText(frame.rectTransform, font, "Letter", center, Vector2.zero, new Vector2(128, 128), 64, TextAlignmentOptions.Center);
        ui.Style(card.letter, TextStyle.Heading);

        card.name = GameUI.CreateText(content, font, "Name", center, new Vector2(-90, 62), new Vector2(640, 60), 44, TextAlignmentOptions.Left);
        card.name.textWrappingMode = TextWrappingModes.NoWrap;
        ui.Style(card.name, TextStyle.Title);
        card.rarity = GameUI.CreateText(content, font, "Rarity", center, new Vector2(340, 64), new Vector2(420, 56), 32, TextAlignmentOptions.Right);
        card.rarity.fontStyle = FontStyles.SmallCaps;
        ui.Style(card.rarity, TextStyle.Heading);
        // Room for two lines under the name; the stat line sits below them.
        card.description = GameUI.CreateText(content, font, "Description", center, new Vector2(70, 4), new Vector2(960, 70), 28, TextAlignmentOptions.TopLeft);
        card.description.color = new Color(0.9f, 0.88f, 0.84f);
        ui.Style(card.description, TextStyle.Body);
        // Two lines have to fit between the name and the stat line.
        card.description.lineSpacing = -14f;
        card.stat = GameUI.CreateText(content, font, "Stat", center, new Vector2(70, -60), new Vector2(960, 34), 28, TextAlignmentOptions.Left);
        ui.Style(card.stat, TextStyle.Hud);
        card.note = GameUI.CreateText(content, font, "Note", center, new Vector2(330, -60), new Vector2(440, 34), 26, TextAlignmentOptions.Right);
        card.note.color = new Color(1f, 0.75f, 0.55f, 0.85f);
        ui.Style(card.note, TextStyle.Hint);
        // The brush rule under the selected card's text.
        card.rule = GameUI.CreateRule(content, center, new Vector2(70, -88), Color.white);
        return card;
    }

    // The ability slots and passives; hidden behind the title menu.
    public void SetHudVisible(bool visible)
    {
        foreach (HudSlot slot in hudSlots) slot.ring.gameObject.SetActive(visible);
        passiveRow.gameObject.SetActive(visible);
        tips.gameObject.SetActive(visible);
    }

    // A column up the left edge: the three ability slots (with cooldown sweeps), then the passives above them. It
    // starts above the ronin's head, so he's never hidden behind it when he walks into the corner.
    void BuildHud()
    {
        Vector2 corner = Vector2.zero;
        tips = GameUI.CreateRect("Tooltips", transform, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<CanvasGroup>();
        RectTransform tipsRect = (RectTransform)tips.transform;
        tipsRect.anchorMin = Vector2.zero;
        tipsRect.anchorMax = Vector2.one;
        tipsRect.sizeDelta = Vector2.zero;
        tips.alpha = 0f;
        tips.blocksRaycasts = false;

        float inner = SlotSize - 10f;
        for (int i = 0; i < hudSlots.Length; i++)
        {
            // Attack on top, then Shuriken, then Parry nearest the ronin.
            Vector2 position = new Vector2(24f, 170f + (hudSlots.Length - 1 - i) * (SlotSize + SlotGap));
            var slot = new HudSlot();
            slot.ring = CreateImage("Slot" + i, transform, corner, position, new Vector2(SlotSize, SlotSize), ProceduralSprites.Disc);
            Image back = CreateImage("Inner", slot.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner, inner), ProceduralSprites.Disc);
            back.color = new Color(0.06f, 0.04f, 0.09f);
            slot.icon = CreateImage("Icon", slot.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner - 6f, inner - 6f), null);
            slot.icon.preserveAspect = true;
            slot.letter = GameUI.CreateText(slot.ring.rectTransform, font, "Letter", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner, inner), 30, TextAlignmentOptions.Center);
            ui.Style(slot.letter, TextStyle.Hud);
            slot.cooldown = CreateImage("Cooldown", slot.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize, SlotSize), ProceduralSprites.Disc);
            slot.cooldown.color = new Color(0f, 0f, 0f, 0.65f);
            slot.cooldown.type = Image.Type.Filled;
            slot.cooldown.fillMethod = Image.FillMethod.Radial360;
            slot.cooldown.fillOrigin = (int)Image.Origin360.Top;
            slot.cooldown.fillClockwise = false;
            slot.cooldown.fillAmount = 0f;
            slot.tip = CreateTip(position + new Vector2(SlotSize + 14f, SlotSize / 2f));
            slot.tip.text = $"{MenuUI.KeyCap(SlotKeys[i], false)}  {SlotNames[i]}";
            hudSlots[i] = slot;
        }

        passiveRow = GameUI.CreateRect("Passives", transform, corner, new Vector2(35f, 170f + hudSlots.Length * (SlotSize + SlotGap)), new Vector2(46, 400));
    }

    // A key and name to the right of a HUD icon, shown while the player holds Tab. `position` is from the screen's
    // bottom-left corner to the text's left edge.
    TextMeshProUGUI CreateTip(Vector2 position)
    {
        TextMeshProUGUI tip = GameUI.CreateText(tips.transform, font, "Tip", Vector2.zero, position, new Vector2(520, 44), 28, TextAlignmentOptions.Left);
        tip.rectTransform.pivot = new Vector2(0f, 0.5f);
        tip.textWrappingMode = TextWrappingModes.NoWrap;
        ui.Style(tip, TextStyle.Hud);
        return tip;
    }

    static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite)
    {
        Image image = GameUI.CreateRect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        return image;
    }
}
