using UnityEngine;

// A design-scale terrain profile, not surveyed elevations. Map north is -Z.
public static class CampusPlanTerrain
{
    public static float West(float z)=>200-(z+150)*.2f;
    public static bool InLiving(float x,float z)=>z>=-150&&z<=95&&x>=West(z)&&x<=303;
    static float Blend(float x,float z,float x0,float x1,float z0,float z1,float feather){
        float d=Mathf.Max(Mathf.Max(x0-x,x-x1),Mathf.Max(z0-z,z-z1));return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(d/feather));
    }
    public static float Height(float x,float z){
        // Confirmed entrance levels take priority: south 1F is 2.85 m above north LG.
        // The same continuous grade crosses the west/east roads and the dorm site.
        float s=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-55,-31,z));
        float h=.23f+2.85f*s;
        float edge=Mathf.Min(x-West(z),303-x, z+150,95-z);
        return Mathf.Lerp(.23f,h,Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/8)));
    }
}

