using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The rise of one freed soul: up nine units over three seconds, swaying, shrinking and fading.
public class FreedSoul : MonoBehaviour
{
    SpriteRenderer soul;
    Vector3 from;
    Color colour;
    float drift;
    float size;
    float age;

    public void Begin(Vector3 start, Color tint)
    {
        soul = GetComponent<SpriteRenderer>();
        from = start;
        colour = tint;
        drift = Random.Range(-1f, 1f);
        size = Random.Range(0.8f, 1.5f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = age / 3f;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = from + new Vector3(drift * k * 2f + Mathf.Sin(age * 3f) * 0.3f, k * 9f, -0.3f);
        transform.localScale = Vector3.one * size * (1f - 0.4f * k);
        soul.color = new Color(colour.r, colour.g, colour.b, 0.8f * (1f - k));
    }
}
