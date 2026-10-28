using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Grows a shot's whole transform (hitbox included) as it flies.
public class Swell : MonoBehaviour
{
    float from, to, seconds, age;
    Vector3 baseScale;

    public void Begin(float fromScale, float toScale, float duration)
    {
        from = fromScale;
        to = toScale;
        seconds = duration;
        baseScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.localScale = baseScale * Mathf.Lerp(from, to, Mathf.Clamp01(age / seconds));
    }
}
