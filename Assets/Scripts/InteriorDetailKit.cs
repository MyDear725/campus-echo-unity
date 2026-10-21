using System.Collections.Generic;
using UnityEngine;

// The same real 3D detail modules are used by persisted scenes and generated dorm rooms.
public sealed class InteriorDetailKit : MonoBehaviour
{
    readonly List<Mesh> owned=new List<Mesh>();
    public InteriorRefinementPalette palette;
    public static InteriorDetailKit Create(Transform parent,string name)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        var kit=go.AddComponent<InteriorDetailKit>();kit.palette=InteriorRefinementPalette.Load();return kit;
    }
    public GameObject Box(string name,Vector3 at,Vector3 size,Material material,float radius=.012f,bool solid=false)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=at;
        var mesh=Bevel(size,radius);owned.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
        if(solid){var box=go.AddComponent<BoxCollider>();box.size=size;}
        return go;
    }
    public void Beam(string name,Vector3 a,Vector3 b,float radius,Material material)
    {
        var go=Box(name,(a+b)/2,new Vector3(radius,(a-b).magnitude,radius),material,radius*.25f);
        go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    public void Sign(string heading,string caption,Vector3 at,Vector3 normal,float width=2.4f,float height=.75f)
    {
        var plate=Box("Wayfinding · "+heading,at,new Vector3(width,height,.035f),palette.teal,.018f);
        plate.transform.localRotation=Quaternion.LookRotation(-normal);
        Text(plate.transform,heading,new Vector3(0,.11f,-.027f),.19f,Color.white);
        Text(plate.transform,caption,new Vector3(0,-.19f,-.028f),.085f,new Color(.78f,.84f,.84f));
    }
    public void Text(Transform parent,string value,Vector3 at,float size,Color color)
    {
        var go=new GameObject(value);go.transform.SetParent(parent,false);go.transform.localPosition=at;
        var text=go.AddComponent<TextMesh>();text.text=value;text.font=palette.font;text.fontSize=96;
        text.characterSize=size/10;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;
        go.GetComponent<MeshRenderer>().sharedMaterial=palette.font.material;
        go.AddComponent<InteriorWorldLabel>();
    }
    public void Plant(Vector3 at,float height=.9f)
    {
        Box("Stone planter",at+Vector3.up*.21f,new Vector3(.5f,.42f,.5f),palette.plaster,.045f);
        Box("Pot soil",at+Vector3.up*.43f,new Vector3(.43f,.02f,.43f),palette.soil);
        for(int i=0;i<9;i++)
        {
            float a=i*2.4f;var tip=at+new Vector3(Mathf.Cos(a)*.28f,.43f+height*(.58f+(i%3)*.2f),Mathf.Sin(a)*.28f);
            Beam("Plant stem",at+Vector3.up*.42f,tip,.015f,palette.leaf);
            var leaf=Box("Sculpted leaf",tip,new Vector3(.11f,.018f,.35f),palette.leaf,.008f);
            leaf.transform.localRotation=Quaternion.Euler(25+i*6,a*Mathf.Rad2Deg,18);
        }
    }
    public void Luminaire(Vector3 at,float length=2)
    {
        Box("Luminaire housing",at,new Vector3(length,.07f,.16f),palette.graphite);
        var face=Box("LED diffuser",at+Vector3.down*.042f,new Vector3(length-.08f,.012f,.11f),palette.light,.004f);
        face.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    public void Disc(string name,Vector3 at,float radius,float height,Material material)
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
        void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
        {int n=vertices.Count;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{normal,normal,normal});triangles.AddRange(new[]{n,n+1,n+2});}
        for(int i=0;i<32;i++)
        {
            float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
            var u=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);var v=new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);var h=Vector3.up*height;
            Tri(h,v+h,u+h,Vector3.up);Tri(Vector3.zero,u,v,Vector3.down);
            var normal=(u+v).normalized;Tri(u,v+h,v,normal);Tri(u,u+h,v+h,normal);
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();owned.Add(mesh);
        var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=at;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    public void TaskLamp(Vector3 desk)
    {
        Disc("Task lamp weighted base",desk,.10f,.022f,palette.graphite);
        Beam("Task lamp upright",desk+Vector3.up*.02f,desk+new Vector3(0,.32f,0),.025f,palette.graphite);
        Beam("Task lamp arm",desk+new Vector3(0,.32f,0),desk+new Vector3(0,.39f,-.18f),.025f,palette.graphite);
        Box("Task lamp shade",desk+new Vector3(0,.38f,-.21f),new Vector3(.30f,.045f,.12f),palette.graphite,.018f);
        Box("Task lamp diffuser",desk+new Vector3(0,.353f,-.21f),new Vector3(.25f,.008f,.085f),palette.light,.003f);
    }
    public void Laptop(Vector3 desk,Material accent)
    {
        Box("Laptop aluminium base",desk+Vector3.up*.012f,new Vector3(.36f,.024f,.25f),palette.steel,.009f);
        Box("Laptop keyboard",desk+new Vector3(0,.026f,.025f),new Vector3(.31f,.004f,.13f),palette.graphite,.002f);
        Box("Laptop trackpad",desk+new Vector3(0,.027f,-.074f),new Vector3(.10f,.004f,.055f),palette.plaster,.002f);
        var screen=Box("Laptop screen frame",desk+new Vector3(0,.14f,.13f),new Vector3(.36f,.245f,.016f),palette.graphite,.008f);
        screen.transform.localRotation=Quaternion.Euler(-12,0,0);
        var face=Box("Laptop screen",desk+new Vector3(0,.14f,.119f),new Vector3(.325f,.21f,.004f),accent,.002f);face.transform.localRotation=screen.transform.localRotation;
        for(int i=0;i<3;i++)Box("Screen document line",desk+new Vector3(-.025f,.17f-i*.035f,.112f),new Vector3(.20f,.007f,.004f),palette.linen,.001f);
    }
    public void WallComposition(Vector3 at,Vector3 normal,Material accent,float width=3)
    {
        var sub=Create(transform,"Framed geometric wall artwork");sub.palette=palette;sub.transform.localPosition=at;sub.transform.localRotation=Quaternion.LookRotation(-normal);
        sub.Box("Champagne frame",Vector3.zero,new Vector3(width,1.35f,.06f),palette.brass,.018f);
        sub.Box("Linen picture mount",new Vector3(0,0,-.037f),new Vector3(width-.06f,1.29f,.015f),palette.linen,.005f);
        sub.Box("Colour field",new Vector3(-width*.22f,0,-.050f),new Vector3(width*.33f,1.04f,.008f),accent,.04f);
        for(int i=0;i<7;i++)sub.Box("Oak rhythm",new Vector3(-width*.02f+i*width*.06f,-.12f+i*.07f,-.055f),new Vector3(.025f,.64f,.008f),palette.walnut,.002f);
        sub.Box("Art baseline",new Vector3(0,-.51f,-.058f),new Vector3(width*.82f,.012f,.008f),palette.graphite,.001f);
    }
    public void Annulus(Vector3 center,float inner,float outer,float depth,Material material)
    {
        // Continuous visible slab replaces overlapping visual boxes, while their
        // original segmented colliders continue to protect the central opening.
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});for(int i=0;i<4;i++)normals.Add(normal);indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        Vector3 P(float a,float r,float y)=>center+new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
        for(int i=0;i<128;i++)
        {
            float a=i*Mathf.PI*2/128,b=(i+1)*Mathf.PI*2/128,m=(a+b)/2;
            Quad(P(a,inner,0),P(b,inner,0),P(b,outer,0),P(a,outer,0),Vector3.up);
            Quad(P(a,outer,-depth),P(b,outer,-depth),P(b,inner,-depth),P(a,inner,-depth),Vector3.down);
            var radial=new Vector3(Mathf.Cos(m),0,Mathf.Sin(m));
            Quad(P(a,outer,0),P(b,outer,0),P(b,outer,-depth),P(a,outer,-depth),radial);
            Quad(P(b,inner,0),P(a,inner,0),P(a,inner,-depth),P(b,inner,-depth),-radial);
        }
        var mesh=new Mesh{name="Continuous reading hall ring slab"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();owned.Add(mesh);
        var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    public void Soften(GameObject go,float radius)
    {
        var filter=go.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)return;
        var size=Vector3.Scale(filter.sharedMesh.bounds.size,go.transform.localScale);
        var mesh=Bevel(size,radius);owned.Add(mesh);filter.sharedMesh=mesh;go.transform.localScale=Vector3.one;
        var collider=go.GetComponent<BoxCollider>();if(collider){collider.size=size;collider.center=Vector3.zero;}
    }
    public static Mesh Bevel(Vector3 size,float radius)
    {
        var h=size*.5f;float r=Mathf.Max(.0001f,Mathf.Min(radius,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f));var inner=h-Vector3.one*r;
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
        Vector3 Point(Vector3 p,out Vector3 n)
        {var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));n=(p-q).normalized;return q+n*r;}
        for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
        {
            int u=(axis+1)%3,v=(axis+2)%3;float[] a={-h[u],-h[u]+r,h[u]-r,h[u]},b={-h[v],-h[v]+r,h[v]-r,h[v]};
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)
            {
                int start=vertices.Count;
                foreach(var c in new[]{new Vector2(a[i],b[j]),new Vector2(a[i+1],b[j]),new Vector2(a[i+1],b[j+1]),new Vector2(a[i],b[j+1])})
                {var p=Vector3.zero;p[axis]=h[axis]*sign;p[u]=c.x;p[v]=c.y;vertices.Add(Point(p,out var n));normals.Add(n);}
                if(sign>0)indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});else indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
        }
        var mesh=new Mesh{name="Refined bevelled module"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
    }
    void OnDestroy(){if(Application.isPlaying)foreach(var mesh in owned)if(mesh)Destroy(mesh);}
}
