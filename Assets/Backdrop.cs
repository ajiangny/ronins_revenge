using UnityEngine;

// Top-down backdrop behind the play area: the moonlit courtyard the samurai march across, with sakura petals drifting over it.
public class Backdrop : MonoBehaviour
{
    // Another place to fight: its own ground and drifting petals (or flakes, leaves...). Set per stage and per boss.
    [System.Serializable]
    public class Map
    {
        public Sprite ground;
        [Tooltip("Darkens the ground so the samurai and ronin stand out against it.")]
        public Color groundTint = Color.white;
        [Tooltip("Leave empty to keep the sakura petals.")]
        public Sprite petal;
        public Color petalTint = new Color(1f, 1f, 1f, 0.8f);
        [Tooltip("This map's cover. Leave empty to keep the courtyard's bushes and lanterns.")]
        public BlockerWall.BlockerType[] blockers;
        [Tooltip("Blockers at each site, left to right, by each type's first letter.")]
        public string blockerPattern;
        [Tooltip("This map's mood: cloud shadows, moonlight, mist, motes, darkened edges.")]
        public Atmosphere.Settings atmosphere;
    }

    [Header("Inscribed")]
    [SerializeField] Sprite ground;
    [Tooltip("Darkens the ground so the samurai and ronin stand out against it.")]
    [SerializeField] Color groundTint = Color.white;
    [SerializeField] Sprite petal;
    [SerializeField] int petalCount = 40;
    [SerializeField] Vector2 petalSizeRange = new Vector2(0.5f, 1f);
    [SerializeField] Vector2 fallSpeedRange = new Vector2(0.6f, 1.6f);
    [SerializeField] float swayAmount = 0.6f;
    [SerializeField] Color petalTint = new Color(1f, 1f, 1f, 0.8f);
    [Tooltip("The courtyard's mood, and any map's that has no ground of its own.")]
    [SerializeField] Atmosphere.Settings courtyardAtmosphere = new Atmosphere.Settings { clouds = 3, moonlight = new Color(0.8f, 0.88f, 1f, 0.1f) };

    class Petal
    {
        public Transform transform;
        public float fallSpeed, swayPhase, swaySpeed, spin, baseX;
    }

    Petal[] petals;
    float halfWidth, halfHeight, centerX, centerY, viewWidth, viewHeight;
    // The tints of the map on show, which the boss moon blends from.
    Color shownGroundTint, shownPetalTint;
    // A map asked for before Start has built the scene (the GameManager can start first); shown once it's built.
    Map pendingMap;
    bool mapPending;

    public enum MoonLook { Blood, Steel, Spirit, Soul }

    // How a boss moon colours the scene: the ground tint it multiplies in, what the petals become, the moon and its
    // halo, and the tint the moonlight takes.
    struct MoonColours
    {
        public Color ground, petal, moon, halo, light;
    }

    static readonly MoonColours[] Moons =
    {
        // Blood moon: during boss fights the courtyard bleeds red under a crimson moon and the petals turn red.
        new MoonColours
        {
            ground = new Color(0.95f, 0.42f, 0.42f), petal = new Color(0.9f, 0.08f, 0.14f, 0.9f),
            moon = new Color(0.85f, 0.1f, 0.12f), halo = new Color(1f, 0.15f, 0.15f, 0.5f), light = new Color(1f, 0.35f, 0.35f),
        },
        // Steel moon: Utsusemi's pass turns cold and pale, and the petals become drifting flakes of lace.
        new MoonColours
        {
            ground = new Color(0.6f, 0.7f, 0.9f), petal = new Color(0.9f, 0.93f, 1f, 0.85f),
            moon = new Color(0.85f, 0.9f, 1f), halo = new Color(0.6f, 0.75f, 1f, 0.45f), light = new Color(0.7f, 0.8f, 1f),
        },
        // Spirit moon: Shirogane's shrine glows an unearthly cyan, and the petals become drifting snow.
        new MoonColours
        {
            ground = new Color(0.55f, 0.72f, 0.95f), petal = new Color(0.85f, 0.97f, 1f, 0.9f),
            moon = new Color(0.7f, 0.95f, 1f), halo = new Color(0.35f, 0.85f, 1f, 0.5f), light = new Color(0.55f, 0.9f, 1f),
        },
        // No moon for the Emperor: the sky goes starless black and the only light is his teal soul-fire, so the disc
        // and halo stay clear and the ground sinks into the dark, faintly green.
        new MoonColours
        {
            ground = new Color(0.42f, 0.55f, 0.52f), petal = new Color(0.35f, 1f, 0.82f, 0.75f),
            moon = Color.clear, halo = Color.clear, light = new Color(0.3f, 0.95f, 0.8f),
        },
    };

    MoonLook look;
    SpriteRenderer groundRenderer;
    SpriteRenderer[] petalRenderers;
    SpriteRenderer moon;
    SpriteRenderer moonHalo;
    Atmosphere atmosphere;
    float blood;
    float bloodTarget;
    // The true ending's sunrise: a warm sky climbing from the top of the field and a sun glow over the hill line.
    // Nothing shows a dawn before it.
    SpriteRenderer dawnSky, dawnSun;
    float dawn;
    float dawnTarget;

    void Start()
    {
        Camera cam = Camera.main;
        // Over-size the field a little so resizing the window doesn't reveal gaps.
        halfHeight = cam.orthographicSize + 0.5f;
        halfWidth = halfHeight * Mathf.Max(cam.aspect, 2f);
        centerX = CameraShake.Center.x;
        centerY = CameraShake.Center.y;

        // Fit the ground to the real view (not the petals' overscan) so the courtyard isn't cropped more than needed.
        viewHeight = 2f * halfHeight;
        viewWidth = viewHeight * cam.aspect;
        groundRenderer = CreateSprite("Ground", ground, new Vector3(centerX, centerY, 0), 1f, 0, groundTint);
        FitGround();
        shownGroundTint = groundTint;
        shownPetalTint = petalTint;

        atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
        atmosphere.transform.SetParent(transform);
        atmosphere.Init(new Vector2(centerX, centerY), viewWidth / 2f, viewHeight / 2f, transform.position.z - 0.3f);
        atmosphere.Apply(courtyardAtmosphere);

        // The blood moon hangs in the top-left corner, hidden until a boss fight raises it.
        Vector3 moonSpot = new Vector3(centerX - viewWidth * 0.36f, centerY + viewHeight * 0.3f, 0);
        moonHalo = CreateSprite("MoonHalo", ProceduralSprites.Glow, moonSpot, 9f, 0, Color.clear);
        moon = CreateSprite("BloodMoon", ProceduralSprites.Disc, moonSpot, 3.2f, 0, Color.clear);
        moonHalo.sortingOrder = moon.sortingOrder = -99;
        dawnSky = CreateSprite("DawnSky", ProceduralSprites.FadeDown, new Vector3(centerX, centerY + viewHeight * 0.1f, 0), 1f, 0, Color.clear);
        // The gradient is a one-pixel-wide strip, so it's stretched by its own size to cover the view.
        Vector2 skySize = dawnSky.sprite.bounds.size;
        dawnSky.transform.localScale = new Vector3(viewWidth * 1.2f / skySize.x, viewHeight * 0.9f / skySize.y, 1f);
        dawnSun = CreateSprite("DawnSun", ProceduralSprites.SoftGlow, new Vector3(centerX + viewWidth * 0.18f, centerY + viewHeight * 0.42f, 0), 16f, 0, Color.clear);
        dawnSky.sortingOrder = -98;
        dawnSun.sortingOrder = -97;

        petals = new Petal[petalCount];
        petalRenderers = new SpriteRenderer[petalCount];
        for (int i = 0; i < petalCount; i++)
        {
            float size = Random.Range(petalSizeRange.x, petalSizeRange.y);
            petalRenderers[i] = CreateSprite("Petal", petal, Vector3.zero, size, 1, petalTint);
            var p = new Petal
            {
                transform = petalRenderers[i].transform,
                // Bigger petals are "closer", so they fall faster.
                fallSpeed = Mathf.Lerp(fallSpeedRange.x, fallSpeedRange.y, Mathf.InverseLerp(petalSizeRange.x, petalSizeRange.y, size)),
                swayPhase = Random.Range(0f, Mathf.PI * 2f),
                swaySpeed = Random.Range(0.8f, 1.6f),
                spin = Random.Range(-120f, 120f),
                baseX = centerX + Random.Range(-halfWidth, halfWidth),
            };
            p.transform.position = new Vector3(p.baseX, centerY + Random.Range(-halfHeight, halfHeight), p.transform.position.z);
            petals[i] = p;
        }
        if (mapPending) Show(pendingMap);
    }

    // Scales the ground to cover the view, whatever size its art is.
    void FitGround()
    {
        Vector2 size = groundRenderer.sprite.bounds.size;
        groundRenderer.transform.localScale = Vector3.one * Mathf.Max(viewWidth / size.x, viewHeight / size.y);
    }

    // Swaps to another map's ground and petals; null (or a map without ground art) brings back the courtyard.
    public void Show(Map map)
    {
        if (groundRenderer == null)
        {
            pendingMap = map;
            mapPending = true;
            return;
        }
        bool own = map != null && map.ground != null;
        groundRenderer.sprite = own ? map.ground : ground;
        FitGround();
        shownGroundTint = own ? map.groundTint : groundTint;
        bool ownPetals = own && map.petal != null;
        shownPetalTint = ownPetals ? map.petalTint : petalTint;
        foreach (SpriteRenderer r in petalRenderers) r.sprite = ownPetals ? map.petal : petal;
        atmosphere.Apply(own ? map.atmosphere : courtyardAtmosphere);
        UpdateBloodMoon(0f);
    }

    // Eases the whole scene toward (or away from) the blood moon over about two seconds.
    void UpdateBloodMoon(float dt)
    {
        blood = Mathf.MoveTowards(blood, bloodTarget, dt * 0.5f);
        MoonColours colours = Moons[(int)look];
        groundRenderer.color = Color.Lerp(shownGroundTint, shownGroundTint * colours.ground, blood);
        Color petalColour = Color.Lerp(shownPetalTint, colours.petal, blood);
        foreach (SpriteRenderer r in petalRenderers) r.color = petalColour;
        moon.color = new Color(colours.moon.r, colours.moon.g, colours.moon.b, colours.moon.a * blood);
        moonHalo.color = new Color(colours.halo.r, colours.halo.g, colours.halo.b, colours.halo.a * blood);
        atmosphere.TintMoon(colours.light, blood);
    }

    SpriteRenderer CreateSprite(string name, Sprite sprite, Vector3 position, float scale, int order, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        // Petals sit slightly nearer the camera than the ground; z keeps both behind the gameplay plane.
        go.transform.position = new Vector3(position.x, position.y, transform.position.z - order * 0.1f);
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = color;
        r.sortingOrder = order - 100;
        return r;
    }

    // The sun comes up over the field, slowly, over about six seconds.
    public void SetDawn(bool on)
    {
        dawnTarget = on ? 1f : 0f;
    }

    static readonly Color DawnGlow = new Color(1f, 0.62f, 0.38f);
    static readonly Color DawnGround = new Color(1.15f, 0.92f, 0.8f);

    void UpdateDawn(float dt)
    {
        dawn = Mathf.MoveTowards(dawn, dawnTarget, dt / 6f);
        float k = Mathf.SmoothStep(0, 1, dawn);
        dawnSky.color = new Color(DawnGlow.r, DawnGlow.g, DawnGlow.b, 0.55f * k);
        dawnSun.color = new Color(1f, 0.85f, 0.6f, 0.6f * k);
        if (blood <= 0f) groundRenderer.color = Color.Lerp(shownGroundTint, shownGroundTint * DawnGround, k);
        atmosphere.TintMoon(DawnGlow, k);
    }

    // Raises (or sets) the boss moon; the look only changes when it rises, so it fades out in the colour it came in.
    public void SetBossMoon(bool on, MoonLook moonLook = MoonLook.Blood)
    {
        if (on) look = moonLook;
        bloodTarget = on ? 1f : 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (!Mathf.Approximately(blood, bloodTarget)) UpdateBloodMoon(Time.unscaledDeltaTime);
        if (!Mathf.Approximately(dawn, dawnTarget)) UpdateDawn(Time.unscaledDeltaTime);
        foreach (Petal p in petals)
        {
            Vector3 position = p.transform.position;
            position.y -= p.fallSpeed * dt;
            p.swayPhase += p.swaySpeed * dt;
            position.x = p.baseX + Mathf.Sin(p.swayPhase) * swayAmount;
            if (position.y < centerY - halfHeight)
            {
                position.y += 2f * halfHeight;
                p.baseX = centerX + Random.Range(-halfWidth, halfWidth);
            }
            p.transform.position = position;
            p.transform.Rotate(0, 0, p.spin * dt);
        }
    }
}
