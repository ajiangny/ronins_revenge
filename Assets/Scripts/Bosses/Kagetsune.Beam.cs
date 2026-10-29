using System.Collections.Generic;
using UnityEngine;

// How the Spirit Hyper Beam is drawn: pixel art built in code every frame, never a stretched texture. The beam is a
// cylinder of soul-fire from his chest to the ground, shaded in stepped bands (dark rim, teal, pale, a white-hot core)
// with bright streaks pouring down it; strands of soul-fire coil round it on a helix, and souls ride the coil. The
// helix is worked out in 3D and projected: where a strand or soul is behind the beam it draws behind the body,
// thinner and dimmer; in front it draws over the core. Everything lands on the art's own pixel grid (48 to a unit, as
// PixelArtImporter imports the sprites), so its edges stair-step like the sprites round it.
public partial class Kagetsune
{
    // The art's pixels per unit (PixelArtImporter.PixelsPerUnit).
    const float BeamPixel = 48f;
    // Body rows are this many art pixels tall: chunky enough to read at the beam's size.
    const int BeamRow = 2;
    // A strand winds once per this many units of beam, more as the beam widens, so the coil reads evenly.
    const float CoilPitch = 2.4f;
    // How fast the coil screws down the beam (radians a second).
    const float CoilSpin = 7f;

    static readonly Color32 BeamRim = new Color32(14, 92, 86, 235);
    static readonly Color32 BeamMid = new Color32(34, 173, 152, 255);
    static readonly Color32 BeamLight = new Color32(130, 242, 214, 255);
    static readonly Color32 BeamCore = new Color32(232, 255, 248, 255);
    static readonly Color32 StrandFront = new Color32(214, 255, 244, 255);
    static readonly Color32 StrandEdge = new Color32(10, 64, 60, 255);
    static readonly Color32 StrandBack = new Color32(26, 120, 110, 255);
    static readonly Color SoulBehind = new Color(0.3f, 0.65f, 0.6f, 0.55f);

    static int Px(float world) => Mathf.RoundToInt(world * BeamPixel);
    static float Snap(float world) => Mathf.Round(world * BeamPixel) / BeamPixel;

    // A mesh of axis-aligned quads covering whole art pixels, drawn with the sprites' material in flat vertex colours.
    // Rebuilt each frame from reused lists.
    class PixelMesh
    {
        readonly GameObject shape;
        readonly Mesh mesh;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color32> colours = new List<Color32>();
        readonly List<int> triangles = new List<int>();

        public PixelMesh(Kagetsune owner, int order)
        {
            shape = new GameObject("HyperBeam");
            mesh = new Mesh();
            mesh.MarkDynamic();
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = owner.sprite.sharedMaterial;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", Texture2D.whiteTexture);
            renderer.SetPropertyBlock(block);
            renderer.sortingOrder = order;
            owner.transient.Add(shape);
        }

        public void Begin()
        {
            vertices.Clear();
            colours.Clear();
            triangles.Clear();
        }

        // Pixels x0..x1 (inclusive) on rows y..y+height-1.
        public void Run(int x0, int x1, int y, int height, Color32 colour)
        {
            if (x1 < x0) return;
            float left = x0 / BeamPixel, right = (x1 + 1) / BeamPixel, bottom = y / BeamPixel, top = (y + height) / BeamPixel;
            int at = vertices.Count;
            vertices.Add(new Vector3(left, top, 0));
            vertices.Add(new Vector3(right, top, 0));
            vertices.Add(new Vector3(right, bottom, 0));
            vertices.Add(new Vector3(left, bottom, 0));
            for (int i = 0; i < 4; i++) colours.Add(colour);
            triangles.Add(at);
            triangles.Add(at + 1);
            triangles.Add(at + 2);
            triangles.Add(at);
            triangles.Add(at + 2);
            triangles.Add(at + 3);
        }

        public void End()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
        }

        public void Remove(Kagetsune owner)
        {
            owner.Drop(shape.transform);
            Object.Destroy(mesh);
        }
    }

    // The soft light behind the beam: one trapezoid of the glow texture, its sides fading. The only part of the beam
    // not on the pixel grid, a bloom like the game's other glows.
    class HyperGlow
    {
        readonly GameObject shape;
        readonly Mesh mesh;
        readonly Vector3[] vertices = new Vector3[4];
        readonly Color[] colours = new Color[4];

        public HyperGlow(Kagetsune owner, int order)
        {
            shape = new GameObject("HyperBeam");
            mesh = new Mesh();
            mesh.MarkDynamic();
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = owner.sprite.sharedMaterial;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", ProceduralSprites.Glow.texture);
            renderer.SetPropertyBlock(block);
            renderer.sortingOrder = order;
            owner.transient.Add(shape);
            mesh.vertices = vertices;
            mesh.uv = new[] { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }

        public void Set(Vector3 top, float topHalf, Vector3 bottom, float bottomHalf, Color colour)
        {
            vertices[0] = top + Vector3.left * topHalf;
            vertices[1] = top + Vector3.right * topHalf;
            vertices[2] = bottom + Vector3.right * bottomHalf;
            vertices[3] = bottom + Vector3.left * bottomHalf;
            for (int i = 0; i < 4; i++) colours[i] = colour;
            mesh.vertices = vertices;
            mesh.colors = colours;
            mesh.RecalculateBounds();
        }

        public void Remove(Kagetsune owner)
        {
            owner.Drop(shape.transform);
            Object.Destroy(mesh);
        }
    }

    // One soul riding the coil: how far down the beam (0 chest, 1 ground), where round it, and its own pace.
    class BeamSoul
    {
        public SpriteRenderer look;
        public float along, angle, speed, frame;
        // Thrown off as the beam dies: its sideways drift.
        public float scatter;
    }

    // The whole drawn beam: glow, body, the coil's back and front, and the souls.
    class SpiritBeamView
    {
        readonly Kagetsune owner;
        readonly HyperGlow glow;
        readonly PixelMesh body, back, front;
        readonly List<BeamSoul> souls = new List<BeamSoul>();
        readonly Sprite[] soulFrames;
        readonly int strands;
        // The front strands' strokes, laid after all their outlines so one strand's edge never cuts another's stroke.
        readonly List<(int, int, int)> stroke = new List<(int, int, int)>();
        float spin;

        public SpiritBeamView(Kagetsune owner, bool host)
        {
            this.owner = owner;
            glow = new HyperGlow(owner, 53);
            back = new PixelMesh(owner, 54);
            body = new PixelMesh(owner, 55);
            front = new PixelMesh(owner, 57);
            strands = host ? 3 : 2;
            soulFrames = Fx.Frames(Fx.KageSoulRise) ?? Fx.Frames(Fx.KageSkull);
            int count = host ? 20 : 14;
            for (int i = 0; i < count; i++)
            {
                var soul = new BeamSoul
                {
                    along = i / (float)count,
                    angle = Random.value * Mathf.PI * 2f,
                    speed = Random.Range(0.35f, 0.55f),
                    frame = Random.value * 8f,
                };
                if (soulFrames != null)
                {
                    soul.look = new GameObject("BeamSoul").AddComponent<SpriteRenderer>();
                    soul.look.sprite = soulFrames[0];
                    owner.transient.Add(soul.look.gameObject);
                }
                souls.Add(soul);
            }
        }

        // One frame of the beam from `top` (half-width topHalf) to `foot` (halfFoot). `dying` 0..1 scatters the souls
        // and blinks them out.
        public void Draw(Vector3 top, float topHalf, Vector3 foot, float halfFoot, float dying = 0f)
        {
            float dt = Time.deltaTime * owner.Tempo;
            spin += CoilSpin * dt;
            glow.Set(top, topHalf * 1.6f + 0.3f, foot, halfFoot * 1.25f + 0.6f, new Color(Teal.r, Teal.g, Teal.b, 0.32f * (1f - dying)));
            DrawBody(top, topHalf, foot, halfFoot);
            DrawCoil(top, topHalf, foot, halfFoot, dying);
            DrawSouls(top, topHalf, foot, halfFoot, dt, dying);
        }

        // Rows from the chest down, each split into the stepped bands of a lit cylinder (brightness = cos of the angle
        // round it), the bands' edges shifted row by row by streaks pouring down.
        void DrawBody(Vector3 top, float topHalf, Vector3 foot, float halfFoot)
        {
            body.Begin();
            int rowTop = Px(top.y), rowFoot = Px(foot.y);
            float t = Time.time;
            for (int y = rowTop; y > rowFoot; y -= BeamRow)
            {
                float f = Mathf.InverseLerp(rowTop, rowFoot, y);
                float c = Mathf.Lerp(top.x, foot.x, f) * BeamPixel;
                // A pixel of flicker on the edges, stepped.
                float half = Mathf.Lerp(topHalf, halfFoot, f) * BeamPixel + Mathf.Round(Mathf.Sin(y * 0.37f + t * 31f));
                if (half < 0.5f) continue;
                float flow = Mathf.Round(Mathf.Sin(y * 0.11f - t * 22f) * 2f) / 2f;
                // Where each band ends, as a share of the half-width out from the middle.
                float core = 0.22f + 0.06f * flow, light = 0.5f + 0.04f * flow, mid = 0.86f;
                int y0 = y - BeamRow;
                int left = Mathf.RoundToInt(c - half), right = Mathf.RoundToInt(c + half) - 1;
                int midL = Mathf.RoundToInt(c - half * mid), midR = Mathf.RoundToInt(c + half * mid) - 1;
                int lightL = Mathf.RoundToInt(c - half * light), lightR = Mathf.RoundToInt(c + half * light) - 1;
                int coreL = Mathf.RoundToInt(c - half * core), coreR = Mathf.RoundToInt(c + half * core) - 1;
                body.Run(left, midL - 1, y0, BeamRow, BeamRim);
                body.Run(midL, lightL - 1, y0, BeamRow, BeamMid);
                body.Run(lightL, coreL - 1, y0, BeamRow, BeamLight);
                body.Run(coreL, coreR, y0, BeamRow, BeamCore);
                body.Run(coreR + 1, lightR, y0, BeamRow, BeamLight);
                body.Run(lightR + 1, midR, y0, BeamRow, BeamMid);
                body.Run(midR + 1, right, y0, BeamRow, BeamRim);
            }
            body.End();
        }

        // Each strand walks the beam a pixel row at a time on its helix: the angle winds on with the distance
        // travelled (a pitch that grows with the beam's width), the point x = centre + radius·sin, depth = cos. Behind
        // the beam it's a 1-pixel dim line under the body; in front a 3-pixel bright one outlined in dark teal over it.
        // Between rows the line fills across, so a steep stretch stays unbroken.
        void DrawCoil(Vector3 top, float topHalf, Vector3 foot, float halfFoot, float dying)
        {
            back.Begin();
            front.Begin();
            int rowTop = Px(top.y), rowFoot = Px(foot.y);
            float shrink = 1f - dying;
            for (int strand = 0; strand < strands; strand++)
            {
                float angle = strand * Mathf.PI * 2f / strands - spin;
                int lastX = int.MinValue;
                for (int y = rowTop; y > rowFoot; y--)
                {
                    float f = Mathf.InverseLerp(rowTop, rowFoot, y);
                    float radius = (Mathf.Lerp(topHalf, halfFoot, f) + 2f / BeamPixel) * shrink;
                    float centre = Mathf.Lerp(top.x, foot.x, f);
                    angle += Mathf.PI * 2f / (BeamPixel * Mathf.Max(CoilPitch, radius * 1.4f));
                    int x = Px(centre + radius * Mathf.Sin(angle));
                    bool inFront = Mathf.Cos(angle) >= 0f;
                    int from = lastX == int.MinValue ? x : lastX;
                    int x0 = Mathf.Min(from, x), x1 = Mathf.Max(from, x);
                    if (inFront)
                    {
                        // The outline first, a pixel round the stroke; the stroke over it.
                        front.Run(x0 - 1, x1 + 1, y - 3, 5, StrandEdge);
                        stroke.Add((x0, x1, y));
                    }
                    else back.Run(x0, x1, y, 1, StrandBack);
                    lastX = x;
                }
            }
            foreach ((int x0, int x1, int y) in stroke) front.Run(x0, x1, y - 2, 3, StrandFront);
            stroke.Clear();
            back.End();
            front.End();
        }

        // Souls at their own size, on the pixel grid, riding the coil down: in front of the beam they're drawn over
        // it in full; behind it, under the body and a step darker. Only flipped, never scaled or turned.
        void DrawSouls(Vector3 top, float topHalf, Vector3 foot, float halfFoot, float dt, float dying)
        {
            foreach (BeamSoul soul in souls)
            {
                soul.along += soul.speed * dt;
                soul.angle -= CoilSpin * dt;
                if (soul.along >= 1f)
                {
                    soul.along -= 1f;
                    soul.angle = Random.value * Mathf.PI * 2f;
                    if (Random.value < 0.3f) Fx.Play(Fx.Impact, foot, Teal, 0.8f);
                }
                float radius = Mathf.Lerp(topHalf, halfFoot, soul.along) + 2f / BeamPixel;
                Vector3 axis = Vector3.Lerp(top, foot, soul.along);
                soul.scatter += dying * dt * 9f * (Mathf.Sin(soul.angle) >= 0f ? 1f : -1f);
                var at = new Vector3(Snap(axis.x + radius * Mathf.Sin(soul.angle) + soul.scatter), Snap(axis.y), 0);
                bool inFront = Mathf.Cos(soul.angle) >= 0f;
                // Dying, they blink out on twos.
                bool shown = dying <= 0.4f || (Time.frameCount / 2) % 2 == 0;
                if (soul.look == null)
                {
                    if (shown && inFront) front.Run(Px(at.x) - 1, Px(at.x) + 1, Px(at.y) - 1, 3, BeamCore);
                    continue;
                }
                soul.look.enabled = shown;
                soul.look.transform.position = at;
                soul.look.sortingOrder = inFront ? 58 : 54;
                soul.look.color = inFront ? Color.white : SoulBehind;
                // Facing the way it swings round the beam.
                soul.look.flipX = Mathf.Cos(soul.angle) > 0f;
                soul.frame += dt * 12f;
                soul.look.sprite = soulFrames[(int)soul.frame % soulFrames.Length];
            }
            // The block fallback went into the front mesh after it was uploaded: upload again.
            if (soulFrames == null) front.End();
        }

        public void Remove()
        {
            glow.Remove(owner);
            body.Remove(owner);
            back.Remove(owner);
            front.Remove(owner);
            foreach (BeamSoul soul in souls) if (soul.look != null) owner.Drop(soul.look);
        }
    }
}
