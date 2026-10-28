# Poguru, the Lost Dragon

[Back to the story](README.md)

| | |
|---|---|
| Role | Oni's guide and comic relief. Never fights. The game's tutorial. |
| Was | A sliver of soul-fire that came away with Oni when he tore himself out of the armor. |
| Is | A baby dragon of teal soul-fire who lives in Oni's left spirit wisp. |
| Wants | To be a fierce dragon. Failing that, to help. |
| Fears | The damp temple, and Kagetsune's bells. |
| Signature | Huge goggly eyes, his tongue always out, and a scrap of red cape. |

## Look

A chubby baby dragon about the size of Oni's head, drawn in the game's teal soul-fire.

- **Head and body:** a round head and belly with huge goggly eyes (gold rims, dark pupils, a white glint). He has a
  wide, happy open mouth, and his tongue is always hanging out. Two stubby horns, nub ears, and tiny wings with a
  soft pink membrane.
- **Below the belly:** he melts into a spirit-flame wisp, and a ghostly tail tapers away and curls like smoke.
- **Cape and glow:** a tiny red cape, the scrap of the festival dragon, is knotted at his neck with a gold cord. Pale
  flame licks off his head and tail.
- **Mood:** friendly and silly, never menacing. Moonlit like everything else in the game, and lit by his own glow.

The user's reference images inspired him: the teal baby dragon (the main one), a flame wisp, a ghost wisp and a
caped spirit. They live outside the repo and must never be committed.

- **Pixel art** (`Assets/Art/Sprites/Characters/Poguru/Poguru_<Clip>_<i>.png`, 32×32, PixelLab): the clips are Idle,
  Talk, Pop (swelling out of the wisp), Bonk (headbutting an arrow), Annoyed, Point, Gasp and Cheer.
- **Paintings** (`Assets/Art/Portraits/Poguru_Closeup.png`, `Poguru_Full.png`, 1024×1536 transparent): the moonlit
  painted style of the other close-ups. He stands on the left of a talk scene, or on the right when he points at the
  ability icons.

## History

- When Oni tore himself out of the armor on the Night of a Thousand Bells, a sliver of its soul-fire came with him
  and became his two spirit wisps. One of them had been listening.
- Poguru woke in the left wisp. He shaped himself after the fiercest thing he half-remembered: the paper dragon
  from the lantern festival in Oni's village. He kept a scrap of its red cloth as a cape and doesn't know why it
  smells like lanterns.
- Each time Oni dies he forgets a little, so Poguru "remembers for him". Mostly he remembers the wrong things, like
  whether Oni liked plums. (He did.)
- He has no part in the betrayal and no bell. When Shirogane is near, the wisps go warm. In the true ending the
  *other* wisp is hers; Poguru is delighted to have a neighbour.

## Relationships

| With | How he sees them |
|---|---|
| [Oni](Oni.md) | His person. Oni tolerates him, answers in two words, and secretly needs him. |
| The souls ([Souls](Souls.md)) | He half-recognises them: "it looks like someone familiar..." |
| [Shirogane](Shirogane.md) | The wisps go warm near her. He doesn't know why, and Oni won't say. |
| [Kagetsune](Kagetsune.md) | He hides in the wisp. He isn't proud of it. |

## Voice

Silly, excitable, kind and short. He shouts in capitals when he panics and puts himself down for a laugh. He's the
comic contrast to Oni, who never jokes; Oni answers him with "..." and a few words.

**In game:** a voice designed for him in ElevenLabs (`POGURU` in `Tools/generate-voice.mjs`): a tiny cartoon
baby-dragon spirit, young, boyish, high and bright but warm (not shrill), fast and breathless, giggly, with comedic
gasps and whines. He speaks through his own source (`GameManager.poguruVoice`, a light chorus). Oni's replies inside
Poguru's conversations use the `ONI` voice.

## Lines

The ids are the voice file names: line *n* > 0 of a conversation is `<Id>_<n+1>`. P = Poguru.

### Tutorial (`DialogueEvent.Tutorial`, played by id)

| Id | Lines |
|---|---|
| `Poguru_Tut_Wake` | P: "ONI! ONI! THE ARMY IS COMING!" · Oni: "...Who are you." · P: "Poguru! The Lost Dragon! I live in your wisp. Rent-free!" · P: "No time! Do you remember how to fight?" |
| `Poguru_Tut_Again` | P: "Oni! Army! Again! Want the lesson again? I LOVE the lesson." |
| `Poguru_Tut_Yay` | P: "Yay! Stay close to me. I mean, I'll stay close to you!" |
| `Poguru_Tut_Skip` | P: "Okay okay okay! I'll be in your wisp. Cheering. Quietly. Mostly." |
| `Poguru_Tut_Basics` | P: "Bad guys up there, you down here. They march down, you stop them!" · P: "This is SLASH. Press SPACE and a cut flies up. Whoosh!" · P: "Your Spirit SHURIKEN! Press W. Big and sparkly, but it needs a little nap after." · P: "And PARRY! Press SHIFT just before an arrow bonks you, and it goes right back at them!" · P: "Beat the whole army. I'll float nearby and bonk arrows. ...Try not to make me bonk too many." |
| `Poguru_Block_1` | P: "Bonk! Got it!" |
| `Poguru_Block_3` | P: "Ow. Okay. Still fine!" |
| `Poguru_Block_6` | P: "Oni, you can ALSO move, you know!" |
| `Poguru_Block_10` | P: "My HEAD is not a SHIELD!" |
| `Poguru_Block_Break` | P: "That's it! Dragon break! You're on your own for five seconds!" |
| `Poguru_Block_Back` | P: "...Fine. FINE. I'm back. Hmph." |
| `Poguru_Tut_LookOut` | P: "LOOK OUT!!!" |
| `Poguru_Tut_Hearts` | P: "Waaah! It went right through me! Are you okay?!" · Oni: "...Fine." · P: "Those hearts in the corner are your lives. Lose them all and it's back to the temple. It's damp there." · P: "Good news! I've been saving a heart soul. For emergencies. This is an emergency!" · P: "Hearts fall out of broken cover sometimes. Once in a blue moon a soldier drops one too!" |
| `Poguru_Tut_Collect` | P: "Go get it! It's on your row!" |
| `Poguru_Tut_Collected` | P: "Yummy! I mean... healthy!" |
| `Poguru_Tut_Reward` | P: "Ooooo! Pretty cool soul!" · P: "Wait... it looks like someone familiar..." |
| `Poguru_Tut_Boons` | P: "Souls lend you their power. That's a boon!" · P: "Pick ONE. Up and down to look, ENTER to take it." · P: "They change your slash, shuriken or parry. Or pick the shiniest. That's what I'd do." |
| `Poguru_Tut_Paths` | P: "Paths! Each leads to your next fight. What's floating in it is your prize for winning." · P: "Walk under one and press E. After a few rounds, one of them leads to the boss!" |
| `Poguru_Tut_Done` | P: "Looks like you DO remember how to fight!" · P: "Let's hurry and free everyone!!" · Oni: "...Back in the wisp." · P: "Going! Shutting up! ...Mostly!" |

### Barks (subtitles; he pops out of the wisp, then back in)

| Event | Priority | Requires | Id | Text |
|---|---|---|---|---|
| PoguruRound (20%) | Repeatable | | `Poguru_Round_Go` | "Go go go!" |
| | Repeatable | | `Poguru_Round_Snacks` | "More of them! Do they ever bring snacks?" |
| | Repeatable | | `Poguru_Round_Wave` | "Hi, army! He's going to beat you now! ...Sorry!" |
| | Repeatable | | `Poguru_Round_Breathe` | "Deep breaths! Like me! Hff hff hff." |
| | Repeatable | | `Poguru_Round_Count` | "I counted them. There's... a lot." |
| PoguruSoul (first boon of a run) | Repeatable | | `Poguru_Soul_Shiny` | "Shiny! Can I hold it? No? Okay." |
| | Repeatable | | `Poguru_Soul_Thanks` | "A friend from the coffin! Say thank you, Oni!" |
| | Repeatable | | `Poguru_Soul_Glow` | "You're glowing! I'm glowing too. Solidarity." |
| PoguruBoss (after a boss falls, not Kagetsune) | Story | first win vs Benibana | `Poguru_Boss_Benibana` | "Smoke lady's gone! I kept one petal." |
| | Story | first win vs Utsusemi | `Poguru_Boss_Utsusemi` | "The ticking stopped! Is it lunchtime?" |
| | Story | first win vs Shirogane | `Poguru_Boss_Shirogane` | "She looked so sad... Wisps can't cry. I checked." |
| | Repeatable | | `Poguru_Boss_Won` | "WE WON! I mean, you won. I helped. Emotionally." |
| | Repeatable | | `Poguru_Boss_Wiggle` | "Victory wiggle! ...No? Okay." |
| PoguruWake (climbing out of the pool) | Story | a death | `Poguru_Wake_First` | "Back in the damp temple. ...But I like that you're here." |
| | Reactive, cooldown 3 | killed by the army | `Poguru_Wake_Army` | "Those soldiers were VERY pointy today." |
| | Reactive, cooldown 3 | killed by Benibana | `Poguru_Wake_Benibana` | "Her smoke made me sneeze." |
| | Reactive, cooldown 3 | killed by Utsusemi | `Poguru_Wake_Utsusemi` | "Tick, tock, ow." |
| | Reactive, cooldown 3 | killed by Shirogane | `Poguru_Wake_Shirogane` | "She hits SO hard." |
| | Reactive, cooldown 3 | killed by Kagetsune | `Poguru_Wake_Kagetsune` | "I hid in your wisp the whole time. I'm not proud." |
| | Repeatable | | `Poguru_Wake_Up` | "Up you get! The pool's warm. Ish." |
| | Repeatable | | `Poguru_Wake_Hug` | "Wisp hug! Very warm. Very small." |
| | Repeatable | | `Poguru_Wake_Count` | "That's {deaths}! I'm keeping count." (subtitle only: the number changes) |

### Temple talk (`PoguruTalk`, at his nest)

| Priority | Requires | Id | Lines |
|---|---|---|---|
| Story | | `Poguru_Talk_Who` | Oni: "Where did you come from." · P: "Your wisp! The left one. It's cosy. I think I was always there, I just woke up late." · P: "I'm a dragon! Probably. A small one. Lost. That's my title!" |
| Story | heard Who, a run since | `Poguru_Talk_Cape` | Oni: "That red cloth." · P: "My cape! It smells like festival lanterns. Do you know why?" · Oni: "...No." |
| Story | 3 runs | `Poguru_Talk_Remember` | P: "Every time you wake up, you forget a little. So I remember for you!" · P: "Mostly the wrong things. Did you used to like plums?" · Oni: "...I did." |
| Story | met Shirogane | `Poguru_Talk_Silver` | P: "When the silver lady's near, your wisps go all warm. Is she a friend?" · Oni: "...She was." |
| Story | a dawn | `Poguru_Talk_Dawn` | P: "The sun! I'd never seen it! It's very bright. I hid. But I peeked!" |
| Repeatable | | `Poguru_Talk_Nap` | P: "I was napping. In your wisp. Which is in you. So technically you were napping too." |
| Repeatable | | `Poguru_Talk_Rawr` | P: "Rawr. That's my fierce face. Was it fierce? Don't answer." |
| Repeatable | | `Poguru_Talk_GhostFire` | P: "I tried eating a ghost-fire. It tasted like Tuesday." |
| Repeatable | | `Poguru_Talk_Flute` | P: "Play the flute later? The wisps dance when you do." · Oni: "...Maybe." |

### Tips page (at his nest; not voiced)

| Topic | Text |
|---|---|
| SLASH (SPACE) | "A cut flies up the field. Cover stops it, so step out first!" |
| SHURIKEN (W) | "Big and sparkly, slow to come back. Save it for crowds." |
| PARRY (SHIFT) | "Just before an arrow lands. A good parry is ready again straight away! Crimson blows can't be parried. Dodge those!" |
| SPEARS | "The glowing red ones go right through cover. Move!" |
| HEARTS | "From broken cover, and rarely a soldier. At full lives they wait on your row." |
| THE ARMY | "If it reaches your row, it's over. Thin out the bottom rank!" |
| BOONS | "One per soul. Hold TAB in a fight to see what your icons do." |
| PATHS | "Walk under, press E. The boss's path opens after round three." |
| SPIRIT NOTES | "They stay even when you fall. Spend them at the flute altar." |

## Keep consistent

- He never fights and never takes damage. In the tutorial he only bonks arrows aside, and the spear passes through him.
- He lives in Oni's **left** wisp (wisp 0). When he's out, that wisp is hidden.
- Teal is soul-fire: his body is teal, and only his cape is red.
- Short lines. One joke at most per line. He is kind even when he's annoyed.
