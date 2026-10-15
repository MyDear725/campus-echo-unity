using System;
using System.Linq;
using UnityEngine;

[Serializable] public class SkybridgeViewRoute
{
    public float side;
    public Vector3 sourceMapAnchor,sourceMapDirection;
    [NonSerialized] public GameObject scenery;
}

// Reuse visible exterior meshes as a rendering-only view from each independent
// interior. No duplicate physics, lights, cameras, scripts or mesh assets.
public sealed class SkybridgeCampusView:MonoBehaviour
{
    public SkybridgeViewRoute[] routes;
    CampusGame game;
    void Start(){game=FindFirstObjectByType<CampusGame>();}
    void LateUpdate(){
        if(!game||game.Interiors.Interior!=GetComponent<InteriorSceneRoot>())return;
        var p=game.enabled?game.player.position:game.followCamera.transform.position;
        foreach(var route in routes){
            bool visible=p.x*route.side>19.6f&&p.y>3.8f&&p.y<8.2f;
            if(visible&&!route.scenery)Create(route);
            if(route.scenery&&route.scenery.activeSelf!=visible)route.scenery.SetActive(visible);
        }
    }
    void Create(SkybridgeViewRoute route){
        var campus=game.gameObject.scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="开发区校区 · 校园环境");if(!campus)return;
        route.scenery=new GameObject("连廊窗外 · 校园实景模型");route.scenery.transform.SetParent(transform,false);
        var source=game.MapToWorld(route.sourceMapAnchor);
        var rotation=Quaternion.FromToRotation(game.MapToWorld(route.sourceMapDirection),Vector3.right*route.side);
        var anchor=new Vector3(route.side*20,4.2f,19.4f);
        float end=GetComponent<InteriorSceneRoot>().connections.Single(c=>Mathf.Sign(c.anchor.position.x)==route.side).anchor.position.x+route.side*.75f;
        var cut=new MaterialPropertyBlock();cut.SetVector("_ViewCutCenter",new Vector4((route.side*20+end)/2,5.9f,19.4f,1));cut.SetVector("_ViewCutSize",new Vector4(Mathf.Abs(end-route.side*20)/2+1,1.8f,1.85f,0));
        foreach(var filter in campus.GetComponentsInChildren<MeshFilter>(true)){
            var renderer=filter.GetComponent<MeshRenderer>();if(!filter.sharedMesh||!renderer||!renderer.enabled)continue;
            if(Vector3.Distance(renderer.bounds.ClosestPoint(source),source)>85)continue;
            // The walkable corridor already renders its own detailed structure.
            bool bridge=false;for(var t=filter.transform;t&&t!=campus.transform;t=t.parent)if(t.name.StartsWith("二层空中连廊")){bridge=true;break;}
            if(bridge)continue;
            var copy=new GameObject(filter.name);copy.transform.SetParent(route.scenery.transform,false);
            copy.transform.position=anchor+rotation*(filter.transform.position-source);copy.transform.rotation=rotation*filter.transform.rotation;copy.transform.localScale=filter.transform.lossyScale;
            copy.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var r=copy.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=renderer.receiveShadows;r.SetPropertyBlock(cut);
        }
    }
}
