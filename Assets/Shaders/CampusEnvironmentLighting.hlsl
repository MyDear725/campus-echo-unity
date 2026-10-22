#ifndef CAMPUS_ENVIRONMENT_LIGHTING_INCLUDED
#define CAMPUS_ENVIRONMENT_LIGHTING_INCLUDED

// World-space detail keeps physical scale consistent across generated mesh batches.
float CampusHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}
float CampusNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(CampusHash(cell), CampusHash(cell + float2(1, 0)), f.x),
                lerp(CampusHash(cell + float2(0, 1)), CampusHash(cell + 1), f.x), f.y);
}
half3 CampusEnvironmentAmbient(float3 normal)
{
    // Cool skylight, restrained earth bounce; retain a readable shadow side.
    half3 hemisphere = lerp(half3(.12, .135, .115), half3(.31, .37, .43), normal.y * .5 + .5);
    return max(SampleSH(normal) * .72, hemisphere);
}
half3 CampusGrassColor(half3 color, float2 world)
{
    float broad = CampusNoise(world * .23);
    float clumps = CampusNoise(world * 3.7);
    float fade = 1 - saturate(length(fwidth(world)) * 18);
    float blades = CampusNoise(world * float2(55, 105));
    half3 tint = lerp(half3(.86, .96, .80), half3(1.06, 1.03, .91), broad);
    return color * tint * (.88 + clumps * .19 + (blades - .5) * .16 * fade);
}
half3 CampusEnvironmentFog(half3 color, float3 world)
{
    // Evaluate at the fragment: large terrain triangles cannot carry vertex fog.
    float d = distance(GetCameraPositionWS(), world);
    float amount = 1 - exp(-d * d * .00000144);
    return lerp(color, unity_FogColor.rgb, saturate(amount));
}
#endif
