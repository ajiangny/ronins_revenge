# Utsusemi, the Hollow Pilgrim

[Back to the story](README.md)

| | |
|---|---|
| Role | Stage 2 boss. Phase 2: **The Unwound Hour** (its art: Fractured Overdrive). |
| Was | A wandering monk who sheltered Oni after the betrayal. |
| Is | A karakuri: the monk's soul wound into clockwork, wearing his hat and shroud. |
| Wants | Nothing of its own. Underneath, the monk wants to be wound down. |
| Signature | Ticking steps, the hour, its hat, a red slit-eye and a lantern-red heart. |

*Utsusemi* is the empty shell a cicada leaves behind: a body walking around with nothing inside.

## Look

A tall, slender, black articulated automaton under an enormous circular pilgrim's hat, in blue and ivory robes with
gold veining and a lace shroud, with hanging gold ornaments, a vertical orange slit-eye and an orange chest core. It
carries a cane sword. Phase 2: the hat breaks into a halo, the chest opens on a blazing core, and the blade glows
incandescent. Full art direction: [CHARACTER_ART_GUIDE.md](../CHARACTER_ART_GUIDE.md#utsusemi--ceremonial-automaton).

## History

- **The monk.** He walked the pilgrim roads for forty years with the same hat and the same kettle. On the first
  night after the betrayal he found Oni half-dead on the Pilgrim's Pass, carried him to his shelter and made him
  tea.
- **Taken.** Kagetsune's soldiers found the monk for harbouring the Emperor's demon. Kagetsune had him hollowed and
  his soul wound into a clockwork body, then set it to walk every road the monk ever walked, looking for the boy he
  saved.
- **The clock.** It also winds the clock that holds the stopped hour in place. That is why its whole fight keeps
  time, and why it counts.

## Its fight, read as story

| Move | What it means |
|---|---|
| Razing the cover | It walked every road; it knows every place Oni could hide. |
| Kasa Wheel | The monk's own hat, thrown at the boy who once slept under it. Once it breaks into a halo, its pieces fly apart and come at him from three sides: even the shelter he remembers is broken. |
| Lace Lattice | His shroud, unravelling. |
| Hour Strikes | "Remember the hours": it counts the hours of the stopped night, one by one, then hunts him down the hours again with its blade. |
| Plumb Lines | The weights of the clock it winds. |
| Phase break | The mainspring slips: it overloads, its lasers firing wild as it cracks open from the heart, the ticks race, and **one of Kagetsune's bells** tolls to wind it back up. It bursts open rather than stop. |
| Heart Lantern / Midnight | Its core: the monk's soul, burning as fuel. |

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | A target. Underneath, the boy he saved. The monk breaks through only as it dies. |
| [Kagetsune](Kagetsune.md) | Its winder. His bell answers its phase break. |
| [Benibana](Benibana.md) | The monk once refused her confession. The machine doesn't remember why. |

## Voice

Its designed ElevenLabs voice: deep, smooth and machine-calm, with dry menace, through Unity's chorus, short echo
and light distortion. Slow and deliberate, with ellipses as gear-stops. When the monk breaks through, the voice
softens and the menace drops out. Barks unchanged: "Kasa.", "Remember the hours... one... by one.", "Gears...
seized...".

## Lines

**In game** (`Utsusemi.Line`, `Tools/generate-voice.mjs`):

| Line | Text |
|---|---|
| Intro | "Tick... tock... The monk who wore this hat once gave you shelter. Now I walk every road he walked, looking for you. Yours... ends here." |
| Phase | "Mainspring... unwound. I will not... stop." |
| Defeat | "The bell... has... stopped... Run... boy..." |

**Reactive dialogue, in game** (`Dialogue.cs`, voiced in `Tools/generate-voice.mjs` except the death count, which stays a subtitle) (`BossIntro`):

| Priority | Requires | Text |
|---|---|---|
| Story, once | first meeting | (the in-game intro) |
| Reactive | deaths ≥ 10 | "Eleven deaths. Eleven roads walked again. I counted every one, Oni." (the count is filled in) |
| Story, once | wins vs. Utsusemi ≥ 2 | "Tick... tock... tea... There was tea, once. Why do I... remember tea?" |
| Story, once | seen the previous line | (`Utsusemi_Intro_WindDown`) "Boy. Wind me down. I cannot do it... from in here." |
| Repeatable | none | "Tick... tock." |

**`KilledOni`**

| Priority | Requires | Text |
|---|---|---|
| Story, once | first time | "One. I will remember the hour." |
| Repeatable | none | "Counted." |

## Arc across runs

Pure machine, then a machine that counts Oni's deaths aloud, then the monk surfacing through the gears (tea, the
boy), then asking to be wound down (`Utsusemi_Intro_WindDown`). Once that line has played and Oni beats it again,
the monk is freed for the ending. Until then, in Kagetsune's phase 2 he winds its soul up out of the coffin to strike
the hour on Oni: three hour marks and its cleave ("The hour... strikes... Run... boy."). The parried cleave strikes
the Emperor.

## Keep consistent

- The machine is "it"; the monk inside is "he". The voice changes when he surfaces.
- It never lies and never gloats. It counts.
- Bells belong to Kagetsune. The bell in its phase break and defeat is his, not a clock chime of its own.
