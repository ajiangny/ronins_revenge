# Oni, the ronin

[Back to the story](README.md)

| | |
|---|---|
| Role | The player. The Emperor's former guard, now his hunter. |
| Wants | To free the souls he led up the mountain, and to end Kagetsune. |
| Fears | That he was always the Emperor's demon, and nothing else. |
| Signature | A katana, teal spirit flames, two spirit wisps, and Shirogane's bone flute. |

## Look

A lone chibi ronin with a katana, wreathed in teal spirit flame, with spirit wisps bobbing beside him. His flames
take the colour of the souls lending him power (fire, frost, storm). On the title screen he sits on his row
playing the flute. In the story, his teal is the soul-fire of Kagetsune's armor: he burns with what he tore
himself out of.

**The portrait.** His painted art (`Assets/Art/Portraits/Oni_Full.png`, `Oni_Closeup.png`) restyles the user's own
concept painting (`Story/Art/Oni_Original.png`) to match the boss paintings: a lean ronin with black swept-back hair
and a long braid, a dark robe with gold trim open on the red cloud tattoo, a red oni-faced armoured left arm and
shoulder, an oni mask at his belt, dark red plated leg armour with jade cloud guards, and his long katana. Two
changes from the concept: his spirit flames are the game's teal soul-fire (not green), and the robe sits more closed,
so the bare chest stays plain and unposed. Lit at night, by the moon and his own fire. The close-up stands at the right
of every talk scene, and is framed beside his words on the hill.

## History

- Born in a mountain village below the Emperor's shrine. Rose through the Emperor's guard beside Shirogane until
  the two of them were his finest blades. Kagetsune named him **Oni**, his demon at the gate. He wore the name with
  pride.
- On the Night of a Thousand Bells he escorted the court, the army and his own village up to the "coronation." He
  led them into the hall himself.
- When the coffin opened, Shirogane cut the tether pulling his soul into the armor. She was sealed in his place.
- A wandering monk found him half-dead on the Pilgrim's Pass and sheltered him through the first night. The monk
  was taken for it (see [Utsusemi](Utsusemi.md)).
- Since then he dies and wakes on the hill where he and Shirogane used to sit, playing her flute. Each return takes
  a little more of his memory: by the time he reaches Shirogane he has forgotten their oath to kill the Emperor
  together.

## Relationships

| With | How he sees them |
|---|---|
| [Kagetsune](Kagetsune.md) | His lord, his betrayer, the man who made him a demon. Every death is a debt. |
| [Shirogane](Shirogane.md) | His sworn partner; the soul that saved his. He remembers her less each run, and that frightens him more than she does. |
| [Benibana](Benibana.md) | At first, the dancer whose smoke helped doom the court. Later, the person most like him. |
| [Utsusemi](Utsusemi.md) | The monk who saved him, worn as a shell. He doesn't recognise him until the gears slip. |

## Voice

He speaks rarely and briefly, the way Zagreus does at his quietest. A low, tired voice, never a quip. Most of his
lines happen on the hill, after a death. In fights he is silent apart from grunts.

**In game:** a voice designed for him in ElevenLabs (`ONI` in `Tools/generate-voice.mjs`): a low, quiet, tired
baritone in his late twenties, slightly hoarse, slow and sparing, weary and haunted, never theatrical. Every written
line of his is voiced, including Second Dawn's "...Not yet." (`Oni_NotYet`). He speaks through his own audio
source, so the bosses' voice filters never touch him.

## Lines

In game through the reactive dialogue (`Dialogue.cs`), voiced and subtitled:

**`HubReturn`** (waking on the hill)

| Priority | Requires | Text |
|---|---|---|
| Story, once | first death | "...The hill. Her flute. Why am I here?" |
| Story, once | first death to Benibana | "That smoke. I've breathed it before." |
| Story, once | first death to Utsusemi | "That hat... I've drunk tea under that hat." |
| Reactive | `killedOniLastRun = Kagetsune` | "He's still ringing." |
| Story, once | seen `Shirogane_Intro_Oath` | "Together. We were meant to do it together." |
| Repeatable | none | (no words; he only plays the flute) |

**Answering the bosses** (talk scenes, once each): his replies to each boss's first intro, phase break and defeat
are in the bosses' files, under "Oni's answers".

**First soul of each element:** his old `FirstElement` lines now open each soul's first meeting
(`Akagane_Meet_Angry`, `Mizore_Meet_Steady`, `Hibiki_Meet_Out`, in the souls' files).

| Element | Text |
|---|---|
| Fire | "Warm... Someone in there is still angry." |
| Frost | "Cold hands. Steady ones." |
| Storm | "It wants out as badly as I do." |

## Arc across runs

Confused, then driven, then guilty as he remembers who led the court up the mountain, then resolved once Shirogane
gives him back the oath. At the true ending he plays the flute at sunrise and is allowed to rest.

## Keep consistent

- He is a ronin, not a hero. He did a terrible thing without knowing it, and the story never lets him off that.
- His name is **Oni**, given by Kagetsune. No other name for him.
- He doesn't joke. Brevity is his voice.
- Teal is his base colour; elemental colours are borrowed souls.
