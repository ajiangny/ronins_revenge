# Economy: Spirit Notes and the Flute Songs

How strong Oni starts, what he can buy between runs, and the research behind the numbers. The code is in
`Upgrades.cs` (see [ARCHITECTURE.md](../ARCHITECTURE.md)).

## The decision

The first shop only added on top of the tuned game (3 lives, speed 10, a full-speed slash), so the songs were too
small to chase. **Since 2026-10-29 a fresh save starts weaker** and the songs buy it back: **2 lives** (Iron Will
raises it to 4), **speed 9.5** (Swift Feet: 11 at its third rank, the old top), a **slash at 85% speed** (Cutting Wind:
100% at its third rank; the slower wave lives longer, so it still reaches the top) and the old 1.1 parry reach (Wide
Guard: 1.3). A skilled player can still win run 1; most will die once or twice and come back stronger, as in Hades,
where Thick Skin and Death Defiance are the first things bought. Damage growth still belongs to the boons.

The top ranks of the stat songs also ask for a **Warlord's Seal** or **Kite Silk** (below), like the Mirror's later rows
asking for Chthonic Keys: notes alone buy the early ranks, and the last steps come from beating warlords and hunting
scouts.

## Earning

Spirit Notes, the notes of the songs Shirogane taught Oni that each freed soul leaves behind, are kept on death: 1 per
samurai, 5 per scout, 25 / 40 / 60 / 100 per boss felled, plus Lost Melody paths (30 × the stage), all raised by
Heat. A death in stage 1
brings in roughly 80–120, a full clear about 900, so the first death buys something and maxing the shop takes about
five clears. Notes go into the save the moment they're earned, so quitting mid-run keeps them.

*Nov 2026:* each round's Elite (`LevelConfig.eliteHealth`) always lets a soul slip free, one per round (about 12 extra
souls over a four-stage run), even when it is the round's last kill and the normal drop would have given nothing. Only
one soul can be on the field at a time, so if one is already falling the Elite pays 5 notes (`Upgrades.PerScout`)
instead. Poguru's practice armies have no Elite.

## Warlord's Seals and Kite Silk

Two rarer things, kept in the save as they're found (`SaveData.relics`, `Relic`), shown in the temple's resource tray
and on the end screen:

- **Warlord's Seal** (Hades' Chthonic Keys and Titan Blood): 1 for every warlord felled, and 1 more the first time
  each one falls (`Upgrades.SealsPerBoss`, `SealsFirstFall`). A first full clear brings 8.
- **Kite Silk** (Hades II's gathered reagents, like the Moly that opens Night's Craftwork): a downed kite scout leaves
  one 35% of the time (`GameManager.silkChanceFromScout`), so a few runs gather what a rank needs.

| Rank | Asks for |
|---|---|
| Keen Eye 2 | 2 Kite Silk |
| Swift Feet 3 | 3 Kite Silk |
| Cutting Wind 3 | 3 Kite Silk |
| Wide Guard 2 | 1 Warlord's Seal |
| Iron Will 2 | 2 Warlord's Seals |
| Second Dawn | 2 Warlord's Seals |

They're counted as they're spent (`relicsSpent`), so Refund All gives back exactly what was paid.

## Spending

Price of the next rank = base × (rank + 1) + ⌊10 × 1.1^ranks bought in total⌋ (the Vampire Survivors formula at half
its fee). Refund returns everything spent, and a maxed song can be switched
off for a harder run. The shop is the flute altar in the temple (`GameManager.ShowSongs`).

| Song | Fresh → max | Ranks | Base |
|---|---|---|---|
| Keen Eye (parry window) | 0.25 → 0.31 s | 2 | 60 |
| Swift Feet (move speed) | 9.5 → 11 | 3 | 60 |
| Cutting Wind (slash speed) | 85% → 100% | 3 | 70 |
| Wide Guard (parry reach) | 1.1 → 1.3 | 2 | 90 |
| Quick Draw (swing cooldown) | 0.45 → 0.405 s | 2 | 80 |
| Spirit Wind (shuriken cooldown) | 6 → 5 s | 2 | 80 |
| Steady Breath (missed parry) | 2 → 1.5 s | 2 | 80 |
| Long Shadow (invulnerability after a hit) | 2 → 2.5 s | 1 | 120 |
| Iron Will (max lives) | 2 → 4 | 2 | 450 |
| Fate's Thread (boon redraws a run, R on the boon screen) | 0 → 3 | 3 | 300 |
| Second Dawn (once a run, rise again with 2 lives) | 0 → 1 | 1 | 1,200 |
| Third Path (paths after each round) | 2 → 3 | 1 | 400 |
| Banishing Bell (take a boon out of the run, B on the boon screen) | 0 → 2 a run | 2 | 350 |
| Pond's Blessing (Spirit Beads after the first boss) | 1 → 2 | 1 | 500 |
| Ancestral Lore (each stage's first soul) | any rarity → Rare or better | 1 | 600 |

### Control, not power (the boon revamp)

With souls dropping from kills a run saw about 65 boon offers, so every slot was Epic by stage 2 and the songs
(deliberately tiny) didn't matter. Souls now come from paths between rounds (about 12–16 boons a run), and held
boons are raised only by Spirit Beads. The newer songs follow Hades' later Mirror rows and Hades II's Arcana: they
give **more say over the rewards** (another path, redraws, banishes, an extra bead, a Rare floor) rather than more
damage, so notes stay worth earning without a fresh save being any weaker. **Soul Pull** is retired (its ranks are
refunded, `Upgrades.RefundRetired`).

## Oaths and Heat (after the first dawn)

The counterweight for strong players, Hades' Pact of Punishment: an oath stone in the temple, sealed until
Kagetsune falls. Each oath rank is a point of Heat; notes earned are raised 10% a point, and the first dawn at a
new highest Heat pays 150 notes per point gained (`Oaths.cs`).

| Oath | Ranks | Per rank |
|---|---|---|
| Hastened March | 2 | armies +10% speed |
| Rain of Arrows | 2 | arrows 10% more often, +5% speed |
| Iron Generals | 2 | warlords +15% health |
| The Long Road | 2 | the boss path opens a round later |
| Faded Spirits | 2 | no Spirit Beads behind paths; then no Rare floor on the warlord's boon |
| Frail Body | 1 | one life fewer |

## Research

**Hades, Mirror of Night** ([gemelog](https://gemelog.jp/en/games/hades/mirror/),
[Fextralife](https://hades.wiki.fextralife.com/Mirror_of_Night)):

| | Fresh | Maxed | Darkness |
|---|---|---|---|
| Max HP (Thick Skin) | 50 | 100 | 10 ranks, 40→85 each, 625 |
| Death Defiance (revive at 50%) | 0 | 3 | 30 / 500 / 1,000 |
| Dashes (Greater Reflex) | 1 | 2 | 50 |
| Cast ammo (Infernal Soul) | 1 | 3 | 20 / 80 |
| Damage | | only conditional (+50% from behind, or +75% on undamaged foes) | 100 a row |
| Boon luck and rerolls (last four rows) | | | about 59% of the 35,365 total |

The first four rows are open from the start and cost about 1,750: the big survival jump is cheap and early. Later
rows are gated by Chthonic Keys (5/10/20/30). A normal room pays about 10 Darkness. About 46.7% of Steam players have
ever escaped ([Steam stats](https://steamcommunity.com/stats/1145360/achievements)); players report 15–50 runs for a
first clear. God Mode starts at 20% damage resistance and adds 2% per death up to 80%
([Inverse](https://www.inverse.com/gaming/hades-god-mode-interview)). Kasavin: "Our big focus from the start with
Hades was how to take the sting of failure and reduce that as much as possible" (same source).

**Hades' gated resources** ([PC Invasion](https://www.pcinvasion.com/hades-items-guide-nectars-keys-ambrosia-titan-blood-and-more/2),
[Prima](https://primagames.com/gaming/hades-guide-mirror-of-night-upgrades-fated-list-of-prophecies)): Chthonic Keys open
the Mirror's later rows and the other weapons (60 keys open the whole Mirror), and Titan Blood, Diamonds and Ambrosia
come mostly from bosses, the first time each is beaten at a Heat. Hades II's Incantations at the Cauldron cost
reagents gathered in runs (Silver, Moly and the like) as well as Bones and Ash
([Shacknews](https://www.shacknews.com/article/146097/what-does-the-cauldron-do-hades-2),
[Maxroll](https://maxroll.gg/hades-2/guides/hades-2-materials-guide)). Both make the early upgrades cheap in the common
currency and put the late ones behind something only progress gives.

**Vampire Survivors, PowerUps** ([wiki](https://vampire.survivors.wiki/w/PowerUps)): price = base × rank plus a
global fee ⌊20 × 1.1^ranks bought⌋. First ranks cost 100–300 gold, the big ones 5,000 (Amount) and 10,000
(Revival). Maxed core stats are +25% Might, −5% Cooldown, +33% HP, +3 Armor, +1 Amount and one Revival: about 1.3×
damage and 1.33× HP, small next to a run (the Whip alone goes from 10 to 40 damage). The core set costs about 66,000
gold. A chest pays 60–500, so the first death nearly always buys something
([wiki](https://vampire.survivors.wiki/w/Gold_Coin_(currency))). There is a refund button, and a maxed PowerUp can be
toggled off.

**Design consensus.** Meta upgrades should make a death feel worthwhile without replacing skill. Too much stat
progression makes the difficulty curve run backwards (hardest first, easier with repetition); keep vertical bumps
small and lean on new options (Slay the Spire, Isaac keep stats fixed and unlock content). Rubber-banding like God
Mode and a challenge counterweight like Heat or Curse cover both ends. No designer we found gives a numeric ratio of
meta power to in-run power.
