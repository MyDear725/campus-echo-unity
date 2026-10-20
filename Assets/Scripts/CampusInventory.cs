using System;
using System.Collections.Generic;
using UnityEngine;

// The single inventory owned by the player, stored as integer fen-priced stacks.
//
// Vending machines already wrote into this record, so the water bar and the fruit stall
// write into it as well instead of inventing a second list. That is what makes a bought
// drink or fruit a real object: it appears in "持有" lists, can be consumed, and survives
// a restart.
[Serializable]
public class InventoryState
{
    public List<string> names = new List<string>();
    public List<int> counts = new List<int>();

    public void Add(string name, int amount = 1)
    {
        if (string.IsNullOrEmpty(name) || amount <= 0) return;
        int i = names.IndexOf(name);
        if (i >= 0) counts[i] += amount;
        else { names.Add(name); counts.Add(amount); }
    }

    public bool Consume(string name, int amount = 1)
    {
        int i = names.IndexOf(name);
        if (i < 0 || counts[i] < amount) return false;
        counts[i] -= amount;
        if (counts[i] <= 0) { names.RemoveAt(i); counts.RemoveAt(i); }
        return true;
    }

    public int CountOf(string name)
    {
        int i = names.IndexOf(name);
        return i >= 0 ? counts[i] : 0;
    }

    public int Total
    {
        get
        {
            int n = 0;
            for (int i = 0; i < counts.Count; i++) n += counts[i];
            return n;
        }
    }

    public string Summary(int maxItems = 8)
    {
        if (names.Count == 0) return "暂无";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < names.Count && i < maxItems; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(names[i]).Append("×").Append(counts[i]);
        }
        if (names.Count > maxItems) sb.Append("  …");
        return sb.ToString();
    }
}

/// <summary>
/// Process-wide access to the player's inventory. Vending machines, the water bar and the
/// fruit stall all go through here; nothing keeps its own copy of what the player owns.
/// </summary>
public static class CampusInventory
{
    public const string Key = "CampusInventory";

    public static string KeyOverride;

    static string SaveKey => string.IsNullOrEmpty(KeyOverride) ? Key : KeyOverride;

    static InventoryState cached;

    // Raised after the inventory changes so visible "持有" strips refresh.
    public static event Action Changed;

    public static InventoryState Load()
    {
        if (cached != null) return cached;

        if (PlayerPrefs.HasKey(SaveKey))
        {
            try
            {
                var s = JsonUtility.FromJson<InventoryState>(PlayerPrefs.GetString(SaveKey));
                if (s != null && s.names != null && s.counts != null && s.names.Count == s.counts.Count)
                {
                    cached = s;
                    return cached;
                }
            }
            catch (Exception) { /* corrupt save: start clean below */ }
        }

        cached = new InventoryState();
        return cached;
    }

    public static void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Load()));
        PlayerPrefs.Save();
        RaiseChanged();
    }

    public static void Add(string name, int amount = 1)
    {
        Load().Add(name, amount);
        Save();
    }

    public static bool Consume(string name, int amount = 1)
    {
        if (!Load().Consume(name, amount)) return false;
        Save();
        return true;
    }

    public static int CountOf(string name) { return Load().CountOf(name); }

    public static string Summary() { return Load().Summary(); }

    // A transaction that both holds the goods and charges for them, in one step at the
    // caller. Kept here so a future shop cannot forget one half of it.
    public static void Grant(string name, int amount) { Add(name, amount); }

    public static void ResetCache() { cached = null; }

    public static void SetForTest(InventoryState state)
    {
        cached = state ?? new InventoryState();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(cached));
        PlayerPrefs.Save();
        RaiseChanged();
    }

    static void RaiseChanged() { var h = Changed; if (h != null) h(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() { ResetCache(); }
}
