using System;
using UnityEngine;

// The single living-allowance (生活费) balance for the whole game.
//
// Every system — cafeteria counters, water bar, fruit stall, library vending machines,
// the phone and the corner HUD — reads and writes this one value. Money is stored as
// integer fen (1 yuan = 100 fen) so it can never drift, and the yen text is made only
// for display.
//
// Persistence: the balance is written to its own PlayerPrefs key the moment it changes.
// It is initialised exactly once, on a profile that has never stored a balance, and is
// never reset by entering a building, changing floors, opening a menu or restarting.
public static class CampusWallet
{
    public const string Key = "CampusWalletBalanceFen";
    public const long DefaultStartingFen = 30000;

    // Tests point this at a throwaway key so real player money is never touched.
    public static string KeyOverride;

    static string SaveKey => string.IsNullOrEmpty(KeyOverride) ? Key : KeyOverride;

    static long cached = -1;
    static bool loaded;

    // Raised after any change (and after a first-time initialisation) so every visible
    // balance readout can refresh in the same frame.
    public static event Action Changed;

    public static long BalanceFen
    {
        get
        {
            EnsureLoaded();
            return cached;
        }
    }

    public static string BalanceText => "生活费：" + FormatYuan(BalanceFen);

    // ¥12.34 — always two decimals, independent of the current locale.
    public static string FormatYuan(long fen)
    {
        long abs = Math.Abs(fen);
        return "¥" + (abs / 100) + "." + (abs % 100).ToString("00");
    }

    public static string FormatYuan(int fen) => FormatYuan((long)fen);

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        if (PlayerPrefs.HasKey(SaveKey))
        {
            long v;
            if (long.TryParse(PlayerPrefs.GetString(SaveKey, ""), out v))
            {
                cached = Math.Max(0L, v);
                return;
            }
        }

        // First run with the shared wallet: carry over whatever the cafeteria saved.
        cached = AdoptLegacyBalance();
        PlayerPrefs.SetString(SaveKey, cached.ToString());
        PlayerPrefs.Save();
    }

    // Older builds kept the balance inside the cafeteria JSON snapshot under "bal".
    static long AdoptLegacyBalance()
    {
        string key = CafeteriaOrdering.PrefsKeyOverride ?? CafeteriaOrdering.PrefsKey;
        if (!PlayerPrefs.HasKey(key)) return DefaultStartingFen;

        string json = PlayerPrefs.GetString(key);
        int idx = json.IndexOf("\"bal\":", StringComparison.Ordinal);
        if (idx < 0) return DefaultStartingFen;

        int start = idx + 6;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
        if (end <= start) return DefaultStartingFen;

        long v;
        return long.TryParse(json.Substring(start, end - start), out v) ? Math.Max(0L, v) : DefaultStartingFen;
    }

    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.SetString(SaveKey, cached.ToString());
        PlayerPrefs.Save();
    }

    // Deducts only when the full amount is available. Returns false and changes nothing
    // otherwise, so a caller can treat a failed charge as "transaction aborted"; the
    // balance can never go negative.
    public static bool TrySpend(long fen)
    {
        EnsureLoaded();
        if (fen <= 0) return true;
        if (fen > cached) return false;
        cached -= fen;
        Save();
        RaiseChanged();
        return true;
    }

    // How much is missing for the given price; 0 when affordable.
    public static long ShortfallFen(long costFen)
    {
        EnsureLoaded();
        return Math.Max(0L, costFen - cached);
    }

    // "还差 ¥12.00" — the phrasing every shop uses when money is not enough.
    public static string ShortfallText(long costFen)
    {
        long shortfall = ShortfallFen(costFen);
        if (shortfall <= 0) return "";
        return "生活费不足，还差 " + FormatYuan(shortfall);
    }

    public static void SetForTest(long fen)
    {
        loaded = true;
        cached = Math.Max(0L, fen);
        Save();
        RaiseChanged();
    }

    // Drops the in-memory copy so the next read goes back to the saved value.
    public static void ResetCache() { loaded = false; cached = -1; }

    static void RaiseChanged() { var h = Changed; if (h != null) h(); }

    // Ensures a fresh session reads the saved value rather than a stale static.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() { ResetCache(); }
}
