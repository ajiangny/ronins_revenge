using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Hades-style reactive dialogue (Story/README.md, Story/DIALOGUE_RESEARCH.md): many conversations written for
// situations, and the game picks the one that fits what has happened across runs.
public enum DialogueEvent { BossIntro, KilledOni, HubReturn, FirstElement }

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
    // The boss's own in-game line and voice clip (Boss.Line), instead of written lines.
    public BossLine? BuiltIn;
    public DialogueLine[] Lines = Array.Empty<DialogueLine>();

    // Voice clips are Audio/Voice/<Id>.mp3 for the first line and <Id>_<n> for the nth after it.
    public string VoiceId(int line) => line == 0 ? Id : $"{Id}_{line + 1}";
}

// What a conversation can ask about: the save, and this run's soul and boons.
public struct DialogueContext
{
    public SaveData Save;
    public int Run;
    public Element? Element;
    public RoninLoadout Loadout;
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
    public static Req HoldsDuo => new Req("holds a Duo", c => c.Loadout != null && HasDuo(c.Loadout));

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
    const DialogueEvent FirstSoul = DialogueEvent.FirstElement;
    const Tier Story = Tier.Story;
    const Tier Repeat = Tier.Repeatable;

    // Every conversation, from the characters' files in Story/. Within a tier the first eligible one plays, so
    // order matters. Keep the texts in step with those files and Tools/generate-voice.mjs.
    public static readonly List<Conversation> All = new List<Conversation>
    {
        // Benibana
        Built("Benibana_Intro", "Benibana", Intro, Story, BossLine.Intro),
        Line("Benibana_Intro_Three", "Benibana", Intro, Story,
            "Three times I've killed you. Three bells he swore to silence. Not one has stopped ringing.", DeathsTo(BossKind.Benibana, 3)),
        Line("Benibana_Intro_Bell", "Benibana", Intro, Story,
            "The third bell from the left on his belt. The cracked one. That one is mine.", Seen("Benibana_Intro_Three"), WinsVs(BossKind.Benibana, 2)),
        Reaction("Benibana_Intro_Back", "Benibana", Intro, 3,
            "Back so soon? He promised me a bell for that one. I'm still listening.", LastKiller(BossKind.Benibana)),
        Reaction("Benibana_Intro_Fire", "Benibana", Intro, 4, "Fire? You'd burn my smoke? Cute.", Holds(Element.Fire)),
        Built("Benibana_Intro_Again", "Benibana", Intro, Repeat, BossLine.Intro),
        Line("Benibana_Intro_Dance", "Benibana", Intro, Repeat, "Dance with me, demon."),
        Line("Benibana_Killed", "Benibana", Killed, Story, "Listen... Did a bell stop? ...No. Of course not."),
        Line("Benibana_Killed_Curtain", "Benibana", Killed, Repeat, "Curtain."),

        // Utsusemi
        Built("Utsusemi_Intro", "Utsusemi", Intro, Story, BossLine.Intro),
        Line("Utsusemi_Intro_Tea", "Utsusemi", Intro, Story,
            "Tick... tock... tea... There was tea, once. Why do I... remember tea?", WinsVs(BossKind.Utsusemi, 2)),
        Line("Utsusemi_Intro_WindDown", "Utsusemi", Intro, Story,
            "Boy. Wind me down. I cannot do it... from in here.", Seen("Utsusemi_Intro_Tea")),
        Reaction("Utsusemi_Intro_Counted", "Utsusemi", Intro, 5,
            "{Deaths} deaths. {Deaths} roads walked again. I counted every one, Oni.", Deaths(10)),
        Built("Utsusemi_Intro_Again", "Utsusemi", Intro, Repeat, BossLine.Intro),
        Line("Utsusemi_Intro_Tick", "Utsusemi", Intro, Repeat, "Tick... tock."),
        Line("Utsusemi_Killed", "Utsusemi", Killed, Story, "One. I will remember the hour."),
        Line("Utsusemi_Killed_Counted", "Utsusemi", Killed, Repeat, "Counted."),

        // Shirogane
        Built("Shirogane_Intro", "Shirogane", Intro, Story, BossLine.Intro),
        Line("Shirogane_Intro_Bell", "Shirogane", Intro, Story,
            "She told you which bell is hers. Mine has never rung. I would not kneel.", Seen("Benibana_Intro_Bell")),
        Line("Shirogane_Intro_Oath", "Shirogane", Intro, Story,
            "We swore to end him together, before the coronation. I kept the oath. Now you keep it.", WinsVs(BossKind.Shirogane, 3)),
        Reaction("Shirogane_Intro_Hesitated", "Shirogane", Intro, 3, "You hesitated. You never used to.", LastKiller(BossKind.Shirogane)),
        Built("Shirogane_Intro_Again", "Shirogane", Intro, Repeat, BossLine.Intro),
        Line("Shirogane_Intro_Again2", "Shirogane", Intro, Repeat, "Again, then."),
        Line("Shirogane_Killed", "Shirogane", Killed, Story, "Not yet, then. I'll wait."),
        Line("Shirogane_Killed_Rise", "Shirogane", Killed, Repeat, "Rise."),

        // Kagetsune
        Built("Kagetsune_Intro", "Kagetsune", Intro, Story, BossLine.Intro),
        Line("Kagetsune_Intro_Broken", "Kagetsune", Intro, Story,
            "You broke me once. I have a thousand more of me.", WinsVs(BossKind.Kagetsune, 1)),
        Line("Kagetsune_Intro_Silent", "Kagetsune", Intro, Story,
            "She never rang. Do you know how that grates, little demon?", Seen("Shirogane_Intro_Oath")),
        Built("Kagetsune_Intro_Again", "Kagetsune", Intro, Repeat, BossLine.Intro),
        Line("Kagetsune_Intro_Kneel", "Kagetsune", Intro, Repeat, "Kneel."),
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

        // Oni, the first time each kind of soul lends him its power.
        Line("Oni_Soul_Fire", "Oni", FirstSoul, Story, "Warm... Someone in there is still angry.", OfElement(Element.Fire)),
        Line("Oni_Soul_Frost", "Oni", FirstSoul, Story, "Cold hands. Steady ones.", OfElement(Element.Water)),
        Line("Oni_Soul_Storm", "Oni", FirstSoul, Story, "It wants out as badly as I do.", OfElement(Element.Lightning)),
        new Conversation { Id = "Oni_Soul_Silent", Speaker = "Oni", Event = FirstSoul, Tier = Repeat },
    };

    public static DialogueContext Context(RoninLoadout loadout = null, Element? element = null) =>
        new DialogueContext { Save = Save.Data, Run = Save.Run, Loadout = loadout, Element = element };

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
