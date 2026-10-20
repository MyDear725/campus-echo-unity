using UnityEngine;

public sealed class InteriorRefinementPalette : ScriptableObject
{
    public Material floor, terrazzo, plaster, ceiling, oak, walnut, graphite, steel, brass, teal, sage, fabric, linen, light, soil, leaf, glass;
    public Font font;
    public static InteriorRefinementPalette Load()=>Resources.Load<InteriorRefinementPalette>("InteriorRefinement/Palette");
}
