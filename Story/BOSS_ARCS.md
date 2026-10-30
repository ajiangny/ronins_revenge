# Plan: the bosses' progressive story

[Back to the story](README.md)

**Status: plan, nothing here is in game yet.** Every line below is a draft to be reviewed before it goes into
`Dialogue.Lines.cs` and `Tools/generate-voice.mjs`. Once a line is in, move it into the character's file and mark it
there, and update [RELATIONSHIPS.md](RELATIONSHIPS.md).

## Where things stand

What the bosses already react to (`Dialogue.Lines.cs`):

| | Benibana | Utsusemi | Shirogane | Kagetsune |
|---|---|---|---|---|
| Arc beats (Story) | Three → Bell | Tea → WindDown | Bell → Oath | Broken → Silent |
| Other bosses | Silver (met her) | Confession (Benibana) | Bell (Benibana's) | Souls (the three souls) |
| Counts | 3 deaths to her | 10+ deaths (subtitle-only count) | none | 1 win |
| Element held | all four | all four | all four | all four |
| Phase 2 | one Oni answer, then the card line forever | same | same | same |
| Defeat | one Oni answer, then the built-in line | same | same | same |

The gaps:

1. **The bosses don't know about each other.** Only three lines look sideways, and none of them looks *ahead*:
   Benibana never learns that Oni reached the clock, or the Emperor.
2. **No counts.** Only Benibana's "Three times" and Utsusemi's subtitle-only tally. Nobody keeps score, and Oni never says
   how many times he has beaten anyone.
3. **No one is freed.** The arc gates (README, "Endings") say a boss is freed after her last arc line plus one more win,
   but nothing records it, and no one notices when a bell goes quiet.
4. **Phase 2 is static.** After the first fight, every phase card says the same line. The phase break is the most
   dramatic moment in each fight, and it has the least writing.

## How the bosses know

Story rule (already true, never said out loud): **every soul that falls goes back into the coffin and is forged
again beside the others.** So the bosses hear one another come back in. Benibana, sent out first each night, lies
in the dark and hears the clock come back in ticking, Shirogane's blades laid down, the whole armor shudder when the
Emperor breaks. That's how the stage 1 boss can talk about stage 4. Lines that look ahead should say *how* they
heard ("came back in", "I felt the coffin shake"), never that they watched.

Kagetsune holds all of it, so he knows everything, and he says it to belittle. Utsusemi counts. Shirogane
remembers. Benibana gossips, because it's the closest she gets to company.

## The shape across runs

Four acts, keyed to what Oni has done rather than to run numbers, so a strong player and a struggling one both get
the full arc in order:

| Act | Starts when | What the bosses talk about |
|---|---|---|
| I. Strangers | first run | Themselves. The existing first intros and arc openers. |
| II. The coffin talks | Oni reaches stage 2 | Each other: who he reached, who killed him, how far he got last night. Scorekeeping begins. |
| III. He broke | first win vs Kagetsune (`WinsVs(Kagetsune, 1)`) | The Emperor rebuilt *out of them*. Fear, hope, the first sense that it can end. |
| IV. Bells go quiet | the first boss freed | The freed boss's silence, felt by the others. Phase 2 and echo lines change for freed bosses. Ends at dawn. |

Lines are tiered so arc beats always come first:
**Story** (the arc beats, once) > **Reactive, once** (gossip and milestones, `Cooled(Once, …)`) > **Reactive,
cooled** (last run's outcome, the score) > **Repeatable**.

## 1. The coffin talks: bosses on each other

Draft lines. **Bold** = speaker; *(req)* = requirement.

### Benibana (she hears everyone come home)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Benibana_Intro_Clock` | Reactive, once | met Utsusemi | **Benibana:** "You reached the clock. It came back into the coffin ticking louder than it left. Whatever you did, it's sulking." |
| `Benibana_Intro_Sutra` | Reactive, once | heard `Utsusemi_Intro_Tea` | **Benibana:** "The clock hums in the dark now. An old sutra." **Benibana:** "I didn't know machines could pray. I didn't know I still could." |
| `Benibana_Intro_Emperor` | Reactive, once | met Kagetsune | **Benibana:** "So you've stood in front of him. Does he call you his little demon? He called me his flower." **Benibana:** "Kill him for that alone." |
| `Benibana_Intro_Broke` | Story | `WinsVs(Kagetsune, 1)` | **Benibana:** "You broke him. Every bell rang at once... and then he was poured back together out of us." **Benibana:** "My flock is in his new hands, shepherd." **Oni:** "Then I'll break those too." |
| `Benibana_Intro_ClockStopped` | Reactive, once | Utsusemi freed | **Benibana:** "The ticking's gone. I lie in the dark and miss it. Don't you dare tell anyone." |
| `Benibana_Intro_SilverRests` | Reactive, once | Shirogane freed | **Benibana:** "She laid her blades down. The one who refused, resting." **Benibana:** "...Is that what it looks like?" |
| `Benibana_Intro_Army` | Reactive, rests 3 | died to the army on stage 1 last run | **Benibana:** "You didn't even reach me. Soldiers? Shepherd, I'm insulted." |
| `Benibana_Intro_Further` | Reactive, rests 3 | died past stage 1 last run | **Benibana:** "You got past me last night. Don't look so pleased. Look where it got you." |

### Utsusemi (it counts what comes back)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Utsusemi_Intro_Dancer` | Reactive, once | `WinsVs(Benibana, 3)` | **Utsusemi:** "The dancer... came back in... cursing you. Three times. I counted." |
| `Utsusemi_Intro_Silver` | Reactive, once | met Shirogane | **Utsusemi:** "Past me... the silver one. Her blades keep... perfect time." **Utsusemi:** "I envy... perfect time." |
| `Utsusemi_Intro_Winder` | Reactive, once | met Kagetsune | **Utsusemi:** "You have seen... the hand that winds me. Tick... Did he look... afraid?" **Oni:** "No." **Utsusemi:** "He will... boy." |
| `Utsusemi_Intro_Broke` | Story | `WinsVs(Kagetsune, 1)` | **Utsusemi:** "The winder... broke. For one tick... nothing wound me." **Utsusemi:** "...It was... quiet. Do it... again." |
| `Utsusemi_Intro_BellQuiet` | Reactive, once | Benibana freed | **Utsusemi:** "A bell... stopped. The dancer's. Tick... tock..." **Utsusemi** (the monk): "...Good. She can confess... to someone kinder." |
| `Utsusemi_Intro_Road` | Reactive, rests 3 | died to the army on stage 2 last run | **Utsusemi:** "You died... on my road. Before the hour. Wasteful... boy." |

### Shirogane (she remembers for him)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Shirogane_Intro_Monk` | Reactive, once | heard `Utsusemi_Intro_WindDown` | **Shirogane:** "The monk asked you to wind him down. Do it. He sheltered you the night I couldn't." |
| `Shirogane_Intro_Emperor` | Reactive, once | met Kagetsune | **Shirogane:** "You've seen what he's become. Don't watch the bells. Watch the hammer. It's slower than he wants you to think." |
| `Shirogane_Intro_Rebuilt` | Story | `WinsVs(Kagetsune, 1)` | **Shirogane:** "You killed him. Then he rose. I felt him take the pieces from all of us." **Oni:** "Then I'll take them back." |
| `Shirogane_Intro_Dancer` | Reactive, once | Benibana freed | **Shirogane:** "Benibana's bell went quiet. I heard it stop." **Shirogane:** "...So it can be done." |
| `Shirogane_Intro_Clock` | Reactive, once | Utsusemi freed | **Shirogane:** "The monk's clock has stopped. He gave you a roof once. You gave him rest. That's even." |
| `Shirogane_Intro_Wisp` | Reactive, once | heard `Poguru_Talk_Silver` | **Shirogane:** "Something in your wisp is hiding from me." **Poguru:** "...Hi." **Shirogane:** "...Hello. Keep him warm, Oni." (fills RELATIONSHIPS gap 2) |

### Kagetsune (he knows everything, and sneers)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Kagetsune_Intro_Lost` | Reactive, rests 4 | died to Benibana or Utsusemi last run | **Kagetsune:** "My dancer stopped you last night. Or was it the clock? You don't even reach me, little demon." (played on his next meeting) |
| `Kagetsune_Intro_FreedBenibana` | Reactive, once | Benibana freed | **Kagetsune:** "One of my bells is silent. The cracked one. I was fond of it." **Oni:** "She wasn't." |
| `Kagetsune_Intro_FreedUtsusemi` | Reactive, once | Utsusemi freed | **Kagetsune:** "My clock has stopped. No matter. The hour stays where I put it." |
| `Kagetsune_Intro_FreedShirogane` | Reactive, once | Shirogane freed | **Kagetsune:** "Even her silence was mine. Now there is nothing where she hung." **Kagetsune:** "...Nothing at all." |
| `Kagetsune_Intro_Last` | Story | all three freed | **Kagetsune:** "My dancer. My clock. My silent bell. You've taken my favourites." **Kagetsune:** "What's left of me is only... everyone else." **Oni:** "Then I'll send them home too." |

## 2. Keeping score

Both counters are already saved (`deathsTo[]`, `winsVs[]`). Voiced lines can't say a changing number, so each count
gets **fixed milestones** (3, 5, 10, 25), each a once-only Reactive line; the existing `{deaths}` stays for
subtitle-only lines. Two new requirements give the running score: `Leads(boss)` (more wins than deaths against
them) and `Trails(boss)`.

Oni's count is the emotional thread: **he counts because he is forgetting.** It starts as a soldier's habit and
turns out to be how he holds on.

| id | Requires | Lines |
|---|---|---|
| `Benibana_Intro_Five` | `DeathsTo(Benibana, 5)` | **Benibana:** "Five times now. He owes me five bells. I've stopped asking." |
| `Benibana_Intro_Ten` | `DeathsTo(Benibana, 10)` | **Benibana:** "Ten. Ten bells owed, and I'm still dancing for nothing." |
| `Benibana_Intro_OniFive` | `WinsVs(Benibana, 5)` | **Oni:** "Five times I've put you down." **Benibana:** "And five times I've got up. We're both very stubborn sheep." |
| `Benibana_Intro_Ahead` | `Trails(Benibana)`, rests 4 | **Benibana:** "I'm ahead, shepherd. Shall we keep score out loud?" |
| `Benibana_Intro_Behind` | `Leads(Benibana)`, rests 4 | **Oni:** "I'm ahead." **Benibana:** "Don't get used to it." |
| `Utsusemi_Intro_Three` | `DeathsTo(Utsusemi, 3)` | **Utsusemi:** "Three... by my hand. Counted. Filed." |
| `Utsusemi_Intro_OniFive` | `WinsVs(Utsusemi, 5)` | **Oni:** "Five times I've stopped you." **Utsusemi:** "Five... Tick... And still... I wind." |
| `Utsusemi_Intro_OniTen` | `WinsVs(Utsusemi, 10)` | **Utsusemi** (the monk): "Ten times... you've given me... a moment's quiet. Thank you... boy." |
| `Shirogane_Intro_Three` | `DeathsTo(Shirogane, 3)` | **Shirogane:** "Three times. You used to take one bout in three from me. Now you take none." |
| `Shirogane_Intro_OniFive` | `WinsVs(Shirogane, 5)` | **Oni:** "That's five." **Shirogane:** "You never kept count before." **Oni:** "I'm forgetting. Counting helps." |
| `Shirogane_Intro_Ahead` | `Leads(Shirogane)`, rests 4 | **Shirogane:** "You're winning more than you lose. Good. Don't let it make you careless." |
| `Kagetsune_Intro_Three` | `WinsVs(Kagetsune, 3)` | **Kagetsune:** "Three times broken. Three times poured again." **Oni:** "Then four." |
| `Kagetsune_Intro_Ten` | `Deaths(10)` | **Kagetsune:** "Ten times back to your hill. I hear every one. Each time, a little less of you climbs out." |
| `Kagetsune_Intro_TwentyFive` | `Deaths(25)` | **Kagetsune:** "Twenty-five. Does the fish on the hill still pull you out? Tell her she's wasting her time." |
| `Oni_Hub_CountBenibana` | `WinsVs(Benibana, 3)`, once | **Oni:** "Three times, the smoke. ...I should remember her name better than that." |
| `Oni_Hub_CountShirogane` | `WinsVs(Shirogane, 3)`, once | **Oni:** "Three times I've cut her down. She let me, once. I'm not sure which time." |
| `Oni_Hub_CountKagetsune` | `WinsVs(Kagetsune, 2)`, once | **Oni:** "Twice he's fallen. Twice he's stood back up. ...I can count higher than him." |

## 3. Phase 2, rewritten

The phase break is each boss's turning point (Benibana cutting off her robes, the clock's mainspring snapping,
Kuon arriving, the Emperor's chains breaking), so each one gets its own small arc instead of one fixed line.

**How it plays.** Today, any phase conversation is staged as a talk scene after the card. New rule: **a phase
line spoken by the boss alone plays on the card itself** (its own voice clip in place of the built-in one), and a
scene happens only when Oni answers. Phase lines stay under ~6 s so the fight doesn't stall. Each boss gets:

- **Arc beats** (Story, once): how the transformation changes as the arc moves.
- **How phase 1 went** (Reactive, rests 2–3): Oni untouched, Oni on his last life, killed by this form last run
  (`LastKillerSecondPhase`, already saved).
- **Two or three alternates** (Repeatable), so the card isn't the same line every run. The built-in line stays in the
  bag.

### Benibana: The Crimson Unsealed

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Benibana_Phase` | Story | first | (built-in) "Every step I ever danced was his... This one is mine." **Oni:** "Then dance it." *(exists)* |
| `Benibana_Phase_Robes` | Story | `WinsVs(Benibana, 2)` | **Benibana:** "I cut these robes off every night. By morning he's sewn me back into them." |
| `Benibana_Phase_ForThem` | Story | heard `Benibana_Intro_Bell` | **Benibana:** "For them, this time. Not for me. Not for him." **Oni:** "For them." |
| `Benibana_Phase_Clean` | Reactive, rests 3 | no hits taken in phase 1 | **Benibana:** "Not a scratch on you? Then I'll stop being polite." |
| `Benibana_Phase_LastLife` | Reactive, rests 3 | 1 life left | **Benibana:** "One breath left in you. Let me take it slowly." |
| `Benibana_Phase_Remember` | Reactive, rests 3 | killed by her phase 2 last run | **Benibana:** "You remember this part, shepherd. You died in it." |
| `Benibana_Phase_Blade` | Repeatable | | **Benibana:** "No more dancing. Only the blade." |
| `Benibana_Phase_Smoke` | Repeatable | | **Benibana:** "The smoke was his. Now it's my curtain." |

### Utsusemi: The Unwound Hour

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Utsusemi_Phase` | Story | first | (built-in) **Oni:** "Then I'll stop you." *(exists)* |
| `Utsusemi_Phase_Tighter` | Story | `WinsVs(Utsusemi, 2)` | **Utsusemi:** "Spring... snapped... again. He winds it... tighter... each time." |
| `Utsusemi_Phase_Monk` | Story | heard `Utsusemi_Intro_Tea` | **Utsusemi** (the monk, softer): "...Boy... the heart is open... strike it... I cannot hold it back..." **Oni:** "I know." |
| `Utsusemi_Phase_Loose` | Story | heard `Utsusemi_Intro_WindDown` | **Utsusemi** (the monk): "Now, boy. While the spring is loose." |
| `Utsusemi_Phase_Clean` | Reactive, rests 3 | no hits taken in phase 1 | **Utsusemi:** "Unscratched. Recalculating... the hour." |
| `Utsusemi_Phase_LastLife` | Reactive, rests 3 | 1 life left | **Utsusemi:** "One life... left. I will... count it." |
| `Utsusemi_Phase_Remember` | Reactive, rests 3 | killed by its phase 2 last run | **Utsusemi:** "The heart... burned you... last hour. It remembers... your shape." |
| `Utsusemi_Phase_Overwound` | Repeatable | | **Utsusemi:** "Overwound. Overwound." |
| `Utsusemi_Phase_Thirteen` | Repeatable | | **Utsusemi:** "The thirteenth hour... is coming." |

### Shirogane: Oath of the Spectral Wolf

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Shirogane_Phase` | Story | first | (built-in) **Oni:** "Then remind me." *(exists)* |
| `Shirogane_Phase_Kuon` | Story | `WinsVs(Shirogane, 2)` | **Shirogane:** "Kuon remembers you. You ate the rice cakes I brought for it." **Oni:** "...Once." **Shirogane:** "It counts." |
| `Shirogane_Phase_Oath` | Story | heard `Shirogane_Intro_Oath` | **Shirogane:** "Two blades, one oath. He made us cross them. Don't you dare hold back." |
| `Shirogane_Phase_Clean` | Reactive, rests 3 | no hits taken in phase 1 | **Shirogane:** "Clean. You're remembering. Kuon, show him the rest." |
| `Shirogane_Phase_LastLife` | Reactive, rests 3 | 1 life left | **Shirogane:** "Breathe, Oni. Guard your left. I can't stop, so you must." |
| `Shirogane_Phase_Remember` | Reactive, rests 3 | killed by her phase 2 last run | **Shirogane:** "Kuon took you last time. It's a wolf, Oni. Watch its feet, not its teeth." |
| `Shirogane_Phase_Together` | Repeatable | | **Shirogane:** "Kuon. Together." |
| `Shirogane_Phase_Howl` | Repeatable | | **Shirogane:** "Hear it howl. That's my shrine, still standing guard." |

### Kagetsune: The Unchained Wraith

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Kagetsune_Phase` | Story | first | (built-in) **Oni:** "Not any more." *(exists)* |
| `Kagetsune_Phase_Poured` | Story | `WinsVs(Kagetsune, 1)` | **Kagetsune:** "You think this form breaks too? Then break it. I have a thousand more of me." |
| `Kagetsune_Phase_Missing` | Story | any boss freed | **Kagetsune:** "Kneel! KNEEL! ...Why are there fewer of you?" |
| `Kagetsune_Phase_Clean` | Reactive, rests 3 | no hits taken in phase 1 | **Kagetsune:** "Untouched. You were always my finest guard. Kneel, and be it again." |
| `Kagetsune_Phase_LastLife` | Reactive, rests 3 | 1 life left | **Kagetsune:** "One soul left in you, little demon. Ring for me." |
| `Kagetsune_Phase_Remember` | Reactive, rests 3 | killed by his phase 2 last run | **Kagetsune:** "The Wraith ended you last time. Every one of them remembers how." |
| `Kagetsune_Phase_Unchained` | Repeatable | | **Kagetsune:** "Every chain, broken! Every soul, MINE!" |
| `Kagetsune_Phase_Home` | Repeatable | | **Kagetsune:** "Come out, all of you. Your lord is calling." |

### His summons, by arc

In Kagetsune's phase 2 he drags the three bosses' souls out to fight. Their echo lines should move with their arcs:

| Echo | Default (exists) | After their last arc beat | Once freed |
|---|---|---|---|
| Benibana | "Your strings again, Kagetsune? Tch... I'm no puppet of yours." | "Not your dance. Never again." | **Not summoned.** He calls, nothing comes: "Dance for me, Benibana. ...Benibana?" |
| Utsusemi | "The hour... strikes... Run... boy." | "Strike him... boy... not me..." | **Not summoned:** "Strike the hour, clock. ...Clock?" |
| Shirogane | "Move, Oni. I cannot hold my blades." | "Now, Oni. While he's looking at me." | **Not summoned:** "Rise, Shirogane. ...Rise!" |

The "not summoned" column is a **gameplay change** (his phase 2 gets easier as Oni frees souls), so it needs your
sign-off; see open questions.

## 4. Defeats

Like the phase breaks, each defeat gets one arc-aware line between the first answer and the repeatable:

| Boss | id | Requires | Lines |
|---|---|---|---|
| Benibana | `Benibana_Defeat_Bell` | heard `Benibana_Intro_Bell` | **Benibana:** "The cracked one, shepherd... third from the left..." **Oni:** "I know which one." |
| Utsusemi | `Utsusemi_Defeat_Monk` | heard `Utsusemi_Intro_Tea` | **Utsusemi** (the monk): "...The tea was... jasmine. Remember that... for me." |
| Shirogane | `Shirogane_Defeat_Oath` | heard `Shirogane_Intro_Oath` | **Shirogane:** "Keep it, Oni. Keep it even when you forget me." **Oni:** "I won't forget." **Shirogane:** "You will. Keep it anyway." |
| Kagetsune | `Kagetsune_Defeat_Again` | `WinsVs(Kagetsune, 2)` | **Kagetsune:** "Again... they won't ring... why won't they ring for me..." |

## What has to be built

| # | Piece | Where | Notes |
|---|---|---|---|
| 1 | `lastStage` (stage reached last run, 1–4) and `Req DiedOnStage(n)` / `GotPast(boss)` | `SaveData`, `Save.EndRun`, `Dialogue.cs` | For "you didn't even reach me" and "you got past me". Today an army death doesn't say where. |
| 2 | Freed bosses: `SaveData.freed` and `Req Freed(boss)` | `Save.BossFelled`, `Dialogue.cs` | A boss is freed on a win once her last arc line (`Benibana_Intro_Bell`, `Utsusemi_Intro_WindDown`, `Shirogane_Intro_Oath`) has played. Also what the true ending should wait on (README, "Endings"). |
| 3 | `Req Leads(boss)` / `Trails(boss)` | `Dialogue.cs` | `winsVs` against `deathsTo`, both at least 3 so it isn't noise. |
| 4 | Phase context: hits taken in phase 1 | `Boss`/`GameManager.PlayPhaseCard`, `DialogueContext` | Pass `lives` and a `phaseOneHits` count into `Dialogue.Context`. |
| 5 | Phase card plays a boss-only line on the card | `GameManager.PlayPhaseCard` | Use the conversation's voice clip and text in place of the built-in; scene only when Oni has a line. |
| 6 | Echo lines by arc, freed bosses not summoned | `Kagetsune.Phase.cs` / summons | Only if approved. |
| 7 | Voicing | `Tools/generate-voice.mjs`, `npm run voice`, then the Apply voice menu command | About 75 conversations, about 110 clips. Milestone numbers are written in, never `{placeholders}`. |
| 8 | Docs | each boss's file, Oni.md, RELATIONSHIPS.md (coverage table, gaps 2 and 6), Docs/Progression.md | As each batch lands. |

## Order of work

Each step is its own commit, playable on its own:

1. **Engineering** (pieces 1–5), with the F7 debug view (`Dialogue.Explain`) checked for every new requirement.
2. **Phase 2 rewrite** (section 3 minus the echoes): most visible, fixes the staleness the player hears every run.
3. **The coffin talks** (section 1), then **keeping score** (section 2).
4. **Defeats** (section 4) and Oni's hub counts.
5. **Echoes and freed summons** (piece 6), if approved.
6. Voice pass and docs for each batch as it lands, not saved up for the end.

## Open questions

- **Freed bosses leave Kagetsune's phase 2?** Strong payoff, but it makes the final fight easier as the story
  advances. The alternative: they're still summoned but turn their attack on him (one hit, then gone).
- **Poguru and Kagetsune.** RELATIONSHIPS gap 6 floats Poguru being a sliver of the armor's own fire. A line like
  "Something of mine is hiding in your wisp, little demon" would make that canon. In or out?
- **Kuon.** Should any boss besides Shirogane acknowledge it? Benibana ("Her dog bit me once. Through the coffin.")
  is the easiest fit.
- **Pacing.** With these lines, a boss intro can carry a new Story beat most runs for the first ~20 runs. If that
  feels talky, gossip lines can play only on runs where no arc beat is due (they already rank below Story).
