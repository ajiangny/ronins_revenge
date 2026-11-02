# VFX, audio, sprites and animation

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

## VFX, feel and audio

| What | Where |
|---|---|
| Animated pixel effects by id | `Fx.cs` (`Fx.Play`, ids as constants) + `FxLibrary` asset (`Art/FxLibrary.asset`) |
| Code-built glows, blasts, lightning bolts | `ElementFx.cs`, `ProceduralSprites.cs` |
| Debris bursts (petals, chips, sparks) | `Debris.cs` |
| Screen shake (read `CameraShake.Center`, never the camera, for layout) | `CameraShake.cs` |
| Fade to black between maps | `GameUI.FadeTo` (unscaled time) |
| White hit flash | `HitFlash.cs` + `Art/Materials/SpriteFlash.shader` |
| Cracked's seams: a copy of the sprite that draws only a hard network of cracks, on the art's own pixels and inside its silhouette | `CrackSeams.cs` + `Art/Materials/SpriteSeams.shader` (`FxLibrary.seamMaterial`). Without the material the tint alone shows it |
| Steel's effects (`Fx.Steel` plays one at its own colours under the arrows, or a seam-coloured impact without the art) | `Fx/Steel_*`: Sparks, Crack, Anvil, Mend, Forge, Shatter, Grind, Scrap, ScrapHot, Shard, Filings, Ring. Iron and bright seams with hard edges, no soft alpha, never flat gold. Drawn by a PIL script kept with the drafts, not in PixelLab (`~/Drafts/SteelFx/draw.py`). A Forge frame is the plate's own art (`Blockers/SteelPlate`) cooling from white heat, so it matches the plate |
| Draw order under arrows | `Arrow.DrawOrder` 4, `Fx.UnderArrows` 3: above the army and the cover, below a warlord at 5. Older effects still use the default order 60 |
| Parry hit-stop | `GameManager.HitStop` |
| Sound effects by id (pooled, ignores timeScale) | `Sfx.cs` + `SfxLibrary` asset (`Audio/SfxLibrary.asset`). A pool voice is free once its clip's audible part has played (`Sound.audible`, measured by Apply Audio, since the generated clips carry long silent tails); the march beat has its own source (`Sound.ownSource`) |
| Music crossfade and ducking | `Music.cs` |
| Which track plays where | `SfxLibrary`: `titleMusic` (title, dawn), `templeMusic` (the temple, `EnterSafeArea`; falls back to the title), `stageMusic` per stage keyed by its boss (`RoundsMusic` in `BeginRound`, `FightMusic` once the boss intro ends in `BeginBoss`, `PhaseMusic` crossfading in under the phase card, or on `Boss.SecondPhaseBegan` for a boss without one), `battleMusic` for any stage without its own. Files `Audio/Music/Stage_<Boss>.mp3` / `Boss_<Boss>.mp3` / `Phase_<Boss>.mp3` / `TempleMusic.mp3`, prompts in `Tools/generate-music.mjs` |
| Player volume settings (`PlayerPrefs` `MusicVolume`, `SfxVolume`) | `Music.Volume` (applied every frame, so the slider works live), `Sfx.Volume` (also scales boss voice lines) |
| Boss voicelines | `GameManager.voice` AudioSource (its own `BossVoice` child, with the chorus/echo/distortion machine-voice filters `CreateMachineVoice` turns on for Utsusemi), clips in `Audio/Voice/`; `BossLine` full lines (intro/phase/defeat) vs. barks (hurt, stagger, move callouts) in `OnBossSpoke`: a bark is dropped while anything is still playing (only Stagger cuts in). Moves call out through `Boss.Bark(line, cooldown)`. Lines with several takes (`<Boss>_<Line>_2/_3`, loaded in order by `BossArtSetup.SetTakes`) are picked by `BossVoice.Take`, and their subtitle comes from `Boss.Line(line, take)` |

Missing libraries or clips fall back gracefully (simple glows, silence), so the game runs before art/audio is wired.

## Sprites and animation

- `CharacterArt` ScriptableObject (`Art/Characters/*.asset`): clips `idle`, `attack`, `hit`, `death`, `parry`,
  `runLeft`, `runRight`, `walkUp` (the ronin walking away between maps), `walkDown`, `walkLeft`/`walkRight` and the four diagonal walks (the temple) and death particles. Assets: Ronin (+ Fire/Water/Lightning), Ashigaru, Bushi, Taisho,
  Benibana, Utsusemi. A boss can rest in a different loop by overriding `Boss.IdleClip` (Utsusemi's bare-headed
  idle, `GameManager.utsusemiBareIdle`, while its hat is thrown, and its unarmed stance while its sword is planted).
- `SpriteAnimator.cs` plays frame lists on a SpriteRenderer (no Animator controllers).
- Raw frames live in `Art/Sprites/{Characters,Projectiles,Fx,Effects,Blockers,Background,UI}`;
  `Editor/PixelArtImporter.cs` imports them as point-filtered pixel sprites automatically.
- Boss intro paintings: `Art/Portraits/` (each side a multiple of 4, or WebGL can't compress them). Fonts:
  `Art/Fonts/` (Cinzel headers, Zen Kaku Gothic New body, pixel fallback font); the two TTFs are baked into static
  `… SDF.asset` TMP fonts of `GameUI.PrintableCharacters` by Apply UI Fonts (`GameUI.bodyFontBaked`/`headerFontBaked`),
  so builds don't ship or rasterise the TTFs. Delete a baked asset to re-bake it after adding characters. Zen Kaku has no `ū`, so Cinzel is its fallback for "Jūsuke" (set by Apply UI Fonts).
- `Editor/WebImportSettings.cs` (an `AssetPostprocessor`) keeps the web build lean on every import: portraits
  and logo crunch-compressed for WebGL at full size, sound effects and voice lines preloaded (no hitch
  the first time one plays), voice lines mono at quality 0.5 for WebGL. Pixel sprites stay uncompressed.


## Where the frames are

`Art/Sprites/` is imported by `Editor/PixelArtImporter.cs` (point filtering, a fixed PPU; `Characters/` get a bottom-centre
pivot). Its subfolders grew by use rather than by plan: `Characters/<Name>/` and `Projectiles/` are what they say; `Effects/`
holds particle textures (Ash, Bone, Petal, Smoke, Spark…) beside the pickups' loops (Soul_*, SpiritBead, SpiritNotes,
TechniqueScroll, HealingSpring) and the paths' boss icons (`BossIcon_*`); `Fx/` holds every `FxLibrary` clip, the bosses'
attack frames prefixed by boss (`Beni_`, `Utsu_`, `Shiro_`, `Kage_`) beside generic effects and the temple's ambience;
`UI/` holds the ability and boon icons. The Ninja commands and the import hooks hardcode these paths, so moving a
folder means updating `Editor/*.cs` too.

Kagetsune's animated effects (`Kage_Shockwave`, `Kage_TollRing`, `Kage_ChainSnap`, `Kage_SoulFire`, `Kage_LockOn`,
`Kage_WailFlare`, `Kage_SoulRise`, `Kage_OfudaBurn`, `Kage_SoulColumn`) were built in PixelLab's effect workbench and saved to its My effects
under the same names, so they can be reopened and re-exported there. An export is a spritesheet with a JSON frame list;
each frame is cut out to `Fx/<Id>_<n>.png` at the JSON's exact x/y/w/h (with ffmpeg's `crop`: `sips --cropOffset` ignores a zero offset and cuts from the sheet's middle). A new id also needs a constant in `Fx.cs` and a row in `FxSetup.Clips`
(its fps, loop and anchor: the export's origin as a 0-1 pivot from the bottom-left), then `Ninja/Apply VFX`. His hammer's fissures are drawn in code instead (`SoulFissure`), and so is the Spirit Hyper Beam (`Kagetsune.Beam.cs`: whole-pixel quads on the 48-to-a-unit grid, its coil and souls placed in 3D and projected); its first PixelLab strip, which looked smeared stretched across the beam, is kept in `Archive/SpiritBeam/`.
