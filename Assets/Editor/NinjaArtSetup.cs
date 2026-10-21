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

    [MenuItem("Ninja/Apply Sprite Art")]
    public static void Apply()
    {
        Sprite[] samuraiParticles = { Load("Effects/Petal"), Load("Effects/Smoke") };

        CharacterArt ronin = SaveArt("Ronin", Frames("Ronin", "Idle"), Frames("Ronin", "Throw", 2, 3, 4),
            Frames("Ronin", "Hit", 2, 3), null, new[] { Load("Effects/Spark"), Load("Effects/Smoke") }, 5f, 12f);
        // Separate west/east clips rather than one flipped, so his armoured arm stays on the right side.
        ronin.runLeft = Frames("Ronin", "RunWest");
        ronin.runRight = Frames("Ronin", "RunEast");
        CharacterArt ashigaru = SamuraiArt("Ashigaru", samuraiParticles);
        CharacterArt bushi = SamuraiArt("Bushi", samuraiParticles);
        CharacterArt taisho = SamuraiArt("Taisho", samuraiParticles);

        EditPrefab("Assets/Alien.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: true);
            // Feet on the bottom of the collider sphere (radius 0.5 in local space).
            AddSprite(root, "Samurai", Load("Characters/Taisho/Taisho_Base"), new Vector3(0, -0.5f, 0), true);
        });

        EditPrefab("Assets/Spaceship.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: false);
            // The ronin's feet sit 8px above the bottom of his 64px canvas.
            GameObject body = AddSprite(root, "Ronin", Load("Characters/Ronin/Ronin_Base"), new Vector3(0, -0.98f, 0), true);
            // Fit the hitbox to the drawn body and the spirit flames around him rather than the whole canvas.
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0.01f, 0.69f, 0);
            box.size = new Vector3(0.77f, 1.06f, 0.5f);
            Set(root.GetComponent<Spaceship>(), "art", ronin);
        });

        EditPrefab("Assets/Dart.prefab", root =>
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
            Set(root.GetComponent<Dart>(), "spinner", (Object)null);
        });

        EditPrefab("Assets/AlienBullet.prefab", root =>
        {
            StripVisuals(root, keepRootCollider: false);
            root.transform.localScale = Vector3.one;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.08f;
            capsule.height = 0.42f;
            AddSprite(root, "Arrow", Load("Projectiles/Arrow"), Vector3.zero, false);
        });

        // Row 0 is the Taisho (30 pts), rows 1-2 Bushi (20), the rest Ashigaru (10), as in AlienFleet.PointsForRow.
        EditPrefab("Assets/AlienFleet.prefab", root =>
            Set(root.GetComponent<AlienFleet>(), "samuraiByRow", new Object[] { taisho, bushi, bushi, ashigaru }));

        // Reuse the scene if it's already open so unsaved edits in it aren't thrown away.
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        ShieldWall wall = Object.FindFirstObjectByType<ShieldWall>();
        Set(wall, "damageStates", new Object[] { Load("Barricade/Barricade_0"), Load("Barricade/Barricade_1"), Load("Barricade/Barricade_2") });
        Set(wall, "debrisSprites", new Object[] { Load("Effects/WoodChip") });
        SetUpBackdrop();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        Debug.Log("Ninja sprite art applied to Alien, Spaceship, Dart, AlienBullet, AlienFleet, the ShieldWall and the Backdrop.");
    }

    // Re-tunes just the backdrop, without rebuilding the prefabs (which reshuffles their internal IDs).
    [MenuItem("Ninja/Apply Backdrop Only")]
    public static void ApplyBackdropOnly()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SetUpBackdrop();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Backdrop applied.");
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
        so.FindProperty("ground").objectReferenceValue = Load("Background/Courtyard");
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
