# Shirogane, the Forsaken Blade

[Back to the story](README.md)

| | |
|---|---|
| Role | Stage 3 boss. Phase 2: **Oath of the Spectral Wolf**. |
| Was | The Emperor's guard and Oni's sworn partner. White silver to his demon. |
| Is | A ghost bound to stop Oni, with her shrine's guardian wolf bound to her. |
| Wants | Oni to win, and to remember what they swore. |
| Signature | Twin katanas in blue spirit flame, a gliding stance, the spirit wolf. |

## Look

Ghostly white skin, flowing white hair, blue eyes, a blue-horned oni mask worn as a hair ornament (a gift from Oni;
see below), closed black, cobalt and white samurai armor, and a katana in each hand burning with blue spirit
flame. Phase 2: the spectral wolf behind her shoulder, her blades and hair blazing. Full art direction, including
the locked requirements: [CHARACTER_ART_GUIDE.md](../CHARACTER_ART_GUIDE.md#shirogane--stage-3-ghost-samurai).

## History

- **The two blades.** She and Oni rose through the Emperor's guard together and swore an oath to each other. The
  oni mask she wears in her hair was his, given when Kagetsune named him: she wore his name so he wouldn't carry it
  alone.
- **The oath.** Before the coronation she began to suspect the armor. They swore to kill the Emperor together if
  it was what she feared. They never got the chance to compare notes.
- **The cut.** When the coffer opened she saw the armor reaching for Oni and cut the tether dragging his soul in.
  Kagetsune's hammer came down on her. She went into the coffer, and her soul has never rung its bell: she would
  not kneel.
- **Now.** Kagetsune bound the guardian wolf of her family's shrine to her and set her at the top of the Frozen
  Shrine, the last place Oni must pass. She is forced to fight him. She wants him to win, but he has to be able
  to: his blade has to be the one she remembers.

## Her fight, read as story

| Move | What it means |
|---|---|
| Gliding, never blinking | She never hid from him and doesn't start now. |
| Every pattern leaves a gap | It is a test, not an execution. |
| Phase break: the wolf arrives | She calls on the oath. The wolf is her shrine's guardian, and it remembers too. |
| Eclipse Fang, then standing spent | She gives everything, then leaves herself open on purpose. |
| Defeat: the wolf dissolves under her hand | She lets go of the last thing binding her before she goes. |

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | Her partner. She remembers everything he has lost. Grief, patience, and steel. |
| [Kagetsune](Kagetsune.md) | The lord she refused to kneel to. Her bell is the one that never rang. |
| [Benibana](Benibana.md) | Pity, not judgement: she understands bargains made for others. |

## Voice

Lily (ElevenLabs): low, calm and velvety, mournful, through Unity's long echo and slow drift. Short sentences. She
never raises her voice except at Eclipse Fang. Barks unchanged: "Crescent Moon.", "Blades... fall.", "Too slow.",
"Scatter.", "Go.", "Sing for them.", "Together.", "Hunt.", "Eclipse... Fang.", "My stance...".

## Lines

**In game** (`Shirogane.Line`, `Tools/generate-voice.mjs`); unchanged, already true to the story:

| Line | Text |
|---|---|
| Intro | "I remember your blade. Do you remember mine?" |
| Phase | "You still fight as you did before... Then let us remind you what you've forgotten." |
| Defeat | "Even after all this time... you still wield the blade I remember." |
| Final words | "Go. Finish what we started." |

**For the dialogue system** (`BossIntro`):

| Priority | Requires | Text |
|---|---|---|
| Story, once | first meeting | (the in-game intro) |
| Story, once | seen `Benibana_Intro_Bell` | "She told you which bell is hers. Mine has never rung. I would not kneel." |
| Reactive | `killedOniLastRun = Shirogane` | "You hesitated. You never used to." |
| Story, once | wins vs. Shirogane ≥ 3 | (`Shirogane_Intro_Oath`) "We swore to end him together, before the coronation. I kept the oath. Now you keep it." |
| Repeatable | none | "Again, then." |

**`KilledOni`**

| Priority | Requires | Text |
|---|---|---|
| Story, once | first time | "Not yet, then. I'll wait." |
| Repeatable | none | "Rise." |

## Arc across runs

A test, then reminders, then the oath given back (`Shirogane_Intro_Oath`), which also unlocks Oni's own memory of
it. Once it has played and he beats her again, she lays down her blades for good, and in Kagetsune's phase 2 her
ghost swords fall on the Emperor. In the true ending she is the second wisp beside Oni at sunrise.

## Keep consistent

- She is never cruel to Oni and never sexualised. Armor stays closed; skin stays ghost-white.
- She is the only boss who never served Kagetsune willingly, not even once.
- The wolf is her shrine's guardian, not Kagetsune's creature. It obeys her, not him.
