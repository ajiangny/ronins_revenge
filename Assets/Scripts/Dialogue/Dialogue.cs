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

public static partial class Dialogue
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
