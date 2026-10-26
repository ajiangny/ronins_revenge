# Kagetsune, the Thousand-Soul Throne

[Back to the story](README.md)

| | |
|---|---|
| Role | The Emperor. Stage 4 final boss (`Assets/Kagetsune.cs`). Phase 2: **The Unchained Wraith**. |
| Was | A mortal emperor, terrified of dying. |
| Is | Colossal and deathless, inside an armor of a thousand sealed souls. |
| Wants | To stay. Forever, and on his throne. |
| Signature | Bells, the Soul Coffin hammer, teal soul-fire, oni-skull faces. |

His armor is a throne a thousand souls kneel to, which gives him his title. When they turn on him in his second
phase and the coffin's chains snap, he becomes the Unchained Wraith: less an emperor than a storm of the souls he
stole, still wearing his shape.

## Look (from the concept art)

- A colossal horned warlord in blackened scale armor edged in pierced bronze. Curved bronze horns rise from a
  closed, spiked helm, with red eyes burning behind the visor.
- Snarling oni-skull faces on his shoulders, belt and knees, cast to mock Oni after the betrayal.
- Tattered crimson and white robes, strung with paper ofuda talismans and red tasselled cords. Braided red cord
  at his waist.
- **Bronze bells** hang from his waist and robes: one for every sealed soul of rank. The imperial crest is on his
  tasset.
- **Teal soul-fire** pours out of his open chest and wreathes his whole body, thick with drifting skulls.
- **The Soul Coffin:** carried on his shoulder, the coffin-hammer: a great war hammer whose head is a sealed
  coffin-shaped reliquary, chained shut and bound in red cord and ofuda, with an oni face set in it. The shaft is
  wrapped in red cord and ends in a tasselled spike.
- **Phase 2, the Unchained Wraith:** the coffin's chains snap and hang loose, and the armor cracks open into a
  lattice of teal ghost-fire, with screaming faces in his chest. His horns burn, his eyes turn from red to soul
  teal (the souls are looking out now), and skull spirits swarm free all around him.

Paintings: `Assets/Art/Portraits/Kagetsune_Full.png`, `Kagetsune_Closeup.png` (a calm bust, hammer at rest) and
`Kagetsune_Phase_Full.png`, `Kagetsune_Phase_Closeup.png` (the Wraith; a gauntlet reaching out around a howling
skull flame).

## History

- **The emperor.** Kagetsune's line had ruled for centuries. He watched his father die slowly and swore it would
  not happen to him.
- **The armor.** He had it forged in secret, around the coffin. It feeds on souls sealed in the coffin, and every
  one rings a bell.
- **The Night of a Thousand Bells.** He called his court, his army and Oni's home village to a coronation at the
  mountain shrine, with Oni as their escort and Benibana dancing her smoke through the hall. Then he opened the
  coffin and drank them all. The last toll stopped the night.
- **Since.** He sends the souls back out each night in borrowed bodies to hunt the one that got away. Every one
  he loses is pulled back into the coffin and forged again, and so is he: the first time Oni kills him, he is
  rebuilt from the souls he still holds.

## His fight

**Stage:** "The Mountain Shrine", the stair up to the hall where the Night of a Thousand Bells was held. Its rounds
bring every kind of soldier and his **Bellringers**, shrine guards whose tolls rouse the army to fire twice as fast;
cutting one down silences its bell. **Arena:** no moon. The coronation courtyard under a starless black sky, lit only
by teal soul-fire braziers and his own chest.

Two bars of 170. He barely moves: a few heavy steps that shake the ground. Each move is slow and telegraphed.

**Phase 1, the Thousand-Soul Throne.**

| Move | What happens | What it means |
|---|---|---|
| Coffer Slam | The hammer marks Oni's lane over three tolls, then a shockwave runs down it. The first cover in its way takes the blow and breaks; skulls leap from the crack and dive at him. | The night the coffin opened. |
| Toll of Fealty | Three tolls (five in phase 2), each loosing a fan of soul-skulls, each fan's gaps where the last one's skulls were. | The souls answering him. |
| Ofuda Decree | Talisman strips drift down into two lanes (three in phase 2), always one of them Oni's. Inside a sealed lane he cannot parry. A slam or a toll follows at once. | Imperial law. |
| Thousand-Soul Wail | A beam of soul-fire from his open chest sweeps the field (there and back in phase 2), burning cover. | His power, which is their pain. |
| The Last Toll (parry-only) | A lock-on closes on Oni and a great bronze bell drops on him, steered onto his lane. Parried, it rings against the Emperor's armor and staggers him. | His bell turned on him. |

**Phase break.** His bells clamour faster and faster and the coffin's chains snap one by one; the souls tear loose
in a burst of skulls and his eyes turn teal.

**Phase 2, the Unchained Wraith.** Faster, longer combos, a second slam lane, plus:

| Move | What happens |
|---|---|
| Wraith Swarm | Waves of skull spirits wheel out of his armor and dive after Oni. Parried, a skull flies home at him as a freed soul. |
| Broken Chains | The snapped chains lash from his shoulders, sweeping stretches of the ground. |
| Soul Coffin (parry-only) | He hurls the coffin-hammer down on its chain. Parried, it bursts against him: a stagger, and he stands open to double damage. |
| Night of a Thousand Bells (every sixth move) | The field goes dark and soul-fire falls down every lane but one in five waves, the safe lane stepping aside each time. Then he kneels, spent and open. |
| **Summoned souls** | He drags a boss's soul out of the coffin, a teal ghost on a chain from the hammer, to use its own move on Oni: Benibana's smoke rings and Crimson Iaido, Utsusemi's hour marks and cleave, Shirogane's blade rain. Their parry-only blows fly back at him, not at them. Shirogane, bound to his will, can only warn Oni before her blades fall. |

**Defeat.** The coffin bursts. The bells fall silent one by one, each toll lower and quieter than the last, a soul
rising free with each, and the armor sinks hollow and fades. Then the stopped hour ticks on and dawn breaks for the
first time in the game: Oni walks out into it and plays the flute, and Shirogane's wisp settles beside him.

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | His demon at the gate, the one soul that got away. He speaks to Oni as a master to a dog. |
| [Benibana](Benibana.md) | A tool that kept its bargain. He kept his, to the letter. |
| [Utsusemi](Utsusemi.md) | A clock he winds. |
| [Shirogane](Shirogane.md) | The one bell that never rang. It is the only thing that unsettles him. |

## Voice

His designed ElevenLabs voice: an extremely deep, slow, resonant bass, as if from inside an iron helm, with a choir of
the souls inside him layered underneath (`GameManager.TuneVoice`'s wide, slow chorus and a hall's echo). He never hurries and never shouts until phase 2,
when the chorus starts speaking over him.

## Lines

**(in game)** lines match `Kagetsune.Line()` and `Tools/generate-voice.mjs` word for word.

| Line | Text |
|---|---|
| Intro **(in game)** | "Every bell upon me is a soul that knelt, Oni. One still hangs silent... yours." |
| Phase **(in game)** | "Kneel! KNEEL! You are MINE, every one of you..." |
| Defeat **(in game)** | "The bells... why do they not ring...?" |
| Stagger **(in game)** | "You dare ring MY bell?" |
| Hurt **(in game)** | "Insolence." / "A scratch, little demon." / "Hm." |
| KilledOni **(in game)** | "Back to your hill, little demon. I will be here. I am always here." (also the end screen's text) |
| Callouts **(in game)** | Coffer Slam "Bow." · Toll "Answer me." · Ofuda "By my decree." · Wail "Hear them, little demon." · Last Toll "Hear the last toll." · Swarm "Go. Fetch him." · Chains "Chains do not hold me." · Soul Coffin "Into the coffin!" · Thousand Bells "A thousand bells, Oni! Ring!" |
| Summons **(in game)** | "Dance for me, Benibana." · "Strike the hour, clock." · "Rise, Shirogane. Kneel, and strike." |
| The souls answer **(in game)**, in their own voices | Benibana: "Forgive me, Oni... he has my bell." · Utsusemi: "The hour... strikes... Run... boy." · Shirogane: "Move, Oni. I cannot hold my blades." |

Planned for the reactive dialogue system:

**`BossIntro` variants**

| Priority | Requires | Text |
|---|---|---|
| Story, once | first meeting | (the intro above) |
| Story, once | wins vs. Kagetsune ≥ 1 | "You broke me once. I have a thousand more of me." |
| Story, once | Shirogane's oath line seen | "She never rang. Do you know how that grates, little demon?" |
| Repeatable | none | "Kneel." |

**`KilledOni`**

| Priority | Requires | Text |
|---|---|---|
| Story, once | first time | "Back to your hill, little demon. I will be here. I am always here." |
| Reactive | Oni holds a Duo boon | "Two stolen souls, holding hands. How touching. They'll ring again." |
| Repeatable | none | "Ring." |

## Keep consistent

- No moon motifs: bells, souls, seals and horns are his.
- He never admits fear, except in his defeat line.
- He is the only character who calls Oni "little demon".
- Teal fire is the souls, not his own power. When souls leave him, the fire goes with them.
