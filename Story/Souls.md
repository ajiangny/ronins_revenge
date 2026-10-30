# The four souls: Akagane, Mizore, Hibiki and Asagi

[Back to the story](README.md)

The Fire, Frost, Storm and Water souls get faces, names and voices. They talk to Oni on the boon screen the way
Hades' Olympians talk to Zagreus when he picks up their boons. Three of them are people from the Night of a Thousand
Bells, one each from the court, the army and Oni's own village, the three groups he led up the mountain. The fourth
was never on the mountain: she is the koi of the temple pond he wakes in, and she joins once he has met her there.

| Soul | Element | Was | Group | Calls Oni | File |
|---|---|---|---|---|---|
| **Akagane**, Captain of the Vanguard | Fire | Captain of the Emperor's vanguard; the "copper" to Shirogane's silver | The army | "Guardsman" | [Akagane.md](Akagane.md) |
| **Mizore**, the Court Physician | Frost | Physician to the court for forty years; sat with the old Emperor as he died, and his nine-year-old son | The court | "Oni", "young man" | [Mizore.md](Mizore.md) |
| **Hibiki**, the Village Drummer | Storm | The festival drummer of Oni's home village, seventeen; drummed the procession up the mountain | The village | "Oni", "my lord guardsman" | [Hibiki.md](Hibiki.md) |
| **Asagi**, the Pond Physician | Water | The koi of the temple pond on Oni's hill, three hundred years old; taught Mizore; pulls Oni out of the pool each time he dies | The hill (outside the three) | "little fry", "flute boy" | [Asagi.md](Asagi.md) |

- **Akagane** is anger with somewhere to go. He blames Oni, blames himself more, and aims his fire at Kagetsune.
- **Mizore** is cold, steady care. She treats Oni as a patient, pities Kagetsune as the boy he was, and holds the
  missing piece of Shirogane's oath.
- **Hibiki** is restless joy over grief. She's the only one who jokes, the only one who still loves Oni without
  conditions, and the one whose name he forgets.
- **Asagi** is warm, nosy care, Mizore's opposite and her teacher. She was never in the coffin, has no bell, and is
  the only one who could leave and doesn't. She has decided Oni is her last patient.

## Where they stand in the story

This fits the README's "Every boon is a soul stolen back" and adds faces to it:

- When a samurai dies its soul is pulled back toward the coffin. **Akagane, Mizore and Hibiki are the souls who
  learned to pull back.** Their bells still hang on Kagetsune (or, for the village, ring inside the coffin), so
  they're not free; they're out on a long chain, every night, in the gap between the body and the coffin.
- A dying soul catches on whichever of them it's most like: angry souls on Akagane's fire, frightened, still souls
  on Mizore's cold, thrashing ones on Hibiki's storm. They hand it to Oni. **The soul on the boon screen is one of
  the dead; the voice behind it is the one who caught it.**
- **Asagi is the exception.** She was on her hill that night, not in the hall, so the coffin never had her. The
  souls that slip past it on the way back *with Oni* sink into her pond, the tired ones that only want to be clean
  of it, and she lifts them out and hands them to him as Water. Her boons open only after he has met her in the
  temple (`Save.Data.asagiMet`), a few runs in.
- Their element colours are what hold them out of the coffin. **None of them is ever teal** until they're freed in
  the ending, when the colour burns out of them and they rise as plain soul-fire like every other freed soul.
  Asagi's sea-blue is simply her own; the pool she stands in is teal, and that is soul-fire, not her.
- They are Kagetsune's victims and none of them serves him. Each blames Oni to a different degree (Akagane most,
  Hibiki least and then, honestly, a little) and each helps him anyway, for their own reason. Asagi is nobody's
  victim; she blames herself, for being honest with a boy who climbed her hill.

### Tie-ins to the existing cast

| Soul | Benibana | Utsusemi | Shirogane | Kagetsune |
|---|---|---|---|---|
| Akagane | Her smoke put his vanguard on its knees. Curses her, until she names her bell. | His men, in borrowed bodies, took the monk. | Beat him at every tournament. The person he respects most. | Knelt to him for twenty years. His bell is the war bell on Kagetsune's right shoulder, the one the Bellringers answer. |
| Mizore | Treated her ankles, saw the rosary, kept the secret. | Wants to learn how the soul was wound into gears, to undo it. | **Told her about the dying smiths**, which is where Shirogane's suspicion and the oath came from. | Held his hand as his father died, and told him "Everyone dies, my prince. Even emperors." Her bell is closest to his heart. |
| Hibiki | Watched the dance; "then everyone got so sleepy." | The tea monk who let village children wear his hat at the well. | Held her sword once and dropped it. | Saw him once from the procession. The village got no bells of its own; it rings as one sound inside the coffin. |
| Asagi | A dancer came up the hill for her ankles once; that night the smoke drifted up and settled on the pond. | The monk stopped at her pond every spring and fed her rice, and talked to the fish, not the body. | One of the two guards who sat by her pond; the one who listened. She keeps her stone clear. | Climbed her hill young to ask how forever was done. She told him it wasn't worth having. No bell: he never had her. |

## Elements, boons and Duos

Code names differ from screen names: `Element.Fire` = Fire, `Element.Water` = Frost, `Element.Lightning` = Storm,
`Element.Tide` = Water (added last, so saves made before her keep their `lastRunElement`).

| Soul | Element | Core boons (family) | Passives | Legendary |
|---|---|---|---|---|
| Akagane | `Fire` | Flame Blade, Inferno Shuriken, Burning Riposte (Blast) | Blazing Hands, Kindling | **Wildfire**: "Every man of the vanguard, burning at once." |
| Mizore | `Water` | Frost Blade, Glacial Shuriken, Frozen Riposte (Ice) | Cold Compress (the army freezes when he's hit), Glacier Shell | **Still Water**: "Slow the pulse and a fever loses its hurry." |
| Hibiki | `Lightning` | Storm Blade, Thunder Shuriken, Thunder Riposte (Strike) | Storm Step, Static Charge | **Raijin's Drum**: her own festival drum. |
| Asagi | `Tide` | Riptide Blade, Tidewake Shuriken, Undertow Riposte (Tide: waves that soak, wash arrows away and push the army back up) | Tidal Renewal (her medicine; it was Mizore's until she arrived), Healing Spring | **Dragon Gate**: the koi climbs the falls; once a stage the last blow is turned aside and heals him. |

| Duo | Souls | Their bond |
|---|---|---|
| **Thermal Shock** | Akagane + Mizore | The bone she set and he called butchery. Old palace staff who bicker like a married couple. |
| **Plasma Arc** | Akagane + Hibiki | His men marched to her drum. The night before the coronation the vanguard camped in her village, and her festival fireworks burned down his supply tent. |
| **Superconductor** | Mizore + Hibiki | The old woman who held the girl's wrist at the end. The gentlest pair. |
| **Scalding Steam** | Akagane + Asagi | "Carp." "Koi." He wants her out of his fire; she wets the enemy first so the steam carries. |
| **Black Ice** | Mizore + Asagi | Teacher and student. The one place water turns to ice, and the one argument they've had for forty years: warm the patient or chill him. |
| **Thunder Rain** | Hibiki + Asagi | Asagi heard the girl's festival drum from her pond every summer. Hibiki adores her on sight. |

### Rivalries and jealousy

| Jealous soul | Of Fire | Of Frost | Of Storm | Of Water |
|---|---|---|---|---|
| Akagane | | Mild: "cold is for people who've given up." | Strong: a village girl's speed over the vanguard's fire is an insult. | Dismissive: "stay in your pond, carp." |
| Mizore | Dry: "fevers are popular, briefly." | | Worried: "quick is how fevers spread." | Stiff: her teacher watching her work. |
| Hibiki | Sulky: "Captain Kettle doesn't even smile." | Sulky: "Grandmother made you sit still." | | None. "Great-Grandmother!" |
| Asagi | Fond: "running a temperature." | Fond: "sleet over-chills a patient." | Fond: "you'll slosh me." | |

Nicknames: Hibiki calls Akagane **"Captain Kettle"**, Mizore **"Grandmother"** ("I'm not your grandmother") and Asagi
**"Great-Grandmother"** ("Yes, dear."). Akagane calls Mizore **"physician"**, Hibiki **"girl"**, Asagi **"carp"**
("Koi."). Mizore calls Akagane **"Captain"**, Hibiki by her name, Asagi **"Teacher"**. Asagi calls Oni **"little
fry"** and **"flute boy"**, Mizore **"little sleet"**, Akagane **"little kettle"**, everyone **"dear"**.

## Duo conversations

The same conversation plays whichever soul offers the Duo. Each line's speaker swaps the bust.

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Duo_ThermalShock_First` | Story, once | offers `ThermalShock` | **Mizore:** "Heat after cold. It cracks stone, and anything standing in it."<br>**Akagane:** "You agree with me? Someone write the date down."<br>**Mizore:** "I agree with the fire, Captain. You only carry it." |
| `Duo_ThermalShock_Arm` | Story, once | offers `ThermalShock`, `seen Duo_ThermalShock_First`, `runsSince Duo_ThermalShock_First ≥ 2` | **Akagane:** "My arm still aches where you set it, physician."<br>**Mizore:** "You're dead, Captain. It's in your head."<br>**Akagane:** "...It's in my head because you set it wrong." |
| `Duo_ThermalShock_Again` | Repeatable | offers `ThermalShock` | **Akagane:** "Freeze them, physician."<br>**Mizore:** "Burn them, Captain. Mind your sleeves." |
| `Duo_PlasmaArc_First` | Story, once | offers `PlasmaArc` | **Hibiki:** "Captain! You and me! Thunder and fire, like the summer festival!"<br>**Akagane:** "Your festival fireworks burned down my supply tent, girl. The night before the coronation."<br>**Hibiki:** "It was a beautiful fire."<br>**Akagane:** "...It was." |
| `Duo_PlasmaArc_Pace` | Story, once | offers `PlasmaArc`, `seen Duo_PlasmaArc_First`, `runsSince Duo_PlasmaArc_First ≥ 2` | **Akagane:** "My men marched up that mountain to your drum. You set a terrible pace."<br>**Hibiki:** "You kept up, though."<br>**Akagane:** "We always kept up. That was the trouble." |
| `Duo_PlasmaArc_Again` | Repeatable | offers `PlasmaArc` | **Hibiki:** "Strike and burn!"<br>**Akagane:** "You strike. I'll burn." |
| `Duo_Superconductor_First` | Story, once | offers `Superconductor` | **Hibiki:** "Grandmother! You're in here too?"<br>**Mizore:** "I'm not your grandmother. ...Give me your hand. Steady."<br>**Hibiki:** "Your hands were cold that night, too. I didn't mind." |
| `Duo_Superconductor_Count` | Story, once | offers `Superconductor`, `seen Duo_Superconductor_First`, `runsSince Duo_Superconductor_First ≥ 2` | **Mizore:** "I counted your pulse to the end, that night. I lost count at the bells."<br>**Hibiki:** "I know. I heard you say steady. It helped."<br>**Mizore:** "...Then we'll do it again. Quick, Hibiki. But careful." |
| `Duo_Superconductor_Again` | Repeatable | offers `Superconductor` | **Mizore:** "Quick, but careful."<br>**Hibiki:** "Quick AND careful!" |
| `Duo_ScaldingSteam_First` | Story, once | offers `ScaldingSteam` | **Akagane:** "Water. You'd put out my fire, carp?"<br>**Asagi:** "Koi. And no, dear. Wet a man and then burn him, and see how far the steam goes."<br>**Akagane:** "...Hm. Soak them, then."<br>**Asagi:** "Soak them. Then you may boil." |
| `Duo_ScaldingSteam_Kettle` | Story, once | offers `ScaldingSteam`, `seen Duo_ScaldingSteam_First`, `runsSince Duo_ScaldingSteam_First ≥ 2` | **Asagi:** "The drummer girl calls you a kettle. I see why."<br>**Akagane:** "The drummer girl is seventeen."<br>**Asagi:** "And I'm three hundred, little kettle. Whistle when you're ready." |
| `Duo_ScaldingSteam_Again` | Repeatable | offers `ScaldingSteam` | **Akagane:** "Wet them."<br>**Asagi:** "Burn them, dear. Mind the steam." |
| `Duo_BlackIce_First` | Story, once | offers `BlackIce` | **Mizore:** "Teacher."<br>**Asagi:** "Little sleet. Look at you. Still cold."<br>**Mizore:** "You taught me that wet and cold together is worse than either."<br>**Asagi:** "I taught you to warn patients about it. Not to do it to them."<br>**Mizore:** "These aren't patients." |
| `Duo_BlackIce_Hill` | Story, once | offers `BlackIce`, `seen Duo_BlackIce_First`, `runsSince Duo_BlackIce_First ≥ 2` | **Asagi:** "You never came back up the hill."<br>**Mizore:** "I had the court."<br>**Asagi:** "You had forty years, dear."<br>**Mizore:** "...I had the court." |
| `Duo_BlackIce_Again` | Repeatable | offers `BlackIce` | **Asagi:** "Soak them, sleet."<br>**Mizore:** "Then freeze them, Teacher. The old way." |
| `Duo_ThunderRain_First` | Story, once | offers `ThunderRain` | **Hibiki:** "Great-Grandmother! Water and lightning! Is that safe?"<br>**Asagi:** "Not for them, dear."<br>**Hibiki:** "...I like her, Oni." |
| `Duo_ThunderRain_Festival` | Story, once | offers `ThunderRain`, `seen Duo_ThunderRain_First`, `runsSince Duo_ThunderRain_First ≥ 2` | **Asagi:** "There was a girl in the valley who drummed at the summer festival. I could hear it from the pond, every year."<br>**Hibiki:** "...That was me. That was ME!"<br>**Asagi:** "I know, dear. You were always a little fast." |
| `Duo_ThunderRain_Again` | Repeatable | offers `ThunderRain` | **Hibiki:** "Rain and thunder!"<br>**Asagi:** "Wet first. Then loud." |

---

# Implementation hooks

**Built so far:** each soul's `SoulPickup` tables (first meetings, reactions, greetings) are in `Dialogue.cs` and play as
a talk scene (`DialogueScene`) before the first boon screen of that soul in a run, with the soul's painting
(`Portraits/<Soul>_Closeup.png`, `_Full.png`) at the left and Oni at the right. `runsSince self ≥ n` became the
conversation's `Cooldown`, "play once" a Reactive that never comes back. Their lines are voiced. Each talk opens on
the soul's sting, as Hades' gods announce themselves (`Sfx.SoulSting<Fire|Frost|Storm|Water>`, `GameManager.StingFor`):
Akagane's war-drum boom and flame, Mizore's ice chimes over a singing bowl, Hibiki's taiko drumroll into a thunderclap,
Asagi's temple bell under a water drop and a rising wave.

Also built and voiced, in the order of "When each event fires" below (`GameManager.SoulTalk`): `Legendary`, `Duo`
(speaker id `"Duo"`, so either soul offering it plays it), `BossFelled` (the boon after a boss), `BossAhead` (its
Repeatable about one first screen in three), `SoulJealous` (`Dialogue.Most` / `Rival`; its Repeatables about half the
time a rival leads), and the souls' temple lines as the `SoulHub` event (`SaveData.lastRunElement`; Story and Reactive
always, a Repeatable 40% of the time, only when Oni has nothing of his own to say). Later screens of a soul in the
same run stay quiet unless a Legendary or Duo is on offer. Not yet: the freed beats.

Everything below is a suggestion for the dialogue system being built in parallel; field names follow
`Assets/Scripts/Core/SaveData.cs` where it already has them.

## Record format

Each table row in the soul files is one conversation:

| Field | Value |
|---|---|
| `id` | The first column. Ids never end in a digit, so `<id>_<n>` voice names can't collide. |
| `speaker` | The soul who owns it (`Akagane`, `Mizore`, `Hibiki`, `Asagi`). Duo records: the soul offering the boon. |
| `event` | The section it sits in: `SoulPickup`, `SoulJealous`, `Duo`, `Legendary`, `BossAhead`, `BossFelled`, `HubReturn`. |
| `tier` | `Story` (play once), `Reactive` (play once when marked "once", else gated by `runsSince` itself), `Repeatable` (shuffle bag). |
| `requires` | All must hold (notation below). |
| `lines` | Ordered `{ speaker, text }`. Speakers: the four souls and `Oni`. |

The Story first-meeting chains are written so that each needs the previous one and `runsSince previous ≥ 1`: at most
one chapter per soul per run.

## Requirement notation

| Notation | Meaning | SaveData |
|---|---|---|
| `seen X` / `!seen X` | Conversation X has / hasn't played | `seen` |
| `runsSince X ≥ n` / `≤ n` | Runs since X last played (cooldown / expiry); fails if X never played | `Run - SeenIn(X)` |
| `runs ≥ n` | Runs started | `runsStarted` |
| `clears ≥ n` | Runs cleared | `runsCleared` |
| `deathsTo B ≥ n` | Deaths to boss B | `deathsTo[(int)BossKind.B]` |
| `winsVs B ≥ n` | Wins against boss B | `winsVs[...]` |
| `lastCleared` | The last finished run was a clear | `lastRunCleared` |
| `lastKiller = B` | What killed him last run (`Army` = `BossKind.None`) | `lastKiller` |
| `holds E ≥ n` | Boons of element E held this run | `RoninLoadout.ElementCount` |
| `most = E` | E is the element he holds most of (see [SoulJealous](#souljealous)) | `RoninLoadout.DominantElement` |
| offers `BoonId` | That boon is among this screen's cards | the offer list |
| **New:** `nextBoss = B` | The current stage's boss is B (only during its rounds) | `stages[stageIndex].boss` |
| **New:** `felled = B` | The boss just felled (the post-boss boon) | `Stage.boss` in `BossFell` |
| **New:** `lives = n` | Lives left right now | `lives` |
| **New:** `lastRunElement = E` | The element he held most when the last run ended | add `int lastRunElement = -1` to `SaveData` |

`seen` of boss conversations (`Benibana_Intro_Bell`, `Utsusemi_Intro_WindDown`, `Shirogane_Intro_Oath`) uses the ids
already planned in their files.

## When each event fires

All soul events go through `GameManager.OfferBoons(element, …)`, the one place every elemental boon screen opens
(from `OnSoulCollected`, `GatherDrops`, `OfferStageBoon` and `BossFell`). Pick the conversation there and pass it to
`BoonChoiceUI.Show`. Pass a context so the picker knows the source: `Soul`, `StageBoon` or `BossReward`.

Speaker = the soul of `element`. Try these in order, and play the first that has an eligible conversation:

1. **`Legendary`**: the offers include a Legendary (its `BoonId` → that soul's `Legendary` records).
2. **`Duo`**: the offers include a Duo (`Duo_<BoonId>_…` records).
3. **`BossFelled`**: context is `BossReward` (the boon in `BossFell` after Benibana, Utsusemi or Shirogane).
4. **`SoulPickup` Story**: the next first-meeting chapter.
5. **`BossAhead`**: during a stage's rounds (`nextBoss`), Story then Repeatable.
6. **`SoulJealous`**: another element leads (below).
7. **`SoulPickup` Reactive**, then **`SoulPickup` Repeatable**.

Only the first screen of each element in a run gets the full pick. Later screens of that element in the same run
play a Repeatable greeting about one time in three, and nothing otherwise: a run has roughly 28 boon screens, and
Hades' gods don't talk on every pickup either. Capstones (steps 1 and 2) always get their line. A run's first ever
soul of an element has Oni's `FirstElement` line folded into the soul's `…_Meet_…` opener, so the separate
`FirstElement` event in the README isn't needed for souls.

### SoulJealous

Fires when another element E leads: `ElementCount(E) ≥ 3` and `ElementCount(E) ≥ ElementCount(speaker) + 2`.
Records then check `most = E` to pick the rival. It's the Hades rejection analogue as words only; it doesn't change
the offers.

### HubReturn

On the title hill after a death or win (`ShowTitleMenu` with `Save.Data.hubPending`). Oni's own `HubReturn` Story
lines win; a soul line plays only when he has none, at most one a return, from the soul of `lastRunElement`. Show it
as a subtitle with the bust, not a full panel: the hill is his.

## The boon screen

Today `BoonChoiceUI.Show` puts Oni's painted portrait on the left with the caught soul floating above his palm, a heading
("Boons of Fire"), the element subtitle, and three cards. Add, Hades-style:

- **A dialogue strip** across the top of the panel, above the cards: the speaker's **bust** (about 96×96 px source,
  drawn at the UI's pixel scale) on the left in an element-coloured glow, a **name plate** ("AKAGANE", with a small
  epithet line "Captain of the Vanguard"), and the **typed line** beside it.
- **Typing** runs on unscaled time: `OfferBoons` sets `Time.timeScale = 0`. About 45 characters a second. The voice
  clip plays on the existing voice `AudioSource`.
- **Advancing:** confirm finishes the typing, then moves to the next line. Each line swaps the bust and name plate to
  its speaker. Oni's lines use his existing boon-screen portrait.
- **Cards stay locked** until a multi-line conversation's last line is up, so a mashed confirm can't pick a boon by
  accident. One-line greetings never lock the cards.
- **The subtitle line** under the heading could become the soul's: "Akagane lends you his fury." /
  "Mizore lends you her steady hands." / "Hibiki lends you her storm." (today's `Boons.Elements` subtitles speak of
  "the burned shrine", "the temple pond" and "the mountain pass").
- **Fallback:** with no bust art, show the element's soul frames (already passed as `soulFrames`) in the bust slot,
  as the panel does now without a portrait.

Busts go in `Assets/Art/Portraits/` (`Akagane_Bust.png`, `Mizore_Bust.png`, `Hibiki_Bust.png`) and are wired the way
`ElementArt.portrait` is today, through a `Ninja/Apply …` command, not by hand. Each soul file's **Look** section is
the art brief. They should match each other in framing. (The painted portraits, as revised, drop the chain on all three,
and Mizore is a white crane-woman; see each file's Look.)

**The souls he holds (built).** Each element's soul on the boon screen is painted for the one who caught it (GPT image
gen, `Assets/Art/Portraits/Soul_<Fire|Water|Lightning>_Painted.png`); in play the pixel soul frames still drop:

- **Akagane (Fire):** a roaring copper-orange flame, a broken kabuto crescent and his scorched war fan in the fire,
  his bronze war bell hanging below on a snapped chain.
- **Mizore (Frost):** a still, upright frost-blue flame with a crane at its core and a silver needle of light through
  it, frost ferns and hanging snow, her small cracked bell on a fine chain.
- **Hibiki (Storm):** a restless violet flame spitting lightning around a drum-crest swirl and wave pattern, crossed
  bachi, and the red festival cord with its tin bells.
- **Asagi (Water):** a sea-blue flame shaped like a standing wave with her koi circling at its core, a ring of wave
  crests, drops hanging in the air, and no bell at all: a glass bowl's rim where the others have their chain.

## Voice

- Clips (`Conversation.VoiceId`): the first line is `Assets/Audio/Voice/<conversationId>.mp3`, the nth after it
  `<conversationId>_<n>.mp3`, `n` counted from 1 over the conversation's lines, including Oni's, as the bosses' takes
  are named. Example: `Akagane_Meet_Angry_2.mp3` is "Angry. Yes. That's the word for it. Hello, Guardsman."
  `Ninja/Apply Voice Lines` gathers them and logs which written lines still have no clip.
- **Oni's lines are voiced** under their own `n` (`Akagane_Meet_Copper_3` is his "...I did."); the souls' own lines skip those numbers (don't renumber), so adding their voices later
  doesn't rename anything.
- **Voices, designed:** `AKAGANE`, `MIZORE`, `HIBIKI`, `ASAGI` in `Tools/generate-voice.mjs` (Akagane's made angrier,
  with a Japanese accent; Asagi's given a glassy ring in `FX`, and no coffin echo, since she was never in it). Every
  `SoulPickup` line is voiced; Duo, Legendary and the other events still need theirs.
- **Lines:** add `LINES` entries in the existing form, with bracketed delivery tags:
  `Akagane_Meet_Angry_2: [AKAGANE, "[dry, gruff] Angry. Yes. That's the word for it. [low] Hello, Guardsman."]`.
  Duo lines take the speaking soul's voice per line. Then `npm run voice -- <names>` from `Tools/`.
- **Tuning, built** as `GameManager.soulVoice`: the souls are half in the coffin, so a faint version of Kagetsune's
  choir: a light, slow chorus and a short hall echo, softer than his, the same for all three so they read as one
  family.
- Total: about 45 conversations per soul plus 9 Duos, roughly 330 clips. Story and Duo lines first; repeatables
  can ship as subtitles.

## Freed in the true ending

`GameManager.PlayDawn` already silences the bells one by one. Suggested beats, each only if that soul's last chain
conversation has played (`Akagane_Meet_Salute`, `Mizore_Meet_Oath`, `Hibiki_Meet_River`); otherwise they rise
unseen among the thousands:

1. The coffin bursts: the village's rattle of small bells goes quiet first. `Hibiki_Freed`.
2. The army's war bell, then every army bell at once. `Akagane_Freed`, and his one salute.
3. The small cracked bell over Kagetsune's heart. `Mizore_Freed`.
4. Shirogane's bell, as built.

Each rises in teal, its element colour burning out. At dawn a single drumbeat answers Oni's flute from down the
valley. Asagi has no bell and nothing to burn out of her: her beat is the dawn itself, when the pond runs clear
(`Asagi_Dawn`, if `Asagi_Meet_Home` has played).

## Suggested lines for other files

Not added to their files; take them if you like them.

- **Kagetsune** `BossIntro` once Oni has met all three: "My captain, my physician, and a peasant's drum. You
  collect my bells, little demon. They are still mine."
- **Kagetsune** `KilledOni`, `holds Fire ≥ 3`: "Akagane's fire. He knelt so well, once."
- **Oni** `HubReturn`, `seen Hibiki_Meet_Name`: "Hibiki. ...Hibiki. Hibiki." (repeating it, so he keeps it)

## Open questions

1. **Ending gate.** Should the true ending wait on the souls' chains as it will on the bosses'? Suggested: no. The
   bosses gate the ending; the souls only add their freed beats.
2. **Oni's voice.** His lines inside soul conversations are subtitle-only. Design his voice now, or keep him silent
   on the boon screen?
3. **The village's bell.** Kagetsune.md says his bells are "one for every sealed soul of rank". Hibiki's file reads
   that as: the village has none, and rings as one sound inside the coffin. Should Kagetsune.md say so too?
4. **Village name.** Oni's village is never named. Hibiki would say it; name it, or keep it "the village"?
5. **Element subtitles.** Replace `Boons.Elements`' subtitles (burned shrine, temple pond, mountain pass) with the
   souls' names, or keep both?
6. **How often they talk.** Is first screen of each element per run, then one in three greetings, the right rate?
7. **Replaced boons.** A card that pushes another element's boon out of its slot could get a one-line protest from
   the slighted soul after the pick (Hades' rejection is closer to this). Worth a `SlotReplaced` event?
8. **Akagane's name.** It echoes Shirogane's on purpose (copper and silver). Too close?
