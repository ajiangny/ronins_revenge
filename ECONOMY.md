# Economy: Soul Embers and the Flute Songs

How strong Oni starts, what he can buy between runs, and the research behind the numbers. The code is in
`Upgrades.cs` (see [ARCHITECTURE.md](ARCHITECTURE.md)).

## The decision

A fresh save plays exactly like the game did before the shop: 3 lives, a 0.35 s swing, a 0.25 s parry window, a 6 s
shuriken. The base game is tuned "harder but beatable" and someone may only play it once or twice, so a skilled
player must be able to win run 1. The shop only adds on top, and keeps to the size Hades and Vampire Survivors land
on when maxed: about twice the survivability, a little speed, better luck, and no flat damage bonus (damage growth
belongs to the boons).

## Earning

Soul Embers are kept on death: 1 per samurai, 5 per scout, 25 / 40 / 60 / 100 per boss felled. A death in stage 1
brings in roughly 80–120, a full clear about 900, so the first death buys something and maxing the shop takes about
five clears. Embers go into the save the moment they're earned, so quitting mid-run keeps them.

## Spending

Price of the next rank = base × (rank + 1) + ⌊10 × 1.1^ranks bought in total⌋ (the Vampire Survivors formula at half
its fee): about 4,600 embers buys every song. Refund returns everything spent, and a maxed song can be switched
off for a harder run. The shop appears on the title menu once there are embers to spend.

| Song | Fresh → max | Ranks | Base |
|---|---|---|---|
| Keen Eye (parry window) | 0.25 → 0.31 s | 2 | 60 |
| Swift Feet (move speed) | 10 → 11 | 2 | 60 |
| Quick Draw (swing cooldown) | 0.35 → 0.315 s | 2 | 80 |
| Spirit Wind (shuriken cooldown) | 6 → 5 s | 2 | 80 |
| Steady Breath (missed parry) | 2 → 1.5 s | 2 | 80 |
| Long Shadow (invulnerability after a hit) | 2 → 2.5 s | 1 | 120 |
| Soul Pull (soul drop chance, sure soul after) | 7% → 9%, 15 → 13 kills | 2 | 100 |
| Iron Will (max lives) | 3 → 4 | 1 | 600 |
| Fate's Thread (one boon redraw a run, R on the boon screen) | 0 → 1 | 1 | 1,000 |
| Second Dawn (once a run, rise again with 2 lives) | 0 → 1 | 1 | 1,200 |

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
