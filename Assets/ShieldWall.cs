using UnityEngine;

// Builds the rows of courtyard blockers (bushes, stone lanterns) between the ronin and the samurai.
public class ShieldWall : MonoBehaviour
{
    [System.Serializable]
    public class BlockerType
    {
        [Tooltip("Its first letter is the code used in the pattern.")]
        public string name = "Bush";
        [Tooltip("From intact to nearly broken.")]
        public Sprite[] damageStates;
        public Sprite[] debris;
        public int health = 6;
        [Tooltip("Drawn height in world units; the width follows the art.")]
        public float height = 1f;
        [Tooltip("Hitbox as a fraction of the drawn size, so shots can graze the edges.")]
        public Vector2 hitbox = new Vector2(0.85f, 0.85f);
    }

    [Header("Inscribed")]
    [SerializeField] BlockerType[] types;
    [Tooltip("Blockers at each site, left to right, by each type's first letter (e.g. BLB = bush, lantern, bush).")]
    [SerializeField] string pattern = "BLB";
    [SerializeField] int siteCount = 4;
    [SerializeField] float gap = 0.05f;

    // Bottom edge of the blockers is the GameObject's y position.
    public float TopY
    {
        get
        {
            float tallest = 0f;
            foreach (BlockerType type in types) tallest = Mathf.Max(tallest, type.height);
            return transform.position.y + tallest;
        }
    }

    public void Rebuild()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        Camera cam = Camera.main;
        float width = 2f * cam.orthographicSize * cam.aspect;
        float spacing = width / siteCount;
        float left = cam.transform.position.x - width / 2f;

        for (int s = 0; s < siteCount; s++)
        {
            BuildSite(left + spacing * (s + 0.5f));
        }
    }

    void BuildSite(float centerX)
    {
        var row = new BlockerType[pattern.Length];
        float rowWidth = gap * (pattern.Length - 1);
        for (int i = 0; i < pattern.Length; i++)
        {
            row[i] = TypeFor(pattern[i]);
            rowWidth += WidthOf(row[i]);
        }

        float x = centerX - rowWidth / 2f;
        foreach (BlockerType type in row)
        {
            float w = WidthOf(type);
            CreateBlocker(type, new Vector3(x + w / 2f, transform.position.y + type.height / 2f, 0));
            x += w + gap;
        }
    }

    BlockerType TypeFor(char code)
    {
        foreach (BlockerType type in types)
        {
            if (char.ToUpperInvariant(type.name[0]) == char.ToUpperInvariant(code)) return type;
        }
        throw new System.ArgumentException($"No blocker type starts with '{code}' (pattern \"{pattern}\").");
    }

    static float WidthOf(BlockerType type)
    {
        Vector3 size = type.damageStates[0].bounds.size;
        return type.height * size.x / size.y;
    }

    void CreateBlocker(BlockerType type, Vector3 center)
    {
        var blocker = new GameObject(type.name);
        blocker.transform.SetParent(transform);
        blocker.transform.position = center;
        float w = WidthOf(type);
        blocker.AddComponent<BoxCollider>().size = new Vector3(w * type.hitbox.x, type.height * type.hitbox.y, 0.5f);

        // The sprite sits on a child scaled to the blocker's drawn height.
        var art = new GameObject("Sprite").AddComponent<SpriteRenderer>();
        art.transform.SetParent(blocker.transform, false);
        art.sprite = type.damageStates[0];
        art.transform.localScale = Vector3.one * (type.height / art.sprite.bounds.size.y);

        blocker.AddComponent<ShieldBlock>().Init(type.health, art, type.damageStates, type.debris);
    }
}
