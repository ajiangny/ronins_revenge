# Boon design: analysis, research and refinements

This is a design note on Ronin's Revenge's Hades-style boon system. It covers what the system did before this pass,
what other roguelites do, what changed and why, and what's left for later. The code map is in
[Docs/Boons.md](Boons.md).

## 1. The system before this pass

| Piece | How it worked |
|---|---|
| Elements | Fire (blasts), Water/Frost (encase, freeze, shatter), Lightning/Storm (strikes, speed). |
| Pool | 15 boons, 5 per element. Each element has one for each core slot (Attack = slash, Special = Spirit Shuriken, Parry) plus 2 passives. |
| Rarity | Rolled per card, 60 / 30 / 10 for Common / Rare / Epic. A held boon comes back as an upgrade to the next rarity; an Epic leaves the pool. |
| Offers | A soul's element was uniformly random, and so was the boon after each stage. 3 cards, the first pick of a run always a slot boon, no skip or reroll. |
| Chains | Effect kills (`KillSource.Effect`) never trigger further effects. Shatter (an encased samurai cut down by the ronin) was the only cross-element interaction. |

### Problems

1. **Too many picks for too small a pool.**
   - The six rounds field 24 + 36 + 40 and 40 + 44 + 55 samurai, about 239 kills.
   - A 7% drop with pity at 15 kills gives a soul roughly every 9.5 kills.
   - Scouts and the two stage boons bring a run to about **28 offers against 15 boons**.
   - Each element maxes out in about 15 picks, so by stage 2 the cards were mostly rote "Upgrade" or "Replaces" offers.
   - When an element's pool was empty, `OfferBoons` returned false and the soul was **silently wasted**.
2. **No steering.**
   - Every soul and every stage boon was a random element, so committing to an element was luck.
   - Hades shows the god on the door before you choose a path, and gods you've met recur.
3. **No capstones.** No Duo, Legendary or prerequisite boons, so nothing rewarded a plan or mixing two elements on purpose. In Hades these are what a run builds toward.
4. **Passives were mostly stat sticks.** Blazing Hands, Storm Step and Static Charge only change numbers; Inferno Fan was the only one that changed how you play (it has since become Kindling).
5. **Uneven against bosses.**
   - Flame Blade added +1 damage on every slash that hit a boss, doubling slash damage.
   - Storm Blade added 25–50% of one point.
   - Thunder Riposte struck a boss once, whatever its rarity.
   - Tidal Renewal was +1 max life at both Common and Rare.
6. **No guarantees.** The boon after a boss had no rarity floor (Slay the Spire's boss reward is always rare).

## 2. Research: what other roguelites do

Many fan wikis block automated fetches, so some details below come from search summaries or memory. Check exact
numbers in-game.

### Hades (Supergiant, 2020)

- **One boon per verb.** Attack, Special, Cast, Dash and Call each hold one boon. Taking another god's boon for a
  filled slot replaces it, and passives sit beside the slots. A boon changes how an existing button feels instead
  of adding a button. Ronin's Revenge's three slots already mirror this. <https://hades.wiki.fextralife.com/Boons>
- **God identity through a status.**
  - Each god has one status, so whichever slot it lands in carries the god's identity: Aphrodite's Weak, Ares'
    Doom, Demeter's Chill, Dionysus' Hangover (caps at 5 stacks), Zeus' Jolted and chain lightning, Poseidon's
    knockback. <https://hades.wiki.fextralife.com/Status+Effects>
  - A Mirror talent rewards enemies carrying two curses, which pays off mixing gods.
- **Prerequisites, Duos and Legendaries.**
  - A Legendary boon needs specific boons from its own god (Zeus' Splitting Bolt needs one of his lightning boons).
  - A Duo boon needs boons from two gods, and either god can offer it.
  - The Blood Price update gave every pair of Olympians a Duo.
  - Gating turns random offers into a plan you can steer toward.
  - Sources: <https://primagames.com/tips/hades-what-are-duo-boons-how-get-duo-boons>,
    <https://www.supergiantgames.com/blog/hades-the-blood-price-update-patch-notes/>
- **Rarity and Poms.**
  - Common, Rare, Epic and Heroic scale a boon's numbers.
  - A Pom of Power raises one boon a level, with gains roughly halving every two levels, so the first Pom is the
    one you feel.
  - Duo, Legendary and Chaos boons can't be leveled. <https://hades.wiki.fextralife.com/Pom+of+Power>
- **How choices are offered.**
  - Every door shows its reward before you pick a path, and a boon room holds one god's three cards.
  - You choose the god first (strategy) and the boon second (tactics).
  - Runs drift toward about four gods.
  - Source: <https://www.inverse.com/gaming/hades-symbol-meaning-guide-hera-dionysus-apollo-ares-aphrodite>
- **Chaos, Daedalus hammers, Trials.**
  - Chaos boons are a curse for a few rooms, then a permanent blessing.
  - Daedalus hammers transform a weapon's moveset, two per run.
  - A Trial of the Gods makes you pick between two gods, and the loser haunts the room.
  - Sources: <https://hades.wiki.fextralife.com/Chaos>, <https://hades.gamepedia.com/Daedalus_Hammer>
- **Why Supergiant built it this way.**
  - Greg Kasavin cites Dota 2 as the model for ability and item choices.
  - Gods' mechanics were found from their characters; Demeter's cold came from looking past agriculture.
  - In Hades II, popular boons were "re-homed" rather than cut.
  - Sources: <https://afkgaming.com/dota2/news/6052-hades-dev-shares-how-dota-2-influenced-their-game>,
    <https://www.gamebanshee.com/news/121967-hades-interview.html>,
    <https://www.gamesradar.com/games/hades/hades-2-lead-says-the-best-boons-from-early-access-werent-killed-just-reskinned-we-think-its-important-not-to-be-too-precious-about-these-things/>

### Hades II (Supergiant, 2024–25)

- Every boon carries an element (Earth, Water, Air, Fire, Aether).
- **Infusion** boons switch on once you hold enough of their element, and some scale with the count.
- Commitment to an element pays off even through off-slot picks.
- Sources: <https://www.destructoid.com/hades-2-boon-elements-how-they-work-and-best-infusions/>,
  <https://maxroll.gg/hades-2/guides/hades-2-boons-and-gods-guide>

### Other roguelites

- **Vampire Survivors:**
  - A weapon **evolves** when it and its paired passive are maxed and you open a boss chest.
  - Banish, Reroll and Skip are counted run resources.
  - **Here:** the boss reward is where an eligible capstone is guaranteed.
  - <https://vampire-survivors.fandom.com/wiki/Evolution>
- **Brotato:**
  - Shop slots are weighted toward weapon types you already own (about 20%), and tiers unlock by wave.
  - **Here:** souls lean toward elements you hold.
  - <https://brotato.wiki.spellsandguns.com/Shop>
- **Risk of Rain 2:**
  - A **proc coefficient** per attack scales item triggers, and chain items have tiny ones, so chains damp out.
  - **Here:** keep "effects never chain" and give new effects fixed counts.
  - <https://riskofrain2.wiki.gg/wiki/Proc_Coefficient>
- **Enter the Gungeon:**
  - A synergy announces itself with an arrow over the player.
  - **Here:** Duo and Legendary cards are coloured and labelled the moment they're offered.
  - <https://steamcommunity.com/app/311690/discussions/0/364039785153660059>
- **Dead Cells:**
  - Colour-coded stats (Brutality, Tactics, Survival) make a colour mean a build.
  - <https://primagames.com/tips/how-get-upgrades-dead-cells>
- **Slay the Spire:**
  - Rare chance is 3% from normal fights, 10% from elites and 100% from bosses, with a hidden offset that rises
    per common.
  - **Here:** the boss boon is Rare or better.
  - <https://slay-the-spire.fandom.com/wiki/Card_Rewards>
- **Balatro:**
  - LocalThunk's Joker rules: one theme across art, name and effect, about three lines of text at most, and no
    lopsided choices.
  - **Here:** short card text that names the trigger.
  - <https://gamedeveloper.com/design/balatro-creator-unveils-a-few-slick-signposting-strategies>
- **Nova Drift:**
  - This is the closest arcade-shooter match.
  - Offers never repeat back to back, and taking a "specialist" mod prunes its rivals from the pool.
  - <https://pixeljam.itch.io/nova-drift/devlog/84291/the-virulence-update-is-live>
- **20 Minutes Till Dawn:**
  - Short upgrade trees, and a synergy layer offered only once its prerequisites are held.
  - It has a run length close to ours.
  - <https://20-minutes-till-dawn.fandom.com/wiki/Upgrades>

### What carries over to a Space Invaders lane

- **Targets are a grid.** The samurai stand in rows and columns, so on-kill spread (blasts, chains), slows
  (freezing the march) and column-aware effects all have room to shine.
- **The march quickens as the army thins.** That speed-up is Space Invaders' core tension, and a boon that
  bends it is felt at once.
- **Arrows are the threat.** Effects that clear arrows are defence that comes from attacking, which suits a
  player stuck on one row.
- **Picks need to land hard.** A run is short (2 stages, 6 rounds and 2 bosses), so a few picks should change
  play, not only numbers.
- **Arrows must stay readable.** Effects sit under enemy arrows in the draw order and status shows as a tint
  (frost, ice) rather than covering particles.

## 3. What changed

### Capstone boons: Duo and Legendary

Core slot boons fall into three families, which the capstones require:

| Family | Boons |
|---|---|
| **Blast** (Fire) | Flame Blade, Inferno Shuriken, Burning Riposte |
| **Ice** (Frost) | Frost Blade, Glacial Shuriken, Frozen Riposte |
| **Strike** (Storm) | Storm Blade, Thunder Shuriken, Thunder Riposte |

#### Duo boons

You need one boon from each family of the pair. A soul of either element can offer it.

| Duo | Elements | Effect |
|---|---|---|
| **Thermal Shock** | Fire + Frost | Fire blasts deal double damage to samurai held in ice, or to the whole army while it's frozen. On a frozen boss a blast deals 1 more. |
| **Plasma Arc** | Fire + Storm | Every lightning strike bursts into a small fire blast around its target. On a boss each strike deals 1 more. |
| **Superconductor** | Frost + Storm | A strike on a samurai held in ice arcs on to up to 3 more within reach. Strikes on a frozen boss deal double. |

#### Legendary boons

You need two core-slot boons of that element.

| Legendary | Element | Effect |
|---|---|---|
| **Wildfire** | Fire | Blasts grow wider and burn away the enemy arrows inside them (not spears or other unbreakable shots). |
| **Still Water** | Frost | The army quickens only half as much as its ranks thin, and your freezes last half again as long. |
| **Raijin's Drum** | Storm | Every 8th hit from your own attacks beats the drum: lightning strikes the front samurai of the 3 columns nearest you, or the boss 3 times. |

All of them are passives that can't be upgraded. Every kill they make is still an effect kill, so nothing chains
further. On the card the rarity line reads **Duo** (green) or **Legendary** (orange) and names the elements.

### Steering and guarantees

- **Souls lean toward held elements.** After the first boon, each soul has a chance (`GameManager.soulElementBias`,
  50%) to take an element the ronin already holds, weighted by how many boons of it he has. Otherwise its element
  is random.
- **The boss boon is a reward.**
  - If a capstone is within reach, the stage boon uses its element and is sure to include it.
  - Its cards are Rare or better.
- **No wasted souls.** When an element has nothing left to offer, its soul gives a **Spirit Offering**: it heals
  one life, or gives points at full health.

### Rebalance

- **Storm Blade:** chain chance goes from 25 / 35 / 50% to **35 / 50 / 70%**, closer to Flame Blade's sure blast.
- **Thunder Riposte:** on a boss it now strikes once per level, as it does against the army.
- **Tidal Renewal:** Rare now also heals every lost life (Common +1, Rare +1 and a full heal, Epic +2).

### Sword Arts (done since)

- **The drop.** Hades' Daedalus Hammer became the **technique scroll**: a rare pickup, mostly from kite scouts (40%,
  plus 0.5% from any kill), at most two a run. It opens a **Sword Arts** screen of arts not yet held.
- **The arts:** Split Slash, Piercing Slash, Echo Slash and Shadow Clone. They reshape the slash itself and stack
  with each other and with the element Blade boons. Crescent Moon (a wider slash) was the first fourth art, but even at
  2.6× as wide it felt weak next to the others. Shadow Clone replaced it: a mirrored shadow that slashes with you
  doubles your coverage of the formation.
- **Kindling replaced Inferno Fan.** Inferno Fan's spread of slashes overlapped Split Slash. Kindling first set off a
  blast every few slash kills, which played too quietly. Now every few swings it throws a great fireball instead
  of a slash, which bursts in a wide blast. It's something you see coming and can aim.

## 4. The path revamp (fewer, weightier boons)

**The problem.** With four stages a run fields about 636 samurai. At 7% a kill with pity at 15 that was a soul every
~9.5 kills: **about 65 offers a run**, plus scouts and stage boons, against 21 boons, and every held boon came back as
an upgrade card. Slots were Epic by stage 2, every capstone was reachable, and runs became god runs.

**What changed.**
- **Paths between rounds** (Hades' chamber doors): each round is fought for a reward chosen at a path (a soul of a
  shown element, a Spirit Bead, Spirit Notes (a Lost Melody), a healing spring, a scroll); a stage's first round is always a soul. The
  boss's path opens from round 3; a stage runs up to 5 rounds, the extra ones harder. Samurai drop souls only rarely
  (1%, at most one a stage); scouts do 20% of the time. Picking the soul's element at the path is Hades' "god first, boon second".
- **Spirit Beads** (Pom of Power): souls no longer offer held boons; a bead raises one held boon a rarity. One follows
  each stage boon, more from bead paths.
- About **12–16 boons a run**, each a new boon, a slot swap or a capstone.

**Numbers** (before → after):

| Boon | Before | After |
|---|---|---|
| Rarity odds | 60 / 30 / 10 | 70 / 25 / 5 |
| Glacial Shuriken freeze | 1 / 1.5 / 2 s | 0.6 / 0.9 / 1.2 s |
| Frost Blade freeze | 2 / 3 / 4 s | 1.5 / 2 / 2.5 s |
| Flame Blade / Inferno Shuriken / Burning Riposte Epic radius | 2.7 (diagonals) | 2.3 (diagonals are Wildfire's) |
| Blazing Hands, Static Charge | 15 / 25 / 35% | 10 / 18 / 25% |
| Tidal Renewal Rare | +1 life and heal all | +1 life |

**First balance pass from the play log** (`RunLog`, three runs, one dawn):
- Rounds without boons took 21–30 s with almost no hits; once a fire build formed, rounds took **5–10 s** even in
  stage 4, with boon effects making **55–66% of kills**. Rounds hit the player once in about 25; bosses (63–100 s,
  4–7 hits) were where all the danger was.
- **Fire blasts burn at most 3 samurai**, nearest first (5 with Wildfire), so chains of blasts can't wipe a packed army.
- **Armies shoot more:** fire interval ×0.8 in every round, aimed shots +10 points (to at most 60%), divers from
  stage 1 round 2 (9 s) and a bit more often everywhere (×0.85).
- **Sword Arts are rarer:** no scroll drops from kills (was 0.5%), scouts 25% (was 40%); paths still offer them.
- Boss health unchanged: the fights already land in the 60–120 s target.

**Second pass** (two more runs): rounds took 15–40 s with a hit about every other round, but frost builds now made
50–65% of kills (Epic Frost Blade cleared stage 1 rounds in 8–11 s). **Frost Blade** now encases each samurai in reach
with a 50% chance, at most 2 per kill (was every one in reach), and its shatter shards are 1 / 2 / 3 (were 2 / 3 / 4).

**Third pass (play testers):** Flame Blade + Shadow Clone + Split Slash trivialised the game. Flame Blade blasted on
every slash kill and added a point to every boss hit; the shadow doubled every slash at full strength with every boon
proc; Split added two more. Bosses with slow, static moves (Utsusemi's Hour Strikes) took free damage, and Shirogane's
crescent fans were cut down before they arrived.
- **Shadow Clone became an ultimate** (`Ronin.Ultimate`), chosen at the temple: a bar filled by 40 of the ronin's own
  hits, unleashed with Z or I (E or Middle Click with the mouse) for 10 s of fighting. Its slashes are `KillSource.Shadow`: half damage to bosses, no boon effects,
  no charge. It left the scroll pool (Split, Piercing, Echo remain).
- **Ultimate boons** (`BoonSlot.Ultimate`, Hades' Calls, done since): Pyre Shadow, Frost Shadow and Storm Shadow lend
  the Shadow Clone their element, held one at a time and shown on the medallion.
- **Split Slash's side slashes** are `Shadow` slashes too.
- **Fire:** Flame Blade bursts on 40 / 55 / 70% of slash kills (on a boss hit, 60% of that); blasts burn 2 samurai (4 with
  Wildfire); Kindling 6 / 5 / 4 swings and its fireball no longer also sets off Flame Blade; Blazing Hands 8 / 14 / 20%.
- **Storm:** Storm Blade 45 / 60 / 80%, Thunder Shuriken 40 / 55 / 70%, Static Charge 15 / 22 / 30%, Raijin's Drum every 6 hits.
- **Glacier Wall → Glacier Shell** (`BoonId.GlacierShell`): tougher cover also stopped the player's own slashes, so it was
  rarely taken. Now a shell of ice takes one blow, bursts into 5 homing shards and regrows in 18 / 13 / 9 s (a bead shortens it).
- **Cover** is rebuilt every round.

## 5. Levels, Exchanges and the Thread (Hades' boon growth)

**The problem.** A Spirit Bead raised a boon one rarity, so every boon topped out after two beads and the bead stopped
mattering once a build was Epic.

**What Hades does** ([Hades wiki](https://hades.fandom.com/wiki/Pom_of_Power?oldid=9984),
[Fextralife](https://hades.wiki.fextralife.com/Pom+of+Power), [Hades II](https://hades2.wiki.fextralife.com/Boon)).
Rarity and level are separate:
- **The Pom of Power adds a level.** Each level adds less than the last, so Lv1 → 2 is the biggest step. A level is
  kept when the rarity changes.
- **Poms can't touch capstones.** Duos, Legendaries and Infusions can't be pommed.
- **Rarity rises in only a few ways.** An **Exchange**, a god's boon taking another god's slot, raises the slot's
  rarity a tier and keeps its level; Hades II's Sacrifice does the same. Hades II's **Yarn of Ariadne** (sold at
  Charon's well) and a few boons (Chaos favours, Hera's Bridal Glow) do too. Otherwise rarity is what the card rolled.
- **Rule of thumb.** In Hades II a Lv2 Common is about a Lv1 Rare.

**What we do.**
- **Spirit Beads add a level** (`EquippedBoon.Level`, `OfferKind.Level`).
  - `BoonDefinition.ValueAt(rarity, level)` adds `LevelStep` for the first level and 0.8 of the previous step for each
    after it (`LevelFalloff`). The steps are sized so a **Lv3 Common is about a Lv1 Rare**.
  - Whole-number stats (strikes, shards, Kindling's swings) move once the levels add up to a whole one.
  - `Min` / `Max` bound every stat: blasts stay under the diagonals at 2.6, chances under 95%, Glacier Shell at 6 s or
    more, Kindling at 3 swings or more.
  - There is no level cap. A boon whose stats are already at their bounds isn't offered.
- **Exchange.** A soul's boon that would replace another element's in its slot comes with that boon's level and at
  least one rarity above it (`Boons.Draw`). The card says "Exchanges X · keeps Lv N".
- **Spirit Thread** (`PathReward.Thread`, weight 6). It raises a held boon a rarity and keeps its level
  (`Boons.DrawThread`, `BoonChoiceUI.ShowThread`, `Boons.ThreadColor`).

| Boon | Step per level (first) | Bound |
|---|---|---|
| Fire blasts (Flame Blade, Inferno Shuriken, Burning Riposte, Pyre Shadow) | +1 samurai burned (whole; see §8) | 4 (5 for Inferno, Burning) |
| Flame Blade chance | +8% | 90% |
| Blazing Hands | +3.5% | 30% |
| Kindling | −1 swing (whole) | 3 |
| Frost Blade | +0.28 s, +1 shard (whole) | 5 shards |
| Glacial Shuriken / Frost Shadow | +0.17 s / +0.28 s | |
| Frozen Riposte | +1 shard (whole) | 6 |
| Glacier Shell | −2.8 s | 6 s |
| Storm Blade, Thunder Shuriken, Storm Shadow | +8% | 95% |
| Thunder Riposte | +1 strike (whole) | 5 |
| Storm Step | +5.5% | 50% |
| Static Charge | +4% | 45% |
| Storm Shadow fill speed | +5.5% | 60% |

## 6. Later

- **Shared statuses.**
  - Burn: stacking damage over time, capped.
  - Chill: stacks that freeze at a threshold, then a short immunity.
  - Jolt: discharges on the samurai's next shot.
  - These would give each element one readable status across every slot, like Hades' gods. Done since for one
    element: Water's Soaked (§7) is the first status that every slot of an element applies and every other
    element's effects read.
- **More Sword Arts.** Arts for the shuriken and parry: the shuriken splits into three lanes, or the parry holds a
  stance that reflects every arrow for half a second.
- **Cursed souls (Chaos).** For example the army marches 20% faster for two rounds, then the shuriken recharges
  30% faster for good.
- **Run resources.** Redraws and banishes are now Flute Songs (Fate's Thread, Banishing Bell). Still open: never
  repeat the previous offer, and a rarity pity offset.
- **Icons.** Done since: every capstone and the Offering has its icon in `UI/Boons/<BoonId>`.

## 7. The fourth element: Water (Asagi)

**Why a fourth.** Three elements give three Duos, and with ~12–16 picks a run every pair is reachable most runs.
Hades' boon space is wide because the gods are many: a fourth element adds three new pairs (six Duos in all), a
fourth Legendary, and a first element whose identity is defence rather than damage. It arrives late, as Hades'
later gods do: the Water soul, [Asagi](../Story/Asagi.md), joins only once Oni has met her in the temple
(`Save.Data.asagiMet`, the wake after the tenth death), so the first runs keep the three-element pool.

**Code name.** `Element.Tide`, appended to the enum so `SaveData.lastRunElement` keeps meaning what it did.
Screen name "Water" (and `Element.Water` keeps meaning Frost, since every Frost asset is named by the enum).

**What Water does.** Tide and healing, kept apart from Frost's freeze and shatter:

- **Soaked**, the status (`Samurai.Soaked`, `Boss.Soaked`; a sea-blue tint and drips). A soaked target takes **+1
  from every boon effect** (blasts, strikes, shards, waves). It does nothing on its own: it's the amplifier that
  makes mixing a second element pay, which the three-status note in §6 asked for.
- **The tide push** (`SamuraiArmy.PushBack(units)`). The formation is lifted back up the screen, undoing march
  steps, never above where it spawned. The loose last row is lifted with it and sinks back at the march's pace
  (rejoining the formation would spread the march edge to edge, so it would turn and drop at once). Space Invaders'
  whole tension is the descent; Water is the one element that rolls it back.
- **Washing arrows** (`Arrow.Wash`): like Wildfire's burn, for ordinary arrows only, never spears or other
  unbreakable shots.

| Boon | Slot | Common / Rare / Epic |
|---|---|---|
| **Riptide Blade** | Attack | A slash kill may (40 / 55 / 70%) send a wave up the victim's column: every samurai in it is soaked (4 s) and the arrows in the column wash away. On a boss hit, the same chance soaks the boss. |
| **Tidewake Shuriken** | Special | The shuriken leaves a wake: arrows it passes wash away, samurai it passes within reach are soaked for 3 / 4 / 5 s, a boss it passes too. |
| **Undertow Riposte** | Parry | A parry sends a wave that pushes the army back up 0.3 / 0.45 / 0.6 units and soaks the front row. On a boss, soaks it. |
| **Tidal Renewal** | Passive, instant | +1 / +1 / +2 max life. Moved here from Frost: it's her medicine and her name. |
| **Healing Spring** | Passive | Every 45 / 35 / 25 kills by Oni's own hits (not effects) restores one lost life, or pays points at full health. |
| **Tide Shadow** | Ultimate | Unleashing the clone sends a great wave: every arrow washes away and the army is pushed up 0.6 (a boss is soaked). Clone kills soak their neighbours. |
| **Dragon Gate** | Legendary (two Tide slots) | Once a stage, a blow that would take the last life is turned aside and 2 lives are healed. The koi climbs the falls. |

Frost loses Tidal Renewal and gains **Cold Compress** (Passive): when Oni is hit, the army freezes for 1 / 1.5 / 2 s,
or the standing boss for half that. It is the defensive passive Frost never had, built on `SamuraiArmy.Freeze` and
`Boss.Freeze`, and it keeps Mizore's family at five.

**Duos** (the `TideBoons` family: Riptide Blade, Tidewake Shuriken, Undertow Riposte, Tide Shadow):

| Duo | Pair | Effect |
|---|---|---|
| **Scalding Steam** | Fire + Water | A blast centred on a soaked samurai is +0.5 wider and washes away the arrows inside it. |
| **Black Ice** | Frost + Water | A soaked samurai that is encased stays encased twice as long, and its statue shatters into double shards. |
| **Thunder Rain** | Storm + Water | A strike on a soaked samurai leaps to every other soaked samurai in its row. |

**Levels** (the §5 conventions): wave chances +8% a level to 90%; soak times +0.4 s to 8 s; the push +0.08 to 0.9;
Healing Spring −5 kills (whole) to a floor of 15; Tidal Renewal is instant, so it isn't levelled; Cold Compress
+0.28 s to 4 s.

**Balance to watch** (`RunLog`): Water should make few kills of its own, so its kill share will read low; what to
check is the share of *other* elements' effect kills on soaked targets and the arrows washed per round. If Undertow
alone holds the army off the floor, lower the push or let it apply only when the army is below half its descent.
Dragon Gate is once a stage so it can't trivialise bosses.

## 8. Review fixes: dead levels, boss-fight healing, Kuon and Water

A review of the code found three places where a boon's stat didn't do what its card said.

- **Fire levels did almost nothing against the army.** A blast burned at most 2 samurai, nearest first, but rarity
  and Spirit Beads raised its *radius* (1.8 → 2.6). Once two samurai were in reach, a bigger radius changed nothing,
  so a bead on Inferno Shuriken, Burning Riposte or Pyre Shadow was close to wasted. Now every blast reaches a fixed
  `BlastRadius` 2.1 (above, below and beside; the diagonals stay Wildfire's), and the stat is **samurai burned**:

  | Boon | Common / Rare / Epic | Per level | Bound |
  |---|---|---|---|
  | Flame Blade | 2 / 2 / 3 (its chance is what Rare raises) | +1 (whole) | 4 |
  | Inferno Shuriken, Burning Riposte | 2 / 3 / 4 | +1 (whole) | 5 |
  | Pyre Shadow (radius 1.9; its end blast 2.9, one more) | 1 / 2 / 3 | +1 (whole) | 4 |

  Wildfire now reads "+0.6 radius and two more burned" (it was 4 burned in place of 2). Kindling and Plasma Arc keep 2.
  Common play is unchanged; Epic Inferno and Burning gain the most. Watch the fire kill share in `RunLog`.
- **Healing Spring was dead in boss fights**, where the play log says the lives are lost: it counted only kills.
  Three procing blows on a warlord (or Kuon) now count as one kill.
- **Water did nothing to Kuon.** Kuon now has its own Soaked (+1 from boon effects, a sea-blue wash), applied by
  Riptide Blade (at the boss share of its chance), Tidewake Shuriken's wake, Undertow Riposte and Tide Shadow.

Two more from the same review:

- **Choosing the element.** Paths offered at most one soul, so the "god first, boon second" choice was only a hidden
  50% lean. Once a soul path rolls, a second soul path (weight 30 against the others) can roll beside it, never of
  an element already shown. `PickSoulElement` now weights the lean by the boons held of the elements still on offer
  (an exchanged-out boon no longer counts; before, the leftover fell to the dominant element).
- **Synergy signposting** (Enter the Gungeon's arrow): a card whose boon would bring a Duo or Legendary within reach
  says "Opens <name>" in the capstone's colour. An Exchange that breaks a requirement opens nothing, so the hint
  never promises a capstone the swap would lose.

## 9. Statuses and Infusions

**Why.** §6 asked for one status per element, and the review found two gaps it would close: effect builds did a
flat 1 damage whatever their rarity, and passives counted toward nothing. Most samurai have 1 HP (shieldbearers,
taisho and bellringers 2), so a damage-over-time status would almost never matter against the army. The army's
statuses work on its **arrows** instead, the real threat in a Space Invaders lane, and on a warlord each gives an
edge rather than raw damage, so the fights' 60–120 s target holds.

| Status | Applied by | On the army | On a warlord |
|---|---|---|---|
| Burning (3 s) | every fire blast, to the samurai in reach it doesn't kill | can't shoot | stance from your hits ×1.5 |
| Chilled (4 s) | Frost Blade / Frost Shadow, to those not encased; every freeze, for its length + 4 s | arrows at 60% speed, frost-pale | next freeze ×1.5, spent |
| Jolted (4 s) | every lightning strike, to the samurai beside its target in the rank | discharges instead of shooting: one effect hit | next own hit +1, spent |
| Soaked | Water (§7) | +1 from boon effects | +1 from boon effects |

Jolt's discharge is a `KillSource.Effect` hit, so the "effects never chain" rule holds: it kills, but sets off
nothing. Chilled arrows have their lifetime stretched by the same factor, or they'd vanish before reaching the ground.

**Infusions** (Hades II): a capstone per element, offered once 3 boons of that element are held, **any slot**, so
the passives finally build toward something. Each deepens its status rather than adding a new mechanic:

| Infusion | Effect |
|---|---|
| Ember Heart (Fire) | Burning ×2 duration; a burning warlord's stance ×2 instead of ×1.5 |
| Hoarfrost (Frost) | Chilled ×2 duration; chilled arrows at 35% |
| Static Field (Storm) | a strike jolts its target's whole rank; a warlord's discharge +2 |
| Deep Current (Water) | Soaked ×2 duration; tide pushes ×1.5 |

**Tested** in the editor: jolted samurai discharged (7 of 8 within 12 s, as each was picked to shoot), chilled arrows
flew at 4.2 against 7 and reached the ground, and on Benibana a jolted slash dealt 2, a burning shuriken added 0.15
stance against 0.10, and a chilled 1 s freeze held 1.5 s.

**To watch** (`RunLog`): Storm's kill share (discharges are new effect kills), how often rounds pass with no arrows
loosed under fire builds, and whether boss stagger rates climb too far with Ember Heart. 

**Since.** The "Opens …" card hint was taken out: playtesting found it distracting and too much text on a card that
already carries a description and two stats. Infusions now have icons and voiced soul lines, and Kuon takes all
four statuses (its own forms, since it has no stance and frost can't hold it): Burning adds half of each of the
ronin's blows as extra damage, Chilled lengthens its next daze ×1.5, Jolted adds to his next blow. Tested on Kuon in
phase 2: a jolted hit dealt 2 against 1, a burning 2-point hit dealt 3, a chilled 1 s daze held 1.5 s.

## 10. Softer statuses, passives that play, fairer offers, two more arts

**Statuses were as good as kills.** Every samurai that shoots has 1 HP (the 2-HP shieldbearers and bellringers
never shoot, the taisho only throw spears), so a status that stopped a samurai shooting removed it from the fight,
and Jolt's discharge simply killed it. Raising every samurai's HP was considered and rejected: the slash's one-hit
kill is the Space Invaders feel, rounds would roughly double in length, and every on-kill boon would fire half as
often. Instead Burning sends a samurai's shots **wild** (up to 35° off, ember-red) and a Jolted samurai's next shot
**fizzles**, leaving it unhurt. The cost of 1 HP that remains: Soaked's +1 and Thermal Shock's ×2 do nothing to an
ordinary samurai. If that needs fixing, the targeted answer is a rank of 2-HP veteran shooters in later stages, not HP
everywhere.

**Passives that play.** The three stat-stick passives each gained a behaviour tied to their element's status:

| Passive | Was | Now |
|---|---|---|
| Blazing Hands | slash cooldown −8 / 14 / 20% | heat: up to −12 / 20 / 28% after three quick swings; at full heat a slash kill ignites its neighbours (a warlord, or Kuon) |
| Storm Step | move speed +15 / 25 / 35% | +8 / 12 / 16%, and while moving, static breaks the nearest enemy arrow every 2.5 / 2 / 1.5 s |
| Static Charge | cooldowns −15 / 22 / 30% | −10 / 15 / 20%, and a landed parry charges the next shuriken to jolt what it passes |

**Fairer offers.** A redraw or the next soul no longer hands back the cards just shown, unless nothing else is left.
Rarity has Slay the Spire's pity: each Common rolled adds 2 to the Rare weight and 0.5 to the Epic, reset by a Rare or
better. Simulated over 60,000 cards: 70 / 25 / 5 becomes about 67 / 28 / 6, and dry spells of ten Commons halve.

**Two more Sword Arts** (§6's "arts for the shuriken and parry"): **Scattering Star** (the shuriken splits, two lesser
blades at ±14° that stop after two samurai) and **Mirror Stance** (a landed parry holds 0.5 s longer, turning back
everything that arrives). Scrolls now draw from five arts.

**Tested in the editor:** jolted samurai fizzled and none died; burning samurai's arrows flew at an angle; three quick
swings reached full heat (cooldown ×0.88 at Common); Storm Step's static popped an arrow beside him; Mirror Stance held
0.50 s; a charged, scattered shuriken left as three blades and jolted what it passed.

## 11. The fifth element: Steel (Oliver)

**Status: built, with his art and figure (§12, steps 18 and 19).** Every number below is a paper value and none has
been balance tested: see [Tested](#tested) in §12. The code references were checked against the scripts.

**Why a fifth.** Four elements give six Duos; a fifth adds four more pairs, a fifth Legendary and a fifth Infusion. It
also fills the one gap left in the lane. Fire and Storm deal damage, Frost controls, Water soaks and heals, and nothing
touches the **cover**: twelve blockers (four sites of three, 18 to 20 health a site, 72 to 80 in all) that are rebuilt
every round and only ever worn down. Steel's identity is that **attacking rebuilds cover**. The soul is Oliver, the cat
smith with the spirit-charged hammer. Like Asagi he arrives late, so the first runs keep the smaller pool.

**Code name.** `Element.Steel`, appended to the enum, with its `BoonId`s appended too, so saved indexes and the icon
array keep meaning what they did. The family is `SteelBoons`: Anvil Blade, Lodestone Shuriken, Hammer Riposte, Steel
Shadow.

### What the code forced

Four facts decide the design, and each one changes the first draft.

- **Scrap falling straight down lands in his firing lane.** He shoots from the gaps between sites, so what he kills
  stands above a gap. Falling straight, scrap would either miss the hurt cover or plate the gap shut in front of him.
  Every slash stops on a blocker and chips it (`Blocker.OnCollisionEnter`, `Projectile.OnCollisionEnter`), which is
  exactly what got Glacier Wall cut (§4). So scrap is drawn sideways, and plates let his shots through.
- **Homing shots stop at cover and chip it** (`Projectile.cs`, the `passes` test). A steel shard built by copying
  `LaunchIceShard` would chip the cover beside the one that burst, could break it, and could set off Shrapnel again.
  Steel shards must pass cover.
- **`Blocker.Broken` pays a heart 20% of the time** (`GameManager.OnBlockerBroken`). One-point plates raising it would
  be a healing farm.
- **Cover is more than bushes.** `BeamEnd` stops a beam on any `Blocker`, Utsusemi's Hyper Beam is measured against its
  rocks' shade, and those rocks and its planted sword are `Blocker`s with `int.MaxValue` health. A new plate would be
  a safe spot no warning showed, and adding to `int.MaxValue` overflows. So in a warlord's fight Steel never adds a
  `Blocker`, and it only mends the wall's own children.

### The status and scrap rules

**Cracked** (`Samurai.Crack`, `Boss.Crack`, `SpiritWolf.Crack`; 4 s, dull iron with bright seams, `Statuses.CrackSeconds`).
Most samurai die in one hit, so it works on cover, not health.

| Status | Applied by | On the army | On a warlord | On Kuon |
|---|---|---|---|---|
| Cracked (4 s) | Steel's boons (below) | felled by Oni's own hit, sheds 1 scrap | his next own hit knocks loose 3 scrap and spends it, at most once per 5 s (`BossCrackCooldown`, as `BossFrostCooldown` limits frost) | as a warlord, sharing the cooldown: the hammer is spirit-charged, so a spirit cracks too, and Kuon's usual reason for its own form (no stance, no freeze) doesn't apply |

This is the one status that gives no edge against the warlord herself: Burning fills her stance, Chilled holds her
longer, Jolted and Soaked add damage. Cracked only pays in cover, so the 60–120 s fight target isn't touched.

**Only his own hits shed scrap** (`Damage.Procs(source)`). A `KillSource.Effect` kill of a cracked samurai sheds
nothing, and neither do Shadow or Clone kills, because a kill by an effect must not trigger another effect. An effect
may still *apply* Cracked, as blasts apply Burning.

**Scrap** (one piece, one point) falls from where it was shed and is resolved in this order:

1. **Mend.** Hurt wall cover within 2.5 units sideways draws it in and regains 1 health, never above its maximum.
2. **Plate.** In a round, with no hurt cover in reach, it forms a **steel plate** in the gap below.
3. **Spare.** Otherwise it's worth half a hit on the Ultimate's bar, so scrap is never dead, even after Utsusemi razes
   the wall.

In a warlord's fight step 2 is skipped and step 1 reaches any hurt wall cover on the field.

**Steel plates:**
- 1 health, at most 4 standing (`MaxPlates`), gone at the round's end with the rest of the cover.
- **One-way:** every projectile of Oni's passes through without chipping it, the way the shuriken does. Arrows stop on
  it. Spears, yurei arrows and boss slashes pass as they pass all cover.

**What Steel builds pays nothing when it breaks.** Plates and blockers raised by Steel Shadow raise neither a heart nor
Shrapnel. Mending a blocker doesn't re-arm it either: each of the twelve can burst only once a round.

**Steel shards** are `KillSource.Effect`, 1 damage, homing, and pass cover. They set off nothing.

### Boons

| Boon | Slot | Common / Rare / Epic | Card text |
|---|---|---|---|
| **Anvil Blade** | Attack | Chance 40 / 55 / 70%; samurai cracked 1 / 2 / 2 | A <b>Slash</b> kill may ring the anvil: it sheds <b>Scrap</b> and <b>Cracks</b> the samurai beside it. |
| **Lodestone Shuriken** | Special | Arrows caught 2 / 3 / 4 | Your <b>Shuriken</b> draws in arrows, which fall as <b>Scrap</b>, and <b>Cracks</b> the samurai it passes. |
| **Hammer Riposte** | Parry | Cover mended 1 / 2 / 3 | A successful <b>Parry</b> rings out and mends the cover nearest you. |
| **Steel Shadow** | Ultimate | Crack time 3 / 4 / 5 s | Unleashing your <b>Shadow Clone</b> restores all your cover, and its kills <b>Crack</b> their neighbours. |
| **Whetstone** | Passive | Cover spared 15 / 30 / 45% | Your <b>Slash</b> passes through your own cover, wearing it down, and pierces one samurai. |
| **Shrapnel** | Passive | Shards 2 / 3 / 4 | When your cover breaks, it bursts into homing steel <b>Shards</b>. |
| **Spear-Breaker** | Legendary (two Steel slots: `TwoSlotsOf(l, Element.Steel)`) | Scrap per spear 1 | Your <b>Slash</b> breaks cursed spears, and each falls as <b>Scrap</b>. |
| **Tamahagane** | Infusion (`ElementCount(Element.Steel) >= InfusionCount`, 3 of any slot) | Crack time ×2 | <b>Cracked</b> lasts twice as long, and <b>Scrap</b> can temper cover beyond new. |

What each does beyond its card, which stays lean as the other cards do:

- **Anvil Blade.** The proc sheds one scrap at once, so it pays before anything cracked is cut down. Those cracked are
  the nearest in the victim's rank (`SamuraiInRank`). On a boss or Kuon, a `BossRiptideShare` (60%) of its chance cracks
  the target.
- **Lodestone Shuriken.** Catches within 2 units, each arrow becoming a scrap on the spot. It skips what Storm Step's
  static skips (`Unbreakable`, `Reflectable`, `Unparryable`) and any shot with `Parried` set. Cracks within
  `WakeRadius`, once each, built like `ShurikenWake`; a boss or Kuon it passes is cracked too.
- **Hammer Riposte.** The nearest hurt wall blocker within 3 units takes the points. With none, in a round, a plate
  forms above him; in a warlord's fight, the points go to the nearest hurt wall cover anywhere, or the target is simply
  cracked. On a boss or Kuon it always cracks.
- **Steel Shadow.** In a round, standing cover returns to full and fallen cover is raised. In a warlord's fight only
  standing cover is restored, so no new `Blocker` appears. On a boss the unleashing cracks it, and clone hits crack on
  `CloneBossShare` (25%). Clone kills shed nothing themselves; the neighbours they crack pay when Oni cuts them down.
- **Whetstone.** Only `KillSource.Slash` passes, and it gains one pierce however many blockers it crosses. Each blocker
  still takes a chip unless the spared roll succeeds. A site lasts about 20 s of slashing at the 60% bound, so holding
  still under cover always costs something.
- **Shrapnel.** Fires on hits only, as `Blocker.Broken` does; trampling and razing pay nothing. Shards home as Frost's
  do. Against a warlord or Kuon the count is halved, rounded up. Worst case in a round is 12 × 4 = 48 shards, which
  takes about 72 slashes of his own, more than clearing the round with those slashes would need, so breaking his own
  cover on purpose doesn't beat shooting.
- **Spear-Breaker.** On a warlord or Kuon, parrying one of their unbreakable shots home also cracks them, so the
  Legendary isn't idle in the fights where lives are lost. See below for what it may break.
- **Tamahagane.** `Statuses.CrackScale` 2. Scrap at full cover over-tempers it, up to 2 points above its original
  maximum; this raises `maxHealth` too, because `UpdateSprite` indexes out of range if health passes it. A warlord's
  crack knocks loose 5 scrap, not 3.

### What Spear-Breaker may break

**Exactly one thing: the yari a Taisho throws in `Samurai.Fire`.** Mark it with a new `Arrow.CursedSpear`, set at that
call site (`Samurai.cs`, the `Kind == SamuraiKind.Taisho` branch), and test the flag in `Arrow.OnCollisionEnter`'s
`Unbreakable` branch for a `Projectile` whose `Source` is `KillSource.Slash`.

Neither of the two obvious shortcuts is safe:

- **Not inside `MakeUnbreakable(Sprite)`, and not by `Cause == DeathCause.Spear`.** Utsusemi's flying hats and sword
  call `hat.MakeUnbreakable(null)` (`Utsusemi.Kasa.cs`), the same overload, which sets that cause too.
- **Not by `Unbreakable`.** That also covers Shirogane's charges, pillars, spires, moons and Fang, Utsusemi's hour
  cleaves, and Kagetsune's bell and coffin. Several are also `Reflectable` set pieces, whose parry is what breaks a
  warlord's stance (`SetPieceStance`). If a slash could cut them, the parry that makes those fights would have no job.

Also left alone: the tutorial's `GameManager.SpawnSpear` and `TrainingSoldier`, which teach that spears can't be cut.
The Taisho in Kagetsune's levy throw through the same `Samurai.Fire`, so their yari can be broken; his own shots can't.

Steel shards don't break spears, unlike in the draft. Homing shots slip past arrows on the way to their target (the
`passes` test), so they would never touch one. Clone slashes and Split Slash's forks don't either: `Source` is what's
checked, and they're `Clone` and `Shadow`.

### Duos

| Duo | Pair | Effect | Card text |
|---|---|---|---|
| **Red-Hot Iron** | Fire + Steel | Each scrap burns up to 2 ordinary arrows within 1 unit of its path (`Arrow.Burn`, skipping what Storm Step skips). | Your <b>Scrap</b> falls red-hot, burning away the arrows in its path. |
| **Quenching** | Frost + Steel | Oni's own hit on a samurai that's encased, or while the army is frozen, sheds scrap whether cracked or not, and that scrap mends 2. A freeze on a boss cracks it; the cold reaches Kuon as it does for Chilled. | Samurai you cut down frozen or held in ice shed <b>Scrap</b> that mends double. |
| **Lightning Rod** | Storm + Steel | An arrow chipping wall cover strikes the front samurai of the nearest column to it (`ClosestColumnTo`, `FrontSamuraiInColumn`), at most once per 3 s; on a boss, once per 8 s. | When an arrow chips your cover, <b>Lightning</b> strikes up its lane. |
| **Rust** | Water + Steel | Every `Soak` also cracks, on the army, a warlord and Kuon alike. | Whatever you <b>Soak</b> is <b>Cracked</b> too. |

Each needs one boon of the partner's family and one of `SteelBoons`, so each must work whichever family boon is held.
That rules out two of the draft's triggers (below).

### Levels

The §5 conventions apply. A whole step is first earned at Lv 2, so a Lv 3 Common matches a Lv 1 Rare.

| Boon | Step per level (first) | Bound |
|---|---|---|
| Anvil Blade chance | +8% | 90% |
| Anvil Blade samurai cracked | +1 (whole) | 4 |
| Lodestone Shuriken | +1 arrow (whole) | 6 |
| Hammer Riposte | +1 point (whole) | 5 |
| Steel Shadow | +0.4 s | 8 s |
| Whetstone | +8% spared | 60% |
| Shrapnel | +1 shard (whole) | 5 |

Spear-Breaker, Tamahagane and the Duos are capstones, so they aren't levelled.

### What changed from the draft, and why

| Draft | Now | Why |
|---|---|---|
| **Reinforced Cover** (+1 / 2 / 3 toughness) | Cut; **Whetstone** takes its slot | It is Glacier Wall again: only numbers, and more of his own slashes stopped (§4). Whetstone turns cover into something he spends to attack. |
| Scrap falls straight; plates any gap | Drawn up to 2.5 units; plates one-way, capped at 4, none in a warlord's fight | Straight down lands in his firing lane, and a solid plate shuts it. In a warlord's fight a new `Blocker` is an unmarked beam stop. |
| A cracked samurai that dies sheds scrap | Only to his own hits | An effect kill must not trigger a further effect. |
| A hit on a cracked boss sheds scrap | Next own hit, 3 scrap, spent, 5 s cooldown | Slashing every 0.45 s would have refilled the wall without limit. |
| Lodestone's caught arrows return as shards "cracking what they hit" | Caught arrows fall as scrap; the blade cracks what it passes | A 1-health samurai dies to the shard, so the crack did nothing, and an Effect kill sheds nothing. This also puts the core identity on the Special. |
| Quenching: on an army freeze, every cover mended 1 | Frozen or encased kills shed double scrap | Frost Blade and Frozen Riposte encase but never freeze the army, so the Duo would be dead with them. Glacial Shuriken every 6 s would also have mended 12 points each time, unasked. |
| Rust: a soaked and cracked samurai's next spear turns plain | Every soak also cracks | It only touched Taisho, so it did nothing in stage 1 or against most warlords. |
| Lightning Rod: strikes the shooter, no limit | Strikes up the lane, once per 3 s (8 s on a boss) | Arrows don't record who shot them, and an unlimited trigger could have restarted the 55–66% effect kill share of §4. |
| Spear-Breaker: slashes and shards | Slashes only, one call site | See above. |
| Shrapnel, any break | Own cover only, pays once a blocker, halved on a boss | Otherwise plates and a Whetstone slash would feed it without limit. |
| Kept: Anvil Blade, Hammer Riposte, Steel Shadow, Red-Hot Iron, Tamahagane | Numbers and boss clauses settled | They hold up. |

### Balance to watch (`RunLog`)

Steel, like Water, should make few kills of its own. `EndSegment` doesn't log cover, so add `RunLog.Note` lines per
round for scrap shed, points mended, plates formed, arrows stopped by cover and spare scrap.

- **Hit-free rounds are the main risk.** Rounds should still hit him about every other round (§4). If Steel builds
  stop being hit, lower Whetstone's bound first, then `MaxPlates`.
- **Scrap against arrows.** On paper Common Anvil Blade gives about 14 mends in a 40-samurai round, and Epic about 30,
  against 20 to 80 arrows loosed. Epic is meant to keep pace, not to win. An estimate, not a measurement.
- **Standing still.** Check how long he stays under one site with Whetstone. Dodging must still matter, and spears and
  yurei arrows are what's meant to move him.
- **Effect kill share**, with Shrapnel and Lightning Rod held. It should stay well below the 55–66% that forced §4's
  caps.
- **The Ultimate's fill rate** from spare scrap, especially against Utsusemi. If Steel Shadow returns too often, lower
  spare scrap to a third of a hit.
- **Warlord damage from Shrapnel.** Benibana has only 70 health, so she is the fight to check.
- **Hearts per round** must not rise with Steel held. If they do, something it built is raising `Broken`.

### Questions for the integration pass

1. Spare scrap fills the Ultimate, which until now only his own hits filled. Is that acceptable to the Ultimate's
   design, or should spare scrap pay points, as Healing Spring does at full health?
2. Confirm Utsusemi's and Kagetsune's beam warnings stay true with mended cover, since mending adds no `Blocker` but
   does keep one standing longer.
3. Frozen Riposte calls `struck.Freeze` directly rather than `FreezeBoss`, so Quenching's boss crack needs a hook there.
4. Kagetsune's summoned souls: do cracks and the shared 5 s cooldown apply per boss or across all of them?
5. Whetstone beside Piercing Slash (`CutsArrows`), Echo Slash and Kindling's fireball, which all count as `Slash`:
   does the pierce stack sensibly?
6. Do the Tutorial's and Poguru's lines that spears can't be cut need a variant once Spear-Breaker is held?
7. Colour: `Boons.ArtColor` is already "polished steel" for Sword Arts, and bronze sits near `BeadColor` and
   Kagetsune's bronze. What is Steel's colour on cards and the HUD?
8. Lodestone in a `BulletHell` curtain: do `CappedRiposte` shots count as caught, and should they share `RiposteCap`?

## 12. Steel in the rest of the game

**Status: all 21 steps are built. The auras and crackle have no art and stay on their fallback. Not played by hand: see [Tested](#tested).**
The spec was checked line by line against the scripts on 2026-11-01. §11 stays the
authority on what Steel does; where the code forced a change it is listed under Overrides. The build list at the end
is ordered so the game compiles and plays after every step, and Steel is out of reach until step 8.

### The eight questions

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Spare scrap and the Ultimate | **Keep it: half a hit** (`step * 0.5`, 1/80 of the bar with `ultimateHits` 40, Storm Shadow's bonus included), through a new `Ronin.ChargeSpare()` with `ChargeUltimate`'s guards. | Effects shed nothing, so all scrap traces back to his own blow, parry or throw, and the rule "only his own hits fill it" holds in spirit. Points would be a dead reward against Utsusemi. A cracked warlord pays 3 scrap per 5 s, 1.5 hits against about 11 slash hits in that time, so about +14%. `UltimateActive` blocks it, so it can't feed itself. |
| 2 | Beam warnings with mended cover | **They stay true; no change.** | Beams and wall cover never meet. Utsusemi's `RazeCover` demolishes the wall before the fight, and its cover-stopped beams (Heart Lantern, Midnight, Hyper Beam, Thirteenth Hour) are phase 2 only, while a summoned Utsusemi is first-phase only, so `DrawHyperFan` only ever measures rocks and the sword, which Steel never touches (`InWall` false). Kagetsune's Spirit Hyper Beam ignores cover and its fan says so. His `SlamDown` measures `BeamEnd` at the blow and demolishes what it meets, whatever its health. |
| 3 | Quenching at Frozen Riposte | **A helper, `QuenchBoss(Boss)`, called from `FreezeBoss` and at `struck.Freeze` in `OnRiposteImpact`.** Don't reroute that line through `FreezeBoss`. | `FreezeBoss` also Chills, which would change Frozen Riposte for every build without Steel. The helper cracks the boss, and Kuon too when `WolfFight`, as `FreezeBoss` chills it. |
| 4 | Crack cooldown and Kagetsune's summons | **Crack per boss, cooldown shared**: one `nextBossCrack` in `BoonEffects`, for all bosses and Kuon. | The payout is cover, and there is one wall. Per-boss cooldowns would double the income for having a soul out. `nextBossFrost` already works this way. |
| 5 | Whetstone with Piercing, Echo, Kindling | **+1 pierce once a shot, only on a plain slash.** Piercing Slash: 2 becomes 3 after crossing. Echo's ghost and Kindling's fireball pass and wear cover but gain no pierce. | A pierced fireball would call `Impacted` twice, so two great blasts from one swing. A homing shot with extra pierce re-targets and can circle (see `TurnGrowthPerSecond`'s comment). Forks and Clone shots stop at cover, as §11 says. |
| 6 | "Spears can't be cut" lines | **No variants.** Only Oliver's Legendary line answers them (step 20). | The Tutorial runs on an empty loadout, and `SpawnSpear` and `TrainingSoldier` never get the new flag. `Poguru_First_Taisho` is a first, heard in stage 1 long before `deaths ≥ 10`. The roasts and Tips row say parry or move, which stay true. |
| 7 | Steel's colour | **`#E0A83A` on cards, HUD, souls and aura. On the field, Steel's objects are iron with bright seams (`#6F6A63`, seam `#FFE08A`), never flat gold.** | Hue 40°. CIE76 ΔE: Fire 49, Offering 22, Legendary 21, Bronze stand-in 17, `BulletHell.Bronze` 15, `BeadColor` 13, `ArtColor` 73. Fire and Art are clear. A Bead path can sit beside a Steel path, but icon and label tell them apart. Bronze means "the parry that opens a way", so a gold scrap in a Kagetsune curtain would lie; hence iron. Spear-Breaker's orange label on a gold ring is legible and is its only pairing. `ArtColor` keeps its name: it's a comment, not a clash. |
| 8 | Lodestone in `BulletHell` curtains | **Curtain shots can be caught, and don't touch `RiposteCap`.** The boon's count (2 to 6 a throw) is the cap. | `RiposteCap` stops a swarm of homing ripostes, and a caught shot makes none. Tidewake, Wildfire and Storm Step already pop curtain shots, with no cap at all. A hole round a straight-flying blade costs a 5 to 6 s cooldown. Bells stay uncaught (Override 1). |

### Overrides of §11

1. **Lodestone's skip list.** §11 skips "any shot with `Parried` set", but `Kagetsune.Skull` sets `Parried = FreeSoul`
   on every skull, so Lodestone would catch nothing from him. Skip a new `Arrow.Resonant` instead, set in
   `BulletHell.MakeResonant`, so only bells are spared.
2. **Spear-Breaker on a warlord or Kuon cracks on `KillSource.Reflected` only.** `Ronin.Deflect` keeps no record that
   a `Deflected` shot was heavy, so a parried hat or pillar can't be told apart. In practice: Iaido, cleave, moons,
   Fang, Last Toll and coffin crack; hats and pillars don't.
3. **Homing shots do touch arrows.** §11 says they slip past and never touch one, but `Arrow.OnCollisionEnter`
   destroys an ordinary arrow on any contact, whatever the shot does. So Steel shards break ordinary arrows in
   passing, as ice shards do, and Echo's ghost (its `Source` is `Slash`) breaks a yari in passing. The conclusion
   stands for shards, Clone slashes and forks. Echo is a slash, so the card stays true; leave it.
4. **"Your own cover" means `Blocker.InWall`.** Whetstone stops at Utsusemi's rocks and sword. Slashing out from a
   rock's shade would remove the Hyper Beam's whole risk.
5. **"The gap below" needs a rule.** If the spot under the scrap is taken, `TryPlate` steps outward by 0.5 up to
   `ScrapReach` 2.5. With none free the scrap is spare.
6. **Plates go at the next `Rebuild`, not on the round's last kill**, with the rest of the cover, so they stand
   through the path choice. `Rebuild` also runs before every boss (`TravelTo` → `ShowMap`), so no plate reaches a
   warlord's fight.
7. **Tamahagane's temper comes before the plate**: mend, temper, plate, spare. A plate is only better if nothing can
   be tempered.

### How it plays

**Every warlord:** no plates, scrap reaches any hurt wall cover, Shrapnel is halved (rounded up), cracks only pay in
cover.

| Boss | Cover | What Steel does | To watch |
|---|---|---|---|
| **Benibana** | Full wall at the start. Smoke rings chip it; `SmashBlockers` demolishes and pays nothing. | Steel's home fight: mending is visible and worth it. A parried Iaido cracks her with Spear-Breaker. | Shrapnel: 12 blockers × 3 shards at most is 36 damage (72 soaked) against two 70 bars, once a fight since cover is only rebuilt by the map. |
| **Utsusemi** | None after `RazeCover`: rocks and sword only, unlimited health. | Steel's weakest fight, by design. All scrap is spare; Whetstone and Shrapnel idle; Steel Shadow restores nothing but still cracks. `Crack` lands through super armour as `Chill` does; it gives no edge there. | Ultimate fill rate. If it returns too often, a third of a hit (§11). |
| **Shirogane and Kuon** | Full wall, but ghost swords, pillars and spires pass it, so it lasts. | Kuon cracks like a warlord, on the shared cooldown; a hidden Kuon can't be struck, so its crack waits. The Fang parried at Kuon cracks Kuon. Quenching reaches Kuon through `QuenchBoss`. | Wall usually full, so spare scrap is high; check that Steel Shadow isn't always up. |
| **Kagetsune** | Full wall, worn fast: cracks and slams `Demolish`; Hyper Beam, Ofuda and pendulum `Hit`. | Summons take cracks on the shared cooldown. Levy soldiers crack and shed; their Taisho yari can be broken. Lodestone catches skulls (Override 1), never bells or darts. | His beam breaking the wall raises `Broken`, so Shrapnel fires during it (halved). Fine, but look. |

| Stage (rounds) | New to Steel's play | Steel's answer |
|---|---|---|
| 1 (24 / 36 / 40) | Archer fans from round 2, Taisho from round 3 | More arrows means more for Lodestone and Lightning Rod. Round 3 is the first yari for Spear-Breaker. |
| 2 | Shieldbearers (2 HP), divers | A crack doesn't change health: a shieldbearer takes its two hits and sheds on the kill. Divers `Hit` cover and pop plates, which pays nothing. |
| 3 (55 / 60 / 72) | Yurei, whose arrows pass cover | Cover and plates don't stop them, so Steel still has to dodge, as intended. Lodestone can catch them. |
| 4 (60 / 72 / 78) | Bellringers, scouts, every kind | Bellringers crack and shed. Scouts aren't `Samurai`, so they never crack. |

### Everything else it touches

- **Sword Arts.** Piercing and Echo: question 5. Split Slash's forks are `Shadow`: they pass plates, stop at cover and
  shed nothing. Scattering Star's blades raise no `ShurikenThrown`, so only the main blade is Lodestone's. Mirror
  Stance: Hammer Riposte pays once, as `LandParry` fires `Parried` once a parry.
- **Other Ultimate boons.** One slot, so Steel Shadow replaces them. `OnUltimateStarted` returns early when Frost
  Shadow isn't held, so Steel's block must go **above** line 235.
- **Flute Songs, Oaths.** Nothing enumerates elements, so no change. Keen Eye helps Hammer, Spirit Wind helps
  Lodestone, Rain of Arrows raises Lodestone's and Lightning Rod's supply.
- **Spirit Beads, Threads, Exchange.** All generic once each boon has `LevelStep`. Anvil Blade copies Flame Blade's
  shape (`Value` whole, `Extra` chance), so an Exchange carries level cleanly. `Spear-Breaker` and the Duos stay after
  an Exchange takes their requirement away, like every capstone.
- **Duos and Infusions that count.** The old families and `ElementCount` never see Steel. New Duos take the older
  element as `Element` and Steel as `Partner`, as Water's do, so Red-Hot Iron counts toward Ember Heart, Quenching
  toward Hoarfrost, Lightning Rod toward Static Field and Rust toward Deep Current, and none toward Tamahagane.
  Lightning Rod's `Strike` sets off Plasma Arc, Thunder Rain and Superconductor as Raijin's Drum does; its 3 s limit
  is the guard.
- **HUD.** Medallions, rings and Tab tips read `Boons.Info` and `Description`: no change. `SetIcon` draws a coloured
  disc and a letter past the end of `boonIcons`. The passive column grows 52 px a boon with no limit; picks are capped
  by the run (12 to 16), not by the element count, so a fifth element doesn't worsen it. No HUD for cover, and no
  extra card text.
- **`RunLog`.** Sized by `KillSource`, not `Element`: no change to its arrays. Steel's tallies go through `Note` (step
  17).
- **Fate's Thread and Banishing Bell.** `Refill` and `banished` work on `BoonId` through `Boons.Draw`: no change.

### Where five elements break an assumption

| Place | What happens | Fix |
|---|---|---|
| `Boons.Elements[]` | `Info(Steel)` throws | Same commit as the enum (step 1) |
| `SoulOf`, `StingFor` | Default to Hibiki and Storm's sting: wrong, silently | Step 1 |
| `VoiceOf` (`GameManager.Talk`) | Unknown speaker gets `null`, the boss source | Step 1 |
| `SpeakerFor` | Unknown name gets a blank white speaker | Step 1 |
| `Ronin.SlashUpgrades`, `ShurikenUpgrades`, `RiposteUpgrades` | Steel slots would add no stance bonus | Step 1 |
| `AvailableElements` | Gates Tide by hand; Steel would be open from run 1 | Step 1 gates it on `oliverMet` |
| `PickSoulElement` | One owed soul. With both owed (Asagi's unpaid at his next death), Asagi first, then Oliver; `taken` lets a second path show the other | Step 8 |
| `BoonArtSetup.Variants` | `elementArt.arraySize = Variants.Length`, so Steel is dropped until listed | Step 18 |
| `FxSetup`, `AudioSetup`, `UiSetup.Souls`, `generate-sfx.mjs` | Lists by hand | Step 18 |
| `elementArt` in the scene | 4 entries; the enum-sized default is ignored. **Safe:** `ArtFor` returns null and every use is `?.` | Optional, step 18 |
| Soul lean | Uniform half the time, now 1 in 5, so Legendaries and Infusions come a little less often. Bias is generic | Watch `RunLog`; don't retune today |

**Safe, checked:** `elementCounts` and `RunLog.kills` size from their enums; `RoninLoadout.slots` is `BoonSlot`, not
elements; `Poguru.eyes`, `SoulPool.shimmer` and the beam's vertices are unrelated 4s; `Rival`, `Most` and F5's cycle
use the enum.

### The unlock

`OliverDue => deaths >= OliverDeaths (10) && asagiMet && !oliverMet`. Because it asks for `asagiMet` and `meetAsagi`
is read before `MeetAsagi` sets it, the two can never fall on one wake: `bool meetOliver = !meetAsagi && OliverDue;`
at the same place in `EnterSafeArea`. The `!meetAsagi` is only a guard for dev saves. `WakeTalk(meetAsagi ||
meetOliver)`.

**No new station.** The scene's saved `stations` win over `DefaultSafeAreas`, so a seventh kind would need
`Ninja/Apply Maps`. He is at the rack by story anyway: `ShowRack` gets a TALK row when `oliverMet`, and step 19's
figure stands by the rack without being a station.

### Tested

**As of 2026-11-01; this is everything these builds ran.** Steps 2 to 17: effects were mostly fired by direct calls in
god mode in the Editor. There was no real input and no visual check, so no key or button was pressed and no plate, tint
or scrap was looked at.

Step 20, by script and in the Editor, with the save backed up and restored:

- **Text.** A script compared all 94 conversations (221 lines) in `Dialogue.Lines.cs` with the Story tables and with
  `LINES` in `generate-voice.mjs`: ids, speakers, voices and text agree. The clip for `Oliver_Dawn` has no conversation,
  since the freed beats aren't built.
- **Clips.** `Ninja/Apply Voice Lines` made 1051 clips. 90 of the 91 new ones match a written line. 131 of Oliver's
  lines have no clip (6 others in the game don't either, 137 in the log).
- **The meeting.** A wake on a test save (`EnterSafeArea`, `deaths` 12, `asagiMet`) played `Oliver_Meet_Rack` with all six
  clips: Oni's three on `oniVoice`, Oliver's three on `soulVoice` with its chorus and echo filters enabled and a signal
  on the source. That confirms the routing. Nobody listened to it, and the web build doesn't run those filters.
- **Silent lines.** `Oliver_Greet_Draft`, `Oliver_Rack_Mine` and `Oliver_Hub_Name` (through `PlayTalk`, advanced by a
  script), and the "..." inside `Duo_RedHotIron_First`, played as subtitles with no error.
- **Picking.** Direct calls to `GameManager.SoulTalk` and `Dialogue.Pick` chose the expected id for the next chain
  chapter, Legendary and its repeat, Infusion, all four Duos' first talks, `Felled`, `Ahead`, `SoulHub` and a reactive
  line. `Dialogue.Explain` showed the Scabbard and Seventh greetings gated and the others' `Jealous_Steel` eligible.
- **Found by that run:** the static fonts have no `ū`, so "Jūsuke" shows as "J□suke" in `Oliver_Meet_Enormous` and
  `Oliver_Meet_Cold`. Step 18 fixed it; see below.

Steps 18 and 19, on 2026-11-01, in the Editor, driven by script with the three saves backed up and restored byte for
byte. Screenshots were taken and looked at; no key was pressed:

- **Menus.** Apply Boon Art, VFX, Audio, UI Fonts and Maps each changed only what they should: the new `Ronin_Steel`
  frames, Steel's scene entries, the `SoulStingSteel` entry. No other character's asset was rewritten.
- **Fonts.** Zen Kaku Gothic has no `ū` at all, so a re-bake could never fix the body text. Deleting the baked fonts was
  blocked by the tool's safety check, so `ū` was added to `Cinzel SDF` in place, and `UiSetup` makes Cinzel the body
  font's fallback. "Jūsuke" now draws in the scene, but as a thin serif capital among bold gothic letters.
- **Temple.** He stands at the top of the dais behind the rack (moved there from a tight corner at the user's request) at the size wanted, out of the rack's use spot. Oni
  sorts over him in front and under him behind. He's absent before the meeting; on a replayed wake he went Loaf, Talk,
  Strike, Idle (the Strike was seen as one frame), and `oliverMet` was set.
- **Talk.** `Oliver_Meet_Cold`: the close-up is framed well and `soulVoice` was playing the right clips. Nobody listened.
- **Boon screen.** All twelve cards were seen: each icon matches its boon, and all 57 `boonIcons` names match `BoonId`.
  The soul painting and `Oni_Boon_Steel` are right. Long Duo headers wrap, and "PASSIVE" touches Quenching's text.
- **Round (stage 1, round 1).** Scrap fell (`ShedScrap` by direct call), formed plates in the gaps, and stopped at 4. Half
  the army was cracked by direct call: the tint and flicker show, but subtly. The scrap is a pale soft disc that's hard to
  see on this ground. The field soul, its arrow, its pickup scene, the boon choice and the HUD's gold rings all worked.

**Not tested:** Kagetsune and his summons with Steel; a real parried Iaido; real Utsusemi rocks; the real F5 key; a real
Taisho yari at stage 1 round 3; stage 2 to 4 rounds; balance, including every number in §11 and the Ultimate fill rate; how
a thrown Steel shuriken; the Ultimate's aura and crackle on their fallback; how the sting and voice sound; plates or tints
on any other map; a build, web or otherwise; a whole run with Steel. Docs that describe a Steel
effect describe what the code is written to do, not something seen in play.

### Build list

Each step is one commit. Nothing touches `.unity` or `.prefab` on disk, and only step 18 and the art half of step 19
need Unity to run a menu; all others work with the placeholder fallback. Test with F5 (soul) and the dev jumps; check
the console after each. **Enums are appended to, never reordered.**

**1. Foundation.** `Boon.cs`: append `Steel` to `Element`; append to `BoonId` in this order `AnvilBlade,
LodestoneShuriken, HammerRiposte, SteelShadow, Whetstone, Shrapnel, SpearBreaker, Tamahagane, RedHotIron, Quenching,
LightningRod, Rust`; add `Elements[4]`: `new ElementInfo("Steel", "the smith's cat of the Emperor's forge lends you
his master's hammer.", new Color32(0xE0, 0xA8, 0x3A, 255))`; add `static readonly BoonId[] SteelBoons` (the first
four). Definitions wait for their steps, so nothing unbuilt is ever offered. `SaveData.cs`: `oliverMet`,
`oliverSoulOwed` after Asagi's (JsonUtility fills old saves with false). `GameManager.Rewards.cs`: `AvailableElements`
also skips `Steel` unless `oliverMet`; `SoulOf` `"Oliver"`; `StingFor` `Sfx.SoulStingSteel`, with the const added to
`Sfx.cs` (`Sting` already checks `Sfx.Has`). `GameManager.Talk.cs`: `VoiceOf` adds `"Oliver"` to `soulVoice`;
`SpeakerFor` copies Asagi's case: `SoulSpeaker(name, "The Smith's Cat", Element.Steel, oliverCloseup, new
Vector2(0.5f, 0.3f))`; `GameManager.cs`: `[SerializeField] Sprite oliverCloseup` (null is fine). `Ronin.cs`: add the
three slot boons to its upgrade arrays. *Test:* compiles; a normal run never shows Steel; F5's fifth press gives a
Steel soul with a Spirit Offering (nothing defined yet).

**2. Cracked.** `Statuses.cs`: `CrackSeconds` 4, `BossCrackCooldown` 5, `BossCrackScrap` 3, `CrackColor` `#6F6A63`,
`SeamColor` `#FFE08A`, `CrackScale` 1 and `BossCrackScrapNow` set in `From` (step 12 fills them). `Samurai.cs`: copy
`Jolt`: `Cracked`, `crackedUntil`, `Crack(seconds)`; bit 16 in `StatusMask`; in `SetFrost`, `clan = Lerp(clan,
CrackColor, 0.5)` with a seam flicker as Jolted has. `Boss.cs`: copy `Jolt` (same `IsDead || PhaseBreaking || Down`
guards), add `SpendCrack()`; tint in `FixedUpdate`. `SpiritWolf.cs`: copy its `Jolt` (`Armed && !IsBroken`) and
`SpendCrack`; tint near line 586. *Test:* call `Crack` through the console; the tint shows and fades.

**3. Cover can be mended.** `Blocker.cs`: `baseHealth` in `Init`; `InWall`, `Plate`, `Forged`; `Hurt => !destroyed &&
health < maxHealth`; `Mend(int points)`, never above `maxHealth`, which calls `UpdateSprite`; `Temper(int points, int
bound)`, raising `maxHealth` with `health`, up to `baseHealth + bound`; `Hit` raises `Broken` only if `!Forged`. Then
this in `OnCollisionEnter`, replacing the Spinning line:

```csharp
Projectile shot = coll.collider.GetComponentInParent<Projectile>();
// The projectile decides what a pass does (Projectile.OnCollisionEnter), so this doesn't hang on who's called first.
if (shot != null && (shot.Spinning || shot.PassesCover || Plate || (shot.Whets && InWall))) return;
```

`Projectile.cs`: add `PassesCover` and `Whets` (false for now, so nothing changes yet). First thing after `spent` in
`OnCollisionEnter`: `Blocker met = coll.collider.GetComponentInParent<Blocker>(); if (met != null && (met.Plate ||
PassesCover)) { KeepFlying(coll.collider); return; }`. `BlockerWall.cs`: `CreateBlocker` returns its `Blocker`, sets
`InWall`, and records a `Slot { type, look, centre, blocker }` in a list both `Rebuild` and `Clear` empty; add
`NearestHurt(x, reach)`, `NearestUntempered(x, reach, bound)`, `PlateCount`, `TryPlate(x, reach, sprite)` (Override 5;
copy `CreateBlocker`, `Init(1, art, new[] { sprite }, null)`, `Debris.BurstSprites` is null-safe; `Plate = Forged =
true`; 1.1 × 0.8 units; `ProceduralSprites.White` tinted iron) and `Restore(bool raise)` (standing to full; if
`raise`, rebuild each null slot `Forged`, removing plates it lands on). Skip children that aren't `Blocker`s. *Test:*
a normal run plays exactly as before.

**4. Scrap and Anvil Blade (the core loop).** New `Boons/Scrap.cs`, a code-built mover, no prefab: an iron
`ProceduralSprites.Disc` with a small `Projectile.CreateGlow` seam, falling at 9 u/s from where it was shed to
`wall.TopY` while drawing to a target x, on `Time.deltaTime`, parented to the wall so `Rebuild` clears it; calls back
on landing. `BoonEffects.cs`: add `partial`, take `BlockerWall wall` in the constructor (one call site,
`GameManager.ResetLoadout`). New `Boons/BoonEffects.Steel.cs`, holding from here on all of Steel: `const ScrapReach =
2.5f`, `MaxPlates = 4`, `MaxScrapLive = 12` (past it a scrap lands at once); `nextBossCrack`; `ShedScrap(from, count =
1, points = 1)` (spread ±0.5); and `Settle`, resolved at landing so a target gone by then isn't a bug:

```csharp
Blocker hurt = wall.NearestHurt(x, BossFight ? float.MaxValue : ScrapReach);
if (hurt != null) { hurt.Mend(points); return; }
if (tempered) { /* step 12 */ }
if (!BossFight && army.Remaining > 0 && wall.PlateCount < MaxPlates && wall.TryPlate(x, ScrapReach, plateSprite)) return;
ronin.ChargeSpare();
```

Also `Ronin.ChargeSpare()` (split `ChargeUltimate`'s step out; question 1). Hooks: `OnKill`, `case Slash`: the Anvil's
roll on `Extra`: `ShedScrap(position)`, crack `(int)value` nearest in `army.SamuraiInRank(samurai)`. `OnKill` after
the `Procs` guard: `if (samurai.Cracked) ShedScrap(position)`. `OnBossHit` and `OnWolfHit`: **before** the `switch`,
`if (Damage.Procs(source) && boss.Cracked && Time.time >= nextBossCrack) { ...SpendCrack(); ShedScrap(point,
Statuses.BossCrackScrapNow); }`, so a crack applied by this same blow pays on the next, then `case Slash`: the roll ×
`BossRiptideShare` cracks. Define **Anvil Blade** from §11, copying Flame Blade's fields. *Test:* F5 to Steel, dev
jump to S1; a kill may shed scrap, hurt cover mends, a plate forms and slashes pass it, arrows pop it, no heart.

**5. Lodestone Shuriken.** `Arrow.cs`: `public bool Resonant`, set in `BulletHell.MakeResonant`. New
`Boons/ShurikenLodestone.cs`, a copy of `ShurikenWake`: cracks in `WakeRadius` once each (bosses and Kuon at the same
×4 test); catches the nearest shots in 2 units until its count runs out, skipping `Unbreakable`, `Reflectable`,
`Unparryable`, `Resonant`; `Pop` in `CrackColor`, `ShedScrap` from the arrow. Hook in `OnShurikenThrown` beside
Tidewake, **above** the `BossFight` return at line 345. Define the boon. *Test:* round 2's fans; Benibana's Bloom; a
bell passes.

**6. Hammer Riposte.** `BoonEffects.OnParried`, **above** the `if (BossFight)` return: `RingHammer((int)value)`. Boss
fight: crack `Bosses` and `FightingWolf`, mend `NearestHurt` anywhere. Round: `NearestHurt` within 3; none, `TryPlate`
at his x. `Fx.Impact` in the element's colour. `Ronin.Deflect`'s fallback branch already colours the riposte. Define
the boon. *Test:* parries in round 1 and on Benibana.

**7. Steel Shadow.** `OnUltimateStarted`, **above** line 235's early return: `wall.Restore(raise: !BossFight)`, crack
`Bosses` and `FightingWolf`, only in a boss fight or with `army.Remaining > 0`. `OnCloneKill`: crack
`SamuraiWithin(position, IceRadius)`, skipping the victim. `OnBossHit` and `OnWolfHit`, `case Clone` after the
`CloneBossShare` break: crack. Define the boon. All four slots now work. *Test:* F9; fallen cover returns; breaking it
pays nothing.

**8. The unlock (Steel reaches players here).** `GameManager.Hub.cs`: copy `AsagiDue`, `MeetAsagi` to `OliverDue`,
`MeetOliver` (sets `oliverMet`, `oliverSoulOwed`, `AddFirst(Firsts.Oliver, Save.Run)`, plays `Oliver_Meet_Rack`, no
figure call yet); `EnterSafeArea`, as under The unlock. `Rewards.cs`: `PickSoulElement`, after Asagi's line: `if
(Save.Data.oliverSoulOwed && (taken == null || !taken.Contains(Element.Steel))) return Element.Steel;`. `OfferBoons`:
clear it, copying Tide's. `Dialogue.cs`: `Firsts.Oliver = "Met_Oliver"`; append `OliverTalk` to `DialogueEvent`.
`SaveData.Backfill`: `if (d.oliverMet) d.AddFirst(Firsts.Oliver, 0);`. `Dialogue.Lines.cs`: `const string Oli`,
`Ol(text)`, then `Oliver_Meet_Rack` from `Story/Oliver.md`'s sample, `When(Seen("Asagi_Meet_Pool"),
NotSeen("Oliver_Meet_Rack"))`, a `SoulPickup` Repeat from the same table, a `SoulHub` Repeat. They run silent until
voiced. `ShowRack`: a TALK row before BACK when `oliverMet`, copying `TalkToAsagi` with `DialogueEvent.OliverTalk`.
`GameManager.Dev.cs`: F5's Steel branch, copying Tide's, and its comment. *Test:* save at `deaths` 10 with `asagiMet`;
next wake plays his scene; first soul is Steel. A save with neither gets Asagi only.

**Past this point every step can be cut alone.**

**9. Whetstone.** `Ronin.LaunchSlash`: `projectile.Whets = source == KillSource.Slash && has`, with `WhetSpared` the
value; the same in `ThrowFireball` when `!clone`. `Blocker.Wear(point, spared)`: `Random.value < spared` skips `Hit`.
`Projectile.OnCollisionEnter`, after step 3's line: `if (met != null && Whets && met.InWall) { met.Wear(...); if
(!whetted && !Kindled && pickTarget == null) { whetted = true; pierceLeft++; } KeepFlying(coll.collider); return; }`.
Define the boon. *Test:* slash from under a site, with Piercing, Echo and Kindling; a fireball blasts once.

**10. Shrapnel.** `Ronin.LaunchSteelShard(from, picker)`, a copy of `LaunchIceShard`: no `FreezeOnHit`, `PassesCover =
true`, tinted iron. `GameManager.OnBlockerBroken`: split the guard so the heart's roll comes after
`effects.OnCoverBroken(position)` for `state == Playing && !tutorial.Active`. There: count, halved and rounded up if
`BossFight`, picker `ronin.RiposteTarget`. Define the boon. *Test:* a broken blocker bursts; a shard crosses cover
unchipped; a plate bursts nothing.

**11. Spear-Breaker.** `Arrow.cs`: `CursedSpear`, set only in `Samurai.Fire`'s Taisho branch; a static `SpearBroken`;
in `OnCollisionEnter` above the `Unbreakable` branch, `else if (CursedSpear && Statuses.SpearBreaker &&
coll.collider.GetComponentInParent<Projectile>() is { Source: KillSource.Slash }) {
SpearBroken?.Invoke(transform.position); Shatter(); return; }`. `Statuses.From` sets the flag.
`GameManager.OnEnable`/`OnDisable` subscribe beside `Blocker.Broken` (domain reload is off, so both are needed).
`OnBossHit`, `OnWolfHit`: `source == Reflected` cracks (Override 2). Define the boon with `TwoSlotsOf`. *Test:* round
3 yari; a parried Iaido; `Tut_Spear` unaffected.

**12. Tamahagane.** `Statuses.From`: `CrackScale` 2, `BossCrackScrapNow` 5; `Crack` already multiplies. `Settle`'s
temper line: `wall.NearestUntempered(x, ScrapReach, 2)`, `Temper(points, 2)`. Define the Infusion.

**13. Rust.** `Statuses.Rust`; one line at the end of `Samurai.Soak`, `Boss.Soak` and `SpiritWolf.Soak`: `if
(Statuses.Rust) Crack(Statuses.CrackSeconds);`. Define the Duo: `Element.Tide`, `Partner` Steel, `HoldsAny(TideBoons)`
and `HoldsAny(SteelBoons)`.

**14. Red-Hot Iron.** `Scrap`: `hot` flag; every third frame, up to 2 arrows within 1 unit, Storm Step's skips,
`Burn(Fire)`, with `Physics.OverlapSphereNonAlloc` as `BulletHell.Resonance` does rather than `FindObjectsByType`,
since many scrap may fall at once. Tint it ember. Define the Duo (`Fire`).

**15. Quenching.** `OnKill`, after the `Procs` guard: `samurai.Encased || army.Frozen` sheds scrap with `points` 2,
cracked or not, in place of the plain shed (`Kill` leaves `encasedUntil` set, so this reads true for a statue too).
`QuenchBoss` from question 3, gated on the boon. Define the Duo (`Water`).

**16. Lightning Rod.** `Blocker.OnCollisionEnter`: a static `Chipped(Vector3)` when an `Arrow` strikes `InWall &&
!Plate`, after the `PassesCover` return. `GameManager` subscribes as in step 11; `effects.OnCoverChipped`: 3 s limit
in a round, `Strike` on `army.FrontSamuraiInColumn(army.ClosestColumnTo(x))`; 8 s on a boss, `Strike` the boss. Define
the Duo (`Lightning`). *Watch* effect share.

**17. `RunLog`.** `BoonEffects.Steel`: tallies for scrap shed, points mended, plates formed, spare scrap, and arrows
stopped (`Chipped`, so subscribe regardless of the Duo); `LogSteel()` writes one `RunLog.Note` when any is above 0,
and clears. Call it above the `EndSegment` lines in `EndRound` and `GameManager.Bosses.cs:568`, `Stages.cs:162`.

**18. Art and sound, through Unity only.** Code first: `BoonArtSetup.Variants` adds `(Element.Steel, "Steel", 40f /
360f, 1.05f)`; `FxSetup` adds `Fx.Aura(Element.Steel)`, `Fx.SoulCrackle(Element.Steel)`; `AudioSetup` adds
`SoulStingSteel`, `generate-sfx.mjs` its prompt; `UiSetup.Souls` adds `"Oliver"`, which needs the `oliverCloseup`
field from step 1. Then in the Editor: `Ninja/Apply Boon Art` (it resizes the scene's `ElementArt[]` to 5 and rewrites
`boonIcons`; a missing icon stays a disc), `Apply VFX`, `Apply Audio`, `Apply UI Fonts`. Each menu tolerates missing
files, so run them with whatever art exists. Art from `Story/Oliver.md`: `UI/Boons/<BoonId>`,
`Effects/Soul_Steel_<i>`, `Projectiles/ShurikenArt_Steel`, `Portraits/Oni_Boon_Steel`, `Soul_Steel_Painted`,
`Oliver_Closeup`. Expect recoloured assets to appear; review them before committing.

**19. Oliver at the rack.** New `Hub/Oliver.cs`, a copy of `Asagi.cs` with clips `Idle, Talk, Loaf` and a code-built
placeholder, in forge-gold and grey. `SafeArea.Config.oliverArt`; `SafeArea.Build`: `OliverFigure` at the rack's
`propAt` when `Save.Data.oliverMet`, sorted with `standing`. `MeetOliver` and the TALK row play its clips.
`NinjaArtSetup.SetUpSafeAreas`: copy Asagi's clip loop, then `Ninja/Apply Maps`, only when frames exist.

**20. His lines.** `Dialogue.Lines.cs`, the rest of Oliver's first chain and `Oliver_Legendary_SpearBreaker`,
`OffersBoon(BoonId.SpearBreaker)`, a proposal in his voice: "Cursed yari. Not his. He'd have got the balance right.
...Hit it where it rings thin, Scabbard." `Duo_*` lines for the four Duos, copying `Duo_ScaldingSteam_First`. Read
`Story/Oliver.md` first; voices come from the connector's `creative_design_voice` with the user picking a preview, so
plan that with the user.

**21. Docs.** `ARCHITECTURE.md` (Boons row: five statuses, `Scrap`, `ShurikenLodestone`; Hub: `Oliver`),
`Docs/Boons.md`, `Docs/World.md` (plates), `Docs/Hub.md`, `Docs/Player.md`, `README.md`, `Story/Oliver.md` and
`Story/Souls.md`. `Dev.cs`'s and `CLAUDE.md`'s F5 lists should say Fire → Frost → Storm → Water → Steel; `CLAUDE.md`
is the user's, so raise it with them. Add a "Tested" paragraph here. Do a short version of this step first if time is
about to run out, since the write-up is what's graded.

*Step 20 as built:* his Legendary uses `Story/Oliver.md`'s voiced line, not the proposal above, and his voice was designed
and picked before this step. *Step 21 as built:* `Hub: Oliver` waits on step 19.

**Riskiest:** steps 3 and 9, since they sit in the collision path of the game's main attack. Both `OnCollisionEnter`s
fire for one contact and neither order is promised, which is why `Blocker` returns and `Projectile` does the wearing.
Play a full round with no Steel after step 3.

**If time runs out:** 1 to 8 are a complete fifth element. Cut in this order: 19, 20, 16, 14, 15, 13, 12, 17, 11, 10,
9. Without a boon's step its definition doesn't exist, so it's simply never offered. Step 18 can stay at a code-only
colour with no menu run; step 21's short version can't be cut.
