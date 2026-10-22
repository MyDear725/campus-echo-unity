using UnityEngine;

// Marks an ordering anchor as belonging to one shop, so walking to the water bar opens
// the water bar menu rather than the restaurant menu.
public class CafeteriaCounter : MonoBehaviour
{
    public CafeteriaShop shop = CafeteriaShop.Restaurant;
}
