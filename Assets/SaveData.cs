using System;
using System.Collections.Generic;
using UnityEngine;

// Everything that carries over from one run to the next: how the runs went, which lines have been heard, and the
// Soul Embers and Flute Songs. One JSON string in PlayerPrefs, which the web build keeps too.
[Serializable]
public class SaveData
{
    public int runsStarted;
    public int runsCleared;
    public int deaths;
    // Indexed by BossKind; None counts the army.
    public int[] deathsTo = new int[5];
    public int[] winsVs = new int[5];
    // How the last finished run ended, and what killed him (BossKind; None is the army).
    public bool lastRunCleared;
    public int lastKiller = -1;
    // A death or a win not yet answered on the hill.
    public bool hubPending;

    // Conversations heard, and the run each was last heard in (parallel lists: JsonUtility has no dictionaries).
    public List<string> seen = new List<string>();
    public List<int> seenRun = new List<int>();
    // Repeatables already played since their bag was last emptied.
    public List<string> shuffleBag = new List<string>();

    public int embers;
    // Indexed by UpgradeId: ranks bought, and the maxed songs switched off.
    public int[] ranks = new int[0];
    public List<int> songsOff = new List<int>();

    public int SeenIn(string id)
    {
        int i = seen.IndexOf(id);
        return i < 0 ? -1 : seenRun[i];
    }

    public void MarkSeen(string id, int run)
    {
        int i = seen.IndexOf(id);
        if (i < 0)
        {
            seen.Add(id);
            seenRun.Add(run);
        }
        else seenRun[i] = run;
    }
}

public static class Save
{
    const string Key = "Save";
    static SaveData data;

    // The project enters Play mode without a domain reload, so the cached save is dropped and read fresh.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() => data = null;

    public static SaveData Data
    {
        get
        {
            if (data != null) return data;
            string json = PlayerPrefs.GetString(Key, "");
            data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            // A save from before a field grew keeps its old length.
            if (data.deathsTo.Length < 5) Array.Resize(ref data.deathsTo, 5);
            if (data.winsVs.Length < 5) Array.Resize(ref data.winsVs, 5);
            return data;
        }
    }

    // The run in progress, counted from 1.
    public static int Run => Data.runsStarted;

    public static void Write()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    public static void BeginRun()
    {
        Data.runsStarted++;
        Write();
    }

    // A finished run: a clear, or a death and what caused it. Restarting or quitting mid-run records nothing.
    public static void EndRun(bool cleared, BossKind killer)
    {
        SaveData d = Data;
        d.lastRunCleared = cleared;
        d.lastKiller = cleared ? -1 : (int)killer;
        if (cleared) d.runsCleared++;
        else
        {
            d.deaths++;
            d.deathsTo[(int)killer]++;
        }
        d.hubPending = true;
        Write();
    }

    public static void BossFelled(BossKind boss)
    {
        Data.winsVs[(int)boss]++;
        Write();
    }

    public static void Reset()
    {
        data = new SaveData();
        Write();
    }
}
