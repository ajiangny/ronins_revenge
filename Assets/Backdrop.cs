using UnityEngine;

// Top-down backdrop behind the play area: the moonlit courtyard the samurai march across, with sakura petals drifting over it.
public class Backdrop : MonoBehaviour
{
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

    class Petal
    {
        public Transform transform;
        public float fallSpeed, swayPhase, swaySpeed, spin, baseX;
    }

    Petal[] petals;
    float halfWidth, halfHeight, centerX, centerY;

    // Blood moon: during boss fights the courtyard bleeds red under a crimson moon and the petals turn red.
    static readonly Color BloodGround = new Color(0.95f, 0.42f, 0.42f);
    static readonly Color BloodPetal = new Color(0.9f, 0.08f, 0.14f, 0.9f);
    SpriteRenderer groundRenderer;
    SpriteRenderer[] petalRenderers;
    SpriteRenderer moon;
    SpriteRenderer moonHalo;
    float blood;
    float bloodTarget;

    void Start()
    {
        Camera cam = Camera.main;
        // Over-size the field a little so resizing the window doesn't reveal gaps.
        halfHeight = cam.orthographicSize + 0.5f;
        halfWidth = halfHeight * Mathf.Max(cam.aspect, 2f);
        centerX = CameraShake.Center.x;
        centerY = CameraShake.Center.y;

        // Fit the ground to the real view (not the petals' overscan) so the courtyard isn't cropped more than needed.
        float viewHeight = 2f * halfHeight, viewWidth = viewHeight * cam.aspect;
        float scale = Mathf.Max(viewWidth / ground.bounds.size.x, viewHeight / ground.bounds.size.y);
        groundRenderer = CreateSprite("Ground", ground, new Vector3(centerX, centerY, 0), scale, 0, groundTint);

        // The blood moon hangs in the top-left corner, hidden until a boss fight raises it.
        Vector3 moonSpot = new Vector3(centerX - viewWidth * 0.36f, centerY + viewHeight * 0.3f, 0);
        moonHalo = CreateSprite("MoonHalo", ProceduralSprites.Glow, moonSpot, 9f, 0, Color.clear);
        moon = CreateSprite("BloodMoon", ProceduralSprites.Disc, moonSpot, 3.2f, 0, Color.clear);
        moonHalo.sortingOrder = moon.sortingOrder = -99;

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
    }

    // Eases the whole scene toward (or away from) the blood moon over about two seconds.
    void UpdateBloodMoon(float dt)
    {
        blood = Mathf.MoveTowards(blood, bloodTarget, dt * 0.5f);
        groundRenderer.color = Color.Lerp(groundTint, groundTint * BloodGround, blood);
        Color petalColour = Color.Lerp(petalTint, BloodPetal, blood);
        foreach (SpriteRenderer r in petalRenderers) r.color = petalColour;
        moon.color = new Color(0.85f, 0.1f, 0.12f, blood);
        moonHalo.color = new Color(1f, 0.15f, 0.15f, 0.5f * blood);
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

    public void SetBloodMoon(bool on)
    {
        bloodTarget = on ? 1f : 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (!Mathf.Approximately(blood, bloodTarget)) UpdateBloodMoon(Time.unscaledDeltaTime);
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
