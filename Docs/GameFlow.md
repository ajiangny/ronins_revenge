# Game flow

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

`GameManager` owns the run and is the hub everything reports to.

```
Title ─Start─▶ Choose a journey ─new slot (control cards, flute intro)───────────────┐
                                └─saved slot──▶ Temple (Hub) ─gate─travel─▶ Stage intro ─▶ Round 1 ─reward─path─▶ Round 2 … (up to 5; boss path from round 3) ─travel─▶ Boss ─▶ stage boon + bead ─travel─▶ next stage / Victory
                                                    ▲         │  (soul caught → Choosing: boon screen, time paused)            │
                                                    │         │  (Esc / P / Start → Paused: pause menu, time paused)            │
                                                    └─reload, wakes in the pool ◀── ronin out of lives / army reaches ronin ─┘
```

- States: `GameManager.State { Title, Transition, Playing, Choosing, Paused, GameOver, Dialogue, Hub }`.
- Menu pages are built in `GameManager` (`ShowTitleMenu`, `ShowPauseMenu`, `ShowControls`, `ShowSettings`,
  `ShowEndMenu`) and shown by `MenuUI`. Sub-pages take a `back` callback so Controls/Settings return to
  whichever page opened them. `Pause` only fires in `Playing`; `Resume` always restores `timeScale = 1`
  (a pause can cut off a parry hit-stop). In a browser, fullscreen takes Esc: `Plugins/WebGL/FullscreenKeys.jslib`
  (`LockEscapeInFullscreen`, called from `Start`) locks Escape with the Keyboard Lock API where it exists, and
  `GameManager.Update` pauses when the web build leaves fullscreen mid-fight (`wasFullScreen`); P and Backspace also work.
- Content is data: `GameManager.stages` is a `StageConfig[]` (`StageConfig.cs`). Each stage has a name,
  `LevelConfig[] rounds` (`LevelConfig.cs`) and a `BossKind boss`. Four stages: "The Bamboo Road" (three
  rounds, then Benibana), "The Pilgrim's Pass" (three rounds with Shieldbearers and divers, then Utsusemi), "The
  Frozen Shrine" (three rounds with Yurei ghosts and divers from the first, then Shirogane) and "The Mountain Shrine"
  (three rounds of every kind plus Bellringers, then the final boss Kagetsune). The array is written in
  `GameManager.DefaultStages()`; the scene's serialized copy wins at runtime, and `Ninja/Apply Stages` copies the
  coded stages into it (and re-applies the maps), so add or retune a stage in code and re-run it.
- **Paths (Hades' chamber doors):** `EndRound` → `GatherDrops` (drops fly to Oni first; hearts heal him to full and the
  spare ones `Heart.Dissipate` into `SpareHeartPoints` × stage) → `GrantReward` (the round's `pendingReward`, a
  `PathReward`, first materializes mid-field as a `RewardSpirit` that hangs a moment and is pulled into him; then: soul → `OfferBoons`, bead → `OfferBead` (a level), thread → `OfferThread` (a rarity), scroll → `OfferArts`, spring heals, Lost Melody pays Spirit Notes `noteCache` ×
  stage; one with no use falls back to notes) → `ChoosePath`: `RollPaths` picks `portalChoices` (+ Third Path)
  rewards, no two alike (souls 45, notes 20, beads 20, springs 10 while hurt, threads 6, scrolls 5), plus the boss's from
  `BossPortalAfter` (`bossPortalAfterRound` + The Long Road). `RewardPath.Spawn` builds each (code-built glow, reward icon,
  TMP label). Each reward's icon is an 8-frame PixelLab loop from `RewardIcon`: the element's soul frames, `beadFrames`, `noteFrames` (`Effects/SpiritNotes_`; a procedural note without it), `springFrames`, `scrollFrames` and, for the boss's portal, `bossIcons` by the stage's boss (Benibana's kiseru and rosary, Utsusemi's hat, Kuon's head, Kagetsune's helm and bells), all wired by `Ninja/Apply Boon Art` from `Effects/<Name>_0..7`; a missing one is a disc. Oni walks his row with `Ronin.MoveOnly`; a path lights only while he's within `RewardPath.Reach` of it (between paths a "Walk under a path." prompt follows him, `GameUI.MovePathInfo`), and `GameUI.ShowPathInfo` hangs a tooltip under the lit one's name (`RewardPath.TooltipAnchor`: a brush rule in its colour, `RewardDescription` and the key hint over a soft shadow), and `ChooseKey` (E, Enter, pad A; not Space or a click, which slash)
  picks it: `RewardPath.Release` floats its reward away up the field (to come back as that round's reward) and `WalkOn` has him follow it into the dark and out at his place for the next round on the same map (cover and music kept), or
  the boss's goes on to `StageCleared`. A stage's first round is always a soul (`BeginRound`), whose banner names the
  reward. After `maxRounds` only the boss is left. Rounds past `Stage.rounds` come from `ExtraRound`: the last round,
  `extraRoundSpeedUp`/`extraRoundFireCut` per extra round, +5% aimed, a Taisho top row, one more row from the second.
- **Run log (dev tools):** `RunLog` appends a line per round and boss (time, hits, lives, kills by source with the effect share, frozen time, the army's closest approach), the portals and boons taken and the run's end to `runlog.txt` in `Application.persistentDataPath`; F8 prints the current run.
- Key methods: `StartGame`, `BeginRound`, `OnArmyDefeated` → `EndRound` → `ChoosePath` / `StageCleared` → `BeginBoss` / `OfferStageBoon`,
  `NextStage` → `EnterStage`, `EndGame`. Blockers are rebuilt at the start of every round (`BeginRound`) and on a new map.
- **Rounds:** `BeginRound` spawns the army above the view and `SamuraiArmy.MarchIn` marches it down to its slots
  under the banner (`armyMarchInTime`). Until `Advance` the army is `Entering`: its colliders are off, `TakeHit`/`Kill`
  do nothing, and `Soldiers` and the boon queries read it as empty, so nothing from the last round can touch it.
- **Maps and travel:** each `StageConfig` has an optional `roundsMap` and `bossMap` (`Backdrop.Map`: ground, tint,
  petal sprite/tint; empty = the courtyard, and a boss without its own map fights on the stage's). `TravelTo(map)`
  walks the ronin off the top (`Ronin.WalkTo`, collider off, `CharacterArt.walkUp` or an idle bob), fades to black
  (`GameUI.FadeTo`), swaps the ground (`Backdrop.Show`), rebuilds the blockers and walks him up from below to
  `Ronin.Home`. Used before every boss (`StageCleared`) and every later stage (`EnterStage`). F2 swaps the map at once.
- **Title → game:** an empty slot first shows `ShowSchemeChoice` (HOW WILL YOU FIGHT?: a `MenuUI` card page, one
  `Item.Card` per `Controls.Scheme`, its `MenuUI.CardLine`s an action each with `Controls.MoveGlyphs`/`AllGlyphs` drawn by `GameUI.CreateKeyRow`, the current scheme preselected,
  Esc back to the slots); a pick sets `Controls.Current`, `Save.Use`s the slot and runs `TitleToGame`: `MenuUI.LeaveTitle` fades the logo and rows (input off), the title
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
  `DevHotkeys`: F1 kills the current army, F2 jumps to the last stage's boss (or last round if it has none), F5 drops a soul at the ronin (`SpawnSoul`, elements in turn), F6 a technique scroll (`SpawnScroll`), F7 logs `Dialogue.Explain` for the stage's boss and Oni, F10 opens a Spirit Bead's screen and F11 a Spirit Thread's. The page also has +1000 NOTES and RESET SAVE.

- **Poguru's tutorial (`Tutorial.cs`, a component `GameManager.Start` adds):** replaces Stage I, Round 1 of a run when
  `Tutorial.ShouldOffer(save)` (`tutorialReplay`, or a journey with no runs that hasn't `tutorialSettled`), asked in
  `StartGame` before `Save.BeginRun` and armed after the dev-jump return (`Arm`), so a jump never offers it. Hooks:
  `BeginRound` → `Offer` (after `ClearProjectiles`, before the army: Poguru pops out, `Poguru_Tut_Wake`/`_Again`, then a
  `MenuUI` page DO YOU REMEMBER? with TUTORIAL / SKIP, Esc skipping; TUTORIAL sets `tutorialReplay` so a death offers it
  again and sets `Ronin.Shielded` (no hit counts until the spear), SKIP settles it) → `Drills` (taken; before
  `blockers.Rebuild`, with the cover cleared so nothing blocks his blows: five `TrainingSoldier`s (`TrainingSoldier.cs`,
  code-built `IHittable`s, `FootSoldierArt`/`SpearmanArt`, each `Untouchable` until its own lesson so a wide slash can't
  take the next ones early) march in, their lines hung over them (`GameManager.SayLine`
  with `beside`); an archer shoots down Oni's column, two chargers run at him in a file, a spearman throws a
  `SpearSprite` spear, and a coward trembles at the back. Each lesson is `Lesson(move, slot, yell, moment)`: at its
  moment `GameManager.HoldForLesson` stops time (`State.Dialogue`; it hands back whichever state it found), the HUD slots
  are lifted onto the `TutorialLayer`, Poguru's pointer bobs beside the move's slot with its keys as a
  `GameUI.CreateKeyRow` (key caps, mouse buttons drawn by `ProceduralSprites.Mouse`), his yell is voiced only and shown
  with the keys beside Oni (on the side away from the shot, or the other when the HUD side lacks room), and
  `Ronin.Allowed` lets only that move fire (once it does, `GameManager.CutLine("Poguru")` stops his yell and `Poguru.Hush` his talking): Ronin still reads input with time stopped, so the real press fires the real
  move (`Slashed`, `ShurikenThrown`, `ParryStarted`; `Tutorial.Press` for a test harness). The slash breaks the arrow,
  then he cuts the archer down; the shuriken goes through both chargers (Poguru headbutts any that get through back up
  to try again); the parried spear skims past the spearman (`RiposteTarget` a point beside his head, the spearman and
  coward `Untouchable`), who flees with the coward for the army) → `Backup` (after `HideBanner`, round 0:
  `Poguru_Tut_Backup`) → `RoundStarted` (after `army.Advance`: `Poguru.Guard` for the whole round, never a break;
  `Poguru.Anger` builds with the blocks (redder, shaking, steam) and barks hang over him at 1/3/6/10/15/20/30/45 blocks,
  then every 15; partway in, `UltimateDrill`: once the army is down to `UltimateAtRemaining` or `UltimateAfter` s have
  passed, `Poguru_Tut_Ultimate`, `ShowsUltimate` lifts the medallion into view, `FillUltimate` (the bar stays
  `UltimateLocked` otherwise), a `Lesson(Moves.Ultimate, BoonChoice.UltimateRect, Controls.Glyphs(Moves.Ultimate), "Poguru_Tut_UltimateIt")`, then
  `Poguru_Tut_Clone` as the clone fights on; skipped if he rages first) → either the army falls, or `SamuraiArmy.Holding` (the front rank reaches the cover) sets off `Rage`:
  he rises over Oni and a ring of his soul-fire sweeps the field, `Kill(KillSource.Effect)` on every samurai it passes →
  `OnArmyDefeated` runs `TutorialRoundEnd` instead of `EndRound` for round 0: `SpearAndHeart` (still `Playing`, so the
  hit counts) pops every arrow, plays `Poguru_Tut_Win` or, after his rage, `Poguru_Tut_Raged` at Oni, lifts the shield.
  Nothing drops while the tutorial is active (`MaybeDropSoul`, `MaybeDropScroll`, `OnBlockerBroken`, the kill heart).
  Then the spear (skipped if `MaxLives < 2`):
  `EnsureLives(2)`, `SpawnSpear` (unbreakable, unparryable,
  homing) from the far top corner while he dashes into its path at 0.35× time, ghosts, and `ForceHit` lands it through
  god mode and blink if it hasn't within its flight time (distance / `SpearSpeed`) plus `SpearGrace`; then `Poguru_Tut_Hearts` (lives row lifted onto the overlay and pointed
  at), `SpawnHeart` on Oni's row, waits for it → `GrantReward` → `RewardShown` (a talk while the `RewardSpirit`
  hangs) → `ShowBoons` → `BoonsShown` (once; `BoonChoiceUI.InputLocked` while a lightly dimmed `keepHud` talk explains
  the cards) → `ChoosePath` → `PathsShown` (once) → round 1's `BeginRound` → `Finish` (`Poguru_Tut_Done`, settle,
  back in the wisp). `StartAt` and `EndGame` call `Abort` (back in the wisp, nothing settled). **Pause:** while `Active`,
  `GameManager.Update` lets Esc pause in `Transition` and `Dialogue` too (lessons, talks), not just `Playing`; `Pause` keeps
  `pausedFrom`/`pausedTimeScale`/`pausedControls`, `HoldVoices` pauses the voice sources and `DialogueScene.Hold` freezes a talk's
  line (no typing, no advancing, a short input guard after). `ShowTutorialPause`: RESUME (`ResumeTutorial` hands it all back,
  controls a frame late so the closing key can't fire a lesson's move), SKIP TUTORIAL (`Tutorial.Skip` settles it, then
  `SkipToRoundOne`: `GameUI.FadeTo` black and `Reload(true)`), CONTROLS, SETTINGS, QUIT TO TITLE, QUIT GAME (not on WebGL). GameManager's public
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
  `SayAsPoguru` voices one line through `poguruVoice` with a subtitle. Bosses call him out mid-fight through
  `Boss.PoguruWarn(lineId, over)` / `Boss.PoguruRest` (`PoguruWarns`, `poguruWarning`): Utsusemi's sword and rock warnings. He
  always flies out to point, but only speaks when `WarningSpoken` allows (the first run they come up, then every
  `SaveData.poguruWarnGap` (2–3) runs after `poguruWarnRun`, 2–3 lines in such a run), picking `<id>`, `<id>_B` or `<id>_C`
  by `WarningTake`, never the one said last.
