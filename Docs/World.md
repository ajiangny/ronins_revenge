# Environment and physics

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

## Environment

- `BlockerWall.cs` / `Blocker.cs` — courtyard cover (bushes 5 HP, lanterns 8 HP), `Rebuild()` every round; sites are spread across the view minus `sideInset` at each side, so
  the outer ones stay off a map's edge scenery. A `Backdrop.Map` can bring its own `blockers` and
  `blockerPattern`; `BlockerWall.Use` switches to them for every later `Rebuild` (null goes back to the courtyard's).
  Each `BlockerType` can have extra `variants` (`Look`s), and every blocker built picks one at random (`PickLook`),
  so a row isn't all identical; the courtyard has three looks of each too (`BushB/C`, `LanternB/C`, wired by `Apply Backdrop And Blockers Only`). Map sets: Bamboo Road = roadside stones and bamboo clumps (`SBS`), Pass = boulders and Jizo statues (`RJR`), Benibana = maple shrubs and
  komainu (`MKM`), Utsusemi = gear piles and clock pillars (`GCG`), Frozen Shrine = snow-capped stones and broken torii
  posts (`STS`), Shirogane = frosted shrubs and shrine lanterns (`ISI`), Mountain Shrine = grave markers and temple bell
  frames (`GBG`), Kagetsune = oni braziers and broken palace pillars (`OPO`), three looks each, art in `Art/Sprites/Blockers/`
  as `<Name>_0..2`, `<Name>B_0..2`, `<Name>C_0..2` (from PixelLab), wired by `Ninja/Apply Maps`.
- Steel works on the cover (`BoonEffects.Steel.cs`). `BlockerWall` keeps a `Slot` per wall blocker, so `Restore(raise)` can bring standing cover to full and, in a round, rebuild the fallen. `Blocker.Mend` never passes `maxHealth`; `Temper` (Tamahagane) raises it, up to `baseHealth` + 2. `NearestHurt` and `NearestUntempered` pick the target. **Plates**: `TryPlate` adds a 1.1 × 0.8 tinted iron `Blocker` (`Plate`, `Forged`; one hit) in the gap below, stepping outward by 0.5 up to the reach, at most 4, rounds only; `Rebuild` clears them, so none reaches a warlord's fight. Slashes pass a plate and arrows pop it, and it pays nothing when it breaks. `Forged` cover doesn't raise `Blocker.Broken`, and `Blocker.Chipped` fires when an arrow strikes wall cover.
- `Backdrop.cs` — moonlit courtyard, sakura petals, blood moon for boss fights; `Show(Map)` swaps to another map's
  ground and petals (the moon blends from the map's own tints). Maps: `Art/Sprites/Background/` —
  `BambooRoad_Stage1` (a dirt road with bamboo only at the edges), `Pass_Stage2` (the Pilgrim's Pass), `Arena_Benibana` (maple shrine),
  `Arena_Utsusemi` (clockwork temple floor), `Mountain_Stage4` (a broad paved landing on the shrine stair, steps along its top and bottom edges) and `Arena_Kagetsune` (the coronation courtyard, the hall front along its top edge), generated with PixelLab. On these two the ground runs edge to edge from map y 40 to 362 (of 384), with only flat marks on it and nothing upright, because soldiers, the ronin and Kagetsune all reach the far sides of the view. `CourtyardGrass` with its bushes and lanterns
  is the fallback for any stage without a rounds map in `NinjaArtSetup.RoundsGrounds` (`Shrine_Stage3` is stage 3's;
  `Arena_Shirogane` her court).
  `GameManager.Start` shows stage 1's map (behind the title menu too).
- `Atmosphere.cs` — each map's mood, built by `Backdrop` and swapped in `Show` (`Map.atmosphere`, `Atmosphere.Settings`;
  the courtyard's is `Backdrop.courtyardAtmosphere`): cloud shadows drifting with the wind, a moonlight glow and a
  shade deepening away from the moon's top-left corner, low mist banks, drifting motes (fireflies, embers, dust) and a
  vignette. Cloud shadows sort at 20, over everything standing in the field (characters, bosses, cover, arrows at 0-5)
  but under effects (40+); mist and light lie on the ground (-95..-93); motes and the vignette float over the field
  (58, 60). Textures are built in code with full-rect meshes; all of a map's clouds are painted into one seamless
  tile (`CloudLayer`, spread on a jittered grid, overlaps kept at the darker cloud rather than stacked) that a tiled
  sprite scrolls with the wind, so shadows never double up; each shape is cut to leave about two fifths in shadow
  (`CloudShape`), giving dappled patches with gaps. `Backdrop` tints the moonlight with the boss moon (`TintMoon`). Each
  map's mood is set in `NinjaArtSetup.Moods` and wired by `Ninja/Apply Maps`.


## Physics

Gameplay objects use 3D Rigidbodies and colliders on the gameplay plane (sprites are visuals only). Layers:
`Ronin`, `Samurai`, `Projectile`, `Arrow`, `BossBullet` (bullet-hell shots; its collisions are copied from `Arrow` at
runtime by `BulletHell`, minus arrows and itself); the collision matrix is in `ProjectSettings/DynamicsManager.asset`.
Characters move kinematically with `Rigidbody.MovePosition`; projectiles are launched once with `linearVelocity`.
Each object handles its own collision (projectiles only destroy themselves).
