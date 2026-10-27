using System.Collections.Generic;
using TMPro;
using UnityEngine;

// What a station does when Oni uses it.
public enum StationKind { Exit, FluteSongs, Memorial, WeaponRack }

// A little life a station gives off: incense smoke rising, light glinting on blades, a soft glow.
public enum StationFx { None, Incense, Glint, Glow }

// A place between runs where Oni walks freely, in every direction (the only such place: in a fight he keeps to his
// row). The first is the abandoned temple on the hill (Hades' House): he wakes in its spirit pool after each death and
// walks to its stations. Everything about one is data (SafeArea.Config, GameManager.safeAreas), so another is a new
// entry. Code-built over its map; a station without art is a plain tinted block, and without a map the pool and the
// exit are drawn too.
public class SafeArea : MonoBehaviour
{
    [System.Serializable]
    public class Station
    {
        public StationKind kind;
        public string label = "SET OUT";
        [Tooltip("Where it stands, as a fraction of the map from its bottom-left (of the view, without a map).")]
        public Vector2 spot;
        [Tooltip("Its prop, drawn at the map's pixel size. Empty: a tinted block, or nothing if the map already shows it.")]
        public Sprite art;
        [Tooltip("Drawn on the map itself (the temple gate), so no prop and no placeholder.")]
        public bool onMap;
        public Color placeholder = new Color(0.4f, 0.32f, 0.25f);
        [Tooltip("How close his feet must come to use it, in world units.")]
        public float reach = 1.7f;
        [Tooltip("Where its prop's base stands on the map (its dais, its nook), in map pixels from the top-left. Zero: just behind its spot.")]
        public Vector2 propAt;
        [Tooltip("The ground its prop stands on that he can't walk through (width, depth in world units), from its base. Zero: none.")]
        public Vector2 footprint;
        public StationFx fx;
    }

    // A flame on the map (a lantern, a candle) that lights the ground around it and flickers.
    [System.Serializable]
    public class Lamp
    {
        [Tooltip("The flame, in map pixels from the top-left.")]
        public Vector2 px;
        public Color colour = new Color(1f, 0.7f, 0.4f, 0.35f);
        [Tooltip("How far its light reaches on the ground, in world units.")]
        public float radius = 2.5f;
    }

    [System.Serializable]
    public class Config
    {
        public string name = "The Temple";
        public Backdrop.Map map;
        [Tooltip("Map pixels top to bottom, for sizing props when the map isn't laid out yet.")]
        public float mapPixelsHigh = 384f;
        [Tooltip("The open floor he can walk, as fractions of the map.")]
        public Rect floor = Rect.MinMaxRect(0.2f, 0.08f, 0.8f, 0.68f);
        [Tooltip("Where he wakes after a death, as a fraction of the map.")]
        public Vector2 wakeSpot = new Vector2(0.5f, 0.17f);
        public Color wakeGlow = new Color(0.45f, 1f, 0.9f, 0.25f);
        public Station[] stations = new Station[0];
        [Tooltip("The spirit pool's water: its middle and half-size in map pixels from the top-left. Zero: no pool.")]
        public Vector2 poolCentre;
        public Vector2 poolRadii;
        [Tooltip("The water cut out of the map (Ninja/Temple/Cut Pool Layers), what the ripples and shimmer show in.")]
        public Sprite poolWater;
        [Tooltip("The near half of the pool's rim cut out of the map, drawn over his shins while he wades.")]
        public Sprite poolRimFront;
        [Tooltip("A soul rising off the water, played as it drifts up and fades. Empty: a soft glow rises instead.")]
        public Sprite[] poolSouls = new Sprite[0];
        public Lamp[] lamps = new Lamp[0];
        [Tooltip("Ground painted on the map he can't walk through (lanterns, a dais's edges, a kerb): rectangles in map pixels, x and y their top-left corner.")]
        public Rect[] walls = new Rect[0];
        [Tooltip("Ghost-fires floating over the map: where each flame's foot is, in map pixels from the top-left.")]
        public Vector2[] spirits = new Vector2[0];
        [Tooltip("The ghost-fire's flicker, drawn at the map's pixel size, its flame's foot at the canvas's bottom-centre row 38.")]
        public Sprite[] spiritFrames = new Sprite[0];
    }

    static readonly Color Teal = new Color(0.45f, 1f, 0.9f);

    Config config;
    TextMeshPro prompt;
    SpriteRenderer wakeGlow;
    // The rectangle spots are measured in: the map where it lies (it covers the view and may be cropped by it), or the
    // view without one.
    Vector2 viewSize;
    Vector3 viewOrigin;
    // World units per map pixel, so props and layers drawn over the map match its pixels exactly.
    float mapPixel;
    Bounds ground;
    bool mapSpace;
    Ronin ronin;
    SoulPool pool;

    // Each prop knows where its base is, so it's drawn in front of him when he walks behind it.
    class Standing
    {
        public Station station;
        public SpriteRenderer renderer;
        public float baseY;
        public Bounds art;
        public SpriteRenderer glow;
        public float nextFx;
    }
    readonly List<Standing> standing = new List<Standing>();
    readonly List<(SpriteRenderer floor, SpriteRenderer flame, Color colour, float seed)> lit = new List<(SpriteRenderer, SpriteRenderer, Color, float)>();
    int propOrder;
    readonly List<(SpriteRenderer renderer, float footY, float phase)> spirits = new List<(SpriteRenderer, float, float)>();
    // The ghost-fire frames put the flame's foot this far below the canvas's middle (row 38 of 48), in its pixels.
    const float SpiritFootBelowMiddle = 14f;

    public Config Area => config;
    public Vector3 WakePoint => At(config.wakeSpot);
    public Rect WalkArea { get; private set; }

    public float MapPixel => mapPixel;

    // Where he stands once he's climbed out of the pool, just past its far rim; a step up from the wake spot without one.
    public Vector3 PoolExit => pool != null ? MapToWorld(config.poolCentre - new Vector2(0f, config.poolRadii.y + 24f)) : WakePoint + Vector3.up * 1.6f;

    public static SafeArea Build(Config config, TMP_FontAsset font, Ronin ronin, Bounds? ground)
    {
        var area = new GameObject("SafeArea " + config.name).AddComponent<SafeArea>();
        area.ronin = ronin;
        area.Lay(config, font, ronin.SpriteOrder, ground);
        return area;
    }

    public Vector3 At(Vector2 fraction) => viewOrigin + new Vector3(fraction.x * viewSize.x, fraction.y * viewSize.y, 0f);

    // Map pixels count from the top-left, as in the art.
    public Vector3 MapToWorld(Vector2 px) => new Vector3(ground.min.x + px.x * mapPixel, ground.max.y - px.y * mapPixel, 0f);
    public Vector2 WorldToMap(Vector3 world) => new Vector2((world.x - ground.min.x) / mapPixel, (ground.max.y - world.y) / mapPixel);

    public Vector3 PointOf(StationKind kind)
    {
        foreach (Station station in config.stations)
            if (station.kind == kind) return At(station.spot);
        return WakePoint;
    }

    void Lay(Config area, TMP_FontAsset font, int order, Bounds? groundBounds)
    {
        config = area;
        bool mapArt = area.map != null && area.map.ground != null;
        mapSpace = mapArt && groundBounds.HasValue;
        if (mapSpace)
        {
            ground = groundBounds.Value;
            viewSize = ground.size;
            viewOrigin = new Vector3(ground.min.x, ground.min.y, 0f);
            mapPixel = ground.size.y / area.map.ground.rect.height;
        }
        else
        {
            Camera cam = Camera.main;
            viewSize = new Vector2(2f * cam.orthographicSize * cam.aspect, 2f * cam.orthographicSize);
            viewOrigin = new Vector3(CameraShake.Center.x - viewSize.x / 2f, CameraShake.Center.y - viewSize.y / 2f, 0f);
            mapPixel = viewSize.y / Mathf.Max(1f, area.mapPixelsHigh);
            ground = new Bounds((Vector3)CameraShake.Center, viewSize);
        }
        Vector3 low = At(area.floor.min), high = At(area.floor.max);
        WalkArea = Rect.MinMaxRect(low.x, low.y, high.x, high.y);

        if (mapSpace && area.poolRadii.x > 0f && area.poolRadii.y > 0f) pool = SoulPool.Build(this, area, ronin, ground);
        // Without its map there's still somewhere to wake and a way out.
        if (!mapArt) Block("Pool", ProceduralSprites.Disc, WakePoint, new Vector2(3f, 1.6f), new Color(0.2f, 0.6f, 0.55f), order - 3);
        wakeGlow = Block("WakeGlow", ProceduralSprites.SoftGlow, WakePoint, new Vector2(4.5f, 3f), area.wakeGlow, order - 2);

        propOrder = order - 1;
        foreach (Station station in area.stations)
        {
            Vector3 at = BaseOf(station);
            SpriteRenderer r = null;
            if (station.art != null) r = Prop(station.kind.ToString(), station.art, at, order - 1);
            else if (!station.onMap || !mapArt) r = Block(station.kind.ToString(), ProceduralSprites.White, at + Vector3.up * 0.7f, new Vector2(1.8f, 1.4f), station.placeholder, order - 1);
            if (r == null) continue;
            // A soft shadow where it meets the ground, so it stands on the floor rather than floating over it.
            Bounds b = r.bounds;
            float baseY = at.y;
            Block("Shadow", ProceduralSprites.Shadow, new Vector3(b.center.x, baseY + 0.1f, 0f), new Vector2(b.size.x * 0.95f, b.size.x * 0.3f), new Color(0f, 0f, 0f, 0.5f), order - 3);
            var s = new Standing { station = station, renderer = r, baseY = baseY, art = b, nextFx = Time.time + Random.Range(0f, 1f) };
            if (station.fx == StationFx.Glow)
                s.glow = Block("Glow", ProceduralSprites.SoftGlow, b.center, new Vector2(b.size.x * 0.9f, b.size.y * 0.8f), new Color(Teal.r, Teal.g, Teal.b, 0f), order - 2);
            standing.Add(s);
        }

        if (mapSpace && area.spiritFrames.Length > 0 && area.spiritFrames[0] != null)
        {
            Sprite first = area.spiritFrames[0];
            for (int i = 0; i < area.spirits.Length; i++)
            {
                Vector3 foot = MapToWorld(area.spirits[i]);
                var r = new GameObject("GhostFire").AddComponent<SpriteRenderer>();
                r.transform.SetParent(transform, false);
                r.sprite = first;
                r.transform.localScale = Vector3.one * mapPixel * first.pixelsPerUnit;
                r.transform.position = foot + Vector3.up * SpiritFootBelowMiddle * mapPixel;
                r.sortingOrder = order - 2;
                // Each flickers out of step with the others.
                spirits.Add((r, foot.y, i * 2.7f));
            }
        }

        if (mapSpace)
        {
            for (int i = 0; i < area.lamps.Length; i++)
            {
                Lamp lamp = area.lamps[i];
                Vector3 flame = MapToWorld(lamp.px);
                // The light pools on the ground under the flame, flattened by the high view.
                SpriteRenderer floor = Block("LampLight", ProceduralSprites.SoftGlow, flame + Vector3.down * lamp.radius * 0.25f, new Vector2(lamp.radius * 2f, lamp.radius * 1.3f), lamp.colour, order - 4);
                SpriteRenderer halo = Block("LampFlame", ProceduralSprites.Glow, flame, Vector2.one * 0.6f, new Color(lamp.colour.r, lamp.colour.g, lamp.colour.b, 0.7f), order - 3);
                lit.Add((floor, halo, lamp.colour, i * 7.3f));
            }
        }

        prompt = new GameObject("Prompt").AddComponent<TextMeshPro>();
        prompt.transform.SetParent(transform, false);
        if (font != null) prompt.font = font;
        prompt.fontSize = 5f;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.color = Color.white;
        prompt.outlineWidth = 0.2f;
        prompt.outlineColor = Color.black;
        prompt.rectTransform.sizeDelta = new Vector2(14f, 2f);
        prompt.sortingOrder = order + 20;
        prompt.gameObject.SetActive(false);
    }

    SpriteRenderer Block(string name, Sprite sprite, Vector3 position, Vector2 size, Color colour, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.transform.position = position;
        r.sprite = sprite;
        Vector2 native = sprite.bounds.size;
        r.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        r.color = colour;
        r.sortingOrder = order;
        return r;
    }

    SpriteRenderer Prop(string name, Sprite sprite, Vector3 at, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = sprite;
        float scale = mapPixel * sprite.pixelsPerUnit;
        r.transform.localScale = Vector3.one * scale;
        // Its lowest drawn pixel stands on its base (its tight mesh skips the canvas's empty rows), not the canvas edge.
        float bottom = float.MaxValue;
        foreach (Vector2 v in sprite.vertices) bottom = Mathf.Min(bottom, v.y);
        if (bottom == float.MaxValue) bottom = sprite.bounds.min.y;
        r.transform.position = at - Vector3.up * (bottom * scale);
        r.sortingOrder = order;
        return r;
    }

    // Where a station's prop stands: its place on the map, or just behind the spot he uses it from, so he faces it.
    Vector3 BaseOf(Station station) =>
        mapSpace && station.propAt != Vector2.zero ? MapToWorld(station.propAt) : At(station.spot) + Vector3.up * 0.2f;

    // The ground under the props that he walks around, in world space at his feet.
    public Rect[] Blocks
    {
        get
        {
            var blocks = new List<Rect>();
            if (mapSpace)
            {
                foreach (Rect wall in config.walls)
                {
                    Vector3 corner = MapToWorld(wall.position);
                    blocks.Add(new Rect(corner.x, corner.y - wall.height * mapPixel, wall.width * mapPixel, wall.height * mapPixel));
                }
            }
            foreach (Station station in config.stations)
            {
                if (station.footprint.x <= 0f || station.footprint.y <= 0f) continue;
                Vector3 at = BaseOf(station);
                blocks.Add(new Rect(at.x - station.footprint.x / 2f, at.y - 0.1f, station.footprint.x, station.footprint.y));
            }
            return blocks.ToArray();
        }
    }

    // The station within reach of his feet, nearest first; null when none is.
    public Station Near(Vector3 feet)
    {
        Station best = null;
        float bestDistance = float.MaxValue;
        foreach (Station station in config.stations)
        {
            float distance = Vector2.Distance(feet, At(station.spot));
            if (distance <= station.reach && distance < bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }
        return best;
    }

    // "E  FLUTE SONGS" floating over the station he's beside, or nothing.
    public void ShowPrompt(Station station)
    {
        prompt.gameObject.SetActive(station != null);
        if (station == null) return;
        prompt.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Teal)}>E</color>  {station.label}";
        float lift = station.art != null ? station.art.bounds.size.y * mapPixel * station.art.pixelsPerUnit + 0.5f : 1.8f;
        Vector3 at = station.onMap ? At(station.spot) : BaseOf(station);
        prompt.transform.position = new Vector3(at.x, Mathf.Max(at.y + lift, At(station.spot).y + 1.2f), 0f);
    }

    void Update()
    {
        // The pool breathes, slowly.
        Color c = config.wakeGlow;
        c.a *= 0.75f + 0.3f * Mathf.Sin(Time.time * 1.3f);
        wakeGlow.color = c;

        float time = Time.time;
        float feetY = ronin != null ? ronin.Feet.y : float.MinValue;
        foreach (Standing s in standing)
        {
            // Behind its base he's hidden by it; in front, he hides it.
            s.renderer.sortingOrder = feetY > s.baseY ? ronin.SpriteOrder + 5 : propOrder;
            if (s.glow != null) s.glow.color = new Color(Teal.r, Teal.g, Teal.b, 0.07f + 0.04f * Mathf.Sin(time * 0.9f));
            if (time < s.nextFx) continue;
            switch (s.station.fx)
            {
                case StationFx.Incense:
                    s.nextFx = time + 0.45f;
                    StartCoroutine(Smoke(new Vector3(s.art.center.x + Random.Range(-0.12f, 0.12f), s.art.max.y - s.art.size.y * 0.25f, 0f), s.renderer));
                    break;
                case StationFx.Glint:
                    s.nextFx = time + Random.Range(2f, 4f);
                    StartCoroutine(Glint(new Vector3(Random.Range(s.art.min.x + s.art.size.x * 0.2f, s.art.max.x - s.art.size.x * 0.2f), s.art.max.y - s.art.size.y * Random.Range(0.15f, 0.35f), 0f), s.renderer));
                    break;
                default:
                    s.nextFx = float.MaxValue;
                    break;
            }
        }

        foreach (var (r, footY, phase) in spirits)
        {
            r.sprite = config.spiritFrames[(int)((time + phase) * 9f) % config.spiritFrames.Length];
            // A flame he stands behind is in front of him.
            r.sortingOrder = feetY > footY ? ronin.SpriteOrder + 5 : propOrder - 1;
        }

        foreach (var (floor, flame, colour, seed) in lit)
        {
            // Flames don't breathe evenly: noise, not a sine.
            float flicker = 0.8f + 0.3f * Mathf.PerlinNoise(time * 6f, seed);
            floor.color = new Color(colour.r, colour.g, colour.b, colour.a * flicker);
            flame.color = new Color(colour.r, colour.g, colour.b, 0.7f * flicker);
            flame.transform.localScale = Vector3.one * (flame.sprite.bounds.size.x > 0f ? 0.6f / flame.sprite.bounds.size.x : 1f) * (0.96f + 0.08f * flicker);
        }
    }

    // A wisp of incense smoke drifting up, swaying, thinning out. Drawn just over its prop.
    System.Collections.IEnumerator Smoke(Vector3 from, SpriteRenderer over)
    {
        SpriteRenderer r = Block("Smoke", ProceduralSprites.SoftGlow, from, Vector2.one * 0.25f, Color.clear, over.sortingOrder + 1);
        float sway = Random.Range(0f, 6f);
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            float k = t / 2.6f;
            r.sortingOrder = over.sortingOrder + 1;
            r.transform.position = from + new Vector3(Mathf.Sin(t * 1.8f + sway) * 0.18f * k, t * 0.55f, 0f);
            r.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.7f, k) / r.sprite.bounds.size.x;
            r.color = new Color(0.8f, 0.82f, 0.85f, 0.22f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        Destroy(r.gameObject);
    }

    // Light catching an edge: a pixel star that swells and shrinks in a quarter of a second.
    System.Collections.IEnumerator Glint(Vector3 at, SpriteRenderer over)
    {
        SpriteRenderer r = Block("Glint", ProceduralSprites.Glint, at, Vector2.one * mapPixel * 7f, Color.white, over.sortingOrder + 1);
        Vector3 full = r.transform.localScale;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / 0.3f * Mathf.PI);
            r.sortingOrder = over.sortingOrder + 1;
            r.transform.localScale = full * k;
            r.color = new Color(0.9f, 1f, 1f, k);
            yield return null;
        }
        Destroy(r.gameObject);
    }

    void OnDestroy()
    {
        if (ronin != null) ronin.Wade(0f);
    }
}
