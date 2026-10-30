using System;
using System.Collections.Generic;
using UnityEngine;

// What struck the last blow of a run, so the dead army's deaths can be told apart (Poguru's roasts). Boss deaths are
// all Boss; which boss is in SaveData.lastKiller. Appended to only: the save stores them as ints.
public enum DeathCause { None, Boss, Arrow, Spear, GhostArrow, Diver, Invasion }

// Everything that carries over from one run to the next: how the runs went, which lines have been heard, and the
// Spirit Notes and Flute Songs. One JSON string per save slot in PlayerPrefs, which the web build keeps too.
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
    // Whether that boss was in its second phase when it killed him (Utsusemi's rocks only fall then).
    public bool lastKillerSecondPhase;
    // The blow that ended it (DeathCause), deaths counted per cause, and how many runs in a row ended the same way.
    public int lastDeathCause;
    public int[] deathsByCause = new int[DeathCauses];
    public int deathStreak;
    // The element he held most when the last run ended (Element), or -1: whose soul speaks in the temple.
    public int lastRunElement = -1;
    // Whether Oni has met Asagi, the koi of the temple pond: until then no Water soul is drawn.
    public bool asagiMet;
    // Her first Water soul, owed from the meeting until its boons are offered: every soul is hers until then.
    public bool asagiSoulOwed;
    public const int DeathCauses = 7;
    // A death or a win not yet answered on the hill.
    public bool hubPending;

    // Conversations heard, and the run each was last heard in (parallel lists: JsonUtility has no dictionaries).
    public List<string> seen = new List<string>();
    public List<int> seenRun = new List<int>();
    // Repeatables already played since their bag was last emptied.
    public List<string> shuffleBag = new List<string>();
    // Firsts: each thing obtained or met for the first time (Firsts keys), and the run it happened in, so the cast
    // can bring it up while it's still news. Run 0 is a first worked out from an older save.
    public List<string> firsts = new List<string>();
    public List<int> firstsRun = new List<int>();

    // The Spirit Notes held. Still named for the old Soul Embers: JsonUtility matches fields by name, so renaming it
    // would empty every existing save.
    public int embers;
    // Indexed by UpgradeId: ranks bought, and the maxed songs switched off.
    public int[] ranks = new int[0];
    public List<int> songsOff = new List<int>();
    // Indexed by Relic: the Warlord's Seals and Kite Silk held, and those spent on songs (a refund returns them).
    public int[] relics = new int[0];
    public int[] relicsSpent = new int[0];
    // The weapon rack's pick (Ronin.Blade): 0 the katana, 1 its kunai skin.
    public int weapon;
    // The ultimate taken from the rack beside it (Ronin.Ultimate).
    public int ultimate;
    // Indexed by OathId: the ranks sworn at the oath stone; the highest Heat a dawn was reached at; and whether the
    // stone's chains have fallen (after the first dawn).
    public int[] oaths = new int[0];
    public int bestHeatCleared;
    public bool oathsUnsealed;
    // Poguru's tutorial: settled once it's been finished or skipped, so it isn't offered again; replay asks for it
    // on the next run (set at his nest, and while a tutorial is under way, so a death in it offers it again).
    public bool tutorialSettled;
    public bool tutorialReplay;
    // Poguru's mid-fight warnings: the last run he said any (0 for never) and how many runs until he says them again.
    public int poguruWarnRun;
    public int poguruWarnGap;

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

    public int FirstIn(string key)
    {
        int i = firsts.IndexOf(key);
        return i < 0 ? -1 : firstsRun[i];
    }

    // Records a first; false when it was already had.
    public bool AddFirst(string key, int run)
    {
        if (firsts.Contains(key)) return false;
        firsts.Add(key);
        firstsRun.Add(run);
        return true;
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
        if (read.deathsByCause == null || read.deathsByCause.Length < SaveData.DeathCauses) Array.Resize(ref read.deathsByCause, SaveData.DeathCauses);
        if (read.firsts == null || read.firstsRun == null || read.firsts.Count != read.firstsRun.Count)
        {
            read.firsts = new List<string>();
            read.firstsRun = new List<int>();
        }
        Backfill(read);
        return read;
    }

    // What an older save already shows was had or met, recorded at run 0 so nobody greets it as news.
    static void Backfill(SaveData d)
    {
        for (int b = 1; b < d.deathsTo.Length; b++)
        {
            if (d.deathsTo[b] + d.winsVs[b] > 0 || d.seen.Exists(id => id.StartsWith((BossKind)b + "_")))
                d.AddFirst(Firsts.Boss((BossKind)b), 0);
        }
        if (d.seen.Contains("Akagane_Meet_Angry")) d.AddFirst(Firsts.Soul(Element.Fire), 0);
        if (d.seen.Contains("Mizore_Meet_Steady")) d.AddFirst(Firsts.Soul(Element.Water), 0);
        if (d.seen.Contains("Hibiki_Meet_Out")) d.AddFirst(Firsts.Soul(Element.Lightning), 0);
        if (d.seen.Contains("Asagi_Meet_Fry")) d.AddFirst(Firsts.Soul(Element.Tide), 0);
        if (d.asagiMet) d.AddFirst(Firsts.Asagi, 0);
        if (d.seen.Contains("Poguru_Scout_First")) d.AddFirst(Firsts.Scout, 0);
        if (d.seen.Contains("Poguru_Stage_Pass")) d.AddFirst(Firsts.Enemy(SamuraiKind.Shieldbearer), 0);
        if (d.seen.Contains("Poguru_Stage_Divers")) d.AddFirst(Firsts.Divers, 0);
        if (d.seen.Contains("Poguru_Stage_Shrine")) d.AddFirst(Firsts.Enemy(SamuraiKind.Yurei), 0);
        if (d.seen.Contains("Poguru_Stage_Mountain")) d.AddFirst(Firsts.Enemy(SamuraiKind.Bellringer), 0);
        int[] held = d.relics ?? new int[0], spent = d.relicsSpent ?? new int[0];
        foreach (Relic relic in (Relic[])Enum.GetValues(typeof(Relic)))
        {
            int r = (int)relic;
            if ((r < held.Length ? held[r] : 0) + (r < spent.Length ? spent[r] : 0) > 0) d.AddFirst(Firsts.Relic(relic), 0);
        }
        if (d.ranks != null && Array.Exists(d.ranks, rank => rank > 0)) d.AddFirst(Firsts.SongBought, 0);
        if (d.oathsUnsealed) d.AddFirst(Firsts.OathsUnsealed, 0);
        if (d.oaths != null && Array.Exists(d.oaths, rank => rank > 0)) d.AddFirst(Firsts.OathSworn, 0);
        if (d.weapon == 1) d.AddFirst(Firsts.KunaiSkin, 0);
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
    public static void EndRun(bool cleared, BossKind killer, DeathCause cause = DeathCause.None, Element? mostHeld = null, bool secondPhase = false)
    {
        SaveData d = Data;
        d.lastRunElement = mostHeld is Element element ? (int)element : -1;
        // A boss's blow is the boss's, whatever it threw.
        if (!cleared && killer != BossKind.None) cause = DeathCause.Boss;
        bool same = !cleared && !d.lastRunCleared && d.lastDeathCause == (int)cause && d.lastKiller == (int)killer;
        d.lastRunCleared = cleared;
        d.lastKiller = cleared ? -1 : (int)killer;
        d.lastKillerSecondPhase = !cleared && killer != BossKind.None && secondPhase;
        d.lastDeathCause = cleared ? 0 : (int)cause;
        d.deathStreak = cleared ? 0 : same ? d.deathStreak + 1 : 1;
        if (cleared) d.runsCleared++;
        else
        {
            d.deaths++;
            d.deathsTo[(int)killer]++;
            d.deathsByCause[(int)cause]++;
        }
        d.hubPending = true;
        Write();
    }

    // Records a first in this run; true only the first time ever, so the caller knows it's news.
    public static bool First(string key)
    {
        if (!Data.AddFirst(key, Run)) return false;
        Write();
        return true;
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
