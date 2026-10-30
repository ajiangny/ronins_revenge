# Mizore, the Court Physician

[Back to the story](README.md) · [The three souls](Souls.md)

| | |
|---|---|
| Role | The **Frost** soul. She speaks for every Frost boon. |
| Was | Physician to the imperial court for forty years. She attended the old Emperor's death, and the boy who became Kagetsune. |
| Is | A soul half out of the coffin. Her bell is the small one he hung closest to his heart. |
| Wants | To finish treating the one patient she failed. She hasn't decided whether that's Oni or Kagetsune. |
| Helps Oni because | A patient doesn't have to deserve treatment. And because she saw the armor coming and said nothing to anyone but Shirogane. |
| Signature | Frost on her sleeves, a silver needle, a medicine case at her sash, steady hands. |

*Mizore* is sleet: rain that has started to freeze. It suits someone who was warm once and decided cold was more
useful.

## Look

**The painting.** Her talk-scene portrait (`Assets/Art/Portraits/Mizore_Closeup.png`, with `Mizore_Full.png`) is
painted in the boss portraits' style (GPT image gen). **She's a white crane-woman** (she was a human, then a wolf-woman,
in earlier passes). The brief, for the paintings and the boon screen's pixel bust:

- Around seventy, small and very upright. A **pure white crane's head** and long neck, the feathers a little thin
  and ragged at the crown, the crest plumes swept back and stiff with frost. A long, narrow grey-horn beak. Pale
  ice-blue eyes that look at you like a wound.
- A white **feather ruff** at the collar. **Court robes** in layered white and pale grey, cut long like old court
  dress, the borders trimmed in dark slate, patterned with snowflakes and frost ferns (not Asagi's waves), with a
  long slate-indigo sash tail. Frost creeps along the hems. The wide sleeves fray into **wing feathers**, white
  shading to slate and frost blue at the tips.
- **Talon hands**, dark grey and scaled. She holds a thin **silver needle** up between two talons, perfectly still,
  the claws rimed with frost. On the full figure, thin crane legs and talons show under the hem.
- An **inrō medicine case** at her sash, lacquered black with a white crane.
- Snow drifts very slowly around her, not falling: hanging.
- **Her bell:** a small bronze bell, cracked, on a fine chain over her heart.
- Palette: the Frost element colour (`#59BFFF`) for the glow, frost and eyes; white, pale grey and slate for the
  robes and feathers. **No red** (no crown patch), and **no teal** until she's freed.
- **Her aura** (`Fx/Aura_Water`, Oni wears it while a Frost Shadow Ultimate runs): slow frost wisps rising out of a
  ring of ice crystals, snowflakes drifting round them. Calm, no flame.

## History

- **The physician.** She came to the palace young and stayed forty years. She set bones for guardsmen, eased
  births for ladies of the court, treated a dancer's ankles without asking about the rosary under her sleeve. She
  set Oni's collarbone when he was sixteen and a guard recruit; he was very rude about it.
- **The old Emperor.** Kagetsune's father died slowly, of a wasting no medicine touched. Mizore sat with him
  through the whole of it, and his son, nine years old, sat beside her holding her sleeve. When the boy asked
  whether it would happen to him too, she told him the truth: "Everyone dies, my prince. Even emperors." She
  thinks that sentence was the first piece of the armor.
- **The smiths.** Years later six smiths came to her coughing ash, all from the same secret forge. All were dead
  by spring. She guessed what they had been making, and told no one at court.
- **Shirogane.** Before the coronation Shirogane came to her asking about the smiths. Mizore told her everything.
  Shirogane went to find Oni that same night; that was where their oath came from.
- **The Night.** In the hall she knew Benibana's smoke by its smell, datura and something sweeter, and had named it
  before she noticed she couldn't lift her hands. She spent the end beside a village girl whose drum had stopped,
  counting the girl's pulse until she couldn't count. That girl is [Hibiki](Hibiki.md).
- **Since.** The quiet souls, the ones who go into the coffin still and frightened, slow down near her the way a
  fever slows under a cold cloth. She keeps them out a breath longer and passes them to Oni as Frost souls.

## How a Frost soul reaches Oni

A soul that dies afraid goes still. Mizore holds the still ones the way she held patients: steady, and cold enough
to keep them from spoiling. She sends them to Oni as Frost: freezes, ice, a steadier hand, more life.

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | Her patient. She's dry and clinical with him and never once cruel; she watches his memories go the way she watched the old Emperor's flesh go, and she minds. She calls him **"Oni"**, plainly, and sometimes "young man". |
| [Kagetsune](Kagetsune.md) | The frightened boy holding her sleeve. She is the only one who still thinks of him as a child, and the only one who pities him. It doesn't make her want him stopped any less. |
| [Benibana](Benibana.md) | She treated the dancer's ankles and saw the rosary, and said nothing. She's glad she said nothing. She doesn't blame her for the smoke; she blames the man who asked for it. |
| [Utsusemi](Utsusemi.md) | A professional horror. A soul wound into gears: she'd like to see how it was done, so she could undo it. |
| [Shirogane](Shirogane.md) | She told Shirogane what she suspected. She has never decided whether that was the right thing, since it got her killed. |
| [Akagane](Akagane.md) (Fire) | The patient who never followed instructions. She set his arm; he called her a butcher. She calls him "Captain" in a tone that makes it a diagnosis. |
| [Hibiki](Hibiki.md) (Storm) | The girl whose pulse she counted. She's gentler with Hibiki than with anyone and would deny it. Hibiki calls her "Grandmother"; Mizore says "I'm not your grandmother" every single time, and never means it less. |
| [Asagi](Asagi.md) (Water) | Her teacher. Mizore climbed the hill at twelve, a herb-gatherer's daughter, and was taught for six years; then she went to the palace and never came back up, and has not decided whether that was cowardice. The only person she gives a title: **"Teacher"**. Asagi calls her "little sleet". They have disagreed about warmth for forty years. Their Duo, Black Ice, is the one place water turns to ice. |

**Jealousy.** Quiet and cutting. She thinks Storm's speed will get Oni killed ("quick is how fevers spread") and
that Fire is a patient refusing to rest. She doesn't compete; she just waits for him to come back to the cold. Of
Water she says nothing cutting at all, which from her is a tell.

## Voice

**In game:** voiced in ElevenLabs (`MIZORE` in `Tools/generate-voice.mjs`), designed from the prompt above. Every
`SoulPickup` line is voiced; the souls share a faint coffin echo (`GameManager.soulVoice`).

**ElevenLabs voice design prompt:** "An elderly woman of about seventy with a low, dry, unhurried voice and a slight
rasp. No accent, very clear diction. Precise and clinical, like a doctor giving a diagnosis; she never raises her
voice. Under the coldness, a reluctant warmth. Quiet and close, as if speaking at a bedside."

Short declaratives. Medical words used plainly: pulse, fever, wound, rest. Delivery tags: `[dry]`, `[clinical]`,
`[quiet]`, `[softer]`, `[tired]`. The only time her voice warms is with Hibiki and in `Mizore_Meet_Prince`.

## Lines

Notation for `requires` is in [Souls.md](Souls.md#requirement-notation).

### `SoulPickup`: first meeting (Story, play once, one per run)

| id | Requires | Lines |
|---|---|---|
| `Mizore_Meet_Steady` | `!seen Mizore_Meet_Steady` | **Oni:** "Cold hands. Steady ones."<br>**Mizore:** "Hold still. I can't treat a man who flinches."<br>**Mizore:** "There. The cold will keep it clean." |
| `Mizore_Meet_Collarbone` | `seen Mizore_Meet_Steady`, `runsSince Mizore_Meet_Steady ≥ 1` | **Mizore:** "Mizore. Physician to the court. I set your collarbone when you were sixteen. You were very rude about it."<br>**Oni:** "...I don't remember."<br>**Mizore:** "No. The coffin takes the small things first. I've been watching it take yours." |
| `Mizore_Meet_Smoke` | `seen Mizore_Meet_Collarbone`, `runsSince Mizore_Meet_Collarbone ≥ 1`, `runs ≥ 3` | **Mizore:** "I knew that smoke by its smell. Datura, and something sweeter."<br>**Mizore:** "By the time I'd named it, I couldn't lift my hands. I had never been too slow before." |
| `Mizore_Meet_Prince` | `seen Mizore_Meet_Smoke`, `runsSince Mizore_Meet_Smoke ≥ 1`, `runs ≥ 5` | **Mizore:** "I sat with his father through the whole of his dying. The boy held my sleeve all night."<br>**Mizore:** "He asked if it would happen to him. I told him the truth. Everyone dies, my prince. Even emperors."<br>**Mizore:** "I think I forged the first piece of that armor, that night." |
| `Mizore_Meet_Smiths` | `seen Mizore_Meet_Prince`, `runsSince Mizore_Meet_Prince ≥ 1`, `deathsTo Shirogane ≥ 1` | **Mizore:** "Six smiths came to me coughing ash, the year before the coronation. All dead by spring."<br>**Mizore:** "I told no one at court. I told Shirogane, when she came asking. Then she went looking for you." |
| `Mizore_Meet_Oath` | `seen Mizore_Meet_Smiths`, `seen Shirogane_Intro_Oath`, `runsSince Mizore_Meet_Smiths ≥ 1` | **Mizore:** "So she gave you the oath back."<br>**Mizore:** "She came to me after you'd sworn it. She looked lighter than I'd seen her in years. She said you'd hesitate, and she'd forgive it."<br>**Oni:** "Did she."<br>**Mizore:** "She did. Now stop hesitating. That's my prescription." |

`Mizore_Meet_Oath` marks her ready to be freed. It needs Shirogane's oath line, so she is the last of the three to
finish, by design: her story closes Shirogane's loop.

### `SoulPickup`: reactive (Reactive)

| id | Requires | Lines |
|---|---|---|
| `Mizore_Pickup_DiedBenibana` | `lastKiller = Benibana`, `runsSince Mizore_Pickup_DiedBenibana ≥ 3` | **Mizore:** "Her smoke again. Shallow breaths, Oni. The dose is in the depth." |
| `Mizore_Pickup_DiedUtsusemi` | `lastKiller = Utsusemi`, `runsSince Mizore_Pickup_DiedUtsusemi ≥ 3` | **Mizore:** "The clockwork keeps perfect time. You don't have to. Be early." |
| `Mizore_Pickup_DiedShirogane` | `lastKiller = Shirogane`, `runsSince Mizore_Pickup_DiedShirogane ≥ 3` | **Mizore:** "She cut you to the bone and left every vital alone. That isn't an accident. It's a lesson." |
| `Mizore_Pickup_DiedKagetsune` | `lastKiller = Kagetsune`, `runsSince Mizore_Pickup_DiedKagetsune ≥ 3` | **Mizore:** "He's never lost anything in his life except his father. Take something from him. He won't know what to do." |
| `Mizore_Pickup_DiedArmy` | `lastKiller = Army`, `runsSince Mizore_Pickup_DiedArmy ≥ 3` | **Mizore:** "Arrows. Of all things. You were careless, and you know it." |
| `Mizore_Pickup_FirstClear` | `clears ≥ 1`, `lastCleared`, play once | **Mizore:** "He fell, and he got up again. Like his father, at the end. He'd hate to know that."<br>**Mizore:** "Again, then. I'll be here." |
| `Mizore_Pickup_Cleared` | `lastCleared`, `seen Mizore_Pickup_FirstClear`, `runsSince Mizore_Pickup_Cleared ≥ 2` | **Mizore:** "You won last night. Your pulse is calmer for it. Keep it that way." |
| `Mizore_Pickup_HoldsFire` | `holds Fire ≥ 1`, play once | **Mizore:** "You're running hot. That'll be the Captain. Let me take the edge off." |
| `Mizore_Pickup_HoldsStorm` | `holds Storm ≥ 1`, play once | **Mizore:** "You have Hibiki's spark in you. Mind it. She was always quicker than was good for her." |
| `Mizore_Pickup_HoldsWater` | `holds Water ≥ 1`, play once | **Mizore:** "You've been to the pond. ...She taught me. Everything I know about hands. Don't tell her I said so." |
| `Mizore_Pickup_ManyRuns` | `runs ≥ 25`, play once | **Mizore:** "Most patients die once. You've made a career of it." |
| `Mizore_Pickup_VeryManyRuns` | `runs ≥ 50`, play once | **Mizore:** "I've stopped writing your case down. I ran out of paper in the coffin." |
| `Mizore_Pickup_LowLives` | `lives = 1` | **Mizore:** "One life left, and you're still picking fights. Sit. Let me see." |
| `Mizore_Pickup_Offering` | event offers `SpiritOffering` | **Mizore:** "I've no more to teach your hands. I can still close a wound." |

### `SoulPickup`: greetings (Repeatable)

| id | Lines |
|---|---|
| `Mizore_Greet_Still` | **Mizore:** "Hold still." |
| `Mizore_Greet_Breathe` | **Mizore:** "Breathe slower. There." |
| `Mizore_Greet_Clean` | **Mizore:** "Cold keeps a wound clean." |
| `Mizore_Greet_Bleeding` | **Mizore:** "You're bleeding again. Of course you are." |
| `Mizore_Greet_Hands` | **Mizore:** "Steady hands, Oni. Borrow mine." |
| `Mizore_Greet_Pulse` | **Mizore:** "Your pulse is racing. Let me slow it." |
| `Mizore_Greet_Patient` | **Mizore:** "Another night. Another patient." |
| `Mizore_Greet_Thanks` | **Mizore:** "Don't thank me. Survive." |

### `SoulJealous`

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Mizore_Jealous_Akagane` | Reactive, once | `most = Fire` | **Mizore:** "You're burning up, Oni. The Captain never knew when to stop either."<br>**Akagane:** "And yet I'm the one he keeps choosing."<br>**Mizore:** "Fevers are popular, Captain. Briefly." |
| `Mizore_Jealous_Hibiki` | Reactive, once | `most = Storm` | **Mizore:** "All that speed. Quick is how fevers spread."<br>**Hibiki:** "Quick is how you get there first, Grandmother!"<br>**Mizore:** "I'm not your grandmother." |
| `Mizore_Jealous_Asagi` | Reactive, once | `most = Water` | **Mizore:** "You're wet through and warm. That's her way. It was always her way."<br>**Asagi:** "It works, little sleet."<br>**Mizore:** "...It does. Don't let it go to your bowl." |
| `Mizore_Jealous_Fire` | Repeatable | `most = Fire` | **Mizore:** "Too hot. Sit down." |
| `Mizore_Jealous_Water` | Repeatable | `most = Water` | **Mizore:** "Too warm. Too wet. ...Fine." |
| `Mizore_Jealous_Storm` | Repeatable | `most = Storm` | **Mizore:** "Slow down before you shake apart." |
| `Mizore_Jealous_Back` | Repeatable | none | **Mizore:** "Back for the cold, then. They always come back for the cold." |

### `Legendary` (Still Water)

| id | Tier | Lines |
|---|---|---|
| `Mizore_Legendary_StillWater` | Story, once | **Mizore:** "Still Water. Slow the pulse and a fever loses its hurry."<br>**Mizore:** "So does an army." |
| `Mizore_Legendary_Again` | Repeatable | **Mizore:** "Slower. Slower. There." |

### `Legendary` event: Infusion (Hoarfrost)

| id | Tier | Lines |
|---|---|---|
| `Mizore_Infusion_Hoarfrost` | Story, once | **Mizore:** "Hoarfrost. It settles on whatever is too slow to shake it off."<br>**Mizore:** "Their arrows will crawl, Oni. Watch them come, and step aside. No need to hurry." |
| `Mizore_Infusion_Again` | Repeatable | **Mizore:** "Let the frost settle." |

### `Duo`

Her Duos (Thermal Shock with Akagane, Superconductor with Hibiki) are in
[Souls.md](Souls.md#duo-conversations).

### Boss reactions

| id | Event | Tier | Requires | Lines |
|---|---|---|---|---|
| `Mizore_Ahead_Benibana` | `BossAhead` | Story, once | `nextBoss = Benibana` | **Mizore:** "The dancer came to me for her ankles. I saw the rosary under her sleeve and said nothing. I'm glad I said nothing." |
| `Mizore_Ahead_Utsusemi` | `BossAhead` | Story, once | `nextBoss = Utsusemi` | **Mizore:** "A soul wound into gears. I'd like to see how it was done. Then I'd like to undo it." |
| `Mizore_Ahead_Shirogane` | `BossAhead` | Story, once | `nextBoss = Shirogane` | **Mizore:** "She guards high on the left. She always did. I stitched that side of her more than once." |
| `Mizore_Ahead_Kagetsune` | `BossAhead` | Story, once | `nextBoss = Kagetsune` | **Mizore:** "Remember that he's afraid, Oni. It's all he has ever been." |
| `Mizore_Ahead_KagetsuneWon` | `BossAhead` | Story, once | `nextBoss = Kagetsune`, `winsVs Kagetsune ≥ 1` | **Mizore:** "He was nine, and holding my sleeve. Now he's holding a thousand of us instead. Let go of him for me." |
| `Mizore_Ahead_Any` | `BossAhead` | Repeatable | none | **Mizore:** "Something heavy ahead. Breathe before you go in." |
| `Mizore_Felled_Benibana` | `BossFelled` | Reactive, once | `felled = Benibana` | **Mizore:** "She'll mend. They always come back, here. That's the sickness." |
| `Mizore_Felled_Utsusemi` | `BossFelled` | Reactive, once | `felled = Utsusemi` | **Mizore:** "Wound down, for a while. Kinder than anything else in these mountains." |
| `Mizore_Felled_Shirogane` | `BossFelled` | Reactive, once | `felled = Shirogane` | **Mizore:** "She was smiling under that. She hid pain from me the same way." |
| `Mizore_Felled_Any` | `BossFelled` | Repeatable | none | **Mizore:** "Done. Let me look at you." |

### `SoulHub` (in the temple, after a run he mostly held their element)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Mizore_Hub_First` | Story, once | `lastRunElement = Frost` | **Mizore:** "Her flute, on the altar. You still look at it like it might break. Good." |
| `Mizore_Hub_Kagetsune` | Reactive | `lastRunElement = Frost`, `lastKiller = Kagetsune`, `runsSince Mizore_Hub_Kagetsune ≥ 4` | **Mizore:** "You'll live. That's my professional opinion. It's been wrong every night so far." |
| `Mizore_Hub_Rest` | Repeatable | `lastRunElement = Frost` | **Mizore:** "Rest. Then go again." |

### Crossovers

Lines with the other characters (all voiced).

| id | Event | Tier | Requires | Lines |
|---|---|---|---|---|
| `Mizore_Poguru` | `SoulPickup` | Reactive, once | `seen Mizore_Meet_Collarbone` | **Mizore:** "Your little dragon is running warm. Hold still, creature."<br>**Poguru:** "Is it bad? Do I have a fever?"<br>**Mizore:** "Dragons don't get fevers."<br>**Poguru:** "...Do souls?"<br>**Mizore:** "...Take the boon, Oni." |
| `Mizore_Cut_Hibiki` | `SoulPickup` | Reactive, rests 5 | `holds Storm`, `seen Mizore_Meet_Steady` | **Mizore:** "Hold still, Oni. Let me see."<br>**Hibiki:** "He CAN'T hold still, Grandmother, he's got my spark!"<br>**Mizore:** "Then I'll work quickly." |
| `Mizore_Hub_Sleep` | `SoulHub` | Story | `lastRunElement = Frost`, `runs ≥ 6` | **Mizore:** "Do you sleep, in between?"<br>**Oni:** "...I play."<br>**Mizore:** "That'll do. Doctor's orders." |
| `Mizore_Pickup_Spring` | `SoulPickup` | Reactive, once | `got Item_Spring` (a Healing Spring path ever taken) | **Mizore:** "Spring water. Good. A clean wound and a full heart, in that order." |
| `Mizore_Pickup_Thread` | `SoulPickup` | Reactive, once | `got Item_Thread` (a Spirit Thread ever offered) | **Mizore:** "Someone's been stitching your boons finer. Neat work. Not mine. I'd have used fewer knots." |

## Arc across runs

A clinician at a bedside, then the physician who remembers Oni's past better than he does, then the smoke she was
too slow to name, then the boy holding her sleeve, the confession that she helped make Kagetsune. Then the smiths,
and the admission that she told Shirogane and nobody else. Once Shirogane has given Oni the oath, Mizore gives him
the last piece of it: Shirogane forgave his hesitation before it happened. She ends where she started, prescribing.

## Freed (the ending)

Her bell is the small cracked one closest to Kagetsune's heart, the first he ever hung. When the armor sinks hollow
and the bells go silent, hers is the last before Shirogane's. If `Mizore_Meet_Oath` has played, she rises in teal
over the place he fell, and speaks to the boy, not the Emperor:

| Moment | Line |
|---|---|
| Her bell stops | `Mizore_Freed`: **Mizore:** "There, my prince. Everyone rests. Even emperors." |

## Keep consistent

- She is never sentimental out loud, except with Hibiki and about the boy Kagetsune was.
- She doesn't excuse Kagetsune. Pity isn't forgiveness.
- She calls Oni **"Oni"** or "young man". Never "Guardsman" (Akagane's) or "little demon".
- Her medicine is real-world plain: needles, cold cloths, bone-setting. No magic healing words.
- Frost blue, slate and white; no teal until she's freed.
- She's a white crane with talon hands, not a wolf: lines about her look (Poguru's "bird granny") say bird, never fur or fangs.
