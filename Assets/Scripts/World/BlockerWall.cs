using UnityEngine;

// Builds the rows of courtyard blockers (bushes, stone lanterns) between the ronin and the samurai.
public class BlockerWall : MonoBehaviour
{
    [System.Serializable]
    public class BlockerType
    {
        [Tooltip("Its first letter is the code used in the pattern.")]
        public string name = "Bush";
        [Tooltip("From intact to nearly broken.")]
        public Sprite[] damageStates;
        [Tooltip("Other looks for the same blocker, so a row isn't all identical. Each blocker built picks one of these or damageStates at random.")]
        public Look[] variants;
        public Sprite[] debris;
        public int health = 6;
        [Tooltip("Drawn height in world units; the width follows the art.")]
        public float height = 1f;
        [Tooltip("Hitbox as a fraction of the drawn size, so shots can graze the edges.")]
        public Vector2 hitbox = new Vector2(0.85f, 0.85f);

        // One of its looks at random: damage states from intact to nearly broken.
        public Sprite[] PickLook()
        {
            int pick = variants == null ? 0 : Random.Range(0, variants.Length + 1);
            return pick == 0 || variants[pick - 1].damageStates == null || variants[pick - 1].damageStates.Length == 0
                ? damageStates : variants[pick - 1].damageStates;
        }
    }

    [System.Serializable]
    public class Look
    {
        public Sprite[] damageStates;
    }

    [Header("Inscribed")]
    [SerializeField] BlockerType[] types;
    [Tooltip("Blockers at each site, left to right, by each type's first letter (e.g. BLB = bush, lantern, bush).")]
    [SerializeField] string pattern = "BLB";
    [SerializeField] int siteCount = 4;
    [Tooltip("World units kept clear at each side of the view, so the outer sites stand on open ground rather than the map's edge scenery.")]
    [SerializeField] float sideInset = 2.5f;
    [SerializeField] float gap = 0.05f;
    [Tooltip("The steel plate scrap forms in a gap (Ninja/Apply Boon Art). Empty draws a bar of tinted iron with a bright seam.")]
    [SerializeField] Sprite plateArt;

    // The set Rebuild builds from: the courtyard's own, unless the map on show brought its own (Use).
    BlockerType[] activeTypes;
    string activePattern;
    BlockerType[] ActiveTypes => activeTypes ?? types;
    string ActivePattern => activePattern ?? pattern;
    readonly System.Collections.Generic.List<Vector3> built = new System.Collections.Generic.List<Vector3>();
    // What was built where, so Steel can mend what stands and raise what fell.
    class Slot
    {
        public BlockerType type;
        public Sprite[] look;
        public Vector3 centre;
        public Blocker blocker;
    }
    readonly System.Collections.Generic.List<Slot> slots = new System.Collections.Generic.List<Slot>();
    readonly System.Collections.Generic.List<Blocker> plates = new System.Collections.Generic.List<Blocker>();
    const float PlateWidth = 1.1f;
    const float PlateHeight = 0.8f;
    const float PlateStep = 0.5f;
    // The bright seam across a plate's iron.
    const float PlateSeamHeight = 0.07f;

    // Centres of the blockers made by the last Rebuild (the old ones are still being destroyed this frame).
    public System.Collections.Generic.IReadOnlyList<Vector3> BlockerPositions => built;

    // Bottom edge of the blockers is the GameObject's y position.
    public float TopY
    {
        get
        {
            float tallest = 0f;
            foreach (BlockerType type in ActiveTypes) tallest = Mathf.Max(tallest, type.height);
            return transform.position.y + tallest;
        }
    }

    // Builds from a map's own blockers from the next Rebuild on; null (or an empty set) goes back to the courtyard's.
    public void Use(BlockerType[] mapTypes, string mapPattern)
    {
        bool own = mapTypes != null && mapTypes.Length > 0 && !string.IsNullOrEmpty(mapPattern);
        activeTypes = own ? mapTypes : null;
        activePattern = own ? mapPattern : null;
    }

    // No cover at all, for the temple between runs.
    public void Clear()
    {
        built.Clear();
        slots.Clear();
        plates.Clear();
        foreach (Transform child in transform) Destroy(child.gameObject);
    }

    public void Rebuild()
    {
        built.Clear();
        slots.Clear();
        plates.Clear();
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        Camera cam = Camera.main;
        float width = 2f * cam.orthographicSize * cam.aspect - 2f * sideInset;
        float spacing = width / siteCount;
        float left = CameraShake.Center.x - width / 2f;

        for (int s = 0; s < siteCount; s++)
        {
            BuildSite(left + spacing * (s + 0.5f));
        }
    }

    void BuildSite(float centerX)
    {
        string sitePattern = ActivePattern;
        var row = new BlockerType[sitePattern.Length];
        var looks = new Sprite[sitePattern.Length][];
        float rowWidth = gap * (sitePattern.Length - 1);
        for (int i = 0; i < sitePattern.Length; i++)
        {
            row[i] = TypeFor(sitePattern[i]);
            looks[i] = row[i].PickLook();
            rowWidth += WidthOf(row[i], looks[i]);
        }

        float x = centerX - rowWidth / 2f;
        for (int i = 0; i < row.Length; i++)
        {
            float w = WidthOf(row[i], looks[i]);
            CreateBlocker(row[i], looks[i], new Vector3(x + w / 2f, transform.position.y + row[i].height / 2f, 0));
            x += w + gap;
        }
    }

    BlockerType TypeFor(char code)
    {
        foreach (BlockerType type in ActiveTypes)
        {
            if (char.ToUpperInvariant(type.name[0]) == char.ToUpperInvariant(code)) return type;
        }
        throw new System.ArgumentException($"No blocker type starts with '{code}' (pattern \"{ActivePattern}\").");
    }

    static float WidthOf(BlockerType type, Sprite[] look)
    {
        Vector3 size = look[0].bounds.size;
        return type.height * size.x / size.y;
    }

    void CreateBlocker(BlockerType type, Sprite[] look, Vector3 center)
    {
        built.Add(center);
        slots.Add(new Slot { type = type, look = look, centre = center, blocker = BuildBlocker(type, look, center) });
    }

    Blocker BuildBlocker(BlockerType type, Sprite[] look, Vector3 center)
    {
        var blocker = new GameObject(type.name);
        blocker.transform.SetParent(transform);
        blocker.transform.position = center;
        float w = WidthOf(type, look);
        blocker.AddComponent<BoxCollider>().size = new Vector3(w * type.hitbox.x, type.height * type.hitbox.y, 0.5f);

        // The sprite sits on a child scaled to the blocker's drawn height.
        var art = new GameObject("Sprite").AddComponent<SpriteRenderer>();
        art.transform.SetParent(blocker.transform, false);
        art.sprite = look[0];
        art.transform.localScale = Vector3.one * (type.height / art.sprite.bounds.size.y);

        Blocker made = blocker.AddComponent<Blocker>();
        made.Init(type.health, art, look, type.debris);
        made.InWall = true;
        return made;
    }

    // The nearest of the wall's own cover that's hurt, within reach sideways of x. Null when none is.
    public Blocker NearestHurt(float x, float reach)
    {
        Blocker nearest = null;
        float best = reach;
        foreach (Slot slot in slots)
        {
            if (slot.blocker == null || !slot.blocker.Hurt) continue;
            float distance = Mathf.Abs(slot.blocker.transform.position.x - x);
            if (distance > best) continue;
            best = distance;
            nearest = slot.blocker;
        }
        return nearest;
    }

    // The nearest whole cover with room to be tempered above how it was built (Tamahagane).
    public Blocker NearestUntempered(float x, float reach, int bound)
    {
        Blocker nearest = null;
        float best = reach;
        foreach (Slot slot in slots)
        {
            if (slot.blocker == null || !slot.blocker.CanTemper(bound)) continue;
            float distance = Mathf.Abs(slot.blocker.transform.position.x - x);
            if (distance > best) continue;
            best = distance;
            nearest = slot.blocker;
        }
        return nearest;
    }

    public int PlateCount
    {
        get
        {
            plates.RemoveAll(plate => plate == null || !plate.Standing);
            return plates.Count;
        }
    }

    // A steel plate in the gap at x, or as near as reach allows: it steps outward until a spot is free, and false means
    // none was. Without art it stands as a bar of tinted iron with a bright seam.
    public bool TryPlate(float x, float reach)
    {
        for (float step = 0f; step <= reach; step += PlateStep)
        {
            if (BuildPlateAt(x + step) || (step > 0f && BuildPlateAt(x - step))) return true;
        }
        return false;
    }

    bool BuildPlateAt(float x)
    {
        Sprite sprite = plateArt;
        Camera cam = Camera.main;
        float half = cam.orthographicSize * cam.aspect - sideInset;
        if (Mathf.Abs(x - CameraShake.Center.x) > half - PlateWidth / 2f) return false;
        foreach (Slot slot in slots)
        {
            if (slot.blocker == null || !slot.blocker.Standing) continue;
            if (Mathf.Abs(slot.centre.x - x) < (WidthOf(slot.type, slot.look) + PlateWidth) / 2f) return false;
        }
        foreach (Blocker other in plates)
        {
            if (other != null && other.Standing && Mathf.Abs(other.transform.position.x - x) < PlateWidth) return false;
        }

        var plate = new GameObject("SteelPlate");
        plate.transform.SetParent(transform);
        plate.transform.position = new Vector3(x, transform.position.y + PlateHeight / 2f, 0f);
        plate.AddComponent<BoxCollider>().size = new Vector3(PlateWidth, PlateHeight, 0.5f);

        Sprite look = sprite != null ? sprite : ProceduralSprites.White;
        var art = new GameObject("Sprite").AddComponent<SpriteRenderer>();
        art.transform.SetParent(plate.transform, false);
        art.sprite = look;
        art.transform.localScale = new Vector3(PlateWidth / look.bounds.size.x, PlateHeight / look.bounds.size.y, 1f);
        if (sprite == null)
        {
            art.color = Statuses.CrackColor;
            var seam = new GameObject("Seam").AddComponent<SpriteRenderer>();
            seam.transform.SetParent(plate.transform, false);
            seam.transform.localPosition = Vector3.up * (PlateHeight * 0.2f);
            seam.transform.localScale = new Vector3(PlateWidth, PlateSeamHeight, 1f);
            seam.sprite = ProceduralSprites.White;
            seam.color = Statuses.SeamColor;
            seam.sortingOrder = 1;
        }

        Blocker made = plate.AddComponent<Blocker>();
        made.Init(1, art, new[] { look }, null);
        made.InWall = made.Plate = made.Forged = true;
        plates.Add(made);
        return true;
    }

    // Steel Shadow: standing cover back to full and, if raise, each fallen blocker built again where it stood. What's
    // raised is Forged, so it pays nothing when it breaks, and a plate it lands on makes way.
    public void Restore(bool raise)
    {
        foreach (Slot slot in slots)
        {
            if (slot.blocker != null && slot.blocker.Standing)
            {
                slot.blocker.Mend(int.MaxValue);
                continue;
            }
            if (!raise) continue;
            float reach = (WidthOf(slot.type, slot.look) + PlateWidth) / 2f;
            foreach (Blocker plate in plates)
            {
                if (plate != null && Mathf.Abs(plate.transform.position.x - slot.centre.x) < reach) plate.Demolish();
            }
            slot.blocker = BuildBlocker(slot.type, slot.look, slot.centre);
            slot.blocker.Forged = true;
        }
    }
}
