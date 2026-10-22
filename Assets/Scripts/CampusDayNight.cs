using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Uses the saved game calendar rather than wall-clock time. Only exterior RenderSettings
// are changed; an additive interior keeps its authored lighting, then campus catches up on exit.
public sealed class CampusDayNight : MonoBehaviour
{
    public float Daylight { get; private set; }
    public bool LampsOn { get; private set; }
    public int LampCount => lamps.Count;
    public Light Sun => sun;
    public Material Sky => sky;
    CampusSemester owner;
    CampusGame game;
    int scene=-1;
    Light sun;
    Material sky,originalSky,poleMaterial,lensMaterial;
    Color ambientSky,ambientEquator,ambientGround,fog;
    float baseSunIntensity,baseExposure;
    readonly List<Renderer> exterior=new List<Renderer>(),fixtureRenderers=new List<Renderer>();
    readonly List<Vector3> lamps=new List<Vector3>();
    readonly List<Renderer> lenses=new List<Renderer>();
    readonly List<Light> pool=new List<Light>();
    readonly Dictionary<Material,Material> outdoorMaterials=new Dictionary<Material,Material>();
    readonly Vector4[] lampUniforms=new Vector4[8];
    Transform lampRoot;
    float poolTimer;
    bool? lastLampState;
    public static bool LightsScheduled(float hour)=>hour>=18f||hour<6f;
    public static float DaylightAt(float hour)
    {
        hour=Mathf.Repeat(hour,24);
        if(hour<5||hour>=19.5f)return 0;
        if(hour<7)return Mathf.SmoothStep(0,1,(hour-5)/2);
        if(hour<=16.5f)return 1;
        return 1-Mathf.SmoothStep(0,1,(hour-16.5f)/3);
    }
    public void Initialize(CampusSemester semester){owner=semester;}
    void LateUpdate(){ApplyNow();}
    public void ApplyNow()
    {
        if(!owner||!owner.Game)return;
        game=owner.Game;
        if(game.Interiors.Busy)return;
        // A scene reload restores the authored campus settings before this component rebinds.
        if(scene!=game.gameObject.scene.handle&&!game.Interiors.Inside)BindCampus();
        float hour=(float)owner.Clock.Now.TimeOfDay.TotalHours;
        Daylight=DaylightAt(hour);LampsOn=LightsScheduled(hour);
        Shader.SetGlobalFloat("_CampusNightAmount",1-Daylight);
        if(lastLampState!=LampsOn){UpdateFixtures();lastLampState=LampsOn;poolTimer=0;}
        if(sun)
        {
            float dusk=Mathf.Sin((1-Daylight)*Mathf.PI);
            sun.transform.rotation=Quaternion.Euler(hour*15-90,-35,0);
            sun.intensity=Mathf.Lerp(.025f,baseSunIntensity,Daylight);
            sun.color=Color.Lerp(new Color(.4f,.52f,.8f),Color.Lerp(new Color(1,.96f,.86f),new Color(1,.52f,.27f),dusk*.7f),Daylight);
        }
        if(game.Interiors.Inside&&game.Interiors.Interior&&game.followCamera)
            game.followCamera.backgroundColor=Color.Lerp(new Color(.025f,.04f,.075f),game.Interiors.Interior.backgroundColor,Daylight);
        if(!game.Interiors.Inside&&sky)
        {
            RenderSettings.sun=sun;RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.025f,.04f,.085f),ambientSky,Daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.025f,.035f,.055f),ambientEquator,Daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.018f,.022f,.03f),ambientGround,Daylight);
            RenderSettings.fogColor=Color.Lerp(new Color(.025f,.04f,.07f),fog,Daylight);
            float warmth=Mathf.Sin(Daylight*Mathf.PI);
            if(sky.HasProperty("_Exposure"))sky.SetFloat("_Exposure",Mathf.Lerp(.075f,baseExposure,Daylight));
            if(sky.HasProperty("_SkyTint"))sky.SetColor("_SkyTint",Color.Lerp(new Color(.1f,.16f,.3f),Color.Lerp(new Color(.49f,.55f,.61f),new Color(.69f,.36f,.23f),warmth*.7f),Daylight));
            if(sky.HasProperty("_GroundColor"))sky.SetColor("_GroundColor",Color.Lerp(new Color(.015f,.025f,.045f),new Color(.37f,.35f,.34f),Daylight));
        }
        poolTimer-=Time.unscaledDeltaTime;
        if(poolTimer<=0){UpdatePool();poolTimer=.35f;}
    }
    void BindCampus()
    {
        if(sky)Destroy(sky);
        if(poleMaterial)Destroy(poleMaterial);if(lensMaterial)Destroy(lensMaterial);
        exterior.Clear();fixtureRenderers.Clear();lamps.Clear();lenses.Clear();pool.Clear();
        scene=game.gameObject.scene.handle;
        var roots=game.gameObject.scene.GetRootGameObjects();
        sun=roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
        baseSunIntensity=sun?sun.intensity:1.4f;
        originalSky=RenderSettings.skybox;
        if(originalSky){sky=new Material(originalSky){name="Campus sky · runtime day and night"};baseExposure=sky.HasProperty("_Exposure")?sky.GetFloat("_Exposure"):1.1f;}
        ambientSky=RenderSettings.ambientSkyColor;ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;fog=RenderSettings.fogColor;
        var environment=roots.FirstOrDefault(r=>r.name=="开发区校区 · 校园环境");
        if(!environment)return;
        foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
        {
            exterior.Add(renderer);
            // A property block on every tree disabled SRP batching / instancing,
            // even for shaders that never read this building-only property.
            var shared=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<shared.Length;i++)
            {
                var original=shared[i];if(!original||!original.HasProperty("_CampusOutdoorSurface"))continue;
                if(!outdoorMaterials.TryGetValue(original,out var material))
                {material=new Material(original){name=original.name+" · outdoor lighting"};material.SetFloat("_CampusOutdoorSurface",1);outdoorMaterials.Add(original,material);}
                shared[i]=material;changed=true;
            }
            if(changed)renderer.sharedMaterials=shared;
            if(renderer.name.Equals("Lamp",StringComparison.OrdinalIgnoreCase)||renderer.name.IndexOf("路灯",StringComparison.Ordinal)>=0)fixtureRenderers.Add(renderer);
        }
        lampRoot=new GameObject("Campus lamps · 18:00–06:00").transform;lampRoot.SetParent(environment.transform,false);
        var shader=Resources.Load<Shader>("CampusRoute");
        poleMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));poleMaterial.color=new Color(.1f,.14f,.12f);
        lensMaterial=new Material(shader);lensMaterial.SetColor("_BaseColor",new Color(1,.78f,.42f));
        // Reuse the eight existing courtyard lamp heads, whose geometry is batched with plants.
        foreach(float x in new[]{222.7f,267.3f})foreach(float z in new[]{-113f,-98f,-82f,-72f})AddLamp(new Vector3(x,3.53f,z),false);
        // Existing lamp meshes can contain many fixtures. Add visible lamps beside actual
        // walkable paths to provide a consistent night route across the campus.
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        foreach(var road in data.roads.Where(r=>r.kind!="external"))
        {
            for(int segment=1;segment<road.points.Length;segment++)
            {
                var a=new Vector3(road.points[segment-1].x,0,road.points[segment-1].z);var b=new Vector3(road.points[segment].x,0,road.points[segment].z);
                var side=Vector3.Cross((b-a).normalized,Vector3.up)*(road.width*.5f+1);
                int count=Mathf.Max(1,Mathf.FloorToInt(Vector3.Distance(a,b)/35));
                for(int i=0;i<count;i++)
                {
                    var map=Vector3.Lerp(a,b,(i+.5f)/count)+side;
                    if(!game.IsMapWalkable(map)||lamps.Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(game.MapToWorld(map).x,game.MapToWorld(map).z))<24))continue;
                    AddLamp(new Vector3(map.x,3.6f,map.z),true);
                }
            }
        }
        // A bounded pool supplies real additional lights for URP characters; custom campus
        // shaders share the same eight positions to receive visible warm light on the ground.
        for(int i=0;i<8;i++)
        {
            var light=new GameObject("Nearby campus lamp "+i).AddComponent<Light>();light.transform.SetParent(lampRoot,false);
            light.type=LightType.Point;light.color=new Color(1,.74f,.42f);light.range=18;light.intensity=3.5f;light.shadows=LightShadows.None;light.enabled=false;pool.Add(light);
        }
        lastLampState=null;poolTimer=0;
    }
    void AddLamp(Vector3 mapHead,bool pole)
    {
        if(game.Outdoor)mapHead.y+=CampusPlanTerrain.Height(mapHead.x,mapHead.z)-.23f;
        Vector3 world=game.MapToWorld(mapHead);lamps.Add(world);
        if(pole)
        {
            var stem=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stem.name="Campus path lamp post";stem.transform.SetParent(lampRoot,false);stem.transform.position=world-Vector3.up*1.8f;stem.transform.localScale=new Vector3(.1f,1.8f,.1f);Destroy(stem.GetComponent<Collider>());stem.GetComponent<Renderer>().sharedMaterial=poleMaterial;
            var cap=GameObject.CreatePrimitive(PrimitiveType.Cube);cap.name="Campus path lamp cap";cap.transform.SetParent(lampRoot,false);cap.transform.position=world+Vector3.up*.16f;cap.transform.localScale=new Vector3(.55f,.1f,.55f);Destroy(cap.GetComponent<Collider>());cap.GetComponent<Renderer>().sharedMaterial=poleMaterial;
        }
        var bulb=GameObject.CreatePrimitive(PrimitiveType.Cube);bulb.name="Campus warm lamp lens";bulb.transform.SetParent(lampRoot,false);bulb.transform.position=world;bulb.transform.localScale=new Vector3(.3f,.14f,.3f);Destroy(bulb.GetComponent<Collider>());bulb.GetComponent<Renderer>().sharedMaterial=lensMaterial;lenses.Add(bulb.GetComponent<Renderer>());
    }
    void UpdateFixtures()
    {
        foreach(var renderer in fixtureRenderers)
        {
            if(!renderer)continue;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetFloat("_CampusLampOn",LampsOn?1:0);
            if(renderer.sharedMaterials.Any(m=>m&&m.HasProperty("_Emission")))block.SetFloat("_Emission",LampsOn?1.25f:0);
            renderer.SetPropertyBlock(block);
        }
        foreach(var lens in lenses)if(lens)lens.enabled=LampsOn;
    }
    void UpdatePool()
    {
        bool visible=LampsOn&&game&&!game.Interiors.Inside;
        var closest=visible?lamps.OrderBy(p=>(p-game.player.position).sqrMagnitude).Take(8).ToArray():Array.Empty<Vector3>();
        for(int i=0;i<pool.Count;i++)
        {
            bool active=i<closest.Length;pool[i].enabled=active;
            if(active){pool[i].transform.position=closest[i];lampUniforms[i]=new Vector4(closest[i].x,closest[i].y,closest[i].z,18);}
            else lampUniforms[i]=Vector4.zero;
        }
        Shader.SetGlobalInt("_CampusLampCount",closest.Length);Shader.SetGlobalVectorArray("_CampusLampPositions",lampUniforms);
    }
    void OnDestroy()
    {
        Shader.SetGlobalFloat("_CampusNightAmount",0);Shader.SetGlobalInt("_CampusLampCount",0);
        if(sky){if(RenderSettings.skybox==sky)RenderSettings.skybox=originalSky;Destroy(sky);}
        if(poleMaterial)Destroy(poleMaterial);if(lensMaterial)Destroy(lensMaterial);
        foreach(var material in outdoorMaterials.Values)if(material)Destroy(material);outdoorMaterials.Clear();
    }
}
