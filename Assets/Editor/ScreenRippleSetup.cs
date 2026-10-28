using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Adds the TimeRipple full-screen pass to every URP renderer in Assets/Settings, with its material. Safe to re-run:
// a renderer that already has the pass only has its settings refreshed.
public static class ScreenRippleSetup
{
    const string ShaderName = "Ninja/TimeRipple";
    const string MaterialPath = "Assets/Art/Materials/TimeRipple.mat";

    [MenuItem("Ninja/Apply Screen Ripples")]
    public static void Apply()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Screen ripples: shader {ShaderName} not found.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;

        int added = 0, refreshed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            TimeRippleFeature feature = null;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
                if (f is TimeRippleFeature ripple) feature = ripple;

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<TimeRippleFeature>();
                feature.name = "TimeRipple";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                // The renderer keeps its features and a map of their file ids side by side, as its own inspector does.
                var so = new SerializedObject(data);
                SerializedProperty features = so.FindProperty("m_RendererFeatures");
                SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
                added++;
            }
            else refreshed++;

            // Before post-processing, so bloom still glows over the bent picture.
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Color;
            feature.fetchColorBuffer = true;
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Screen ripples: added to {added} renderer(s), refreshed {refreshed}.");
    }
}
