# Space Invaders 3D — Assignment 1

Built on the class template ([Oren-Steinberg/Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D)), Unity 6000.6, URP.
Open `Assets/Scenes/SampleScene.unity` and press Play.

**Controls:** Left/Right or A/D to move, Space to fire, Enter to start or restart.

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

- **Start and end screens.** A title screen with controls, a "Wave N" intro between levels, and Game Over / You Win screens showing score and high score. High score is saved with `PlayerPrefs`. (`GameManager`, `GameUI`)
- **Additional levels.** Three waves, configured in `GameManager.levels` (`LevelConfig`). Each wave has more aliens, starts lower, moves faster, fires more often with more aimed shots and faster bullets, and uses a new alien colour.
- **Lives.** 3 lives, shown as ship icons bottom-left. After a hit the ship blinks and is invulnerable for 2 s. (`Spaceship`, `GameUI.SetLives`)
- **Shields.** Four sets of courtyard blockers, each a bush, a stone lantern and a bush (`ShieldWall`, `ShieldBlock`). Bushes take 5 hits and lanterns 8; each shows three damage states and then breaks into leaves or stone chips, so you can shoot gaps into a set. Samurai that march into the blockers trample them. Blockers are rebuilt every stage.
- **Collision animations.** Exploding debris bursts for alien kills, ship hits, ship destruction and shield blocks breaking. (`Debris`)
- **New background.** A top-down moonlit grass courtyard the samurai march across, with sakura petals drifting over it (`Backdrop`). It's top-down to match the 3/4-view character sprites, so the armies stand on the ground instead of floating in front of a sky.

## Balancing

The template was easy because the player could fire 20 shots per second and the fleet never sped up. Changes:

- Player fire cooldown raised from 0.05 s to 0.35 s.
- The fleet speeds up as it shrinks: 4× at wave 1, up to 5× at wave 3.
- More aliens (40 / 55 / 55 instead of 30), and the fleet starts closer to the player on later waves.
- Alien shots come from the front line, partly aimed at the player, and get faster each wave.
- The game is still beatable thanks to 3 lives, invulnerability after a hit, and shields.

All of these values can be tuned in the Inspector on `GameManager` (the per-wave `levels` array), `AlienFleet`, `Spaceship` and `Shields`.
