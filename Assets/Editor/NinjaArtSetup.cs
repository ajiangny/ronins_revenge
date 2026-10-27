using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wires the PixelLab sprites into the gameplay prefabs and the scene. Safe to re-run after regenerating any art.
public static class NinjaArtSetup
{
    const string SpriteRoot = "Assets/Art/Sprites/";
    const string ArtRoot = "Assets/Art/Characters/";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    // Characters are drawn bigger than one 48px unit so they read clearly on the courtyard.
    const float SamuraiScale = 1.4f;
    const float RoninScale = 1.5f;

    [MenuItem("Ninja/Apply Sprite Art")]
    public static void Apply()
    {
        Sprite[] samuraiParticles = { Load("Effects/Petal"), Load("Effects/Smoke") };

        CharacterArt ronin = SaveArt("Ronin", Frames("Ronin", "Idle"), Frames("Ronin", "Throw", 2, 3, 4),
            Frames("Ronin", "Hit", 2, 3), null, new[] { Load("Effects/Spark"), Load("Effects/Smoke") }, 5f, 12f);
        // Separate west/east clips rather than one flipped, so his armoured arm stays on the right side.
        ronin.runLeft = Frames("Ronin", "RunWest");
        ronin.runRight = Frames("Ronin", "RunEast");
        SetUpWalks(ronin);
        // The middle of the katana swing: four frames finish inside the 0.35 s attack cooldown while Space is held.
        ronin.slash = Frames("Ronin", "Slash", 1, 2, 3, 4);
        CharacterArt ashigaru = SamuraiArt("Ashigaru", samuraiParticles);
        CharacterArt bushi = SamuraiArt("Bushi", samuraiParticles);
        CharacterArt taisho = SamuraiArt("Taisho", samuraiParticles);
        // The Bellringer's own look, once its frames exist; without them the army tints the Bushi bronze.
        CharacterArt bellringer = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "Characters/Bellringer/Bellringer_March_0.png") != null ? SamuraiArt("Bellringer", samuraiParticles) : null;

        EditPrefab("Assets/Samurai.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: true);
            root.transform.localScale = Vector3.one * SamuraiScale;
            // Feet on the bottom of the collider sphere (radius 0.5 in local space).
            AddSprite(root, "Samurai", Load("Characters/Taisho/Taisho_Base"), new Vector3(0, -0.5f, 0), true);
            Set(root.GetComponent<Samurai>(), "spearSprite", Load("Projectiles/Spear"));
        });

        EditPrefab("Assets/Ronin.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: false);
            // Ronin measures its sprite's world bounds at runtime, so clamping and TopY follow this scale.
            root.transform.localScale = Vector3.one * RoninScale;
            // The ronin's feet sit 8px above the bottom of his 64px canvas.
            GameObject body = AddSprite(root, "Ronin", Load("Characters/Ronin/Ronin_Base"), new Vector3(0, -0.98f, 0), true);
            // Fit the hitbox to the drawn body and the spirit flames around him rather than the whole canvas.
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0.01f, 0.69f, 0);
            box.size = new Vector3(0.77f, 1.06f, 0.5f);
            Set(root.GetComponent<Ronin>(), "art", ronin);
            Set(root.GetComponent<Ronin>(), "slashPrefab", BuildSlashWave());
        });

        EditPrefab("Assets/Kunai.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: false);
            root.transform.localScale = Vector3.one;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.1f;
            capsule.height = 0.46f;
            // The kunai flies point-first, so it doesn't spin. Drawn at half size to match the samurai arrows.
            GameObject kunai = AddSprite(root, "Kunai", Load("Projectiles/Kunai"), Vector3.zero, false);
            kunai.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            Set(root.GetComponent<Kunai>(), "spinner", (Object)null);
        });

        EditPrefab("Assets/Arrow.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: false);
            root.transform.localScale = Vector3.one;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.08f;
            capsule.height = 0.42f;
            AddSprite(root, "Arrow", Load("Projectiles/Arrow"), Vector3.zero, false);
        });

        // Row 0 is the Taisho (30 pts), rows 1-2 Bushi (20), the rest Ashigaru (10), as in SamuraiArmy.PointsForRow.
        EditPrefab("Assets/SamuraiArmy.prefab", root =>
        {
            var army = root.GetComponent<SamuraiArmy>();
            Set(army, "samuraiByRow", new Object[] { taisho, bushi, bushi, ashigaru });
            Set(army, "bellringerArt", bellringer);
            // The army's trampling and landing checks use the samurai collider's world radius; rows are spread to fit the bigger bodies.
            SetFloat(army, "samuraiRadius", 0.5f * SamuraiScale);
            SetFloat(army, "spacingY", 1.75f);
        });

        // Reuse the scene if it's already open so unsaved edits in it aren't thrown away.
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SetUpBlockers(Object.FindAnyObjectByType<BlockerWall>());
        SetUpBackdrop();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        Debug.Log("Ninja sprite art applied to Samurai, Ronin, Kunai, SlashWave, Arrow, SamuraiArmy, the BlockerWall and the Backdrop.");
    }

    // The katana's crescent wave: a Kunai projectile with a wide, flat hitbox. Built from a copy of the kunai prefab
    // so it keeps its layer and rigidbody. Returns null (the ronin then throws kunai) until the art exists.
    static GameObject BuildSlashWave()
    {
        const string path = "Assets/SlashWave.prefab";
        Sprite wave = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "Projectiles/SlashWave.png");
        if (wave == null) return null;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset("Assets/Kunai.prefab", path);

        EditPrefab(path, root =>
        {
            StripVisuals(root, keepRootCollider: false);
            root.transform.localScale = Vector3.one;
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.6f, 0.25f, 0.4f);
            // Drawn at half size like the kunai; Kunai.artScale brings it back up.
            GameObject art = AddSprite(root, "SlashWave", wave, Vector3.zero, false);
            art.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            Set(root.GetComponent<Kunai>(), "spinner", (Object)null);
        });
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // Re-tunes just the backdrop, without rebuilding the prefabs (which reshuffles their internal IDs).
    [MenuItem("Ninja/Apply Backdrop And Blockers Only")]
    public static void ApplyBackdropOnly()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SetUpBlockers(Object.FindAnyObjectByType<BlockerWall>());
        SetUpBackdrop();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Backdrop and blockers applied.");
    }

    // Each barricade site is a bush, a stone lantern and a bush. Lanterns are taller and tougher; bushes are wider.
    static void SetUpBlockers(BlockerWall wall)
    {
        var so = new SerializedObject(wall);
        FillBlockerTypes(so.FindProperty("types"), new[]
        {
            new BlockerSpec("Bush", "Blockers/Bush", "Effects/Leaf", 5, 1.2f, 3),
            new BlockerSpec("Lantern", "Blockers/Lantern", "Effects/StoneChip", 8, 1.8f, 3),
        });
        so.FindProperty("pattern").stringValue = "BLB";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // One kind of blocker: its first letter is its code in a site pattern, and its art is <sprite>_0..2 from intact to nearly
    // broken. Extra looks follow as <sprite>B_0..2, <sprite>C_0..2 and so on, one picked at random per blocker.
    struct BlockerSpec
    {
        public string name, sprite, debris;
        public int health;
        public float height;
        public int looks;

        public BlockerSpec(string name, string sprite, string debris, int health, float height, int looks = 1)
        {
            this.name = name;
            this.sprite = sprite;
            this.debris = debris;
            this.health = health;
            this.height = height;
            this.looks = looks;
        }

        public string Look(int index) => index == 0 ? sprite : sprite + (char)('A' + index);

        public bool HasArt
        {
            get
            {
                for (int look = 0; look < looks; look++)
                {
                    for (int d = 0; d < 3; d++)
                    {
                        if (AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{Look(look)}_{d}.png") == null) return false;
                    }
                }
                return true;
            }
        }
    }

    // Each map's own cover, matching its ground. The courtyard keeps the bushes and lanterns above.
    static readonly Dictionary<string, (BlockerSpec[] types, string pattern)> MapBlockers = new()
    {
        // Roadside stones either side of a clump of bamboo.
        ["BambooRoad_Stage1"] = (new[]
        {
            new BlockerSpec("Stone", "Blockers/Stone", "Effects/StoneChip", 5, 1.1f, 3),
            new BlockerSpec("Bamboo", "Blockers/Bamboo", "Effects/Leaf", 8, 1.8f, 3),
        }, "SBS"),
        // Mountain boulders flanking a roadside Jizo statue.
        ["Pass_Stage2"] = (new[]
        {
            new BlockerSpec("Rock", "Blockers/Rock", "Effects/StoneChip", 6, 1.1f, 3),
            new BlockerSpec("Jizo", "Blockers/Jizo", "Effects/StoneChip", 8, 1.7f, 3),
        }, "RJR"),
        // Crimson maple shrubs either side of a stone komainu guardian.
        ["Arena_Benibana"] = (new[]
        {
            new BlockerSpec("Maple", "Blockers/Maple", "Effects/Petal", 5, 1.2f, 3),
            new BlockerSpec("Komainu", "Blockers/Komainu", "Effects/StoneChip", 8, 1.7f, 3),
        }, "MKM"),
        // Piles of brass gears either side of a bronze clock pillar.
        ["Arena_Utsusemi"] = (new[]
        {
            new BlockerSpec("Gears", "Blockers/Gears", "Effects/Spark", 6, 1.1f, 3),
            new BlockerSpec("Clock", "Blockers/Clock", "Effects/StoneChip", 8, 1.8f, 3),
        }, "GCG"),
        // Snow-capped stones either side of a broken torii post.
        ["Shrine_Stage3"] = (new[]
        {
            new BlockerSpec("Snowstone", "Blockers/Snowstone", "Effects/StoneChip", 5, 1.1f, 3),
            new BlockerSpec("Torii", "Blockers/Torii", "Effects/StoneChip", 8, 1.8f, 3),
        }, "STS"),
        // Frosted shrubs either side of a snow-capped shrine lantern.
        ["Arena_Shirogane"] = (new[]
        {
            new BlockerSpec("Icebloom", "Blockers/Icebloom", "Effects/Leaf", 5, 1.2f, 3),
            new BlockerSpec("ShrineLantern", "Blockers/ShrineLantern", "Effects/StoneChip", 8, 1.7f, 3),
        }, "ISI"),
        // Grave markers either side of a temple bell frame on the mountain stair.
        ["Mountain_Stage4"] = (new[]
        {
            new BlockerSpec("Gravestone", "Blockers/Gravestone", "Effects/StoneChip", 6, 1.1f, 3),
            new BlockerSpec("Bellframe", "Blockers/Bellframe", "Effects/Spark", 8, 1.8f, 3),
        }, "GBG"),
        // Oni-faced braziers of soul-fire either side of a broken palace pillar.
        ["Arena_Kagetsune"] = (new[]
        {
            new BlockerSpec("OniBrazier", "Blockers/OniBrazier", "Effects/Spark", 6, 1.1f, 3),
            new BlockerSpec("Pillar", "Blockers/Pillar", "Effects/StoneChip", 8, 1.8f, 3),
        }, "OPO"),
    };

    // Copies the stages written in GameManager.DefaultStages into the scene's own serialized copy (which wins at runtime),
    // so a stage added in code reaches the game without a hand edit of the scene. Maps are left to Ninja/Apply Maps.
    [MenuItem("Ninja/Apply Stages")]
    public static void ApplyStages()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var manager = Object.FindAnyObjectByType<GameManager>();
        var so = new SerializedObject(manager);
        SerializedProperty stages = so.FindProperty("stages");
        StageConfig[] defaults = GameManager.DefaultStages();
        stages.arraySize = defaults.Length;
        for (int i = 0; i < defaults.Length; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            stage.FindPropertyRelative("name").stringValue = defaults[i].name;
            stage.FindPropertyRelative("boss").enumValueIndex = (int)defaults[i].boss;
            SerializedProperty rounds = stage.FindPropertyRelative("rounds");
            rounds.arraySize = defaults[i].rounds.Length;
            for (int r = 0; r < defaults[i].rounds.Length; r++)
            {
                LevelConfig level = defaults[i].rounds[r];
                SerializedProperty round = rounds.GetArrayElementAtIndex(r);
                round.FindPropertyRelative("columns").intValue = level.columns;
                round.FindPropertyRelative("rows").intValue = level.rows;
                round.FindPropertyRelative("topRowY").floatValue = level.topRowY;
                SerializedProperty kinds = round.FindPropertyRelative("rowKinds");
                kinds.arraySize = level.rowKinds?.Length ?? 0;
                for (int k = 0; k < kinds.arraySize; k++) kinds.GetArrayElementAtIndex(k).enumValueIndex = (int)level.rowKinds[k];
                round.FindPropertyRelative("scouts").boolValue = level.scouts;
                round.FindPropertyRelative("diveInterval").floatValue = level.diveInterval;
                round.FindPropertyRelative("baseSpeed").floatValue = level.baseSpeed;
                round.FindPropertyRelative("maxSpeedMultiplier").floatValue = level.maxSpeedMultiplier;
                round.FindPropertyRelative("fireInterval").floatValue = level.fireInterval;
                round.FindPropertyRelative("aimedShotChance").floatValue = level.aimedShotChance;
                round.FindPropertyRelative("arrowSpeed").floatValue = level.arrowSpeed;
                round.FindPropertyRelative("samuraiColor").colorValue = level.samuraiColor;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        SetUpMaps(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"{defaults.Length} stages applied to the scene from GameManager.DefaultStages, with their maps.");
    }

    static void FillBlockerTypes(SerializedProperty types, BlockerSpec[] specs)
    {
        types.arraySize = specs.Length;
        for (int i = 0; i < specs.Length; i++)
        {
            SerializedProperty type = types.GetArrayElementAtIndex(i);
            type.FindPropertyRelative("name").stringValue = specs[i].name;
            SerializedProperty states = type.FindPropertyRelative("damageStates");
            states.arraySize = 3;
            for (int d = 0; d < 3; d++)
            {
                states.GetArrayElementAtIndex(d).objectReferenceValue = Load($"{specs[i].sprite}_{d}");
            }
            SerializedProperty variants = type.FindPropertyRelative("variants");
            variants.arraySize = specs[i].looks - 1;
            for (int v = 0; v < variants.arraySize; v++)
            {
                SerializedProperty look = variants.GetArrayElementAtIndex(v).FindPropertyRelative("damageStates");
                look.arraySize = 3;
                for (int d = 0; d < 3; d++)
                {
                    look.GetArrayElementAtIndex(d).objectReferenceValue = Load($"{specs[i].Look(v + 1)}_{d}");
                }
            }
            SerializedProperty debris = type.FindPropertyRelative("debris");
            debris.arraySize = 1;
            debris.GetArrayElementAtIndex(0).objectReferenceValue = Load(specs[i].debris);
            type.FindPropertyRelative("health").intValue = specs[i].health;
            type.FindPropertyRelative("height").floatValue = specs[i].height;
            type.FindPropertyRelative("hitbox").vector2Value = new Vector2(0.85f, 0.85f);
        }
    }

    // Replaces the old starfield with the top-down courtyard backdrop on the same GameObject (z = 20, behind the play area).
    static void SetUpBackdrop()
    {
        GameObject go = GameObject.Find("Backdrop");
        if (go == null) go = GameObject.Find("NightSky");
        if (go == null) go = GameObject.Find("Starfield");
        go.name = "Backdrop";
        // Starfield.cs is deleted, so its component shows up as a missing script.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        if (!go.TryGetComponent(out Backdrop backdrop)) backdrop = go.AddComponent<Backdrop>();

        var so = new SerializedObject(backdrop);
        so.FindProperty("ground").objectReferenceValue = Load("Background/CourtyardGrass");
        so.FindProperty("groundTint").colorValue = new Color(0.8f, 0.8f, 0.9f);
        so.FindProperty("petal").objectReferenceValue = Load("Effects/Petal");
        so.ApplyModifiedPropertiesWithoutUndo();

        // Deep indigo shows only if the ground ever fails to cover the view.
        Camera.main.backgroundColor = new Color(0.07f, 0.06f, 0.16f);
        EditorUtility.SetDirty(Camera.main);
    }

    // The ronin's walks (off the field between maps, and every way around a safe area), and the ground of each stage and boss, without touching the prefabs.
    [MenuItem("Ninja/Apply Maps")]
    public static void ApplyMaps()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SetUpWalks(AssetDatabase.LoadAssetAtPath<CharacterArt>(ArtRoot + "Ronin.asset"));
        SetUpMaps(Object.FindAnyObjectByType<GameManager>());
        SetUpSafeAreas(Object.FindAnyObjectByType<GameManager>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Maps and the ronin's walks applied. Re-run Ninja/Apply Boon Art to recolour the walks for the elemental ronin.");
    }

    static void SetUpWalks(CharacterArt ronin)
    {
        ronin.walkUp = Frames("Ronin", "WalkNorth");
        ronin.walkDown = Frames("Ronin", "WalkSouth");
        ronin.walkUpLeft = Frames("Ronin", "WalkNorthWest");
        ronin.walkUpRight = Frames("Ronin", "WalkNorthEast");
        ronin.walkDownLeft = Frames("Ronin", "WalkSouthWest");
        ronin.walkDownRight = Frames("Ronin", "WalkSouthEast");
        ronin.idleDown = Frames("Ronin", "IdleSouth");
        ronin.walkFps = 9f;
        EditorUtility.SetDirty(ronin);
    }

    // Each stage fights on its RoundsGrounds entry, and each boss waits on Background/Arena_<boss>.
    // Each stage's rounds ground, in stage order, with the tint of the leaves drifting over it.
    static readonly (string ground, Color leaves)[] RoundsGrounds =
    {
        ("Background/BambooRoad_Stage1", new Color(0.65f, 0.85f, 0.45f, 0.6f)),
        ("Background/Pass_Stage2", new Color(0.85f, 0.75f, 0.5f, 0.6f)),
        // Snow, not leaves, drifts over the frozen shrine path.
        ("Background/Shrine_Stage3", new Color(0.9f, 0.96f, 1f, 0.75f)),
        // Red maple leaves fall dimly down the mountain stair.
        ("Background/Mountain_Stage4", new Color(0.75f, 0.25f, 0.2f, 0.55f)),
    };

    static void SetUpMaps(GameManager manager)
    {
        var so = new SerializedObject(manager);
        SerializedProperty stages = so.FindProperty("stages");
        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            var boss = (BossKind)stage.FindPropertyRelative("boss").enumValueIndex;
            // Stages past the listed grounds fight in the moonlit courtyard, whose bushes and lanterns are kept for them.
            string ground = i < RoundsGrounds.Length ? RoundsGrounds[i].ground : null;
            Color leaves = i < RoundsGrounds.Length ? RoundsGrounds[i].leaves : Color.white;
            SetMap(stage.FindPropertyRelative("roundsMap"), ground, Color.white, "Effects/Leaf", leaves);
            // Benibana's maple leaves are bright; dimmed so her crimson shots still stand out on them.
            SetMap(stage.FindPropertyRelative("bossMap"), boss == BossKind.None ? null : $"Background/Arena_{boss}",
                boss == BossKind.Benibana ? new Color(0.7f, 0.66f, 0.7f) : Color.white,
                boss == BossKind.Benibana ? "Effects/Leaf" : null, new Color(0.9f, 0.2f, 0.2f, 0.85f));
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Each safe area's map and station props, by the station's kind. The temple's gate is drawn on its map.
    static readonly (string ground, Color tint)[] SafeAreaGrounds = { ("Background/Temple_Hub", Color.white) };
    static readonly (StationKind kind, string art)[] StationArt =
    {
        (StationKind.FluteSongs, "Props/Temple_FluteAltar"),
        (StationKind.Memorial, "Props/Temple_Memorial"),
        (StationKind.WeaponRack, "Props/Temple_Rack"),
    };

    // The layout (floor, spots, footprints, pool, lamps) is copied from GameManager.DefaultSafeAreas, which is laid out
    // against the map, since the scene's own copy wins at runtime; the art is wired by kind.
    static void SetUpSafeAreas(GameManager manager)
    {
        var so = new SerializedObject(manager);
        SerializedProperty areas = so.FindProperty("safeAreas");
        SafeArea.Config[] layouts = GameManager.DefaultSafeAreas();
        if (areas.arraySize < layouts.Length) areas.arraySize = layouts.Length;
        for (int i = 0; i < areas.arraySize && i < SafeAreaGrounds.Length; i++)
        {
            SerializedProperty area = areas.GetArrayElementAtIndex(i);
            SetMap(area.FindPropertyRelative("map"), SafeAreaGrounds[i].ground, SafeAreaGrounds[i].tint, "Effects/Leaf", new Color(0.55f, 0.6f, 0.65f, 0.4f));
            if (i < layouts.Length) CopyLayout(area, layouts[i]);
            area.FindPropertyRelative("poolWater").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TempleLayers.Water);
            area.FindPropertyRelative("poolRimFront").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TempleLayers.RimFront);
            SerializedProperty fires = area.FindPropertyRelative("spiritFrames");
            var fireFrames = new List<Sprite>();
            for (int k = 0; AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}Fx/Temple_GhostFire_{k}.png") is Sprite frame; k++) fireFrames.Add(frame);
            fires.arraySize = fireFrames.Count;
            for (int k = 0; k < fireFrames.Count; k++) fires.GetArrayElementAtIndex(k).objectReferenceValue = fireFrames[k];
            SerializedProperty souls = area.FindPropertyRelative("poolSouls");
            var soulFrames = new List<Sprite>();
            for (int k = 0; AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}Fx/Temple_Soul_{k}.png") is Sprite frame; k++) soulFrames.Add(frame);
            souls.arraySize = soulFrames.Count;
            for (int k = 0; k < soulFrames.Count; k++) souls.GetArrayElementAtIndex(k).objectReferenceValue = soulFrames[k];
            SerializedProperty stations = area.FindPropertyRelative("stations");
            for (int j = 0; j < stations.arraySize; j++)
            {
                SerializedProperty station = stations.GetArrayElementAtIndex(j);
                var kind = (StationKind)station.FindPropertyRelative("kind").enumValueIndex;
                string art = System.Array.Find(StationArt, entry => entry.kind == kind).art;
                station.FindPropertyRelative("art").objectReferenceValue =
                    art != null ? AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + art + ".png") : null;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CopyLayout(SerializedProperty area, SafeArea.Config layout)
    {
        area.FindPropertyRelative("name").stringValue = layout.name;
        area.FindPropertyRelative("floor").rectValue = layout.floor;
        area.FindPropertyRelative("wakeSpot").vector2Value = layout.wakeSpot;
        area.FindPropertyRelative("poolCentre").vector2Value = layout.poolCentre;
        area.FindPropertyRelative("poolRadii").vector2Value = layout.poolRadii;
        SerializedProperty lamps = area.FindPropertyRelative("lamps");
        lamps.arraySize = layout.lamps.Length;
        for (int i = 0; i < layout.lamps.Length; i++)
        {
            SerializedProperty lamp = lamps.GetArrayElementAtIndex(i);
            lamp.FindPropertyRelative("px").vector2Value = layout.lamps[i].px;
            lamp.FindPropertyRelative("colour").colorValue = layout.lamps[i].colour;
            lamp.FindPropertyRelative("radius").floatValue = layout.lamps[i].radius;
        }
        SerializedProperty spirits = area.FindPropertyRelative("spirits");
        spirits.arraySize = layout.spirits.Length;
        for (int i = 0; i < layout.spirits.Length; i++) spirits.GetArrayElementAtIndex(i).vector2Value = layout.spirits[i];
        SerializedProperty walls = area.FindPropertyRelative("walls");
        walls.arraySize = layout.walls.Length;
        for (int i = 0; i < layout.walls.Length; i++) walls.GetArrayElementAtIndex(i).rectValue = layout.walls[i];
        SerializedProperty stations = area.FindPropertyRelative("stations");
        stations.arraySize = layout.stations.Length;
        for (int i = 0; i < layout.stations.Length; i++)
        {
            SafeArea.Station from = layout.stations[i];
            SerializedProperty station = stations.GetArrayElementAtIndex(i);
            station.FindPropertyRelative("kind").enumValueIndex = (int)from.kind;
            station.FindPropertyRelative("label").stringValue = from.label;
            station.FindPropertyRelative("spot").vector2Value = from.spot;
            station.FindPropertyRelative("onMap").boolValue = from.onMap;
            station.FindPropertyRelative("placeholder").colorValue = from.placeholder;
            station.FindPropertyRelative("reach").floatValue = from.reach;
            station.FindPropertyRelative("propAt").vector2Value = from.propAt;
            station.FindPropertyRelative("footprint").vector2Value = from.footprint;
            station.FindPropertyRelative("fx").enumValueIndex = (int)from.fx;
        }
    }

    // A missing ground sprite leaves the map empty, which the game reads as "stay where you are".
    static void SetMap(SerializedProperty map, string ground, Color groundTint, string petal, Color petalTint)
    {
        Sprite groundSprite = ground != null ? AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + ground + ".png") : null;
        map.FindPropertyRelative("ground").objectReferenceValue = groundSprite;
        map.FindPropertyRelative("groundTint").colorValue = groundTint;
        map.FindPropertyRelative("petal").objectReferenceValue = groundSprite != null && petal != null ? Load(petal) : null;
        map.FindPropertyRelative("petalTint").colorValue = petalTint;

        // A map whose blocker art isn't all there yet keeps the courtyard's.
        SerializedProperty types = map.FindPropertyRelative("blockers");
        if (groundSprite != null && MapBlockers.TryGetValue(groundSprite.name, out var set) && System.Array.TrueForAll(set.types, t => t.HasArt))
        {
            FillBlockerTypes(types, set.types);
            map.FindPropertyRelative("blockerPattern").stringValue = set.pattern;
        }
        else
        {
            types.arraySize = 0;
            map.FindPropertyRelative("blockerPattern").stringValue = "";
        }

        SetAtmosphere(map.FindPropertyRelative("atmosphere"),
            groundSprite != null && Moods.TryGetValue(groundSprite.name, out var mood) ? mood : new Atmosphere.Settings());
    }

    // Each map's mood, by its ground. Moonlight always pools from the top-left, where the boss moon rises.
    static readonly Dictionary<string, Atmosphere.Settings> Moods = new()
    {
        // Big slow cloud shadows sliding down the road, bright moonlight and fireflies in the bamboo.
        ["BambooRoad_Stage1"] = new Atmosphere.Settings
        {
            clouds = 4, cloudShadow = new Color(0.01f, 0.02f, 0.06f, 0.4f), wind = new Vector2(0.7f, -0.45f),
            moonlight = new Color(0.78f, 0.9f, 1f, 0.12f), shade = 0.4f, vignette = 0.35f,
            motes = 16, moteColor = new Color(0.8f, 1f, 0.45f, 0.85f),
        },
        // A windswept mountain pass: quicker clouds, low mist rolling over the stones, a thin moon.
        ["Pass_Stage2"] = new Atmosphere.Settings
        {
            clouds = 3, cloudShadow = new Color(0.02f, 0.02f, 0.05f, 0.28f), wind = new Vector2(1.4f, -0.2f),
            moonlight = new Color(0.75f, 0.82f, 1f, 0.08f), shade = 0.35f, vignette = 0.4f,
            mist = 5, mistColor = new Color(0.72f, 0.78f, 0.88f, 0.1f),
        },
        // The shrine under the blood moon: drifting embers and warm, heavy dark at the edges.
        ["Arena_Benibana"] = new Atmosphere.Settings
        {
            clouds = 2, cloudShadow = new Color(0.06f, 0.0f, 0.02f, 0.3f), wind = new Vector2(0.5f, -0.3f),
            moonlight = new Color(1f, 0.75f, 0.75f, 0.08f), shade = 0.4f, vignette = 0.45f,
            motes = 14, moteColor = new Color(1f, 0.5f, 0.3f, 0.85f),
        },
        // The clockwork temple: still air, dust hanging in a cold shaft of moonlight, a creeping floor mist.
        ["Arena_Utsusemi"] = new Atmosphere.Settings
        {
            clouds = 0, wind = new Vector2(0.25f, 0f),
            moonlight = new Color(0.7f, 0.8f, 1f, 0.14f), shade = 0.45f, vignette = 0.45f,
            mist = 2, mistColor = new Color(0.65f, 0.75f, 0.95f, 0.08f),
            motes = 22, moteColor = new Color(0.8f, 0.85f, 1f, 0.55f),
        },
        // The temple between runs: still air, a little low mist and a few teal spirit motes drifting off the pool.
        ["Temple_Hub"] = new Atmosphere.Settings
        {
            clouds = 2, cloudShadow = new Color(0.02f, 0.03f, 0.08f, 0.25f), wind = new Vector2(0.3f, -0.15f),
            moonlight = new Color(0.75f, 0.85f, 1f, 0.1f), shade = 0.35f, vignette = 0.45f,
            mist = 2, mistColor = new Color(0.7f, 0.85f, 0.9f, 0.08f),
            motes = 14, moteColor = new Color(0.45f, 1f, 0.9f, 0.7f),
        },
        // The frozen shrine path: heavy slow snow clouds, a cold hard moon, mist in the hollows and snowflakes in the air.
        ["Shrine_Stage3"] = new Atmosphere.Settings
        {
            clouds = 3, cloudShadow = new Color(0.02f, 0.03f, 0.08f, 0.32f), wind = new Vector2(0.5f, -0.6f),
            moonlight = new Color(0.75f, 0.85f, 1f, 0.12f), shade = 0.42f, vignette = 0.42f,
            mist = 4, mistColor = new Color(0.8f, 0.88f, 1f, 0.1f),
            motes = 20, moteColor = new Color(0.95f, 0.98f, 1f, 0.8f),
        },
        // Shirogane's court: dead still, spirit light pooling on the frost, a thick cyan mist and drifting spirit motes.
        ["Arena_Shirogane"] = new Atmosphere.Settings
        {
            clouds = 0, wind = new Vector2(0.2f, -0.15f),
            moonlight = new Color(0.6f, 0.85f, 1f, 0.16f), shade = 0.45f, vignette = 0.5f,
            mist = 6, mistColor = new Color(0.5f, 0.85f, 1f, 0.1f),
            motes = 26, moteColor = new Color(0.55f, 0.95f, 1f, 0.7f),
        },
        // The mountain stair under a starless sky: no moonlight at all, smoke drifting down from the shrine above,
        // crimson mist on the steps and a few teal souls in the air.
        ["Mountain_Stage4"] = new Atmosphere.Settings
        {
            clouds = 2, cloudShadow = new Color(0.03f, 0f, 0.01f, 0.3f), wind = new Vector2(0.3f, -0.5f),
            moonlight = new Color(0f, 0f, 0f, 0f), shade = 0.5f, vignette = 0.5f,
            mist = 4, mistColor = new Color(0.6f, 0.12f, 0.12f, 0.08f),
            motes = 14, moteColor = new Color(0.35f, 1f, 0.82f, 0.7f),
        },
        // The coronation hall: dead black but for the soul-fire. Heavy dark, low crimson smoke, and teal embers
        // drifting up from the braziers.
        ["Arena_Kagetsune"] = new Atmosphere.Settings
        {
            clouds = 0, wind = new Vector2(0.15f, 0.25f),
            moonlight = new Color(0f, 0f, 0f, 0f), shade = 0.55f, vignette = 0.55f,
            mist = 6, mistColor = new Color(0.55f, 0.08f, 0.1f, 0.1f),
            motes = 30, moteColor = new Color(0.35f, 1f, 0.82f, 0.8f),
        },
    };

    static void SetAtmosphere(SerializedProperty property, Atmosphere.Settings mood)
    {
        property.FindPropertyRelative("clouds").intValue = mood.clouds;
        property.FindPropertyRelative("cloudShadow").colorValue = mood.cloudShadow;
        property.FindPropertyRelative("wind").vector2Value = mood.wind;
        property.FindPropertyRelative("moonlight").colorValue = mood.moonlight;
        property.FindPropertyRelative("vignette").floatValue = mood.vignette;
        property.FindPropertyRelative("shade").floatValue = mood.shade;
        property.FindPropertyRelative("mist").intValue = mood.mist;
        property.FindPropertyRelative("mistColor").colorValue = mood.mistColor;
        property.FindPropertyRelative("motes").intValue = mood.motes;
        property.FindPropertyRelative("moteColor").colorValue = mood.moteColor;
    }

    static CharacterArt SamuraiArt(string name, Sprite[] particles)
    {
        // The template walk is 4 frames; the fall-back death is 7, of which every other frame reads fine at speed.
        return SaveArt(name, Frames(name, "March"), null, null, Frames(name, "Death", 2, 4, 6), particles, 6f, 8f);
    }

    static CharacterArt SaveArt(string name, Sprite[] idle, Sprite[] attack, Sprite[] hit, Sprite[] death,
        Sprite[] particles, float idleFps, float actionFps)
    {
        string path = ArtRoot + name + ".asset";
        var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(path);
        if (art == null)
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Characters");
            }
            art = ScriptableObject.CreateInstance<CharacterArt>();
            AssetDatabase.CreateAsset(art, path);
        }
        art.idle = idle;
        art.attack = attack;
        art.hit = hit;
        art.death = death;
        art.deathParticles = particles;
        art.idleFps = idleFps;
        art.actionFps = actionFps;
        EditorUtility.SetDirty(art);
        return art;
    }

    // With no indices, loads every numbered frame of the clip in order.
    static Sprite[] Frames(string character, string clip, params int[] indices)
    {
        string prefix = $"Characters/{character}/{character}_{clip}_";
        var frames = new List<Sprite>();
        if (indices.Length > 0)
        {
            foreach (int i in indices) frames.Add(Load(prefix + i));
        }
        else
        {
            for (int i = 0; AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + prefix + i + ".png") != null; i++)
            {
                frames.Add(Load(prefix + i));
            }
        }
        return frames.ToArray();
    }

    static Sprite Load(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + path + ".png");
        if (sprite == null) throw new System.IO.FileNotFoundException("Missing sprite " + SpriteRoot + path + ".png");
        return sprite;
    }

    static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Removes the old primitive meshes (and any sprite children from a previous run) so the prefab can be rebuilt.
    static void StripVisuals(GameObject root, bool keepRootCollider)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        }
        Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(root.GetComponent<MeshFilter>());
        if (!keepRootCollider)
        {
            foreach (Collider c in root.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }
    }

    // Children keep the root's layer so the physics collision matrix still applies to them.
    static GameObject AddSprite(GameObject root, string name, Sprite sprite, Vector3 localPosition, bool animated)
    {
        var child = new GameObject(name) { layer = root.layer };
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = localPosition;
        child.AddComponent<SpriteRenderer>().sprite = sprite;
        if (animated) child.AddComponent<SpriteAnimator>();
        return child;
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty array = so.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
