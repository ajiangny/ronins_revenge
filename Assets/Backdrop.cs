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
        CreateSprite("Ground", ground, new Vector3(centerX, centerY, 0), scale, 0, groundTint);

        petals = new Petal[petalCount];
        for (int i = 0; i < petalCount; i++)
        {
            float size = Random.Range(petalSizeRange.x, petalSizeRange.y);
            var p = new Petal
            {
                transform = CreateSprite("Petal", petal, Vector3.zero, size, 1, petalTint).transform,
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

    void Update()
    {
        float dt = Time.deltaTime;
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
