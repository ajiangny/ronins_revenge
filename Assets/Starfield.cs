using UnityEngine;

// Scrolling star background drawn behind the play area.
public class Starfield : MonoBehaviour
{
    [Header("Inscribed")]
    [SerializeField] Material starMaterial;
    [SerializeField] int starCount = 160;
    [SerializeField] Vector2 sizeRange = new Vector2(0.04f, 0.16f);
    [SerializeField] Vector2 speedRange = new Vector2(0.3f, 1.5f);

    Transform[] stars;
    float[] speeds;
    float halfWidth, halfHeight, centerX, centerY;

    void Start()
    {
        Camera cam = Camera.main;
        // Over-size the field a little so resizing the window doesn't reveal gaps.
        halfHeight = cam.orthographicSize + 1f;
        halfWidth = halfHeight * Mathf.Max(cam.aspect, 2f);
        centerX = cam.transform.position.x;
        centerY = cam.transform.position.y;

        stars = new Transform[starCount];
        speeds = new float[starCount];
        for (int i = 0; i < starCount; i++)
        {
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(star.GetComponent<Collider>());
            star.name = "Star";
            star.transform.SetParent(transform);
            star.transform.position = new Vector3(
                centerX + Random.Range(-halfWidth, halfWidth),
                centerY + Random.Range(-halfHeight, halfHeight),
                transform.position.z);

            // Smaller stars move slower, which gives a cheap parallax effect.
            float t = Random.value;
            star.transform.localScale = Vector3.one * Mathf.Lerp(sizeRange.x, sizeRange.y, t);
            speeds[i] = Mathf.Lerp(speedRange.x, speedRange.y, t);

            Renderer r = star.GetComponent<Renderer>();
            r.sharedMaterial = starMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stars[i] = star.transform;
        }
    }

    void Update()
    {
        for (int i = 0; i < stars.Length; i++)
        {
            Vector3 position = stars[i].position;
            position.y -= speeds[i] * Time.deltaTime;
            if (position.y < centerY - halfHeight)
            {
                position.y += 2f * halfHeight;
                position.x = centerX + Random.Range(-halfWidth, halfWidth);
            }
            stars[i].position = position;
        }
    }
}
