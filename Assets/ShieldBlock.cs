using UnityEngine;

// One brick of a shield. Takes a few hits from either side before crumbling.
public class ShieldBlock : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Renderer blockRenderer;
    MaterialPropertyBlock colorBlock;
    Color baseColor;
    int maxHealth;
    int health;
    bool destroyed;

    public void Init(int hitPoints, Color color)
    {
        blockRenderer = GetComponent<Renderer>();
        colorBlock = new MaterialPropertyBlock();
        baseColor = color;
        maxHealth = health = hitPoints;
        UpdateColor();
    }

    void OnCollisionEnter(Collision coll)
    {
        health--;
        if (health <= 0)
        {
            Demolish();
        }
        else
        {
            UpdateColor();
        }
    }

    public void Demolish()
    {
        if (destroyed) return;
        destroyed = true;
        Debris.Burst(transform.position, blockRenderer.sharedMaterial, baseColor, 4, 0.18f, 4f);
        Destroy(gameObject);
    }

    // Damaged blocks get darker so the player can see which ones are about to break.
    void UpdateColor()
    {
        colorBlock.SetColor(BaseColorId, Color.Lerp(baseColor * 0.35f, baseColor, (float)health / maxHealth));
        blockRenderer.SetPropertyBlock(colorBlock);
    }
}
