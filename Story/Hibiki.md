# Hibiki, the Village Drummer

[Back to the story](README.md) · [The three souls](Souls.md)

| | |
|---|---|
| Role | The **Storm** soul. She speaks for every Storm boon. |
| Was | The festival drummer of Oni's home village, seventeen, two summers younger than him and always at his heels. |
| Is | A soul half out of the coffin. Her village has no bells of rank on Kagetsune: they ring together, inside the coffin. |
| Wants | Out. Then home, with everyone. |
| Helps Oni because | He's from home. She says she doesn't blame him, which hurts him more than anger would; later she admits she does, and would follow him up the mountain again anyway. |
| Signature | Taiko drumsticks crackling with lightning, a festival happi coat, a twisted headband, never standing still. |

*Hibiki* is an echo, a ringing that carries. Her drum carried through the whole valley. On the mountain the bells
drowned it.

## Look

**The painting.** Her talk-scene portrait (`Assets/Art/Portraits/Hibiki_Closeup.png`, with `Hibiki_Full.png`) was painted
from this brief in the boss portraits' style (GPT image gen), facing right toward Oni. The user's revision dresses her in
purple storm-patterned robes (cloud and lightning motifs, her Storm colour) and drops the chain.

For the boon screen's pixel bust:

- A girl of seventeen with a round, sunburnt face, a gap-toothed grin and quick dark eyes with violet sparks in
  them. Short, messy black hair tied up off her neck; loose strands lift and float with static.
- A **twisted hachimaki headband**, white and indigo.
- A village **festival happi coat** in indigo with a white wave-and-cloud pattern, sleeves pushed up, one sleeve
  torn at the seam. A plain dark under-kimono, collar closed.
- **Two taiko bachi** (drumsticks), one held up mid-beat and one tucked behind her ear, little arcs of lightning
  jumping between them.
- She's leaning toward the frame edge, as if about to bolt.
- **Her bell:** a red festival cord runs from her wrist out of frame, a cluster of tiny tin bells knotted into it:
  the whole village on one cord.
- Palette: the Storm element colour (`#C773FF`) for the sparks and glow, indigo and white for the coat, warm skin.
  **No teal** until she's freed.
- **Her aura** (`Fx/Aura_Lightning`, Oni wears it while a Storm Shadow Ultimate runs): two jagged violet bolts
  re-striking over a shockwave ring that pulses like her drum's boom, sparks jumping off them.

## History

- **The drummer.** Every summer festival in the village, she drummed. She taught herself at nine on an upturned rice
  tub and by fourteen nobody else was allowed near the big drum. She followed Oni to the river every day the summer
  before he left for the capital, and drummed him out of the village the morning he went.
- **The procession.** When word came that "our Oni" had sent for the whole village to the Emperor's coronation,
  everyone wore their best. Hibiki drummed the whole way up the mountain, and the army marched behind to her beat.
  Her mother cried, she was so proud.
- **The Night.** She was drumming when Benibana's smoke reached the village rows. Her hands slowed and stopped. An
  old woman from the court sat down next to her and held her wrist, counting, and said "steady" until neither of
  them could hear anything over the bells. That was [Mizore](Mizore.md).
- **One bell.** Kagetsune gave bells of rank to the court and the army. The village got none of its own: it rings
  inside the coffin, all together, one sound. You can hear it in the hammer when he swings.
- **Since.** Hibiki has never stopped moving, because stopping is how the coffin catches you. Restless souls, the
  ones that thrash on the way back, catch on her like sparks to a rod, and she flings them at Oni as Storm.

## How a Storm soul reaches Oni

A soul that dies fighting the pull crackles. Hibiki gathers the ones that won't sit still and throws them to Oni:
speed, chain lightning, a faster draw. She does it at a run and talks the whole time.

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | The big boy from her village who went to the capital and became someone. She adored him, and she led her village up the mountain behind him with her drum. He doesn't remember her name at first, which she takes with a grin and a flinch. She calls him **"Oni"**, and teasingly **"my lord guardsman"**. |
| [Kagetsune](Kagetsune.md) | She saw him once, from the procession: a tall man in a tall helmet who didn't look at any of them. She hates bells now. Her drum against his bells is the whole of her feeling about him. |
| [Benibana](Benibana.md) | The most beautiful thing she ever saw was that dance. Then everyone got sleepy. She's still working out how to feel. |
| [Utsusemi](Utsusemi.md) | The tea monk who stopped at the village well every spring on his pilgrim road, and let the children wear his hat. She's sure he's still in there. |
| [Shirogane](Shirogane.md) | "Lady Shirogane" visited the village once with Oni and let Hibiki hold her sword. Hibiki dropped it. She worships her and is terrified of her in equal parts. |
| [Akagane](Akagane.md) (Fire) | "Captain Kettle", because he boils. His men marched to her drum and he hasn't forgiven the drum. She teases him relentlessly and runs to him first when something scares her. |
| [Mizore](Mizore.md) (Frost) | "Grandmother." Not her grandmother. The woman who held her wrist at the end. Hibiki is the only one who can make Mizore laugh, and she's never heard it. |
| [Asagi](Asagi.md) (Water) | "Great-Grandmother!" Asagi says "Yes, dear," and Hibiki has never been so pleased with anyone. The old koi heard her festival drum from the pond every summer; Hibiki didn't know anyone outside the village could. |
| [Oliver](Oliver.md) (Steel) | "Uncle Cat." She keeps trying to pat him. Once she tapped three on his little anvil, and "Don't." stopped her dead. He's the only one who has told her that her village rang true. |

**Jealousy.** Loud and sulky. If Oni favours Fire she complains the Captain is boring; if Frost, that Grandmother
will make him sit still. Of Water, none: she tries to be cross with Great-Grandmother and can't. It always ends
with her giving him the boon anyway.

## Voice

**In game:** voiced in ElevenLabs (`HIBIKI` in `Tools/generate-voice.mjs`), designed from the prompt above. Every
`SoulPickup` line is voiced; the souls share a faint coffin echo (`GameManager.soulVoice`).

**ElevenLabs voice design prompt:** "A girl of about seventeen with a bright, quick, slightly breathless voice and
a warm country lilt, no accent. Talks fast, laughs easily, teases. When she turns serious her voice drops and
catches a little. Energetic and youthful but not babyish or cutesy."

Run-on sentences, exclamations, repeated words ("Out, out, out"), sound effects ("ba-dum"). Delivery tags:
`[excited, fast]`, `[teasing]`, `[laughing]`, `[serious, blunt]`, `[voice catching]`. Her serious lines are short and
should sound like she's stopped running for the first time, but at full speaking volume: tags like `[quiet]`,
`[soft]` or `[suddenly quiet]` make eleven_v3 whisper her, and those takes were too faint to hear. The generator also
levels every Hibiki take to the other souls' loudness (about -16 LUFS).

## Lines

Notation for `requires` is in [Souls.md](Souls.md#requirement-notation).

### `SoulPickup`: first meeting (Story, play once, one per run)

| id | Requires | Lines |
|---|---|---|
| `Hibiki_Meet_Out` | `!seen Hibiki_Meet_Out` | **Oni:** "It wants out as badly as I do."<br>**Hibiki:** "Out! Yes! Out, out, out, finally somebody listens!"<br>**Hibiki:** "Wait. Oni? It's you! It's really you!" |
| `Hibiki_Meet_Name` | `seen Hibiki_Meet_Out`, `runsSince Hibiki_Meet_Out ≥ 1` | **Hibiki:** "You don't know my name, do you."<br>**Oni:** "..."<br>**Hibiki:** "Hibiki. The festival drum? I followed you to the river every day for a whole summer?"<br>**Hibiki:** "It's fine! I'll tell you every time. I'm good at repeating things. I'm a drummer." |
| `Hibiki_Meet_Procession` | `seen Hibiki_Meet_Name`, `runsSince Hibiki_Meet_Name ≥ 1`, `runs ≥ 3` | **Hibiki:** "I drummed the whole way up the mountain. Everyone kept saying, our Oni sent for us! The whole village, in our best!"<br>**Hibiki:** "Ma cried, she was so proud of you."<br>**Hibiki:** "Don't. Don't make that face. I'll hit you with a stick." |
| `Hibiki_Meet_OneBell` | `seen Hibiki_Meet_Procession`, `runsSince Hibiki_Meet_Procession ≥ 1`, `runs ≥ 5` | **Hibiki:** "He gave the court bells. He gave the soldiers bells. He didn't give us any."<br>**Hibiki:** "The whole village rings inside the coffin. All of us, one sound. Listen for it when he swings that hammer." |
| `Hibiki_Meet_Blame` | `seen Hibiki_Meet_OneBell`, `runsSince Hibiki_Meet_OneBell ≥ 2` | **Hibiki:** "I told you I don't blame you. That was a lie."<br>**Hibiki:** "I do. A little. Every night. And I'd follow you up there again anyway. That's the stupid part."<br>**Oni:** "Hibiki."<br>**Hibiki:** "...You said my name." |
| `Hibiki_Meet_River` | `seen Hibiki_Meet_Blame`, `runsSince Hibiki_Meet_Blame ≥ 1`, `clears ≥ 1` | **Oni:** "The drum by the river. You played it until your hands bled. I remember."<br>**Hibiki:** "...You remembered on your own."<br>**Hibiki:** "Don't you dare forget it again, my lord guardsman." |

`Hibiki_Meet_River` marks her ready to be freed. It's the one place in the game where Oni wins back a memory by
himself; keep it his longest line to the souls.

### `SoulPickup`: reactive (Reactive)

| id | Requires | Lines |
|---|---|---|
| `Hibiki_Pickup_DiedBenibana` | `lastKiller = Benibana`, `runsSince Hibiki_Pickup_DiedBenibana ≥ 3` | **Hibiki:** "She got you with the sleepy smoke again? Stay awake! Do what I do. Never stop moving!" |
| `Hibiki_Pickup_DiedUtsusemi` | `lastKiller = Utsusemi`, `runsSince Hibiki_Pickup_DiedUtsusemi ≥ 3` | **Hibiki:** "The tea monk wouldn't hurt anybody. That thing would. Be faster than its ticking!" |
| `Hibiki_Pickup_DiedShirogane` | `lastKiller = Shirogane`, `runsSince Hibiki_Pickup_DiedShirogane ≥ 3` | **Hibiki:** "Lady Shirogane beat you? Of course she did. She's Lady Shirogane." |
| `Hibiki_Pickup_DiedKagetsune` | `lastKiller = Kagetsune`, `runsSince Hibiki_Pickup_DiedKagetsune ≥ 3` | **Hibiki:** "I heard the village ring when he hit you. All of us at once. ...Get up, Oni. Please." |
| `Hibiki_Pickup_DiedArmy` | `lastKiller = Army`, `runsSince Hibiki_Pickup_DiedArmy ≥ 3` | **Hibiki:** "Just soldiers? Just soldiers got you? Captain Kettle's going to be unbearable." |
| `Hibiki_Pickup_FirstClear` | `clears ≥ 1`, `lastCleared`, play once | **Hibiki:** "You did it! You did it, you did it! The whole coffin shook!"<br>**Hibiki:** "...And we're all still in here. Oh." <br>**Hibiki:** "That's fine! We'll do it again, louder!" |
| `Hibiki_Pickup_Cleared` | `lastCleared`, `seen Hibiki_Pickup_FirstClear`, `runsSince Hibiki_Pickup_Cleared ≥ 2` | **Hibiki:** "Again! Like last night! Ba-dum, down he goes!" |
| `Hibiki_Pickup_HoldsFire` | `holds Fire ≥ 1`, play once | **Hibiki:** "You smell like smoke. Was that Captain Kettle? He's so grumpy. Here, have something fun." |
| `Hibiki_Pickup_HoldsFrost` | `holds Frost ≥ 1`, play once | **Hibiki:** "Grandmother's been looking after you! She held my wrist, at the end. She's good at that." |
| `Hibiki_Pickup_HoldsWater` | `holds Water ≥ 1`, play once | **Hibiki:** "Great-Grandmother's water! She says yes, dear, when I call her that. Grandmother never does." |
| `Hibiki_Pickup_ManyRuns` | `runs ≥ 25`, play once | **Hibiki:** "I've been counting your nights in drumbeats. I've run out of rhythms. I'm making new ones." |
| `Hibiki_Pickup_VeryManyRuns` | `runs ≥ 50`, play once | **Hibiki:** "Everyone in the coffin knows your name now. Even the ones who never met you. I told them all." |
| `Hibiki_Pickup_Offering` | event offers `SpiritOffering` | **Hibiki:** "I'm all out of thunder! Um. Here's a hug instead. It's a soul hug. It counts." |

### `SoulPickup`: greetings (Repeatable)

| id | Lines |
|---|---|
| `Hibiki_Greet_Hi` | **Hibiki:** "Oni! Over here! Hi!" |
| `Hibiki_Greet_Faster` | **Hibiki:** "Faster, faster, faster!" |
| `Hibiki_Greet_Thunder` | **Hibiki:** "Ba-dum! That's thunder. That's me." |
| `Hibiki_Greet_Keep` | **Hibiki:** "Try to keep up, my lord guardsman!" |
| `Hibiki_Greet_Cheer` | **Hibiki:** "The village is cheering for you. Can you hear it?" |
| `Hibiki_Greet_Stop` | **Hibiki:** "Don't stop! Stopping's the worst." |
| `Hibiki_Greet_Catch` | **Hibiki:** "Here! Catch!" |
| `Hibiki_Greet_Drumroll` | **Hibiki:** "Drumroll, please... It's me again!" |

### `SoulJealous`

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Hibiki_Jealous_Akagane` | Reactive, once | `most = Fire` | **Hibiki:** "You picked Captain Kettle? Over me? He doesn't even smile!"<br>**Akagane:** "I smile when it's earned."<br>**Hibiki:** "When was the last time?"<br>**Akagane:** "...Take her boon, Guardsman." |
| `Hibiki_Jealous_Mizore` | Reactive, once | `most = Frost` | **Hibiki:** "You're all frosty and careful. Grandmother's made you sit still, hasn't she."<br>**Mizore:** "Someone has to."<br>**Hibiki:** "Not tonight!" |
| `Hibiki_Jealous_Asagi` | Reactive, once | `most = Water` | **Hibiki:** "You're all soggy. Did Great-Grandmother... no. I can't be cross with her. I tried."<br>**Asagi:** "Thank you, dear."<br>**Hibiki:** "Still! Hurry up!" |
| `Hibiki_Jealous_Fire` | Repeatable | `most = Fire` | **Hibiki:** "Fire's so slow. Watch this instead!" |
| `Hibiki_Jealous_Water` | Repeatable | `most = Water` | **Hibiki:** "Soggy! Come and be fast instead!" |
| `Hibiki_Jealous_Frost` | Repeatable | `most = Frost` | **Hibiki:** "Wake up, frosty! Thunder's here!" |
| `Hibiki_Jealous_Back` | Repeatable | none | **Hibiki:** "You came back to me! I knew it. Mostly knew it." |

### `Legendary` (Raijin's Drum)

| id | Tier | Lines |
|---|---|---|
| `Hibiki_Legendary_Drum` | Story, once | **Hibiki:** "My drum! He threw it in the coffin with us, can you believe that?"<br>**Hibiki:** "Every eighth beat's for the village. Count with me!" |
| `Hibiki_Legendary_Again` | Repeatable | **Hibiki:** "Six, seven, EIGHT!" |

### `Legendary` event: Infusion (Static Field)

| id | Tier | Lines |
|---|---|---|
| `Hibiki_Infusion_StaticField` | Story, once | **Hibiki:** "You know when you scuff your feet on the shrine mats and touch someone? Bzzt?"<br>**Hibiki:** "That, but the whole row! And the first one to draw a bow goes ba-DUM!" |
| `Hibiki_Infusion_Again` | Repeatable | **Hibiki:** "Whole row's buzzing, Oni!" |

### `Duo`

Her Duos (Plasma Arc with Akagane, Superconductor with Mizore) are in
[Souls.md](Souls.md#duo-conversations).

### Boss reactions

| id | Event | Tier | Requires | Lines |
|---|---|---|---|---|
| `Hibiki_Ahead_Benibana` | `BossAhead` | Story, once | `nextBoss = Benibana` | **Hibiki:** "The dancer's up ahead! She was so beautiful. Then everyone got so sleepy. ...Oh." |
| `Hibiki_Ahead_Utsusemi` | `BossAhead` | Story, once | `nextBoss = Utsusemi` | **Hibiki:** "That's the tea monk's hat! He let us kids wear it at the well. That's not him in there, Oni. It can't be." |
| `Hibiki_Ahead_UtsusemiTea` | `BossAhead` | Story, once | `nextBoss = Utsusemi`, `seen Utsusemi_Intro_WindDown` | **Hibiki:** "He asked you to wind him down. So he is in there. ...Be gentle. As gentle as you can." |
| `Hibiki_Ahead_Shirogane` | `BossAhead` | Story, once | `nextBoss = Shirogane` | **Hibiki:** "Lady Shirogane let me hold her sword once. Don't tell her I dropped it." |
| `Hibiki_Ahead_Kagetsune` | `BossAhead` | Story, once | `nextBoss = Kagetsune` | **Hibiki:** "Our drum against his bells. Let's be louder this time." |
| `Hibiki_Ahead_Any` | `BossAhead` | Repeatable | none | **Hibiki:** "Big one coming! Ba-dum, ba-dum!" |
| `Hibiki_Felled_Benibana` | `BossFelled` | Reactive, once | `felled = Benibana` | **Hibiki:** "Ha! ...Sorry. She's sad, isn't she. I shouldn't cheer." |
| `Hibiki_Felled_Utsusemi` | `BossFelled` | Reactive, once | `felled = Utsusemi` | **Hibiki:** "Sleep well, tea monk." |
| `Hibiki_Felled_Shirogane` | `BossFelled` | Reactive, once | `felled = Shirogane` | **Hibiki:** "She smiled at you. Did you see? Right at the end. She smiled!" |
| `Hibiki_Felled_Any` | `BossFelled` | Repeatable | none | **Hibiki:** "Down! Who's next?" |

### `SoulHub` (in the temple, after a run he mostly held their element)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Hibiki_Hub_First` | Story, once | `lastRunElement = Storm` | **Hibiki:** "You can see the village roofs from up here! ...Well. Where they were." |
| `Hibiki_Hub_Song` | Reactive | `lastRunElement = Storm`, `runsSince Hibiki_Hub_Song ≥ 4` | **Hibiki:** "Play the river song! ...No? Fine. I'll hum it." |
| `Hibiki_Hub_Up` | Repeatable | `lastRunElement = Storm` | **Hibiki:** "Up, up, up!" |

### Crossovers

Lines with the other characters (all voiced).

| id | Event | Tier | Requires | Lines |
|---|---|---|---|---|
| `Hibiki_Poguru` | `SoulPickup` | Reactive, once | `seen Hibiki_Meet_Name` | **Hibiki:** "Wait. Is that the festival dragon? The paper one, with the red cape?"<br>**Poguru:** "I'm a REAL dragon!"<br>**Hibiki:** "You've got its cape! We paraded that dragon round the well every summer!"<br>**Poguru:** "...So THAT'S why it smells like lanterns." |
| `Hibiki_Cut_Akagane` | `SoulPickup` | Reactive, rests 5 | `holds Fire`, `seen Hibiki_Meet_Out` | **Hibiki:** "Oni! Hi! Oh, you've got Captain Kettle's fire on you."<br>**Akagane:** "Captain Akagane, girl."<br>**Hibiki:** "Captain Kettle Akagane."<br>**Akagane:** "...Take her boon before I boil." |
| `Hibiki_Hub_RiverSong` | `SoulHub` | Story | `lastRunElement = Storm`, `seen Hibiki_Meet_River` | **Hibiki:** "You remembered the river. Do you remember the song too?"<br>**Oni:** "...It goes up at the end."<br>**Hibiki:** "It goes UP at the end! Yes! Ba-dum!" |
| `Hibiki_Pickup_Silk` | `SoulPickup` | Reactive, once | `got Relic_Silk` (Kite Silk ever won) | **Hibiki:** "Kite silk! We flew kites off the river bank every festival. Mine always crashed."<br>**Hibiki:** "Into Captain Kettle, once. Best day of my life." |
| `Hibiki_Laughed` | `SoulPickup` | Story | `seen Shirogane_Intro_Storm`, `seen Hibiki_Meet_Name` | **Oni:** "Shirogane said to tell you. About the sword in the mud. She laughed."<br>**Hibiki:** "...She LAUGHED? At me?"<br>**Hibiki:** "...Good. Good! I'll take it. Tell her I'm still sorry about the mud." |

## Arc across runs

Pure joy at finding him, then the hurt of being forgotten, laughed off. Then the procession, the pride that's
become the worst part. Then the one bell, the village that wasn't even given its own. Then the confession: she does
blame him, and would follow him anyway. He says her name. After a first clear he remembers the river and the drum
on his own, the one memory in the game he wins back himself.

## Freed (the ending)

When the coffin bursts in Kagetsune's defeat, the village's sound is the first to stop: not one bell but a rattle
of many small ones, then quiet. If `Hibiki_Meet_River` has played, a crowd of teal souls rises together and one runs
ahead of the rest:

| Moment | Line |
|---|---|
| The village goes quiet | `Hibiki_Freed`: **Hibiki:** "We're out! Everyone, we're out! ...I'll tell them you brought us home, Oni." |

At dawn, under Oni's flute, a single drumbeat answers from far down the valley.

## Keep consistent

- She's a teenager from a farming village, not a warrior. Her bravery is noise and motion.
- She jokes; she's the only one of the souls who does. Her jokes stop dead when it matters.
- Nothing romantic between her and Oni. She's the village's little sister, and he's the one who left.
- She calls Oni **"Oni"** or **"my lord guardsman"**; Mizore "Grandmother"; Akagane "Captain Kettle".
- Storm violet and indigo; no teal until she's freed.
