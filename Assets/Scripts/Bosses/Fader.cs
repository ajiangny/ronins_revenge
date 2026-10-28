using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fades a sprite out and removes it.
public class Fader : MonoBehaviour
{
    SpriteRenderer target;
    float seconds;
    float startAlpha;
    float age;

    public static void Fade(SpriteRenderer renderer, float seconds)
    {
        var fader = renderer.gameObject.AddComponent<Fader>();
        fader.target = renderer;
        fader.seconds = seconds;
        fader.startAlpha = renderer.color.a;
    }

    void Update()
    {
        age += Time.deltaTime;
        Color c = target.color;
        c.a = startAlpha * (1f - age / seconds);
        target.color = c;
        if (age >= seconds) Destroy(gameObject);
    }
}
