using System.Collections.Generic;
using TMPro;
using UnityEngine;

// What a station does when Oni uses it.
public enum StationKind { Exit, FluteSongs, Memorial, WeaponRack }

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

    public Config Area => config;
    public Vector3 WakePoint => At(config.wakeSpot);
    public Rect WalkArea { get; private set; }

    public float MapPixel => mapPixel;

    public static SafeArea Build(Config config, TMP_FontAsset font, int order, Bounds? ground)
    {
        var area = new GameObject("SafeArea " + config.name).AddComponent<SafeArea>();
        area.Lay(config, font, order, ground);
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

        // Without its map there's still somewhere to wake and a way out.
        if (!mapArt) Block("Pool", ProceduralSprites.Disc, WakePoint, new Vector2(3f, 1.6f), new Color(0.2f, 0.6f, 0.55f), order - 3);
        wakeGlow = Block("WakeGlow", ProceduralSprites.SoftGlow, WakePoint, new Vector2(4.5f, 3f), area.wakeGlow, order - 2);

        foreach (Station station in area.stations)
        {
            Vector3 at = At(station.spot);
            if (station.art != null) Prop(station.kind.ToString(), station.art, at, order - 1);
            else if (!station.onMap || !mapArt) Block(station.kind.ToString(), ProceduralSprites.White, at + Vector3.up * 0.7f, new Vector2(1.8f, 1.4f), station.placeholder, order - 1);
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

    void Prop(string name, Sprite sprite, Vector3 at, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = sprite;
        float scale = mapPixel * sprite.pixelsPerUnit;
        r.transform.localScale = Vector3.one * scale;
        // The art is centred; its base stands just behind its spot, so he uses it from in front.
        r.transform.position = at + Vector3.up * (sprite.bounds.extents.y * scale + 0.2f);
        r.sortingOrder = order;
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
        float lift = station.art != null ? station.art.bounds.size.y * mapPixel * station.art.pixelsPerUnit + 0.7f : 1.8f;
        prompt.transform.position = At(station.spot) + Vector3.up * Mathf.Max(1.2f, lift);
    }

    void Update()
    {
        // The pool breathes, slowly.
        Color c = config.wakeGlow;
        c.a *= 0.75f + 0.3f * Mathf.Sin(Time.time * 1.3f);
        wakeGlow.color = c;
    }
}
