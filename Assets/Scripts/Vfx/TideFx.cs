using System.Collections.Generic;
using UnityEngine;

// Asagi's waves, drawn from the TideWave clip at its native size and never stretched: Riptide's column wave is a run
// of crests racing up the column, and the great wave (Undertow Riposte, Tide Shadow) is rows of crests side by side
// across the whole field, each row breaking a moment after the one below it. Both return false when the clip is
// missing, so the caller keeps its ElementFx.Wave.
public class TideFx : MonoBehaviour
{
    // Up a column: a crest every ColumnStep units, each StepDelay seconds after the one below it.
    const float ColumnStep = 1.1f;
    const float StepDelay = 0.045f;
    // Across the field: crests AcrossStep apart (a crest is about 2.2 units wide, so they overlap a little), in up to
    // GreatRows rows, each RowDelay after the last, every other row shifted half a crest so the seams don't line up.
    const float AcrossStep = 1.9f;
    const int GreatRows = 4;
    const float RowDelay = 0.09f;
    // A little scatter in when each crest of a row breaks, so the row doesn't land as one stamp.
    const float RowJitter = 0.04f;

    readonly List<(float at, Vector3 position)> queue = new List<(float, Vector3)>();
    float age;

    public static bool Column(Vector3 from, Vector3 to)
    {
        if (!Fx.Has(Fx.TideWave)) return false;
        TideFx run = Runner();
        int count = 1 + Mathf.CeilToInt(Mathf.Max(0f, to.y - from.y) / ColumnStep);
        for (int i = 0; i < count; i++) run.queue.Add((i * StepDelay, new Vector3(from.x, from.y + i * ColumnStep, 0f)));
        return true;
    }

    public static bool Great(Vector3 from, Vector3 to)
    {
        if (!Fx.Has(Fx.TideWave)) return false;
        TideFx run = Runner();
        Camera cam = Camera.main;
        float centre = CameraShake.Center.x;
        float halfWidth = cam != null ? cam.orthographicSize * cam.aspect : 9f;
        int across = Mathf.CeilToInt(2f * halfWidth / AcrossStep) + 1;
        for (int row = 0; row < GreatRows; row++)
        {
            float y = Mathf.Lerp(from.y, Mathf.Max(from.y, to.y), row / (float)(GreatRows - 1));
            float shift = row % 2 == 1 ? AcrossStep * 0.5f : 0f;
            for (int i = 0; i < across; i++)
            {
                float x = centre - halfWidth + shift + i * AcrossStep;
                run.queue.Add((row * RowDelay + Random.Range(0f, RowJitter), new Vector3(x, y, 0f)));
            }
        }
        return true;
    }

    static TideFx Runner() => new GameObject("TideFx").AddComponent<TideFx>();

    void Update()
    {
        age += Time.deltaTime;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].at > age) continue;
            Fx.Play(Fx.TideWave, queue[i].position, Color.white, 1f, null, Fx.UnderArrows);
            queue.RemoveAt(i);
        }
        if (queue.Count == 0) Destroy(gameObject);
    }
}
