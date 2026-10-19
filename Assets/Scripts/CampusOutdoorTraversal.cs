using UnityEngine;

// A separate exterior capsule preserves the existing interior controller contract.
// Only authored outdoor collision layers 9/10 participate; camera-only walls do not.
public sealed class CampusOutdoorTraversal : MonoBehaviour
{
    CampusGame game;CharacterController body;
    public void Initialize(){
        if(body)return;game=GetComponent<CampusGame>();
        var go=new GameObject("Outdoor walking capsule");go.layer=2;go.transform.SetParent(transform,false);go.transform.position=game.player.position;
        body=go.AddComponent<CharacterController>();body.height=1.8f;body.radius=.32f;body.center=new Vector3(0,.9f,0);body.stepOffset=.3f;body.skinWidth=.025f;body.slopeLimit=48;body.minMoveDistance=0;
        body.excludeLayers=~((1<<9)|(1<<10));
        game.Interiors.Entered+=_=>body.enabled=false;
    }
    public CollisionFlags Move(Vector3 horizontal,float dt,ref float vertical,bool jump){
        Initialize();if(game.Interiors.Inside||game.Interiors.Busy){body.enabled=false;return CollisionFlags.None;}
        if(!body.enabled||Vector3.Distance(body.transform.position,game.player.position)>.2f){body.enabled=false;body.transform.position=game.player.position;body.enabled=true;Physics.SyncTransforms();}
        if(jump)vertical=6.5f;
        int count=Mathf.Max(1,Mathf.CeilToInt(horizontal.magnitude/.13f));var flags=CollisionFlags.None;
        for(int i=0;i<count;i++){float step=dt/count;vertical-=18*step;var hit=body.Move(horizontal/count+Vector3.up*vertical*step);flags|=hit;if((hit&CollisionFlags.Below)!=0)vertical=-2;if((hit&CollisionFlags.Above)!=0&&vertical>0)vertical=0;}
        game.player.position=body.transform.position;
        if(game.player.position.y< -15){body.enabled=false;var p=game.WorldToMap(game.player.position);p.y=CampusPlanTerrain.Height(p.x,p.z)+.1f;game.player.position=game.MapToWorld(p);vertical=0;}
        return flags;
    }
}
