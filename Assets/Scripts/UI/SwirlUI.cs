using UnityEngine;
using UnityEngine.UI;

// A slow vortex of spirit light for painted screens: a few layers of ProceduralSprites.Swirl, turning at different
// speeds (the middle one against the others) and breathing a little, in unscaled time so it keeps turning while the
// game is paused under a menu. Code-built: Create it under any RectTransform and tint it with Colour.
public class SwirlUI : MonoBehaviour
{
    // Each layer: its size against the whole, turning speed (degrees a second, negative turns the other way) and alpha.
    static readonly (float scale, float speed, float alpha)[] Layers =
    {
        (1f, 9f, 0.7f),
        (0.72f, -14f, 0.6f),
        (0.45f, 24f, 0.75f),
    };

    Image[] layers;
    float[] phase;
    Color colour = Color.white;
    float strength = 1f;

    public static SwirlUI Create(string name, Transform parent, Vector2 anchor, Vector2 position, float size)
    {
        var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = anchor;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = position;
        root.sizeDelta = new Vector2(size, size);
        var swirl = root.gameObject.AddComponent<SwirlUI>();
        swirl.layers = new Image[Layers.Length];
        swirl.phase = new float[Layers.Length];
        for (int i = 0; i < Layers.Length; i++)
        {
            var layer = new GameObject("Layer" + i, typeof(RectTransform)).AddComponent<Image>();
            layer.rectTransform.SetParent(root, false);
            layer.rectTransform.sizeDelta = Vector2.one * size * Layers[i].scale;
            layer.sprite = ProceduralSprites.Swirl;
            layer.raycastTarget = false;
            swirl.layers[i] = layer;
            // Out of step, so the arms never line up.
            swirl.phase[i] = i * 47f;
        }
        swirl.Apply();
        return swirl;
    }

    // The tint, its alpha the swirl's overall strength.
    public Color Colour
    {
        get => colour;
        set
        {
            colour = value;
            Apply();
        }
    }

    // Fades the whole swirl in and out (0 to 1) on top of its colour's alpha, e.g. as a screen opens.
    public float Strength
    {
        get => strength;
        set
        {
            strength = value;
            Apply();
        }
    }

    void Apply()
    {
        for (int i = 0; i < layers.Length; i++)
            layers[i].color = new Color(colour.r, colour.g, colour.b, colour.a * strength * Layers[i].alpha);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < layers.Length; i++)
        {
            layers[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, phase[i] + t * Layers[i].speed);
            layers[i].rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 0.7f + i * 2.1f));
        }
    }
}
