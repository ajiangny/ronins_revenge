using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// What a round pays: drops, souls and scrolls, the boon screens, the loadout's look, the round's reward and the
// paths between rounds, and the stage's boon and bead.
public partial class GameManager
{
    void ResetLoadout()
    {
        loadout = new RoninLoadout();
        soulCaught = false;
        firstBoonTaken = false;
        scrollsDropped = 0;
        gatheredScroll = false;
        ronin.Loadout = loadout;
        effects = new BoonEffects(army, ronin, loadout);
        army.SpeedUpScale = loadout.MarchSpeedUpScale;
        shownElement = null;
        UpdateRoninLook();
        boonChoice.RefreshHud(loadout);
    }

    // Most souls come from the portals; a scout's or a rare samurai's waits for one already falling, and never comes
    // once the round's army is gone.
    void MaybeDropSoul(Vector3 position)
    {
        // Nothing drops in Poguru's tutorial: its one heart and soul are scripted.
        if (activeSoul != null || army.Remaining == 0 || tutorial.Active) return;
        SpawnSoul(position, PickSoulElement());
    }

    void SpawnSoul(Vector3 position, Element element)
    {
        activeSoul = Soul.Spawn(position, element, ArtFor(element)?.soulFrames, ronin, OnSoulCollected);
        if (!soulCaught) activeSoul.ShowHint(soulHintArrow);
    }

    // Like the gods a Hades run keeps meeting, souls lean toward the elements the ronin already carries.
    Element PickSoulElement()
    {
        if (loadout.BoonCount == 0 || Random.value >= soulElementBias) return (Element)Random.Range(0, 3);
        float roll = Random.value * loadout.BoonCount;
        for (int i = 0; i < 3; i++)
        {
            roll -= loadout.ElementCount((Element)i);
            if (roll < 0f) return (Element)i;
        }
        return loadout.DominantElement ?? Element.Fire;
    }

    // Rare, and at most maxScrollsPerRun a run; one on the field at a time, and none once every art is held. Never from
    // the round's last samurai, like souls.
    void MaybeDropScroll(Vector3 position, float chance)
    {
        if (activeScroll != null || scrollsDropped >= maxScrollsPerRun || army.Remaining == 0 || tutorial.Active) return;
        if (Random.value >= chance || Boons.DrawArts(loadout, 1).Count == 0) return;
        scrollsDropped++;
        SpawnScroll(position);
    }

    void SpawnScroll(Vector3 position)
    {
        activeScroll = TechniqueScroll.Spawn(position, scrollFrames, ronin, OnScrollCollected);
        // The first scroll of a run carries the gold arrow, like the first soul.
        if (scrollsDropped <= 1) activeScroll.ShowHint(soulHintArrow);
    }

    void OnScrollCollected(TechniqueScroll scroll)
    {
        if (state == State.Transition)
        {
            gatheredScroll = true;
        }
        else if (state != State.Playing || !OfferArts(ResumePlay))
        {
            return;
        }
        Fx.Play(Fx.Impact, scroll.transform.position, Boons.ArtColor, 1.8f);
        Sfx.Play(Sfx.SoulCollect, 1f, 0.8f);
    }

    // A Spirit Bead raises one held boon a rarity, like Hades' Pom of Power. With nothing left to raise it pays
    // Spirit Notes instead, and false (nothing shown).
    bool OfferBead(System.Action afterPick)
    {
        var offers = Boons.DrawBead(loadout, 3);
        if (offers.Count == 0)
        {
            Earn(BeadNotes * (stageIndex + 1));
            return false;
        }

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.BeadColor, 1.2f);
        Sfx.Play(Sfx.SoulCollect, 1f, 1.15f);
        boonChoice.ShowBead(offers, Held(beadPainted, beadFrames), index =>
        {
            TakeOrReject(offers, index);
            Time.timeScale = 1f;
            afterPick();
        });
        return true;
    }

    // The Sword Arts screen: up to three arts the ronin doesn't hold, like Daedalus's hammer in Hades.
    bool OfferArts(System.Action afterPick)
    {
        var offers = Boons.DrawArts(loadout, 3);
        if (offers.Count == 0) return false;

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        boonChoice.ShowArts(offers, Held(scrollPainted, scrollFrames), index =>
        {
            TakeOrReject(offers, index);
            Time.timeScale = 1f;
            afterPick();
        });
        return true;
    }

    // Pauses the game and offers three boons of the soul's element, like a god's boon in Hades. One gathered at the
    // end of a round waits for GatherDrops to offer it.
    void OnSoulCollected(Soul soul)
    {
        if (state == State.Transition)
        {
            gatheredSoul = soul.Element;
        }
        else if (state != State.Playing || !OfferBoons(soul.Element, ResumePlay))
        {
            return;
        }
        soulCaught = true;
        Fx.Play(Fx.Impact, soul.transform.position, Boons.Info(soul.Element).Color, 1.6f);
        Sfx.Play(Sfx.SoulCollect);
    }

    void OnBlockerBroken(Vector3 position)
    {
        if (state != State.Playing || tutorial.Active || Random.value >= heartDropChance) return;
        Heart.Spawn(position, ui.LifeIcon, ronin, CanHeal, OnHeartCollected);
    }

    // Hearts only heal: at full health they wait on his row until he's lost a life.
    bool CanHeal() => state != State.GameOver && lives < maxLives;

    void OnHeartCollected(Heart heart)
    {
        lives++;
        ui.SetLives(lives, maxLives);
        Fx.Play(Fx.Impact, heart.transform.position, new Color(1f, 0.35f, 0.4f), 1.4f);
        Sfx.Play(Sfx.SoulCollect, 0.8f, 1.3f);
    }

    // At the end of a round every soul still out, and every heart he can use, flies to the ronin. Hearts heal him up
    // to full; any left over fade away into points, so none linger into the next round. A soul caught on the way
    // offers its boons before the game moves on.
    IEnumerator GatherDrops()
    {
        Pickup.GatherAll();
        float giveUp = Time.time + 2f;
        while (Pickup.Gathering > 0 && Time.time < giveUp) yield return null;
        Pickup.StopGathering();

        // Disabled ones are already fading.
        Heart[] spare = System.Array.FindAll(FindObjectsByType<Heart>(), h => h.enabled);
        foreach (Heart heart in spare)
        {
            int points = SpareHeartPoints * (stageIndex + 1);
            score += points;
            heart.Dissipate(points, ui.BodyFont);
        }
        if (spare.Length > 0)
        {
            highScore = Mathf.Max(highScore, score);
            ui.SetScore(score);
            Sfx.Play(Sfx.SoulCollect, 0.6f, 1.5f);
            yield return new WaitForSeconds(Heart.DissipateTime);
        }

        bool picked = false;
        if (gatheredSoul is Element element)
        {
            gatheredSoul = null;
            if (OfferBoons(element, () => picked = true))
            {
                yield return new WaitUntil(() => picked);
                state = State.Transition;
            }
        }
        if (gatheredScroll)
        {
            gatheredScroll = false;
            picked = false;
            if (OfferArts(() => picked = true))
            {
                yield return new WaitUntil(() => picked);
                state = State.Transition;
            }
        }
    }

    void ResumePlay()
    {
        state = State.Playing;
        ronin.ControlsEnabled = true;
    }

    // Pauses on the boon screen and runs afterPick once a boon is taken. An element with nothing left to give
    // offers a Spirit Offering instead. False (and nothing shown) only when nothing could be drawn.
    // `felled` is the boss whose fall this boon rewards.
    bool OfferBoons(Element element, System.Action afterPick, bool ensureCapstone = false, Rarity minRarity = Rarity.Common,
        BossKind? felled = null)
    {
        var offers = Boons.Draw(element, 3, loadout, maxLives, ensureCapstone, minRarity, banished);
        if (offers.Count == 0) return false;

        bool offering = offers.Exists(o => o.Boon.Kind == BoonKind.Offering);
        BossKind? ahead = felled == null && activeBoss == null && Stage.boss != BossKind.None ? Stage.boss : (BossKind?)null;
        var ctx = Dialogue.Context(loadout, element, lives, offering, felled, ahead, offers.ConvertAll(o => o.Boon.Id));
        Conversation greeting = SoulTalk(element, ctx, metThisRun.Add(element));

        state = State.Choosing;
        ronin.ControlsEnabled = false;
        Time.timeScale = 0f;
        if (greeting != null) StartCoroutine(GreetThenChoose(greeting, element, offers, afterPick, ensureCapstone, minRarity));
        else ShowBoons(element, offers, afterPick, ensureCapstone, minRarity);
        return true;
    }

    // What the soul says as the boon screen opens, in Story/Souls.md's order: a Legendary or Duo on offer always gets
    // its line; the boss just felled, a first meeting, the boss ahead, jealousy and the greetings only on the first
    // screen of each soul in a run, as Hades' gods do. Null when nothing's said.
    static Conversation SoulTalk(Element element, DialogueContext ctx, bool first)
    {
        string soul = SoulOf(element);
        Conversation said = Dialogue.Pick(soul, DialogueEvent.Legendary, ctx) ?? Dialogue.Pick("Duo", DialogueEvent.Duo, ctx);
        if (said != null) return said;
        if (ctx.Boss != null) said = Dialogue.Pick(soul, DialogueEvent.BossFelled, ctx);
        if (said != null || !first) return said;
        said = Dialogue.Pick(soul, DialogueEvent.SoulPickup, ctx, Tier.Story, Tier.Story)
            ?? Dialogue.Pick(soul, DialogueEvent.BossAhead, ctx, Tier.Story, Tier.Reactive)
            ?? Dialogue.Pick(soul, DialogueEvent.SoulJealous, ctx, Tier.Story, Tier.Reactive)
            ?? Dialogue.Pick(soul, DialogueEvent.SoulPickup, ctx, Tier.Reactive, Tier.Reactive);
        if (said != null) return said;
        // The everyday lines: a grumble when another soul leads, now and then a word about the boss ahead, else hello.
        float roll = Random.value;
        if (roll < 0.5f) said = Dialogue.Pick(soul, DialogueEvent.SoulJealous, ctx, Tier.Repeatable);
        if (said == null && roll > 0.7f && ctx.NextBoss != null) said = Dialogue.Pick(soul, DialogueEvent.BossAhead, ctx, Tier.Repeatable);
        return said ?? Dialogue.Pick(soul, DialogueEvent.SoulPickup, ctx, Tier.Repeatable);
    }

    IEnumerator GreetThenChoose(Conversation greeting, Element element, List<BoonOffer> offers, System.Action afterPick,
        bool ensureCapstone, Rarity minRarity)
    {
        yield return Sting(element);
        yield return PlayScene(greeting);
        ShowBoons(element, offers, afterPick, ensureCapstone, minRarity);
    }

    void ShowBoons(Element element, List<BoonOffer> offers, System.Action afterPick, bool ensureCapstone, Rarity minRarity)
    {
        ElementArt art = ArtFor(element);
        Sprite[] heldSoul = art?.heldSoul != null ? new[] { art.heldSoul } : art?.soulFrames;
        boonChoice.Show(element, offers, heldSoul, art?.portrait, index =>
        {
            TakeOrReject(offers, index);
            Time.timeScale = 1f;
            afterPick();
        });
        // The tutorial explains the cards over the first boon screen.
        if (tutorial.Active) StartCoroutine(tutorial.BoonsShown());
        // Fate's Thread and Banishing Bell. The new cards replace these in the same list, so the pick reads them.
        List<BoonOffer> Refill()
        {
            var fresh = Boons.Draw(element, 3, loadout, maxLives, ensureCapstone, minRarity, banished);
            if (fresh.Count == 0) return null;
            offers.Clear();
            offers.AddRange(fresh);
            OfferRerolls();
            return offers;
        }
        void OfferRerolls()
        {
            if (redrawsLeft > 0) boonChoice.OfferRedraw(() =>
            {
                redrawsLeft--;
                return Refill();
            });
            if (banishesLeft > 0) boonChoice.OfferBanish(selected =>
            {
                banishesLeft--;
                banished.Add(offers[selected].Boon.Id);
                return Refill();
            });
        }
        OfferRerolls();
    }

    // The soul who catches each element's dead and hands them to Oni (Story/Souls.md).
    static string SoulOf(Element element) => element switch
    {
        Element.Fire => "Akagane",
        Element.Water => "Mizore",
        _ => "Hibiki",
    };

    // The soul's sting as their talk opens: Akagane's war drum and flame, Mizore's ice chimes, Hibiki's drumroll
    // into thunder.
    static string StingFor(Element element) => element switch
    {
        Element.Fire => Sfx.SoulStingFire,
        Element.Water => Sfx.SoulStingFrost,
        _ => Sfx.SoulStingStorm,
    };

    // The sting, then a beat so the first line lands on its tail. Unscaled: the boon screen pauses time.
    IEnumerator Sting(Element element)
    {
        if (!Sfx.Has(StingFor(element))) yield break;
        Sfx.Play(StingFor(element));
        yield return new WaitForSecondsRealtime(0.6f);
    }

    // The boon screen's pick, or nothing when every offer was rejected (X twice); the run goes on either way.
    void TakeOrReject(List<BoonOffer> offers, int index)
    {
        if (index == BoonChoiceUI.Rejected) RunLog.Note($"rejected {string.Join(", ", offers.ConvertAll(o => o.Boon.Name))}");
        else TakeBoon(offers[index]);
    }

    void TakeBoon(BoonOffer offer)
    {
        loadout.Take(offer);
        // The run's first boon gets a word from Poguru once the screen has closed.
        if (!firstBoonTaken && offer.Boon.Kind != BoonKind.Art && !offer.IsUpgrade)
        {
            firstBoonTaken = true;
            StartCoroutine(SoulBark());
        }
        RunLog.Note(offer.IsUpgrade ? $"bead: {offer.Boon.Name} -> {offer.Rarity}"
            : offer.Boon.Kind == BoonKind.Normal ? $"took {offer.Boon.Name} ({offer.Rarity})" : $"took {offer.Boon.Name}");
        float value = offer.Boon.ValueAt(offer.Rarity);
        Color colour = offer.Boon.Kind == BoonKind.Art ? Boons.ArtColor : Boons.Info(offer.Boon.Element).Color;
        Color pillar = Color.Lerp(Color.white, colour, 0.7f);
        Fx.Play(Fx.BoonPillar, ronin.Feet, new Color(pillar.r, pillar.g, pillar.b, 0.75f), offer.Boon.Id == BoonId.TidalRenewal ? 1.4f : 1f);
        switch (offer.Boon.Id)
        {
            case BoonId.TidalRenewal:
                // New hearts arrive full; the ones already lost stay empty.
                int gained = Mathf.Min((int)value, Boons.MaxLives - maxLives);
                maxLives += gained;
                lives += gained;
                ui.SetLives(lives, maxLives);
                break;
            case BoonId.SpiritOffering:
                if (lives < maxLives)
                {
                    lives++;
                    ui.SetLives(lives, maxLives);
                }
                else
                {
                    score += OfferingPoints * (stageIndex + 1);
                    highScore = Mathf.Max(highScore, score);
                    ui.SetScore(score);
                }
                break;
        }
        army.SpeedUpScale = loadout.MarchSpeedUpScale;
        UpdateRoninLook();
        boonChoice.RefreshHud(loadout);
    }

    ElementArt ArtFor(Element element)
    {
        foreach (ElementArt art in elementArt)
        {
            if (art != null && art.element == element) return art;
        }
        return null;
    }

    // His flames and aura follow his Blade boon's element (or, before he has one, the element he has the most boons
    // of); each projectile follows the element in its slot.
    void UpdateRoninLook()
    {
        Element? attack = loadout.SlotElement(BoonSlot.Attack);
        Element? primary = attack ?? loadout.DominantElement;
        CharacterArt look = primary is Element d && ArtFor(d)?.ronin != null ? ArtFor(d).ronin : baseRoninArt;

        Element? special = loadout.SlotElement(BoonSlot.Special);
        Element? parry = loadout.SlotElement(BoonSlot.Parry);
        Sprite slash = attack is Element a ? ArtFor(a)?.slash : null;
        Sprite kunai = attack is Element a1 ? ArtFor(a1)?.kunai : null;
        Sprite shuriken = special is Element s ? ArtFor(s)?.shuriken : null;

        // The animated aura replaces the plain glow when its art exists.
        bool animatedAura = primary is Element a2 && Fx.Has(Fx.Aura(a2));
        Color glowAura = primary is Element glow && !animatedAura ? Boons.Info(glow).Color * new Color(1f, 1f, 1f, 0.45f) : Color.clear;
        ronin.SetLook(look, glowAura,
            slash, kunai, StandInTint(attack, slash), ElementColor(attack),
            shuriken, StandInTint(special, shuriken), ElementColor(special),
            parry is Element p ? Boons.Info(p).Color : Color.white);

        if (primary != shownElement)
        {
            if (aura != null) aura.Stop();
            aura = null;
            if (primary is Element changed)
            {
                // Behind the ronin and wider than him so the flames show round his silhouette; faded so he stays readable.
                aura = Fx.Play(Fx.Aura(changed), ronin.Feet + Vector3.up * auraLift, new Color(1f, 1f, 1f, 0.45f), 1.35f, ronin.transform, ronin.SpriteOrder - 1);
                Color burst = Boons.Info(changed).Color;
                if (Fx.Play(Fx.Impact, ronin.Bounds.center, new Color(burst.r, burst.g, burst.b, 0.7f), 1.3f) == null)
                {
                    ElementFx.Glow(ronin.Bounds.center, Boons.Info(changed).Color, 1f, 4f, 0.5f);
                }
                Debris.BurstSprites(ronin.Bounds.center, ronin.art.deathParticles, Boons.Info(changed).Color, 14, 1f, 6f);
            }
        }
        shownElement = primary;
    }

    static Color? ElementColor(Element? element) => element is Element e ? Boons.Info(e).Color : (Color?)null;

    // Recoloured art needs no tint; without it the default sprite is tinted in the element's colour.
    static Color StandInTint(Element? element, Sprite elementSprite)
    {
        if (element is not Element e || elementSprite != null) return Color.white;
        return Color.Lerp(Color.white, Boons.Info(e).Color, 0.75f);
    }

    void OnArmyDefeated()
    {
        if (state != State.Playing) return;
        if (tutorial.Active && stageIndex == 0 && roundIndex == 0)
        {
            StartCoroutine(TutorialRoundEnd());
            return;
        }
        StartCoroutine(EndRound());
    }

    // The tutorial's first round ends on its spear and heart, still in play so the blow lands, then as any round does.
    IEnumerator TutorialRoundEnd()
    {
        yield return tutorial.SpearAndHeart();
        // A pause opened on the last beat must not skip the round's end; a death (GameOver) does.
        while (state == State.Paused) yield return null;
        if (state == State.Playing) yield return EndRound();
    }

    IEnumerator EndRound()
    {
        RunLog.EndSegment(lives, maxLives, null);
        state = State.Transition;
        ronin.ControlsEnabled = false;
        IceStatue.CrumbleAll();
        yield return GatherDrops();
        yield return GrantReward();

        // The last round leaves only the boss; before that the ronin picks the next round's reward on a path.
        if (roundIndex + 1 >= maxRounds)
        {
            StartCoroutine(StageCleared());
            yield break;
        }
        RewardPath chosen = null;
        yield return ChoosePath(p => chosen = p);
        if (chosen.Reward == PathReward.Boss)
        {
            StartCoroutine(StageCleared());
            yield break;
        }
        pendingReward = chosen.Reward;
        pendingElement = chosen.Element;
        roundIndex++;
        StartCoroutine(BeginRound());
    }

    // The Long Road holds the boss's path back a round per rank.
    int BossPortalAfter => Mathf.Min(bossPortalAfterRound + Oaths.Rank(OathId.LongRoad), maxRounds);

    // The cleared round's reward, given before the paths open: it gathers out of the air in the middle of the field,
    // where the army stood, and is pulled into the ronin before it's given.
    IEnumerator GrantReward()
    {
        PathReward reward = pendingReward;
        // A spring at full health, or a bead or scroll with nothing left to give, falls back to a Lost Melody.
        if (reward == PathReward.Spring && lives >= maxLives) reward = PathReward.Notes;
        if (reward == PathReward.Scroll && Boons.DrawArts(loadout, 1).Count == 0) reward = PathReward.Notes;
        if (reward == PathReward.Bead && Boons.DrawBead(loadout, 1).Count == 0) reward = PathReward.Notes;

        RewardSpirit spirit = RewardSpirit.Spawn(CameraShake.Center, RewardIcon(reward, pendingElement),
            RewardColour(reward, pendingElement), ronin);
        if (tutorial.Active) yield return tutorial.RewardShown();
        yield return new WaitUntil(() => spirit == null);

        bool picked = false;
        switch (reward)
        {
            case PathReward.Soul:
                Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(pendingElement).Color, 1.4f);
                Sfx.Play(Sfx.SoulCollect);
                soulCaught = true;
                // Ancestral Lore: each stage's first soul is Rare or better.
                Rarity floor = roundIndex == 0 && Upgrades.Rank(UpgradeId.AncestralLore) > 0 ? Rarity.Rare : Rarity.Common;
                if (OfferBoons(pendingElement, () => picked = true, false, floor)) yield return new WaitUntil(() => picked);
                break;
            case PathReward.Bead:
                Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.BeadColor, 1.2f);
                Sfx.Play(Sfx.SoulCollect, 1f, 1.1f);
                if (OfferBead(() => picked = true)) yield return new WaitUntil(() => picked);
                break;
            case PathReward.Scroll:
                scrollsDropped++;
                Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.ArtColor, 1.2f);
                Sfx.Play(Sfx.SoulCollect, 1f, 0.8f);
                if (OfferArts(() => picked = true)) yield return new WaitUntil(() => picked);
                break;
            case PathReward.Spring:
                lives = Mathf.Min(lives + 1, maxLives);
                ui.SetLives(lives, maxLives);
                Fx.Play(Fx.BoonPillar, ronin.Feet, RewardColour(reward, pendingElement), 1.2f);
                Sfx.Play(Sfx.SoulCollect, 0.8f, 1.3f);
                yield return new WaitForSeconds(0.6f);
                break;
            case PathReward.Notes:
                int notes = noteCache * (stageIndex + 1);
                Earn(notes);
                Fx.Play(Fx.BoonPillar, ronin.Feet, RewardColour(reward, pendingElement), 1.2f);
                Sfx.Play(Sfx.SoulCollect, 0.9f, 1.2f);
                ui.ShowSubtitle(null, $"+{notes} Spirit Notes", RewardColour(reward, pendingElement), 1.6f);
                yield return new WaitForSeconds(0.6f);
                break;
        }
        state = State.Transition;
        ronin.ControlsEnabled = false;
    }

    const string PathHint = "E / ENTER  to choose";

    // The paths open where the army stood. He walks his row (no attacks); the one he's under lights and a tooltip says
    // what it gives, and E or Enter picks it: its reward floats away up the field and
    // he follows it to the next round, or to the boss.
    IEnumerator ChoosePath(System.Action<RewardPath> onChosen)
    {
        ClearProjectiles();
        var rewards = RollPaths();
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        float y = CameraShake.Center.y + cam.orthographicSize * 0.35f;
        var paths = new List<RewardPath>();
        for (int i = 0; i < rewards.Count; i++)
        {
            float x = CameraShake.Center.x + halfWidth * 1.2f * ((i + 0.5f) / rewards.Count - 0.5f);
            (PathReward reward, Element element) = rewards[i];
            paths.Add(RewardPath.Spawn(new Vector3(x, y, 0f), reward, element, RewardColour(reward, element),
                RewardLabel(reward, element), RewardIcon(reward, element), ui.BodyFont));
        }
        Sfx.Play(Sfx.SoulSpawn, 1f, 0.7f);
        if (tutorial.Active) yield return tutorial.PathsShown();

        ronin.MoveOnly = true;
        ronin.ControlsEnabled = true;
        float readyAt = Time.time + 0.6f;
        RewardPath chosen = null;
        RewardPath described = null;
        bool shownOnce = false;
        while (chosen == null)
        {
            // Only a path he's standing under lights; between them nothing does, and the choose key does nothing.
            RewardPath under = null;
            float best = RewardPath.Reach;
            foreach (RewardPath path in paths)
            {
                float gap = Mathf.Abs(path.transform.position.x - ronin.Bounds.center.x);
                if (gap < best)
                {
                    best = gap;
                    under = path;
                }
            }
            foreach (RewardPath path in paths) path.SetLit(path == under);
            // Between paths, a quiet prompt rides over his head instead.
            Vector3 overHead = new Vector3(ronin.Bounds.center.x, ronin.TopY + 1.4f, 0f);
            if (under != described || !shownOnce)
            {
                described = under;
                shownOnce = true;
                if (under != null) ui.ShowPathInfo(under.TooltipAnchor, RewardDescription(under.Reward), under.Colour, PathHint);
                else ui.ShowPathInfo(overHead, "Walk under a path.", GameUI.MutedColour, "");
            }
            if (under == null) ui.MovePathInfo(overHead);
            if (under != null && Time.time >= readyAt && state == State.Transition && ChooseKey()) chosen = under;
            yield return null;
        }
        ui.HidePathInfo();
        ronin.ControlsEnabled = false;
        ronin.MoveOnly = false;
        var shown = new List<string>();
        foreach (RewardPath path in paths) shown.Add(RewardLabel(path.Reward, path.Element));
        RunLog.Note($"path: {RewardLabel(chosen.Reward, chosen.Element)}  (of {string.Join(" / ", shown)})");
        foreach (RewardPath path in paths)
        {
            if (path != chosen) path.Close();
        }
        // The pick floats away up the field, to come back as the next round's reward; he sets off after it, on to the
        // next round on the same ground, or the boss's path goes on through StageCleared's own travel.
        chosen.Release();
        Sfx.Play(Sfx.SoulSpawn, 1f, 1.2f);
        yield return new WaitForSeconds(0.35f);
        if (chosen.Reward != PathReward.Boss) yield return WalkOn(chosen.transform.position.x);
        onChosen(chosen);
    }

    // What each path gives, for the HUD while he chooses.
    string RewardDescription(PathReward reward) => reward switch
    {
        PathReward.Soul => "Receive a boon from a lost soul.",
        PathReward.Bead => "Raise one of your boons to the next rarity.",
        PathReward.Scroll => "Learn a Sword Art that reshapes your blade.",
        PathReward.Spring => "Restore one lost life.",
        PathReward.Notes => $"Gain {noteCache * (stageIndex + 1)} Spirit Notes to spend at the temple.",
        PathReward.Boss => Stage.boss == BossKind.None ? "Leave this stage behind."
            : "Face the stage's warlord. Felling them grants a rare boon and a Spirit Bead.",
        _ => "",
    };

    // E or Enter (pad A) chooses a path. Not Space or a click: they slash, and a player still swinging as the army
    // falls shouldn't take a path unread.
    static bool ChooseKey()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }

    // After the reward up the field into the dark, and out of it at his place on the row, on the same ground: the cover
    // keeps its damage and the stage's music plays on, unlike TravelTo between maps.
    IEnumerator WalkOn(float towardX)
    {
        Camera cam = Camera.main;
        float top = CameraShake.Center.y + cam.orthographicSize;
        float bottom = CameraShake.Center.y - cam.orthographicSize;
        Vector3 from = ronin.transform.position;
        Coroutine leaving = StartCoroutine(ronin.WalkTo(new Vector3(towardX, top + 2f, from.z), travelWalkSpeed));
        // The fade starts as he nears the top, so he's gone into the dark just as it closes.
        yield return new WaitUntil(() => ronin.transform.position.y > top - 4f);
        yield return ui.FadeTo(1f, travelFadeTime);
        yield return leaving;
        ronin.PlaceAt(new Vector3(ronin.Home.x, bottom - 2f, ronin.Home.z));
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(ui.FadeTo(0f, travelFadeTime));
        yield return ronin.WalkTo(ronin.Home, travelWalkSpeed);
    }

    // The next round's rewards to choose between: no two of a kind, and the boss's path once enough rounds are
    // cleared. Souls are the common pick; springs only show while he's hurt, beads and scrolls only with a use.
    List<(PathReward, Element)> RollPaths()
    {
        var weights = new List<(PathReward reward, float weight)> { (PathReward.Soul, 45f), (PathReward.Notes, 20f) };
        if (Boons.DrawBead(loadout, 1).Count > 0 && Oaths.Rank(OathId.FadedSpirits) == 0) weights.Add((PathReward.Bead, 20f));
        if (lives < maxLives) weights.Add((PathReward.Spring, 10f));
        if (scrollsDropped < maxScrollsPerRun && Boons.DrawArts(loadout, 1).Count > 0) weights.Add((PathReward.Scroll, 5f));

        var picks = new List<(PathReward, Element)>();
        // Third Path opens one more.
        int choices = portalChoices + Upgrades.Rank(UpgradeId.ThirdPath);
        while (picks.Count < choices && weights.Count > 0)
        {
            float total = 0f;
            foreach (var entry in weights) total += entry.weight;
            float roll = Random.value * total;
            int index = 0;
            while (index < weights.Count - 1 && (roll -= weights[index].weight) >= 0f) index++;
            PathReward reward = weights[index].reward;
            weights.RemoveAt(index);
            picks.Add((reward, reward == PathReward.Soul ? PickSoulElement() : Element.Fire));
        }
        if (roundIndex + 1 >= BossPortalAfter) picks.Add((PathReward.Boss, Element.Fire));
        return picks;
    }

    static Color NoteColour => OniColour;
    // The Spirit Notes' icon, looped: the PixelLab art, or a drawn note without it.
    Sprite[] NoteIcon => noteFrames != null && noteFrames.Length > 0 ? noteFrames : new[] { ProceduralSprites.Note(1) };
    static readonly Color SpringColour = new Color(1f, 0.4f, 0.45f);
    static readonly Color BossPortalColour = new Color(0.85f, 0.15f, 0.2f);

    static Color RewardColour(PathReward reward, Element element) => reward switch
    {
        PathReward.Soul => Boons.Info(element).Color,
        PathReward.Bead => Boons.BeadColor,
        PathReward.Scroll => Boons.ArtColor,
        PathReward.Spring => SpringColour,
        PathReward.Boss => BossPortalColour,
        _ => NoteColour,
    };

    string RewardLabel(PathReward reward, Element element) => reward switch
    {
        PathReward.Soul => $"{Boons.Info(element).Name} Soul",
        PathReward.Bead => "Spirit Bead",
        PathReward.Scroll => "Technique Scroll",
        PathReward.Spring => "Healing Spring",
        PathReward.Boss => Stage.boss == BossKind.None ? "Onward" : Stage.boss.ToString(),
        _ => "Lost Melody",
    };

    Sprite[] RewardIcon(PathReward reward, Element element) => reward switch
    {
        PathReward.Soul => ArtFor(element)?.soulFrames,
        PathReward.Scroll => scrollFrames,
        PathReward.Bead => beadFrames,
        PathReward.Notes => NoteIcon,
        PathReward.Spring => springFrames != null && springFrames.Length > 0 ? springFrames : ui.LifeIcon != null ? new[] { ui.LifeIcon } : null,
        PathReward.Boss => BossIconOf(stages[stageIndex].boss),
        _ => null,
    };

    Sprite[] BossIconOf(BossKind boss)
    {
        if (bossIcons == null) return null;
        foreach (BossIcon icon in bossIcons)
            if (icon.boss == boss) return icon.frames;
        return null;
    }

    // What a boon screen holds out: the painting, or the pixel loop without one.
    static Sprite[] Held(Sprite painted, Sprite[] frames) => painted != null ? new[] { painted } : frames;

    // Every cleared stage earns a boon of a random element, then the next stage begins.
    IEnumerator StageCleared()
    {
        state = State.Transition;
        ronin.ControlsEnabled = false;
        yield return new WaitForSeconds(stageClearPause);

        if (Stage.boss != BossKind.None)
        {
            yield return TravelTo(BossMap);
            StartCoroutine(BeginBoss());
        }
        else
        {
            OfferStageBoon();
        }
    }

    // The stage's reward: a Duo or Legendary when one is within reach, like a boss chest evolving a weapon, and
    // never below Rare.
    void OfferStageBoon()
    {
        List<BoonDefinition> capstones = Boons.EligibleCapstones(loadout);
        Element element = PickSoulElement();
        if (capstones.Count > 0)
        {
            BoonDefinition capstone = capstones[Random.Range(0, capstones.Count)];
            element = capstone.Partner is Element partner && Random.value < 0.5f ? partner : capstone.Element;
        }
        Fx.Play(Fx.BoonPillar, ronin.Feet, Boons.Info(element).Color, 1.4f);
        Sfx.Play(Sfx.SoulCollect);
        Rarity floor = Oaths.Rank(OathId.FadedSpirits) >= 2 ? Rarity.Common : Rarity.Rare;
        BossKind? felled = Stage.boss != BossKind.None ? Stage.boss : (BossKind?)null;
        if (!OfferBoons(element, BeadThenNextStage, true, floor, felled)) BeadThenNextStage();
    }

    // A fallen boss also leaves a Spirit Bead, so the stage's reward raises the build as well as widening it.
    // Pond's Blessing adds a second after the first boss.
    void BeadThenNextStage()
    {
        state = State.Transition;
        bool blessed = stageIndex == 0 && Upgrades.Rank(UpgradeId.PondsBlessing) > 0;
        System.Action then = blessed ? () =>
        {
            state = State.Transition;
            if (!OfferBead(NextStage)) NextStage();
        } : NextStage;
        if (!OfferBead(then)) then();
    }
}
