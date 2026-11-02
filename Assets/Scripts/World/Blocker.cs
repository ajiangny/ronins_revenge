using System;
using UnityEngine;

// One courtyard blocker (a bush or stone lantern). Takes a few hits from either side before breaking apart.
public class Blocker : MonoBehaviour
{
    // Raised when hits wear a blocker down (not when it's trampled or razed), so it may drop a heart.
    public static event Action<Vector3> Broken;
    // An arrow has struck the wall's own cover (Lightning Rod; RunLog's tally of arrows it stopped).
    public static event Action<Vector3> Chipped;

    SpriteRenderer tile;
    Sprite[] damageStates;
    Sprite[] debris;
    int maxHealth;
    int health;
    int baseHealth;
    bool destroyed;
    // Tamahagane's bright bands across cover tempered beyond new, one a point.
    SpriteRenderer[] temperBands;
    const float TemperBandHeight = 2f / 48f;
    const float TemperBandGap = 6f / 48f;

    // One of the wall's own, not one of Utsusemi's rocks or his planted sword, which Steel leaves alone.
    public bool InWall { get; set; }
    // A steel plate: one-way cover that Oni's shots pass and arrows stop on.
    public bool Plate { get; set; }
    // Built by Steel, so breaking it pays nothing: no heart, no Shrapnel.
    public bool Forged { get; set; }
    public bool Standing => !destroyed;
    public bool Hurt => !destroyed && health < maxHealth;
    // Whole, and with room left to be tempered above how it was built (Tamahagane).
    public bool CanTemper(int bound) => !destroyed && !Hurt && maxHealth < baseHealth + bound;

    public void Init(int hitPoints, SpriteRenderer tileRenderer, Sprite[] states, Sprite[] debrisSprites)
    {
        tile = tileRenderer;
        damageStates = states;
        debris = debrisSprites;
        maxHealth = health = baseHealth = hitPoints;
        UpdateSprite();
    }

    void OnCollisionEnter(Collision coll)
    {
        // The Spirit Shuriken passes over the ronin's own cover.
        Projectile shot = coll.collider.GetComponentInParent<Projectile>();
        // The projectile decides what a pass does (Projectile.OnCollisionEnter), so this doesn't hang on who's called first.
        if (shot != null && (shot.Spinning || shot.PassesCover || Plate || (shot.Whets && InWall))) return;
        Arrow arrow = coll.collider.GetComponentInParent<Arrow>();
        if (arrow != null && arrow.PassesCover) return;
        if (arrow != null && InWall && !Plate) Chipped?.Invoke(transform.position);
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
            if (!Forged) Broken?.Invoke(transform.position);
        }
        else
        {
            Sfx.Play(Sfx.BlockerHit, 0.7f);
            UpdateSprite();
        }
    }

    // Whetstone's slash crossing: a chip, unless it's spared. Holding still under cover always costs something.
    public void Wear(Vector3 point, float spared)
    {
        if (UnityEngine.Random.value >= spared) Hit(point);
    }

    // Scrap or a hammer's ring: health back, never above its maximum. Mending doesn't re-arm anything, since each
    // blocker can only break once a round.
    public void Mend(int points)
    {
        if (destroyed || points <= 0 || !Hurt) return;
        // Measured against what's missing, so restoring everything can't overflow.
        health = points >= maxHealth - health ? maxHealth : health + points;
        UpdateSprite();
    }

    // Tamahagane: whole cover grows past new, up to `bound` above how it was built. The maximum rises with it, because
    // UpdateSprite indexes out of range if health passes it.
    public void Temper(int points, int bound)
    {
        if (destroyed || points <= 0) return;
        int gain = Mathf.Min(points, baseHealth + bound - maxHealth);
        if (gain <= 0) return;
        maxHealth += gain;
        health += gain;
        UpdateSprite();
    }

    public void Demolish()
    {
        if (destroyed) return;
        destroyed = true;
        Debris.BurstSprites(transform.position, debris, Color.white, 4, 0.8f, 4f);
        // Iron doesn't puff: a plate goes in shards.
        if (Plate) Fx.Steel(Fx.SteelShatter, transform.position);
        else Fx.Play(Fx.SmokePuff, transform.position, new Color(1f, 1f, 1f, 0.85f), 1.2f);
        Sfx.Play(Sfx.BlockerBreak);
        Destroy(gameObject);
    }

    // Spreads the damage states across the block's health: full health shows the first, one hit left shows the last.
    void UpdateSprite()
    {
        float damage = maxHealth > 1 ? (float)(maxHealth - health) / (maxHealth - 1) : 0f;
        tile.sprite = damageStates[Mathf.RoundToInt(damage * (damageStates.Length - 1))];
        ShowTemper();
    }

    // The points above how it was built show as bands on its face, so what's tempered can be told from what's whole, and
    // they go as those points are knocked off.
    void ShowTemper()
    {
        int above = health - baseHealth;
        if (temperBands == null && above <= 0) return;
        if (temperBands == null)
        {
            temperBands = new SpriteRenderer[Statuses.TemperBound];
            Bounds face = tile.bounds;
            for (int i = 0; i < temperBands.Length; i++)
            {
                var band = new GameObject("Temper").AddComponent<SpriteRenderer>();
                band.transform.SetParent(transform, true);
                band.transform.position = new Vector3(face.center.x, face.center.y + (i - 0.5f) * TemperBandGap, 0f);
                band.transform.localScale = new Vector3(face.size.x * 0.8f, TemperBandHeight, 1f);
                band.sprite = ProceduralSprites.White;
                band.color = Statuses.SeamColor;
                band.sortingOrder = tile.sortingOrder + 1;
                temperBands[i] = band;
            }
        }
        for (int i = 0; i < temperBands.Length; i++) temperBands[i].enabled = i < above;
    }
}
