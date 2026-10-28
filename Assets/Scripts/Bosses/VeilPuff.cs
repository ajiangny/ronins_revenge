using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One drifting cloud of Benibana's smoke veil. It wraps around the screen and thins where fire burns it.
public class VeilPuff : MonoBehaviour
{
    SpriteRenderer sprite;
    Color colour;
    float drift;
    float centre;
    float wrap;
    float burnt;
    float fadeIn;
    bool dissolving;

    // frames, when the veil art exists, replace the soft glow with a drifting cloud drawn at about the same size.
    public static VeilPuff Create(Vector3 position, float size, Color colour, float centreX, float wrapDistance, Sprite[] frames = null)
    {
        var puff = new GameObject("Veil").AddComponent<VeilPuff>();
        puff.transform.position = position + Vector3.back * 0.4f;
        puff.transform.localScale = Vector3.one * size;
        puff.sprite = puff.gameObject.AddComponent<SpriteRenderer>();
        puff.sprite.sprite = ProceduralSprites.Glow;
        puff.sprite.sortingOrder = 40;
        if (frames != null && frames.Length > 0)
        {
            puff.transform.localScale = Vector3.one * (size * 0.8f / frames[0].bounds.size.x);
            puff.gameObject.AddComponent<SpriteAnimator>().Play(frames, true, null, 8f);
            // The cloud art is already dark; only its alpha comes from the colour.
            colour = new Color(1f, 1f, 1f, 0.55f);
        }
        puff.colour = colour;
        puff.drift = Random.Range(0.3f, 0.7f) * (Random.value < 0.5f ? -1f : 1f);
        puff.centre = centreX;
        puff.wrap = wrapDistance;
        puff.sprite.color = Color.clear;
        return puff;
    }

    public void Burn(Vector3 point, float radius)
    {
        if (Vector2.Distance(point, transform.position) < radius + transform.localScale.x * 0.3f) burnt = 1f;
    }

    public void Dissolve()
    {
        dissolving = true;
    }

    void Update()
    {
        Vector3 position = transform.position;
        position.x += drift * Time.deltaTime;
        if (position.x > centre + wrap) position.x -= wrap * 2f;
        else if (position.x < centre - wrap) position.x += wrap * 2f;
        transform.position = position;

        fadeIn = Mathf.MoveTowards(fadeIn, dissolving ? 0f : 1f, Time.deltaTime * (dissolving ? 0.6f : 1.2f));
        // Burnt smoke clears at once and creeps back over a few seconds.
        burnt = Mathf.MoveTowards(burnt, 0f, Time.deltaTime / 4f);
        Color c = colour;
        c.a *= fadeIn * (1f - burnt * 0.85f);
        sprite.color = c;
        if (dissolving && fadeIn <= 0f) Destroy(gameObject);
    }
}
