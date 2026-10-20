#ifndef CAMPUS_NIGHT_LIGHTING_INCLUDED
#define CAMPUS_NIGHT_LIGHTING_INCLUDED
float _CampusNightAmount;
int _CampusLampCount;
float4 _CampusLampPositions[8];
float3 CampusWarmLampLight(float3 world,float3 normal)
{
    float3 total=0;
    [loop] for(int i=0;i<min(_CampusLampCount,8);i++)
    {
        float3 delta=_CampusLampPositions[i].xyz-world;
        float distanceSquared=max(dot(delta,delta),.04);
        float fade=saturate(1-sqrt(distanceSquared)/_CampusLampPositions[i].w);
        float attenuation=fade*fade*2.2;
        total+=float3(1,.68,.32)*attenuation*(.2+.8*saturate(dot(normal,normalize(delta))));
    }
    return total;
}
float3 CampusOutdoorLight(float3 lit,float3 albedo,float3 normal,float3 world,float enabled)
{
    return lit*lerp(1,.16,saturate(_CampusNightAmount*enabled))+albedo*CampusWarmLampLight(world,normal)*enabled;
}
#endif
