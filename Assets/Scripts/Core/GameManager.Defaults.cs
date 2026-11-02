using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// The data written in code: the safe areas and the stages. The scene keeps its own serialized copies, which win at
// runtime; Ninja/Apply Stages and Ninja/Apply Maps copy these into it.
public partial class GameManager
{
    // The stages as written in code. The scene keeps its own serialized copy, which wins at runtime; Ninja/Apply Stages
    // copies these into it (maps are wired separately by Ninja/Apply Maps).
    // The temple on the hill: its gate at the top leads out, its spirit pool at the bottom is where he wakes, and the
    // flute altar, memorial stones and weapon rack stand on the open flagstones between. Spots match Temple_Hub.png.
    public static SafeArea.Config[] DefaultSafeAreas() => new[]
    {
        new SafeArea.Config
        {
            name = "The Temple",
            floor = Rect.MinMaxRect(0.17f, 0.08f, 0.86f, 0.69f),
            wakeSpot = new Vector2(0.4985f, 0.17f),
            // The water, in map pixels (TempleLayers cuts the same ellipse out of the map).
            poolCentre = new Vector2(343f, 319f),
            poolRadii = new Vector2(39f, 30f),
            stations = new[]
            {
                new SafeArea.Station { kind = StationKind.Exit, label = "SET OUT", spot = new Vector2(0.5f, 0.68f), onMap = true, reach = 2.2f },
                // On the stone dais to the left, used from the top of its steps.
                new SafeArea.Station { kind = StationKind.FluteSongs, label = "FLUTE SONGS", spot = new Vector2(0.2689f, 0.4219f), propAt = new Vector2(185f, 180f),
                    footprint = new Vector2(2.4f, 0.6f), fx = StationFx.Incense, reach = 2f },
                // In the kerbed grave nook to the upper right, used from in front of its offerings.
                new SafeArea.Station { kind = StationKind.Memorial, label = "MEMORIAL STONES", spot = new Vector2(0.6933f, 0.4401f), propAt = new Vector2(477f, 170f),
                    placeholder = new Color(0.4f, 0.42f, 0.45f), footprint = new Vector2(4.2f, 0.6f), fx = StationFx.Glow, reach = 2f },
                // On the plank platform under the broken eave, used from its left end.
                new SafeArea.Station { kind = StationKind.WeaponRack, label = "WEAPON RACK", spot = new Vector2(0.7849f, 0.0885f), propAt = new Vector2(562f, 340f),
                    footprint = new Vector2(2.6f, 0.5f), fx = StationFx.Glint, reach = 2.2f },
                // On the flagstones to the lower left, between the dais and the pool, used from its right side.
                new SafeArea.Station { kind = StationKind.Oaths, label = "OATH STONE", spot = new Vector2(0.3125f, 0.1f), propAt = new Vector2(190f, 334f),
                    placeholder = new Color(0.32f, 0.36f, 0.3f), footprint = new Vector2(2f, 0.5f), fx = StationFx.Glow, glowColour = Oaths.HeatColour, reach = 2f },
                // Poguru's nest, an old offering bowl on the flagstones right of the pool, used from in front of it.
                new SafeArea.Station { kind = StationKind.Poguru, label = "POGURU", spot = new Vector2(0.6105f, 0.215f), propAt = new Vector2(452f, 292f),
                    placeholder = new Color(0.3f, 0.45f, 0.55f), footprint = new Vector2(1.2f, 0.4f), fx = StationFx.Glow, glowColour = PoguruColour, reach = 1.8f },
                // Asagi on the rim stones at the pool's back left, at the water's edge (clear of the way he climbs out,
                // straight up its middle), used from the flagstones beside her. Hidden until he's met her (SaveData.asagiMet).
                new SafeArea.Station { kind = StationKind.Asagi, label = "ASAGI", spot = new Vector2(0.42f, 0.217f), propAt = new Vector2(310f, 294f),
                    placeholder = Asagi.Colour, footprint = new Vector2(0.8f, 0.3f), reach = 1.8f },
                // Oliver at the top of the rack's dais, straight behind the rack, used from the boards in front of him.
                // Hidden until Oni has met him (SaveData.oliverMet).
                new SafeArea.Station { kind = StationKind.Oliver, label = "OLIVER", spot = new Vector2(0.8168f, 0.3238f), propAt = new Vector2(562f, 242f),
                    placeholder = new Color(0.878f, 0.659f, 0.227f), footprint = new Vector2(2.6f, 0.7f), reach = 1.8f },
            },
            // The ghost-fires at either side, and the two stone lanterns at the dais, lit.
            lamps = new[]
            {
                new SafeArea.Lamp { px = new Vector2(100f, 147f), colour = new Color(0.45f, 1f, 0.9f, 0.2f), radius = 2.6f },
                new SafeArea.Lamp { px = new Vector2(562f, 178f), colour = new Color(0.45f, 1f, 0.9f, 0.2f), radius = 2.6f },
                new SafeArea.Lamp { px = new Vector2(120f, 224f), colour = new Color(1f, 0.65f, 0.35f, 0.28f), radius = 2.2f },
                new SafeArea.Lamp { px = new Vector2(242f, 224f), colour = new Color(1f, 0.65f, 0.35f, 0.28f), radius = 2.2f },
            },
            // The ghost-fires at either side, lifted off the map so they flicker. The pool is left to the souls.
            spirits = new[] { new Vector2(100f, 154f), new Vector2(562f, 186f) },
            // The gate's doorway, whose leaves swing open as he comes up; past its threshold he's under its beam and
            // roof (raised off the map's top, so he stays hidden walking out of view) and between its posts.
            gateDoors = new Rect(309f, 58f, 70f, 50f),
            gateOver = new[] { new Rect(284f, -300f, 118f, 358f), new Rect(284f, 58f, 25f, 52f), new Rect(379f, 58f, 23f, 52f) },
            // He takes the steps up to the altar's dais and the side step onto the rack's platform; the toro lanterns,
            // the dais's raised edges, the platform's edges and the grave nook's kerb stand in his way.
            walls = new[]
            {
                new Rect(106f, 198f, 54f, 54f), new Rect(209f, 198f, 45f, 54f),
                new Rect(126f, 126f, 12f, 72f), new Rect(233f, 126f, 12f, 72f), new Rect(126f, 122f, 119f, 12f),
                new Rect(416f, 110f, 124f, 90f),
                new Rect(508f, 206f, 126f, 12f), new Rect(508f, 212f, 12f, 51f), new Rect(508f, 320f, 12f, 26f), new Rect(508f, 334f, 126f, 12f),
            },
        },
    };

    public static StageConfig[] DefaultStages() => new[]
    {
        new StageConfig
        {
            name = "The Bamboo Road",
            rounds = new[]
            {
                new LevelConfig { columns = 8,  rows = 3, topRowY = 8.5f, baseSpeed = 2.3f, maxSpeedMultiplier = 3.5f, fireInterval = 0.8f,   aimedShotChance = 0.3f,  arrowSpeed = 7f,   samuraiColor = new Color(0f, 0.98f, 0.6f) },
                new LevelConfig { columns = 9,  rows = 4, topRowY = 8.4f, baseSpeed = 2.5f, maxSpeedMultiplier = 4f,   fireInterval = 0.72f, aimedShotChance = 0.35f, arrowSpeed = 7.5f, samuraiColor = new Color(0f, 0.98f, 0.6f),
                    rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru }, diveInterval = 9f },
                new LevelConfig { columns = 10, rows = 4, topRowY = 8.2f, baseSpeed = 2.7f, maxSpeedMultiplier = 4.2f, fireInterval = 0.64f, aimedShotChance = 0.4f,  arrowSpeed = 8f,   samuraiColor = new Color(0f, 0.98f, 0.6f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Ashigaru }, scouts = true, diveInterval = 7f },
            },
            boss = BossKind.Benibana,
        },
        new StageConfig
        {
            name = "The Pilgrim's Pass",
            rounds = new[]
            {
                new LevelConfig { columns = 10, rows = 4, topRowY = 8.3f, baseSpeed = 2.6f, maxSpeedMultiplier = 4f,   fireInterval = 0.68f, aimedShotChance = 0.4f,  arrowSpeed = 8f,   samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer }, diveInterval = 7f },
                new LevelConfig { columns = 11, rows = 4, topRowY = 8.3f, baseSpeed = 2.8f, maxSpeedMultiplier = 4.2f, fireInterval = 0.64f,  aimedShotChance = 0.4f,  arrowSpeed = 8f,   samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Archer, SamuraiKind.Ashigaru }, diveInterval = 5.1f },
                new LevelConfig { columns = 11, rows = 5, topRowY = 8.5f, baseSpeed = 3f,   maxSpeedMultiplier = 4.4f, fireInterval = 0.6f, aimedShotChance = 0.45f, arrowSpeed = 8.5f, samuraiColor = new Color(0.4f, 0.6f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer },
                    diveInterval = 4.2f, scouts = true },
            },
            boss = BossKind.Utsusemi,
        },
        new StageConfig
        {
            name = "The Frozen Shrine",
            rounds = new[]
            {
                new LevelConfig { columns = 11, rows = 5, topRowY = 8.5f, baseSpeed = 3f,   maxSpeedMultiplier = 4.4f, fireInterval = 0.6f, aimedShotChance = 0.45f, arrowSpeed = 8.5f, samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Ashigaru, SamuraiKind.Shieldbearer }, diveInterval = 5.1f },
                new LevelConfig { columns = 12, rows = 5, topRowY = 8.6f, baseSpeed = 3.2f, maxSpeedMultiplier = 4.6f, fireInterval = 0.56f,  aimedShotChance = 0.5f,  arrowSpeed = 9f,   samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Yurei, SamuraiKind.Shieldbearer }, diveInterval = 4.2f, scouts = true },
                new LevelConfig { columns = 12, rows = 6, topRowY = 8.8f, baseSpeed = 3.4f, maxSpeedMultiplier = 4.8f, fireInterval = 0.52f, aimedShotChance = 0.55f, arrowSpeed = 9.5f, samuraiColor = new Color(0.75f, 0.9f, 1f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Taisho, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer },
                    diveInterval = 3.4f, scouts = true },
            },
            boss = BossKind.Shirogane,
        },
        // The mountain shrine where the Night of a Thousand Bells was held: every kind of soldier he has, and his
        // bellringers, whose tolls rouse the ranks. The army wears crimson and bronze, the court's colours.
        new StageConfig
        {
            name = "The Mountain Shrine",
            rounds = new[]
            {
                new LevelConfig { columns = 12, rows = 5, topRowY = 8.6f, baseSpeed = 3.3f, maxSpeedMultiplier = 4.6f, fireInterval = 0.56f,  aimedShotChance = 0.5f,  arrowSpeed = 9f,   samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Bellringer, SamuraiKind.Archer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer }, diveInterval = 4.2f },
                new LevelConfig { columns = 12, rows = 6, topRowY = 8.8f, baseSpeed = 3.5f, maxSpeedMultiplier = 4.8f, fireInterval = 0.53f, aimedShotChance = 0.55f, arrowSpeed = 9.5f, samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Bellringer, SamuraiKind.Taisho, SamuraiKind.Archer, SamuraiKind.Bellringer, SamuraiKind.Yurei, SamuraiKind.Shieldbearer },
                    diveInterval = 3.8f, scouts = true },
                new LevelConfig { columns = 13, rows = 6, topRowY = 9f,   baseSpeed = 3.6f, maxSpeedMultiplier = 5f,   fireInterval = 0.5f, aimedShotChance = 0.6f,  arrowSpeed = 10f,  samuraiColor = new Color(1f, 0.55f, 0.4f),
                    rowKinds = new[] { SamuraiKind.Taisho, SamuraiKind.Bellringer, SamuraiKind.Yurei, SamuraiKind.Archer, SamuraiKind.Bellringer, SamuraiKind.Shieldbearer },
                    diveInterval = 3f, scouts = true },
            },
            boss = BossKind.Kagetsune,
        },
    };
}
