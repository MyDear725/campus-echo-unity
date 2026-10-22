using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class TeachingSkybridgeBuilder
{
    [MenuItem("Campus/Interiors/Generate Teaching Skybridges")]
    public static void Generate(){
        TeachingBuilder.Generate();
        foreach(string letter in new[]{"A","B","C"})TeachingCInteriorBuilder.GenerateBuilding(letter);
        var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        if(!game.GetComponent<TeachingSkybridgeQA>())game.gameObject.AddComponent<TeachingSkybridgeQA>();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
    }
    public static void GenerateAndBuild(){Generate();CampusBuilder.Build();}
    public static void RebuildExteriorAndBuild(){TeachingBuilder.Generate();CampusBuilder.Build();}
    public static void Exterior(Transform parent,CampusGame game){
        const string folder="Assets/TeachingRevision/Skybridges";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var root=new GameObject("二层空中连廊 · B—A—C").transform;root.SetParent(parent,false);
        // The visual campus uses mirrored X. Physics boxes must instead live
        // under the existing unscaled, world-space camera collision root.
        var cameraRoot=GameObject.Find("Camera collision surfaces").transform;
        var oldCollision=cameraRoot.Find("Teaching skybridge collisions");if(oldCollision)UnityEngine.Object.DestroyImmediate(oldCollision.gameObject);
        var collisionRoot=new GameObject("Teaching skybridge collisions").transform;collisionRoot.SetParent(cameraRoot,false);
        var geometry=new Dictionary<string,DormitoryBuilder.Geometry>();
        foreach(string material in new[]{"Floor","Metal","Glass","Wood","Lamp","Limestone","Trim"})geometry[material]=new DormitoryBuilder.Geometry();
        void Bridge(string name,Vector3 a,Vector3 b){
            var u=(b-a).normalized;var n=Vector3.Cross(u,Vector3.up);float length=Vector3.Distance(a,b);var center=(a+b)*.5f;
            void Box(string material,float along,float h,float side,float w,float height,float depth){geometry[material].Box(center+u*along+Vector3.up*h+n*side,u,Vector3.up,n,new Vector3(w,height,depth));}
            Box("Floor",0,4.69f,0,length,.32f,3.8f);Box("Metal",0,8.2f,0,length+.3f,.16f,4.02f);
            Box("Trim",0,8.3f,0,length+.22f,.035f,3.98f);Box("Wood",0,8.06f,0,length,.06f,3.8f);
            foreach(float s in new[]{-1.96f,1.96f}){Box("Trim",0,8.18f,s,length+.25f,.055f,.025f);Box("Metal",0,4.56f,s*.95f,length,.12f,.16f);}
            for(float t=-length/2+.1f;t<length/2;t+=1.15f)Box("Metal",t,8.33f,0,.025f,.04f,3.92f);
            for(float s=-1.7f;s<1.8f;s+=.14f)Box("Wood",0,8.01f,s,length,.05f,.075f);
            foreach(float side in new[]{-1.85f,1.85f}){
                Box("Metal",0,4.94f,side,length,.18f,.14f);Box("Trim",0,6,side,length,.045f,.045f);Box("Metal",0,7.98f,side,length,.12f,.12f);
                int bays=Mathf.CeilToInt(length/2.65f);float bay=length/bays;
                for(int i=0;i<bays;i++){float t=-length/2+(i+.5f)*bay;var p=center+u*t+n*side+Vector3.up*6.45f;geometry["Glass"].Quad(p-u*(bay-.09f)/2-Vector3.up*1.47f,p+u*(bay-.09f)/2-Vector3.up*1.47f,p+u*(bay-.09f)/2+Vector3.up*1.47f,p-u*(bay-.09f)/2+Vector3.up*1.47f);}
                for(int i=0;i<=bays;i++){float t=-length/2+length*i/bays;Box("Metal",t,6.48f,side,.12f,3.2f,.16f);
                    Box("Trim",t,6.48f,side+Mathf.Sign(side)*.09f,.032f,3.1f,.025f);}
            }
            foreach(float s in new[]{-1.38f,1.38f}){Box("Metal",0,7.99f,s,length-.6f,.035f,.1f);Box("Lamp",0,7.965f,s,length-.65f,.018f,.045f);}
            foreach(float t in new[]{-length/2+.25f,length/2-.25f}){
                foreach(float s in new[]{-2f,2f}){Box("Limestone",t,6.5f,s,.5f,3.6f,.35f);Box("Metal",t,6.5f,s,.055f,3.5f,.37f);}
                Box("Metal",t,8.2f,0,.5f,.2f,4.3f);Box("Trim",t,4.83f,0,.14f,.04f,3.55f);
            }
            // Elevated camera collision only: no ground-level obstacle across the courtyard.
            var collision=new GameObject(name+" · 高架结构");collision.transform.SetParent(collisionRoot,false);collision.layer=8;
            collision.transform.position=game.MapToWorld(center);collision.transform.rotation=Quaternion.LookRotation(game.MapToWorld(n),Vector3.up);
            void Solid(Vector3 p,Vector3 size){var c=collision.AddComponent<BoxCollider>();c.center=p;c.size=size;}
            Solid(new Vector3(0,4.69f,0),new Vector3(length,.32f,3.8f));Solid(new Vector3(0,8.2f,0),new Vector3(length,.22f,4.15f));
            foreach(float side in new[]{-1.85f,1.85f})Solid(new Vector3(0,6.45f,side),new Vector3(length,3,.12f));
        }
        Bridge("A ↔ B",new Vector3(32,0,-113.4f),new Vector3(32,0,-105.1f));
        Bridge("A ↔ C",new Vector3(88,0,-118.2f),new Vector3(88,0,-108.4f));
        foreach(var pair in geometry){
            string path=folder+"/"+pair.Key+".asset";var mesh=pair.Value.Mesh("Skybridge "+pair.Key);var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            var obj=new GameObject(pair.Key);obj.transform.SetParent(root,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=BridgeMaterial(pair.Key,folder);r.shadowCastingMode=pair.Key=="Glass"||pair.Key=="Lamp"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
    }
    public static Material BridgeMaterial(string key,string folder){
        string color=key=="Metal"?"#48545a":key=="Trim"?"#9b9c94":key=="Floor"?"#b4b6b1":key=="Wood"?"#b49a76":key=="Lamp"?"#fff0d7":"#d8d4c9";
        ColorUtility.TryParseHtmlString(color,out var tint);
        var m=new Material(Shader.Find(key=="Glass"?"Universal Render Pipeline/Lit":"Campus/TeachingInterior")){name="Skybridge "+key};m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",key=="Metal"||key=="Trim"?.55f:.27f);
        if(key=="Metal"||key=="Trim")m.SetFloat("_Metallic",.45f);
        if(key=="Wood")m.SetFloat("_Wood",1);if(key=="Lamp")m.SetFloat("_Emission",1.15f);
        if(key=="Glass"){
            m.SetColor("_BaseColor",new Color(.8f,.9f,.94f,.14f));m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.SetFloat("_Smoothness",.88f);m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);
        }
        string path=folder+"/Bridge-"+key+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(m,path);return m;
    }
}

public static partial class TeachingCInteriorBuilder
{
    // Open only the 2F cross-corridor end bays. Keep all other floors sealed.
    static bool BridgeSide(float x)=>floor==1&&(Building=="A"||Building=="B"&&x>0||Building=="C"&&x<0);
    static void BridgeSideWall(float x){
        foreach(var range in new[]{new Vector2(-12.1f,17.9f),new Vector2(20.9f,30.1f)}){
            float z=(range.x+range.y)/2,d=range.y-range.x;
            B("Dado",x,y+.5f,z,.28f,1,d,"Exterior sill");B("Glass",x,y+2.25f,z,.06f,2.6f,d,"Window glazing");
            B("Metal",x,y+2.35f,z,.12f,.05f,d);B("Metal",x,y+1,z,.16f,.07f,d);
        }
        B("Wall",x,y+3.85f,9,.28f,.7f,42.2f,"Exterior lintel");
        for(float z=-12;z<=30;z+=3.5f){if(z<17.7f||z>21.1f)B("Stone",x,y+2.25f,z,.38f,2.6f,.22f,"Window pier");if(z+1.75f<17.8f||z+1.75f>21)B("Metal",x,y+2.25f,z+1.75f,.12f,2.6f,.055f);}
        foreach(float z in new[]{17.82f,20.98f})B("Wood",x,y+1.72f,z,.36f,3.44f,.16f,"Bridge jamb");
        B("Wood",x,y+3.45f,19.4f,.36f,.15f,3.3f);
    }
    static void SkybridgeConnections(InteriorSceneRoot contract){
        var connections=new List<InteriorConnection>();var spawns=contract.spawnPoints.ToList();var views=new List<SkybridgeViewRoute>();
        foreach(float side in Building=="A"?new[]{-1f,1f}:new[]{Building=="B"?1f:-1f}){
            string target=Building=="A"?(side<0?"B":"C"):"A";
            float length=Building=="A"?(side<0?8.3f:9.8f):4f;
            float end=side*(20+length),center=side*(20+length/2);const float z=19.4f;
            B("Bridge Floor",center,y-.12f,z,length+.2f,.24f,3.3f,"Bridge deck");
            // Actual 600 x 1200 mm tiles, 4 mm joints and a narrow dark border.
            B("Dark tile",center,y+.004f,z,length,.008f,3.22f);
            for(float t=0;t<length;t+=1.2f)for(int row=0;row<5;row++){
                float w=Mathf.Min(1.2f,length-t);SoftBox("Bridge Floor",V(side*(20+t+w/2),y+.014f,18.2f+row*.6f),V(w-.004f,.02f,.596f),.003f);
            }
            B("Ceiling",center,y+3.4f,z,length+.3f,.18f,3.5f,"Bridge ceiling");
            B("Bridge Metal",center,y+3.29f,z,length,.04f,3.2f);
            for(float edge=17.98f;edge<20.9f;edge+=.14f)SoftBox("Bridge Wood",V(center,y+3.245f,edge),V(length,.065f,.085f),.008f);
            foreach(float edge in new[]{18.08f,20.72f}){
                B("Bridge Metal",center,y+3.19f,edge,length-.35f,.045f,.105f);
                B("Bridge Lamp",center,y+3.163f,edge,length-.4f,.012f,.045f);
            }
            foreach(float edge in new[]{17.75f,21.05f}){
                float inward=edge<19.4f?1:-1;
                SoftBox("Bridge Metal",V(center,y+.10f,edge),V(length,.2f,.13f),.012f,"Bridge curb");
                Collider("Bridge guard glazing",V(center,y+1.75f,edge),V(length,3.1f,.06f));
                SoftBox("Bridge Metal",V(center,y+3.2f,edge),V(length,.12f,.13f),.01f);
                int bays=Mathf.CeilToInt(length/2.65f);float bay=length/bays;
                for(int i=0;i<bays;i++){
                    float a=side*(20+i*bay+.045f),b=side*(20+(i+1)*bay-.045f);
                    meshes["Bridge Glass"].Quad(V(a,y+.2f,edge),V(b,y+.2f,edge),V(b,y+3.12f,edge),V(a,y+3.12f,edge));
                }
                for(int i=0;i<=bays;i++){
                    float xx=side*(20+length*i/bays);
                    SoftBox("Bridge Metal",V(xx,y+1.7f,edge),V(.09f,3.25f,.13f),.009f);
                    B("Bridge Trim",xx,y+1.7f,edge+inward*.072f,.027f,3.1f,.018f);
                    Tube("Bridge Trim",V(xx,y+1.1f,edge),V(xx,y+1.1f,edge+inward*.13f),.013f);
                }
                Tube("Bridge Trim",V(side*20,y+1.1f,edge+inward*.13f),V(end,y+1.1f,edge+inward*.13f),.028f,16);
                // White frit dots at eye level make clear glass legible.
                for(float t=.18f;t<length;t+=.19f)B("Ceramic",side*(20+t),y+1.47f,edge+inward*.006f,.022f,.022f,.007f);
            }
            Collider("Bridge transfer boundary",V(end,y+1.5f,z),V(.06f,3,3.2f));
            foreach(float edge in new[]{17.82f,20.98f})SoftBox("Bridge Metal",V(end,y+1.5f,edge),V(.18f,3,.12f),.012f);
            SoftBox("Bridge Metal",V(end,y+2.95f,z),V(.18f,.18f,3.3f),.012f);
            foreach(float doorZ in new[]{18.61f,20.19f}){
                meshes["Bridge Glass"].Quad(V(end,y+.12f,doorZ-.73f),V(end,y+.12f,doorZ+.73f),V(end,y+2.85f,doorZ+.73f),V(end,y+2.85f,doorZ-.73f));
                foreach(float dz in new[]{-.74f,.74f})SoftBox("Bridge Metal",V(end-side*.035f,y+1.48f,doorZ+dz),V(.09f,2.74f,.045f),.007f);
                SoftBox("Bridge Trim",V(end-side*.05f,y+.2f,doorZ),V(.06f,.26f,1.43f),.01f);
                Tube("Bridge Trim",V(end-side*.13f,y+1.12f,doorZ-.5f),V(end-side*.13f,y+1.12f,doorZ+.5f),.024f,16);
                foreach(float dz in new[]{-.48f,.48f})Tube("Bridge Trim",V(end-side*.04f,y+1.12f,doorZ+dz),V(end-side*.13f,y+1.12f,doorZ+dz),.015f);
                SoftBox("Bridge Trim",V(end-side*.07f,y+2.72f,doorZ-.3f),V(.1f,.07f,.3f),.01f);
            }
            SoftBox("Bridge Metal",V(end-side*.08f,y+3.16f,z),V(.16f,.28f,3.2f),.01f);
            Sign(target+"楼  /  2F",V(end-side*.18f,y+3.16f,z),Vector3.left*side,.2f,White);
            foreach(float xx in new[]{side*20,end-side*.18f}){
                B("Bridge Trim",xx,y+.023f,z,.055f,.025f,3.13f);
                B("Bridge Metal",xx-side*.05f,y+.01f,z,.024f,.01f,3.13f);
            }
            for(float t=1.8f;t<length;t+=3.2f)FillLight(V(side*(20+t),y+3.05f,z),.7f,6);
            var anchor=new GameObject("2F · 前往"+target+"楼").transform;anchor.SetParent(root,false);anchor.position=V(end-side*.75f,y+.06f,z);
            var spawn=new GameObject("bridge-from-"+target.ToLowerInvariant()).transform;spawn.SetParent(root,false);spawn.position=V(end-side*3.3f,y+.06f,z);spawn.rotation=Quaternion.LookRotation(Vector3.left*side);
            spawns.Add(new InteriorSpawnPoint{id=spawn.name,anchor=spawn});
            connections.Add(new InteriorConnection{id=Building.ToLowerInvariant()+"-to-"+target.ToLowerInvariant(),displayName=target+"楼 · 二楼连廊",anchor=anchor,destination=AssetDatabase.LoadAssetAtPath<BuildingEntranceDefinition>("Assets/Interiors/Entrances/teaching-"+target.ToLowerInvariant()+".asset"),destinationSpawnId="bridge-from-"+Building.ToLowerInvariant(),radius=1.6f});
            SoftBox("Bridge Metal",V(side*19.78f,y+2.1f,17.48f),V(.08f,.5f,.7f),.02f);
            Sign("2F → "+target,V(side*19.72f,y+2.1f,17.48f),Vector3.left*side,.16f,White);
            bool ab=Building=="B"||target=="B";
            views.Add(new SkybridgeViewRoute{side=side,sourceMapAnchor=ab?(Building=="A"?V(32,4.85f,-113.4f):V(32,4.85f,-105.1f)):(Building=="A"?V(88,4.85f,-118.2f):V(88,4.85f,-108.4f)),sourceMapDirection=Building=="A"?Vector3.forward:Vector3.back});
        }
        contract.connections=connections.ToArray();contract.spawnPoints=spawns.ToArray();
        root.gameObject.AddComponent<SkybridgeCampusView>().routes=views.ToArray();
        Sign(Building=="A"?"← B楼二层连廊        C楼二层连廊 →":"二层连廊通往 A楼",V(0,y+2.6f,19.48f),Vector3.back,.18f,Ink);
    }
}
