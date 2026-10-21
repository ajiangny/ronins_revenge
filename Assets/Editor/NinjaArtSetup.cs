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
        // The middle of the katana swing: four frames finish inside the 0.35 s attack cooldown while Space is held.
        ronin.slash = Frames("Ronin", "Slash", 1, 2, 3, 4);
        CharacterArt ashigaru = SamuraiArt("Ashigaru", samuraiParticles);
        CharacterArt bushi = SamuraiArt("Bushi", samuraiParticles);
        CharacterArt taisho = SamuraiArt("Taisho", samuraiParticles);

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
        (string name, string sprite, string debris, int health, float height)[] specs =
        {
            ("Bush", "Blockers/Bush", "Effects/Leaf", 5, 1.2f),
            ("Lantern", "Blockers/Lantern", "Effects/StoneChip", 8, 1.8f),
        };
        SerializedProperty types = so.FindProperty("types");
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
            SerializedProperty debris = type.FindPropertyRelative("debris");
            debris.arraySize = 1;
            debris.GetArrayElementAtIndex(0).objectReferenceValue = Load(specs[i].debris);
            type.FindPropertyRelative("health").intValue = specs[i].health;
            type.FindPropertyRelative("height").floatValue = specs[i].height;
            type.FindPropertyRelative("hitbox").vector2Value = new Vector2(0.85f, 0.85f);
        }
        so.FindProperty("pattern").stringValue = "BLB";
        so.ApplyModifiedPropertiesWithoutUndo();
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
