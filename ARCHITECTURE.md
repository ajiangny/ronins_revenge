# Architecture

Shadow Blade is a Space Invaders remake (Unity 6000.6.6f1, URP, new Input System). One scene,
`Assets/Scenes/SampleScene.unity`. Every gameplay script sits flat in `Assets/`; editor tooling sits in
`Assets/Editor/`. This file is a map: where each system lives and how the pieces talk to each other.

## Game flow

`GameManager` owns the run and is the hub everything reports to.

```
Title ─Start─▶ Choose a journey ─new slot (flute intro)──────────────────────────────┐
                                └─saved slot──▶ Temple (Hub) ─gate─travel─▶ Stage intro ─▶ Round 1 ─reward─path─▶ Round 2 … (up to 5; boss path from round 3) ─travel─▶ Boss ─▶ stage boon + bead ─travel─▶ next stage / Victory
                                                    ▲         │  (soul caught → Choosing: boon screen, time paused)            │
                                                    │         │  (Esc / P / Start → Paused: pause menu, time paused)            │
                                                    └─reload, wakes in the pool ◀── ronin out of lives / army reaches ronin ─┘
```

- States: `GameManager.State { Title, Transition, Playing, Choosing, Paused, GameOver, Dialogue, Hub }`.
- Menu pages are built in `GameManager` (`ShowTitleMenu`, `ShowPauseMenu`, `ShowControls`, `ShowSettings`,
  `ShowEndMenu`) and shown by `MenuUI`. Sub-pages take a `back` callback so Controls/Settings return to
  whichever page opened them. `Pause` only fires in `Playing`; `Resume` always restores `timeScale = 1`
  (a pause can cut off a parry hit-stop).
- Content is data: `GameManager.stages` is a `StageConfig[]` (`StageConfig.cs`). Each stage has a name,
  `LevelConfig[] rounds` (`LevelConfig.cs`) and a `BossKind boss`. Four stages: "The Bamboo Road" (three
  rounds, then Benibana), "The Pilgrim's Pass" (three rounds with Shieldbearers and divers, then Utsusemi), "The
  Frozen Shrine" (three rounds with Yurei ghosts and divers from the first, then Shirogane) and "The Mountain Shrine"
  (three rounds of every kind plus Bellringers, then the final boss Kagetsune). The array is written in
  `GameManager.DefaultStages()`; the scene's serialized copy wins at runtime, and `Ninja/Apply Stages` copies the
  coded stages into it (and re-applies the maps), so add or retune a stage in code and re-run it.
- **Paths (Hades' chamber doors):** `EndRound` → `GatherDrops` (drops fly to Oni first; hearts heal him to full and the
  spare ones `Heart.Dissipate` into `SpareHeartPoints` × stage) → `GrantReward` (the round's `pendingReward`, a
  `PathReward`, first materializes mid-field as a `RewardSpirit` that hangs a moment and is pulled into him; then: soul → `OfferBoons`, bead → `OfferBead`, scroll → `OfferArts`, spring heals, Lost Melody pays Spirit Notes `noteCache` ×
  stage; one with no use falls back to notes) → `ChoosePath`: `RollPaths` picks `portalChoices` (+ Third Path)
  rewards, no two alike (souls 45, notes 20, beads 20, springs 10 while hurt, scrolls 5), plus the boss's from
  `BossPortalAfter` (`bossPortalAfterRound` + The Long Road). `RewardPath.Spawn` builds each (code-built glow, reward icon,
  TMP label). Each reward's icon is an 8-frame PixelLab loop from `RewardIcon`: the element's soul frames, `beadFrames`, `noteFrames` (`Effects/SpiritNotes_`; a procedural note without it), `springFrames`, `scrollFrames` and, for the boss's portal, `bossIcons` by the stage's boss (Benibana's kiseru and rosary, Utsusemi's hat, Kuon's head, Kagetsune's helm and bells), all wired by `Ninja/Apply Boon Art` from `Effects/<Name>_0..7`; a missing one is a disc. Oni walks his row with `Ronin.MoveOnly`; a path lights only while he's within `RewardPath.Reach` of it (between paths a "Walk under a path." prompt follows him, `GameUI.MovePathInfo`), and `GameUI.ShowPathInfo` hangs a tooltip under the lit one's name (`RewardPath.TooltipAnchor`: a brush rule in its colour, `RewardDescription` and the key hint over a soft shadow), and `ChooseKey` (E, Enter, pad A; not Space or a click, which slash)
  picks it: `RewardPath.Release` floats its reward away up the field (to come back as that round's reward) and `WalkOn` has him follow it into the dark and out at his place for the next round on the same map (cover and music kept), or
  the boss's goes on to `StageCleared`. A stage's first round is always a soul (`BeginRound`), whose banner names the
  reward. After `maxRounds` only the boss is left. Rounds past `Stage.rounds` come from `ExtraRound`: the last round,
  `extraRoundSpeedUp`/`extraRoundFireCut` per extra round, +5% aimed, a Taisho top row, one more row from the second.
- **Run log (dev tools):** `RunLog` appends a line per round and boss (time, hits, lives, kills by source with the effect share, frozen time, the army's closest approach), the portals and boons taken and the run's end to `runlog.txt` in `Application.persistentDataPath`; F8 prints the current run.
- Key methods: `StartGame`, `BeginRound`, `OnArmyDefeated` → `EndRound` → `ChoosePath` / `StageCleared` → `BeginBoss` / `OfferStageBoon`,
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
- **Title → game:** START runs `TitleToGame`: `MenuUI.LeaveTitle` fades the logo and rows (input off), the title
  figure plays `titleStandFrames` once (`MenuUI.PlayFigureOnce` / `SpriteAnimatorUI.PlayOnce`), the world ronin is
  `PlaceAt` the title seat (`TitleSeat`) and `WalkTo(Home, titleWalkSpeed, runRight, …)` while `MenuUI.FadePaper`
  fades the paper off stage 1's map; until the page closes the UI figure tracks him (`PlaceFigure` with
  `Ronin.CurrentFrame`) so the handoff is seamless. Any key skips (`Ronin.FinishWalk`). Then `StartGame` as before;
  Restart, Play Again and DevJump still call `StartGame` directly.
- **The ending:** the last stage's boss is Kagetsune; `BossFell` skips the boon for him and runs `PlayDawn(throne)`:
  the boss moon lifts, `ShiroganeFreed` (code-built, `freedKneel`/`freedBow`/`freedIdle`, falling back to her
  boss art) kneels her soul mid-field chained (`Fx.KageChain` via `Kagetsune.Lay`) to her bell (`Fx.KageBell`); the
  ronin walks up and `Swing`s, `Snap` bursts the chain and rings the bell once before it fades, she `Rise`s (her
  kneel reversed, teal to silver) and speaks through `Say` (`TuneVoice(Shirogane)`, `shiroganeFreedLine`), then
  `Backdrop.SetDawn` warms the sky (`DawnSky`, a `ProceduralSprites.FadeDown` gradient, and a `DawnSun` glow), she
  says farewell (`shiroganeFarewellLine`), he holds his flute pose (`Ronin.Pose` with `titleFluteFrames`), she
  `Bow`s and `Dissolve`s and `SecondWisp` rises from her into his shoulder, a "Dawn" card shows, then
  `EndGame("DAWN", …)`. Kagetsune's `RisingSoul`s drive themselves (`FreedSoul`) so they finish after he is gone. Dying to him
  ends on his `BossLine.KilledOni` line instead of the plain defeat text.
- Restart reloads the scene with `skipTitle` set (`Reload(true)`); Quit to Title / Title reload without it. A death
  (`EndGame` with `cleared` false) has no end screen: `FallToTemple` waits `deathToTempleTime` for the killer's last
  word, fades to black and `ReloadToSafeArea(true)`; Victory's RETURN TO TEMPLE and the pause menu's RETURN TO TEMPLE
  (gives up the run, not counted as a death) do the same.
- **Dev tools (Editor and development builds, `Debug.isDebugBuild`, or release builds while `devToolsInRelease` is on; both via `DevToolsOn`):** a DEV TOOLS page (`ShowDevMenu`) on the
  title and pause menus, or F3, is a two-column grid (`MenuUI.Show(…, gridColumns: 2)`) with a row per stage, its round 1 on the left and its boss on the right; `DevJump` stores the pick in statics,
  reloads the scene and `StartGame` hands it to `StartAt`, which shows that fight's map and begins it. God mode
  (`SetGodMode`, F4, kept across reloads) sets `Ronin.Invincible` so `TakeHit` ignores everything, sweeps away an
  army that lands instead of ending the run, and shows a gold tag over the lives (`GameUI.SetGodMode`).
  `DevHotkeys`: F1 kills the current army, F2 jumps to the last stage's boss (or last round if it has none), F5 drops a soul at the ronin (`SpawnSoul`, elements in turn), F6 a technique scroll (`SpawnScroll`), F7 logs `Dialogue.Explain` for the stage's boss and Oni. The page also has +1000 NOTES and RESET SAVE.

- **Poguru's tutorial (`Tutorial.cs`, a component `GameManager.Start` adds):** replaces Stage I, Round 1 of a run when
  `Tutorial.ShouldOffer(save)` (`tutorialReplay`, or a journey with no runs that hasn't `tutorialSettled`), asked in
  `StartGame` before `Save.BeginRun` and armed after the dev-jump return (`Arm`), so a jump never offers it. Hooks:
  `BeginRound` → `Offer` (after `ClearProjectiles`, before the army: Poguru pops out, `Poguru_Tut_Wake`/`_Again`, then a
  `MenuUI` page DO YOU REMEMBER? with TUTORIAL / SKIP, Esc skipping; TUTORIAL sets `tutorialReplay` so a death offers it
  again and sets `Ronin.Shielded` (no hit counts until the spear), SKIP settles it) → `Drills` (taken; before
  `blockers.Rebuild`, with the cover cleared so nothing blocks his blows: five `TrainingSoldier`s (`TrainingSoldier.cs`,
  code-built `IHittable`s, `FootSoldierArt`/`SpearmanArt`) march in, their lines hung over them (`GameManager.SayLine`
  with `beside`); an archer shoots down Oni's column, two chargers run at him in a file, a spearman throws a
  `SpearSprite` spear, and a coward trembles at the back. Each lesson is `Lesson(move, slot, yell, moment)`: at its
  moment `GameManager.HoldForLesson` stops time (`State.Dialogue`; it hands back whichever state it found), the HUD slots
  are lifted onto the `TutorialLayer`, Poguru's pointer bobs beside the move's slot with its keys as a
  `GameUI.CreateKeyRow` (key caps, mouse buttons drawn by `ProceduralSprites.Mouse`), his yell is voiced only and shown
  with the keys beside Oni (on the side away from the shot, or the other when the HUD side lacks room), and
  `Ronin.Allowed` lets only that move fire: Ronin still reads input with time stopped, so the real press fires the real
  move (`Slashed`, `ShurikenThrown`, `ParryStarted`; `Tutorial.Press` for a test harness). The slash breaks the arrow,
  then he cuts the archer down; the shuriken goes through both chargers (Poguru headbutts any that get through back up
  to try again); the parried spear skims past the spearman (`RiposteTarget` a point beside his head, the spearman and
  coward `Untouchable`), who flees with the coward for the army) → `Backup` (after `HideBanner`, round 0:
  `Poguru_Tut_Backup`) → `RoundStarted` (after `army.Advance`: `Poguru.Guard` for the whole round, never a break;
  `Poguru.Anger` builds with the blocks (redder, shaking, steam) and barks hang over him at 1/3/6/10/15/20/30/45 blocks,
  then every 15) → either the army falls, or `SamuraiArmy.Holding` (the front rank reaches the cover) sets off `Rage`:
  he rises over Oni and a ring of his soul-fire sweeps the field, `Kill(KillSource.Effect)` on every samurai it passes →
  `OnArmyDefeated` runs `TutorialRoundEnd` instead of `EndRound` for round 0: `SpearAndHeart` (still `Playing`, so the
  hit counts) pops every arrow, plays `Poguru_Tut_Win` or, after his rage, `Poguru_Tut_Raged` at Oni, lifts the shield.
  Nothing drops while the tutorial is active (`MaybeDropSoul`, `MaybeDropScroll`, `OnBlockerBroken`, the kill heart).
  Then the spear (skipped if `MaxLives < 2`):
  `EnsureLives(2)`, `SpawnSpear` (unbreakable, unparryable,
  homing) from the far top corner while he dashes into its path at 0.35× time, ghosts, and `ForceHit` lands it through
  god mode and blink if it hasn't in 2.5 s; then `Poguru_Tut_Hearts` (lives row lifted onto the overlay and pointed
  at), `SpawnHeart` on Oni's row, waits for it → `GrantReward` → `RewardShown` (a talk while the `RewardSpirit`
  hangs) → `ShowBoons` → `BoonsShown` (once; `BoonChoiceUI.InputLocked` while a lightly dimmed `keepHud` talk explains
  the cards) → `ChoosePath` → `PathsShown` (once) → round 1's `BeginRound` → `Finish` (`Poguru_Tut_Done`, settle,
  back in the wisp). `StartAt` and `EndGame` call `Abort` (back in the wisp, nothing settled). GameManager's public
  block for it: `PlayTalk`, `Bark`, `SayLine`, `HoldForLesson`, `ForceHit`, `SpawnHeart`, `SpawnSpear`, `EnsureLives`,
  `FootSoldierArt`, `SpearmanArt`, `SpearSprite`, `ArrowPrefab`, `Army`, and `Poguru`, `Menu`,
  `BoonChoice`, `Scene`, `Ui`, `Lives`, `MaxLives`.
- **Poguru (`Poguru.cs`):** Oni's companion in his left wisp, code-built like `ShiroganeFreed`: drawn at
  `Ronin.ArtPixelSize`, sorted at `SpriteOrder + 2` with a `SoftGlow`, animated on unscaled time from
  `GameManager.poguruArt` (`PoguruArt`: idle, talk, pop, bonk, annoyed, point, gasp, cheer), a teal glow with two
  white disc eyes without it. `Mode { Hidden, Popping, Hover, Guard, Returning, Perch }`; `PopOut` swells him out of
  `Ronin.WispPoint(0)` (that wisp hidden by `Ronin.SetWispHidden` while he's out), `ReturnToWisp`, `Hover` at Oni's
  shoulder, `DashTo`, `PerchAt` (his nest), `Ghost`, `Talk`, `Play`. Guarding, each frame he finds the arrow due to cross
  `Ronin.TopY` on Oni within 0.35 s (never spears, reflectables, rising shots, or anything while Oni parries), dashes
  at 14 u/s and `Arrow.Pop`s it, raising `Blocked(count)`. He has no collider. **Barks** (`PoguruBark(evt, chance,
  boss)`, every guard before `Dialogue.Pick`: no tutorial, no full line playing, he isn't out, state Playing /
  Transition / Hub): `PoguruRound` 20% after `army.Advance`, `PoguruSoul` after the run's first boon (`TakeBoon`),
  `PoguruBoss` in `BossFell` after `Save.BossFelled` (not Kagetsune), `PoguruWake` after `HubReturn` (`WakeTalk`);
  `SayAsPoguru` voices one line through `poguruVoice` with a subtitle.

## Safe areas: the temple between runs

Hades' House of Hades. Oni's home between runs is the abandoned temple on the hill; it is the **only** place he walks
in every direction (`Ronin.Roaming`), everywhere else he keeps to his row.

| What | Where |
|---|---|
| Save slots (Hades' profiles): `Save.Slots` = 3 keys `Save1..3`, the slot in use in `SaveSlot` (`Save.Use`, `Exists`, `Peek`, `Delete`). A pre-slot `"Save"` key moves into slot one once (`MoveLegacySave`) | `SaveData.cs` |
| Title START → `ShowSlots` (each slot's runs, notes, deaths, dawns; ERASE A JOURNEY → `ConfirmErase`). `ChooseSlot`: an empty slot is a new game and plays the flute intro straight into the first run (`TitleToGame`), as Hades' first run starts in Tartarus; a saved slot fades the paper off Oni standing in the temple (`TitleToSafeArea`) | `GameManager` |
| A safe area is data: `SafeArea.Config` (name, `Backdrop.Map`, `floor` and `wakeSpot` as fractions of the map, `Station[]`, each a `StationKind`, label, spot, prop art, `propAt` (where its prop stands, map px), `footprint`, `StationFx`, `onMap`, reach; the pool's `poolCentre`/`poolRadii` in map px with its cut-out `poolWater`/`poolRimFront` layers and `poolSouls` frames; `Lamp[]` flames by map px). Map pixels count from the top-left, as in the art; `Ninja/Apply Maps` copies the layout from `DefaultSafeAreas()` into the scene, since the scene's copy wins. `GameManager.safeAreas` (defaults in `DefaultSafeAreas()`, art from `Ninja/Apply Maps`) holds them; only the first is used so far. To add one, add a Config (and its map/props to `SafeAreaGrounds`/`StationArt` in `NinjaArtSetup`) and pick it in `CurrentSafeArea` | `SafeArea.cs`, `GameManager` |
| `SafeArea.Build` measures in map space (`Backdrop.GroundBounds`, worked out from the camera so it's right before `Backdrop.Start`, as after a death's scene reload; `MapToWorld`/`WorldToMap`, `MapPixel`), so props and layers match the map's pixels whatever the window crops. It lays each prop with its lowest drawn pixel (its tight mesh) on `propAt`, a contact shadow, and a world-space prompt; placeholders when art is missing (a pool disc, tinted blocks). Each frame a prop is drawn over him when his feet are above its base (walking behind it), and `Blocks` (the footprints, plus the config's `walls`: map-px rectangles for ground painted on the map, the dais's lanterns and raised edges so he takes its steps, the platform's edges so he takes its side step, the grave nook) become `Ronin.RoamBlocks`, which he slides along. The map's ghost-fires are lifted off it and drawn flickering (`spirits` feet in map px, `spiritFrames` = `Fx/Temple_GhostFire_*`), out of step with each other and over him when he stands behind one. Station effects: incense smoke (`Incense`), a pixel glint on the blades (`Glint`, `ProceduralSprites.Glint`), a breathing glow (`Glow`); lamps light the floor around them and flicker (Perlin). All code-built, so they work without art. `Near(feet)` finds the station in reach, `ShowPrompt` floats "E  LABEL" over it. Spots are where his **feet** go; `GameManager.BodyAt` converts for `PlaceAt`/`WalkTo` and the roam clamp | `SafeArea.cs` |
| The gate (`SafeArea.BuildGate`/`SwingGate`, when the config has `gateDoors`): its two leaves are cut from the map's own pixels (`Sprite.Create` on the map texture, hinged at their outer edges, under the moon's shade at `GateOrder`) over a dark passage, and swing open (narrowing and darkening, `Sfx.GateOpen`/`GateClose`) while his feet are near the Exit station or `HoldGateOpen` is set. `gateOver` (map-px rects: the beam and roof, raised far off the map's top, and the posts) are `SpriteMask`s on his order and his wisps' alone, biting once his feet pass the doorway's threshold (`Ronin.Cover`), so the map's own beam hides him as he walks out under it | `SafeArea.cs`, `Ronin.Cover` |
| The spirit pool (`SoulPool`, built by `SafeArea` when the config has a pool): light dashes drift over the water (`ProceduralSprites.Caustic`), rings spread on it (`ProceduralSprites.Ring`, one-pixel ellipses stepped through radii) and souls rise off it and off the flagstones around the temple (`poolSouls`, a soft glow without them; in front of him when they rise from nearer the viewer than his feet), the surface clipped to the water by a `SpriteMask` of `poolWater`. Oni wades when his feet are inside the water ellipse: `Ronin.Wade(depth)` masks his sprite below the waterline (ankle- to shin-deep), the near rim (`poolRimFront`) is a mask on his order alone, so the map's own rim hides his shins at the moon-shaded brightness (drawing it over him lifted it above the atmosphere's shade), a ring sits at the waterline, rings follow his steps, and stepping in or out splashes (`Sfx.PoolSplash`, `Sfx.WaterStep`, drops). `Sfx.PoolAmbience` loops while the temple is up. `Ninja/Temple/Cut Pool Layers` (`Editor/TempleLayers.cs`) cuts the water and near rim out of `Temple_Hub.png` | `SoulPool.cs`, `Ronin.Wade` |
| Entering: `EnterSafeArea(wake)` hides the HUD, clears the cover (`BlockerWall.Clear`), plays the title theme; on a wake he is placed in the pool as the dark lifts, the soul pillar flares and he climbs out to `SafeArea.PoolExit` (past the far rim), `NoteTally` shows what the run earned and `HubReturn` plays Oni's line. Then `State.Hub`, `Ronin.Roaming` on, `RoamArea` = the floor | `GameManager` |
| In the hub, `UpdateSafeArea`: E / Enter / Space / gamepad A uses the station in reach, Esc opens `ShowSafeAreaPause`; a station's page sets `stationOpen` and freezes him (`OpenStation` / `CloseStation`). A station can start sealed: `Station.sealedArt` is drawn while `isSealed` (set in `BuildSafeArea`), and `SafeArea.Unseal` swaps in its `art` in place and lets its `glowColour` Glow come up. Stations: **Oaths** (the oath stone, lower left: `SEALED STONE` until `Oaths.Unlocked` (a dawn), its chains break once in `UnsealOaths` (`SaveData.oathsUnsealed`), then `ShowOaths`), **Exit** (the gate, `SetOut`: holds its doors open (`SafeArea.HoldGateOpen`), walks up to it, waits for `GateOpened`, `TravelTo` stage 1 with `inDark` removing the temple, `StartGame`), **FluteSongs** (`ShowSongs`, the Mirror of Night), **Memorial** (`ShowRecords`: runs, dawns, deaths, each boss met by kills both ways, unmet ones worn smooth; a cut-down Codex), **WeaponRack** (`ShowRack`, Hades' Infernal Arms: katana or kunai, `SaveData.weapon` → `Ronin.CurrentWeapon` at `StartGame`), **Poguru** (his nest, right of the pool: `ShowPoguru` with TALK once a visit (`poguruTalked`, a `PoguruTalk` scene through `PlayTalk` with `keepHud` so the HUD stays hidden), TIPS (`ShowTips`) and REPLAY TUTORIAL ON/OFF (`SaveData.tutorialReplay`)). Poguru perches on it (`NestPoint`, `SafeArea.TopOf`) while Oni is in the temple: at once when he loads in, after his wake line when he wakes; `SetOut` sends him back into the wisp | `GameManager` |
| Roaming movement: WASD/arrows in 2D at `RoamSpeedScale` of his speed, no attacks; clips by the nearest of eight directions (`Ronin.WalkClip`): `runLeft`/`runRight` sideways, `walkUp`/`walkDown` up and down, and `walkUpLeft`/`walkUpRight`/`walkDownLeft`/`walkDownRight` on the diagonals; a missing diagonal falls back to the run, a missing up/down to the `idle` with a step bob. Standing still while roaming he faces us (`idleDown`, `Ronin_IdleSouth_*`); in a fight his `idle` shows his back. `WalkTo` faces the way he's going the same way | `Ronin.Roam`, `Ronin.UpdateLoopClip`, `Ronin.WalkClip` |
| Art: `Background/Temple_Hub.png` (PixelLab create_image_pro 688×384 with the first temple map as style and layout reference, its original spirit pool pasted back pixel for pixel: gate top centre, pool bottom centre, a stone dais with toro lanterns on the left, a kerbed grave nook upper right, a plank platform under a broken eave lower right), `Background/Temple_PoolWater`/`Temple_PoolRimFront` (cut from it), `Props/Temple_FluteAltar`, `Temple_Rack` (64×64), `Temple_Memorial` (96×64), `Temple_PoguruNest` (48×48, an offering bowl with a red cushion), `Temple_OathStone_Sealed` and `Temple_OathStone` (64×64, the chained stone and a PixelLab state of it with the chains snapped at its foot and the vows glowing; same canvas, so it stands on the sealed one's base in both) (Pro Flash objects styled on map crops), `Fx/Temple_Soul_*` (an 8-frame upright soul with beady eyes); `Fx/Temple_GhostFire_*` (the map's own ghost-fire, cut out and animated with PixelLab v3; the painted ones are inpainted away, and the two in the pool are gone so it holds only the souls); mood `Temple_Hub` in `NinjaArtSetup.Moods`; sounds `WaterStep`, `PoolSplash`, `PoolAmbience` (`npm run sfx`) | `Ninja/Apply Maps`, `Ninja/Apply Audio` |

## Across runs: save, dialogue, Flute Songs

Hades' loop: what happened in earlier runs changes what is said and how strong Oni starts. Research and numbers in
[Story/DIALOGUE_RESEARCH.md](Story/DIALOGUE_RESEARCH.md) and [ECONOMY.md](ECONOMY.md).

| What | Where |
|---|---|
| The save: runs started and cleared, deaths per boss (`deathsTo`) and wins (`winsVs`) by `BossKind`, how the last run ended (`lastRunCleared`, `lastKiller`), `hubPending`, conversations heard (`seen`/`seenRun`, the run each was last heard in), the repeatables' `shuffleBag`, `embers` (the Spirit Notes, named for their old Soul Embers so saves keep them), song `ranks` and `songsOff`, and Poguru's tutorial flags `tutorialSettled`/`tutorialReplay`. One JSON string per save slot in `PlayerPrefs` (`"Save1"`..`"Save3"`, see Safe areas), cached in `Save.Data` (reset on entering Play mode, which has no domain reload) | `SaveData.cs` (`Save.BeginRun` in `StartGame`, `Save.EndRun(cleared, killer)` in `EndGame`, `Save.BossFelled` in `BossFell`, `Save.Write` also in `Reload`) |
| Reactive dialogue: a `Conversation` (id, speaker, `DialogueEvent`, `Tier` Story / Reactive / Repeatable, `Req` requirements, `Cooldown` in runs, and either `Lines` or `BuiltIn`, the boss's own `Line()` and clip). `Dialogue.Pick` filters by `Blocked` (heard, cooling down, a `Req` failing), takes the best tier with anything left, the first Story/Reactive one written or a Repeatable from the bag, and marks it heard. No dice in eligibility, so `Explain` is repeatable. Requirements are helpers (`Seen`, `NotSeen`, `LastKiller`, `Deaths`, `DeathsTo`, `WinsVs`, `OfElement`, `Holds`, `HoldsDuo`, `Felled` (the boss in `DialogueContext.Boss`), `Met`); `Fill` writes `{Deaths}` in words. Poguru's events: `Tutorial` (never picked: `Dialogue.Find(id, mark)` plays them by id and marks them heard), `PoguruRound`, `PoguruSoul`, `PoguruBoss`, `PoguruWake`, `PoguruTalk` | `Dialogue.cs` (`Dialogue.All` holds every conversation; keep it in step with `Story/`) |
| Where they play: `BeginBoss` picks `BossIntro`, `PlayPhaseCard` picks `BossPhase`, `BossFell` picks `BossDefeat` (none written plays the boss's own line, `Unwritten`), and `OfferBoons` picks the soul's `SoulPickup` (`SoulOf(element)`) on that element's first screen of the run (`metThisRun`) and shows the cards once its scene ends (`GreetThenChoose` → `ShowBoons`). All four play through `PlayScene` (below). `OnRoninHit` picks `KilledOni` from the killing boss (a subtitle; its line is also the end screen text); `EnterSafeArea` runs `HubReturn` when `hubPending`, as he climbs out of the temple's pool (a plain subtitle; `Say` still frames his close-up beside his words if one plays on the title page). `PlayConversation` → `Say` plays lines as subtitles, voiced if `VoiceLibrary` has `Conversation.VoiceId(i)`, and marks them full lines so barks wait | `GameManager` |
| Talk scenes (Hades-style): `PlayScene(conversation)` is `PlayTalk(conversation, default)`; `PlayTalk(said, TalkOptions { keepHud, dim, poguruRight, onLine })` builds the lines (`LinesOf`), and with `keepHud` leaves the HUD exactly as it was (the tutorial's pointing, talks over the boon screen, the temple), `dim` lightens the field, `poguruRight` stands Poguru on the right and Oni on the left, and `onLine(i)` runs as each line comes up (`DialogueScene.Line.shown`). `DialogueScene.Overlay` sits over the plate and figures for what a talk lifts out of the dark, and `Advance()` steps a line for test harnesses. It waits out a hit-stop, sets `State.Dialogue` and `timeScale` 0, hides the HUD and boss bar (`GameUI.HideForScene`), and turns the conversation into `DialogueScene.Line`s: a `BuiltIn` boss line first in its own clip (`BossVoice.Take`), then the written lines with their `VoiceLibrary` clips (a built-in line counts as line 0 of `VoiceId`). `SpeakerFor(name)` stands each speaker: Oni at the right (`oniCloseup`), the boss at the left with its `VoiceFor` close-up (its phase close-up and epithet once `SecondPhase`, drawn at `SceneArtScale` of the card size), a soul at the left with its painting (`akaganeCloseup`…, framed by its eye point) or its soul frames. `DialogueScene.Play` dims the field, lights the speaker and drops the listener back, masks each half (feathered where a painting's canvas edge would show, cut edges pushed off screen), types each line under an ink name tab on the speaker's side, and advances on Space / Enter / click / South after a 0.3 s guard | `DialogueScene.cs`, `GameManager` |
| Voices for written lines: `VoiceLibrary` (every clip in `Audio/Voice` by file name), built by `Ninja/Apply Voice Lines`, which also logs the lines still unvoiced. Oni's lines (hill, answers, soul meetings, `Oni_NotYet`) play through `GameManager.oniVoice`, a filter-free source beside `BossVoice`, the souls' through `soulVoice` (`CreateSoulVoice`: a light chorus and short echo, their shared coffin sound) and Poguru's through `poguruVoice` (`CreatePoguruVoice`, a light quick chorus); `VoiceOf(speaker)` picks the source per line for `DialogueScene.Line.source` and `Say`. An unvoiced intro line stays on the card long enough to read | `VoiceLibrary.cs`, `Editor/VoiceSetup.cs`, `GameUI.PlayBossIntro` |
| The souls behind the boons (Akagane, Mizore, Hibiki): their `SoulPickup` conversations are in `Dialogue.All`; Duo, Legendary, jealousy and boss reactions are written but not wired yet | [Story/Souls.md](Story/Souls.md) |
| Spirit Notes (`Upgrades.PerSamurai`, `PerScout`, `ForBoss`) go into the save as they're earned (`GameManager.Earn`, `Upgrades.AddNotes`); the end screen tallies `notesThisRun` | `GameManager` |
| Flute Songs: `UpgradeId` (append-only: saves index ranks by it; `Song.Retired` hides one and `RefundRetired` returns its notes, as for Soul Pull), `Song` (name, max rank, base cost, `Effect` text), `Price` (base × rank + ⌊10 × 1.1^ranks bought⌋), `Buy`, `Spent`/`RefundAll`, `Toggle` (maxed songs only), `Rank` (0 while off). Applied once a run, on top of the scene values: `Ronin.LearnSongs` (parry window, speed, swing, shuriken, missed parry, invulnerability), and in `GameManager` Iron Will's life (`StartGame`), Fate's Thread (`redrawsLeft`, `BoonChoiceUI.OfferRedraw`, R) and Banishing Bell (`banishesLeft`, `OfferBanish`, B; the run's `banished` set is skipped by `Boons.Draw`), Third Path (`RollPortals`), Pond's Blessing (`BeadThenNextStage`), Ancestral Lore (`GrantReward`) and Second Dawn (`OnRoninHit`, `dawnUsed`) | `Upgrades.cs` |
| Oaths (Hades' Pact of Punishment), sworn at the temple's oath stone after the first dawn: `OathId`, `Oath`, `Oaths.Rank` (0 while `!Unlocked`), `Heat`, `NoteMultiplier` (applied in `GameManager.Earn`, fractions carried in `noteFraction`), `Cycle`, `ClaimBounty` (in `EndGame` on a dawn, `SaveData.bestHeatCleared`). Effects: `GameManager.Sworn` (march, arrows) on each round's config copy (`LevelConfig.Clone`), `Boss.HealthScale` (set in `StartGame`), `BossPortalAfter` (Long Road), `RollPortals`/`OfferStageBoon` (Faded Spirits), `StartGame` (Frail Body). Page: `ShowOaths` | `Oaths.cs`, `GameManager` |
| The shop page: `ShowSongs`, a two-column `MenuUI` table (any `Item.detail`/`pips` makes a page a table: shorter, wider cells, labels left-aligned, the price right-aligned and the ranks as diamond pips in their own column) with a `MenuUI.Item.note` line for the selected song just under the rows. Opened at the temple's flute altar | `GameManager`, `MenuUI` |
| Resources, Hades-style: `ResourceTray` (built in `GameManager.Start`, its own canvas sorted over the menu pages) stacks a count and its looping icon per resource in the bottom-right corner while `state == State.Hub`, station pages included. Each is one `resources.Add(name, count, icon)` (Spirit Notes: `Save.Data.embers`, `GameManager.NoteIcon`); one at zero stays hidden until found, and a count that changes swells for a moment | `ResourceTray.cs`, `GameManager` |

## Player combat — the Ronin

| What | Where |
|---|---|
| Movement, input, screen clamp, cooldowns, lives/invulnerability | `Ronin.cs` (reads `Keyboard.current` directly) |
| Kindling: every Nth swing `Ronin.Attack` calls `ThrowFireball` instead (Fireball fx frames at twice the riposte size, `KillSource.Slash`, `CutsArrows`); its hit raises `Ronin.FireballImpact` → `BoonEffects.OnFireballImpact`, a `KindlingRadius` blast | `Ronin`, `BoonEffects` |
| Sword slash (Space / Left Click), reshaped by the Sword Arts | `Ronin.Attack` → `Ronin.Slash` → `Ronin.Fan` → `LaunchSlash` → `SlashWave.prefab` (a `Kunai` with a wide hitbox, `KillSource.Slash`). Piercing Slash sets the pierce count and `Kunai.CutsArrows`, Shadow Clone (`Ronin.UpdateShadowClone`, a flipped, darkened copy of his frame mirrored across `CameraShake.Center` by `MirrorX`, no collider) sends a second slash from the mirrored muzzle; Split Slash forks it with `Kunai.SplitAfter` → `Ronin.SplitOff`; Echo Slash adds a homing ghost (`Kunai.Home(RiposteTarget)`) |
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
| What the ronin holds and the stats they add up to | `RoninLoadout.cs` (`Take`, `Held`, `Value`, `DominantElement`) |
| Souls offer only boons not held; leveling is the **Spirit Bead** (Hades' Pom): `Boons.DrawBead` offers up to 3 held Normal, non-Instant, below-Epic boons a rarity up (`BoonOffer.IsUpgrade`; `RoninLoadout.Take` raises without recounting the element), shown by `BoonChoiceUI.ShowBead` (`Boons.BeadColor`). From bead portals and after each stage boon (`BeadThenNextStage`); nothing to raise pays `BeadNotes` × stage | `Boon.cs`, `GameManager.OfferBead` |
| Effects in play (blasts, encase, freeze, lightning, boss versions) | `BoonEffects.cs` (`OnKill`, `OnBossHit`, `OnShurikenThrown`, `OnParried`, `OnRiposteImpact`, `OnStatueShattered`). Storm Blade arcs to a random samurai within `StormBladeRange`. Thunder Shuriken speeds the shuriken up (`Ronin.ThunderShurikenSpeed`) and `OnShurikenThrown` adds `ShurikenStorm` to it, which gives each samurai or boss it passes within `ShurikenArcRadius` one roll to be arced. Sideways lightning (Storm Blade, Thunder Riposte, Thunder Shuriken, Superconductor) uses `Chain` → `ElementFx.Chain`, an arc between the two samurai; sky strikes use `ElementFx.Bolt` and `Fx.LightningStrike` |
| Ice shell: `Samurai.AddIceShell` lays `SamuraiArmy.IceShell` (`Art/Sprites/Effects/IceShell.png`, PixelLab, wired by `Ninja/Apply Boon Art`) over ice statues only (living encased samurai keep just the tint and halo), as a child of the body so it melts with it; without the art only the ice tint and halo show | `Samurai`, `IceStatue` |
| Ice statues: a samurai killed while `Encased` (any source) leaves the army as an `IceStatue` (code-built from its last frame, on the Samurai layer). The ronin's next non-`Effect` hit raises `IceStatue.Shattered` → `BoonEffects.OnStatueShattered`, which sends `Boons.IceShardsAt` (Frost Blade's `ExtraValues`) homing shards (`Ronin.LaunchIceShard`, `KillSource.Effect`, non-piercing). Shards and Frozen Riposte shards set `Kunai.FreezeOnHit`, encasing before they damage, so their kills freeze too. Left unhit, a statue melts (`SolidTime` 3 s, then `Melt` over `MeltTime` 1.6 s: slump, meltwater tint, drips and a puddle, all code-drawn) and can't be burst past `BreakableUntil`. Statues crumble in `EndRound` (`CrumbleAll`) | `IceStatue.cs`, `Samurai.Kill`, `Kunai`, `Ronin` |
| Sword Arts (Hades' Daedalus Hammer): `BoonKind.Art` boons with no element (`Boons.ArtColor` steel), never in soul draws, offered by `Boons.DrawArts` from a technique scroll. `GameManager.MaybeDropScroll` (`scrollChanceFromScout` 40%, `scrollChancePerKill` 0.5%, `maxScrollsPerRun` 2, one at a time) → `TechniqueScroll` → `OnScrollCollected` / `GatherDrops` → `OfferArts` → `BoonChoiceUI.ShowArts` (holding out `scrollPainted`, or the pixel `scrollFrames` without it; the pickup loops `scrollFrames`). Arts join the passives (`RoninLoadout.Take`) without counting toward elements | `Boon.cs`, `TechniqueScroll.cs`, `GameManager`, `Ronin`, `Kunai` |
| Pickups: souls and hearts fall to the ronin's row and wait there; at a round's end (`GameManager.EndRound`, and `BossFell`) `GameManager.GatherDrops` flies every one he can use to him (`Pickup.GatherAll`, `Pickup.CanCollect`) and offers a soul caught that way | `Pickup.cs` (base: fall, rest, magnet, collect), `Soul.cs`, `Heart.cs`, `TechniqueScroll.cs` |
| Lives are health: `GameManager.lives` out of `maxLives`, which only Tidal Renewal raises (to `Boons.MaxLives`). The HUD shows the life heart (`UI/LifeHeart`, a crimson heart in teal soul-fire) per life and the same heart tinted `SpentLife` per lost one (`GameUI.SetLives`); dropped hearts draw it `Heart.Size` across | `GameManager`, `GameUI` |
| Heart drops (`heartDropChance`, 20%) heal one lost life; at full health a heart can't be taken (`GameManager.CanHeal`) and waits on his row, surviving `ClearProjectiles`, until the round's end, where `GatherDrops` heals him with it or fades it into points (`Heart.Dissipate`). A fallen samurai drops one too, rarely (`killHeartChance`, 0.5%): only to a hit that isn't a boon effect, only while he's hurt, at most once a stage (`killHeartThisStage`), as Poguru promises in the tutorial | `Blocker.Broken` (hits only, not trampling or a boss razing) → `GameManager.OnBlockerBroken` / `OnHeartCollected`; `OnSamuraiKilled`; art is `GameUI.LifeIcon` |
| Soul drops: souls are mostly path rewards; a downed scout drops one (`scoutSoulChance`, 20%) and a fallen samurai rarely (`killSoulChance` 1%, at most `killSoulsPerStage` a stage), one at a time | `Soul.cs`; drop logic in `GameManager.MaybeDropSoul`. Until the first soul of a run is caught, `Soul.ShowHint` adds the bobbing `GameManager.soulHintArrow` (`UI/SoulArrow`, wired by Apply Boon Art) |
| Boon pick screen + HUD slot medallions (left-edge column; hold Tab for key/name tips). Any offer can be turned down: X (or a click on its hint link) arms `Reject` and a second press confirms, calling the pick back with `BoonChoiceUI.Rejected` (-1), which `GameManager.TakeOrReject` logs and takes nothing for; changing the selection, a redraw or a banish disarms it. The screen is painted, not pixel art: Oni's upper body (`Portraits/Oni_Boon[_<suffix>].png`, his pixel portrait's raised-palm pose, flames in the element's colour; framed by `portraitHeight`/`portraitCorner`) holds the caught soul (`ElementArt.heldSoul`, `Portraits/Soul_<suffix>_Painted.png`, one per catcher: Akagane's copper fire and war bell, Mizore's still frost, crane and needle, Hibiki's violet storm, drum crest and tin bells) at `portraitSoulPoint`. Without the paintings it falls back to the pixel portrait and the pixel soul frames. The pixel souls (`Effects/Soul_<suffix>_0..7`, the pickups, portal icons and talk-scene fallback) are PixelLab loops drawn from these paintings, each wisp over its bell. The Spirit Bead and Sword Arts screens hold out `beadPainted`/`scrollPainted` (`Portraits/SpiritBead_Painted`, `TechniqueScroll_Painted`, GPT image) the same way, or their pixel loops. The held art floats in a dark pocket (`Pocket`, a `SoftGlow` in `PocketColour` at the swirl's heart, behind Oni) over a soft cast shadow (`ProceduralSprites.ShadowOf`: the sprite's solid pixels, read back from the GPU and placed by `textureRectOffset` since paintings are packed trimmed, blurred at a small working size and cached per sprite; laid `ShadowDrop` below it), with only a faint aura behind it, so it is the brightest thing in a dark frame (a traced outline looked sloppy on the painted edges). Behind the held soul and Oni a `SwirlUI` vortex turns in the screen's colour over a soft wash (`SoftGlow` at 0.22, so pale colours don't turn the corner white). `SwirlUI` is reusable: `SwirlUI.Create` under any RectTransform, three counter-rotating layers of `ProceduralSprites.Swirl` (a code-built log-spiral of thin, noise-bent wisps that fade in and out along their length) in unscaled time, tinted by `Colour` and faded by `Strength` | `BoonChoiceUI.cs` (built entirely in code) |

Rule: kills from `KillSource.Effect` never trigger further effects (no chain reactions). `BoonId` order must
match `BoonChoiceUI.boonIcons`.

## Enemies

### Samurai army (the "invaders")
- `SamuraiArmy.cs` — spawns a round from a `LevelConfig` (`SpawnStage`), marches, speeds up as it shrinks
  (`CurrentSpeed`), drops and turns at the camera edges, fires from the front of a column, tramples blockers,
  holds once its front rank reaches the cover's row (`HoldY` from `holdDepth`, `BeginHolding`: no more drops, it walks that row
  and `ErodeBlockers` smashes the cover) and starts lunges (`StartLunge`/`UpdateLunge`: a front-rank samurai near the ronin
  shakes under `Samurai.WarnDive`'s red "!" (`ProceduralSprites.Exclaim`), lunges through where he stood and walks back).
  `ArmyReachedRonin` (a landing at `Ronin.TopY`) is only a safeguard now. In `DiveContact` a parrying ronin (`Ronin.Parrying`,
  within `parryReach`) flings a lunger or diver away (`Ronin.ParryBody`, `Samurai.Knock` tints it, dive phase 3 arcs it off,
  then `Kill(KillSource.Deflected)`). Rounds with `LevelConfig.diveInterval` send a samurai from behind the
  front rank on a telegraphed bezier swoop at the ronin (`StartDive`/`UpdateDives`): it hits blockers (`Blocker.Hit`)
  and the ronin, then glides back into its marching slot; divers are left out of the edge, landing and front-column
  checks. Also hosts the army-wide freeze and spatial queries
  (`NearestSamurai`, `SamuraiWithin`, `FrontSamuraiInColumn`, `ClosestColumnTo`) used by boons.
- `Samurai.cs` — one soldier: `Init`, `Fire`, `TakeHit`, `Kill`, frost tint, and `Encase` (ice from Frost Blade / Frozen Riposte: holds its fire, and the ronin's next hit kills it outright).
- `SamuraiKind.cs` — `Ashigaru` (single arrow), `Archer` (3-arrow fan), `Taisho` (2 HP, unbreakable spear
  thrown on its own army timer, `SamuraiArmy.ThrowSpear`, over the ranks in front), `Shieldbearer` (2 HP,
  steel-tinted, never fires; `FrontShooterInColumn` lets the rank behind shoot over it) and `Yurei` (a ghost in the
  Bushi art, drawn translucent cyan over a spirit glow by `Samurai.SetFrost`; fires a parted pair of arrows with
  `Arrow.PassesCover`, so cover doesn't stop them) and `Bellringer` (2 HP, never fires; `CanFireKind` lets the
  rank behind shoot over it like a Shieldbearer. `SamuraiArmy.UpdateBells` picks a living one every `bellInterval`,
  `Samurai.WarnToll` glows for `tollWarning`, then `Toll` rings a teal pulse and the army's fire interval is scaled
  by `tollFireScale` for `tollHaste`; one cut down first never rings, and each kill pushes the next toll back by
  `silenceDelay` with a `Sfx.BellSilenced` chime. Its art is `SamuraiArmy.bellringerArt`; without it `Bronzed`
  tints the Bushi art bronze). Rows pick a kind via
  `LevelConfig.rowKinds`; art and points come from `SamuraiArmy.ArtFor` / `PointsFor`
  (`samuraiByRow` art: Taisho, Bushi, Bushi, Ashigaru; the Taisho kind wears the Taisho art, the Yurei the Bushi's).
- `Arrow.cs` — enemy projectile; also restyled into boss shots (`Restyle`, `MakeReflectable`, `Arrow.Fan`).
  Arrows break on each other unless both have `PassesShots` (Shirogane's).
  `MakeUnbreakable` makes the Taisho spear: it passes through slashes, kunai, arrows and blockers and only stops on the
  ronin, so it must be dodged or parried (a parried one gives a riposte that pierces two samurai). `Tint` recolours a
  plain arrow (the yurei's). Its yari art
  (`Art/Sprites/Projectiles/Spear.png`) reaches it through `Samurai.spearSprite`, wired by `Ninja/Apply Sprite Art`.
- `SamuraiCounter.cs` — the army's remaining-samurai count; its scene text is hidden by `GameUI`, as the HUD no longer shows it.

### Mini-enemy: Scout
- `Scout.cs` — kite-borne scout crossing the top of the screen (the mystery-ship role). One hit, bonus points,
  guaranteed soul. Built in code (no prefab). Spawned by `GameManager.UpdateScouts` in rounds with
  `LevelConfig.scouts = true`, every 15–25 s.

### Bullet hell
- `BulletHell.cs` — the static kit bosses build curtains from, written round the ronin's one axis: a pattern only
  matters where it crosses his row, so every crossing leaves `MinGap` (2.2) clear and the next gap is never more than
  `MaxDodgeSpeed` (6 u/s, 60% of his speed) away. Shooters are `BulletHell.Shooter` delegates (a boss's own styled
  shot). `Enlist` puts a shot on the `BossBullet` physics layer (it copies the `Arrow` layer's collisions at first use,
  minus arrows and itself; `PassesShots` without the layer), counts it against `Budget` (`Room` waits for space) and
  marks it `Arrow.CappedRiposte`. Patterns: `Ring`, `Fan` (aimed, capped at `AimedSpeedCap`), `Spiral` (its interval
  is worked out from the height so one arm's landings sit a gap apart; only downward arms fire), `Curtain` (a row with
  gaps; `shooterFor` swaps single shots, e.g. a bell), `NextGap`/`GapOk` (staircase steps and a debug check),
  `HaltAndAim`. Colour language: `Crimson` = `MakeSealDart` (`Arrow.Unparryable`, passes cover), `Bronze` =
  `MakeBell`/`MakeResonant` (parried, `Resonance` pops every breakable shot within `ResonanceRadius`).
- `ArrowShadow.cs` — every `Arrow` (`Arrow.Awake`) marks where it will cross the ronin's row, glowing in its
  `GlowColor` and brightening as it nears; none for shots rising or nearly level.
- Parry rules in `Ronin`: `DeflectNearbyArrows` fizzles on `Unparryable` shots (`SealedParry`, raising `ParryFizzled`;
  `GameManager.OnParryFizzled` shows a one-time hint) and `Deflect` sends back at most `RiposteCap` (3) capped
  shots per parry, popping the rest (`Arrow.Pop`) while still landing the parry (`LandParry`).

### Bosses
- `Boss.cs` — abstract base. Handles creation (`Boss.Create<T>`, no prefab), entrance, HP, the second-phase art
  (`SetPhaseArt` stores a transformed `CharacterArt`; each `PhaseBreak` calls `WearPhaseArt` before its first `PlayIdle`,
  so the boss fights phase 2 in its Phase 2 painting's sprites), the two-bar phase break
  (`HasSecondPhase`: emptying the first bar runs `BreakPhase`, which plays the subclass `PhaseBreak` cutscene while
  `PhaseBreaking` makes it invincible and refills the bar, raising `Healed`, then starts phase 2 via `SecondPhase`),
  staggers, freeze (boss-time `Wait`), dashes with afterimages, telegraph bars, shots, a stall watchdog
  that restarts a dead fight loop, and the default defeat. For a pair sharing a bar: `BarFraction` (what the HUD
  shows), `PhaseTitle` (the bar's name after the phase card), `RenameBar` (set by the GameManager), and
  `HoldOnAtZero` → `GoDown` (`Down`: out of the fight, unhittable, `OnDowned`) until `Defeat` finishes her;
  `OnCleared` runs on a halt or fall for anything kept outside `transient`. Lines: `BossLine { Intro, Phase, Defeat }`. What each
  boss says follows its story file in [Story/](Story/README.md) (one per character, plus the planned Kagetsune);
  keep `Line()`, `Tools/generate-voice.mjs` and that file in step.
- `Benibana.cs` — Stage 1 boss, "The Crimson Smoke" (two 70 HP bars). Moves: Smoke Rings, Rosary Lash, Crimson Iaido
  (only a parry answers it: `Arrow.OnCollisionEnter` lets reflectable shots cut through slashes and cover; parried
  back it staggers her and `Arrow.Shatter` breaks any Iaido crescent it meets), Rosary Bloom (phase 2 only; ends with
  one `BulletHell.Curtain` of crosses with a wide gap, the game's first curtain). Phase 2 once her first bar empties: smoke veil
  (`VeilPuff`, burnable by fire), aura, glowing eyes, faster combos. Also defines helpers `Swell`, `VeilPuff`.
- `Utsusemi.cs` — Stage 2 boss, "The Hollow Pilgrim" (two 120 HP bars), a karakuri automaton that moves in ticking steps, trailing digital afterimages (`DigitalGhost`: steel-blue and gold `SpriteFlash` silhouettes, faded by `GlitchFade`)
  (`Stride`). Its `Entrance` (a `Boss` hook `BeginBoss` plays after the descent, before the title card) is
  `RazeCover`, a beam from its `Eye` swept along the foot of the blocker row, demolishing every `Blocker`. Moves: Kasa Wheel (its unbreakable hat flies a telegraphed boomerang loop through the ronin's lane,
  `AimHat`/`FlyHat`, while the fight carries on; hits deal double while the hat is off via `Boss.DamageMultiplier`,
  and a parried hat circles it as the ronin's riposte, back on its head only once that riposte is gone: `Arrow.Riposte`, set by `Ronin.Deflect`; in phase 2 it's `ShatteredHalo`: the halo's three pieces (`Fx.UtsuHaloShards`, `HaloLeft`/`HaloCrown`/`HaloRight`) fly as separate `FlyHat` shots, the crown on the old `AimHat` loop and the side shards a beat later on `AimHat(x, sweepDir, skim)` loops diving `HaloShardOffset` either side of the ronin and skimming outward, each counted in `hatsOut`; then the sword wheels round a loop of its own, `ThrowSwordWheel`/`swordWheeling`, and meanwhile `NextMove` picks only the bladeless Lattice and Plumb Lines, its stance `BareUnarmedIdle` patched at runtime by `ProceduralSprites.Patched` from its unarmed and bare-headed idles), Lace Lattice (a curtain of breakable diamonds with gaps that
  drifts through cover, `Arrow.PassesCover`), Hour Strikes (announced in super armour, `Boss.SuperArmor`, which blocks stagger and freeze and lets only `ArmorDamageTaken`, 5%, of each hit's damage through
  while `HoldArmor` shows `ArmorShell`, a gold outline of eight one-pixel-offset silhouettes drawn with the
  `SpriteFlash` shader, whose colour comes from `_FlashColor` in a property block; `LaserHour` lasers each hour where the ronin stands (a sight line that tracks him, locks, then an eye laser that hits him if he hasn't moved); then for each hour `LockOnDash`
  tracks him with the sight line again, locks, raises an `HourMark` (the `Fx.UtsuNumerals` frame for that hour, I to V, or numerals drawn from bars without the art; it `Struck()` flares red), and after a dodge window `Dash`es down from its row onto that spot, chops it with its own chop frames and dashes back; then `StrikeTheHour` (the next hour's `HourMark`, slid along under the ronin by `MoveTo`) from its row flings a giant
  unbreakable, reflectable cleave that `HuntRonin` steers onto him, so only a parry answers it; parried, it jams and
  staggers, `OnStaggered`. Its dash, chop and hour-slash frames come from `GameManager.utsusemiDash`/`Chop`/`HourSlash` via
  `SetSwordFrames`, with the thrust frames as the fallback), Plumb Lines (weights hang from `Utsu_PlumbPortal` portals opened above and behind it; a cut thread plays `SnapThread`/`DropLooseEnd`, built in code; it `Pace`s about from the first portal until the swings die,
  `OpenPortal`/`ClosePortal`, and swing through the ronin's lane as a pendulum wave before being reeled back in, flown
  with `Arrow.Steer` per physics step). Its shots all pass through one another (`Own` sets `Arrow.PassesShots`). Its `PhaseBreak` opens with `Overload`: it holds the `Fx.UtsuCracking` frames (its idle with cracks spreading from the core, one stage at a time) while `WildLaser`s fire from its eye and core in random directions, ever faster, red then molten (`MakeBeam(from, flare, molten)`; harmless, no `HitIfCrossed`). Phase 2 once its first bar empties opens its core and is the laser phase (`NextMove` puts a
  laser at least every other move): the Heart Lantern (a beam swept there and back) and Midnight (two clock-hand beams
  closing on the planted sword; at low health `HoldingHands` keeps them burning at the edges while it `RecallSword`s
  and throws again). Both open with `ThrowSword` (an `HourMark` where it will land, the `FlySword` flight, then `PlantSword`: a never-breaking
  `Blocker` with the `Utsu_Sword` loop, `Utsu_SwordP2` molten in phase 2, which beams only spark off in `BurnCover`; in flight, here and in the sword wheel, `FlyingSwordFrames` draws `Utsu_SwordFlying`/`FlyingP2`, the blade without the planted art's cracked ground; `SwordGlow` is its steel or molten colour) and mark the shelter with `ShowShelter`/`HideShelter`: `ShelterSpan` samples the coming beams' rays (`ShelterSweep`s through `BeamEnd`) for the stretch of the ronin's row none of them reach, plus the sword's whole ward (`InSwordWard`, which `BeamHit` also spares), shown with a `SafeZone`; and end with `ReclaimSword` (a
  `Dash` down and a `Chop` at the sword). While the sword is out it rests in `GameManager.utsusemiUnarmedIdle`, and
  the throw wind-up is `utsusemiSwordThrow`, both via `SetThrowFrames`. The beams fire from `Core`, its heart, except Midnight's left hand, which fires from its `Eye` (`Beam.fromEye`). Both draw with `Beam` (`MakeBeam`/`DrawBeam`: a glow under its body, `Fx.UtsuBeamRed`, or `Fx.UtsuBeamMolten` in phase 2, laid `SpriteDrawMode.Tiled` by `LayTiled` and scrolled at `BeamFps`; the molten one jitters in width, flips and spits embers; without the art a red bar over a white core), stop on cover (`BeamEnd`)
  and hit the ronin where they cross his height (`HitIfCrossed`), leaving `GroundFire` patches burning along the ground; the hum is `Sfx.PlayLoop`/`Stop` on its own source. Fire burns the lace (`Burn`).
  Its effects are `Fx.Utsu*` clips (code shapes when missing); its clock is `Sfx.ClockTick`, with `HatWhirr`, `HatClang`, `GearGrind`, `LanternCharge`, `LanternBeam`, `MidnightChime`, `BladeChop`, `HourSlash`, `SwordWhirl` and `SwordPlant`.
- `SafeZone.cs` — a reusable code-built marker for where the ronin is safe from a coming attack: `SafeZone.Create()`, then
  `Place(lo, hi, groundY)` each time it moves, `Strength` to brighten it, `Dismiss` to fade it. Its ward (`Fx.SafeZone`: a
  left post, a band tiled to any width and a right post) and shield icon (`Fx.SafeZoneIcon`) are PixelLab art, with plain
  bars as the fallback. Used by Utsusemi's sword shelter.
- `Shirogane.cs` — Stage 3 boss, "The Forsaken Blade" (two 140 HP bars), a ghost swordswoman with twin blue-flame
  katanas and, from phase 2, her `SpiritWolf` **Kuon** ([Story/Kuon.md](Story/Kuon.md); same file: a code-built body
  that keeps its own ground on her row (`Home`/`den`, re-picked from `ProwlSpot`; `Hold`/`Release` keep it still for a
  move fired where it stands, `ReturnHome` reappears it on fresh ground), `Vanish`/`AppearAt(at, hold)` through mist,
  `Glare`, `PlayHowl`, `PlayMaw`, `Grow`, `Gulp`, `OpenEyes`, `WalkTo`, `Dissolve`, `Silhouette` for the pack, `Enrage`
  (bigger, burning red), `Daze`). Kuon is an `IHittable` of its own
  (`StartingHealth` 80, a kinematic body and a `BoxCollider` on the Samurai layer, on only while `Targetable`: armed,
  unbroken, not hidden in mist or as a charge). `Arm` at her phase break, `Disarm` at her fall; a `Reflected` hit is
  `ReflectedDamage` 20 and dazes it (`Dazed`, hits on it double), `Backfire` is its Maw bursting in its jaws, and at
  0 HP `Break` fades it to a shadow at her side (it is never destroyed, so her defeat scene still plays) and raises
  `Broken`. There is no ward: from phase 2 she and Kuon **share one bar** (`Boss.BarFraction`, overridden to her health
  plus Kuon's over 220; `PhaseTitle` "SHIROGANE & KUON"), and either can fall first. Kuon fights in its own loop,
  `Hunt`, on its own clock (`WolfWait`, real time, so her stagger or frost doesn't hold it; its telegraphs, beams, orb
  and dimmer live in `wolfTransient` via `WolfOwns`/`Glowing(wolfs:)`, so her `Interrupt` doesn't clear them): WolfCharge,
  Howl, WolfPack, `SpiritLances` (quick light `MawBeam`s behind short tells), `SpiritMaw` (`MawStrike`). It never
  waits on her; she only picks a joint move (TwinAssault, EclipseFang, a together OathFang) while `WolfFree`, and
  `borrowed` makes its hunt skip its turn until her move ends (`ReleaseWolf` if her stagger cuts it short).
  **Mutual enrage:** `OnWolfBroken` (`BossLine.WolfFallen`, a quiet `Stagger(GriefStagger, cry: false)`, bar renamed
  via `Boss.RenameBar`) starts her **Enraged Blade** (`enraged`: faster stats, double crescent fans, eight-lane rain with
  one open lane, a there-and-back dash, two Mirror Moon reflections, harder seeking, a third Glacier spire, four-wave
  Moonfall with spires, white-hot blades and a blood-red aura pulse). If her share empties first, `Boss.HoldOnAtZero`
  puts her `Down` instead of killing her (`Boss.GoDown`: fight stopped, hitbox off, no stagger/freeze, skipped by
  `GameManager.RandomEnemy`); `OnDowned` plays `Kneel` (blades dropped, faded to 45%), speaks `BossLine.KuonEnraged`
  and calls `wolf.Enrage()`: enraged Kuon gets a double charge, three nine-orb howls, four pups over seven lanes, five
  quicker lances, a wider Maw that drags after him (`MawBeam` `sweep`), her parry draw as its own `WolfFang` (reflects
  at Kuon) and its own ultimate `FrozenHowl` (a long `MawStrike` while a `SpireVolley` falls, then `Daze(2)`). Kuon
  breaking while she is `Down` calls `Boss.Defeat`, and `Fall` picks up from her kneel. `WolfJoined`
  hands Kuon to `GameManager.OnWolfJoined`, which routes its hits (`OnWolfDamaged` → the shared bar,
  `BoonEffects.OnWolfHit`/`WolfBlast`) and adds it to `RandomEnemy`. She `Glide`s along her row (`Roam` between combos,
  `RoamMeanwhile` beside an attack, stopped in `OnStaggered`) rather than blinking. Shots (all through `Fire`, at `ShotSpeed` 1.35× the speeds in the moves, with `Arrow.PassesShots` so her and Kuon's
  overlapping patterns fly through each other): `Crescent` (breakable),
  `GhostSword` (`PassesCover`), `IcePillar` (unbreakable, parryable, `PassesCover`, its capsule drawn out by
  `Arrow.Lengthen`; `Fx.ShiroIcePillar`), `IceSpire` (a glacier spire: unbreakable and `Unparryable`; `Fx.ShiroIceSpire`),
  both bursting into `Fx.ShiroIceShatter` via `ShatterAtGround`, `Orb`, and `Charge` (an unbreakable wolf bolt that `DissolveAtGround`; it and the Fang are dressed by `WolfShot` in Kuon's charge clip at its own proportions with a `Wake` of afterimages, and their lanes are marked by `MarkBeam` from the perch).
  Every sword slash plays `WolfSlash` (`Fx.ShiroIceSlash`, a frost arc turned to the swing, and the `Fx.ShiroWolfSlash`
  ice wolf's head that `Lunge`s along it; `Fx.SlashArc` without the art). Telegraphs:
  `LaneWarning` + `Brighten`, `FrostMark`, `PillarPicture`/`SwordPicture` hovers, `Spire(x, tell)` (a deep-blue band,
  a spreading shadow and rumble while the spire lowers into sight, then `IceSpire`; it never falls if its tell was
  cleared), `Lanes(n)`. Phase 1 moves: Crescent Moon Slash, Phantom Blade
  Rain (ice pillars; one lane always empty; two in phase 2, plus a spire in a lane away from the open ones; followed by `RingingFence`, a short sword curtain over the ronin whose
  middle blade is bronze and `BulletHell.MakeResonant`: the game's first bronze shot), Moonlit Dash (`Linger` afterimages that each drop a pillar), Spirit Blade
  Barrage (`PlaceOrbiting`, `LooseRow`; each sword `Seek`s the ronin at `SeekTurn` degrees a second via `Arrow.Turn` until `SeekStops` above him), Mirror Moon (a `Linger` reflection mirrored across the centre, `AimedFan`).
  Parry-only draws, one per phase (`NextMove` offers one once `ParryEvery` moves have passed and forces it by
  `ParryAtLatest`): Crossing Moons (`Moonsplitter`, phase 1: a `LockOn` reticle (`Fx.ShiroLockOn`) closes on him while ghost crescents
  brighten on her blades and `TraceMoonPaths` dots their curves, then two unbreakable `MakeReflectable` crescents leave her `Hands` and `Close` on him along mirrored `MoonPath` sickle curves bending past the field's edges (a quadratic Bezier whose end follows him), trailing afterimages) and
  Fang of the Oath (phase 2: a `LockOn` tell, then `LooseFang`, an unbreakable reflectable bolt steered onto him by `Pursue`); the Fang is reflectable at Kuon when
  cast together, at her when she casts it alone. Phase 2 adds Glacier (two spires on his lane and the next), Spirit Wolf Charge, Howl of the Departed (`OrbFan`), Twin
  Spirit Assault, Phantom Wolf Pack (`lastPackLanes`), Kuon's Maw (`SpiritMaw`, Bijuu-bomb style: `GatherOrb`
  dims the arena (`Dim`) and spirals streaks into the `Fx.ShiroMawOrb` orb at its jaws, `SwallowOrb` crushes it
  while `MarkBeam` (a `BeamTell`: a code-built mesh drawn with the sprite material, which `OpenBeamTell` draws as a trapezoid, the beam's width at Kuon's jaws and `TellFoot` times that at the ground, that fills over three ringing beats while the foot draws in to the beam's rectangle) marks the lane, `MawBeam` fires with
  `Fx.ShiroMawBurst`, the `Fx.ShiroMawBeam` segment drawn `SpriteDrawMode.Tiled` down the lane and `Fx.ShiroMawHit`
  where it lands, code bars without the art; `MawBreakDamage` on Kuon
  during the gather bursts it, `orbBurst`) and the Eclipse Fang ultimate (`ultimate` feeds `Busy`; a code
  `Moon`, two crescent waves, the same gather and `MawBeam` from the wolf's `Mouth`, a sword spread, then `exposed`
  doubles damage for 2 s via `DamageMultiplier`). `NextMove` weights the phase's signature twice,
  never repeats a move three times (`repeats`) and forces the ultimate every fifth move (Moonfall when enraged or
  Kuon is busy).
  `PhaseBreak` plays the wolf's arrival (`GatherSouls`: `Souls` wisps, `Fx.ShiroSpiritWisp` turned along their
  flight, rise across the court and spiral into a soul core (the `Fx.ShiroMawOrb` art) that swells per merge while
  `SpiritWolf.Form` draws Kuon inside it, then crushes; `OpenEyes` in it, the burst, `AppearAt`) and swaps her to her awakened frames (blades burning; `SetPhaseDash` for her
  phase dash); `Fall` is an 11 s scene (`DefeatSeconds`, which `GameManager.BossFell`
  waits for): blades `DropBlade`/`Tumble`, the wolf walks to her and dissolves, she speaks `BossLine.DefeatFinal`
  (a full line) and fades. Extra frames: `GameManager.shiroganeDash` via `SetDashFrames`, the wolf's clips (idle, howl, charge, vanish,
  maw) via `SetWolfFrames`. Effects `Fx.Shiro*`; sounds `WolfGrowl`, `WolfHowl`, `BladeRing`, `GhostSwordFall`, `SpiritBeam`
  (loop), `MistSwirl`.
- `Kagetsune.cs` — Stage 4's final boss, "The Thousand-Soul Throne" (two 210 HP bars), the Emperor
  ([Story/Kagetsune.md](Story/Kagetsune.md)). Drawn about 1.5× the other bosses and stood `kagetsuneDrop` lower; he
  never blinks or glides, only takes heavy `Stride` steps. Body points on his art: `Chest` (the soul-fire `chestGlow`,
  and the Wail's source), `Waist` (bells), `HammerHead`, `Shoulder(side)`. Shots: `Skull` (a breakable `Fx.KageSkull`
  orb with `PassesShots`; `Arrow.Parried` → `FreeSoul` dresses a parried one's riposte as a teal freed soul),
  `SkullFan`, `SkullBurst`, `Hunt`; tells: `CloseLanes` (wide faint lane bands that narrow and brighten over ringing
  beats), `LockOn` (a code-built ring closing on the ronin). Phase 1: Coffer Slam (a shockwave down the ronin's lane
  that the first `Blocker` in its way absorbs and breaks, `BreakAfter`), Toll of Fealty, Ofuda Decree (`Ofuda`
  strips; `seals` feed `Ronin.ParrySealed` = `InSealedLane`, which makes `Ronin.SealedParry` fizzle instead of
  parrying; `LiftSeals` keeps them for their time even if the move is cut short; always followed by a toll or slam),
  Thousand-Soul Wail (a three-bar beam from the chest using `Boss.BeamEnd`/`HitIfCrossed`/`Stretch`, burning cover,
  `Sfx.SoulWail` loop), and the parry-only Last Toll (`Fx.KageGreatBell`, unbreakable, `MakeReflectable`, `Pursue`).
  **Bullet hell** (built from `BulletHell`, all his skulls `Enlist`ed; `BellShot`/`SealDart` are his bronze and crimson
  shots; `Hardened` = phase 2 or past half of bar 1): the Toll of Fealty is a `Spiral` per toll from `Waist`; the
  Rosary Curtain (`RosaryCurtain`, `Move.Curtain`) drops gapped `Curtain` rows stepped by `NextGap`, one gapless row
  with a bell when hardened; the slam's `SlamRing`; the Ofuda Decree's `DecreeRain` (darts outside the seals, skull
  walls inside); the Wail gutters once a sweep (`GutterTell`/`GutterSeconds`) and sheds embers; the Last Toll's
  `ClosingRing`. VFX: `Crack` and `Shockwave` under slams, stride dust and phase-2 afterimages, `UpdateWisps`, a
  `chestGlow` that swells as bar 2 drains; chains and soul-fire columns are `Tiled` strips (`Fx.KageChain`,
  `Fx.KageSoulPillar`, drawn `SpriteDrawMode.Tiled` and laid by `Lay`, bars without the art); the ultimate hangs
  `HangBell`s (`Fx.KageBell`) that `Swing` when rung.
  `PhaseBreak`: bells clamour, chains snap, skulls burst, `WearPhaseArt`, teal `eyes`, `PhaseCard`. Phase 2
  adds Wraith Swarm (the Thousand-Soul Bloom: a `Ring` from the chest under `HaltAndAim`), Soul River (`SoulRiver`,
  `Move.River`: curtain rows round a sine channel kept under `MaxDodgeSpeed`, bells on its banks), Broken Chains
  (`ChainCage` first: two chain walls that `HitIfCrossed` for 3 s round a two-arm `cageSpiral`, then lashes from his
  shoulders), the parry-only Soul Coffin (seal-darts flank it; `Fx.KageCoffin` on a
  chain; parried, `OnStaggered` sees `coffinThrownAt` and sets `exposedUntil`, doubling `DamageMultiplier`), the Night
  of a Thousand Bells (`ultimate`, every `Ultimate` moves: `Darkness`, eight lanes, five waves with a stepping
  safe lane that burn for `ColumnSeconds`, then a gapless curtain with a bell over the ronin, then exposed), and the summoned souls (`Summon` builds an `Echo`: the boss's own CharacterArt idle, set
  by `SetEcho` from `GameManager.CreateBoss`, drawn teal and translucent on a chain from the hammer; `EchoBenibana`
  smoke rings + a reflectable Iaido crescent, `EchoUtsusemi` three hour marks + a reflectable cleave, `EchoShirogane`
  two waves of blade rain, her warning line spoken over the first). A soul runs beside the fight loop as
  `echoRoutine` (`RunEcho`); while `EchoOut`, `NextMove` picks only light moves (no parry-only move, ultimate or second
  soul), and `RecallEcho` (stagger, phase break, fall) stops it and drops `activeEcho` and the `cageSpiral`. Otherwise
  `NextMove` forces the ultimate, then the phase's parry move (`ParryAtLatest`), then a soul (`EchoAtLatest`; the first
  three in boss order). Under `LastBellAt` (30%) of bar 2, `lastBell`: four spiral arms, denser blooms, `Ultimate` 4,
  quicker `Recovery`, and `UpdateLastBell`'s pulsing dark. `DamageMultiplier` doubles while exposed but not while
  staggered (×2 at most); `OnStaggered` adds a `Boss.HitStop`, a bronze flash and freed souls.
  `Speaker`/`SpeakerColour` (virtual on `Boss`) put the souls' answers under their own names; `OnBossSpoke` treats
  the summons and answers as full lines. `Fall` is an 11 s scene: the coffin bursts, seven tolls fade lower and
  quieter, each loosing a `RisingSoul`, and the armor sinks hollow. Sounds `BellToll`, `BellSilenced`, `HammerSlam`,
  `ChainSnap`, `SoulWail`, `OfudaFlutter`, `SkullShriek`, `DawnChime` (each falls back to an older sound via
  `Sfx.Has` until generated). Shared helpers lifted into `Boss` for him: `TopOfScreen`/`GroundY`/`LeftEdge`/
  `RightEdge`, `NearX`, `Lanes`, `Pursue`, `BeamEnd`, `HitIfCrossed`, `Stretch`.
- Wiring in `GameManager`: `BeginBoss` (intro, boss moon via `Backdrop.SetBossMoon`: blood for Benibana, steel for Utsusemi, spirit blue for Shirogane, none for Kagetsune (`MoonLook.Soul`, whose clear moon colour keeps the disc hidden), `Backdrop.MoonLook`/`Moons`; `TuneVoice` retunes the shared chorus/echo/distortion filters per boss),
  `CreateBoss` (switch on `BossKind`), `VoiceFor` (portrait/close-up/voice clips), `OnBossSpoke`,
  `OnBossDamaged`, `OnBossDefeated` → `BossFell`. HP bar, subtitles and the intro cinematic are in `GameUI`
  (`ShowBossBar`, `SetBossHealth`, `ShowSubtitle`, `PlayBossIntro`). The intro is two beats on an opaque dark
  stage lit by a wash of the boss's colour (`ProceduralSprites.SoftGlow`): the full-body portrait fades up, then
  cross-fades to the close-up while a lower-third title (epithet, a rule, name; Title style) rises in, with the
  spoken line typed on beneath. Each boss's phase break replays it as a phase card (`Boss.PhaseCard`, set in
  `BeginBoss` to `PlayPhaseCard` when `VoiceFor` has `phasePortrait`/`phaseCloseup`) with its transformed art,
  `phaseEpithet` (`Benibana.PhaseEpithet`, `Utsusemi.PhaseEpithet`, `Shirogane.OathEpithet`) and Phase line, its wash
  in `phaseColour` when the transformed art needs one (Utsusemi's molten orange); after it `GameUI.RenameBossBar` gives the
  bar the phase epithet. Before the card each boss stages an in-world lead-in in its `PhaseBreak` (Benibana: stagger,
  pipe inhale, silence, crossing cut and reveal; Utsusemi: seizing shudder with quickening ticks, a bell, the burst open;
  Shirogane: the wolf's arrival), shaking the body with `Boss.Tremble` and swapping to the phase art at the reveal;
  without that art the line falls back to a subtitle.
- Art built by `Ninja/Apply Boss Art` (`Editor/BossArtSetup.cs`): `Characters/<Boss>/<Boss>_<Clip>_<i>.png`,
  `Characters/SpiritWolf/SpiritWolf_<Clip>_<i>.png`, `Characters/<Boss>Phase2/<Boss>Phase2_<Clip>_<i>.png` (each boss's
  second-phase form → `GameManager.<boss>PhaseArt` via `PhaseArt()`; Utsusemi's extra clips too → `utsusemiPhase<Clip>`,
  swapped by `Utsusemi.SetPhaseFrames`), `Portraits/<Boss>_Full/_Closeup.png` (Oni has `Oni_Full`/`Oni_Closeup` in the same style, and so do the souls, `Akagane_…`, `Mizore_…`, `Hibiki_…`, all wired by Apply UI Fonts; phase cards: `<Boss>_Phase_…`; full-body art is cropped to the figure so all bosses fill the same height; the phase 1 close-ups are transparent dialogue portraits (1024×1536; Kagetsune's a 1536×1024 bust), framed in `VoiceFor` with every boss's eyes at the same point on screen), `Audio/Voice/<Boss>_<Line>.mp3`.

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
  komainu (`MKM`), Utsusemi = gear piles and clock pillars (`GCG`), Frozen Shrine = snow-capped stones and broken torii
  posts (`STS`), Shirogane = frosted shrubs and shrine lanterns (`ISI`), Mountain Shrine = grave markers and temple bell
  frames (`GBG`), Kagetsune = oni braziers and broken palace pillars (`OPO`), three looks each, art in `Art/Sprites/Blockers/`
  as `<Name>_0..2`, `<Name>B_0..2`, `<Name>C_0..2` (from PixelLab), wired by `Ninja/Apply Maps`.
- `Backdrop.cs` — moonlit courtyard, sakura petals, blood moon for boss fights; `Show(Map)` swaps to another map's
  ground and petals (the moon blends from the map's own tints). Maps: `Art/Sprites/Background/` —
  `BambooRoad_Stage1` (a dirt road with bamboo only at the edges), `Pass_Stage2` (the Pilgrim's Pass), `Arena_Benibana` (maple shrine),
  `Arena_Utsusemi` (clockwork temple floor), `Mountain_Stage4` (the shrine stair) and `Arena_Kagetsune` (the coronation courtyard), generated with PixelLab. `CourtyardGrass` with its bushes and lanterns
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

## UI

- `GameUI.cs` — HUD (score in one row; stage with the round as a smaller line under it, right-aligned; a small dim
  label beside each number; lives; no high score, so the top centre stays free for the compact boss bar), the stage/round card (`ShowBanner(overline, title, body, accent)`), boss bar, subtitles on a plate with the
  speaker's name (fading in and out, with an optional speaker portrait, used only on the title hill: `ShowSubtitle(…, portrait)` takes a
  `GameUI.SpeakerPortrait`, a close-up plus the point to centre on and the height to draw it, cropped into a framed
  night-dark window at the plate's left, or standing on the plate when it sits `beside` someone), boss intro (a null
  `lineText` makes it a name reveal only, for a talk scene to follow). Talk scenes are `DialogueScene.cs`, built the
  same way from the shared parts below. Builds its pieces in code on the Canvas; `SetHudVisible` hides the HUD behind the
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
- `BoonChoiceUI.cs` — see Boons. For the tutorial: `SlotRect(slot)`, `LiftSlots(layer)`/`DropSlots()` move the ability
  slots onto a talk's overlay and back, and `InputLocked` stops a pick while Poguru talks over the screen.
  `GameUI.LivesRow` is lifted the same way for the hearts lesson. `SpriteAnimatorUI.cs` animates UI images in unscaled time.

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
| Which track plays where | `SfxLibrary`: `titleMusic` (title, dawn), `templeMusic` (the temple, `EnterSafeArea`; falls back to the title), `stageMusic` per stage keyed by its boss (`RoundsMusic` in `BeginRound`, `FightMusic` once the boss intro ends in `BeginBoss`, `PhaseMusic` crossfading in under the phase card, or on `Boss.SecondPhaseBegan` for a boss without one), `battleMusic` for any stage without its own. Files `Audio/Music/Stage_<Boss>.mp3` / `Boss_<Boss>.mp3` / `Phase_<Boss>.mp3` / `TempleMusic.mp3`, prompts in `Tools/generate-music.mjs` |
| Player volume settings (`PlayerPrefs` `MusicVolume`, `SfxVolume`) | `Music.Volume` (applied every frame, so the slider works live), `Sfx.Volume` (also scales boss voice lines) |
| Boss voicelines | `GameManager.voice` AudioSource (its own `BossVoice` child, with the chorus/echo/distortion machine-voice filters `CreateMachineVoice` turns on for Utsusemi), clips in `Audio/Voice/`; `BossLine` full lines (intro/phase/defeat) vs. barks (hurt, stagger, move callouts) in `OnBossSpoke`: a bark is dropped while anything is still playing (only Stagger cuts in). Moves call out through `Boss.Bark(line, cooldown)`. Lines with several takes (`<Boss>_<Line>_2/_3`, loaded in order by `BossArtSetup.SetTakes`) are picked by `BossVoice.Take`, and their subtitle comes from `Boss.Line(line, take)` |

Missing libraries or clips fall back gracefully (simple glows, silence), so the game runs before art/audio is wired.

## Sprites and animation

- `CharacterArt` ScriptableObject (`Art/Characters/*.asset`): clips `idle`, `attack`, `hit`, `death`, `parry`,
  `runLeft`, `runRight`, `walkUp` (the ronin walking away between maps), `walkDown` and the four diagonal walks (the temple) and death particles. Assets: Ronin (+ Fire/Water/Lightning), Ashigaru, Bushi, Taisho,
  Benibana, Utsusemi. A boss can rest in a different loop by overriding `Boss.IdleClip` (Utsusemi's bare-headed
  idle, `GameManager.utsusemiBareIdle`, while its hat is thrown, and its unarmed stance while its sword is planted).
- `SpriteAnimator.cs` plays frame lists on a SpriteRenderer (no Animator controllers).
- Raw frames live in `Art/Sprites/{Characters,Projectiles,Fx,Effects,Blockers,Background,UI}`;
  `Editor/PixelArtImporter.cs` imports them as point-filtered pixel sprites automatically.
- Boss intro paintings: `Art/Portraits/` (each side a multiple of 4, or WebGL can't compress them). Fonts:
  `Art/Fonts/` (Cinzel headers, Zen Kaku Gothic New body, pixel fallback font); the two TTFs are baked into static
  `… SDF.asset` TMP fonts of `GameUI.PrintableCharacters` by Apply UI Fonts (`GameUI.bodyFontBaked`/`headerFontBaked`),
  so builds don't ship or rasterise the TTFs. Delete a baked asset to re-bake it after adding characters.
- `Editor/WebImportSettings.cs` (an `AssetPostprocessor`) keeps the web build lean on every import: portraits
  and logo crunch-compressed for WebGL at full size, sound effects and voice lines preloaded (no hitch
  the first time one plays), voice lines mono at quality 0.5 for WebGL. Pixel sprites stay uncompressed.

## Editor menu commands (`Ninja/…`)

These rebuild assets/prefabs/scene wiring from the art on disk. Run them inside Unity; they're safe to re-run.

| Menu | Script | Does |
|---|---|---|
| Apply Sprite Art | `Editor/NinjaArtSetup.cs` | Rebuilds character/projectile prefabs, CharacterArt, blockers, backdrop |
| Apply Backdrop And Blockers Only | `Editor/NinjaArtSetup.cs` | Just the backdrop/blockers (no prefab ID churn) |
| Apply Stages | `Editor/NinjaArtSetup.cs` | Copies `GameManager.DefaultStages()` into the scene's `stages` (names, bosses, every round's values), then the maps |
| Apply Maps | `Editor/NinjaArtSetup.cs` | Stage `roundsMap`/`bossMap` (`Background/Pass_Stage<n>`, `Background/Arena_<Boss>`, each with its blocker set from `MapBlockers`), the safe areas' maps, layout, station props, pool layers and souls (`SetUpSafeAreas`) and the ronin's walks (`walkUp` from `Ronin_WalkNorth_*`, `walkDown` and the diagonals from `Ronin_WalkSouth_*`, `Ronin_WalkNorthEast_*` and so on, `idleDown` from `Ronin_IdleSouth_*`); then re-run Apply Boon Art for the elemental walks |
| Temple/Cut Pool Layers | `Editor/TempleLayers.cs` | Cuts the spirit pool's water and near rim out of `Background/Temple_Hub.png` into full-canvas layers (`Temple_PoolWater`, `Temple_PoolRimFront`); re-run after the map changes, then Apply Maps |
| Apply Boon Art | `Editor/BoonArtSetup.cs` | Ronin spirit wisps cut from his base art, elemental ronin recolours, soul and boon icon wiring (`UI/Boons/<BoonId>`), each element's shuriken (`Projectiles/ShurikenArt_<suffix>`, PixelLab; recoloured from the base when missing), the ice statue shell, and the boon screen's paintings (`Portraits/Oni_Boon*`, `Portraits/Soul_<suffix>_Painted`, imported as smooth UI sprites) |
| Apply Boss Art | `Editor/BossArtSetup.cs` | Boss CharacterArt, intro portraits and voice → GameManager; also Poguru's clips (`Characters/Poguru/Poguru_<Clip>_<i>`, Idle Talk Pop Bonk Annoyed Point Gasp Cheer) → `GameManager.poguruArt` |
| Apply UI Fonts | `Editor/UiSetup.cs` | Header font (Cinzel) and body font (Zen Kaku Gothic New Bold) baked → `GameUI.headerFontBaked` / `bodyFontBaked` (the TTF fields are cleared), `Art/Logo/RoninsRevenge.png` → `GameUI.titleLogo`, `Ronin_Flute_*` frames → `GameManager.titleFluteFrames`, `Ronin_StandUp_*` → `titleStandFrames`, `Art/Portraits/Oni_Closeup.png` (imported like Benibana's close-up) → `GameManager.oniCloseup`; `Akagane/Mizore/Hibiki_Closeup.png` → `akaganeCloseup`/`mizoreCloseup`/`hibikiCloseup` (their `_Full.png` imported the same way); `Poguru_Closeup.png` → `poguruCloseup` (and his `_Full.png`) |
| Apply Voice Lines | `Editor/VoiceSetup.cs` | `VoiceLibrary` from every clip in `Audio/Voice` → `GameManager.voiceLibrary`; logs the written dialogue lines with no clip yet |
| Apply Web Import Settings | `Editor/WebImportSettings.cs` | Reimports portraits, logo, sound effects and voice lines through the web import hooks |
| Apply VFX | `Editor/FxSetup.cs` | Builds `FxLibrary` from `Art/Sprites/Fx` |
| Apply Audio | `Editor/AudioSetup.cs` | Builds `SfxLibrary` from `Audio/Sfx` and `Audio/Music` (including each stage's and boss's track) |
| Rename To Ronin Theme | `Editor/ThemeRename.cs` | One-off template rename (already run) |

## Physics

Gameplay objects use 3D Rigidbodies and colliders on the gameplay plane (sprites are visuals only). Layers:
`Ronin`, `Samurai`, `Kunai`, `Arrow`, `BossBullet` (bullet-hell shots; its collisions are copied from `Arrow` at
runtime by `BulletHell`, minus arrows and itself); the collision matrix is in `ProjectSettings/DynamicsManager.asset`.
Characters move kinematically with `Rigidbody.MovePosition`; projectiles are launched once with `linearVelocity`.
Each object handles its own collision (projectiles only destroy themselves).

## Outside Unity

- `Tools/generate-music.mjs` — regenerates music via ElevenLabs (`npm run music`, key in gitignored `.env.local`).
- `Tools/generate-sfx.mjs` / `Tools/generate-voice.mjs` — the newer sound effects, all of Benibana's lines in her designed voice, all of Utsusemi's lines in its designed voice, all of Shirogane's in Lily's and all of Kagetsune's in his designed voice (plus the three souls' answers in theirs), all of Oni's in his designed voice, the three souls' `SoulPickup` lines in theirs, and every Poguru line in his designed voice (`POGURU`; Oni's replies in his) (`npm run sfx`, `npm run voice`).
- `README.md` — the assignment write-up (requirements, template fixes, features, balancing).
