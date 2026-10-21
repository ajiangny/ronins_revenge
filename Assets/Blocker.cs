using UnityEngine;

// One courtyard blocker (a bush or stone lantern). Takes a few hits from either side before breaking apart.
public class Blocker : MonoBehaviour
{
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
        if (coll.collider.GetComponentInParent<Kunai>() is { Source: KillSource.Shuriken }) return;

        health--;
        if (health <= 0)
        {
            Demolish();
        }
        else
        {
            UpdateSprite();
        }
    }

    public void Demolish()
    {
        if (destroyed) return;
        destroyed = true;
        Debris.BurstSprites(transform.position, debris, Color.white, 4, 0.8f, 4f);
        Destroy(gameObject);
    }

    // Spreads the damage states across the block's health: full health shows the first, one hit left shows the last.
    void UpdateSprite()
    {
        float damage = maxHealth > 1 ? (float)(maxHealth - health) / (maxHealth - 1) : 0f;
        tile.sprite = damageStates[Mathf.RoundToInt(damage * (damageStates.Length - 1))];
    }
}
