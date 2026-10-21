# Architecture

Shadow Blade is a Space Invaders remake (Unity 6000.6.6f1, URP, new Input System). One scene,
`Assets/Scenes/SampleScene.unity`. Every gameplay script sits flat in `Assets/`; editor tooling sits in
`Assets/Editor/`. This file is a map: where each system lives and how the pieces talk to each other.

## Game flow

`GameManager` owns the run and is the hub everything reports to.

```
Title ──Enter──▶ Stage intro ──▶ Round 1 ─▶ Round 2 ─▶ Round 3 ─▶ Boss ─▶ stage boon ─▶ next stage / Victory
                                   │  (soul caught → Choosing: boon screen, time paused)            │
                                   └──── ronin out of lives / army reaches ronin ──▶ GameOver ◀─────┘
```

- States: `GameManager.State { Title, Transition, Playing, Choosing, GameOver }`.
- Content is data: `GameManager.stages` is a `StageConfig[]` (`StageConfig.cs`). Each stage has a name,
  `LevelConfig[] rounds` (`LevelConfig.cs`) and a `BossKind boss`. Two stages: "The Bamboo Road" (three
  rounds, then Benibana) and "The Pilgrim's Pass" (three rounds with Shieldbearers and divers, then Utsusemi). The default array in code is only the initial value; the scene's serialized
  copy wins, so edit it in the Inspector (or keep both in step).
- Key methods: `StartGame`, `BeginRound`, `OnArmyDefeated` → `StageCleared` → `BeginBoss` / `OfferStageBoon`,
  `NextStage`, `EndGame`. Blockers are rebuilt only when a stage starts, so they wear down across its rounds.
- Restart reloads the scene with `skipTitle` set.
- **Dev hotkeys (Editor only, `GameManager.DevHotkeys`):** F1 kills the current army, F2 jumps to the last
  stage's boss (or last round if it has none).

## Player combat — the Ronin

| What | Where |
|---|---|
| Movement, input, screen clamp, cooldowns, lives/invulnerability | `Ronin.cs` (reads `Keyboard.current` directly) |
| Sword slash (Space), Inferno Fan spread | `Ronin.Attack` → `Ronin.Slash` → `SlashWave.prefab` (a `Kunai` with a wide hitbox, `KillSource.Slash`) |
| Kunai throw (kept for a future weapon select) | `Ronin.weapon = Kunai` → `Ronin.ThrowKunai` → `Kunai.prefab`; also the fallback while `slashPrefab` is unset |
| Spirit Shuriken (E / Up), 6 s cooldown, pierces a column | `Ronin` → `Kunai.Launch(..., KillSource.Shuriken)` |
| Parry (Shift / Down), 1.5 s cooldown, 0.25 s window | `Ronin.Deflect` → `Arrow.TryDeflect()` → riposte `Kunai` (`KillSource.Deflected`), or a boss slash sent back (`KillSource.Reflected`). The riposte copies the arrow's art (`Kunai.Mimic`), wears `Kunai.ParryAura` and homes (`Kunai.Home`) on `Ronin.RiposteTarget` = `GameManager.RandomEnemy`, passing through everything but its target and blockers. With Burning Riposte it's a fireball, with Frozen Riposte a fan of ice shards (`Ronin.SpawnRiposte`, `Kunai.Animate` with `Fx.Fireball` / `Fx.IceShard` frames); their hits raise `Kunai.Impacted` → `Ronin.RiposteImpact` → `BoonEffects.OnRiposteImpact` |
| Unparryable damage (boss chain) | `Ronin.TakeHit(Vector3)` |
| Elemental look (aura, flame colour, projectile art) | `Ronin.SetLook`, driven by `GameManager.UpdateRoninLook` |
| Ronin events | `Hit`, `ShurikenThrown`, `Parried`, `RiposteImpact` (GameManager subscribes) |

All player projectiles use the `Kunai` component (slash waves, kunai, shuriken, ripostes); `Kunai.cs` also defines
`KillSource { Kunai, Shuriken, Deflected, Effect, Reflected, Slash }`. The Attack-slot boons (Flame/Frost/Storm Blade)
trigger on both `Slash` and `Kunai`. Damage per source is `Damage.Of` in `IHittable.cs` (slash and kunai 1, shuriken 3, deflected 2, reflected 0: a parried boss slash only staggers).
Anything the player can damage implements `IHittable.TakeHit(int, KillSource)`: `Samurai`, `Scout`, `Boss`.

## Boons (Hades-style upgrades)

| What | Where |
|---|---|
| Elements, slots, rarities, every boon's numbers and text | `Boon.cs` (`Element`, `BoonSlot`, `Rarity`, `BoonId`, `Boons.All`, `Boons.Draw`) |
| What the ronin holds and the stats they add up to | `RoninLoadout.cs` (`Take`, `Value`, `DominantElement`) |
| Effects in play (blasts, encase/shatter, freeze, lightning, boss versions) | `BoonEffects.cs` (`OnKill`, `OnBossHit`, `OnShurikenThrown`, `OnParried`, `OnRiposteImpact`). Any encased samurai killed by a non-`Effect` source `Shatter`s, hitting its neighbours |
| Soul drops (7%, pity after 15 kills, one at a time) | `Soul.cs`; drop logic in `GameManager.MaybeDropSoul`. Until the first soul of a run is caught, `Soul.ShowHint` adds the bobbing `GameManager.soulHintArrow` (`UI/SoulArrow`, wired by Apply Boon Art) |
| Boon pick screen + HUD slot medallions | `BoonChoiceUI.cs` (built entirely in code) |

Rule: kills from `KillSource.Effect` never trigger further effects (no chain reactions). `BoonId` order must
match `BoonChoiceUI.boonIcons`.

## Enemies

### Samurai army (the "invaders")
- `SamuraiArmy.cs` — spawns a round from a `LevelConfig` (`SpawnStage`), marches, speeds up as it shrinks
  (`CurrentSpeed`), drops and turns at the camera edges, fires from the front of a column, tramples blockers,
  raises `ArmyReachedRonin` when it lands. Rounds with `LevelConfig.diveInterval` send a samurai from behind the
  front rank on a telegraphed bezier swoop at the ronin (`StartDive`/`UpdateDives`): it hits blockers (`Blocker.Hit`)
  and the ronin, then glides back into its marching slot; divers are left out of the edge, landing and front-column
  checks. Also hosts the army-wide freeze and spatial queries
  (`NearestSamurai`, `SamuraiWithin`, `FrontSamuraiInColumn`, `ClosestColumnTo`) used by boons.
- `Samurai.cs` — one soldier: `Init`, `Fire`, `TakeHit`, `Kill`, frost tint, and `Encase` (ice from Frost Blade / Frozen Riposte: holds its fire, and the ronin's next hit kills it outright).
- `SamuraiKind.cs` — `Ashigaru` (single arrow), `Archer` (3-arrow fan), `Taisho` (2 HP, unbreakable spear
  thrown on its own army timer, `SamuraiArmy.ThrowSpear`, over the ranks in front) and `Shieldbearer` (2 HP,
  steel-tinted, never fires; `FrontShooterInColumn` lets the rank behind shoot over it). Rows pick a kind via
  `LevelConfig.rowKinds`; art and points come from `SamuraiArmy.ArtFor` / `PointsFor`
  (`samuraiByRow` art: Taisho, Bushi, Bushi, Ashigaru; the Taisho kind wears the Taisho art).
- `Arrow.cs` — enemy projectile; also restyled into boss shots (`Restyle`, `MakeReflectable`, `Arrow.Fan`).
  `MakeUnbreakable` makes the Taisho spear: it passes through slashes, kunai, arrows and blockers and only stops on the
  ronin, so it must be dodged or parried (a parried one gives a riposte that pierces two samurai). Its yari art
  (`Art/Sprites/Projectiles/Spear.png`) reaches it through `Samurai.spearSprite`, wired by `Ninja/Apply Sprite Art`.
- `SamuraiCounter.cs` — "Samurai Left" HUD text.

### Mini-enemy: Scout
- `Scout.cs` — kite-borne scout crossing the top of the screen (the mystery-ship role). One hit, bonus points,
  guaranteed soul. Built in code (no prefab). Spawned by `GameManager.UpdateScouts` in rounds with
  `LevelConfig.scouts = true`, every 15–25 s.

### Bosses
- `Boss.cs` — abstract base. Handles creation (`Boss.Create<T>`, no prefab), entrance, HP, phase checks,
  staggers, freeze (boss-time `Wait`), dashes with afterimages, telegraph bars, shots, a stall watchdog
  that restarts a dead fight loop, and the default defeat. Lines: `BossLine { Intro, Phase, Defeat }`.
- `Benibana.cs` — Stage 1 boss, "The Crimson Smoke" (70 HP). Moves: Smoke Rings, Rosary Lash, Crimson Iaido
  (only a parry answers it: `Arrow.OnCollisionEnter` lets reflectable shots cut through slashes and cover; parried
  back it staggers her and `Arrow.Shatter` breaks any Iaido crescent it meets), Rosary Bloom (phase 2 only). Phase 2 at half health: smoke veil
  (`VeilPuff`, burnable by fire), aura, glowing eyes, faster combos. Also defines helpers `Swell`, `VeilPuff`.
- `Utsusemi.cs` — Stage 2 boss, "The Hollow Pilgrim" (90 HP), a karakuri automaton that moves in ticking steps
  (`Stride`). Moves: Kasa Wheel (its unbreakable hat zig-zags down and back, shedding tassel shots; hits deal double
  while the hat is off via `Boss.DamageMultiplier`), Lace Lattice (a curtain of breakable diamonds with gaps that
  drifts through cover, `Arrow.PassesCover`), Escapement (the arm ticks down over the ronin then thrusts a reflectable
  blade; parried, it jams and staggers, `OnStaggered`), Plumb Lines (weights swing on threads, flown with
  `Arrow.Steer`), and in phase 2 the Heart Lantern (a core beam swept across the ground that only blockers stop, and
  which burns them). Phase 2 at half health opens its core and speeds the clock. Fire burns the lace (`Burn`).
  Effect ids `Fx.Utsu*` fall back to code shapes until art exists.
- Wiring in `GameManager`: `BeginBoss` (intro, boss moon via `Backdrop.SetBossMoon`: blood for Benibana, steel for Utsusemi, music),
  `CreateBoss` (switch on `BossKind`), `VoiceFor` (portrait/close-up/voice clips), `OnBossSpoke`,
  `OnBossDamaged`, `OnBossDefeated` → `BossFell`. HP bar, subtitles and the intro cinematic are in `GameUI`
  (`ShowBossBar`, `SetBossHealth`, `ShowSubtitle`, `PlayBossIntro`). The intro's Borderlands-style title card
  paints its backdrop with `TitleCardArt.Backdrop(colour)` and letters the name/epithet with `GameUI.cardNameFont`
  / `cardTagFont` (TTFs turned into TMP fonts at runtime; HUD font if unset).
- Art built by `Ninja/Apply Boss Art` (`Editor/BossArtSetup.cs`).

**Adding a boss:** add a value to `BossKind` (`StageConfig.cs`) → subclass `Boss` (override `Title`, `Epithet`,
`Colour`, `Line`, the fight coroutine) → add cases to `GameManager.CreateBoss` and `VoiceFor` plus serialized
art/voice fields → set `boss` on a `StageConfig` → extend `BossArtSetup` for its frames.

**Adding a samurai kind:** add to `SamuraiKind` → branch its behaviour in `Samurai.Fire` (or elsewhere) →
give it art/points in `SamuraiArmy.ArtFor` / `PointsFor` → use it in a round's `rowKinds`.

## Environment

- `BlockerWall.cs` / `Blocker.cs` — courtyard cover (bushes 5 HP, lanterns 8 HP), `Rebuild(healthMultiplier)`
  (Glacier Wall uses the multiplier for ice blockers).
- `Backdrop.cs` — moonlit courtyard, sakura petals, blood moon for boss fights.

## UI

- `GameUI.cs` — HUD (score, stage/round, lives), overlay screens (title, stage intro, win/lose), boss bar,
  subtitles, boss intro. Builds its pieces in code on the Canvas.
- `TitleCardArt.cs` — generates the painted paper backdrop (ink band, scratches, paint splatter) for boss title cards.
- `BoonChoiceUI.cs` — see Boons. `SpriteAnimatorUI.cs` animates UI images in unscaled time.

## VFX, feel and audio

| What | Where |
|---|---|
| Animated pixel effects by id | `Fx.cs` (`Fx.Play`, ids as constants) + `FxLibrary` asset (`Art/Fx/FxLibrary.asset`) |
| Code-built glows, blasts, lightning bolts | `ElementFx.cs`, `ProceduralSprites.cs` |
| Debris bursts (petals, chips, sparks) | `Debris.cs` |
| Screen shake (read `CameraShake.Center`, never the camera, for layout) | `CameraShake.cs` |
| White hit flash | `HitFlash.cs` + `Art/Fx/SpriteFlash.shader` |
| Parry hit-stop | `GameManager.HitStop` |
| Sound effects by id (pooled, ignores timeScale) | `Sfx.cs` + `SfxLibrary` asset (`Audio/SfxLibrary.asset`) |
| Music crossfade and ducking | `Music.cs` |
| Boss voicelines | `GameManager.voice` AudioSource, clips in `Audio/Voice/`; `BossLine` full lines (intro/phase/defeat) vs. barks (hurt, stagger, move callouts) in `OnBossSpoke` |

Missing libraries or clips fall back gracefully (simple glows, silence), so the game runs before art/audio is wired.

## Sprites and animation

- `CharacterArt` ScriptableObject (`Art/Characters/*.asset`): clips `idle`, `attack`, `hit`, `death`, `parry`,
  `runLeft`, `runRight` and death particles. Assets: Ronin (+ Fire/Water/Lightning), Ashigaru, Bushi, Taisho,
  Benibana, Utsusemi. A boss can rest in a different loop by overriding `Boss.IdleClip` (Utsusemi's bare-headed
  idle, `GameManager.utsusemiBareIdle`, while its hat is thrown).
- `SpriteAnimator.cs` plays frame lists on a SpriteRenderer (no Animator controllers).
- Raw frames live in `Art/Sprites/{Characters,Projectiles,Fx,Effects,Blockers,Background,UI}`;
  `Editor/PixelArtImporter.cs` imports them as point-filtered pixel sprites automatically.
- Boss intro paintings: `Art/Portraits/`. Fonts: `Art/Fonts/` (pixel HUD font; Shojumaru and Bangers for title cards).

## Editor menu commands (`Ninja/…`)

These rebuild assets/prefabs/scene wiring from the art on disk. Run them inside Unity; they're safe to re-run.

| Menu | Script | Does |
|---|---|---|
| Apply Sprite Art | `Editor/NinjaArtSetup.cs` | Rebuilds character/projectile prefabs, CharacterArt, blockers, backdrop |
| Apply Backdrop And Blockers Only | `Editor/NinjaArtSetup.cs` | Just the backdrop/blockers (no prefab ID churn) |
| Apply Boon Art | `Editor/BoonArtSetup.cs` | Elemental ronin recolours, soul/boon/card art wiring |
| Apply Boss Art | `Editor/BossArtSetup.cs` | Boss CharacterArt, intro portraits and voice → GameManager; title-card fonts → GameUI |
| Apply VFX | `Editor/FxSetup.cs` | Builds `FxLibrary` from `Art/Sprites/Fx` |
| Apply Audio | `Editor/AudioSetup.cs` | Builds `SfxLibrary` from `Audio/Sfx` and `Audio/Music` |
| Rename To Ronin Theme | `Editor/ThemeRename.cs` | One-off template rename (already run) |

## Physics

Gameplay objects use 3D Rigidbodies and colliders on the gameplay plane (sprites are visuals only). Layers:
`Ronin`, `Samurai`, `Kunai`, `Arrow`; the collision matrix is in `ProjectSettings/DynamicsManager.asset`.
Characters move kinematically with `Rigidbody.MovePosition`; projectiles are launched once with `linearVelocity`.
Each object handles its own collision (projectiles only destroy themselves).

## Outside Unity

- `Tools/generate-music.mjs` — regenerates music via ElevenLabs (`npm run music`, key in gitignored `.env.local`).
- `Tools/generate-sfx.mjs` / `Tools/generate-voice.mjs` — the newer sound effects and Benibana's mid-fight barks (`npm run sfx`, `npm run voice`).
- `README.md` — the assignment write-up (requirements, template fixes, features, balancing).
