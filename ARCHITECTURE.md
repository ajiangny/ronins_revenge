# Architecture

Shadow Blade is a Space Invaders remake (Unity 6000.6.6f1, URP, new Input System). One scene,
`Assets/Scenes/SampleScene.unity`. Every gameplay script sits flat in `Assets/`; editor tooling sits in
`Assets/Editor/`. This file is a map: where each system lives and how the pieces talk to each other.

## Game flow

`GameManager` owns the run and is the hub everything reports to.

```
Title menu ──Start──▶ Stage intro ──▶ Round 1 ─▶ Round 2 ─▶ Round 3 ─travel─▶ Boss ─▶ stage boon ─travel─▶ next stage / Victory
                                   │  (soul caught → Choosing: boon screen, time paused)            │
                                   │  (Esc / P / Start → Paused: pause menu, time paused)            │
                                   └──── ronin out of lives / army reaches ronin ──▶ GameOver ◀─────┘
```

- States: `GameManager.State { Title, Transition, Playing, Choosing, Paused, GameOver }`.
- Menu pages are built in `GameManager` (`ShowTitleMenu`, `ShowPauseMenu`, `ShowControls`, `ShowSettings`,
  `ShowEndMenu`) and shown by `MenuUI`. Sub-pages take a `back` callback so Controls/Settings return to
  whichever page opened them. `Pause` only fires in `Playing`; `Resume` always restores `timeScale = 1`
  (a pause can cut off a parry hit-stop).
- Content is data: `GameManager.stages` is a `StageConfig[]` (`StageConfig.cs`). Each stage has a name,
  `LevelConfig[] rounds` (`LevelConfig.cs`) and a `BossKind boss`. Two stages: "The Bamboo Road" (three
  rounds, then Benibana) and "The Pilgrim's Pass" (three rounds with Shieldbearers and divers, then Utsusemi). The default array in code is only the initial value; the scene's serialized
  copy wins, so edit it in the Inspector (or keep both in step).
- Key methods: `StartGame`, `BeginRound`, `OnArmyDefeated` → `StageCleared` → `BeginBoss` / `OfferStageBoon`,
  `NextStage` → `EnterStage`, `EndGame`. Blockers are rebuilt only when a stage starts (or on a new map), so they
  wear down across its rounds.
- **Rounds:** `BeginRound` spawns the army above the view and `SamuraiArmy.MarchIn` marches it down to its slots
  under the banner (`armyMarchInTime`). Until `Advance` the army is `Entering`: its colliders are off, `TakeHit`/`Kill`
  do nothing, and `Soldiers` and the boon queries read it as empty, so nothing from the last round can touch it.
- **Maps and travel:** each `StageConfig` has an optional `roundsMap` and `bossMap` (`Backdrop.Map`: ground, tint,
  petal sprite/tint; empty = the courtyard, and a boss without its own map fights on the stage's). `TravelTo(map)`
  walks the ronin off the top (`Ronin.WalkTo`, collider off, `CharacterArt.walkUp` or an idle bob), fades to black
  (`GameUI.FadeTo`), swaps the ground (`Backdrop.Show`), rebuilds the blockers and walks him up from below to
  `Ronin.Home`. Used before every boss (`StageCleared`) and every later stage (`EnterStage`). F2 swaps the map at once.
- Restart / Play Again reload the scene with `skipTitle` set (`Reload(true)`); Quit to Title / Title reload
  without it.
- **Dev tools (Editor and development builds, `Debug.isDebugBuild`, or release builds while `devToolsInRelease` is on; both via `DevToolsOn`):** a DEV TOOLS page (`ShowDevMenu`) on the
  title and pause menus, or F3, lists every stage's round 1 and boss; `DevJump` stores the pick in statics,
  reloads the scene and `StartGame` hands it to `StartAt`, which shows that fight's map and begins it. God mode
  (`SetGodMode`, F4, kept across reloads) sets `Ronin.Invincible` so `TakeHit` ignores everything, sweeps away an
  army that lands instead of ending the run, and shows a gold tag over the lives (`GameUI.SetGodMode`).
  `DevHotkeys`: F1 kills the current army, F2 jumps to the last stage's boss (or last round if it has none), F5 drops a soul at the ronin (`SpawnSoul`, elements in turn), F6 a technique scroll (`SpawnScroll`).

## Player combat — the Ronin

| What | Where |
|---|---|
| Movement, input, screen clamp, cooldowns, lives/invulnerability | `Ronin.cs` (reads `Keyboard.current` directly) |
| Sword slash (Space / Left Click), reshaped by the Sword Arts | `Ronin.Attack` → `Ronin.Slash` → `Ronin.Fan` → `LaunchSlash` → `SlashWave.prefab` (a `Kunai` with a wide hitbox, `KillSource.Slash`). Piercing Slash sets the pierce count and `Kunai.CutsArrows`, Crescent Moon the scale; Split Slash forks it with `Kunai.SplitAfter` → `Ronin.SplitOff`; Echo Slash adds a homing ghost (`Kunai.Home(RiposteTarget)`) |
| Kunai throw (kept for a future weapon select) | `Ronin.weapon = Kunai` → `Ronin.ThrowKunai` → `Kunai.prefab`; also the fallback while `slashPrefab` is unset |
| Spirit Shuriken (W / Up), 6 s cooldown, pierces a column | `Ronin` → `Kunai.Launch(..., KillSource.Shuriken)` |
| Parry (Shift / Down / Right Click), 0.25 s window; a landed parry resets its cooldown, a miss costs 2 s (`parryMissCooldown`) | `Ronin.Deflect` → `Arrow.TryDeflect()` → riposte `Kunai` (`KillSource.Deflected`), or a boss slash sent back (`KillSource.Reflected`). The riposte copies the arrow's art (`Kunai.Mimic`), wears `Kunai.ParryAura` and homes (`Kunai.Home`) on `Ronin.RiposteTarget` = `GameManager.RandomEnemy`, passing through everything but its target and blockers. With Burning Riposte it's a fireball, with Frozen Riposte a fan of ice shards (`Ronin.SpawnRiposte`, `Kunai.Animate` with `Fx.Fireball` / `Fx.IceShard` frames); their hits raise `Kunai.Impacted` → `Ronin.RiposteImpact` → `BoonEffects.OnRiposteImpact` |
| Unparryable damage (boss chain) | `Ronin.TakeHit(Vector3)` |
| Elemental look (aura, flame colour, projectile art) | `Ronin.SetLook`, driven by `GameManager.UpdateRoninLook` |
| Spirit wisps (bobbing beside him on idle, parry and walk frames, which were drawn without them) | `Ronin.UpdateWisps` from `CharacterArt.wisps`, cut from `Ronin_Base` and recoloured per element by Apply Boon Art; hidden on `CharacterArt.framesWithWisps` (run, hit, throw), which draw their own |
| Ronin events | `Hit`, `ShurikenThrown`, `Parried`, `RiposteImpact` (GameManager subscribes) |

All player projectiles use the `Kunai` component (slash waves, kunai, shuriken, ripostes); `Kunai.cs` also defines
`KillSource { Kunai, Shuriken, Deflected, Effect, Reflected, Slash }`. The Attack-slot boons (Flame/Frost/Storm Blade)
trigger on both `Slash` and `Kunai`. Damage per source is `Damage.Of` in `IHittable.cs` (slash and kunai 1, shuriken 3, deflected 2, reflected 0: a parried boss slash only staggers).
Anything the player can damage implements `IHittable.TakeHit(int, KillSource)`: `Samurai`, `Scout`, `Boss`.

## Boons (Hades-style upgrades)

| What | Where |
|---|---|
| Elements, slots, rarities, every boon's numbers and text | `Boon.cs` (`Element`, `BoonSlot`, `Rarity`, `BoonKind`, `BoonId`, `Boons.All`, `Boons.Draw`) |
| Capstones: Duos (`Partner` element, offered by either soul) and Legendaries, gated by `Requires` on the Blast / Ice / Strike families of core boons; never upgraded. `Boons.Draw` adds the eligible ones (`ensureCapstone` and `minRarity` for the stage boon) and returns a Spirit Offering (`BoonKind.Offering`, a heal or points in `GameManager.TakeBoon`) when an element is spent. Design notes in [BOON_DESIGN.md](BOON_DESIGN.md) | `Boon.cs` (`EligibleCapstones`), `BoonEffects` (`Blast`, `Strike`, `BeatDrum`, `BurnArrows` → `Arrow.Burn`), Still Water via `RoninLoadout.FreezeMultiplier` / `MarchSpeedUpScale` → `SamuraiArmy.SpeedUpScale` |
| Soul elements lean toward held ones (`GameManager.soulElementBias`, `PickSoulElement`); the stage boon (`OfferStageBoon`) picks an eligible capstone's element | `GameManager` |
| What the ronin holds and the stats they add up to | `RoninLoadout.cs` (`Take`, `Value`, `DominantElement`) |
| Effects in play (blasts, encase, freeze, lightning, boss versions) | `BoonEffects.cs` (`OnKill`, `OnBossHit`, `OnShurikenThrown`, `OnParried`, `OnRiposteImpact`, `OnStatueShattered`). Storm Blade arcs to a random samurai within `StormBladeRange`. Thunder Shuriken speeds the shuriken up (`Ronin.ThunderShurikenSpeed`) and `OnShurikenThrown` adds `ShurikenStorm` to it, which gives each samurai or boss it passes within `ShurikenArcRadius` one roll to be arced. Sideways lightning (Storm Blade, Thunder Riposte, Thunder Shuriken, Superconductor) uses `Chain` → `ElementFx.Chain`, an arc between the two samurai; sky strikes use `ElementFx.Bolt` and `Fx.LightningStrike` |
| Ice shell: `Samurai.AddIceShell` lays `SamuraiArmy.IceShell` (`Art/Sprites/Effects/IceShell.png`, PixelLab, wired by `Ninja/Apply Boon Art`) over ice statues only (living encased samurai keep just the tint and halo), as a child of the body so it melts with it; without the art only the ice tint and halo show | `Samurai`, `IceStatue` |
| Ice statues: a samurai killed while `Encased` (any source) leaves the army as an `IceStatue` (code-built from its last frame, on the Samurai layer). The ronin's next non-`Effect` hit raises `IceStatue.Shattered` → `BoonEffects.OnStatueShattered`, which sends `Boons.IceShardsAt` (Frost Blade's `ExtraValues`) homing shards (`Ronin.LaunchIceShard`, `KillSource.Effect`, non-piercing). Shards and Frozen Riposte shards set `Kunai.FreezeOnHit`, encasing before they damage, so their kills freeze too. Left unhit, a statue melts (`SolidTime` 3 s, then `Melt` over `MeltTime` 1.6 s: slump, meltwater tint, drips and a puddle, all code-drawn) and can't be burst past `BreakableUntil`. Statues crumble in `EndRound` (`CrumbleAll`) | `IceStatue.cs`, `Samurai.Kill`, `Kunai`, `Ronin` |
| Sword Arts (Hades' Daedalus Hammer): `BoonKind.Art` boons with no element (`Boons.ArtColor` steel), never in soul draws, offered by `Boons.DrawArts` from a technique scroll. `GameManager.MaybeDropScroll` (`scrollChanceFromScout` 40%, `scrollChancePerKill` 0.5%, `maxScrollsPerRun` 2, one at a time) → `TechniqueScroll` → `OnScrollCollected` / `GatherDrops` → `OfferArts` → `BoonChoiceUI.ShowArts`. Arts join the passives (`RoninLoadout.Take`) without counting toward elements | `Boon.cs`, `TechniqueScroll.cs`, `GameManager`, `Ronin`, `Kunai` |
| Pickups: souls and hearts fall to the ronin's row and wait there; at a round's end (`GameManager.EndRound`, and `BossFell`) `GameManager.GatherDrops` flies every one he can use to him (`Pickup.GatherAll`, `Pickup.CanCollect`) and offers a soul caught that way | `Pickup.cs` (base: fall, rest, magnet, collect), `Soul.cs`, `Heart.cs`, `TechniqueScroll.cs` |
| Lives are health: `GameManager.lives` out of `maxLives`, which only Tidal Renewal raises (to `Boons.MaxLives`). The HUD shows an empty heart per lost life (`GameUI.SetLives`, outline built by `ProceduralSprites.Hollow`) | `GameManager`, `GameUI` |
| Heart drops (`heartDropChance`, 20%) heal one lost life; at full health a heart can't be taken (`GameManager.CanHeal`) and survives `ClearProjectiles` | `Blocker.Broken` (hits only, not trampling or a boss razing) → `GameManager.OnBlockerBroken` / `OnHeartCollected`; art is `GameUI.LifeIcon` |
| Soul drops (7%, pity after 15 kills, one at a time) | `Soul.cs`; drop logic in `GameManager.MaybeDropSoul`. Until the first soul of a run is caught, `Soul.ShowHint` adds the bobbing `GameManager.soulHintArrow` (`UI/SoulArrow`, wired by Apply Boon Art) |
| Boon pick screen + HUD slot medallions (left-edge column; hold Tab for key/name tips) | `BoonChoiceUI.cs` (built entirely in code) |

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
- `Boss.cs` — abstract base. Handles creation (`Boss.Create<T>`, no prefab), entrance, HP, the two-bar phase break
  (`HasSecondPhase`: emptying the first bar runs `BreakPhase`, which plays the subclass `PhaseBreak` cutscene while
  `PhaseBreaking` makes it invincible and refills the bar, raising `Healed`, then starts phase 2 via `SecondPhase`),
  staggers, freeze (boss-time `Wait`), dashes with afterimages, telegraph bars, shots, a stall watchdog
  that restarts a dead fight loop, and the default defeat. Lines: `BossLine { Intro, Phase, Defeat }`.
- `Benibana.cs` — Stage 1 boss, "The Crimson Smoke" (two 70 HP bars). Moves: Smoke Rings, Rosary Lash, Crimson Iaido
  (only a parry answers it: `Arrow.OnCollisionEnter` lets reflectable shots cut through slashes and cover; parried
  back it staggers her and `Arrow.Shatter` breaks any Iaido crescent it meets), Rosary Bloom (phase 2 only). Phase 2 once her first bar empties: smoke veil
  (`VeilPuff`, burnable by fire), aura, glowing eyes, faster combos. Also defines helpers `Swell`, `VeilPuff`.
- `Utsusemi.cs` — Stage 2 boss, "The Hollow Pilgrim" (two 120 HP bars), a karakuri automaton that moves in ticking steps, trailing digital afterimages (`DigitalGhost`: steel-blue and gold `SpriteFlash` silhouettes, faded by `GlitchFade`)
  (`Stride`). Its `Entrance` (a `Boss` hook `BeginBoss` plays after the descent, before the title card) is
  `RazeCover`, a beam from its `Eye` swept along the foot of the blocker row, demolishing every `Blocker`. Moves: Kasa Wheel (its unbreakable hat flies a telegraphed boomerang loop through the ronin's lane,
  `AimHat`/`FlyHat`, while the fight carries on; hits deal double while the hat is off via `Boss.DamageMultiplier`,
  and a parried hat circles it as the ronin's riposte, back on its head only once that riposte is gone: `Arrow.Riposte`, set by `Ronin.Deflect`; in phase 2 the sword wheels round a second loop, `ThrowSwordWheel`/`swordWheeling`, and meanwhile `NextMove` picks only the bladeless Lattice and Plumb Lines, its stance `BareUnarmedIdle` patched at runtime by `ProceduralSprites.Patched` from its unarmed and bare-headed idles), Lace Lattice (a curtain of breakable diamonds with gaps that
  drifts through cover, `Arrow.PassesCover`), Hour Strikes (announced in super armour, `Boss.SuperArmor`, which blocks stagger and freeze and lets only `ArmorDamageTaken`, 5%, of each hit's damage through
  while `HoldArmor` shows `ArmorShell`, a gold outline of eight one-pixel-offset silhouettes drawn with the
  `SpriteFlash` shader, whose colour comes from `_FlashColor` in a property block; `HourMark` numerals drawn from bars, placed by `PlaceHours` in spread spots numbered in a `ZigZag` order, are shown one by one, then hidden; it `Dash`es
  down from its row onto each mark in turn, chops it with its own chop frames and dashes back; then `StrikeTheHour` from its row flings a giant
  unbreakable, reflectable cleave that `HuntRonin` steers onto him, so only a parry answers it; parried, it jams and
  staggers, `OnStaggered`. Its dash, chop and hour-slash frames come from `GameManager.utsusemiDash`/`Chop`/`HourSlash` via
  `SetSwordFrames`, with the thrust frames as the fallback), Plumb Lines (weights hang from `Utsu_PlumbPortal` portals opened above and behind it; a cut thread plays `SnapThread`/`DropLooseEnd`, built in code; it `Pace`s about from the first portal until the swings die,
  `OpenPortal`/`ClosePortal`, and swing through the ronin's lane as a pendulum wave before being reeled back in, flown
  with `Arrow.Steer` per physics step). Phase 2 once its first bar empties opens its core and is the laser phase (`NextMove` puts a
  laser at least every other move): the Heart Lantern (a beam swept there and back) and Midnight (two clock-hand beams
  closing on the planted sword; at low health `HoldingHands` keeps them burning at the edges while it `RecallSword`s
  and throws again). Both open with `ThrowSword` (the `FlySword` flight, then `PlantSword`: a never-breaking
  `Blocker` with the `Utsu_Sword` loop, which beams only spark off in `BurnCover`) and end with `ReclaimSword` (a
  `Dash` down and a `Chop` at the sword). While the sword is out it rests in `GameManager.utsusemiUnarmedIdle`, and
  the throw wind-up is `utsusemiSwordThrow`, both via `SetThrowFrames`. The beams fire from `Core`, its heart, except Midnight's left hand, which fires from its `Eye` (`Beam.fromEye`). Both draw with the three-layer `Beam` (`MakeBeam`/`DrawBeam`), stop on cover (`BeamEnd`)
  and hit the ronin where they cross his height (`HitIfCrossed`), leaving `GroundFire` patches burning along the ground; the hum is `Sfx.PlayLoop`/`Stop` on its own source. Fire burns the lace (`Burn`).
  Its effects are `Fx.Utsu*` clips (code shapes when missing); its clock is `Sfx.ClockTick`, with `HatWhirr`, `HatClang`, `GearGrind`, `LanternCharge`, `LanternBeam`, `MidnightChime`, `BladeChop`, `HourSlash`, `SwordWhirl` and `SwordPlant`.
- Wiring in `GameManager`: `BeginBoss` (intro, boss moon via `Backdrop.SetBossMoon`: blood for Benibana, steel for Utsusemi, music),
  `CreateBoss` (switch on `BossKind`), `VoiceFor` (portrait/close-up/voice clips), `OnBossSpoke`,
  `OnBossDamaged`, `OnBossDefeated` → `BossFell`. HP bar, subtitles and the intro cinematic are in `GameUI`
  (`ShowBossBar`, `SetBossHealth`, `ShowSubtitle`, `PlayBossIntro`). The intro is two beats on an opaque dark
  stage lit by a wash of the boss's colour (`ProceduralSprites.SoftGlow`): the full-body portrait fades up, then
  cross-fades to the close-up while a lower-third title (epithet, a rule, name; Title style) rises in, with the
  spoken line typed on beneath.
- Art built by `Ninja/Apply Boss Art` (`Editor/BossArtSetup.cs`).

**Adding a boss:** add a value to `BossKind` (`StageConfig.cs`) → subclass `Boss` (override `Title`, `Epithet`,
`Colour`, `Line`, the fight coroutine) → add cases to `GameManager.CreateBoss` and `VoiceFor` plus serialized
art/voice fields → set `boss` on a `StageConfig` → extend `BossArtSetup` for its frames.

**Adding a samurai kind:** add to `SamuraiKind` → branch its behaviour in `Samurai.Fire` (or elsewhere) →
give it art/points in `SamuraiArmy.ArtFor` / `PointsFor` → use it in a round's `rowKinds`.

## Environment

- `BlockerWall.cs` / `Blocker.cs` — courtyard cover (bushes 5 HP, lanterns 8 HP), `Rebuild(healthMultiplier)`
  (Glacier Wall uses the multiplier for ice blockers); sites are spread across the view minus `sideInset` at each side, so
  the outer ones stay off a map's edge scenery. A `Backdrop.Map` can bring its own `blockers` and
  `blockerPattern`; `BlockerWall.Use` switches to them for every later `Rebuild` (null goes back to the courtyard's).
  Each `BlockerType` can have extra `variants` (`Look`s), and every blocker built picks one at random (`PickLook`),
  so a row isn't all identical; the courtyard has three looks of each too (`BushB/C`, `LanternB/C`, wired by `Apply Backdrop And Blockers Only`). Map sets: Bamboo Road = roadside stones and bamboo clumps (`SBS`), Pass = boulders and Jizo statues (`RJR`), Benibana = maple shrubs and
  komainu (`MKM`), Utsusemi = gear piles and clock pillars (`GCG`), three looks each, art in `Art/Sprites/Blockers/`
  as `<Name>_0..2`, `<Name>B_0..2`, `<Name>C_0..2` (from PixelLab), wired by `Ninja/Apply Maps`.
- `Backdrop.cs` — moonlit courtyard, sakura petals, blood moon for boss fights; `Show(Map)` swaps to another map's
  ground and petals (the moon blends from the map's own tints). Maps: `Art/Sprites/Background/` —
  `BambooRoad_Stage1` (a dirt road with bamboo only at the edges), `Pass_Stage2` (the Pilgrim's Pass), `Arena_Benibana` (maple shrine),
  `Arena_Utsusemi` (clockwork temple floor), generated with PixelLab. `CourtyardGrass` with its bushes and lanterns
  is the fallback for any stage without a rounds map in `NinjaArtSetup.RoundsGrounds`, kept for a third stage.
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

## UI

- `GameUI.cs` — HUD (score, stage/round, samurai left, lives; no high score, so the top centre stays free for
  the boss bar), the stage/round card (`ShowBanner(overline, title, body, accent)`), boss bar, subtitles on a plate with the
  speaker's name, boss intro. Builds its pieces in code on the Canvas; `SetHudVisible` hides the HUD behind the
  title menu.
- **Text styles:** `GameUI.Style(text, TextStyle)` gives text its font and a shared outline-and-shadow material
  (`Title` = Cinzel Roman capitals, `GameUI.headerFont`; `Heading`/`Hud`/`Body`/`Hint` = Zen Kaku Gothic New
  Bold, `GameUI.bodyFont`). `GameUI.titleLogo`, when set, replaces the set title on the title menu.
  TTFs are turned into TMP fonts at runtime (`BuildFont`); a missing one falls back to the pixel font. Keep new
  text at 26px or more (1080p reference) and give it a style rather than raw TMP settings.
- **Shared "minimal ink" parts:** every screen except the paper title page is built from the same pieces, so new
  UI should reuse them rather than add framed panels. `GameUI.CreateInk` is a dark dry-brush stroke
  (`ProceduralSprites.InkStroke`) that paints on through its horizontal fill. `GameUI.CreateRule`/`AnimateRule`
  is the divider: a tapered brush line (`ProceduralSprites.BrushLine`) with the menus' diamond at its middle,
  drawn out from the centre. `GameUI.Reveal` makes a title line rise in as its letter spacing closes up. Key
  hints use `MenuUI.KeyCap`. The stage card and the boon screen use all four, and dark `MenuUI` pages use the ink stroke as the
  selection bar (paper pages keep a flat wash); the boss intro keeps its own rule and wash.
- `MenuUI.cs` — one full-screen menu page at a time: title, optional rich-text body, a list of
  `MenuUI.Item.Button` / `MenuUI.Item.Slider` rows. Keyboard (arrows/WASD, Enter/Space, Esc/Backspace), mouse
  (hover, click, drag sliders) and gamepad (d-pad/stick, South, East/Start); runs on unscaled time. Added to the
  Canvas by `GameManager.Start`.
  `ShowTitle` is the title screen's own light layout (paper page, `GameUI.titleLogo` centred high, a left-aligned
  menu column under it); `Show(..., paper: true)` draws a regular page ink-on-paper, which
  `GameManager.OnPaper` asks for while on the title (so Controls/Settings/Dev Tools match it; from pause they stay
  dark). `ShowFigure` loops a sprite on it (and, given a source pixel, floats `ProceduralSprites.Note` music
  notes up from it), used by `GameManager.ShowTitleRonin` to sit the
  ronin on his row at his in-game pixel scale (`GameManager.titleFluteFrames`, falling back to his idle).
- `BoonChoiceUI.cs` — see Boons. `SpriteAnimatorUI.cs` animates UI images in unscaled time.

## VFX, feel and audio

| What | Where |
|---|---|
| Animated pixel effects by id | `Fx.cs` (`Fx.Play`, ids as constants) + `FxLibrary` asset (`Art/Fx/FxLibrary.asset`) |
| Code-built glows, blasts, lightning bolts | `ElementFx.cs`, `ProceduralSprites.cs` |
| Debris bursts (petals, chips, sparks) | `Debris.cs` |
| Screen shake (read `CameraShake.Center`, never the camera, for layout) | `CameraShake.cs` |
| Fade to black between maps | `GameUI.FadeTo` (unscaled time) |
| White hit flash | `HitFlash.cs` + `Art/Fx/SpriteFlash.shader` |
| Parry hit-stop | `GameManager.HitStop` |
| Sound effects by id (pooled, ignores timeScale) | `Sfx.cs` + `SfxLibrary` asset (`Audio/SfxLibrary.asset`). A pool voice is free once its clip's audible part has played (`Sound.audible`, measured by Apply Audio, since the generated clips carry long silent tails); the march beat has its own source (`Sound.ownSource`) |
| Music crossfade and ducking | `Music.cs` |
| Player volume settings (`PlayerPrefs` `MusicVolume`, `SfxVolume`) | `Music.Volume` (applied every frame, so the slider works live), `Sfx.Volume` (also scales boss voice lines) |
| Boss voicelines | `GameManager.voice` AudioSource (its own `BossVoice` child, with the chorus/echo/distortion machine-voice filters `CreateMachineVoice` turns on for Utsusemi), clips in `Audio/Voice/`; `BossLine` full lines (intro/phase/defeat) vs. barks (hurt, stagger, move callouts) in `OnBossSpoke` |

Missing libraries or clips fall back gracefully (simple glows, silence), so the game runs before art/audio is wired.

## Sprites and animation

- `CharacterArt` ScriptableObject (`Art/Characters/*.asset`): clips `idle`, `attack`, `hit`, `death`, `parry`,
  `runLeft`, `runRight`, `walkUp` (the ronin walking away between maps) and death particles. Assets: Ronin (+ Fire/Water/Lightning), Ashigaru, Bushi, Taisho,
  Benibana, Utsusemi. A boss can rest in a different loop by overriding `Boss.IdleClip` (Utsusemi's bare-headed
  idle, `GameManager.utsusemiBareIdle`, while its hat is thrown, and its unarmed stance while its sword is planted).
- `SpriteAnimator.cs` plays frame lists on a SpriteRenderer (no Animator controllers).
- Raw frames live in `Art/Sprites/{Characters,Projectiles,Fx,Effects,Blockers,Background,UI}`;
  `Editor/PixelArtImporter.cs` imports them as point-filtered pixel sprites automatically.
- Boss intro paintings: `Art/Portraits/`. Fonts: `Art/Fonts/` (Cinzel headers, Zen Kaku Gothic New body, pixel fallback font).

## Editor menu commands (`Ninja/…`)

These rebuild assets/prefabs/scene wiring from the art on disk. Run them inside Unity; they're safe to re-run.

| Menu | Script | Does |
|---|---|---|
| Apply Sprite Art | `Editor/NinjaArtSetup.cs` | Rebuilds character/projectile prefabs, CharacterArt, blockers, backdrop |
| Apply Backdrop And Blockers Only | `Editor/NinjaArtSetup.cs` | Just the backdrop/blockers (no prefab ID churn) |
| Apply Maps | `Editor/NinjaArtSetup.cs` | Stage `roundsMap`/`bossMap` (`Background/Pass_Stage<n>`, `Background/Arena_<Boss>`, each with its blocker set from `MapBlockers`) and the ronin's `walkUp` (`Ronin_WalkNorth_*`); then re-run Apply Boon Art for the elemental walk |
| Apply Boon Art | `Editor/BoonArtSetup.cs` | Ronin spirit wisps cut from his base art, elemental ronin recolours, soul and boon icon wiring (`UI/Boons/<BoonId>`), each element's shuriken (`Projectiles/ShurikenArt_<suffix>`, PixelLab; recoloured from the base when missing), the ice statue shell |
| Apply Boss Art | `Editor/BossArtSetup.cs` | Boss CharacterArt, intro portraits and voice → GameManager |
| Apply UI Fonts | `Editor/UiSetup.cs` | Header font (Cinzel) → `GameUI.headerFont`, body font (Zen Kaku Gothic New Bold) → `GameUI.bodyFont`, `Art/Logo/RoninsRevenge.png` → `GameUI.titleLogo`, `Ronin_Flute_*` frames → `GameManager.titleFluteFrames` |
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
- `Tools/generate-sfx.mjs` / `Tools/generate-voice.mjs` — the newer sound effects, Benibana's mid-fight barks and all of Utsusemi's lines in its designed voice (`npm run sfx`, `npm run voice`).
- `README.md` — the assignment write-up (requirements, template fixes, features, balancing).
