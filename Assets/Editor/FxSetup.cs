using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the FxLibrary from the PixelLab effect frames in Art/Sprites/Fx and hands it to the GameManager.
// Safe to re-run after re-exporting any effect.
public static class FxSetup
{
    const string FxRoot = "Assets/Art/Sprites/Fx/";
    const string LibraryPath = "Assets/Art/Fx/FxLibrary.asset";
    const string FlashMaterialPath = "Assets/Art/Fx/HitFlash.mat";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // id, frames per second, loops, and where the effect's anchor sits in its canvas (0..1 from the bottom-left).
    // Ground effects anchor on their strike point or feet rather than the canvas centre.
    static readonly (string id, float fps, bool loop, Vector2 pivot)[] Clips =
    {
        (Fx.Impact, 16, false, Centre),
        (Fx.IceBurst, 16, false, Centre),
        (Fx.SmokePuff, 12, false, Centre),
        (Fx.Explosion, 14, false, Centre),
        (Fx.SlashArc, 16, false, Centre),
        (Fx.LightningStrike, 14, false, new Vector2(0.5f, 18f / 128f)),
        (Fx.FrostWave, 14, false, new Vector2(0.5f, 24f / 64f)),
        (Fx.BoonPillar, 12, false, new Vector2(0.5f, 20f / 112f)),
        (Fx.WardBubble, 10, true, Centre),
        (Fx.Fireball, 14, true, Centre),
        (Fx.IceShard, 14, true, Centre),
        (Fx.Aura(Element.Fire), 12, true, AuraFeet),
        (Fx.Aura(Element.Water), 12, true, AuraFeet),
        (Fx.Aura(Element.Lightning), 12, true, AuraFeet),
        (Fx.SoulCrackle(Element.Fire), 12, true, Centre),
        (Fx.SoulCrackle(Element.Water), 12, true, Centre),
        (Fx.SoulCrackle(Element.Lightning), 12, true, Centre),
        (Fx.BeniSmokeRing, 10, true, Centre),
        (Fx.BeniCrescent, 14, true, Centre),
        // The draw streak starts at her and shoots right.
        (Fx.BeniIaidoFlash, 16, false, new Vector2(6f / 76f, 0.5f)),
        (Fx.BeniCross, 10, true, Centre),
        (Fx.BeniChain, 1, true, Centre),
        (Fx.BeniChainImpact, 16, false, new Vector2(40f / 87f, 1f - 31f / 63f)),
        (Fx.BeniSmokeVeil, 8, true, Centre),
        (Fx.BeniPetals, 14, false, Centre),
        (Fx.UtsuKasa, 14, true, Centre),
        (Fx.UtsuLace, 8, true, Centre),
        (Fx.UtsuPlumb, 8, true, Centre),
        (Fx.UtsuThrust, 14, true, Centre),
        // The jam bursts from where its sparks start, high in the canvas, so the steam has room to rise.
        (Fx.UtsuGears, 16, false, new Vector2(30f / 61f, 1f - 16f / 52f)),
        (Fx.UtsuParts, 14, false, Centre),
        (Fx.UtsuCharge, 16, false, Centre),
        (Fx.UtsuBeamFlare, 14, true, Centre),
        // Anchored on the hot spot where the beam lands, so the sparks and smoke rise above it.
        (Fx.UtsuBeamHit, 16, true, new Vector2(0.5f, 1f - 32f / 40f)),
        // The ground fire stands on its base line.
        (Fx.UtsuBurn, 12, true, new Vector2(0.5f, 1f - 30f / 35f)),
        (Fx.UtsuChop, 16, false, Centre),
        // The hour slash flies from the belly of its crescent, its leading edge, where the hitbox sits.
        (Fx.UtsuHourSlash, 14, true, new Vector2(0.5f, 1f - 48f / 64f)),
        (Fx.UtsuClockMark, 10, true, Centre),
        // The thrown sword stands planted where its blade meets the ground.
        (Fx.UtsuSword, 8, true, new Vector2(0.5f, 1f - 72f / 88f)),
        (Fx.UtsuPlumbPortal, 10, true, Centre),
        (Fx.ShiroCrescent, 14, true, Centre),
        (Fx.ShiroSword, 10, true, Centre),
        (Fx.ShiroOrb, 12, true, Centre),
        (Fx.ShiroWolfCharge, 14, true, Centre),
        (Fx.ShiroMist, 12, false, Centre),
        (Fx.ShiroBeamFlare, 14, true, Centre),
        (Fx.ShiroMoon, 8, true, Centre),
        (Fx.ShiroMawOrb, 14, true, Centre),
        (Fx.ShiroMawBeam, 16, true, Centre),
        // The splash sits on the point where the beam lands, near the foot of its canvas.
        (Fx.ShiroMawHit, 16, true, new Vector2(0.5f, 1f - 46f / 64f)),
        (Fx.ShiroMawBurst, 16, false, Centre),
        (Fx.ShiroLockOn, 12, true, Centre),
    };

    static Vector2 Centre => new Vector2(0.5f, 0.5f);
    static Vector2 AuraFeet => new Vector2(0.5f, 14f / 112f);

    [MenuItem("Ninja/Apply VFX")]
    public static void Apply()
    {
        var library = AssetDatabase.LoadAssetAtPath<FxLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<FxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        var clips = new List<FxLibrary.Clip>();
        foreach (var (id, fps, loop, pivot) in Clips)
        {
            Sprite[] frames = LoadFrames(id, pivot);
            if (frames.Length == 0)
            {
                Debug.LogWarning($"No frames for effect {id} in {FxRoot}; it will use its fallback.");
                continue;
            }
            clips.Add(new FxLibrary.Clip { id = id, frames = frames, fps = fps, loop = loop });
        }
        library.clips = clips.ToArray();
        library.flashMaterial = FlashMaterial();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = new SerializedObject(Object.FindAnyObjectByType<GameManager>());
        manager.FindProperty("fxLibrary").objectReferenceValue = library;
        manager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"VFX applied: {clips.Count} effects in {LibraryPath}.");
    }

    // Frames <id>_0, <id>_1, ... with their pivot set on import so each effect plays from its anchor.
    static Sprite[] LoadFrames(string id, Vector2 pivot)
    {
        var frames = new List<Sprite>();
        for (int i = 0; ; i++)
        {
            string path = $"{FxRoot}{id}_{i}.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) break;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom || settings.spritePivot != pivot)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            frames.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        return frames.ToArray();
    }

    static Material FlashMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        if (material != null) return material;
        material = new Material(Shader.Find("Ninja/SpriteFlash"));
        AssetDatabase.CreateAsset(material, FlashMaterialPath);
        return material;
    }
}
