using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Local metres. The 8.4m storeys leave sports headroom; stairs use real 175mm risers.
public static partial class GymnasiumInteriorBuilder
{
    const string Folder="Assets/GymnasiumInterior";
    const float Storey=8.4f;
    static Transform root,level;
    static Dictionary<string,Material> materials;
    static Dictionary<string,DormitoryBuilder.Geometry> meshes;
    static Material lettering;
    static int floor;
    static float y;
    static Vector3 V(float x,float h,float z)=>new Vector3(x,h,z);
    static T Save<T>(T value,string path) where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    static void Mat(string name,string hex,float smooth=.25f,float stone=0,float wood=0,float emission=0,bool glass=false){
        ColorUtility.TryParseHtmlString(hex,out var c);if(glass)c.a=.10f;
        var m=new Material(Shader.Find(glass?"Campus/Cafeteria":"Campus/GymInterior")){name=name};m.SetColor("_BaseColor",c);
        m.SetFloat("_Smoothness",smooth);m.SetFloat("_Stone",stone);m.SetFloat("_Wood",wood);m.SetFloat("_Emission",emission);
        if(glass)m.SetFloat("_Glass",1);else m.SetFloat("_Metallic",name=="Steel"?.65f:name=="Mirror"?.28f:name=="Metal"?.25f:0);
        if(glass){m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.renderQueue=3000;}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");
    }
    static void Box(string mat,Vector3 p,Vector3 size,string solid=null){
        // Subdivide large surfaces so each render chunk receives nearby room lights.
        int nx=Mathf.CeilToInt(size.x/6),ny=Mathf.CeilToInt(size.y/6),nz=Mathf.CeilToInt(size.z/6);
        var cell=V(size.x/nx,size.y/ny,size.z/nz);
        for(int ix=0;ix<nx;ix++)for(int iy=0;iy<ny;iy++)for(int iz=0;iz<nz;iz++)
            meshes[mat].Box(p-size*.5f+V((ix+.5f)*cell.x,(iy+.5f)*cell.y,(iz+.5f)*cell.z),Vector3.right,Vector3.up,Vector3.forward,cell);
        if(solid!=null)Collider(solid,p,size);
    }
    static void B(string mat,float x,float h,float z,float w,float height,float depth,string solid=null)=>Box(mat,V(x,h,z),V(w,height,depth),solid);
    static void Collider(string name,Vector3 p,Vector3 size){var c=new GameObject(name).AddComponent<BoxCollider>();c.transform.SetParent(level,false);c.center=p;c.size=size;}
    static void Beam(string mat,Vector3 a,Vector3 b,float width)=>meshes[mat].Beam(a,b,width);
    // Small edge radii catch light without changing the clear collision envelope.
    static void SoftBox(string mat,Vector3 center,Vector3 size,float radius,string solid=null){
        var h=size*.5f;float r=Mathf.Min(radius,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f);var inner=h-Vector3.one*r;
        float[] Corners(float extent)=>new[]{-extent,-extent+r,extent-r,extent};
        Vector3 Round(Vector3 p){var q=V(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));return center+q+(p-q).normalized*r;}
        for(int axis=0;axis<3;axis++)foreach(float sign in new[]{-1f,1f}){
            int u=(axis+1)%3,v=(axis+2)%3;var a=Corners(h[u]);var b=Corners(h[v]);
            Vector3 P(float s,float t){var p=Vector3.zero;p[axis]=h[axis]*sign;p[u]=s;p[v]=t;return Round(p);}
            for(int i=0;i<3;i++)for(int j=0;j<3;j++){
                var p0=P(a[i],b[j]);var p1=P(a[i+1],b[j]);var p2=P(a[i+1],b[j+1]);var p3=P(a[i],b[j+1]);
                if(sign>0)meshes[mat].Quad(p0,p1,p2,p3);else meshes[mat].Quad(p3,p2,p1,p0);
            }
        }
        if(solid!=null)Collider(solid,center,size);
    }
    static void Tube(string mat,Vector3 a,Vector3 b,float radius,int segments=12){
        var axis=(b-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
        for(int i=0;i<segments;i++){float t=i*2*Mathf.PI/segments,s=(i+1)*2*Mathf.PI/segments;var p=(u*Mathf.Cos(t)+v*Mathf.Sin(t))*radius;var q=(u*Mathf.Cos(s)+v*Mathf.Sin(s))*radius;meshes[mat].Quad(a+p,a+q,b+q,b+p);meshes[mat].Triangle(a,a+q,a+p);meshes[mat].Triangle(b,b+p,b+q);}
    }
    static void Sign(string text,Vector3 p,Vector3 normal,float size,Color color){
        var go=new GameObject(text.Replace('\n',' '));go.transform.SetParent(level,false);go.transform.position=p;go.transform.rotation=Quaternion.LookRotation(-normal);
        var tm=go.AddComponent<TextMesh>();tm.text=text;tm.fontSize=96;tm.characterSize=size/10;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;
        go.AddComponent<CafeteriaSign>().surface=lettering;
    }
    static readonly Color Ink=new Color(.025f,.035f,.043f),White=new Color(.94f,.93f,.86f);
    static void SmoothEdges(Mesh mesh){
        // Weld normals only across gentle bevels. Keep 90-degree wall corners sharp.
        var vertices=mesh.vertices;var normals=mesh.normals;var output=new Vector3[normals.Length];
        var groups=new Dictionary<Vector3Int,List<int>>();
        for(int i=0;i<vertices.Length;i++){
            var p=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
            if(!groups.TryGetValue(key,out var ids)){ids=new List<int>();groups.Add(key,ids);}ids.Add(i);
        }
        foreach(var ids in groups.Values)foreach(var i in ids){var sum=Vector3.zero;foreach(var j in ids)if(Vector3.Dot(normals[i],normals[j])>.55f)sum+=normals[j];output[i]=sum.normalized;}
        mesh.normals=output;
    }
    static void Slab(float x0,float x1,float z0,float z1,float top,string mat="Floor")=>B(mat,(x0+x1)/2,top-.12f,(z0+z1)/2,x1-x0,.24f,z1-z0,"Floor slab");
    static void Plant(Vector3 p,float h){
        Box("Stone",p+Vector3.up*.3f,V(.75f,.6f,.75f),"Plant pot");Box("Soil",p+Vector3.up*.61f,V(.65f,.035f,.65f));
        for(int i=0;i<17;i++){
            float a=i*2.39996f;var tip=p+V(Mathf.Cos(a)*.55f,.6f+h*(.4f+(i%4)*.18f),Mathf.Sin(a)*.55f);
            Beam("Wood",p+Vector3.up*.6f,tip,.025f);var u=new Vector3(Mathf.Cos(a),.15f,Mathf.Sin(a));var v=Vector3.Cross(u,Vector3.up).normalized;
            meshes[i%2==0?"Leaf":"Leaf light"].Quad(tip-u*.38f,tip+v*.14f,tip+u*.13f,tip-v*.14f);
            for(int j=0;j<5;j++){
                var leaf=Vector3.Lerp(p+Vector3.up*.8f,tip,.45f+j*.14f);var axis=new Vector3(Mathf.Cos(a+j),.5f,Mathf.Sin(a+j)).normalized;var cross=Vector3.Cross(axis,Vector3.up).normalized;
                meshes[j%2==0?"Leaf":"Leaf light"].Quad(leaf-axis*.08f,leaf+cross*.13f,leaf+axis*.42f,leaf-cross*.13f);
            }
        }
    }
}
