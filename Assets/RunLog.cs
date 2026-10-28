using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// A play log for balancing (dev tools only): one line per round and boss fight, with how long it took, the hits
// taken, how the army died (the ronin's own blows or boon effects), how long it stood frozen and how close it came,
// plus the portals and boons taken and how the run ended. Appended to runlog.txt in the persistent data folder as it
// goes, so a run that dies still leaves its lines; F8 prints the current run and the file's path.
public static class RunLog
{
    public static bool Enabled;

    static readonly StringBuilder run = new StringBuilder();
    static string label;
    static bool open;
    static float startedAt;
    static int hits;
    static readonly int[] kills = new int[System.Enum.GetValues(typeof(KillSource)).Length];
    static float frozenSeconds;
    static float closest;

    public static string FilePath => Path.Combine(Application.persistentDataPath, "runlog.txt");

    public static void BeginRun(string header)
    {
        if (!Enabled) return;
        run.Clear();
        open = false;
        Line($"\n=== RUN {System.DateTime.Now:yyyy-MM-dd HH:mm}  {header}");
    }

    // A round or boss fight begins: the clock starts once the player has control.
    public static void BeginSegment(string name)
    {
        if (!Enabled) return;
        label = name;
        open = true;
        startedAt = Time.time;
        hits = 0;
        System.Array.Clear(kills, 0, kills.Length);
        frozenSeconds = 0f;
        closest = float.MaxValue;
    }

    // Each frame of play: whether the army stands frozen, and the gap between its lowest rank and the ronin.
    public static void Tick(float dt, bool frozen, float gap)
    {
        if (!Enabled || !open) return;
        if (frozen) frozenSeconds += dt;
        closest = Mathf.Min(closest, gap);
    }

    public static void Hit()
    {
        if (Enabled && open) hits++;
    }

    public static void Kill(KillSource source)
    {
        if (Enabled && open) kills[(int)source]++;
    }

    public static void EndSegment(int lives, int maxLives, string held)
    {
        if (!Enabled || !open) return;
        open = false;
        float seconds = Time.time - startedAt;
        var line = new StringBuilder($"{label,-24} {seconds,6:0.0}s  hits {hits}  lives {lives}/{maxLives}");
        int total = 0;
        foreach (int count in kills) total += count;
        if (total > 0)
        {
            int effect = kills[(int)KillSource.Effect];
            line.Append($"  kills {total}: slash {kills[(int)KillSource.Slash] + kills[(int)KillSource.Kunai]}, shadow {kills[(int)KillSource.Shadow]}, clone {kills[(int)KillSource.Clone]}, shuriken {kills[(int)KillSource.Shuriken]}, " +
                $"parry {kills[(int)KillSource.Deflected] + kills[(int)KillSource.Reflected]}, effect {effect} ({100f * effect / total:0}%)");
            line.Append($"  frozen {frozenSeconds:0.0}s ({100f * frozenSeconds / Mathf.Max(seconds, 0.01f):0}%)");
        }
        if (closest < float.MaxValue) line.Append($"  closest {closest:0.0}u");
        Line(line.ToString());
        if (!string.IsNullOrEmpty(held)) Line($"    held: {held}");
    }

    public static void Note(string text)
    {
        if (Enabled) Line($"    {text}");
    }

    public static void EndRun(string result)
    {
        if (!Enabled) return;
        open = false;
        Line($"=== END  {result}");
    }

    public static void Dump()
    {
        Debug.Log($"RunLog ({FilePath}):\n{run}");
    }

    static void Line(string text)
    {
        run.AppendLine(text);
        try
        {
            File.AppendAllText(FilePath, text + "\n");
        }
        catch (IOException)
        {
            // A log that can't be written mustn't stop the game.
        }
    }
}
