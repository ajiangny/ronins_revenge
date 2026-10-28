using System;
using UnityEngine;

// One courtyard blocker (a bush or stone lantern). Takes a few hits from either side before breaking apart.
public class Blocker : MonoBehaviour
{
    // Raised when hits wear a blocker down (not when it's trampled or razed), so it may drop a heart.
    public static event Action<Vector3> Broken;

    SpriteRenderer tile;
    Sprite[] damageStates;
    Sprite[] debris;
    int maxHealth;
    int health;
    bool destroyed;

    public void Init(int hitPoints, SpriteRenderer tileRenderer, Sprite[] states, Sprite[] debrisSprites)
    {
        tile = tileRenderer;
        damageStates = states;
        debris = debrisSprites;
        maxHealth = health = hitPoints;
        UpdateSprite();
    }

    void OnCollisionEnter(Collision coll)
    {
        // The Spirit Shuriken passes over the ronin's own cover.
        if (coll.collider.GetComponentInParent<Kunai>() is { Spinning: true }) return;
        if (coll.collider.GetComponentInParent<Arrow>() is { PassesCover: true }) return;
        Hit(coll.GetContact(0).point);
    }

    // One hit's worth of damage, from a projectile or anything that barges through (a diving samurai).
    public void Hit(Vector3 point)
    {
        if (destroyed) return;
        health--;
        HitFlash.Flash(tile, 0.07f);
        Fx.Play(Fx.Impact, point, new Color(1f, 0.95f, 0.85f), 0.6f);
        if (health <= 0)
        {
            Demolish();
            Broken?.Invoke(transform.position);
        }
        else
        {
            Sfx.Play(Sfx.BlockerHit, 0.7f);
            UpdateSprite();
        }
    }

    public void Demolish()
    {
        if (destroyed) return;
        destroyed = true;
        Debris.BurstSprites(transform.position, debris, Color.white, 4, 0.8f, 4f);
        Fx.Play(Fx.SmokePuff, transform.position, new Color(1f, 1f, 1f, 0.85f), 1.2f);
        Sfx.Play(Sfx.BlockerBreak);
        Destroy(gameObject);
    }

    // Spreads the damage states across the block's health: full health shows the first, one hit left shows the last.
    void UpdateSprite()
    {
        float damage = maxHealth > 1 ? (float)(maxHealth - health) / (maxHealth - 1) : 0f;
        tile.sprite = damageStates[Mathf.RoundToInt(damage * (damageStates.Length - 1))];
    }
}
