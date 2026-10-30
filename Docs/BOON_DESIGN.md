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

