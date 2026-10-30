# How Hades picks its dialogue

[Back to the story](README.md)

Research behind the reactive dialogue system ([README](README.md#dialogue-design-reactive-lines-the-hades-way)).
Most of it is read straight from the game's shipped Lua scripts, mirrored at
[xuqifzz/hades-mod-tutorial](https://github.com/xuqifzz/hades-mod-tutorial/tree/master/Scripts) (the patch it
matches isn't known). Counts are raw matches over those files.

## The data

- **A conversation is the unit, not a line.** A "TextLineSet" is a table of requirements plus an ordered list of
  lines, each `{ Cue, Speaker, Portrait, PreLineWait, Text }`, so a boss and Zagreus trade two or three lines. Its
  key is its name, and chains are built by requiring the previous name (`FuryMetSisters01` → `02`)
  ([EnemyData.lua](https://github.com/xuqifzz/hades-mod-tutorial/blob/master/Scripts/EnemyData.lua)).
- **Buckets per character and moment:** `InteractTextLineSets`, `RepeatableTextLineSets`, `GiftTextLineSets`, and
  for bosses `BossPresentationSuperPriorityIntro…`, `…PriorityIntro…`, `…Intro…`, `…TextLineSets`,
  `…Repeatable…`, `…Outro…`, `…NextStage…`. Boons have `PriorityPickup…`, `Pickup…`, `DuoPickup…`.
- **About 350 requirement keys, all checked by one function**, `IsGameStateEligible`
  ([RunManager.lua](https://github.com/xuqifzz/hades-mod-tutorial/blob/master/Scripts/RunManager.lua#L2569)). The
  ones that carry the story:
  - `RequiredTextLines` (3438 uses) and `RequiredFalseTextLines` (1471). Negation is used half as often as
    prerequisites: it is how a later beat retires an earlier one.
  - `RequiredAnyTextLines`, and `…ThisRun` / `…LastRun` scopes.
  - `MinRunsSinceAnyTextLines` and `MaxRunsSinceAnyTextLines`: a gap, and an expiry so stale callbacks stop.
  - `RequiredMinCompletedRuns` vs `RequiredMinRunsCleared` (runs played vs runs won), `RequiresLastRunCleared`.
  - `RequiredLastKilledByUnits`: the best hub lines react to what killed you.
- **Records** ([Narrative.lua](https://github.com/xuqifzz/hades-mod-tutorial/blob/master/Scripts/Narrative.lua)):
  lines ever played (saved), this run, and this room; a shuffle-bag memory (`PlayedRandomTextLines`); a voice-cue
  record; the queued conversations.

## Picking one

`PlayRandomRemainingTextLines`
([Narrative.lua L817](https://github.com/xuqifzz/hades-mod-tutorial/blob/master/Scripts/Narrative.lua#L817)):

1. Keep the sets whose requirements hold (`PlayOnce` sets already heard are out).
2. If any are `SuperPriority`, pick one of those at random; else any `Priority` one.
3. Otherwise pick at random among the ones **not yet played**; once all have played, clear the bag and pick from
   all of them.
4. Return false if nothing is eligible, so the caller falls through to the next bucket. A boss intro tries
   SuperPriority → Priority → Intro → general → Repeatable, so something always plays.

Priority sets are nearly always `PlayOnce`: one that never runs out would block every lower tier for good.

**The House.** On each return every NPC gets one conversation queued, in a fixed `ConversationOrder`, with at most
one two-character scene per run. It stays queued until you talk to them
([RoomManager.lua](https://github.com/xuqifzz/hades-mod-tutorial/blob/master/Scripts/RoomManager.lua#L4425)).

**Barks** are a separate, lighter system
([AudioScripts.lua](https://github.com/xuqifzz/hades-mod-tutorial/blob/master/Scripts/AudioScripts.lua#L681)):
`RandomRemaining` (no repeats until all have played), named real-time cooldowns shared across a speaker's quips,
`ChanceToPlay` with a lower `SuccessiveChanceToPlay` once heard, `PlayOnceThisRun`, a bark dropped if the speaker is
already talking, and **no barks at all while a conversation is on screen**.

## What the developers say

- Greg Kasavin: "I come up with as many different possible contexts in which a character can talk to you… have the
  game hold the right one for the right situation." A small line repeats only after "20 or 30" loops
  ([GeekDad, 2019](https://geekdad.com/2019/10/narrative-and-early-access-supergiants-greg-kasavin-discusses-hades-development/)).
- More than 300,000 words, over two thirds of them dialogue
  ([Cultured Vultures](https://culturedvultures.com/supergiant-hades-word-count-dialogue/)); over 22,000 voiced lines
  ([GDC 2021, Breathing Life into Greek Myth](https://gdconf.com/news/dive-dialogue-hades-gdc-2021)).
- Each line's link to the moment it answers was authored by hand
  ([Sean Z](https://seanzwrites.com/posts/supergiant-games-greg-kasavin-interview/)); "the story moves forward, no
  matter what happens" ([Origin Story](https://www.originstory.show/episodes/greg-kasavin)).

## Lessons we took

- The record is a conversation with lines inside it.
- Requirements need negation, any-of, last-run scope and an expiry, and runs played apart from runs won.
- A Priority tier above Story for the major events, and a record of firsts (each thing had or met, and the run)
  with an expiry, so "you just found this" lines play while they're news and never thirty runs later.
- Every event ends in a repeatable with no requirements.
- Story lines are play-once; repeatables go through a shuffle bag that resets.
- No random rolls inside eligibility, and a debug view that says why each line passed or failed (Hades' own code
  has a cooldown check that starts the cooldown, and a helper silently broken by a typo).
