using UnityEngine;

// A burn mark a beam or blow leaves on the ground: it glows briefly at its edge, then sets to a dark char mark that
// fades. Purely for show, and never a live flame: players took burning trails for a hazard.
public class GroundFire : MonoBehaviour
{
    const float Width = 1.3f;
    static readonly Color ScorchHot = new Color(1f, 0.5f, 0.2f, 0.45f);
    // Kagetsune's soul-fire scorches with a teal edge.
    static readonly Color SoulScorchHot = new Color(0.3f, 1f, 0.82f, 0.4f);
    static readonly Color ScorchChar = new Color(0.07f, 0.04f, 0.04f, 0.7f);
    const float ScorchGlow = 0.35f;
    const float ScorchLife = 2.6f;

    SpriteRenderer embers;
    Color hot;
    float age;

    // Scale widens one strike's mark (a single laser hour) beyond a dragged beam's patches; `soul` gives it
    // Kagetsune's teal edge instead of the laser's orange.
    public static void Scorch(Vector3 at, float scale = 1f, bool soul = false)
    {
        var mark = new GameObject("Scorch").AddComponent<GroundFire>();
        mark.transform.position = new Vector3(at.x, at.y, 0f);
        float size = Random.Range(0.85f, 1.15f) * scale;
        mark.hot = soul ? SoulScorchHot : ScorchHot;
        mark.embers = new GameObject("Mark").AddComponent<SpriteRenderer>();
        mark.embers.transform.SetParent(mark.transform, false);
        mark.embers.transform.localPosition = new Vector3(0, 0.02f, 0.1f);
        mark.embers.transform.localScale = new Vector3(Width * 1.1f * size, 0.26f * Mathf.Sqrt(scale), 1f);
        mark.embers.sprite = ProceduralSprites.Glow;
        mark.embers.color = mark.hot;
        mark.embers.sortingOrder = 2;
    }

    void Update()
    {
        age += Time.deltaTime;
        Color c = Color.Lerp(hot, ScorchChar, Mathf.Clamp01(age / ScorchGlow));
        float fade = Mathf.Clamp01((age - ScorchLife * 0.6f) / (ScorchLife * 0.4f));
        c.a *= 1f - fade;
        embers.color = c;
        if (age >= ScorchLife) Destroy(gameObject);
    }
}
