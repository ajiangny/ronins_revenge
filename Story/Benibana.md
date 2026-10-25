# Benibana, the Crimson Smoke

[Back to the story](README.md)

| | |
|---|---|
| Role | Stage 1 boss. Phase 2: **The Crimson Unsealed**. |
| Was | The imperial court's kenbu sword-dancer, and its secret assassin. |
| Wants | To silence the bells of her people. |
| Fights Oni because | Kagetsune promised to free one of them for every time she kills him. He never has. |
| Signature | Kiseru smoke, a rosary chain, a crimson iaido. |

Her story is Oni's in a mirror: another of the Emperor's tools who led her own people to his altar.

## Look

Long vivid red hair, a black and crimson kimono, silver cross-shaped ornaments and chains, tattoos, red gloves,
and an ornate katana. Phase 2 (Crimson Unsealed): hair loose, tattoos glowing, chains and charms lifted, the blade
drawn. Full art direction: [CHARACTER_ART_GUIDE.md](../CHARACTER_ART_GUIDE.md#benibana--crimson-swordswoman).

## History

- **The dancer.** Named for the safflower that gives the crimson of her stage paint, she danced kenbu, the sword
  dance, before the imperial court. In secret she was also the court's assassin. A dancer's balance and an iaido
  draw too fast to follow made her the quietest killer in the capital.
- **The faith.** She was a hidden Christian at a time when that meant death. Her silver crosses and her rosary are
  that faith, worn openly now that she has nothing left to hide.
- **The bargain.** Kagetsune's inquisitors found her small congregation and took them. The Emperor offered her a
  way to save them: dance at the coronation, trailing her kiseru's drugged smoke through the hall so no one could
  resist what came next, and her people would *never die*.
- **The betrayal.** She danced. Her congregation was in the hall too, and the coffer took them with everyone else.
  He kept his word to the letter, since nothing sealed in the armor ever dies. Then he sealed her last.
- **Now.** Each of her people rings one of Kagetsune's bells. He has promised to silence one for every time she
  kills Oni. She has killed him many times. Not one bell has stopped.

## Her fight, read as story

| Move | What it means |
|---|---|
| Kiseru Smoke Rings | The smoke she danced through the hall. Her sin. |
| Rosary Lash / Rosary Bloom | Her people, swung like a dancer's ribbon. |
| Crimson Iaido | The blade: the one thing she never sold. Only a parry answers it, because only someone who knows that draw can turn it. |
| Phase break | She breathes the smoke back in, and in the silence after it she cuts the ceremonial robes off with her own blade. She stops dancing for an emperor. |
| Smoke veil (phase 2) | She fights from inside her own smoke, no longer hiding it. |

## Relationships

| With | |
|---|---|
| [Oni](Oni.md) | Envy and contempt at first: he got out and she didn't, and he is proof the bargain could have been refused. Later, the only person who understands her. |
| [Kagetsune](Kagetsune.md) | The man who kept his promise to the letter. Her hatred for him is colder than anything she says to Oni. |
| [Shirogane](Shirogane.md) | The one who refused. Benibana resents her for it, and admires her. |

## Voice

Izumi (ElevenLabs), read sultry and amused in phase 1, furious and raw in phase 2. Mocking, theatrical; she speaks
like someone used to an audience. Her mid-fight barks are unchanged: "Breathe it in...", "Kneel!",
"Crimson... Iaido!", "Bloom, my rosary!", "What?! My own blade...".

## Lines

**In game** (`Benibana.Line`, `Tools/generate-voice.mjs`):

| Line | Text |
|---|---|
| Intro | "We both led our flocks to his altar, Oni... and only you walked away. The heavens have forsaken you. Let me paint your final moments in crimson." |
| Phase | "Every step I ever danced was his... This one is mine." |
| Defeat | "Crimson... suits you... Find my bell, Oni... silence it for them..." |

**For the dialogue system** (`BossIntro`):

| Priority | Requires | Text |
|---|---|---|
| Story, once | first meeting | (the in-game intro) |
| Reactive | `killedOniLastRun = Benibana` | "Back so soon? He promised me a bell for that one. I'm still listening." |
| Story, once | deaths to Benibana ≥ 3 | "Three times I've killed you. Three bells he swore to silence. Not one has stopped ringing." |
| Story, once | seen the previous line, wins vs. Benibana ≥ 2 | (`Benibana_Intro_Bell`) "The third bell from the left on his belt. The cracked one. That one is mine." |
| Reactive | holds a Fire boon | "Fire? You'd burn my smoke? Cute." |
| Repeatable | none | "Dance with me, demon." |

**`KilledOni`**

| Priority | Requires | Text |
|---|---|---|
| Story, once | first time | "Listen... Did a bell stop? ...No. Of course not." |
| Repeatable | none | "Curtain." |

## Arc across runs

Mockery, then envy, then the night she realises no bell has ever gone quiet, then telling Oni which bell is hers
(`Benibana_Intro_Bell`). Once that line has played and he has beaten her again, her soul counts as freed for the
ending, and in Kagetsune's phase 2 her smoke turns on the Emperor.

## Keep consistent

- She is a dancer and an assassin. **No courtesan, pleasure-quarter or sexualised background**, in lines or art.
- She helped doom the court knowingly, to save her people. She doesn't deny it, and the story doesn't excuse it.
- The smoke is her sin, the rosary her people, the blade herself. Keep those meanings in new moves and lines.
- She tears her own robes in the phase break. Nobody else does it for her.
