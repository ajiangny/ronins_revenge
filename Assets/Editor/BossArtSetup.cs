using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the bosses' CharacterArt from their PixelLab frames and hands it to the GameManager in the scene, along
// with their intro portraits, voice lines and title-card fonts.
// Safe to re-run after regenerating any of the art.
public static class BossArtSetup
{
    const string SpriteRoot = "Assets/Art/Sprites/Characters/";
    const string ArtRoot = "Assets/Art/Characters/";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string PortraitRoot = "Assets/Art/Portraits/";
    const string VoiceRoot = "Assets/Audio/Voice/";
    // About twice a samurai's height, so she reads as the warlord the moment she lands.
    const float BenibanaScale = 2.3f;
    // Utsusemi stands taller than her, stilts and hat brim included.
    const float UtsusemiScale = 2.6f;
    // Shirogane stands about Utsusemi's height; her hair and wolf fill out the rest.
    const float ShiroganeScale = 2.5f;
    // Low enough that her ponytail clears the boss bar.
    const float BossDepth = 4.5f;

    [MenuItem("Ninja/Apply Boss Art")]
    public static void Apply()
    {
        var particles = new[]
        {
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Effects/Petal.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Effects/Smoke.png"),
        };

        // The pipe puff goes in the parry slot: CharacterArt has no slot of its own for it.
        CharacterArt benibana = SaveArt("Benibana",
            idle: Frames("Benibana", "Idle"),
            attack: Frames("Benibana", "Slash"),
            parry: Frames("Benibana", "Puff"),
            hit: Frames("Benibana", "Hit"),
            death: Frames("Benibana", "Defeat"),
            particles);

        // Utsusemi's frames may not exist yet; without them the GameManager keeps its stand-in.
        // The hat throw goes in the parry slot, the thrust in the attack slot.
        Sprite[] utsusemiIdle = TryFrames("Utsusemi", "Idle");
        CharacterArt utsusemi = utsusemiIdle == null ? null : SaveArt("Utsusemi",
            idle: utsusemiIdle,
            attack: TryFrames("Utsusemi", "Thrust") ?? utsusemiIdle,
            parry: TryFrames("Utsusemi", "Throw") ?? utsusemiIdle,
            hit: TryFrames("Utsusemi", "Hit") ?? utsusemiIdle,
            death: TryFrames("Utsusemi", "Defeat") ?? utsusemiIdle,
            new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Effects/Smoke.png") });

        // Shirogane: the blade-call (her swords wheeling about her) goes in the parry slot.
        Sprite[] shiroganeIdle = TryFrames("Shirogane", "Idle");
        CharacterArt shirogane = shiroganeIdle == null ? null : SaveArt("Shirogane",
            idle: shiroganeIdle,
            attack: TryFrames("Shirogane", "Slash") ?? shiroganeIdle,
            parry: TryFrames("Shirogane", "BladeCall") ?? TryFrames("Shirogane", "Slash") ?? shiroganeIdle,
            hit: TryFrames("Shirogane", "Hit") ?? shiroganeIdle,
            death: TryFrames("Shirogane", "Defeat") ?? shiroganeIdle,
            new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Effects/Smoke.png") });

        // Each boss's second-phase form, worn from its phase break on; any clip it lacks falls back to the first form's.
        CharacterArt benibanaPhase = PhaseArt("BenibanaPhase2", benibana, "Slash", "Puff");
        CharacterArt utsusemiPhase = PhaseArt("UtsusemiPhase2", utsusemi, "Thrust", "Throw");
        CharacterArt shiroganePhase = PhaseArt("ShiroganePhase2", shirogane, "Slash", "BladeCall");

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = Object.FindAnyObjectByType<GameManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("benibanaArt").objectReferenceValue = benibana;
        so.FindProperty("benibanaScale").floatValue = BenibanaScale;
        so.FindProperty("benibanaPhaseArt").objectReferenceValue = benibanaPhase;
        so.FindProperty("bossDepth").floatValue = BossDepth;
        so.FindProperty("benibanaPortrait").objectReferenceValue = Portrait("Benibana_Full");
        so.FindProperty("benibanaCloseup").objectReferenceValue = Portrait("Benibana_Closeup");
        so.FindProperty("benibanaPhasePortrait").objectReferenceValue = Portrait("Benibana_Phase_Full");
        so.FindProperty("benibanaPhaseCloseup").objectReferenceValue = Portrait("Benibana_Phase_Closeup");
        so.FindProperty("benibanaIntroLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_Intro.mp3");
        so.FindProperty("benibanaPhaseLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_Phase.mp3");
        so.FindProperty("benibanaDefeatLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_Defeat.mp3");
        so.FindProperty("benibanaStaggerLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_Stagger.mp3");
        so.FindProperty("benibanaSmokeRingsLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_SmokeRings.mp3");
        so.FindProperty("benibanaRosaryLashLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_RosaryLash.mp3");
        so.FindProperty("benibanaIaidoLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_Iaido.mp3");
        so.FindProperty("benibanaRosaryBloomLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Benibana_RosaryBloom.mp3");
        SetTakes(so.FindProperty("benibanaHurtLines"), "Benibana_Hurt");

        if (utsusemi != null)
        {
            so.FindProperty("utsusemiArt").objectReferenceValue = utsusemi;
            so.FindProperty("utsusemiScale").floatValue = UtsusemiScale;
            SetFrames(so.FindProperty("utsusemiBareIdle"), TryFrames("Utsusemi", "BareIdle"));
            SetFrames(so.FindProperty("utsusemiDash"), TryFrames("Utsusemi", "Dash"));
            SetFrames(so.FindProperty("utsusemiChop"), TryFrames("Utsusemi", "Chop"));
            SetFrames(so.FindProperty("utsusemiHourSlash"), TryFrames("Utsusemi", "HourSlash"));
            SetFrames(so.FindProperty("utsusemiSwordThrow"), TryFrames("Utsusemi", "SwordThrow"));
            SetFrames(so.FindProperty("utsusemiUnarmedIdle"), TryFrames("Utsusemi", "UnarmedIdle"));
        }
        so.FindProperty("utsusemiPhaseArt").objectReferenceValue = utsusemiPhase;
        foreach (string clip in new[] { "BareIdle", "UnarmedIdle", "Dash", "Chop", "HourSlash", "SwordThrow" })
        {
            SetFrames(so.FindProperty("utsusemiPhase" + clip), utsusemiPhase == null ? null : TryFrames("UtsusemiPhase2", clip));
        }
        so.FindProperty("utsusemiPortrait").objectReferenceValue = Portrait("Utsusemi_Full");
        so.FindProperty("utsusemiCloseup").objectReferenceValue = Portrait("Utsusemi_Closeup");
        so.FindProperty("utsusemiPhasePortrait").objectReferenceValue = Portrait("Utsusemi_Phase_Full");
        so.FindProperty("utsusemiPhaseCloseup").objectReferenceValue = Portrait("Utsusemi_Phase_Closeup");
        so.FindProperty("utsusemiIntroLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Utsusemi_Intro.mp3");
        so.FindProperty("utsusemiPhaseLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Utsusemi_Phase.mp3");
        so.FindProperty("utsusemiDefeatLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Utsusemi_Defeat.mp3");
        so.FindProperty("utsusemiStaggerLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Utsusemi_Stagger.mp3");
        so.FindProperty("utsusemiKasaLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Utsusemi_Kasa.mp3");
        so.FindProperty("utsusemiHourStrikesLine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Utsusemi_HourStrikes.mp3");
        SetTakes(so.FindProperty("utsusemiHurtLines"), "Utsusemi_Hurt");

        if (shirogane != null)
        {
            so.FindProperty("shiroganeArt").objectReferenceValue = shirogane;
            so.FindProperty("shiroganeScale").floatValue = ShiroganeScale;
            SetFrames(so.FindProperty("shiroganeDash"), TryFrames("Shirogane", "Dash"));
        }
        so.FindProperty("shiroganePhaseArt").objectReferenceValue = shiroganePhase;
        SetFrames(so.FindProperty("shiroganePhaseDash"), shiroganePhase == null ? null : TryFrames("ShiroganePhase2", "Dash"));
        SetFrames(so.FindProperty("shiroWolfIdle"), TryFrames("SpiritWolf", "Idle"));
        SetFrames(so.FindProperty("shiroWolfHowl"), TryFrames("SpiritWolf", "Howl"));
        SetFrames(so.FindProperty("shiroWolfCharge"), TryFrames("SpiritWolf", "Charge"));
        SetFrames(so.FindProperty("shiroWolfVanish"), TryFrames("SpiritWolf", "Vanish"));
        so.FindProperty("shiroganePortrait").objectReferenceValue = Portrait("Shirogane_Full");
        so.FindProperty("shiroganeCloseup").objectReferenceValue = Portrait("Shirogane_Closeup");
        so.FindProperty("shiroganePhasePortrait").objectReferenceValue = Portrait("Shirogane_Phase_Full");
        so.FindProperty("shiroganePhaseCloseup").objectReferenceValue = Portrait("Shirogane_Phase_Closeup");
        foreach (var (field, file) in new[]
        {
            ("shiroganeIntroLine", "Intro"), ("shiroganePhaseLine", "Phase"), ("shiroganeDefeatLine", "Defeat"),
            ("shiroganeDefeatFinalLine", "DefeatFinal"), ("shiroganeStaggerLine", "Stagger"), ("shiroganeCrescentLine", "Crescent"),
            ("shiroganeBladeRainLine", "BladeRain"), ("shiroganeEclipseLine", "Eclipse"),
        })
        {
            so.FindProperty(field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + "Shirogane_" + file + ".mp3");
        }
        SetTakes(so.FindProperty("shiroganeHurtLines"), "Shirogane_Hurt");
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Benibana art applied: {benibana.idle.Length} idle, {benibana.attack.Length} slash, {benibana.parry.Length} puff, " +
            $"{benibana.hit.Length} hit, {benibana.death.Length} defeat frames.");
        Debug.Log(utsusemi == null ? "Utsusemi has no frames yet; portraits wired, stand-in art kept."
            : $"Utsusemi art applied: {utsusemi.idle.Length} idle, {utsusemi.attack.Length} thrust, {utsusemi.parry.Length} throw, " +
              $"{utsusemi.hit.Length} hit, {utsusemi.death.Length} defeat frames.");
        Debug.Log(shirogane == null ? "Shirogane has no frames yet; portraits and voice wired, stand-in art kept."
            : $"Shirogane art applied: {shirogane.idle.Length} idle, {shirogane.attack.Length} slash, {shirogane.parry.Length} blade-call, " +
              $"{shirogane.hit.Length} hit, {shirogane.death.Length} defeat frames.");
    }

    // Every <prefix>*.mp3 take, so more can be added without touching this.
    static void SetTakes(SerializedProperty lines, string prefix)
    {
        var takes = new List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets(prefix + " t:AudioClip", new[] { VoiceRoot.TrimEnd('/') }))
        {
            takes.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
        }
        lines.arraySize = takes.Count;
        for (int i = 0; i < takes.Count; i++) lines.GetArrayElementAtIndex(i).objectReferenceValue = takes[i];
    }

    // The intro illustrations are painted, not pixel art: smooth filtering and full size, as UI sprites.
    static Sprite Portrait(string name)
    {
        string path = PortraitRoot + name + ".png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Bilinear || importer.maxTextureSize != 2048))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static CharacterArt SaveArt(string name, Sprite[] idle, Sprite[] attack, Sprite[] parry, Sprite[] hit, Sprite[] death, Sprite[] particles)
    {
        string path = ArtRoot + name + ".asset";
        var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(path);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<CharacterArt>();
            AssetDatabase.CreateAsset(art, path);
        }
        art.idle = idle;
        art.attack = attack;
        art.parry = parry;
        art.hit = hit;
        art.death = death;
        art.deathParticles = particles;
        art.idleFps = 6f;
        art.actionFps = 11f;
        EditorUtility.SetDirty(art);
        return art;
    }

    // The boss's second-phase CharacterArt from its <name>_<Clip>_<i> frames, with the first form's clips filling any
    // gap; null until its idle exists (or without a first form to fall back on).
    static CharacterArt PhaseArt(string name, CharacterArt first, string attackClip, string parryClip)
    {
        Sprite[] idle = first == null ? null : TryFrames(name, "Idle");
        return idle == null ? null : SaveArt(name,
            idle: idle,
            attack: TryFrames(name, attackClip) ?? first.attack,
            parry: TryFrames(name, parryClip) ?? first.parry,
            hit: TryFrames(name, "Hit") ?? first.hit,
            death: TryFrames(name, "Defeat") ?? first.death,
            first.deathParticles);
    }

    // The clip's frames, or null when it hasn't been made yet.
    static void SetFrames(SerializedProperty list, Sprite[] frames)
    {
        list.arraySize = frames?.Length ?? 0;
        for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    static Sprite[] TryFrames(string character, string clip)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{character}/{character}_{clip}_0.png");
        return sprite == null ? null : Frames(character, clip);
    }

    // Every numbered frame of the clip, in order.
    static Sprite[] Frames(string character, string clip)
    {
        var frames = new List<Sprite>();
        for (int i = 0; ; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{character}/{character}_{clip}_{i}.png");
            if (sprite == null) break;
            frames.Add(sprite);
        }
        if (frames.Count == 0) throw new System.IO.FileNotFoundException($"No frames for {character} {clip}");
        return frames.ToArray();
    }
}
