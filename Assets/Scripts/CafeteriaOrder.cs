using System;
using System.Collections.Generic;
using UnityEngine;

// One placed order. Several can exist at once: one per counter, each with its own
// seller, floor, timer, pickup point and progress. Money was already charged when the
// order was created, and the items/prices are a snapshot taken at that moment, so a
// later menu change can never alter an existing order.
[Serializable]
public class CafeteriaOrder
{
    public int id;
    public int shop;                         // (int)CafeteriaShop — stable seller id
    public int floor;                        // 1..3, frozen at purchase time
    public string shopName;                  // snapshot, survives a shop table change
    public List<string> items = new List<string>();
    public List<int> quantities = new List<int>();
    public List<int> unitFen = new List<int>();
    public List<string> units = new List<string>();
    public int totalFen;                     // snapshot of what was actually paid
    public string placedDate;                // game date the menu came from
    public int state;                        // CafeteriaOrderState
    public long readyTicks;                  // wall-clock preparation deadline (meals)
    public float eatProgress;                // 0..1
    public string pickupAnchor;              // scene anchor id where it is collected
    public bool collected;                   // handed to the player already
    public int feedbackSeq;                  // increments when a transition should be announced

    public CafeteriaShop Shop => (CafeteriaShop)shop;
    public CafeteriaShopDef Def => CafeteriaShops.Get(Shop);
    public CafeteriaBusiness Business => Def != null ? Def.business : CafeteriaBusiness.Meal;

    public bool NeedsTrayReturn => Business == CafeteriaBusiness.Meal;

    public bool IsActive =>
        state != (int)CafeteriaOrderState.None && state != (int)CafeteriaOrderState.Done &&
        state != (int)CafeteriaOrderState.TrayToReturn;

    public string FloorShopLabel => floor + "F " + (Def != null ? Def.shortName : shopName);

    public string ItemsSummary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append("、");
                sb.Append(items[i]);
                if (i < units.Count && !string.IsNullOrEmpty(units[i])) sb.Append("／").Append(units[i]);
                if (quantities[i] > 1) sb.Append("×").Append(quantities[i]);
            }
            return sb.ToString();
        }
    }
}
