# The Story of Ronin's Revenge

The story behind Shadow Blade: what happened, why everyone keeps coming back, how it ends, and how lines should
react as runs pile up, the way Hades' do. Each character has their own file. **Check a character's file before
writing a line, a scene or art for them**, and update it when something about them changes.

## Cast

| Who | Epithets (phase 1 / phase 2) | Role | File |
|---|---|---|---|
| **Oni** | the ronin | The Emperor's former guard, now his hunter. The player. | [Oni.md](Oni.md) |
| **Benibana** | The Crimson Smoke / The Crimson Unsealed | Court sword-dancer and assassin. Stage 1 boss. | [Benibana.md](Benibana.md) |
| **Utsusemi** | The Hollow Pilgrim / The Unwound Hour | A karakuri wearing a dead monk's hat. Stage 2 boss. | [Utsusemi.md](Utsusemi.md) |
| **Shirogane** | The Forsaken Blade / Oath of the Spectral Wolf | Oni's sworn partner, now a ghost. Stage 3 boss. | [Shirogane.md](Shirogane.md) |
| **Kuon** | The Shrine Guardian | Shirogane's spirit wolf, her family shrine's guardian. Fought beside her in her phase 2. | [Kuon.md](Kuon.md) |
| **Kagetsune** | The Thousand-Soul Throne / The Unchained Wraith | The Emperor. Planned Stage 4 final boss. | [Kagetsune.md](Kagetsune.md) |

## The betrayal

Emperor Kagetsune was mortal once, and afraid of it. His two finest blades were his guards, **Oni** and
**Shirogane**, sworn to him and to each other. "Oni" was the name the Emperor gave him: his demon at the gate.

In secret, Kagetsune had an armor forged that no smith should have made. It was black scale edged in bronze, hung
with bells, and built around a reliquary coffin he carried as the head of a great hammer. Every soul sealed in the
coffin would feed the armor and ring one of its bells. A man wearing enough of them could not die.

He called it a coronation. On the **Night of a Thousand Bells** he summoned his whole court, his army and the
village Oni was born in to the mountain shrine, and Oni escorted them there himself, proud to. Benibana danced for
the feast, her smoke curling through the hall until no one could lift a hand. Then the coffin opened.

The bells rang until there were too many to hear apart. Thousands of souls poured into the armor in a flood of
teal fire, and Kagetsune rose out of it colossal and deathless. With the last toll, the night stopped where it
stood. Dawn has not come since.

Shirogane saw the armor reach for Oni and cut the tether that was dragging his soul into it. Kagetsune's hammer
came down on her, and her soul went into the coffin with the rest. Oni's tore loose. To mock him, Kagetsune had the
new faces on his armor cast as snarling oni.

## Why everyone comes back

- **Oni** is free but has nowhere to go. His soul can't pass on while the armor holds everyone he led up the
  mountain, so every death pulls him back to the hill where he and Shirogane used to sit, playing the bone flute
  she gave him. The title screen is that hill, which makes the menu the game's hub (Hades' House). Each return
  wears a little more of his memory away.
- **The army and the bosses** are souls from the armor, sent out each night in borrowed bodies. When one falls it
  is drawn back into the coffin and forged again, so the army is always full and the bosses remember every fight.
  The yurei of the Frozen Shrine are the souls the armor hasn't bothered to give bodies to.
- **The endless night.** The last bell froze the hour, and Utsusemi winds the clock that holds it there. The moons
  over the boss fights (blood, steel, spirit) belong to the bosses, not to Kagetsune.
- **Boons.** When a soldier dies, its soul sometimes slips past the coffin's pull as a Fire, Frost or Storm soul.
  It lends Oni its power because he is the only one who might set it free. Every boon is a soul stolen back.
  Technique scrolls are pages of Oni's own sword arts, the parts of himself the dying took.
- **Teal fire.** The fire pouring from Kagetsune's chest is the same teal as Oni's spirit flames and wisps,
  because Oni tore himself out of it.

## Endings

Like Hades, the first win isn't the end. Kagetsune falls, but the coffin still holds enough souls to forge him
again, and Oni wakes on his hill. A clear frees a boss for good once their story lines have played (see each
character's file): Benibana's bell goes silent, the monk's clock stops, Shirogane lays down her blades. When all
three are free and Kagetsune falls again, he can't be rebuilt. The **true ending** is dawn: the stopped hour moves,
the sun comes up over the hill, Oni plays the flute one last time, and the second spirit wisp beside him is
Shirogane.

## Consistency rules

- The world is always night until the true ending. Nothing in the game shows a sunrise before it.
- Teal is soul-fire: Oni's flames, Kagetsune's chest, freed souls. Don't use teal for anything else story-wise.
- Bells mean sealed souls. A bell going silent means a soul set free.
- Every boss is a victim of Kagetsune as well as Oni's enemy. None of them serves him willingly any more.
- No sexualised backgrounds or framing for any character (see the art guide's Shirogane notes for the same rule
  in art).
- Lines marked **(in game)** in the character files must match `Line()` in the boss's script and
  `Tools/generate-voice.mjs` word for word.

---

# Dialogue design: reactive lines, the Hades way

## What Hades does

Hades has more than 21,000 voiced lines across about 30 characters, and players rarely hear the same conversation
twice in a row. The sources agree on the approach:

- **Write for situations, and let the game pick.** Greg Kasavin (creative director) described writing as many
  different contexts as possible in which a character could talk to you, then having the game choose the right one
  for the moment ([GeekDad, 2019](https://geekdad.com/2019/10/narrative-and-early-access-supergiants-greg-kasavin-discusses-hades-development/)).
- **The mapping is authored by hand.** Each line's link to the situation it answers was built manually, permutation
  by permutation ([Sean Z, interview with Kasavin](https://seanzwrites.com/posts/supergiant-games-greg-kasavin-interview/)).
- **Casting, recording and implementation are one pipeline**, covered in Kasavin and Darren Korb's GDC 2021 talk,
  [Breathing Life into Greek Myth: The Dialogue of Hades](https://www.youtube.com/watch?v=m5KJSAj4afg). Kasavin also
  pointed people to Chris Bratt's November 2020 video breakdown of the conversation system
  ([his post](https://x.com/kasavin/status/1329487797607043073)). The line count comes from Supergiant's own
  breakdown ([Nintendo Wire](https://nintendowire.com/news/2020/12/30/hades-has-over-300000-words-of-voiced-dialogue-heres-a-handy-breakdown-of-who-speaks-most-and-least/)).

How the shipped script data is organised (my summary of the game's Lua data files, not taken from the sources
above): every conversation is a record with **requirements** (lines that must have played, lines that must *not*
have played, run counts, recent kills or deaths, items held), a **priority** (story-critical conversations jump the
queue) and **play-once** flags. Short **repeatable** barks sit underneath, so a character always has something to
say. Whatever plays is recorded in the save, which is what lets a conversation chain over dozens of runs.

## Our version

**A line record.**

| Field | Meaning |
|---|---|
| `id` | Unique, e.g. `Benibana_Intro_Bell` |
| `speaker` | `Benibana`, `Utsusemi`, `Shirogane`, `Kagetsune`, `Oni` |
| `event` | Which moment it answers (below) |
| `text` | Subtitle text; the voice clip is `Audio/Voice/<id>.mp3` |
| `priority` | `Story` > `Reactive` > `Repeatable` |
| `requires` | Conditions that must all hold (below) |
| `playOnce` | Never repeats once heard |
| `cooldownRuns` | Runs before it can play again (repeatables) |

**Requirements.** Runs started; total deaths; deaths to this boss; wins against this boss; `requiresSeen` /
`requiresUnseen` (other line ids, which chain lines into a sequence); a boon or element held; the boss's phase; how
the last run ended, and to whom.

**Picking a line.** When an event fires, collect every line for that speaker and event whose requirements hold,
keep only the highest priority tier, and play the first unseen line in authored order (or a random one among
repeatables). Record it in the persistent save. Every event keeps at least one `Repeatable` line with no
requirements, so nothing ever goes silent. Mid-fight barks (hurt, stagger, move callouts) stay as they are now:
short, repeatable and never cutting off a full line (`GameManager.OnBossSpoke`).

**Events.**

| Event | When |
|---|---|
| `BossIntro` | Boss title card |
| `BossPhase` | Phase card |
| `BossDefeat` | Boss falls |
| `KilledOni` | Oni dies to this boss |
| `HubReturn` | Oni wakes on the hill (title screen) after a death |
| `FirstElement` | First Fire / Frost / Storm soul of the save |
| `Capstone` | A Duo or Legendary is taken |
| `Scroll` | A technique scroll is caught |
| `LastLife` | Oni is down to one life in a boss fight |
| `GodMode` | Dev god mode is on (dev builds only, a joke line) |

Each character's file holds their sample lines for these events.
