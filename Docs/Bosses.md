# Bosses

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

- `Boss.cs` — abstract base. Handles creation (`Boss.Create<T>`, no prefab), entrance, HP, the second-phase art
  (`SetPhaseArt` stores a transformed `CharacterArt`; each `PhaseBreak` calls `WearPhaseArt` before its first `PlayIdle`,
  so the boss fights phase 2 in its Phase 2 painting's sprites), the two-bar phase break
  (`HasSecondPhase`: emptying the first bar runs `BreakPhase`, which plays the subclass `PhaseBreak` cutscene while
  `PhaseBreaking` makes it invincible and refills the bar, raising `Healed`, then starts phase 2 via `SecondPhase`),
  the stance meter (`Poise`, 0-1: `TakeHit` → `AddPoise` scaled by the virtual `StanceResistance` (0.85 in phase 2, Kagetsune 0.8/0.7);
  full means `Stagger(StaggerSeconds)`, which empties it; held by super armour, a stagger, a phase break or going down;
  drains at `PoiseDecayRate` after `PoiseDecayDelay` without a hit; `PoiseChanged` → `GameUI.SetBossPoise`, the gold strip
  under the boss bar that flashes white on a break), staggers, freeze (boss-time `Wait`), dashes with afterimages, telegraph bars, shots, a stall watchdog
  that restarts a dead fight loop, and the default defeat. For a pair sharing a bar: `BarFraction` (what the HUD
  shows), `PhaseTitle` (the bar's name after the phase card), `RenameBar` (set by the GameManager), and
  `HoldOnAtZero` → `GoDown` (`Down`: out of the fight, unhittable, `OnDowned`) until `Defeat` finishes her;
  `Pursue(shot, speed, face)` homes a shot onto the ronin's lane; a blade `Turn`s to face its travel as it curves, a bell or coffin (`face: false`) doesn't tilt;
  attacks already launched survive a stagger: `Launch(rest)` runs an attack's tail outside the fight loop in real time and
  `Detach` moves what it draws out of `transient`, so only a halt or the fall clears it (Benibana's `Lash`, Utsusemi's
  `SwingPlumbs` and `LooseCleaves`; a stagger still breaks the boss's stance and its next move waits);
  `OnCleared` runs on a halt or fall for anything kept outside `transient`. Lines: `BossLine { Intro, Phase, Defeat }`. What each
  boss says follows its story file in [Story/](../Story/README.md) (one per character, plus the planned Kagetsune);
  keep `Line()`, `Tools/generate-voice.mjs` and that file in step.
- `Benibana.cs` — Stage 1 boss, "The Crimson Smoke" (two 70 HP bars). Moves: Smoke Rings, Rosary Lash, Crimson Iaido
  (only a parry answers it: `Arrow.OnCollisionEnter` lets reflectable shots cut through slashes and cover; parried
  back it staggers her and `Arrow.Shatter` breaks any Iaido crescent it meets), Rosary Bloom (phase 2 only; ends with
  one `BulletHell.Curtain` of crosses with a wide gap, the game's first curtain). Phase 2 once her first bar empties: smoke veil
  (`VeilPuff`, burnable by fire), aura, glowing eyes, faster combos. Also defines helpers `Swell`, `VeilPuff`.
- `Utsusemi.cs` — Stage 2 boss, "The Hollow Pilgrim" (two 120 HP bars), a karakuri automaton that moves in ticking steps, trailing digital afterimages (`DigitalGhost`: steel-blue and gold `SpriteFlash` silhouettes, faded by `GlitchFade`)
  (`Stride`). Its `Entrance` (a `Boss` hook `BeginBoss` plays after the descent, before the title card) is
  `RazeCover`, a beam from its `Eye` swept along the foot of the blocker row, demolishing every `Blocker`. Moves (`NextMove`: never the same twice; Hour Strikes at most once a combo, at least every third move; in phase 2 a laser move at least every third move, often sooner, never the same laser twice; the first move after the phase break is always the Hyper Beam, `hyperNext`): Kasa Wheel (its unbreakable hat flies a telegraphed boomerang loop through the ronin's lane,
  `AimHat`/`FlyHat`, while the fight carries on, its climbing last leg spitting one `BulletHell.Fan` of `GearShot`s once `HatFanHeight` above him (never lower: a shot along his row can't be dodged); hits deal double while the hat is off via `Boss.DamageMultiplier`,
  and a parried hat circles it as the ronin's riposte, back on its head only once that riposte is gone: `Arrow.Riposte`, set by `Ronin.Deflect`; in phase 2 it's `ShatteredHalo`: the halo's three pieces (`Fx.UtsuHaloShards`, `HaloLeft`/`HaloCrown`/`HaloRight`) fly as separate `FlyHat` shots, the crown on the old `AimHat` loop and the side shards a beat later on `AimHat(x, sweepDir, skim)` loops diving `HaloShardOffset` either side of the ronin and skimming outward, each counted in `hatsOut`; then the sword wheels round a loop of its own, `ThrowSwordWheel`/`swordWheeling`, and meanwhile `NextMove` picks only the bladeless Lattice and Plumb Lines, its stance `BareUnarmedIdle` patched at runtime by `ProceduralSprites.Patched` from its unarmed and bare-headed idles), Lace Lattice (layers of breakable diamonds, `BulletHell.Enlist`ed, drifting through cover, `Arrow.PassesCover`: two layers with two gaps, three with one in phase 2 in `Fx.UtsuLaceP2`, each gap `LatticeGap` wide and stepped on by `BulletHell.NextGap`/`GapOk` from the last layer's), Escapement (its PixelLab escape wheel, `Fx.UtsuEscapeWheel`, rings it `WheelSize` wide and `TurnEscapeWheel` snaps it on a tooth each tick, with a flash at the core and a strong `TimeRipple` warp from the wheel (the bell's strongest, with a `StopFlicker` toward stopped-time grey); it rocks on a quickening tick, each one an aimed `BulletHell.Fan` of `GearShot`s (spinning `ProceduralSprites.Cog`), every fourth a `BulletHell.MakeBell`, phase 2's off-beats a pair of `MakeSealDart`s bracketing him), Pendulum Kick (`Camped`: `CampHits` cuts within `CampRange` of it inside `CampWindow`, counted in `OnDamaged`; once a combo it dashes aside and fires a ring of `GearShot`s with one clear arc past it), Hour Strikes (announced in super armour, `Boss.SuperArmor`, which blocks stagger and freeze and lets only `ArmorDamageTaken`, 5%, of each hit's damage through
  while `HoldArmor` shows `ArmorShell`, a gold outline of eight one-pixel-offset silhouettes drawn with the
  `SpriteFlash` shader, whose colour comes from `_FlashColor` in a property block; the shell drops after the call, and `LaserHour` lasers each hour (two, three in phase 2) where the ronin stands, leaving a `GroundFire.Scorch(spot, 2f)` (a sight line that tracks him, locks, then an eye laser that hits him if he hasn't moved); then for each hour `LockOnDash`
  tracks him with the sight line again (still in its armour, `Armour(shell, true)`, which drops only as it dashes), locks, raises an `HourMark` (the `Fx.UtsuNumerals` frame for that hour, I to V, or numerals drawn from bars without the art; it `Struck()` flares red), and after a dodge window `Dash`es down from its row onto that spot, chops it with its own chop frames and dashes back (after the last, it lingers `RewindSeconds` on his ground, the opening); then `StrikeTheHour` (the next hour's `HourMark`, slid along under the ronin by `MoveTo`) from its row flings a giant
  unbreakable, reflectable cleave that `HuntRonin` steers onto him, so only a parry answers it; parried, it jams and
  staggers, `OnStaggered`. Its dash, chop and hour-slash frames come from `GameManager.utsusemiDash`/`Chop`/`HourSlash` via
  `SetSwordFrames`, with the thrust frames as the fallback), Plumb Lines (weights hang from `Utsu_PlumbPortal` portals opened above and behind it; a cut thread plays `SnapThread`/`DropLooseEnd`, built in code; it `Pace`s about from the first portal until the swings die,
  `OpenPortal`/`ClosePortal`, and swing through the ronin's lane as a pendulum wave before being reeled back in, flown
  with `Arrow.Steer` per physics step). Its shots all pass through one another (`Own` sets `Arrow.PassesShots`). Plumb weights have no `ArrowShadow` (a straight-fall guess lies about a pendulum). Its `PhaseBreak` opens with `Overload` (2.4 s; each `WildLaser` is previewed by thin start and end lines and growing `GroundMark`s): it holds the `Fx.UtsuCracking` frames (its idle with cracks spreading from the core, one stage at a time) while `WildLaser`s fire from its eye and core in random directions, ever faster, red then molten (`MakeBeam(from, flare, molten)`; harmless here, `harmful` only in the Hyper Beam), while `Rockfall` shakes the camera and rains `FallingRock`s (harmless here), keeping one or two in the cover row. Phase 2 once its first bar empties opens its core and is the laser phase. Its painted second form (152 px frames, detected in `PhaseBreak`, `painted`) moves `eyePixel`/`corePixel`, and its empty-handed pose (the sword planted, as its lasers fire) has its own `UnarmedEyePixel`/`UnarmedCorePixel` (`InUnarmedPose`), so beams leave its eye and heart in either pose and the core glow follows; and `BurnShroud` adds pixel flames (`FlamePoints`, `ProceduralSprites.PixelFlame`, drawn at its own `BodyPixel` scale), chaotic `Ember`s (tiny specks, one of its pixels square or now and then two, snapped to its pixel grid by `SnapToPixels`, kicked, gusted and burst out sideways, cooling through `EmberSteps`) and a heat glow under its feet; the core pulses `CoreMolten` and `DigitalGhost` turns molten and ivory. Lasers: Stopped Hour (`StoppedHour`: its `Stop` clip, `TimeRipple.Emit` from its raised fist (`FistPixel`) and `StopFade` to `TimeRipple.SetStop(1)`, `HoldTime` freezing every other `Arrow` and `Projectile` in the air, its own fire, and the ronin (`Ronin.TimeHeld`: no moving, attacking or parrying, his animation stopped); it hangs rows of `Needle`s (`Fx.UtsuLaceP2` fragments, unparryable; `BulletHell.Curtain` at zero velocity, the first row's gap a few steps aside, each higher row `NeedleRowGap` up with its gap `NextGap`ed on, rows and gap narrowing with each `stoppedHours`), then `Dash`es to three spots around him and at each swings and `HangCleave`s an unbreakable, reflectable hour cleave held dead still facing him; back in its row, an inward ripple and a bell, time runs, the lace falls lowest row first, and as its last rows pass `LooseCleaves` (launched, so a stagger from the first parried one doesn't stop the rest) sends them after him `CleaveGap` apart on `Pursue`; `ClearStoppedTime` frees him and bursts unloosed cleaves if the move is cut short), the Heart Lantern (a beam swept there and back) and Midnight (two clock-hand beams
  closing on the planted sword; at low health `HoldingHands` keeps them burning at the edges while it `RecallSword`s
  and throws again). Both open with `ThrowSword` (an `HourMark` where it will land, the `FlySword` flight, then `PlantSword`: a never-breaking
  `Blocker` with the `Utsu_Sword` loop, `Utsu_SwordP2` molten in phase 2, which beams only spark off in `BurnCover`; in flight, here and in the sword wheel, `FlyingSwordFrames` draws `Utsu_SwordFlying`/`FlyingP2`, the blade without the planted art's cracked ground; `SwordGlow` is its steel or molten colour) and point the shelter out with Poguru: `WarnSword` → `CallPoguru(line, over)` raises `Boss.PoguruWarn` (`GameManager.PoguruWarns`: he pops out of the wisp, says `Poguru_Utsu_Sword` and `DashTo`s over the blade; the sword's whole ward stays safe ground, `InSwordWard`, which `BeamHit` spares); and end with `ReclaimSword` (Poguru flies back to Oni with `Poguru_Utsu_Reclaim`, a short yelp, then a
  `Dash` down and a `Chop(…, parryable: true)` at the sword, told by `ChopTell` (a gold `ProceduralSprites.Ring` closing on the spot and a gold `Exclaim` over the ronin, both flashing white just before the blade lands); a parry there is `Ronin.ParryBody` plus `Boss.AddStance(ParriedChopStance)`, no damage, after which `RestPoguru` raises `Boss.PoguruRest` and he goes back in the wisp; `Fight`, `PhaseBreak` and `OnCleared` also rest him if a move was cut short). While the sword is out it rests in `GameManager.utsusemiUnarmedIdle`, and
  the throw wind-up is `utsusemiSwordThrow`, both via `SetThrowFrames`. The beams fire from `Core`, its heart, except Midnight's left hand, which fires from its `Eye` (`Beam.fromEye`). Both draw with `Beam` (`MakeBeam`/`DrawBeam`: a glow under its body, `Fx.UtsuBeamRed`, or `Fx.UtsuBeamMolten` in phase 2, laid `SpriteDrawMode.Tiled` by `LayTiled` and scrolled at `BeamFps`; the molten one jitters in width, flips and spits embers; without the art a red bar over a white core), stop on cover (`BeamEnd`)
  and hit the ronin where they cross his height (`HitIfCrossed`), leaving `GroundFire` patches burning along the ground; the hum is `Sfx.PlayLoop`/`Stop` on its own source. Fire burns the lace (`Burn`).
  Its effects are `Fx.Utsu*` clips (code shapes when missing); its clock is `Sfx.ClockTick`, with `HatWhirr`, `HatClang`, `GearGrind`, `LanternCharge`, `LanternBeam`, `MidnightChime`, `BladeChop`, `HourSlash`, `SwordWhirl` and `SwordPlant`.
  The Hyper Beam (`HyperBeam(overload)`, phase 2): in super armour (`ArmorShell`/`HoldArmor`) it fires `harmful` `WildLaser`s (stopped by cover) for `RockfallSeconds` while `Rockfall(seconds, keep, harmful)` shakes the camera and drops `FallingRock`s on darkening shadows (`ProceduralSprites.SoftGlow`; they hurt the ronin where they smash) and `keep` (1–2) rocks into the cover row at `CoverRowY` (measured by `RazeCover`; `RockSpots` keeps them `RockSpacing` apart, clear of the HUD, at most `MaxRocks`, crumbling the oldest). A kept `Rock` is a `Blocker` with unlimited health (a launched object, `Detach`) drawn from `Fx.UtsuRock` (the stage's Rock/RockB/RockC blocker frames, three damage states per look; a grey disc without them). Then `NearestRock` → Poguru (`Poguru_Utsu_Rock`) hovers at its `ShadeX`, and `HyperCharge` plays `GameManager.utsusemiPhaseHyperCharge` (`UtsusemiPhase2_HyperCharge_*`, a PixelLab v3 clip on the P2 character; wound up once, then its last two frames flicker; its attack pose without it) under a code-built orb, `Inflow` streaks, racing ticks and a guide line. The sweep is laid out on the ronin's row from edge to edge (`GroundFor`) with a `Beam.width` of `HyperWidth`; `HyperHit` burns him within `HyperReach` of where its middle crosses his row unless `BeamEnd` from the core to his middle meets a blocker; a rock it strikes is `Scorch`ed (hotter, cracked), and `CrumblePassed` crumbles it only once the beam's trailing edge on his row is past the rock's whole shade (`Project`).
  The Thirteenth Hour (`ThirteenthHour`, once under `ThirteenthAt` of its last bar, from `Fight`): at the middle, a six-arm spiral of molten `GearShot`s (beat from its height, as `BulletHell.Spiral`) while a molten beam sweeps one half of the ground per `HandSeconds`, a `HandTell` warning line first; not armoured.
- `TimeRipple.cs` — spatial ripples and stopped-time colour: `Emit(point, radius, strength, seconds, inward, band)` sends a ring that bends the picture (strength 1 is about 3 px at 1080p, up to 3 for a hard warp; `band` its half width, per ring via `_RippleBands`), `SetStop(0..1)` drains and cools the field, `Clear`. Globals for the `Ninja/TimeRipple` shader, drawn by `TimeRippleFeature` (a `FullScreenPassRendererFeature` on both URP renderers, added by `Ninja/Apply Screen Ripples`, enqueued only while `TimeRipple.Live`); without the pass a faint `ProceduralSprites.Ring` outline stands in. Used by Utsusemi's Stopped Hour, Thirteenth Hour and phase-break bell.
- `Shirogane.cs` — Stage 3 boss, "The Forsaken Blade" (two 140 HP bars), a ghost swordswoman with twin blue-flame
  katanas and, from phase 2, her `SpiritWolf` **Kuon** ([Story/Kuon.md](../Story/Kuon.md); `SpiritWolf.cs`: a code-built body
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
  ([Story/Kagetsune.md](../Story/Kagetsune.md)). Drawn about 1.5× the other bosses and stood `kagetsuneDrop` lower; he
  never blinks or glides, only takes heavy `Stride` steps. Body points on his art: `Chest` (the soul-fire `chestGlow`,
  and the Wail's source), `Waist` (bells), `HammerHead`, `Shoulder(side)`. Shots: `Skull` (a breakable `Fx.KageSkull`
  orb with `PassesShots`; `Arrow.Parried` → `FreeSoul` dresses a parried one's riposte as a teal freed soul),
  `SkullFan`, `SkullBurst`, `Hunt`; tells: `CloseLanes` (wide faint lane bands that narrow and brighten over ringing
  beats), `LockOn` (a code-built ring closing on the ronin). Phase 1: Coffer Slam (`RaiseHammer` plays his slam clip's
  wind-up, everything before `ImpactFrame`, over half the tell while the art rears up and trembles off `viewHome`/
  `viewScale`; `StrikeHammer` plays the impact on the blow and `SettleView` springs the squash back, `ResetView` on a
  stagger or phase break; then fans of `OpenCrack`s, a cone `ConeHalfAngle` either side of straight down to the
  ronin's row (three cracks, then two between them, then three again when hardened): each crack is a
  `SoulFissure` (`Split`, `SoulFissure.cs`: pixel art drawn in code along its own jagged path at the ronin's art pixel
  size: a near-black rift tapering from `StartWidth` to `TipWidth`, a lit far wall, a bright near lip and a shadowed far
  edge, side cracks, a stepped teal light round it; it opens piece by piece at `FissureSpeed`, white-hot then teal with
  motes rising, then cools to a scar; each piece's `Opened` runs `BreakCoverAlong`, which demolishes any `Blocker` it
  crosses, and kicks up chips and dust), a star of short cracks marks the crater, and `EruptColumn`s glow for
  `ColumnTell` (`Flare`ing their stretch of crack) then burst as `SoulColumn`s every `ColumnStep`, only the one at the
  row burning the ronin; launched columns run in real time), Toll of Fealty, Ofuda Decree (`Ofuda`
  strips; `seals` feed `Ronin.ParrySealed` = `InSealedLane`, which makes `Ronin.SealedParry` fizzle instead of
  parrying; `LiftSeals` keeps them for their time even if the move is cut short; always followed by a toll or slam),
  Spirit Hyper Beam (`SpiritHyperBeam`, `Move.Beam`, his old Thousand-Soul Wail, drawn by `SpiritBeamView` in
  `Kagetsune.Beam.cs` as pixel art built every frame on the art's 48-to-a-unit grid: `PixelMesh` quads of whole
  pixels in vertex colours with the sprites' material; the body in `BeamRow`-pixel rows split into the stepped bands of
  a lit cylinder (rim, teal, pale, white core) whose edges shift row by row as streaks pour down; strands on a 3D helix
  (`CoilPitch`, `CoilSpin`; two, three in phase 2) walked a pixel row at a time, behind the beam as a dim 1-px line
  under the body (order 54), in front as a 3-px stroke outlined in dark teal over it (57); `BeamSoul`s
  (`Fx.KageSoulRise` at native size, 14 or 20) riding the helix, pixel-snapped, re-sorted and darkened behind it, only
  flipped, never scaled or turned; a soft `HyperGlow` bloom behind it all; its footprint opens `HyperOpenWidth` wide (11, 12 in phase 2) at the edge the ronin stands nearer, both edges run to `stop`
  (`HyperSafe` short of the far edge) over `HyperSweep`, so it thins as it goes and its leading edge stays well under
  the ronin's speed; the hit is the wedge's span at his row; cover isn't a shield: an `OverlapBox` over the footprint
  `Hit`s every `Blocker` in it; a pulsing danger band, two guides and the pale safe strip (`HyperSafeColour`) over
  `HyperTell`, over which `BeamPose` plays his beam pose (`Kagetsune_Beam_*` / `KagetsunePhase2_Beam_*`, PixelLab, wired to
  `GameManager.kagetsuneBeam`/`kagetsunePhaseBeam` by Apply Boss Art and handed over by `SetBeamFrames`): the hammer
  slams down at his side on `BeamImpactFrame` (the Wraith's `PhaseBeamImpactFrame`) with a `Slam`, a shake and a
  `Shockwave`, and he holds the braced last frame until the beam is spent, then `PlayIdle`; it dissipates as a thread at `stop`, the coil drawing in and the souls flung off, blinking out on twos.
  `Sfx.SoulWail` + `LanternBeam` loops, `Fx.KageWailFlare` as the muzzle flare behind the beam (order 52, `SizeFlare`: `FlareOverBeam` × the beam's width at the chest, following it as it thins), the beam's top rounded into a dome (`DomeShare`, `DomeMax`) so it swells out of the middle of his chest and at the
  chest and the footprint), the levy of the dead (`Kagetsune.Levy.cs`, `Move.Levy`: `RaiseLevy` puts him in super
  armour (`levyArmour`: `SuperArmor`, `ArmorDamageTaken` 0.2, a teal `SpriteOutline`) until the last soldier has
  fallen in, tolls three times and
  calls the static `Boss.Muster` (→ `GameManager.MusterLevy` → `SamuraiArmy.Muster`) with `LevyConfig()`: 8×2 archers
  and ashigaru on the Throne, 13×3 (14 under the Last Bell) Taisho, archers and yurei with divers on the Wraith, its
  top rank at `LevyTop` under his feet; `NextMove` calls it when `LevyRemaining` is 0 and `movesSinceLevy` reaches
  `LevyAtLatest` (sometimes sooner), never while a soul is out; the phase break and `OnCleared` `DisbandLevy`), Reliquary Rend (`ReliquaryRend`: two quick
  `SlamDown`s on the ronin's lane, a third held a random 0.4-1 s, then an aftershock on the first crack; Coffer Slam's
  blow is `SlamDown(xs, ring, weight)`, which hit-stops and `TimeRipple`s, and hardened its tell can feint onto the
  ronin halfway), and the parry-only Last Toll (`Fx.KageGreatBell`, unbreakable, `MakeReflectable`, `Pursue`).
  **Bullet hell** (built from `BulletHell`, all his skulls `Enlist`ed; `BellShot`/`SealDart` are his bronze and crimson
  shots; `Hardened` = phase 2 or past half of bar 1): the Toll of Fealty is a `Spiral` per toll from `Waist`; the
  Rosary Curtain (`RosaryCurtain`, `Move.Curtain`) drops gapped `Curtain` rows stepped by `NextGap`, one gapless row
  with a bell when hardened; the slam's `SlamRing`; the Ofuda Decree's `DecreeRain` (darts outside the seals, skull
  walls inside); hardened, the Spirit Hyper Beam sweeps faster; the Last Toll's
  `ClosingRing`. VFX: `Crack` and `Shockwave` under slams, stride dust and phase-2 afterimages, `UpdateWisps`, a
  `chestGlow` that swells as bar 2 drains; chains are `Tiled` strips (`Fx.KageChain`, drawn
  `SpriteDrawMode.Tiled` and laid by `Lay`, bars without the art); the slam's and the ultimate's soul-fire columns are
  `SoulColumn` (`Fx.KageSoulColumn`, anchored on its ground ring and stretched up to the column's top; a bar without it); the ultimate hangs
  `HangBell`s (`Fx.KageBell`) that `Swing` when rung.
  `PhaseBreak`: bells clamour (each a `TollRing`), chains snap (`Fx.KageChainSnap`), skulls burst, `WearPhaseArt`, two
  teal glint `eyes` (`Eye(i)`), `soulFire` (`Fx.KageSoulFire` behind him at `SoulFireAlpha`), `PhaseCard`. Kit
  (`Kagetsune.Kit.cs`): `Shockwave` (`Fx.KageShockwave`), `TollRing` (`Fx.KageTollRing`), `LockOn` (`Fx.KageLockOn`
  closing with ticking pings), `Streak` (afterimages behind the thrown bell and coffin). `UpdateWisps` circles
  `Fx.KageSoulRise` ghosts (dimmer behind him); `UpdateLastBell` keeps its dark on the camera, gives way to the
  ultimate's and beats a `Heartbeat`; `RisingSoul` uses the same ghost. His burns are `GroundFire.Light(at, soul: true)`,
  `Fx.KageSoulFire` with teal embers instead of the laser's orange. Phase 2
  adds Coffin Pendulum (`CoffinPendulum`: three ground passes on alternating sides, each with a band tell long
  enough to cross to the clear side by `MaxDodgeSpeed`, the last shedding spaced skulls, then a `SlamDown` on the
  ronin's lane; `CoffinOnChain`/`SetCoffin`), Soul Harvest (`SoulHarvest`: rows of floor glows that rise as skulls
  through the ronin's row, `HomeToChest` bending them into his chest above it, gaps stepped by `NextGap`), Wraith Swarm
  (the Thousand-Soul Bloom: a `Ring` from the chest under `HaltAndAim`), Soul River (`SoulRiver`,
  `Move.River`: curtain rows round a sine channel kept under `MaxDodgeSpeed`, bells on its banks), Broken Chains
  (`ChainCage` first: two chain walls that `HitIfCrossed` for 3 s round a two-arm `cageSpiral`, then lashes from his
  shoulders), the parry-only Soul Coffin (seal-darts flank it; `Fx.KageCoffin` on a
  chain; parried, its hit (`OnHitBy`, on `Damaged`) sees `coffinThrownAt` and sets `exposedUntil`, which a stagger pushes back past itself, doubling `DamageMultiplier`), the Night
  of a Thousand Bells (`ultimate`, every `Ultimate` moves: `Darkness`, eight lanes, five waves with a stepping
  safe lane that burn for `ColumnSeconds`, then a gapless curtain with a bell over the ronin, then exposed), and the summoned souls (`Kagetsune.Souls.cs`): `CallSoul` raises a real boss through `Boss.Summon` (→
  `GameManager.SummonSoul`) at 30% health on the boss row, `Haunt`s it (a teal wash and a `SpriteOutline`) and binds
  it with `soulChain`/`soulAura`, laid every frame by `UpdateSoul`. The soul fights its own first phase (it is
  `Summoned`, so it never phase-breaks) while his `NextMove` keeps to plain moves (no Ofuda, parry-only move, ultimate or
  second soul; the ultimate's count holds). Its `Defeated` → `OnSoulFreed`: `Fx.KageChainSnap`, then `SoulComesHome`
  arcs a freed soul into his chest for `AddStance(FreedStance)`. Left `AbsorbAfter` (20 s of his time), `Absorb`
  banishes it and sets `borrowed`/`borrowedLeft`, and `Borrow` lays its power on his next `BorrowedBlows` plain moves
  (`BorrowedSmoke`, `BorrowedHours`, `BorrowedBlades`). `BanishSoul` takes it off with no reward (phase break, `OnCleared`
  on a halt or his fall, `OnDestroy`); a stagger leaves it fighting. `NextMove` forces the ultimate, then the phase's
  parry move (`ParryAtLatest`), then a soul (`SoulAtLatest` moves after the last left; the first within two moves of the
  break; `NextSoul` goes in boss order, then at random without repeats). Under `LastBellAt` (30%) of bar 2, `lastBell`: four spiral arms, denser blooms, `Ultimate` 4,
  quicker `Recovery`, and `UpdateLastBell`'s pulsing dark. `DamageMultiplier` doubles while exposed but not while
  staggered (×2 at most); `OnStaggered` adds a `Boss.HitStop`, a bronze flash and freed souls.
  `Speaker`/`SpeakerColour` (virtual on `Boss`) put the souls' answers under their own names; `OnBossSpoke` treats
  the summons and answers as full lines. `Fall` is an 11 s scene: the coffin bursts, seven tolls fade lower and
  quieter, each loosing a `RisingSoul`, and the armor sinks hollow. Sounds `BellToll`, `BellSilenced`, `HammerSlam`,
  `ChainSnap`, `SoulWail`, `OfudaFlutter`, `SkullShriek`, `DawnChime` (each falls back to an older sound via
  `Sfx.Has` until generated). Shared helpers lifted into `Boss` for him: `TopOfScreen`/`GroundY`/`LeftEdge`/
  `RightEdge`, `NearX`, `Lanes`, `Pursue`, `BeamEnd`, `HitIfCrossed`, `Stretch`.
- Kagetsune is `partial`: `Kagetsune.cs` (fields, lifecycle, the fight loop and `NextMove`), `Kit` (stride, bullets,
  telegraph and effect helpers), `Throne` (phase 1), `Wraith` (phase 2), `Souls` (the summons and borrowed powers),
  `Levy` (his levy of the dead army), `Beam` (how the Spirit Hyper Beam is drawn) and `Phase` (the break and his fall).
- The levy uses the round's own `SamuraiArmy`: `Muster` builds the formation in its slots (`SpawnStage(…, aboveView:
  false)`), lifts every soldier's sprites to `LevyOrder` (in front of the boss and his wisps) and `FallIn` flies each
  soldier out of his chest into its slot, front rank first (`FlyToSlot`: a flat soul-fire `Silhouette` of its own
  frame over it, a glow and shed motes; landed, the silhouette burns off white to teal over `Solidify`), then
  `Advance`s; `Disband`
  clears it in puffs. While a boss is up, `GameManager` ignores `ArmyDefeated`, turns `ArmyReachedRonin` into a hit and
  a disband, and levy kills drop no souls or scrolls (hearts still can).
- Several bosses at once (Kagetsune's summons): `Boss.Summoned` skips the phase break, makes `Shoot`'s shots
  `PassesShots` and swaps the fall for `SoulFall` (no Defeat line); `Boss.Banish` ends one without `Defeated` (raises
  `Banished`); `Boss.Haunt` tints and outlines it; the static `Boss.Summon` is `GameManager.SummonSoul`, which builds it
  with `CreateBoss(kind, pos, SummonHealth)`, glides it in without its `Entrance` (`RaiseSoul`), subscribes `Damaged`
  (as `OnBossDamaged(hit, …)`), `Spoke` and its defeat score, never `OnBossDefeated`, and keeps it in `summons` and
  `effects.Summons` (`LiveSummon`, `ClearSummons`). `ShowBossHealth` splits the Emperor's bar with the live soul,
  `RandomEnemy` homes ripostes on it, `OnBossSpoke` voices each boss with its own clips (`KindOf`), and a summoned
  Shirogane comes without Kuon.
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
