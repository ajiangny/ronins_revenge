# Shadow Blade: Ronin vs. Samurai — Assignment 1

A Space Invaders remake re-themed as a lone chibi ronin holding a moonlit temple courtyard against marching samurai armies.

Built on the class template ([Oren-Steinberg/Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D)), Unity 6000.6, URP.
Open `Assets/Scenes/SampleScene.unity` and press Play.

**Controls:** Left/Right or A/D to move, Space to throw a kunai, Enter to start or restart.

In the code the original class names are kept (`Alien` = samurai, `Spaceship` = ronin, `Dart` = kunai, `AlienBullet` = arrow) so prefab and scene references don't break.

## Required features

| Requirement | Where |
|---|---|
| Remaining aliens speed up as others are destroyed | `AlienFleet.CurrentSpeed()` scales linearly from `baseSpeed` to `baseSpeed × maxSpeedMultiplier` as the fleet empties. |
| Detect when the game can no longer be won | `AlienFleet.FixedUpdate()` raises `FleetLanded` once the lowest alien reaches the top of the ship's hull (`Spaceship.TopY`). At that height the aliens can't be shot down, so `GameManager` ends the game. |
| Ship can't move offscreen | `Spaceship.FixedUpdate()` clamps the ship to the camera's visible width, using the hull's measured bounds (the pivot isn't centred on the mesh). |
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
- **Lives.** 3 lives, shown as icons bottom-left. After a hit the ronin plays his hit animation, blinks and is invulnerable for 2 s. (`Spaceship`, `GameUI.SetLives`)
- **Shields.** Four sets of courtyard blockers, each a bush, a stone lantern and a bush (`ShieldWall`, `ShieldBlock`). Bushes take 5 hits and lanterns 8; each shows three damage states and then breaks into leaves or stone chips, so you can shoot gaps into a set. Samurai that march into the blockers trample them. Blockers are rebuilt every stage.
- **Collision animations.** Samurai play a death animation and burst into sakura petals and smoke; the ronin bursts into sparks and smoke when hit and when killed; blockers break into leaves or stone chips. (`Debris.BurstSprites`, `SpriteAnimator`)
- **New graphics.** Every primitive (sphere aliens, capsule ship, cube debris) is replaced with chibi pixel art I generated with PixelLab's AI tools from my own prompts:
  - **Samurai:** three types by row, worth more points the higher they stand: Ashigaru (spear, straw hat) in the bottom rows, Bushi (katana, lamellar armour) in the middle, and Taishō (horned kabuto, menpō mask) on top. Each has a march loop and a death animation.
  - **Ronin (player):** idle, kunai throw, hit, and left/right sprint animations. The hitbox is fitted to his body and spirit flames.
  - **Projectiles:** a spirit-flame kunai that flies point-first, and samurai arrows.
  - **Pipeline:** `Editor/PixelArtImporter.cs` imports everything under `Assets/Art/Sprites` as crisp point-filtered sprites at a fixed pixels-per-unit. `CharacterArt` ScriptableObjects hold each character's clips, `SpriteAnimator` plays them, and the `Ninja/Apply Sprite Art` menu command (`Editor/NinjaArtSetup.cs`) rebuilds the prefabs from the art. Gameplay objects keep their 3D Rigidbodies and colliders, so only the visuals changed.
- **New background.** A top-down moonlit grass courtyard the samurai march across, with sakura petals drifting over it (`Backdrop`). It's top-down to match the 3/4-view character sprites, so the armies stand on the ground instead of floating in front of a sky.

## Balancing

The template was easy because the player could fire 20 shots per second and the fleet never sped up. Changes:

- Player fire cooldown raised from 0.05 s to 0.35 s.
- The army speeds up as it shrinks: 4× at stage 1, up to 5× at stage 3.
- More samurai (40 / 55 / 55 instead of 30), and the army starts closer to the player on later stages.
- Arrows come from the front line, partly aimed at the player, and get faster each stage.
- The game is still beatable thanks to 3 lives, invulnerability after a hit, and the courtyard blockers.

All of these values can be tuned in the Inspector on `GameManager` (the per-stage `levels` array), `AlienFleet`, `Spaceship` and `Shields`.
