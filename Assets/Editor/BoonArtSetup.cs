using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the elemental ronin variants and wires the soul/boon art into the scene. Safe to re-run;
// any art that doesn't exist yet is skipped, and the game falls back to placeholders for it.
public static class BoonArtSetup
{
    const string SpriteRoot = "Assets/Art/Sprites/";
    const string ArtRoot = "Assets/Art/Characters/";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // The ronin's spirit flames, the slash wave and the spirit kunai are teal-green (hue ~160°); his red scarf and skin sit near 0-20°,
    // so only this hue band is recoloured.
    const float FlameHueMin = 120f / 360f;
    const float FlameHueMax = 200f / 360f;

    static readonly (Element element, string suffix, float hue, float valueBoost)[] Variants =
    {
        (Element.Fire, "Fire", 18f / 360f, 1.1f),
        (Element.Water, "Water", 205f / 360f, 1f),
        (Element.Lightning, "Lightning", 280f / 360f, 1.05f),
    };

    [MenuItem("Ninja/Apply Boon Art")]
    public static void Apply()
    {
        CharacterArt baseRonin = AssetDatabase.LoadAssetAtPath<CharacterArt>(ArtRoot + "Ronin.asset");
        Sprite[] parry = Frames("Characters/Ronin/Ronin_Parry_");
        if (parry.Length > 0)
        {
            baseRonin.parry = parry;
            EditorUtility.SetDirty(baseRonin);
        }
        Sprite[] wisps = ExtractWisps(Load("Characters/Ronin/Ronin_Base"));
        if (wisps.Length > 0)
        {
            baseRonin.wisps = wisps;
            baseRonin.framesWithWisps = FramesWithWisps(baseRonin);
            EditorUtility.SetDirty(baseRonin);
        }

        Sprite shuriken = Load("Projectiles/Shuriken");
        if (shuriken != null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Ronin.prefab");
            try
            {
                var so = new SerializedObject(root.GetComponent<Ronin>());
                so.FindProperty("shurikenSprite").objectReferenceValue = shuriken;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Ronin.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var manager = Object.FindAnyObjectByType<GameManager>();
        var managerSo = new SerializedObject(manager);
        SerializedProperty elementArt = managerSo.FindProperty("elementArt");
        elementArt.arraySize = Variants.Length;
        for (int i = 0; i < Variants.Length; i++)
        {
            var (element, suffix, hue, boost) = Variants[i];
            SerializedProperty entry = elementArt.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("element").enumValueIndex = (int)element;
            SetArray(entry.FindPropertyRelative("soulFrames"), Frames($"Effects/Soul_{suffix}_"));
            entry.FindPropertyRelative("ronin").objectReferenceValue = BuildRoninVariant(baseRonin, suffix, hue, boost);
            entry.FindPropertyRelative("slash").objectReferenceValue = Recolor(Load("Projectiles/SlashWave"), suffix, hue, boost);
            entry.FindPropertyRelative("kunai").objectReferenceValue = Recolor(Load("Projectiles/Kunai"), suffix, hue, boost);
            // Each element has its own drawn shuriken (PixelLab); without it the base one is recoloured.
            Sprite elementShuriken = Load($"Projectiles/ShurikenArt_{suffix}");
            entry.FindPropertyRelative("shuriken").objectReferenceValue = elementShuriken != null ? elementShuriken : Recolor(shuriken, suffix, hue, boost);
            // The boon screen is painted: Oni_Boon_<suffix> and the soul its catcher holds out. Without the
            // paintings the pixel portrait is recoloured and the pixel soul is shown.
            Sprite painted = BossArtSetup.Portrait($"Oni_Boon_{suffix}");
            entry.FindPropertyRelative("portrait").objectReferenceValue = painted != null ? painted : RecolorPortrait(Load("UI/RoninPortrait"), suffix, hue, boost);
            entry.FindPropertyRelative("heldSoul").objectReferenceValue = BossArtSetup.Portrait($"Soul_{suffix}_Painted");
        }

        GameUI ui = Object.FindAnyObjectByType<GameUI>();
        if (!ui.TryGetComponent(out BoonChoiceUI choice)) choice = ui.gameObject.AddComponent<BoonChoiceUI>();
        managerSo.FindProperty("boonChoice").objectReferenceValue = choice;
        managerSo.FindProperty("soulHintArrow").objectReferenceValue = Load("UI/SoulArrow");
        // The portal rewards' loops (PixelLab objects, see ARCHITECTURE's Portals), and the bead and scroll held out
        // on their boon screens.
        SetArray(managerSo.FindProperty("scrollFrames"), Frames("Effects/TechniqueScroll_"));
        SetArray(managerSo.FindProperty("beadFrames"), Frames("Effects/SpiritBead_"));
        SetArray(managerSo.FindProperty("noteFrames"), Frames("Effects/SpiritNotes_"));
        SetArray(managerSo.FindProperty("springFrames"), Frames("Effects/HealingSpring_"));
        managerSo.FindProperty("scrollPainted").objectReferenceValue = BossArtSetup.Portrait("TechniqueScroll_Painted");
        managerSo.FindProperty("beadPainted").objectReferenceValue = BossArtSetup.Portrait("SpiritBead_Painted");
        SerializedProperty bossIcons = managerSo.FindProperty("bossIcons");
        var kinds = System.Array.FindAll((BossKind[])System.Enum.GetValues(typeof(BossKind)), kind => kind != BossKind.None);
        bossIcons.arraySize = kinds.Length;
        for (int i = 0; i < kinds.Length; i++)
        {
            SerializedProperty icon = bossIcons.GetArrayElementAtIndex(i);
            icon.FindPropertyRelative("boss").enumValueIndex = (int)kinds[i];
            SetArray(icon.FindPropertyRelative("frames"), Frames($"Effects/BossIcon_{kinds[i]}_"));
        }
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        // The spiked ice shell drawn over ice statues.
        var army = Object.FindAnyObjectByType<SamuraiArmy>();
        var armySo = new SerializedObject(army);
        armySo.FindProperty("iceShell").objectReferenceValue = Load("Effects/IceShell");
        armySo.ApplyModifiedPropertiesWithoutUndo();

        var choiceSo = new SerializedObject(choice);
        var icons = new List<Sprite>();
        foreach (BoonId id in System.Enum.GetValues(typeof(BoonId))) icons.Add(Load("UI/Boons/" + id));
        SetArray(choiceSo.FindProperty("boonIcons"), icons.ToArray());
        SetArray(choiceSo.FindProperty("abilityIcons"), new[] { Load("UI/Abilities/Sword"), Load("UI/Abilities/Shuriken"), Load("UI/Abilities/Parry") });
        Sprite paintedRonin = BossArtSetup.Portrait("Oni_Boon");
        choiceSo.FindProperty("roninPortrait").objectReferenceValue = paintedRonin != null ? paintedRonin : Load("UI/RoninPortrait");
        choiceSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Boon art applied: elemental ronin variants, souls and boon icons.");
    }

    // Copies every clip of the ronin with his flames recoloured, into Characters/Ronin_<suffix>/.
    static CharacterArt BuildRoninVariant(CharacterArt source, string suffix, float hue, float boost)
    {
        string path = ArtRoot + "Ronin" + suffix + ".asset";
        var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(path);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<CharacterArt>();
            AssetDatabase.CreateAsset(art, path);
        }
        art.idle = Recolor(source.idle, suffix, hue, boost);
        art.attack = Recolor(source.attack, suffix, hue, boost);
        art.hit = Recolor(source.hit, suffix, hue, boost);
        art.death = Recolor(source.death, suffix, hue, boost);
        art.parry = Recolor(source.parry, suffix, hue, boost);
        art.slash = Recolor(source.slash, suffix, hue, boost);
        art.runLeft = Recolor(source.runLeft, suffix, hue, boost);
        art.runRight = Recolor(source.runRight, suffix, hue, boost);
        art.walkUp = Recolor(source.walkUp, suffix, hue, boost);
        art.walkDown = Recolor(source.walkDown, suffix, hue, boost);
        art.walkUpLeft = Recolor(source.walkUpLeft, suffix, hue, boost);
        art.walkUpRight = Recolor(source.walkUpRight, suffix, hue, boost);
        art.walkDownLeft = Recolor(source.walkDownLeft, suffix, hue, boost);
        art.walkDownRight = Recolor(source.walkDownRight, suffix, hue, boost);
        art.idleDown = Recolor(source.idleDown, suffix, hue, boost);
        art.idleLeft = Recolor(source.idleLeft, suffix, hue, boost);
        art.idleRight = Recolor(source.idleRight, suffix, hue, boost);
        art.idleUpLeft = Recolor(source.idleUpLeft, suffix, hue, boost);
        art.idleUpRight = Recolor(source.idleUpRight, suffix, hue, boost);
        art.idleDownLeft = Recolor(source.idleDownLeft, suffix, hue, boost);
        art.idleDownRight = Recolor(source.idleDownRight, suffix, hue, boost);
        art.wisps = Recolor(source.wisps, suffix, hue, boost);
        art.framesWithWisps = Recolor(source.framesWithWisps, suffix, hue, boost);
        art.idleFps = source.idleFps;
        art.actionFps = source.actionFps;
        art.runFps = source.runFps;
        art.walkFps = source.walkFps;
        art.deathParticles = source.deathParticles;
        EditorUtility.SetDirty(art);
        return art;
    }

    static Sprite[] Recolor(Sprite[] frames, string suffix, float hue, float boost)
    {
        if (frames == null) return null;
        var result = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++) result[i] = Recolor(frames[i], suffix, hue, boost);
        return result;
    }

    // Writes <folder>_<suffix>/<name>_<suffix>.png (characters) or <name>_<suffix>.png (everything else) next to the source.
    static Sprite Recolor(Sprite source, string suffix, float hue, float boost)
    {
        if (source == null) return null;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string folder = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string targetFolder = sourcePath.StartsWith(SpriteRoot + "Characters/") ? $"{folder}_{suffix}" : folder;
        string targetPath = $"{targetFolder}/{name}_{suffix}.png";
        Directory.CreateDirectory(targetFolder);

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(sourcePath));
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (p.a == 0) continue;
            Color.RGBToHSV(p, out float h, out float s, out float v);
            if (h < FlameHueMin || h > FlameHueMax || s < 0.15f) continue;
            Color shifted = Color.HSVToRGB(hue, s, Mathf.Clamp01(v * boost));
            pixels[i] = new Color32((byte)(shifted.r * 255), (byte)(shifted.g * 255), (byte)(shifted.b * 255), p.a);
        }
        texture.SetPixels32(pixels);
        File.WriteAllBytes(targetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(targetPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
    }

    // The boon screen portrait: his spirit wisps, oni eyes and blade glint take the element's colour, but the jade knee
    // guards (solid green patches below the belt) keep theirs. The wisps are a pale mint, so they get extra saturation,
    // or fire would come out peach. Writes UI/RoninPortrait_<suffix>.png.
    const int PortraitBeltRow = 130;
    const int KneeGuardMinPixels = 30;
    const float PortraitMinSaturation = 0.65f;

    static Sprite RecolorPortrait(Sprite source, string suffix, float hue, float boost)
    {
        if (source == null) return null;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string targetPath = $"{Path.GetDirectoryName(sourcePath).Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(sourcePath)}_{suffix}.png";

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(sourcePath));
        int width = texture.width, height = texture.height;
        Color32[] pixels = texture.GetPixels32();

        var flame = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) continue;
            Color.RGBToHSV(pixels[i], out float h, out float s, out float _);
            flame[i] = h >= FlameHueMin && h <= FlameHueMax && s >= 0.15f;
        }

        // Flood-fill each patch of green; a big patch that sits wholly below the belt is a knee guard.
        var seen = new bool[pixels.Length];
        var patch = new List<int>();
        var stack = new Stack<int>();
        for (int start = 0; start < pixels.Length; start++)
        {
            if (!flame[start] || seen[start]) continue;
            patch.Clear();
            stack.Push(start);
            seen[start] = true;
            int highestRow = height;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                patch.Add(i);
                int x = i % width, y = i / width;
                highestRow = Mathf.Min(highestRow, height - 1 - y);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int n = ny * width + nx;
                    if (!flame[n] || seen[n]) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }
            if (highestRow > PortraitBeltRow && patch.Count >= KneeGuardMinPixels) continue;

            foreach (int i in patch)
            {
                Color.RGBToHSV(pixels[i], out float _, out float s, out float v);
                Color shifted = Color.HSVToRGB(hue, Mathf.Max(s, PortraitMinSaturation), Mathf.Clamp01(v * boost));
                pixels[i] = new Color32((byte)(shifted.r * 255), (byte)(shifted.g * 255), (byte)(shifted.b * 255), pixels[i].a);
            }
        }

        texture.SetPixels32(pixels);
        File.WriteAllBytes(targetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(targetPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
    }

    // Only his base pose has the two spirit wisps floating beside him; his animation frames were drawn without them.
    // Cuts each wisp (a patch of flame-green) onto its own canvas the size of his frames, left to right, as
    // <name>_Wisp_<n>.png next to the source, so the Ronin can layer them on and bob them.
    const int MinWispPixels = 4;

    static Sprite[] ExtractWisps(Sprite source)
    {
        if (source == null) return new Sprite[0];
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string folder = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(sourcePath).Replace("_Base", "");

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(sourcePath));
        int width = texture.width, height = texture.height;
        Color32[] pixels = texture.GetPixels32();
        Object.DestroyImmediate(texture);

        var flame = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) continue;
            Color.RGBToHSV(pixels[i], out float h, out float s, out float _);
            flame[i] = h >= FlameHueMin && h <= FlameHueMax && s >= 0.15f;
        }

        var patches = new List<(int left, List<int> pixels)>();
        var seen = new bool[pixels.Length];
        var stack = new Stack<int>();
        for (int start = 0; start < pixels.Length; start++)
        {
            if (!flame[start] || seen[start]) continue;
            var patch = new List<int>();
            int left = width;
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                patch.Add(i);
                int x = i % width, y = i / width;
                left = Mathf.Min(left, x);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int n = ny * width + nx;
                    if (!flame[n] || seen[n]) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }
            if (patch.Count >= MinWispPixels) patches.Add((left, patch));
        }
        patches.Sort((a, b) => a.left.CompareTo(b.left));

        var sprites = new Sprite[patches.Count];
        for (int w = 0; w < patches.Count; w++)
        {
            var canvas = new Color32[pixels.Length];
            foreach (int i in patches[w].pixels) canvas[i] = pixels[i];
            var wisp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            wisp.SetPixels32(canvas);
            string targetPath = $"{folder}/{name}_Wisp_{w}.png";
            File.WriteAllBytes(targetPath, wisp.EncodeToPNG());
            Object.DestroyImmediate(wisp);
            AssetDatabase.ImportAsset(targetPath);
            sprites[w] = AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
        }
        return sprites;
    }

    // The run, hit, throw and early slash frames draw his wisps themselves: enough flame-green in the upper part of
    // the canvas, above the jade knee guards.
    const float WispZoneBottom = 0.45f;
    const int MinDrawnWispPixels = 8;

    static Sprite[] FramesWithWisps(CharacterArt art)
    {
        var found = new List<Sprite>();
        var clips = new[] { art.idle, art.attack, art.hit, art.death, art.parry, art.slash, art.runLeft, art.runRight, art.walkUp,
            art.walkDown, art.walkUpLeft, art.walkUpRight, art.walkDownLeft, art.walkDownRight, art.idleDown,
            art.idleLeft, art.idleRight, art.idleUpLeft, art.idleUpRight, art.idleDownLeft, art.idleDownRight };
        foreach (Sprite[] clip in clips)
        {
            if (clip == null) continue;
            foreach (Sprite frame in clip)
            {
                if (frame == null || found.Contains(frame)) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame)));
                Color32[] pixels = texture.GetPixels32();
                int width = texture.width, bottom = Mathf.RoundToInt(texture.height * WispZoneBottom);
                Object.DestroyImmediate(texture);

                int green = 0;
                for (int i = bottom * width; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0) continue;
                    Color.RGBToHSV(pixels[i], out float h, out float s, out float _);
                    if (h >= FlameHueMin && h <= FlameHueMax && s >= 0.15f) green++;
                }
                if (green >= MinDrawnWispPixels) found.Add(frame);
            }
        }
        return found.ToArray();
    }

    // Every numbered frame <prefix>0, <prefix>1, ... that exists.
    static Sprite[] Frames(string prefix)
    {
        var frames = new List<Sprite>();
        for (int i = 0; Load(prefix + i) is Sprite frame; i++) frames.Add(frame);
        return frames.ToArray();
    }

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + path + ".png");

    static void SetArray(SerializedProperty array, Sprite[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
