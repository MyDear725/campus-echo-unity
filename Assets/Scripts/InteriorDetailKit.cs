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
