using UnityEngine;

// Builds the row of barricades between the ronin and the samurai.
public class ShieldWall : MonoBehaviour
{
    [Header("Inscribed")]
    [Tooltip("Barricade tile from intact to nearly broken.")]
    [SerializeField] Sprite[] damageStates;
    [SerializeField] Sprite[] debrisSprites;
    [SerializeField] int shieldCount = 4;
    [SerializeField] int blocksWide = 6;
    [SerializeField] int blocksHigh = 3;
    [SerializeField] float blockSize = 0.5f;
    [SerializeField] int blockHealth = 3;

    // Bottom edge of the wall is the GameObject's y position.
    public float TopY => transform.position.y + blocksHigh * blockSize;

    public void Rebuild()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        Camera cam = Camera.main;
        float width = 2f * cam.orthographicSize * cam.aspect;
        float spacing = width / shieldCount;
        float left = cam.transform.position.x - width / 2f;

        for (int s = 0; s < shieldCount; s++)
        {
            float centerX = left + spacing * (s + 0.5f);
            for (int x = 0; x < blocksWide; x++)
            {
                for (int y = 0; y < blocksHigh; y++)
                {
                    if (!IsSolid(x, y)) continue;

                    Vector3 position = new Vector3(
                        centerX + (x - (blocksWide - 1) / 2f) * blockSize,
                        transform.position.y + (y + 0.5f) * blockSize,
                        0);
                    CreateBlock(position);
                }
            }
        }
    }

    // Classic bunker shape: clipped top corners and an arch cut out of the bottom middle.
    bool IsSolid(int x, int y)
    {
        bool topCorner = y == blocksHigh - 1 && (x == 0 || x == blocksWide - 1);
        bool arch = y == 0 && x > 0 && x < blocksWide - 1 && Mathf.Abs(x - (blocksWide - 1) / 2f) < 1f;
        return !topCorner && !arch;
    }

    void CreateBlock(Vector3 position)
    {
        var block = new GameObject("ShieldBlock");
        block.transform.SetParent(transform);
        block.transform.position = position;
        block.AddComponent<BoxCollider>().size = new Vector3(blockSize, blockSize, blockSize);

        // The tile sprite sits on a child scaled to fill exactly one block.
        var tile = new GameObject("Tile").AddComponent<SpriteRenderer>();
        tile.transform.SetParent(block.transform, false);
        tile.sprite = damageStates[0];
        tile.transform.localScale = Vector3.one * (blockSize / tile.sprite.bounds.size.x);

        block.AddComponent<ShieldBlock>().Init(blockHealth, tile, damageStates, debrisSprites);
    }
}
