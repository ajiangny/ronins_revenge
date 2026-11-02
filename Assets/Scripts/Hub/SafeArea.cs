using System.Collections.Generic;
using TMPro;
using UnityEngine;

// What a station does when Oni uses it.
public enum StationKind { Exit, FluteSongs, Memorial, WeaponRack, Oaths, Poguru, Asagi }

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
        [Tooltip("Its prop while it's sealed (the oath stone in its chains). Empty: its art.")]
        public Sprite sealedArt;
        // Set by the game before the area is built; Unseal clears it.
        [System.NonSerialized] public bool isSealed;
        // Not there yet (Asagi before Oni has met her): nothing built, nothing in reach. Set by the game before the
        // area is built; Reveal clears it.
        [System.NonSerialized] public bool hidden;
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
        [Tooltip("The Glow fx's colour. Clear: the souls' teal. A sealed station doesn't glow.")]
        public Color glowColour;
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
        [Tooltip("Asagi's clips, for her station at the pool's rim (Ninja/Apply Maps). Empty draws her as a block of her sea-blue.")]
        public AsagiArt asagiArt = new AsagiArt();
        [Tooltip("Oliver's clips, for his figure by the weapon rack (Ninja/Apply Maps). Empty draws him as a block of iron and forge-gold.")]
        public OliverArt oliverArt = new OliverArt();
        public Lamp[] lamps = new Lamp[0];
        [Tooltip("Ground painted on the map he can't walk through (lanterns, a dais's edges, a kerb): rectangles in map pixels, x and y their top-left corner.")]
        public Rect[] walls = new Rect[0];
        [Tooltip("Ghost-fires floating over the map: where each flame's foot is, in map pixels from the top-left.")]
        public Vector2[] spirits = new Vector2[0];
        [Tooltip("The ghost-fire's flicker, drawn at the map's pixel size, its flame's foot at the canvas's bottom-centre row 38.")]
        public Sprite[] spiritFrames = new Sprite[0];
        [Tooltip("The gate's doorway on the map, in map pixels (x, y its top-left): its two leaves are cut from it and swing open as he nears the exit. Zero: no doors.")]
        public Rect gateDoors;
        [Tooltip("The parts of the gate he passes under once he's through the doorway (its beam and roof, its posts), in map pixels: they hide him there.")]
        public Rect[] gateOver = new Rect[0];
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
        // Asagi's figure, when the station is her.
        public Asagi figure;
    }
    readonly List<Standing> standing = new List<Standing>();
    readonly List<(SpriteRenderer floor, SpriteRenderer flame, Color colour, float seed)> lit = new List<(SpriteRenderer, SpriteRenderer, Color, float)>();
    int propOrder;
    readonly List<(SpriteRenderer renderer, float footY, float phase)> spirits = new List<(SpriteRenderer, float, float)>();
    // The ghost-fire frames put the flame's foot this far below the canvas's middle (row 38 of 48), in its pixels.
    const float SpiritFootBelowMiddle = 14f;

    // The gate's doors are drawn as the map is, under the moon's shade (Atmosphere's -94), so they match its pixels.
    const int GateOrder = -96;
    // How long the doors take to swing, and how narrow a leaf looks turned away from us.
    const float GateSwingTime = 0.7f;
    const float GateOpenWidth = 0.14f;
    SpriteRenderer passage;
    SpriteRenderer[] leaves;
    Color leafTint;
    float gateOpen, threshold;
    bool gateOpening;
    // Through the doorway, the gate's beam and posts hide him (Ronin.Cover).
    bool covered;

    public Config Area => config;
    public Vector3 WakePoint => At(config.wakeSpot);
    // Asagi at the pool's rim, once her station is built; null before she's met.
    public Asagi AsagiFigure { get; private set; }
    // Oliver by the rack; built always, and out of sight until he's met (ShowOliver).
    public Oliver OliverFigure { get; private set; }
    float oliverBaseY;
    // Where he stands from the rack's base, in map pixels: right of it, past the floor Oni can walk, so he blocks neither
    // the rack nor the spot Oni uses it from.
    static readonly Vector2 OliverFromRack = new Vector2(52f, -20f);
    public Rect WalkArea { get; private set; }

    public float MapPixel => mapPixel;

    // Held open while he sets out, whether or not he's near; Opened once the leaves have swung all the way.
    public bool HoldGateOpen { get; set; }
    public bool GateOpened => leaves == null || gateOpen >= 1f;

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

    // The top of a station's prop (where Poguru perches on his nest), or a little over its spot without one.
    public Vector3 TopOf(StationKind kind)
    {
        foreach (Standing s in standing)
            if (s.station.kind == kind) return new Vector3(s.art.center.x, s.art.max.y, 0f);
        return PointOf(kind) + Vector3.up * 1.5f;
    }

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

        if (mapSpace && area.poolRadii.x > 0f && area.poolRadii.y > 0f)
        {
            pool = SoulPool.Build(this, area, ronin, ground);
            // Until Oni has met her, the koi is under the water: her ripples cross it.
            foreach (Station station in area.stations)
                if (station.kind == StationKind.Asagi && station.hidden) pool.Koi = true;
        }
        // Without its map there's still somewhere to wake and a way out.
        if (!mapArt) Block("Pool", ProceduralSprites.Disc, WakePoint, new Vector2(3f, 1.6f), new Color(0.2f, 0.6f, 0.55f), order - 3);
        wakeGlow = Block("WakeGlow", ProceduralSprites.SoftGlow, WakePoint, new Vector2(4.5f, 3f), area.wakeGlow, order - 2);

        propOrder = order - 1;
        foreach (Station station in area.stations)
        {
            Vector3 at = BaseOf(station);
            SpriteRenderer r = null;
            if (station.kind == StationKind.Asagi)
            {
                // She stands on the pool's back-left rim at Oni's pixel size, her own shadow and glow with her; before
                // he's met her she's not there at all.
                AsagiFigure = Asagi.Create(area.asagiArt, transform, at, ronin.ArtPixelSize, order - 1);
                AsagiFigure.gameObject.SetActive(!station.hidden);
                standing.Add(new Standing { station = station, renderer = AsagiFigure.Body, baseY = at.y, art = AsagiFigure.Bounds, figure = AsagiFigure, nextFx = float.MaxValue });
                continue;
            }
            Sprite art = station.isSealed && station.sealedArt != null ? station.sealedArt : station.art;
            if (art != null)
            {
                // Both looks of a sealed station share a canvas; it stands by the sealed one's base in either, so
                // the broken chains lying in front of it don't lift it.
                r = Prop(station.kind.ToString(), station.sealedArt != null ? station.sealedArt : art, at, order - 1);
                r.sprite = art;
            }
            else if (!station.onMap || !mapArt) r = Block(station.kind.ToString(), ProceduralSprites.White, at + Vector3.up * 0.7f, new Vector2(1.8f, 1.4f), station.placeholder, order - 1);
            if (station.kind == StationKind.WeaponRack && mapSpace)
            {
                Vector3 feet = MapToWorld(station.propAt + OliverFromRack);
                oliverBaseY = feet.y;
                OliverFigure = Oliver.Create(area.oliverArt, transform, feet, ronin.ArtPixelSize, order - 1);
                OliverFigure.gameObject.SetActive(Save.Data.oliverMet);
            }
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

        if (mapSpace && area.gateDoors.width > 0f && area.gateDoors.height > 0f) BuildGate(area);

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

    // The gate's two doors, cut from the map's own pixels and hinged at their outer edges, over the dark road beyond;
    // and the gate he walks under, as masks on his sprite alone so the map's own (moon-shaded) beam shows over him.
    void BuildGate(Config area)
    {
        Rect doors = area.gateDoors;
        Vector3 topLeft = MapToWorld(doors.position);
        Vector3 bottomRight = MapToWorld(doors.position + doors.size);
        threshold = bottomRight.y;
        Vector3 middle = (topLeft + bottomRight) / 2f;
        Vector2 size = new Vector2(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);

        // Past the doors the road falls away into the dark, a little moonlit stone at its near end.
        var dark = new Texture2D(1, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 16; y++)
            dark.SetPixel(0, y, Color.Lerp(new Color(0.16f, 0.17f, 0.24f), new Color(0.03f, 0.02f, 0.06f), Mathf.Sqrt(y / 15f)));
        dark.Apply();
        passage = Block("GatePassage", Sprite.Create(dark, new Rect(0, 0, 1, 16), new Vector2(0.5f, 0.5f), 16f), middle, size, area.map.groundTint, GateOrder);
        passage.enabled = false;

        Sprite map = area.map.ground;
        leafTint = area.map.groundTint;
        float half = Mathf.Round(doors.width / 2f);
        leaves = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            // Texture rows count from the bottom; map pixels from the top.
            float x = map.rect.x + doors.x + (i == 0 ? 0f : half);
            float y = map.rect.y + map.rect.height - doors.y - doors.height;
            float width = i == 0 ? half : doors.width - half;
            Sprite leaf = Sprite.Create(map.texture, new Rect(x, y, width, doors.height), new Vector2(i == 0 ? 0f : 1f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            var r = new GameObject(i == 0 ? "GateLeafLeft" : "GateLeafRight").AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            r.sprite = leaf;
            r.color = leafTint;
            r.sortingOrder = GateOrder + 1;
            r.transform.localScale = Vector3.one * mapPixel;
            r.transform.position = new Vector3(i == 0 ? topLeft.x : bottomRight.x, middle.y, 0f);
            leaves[i] = r;
        }

        foreach (Rect over in area.gateOver)
        {
            var mask = new GameObject("GateOver").AddComponent<SpriteMask>();
            mask.transform.SetParent(transform, false);
            mask.sprite = ProceduralSprites.White;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = ronin.SpriteOrder - 1;
            mask.frontSortingOrder = ronin.SpriteOrder + 1;
            Vector3 corner = MapToWorld(over.position), far = MapToWorld(over.position + over.size);
            mask.transform.position = (corner + far) / 2f;
            Vector2 native = mask.sprite.bounds.size;
            mask.transform.localScale = new Vector3((far.x - corner.x) / native.x, (corner.y - far.y) / native.y, 1f);
        }
    }

    void SwingGate()
    {
        // The doors open as he comes up to the exit and close behind him when he walks away.
        bool near = HoldGateOpen;
        foreach (Station station in config.stations)
            if (station.kind == StationKind.Exit && Vector2.Distance(ronin.Feet, At(station.spot)) <= station.reach + 0.6f) near = true;
        if (near != gateOpening && (near || gateOpen > 0f)) Sfx.Play(near ? Sfx.GateOpen : Sfx.GateClose);
        gateOpening = near;
        gateOpen = Mathf.MoveTowards(gateOpen, near ? 1f : 0f, Time.deltaTime / GateSwingTime);

        // Eased, and darkening as each leaf turns away from the moon.
        float k = Mathf.SmoothStep(0f, 1f, gateOpen);
        passage.enabled = gateOpen > 0f;
        foreach (SpriteRenderer leaf in leaves)
        {
            Vector3 scale = leaf.transform.localScale;
            leaf.transform.localScale = new Vector3(mapPixel * Mathf.Lerp(1f, GateOpenWidth, k), scale.y, scale.z);
            leaf.color = Color.Lerp(leafTint, leafTint * new Color(0.45f, 0.45f, 0.5f, 1f), k);
        }

        // Over the threshold he's under the gate; only an open one lets him through.
        bool under = ronin.Feet.y > threshold;
        if (under != covered)
        {
            covered = under;
            ronin.Cover(under);
        }
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

    // A sealed station's chains are gone: its prop changes to its own art where it stands, and its glow comes up.
    public void Unseal(StationKind kind)
    {
        foreach (Standing s in standing)
        {
            if (s.station.kind != kind || !s.station.isSealed) continue;
            s.station.isSealed = false;
            if (s.station.art != null) s.renderer.sprite = s.station.art;
        }
    }

    // A hidden station comes into the temple (Asagi at the rim on the wake he meets her): her figure appears where it
    // was laid, the pool's koi ripples stop, and the station can be used.
    public void Reveal(StationKind kind)
    {
        foreach (Standing s in standing)
        {
            if (s.station.kind != kind || !s.station.hidden) continue;
            s.station.hidden = false;
            if (s.figure != null)
            {
                s.figure.gameObject.SetActive(true);
                s.art = s.figure.Bounds;
            }
            else s.renderer.gameObject.SetActive(true);
        }
        if (kind == StationKind.Asagi && pool != null) pool.Koi = false;
    }

    // Oliver comes into the temple on the wake Oni meets him, in the clip he's found in.
    public void ShowOliver(Oliver.Clip clip, bool loop = false)
    {
        if (OliverFigure == null) return;
        OliverFigure.gameObject.SetActive(true);
        OliverFigure.Play(clip, loop);
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
                if (station.hidden || station.footprint.x <= 0f || station.footprint.y <= 0f) continue;
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
            if (station.hidden) continue;
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
        foreach (Standing s in standing)
            if (s.station == station && s.figure != null) lift = s.figure.Bounds.size.y + 0.5f;
        Vector3 at = station.onMap ? At(station.spot) : BaseOf(station);
        prompt.transform.position = new Vector3(at.x, Mathf.Max(at.y + lift, At(station.spot).y + 1.2f), 0f);
    }

    void Update()
    {
        // The pool breathes, slowly.
        Color c = config.wakeGlow;
        c.a *= 0.75f + 0.3f * Mathf.Sin(Time.time * 1.3f);
        wakeGlow.color = c;

        if (leaves != null && ronin != null) SwingGate();

        float time = Time.time;
        float feetY = ronin != null ? ronin.Feet.y : float.MinValue;
        foreach (Standing s in standing)
        {
            // Behind its base he's hidden by it; in front, he hides it.
            if (s.figure != null) s.figure.SetOrder(feetY > s.baseY ? ronin.SpriteOrder + 5 : propOrder);
            else s.renderer.sortingOrder = feetY > s.baseY ? ronin.SpriteOrder + 5 : propOrder;
            if (s.glow != null)
            {
                Color glow = s.station.glowColour.a > 0f ? s.station.glowColour : Teal;
                s.glow.color = new Color(glow.r, glow.g, glow.b, s.station.isSealed ? 0f : 0.07f + 0.04f * Mathf.Sin(time * 0.9f));
            }
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

        // Behind his feet Oni is hidden by him, as by any prop.
        if (OliverFigure != null) OliverFigure.SetOrder(feetY > oliverBaseY ? ronin.SpriteOrder + 5 : propOrder);

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
        if (ronin == null) return;
        ronin.Wade(0f);
        ronin.Cover(false);
    }
}
