# Architecture

Ronin's Revenge is a Space Invaders remake (Unity 6000.6.6f1, URP, new Input System). One scene,
`Assets/Scenes/SampleScene.unity`. This file is the map: where each system lives and which page in [Docs/](Docs/)
explains how its pieces talk to each other. Read the page before changing a system.

## Folder tree

```
Assets/
  Scripts/<System>/   gameplay code, one folder per system (table below); no namespaces, no asmdefs
  Editor/             the Ninja/… menu commands and the import hooks
  Prefabs/            Ronin, Samurai, SamuraiArmy, Arrow, Projectile, SlashWave (everything else is built in code)
  Scenes/             SampleScene.unity, the only scene
  Art/                Sprites/ (raw frames), Characters/ (CharacterArt assets), Portraits/, Fonts/, Logo/,
                      Materials/ (the flash and ripple shaders), FxLibrary.asset
  Audio/              Sfx/, Music/, Voice/, SfxLibrary.asset, VoiceLibrary.asset
  Settings/           URP assets, renderers and build profiles
  Plugins/WebGL/      FullscreenKeys.jslib (Esc in a fullscreen browser)
  Archive/            cut content kept for later; nothing references it
Docs/                 a page per system (linked below) and the design notes
Story/                the plot and a file per character
Tools/                the ElevenLabs scripts (npm run music | sfx | voice)
```

## Systems

| Folder | Scripts | What it is | Page |
|---|---|---|---|
| `Core/` | `GameManager` (+ partials), `StageConfig`, `LevelConfig`, `SaveData`, `RunLog`, `Controls`, `IHittable` | The run's hub and state machine, stages and rounds as data, the save slots, the control schemes, the `IHittable` contract and `Damage.Of` | [GameFlow](Docs/GameFlow.md), [Progression](Docs/Progression.md) |
| `Player/` | `Ronin`, `Projectile`, `Pickup`, `Heart`, `Soul`, `TechniqueScroll` | Oni: movement, slash, shuriken, parry, the ultimate, and what he walks into | [Player](Docs/Player.md) |
| `Enemies/` | `Samurai`, `SamuraiArmy`, `SamuraiKind`, `ShotClass`, `SamuraiCounter`, `Scout`, `Arrow`, `ArrowShadow`, `IceStatue` | The army and its kinds, its arrows and spears, the kite scout, frozen statues | [Enemies](Docs/Enemies.md) |
| `Bosses/` | `Boss`, `BulletHell`, `Benibana`, `Utsusemi` (+ partials), `Shirogane` (+ partials), `SpiritWolf`, `Kagetsune` (+ partials), `FreedSoul`, `ShiroganeFreed`, `GroundFire`, `SunrayFan`, `TimeRipple`, `TimeRippleFeature`, `Fader`, `Swell`, `VeilPuff` | The four warlords on their shared base and pattern kit, Kuon, the ending's freed Shirogane, and their helpers | [Bosses](Docs/Bosses.md) |
| `Boons/` | `Boon`, `BoonEffects` (+ `BoonEffects.Steel`), `Statuses`, `RollingWave`, `RoninLoadout`, `ShurikenStorm`, `ShurikenWake`, `ShurikenLodestone`, `Scrap`, `Upgrades`, `Oaths` | Elements, every boon's numbers, levels and effects, the five elemental statuses, Steel's falling scrap, the run's loadout, the Flute Songs with their Warlord's Seals and Kite Silk, and the Oaths | [Boons](Docs/Boons.md), [Progression](Docs/Progression.md) |
| `Rewards/` | `RewardPath`, `RewardSpirit` | The paths between rounds and the reward drawn into Oni | [GameFlow](Docs/GameFlow.md) |
| `Hub/` | `SafeArea`, `SoulPool`, `Asagi`, `Oliver`, `SoulVisitor` | The temple between runs, its stations, gate and spirit pool, Asagi at the pool's rim, Oliver at the top of the weapon rack's dais, a station of his own, and the souls who visit the gate after a run | [Hub](Docs/Hub.md) |
| `Dialogue/` | `Dialogue` (+ `Dialogue.Lines`), `DialogueScene`, `Poguru`, `Tutorial`, `TrainingSoldier` | Reactive conversations and their picker, talk scenes, Poguru and his tutorial | [Progression](Docs/Progression.md), [GameFlow](Docs/GameFlow.md) |
| `UI/` | `GameUI`, `MenuUI`, `BoonChoiceUI`, `SwirlUI`, `ResourceTray`, `SpriteAnimatorUI`, `EncyclopediaUI` | The HUD, menu pages, the boon screen, the resource tray, the memorial stones' encyclopedia (and its `Foes` table) | [UI](Docs/UI.md) |
| `World/` | `Backdrop`, `Atmosphere`, `BlockerWall`, `Blocker`, `CameraShake` | Maps and travel, each map's mood, the cover row, the camera | [World](Docs/World.md) |
| `Vfx/` | `Fx`, `FxPlayer`, `FxLibrary`, `ElementFx`, `TideFx`, `Debris`, `HitFlash`, `SpriteOutline`, `ProceduralSprites`, `SpriteAnimator`, `CharacterArt` | Pixel effects by id, code-built glows and bolts, Asagi's waves laid out from native-size crests, debris, hit flashes, procedural sprites, sprite clips | [Art](Docs/Art.md) |
| `Audio/` | `Music`, `Sfx`, `SfxLibrary`, `VoiceLibrary` | Music, sound effects by id, the voice clips | [Art](Docs/Art.md) |

`GameManager` owns the run and is what everything reports to; the state machine and the flow from title to dawn are
in [Docs/GameFlow.md](Docs/GameFlow.md).

## Partial classes

The biggest classes are `partial`: the declaration, every serialized field and the lifecycle stay in `Name.cs`, and
each `Name.Part.cs` beside it holds one family of members. Unity only needs the declaring file named after the class;
the scene keeps pointing at `Name.cs`. Keep new members in the part they belong to.

- `GameManager`: `Defaults` (the stages and safe areas as written in code), `Menus`, `Hub`, `Dev`, `Rounds`,
  `Rewards`, `Bosses`, `Talk`, `Stages` and `Clock` (the time bonus).
- `SamuraiArmy`: `Formation` (slot layout by `FormationShape`), `Elite` (veteran rows and the round's Elite), `Knockback` (`Shove`); `Samurai`: `Rank` (Elite, veteran, Burning's tick), `Throw` (the attack clip and its release frame).
- `Utsusemi`: `Kasa`, `Lattice`, `Hours`, `Plumb`, `Lantern`, `Beams`, `Overload` and `Clock`, one per move family.
- `Kagetsune`: `Kit` (stride, bullets, telegraph and effect helpers), `Throne` (phase 1), `Wraith` (phase 2), `Souls`
  (the summoned bosses and their borrowed powers), `Levy` (his levy of the dead army), `Beam` (how the Spirit Hyper Beam is drawn) and `Phase` (the break and his fall).
- `Shirogane`: `Moves` (phase 1 and the parry-only draws), `Wolf` (phase 2 and Kuon's Maw) and `Phase` (the break and
  her fall). Kuon is `SpiritWolf.cs`.
- `BoonEffects`: `Steel` holds all of Steel's effects: scrap, cracks, plates and its Duos' hooks.
- `Dialogue`: `Dialogue.Lines.cs` is the table of every conversation; `Dialogue.cs` holds the types and the picker.

## Pages

- [Docs/GameFlow.md](Docs/GameFlow.md): states, stages and rounds, paths and rewards, title to game, the ending, dev tools, Poguru and his tutorial.
- [Docs/Hub.md](Docs/Hub.md): the temple, its stations, gate and pool.
- [Docs/Progression.md](Docs/Progression.md): the save, reactive dialogue and talk scenes, voices, Spirit Notes, Flute Songs, Oaths, the resource tray.
- [Docs/Player.md](Docs/Player.md): the ronin's moves, controls, lives and his elemental looks.
- [Docs/Boons.md](Docs/Boons.md): elements, slots, rarities, effects, the boon screen.
- [Docs/Enemies.md](Docs/Enemies.md): the army, the scout, the bullet-hell kit.
- [Docs/Bosses.md](Docs/Bosses.md): the base class and each warlord's fight.
- [Docs/World.md](Docs/World.md): cover, maps, atmosphere, physics layers.
- [Docs/UI.md](Docs/UI.md): HUD, menus, cards, the boon screen's build.
- [Docs/Art.md](Docs/Art.md): effects, feel, audio, sprite clips, and where the frames are.
- [Docs/Tooling.md](Docs/Tooling.md): the npm scripts and the import hooks.
- Design notes: [Docs/BOON_DESIGN.md](Docs/BOON_DESIGN.md), [Docs/ECONOMY.md](Docs/ECONOMY.md),
  [Docs/CHARACTER_ART_GUIDE.md](Docs/CHARACTER_ART_GUIDE.md). The story: [Story/README.md](Story/README.md).
  The graded write-up: [README.md](README.md).

## Editor menu commands (`Ninja/…`)

These rebuild assets/prefabs/scene wiring from the art on disk. Run them inside Unity; they're safe to re-run.

| Menu | Script | Does |
|---|---|---|
| Apply Sprite Art | `Editor/NinjaArtSetup.cs` | Rebuilds character/projectile prefabs, CharacterArt, blockers, backdrop |
| Apply Backdrop And Blockers Only | `Editor/NinjaArtSetup.cs` | Just the backdrop/blockers (no prefab ID churn) |
| Apply Stages | `Editor/NinjaArtSetup.cs` | Copies `GameManager.DefaultStages()` into the scene's `stages` (names, bosses, every round's values, including the formation, volley, arrow-chance, veteran-row and Elite fields), then the maps |
| Apply Maps | `Editor/NinjaArtSetup.cs` | Stage `roundsMap`/`bossMap` (`Background/Pass_Stage<n>`, `Background/Arena_<Boss>`, each with its blocker set from `MapBlockers`), the safe areas' maps, layout, station props, pool layers, souls, Asagi's clips (`Characters/Asagi/Asagi_<Clip>_<i>` → `asagiArt`) Oliver's (`Characters/Oliver/Oliver_<Clip>_<i>` → `oliverArt`) and the gate visitors' (`Characters/<Soul>/<Soul>_<Clip>_<i>`, Idle Talk Leave Drum → `visitorArt`) (`SetUpSafeAreas`) and the ronin's walks (`walkUp` from `Ronin_WalkNorth_*`, `walkDown` and the diagonals from `Ronin_WalkSouth_*`, `Ronin_WalkNorthEast_*` and so on, `idleDown` from `Ronin_IdleSouth_*`, the other six idles from `Ronin_Idle<Direction>_*`); then re-run Apply Boon Art for the elemental walks |
| Temple/Cut Pool Layers | `Editor/TempleLayers.cs` | Cuts the spirit pool's water and near rim out of `Background/Temple_Hub.png` into full-canvas layers (`Temple_PoolWater`, `Temple_PoolRimFront`); re-run after the map changes, then Apply Maps |
| Apply Boon Art | `Editor/BoonArtSetup.cs` | Ronin spirit wisps cut from his base art, elemental ronin recolours, soul and boon icon wiring (`UI/Boons/<BoonId>`), each element's shuriken (`Projectiles/ShurikenArt_<suffix>`, PixelLab; recoloured from the base when missing), the ice statue shell, the boon screen's paintings (`Portraits/Oni_Boon*`, `Portraits/Soul_<suffix>_Painted`, imported as smooth UI sprites), and the reward loops and held paintings (`Effects/SpiritBead_`, `SpiritThread_`, `SpiritNotes_`, … → `beadFrames`, `threadFrames`, …; `SpiritBead_Painted`, `SpiritThread_Painted`; `Effects/WarlordSeal_`, `KiteSilk_` → the resource tray's `sealFrames`, `silkFrames`) |
| Apply Boss Art | `Editor/BossArtSetup.cs` | Boss CharacterArt, intro portraits and voice → GameManager; also Poguru's clips (`Characters/Poguru/Poguru_<Clip>_<i>`, Idle Talk Pop Bonk Annoyed Point Gasp Cheer) → `GameManager.poguruArt` |
| Apply UI Fonts | `Editor/UiSetup.cs` | Header font (Cinzel) and body font (Zen Kaku Gothic New Bold) baked → `GameUI.headerFontBaked` / `bodyFontBaked` (the TTF fields are cleared), `Art/Logo/RoninsRevenge.png` → `GameUI.titleLogo`, `Ronin_Flute_*` frames → `GameManager.titleFluteFrames`, `Ronin_StandUp_*` → `titleStandFrames`, `Art/Portraits/Oni_Closeup.png` (imported like Benibana's close-up) → `GameManager.oniCloseup`; `Akagane/Mizore/Hibiki_Closeup.png` → `akaganeCloseup`/`mizoreCloseup`/`hibikiCloseup` (their `_Full.png` imported the same way); `Poguru_Closeup.png` → `poguruCloseup` (and his `_Full.png`) |
| Apply Voice Lines | `Editor/VoiceSetup.cs` | `VoiceLibrary` from every clip in `Audio/Voice` → `GameManager.voiceLibrary`; logs the written dialogue lines with no clip yet |
| Apply Web Import Settings | `Editor/WebImportSettings.cs` | Reimports portraits, logo, sound effects and voice lines through the web import hooks |
| Apply VFX | `Editor/FxSetup.cs` | Builds `FxLibrary` from `Art/Sprites/Fx` |
| Apply Audio | `Editor/AudioSetup.cs` | Builds `SfxLibrary` from `Audio/Sfx` and `Audio/Music` (including each stage's and boss's track) |
