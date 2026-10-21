# Shadow Blade: Ronin vs. Samurai — Assignment 1

A Space Invaders remake re-themed as a lone chibi ronin holding a moonlit temple courtyard against marching samurai armies.

Built on the class template ([Oren-Steinberg/Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D)), Unity 6000.6, URP.
Open `Assets/Scenes/SampleScene.unity` and press Play.

**Controls:** Left/Right or A/D to move, Space to throw kunai, E or Up for the Spirit Shuriken, Shift or Down to parry, Enter to start or restart. On the boon screen: Up/Down (or W/S, or 1/2/3) to select, Enter or click to choose.

The template's Space Invaders classes, prefabs and layers are renamed to the theme: `Alien` → `Samurai`, `AlienFleet` → `SamuraiArmy`, `AlienBullet` → `Arrow`, `AlienCounter` → `SamuraiCounter`, `Spaceship` → `Ronin`, `Dart` → `Kunai`, `ShieldWall`/`ShieldBlock` → `BlockerWall`/`Blocker`. The section on template code issues below uses the template's original names.

## Required features

| Requirement | Where |
|---|---|
| Remaining aliens speed up as others are destroyed | `SamuraiArmy.CurrentSpeed()` scales linearly from `baseSpeed` to `baseSpeed × maxSpeedMultiplier` as the army shrinks. |
| Detect when the game can no longer be won | `SamuraiArmy.FixedUpdate()` raises `ArmyReachedRonin` once the lowest samurai reaches the top of the ronin (`Ronin.TopY`). At that height the samurai can't be hit, so `GameManager` ends the game. |
| Ship can't move offscreen | `Ronin.FixedUpdate()` clamps the ronin to the camera's visible width, using his sprite's measured bounds (the pivot isn't centred on the art). |
| Balance (harder but beatable) | See [Balancing](#balancing). |

## Code issues found in the template and fixed

1. **Dynamic Rigidbodies were moved by writing `transform.position` every physics step.** This fights the physics engine and teleports colliders. Aliens and the ship are now kinematic and move with `Rigidbody.MovePosition` (interpolated). Darts and alien bullets are launched once with `linearVelocity`.
2. **Projectiles destroyed whatever they hit (`Destroy(coll.gameObject)`).** An alien bullet would delete the ship, a shield, or anything else outright, which made lives and shields impossible to add. Now each object handles its own collision: `Alien` dies, `Spaceship` loses a life, `ShieldBlock` takes damage, and the projectile only destroys itself. `Dart.TryConsume()` keeps one dart from killing two aliens in the same step.
3. **The UI was polled and rebuilt every frame.** `AlienFleet.Update` copied the count into `AlienCounter`, and `AlienCounter.Update` rebuilt the string every frame, allocating garbage. The count is now pushed only when it changes (`AlienCounter.SetCount`, using TMP's non-allocating `SetText`).
4. **Every alien rolled a random number to fire on every `FixedUpdate`.** That's N random checks per step, back-row aliens shooting through their own fleet, and a `static` cooldown that every alien shared. The fleet now fires one shot on a timer from the bottom alien of a column, sometimes aimed at the player's column.
5. **The fleet's direction change relied on `Invoke("startSidewaysMotion", …)`.** It used a string method name (not refactor-safe, not camelCase), took two passes over the alien list, and started the bounds at `0` instead of ±infinity. It's replaced with a fixed drop distance tracked in `dropRemaining` and a single combined move-and-measure loop. Hard-coded `±16` bounds are replaced with bounds taken from the camera.
6. **Leftover clutter and broken components.** The template had redundant `fleet.GetComponent<AlienFleet>()` calls on a field already typed `AlienFleet`, empty `Start`/`Update` methods (Unity still calls them), and commented-out dead code. Tuning values were public statics, so they didn't show in the Inspector; they're now `[SerializeField]`. The Spaceship also had **two** Rigidbodies (one in the prefab, one added in the scene) and a `MeshCollider` with no mesh. The `List<GameObject>` is now a typed `List<Alien>`.

## Additional features (four or more required)

- **Start and end screens.** A "SHADOW BLADE" title screen with controls, a "Stage N" intro between levels, and Defeated / Victory screens showing score and high score. High score is saved with `PlayerPrefs`. (`GameManager`, `GameUI`)
- **Additional levels.** Three stages, configured in `GameManager.levels` (`LevelConfig`). Each stage has more samurai, starts lower, moves faster, shoots more often with more aimed, faster arrows, and tints the army a new clan colour.
- **Lives.** 3 lives (up to 5 with the Tidal Renewal boon), shown as ronin head icons bottom-left. After a hit the ronin plays his hit animation, blinks and is invulnerable for 2 s. (`Ronin`, `GameUI.SetLives`)
- **Shields (courtyard blockers).** Four sets of courtyard blockers, each a bush, a stone lantern and a bush (`BlockerWall`, `Blocker`). Bushes take 5 hits and lanterns 8; each shows three damage states and then breaks into leaves or stone chips, so you can shoot gaps into a set. Samurai that march into the blockers trample them. Blockers are rebuilt every stage.
- **Collision animations.** Samurai play a death animation and burst into sakura petals and smoke; the ronin bursts into sparks and smoke when hit and when killed; blockers break into leaves or stone chips. (`Debris.BurstSprites`, `SpriteAnimator`)
- **New graphics.** Every primitive (sphere aliens, capsule ship, cube debris) is replaced with chibi pixel art I generated with PixelLab's AI tools from my own prompts:
  - **Samurai:** three types by row, worth more points the higher they stand: Ashigaru (spear, straw hat) in the bottom rows, Bushi (katana, lamellar armour) in the middle, and Taishō (horned kabuto, menpō mask) on top. Each has a march loop and a death animation.
  - **Ronin (player):** idle, kunai throw, katana parry, hit, and left/right sprint animations. The hitbox is fitted to his body and spirit flames.
  - **Projectiles:** a spirit-flame kunai that flies point-first, the Spirit Shuriken, and samurai arrows.
  - **Souls and boons:** fire, water and lightning soul wisps, 15 boon medallion icons, 3 ability icons and an ink-brush boon card with gold trim.
  - **UI:** a pixel brush-style font made with PixelLab (`Art/Fonts/ShadowBladePixel.ttf`, baked into a TMP font asset), a black-lacquer panel with crimson and gold trim behind the title, stage and end screens (9-sliced so it scales cleanly), and ronin head life icons.
  - **Pipeline:** `Editor/PixelArtImporter.cs` imports everything under `Assets/Art/Sprites` as crisp point-filtered sprites at a fixed pixels-per-unit. `CharacterArt` ScriptableObjects hold each character's clips, `SpriteAnimator` plays them, and the `Ninja/Apply Sprite Art` menu command (`Editor/NinjaArtSetup.cs`) rebuilds the prefabs from the art. `Ninja/Apply Boon Art` (`Editor/BoonArtSetup.cs`) builds the fire, water and lightning versions of the ronin and his projectiles by recolouring only the teal spirit-flame hues of the original frames, and wires the soul and boon art into the scene. Gameplay objects keep their 3D Rigidbodies and colliders, so only the visuals changed.
- **Elemental souls and Hades-style boons.** Fallen samurai sometimes drop a **Fire**, **Water** or **Lightning** soul (7% per kill, guaranteed after 15 kills without one, one on screen at a time) that drifts down and fades if you miss it. Catching one pauses the game on a "Boons of Fire / Frost / Storm" screen modelled on Hades: the soul glows on the left, and three ink-brush cards each show a medallion icon, the boon name in its rarity colour (Common, Rare, Epic), its effect and a stat line. Like the Olympian gods, each element has its own theme:
  - **Slots.** The ronin has three core slots, **Attack** (kunai), **Special** (Spirit Shuriken) and **Parry**, which hold one boon each; picking another element's boon for a filled slot replaces it (the card says so). **Passives** stack alongside them. A boon you already hold comes back as an **Upgrade** to the next rarity. The first soul of a run always offers a slot boon.
  - **Fire (burn):** Flame Kunai, Inferno Shuriken and Burning Riposte make kills explode; Blazing Hands throws faster; Inferno Fan adds kunai in a spread.
  - **Water (chill):** Frost Kunai slows the army's march (stacks 3×), Glacial Shuriken freezes the whole army, Frozen Riposte turns parries into Frost Wards that absorb a hit; Tidal Renewal restores lives; Glacier Wall rebuilds the blockers in tougher ice.
  - **Lightning (jolt):** Storm Kunai chains to the nearest samurai, Thunder Shuriken strikes nearby columns, Thunder Riposte calls lightning on samurai near you; Storm Step adds move speed; Static Charge shortens the Special and Parry cooldowns.
  - Kills made by these effects never trigger further effects, so there are no runaway chain reactions. The HUD shows the three slots (with cooldown sweeps) and your passives bottom-right.
  - The ronin changes to match: his spirit flames, aura, kunai, shuriken and parry flash take the colour of his boons. (`Boon`, `RoninLoadout`, `BoonEffects`, `Soul`, `BoonChoiceUI`, `ElementFx`)
- **Animated VFX and game feel.** Every event has an animated pixel-art effect I built in PixelLab's VFX workbench (15 effects: impact sparks, ice bursts, smoke puffs, explosions, a parry slash, purple lightning strikes, a frost wave, a Frost Ward bubble, a boon pillar, and fire/frost/storm auras and soul crackles). Kills spark in the colour of the projectile that made them, Fire boons explode, Storm boons call down lightning, Frost boons burst ice and roll a frost wave over the army, and the ronin wears an aura of his main element. Hits also get a small trauma-based screen shake, a white hit flash, and a short hit-stop when a parry lands. (`Fx`, `FxLibrary`, `CameraShake`, `HitFlash`, `Editor/FxSetup.cs` → `Ninja/Apply VFX`)
- **Sound effects and music (ElevenLabs).** 22 sound effects made with ElevenLabs Sound Effects, most with two takes picked at random and slight pitch variation: kunai and shuriken throws, parry swings and deflect clangs, bow shots, samurai deaths, player hits, Frost Ward shatters, barricade hits and breaks, soul chimes, fire, lightning and frost spells, boon-menu clicks, a taiko-and-gong stage start, and victory and defeat stings. The army marches to a falling four-note taiko beat that speeds up as the army does, like the original Space Invaders. Two instrumental tracks were made with the ElevenLabs Music API: a calm shakuhachi-and-koto title theme and a taiko-and-shamisen battle loop that crossfade between screens. Sounds play through the parry hit-stop and the paused boon screen. (`Sfx`, `SfxLibrary`, `Music`, `Editor/AudioSetup.cs` → `Ninja/Apply Audio`; music is regenerated with `npm run music`, which reads the ElevenLabs key from the gitignored `.env.local`)
- **New abilities.** The **Spirit Shuriken** is a big spinning blade that cuts through a whole column (6 s cooldown). **Parry** is a quick katana stance that knocks arrows back up at the samurai (1.5 s cooldown). Both projectiles go through `Kunai`, which also handles piercing.
- **New background.** A top-down moonlit grass courtyard the samurai march across, with sakura petals drifting over it (`Backdrop`). It's top-down to match the 3/4-view character sprites, so the armies stand on the ground instead of floating in front of a sky.

## Balancing

The template was easy because the player could fire 20 shots per second and the fleet never sped up. Changes:

- Player fire cooldown raised from 0.05 s to 0.35 s.
- The army speeds up as it shrinks: 4× at stage 1, up to 5× at stage 3.
- More samurai (40 / 55 / 55 instead of 30), and the army starts closer to the player on later stages.
- Arrows come from the front line, partly aimed at the player, and get faster each stage.
- The game is still beatable thanks to 3 lives, invulnerability after a hit, and the courtyard blockers.
- Because the base game is hard, souls give the player a way to build up power over a run: about three boons per stage, and the Spirit Shuriken and Parry give tools for clearing columns and dealing with arrows. The drop chance and pity count are tunable on `GameManager`.

All of these values can be tuned in the Inspector on `GameManager` (the per-stage `levels` array), `SamuraiArmy`, `Ronin` and `Blockers`.
