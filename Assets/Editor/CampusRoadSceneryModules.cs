using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

// Shared metre-scale furniture and vegetation. Geometry is authored, not a reference-image backdrop.
public static partial class CampusPlanBuilder
{
    const string SceneryFolder=Folder+"/RoadScenery";
    static GameObject[] treeModules;
    static readonly List<Vector2> sceneryTrees=new List<Vector2>();
    static int sceneryProps;
    static void SceneryMaterial(string name,string hex,int surface,float smooth=.18f,float metal=0,Vector2? tile=null){
        ColorUtility.TryParseHtmlString(hex,out var color);
        var m=new Material(Shader.Find("Campus/RoadLandscape")){name="Campus landscape · "+name,enableInstancing=true};
        m.SetColor("_BaseColor",color);m.SetFloat("_Surface",surface);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);
        var t=tile??new Vector2(.6f,.4f);m.SetVector("_TileSize",new Vector4(t.x,t.y,0,0));
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static void InitRoadScenery(){
        Directory.CreateDirectory(SceneryFolder);Directory.CreateDirectory(SceneryFolder+"/Modules");
        sceneryTrees.Clear();sceneryProps=0;
        SceneryMaterial("Paving","#bdbcb5",1,.19f,0,new Vector2(.9f,.45f));
        SceneryMaterial("Promenade","#c9c5bb",1,.2f,0,new Vector2(.9f,.6f));
        SceneryMaterial("BorderStone","#989b95",1,.14f,0,new Vector2(.3f,.3f));
        SceneryMaterial("PaleStone","#d5d0c3",2,.18f);
        SceneryMaterial("SignBoard","#203b43",5,.14f,.05f);
        SceneryMaterial("Asphalt","#505353",0,.08f);
        SceneryMaterial("Stone","#aba99d",2,.22f);
        SceneryMaterial("Wood","#95734b",3,.28f);
        SceneryMaterial("Metal","#353f42",5,.38f,.65f);
        SceneryMaterial("Leaf","#668745",4,.14f);
        SceneryMaterial("Aggregate","#a5a79e",0,.14f);
        SceneryMaterial("SmallPavers","#bab8b0",1,.16f,0,new Vector2(.4f,.2f));
        SceneryMaterial("DarkBand","#767c77",1,.13f,0,new Vector2(.3f,.3f));
        SceneryMaterial("Track","#975b50",0,.1f);
        SceneryMaterial("Soil","#4b4838",0,.06f);
        SceneryMaterial("Bark","#6b6250",6,.08f);
        SceneryMaterial("GrassDetail","#85905a",4,.1f);
        SceneryMaterial("ShrubLight","#6b8142",4,.1f);
        treeModules=new GameObject[3];for(int i=0;i<3;i++)treeModules[i]=MakeTreeModule(i);
        ExportFurnitureModule("Bench",()=>SceneryBench(0,0));
        ExportFurnitureModule("ReadingTable",()=>ReadingTable(0,0));
        ExportFurnitureModule("PathLight",()=>SceneryLamp(0,0));
        ExportFurnitureModule("LowLight",()=>SceneryLamp(0,0,true));
        ExportFurnitureModule("BicycleBay",()=>CycleParking(0,0));
        ExportFurnitureModule("Planter",()=>SceneryBed(0,0,3,1.7f));
    }
    static void ExportFurnitureModule(string name,Action generate){
        var savedGeometry=geometry;var savedPhysics=physicsRoot;int savedCount=sceneryProps;
        var module=new GameObject(name);physicsRoot=module.transform;
        geometry=materials.Keys.ToDictionary(k=>k,k=>new DormitoryBuilder.Geometry());generate();
        foreach(var item in geometry.Where(p=>p.Value.vertices.Count>0)){
            var mesh=item.Value.Mesh(name+" "+item.Key);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].y-=H(0,0);mesh.vertices=vertices;mesh.RecalculateBounds();
            mesh=Save(mesh,SceneryFolder+"/Modules/"+name+"-"+item.Key+".asset");MeshObject(item.Key,mesh,materials[item.Key],module.transform);
        }
        foreach(var c in module.GetComponentsInChildren<Collider>())c.transform.localPosition-=Vector3.up*H(0,0);
        PrefabUtility.SaveAsPrefabAsset(module,SceneryFolder+"/Modules/"+name+".prefab");UnityEngine.Object.DestroyImmediate(module);
        physicsRoot=savedPhysics;geometry=savedGeometry;sceneryProps=savedCount;
    }
    sealed class LeafMesh {
        readonly List<Vector3> p=new List<Vector3>(),n=new List<Vector3>();readonly List<Color> c=new List<Color>();
        public void Tri(Vector3 a,Vector3 b,Vector3 d,Color color){p.AddRange(new[]{a,b,d});var normal=Vector3.Cross(b-a,d-a).normalized;n.AddRange(new[]{normal,normal,normal});c.AddRange(new[]{color,color,color});}
        public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(p);m.SetNormals(n);m.SetColors(c);m.SetTriangles(Enumerable.Range(0,p.Count).ToArray(),0);m.RecalculateBounds();return m;}
    }
    static void Tube(DormitoryBuilder.Geometry g,Vector3 a,Vector3 b,float ra,float rb,int sides=8){
        var axis=(b-a).normalized;var u=Vector3.Cross(axis,Vector3.forward).normalized;if(u.sqrMagnitude<.1f)u=Vector3.right;var v=Vector3.Cross(axis,u);
        for(int k=0;k<sides;k++){float t=k*Mathf.PI*2/sides,tt=(k+1)*Mathf.PI*2/sides;var d=u*Mathf.Cos(t)+v*Mathf.Sin(t);var e=u*Mathf.Cos(tt)+v*Mathf.Sin(tt);g.Quad(a+d*ra,b+d*rb,b+e*rb,a+e*ra);}
        g.Disc(b,u,v,rb,sides);g.Disc(a,u,-v,ra,sides);
    }
    static GameObject MakeTreeModule(int variant){
        var temp=new GameObject("Canopy tree "+variant);var lods=new List<LOD>();
        for(int level=0;level<3;level++){
            var branches=new DormitoryBuilder.Geometry();var leaf=new LeafMesh();var rng=new System.Random(5741+variant*123);
            float R()=> (float)rng.NextDouble();var centres=new List<Vector3>();
            Tube(branches,Vector3.zero,new Vector3(.11f,3.4f,0),.19f,.105f);
            for(int k=0;k<11;k++){
                float angle=k*2.399f+variant;var end=new Vector3(Mathf.Cos(angle)*(1.2f+R()*1.2f),4.5f+R()*2.2f,Mathf.Sin(angle)*(1.2f+R()*1.2f));
                var fork=new Vector3(end.x*.42f,3.2f+R()*.8f,end.z*.42f);Tube(branches,new Vector3(.1f,2.5f+k*.06f,0),fork,.07f,.045f);Tube(branches,fork,end,.045f,.013f,6);centres.Add(end);
            }
            int count=level==0?500:level==1?150:40;
            foreach(var centre in centres)for(int j=0;j<count;j++){
                float az=R()*Mathf.PI*2,yy=R()*2-1,rr=Mathf.Pow(R(),.333f);var q=centre+new Vector3(Mathf.Cos(az)*Mathf.Sqrt(1-yy*yy)*1.3f,yy*.95f,Mathf.Sin(az)*Mathf.Sqrt(1-yy*yy)*1.3f)*rr;
                float angle=R()*Mathf.PI*2,length=(level==0?.09f:level==1?.17f:.31f)*(1+R()*.45f);
                var u=new Vector3(Mathf.Cos(angle),R()*.8f-.4f,Mathf.Sin(angle)).normalized*length;var v=Vector3.Cross(u,Vector3.up).normalized*length*.48f;var fold=Vector3.up*length*.13f;
                var tint=Color.Lerp(new Color(.65f,.76f,.49f),new Color(1.12f,1.16f,.84f),R());if(variant==2&&R()<.24f)tint=new Color(1.55f,1.24f,.49f);
                // Eight-sided leaf silhouette, with a raised midrib and rounded shoulders.
                var outline=new[]{q-u,q-u*.6f+v*.75f,q+v,q+u*.6f+v*.65f,q+u,q+u*.6f-v*.65f,q-v,q-u*.6f-v*.75f};
                for(int k=0;k<8;k++)leaf.Tri(outline[k],outline[(k+1)%8],q+fold,tint);
            }
            var group=new GameObject("LOD"+level);group.transform.SetParent(temp.transform,false);
            var bark=Save(branches.Mesh("Branches "+variant+" LOD"+level),SceneryFolder+"/Modules/tree-"+variant+"-"+level+"-branches.asset");MeshObject("Branches",bark,materials["Bark"],group.transform);
            var m=Save(leaf.Mesh("Foliage "+variant+" LOD"+level),SceneryFolder+"/Modules/tree-"+variant+"-"+level+"-leaves.asset");
            string matPath=Folder+"/Materials/Foliage.mat";var mat=new Material(materials["Leaf"]);mat.SetFloat("_VertexTint",1);mat=Save(mat,matPath);
            MeshObject("Foliage",m,mat,group.transform);lods.Add(new LOD(level==0?.12f:level==1?.045f:.008f,group.GetComponentsInChildren<Renderer>()));
        }
        var lod=temp.AddComponent<LODGroup>();lod.SetLODs(lods.ToArray());lod.RecalculateBounds();var prefab=PrefabUtility.SaveAsPrefabAsset(temp,SceneryFolder+"/Modules/Canopy-"+variant+".prefab");UnityEngine.Object.DestroyImmediate(temp);return prefab;
    }
    static void SceneryTree(float x,float z,float scale=1){
        if(sceneryTrees.Any(p=>Vector2.Distance(p,new Vector2(x,z))<3.5f))return;
        sceneryTrees.Add(new Vector2(x,z));int seed=Mathf.Abs(Mathf.RoundToInt(x*31+z*57));
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(treeModules[seed%3],holder);obj.name="路荫乔木";obj.transform.localPosition=V(x,H(x,z),z);obj.transform.localRotation=Quaternion.Euler(0,seed%360,0);obj.transform.localScale=Vector3.one*scale;
        ColliderBox("Tree trunk",V(x,H(x,z)+1.2f,z),V(.36f*scale,2.4f,.36f*scale));
        // Root bed with a genuine recessed planting surface, below the pedestrian shoulder.
        Box("Soil",V(x,H(x,z)+.04f,z),V(1.5f,.06f,1.5f));
        foreach(float side in new[]{-1f,1f}){Box("Stone",V(x+side*.78f,H(x,z)+.07f,z),V(.1f,.14f,1.65f));Box("Stone",V(x,H(x,z)+.07f,z+side*.78f),V(1.5f,.14f,.1f));}
    }
    static Vector3 Local(float x,float z,float yaw,Vector3 p){return V(x,H(x,z),z)+Quaternion.Euler(0,yaw,0)*p;}
    static void OrientedBox(string mat,float x,float z,float yaw,Vector3 p,Vector3 size){var rot=Quaternion.Euler(0,yaw,0);geometry[mat].Box(Local(x,z,yaw,p),rot*Vector3.right,Vector3.up,rot*Vector3.forward,size);}
    static void PropCollider(string name,float x,float z,float yaw,Vector3 p,Vector3 size){var o=new GameObject(name+" · collision"){layer=9};o.transform.SetParent(physicsRoot,false);o.transform.position=game.MapToWorld(Local(x,z,yaw,p));o.transform.rotation=Quaternion.Euler(0,-yaw,0);o.AddComponent<BoxCollider>().size=size;sceneryProps++;}
    static void SceneryBench(float x,float z,float yaw=0){
        void B(string m,Vector3 p,Vector3 s)=>OrientedBox(m,x,z,yaw,p,s);
        foreach(float xx in new[]{-.98f,.98f}){B("Stone",V(xx,.23f,0),V(.28f,.46f,.66f));B("Metal",V(xx,.69f,.25f),V(.055f,.55f,.055f));}
        for(int i=0;i<5;i++)B("Wood",V(0,.48f,-.28f+i*.125f),V(2.5f,.07f,.108f));
        for(int i=0;i<3;i++)B("Wood",V(0,.7f+i*.14f,.29f),V(2.5f,.115f,.07f));
        foreach(float xx in new[]{-1f,1f}){B("Metal",V(xx,.66f,-.02f),V(.045f,.035f,.55f));B("Metal",V(xx,.55f,-.25f),V(.04f,.24f,.04f));}
        PropCollider("Slatted seat",x,z,yaw,V(0,.5f,0),V(2.5f,1,.68f));
    }
    static void SceneryLamp(float x,float z,bool low=false){
        float y=H(x,z),h=low?.85f:4.5f;Box("Metal",V(x,y+.06f,z),V(.26f,.12f,.26f));Box("Metal",V(x,y+h/2,z),V(.1f,h,.1f));
        if(low){Box("Metal",V(x,y+.83f,z),V(.23f,.08f,.23f));Box("Light",V(x,y+.69f,z),V(.18f,.2f,.18f));}
        else {Box("Metal",V(x+.38f,y+h,z),V(.88f,.09f,.22f));Box("Light",V(x+.4f,y+h-.06f,z),V(.68f,.025f,.12f));}
        ColliderBox("Landscape lamp",V(x,y+h/2,z),V(.18f,h,.18f));
    }
    static void Shrubs(float x,float z,float radius=.65f){
        float y=H(x,z);var rng=new System.Random(Mathf.RoundToInt(x*79+z*53));
        Vector3 Core(float a,float b)=>V(x+Mathf.Cos(a)*Mathf.Sin(b)*radius*.82f,y+.29f+Mathf.Cos(b)*.23f,z+Mathf.Sin(a)*Mathf.Sin(b)*radius*.82f);
        for(int row=0;row<6;row++)for(int col=0;col<16;col++){float a=col*Mathf.PI/8,b=(col+1)*Mathf.PI/8,t=row*Mathf.PI/6,tt=(row+1)*Mathf.PI/6;geometry["Leaf"].Quad(Core(a,t),Core(b,t),Core(b,tt),Core(a,tt));}
        // Branching shoots and small folded leaflets replace solid rectangular hedge blocks.
        for(int i=0;i<260;i++){float angle=(float)rng.NextDouble()*6.283f,r=radius*Mathf.Sqrt((float)rng.NextDouble());var c=V(x+Mathf.Cos(angle)*r,y+.13f+(float)rng.NextDouble()*.46f,z+Mathf.Sin(angle)*r);var u=V(Mathf.Cos(angle),.25f,Mathf.Sin(angle))*.075f;var v=Vector3.Cross(u,Vector3.up).normalized*.04f;string mat=i%4==0?"ShrubLight":"Leaf";
            geometry[mat].Quad(c-u,c-u*.4f+v,c+u*.4f+v,c+u);geometry[mat].Quad(c-u,c+u,c+u*.4f-v,c-u*.4f-v);
            if(i%20==0)Beam("Bark",V(c.x,y,c.z),c,.012f);
        }
    }
    static void OrnamentalGrass(float x,float z){float y=H(x,z);for(int k=0;k<13;k++){float a=k*2.399f,h=.38f+(k%4)*.09f;var u=V(Mathf.Cos(a),0,Mathf.Sin(a));var p=V(x,y,z);var q=p+u*.1f+Vector3.up*h*.65f;var tip=p+u*.3f+Vector3.up*h;geometry["GrassDetail"].Triangle(p-u*.018f,q-u*.025f,tip);geometry["GrassDetail"].Triangle(p-u*.018f,tip,p+u*.018f);}}
    static void SceneryBed(float x,float z,float w,float d){
        float y=H(x,z);Box("Soil",V(x,y+.13f,z),V(w,.22f,d));
        foreach(float s in new[]{-1f,1f}){Box("Stone",V(x+s*w/2,y+.19f,z),V(.14f,.38f,d+.14f),true);Box("Stone",V(x,y+.19f,z+s*d/2),V(w,.38f,.14f),true);}
        for(float xx=x-w/2+.45f;xx<x+w/2;xx+=.85f)for(float zz=z-d/2+.4f;zz<z+d/2;zz+=.8f){Shrubs(xx,zz,.49f);if(((int)(xx+zz))%3==0)OrnamentalGrass(xx,zz);}
    }
    static void ReadingTable(float x,float z,float yaw=0){
        OrientedBox("Wood",x,z,yaw,V(0,.76f,0),V(1.5f,.08f,.8f));foreach(float s in new[]{-1f,1f})OrientedBox("Metal",x,z,yaw,V(s*.6f,.38f,0),V(.06f,.76f,.6f));
        PropCollider("Reading table",x,z,yaw,V(0,.4f,0),V(1.5f,.8f,.8f));
        var a=Local(x,z,yaw,V(0,0,1.2f));SceneryBench(a.x,a.z,yaw);var b=Local(x,z,yaw,V(0,0,-1.2f));SceneryBench(b.x,b.z,yaw+180);
    }
    static void CycleParking(float x,float z,float yaw=0,int count=5){
        for(int i=0;i<count;i++){
            float offset=(i-(count-1)/2f)*.8f;Vector3 Q(float a,float y,float b)=>Local(x,z,yaw,V(offset+a,y,b));
            foreach(float side in new[]{-.4f,.4f})Beam("Metal",Q(-.25f,0,side),Q(-.25f,.75f,side),.04f);Beam("Metal",Q(-.25f,.75f,-.4f),Q(-.25f,.75f,.4f),.04f);
            if(i%3==2)continue;
            var rot=Quaternion.Euler(0,yaw,0);var f=rot*Vector3.forward;
            foreach(float zz in new[]{-.62f,.62f}){var c=Q(.13f,.35f,zz);geometry["Metal"].Ring(c,f,Vector3.up,.33f,.04f,20);geometry["Stone"].Ring(c,f,Vector3.up,.282f,.016f,20);for(int j=0;j<6;j++){float a=j*Mathf.PI/3;Beam("Metal",c,c+(f*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*.28f,.008f);}}
            var rear=Q(.13f,.35f,-.62f);var front=Q(.13f,.35f,.62f);var crank=Q(.13f,.32f,0);var seat=Q(.13f,.9f,-.17f);var head=Q(.13f,.85f,.42f);
            foreach(var pair in new[]{new[]{rear,crank},new[]{rear,seat},new[]{crank,seat},new[]{seat,head},new[]{head,crank},new[]{head,front}})Beam("Metal",pair[0],pair[1],.025f);
            OrientedBox("Metal",x,z,yaw,V(offset+.13f,.93f,-.17f),V(.2f,.04f,.28f));Beam("Metal",head,Q(.13f,1.03f,.48f),.025f);Beam("Metal",Q(-.1f,1.03f,.48f),Q(.36f,1.03f,.48f),.025f);
        }
        PropCollider("Cycle bay",x,z,yaw,V(0,.5f,0),V(count*.8f,1,1.6f));
    }
    static void WasteStation(float x,float z){
        for(int i=0;i<3;i++){float xx=x+(i-1)*.48f,y=H(xx,z);Box("Metal",V(xx,y+.48f,z),V(.43f,.96f,.44f),true);Box("Stone",V(xx,y+.92f,z),V(.45f,.08f,.46f));Box("Soil",V(xx,y+.76f,z-.225f),V(.28f,.13f,.015f));Box("Paint",V(xx,y+.46f,z-.23f),V(.16f,.16f,.01f));}
    }
}
