using UnityEngine;

// Trauma-based camera shake: Add() raises trauma, which decays; the offset grows with trauma squared, so small hits
// barely move the view and big ones kick. Gameplay that measures the view reads Center, which never shakes.
public class CameraShake : MonoBehaviour
{
    const float MaxOffset = 0.35f;
    const float Decay = 2.2f;
    const float Frequency = 22f;

    static CameraShake instance;

    Vector3 basePosition;
    float trauma;

    // The camera's resting position (the live one while no shake component exists yet).
    public static Vector3 Center => instance != null ? instance.basePosition : Camera.main.transform.position;

    public static void Add(float amount)
    {
        if (instance == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            instance = cam.gameObject.AddComponent<CameraShake>();
        }
        instance.trauma = Mathf.Min(1f, instance.trauma + amount);
    }

    void Awake()
    {
        instance = this;
        basePosition = transform.position;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Scaled time: the shake freezes with the game on the boon screen and hit-stop.
    void LateUpdate()
    {
        trauma = Mathf.Max(0f, trauma - Decay * Time.deltaTime);
        float strength = trauma * trauma * MaxOffset;
        float t = Time.time * Frequency;
        transform.position = basePosition + new Vector3(
            (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * strength,
            (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * strength,
            0f);
    }
}
