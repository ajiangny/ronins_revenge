# Enemies

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

## Samurai army (the "invaders")
- `SamuraiArmy.cs` — spawns a round from a `LevelConfig` (`SpawnStage`), marches, speeds up as it shrinks
  (`CurrentSpeed`), drops and turns at the camera edges, fires from the front of a column, tramples blockers,
  and collapses into a loose last row in the cover's row (`HoldY` from `holdDepth`, `RowY`): each samurai that reaches it breaks
  formation (`loose`, `IsLoose`), picks left or right at random and walks the row on its own at the march speed, turning at the
  edges and passing through the others, while the formation above keeps marching and dropping (its turns come from its own
  extents only). The first to arrive calls `BeginHolding`; `ErodeBlockers` smashes the cover, aimed shots come from the loose
  samurai nearest the ronin, and lunges begin (`StartLunge`/`UpdateLunge`: a front-rank samurai near the ronin
  shakes under `Samurai.WarnDive`'s red "!" (`ProceduralSprites.Exclaim`, white-edged, popping in), lunges through where he stood at a
  steady `lungeSpeed` (its `duration` from the distance) and walks back). Every warning (`Warn`: `WarnDive`, a `BladeRing`, a small
  shake) also draws `DrawTell`, a red streak from the samurai to the ronin and a mark at his `Feet`, until it launches. One at a time:
  no lunge while any diver is out, and no dives once `holding`.
  `ArmyReachedRonin` (a landing at `Ronin.TopY`) is only a safeguard now. In `DiveContact` a parrying ronin (`Ronin.Parrying`,
  within `parryReach`) flings a lunger or diver away (`Ronin.ParryBody`, `Samurai.Knock` tints it, dive phase 3 arcs it off,
  then `Kill(KillSource.Deflected)`). Rounds with `LevelConfig.diveInterval` send a samurai from behind the
  front rank on a telegraphed bezier swoop at the ronin (`StartDive`/`UpdateDives`): it hits blockers (`Blocker.Hit`)
  and the ronin, ends just past his row (`InView` keeps its turning points inside the view), then glides back up from there
  into its marching slot, in sight the whole way; divers are left out of the edge, landing and front-column
  checks. Also hosts the army-wide freeze and spatial queries
  (`NearestSamurai`, `SamuraiWithin`, `FrontSamuraiInColumn`, `ClosestColumnTo`) used by boons.
  Partial parts: `SamuraiArmy.Formation.cs` lays the slots out by `LevelConfig.shape` (`FormationShape`: `Grid`,
  `Staggered` rows offset a quarter slot, `Squads` of `squads` blocks `squadGap` apart, `Wedge`), through `SpacingFor`
  (column gap, so the widest shape still fits 60% of the view), `HasSlot` and `SlotX`. `SamuraiArmy.Knockback.cs`:
  `Shove(soldier, up)` lifts one soldier back up the field, capped by `MaxShove` (1.6), its slot height
  (`Samurai.HomeY`, set at spawn) and the top of the view; it stays where it lands, so the formation goes ragged; divers
  are skipped, a loose soldier stays loose, and the army stops `holding` once none is left in the last row
  (`AnyAtRow`). `SamuraiArmy.Elite.cs`: `Promote` runs after the spawn and raises the veteran rows (`rowHealth`, an
  `int[]` by row from the top, 0 keeps the kind's own; only ever raises) and picks the round's one Elite
  (`LevelConfig.eliteHealth`, 0 for none) from the soldiers that can shoot, near the middle of the formation;
  `UpdateElite` (from `FixedUpdate`) fires the Elite's own shot every `eliteFireInterval` (2.4 s average) at 1.1x speed,
  shielded when the stage rolls shields. Volleys: each `FireShot` looses `LevelConfig.volley` shots from columns at
  least two apart; every shot is rolled by `RollMods` into a `ShotMods` (`speed`, `toughness`, `guarded`) from
  `fastArrowChance` (speed `fastArrowScale`, 1.25), `toughArrowChance` (`Toughness` 2) and `guardedArrowChance`
  (`Arrow.Guard`; outranks shielded). Poguru's practice armies and Kagetsune's levy keep every new field off.
  `Samurai.Rank.cs`: `MakeElite` (x1.25 scale, x3 points, clan-coloured halo and edge), `MakeVeteran` (+10 points, a
  thin steel edge) and `UpdateBurn` (Burning wears a soldier with more than one HP down every `Statuses.BurnTick`, never
  to zero).
- `Samurai.cs` — one soldier (partial; see `Samurai.Rank.cs` above): `Init`, `Fire`, `TakeHit`, `Kill`, frost tint, and `Encase` (ice from Frost Blade / Frozen Riposte: holds its fire, and the ronin's next hit kills it outright).
- `SamuraiKind.cs` — `Ashigaru` (single arrow), `Archer` (3-arrow fan), `Taisho` (2 HP, unbreakable spear
  thrown on its own army timer, `SamuraiArmy.ThrowSpear`, over the ranks in front), `Shieldbearer` (2 HP,
  steel-tinted, never fires; `FrontShooterInColumn` lets the rank behind shoot over it) and `Yurei` (a ghost in the
  Bushi art, drawn translucent cyan over a spirit glow by `Samurai.SetFrost`; fires a parted pair of arrows with
  `Arrow.PassesCover`, so cover doesn't stop them) and `Bellringer` (2 HP, never fires; `CanFireKind` lets the
  rank behind shoot over it like a Shieldbearer. `SamuraiArmy.UpdateBells` picks a living one every `bellInterval`,
  `Samurai.WarnToll` glows for `tollWarning`, then `Toll` rings a teal pulse and the army's fire interval is scaled
  by `tollFireScale` for `tollHaste`; one cut down first never rings, and each kill pushes the next toll back by
  `silenceDelay` with a `Sfx.BellSilenced` chime). Rows pick a kind via
  `LevelConfig.rowKinds`; art and points come from `SamuraiArmy.ArtFor` / `PointsFor`
  (`samuraiByRow` art: Taisho, Bushi, Bushi, Ashigaru; the Taisho kind wears the Taisho art, the Archer the Bushi's).
  The Bellringer, Shieldbearer and Yurei wear their own art (`bellringerArt`, `shieldbearerArt`, `yureiArt`, via
  `OwnArt`); a kind without it wears the Bushi as a `Samurai.StandIn`, tinted bronze, steel or a stronger cyan
  (`Samurai.LookTint` gives that look without statuses; the encyclopedia reads it with `SamuraiArmy.ArtOf` / `StandsIn`).
  `Elite` is not a kind but a modifier any kind can wear (`Samurai.IsElite`); its encyclopedia id is the reserved
  `Foes.Elite`, so no `SamuraiKind` may ever be named that.
  Every kind, and the kite scout, has an entry in the memorial stones' encyclopedia (`Foes.All` in `EncyclopediaUI.cs`;
  its id is the kind's name, so a new `SamuraiKind` needs one and an old one is never renamed). The
  look is the undead army of [Story/Army.md](../Story/Army.md); its death debris is `Effects/Bone` and `Ash` (petals and
  smoke without them).
- `Arrow.cs` — enemy projectile; also restyled into boss shots (`Restyle`, `MakeReflectable`, `Arrow.Fan`).
  Arrows break on each other unless both have `PassesShots` (Shirogane's). `Toughness` (Shirogane's crescents: 2) is how
  many plain slashes it takes to cut one; the shuriken, a piercing slash and parried shots break it outright.
  `MakeUnbreakable` makes the Taisho spear: it passes through slashes, arrows and blockers and only stops on the
  ronin, so it must be dodged or parried (a parried one gives a riposte that pierces two samurai). `Guard` is the army's
  parry-only arrow: unbreakable like the spear, but `BreaksOnCover` so the courtyard blockers still stop it.
  `Toughness` above 1 draws a white shield ring (`ShowShield`) that shrinks per cut. `Unparryable` (the violet
  seal-dart) now implies `Unbreakable`. `ShotClass.cs` derives the colour class from the flags (`ShotClasses.Of`) and
  `Arrow.Dress` draws it as a `SpriteOutline` edge plus halo; it runs once `Start` has run and again whenever a flag
  changes. `Tint` recolours a plain arrow (the yurei's). Its yari art
  (`Art/Sprites/Projectiles/Spear.png`) reaches it through `Samurai.spearSprite`, wired by `Ninja/Apply Sprite Art`.
- `SamuraiCounter.cs` — the army's remaining-samurai count; its scene text is hidden by `GameUI`, as the HUD no longer shows it.

## Mini-enemy: Scout
- `Scout.cs` — kite-borne scout crossing the top of the screen (the mystery-ship role): a corpse lashed to a torn
  kite. One hit, bonus points, guaranteed soul. Built in code (no prefab). Wears `SamuraiArmy.ScoutArt` under a pale
  glow; without it the Ashigaru art tinted red under a warm one. Spawned by `GameManager.UpdateScouts` in rounds with
  `LevelConfig.scouts = true`, every 15–25 s.

## Bullet hell
- `BulletHell.cs` — the static kit bosses build curtains from, written round the ronin's one axis: a pattern only
  matters where it crosses his row, so every crossing leaves `MinGap` (2.2) clear and the next gap is never more than
  `MaxDodgeSpeed` (6 u/s, 60% of his speed) away. Shooters are `BulletHell.Shooter` delegates (a boss's own styled
  shot). `Enlist` puts a shot on the `BossBullet` physics layer (it copies the `Arrow` layer's collisions at first use,
  minus arrows and itself; `PassesShots` without the layer), counts it against `Budget` (`Room` waits for space) and
  marks it `Arrow.CappedRiposte`. Patterns: `Ring`, `Fan` (aimed, capped at `AimedSpeedCap`), `Spiral` (its interval
  is worked out from the height so one arm's landings sit a gap apart; only downward arms fire), `Curtain` (a row with
  gaps; `shooterFor` swaps single shots, e.g. a bell), `NextGap`/`GapOk` (staircase steps and a debug check),
  `HaltAndAim`. Colour language: `ShotClass` for every shot, army and boss alike: white
  (`Open`) = cut or parry; red (`Guarded`: `Unbreakable` or `Reflectable`) = parry-only (blades break on it: the spear,
  the army's `Guard` arrows, the moons, fang, pillars, cleaves, coffin); violet (`Sealed`: `Unparryable`, `BulletHell.Violet`)
  = dodge-only, made by `MakeSealDart` (passes cover); bronze (`Bell`: `Resonant`) = `MakeBell`/`MakeResonant` (parried,
  `Resonance` pops every breakable shot within `ResonanceRadius`). `Crimson` is no longer used for darts.
- `ArrowShadow.cs` — every `Arrow` (`Arrow.Awake`) marks where it will cross the ronin's row, glowing in its
  `GlowColor` and brightening as it nears; none for shots rising or nearly level.
- Parry rules in `Ronin`: `DeflectNearbyArrows` fizzles on `Unparryable` shots (`SealedParry`, raising `ParryFizzled`;
  `GameManager.OnParryFizzled` shows a one-time hint) and `Deflect` sends back at most `RiposteCap` (3) capped
  shots per parry, popping the rest (`Arrow.Pop`) while still landing the parry (`LandParry`).
