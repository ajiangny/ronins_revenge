using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Hades-style reactive dialogue (Story/README.md, Story/DIALOGUE_RESEARCH.md): many conversations written for
// situations, and the game picks the one that fits what has happened across runs.
// Poguru's: Tutorial is played by id (Dialogue.Find); the rest are his barks and his talk at the temple nest.
// The souls' boon-screen talks beyond SoulPickup (GameManager.SoulTalk): a Legendary or Duo on offer, the boss just
// felled or still ahead, another soul's element leading (SoulJealous), and a line in the temple after a run (SoulHub).
public enum DialogueEvent { BossIntro, BossPhase, BossDefeat, KilledOni, HubReturn, SoulPickup, Tutorial, PoguruRound, PoguruSoul, PoguruBoss, PoguruWake, PoguruTalk,
    Legendary, Duo, BossFelled, BossAhead, SoulJealous, SoulHub }

// Story conversations play once, in the order they're written; Reactive ones answer how things stand and outrank
// the Repeatables, which every event has so nobody ever goes silent.
public enum Tier { Story, Reactive, Repeatable }

public struct DialogueLine
{
    public string Speaker;
    // May hold {deaths} (in words) for a count filled in when it plays.
    public string Text;
}

public sealed class Conversation
{
    public string Id;
    public string Speaker;
    public DialogueEvent Event;
    public Tier Tier;
    public Req[] Requires = Array.Empty<Req>();
    // Runs before it can play again (Reactive and Repeatable).
    public int Cooldown;
    // The boss's own in-game line and voice clip (Boss.Line), spoken first; `Lines` then follow it as replies.
    public BossLine? BuiltIn;
    public DialogueLine[] Lines = Array.Empty<DialogueLine>();

    // Voice clips are Audio/Voice/<Id>.mp3 for the first line and <Id>_<n> for the nth after it. A built-in line is
    // the first, so its replies count from 2.
    public string VoiceId(int line) => line == 0 ? Id : $"{Id}_{line + 1}";
}

// What a conversation can ask about: the save, and this run's soul and boons.
public struct DialogueContext
{
    public SaveData Save;
    public int Run;
    public Element? Element;
    public RoninLoadout Loadout;
    // Lives left right now, and whether this boon screen is a Spirit Offering (souls with nothing left to give).
    public int Lives;
    public bool Offering;
    // The boss just felled, for Poguru's boss-down barks and the souls' boon after it.
    public BossKind? Boss;
    // The boss at the end of the stage being played, before its fight; and the boons on this screen.
    public BossKind? NextBoss;
    public List<BoonId> Offers;
}

// One requirement, with the words the debug view prints for it.
public sealed class Req
{
    public readonly string Label;
    readonly Func<DialogueContext, bool> test;

    public Req(string label, Func<DialogueContext, bool> test)
    {
        Label = label;
        this.test = test;
    }

    public bool Holds(DialogueContext c) => test(c);
}

public static class Dialogue
{
    public static Req Seen(string id) => new Req($"heard {id}", c => c.Save.SeenIn(id) >= 0);
    public static Req NotSeen(string id) => new Req($"not heard {id}", c => c.Save.SeenIn(id) < 0);
    public static Req LastKiller(BossKind boss) => new Req($"killed by {boss} last run", c => !c.Save.lastRunCleared && c.Save.lastKiller == (int)boss);
    public static Req Deaths(int n) => new Req($"{n}+ deaths", c => c.Save.deaths >= n);
    public static Req DeathsTo(BossKind boss, int n) => new Req($"{n}+ deaths to {boss}", c => c.Save.deathsTo[(int)boss] >= n);
    public static Req WinsVs(BossKind boss, int n) => new Req($"{n}+ wins vs {boss}", c => c.Save.winsVs[(int)boss] >= n);
    public static Req OfElement(Element element) => new Req($"a {element} soul", c => c.Element == element);
    public static Req Holds(Element element) => new Req($"holds a {element} boon", c => c.Loadout != null && c.Loadout.ElementCount(element) > 0);
    public static Req RunsSince(string id, int n) => new Req($"{n}+ runs since {id}", c => c.Save.SeenIn(id) >= 0 && c.Run - c.Save.SeenIn(id) >= n);
    public static Req Runs(int n) => new Req($"{n}+ runs", c => c.Save.runsStarted >= n);
    public static Req Clears(int n) => new Req($"{n}+ clears", c => c.Save.runsCleared >= n);
    public static Req LastCleared => new Req("cleared last run", c => c.Save.lastRunCleared);
    public static Req LastDiedToArmy => new Req("killed by the army last run", c => !c.Save.lastRunCleared && c.Save.lastKiller == (int)BossKind.None);
    // The blow that ended the last run (army deaths split by what struck it), how many of them, and runs in a row.
    public static Req DiedTo(DeathCause cause) => new Req($"killed by {cause} last run", c => !c.Save.lastRunCleared && c.Save.lastDeathCause == (int)cause);
    public static Req DeathsBy(DeathCause cause, int n) => new Req($"{n}+ deaths by {cause}", c => c.Save.deathsByCause[(int)cause] >= n);
    public static Req Streak(int n) => new Req($"{n}+ deaths in a row the same way", c => !c.Save.lastRunCleared && c.Save.deathStreak >= n);
    public static Req LivesLeft(int n) => new Req($"{n} lives left", c => c.Lives == n);
    public static Req Offering => new Req("a Spirit Offering", c => c.Offering);
    public static Req HoldsDuo => new Req("holds a Duo", c => c.Loadout != null && HasDuo(c.Loadout));
    public static Req OffersBoon(BoonId boon) => new Req($"offers {boon}", c => c.Offers != null && c.Offers.Contains(boon));
    public static Req NextBoss(BossKind boss) => new Req($"{boss} ahead", c => c.NextBoss == boss);
    // Another soul's element leads: 3+ of its boons, and 2+ more than the speaking soul's (Story/Souls.md#souljealous).
    public static Req Most(Element element) => new Req($"mostly {element}", c => Leads(c, element));
    public static Req Rival => new Req("another soul leads", c => c.Element is Element own
        && Array.Exists((Element[])Enum.GetValues(typeof(Element)), e => e != own && Leads(c, e)));
    public static Req LastRunElement(Element element) => new Req($"mostly {element} last run", c => c.Save.lastRunElement == (int)element);
    public static Req Felled(BossKind boss) => new Req($"just felled {boss}", c => c.Boss == boss);
    // Met in any way: fought, heard, or killed by.
    public static Req Met(BossKind boss) => new Req($"met {boss}", c => c.Save.deathsTo[(int)boss] + c.Save.winsVs[(int)boss] > 0
        || c.Save.seen.Exists(id => id.StartsWith(boss + "_")));

    static bool Leads(DialogueContext c, Element element)
    {
        if (c.Loadout == null || c.Element == element || c.Loadout.DominantElement != element) return false;
        int lead = c.Loadout.ElementCount(element);
        return lead >= 3 && lead >= (c.Element is Element own ? c.Loadout.ElementCount(own) : 0) + 2;
    }

    static bool HasDuo(RoninLoadout loadout)
    {
        foreach (EquippedBoon held in loadout.Passives)
        {
            if (held.Boon.Kind == BoonKind.Duo) return true;
        }
        return false;
    }

    static DialogueLine Say(string speaker, string text) => new DialogueLine { Speaker = speaker, Text = text };

    static Conversation Built(string id, string speaker, DialogueEvent evt, Tier tier, BossLine line, params Req[] requires) =>
        new Conversation { Id = id, Speaker = speaker, Event = evt, Tier = tier, BuiltIn = line, Requires = requires };

    // The boss's own line, then Oni's answer.
    static Conversation Answered(string id, string speaker, DialogueEvent evt, BossLine line, string reply) =>
        new Conversation { Id = id, Speaker = speaker, Event = evt, Tier = Story, BuiltIn = line, Lines = new[] { Say("Oni", reply) } };

    // A written exchange between several speakers.
    static Conversation Talk(string id, string speaker, DialogueEvent evt, Tier tier, Req[] requires, params DialogueLine[] lines) =>
        new Conversation { Id = id, Speaker = speaker, Event = evt, Tier = tier, Requires = requires, Lines = lines };

    static Req[] When(params Req[] requires) => requires;

    static Conversation Cooled(int runs, Conversation c)
    {
        c.Cooldown = runs;
        return c;
    }

    // A soul's reaction that plays once, ever, but still ranks below their first-meeting chain.
    const int Once = 100000;

    static Conversation Line(string id, string speaker, DialogueEvent evt, Tier tier, string text, params Req[] requires) =>
        new Conversation { Id = id, Speaker = speaker, Event = evt, Tier = tier, Requires = requires, Lines = new[] { Say(speaker, text) } };

    // A reaction that rests for `cooldown` runs after it plays, so dying to the same boss again doesn't hear it
    // every time (Hades spaces its reactions the same way).
    static Conversation Reaction(string id, string speaker, DialogueEvent evt, int cooldown, string text, params Req[] requires) =>
        new Conversation { Id = id, Speaker = speaker, Event = evt, Tier = Tier.Reactive, Cooldown = cooldown, Requires = requires,
            Lines = new[] { Say(speaker, text) } };

    const DialogueEvent Intro = DialogueEvent.BossIntro;
    const DialogueEvent Killed = DialogueEvent.KilledOni;
    const DialogueEvent Hub = DialogueEvent.HubReturn;
    const DialogueEvent Phase = DialogueEvent.BossPhase;
    const DialogueEvent Defeat = DialogueEvent.BossDefeat;
    const DialogueEvent Soul = DialogueEvent.SoulPickup;
    const Tier Story = Tier.Story;
    const Tier Repeat = Tier.Repeatable;

    // Every conversation, from the characters' files in Story/. Within a tier the first eligible one plays, so
    // order matters. Keep the texts in step with those files and Tools/generate-voice.mjs.
    public static readonly List<Conversation> All = new List<Conversation>
    {
        // Benibana
        Answered("Benibana_Intro", "Benibana", Intro, BossLine.Intro, "I didn't walk away. Someone cut me loose."),
        Line("Benibana_Intro_Three", "Benibana", Intro, Story,
            "Three times I've killed you. Three bells he swore to silence. Not one has stopped ringing.", DeathsTo(BossKind.Benibana, 3)),
        Line("Benibana_Intro_Bell", "Benibana", Intro, Story,
            "The third bell from the left on his belt. The cracked one. That one is mine.", Seen("Benibana_Intro_Three"), WinsVs(BossKind.Benibana, 2)),
        Reaction("Benibana_Intro_Back", "Benibana", Intro, 3,
            "Back so soon? He promised me a bell for that one. I'm still listening.", LastKiller(BossKind.Benibana)),
        Reaction("Benibana_Intro_Fire", "Benibana", Intro, 4, "Fire? You'd burn my smoke? Cute.", Holds(Element.Fire)),
        Built("Benibana_Intro_Again", "Benibana", Intro, Repeat, BossLine.Intro),
        Line("Benibana_Intro_Dance", "Benibana", Intro, Repeat, "Dance with me, demon."),
        Answered("Benibana_Phase", "Benibana", Phase, BossLine.Phase, "Then dance it."),
        Answered("Benibana_Defeat", "Benibana", Defeat, BossLine.Defeat, "I'll find it."),
        Built("Benibana_Defeat_Again", "Benibana", Defeat, Repeat, BossLine.Defeat),
        Line("Benibana_Killed", "Benibana", Killed, Story, "Listen... Did a bell stop? ...No. Of course not."),
        Line("Benibana_Killed_Curtain", "Benibana", Killed, Repeat, "Curtain."),

        // Utsusemi
        Answered("Utsusemi_Intro", "Utsusemi", Intro, BossLine.Intro, "...That hat."),
        Line("Utsusemi_Intro_Tea", "Utsusemi", Intro, Story,
            "Tick... tock... tea... There was tea, once. Why do I... remember tea?", WinsVs(BossKind.Utsusemi, 2)),
        Line("Utsusemi_Intro_WindDown", "Utsusemi", Intro, Story,
            "Boy. Wind me down. I cannot do it... from in here.", Seen("Utsusemi_Intro_Tea")),
        Reaction("Utsusemi_Intro_Counted", "Utsusemi", Intro, 5,
            "{Deaths} deaths. {Deaths} roads walked again. I counted every one, Oni.", Deaths(10)),
        Built("Utsusemi_Intro_Again", "Utsusemi", Intro, Repeat, BossLine.Intro),
        Line("Utsusemi_Intro_Tick", "Utsusemi", Intro, Repeat, "Tick... tock."),
        Answered("Utsusemi_Phase", "Utsusemi", Phase, BossLine.Phase, "Then I'll stop you."),
        Answered("Utsusemi_Defeat", "Utsusemi", Defeat, BossLine.Defeat, "...You. You sheltered me."),
        Built("Utsusemi_Defeat_Again", "Utsusemi", Defeat, Repeat, BossLine.Defeat),
        Line("Utsusemi_Killed", "Utsusemi", Killed, Story, "One. I will remember the hour."),
        Line("Utsusemi_Killed_Counted", "Utsusemi", Killed, Repeat, "Counted."),

        // Shirogane
        Answered("Shirogane_Intro", "Shirogane", Intro, BossLine.Intro, "...I remember a flute."),
        Line("Shirogane_Intro_Bell", "Shirogane", Intro, Story,
            "She told you which bell is hers. Mine has never rung. I would not kneel.", Seen("Benibana_Intro_Bell")),
        Line("Shirogane_Intro_Oath", "Shirogane", Intro, Story,
            "We swore to end him together, before the coronation. I kept the oath. Now you keep it.", WinsVs(BossKind.Shirogane, 3)),
        Reaction("Shirogane_Intro_Hesitated", "Shirogane", Intro, 3, "You hesitated. You never used to.", LastKiller(BossKind.Shirogane)),
        Built("Shirogane_Intro_Again", "Shirogane", Intro, Repeat, BossLine.Intro),
        Line("Shirogane_Intro_Again2", "Shirogane", Intro, Repeat, "Again, then."),
        Answered("Shirogane_Phase", "Shirogane", Phase, BossLine.Phase, "Then remind me."),
        Answered("Shirogane_Defeat", "Shirogane", Defeat, BossLine.Defeat, "Shirogane... I'm sorry I forgot."),
        Built("Shirogane_Defeat_Again", "Shirogane", Defeat, Repeat, BossLine.Defeat),
        Line("Shirogane_Killed", "Shirogane", Killed, Story, "Not yet, then. I'll wait."),
        Line("Shirogane_Killed_Rise", "Shirogane", Killed, Repeat, "Rise."),

        // Kagetsune
        Answered("Kagetsune_Intro", "Kagetsune", Intro, BossLine.Intro, "It stays silent."),
        Line("Kagetsune_Intro_Broken", "Kagetsune", Intro, Story,
            "You broke me once. I have a thousand more of me.", WinsVs(BossKind.Kagetsune, 1)),
        Line("Kagetsune_Intro_Silent", "Kagetsune", Intro, Story,
            "She never rang. Do you know how that grates, little demon?", Seen("Shirogane_Intro_Oath")),
        Built("Kagetsune_Intro_Again", "Kagetsune", Intro, Repeat, BossLine.Intro),
        Line("Kagetsune_Intro_Kneel", "Kagetsune", Intro, Repeat, "Kneel."),
        Answered("Kagetsune_Phase", "Kagetsune", Phase, BossLine.Phase, "Not any more."),
        Answered("Kagetsune_Defeat", "Kagetsune", Defeat, BossLine.Defeat, "They're done ringing for you."),
        Built("Kagetsune_Defeat_Again", "Kagetsune", Defeat, Repeat, BossLine.Defeat),
        Built("Kagetsune_Killed", "Kagetsune", Killed, Story, BossLine.KilledOni),
        Reaction("Kagetsune_Killed_Duo", "Kagetsune", Killed, 3, "Two stolen souls, holding hands. How touching. They'll ring again.", HoldsDuo),
        Built("Kagetsune_Killed_Again", "Kagetsune", Killed, Repeat, BossLine.KilledOni),
        Line("Kagetsune_Killed_Ring", "Kagetsune", Killed, Repeat, "Ring."),

        // Oni, waking on the hill. He mostly just plays the flute.
        Line("Oni_Hub_First", "Oni", Hub, Story, "...The hill. Her flute. Why am I here?", Deaths(1)),
        Line("Oni_Hub_Smoke", "Oni", Hub, Story, "That smoke. I've breathed it before.", LastKiller(BossKind.Benibana)),
        Line("Oni_Hub_Hat", "Oni", Hub, Story, "That hat... I've drunk tea under that hat.", LastKiller(BossKind.Utsusemi)),
        Line("Oni_Hub_Together", "Oni", Hub, Story, "Together. We were meant to do it together.", Seen("Shirogane_Intro_Oath")),
        Reaction("Oni_Hub_Ringing", "Oni", Hub, 3, "He's still ringing.", LastKiller(BossKind.Kagetsune)),
        new Conversation { Id = "Oni_Hub_Flute", Speaker = "Oni", Event = Hub, Tier = Repeat },

        // Akagane, the first time their soul is caught in a run (Story/Akagane.md).
        Talk("Akagane_Meet_Angry", "Akagane", Soul, Story, When(NotSeen("Akagane_Meet_Angry")),
            Say("Oni", "Warm... Someone in there is still angry."), Say("Akagane", "Angry. Yes. That's the word for it. Hello, Guardsman."), Say("Akagane", "Take the fire and burn them quick. They're only borrowed bodies.")),
        Talk("Akagane_Meet_Copper", "Akagane", Soul, Story, When(Seen("Akagane_Meet_Angry"), RunsSince("Akagane_Meet_Angry", 1)),
            Say("Akagane", "You don't remember me. Akagane. Captain of the vanguard."), Say("Akagane", "They called her silver. They called me copper. They called you demon, and you liked it."), Say("Oni", "...I did.")),
        Talk("Akagane_Meet_Men", "Akagane", Soul, Story, When(Seen("Akagane_Meet_Copper"), RunsSince("Akagane_Meet_Copper", 1), Runs(3)),
            Say("Akagane", "Every samurai you cut down tonight was one of mine. I knew their names."), Say("Akagane", "So cut clean. Don't make them feel it twice.")),
        Talk("Akagane_Meet_Doors", "Akagane", Soul, Story, When(Seen("Akagane_Meet_Men"), RunsSince("Akagane_Meet_Men", 1), Runs(5)),
            Say("Akagane", "You led them up the mountain. I set my men at the hall doors. Facing out, as he ordered."), Say("Akagane", "We were guarding the wrong side of the door, Guardsman. Both of us."), Say("Oni", "You couldn't have known."), Say("Akagane", "Neither could you. That's what I can't forgive.")),
        Talk("Akagane_Meet_Bellringers", "Akagane", Soul, Story, When(Seen("Akagane_Meet_Doors"), RunsSince("Akagane_Meet_Doors", 1), DeathsTo(BossKind.Kagetsune, 1)),
            Say("Akagane", "Those bell-bearers at the shrine were my watch. Good men. Their bells drive the rest."), Say("Akagane", "Silence them first. My men stop running at you when the ringing stops.")),
        Talk("Akagane_Meet_Salute", "Akagane", Soul, Story, When(Seen("Akagane_Meet_Bellringers"), RunsSince("Akagane_Meet_Bellringers", 1), Clears(1)),
            Say("Akagane", "I've decided something. When this is done, I'm going to salute you."), Say("Akagane", "Once. Don't get used to it."), Say("Oni", "When this is done.")),
        Cooled(3, Talk("Akagane_Pickup_DiedBenibana", "Akagane", Soul, Tier.Reactive, When(LastKiller(BossKind.Benibana)),
            Say("Akagane", "Her smoke again? Hold your breath and cut through it. That's all I'd have needed."))),
        Cooled(3, Talk("Akagane_Pickup_DiedUtsusemi", "Akagane", Soul, Tier.Reactive, When(LastKiller(BossKind.Utsusemi)),
            Say("Akagane", "The clockwork had you counted. Stop standing where it expects."))),
        Cooled(3, Talk("Akagane_Pickup_DiedShirogane", "Akagane", Soul, Tier.Reactive, When(LastKiller(BossKind.Shirogane)),
            Say("Akagane", "She beat you. She beat me too, for twenty years. It doesn't get easier. You get better."))),
        Cooled(3, Talk("Akagane_Pickup_DiedKagetsune", "Akagane", Soul, Tier.Reactive, When(LastKiller(BossKind.Kagetsune)),
            Say("Akagane", "You reached him. Further than my whole vanguard got. Now go further."))),
        Cooled(3, Talk("Akagane_Pickup_DiedArmy", "Akagane", Soul, Tier.Reactive, When(LastDiedToArmy),
            Say("Akagane", "My men put you down last night. Don't expect an apology. They were good at their work."))),
        Cooled(Once, Talk("Akagane_Pickup_FirstClear", "Akagane", Soul, Tier.Reactive, When(Clears(1), LastCleared),
            Say("Akagane", "He fell. I felt it. Every bell on him shook."), Say("Akagane", "And here we all are again. So. Again, Guardsman."))),
        Cooled(2, Talk("Akagane_Pickup_Cleared", "Akagane", Soul, Tier.Reactive, When(LastCleared, Seen("Akagane_Pickup_FirstClear")),
            Say("Akagane", "You broke him last night. My men felt the chains slack. Do it again."))),
        Cooled(Once, Talk("Akagane_Pickup_HoldsFrost", "Akagane", Soul, Tier.Reactive, When(Holds(Element.Water)),
            Say("Akagane", "You've been to the physician. She'll have told you to rest. Ignore her."))),
        Cooled(Once, Talk("Akagane_Pickup_HoldsStorm", "Akagane", Soul, Tier.Reactive, When(Holds(Element.Lightning)),
            Say("Akagane", "That's the drummer's spark on you. Speed. Hm. My men marched up the mountain to her beat."))),
        Cooled(Once, Talk("Akagane_Pickup_ManyRuns", "Akagane", Soul, Tier.Reactive, When(Runs(25)),
            Say("Akagane", "My men have marched every night since the Night. So have you. I'd stopped counting. You haven't stopped coming."))),
        Cooled(Once, Talk("Akagane_Pickup_VeryManyRuns", "Akagane", Soul, Tier.Reactive, When(Runs(50)),
            Say("Akagane", "Fifty nights, give or take. I'd have promoted you by now. Don't let it go to your head."))),
        Cooled(Once, Talk("Akagane_Pickup_ManyDeaths", "Akagane", Soul, Tier.Reactive, When(DeathsTo(BossKind.Benibana, 5)),
            Say("Akagane", "Five times the dancer. My vanguard only needed the once."))),
        Talk("Akagane_Pickup_Offering", "Akagane", Soul, Tier.Reactive, When(Offering),
            Say("Akagane", "Nothing left to burn. Fine. Have the warmth, then.")),
        Talk("Akagane_Greet_Lit", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Fire's lit, Guardsman. Use it.")),
        Talk("Akagane_Greet_Bodies", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Burn the bodies. Not the men.")),
        Talk("Akagane_Greet_Vanguard", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Vanguard, forward. That's you now.")),
        Talk("Akagane_Greet_Standing", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Hm. Still standing. Good.")),
        Talk("Akagane_Greet_Waiting", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "My men are waiting. Don't keep them.")),
        Talk("Akagane_Greet_Change", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Take it, before I change my mind.")),
        Talk("Akagane_Greet_Copper", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Copper's still warm.")),
        Talk("Akagane_Greet_Clean", "Akagane", Soul, Repeat, When(),
            Say("Akagane", "Cut clean. Burn quick.")),

        // Mizore, the first time their soul is caught in a run (Story/Mizore.md).
        Talk("Mizore_Meet_Steady", "Mizore", Soul, Story, When(NotSeen("Mizore_Meet_Steady")),
            Say("Oni", "Cold hands. Steady ones."), Say("Mizore", "Hold still. I can't treat a man who flinches."), Say("Mizore", "There. The cold will keep it clean.")),
        Talk("Mizore_Meet_Collarbone", "Mizore", Soul, Story, When(Seen("Mizore_Meet_Steady"), RunsSince("Mizore_Meet_Steady", 1)),
            Say("Mizore", "Mizore. Physician to the court. I set your collarbone when you were sixteen. You were very rude about it."), Say("Oni", "...I don't remember."), Say("Mizore", "No. The coffin takes the small things first. I've been watching it take yours.")),
        Talk("Mizore_Meet_Smoke", "Mizore", Soul, Story, When(Seen("Mizore_Meet_Collarbone"), RunsSince("Mizore_Meet_Collarbone", 1), Runs(3)),
            Say("Mizore", "I knew that smoke by its smell. Datura, and something sweeter."), Say("Mizore", "By the time I'd named it, I couldn't lift my hands. I had never been too slow before.")),
        Talk("Mizore_Meet_Prince", "Mizore", Soul, Story, When(Seen("Mizore_Meet_Smoke"), RunsSince("Mizore_Meet_Smoke", 1), Runs(5)),
            Say("Mizore", "I sat with his father through the whole of his dying. The boy held my sleeve all night."), Say("Mizore", "He asked if it would happen to him. I told him the truth. Everyone dies, my prince. Even emperors."), Say("Mizore", "I think I forged the first piece of that armor, that night.")),
        Talk("Mizore_Meet_Smiths", "Mizore", Soul, Story, When(Seen("Mizore_Meet_Prince"), RunsSince("Mizore_Meet_Prince", 1), DeathsTo(BossKind.Shirogane, 1)),
            Say("Mizore", "Six smiths came to me coughing ash, the year before the coronation. All dead by spring."), Say("Mizore", "I told no one at court. I told Shirogane, when she came asking. Then she went looking for you.")),
        Talk("Mizore_Meet_Oath", "Mizore", Soul, Story, When(Seen("Mizore_Meet_Smiths"), Seen("Shirogane_Intro_Oath"), RunsSince("Mizore_Meet_Smiths", 1)),
            Say("Mizore", "So she gave you the oath back."), Say("Mizore", "She came to me after you'd sworn it. She looked lighter than I'd seen her in years. She said you'd hesitate, and she'd forgive it."), Say("Oni", "Did she."), Say("Mizore", "She did. Now stop hesitating. That's my prescription.")),
        Cooled(3, Talk("Mizore_Pickup_DiedBenibana", "Mizore", Soul, Tier.Reactive, When(LastKiller(BossKind.Benibana)),
            Say("Mizore", "Her smoke again. Shallow breaths, Oni. The dose is in the depth."))),
        Cooled(3, Talk("Mizore_Pickup_DiedUtsusemi", "Mizore", Soul, Tier.Reactive, When(LastKiller(BossKind.Utsusemi)),
            Say("Mizore", "The clockwork keeps perfect time. You don't have to. Be early."))),
        Cooled(3, Talk("Mizore_Pickup_DiedShirogane", "Mizore", Soul, Tier.Reactive, When(LastKiller(BossKind.Shirogane)),
            Say("Mizore", "She cut you to the bone and left every vital alone. That isn't an accident. It's a lesson."))),
        Cooled(3, Talk("Mizore_Pickup_DiedKagetsune", "Mizore", Soul, Tier.Reactive, When(LastKiller(BossKind.Kagetsune)),
            Say("Mizore", "He's never lost anything in his life except his father. Take something from him. He won't know what to do."))),
        Cooled(3, Talk("Mizore_Pickup_DiedArmy", "Mizore", Soul, Tier.Reactive, When(LastDiedToArmy),
            Say("Mizore", "Arrows. Of all things. You were careless, and you know it."))),
        Cooled(Once, Talk("Mizore_Pickup_FirstClear", "Mizore", Soul, Tier.Reactive, When(Clears(1), LastCleared),
            Say("Mizore", "He fell, and he got up again. Like his father, at the end. He'd hate to know that."), Say("Mizore", "Again, then. I'll be here."))),
        Cooled(2, Talk("Mizore_Pickup_Cleared", "Mizore", Soul, Tier.Reactive, When(LastCleared, Seen("Mizore_Pickup_FirstClear")),
            Say("Mizore", "You won last night. Your pulse is calmer for it. Keep it that way."))),
        Cooled(Once, Talk("Mizore_Pickup_HoldsFire", "Mizore", Soul, Tier.Reactive, When(Holds(Element.Fire)),
            Say("Mizore", "You're running hot. That'll be the Captain. Let me take the edge off."))),
        Cooled(Once, Talk("Mizore_Pickup_HoldsStorm", "Mizore", Soul, Tier.Reactive, When(Holds(Element.Lightning)),
            Say("Mizore", "You have Hibiki's spark in you. Mind it. She was always quicker than was good for her."))),
        Cooled(Once, Talk("Mizore_Pickup_ManyRuns", "Mizore", Soul, Tier.Reactive, When(Runs(25)),
            Say("Mizore", "Most patients die once. You've made a career of it."))),
        Cooled(Once, Talk("Mizore_Pickup_VeryManyRuns", "Mizore", Soul, Tier.Reactive, When(Runs(50)),
            Say("Mizore", "I've stopped writing your case down. I ran out of paper in the coffin."))),
        Talk("Mizore_Pickup_LowLives", "Mizore", Soul, Tier.Reactive, When(LivesLeft(1)),
            Say("Mizore", "One life left, and you're still picking fights. Sit. Let me see.")),
        Talk("Mizore_Pickup_Offering", "Mizore", Soul, Tier.Reactive, When(Offering),
            Say("Mizore", "I've no more to teach your hands. I can still close a wound.")),
        Talk("Mizore_Greet_Still", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Hold still.")),
        Talk("Mizore_Greet_Breathe", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Breathe slower. There.")),
        Talk("Mizore_Greet_Clean", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Cold keeps a wound clean.")),
        Talk("Mizore_Greet_Bleeding", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "You're bleeding again. Of course you are.")),
        Talk("Mizore_Greet_Hands", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Steady hands, Oni. Borrow mine.")),
        Talk("Mizore_Greet_Pulse", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Your pulse is racing. Let me slow it.")),
        Talk("Mizore_Greet_Patient", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Another night. Another patient.")),
        Talk("Mizore_Greet_Thanks", "Mizore", Soul, Repeat, When(),
            Say("Mizore", "Don't thank me. Survive.")),

        // Hibiki, the first time their soul is caught in a run (Story/Hibiki.md).
        Talk("Hibiki_Meet_Out", "Hibiki", Soul, Story, When(NotSeen("Hibiki_Meet_Out")),
            Say("Oni", "It wants out as badly as I do."), Say("Hibiki", "Out! Yes! Out, out, out, finally somebody listens!"), Say("Hibiki", "Wait. Oni? It's you! It's really you!")),
        Talk("Hibiki_Meet_Name", "Hibiki", Soul, Story, When(Seen("Hibiki_Meet_Out"), RunsSince("Hibiki_Meet_Out", 1)),
            Say("Hibiki", "You don't know my name, do you."), Say("Oni", "..."), Say("Hibiki", "Hibiki. The festival drum? I followed you to the river every day for a whole summer?"), Say("Hibiki", "It's fine! I'll tell you every time. I'm good at repeating things. I'm a drummer.")),
        Talk("Hibiki_Meet_Procession", "Hibiki", Soul, Story, When(Seen("Hibiki_Meet_Name"), RunsSince("Hibiki_Meet_Name", 1), Runs(3)),
            Say("Hibiki", "I drummed the whole way up the mountain. Everyone kept saying, our Oni sent for us! The whole village, in our best!"), Say("Hibiki", "Ma cried, she was so proud of you."), Say("Hibiki", "Don't. Don't make that face. I'll hit you with a stick.")),
        Talk("Hibiki_Meet_OneBell", "Hibiki", Soul, Story, When(Seen("Hibiki_Meet_Procession"), RunsSince("Hibiki_Meet_Procession", 1), Runs(5)),
            Say("Hibiki", "He gave the court bells. He gave the soldiers bells. He didn't give us any."), Say("Hibiki", "The whole village rings inside the coffin. All of us, one sound. Listen for it when he swings that hammer.")),
        Talk("Hibiki_Meet_Blame", "Hibiki", Soul, Story, When(Seen("Hibiki_Meet_OneBell"), RunsSince("Hibiki_Meet_OneBell", 2)),
            Say("Hibiki", "I told you I don't blame you. That was a lie."), Say("Hibiki", "I do. A little. Every night. And I'd follow you up there again anyway. That's the stupid part."), Say("Oni", "Hibiki."), Say("Hibiki", "...You said my name.")),
        Talk("Hibiki_Meet_River", "Hibiki", Soul, Story, When(Seen("Hibiki_Meet_Blame"), RunsSince("Hibiki_Meet_Blame", 1), Clears(1)),
            Say("Oni", "The drum by the river. You played it until your hands bled. I remember."), Say("Hibiki", "...You remembered on your own."), Say("Hibiki", "Don't you dare forget it again, my lord guardsman.")),
        Cooled(3, Talk("Hibiki_Pickup_DiedBenibana", "Hibiki", Soul, Tier.Reactive, When(LastKiller(BossKind.Benibana)),
            Say("Hibiki", "She got you with the sleepy smoke again? Stay awake! Do what I do. Never stop moving!"))),
        Cooled(3, Talk("Hibiki_Pickup_DiedUtsusemi", "Hibiki", Soul, Tier.Reactive, When(LastKiller(BossKind.Utsusemi)),
            Say("Hibiki", "The tea monk wouldn't hurt anybody. That thing would. Be faster than its ticking!"))),
        Cooled(3, Talk("Hibiki_Pickup_DiedShirogane", "Hibiki", Soul, Tier.Reactive, When(LastKiller(BossKind.Shirogane)),
            Say("Hibiki", "Lady Shirogane beat you? Of course she did. She's Lady Shirogane."))),
        Cooled(3, Talk("Hibiki_Pickup_DiedKagetsune", "Hibiki", Soul, Tier.Reactive, When(LastKiller(BossKind.Kagetsune)),
            Say("Hibiki", "I heard the village ring when he hit you. All of us at once. ...Get up, Oni. Please."))),
        Cooled(3, Talk("Hibiki_Pickup_DiedArmy", "Hibiki", Soul, Tier.Reactive, When(LastDiedToArmy),
            Say("Hibiki", "Just soldiers? Just soldiers got you? Captain Kettle's going to be unbearable."))),
        Cooled(Once, Talk("Hibiki_Pickup_FirstClear", "Hibiki", Soul, Tier.Reactive, When(Clears(1), LastCleared),
            Say("Hibiki", "You did it! You did it, you did it! The whole coffin shook!"), Say("Hibiki", "...And we're all still in here. Oh."), Say("Hibiki", "That's fine! We'll do it again, louder!"))),
        Cooled(2, Talk("Hibiki_Pickup_Cleared", "Hibiki", Soul, Tier.Reactive, When(LastCleared, Seen("Hibiki_Pickup_FirstClear")),
            Say("Hibiki", "Again! Like last night! Ba-dum, down he goes!"))),
        Cooled(Once, Talk("Hibiki_Pickup_HoldsFire", "Hibiki", Soul, Tier.Reactive, When(Holds(Element.Fire)),
            Say("Hibiki", "You smell like smoke. Was that Captain Kettle? He's so grumpy. Here, have something fun."))),
        Cooled(Once, Talk("Hibiki_Pickup_HoldsFrost", "Hibiki", Soul, Tier.Reactive, When(Holds(Element.Water)),
            Say("Hibiki", "Grandmother's been looking after you! She held my wrist, at the end. She's good at that."))),
        Cooled(Once, Talk("Hibiki_Pickup_ManyRuns", "Hibiki", Soul, Tier.Reactive, When(Runs(25)),
            Say("Hibiki", "I've been counting your nights in drumbeats. I've run out of rhythms. I'm making new ones."))),
        Cooled(Once, Talk("Hibiki_Pickup_VeryManyRuns", "Hibiki", Soul, Tier.Reactive, When(Runs(50)),
            Say("Hibiki", "Everyone in the coffin knows your name now. Even the ones who never met you. I told them all."))),
        Talk("Hibiki_Pickup_Offering", "Hibiki", Soul, Tier.Reactive, When(Offering),
            Say("Hibiki", "I'm all out of thunder! Um. Here's a hug instead. It's a soul hug. It counts.")),
        Talk("Hibiki_Greet_Hi", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Oni! Over here! Hi!")),
        Talk("Hibiki_Greet_Faster", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Faster, faster, faster!")),
        Talk("Hibiki_Greet_Thunder", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Ba-dum! That's thunder. That's me.")),
        Talk("Hibiki_Greet_Keep", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Try to keep up, my lord guardsman!")),
        Talk("Hibiki_Greet_Cheer", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "The village is cheering for you. Can you hear it?")),
        Talk("Hibiki_Greet_Stop", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Don't stop! Stopping's the worst.")),
        Talk("Hibiki_Greet_Catch", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Here! Catch!")),
        Talk("Hibiki_Greet_Drumroll", "Hibiki", Soul, Repeat, When(),
            Say("Hibiki", "Drumroll, please... It's me again!")),

        // The souls' other talks (Story/Souls.md, each soul's file): their Legendaries, the Duos (two souls, whichever
        // offers it), the boss felled or ahead, jealousy when another soul's element leads, and a line in the temple.
        Talk("Akagane_Legendary_Wildfire", "Akagane", DialogueEvent.Legendary, Story, When(OffersBoon(BoonId.Wildfire)),
            Say("Akagane", "Wildfire. Every man of the vanguard, burning at once."), Say("Akagane", "Their arrows won't reach you through it. Nothing will.")),
        Talk("Akagane_Legendary_Again", "Akagane", DialogueEvent.Legendary, Repeat, When(OffersBoon(BoonId.Wildfire)),
            Say("Akagane", "All of it, Guardsman. Take all of it.")),
        Talk("Mizore_Legendary_StillWater", "Mizore", DialogueEvent.Legendary, Story, When(OffersBoon(BoonId.StillWater)),
            Say("Mizore", "Still Water. Slow the pulse and a fever loses its hurry."), Say("Mizore", "So does an army.")),
        Talk("Mizore_Legendary_Again", "Mizore", DialogueEvent.Legendary, Repeat, When(OffersBoon(BoonId.StillWater)),
            Say("Mizore", "Slower. Slower. There.")),
        Talk("Hibiki_Legendary_Drum", "Hibiki", DialogueEvent.Legendary, Story, When(OffersBoon(BoonId.RaijinsDrum)),
            Say("Hibiki", "My drum! He threw it in the coffin with us, can you believe that?"), Say("Hibiki", "Every eighth beat's for the village. Count with me!")),
        Talk("Hibiki_Legendary_Again", "Hibiki", DialogueEvent.Legendary, Repeat, When(OffersBoon(BoonId.RaijinsDrum)),
            Say("Hibiki", "Six, seven, EIGHT!")),
        Talk("Duo_ThermalShock_First", "Duo", DialogueEvent.Duo, Story, When(OffersBoon(BoonId.ThermalShock)),
            Say("Mizore", "Heat after cold. It cracks stone, and anything standing in it."), Say("Akagane", "You agree with me? Someone write the date down."), Say("Mizore", "I agree with the fire, Captain. You only carry it.")),
        Talk("Duo_ThermalShock_Arm", "Duo", DialogueEvent.Duo, Story, When(OffersBoon(BoonId.ThermalShock), Seen("Duo_ThermalShock_First"), RunsSince("Duo_ThermalShock_First", 2)),
            Say("Akagane", "My arm still aches where you set it, physician."), Say("Mizore", "You're dead, Captain. It's in your head."), Say("Akagane", "...It's in my head because you set it wrong.")),
        Talk("Duo_ThermalShock_Again", "Duo", DialogueEvent.Duo, Repeat, When(OffersBoon(BoonId.ThermalShock)),
            Say("Akagane", "Freeze them, physician."), Say("Mizore", "Burn them, Captain. Mind your sleeves.")),
        Talk("Duo_PlasmaArc_First", "Duo", DialogueEvent.Duo, Story, When(OffersBoon(BoonId.PlasmaArc)),
            Say("Hibiki", "Captain! You and me! Thunder and fire, like the summer festival!"), Say("Akagane", "Your festival fireworks burned down my supply tent, girl. The night before the coronation."), Say("Hibiki", "It was a beautiful fire."), Say("Akagane", "...It was.")),
        Talk("Duo_PlasmaArc_Pace", "Duo", DialogueEvent.Duo, Story, When(OffersBoon(BoonId.PlasmaArc), Seen("Duo_PlasmaArc_First"), RunsSince("Duo_PlasmaArc_First", 2)),
            Say("Akagane", "My men marched up that mountain to your drum. You set a terrible pace."), Say("Hibiki", "You kept up, though."), Say("Akagane", "We always kept up. That was the trouble.")),
        Talk("Duo_PlasmaArc_Again", "Duo", DialogueEvent.Duo, Repeat, When(OffersBoon(BoonId.PlasmaArc)),
            Say("Hibiki", "Strike and burn!"), Say("Akagane", "You strike. I'll burn.")),
        Talk("Duo_Superconductor_First", "Duo", DialogueEvent.Duo, Story, When(OffersBoon(BoonId.Superconductor)),
            Say("Hibiki", "Grandmother! You're in here too?"), Say("Mizore", "I'm not your grandmother. ...Give me your hand. Steady."), Say("Hibiki", "Your hands were cold that night, too. I didn't mind.")),
        Talk("Duo_Superconductor_Count", "Duo", DialogueEvent.Duo, Story, When(OffersBoon(BoonId.Superconductor), Seen("Duo_Superconductor_First"), RunsSince("Duo_Superconductor_First", 2)),
            Say("Mizore", "I counted your pulse to the end, that night. I lost count at the bells."), Say("Hibiki", "I know. I heard you say steady. It helped."), Say("Mizore", "...Then we'll do it again. Quick, Hibiki. But careful.")),
        Talk("Duo_Superconductor_Again", "Duo", DialogueEvent.Duo, Repeat, When(OffersBoon(BoonId.Superconductor)),
            Say("Mizore", "Quick, but careful."), Say("Hibiki", "Quick AND careful!")),
        Cooled(Once, Talk("Akagane_Felled_Benibana", "Akagane", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Benibana)),
            Say("Akagane", "Down she goes. It was a good dance. I'll only say that once."))),
        Cooled(Once, Talk("Akagane_Felled_Utsusemi", "Akagane", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Utsusemi)),
            Say("Akagane", "Listen. The ticking's stopped. My men owed him that."))),
        Cooled(Once, Talk("Akagane_Felled_Shirogane", "Akagane", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Shirogane)),
            Say("Akagane", "She let you through. She never once let me."))),
        Cooled(Once, Talk("Mizore_Felled_Benibana", "Mizore", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Benibana)),
            Say("Mizore", "She'll mend. They always come back, here. That's the sickness."))),
        Cooled(Once, Talk("Mizore_Felled_Utsusemi", "Mizore", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Utsusemi)),
            Say("Mizore", "Wound down, for a while. Kinder than anything else in these mountains."))),
        Cooled(Once, Talk("Mizore_Felled_Shirogane", "Mizore", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Shirogane)),
            Say("Mizore", "She was smiling under that. She hid pain from me the same way."))),
        Cooled(Once, Talk("Hibiki_Felled_Benibana", "Hibiki", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Benibana)),
            Say("Hibiki", "Ha! ...Sorry. She's sad, isn't she. I shouldn't cheer."))),
        Cooled(Once, Talk("Hibiki_Felled_Utsusemi", "Hibiki", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Utsusemi)),
            Say("Hibiki", "Sleep well, tea monk."))),
        Cooled(Once, Talk("Hibiki_Felled_Shirogane", "Hibiki", DialogueEvent.BossFelled, Tier.Reactive, When(Felled(BossKind.Shirogane)),
            Say("Hibiki", "She smiled at you. Did you see? Right at the end. She smiled!"))),
        Talk("Akagane_Felled_Any", "Akagane", DialogueEvent.BossFelled, Repeat, When(),
            Say("Akagane", "One more down. Forward.")),
        Talk("Mizore_Felled_Any", "Mizore", DialogueEvent.BossFelled, Repeat, When(),
            Say("Mizore", "Done. Let me look at you.")),
        Talk("Hibiki_Felled_Any", "Hibiki", DialogueEvent.BossFelled, Repeat, When(),
            Say("Hibiki", "Down! Who's next?")),
        Talk("Akagane_Ahead_Benibana", "Akagane", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Benibana)),
            Say("Akagane", "The dancer's at the end of this road. Her smoke put my whole vanguard on its knees. Don't breathe it.")),
        Talk("Akagane_Ahead_BenibanaBell", "Akagane", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Benibana), Seen("Benibana_Intro_Bell")),
            Say("Akagane", "She showed you which bell is hers? ...Then I'll stop cursing her. Mostly.")),
        Talk("Akagane_Ahead_Utsusemi", "Akagane", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Utsusemi)),
            Say("Akagane", "My men took that monk. In borrowed bodies, on his orders, but my men. Give the old man an end.")),
        Talk("Akagane_Ahead_Shirogane", "Akagane", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Shirogane)),
            Say("Akagane", "Silver's up there. She beat me every time. Don't you dare lose to her for the wrong reasons.")),
        Talk("Akagane_Ahead_Kagetsune", "Akagane", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Kagetsune)),
            Say("Akagane", "I knelt to him for twenty years. Make him kneel once.")),
        Talk("Mizore_Ahead_Benibana", "Mizore", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Benibana)),
            Say("Mizore", "The dancer came to me for her ankles. I saw the rosary under her sleeve and said nothing. I'm glad I said nothing.")),
        Talk("Mizore_Ahead_Utsusemi", "Mizore", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Utsusemi)),
            Say("Mizore", "A soul wound into gears. I'd like to see how it was done. Then I'd like to undo it.")),
        Talk("Mizore_Ahead_Shirogane", "Mizore", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Shirogane)),
            Say("Mizore", "She guards high on the left. She always did. I stitched that side of her more than once.")),
        Talk("Mizore_Ahead_Kagetsune", "Mizore", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Kagetsune)),
            Say("Mizore", "Remember that he's afraid, Oni. It's all he has ever been.")),
        Talk("Mizore_Ahead_KagetsuneWon", "Mizore", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Kagetsune), WinsVs(BossKind.Kagetsune, 1)),
            Say("Mizore", "He was nine, and holding my sleeve. Now he's holding a thousand of us instead. Let go of him for me.")),
        Talk("Hibiki_Ahead_Benibana", "Hibiki", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Benibana)),
            Say("Hibiki", "The dancer's up ahead! She was so beautiful. Then everyone got so sleepy. ...Oh.")),
        Talk("Hibiki_Ahead_Utsusemi", "Hibiki", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Utsusemi)),
            Say("Hibiki", "That's the tea monk's hat! He let us kids wear it at the well. That's not him in there, Oni. It can't be.")),
        Talk("Hibiki_Ahead_UtsusemiTea", "Hibiki", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Utsusemi), Seen("Utsusemi_Intro_WindDown")),
            Say("Hibiki", "He asked you to wind him down. So he is in there. ...Be gentle. As gentle as you can.")),
        Talk("Hibiki_Ahead_Shirogane", "Hibiki", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Shirogane)),
            Say("Hibiki", "Lady Shirogane let me hold her sword once. Don't tell her I dropped it.")),
        Talk("Hibiki_Ahead_Kagetsune", "Hibiki", DialogueEvent.BossAhead, Story, When(NextBoss(BossKind.Kagetsune)),
            Say("Hibiki", "Our drum against his bells. Let's be louder this time.")),
        Talk("Akagane_Ahead_Any", "Akagane", DialogueEvent.BossAhead, Repeat, When(),
            Say("Akagane", "Warlord ahead. Burn hot.")),
        Talk("Mizore_Ahead_Any", "Mizore", DialogueEvent.BossAhead, Repeat, When(),
            Say("Mizore", "Something heavy ahead. Breathe before you go in.")),
        Talk("Hibiki_Ahead_Any", "Hibiki", DialogueEvent.BossAhead, Repeat, When(),
            Say("Hibiki", "Big one coming! Ba-dum, ba-dum!")),
        Cooled(Once, Talk("Akagane_Jealous_Hibiki", "Akagane", DialogueEvent.SoulJealous, Tier.Reactive, When(Most(Element.Lightning)),
            Say("Akagane", "The drummer's spark, all over you. You'd take a village girl's speed over the vanguard's fire?"), Say("Hibiki", "He would! He did!"), Say("Akagane", "Quiet, girl."))),
        Cooled(Once, Talk("Akagane_Jealous_Mizore", "Akagane", DialogueEvent.SoulJealous, Tier.Reactive, When(Most(Element.Water)),
            Say("Akagane", "You're half frozen, Guardsman. The physician's cold is for people who've given up."), Say("Mizore", "The physician's cold is why you still have two arms, Captain."))),
        Talk("Akagane_Jealous_Storm", "Akagane", DialogueEvent.SoulJealous, Repeat, When(Most(Element.Lightning)),
            Say("Akagane", "Fast won't save you. Fire will.")),
        Talk("Akagane_Jealous_Frost", "Akagane", DialogueEvent.SoulJealous, Repeat, When(Most(Element.Water)),
            Say("Akagane", "Thaw out. Here.")),
        Talk("Akagane_Jealous_Back", "Akagane", DialogueEvent.SoulJealous, Repeat, When(Rival),
            Say("Akagane", "Remembered who your captain is?")),
        Cooled(Once, Talk("Mizore_Jealous_Akagane", "Mizore", DialogueEvent.SoulJealous, Tier.Reactive, When(Most(Element.Fire)),
            Say("Mizore", "You're burning up, Oni. The Captain never knew when to stop either."), Say("Akagane", "And yet I'm the one he keeps choosing."), Say("Mizore", "Fevers are popular, Captain. Briefly."))),
        Cooled(Once, Talk("Mizore_Jealous_Hibiki", "Mizore", DialogueEvent.SoulJealous, Tier.Reactive, When(Most(Element.Lightning)),
            Say("Mizore", "All that speed. Quick is how fevers spread."), Say("Hibiki", "Quick is how you get there first, Grandmother!"), Say("Mizore", "I'm not your grandmother."))),
        Talk("Mizore_Jealous_Fire", "Mizore", DialogueEvent.SoulJealous, Repeat, When(Most(Element.Fire)),
            Say("Mizore", "Too hot. Sit down.")),
        Talk("Mizore_Jealous_Storm", "Mizore", DialogueEvent.SoulJealous, Repeat, When(Most(Element.Lightning)),
            Say("Mizore", "Slow down before you shake apart.")),
        Talk("Mizore_Jealous_Back", "Mizore", DialogueEvent.SoulJealous, Repeat, When(Rival),
            Say("Mizore", "Back for the cold, then. They always come back for the cold.")),
        Cooled(Once, Talk("Hibiki_Jealous_Akagane", "Hibiki", DialogueEvent.SoulJealous, Tier.Reactive, When(Most(Element.Fire)),
            Say("Hibiki", "You picked Captain Kettle? Over me? He doesn't even smile!"), Say("Akagane", "I smile when it's earned."), Say("Hibiki", "When was the last time?"), Say("Akagane", "...Take her boon, Guardsman."))),
        Cooled(Once, Talk("Hibiki_Jealous_Mizore", "Hibiki", DialogueEvent.SoulJealous, Tier.Reactive, When(Most(Element.Water)),
            Say("Hibiki", "You're all frosty and careful. Grandmother's made you sit still, hasn't she."), Say("Mizore", "Someone has to."), Say("Hibiki", "Not tonight!"))),
        Talk("Hibiki_Jealous_Fire", "Hibiki", DialogueEvent.SoulJealous, Repeat, When(Most(Element.Fire)),
            Say("Hibiki", "Fire's so slow. Watch this instead!")),
        Talk("Hibiki_Jealous_Frost", "Hibiki", DialogueEvent.SoulJealous, Repeat, When(Most(Element.Water)),
            Say("Hibiki", "Wake up, frosty! Thunder's here!")),
        Talk("Hibiki_Jealous_Back", "Hibiki", DialogueEvent.SoulJealous, Repeat, When(Rival),
            Say("Hibiki", "You came back to me! I knew it. Mostly knew it.")),
        Talk("Akagane_Hub_First", "Akagane", DialogueEvent.SoulHub, Story, When(LastRunElement(Element.Fire)),
            Say("Akagane", "So this is where you go. A temple pool and a flute. I always wondered.")),
        Cooled(4, Talk("Akagane_Hub_Army", "Akagane", DialogueEvent.SoulHub, Tier.Reactive, When(LastRunElement(Element.Fire), LastDiedToArmy),
            Say("Akagane", "Up. My men are already marching."))),
        Talk("Akagane_Hub_Rise", "Akagane", DialogueEvent.SoulHub, Repeat, When(LastRunElement(Element.Fire)),
            Say("Akagane", "On your feet, Guardsman.")),
        Talk("Mizore_Hub_First", "Mizore", DialogueEvent.SoulHub, Story, When(LastRunElement(Element.Water)),
            Say("Mizore", "Her flute, on the altar. You still look at it like it might break. Good.")),
        Cooled(4, Talk("Mizore_Hub_Kagetsune", "Mizore", DialogueEvent.SoulHub, Tier.Reactive, When(LastRunElement(Element.Water), LastKiller(BossKind.Kagetsune)),
            Say("Mizore", "You'll live. That's my professional opinion. It's been wrong every night so far."))),
        Talk("Mizore_Hub_Rest", "Mizore", DialogueEvent.SoulHub, Repeat, When(LastRunElement(Element.Water)),
            Say("Mizore", "Rest. Then go again.")),
        Talk("Hibiki_Hub_First", "Hibiki", DialogueEvent.SoulHub, Story, When(LastRunElement(Element.Lightning)),
            Say("Hibiki", "You can see the village roofs from up here! ...Well. Where they were.")),
        Cooled(4, Talk("Hibiki_Hub_Song", "Hibiki", DialogueEvent.SoulHub, Tier.Reactive, When(LastRunElement(Element.Lightning)),
            Say("Hibiki", "Play the river song! ...No? Fine. I'll hum it."))),
        Talk("Hibiki_Hub_Up", "Hibiki", DialogueEvent.SoulHub, Repeat, When(LastRunElement(Element.Lightning)),
            Say("Hibiki", "Up, up, up!")),

        // Poguru, the Lost Dragon in Oni's left wisp (Story/Poguru.md). The tutorial's lines are played by id.
        Tut("Poguru_Tut_Wake", P("ONI! ONI! WAKE UP! Scary dead soldiers are coming!"), O("...Who are you."),
            P("Poguru! The Lost Dragon! I live in your wisp. Rent-free!"), P("No time! Do you remember how to fight?")),
        Tut("Poguru_Tut_Again", P("Oni! Wake up! Soldiers! Again! Want the lesson again? I LOVE the lesson.")),
        Tut("Poguru_Tut_Yay", P("Yay! Stay close. They're coming!")),
        Tut("Poguru_Tut_Skip", P("Okay okay okay! I'll be in your wisp. Cheering. Quietly. Mostly.")),
        // The drills: five soldiers find Oni before the army. Their lines hang over them in the field; his frozen
        // yells sit beside Oni with the keys, which he never says.
        Tut("Tut_Soldiers", S("There he is... the Emperor's runaway dog."), S("The coffin wants its demon back. Come... quietly."),
            O("...No."), P("Eep! Five of them! Okay, okay, one at a time!")),
        Tut("Tut_Slash_Taunt", S("Join us, traitor!")),
        Tut("Poguru_Tut_SlashIt", P("An arrow! SLASH it! Your cut breaks it clean in half!")),
        Tut("Poguru_Tut_TakeHim", P("Now take him down!")),
        Tut("Tut_Slash_Fall", S("Not... again...!")),
        Tut("Tut_Charge_Taunt", S("Drag him down! Together!")),
        Tut("Poguru_Tut_ShurikenIt", P("Two in a row! Throw your SHURIKEN, it flies right through them both!")),
        Tut("Poguru_Tut_Stopped", P("Stopped in their tracks! Ha!")),
        Tut("Tut_Spear_Taunt", S("Let's see you cut THIS. Blessed steel from the Emperor's own forge!")),
        Tut("Poguru_Tut_ParryIt", P("That spear's cursed! Kagetsune's blacksmiths forge them with nasty magic. You can't cut it or break it. PARRY IT!")),
        Tut("Tut_Flee", S("Th-that's not natural! CAPTAAAIN!"), S("Wait for meee!"), P("Hehe! Run away! ...Wait. Where are they running TO?")),
        // The army they fetched: he guards it to the end, crosser by the bonk, and bursts if it reaches the cover.
        Tut("Poguru_Tut_Backup", P("Uh oh. He brought a LOT of backup."), O("...Good."),
            P("Okay! I'll bonk every arrow I can. I'm a very good bonker. You do the rest!")),
        // Partway through the round he fills the ultimate's medallion and holds the field for Q.
        Tut("Poguru_Tut_Ultimate", P("You're doing great! Here, have some of my soul-fire!")),
        Tut("Poguru_Tut_UltimateIt", P("Your medallion's full! Let your SHADOW out!")),
        Tut("Poguru_Tut_Clone", P("Two Onis! It copies everything you do. Just weaker. Like me!")),
        Tut("Poguru_Block_1", P("Bonk! Got it!")),
        Tut("Poguru_Block_3", P("Ow. Okay. Still fine!")),
        Tut("Poguru_Block_6", P("Oni, you can ALSO move, you know!")),
        Tut("Poguru_Block_10", P("My HEAD is not a SHIELD!")),
        Tut("Poguru_Block_15", P("I'M BONKING SO HARD RIGHT NOW!")),
        Tut("Poguru_Block_20", P("Oni! Swing the sword! The SWORD!")),
        Tut("Poguru_Block_30", P("My head is RINGING!")),
        Tut("Poguru_Block_45", P("I am going to EXPLODE!")),
        Tut("Poguru_Block_More", P("BONK! BONK! BOOOONK!")),
        Tut("Poguru_Rage_Charge", P("THAT'S IT! THAT IS IT!! RAAAAAGH!!!")),
        Tut("Poguru_Tut_Raged", P("ONI!! WERE YOU NAPPING?!"), P("I did EVERYTHING! I'm a SUPPORT dragon! SUPPORT!"),
            O("...You were doing fine."), P("HMPH!")),
        Tut("Poguru_Tut_Win", P("We did it! Victory wiggle! Wiggle wiggle wiggle...")),
        Tut("Poguru_Tut_LookOut", P("LOOK OUT!!!")),
        Tut("Poguru_Tut_Hearts", P("Waaah! It went right through me! Are you okay?!"), O("...Fine."),
            P("Those hearts in the corner are your lives. Lose them all and it's back to the temple. It's damp there."),
            P("Good news! I've been saving a heart soul. For emergencies. This is an emergency!"),
            P("Hearts fall out of broken cover sometimes. Once in a blue moon a soldier drops one too!")),
        Tut("Poguru_Tut_Collect", P("Go get it! It's on your row!")),
        Tut("Poguru_Tut_Collected", P("Yummy! I mean... healthy!")),
        Tut("Poguru_Tut_Reward", P("Ooooo! Pretty cool soul!"), P("Wait... it looks like someone familiar...")),
        Tut("Poguru_Tut_Boons", P("Souls lend you their power. That's a boon!"), P("Pick ONE. Look them all over, then take the one you like best!"),
            P("They change your slash, shuriken or parry. Or pick the shiniest. That's what I'd do.")),
        Tut("Poguru_Tut_Paths", P("Paths! Each leads to your next fight. What's floating in it is your prize for winning."),
            P("Walk under the one you want, and off we go! After a few rounds, one of them leads to the boss!")),
        Tut("Poguru_Tut_Done", P("Looks like you DO remember how to fight!"), P("Let's hurry and free everyone!!"),
            O("...Back in the wisp."), P("Going! Shutting up! ...Mostly!")),

        // His warnings in Utsusemi's fight, called as he flies out to point the way (Boss.PoguruWarn).
        // Each is written a few ways (_B, _C), and he only says them now and then (GameManager.WarningSpoken).
        Tut("Poguru_Utsu_Sword", P("Laser! Oni, hurry! Hide behind the sword!")),
        Tut("Poguru_Utsu_Sword_B", P("Behind the sword! Quick quick quick!")),
        Tut("Poguru_Utsu_Sword_C", P("Big shiny laser! Get behind the sword!")),
        Tut("Poguru_Utsu_Reclaim", P("Move out the way!")),
        Tut("Poguru_Utsu_Reclaim_B", P("Out the way! It's coming!")),
        Tut("Poguru_Utsu_Reclaim_C", P("Move move MOVE!")),
        Tut("Poguru_Utsu_Rock", P("That's a BIG one! Behind a rock, quick!")),
        Tut("Poguru_Utsu_Rock_B", P("Hide behind a rock! Hurry!")),
        Tut("Poguru_Utsu_Rock_C", P("Rock! Rock! Get behind a rock!")),

        // His barks: a line out of the wisp, then back in.
        Line("Poguru_Round_Go", Pog, DialogueEvent.PoguruRound, Repeat, "Go go go!"),
        Line("Poguru_Round_Snacks", Pog, DialogueEvent.PoguruRound, Repeat, "More of them! Do they ever bring snacks?"),
        Line("Poguru_Round_Wave", Pog, DialogueEvent.PoguruRound, Repeat, "Hi, army! He's going to beat you now! ...Sorry!"),
        Line("Poguru_Round_Breathe", Pog, DialogueEvent.PoguruRound, Repeat, "Deep breaths! Like me! Hff hff hff."),
        Line("Poguru_Round_Count", Pog, DialogueEvent.PoguruRound, Repeat, "I counted them. There's... a lot."),
        Line("Poguru_Soul_Shiny", Pog, DialogueEvent.PoguruSoul, Repeat, "Shiny! Can I hold it? No? Okay."),
        Line("Poguru_Soul_Thanks", Pog, DialogueEvent.PoguruSoul, Repeat, "A friend from the coffin! Say thank you, Oni!"),
        Line("Poguru_Soul_Glow", Pog, DialogueEvent.PoguruSoul, Repeat, "You're glowing! I'm glowing too. Solidarity."),
        Line("Poguru_Boss_Benibana", Pog, DialogueEvent.PoguruBoss, Story, "Smoke lady's gone! I kept one petal.",
            Felled(BossKind.Benibana), WinsVs(BossKind.Benibana, 1)),
        Line("Poguru_Boss_Utsusemi", Pog, DialogueEvent.PoguruBoss, Story, "The ticking stopped! Is it lunchtime?",
            Felled(BossKind.Utsusemi), WinsVs(BossKind.Utsusemi, 1)),
        Line("Poguru_Boss_Shirogane", Pog, DialogueEvent.PoguruBoss, Story, "She looked so sad... Wisps can't cry. I checked.",
            Felled(BossKind.Shirogane), WinsVs(BossKind.Shirogane, 1)),
        Line("Poguru_Boss_Won", Pog, DialogueEvent.PoguruBoss, Repeat, "WE WON! I mean, you won. I helped. Emotionally."),
        Line("Poguru_Boss_Wiggle", Pog, DialogueEvent.PoguruBoss, Repeat, "Victory wiggle! ...No? Okay."),
        Line("Poguru_Wake_First", Pog, DialogueEvent.PoguruWake, Story, "Back in the damp temple. ...But I like that you're here.", Deaths(1)),
        // His roasts of whatever struck the last blow, ahead of the gentler wake lines. Each cause has a few, resting
        // between runs, so dying the same way again gets the next one; now and then Oni answers back.
        Roast("Poguru_Roast_StreakFive", 4, When(Streak(5)), P("Five in a row! The same way! Is this a plan? Is it a secret plan?"),
            O("...Yes."), P("Oh! ...Oh no. It isn't, is it.")),
        Roast("Poguru_Roast_StreakThree", 3, When(Streak(3)), P("Three times. Same way. In a row. I'm making a chart."), O("Don't."),
            P("It has STICKERS.")),
        Roast("Poguru_Roast_ArrowsTen", Once, When(DiedTo(DeathCause.Arrow), DeathsBy(DeathCause.Arrow, 10)),
            P("That's ten arrows now. I've started naming them. That one was Pointy Jiro.")),
        Roast("Poguru_Roast_InvasionFive", Once, When(DiedTo(DeathCause.Invasion), DeathsBy(DeathCause.Invasion, 5)),
            P("Five times they've marched all the way down to you. Their poor feet must be SO tired."), O("So am I.")),
        Roast("Poguru_Roast_Arrow", 3, When(DiedTo(DeathCause.Arrow)), P("An ARROW? The pointy stick? The thing you cut in half for FUN?")),
        Roast("Poguru_Roast_ArrowSlash", 3, When(DiedTo(DeathCause.Arrow)), P("You know you can slash those, right? Swish! Snap! Like that!"),
            O("...I know."), P("Do you, though?")),
        Roast("Poguru_Roast_ArrowOne", 3, When(DiedTo(DeathCause.Arrow)), P("One little arrow. Out of a thousand. You found the ONE."), O("Hmph.")),
        Roast("Poguru_Roast_Spear", 3, When(DiedTo(DeathCause.Spear)), P("The big red spear! The one I TOLD you about! You PARRY those! It was in the lesson!")),
        Roast("Poguru_Roast_SpearCatch", 3, When(DiedTo(DeathCause.Spear)), P("Were you trying to catch it? With your face?"), O("...It was fast."),
            P("It's a SPEAR, Oni. Fast is its whole job.")),
        Roast("Poguru_Roast_SpearRed", 3, When(DiedTo(DeathCause.Spear)), P("Glowing. Red. Spear. That's three warnings in one!"), O("Tch.")),
        Roast("Poguru_Roast_Ghost", 3, When(DiedTo(DeathCause.GhostArrow)), P("The ghosty arrows go right through cover! You hid behind a rock from a GHOST.")),
        Roast("Poguru_Roast_GhostHide", 3, When(DiedTo(DeathCause.GhostArrow)), P("You hid SO well! From the wrong thing!"), O("...I noticed.")),
        Roast("Poguru_Roast_Diver", 3, When(DiedTo(DeathCause.Diver)), P("A soldier jumped on you. He just... jumped. Like a big bony frog!")),
        Roast("Poguru_Roast_DiverHug", 3, When(DiedTo(DeathCause.Diver)), P("He only wanted a hug, Oni. A deadly, bony hug."),
            O("Then you hug him next time."), P("...Fair.")),
        Roast("Poguru_Roast_Invasion", 3, When(DiedTo(DeathCause.Invasion)), P("They walked. They just WALKED to you. Very slowly. For a very long time.")),
        Roast("Poguru_Roast_InvasionRow", 3, When(DiedTo(DeathCause.Invasion)), P("Thin out the bottom row! I say it EVERY time! It's on my tips page!"),
            O("...You have a tips page?"), P("I have a TIPS PAGE!")),
        Roast("Poguru_Roast_InvasionNose", 3, When(DiedTo(DeathCause.Invasion)), P("Were you napping? They marched right up to your nose!"),
            O("I was awake."), P("Your nose wasn't.")),
        Roast("Poguru_Roast_Benibana", 3, When(LastKiller(BossKind.Benibana)), P("You fell asleep in the middle of a sword fight! Napping is MY thing!")),
        Roast("Poguru_Roast_BenibanaDance", 3, When(LastKiller(BossKind.Benibana)), P("She danced. You lost. To a DANCE."), O("...It was a good dance.")),
        Roast("Poguru_Roast_Utsusemi", 3, When(LastKiller(BossKind.Utsusemi)), P("Hide behind the rock! I said ROCK! You hid behind... air!"),
            O("The rock fell apart."), P("So you hide behind a DIFFERENT rock!")),
        Roast("Poguru_Roast_UtsusemiPoint", 3, When(LastKiller(BossKind.Utsusemi)), P("Big shiny laser. I pointed. I flew out and POINTED, Oni."), O("Hmph.")),
        Roast("Poguru_Roast_Shirogane", 3, When(LastKiller(BossKind.Shirogane)), P("She beat you with a sword! YOU have a sword! It's practically the same sword!")),
        Roast("Poguru_Roast_ShiroganeWolf", 3, When(LastKiller(BossKind.Shirogane)), P("Even the wolf was winning. The WOLF, Oni. It doesn't have thumbs!"), O("Tch.")),
        Roast("Poguru_Roast_Kagetsune", 3, When(LastKiller(BossKind.Kagetsune)), P("So close! But his hammer is SO big. Like, unfair big."),
            O("I'll get under it."), P("Good thing you're short!")),
        Roast("Poguru_Roast_KagetsuneBells", 3, When(LastKiller(BossKind.Kagetsune)), P("All those bells! I couldn't even hear myself cheer for you!")),
        Reaction("Poguru_Wake_Army", Pog, DialogueEvent.PoguruWake, 3, "Those soldiers were VERY pointy today.", LastDiedToArmy),
        Reaction("Poguru_Wake_Benibana", Pog, DialogueEvent.PoguruWake, 3, "Her smoke made me sneeze.", LastKiller(BossKind.Benibana)),
        Reaction("Poguru_Wake_Utsusemi", Pog, DialogueEvent.PoguruWake, 3, "Tick, tock, ow.", LastKiller(BossKind.Utsusemi)),
        Reaction("Poguru_Wake_Shirogane", Pog, DialogueEvent.PoguruWake, 3, "She hits SO hard.", LastKiller(BossKind.Shirogane)),
        Reaction("Poguru_Wake_Kagetsune", Pog, DialogueEvent.PoguruWake, 3, "I hid in your wisp the whole time. I'm not proud.", LastKiller(BossKind.Kagetsune)),
        Line("Poguru_Wake_Up", Pog, DialogueEvent.PoguruWake, Repeat, "Up you get! The pool's warm. Ish."),
        Line("Poguru_Wake_Hug", Pog, DialogueEvent.PoguruWake, Repeat, "Wisp hug! Very warm. Very small."),
        // Subtitle only: the count changes, so it can't be recorded.
        Line("Poguru_Wake_Count", Pog, DialogueEvent.PoguruWake, Repeat, "That's {deaths}! I'm keeping count."),

        // His talk at the temple nest, once a visit.
        Talk("Poguru_Talk_Who", Pog, PogTalk, Story, When(), O("Where did you come from."),
            P("Your wisp! The left one. It's cosy. I think I was always there, I just woke up late."),
            P("I'm a dragon! Probably. A small one. Lost. That's my title!")),
        Talk("Poguru_Talk_Cape", Pog, PogTalk, Story, When(Seen("Poguru_Talk_Who"), RunsSince("Poguru_Talk_Who", 1)), O("That red cloth."),
            P("My cape! It smells like festival lanterns. Do you know why?"), O("...No.")),
        Talk("Poguru_Talk_Remember", Pog, PogTalk, Story, When(Runs(3)),
            P("Every time you wake up, you forget a little. So I remember for you!"),
            P("Mostly the wrong things. Did you used to like plums?"), O("...I did.")),
        Talk("Poguru_Talk_Silver", Pog, PogTalk, Story, When(Met(BossKind.Shirogane)),
            P("When the silver lady's near, your wisps go all warm. Is she a friend?"), O("...She was.")),
        Talk("Poguru_Talk_Dawn", Pog, PogTalk, Story, When(Clears(1)),
            P("The sun! I'd never seen it! It's very bright. I hid. But I peeked!")),
        // He does Oni's voice at him (voiced as a deep, deadpan impression).
        Talk("Poguru_Talk_Impression", Pog, PogTalk, Story, When(Runs(4)), P("We should name the pool! I'm thinking... Splashy."), O("...No."),
            P("'...No.' 'Hmph.' 'Back in the wisp.' Ooh, look at me, I'm Oni, I'm all brooding."), O("I don't sound like that."),
            P("'I don't sound like that.'"), O("Tch."), P("Splashy it is!")),
        Talk("Poguru_Talk_ImpressionNest", Pog, PogTalk, Repeat, When(Seen("Poguru_Talk_Impression")),
            P("Can my nest be bigger? Pleeease?"), O("No."), P("'No.' Hehe. Still got it.")),
        Talk("Poguru_Talk_Nap", Pog, PogTalk, Repeat, When(),
            P("I was napping. In your wisp. Which is in you. So technically you were napping too.")),
        Talk("Poguru_Talk_Rawr", Pog, PogTalk, Repeat, When(), P("Rawr. That's my fierce face. Was it fierce? Don't answer.")),
        Talk("Poguru_Talk_GhostFire", Pog, PogTalk, Repeat, When(), P("I tried eating a ghost-fire. It tasted like Tuesday.")),
        Talk("Poguru_Talk_Flute", Pog, PogTalk, Repeat, When(), P("Play the flute later? The wisps dance when you do."), O("...Maybe.")),
    };

    const string Pog = "Poguru";
    const DialogueEvent PogTalk = DialogueEvent.PoguruTalk;
    static DialogueLine P(string text) => Say(Pog, text);
    static DialogueLine O(string text) => Say("Oni", text);
    // The tutorial's soldiers, in the soldier voice (SOLDIER in Tools/generate-voice.mjs).
    public const string Soldier = "Ashigaru";
    static DialogueLine S(string text) => Say(Soldier, text);

    // A roast at the temple, resting `cooldown` runs after it plays.
    static Conversation Roast(string id, int cooldown, Req[] requires, params DialogueLine[] lines) =>
        Cooled(cooldown, Talk(id, Pog, DialogueEvent.PoguruWake, Tier.Reactive, requires, lines));

    // A tutorial conversation: never picked, only played by id.
    static Conversation Tut(string id, params DialogueLine[] lines) => Talk(id, Pog, DialogueEvent.Tutorial, Repeat, When(), lines);

    // A conversation by its id, recorded as heard when `mark` (so the Story ones it unlocks follow on); null if unwritten.
    public static Conversation Find(string id, bool mark = true)
    {
        Conversation found = All.Find(c => c.Id == id);
        if (found != null && mark)
        {
            global::Save.Data.MarkSeen(id, global::Save.Run);
            global::Save.Write();
        }
        return found;
    }

    public static DialogueContext Context(RoninLoadout loadout = null, Element? element = null, int lives = 0, bool offering = false,
        BossKind? boss = null, BossKind? nextBoss = null, List<BoonId> offers = null) =>
        new DialogueContext { Save = Save.Data, Run = Save.Run, Loadout = loadout, Element = element, Lives = lives, Offering = offering, Boss = boss,
            NextBoss = nextBoss, Offers = offers };

    // Why a conversation can't play now, or null if it can. No dice are rolled here, so the answer is repeatable.
    static string Blocked(Conversation c, DialogueContext ctx)
    {
        int heard = ctx.Save.SeenIn(c.Id);
        if (c.Tier == Story && heard >= 0) return $"already heard (run {heard})";
        if (c.Cooldown > 0 && heard >= 0 && ctx.Run - heard < c.Cooldown) return $"cooling down (heard run {heard}, every {c.Cooldown} runs)";
        foreach (Req req in c.Requires)
        {
            if (!req.Holds(ctx)) return "needs " + req.Label;
        }
        return null;
    }

    // The conversation for this moment: the best tier with anything eligible; within it the first Story or Reactive
    // one written, or a Repeatable drawn from a bag that refills once all of them have played. It's recorded as
    // heard. Null when nothing is written for this speaker and event.
    // Tiers outside best..worst are left out, so a caller can try one event's Story lines before another's.
    public static Conversation Pick(string speaker, DialogueEvent evt, DialogueContext ctx, Tier best = Story, Tier worst = Repeat)
    {
        var eligible = All.FindAll(c => c.Speaker == speaker && c.Event == evt && Blocked(c, ctx) == null);
        foreach (Tier tier in new[] { Story, Tier.Reactive, Repeat })
        {
            if (tier < best || tier > worst) continue;
            var options = eligible.FindAll(c => c.Tier == tier);
            if (options.Count == 0) continue;
            Conversation chosen = tier == Repeat ? FromBag(options, ctx.Save) : options[0];
            ctx.Save.MarkSeen(chosen.Id, ctx.Run);
            global::Save.Write();
            return chosen;
        }
        return null;
    }

    static Conversation FromBag(List<Conversation> options, SaveData save)
    {
        var fresh = options.FindAll(c => !save.shuffleBag.Contains(c.Id));
        if (fresh.Count == 0)
        {
            foreach (Conversation c in options) save.shuffleBag.Remove(c.Id);
            fresh = options;
        }
        Conversation chosen = fresh[UnityEngine.Random.Range(0, fresh.Count)];
        save.shuffleBag.Add(chosen.Id);
        return chosen;
    }

    // The debug view (F7): every conversation for this speaker and event, and whether it could play now.
    public static string Explain(string speaker, DialogueEvent evt, DialogueContext ctx)
    {
        var text = new StringBuilder($"{speaker} · {evt} (run {ctx.Run}, {ctx.Save.deaths} deaths, {ctx.Save.runsCleared} clears)\n");
        foreach (Conversation c in All)
        {
            if (c.Speaker != speaker || c.Event != evt) continue;
            string blocked = Blocked(c, ctx);
            string bag = c.Tier == Repeat && ctx.Save.shuffleBag.Contains(c.Id) ? ", played from the bag" : "";
            text.Append($"  {(blocked == null ? "OK " : "-- ")}{c.Tier,-10} {c.Id}  {blocked ?? "eligible"}{bag}\n");
        }
        return text.ToString();
    }

    // Fills a line's counts in words, so the subtitle reads as speech.
    public static string Fill(string text, DialogueContext ctx) =>
        text.Replace("{Deaths}", Capitalise(Words(ctx.Save.deaths))).Replace("{deaths}", Words(ctx.Save.deaths));

    static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    static readonly string[] Ones = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
    static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

    static string Words(int n)
    {
        if (n < 20) return Ones[Mathf.Max(0, n)];
        if (n < 100) return Tens[n / 10] + (n % 10 > 0 ? "-" + Ones[n % 10] : "");
        return n.ToString();
    }
}
