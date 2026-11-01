using UnityEngine;

// A hyper beam's warning: a fan of hard-edged rays of light from the boss to the ground, laid on the floor over all the
// beam will sweep. A ray can stop short of the ground, and what lies past that point is drawn in a pale colour: that's
// where the player can stand. The owner says where each ray stops, since each beam has its own idea of what's safe.
// Redrawn every frame; destroying the object takes the mesh with it, so a cast cut short can't leak one.
public class SunrayFan : MonoBehaviour
{
    // How many rays it's measured with, how many wedges make one band of light, and how fast the bands drift.
    const int Rays = 180, Band = 6;
    const float Drift = 4f;
    // Its dark bands, as a share of its bright ones, and how bright the pale part is, relative to the light.
    const float DimBand = 0.5f, PaleShare = 1.1f;
    // On the floor (the backdrop is at -97 and below), under the ronin (0) and his aura (-1), so it never hides him.
    const int Order = -10;

    Mesh mesh;
    Vector3[] vertices;
    Color[] colours;

    // Drawn with the sprites' own material, white, so the vertex colours give it its look.
    public static SunrayFan Create(Material material)
    {
        var fan = new GameObject("SunrayFan").AddComponent<SunrayFan>();
        fan.mesh = new Mesh();
        fan.mesh.MarkDynamic();
        // Each wedge is a lit triangle and a pale quad, sharing no vertices, so the bands have hard edges.
        fan.vertices = new Vector3[Rays * 7];
        fan.colours = new Color[Rays * 7];
        var triangles = new int[Rays * 9];
        for (int w = 0; w < Rays; w++)
        {
            int v = w * 7, i = w * 9;
            triangles[i] = v; triangles[i + 1] = v + 1; triangles[i + 2] = v + 2;
            triangles[i + 3] = v + 3; triangles[i + 4] = v + 4; triangles[i + 5] = v + 5;
            triangles[i + 6] = v + 3; triangles[i + 7] = v + 5; triangles[i + 8] = v + 6;
        }
        fan.mesh.vertices = fan.vertices;
        fan.mesh.colors = fan.colours;
        fan.mesh.triangles = triangles;
        fan.gameObject.AddComponent<MeshFilter>().sharedMesh = fan.mesh;
        var renderer = fan.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", Texture2D.whiteTexture);
        renderer.SetPropertyBlock(block);
        renderer.sortingOrder = Order;
        return fan;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    // Lays the fan from `apex` over the ground at height `groundY`, from `from` to `to`. `swept` is the share of the way
    // across already gone (the beam has passed it), `pour` how far down the rays it has reached so far. `stops` says, for
    // a ray landing at x, how far along it the light stops: 1 reaches the ground, less leaves the rest of it pale.
    // Wedges past `brightShare` of the way across are drawn at `farStrength` of `alpha`.
    public void Draw(Vector3 apex, float groundY, float from, float to, float swept, float pour, float alpha, Color light, Color pale,
        System.Func<float, float> stops, float brightShare = 1f, float farStrength = 1f)
    {
        Vector3 back = Vector3.back * 0.2f;
        int drift = Mathf.FloorToInt(Time.time * Drift);
        // One ray's ends: where the light stops, and how far the pale part behind it runs on.
        void Ray(int r, out Vector3 stop, out Vector3 end)
        {
            float x = Mathf.Lerp(from, to, Mathf.Max((float)r / Rays, swept));
            Vector3 ground = new Vector3(x, groundY, 0);
            stop = Vector3.LerpUnclamped(apex, ground, Mathf.Min(stops(x), pour)) + back;
            end = Vector3.LerpUnclamped(apex, ground, pour) + back;
        }
        Ray(0, out Vector3 stopA, out Vector3 endA);
        for (int w = 0; w < Rays; w++)
        {
            Ray(w + 1, out Vector3 stopB, out Vector3 endB);
            int v = w * 7;
            vertices[v] = apex + back;
            vertices[v + 1] = stopA;
            vertices[v + 2] = stopB;
            vertices[v + 3] = stopA;
            vertices[v + 4] = stopB;
            vertices[v + 5] = endB;
            vertices[v + 6] = endA;
            bool bright = (w + drift) / Band % 2 == 0;
            float strength = (float)w / Rays <= brightShare ? 1f : farStrength;
            Color lit = new Color(light.r, light.g, light.b, alpha * strength * (bright ? 1f : DimBand));
            Color safe = new Color(pale.r, pale.g, pale.b, alpha * PaleShare);
            for (int i = 0; i < 3; i++) colours[v + i] = lit;
            for (int i = 3; i < 7; i++) colours[v + i] = safe;
            stopA = stopB;
            endA = endB;
        }
        mesh.vertices = vertices;
        mesh.colors = colours;
        mesh.RecalculateBounds();
    }
}
