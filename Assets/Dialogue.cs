using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Hades-style reactive dialogue (Story/README.md, Story/DIALOGUE_RESEARCH.md): many conversations written for
// situations, and the game picks the one that fits what has happened across runs.
// Poguru's: Tutorial is played by id (Dialogue.Find); the rest are his barks and his talk at the temple nest.
public enum DialogueEvent { BossIntro, BossPhase, BossDefeat, KilledOni, HubReturn, SoulPickup, Tutorial, PoguruRound, PoguruSoul, PoguruBoss, PoguruWake, PoguruTalk }

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
    // The boss just felled, for Poguru's boss-down barks.
    public BossKind? Boss;
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
    public static Req Felled(BossKind boss) => new Req($"just felled {boss}", c => c.Boss == boss);
    // Met in any way: fought, heard, or killed by.
    public static Req Met(BossKind boss) => new Req($"met {boss}", c => c.Save.deathsTo[(int)boss] + c.Save.winsVs[(int)boss] > 0
        || c.Save.seen.Exists(id => id.StartsWith(boss + "_")));

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
        BossKind? boss = null) =>
        new DialogueContext { Save = Save.Data, Run = Save.Run, Loadout = loadout, Element = element, Lives = lives, Offering = offering, Boss = boss };

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
    public static Conversation Pick(string speaker, DialogueEvent evt, DialogueContext ctx)
    {
        var eligible = All.FindAll(c => c.Speaker == speaker && c.Event == evt && Blocked(c, ctx) == null);
        foreach (Tier tier in new[] { Story, Tier.Reactive, Repeat })
        {
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
