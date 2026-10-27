# Akagane, Captain of the Vanguard

[Back to the story](README.md) · [The three souls](Souls.md)

| | |
|---|---|
| Role | The **Fire** soul. He speaks for every Fire boon, the way an Olympian is their boons. |
| Was | Captain of the Emperor's vanguard: the army. Noble-born, the third-best blade in the empire. |
| Is | A soul half out of the coffin. His bell still hangs on Kagetsune's armor; the chain is just long enough. |
| Wants | His men stood down and sent home. Then, maybe, to forgive one of the two people he blames. |
| Helps Oni because | He hates Kagetsune more than he hates Oni, and Oni is the only blade left that reaches the Emperor. "I'm not lending you my fire. I'm aiming it." |
| Signature | Copper-lacquered armor, a scorched war fan (gunbai), embers, a bark. |

*Akagane* is red copper. The guard called Shirogane "white silver" and Oni "the demon"; Akagane was the copper,
always one step below them, and he never once pretended not to mind.

## Look

For the boon screen's pixel bust (head and shoulders, facing three-quarters toward Oni's portrait):

- A broad man in his mid-forties with a square, scarred jaw and a close beard going grey, soot on his cheekbones.
  Ember-orange eyes, the glow strongest at the pupils.
- **Copper-red lacquered armor** (do-maru and broad shoulder plates), the vanguard's crest on the chest. The left
  shoulder plate is burned black and cracked, as if from inside.
- His **kabuto** has lost half its crescent crest: one horn of the crescent snapped off on the Night.
- A **gunbai war fan** held up at his shoulder, scorched at the edge, the vanguard's command fan.
- Embers rise from his collar and the gaps in his armor, and his outline flickers like heat-haze at the top.
- **His bell:** a bronze bell hangs at his chest on a chain that runs out of frame (toward the Emperor). Bound souls
  always show their chain.
- Palette: the Fire element colour (`#FF6133`) for the glow and embers, copper and lacquer red for the armor, soot
  black. **No teal**: his fire is what holds him out of the coffin; teal is only for when he is freed.

## History

- **The copper.** Born to a minor house of the capital, trained from childhood for the guard. He won every
  tournament except the ones Shirogane entered. When the Emperor chose his two personal blades he chose Shirogane
  and a village boy, and named the village boy his demon. Akagane got the vanguard: an honour, and a consolation.
- **The captain.** He was a good one. He knew every man's name and every man's mother's name. His men would have
  marched into the sea for him, and on the Night of a Thousand Bells they marched up the mountain instead.
- **The doors.** At the coronation Kagetsune ordered the vanguard to hold the hall's doors, facing out. Akagane
  set them there himself. When Benibana's smoke came through the hall his men were the last to feel it: they heard
  the coffin open behind them, and couldn't turn around in time. He was guarding the wrong side of the door.
- **Since.** His men are the army Oni cuts down every night, sent out in borrowed bodies. When one of them falls
  angry enough, Akagane catches the soul before the coffin can, and lends its fire to the one man he'd most like to
  hit.
- **The Bellringers** at the Mountain Shrine were his shrine watch. Their tolls are what drive his men forward now.

## How a Fire soul reaches Oni

When a samurai dies, its soul is dragged toward the coffin. Akagane is the soul who learned to pull back. Angry
souls catch on him like sparks on tinder, and he hands them on to Oni as a Fire soul. The boon is one of his men;
Akagane is the voice behind it.

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | Rival, then grudging ally. He resented the village boy who got the place he trained his whole life for, and now that boy cuts down his men every night. He calls Oni **"Guardsman"**, the rank he never got. Underneath: they made the same mistake, at the same doors, on the same night. |
| [Kagetsune](Kagetsune.md) | His lord. Twenty years on his knees. His anger at Kagetsune is the hottest thing he has, and the only anger he never jokes about. |
| [Benibana](Benibana.md) | Her smoke put his whole vanguard on its knees. He curses her by name, until she tells Oni which bell is hers; after that he stops, mostly. |
| [Utsusemi](Utsusemi.md) | His men, in borrowed bodies, took the monk for sheltering Oni. He remembers marching, not choosing. It shames him. |
| [Shirogane](Shirogane.md) | She beat him at every tournament and was kind about it, which was worse. He respects her more than anyone alive or dead, and won't say so. |
| [Mizore](Mizore.md) (Frost) | She set his broken arm once, at the palace, and he called her a butcher. She calls him a patient who never followed instructions. Mutual respect neither admits; they bicker like an old married staff. |
| [Hibiki](Hibiki.md) (Storm) | A village girl who should never have been on the mountain. Her drum set the army's pace up it, and he hasn't forgiven the drum. He is gruff with her and quietly protective; she calls him "Captain Kettle" because he boils. |

**Jealousy.** He is jealous of Hibiki above all: Oni taking a village girl's speed over a soldier's fire is an
insult to the vanguard. Of Mizore, less: he thinks cold is for people who've given up.

## Voice

**ElevenLabs voice design prompt:** "A man in his mid-forties with a gravelly, weathered baritone and a
parade-ground bark. No accent, clear diction. Clipped, impatient sentences with heat simmering under every word:
a soldier's anger kept on a short leash. Occasional dry, grudging humour. Close and warm, as if speaking across a
campfire."

Short, hard sentences, imperatives. He swears by the vanguard, never by the Emperor. Delivery tags for
`generate-voice.mjs`: `[gruff]`, `[barking an order]`, `[dry]`, `[low, bitter]`, `[grudging]`. The one time he
goes quiet (`Akagane_Meet_Doors`) should land.

## Lines

Notation for `requires` is in [Souls.md](Souls.md#requirement-notation). Every conversation's lines play in order;
Oni's lines are subtitle-only until he has a voice.

### `SoulPickup`: first meeting (Story, play once, one per run)

| id | Requires | Lines |
|---|---|---|
| `Akagane_Meet_Angry` | `!seen Akagane_Meet_Angry` | **Oni:** "Warm... Someone in there is still angry."<br>**Akagane:** "Angry. Yes. That's the word for it. Hello, Guardsman."<br>**Akagane:** "Take the fire and burn them quick. They're only borrowed bodies." |
| `Akagane_Meet_Copper` | `seen Akagane_Meet_Angry`, `runsSince Akagane_Meet_Angry ≥ 1` | **Akagane:** "You don't remember me. Akagane. Captain of the vanguard."<br>**Akagane:** "They called her silver. They called me copper. They called you demon, and you liked it."<br>**Oni:** "...I did." |
| `Akagane_Meet_Men` | `seen Akagane_Meet_Copper`, `runsSince Akagane_Meet_Copper ≥ 1`, `runs ≥ 3` | **Akagane:** "Every samurai you cut down tonight was one of mine. I knew their names."<br>**Akagane:** "So cut clean. Don't make them feel it twice." |
| `Akagane_Meet_Doors` | `seen Akagane_Meet_Men`, `runsSince Akagane_Meet_Men ≥ 1`, `runs ≥ 5` | **Akagane:** "You led them up the mountain. I set my men at the hall doors. Facing out, as he ordered."<br>**Akagane:** "We were guarding the wrong side of the door, Guardsman. Both of us."<br>**Oni:** "You couldn't have known."<br>**Akagane:** "Neither could you. That's what I can't forgive." |
| `Akagane_Meet_Bellringers` | `seen Akagane_Meet_Doors`, `runsSince Akagane_Meet_Doors ≥ 1`, `deathsTo Kagetsune ≥ 1` | **Akagane:** "Those bell-bearers at the shrine were my watch. Good men. Their bells drive the rest."<br>**Akagane:** "Silence them first. My men stop running at you when the ringing stops." |
| `Akagane_Meet_Salute` | `seen Akagane_Meet_Bellringers`, `runsSince Akagane_Meet_Bellringers ≥ 1`, `clears ≥ 1` | **Akagane:** "I've decided something. When this is done, I'm going to salute you."<br>**Akagane:** "Once. Don't get used to it."<br>**Oni:** "When this is done." |

`Akagane_Meet_Salute` marks him ready to be freed (see [Freed](#freed-the-ending)).

### `SoulPickup`: reactive (Reactive)

| id | Requires | Lines |
|---|---|---|
| `Akagane_Pickup_DiedBenibana` | `lastKiller = Benibana`, `runsSince Akagane_Pickup_DiedBenibana ≥ 3` | **Akagane:** "Her smoke again? Hold your breath and cut through it. That's all I'd have needed." |
| `Akagane_Pickup_DiedUtsusemi` | `lastKiller = Utsusemi`, `runsSince Akagane_Pickup_DiedUtsusemi ≥ 3` | **Akagane:** "The clockwork had you counted. Stop standing where it expects." |
| `Akagane_Pickup_DiedShirogane` | `lastKiller = Shirogane`, `runsSince Akagane_Pickup_DiedShirogane ≥ 3` | **Akagane:** "She beat you. She beat me too, for twenty years. It doesn't get easier. You get better." |
| `Akagane_Pickup_DiedKagetsune` | `lastKiller = Kagetsune`, `runsSince Akagane_Pickup_DiedKagetsune ≥ 3` | **Akagane:** "You reached him. Further than my whole vanguard got. Now go further." |
| `Akagane_Pickup_DiedArmy` | `lastKiller = Army`, `runsSince Akagane_Pickup_DiedArmy ≥ 3` | **Akagane:** "My men put you down last night. Don't expect an apology. They were good at their work." |
| `Akagane_Pickup_FirstClear` | `clears ≥ 1`, `lastCleared`, play once | **Akagane:** "He fell. I felt it. Every bell on him shook."<br>**Akagane:** "And here we all are again. So. Again, Guardsman." |
| `Akagane_Pickup_Cleared` | `lastCleared`, `seen Akagane_Pickup_FirstClear`, `runsSince Akagane_Pickup_Cleared ≥ 2` | **Akagane:** "You broke him last night. My men felt the chains slack. Do it again." |
| `Akagane_Pickup_HoldsFrost` | `holds Frost ≥ 1`, play once | **Akagane:** "You've been to the physician. She'll have told you to rest. Ignore her." |
| `Akagane_Pickup_HoldsStorm` | `holds Storm ≥ 1`, play once | **Akagane:** "That's the drummer's spark on you. Speed. Hm. My men marched up the mountain to her beat." |
| `Akagane_Pickup_ManyRuns` | `runs ≥ 25`, play once | **Akagane:** "My men have marched every night since the Night. So have you. I'd stopped counting. You haven't stopped coming." |
| `Akagane_Pickup_VeryManyRuns` | `runs ≥ 50`, play once | **Akagane:** "Fifty nights, give or take. I'd have promoted you by now. Don't let it go to your head." |
| `Akagane_Pickup_ManyDeaths` | `deathsTo Benibana ≥ 5`, play once | **Akagane:** "Five times the dancer. My vanguard only needed the once." |
| `Akagane_Pickup_Offering` | event offers `SpiritOffering` | **Akagane:** "Nothing left to burn. Fine. Have the warmth, then." |

### `SoulPickup`: greetings (Repeatable)

| id | Lines |
|---|---|
| `Akagane_Greet_Lit` | **Akagane:** "Fire's lit, Guardsman. Use it." |
| `Akagane_Greet_Bodies` | **Akagane:** "Burn the bodies. Not the men." |
| `Akagane_Greet_Vanguard` | **Akagane:** "Vanguard, forward. That's you now." |
| `Akagane_Greet_Standing` | **Akagane:** "Hm. Still standing. Good." |
| `Akagane_Greet_Waiting` | **Akagane:** "My men are waiting. Don't keep them." |
| `Akagane_Greet_Change` | **Akagane:** "Take it, before I change my mind." |
| `Akagane_Greet_Copper` | **Akagane:** "Copper's still warm." |
| `Akagane_Greet_Clean` | **Akagane:** "Cut clean. Burn quick." |

### `SoulJealous`

When Oni mostly holds another soul's boons (see [Souls.md](Souls.md#souljealous)).

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Akagane_Jealous_Hibiki` | Reactive, once | `most = Storm` | **Akagane:** "The drummer's spark, all over you. You'd take a village girl's speed over the vanguard's fire?"<br>**Hibiki:** "He would! He did!"<br>**Akagane:** "Quiet, girl." |
| `Akagane_Jealous_Mizore` | Reactive, once | `most = Frost` | **Akagane:** "You're half frozen, Guardsman. The physician's cold is for people who've given up."<br>**Mizore:** "The physician's cold is why you still have two arms, Captain." |
| `Akagane_Jealous_Storm` | Repeatable | `most = Storm` | **Akagane:** "Fast won't save you. Fire will." |
| `Akagane_Jealous_Frost` | Repeatable | `most = Frost` | **Akagane:** "Thaw out. Here." |
| `Akagane_Jealous_Back` | Repeatable | none (the event already means another soul leads) | **Akagane:** "Remembered who your captain is?" |

### `Legendary` (Wildfire)

| id | Tier | Lines |
|---|---|---|
| `Akagane_Legendary_Wildfire` | Story, once | **Akagane:** "Wildfire. Every man of the vanguard, burning at once."<br>**Akagane:** "Their arrows won't reach you through it. Nothing will." |
| `Akagane_Legendary_Again` | Repeatable | **Akagane:** "All of it, Guardsman. Take all of it." |

### `Duo`

His Duos (Thermal Shock with Mizore, Plasma Arc with Hibiki) are two-soul conversations, written in
[Souls.md](Souls.md#duo-conversations).

### Boss reactions

`BossAhead` plays on a soul pickup during a boss's stage; `BossFelled` on the boon right after the boss falls.

| id | Event | Tier | Requires | Lines |
|---|---|---|---|---|
| `Akagane_Ahead_Benibana` | `BossAhead` | Story, once | `nextBoss = Benibana` | **Akagane:** "The dancer's at the end of this road. Her smoke put my whole vanguard on its knees. Don't breathe it." |
| `Akagane_Ahead_BenibanaBell` | `BossAhead` | Story, once | `nextBoss = Benibana`, `seen Benibana_Intro_Bell` | **Akagane:** "She showed you which bell is hers? ...Then I'll stop cursing her. Mostly." |
| `Akagane_Ahead_Utsusemi` | `BossAhead` | Story, once | `nextBoss = Utsusemi` | **Akagane:** "My men took that monk. In borrowed bodies, on his orders, but my men. Give the old man an end." |
| `Akagane_Ahead_Shirogane` | `BossAhead` | Story, once | `nextBoss = Shirogane` | **Akagane:** "Silver's up there. She beat me every time. Don't you dare lose to her for the wrong reasons." |
| `Akagane_Ahead_Kagetsune` | `BossAhead` | Story, once | `nextBoss = Kagetsune` | **Akagane:** "I knelt to him for twenty years. Make him kneel once." |
| `Akagane_Ahead_Any` | `BossAhead` | Repeatable | none | **Akagane:** "Warlord ahead. Burn hot." |
| `Akagane_Felled_Benibana` | `BossFelled` | Reactive, once | `felled = Benibana` | **Akagane:** "Down she goes. It was a good dance. I'll only say that once." |
| `Akagane_Felled_Utsusemi` | `BossFelled` | Reactive, once | `felled = Utsusemi` | **Akagane:** "Listen. The ticking's stopped. My men owed him that." |
| `Akagane_Felled_Shirogane` | `BossFelled` | Reactive, once | `felled = Shirogane` | **Akagane:** "She let you through. She never once let me." |
| `Akagane_Felled_Any` | `BossFelled` | Repeatable | none | **Akagane:** "One more down. Forward." |

### `HubReturn` (optional, on the hill)

| id | Tier | Requires | Lines |
|---|---|---|---|
| `Akagane_Hub_First` | Story, once | `lastRunElement = Fire` | **Akagane:** "So this is where you go. A hill and a flute. I always wondered." |
| `Akagane_Hub_Army` | Reactive | `lastRunElement = Fire`, `lastKiller = Army`, `runsSince Akagane_Hub_Army ≥ 4` | **Akagane:** "Up. My men are already marching." |
| `Akagane_Hub_Rise` | Repeatable | `lastRunElement = Fire` | **Akagane:** "On your feet, Guardsman." |

## Arc across runs

Contempt ("Hello, Guardsman"), then the old rivalry said out loud (copper and silver), then his men, then the
doors: the moment he admits he made Oni's mistake in miniature, and can't forgive either of them for it. Then he
starts helping on purpose (the Bellringers), and after a first clear he promises a salute. His anger never goes out;
it changes target, from Oni to Kagetsune to the bells.

## Freed (the ending)

His bell hangs at the front of Kagetsune's right shoulder, the war bell the Bellringers answer. When the Emperor
falls and the bells go silent one by one, his is among the last: it stops, and every army bell after it stops at
once. If `Akagane_Meet_Salute` has played, he rises in teal (the copper and ember burn out of him), turns to the
field, and says:

| Moment | Line |
|---|---|
| His bell stops | `Akagane_Freed`: **Akagane:** "Vanguard. Stand down. ...All of you. Go home." |
| Turning to Oni | `Akagane_Salute`: (no words; he salutes, once) |

Without the chain complete he rises silently among the thousands.

## Keep consistent

- He is angry, never cruel. He doesn't mock Oni's guilt; he shares it.
- He calls Oni **"Guardsman"**. Never "little demon" (Kagetsune's alone) or "boy" (Utsusemi's).
- His men are always "my men", never "the army" or "samurai" when he speaks of them.
- Fire orange and copper red; no teal until he's freed.
