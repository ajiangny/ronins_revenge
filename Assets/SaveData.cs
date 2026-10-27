using System;
using System.Collections.Generic;
using UnityEngine;

// Everything that carries over from one run to the next: how the runs went, which lines have been heard, and the
// Soul Embers and Flute Songs. One JSON string per save slot in PlayerPrefs, which the web build keeps too.
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
    // The blade taken from the temple's weapon rack (Ronin.Weapon).
    public int weapon;

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
    // Like Hades' profiles: three journeys side by side, each its own key. The old single save becomes slot one.
    public const int Slots = 3;
    const string LegacyKey = "Save";
    const string SlotKey = "SaveSlot";
    static SaveData data;
    static int slot = -1;

    // The project enters Play mode without a domain reload, so the cached save is dropped and read fresh.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        data = null;
        slot = -1;
    }

    static string KeyFor(int index) => $"Save{index + 1}";

    // The slot in use, kept across reloads and launches so a reload lands back in the same journey.
    public static int Slot
    {
        get
        {
            if (slot < 0) slot = Mathf.Clamp(PlayerPrefs.GetInt(SlotKey, 0), 0, Slots - 1);
            return slot;
        }
    }

    public static void Use(int index)
    {
        slot = Mathf.Clamp(index, 0, Slots - 1);
        PlayerPrefs.SetInt(SlotKey, slot);
        data = null;
        Write();
    }

    public static bool Exists(int index)
    {
        MoveLegacySave();
        return PlayerPrefs.HasKey(KeyFor(index));
    }

    // A slot's save without switching to it, for the slot picker; null when it's empty.
    public static SaveData Peek(int index) => Exists(index) ? Parse(PlayerPrefs.GetString(KeyFor(index), "")) : null;

    public static void Delete(int index)
    {
        PlayerPrefs.DeleteKey(KeyFor(index));
        PlayerPrefs.Save();
        if (index == Slot) data = null;
    }

    public static SaveData Data
    {
        get
        {
            if (data != null) return data;
            MoveLegacySave();
            data = Parse(PlayerPrefs.GetString(KeyFor(Slot), ""));
            return data;
        }
    }

    static SaveData Parse(string json)
    {
        SaveData read = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        // A save from before a field grew keeps its old length.
        if (read.deathsTo.Length < 5) Array.Resize(ref read.deathsTo, 5);
        if (read.winsVs.Length < 5) Array.Resize(ref read.winsVs, 5);
        return read;
    }

    // Saves from before the slots were one key; it becomes the first slot's, once.
    static void MoveLegacySave()
    {
        if (!PlayerPrefs.HasKey(LegacyKey)) return;
        if (!PlayerPrefs.HasKey(KeyFor(0))) PlayerPrefs.SetString(KeyFor(0), PlayerPrefs.GetString(LegacyKey));
        PlayerPrefs.DeleteKey(LegacyKey);
        PlayerPrefs.Save();
    }

    // The run in progress, counted from 1.
    public static int Run => Data.runsStarted;

    public static void Write()
    {
        PlayerPrefs.SetString(KeyFor(Slot), JsonUtility.ToJson(Data));
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
