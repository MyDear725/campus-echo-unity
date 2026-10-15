using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// A three-storey photographic interpretation at the sixth cafeteria's map site.
// All dimensions are metres. Front is west; horizontal facade coordinate is +z.
public static class CafeteriaBuilder
{
    const string Folder="Assets/CafeteriaRevision",Group="第六食堂 · 石材与玻璃立面";
    const float Front=183,Center=8,Width=36,Depth=20,Height=18.8f;
    // Include the old canopy's projecting lip, not just the old wall footprint.
    static readonly Rect Mask=Rect.MinMaxRect(176.8f,-13,207,29);
    static Dictionary<string,DormitoryBuilder.Geometry> geometry;
    static Dictionary<string,Material> materials;
    static System.Random random;
    static MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
    static Vector3 V(float u,float y,float d)=>new Vector3(Front+d,y,Center+u);
    static void Box(string m,float u,float y,float d,float w,float h,float depth)=>geometry[m].Box(V(u,y,d),Vector3.forward,Vector3.up,Vector3.right,new Vector3(w,h,depth));
    static void Beam(string m,Vector3 a,Vector3 b,float width)=>geometry[m].Beam(a,b,width);
    static T Save<T>(T value,string path)where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    [MenuItem("Campus/Apply Reference Cafeteria")]
    public static void Generate() {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        var root=GameObject.Find("开发区校区 · 校园环境").transform;
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        // Library clipping can refer to cafeteria-preserved source batches. Restore
        // in reverse order and reapply in order so no old facade is re-enabled.
        bool hasTeaching=root.Find(TeachingBuilder.Group);
        if(hasTeaching)TeachingBuilder.RestoreSources(root);
        bool hasGym=root.Find(GymnasiumBuilder.Group);
        if(hasGym)GymnasiumBuilder.RestoreSources(root);
        bool hasLibrary=root.Find("图书馆 · 石材与通高玻璃大厅");
        if(hasLibrary)LibraryBuilder.RestoreSources(root);
        Apply(root,data,game);
        if(hasLibrary)LibraryBuilder.Apply(root,data,game);
        if(hasGym)GymnasiumBuilder.Apply(root,data,game);
        if(hasTeaching)TeachingBuilder.Apply(root,data,game);
        BuildingInteriorBuilder.ValidateConfiguration();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("CAFETERIA_GENERATION_COMPLETE");
    }
    public static void GenerateAndBuild(){Generate();CampusBuilder.Build();}
    public static void RestoreSources(Transform root) {
        var old=root.Find(Group);if(!old)return;
        var marker=old.GetComponent<CourtyardRevision>();
        if(marker&&marker.suppressedSources!=null)foreach(var r in marker.suppressedSources)if(r)r.enabled=true;
        UnityEngine.Object.DestroyImmediate(old.gameObject);
    }
    public static void Apply(Transform root,CampusData data,CampusGame game) {
        RestoreSources(root);
        Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        var holder=new GameObject(Group).transform;holder.SetParent(root,false);
        ReplaceOldGeometry(root,holder);
        random=new System.Random(77943);geometry=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Limestone","#d6d3c8",.24f,1);Mat("Grey stone","#9c9f9b",.25f,1);
        Mat("Metal","#424d51",.65f);Mat("Bronze","#827464",.4f);Mat("Stainless","#b9c0bc",.85f);
        Mat("Interior","#ded4ba",.1f,0,0,.25f);Mat("Floor","#b8b3a4",.15f,1);
        Mat("Wood","#a78257",.25f,0,0,.08f);materials["Wood"].SetFloat("_Wood",1);Mat("Seat","#d1bb87",.2f);
        Mat("Glass","#93aebc",.8f,0,1);Mat("Lamp","#ffe2a6",.1f,0,0,1.35f);
        Mat("Skin","#be997a",.05f);Mat("Hair","#383a36",.05f);Mat("Shirt","#657989",.1f);Mat("Cream shirt","#c9c1ac",.1f);
        Mat("Rubber","#353936",.1f);Mat("Leaves","#60794a",.1f);Mat("Leaves light","#8a985d",.1f);Mat("Flower","#e2cf9b",.12f);Mat("Banner","#344d70",.15f);
        var paving=new Material(Shader.Find("Campus/Courtyard")){name="Forecourt paving"};ColorUtility.TryParseHtmlString("#bbb8ad",out var pc);paving.SetColor("_BaseColor",pc);paving.SetFloat("_Surface",1);materials["Paving"]=Save(paving,Folder+"/Materials/Paving.mat");geometry["Paving"]=new DormitoryBuilder.Geometry();
        Building();Forecourt();
        foreach(var pair in geometry.Where(p=>p.Value.vertices.Count>0)) {
            var mesh=Save(pair.Value.Mesh("Cafeteria "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");
            var obj=new GameObject(pair.Key);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];r.shadowCastingMode=pair.Key=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
        Sign(holder,game,"学 生 食 堂",0,8.5f,-.5f,1.05f);
        Sign(holder,game,"Student Cafeteria",0,7.75f,-.5f,.34f);
        Sign(holder,game,"饮食有度\n生活有光\n青春有为",-16.25f,13.8f,-.52f,.34f);
        Sign(holder,game,"Good Food\nHealthy Life\nBright Future",-16.25f,11.5f,-.52f,.2f);
        Sign(holder,game,"以食育人\n以味传情\n共筑美好校园",16.25f,13.8f,-.52f,.32f);
        Sign(holder,game,"Nourish\nConnect\nGrow Together",16.25f,11.5f,-.52f,.2f);
        Sign(holder,game,"美好生活  从一餐开始",0,4.42f,-.25f,.27f);
        Sign(holder,game,"三餐烟火\n滋养更好的你",15.25f,1.02f,-4.65f,.15f);
        Sign(holder,game,"知\n行\n合\n一",-18.5f,2.65f,-2.65f,.18f);
        Sign(holder,game,"勤\n学\n笃\n行",18.5f,2.65f,-2.65f,.18f);
        UpdateMap(data,game);
        if(!game.GetComponent<CafeteriaPreview>())game.gameObject.AddComponent<CafeteriaPreview>();
        EditorUtility.SetDirty(game);Directory.CreateDirectory("QA");
        File.WriteAllText("QA/cafeteria-generation.txt",$"PASS: cafeteria 77943, 36 x 20 m, 3 storeys, {Height} m parapet.\nTimber crown, tall glazed atrium, suspended ring lights, dining scenery, entry canopy, steps, ramp and planting generated.\n{geometry.Count} material batches. All campus routes and entrance approach validated.\n");
    }
    static void Mat(string name,string hex,float smooth,float stone=0,float glass=0,float emission=0) {
        ColorUtility.TryParseHtmlString(hex,out var color);if(glass>0)color.a=.22f;
        var m=new Material(Shader.Find("Campus/Cafeteria")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Stone",stone);m.SetFloat("_Glass",glass);m.SetFloat("_Emission",emission);
        if(glass>0){m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static void Building() {
        Box("Grey stone",0,.35f,10,36,.7f,20);
        // Side dining wings have three levels. The central bay stays open above
        // the entrance, with floor slabs set back behind the glass atrium.
        foreach(float y in new[]{.7f,5.95f,11.2f,16.45f}) {
            foreach(float side in new[]{-1f,1f}) {
                Box("Floor",side*12.5f,y,10,10.6f,.18f,19.8f);
                Box("Limestone",side*12.5f,y+.2f,0,10.9f,.55f,.5f);
            }
            Box("Floor",0,y,13.5f,14.4f,.18f,13);
            Box("Limestone",0,y+.16f,19.94f,36,.45f,.32f);
        }
        Box("Floor",0,.7f,3.5f,14.4f,.18f,7);
        foreach(float side in new[]{-1f,1f}) {
            Box("Grey stone",side*12.5f,16.75f,10,11.3f,.42f,20.2f);
            Box("Limestone",side*12.5f,17.02f,10,11.4f,.14f,20.3f);
        }
        Box("Interior",0,8.5f,17.7f,35.6f,16,.2f);
        Box("Limestone",0,8.7f,20,36,16.9f,.3f);
        for(int side=-1;side<=1;side+=2) {
            float u=side*17.85f;
            Box("Limestone",u,8.5f,18.4f,.3f,16.5f,3.2f);
            foreach(float y in new[]{.65f,5.95f,11.2f,16.45f})Box("Limestone",u,y,8.4f,.3f,.8f,16.8f);
            foreach(float d in new[]{.35f,3.7f,7.1f,10.5f,13.9f,17.2f})Box("Limestone",u,8.5f,d,.3f,16.6f,1.1f);
            for(int floor=0;floor<3;floor++)for(int k=0;k<5;k++) {
                float d=2+k*3.4f,y=3.35f+floor*5.25f;
                Box("Glass",side*17.96f,y,d,.025f,4.35f,2.3f);
                foreach(float edge in new[]{-1.16f,0,1.16f})Box("Metal",side*18.0f,y,d+edge,.075f,4.35f,.06f);
                foreach(float edge in new[]{-2.17f,1.1f,2.17f})Box("Metal",side*18.0f,y+edge,d,.075f,.06f,2.4f);
            }
        }
        foreach(float side in new[]{-1f,1f}) {
            Box("Limestone",side*16.3f,8.7f,-.1f,3.4f,17,.65f);
            Box("Grey stone",side*16.3f,16.35f,-.44f,3.42f,.15f,.03f);
            Box("Limestone",side*7.15f,9.15f,-.15f,1.0f,17.8f,.65f);
            // Warm slats on each side of the central glass, from ground to roof.
            Box("Metal",side*6.18f,9.25f,-.1f,1.4f,17.9f,.22f);
            for(int i=0;i<7;i++)Box("Wood",side*6.18f+(i-3)*.19f,9.25f,-.3f,.085f,17.9f,.36f);
            Box("Wood",side*11.1f,16.0f,-.08f,7.1f,.65f,.3f);
            for(int i=0;i<36;i++)Box("Bronze",side*11.1f+(i-17.5f)*.19f,16,-.26f,.022f,.63f,.04f);
        }
        for(int floor=0;floor<3;floor++) {
            float level=.8f+floor*5.25f,low=1.02f+floor*5.25f,high=5.55f+floor*5.25f;
            Window(-14.55f,-7.7f,low,high,.1f);Window(7.7f,14.55f,low,high,.1f);
            foreach(float side in new[]{-1f,1f}) {
                Box("Interior",side*12.5f,high-.04f,8.7f,10.6f,.1f,17);
                for(float u=8.7f;u<14;u+=2.65f)for(int row=0;row<3;row++) {
                    float d=2.1f+row*3.4f;Table(side*u,level,d,(row+floor+(int)u)%3==0);
                    Box("Lamp",side*u,high-.12f,d,.12f,.018f,2.6f);
                }
                for(float d=1.2f;d<16;d+=4)Box("Lamp",side*12.5f,high-.12f,d,9,.018f,.08f);
                if(floor>0) {
                    Box("Glass",side*11.1f,level+.64f,.52f,6.7f,1.02f,.025f);
                    Box("Stainless",side*11.1f,level+1.17f,.49f,6.7f,.045f,.045f);
                }
            }
            // These furnishings are exterior window scenery, not the separate playable interior.
            for(float u=-4;u<=4;u+=2.7f)for(int row=0;row<2;row++)Table(u,level,8.6f+row*3.7f,(floor+row)%2==0);
            Box("Wood",0,level+.6f,15.8f,26,1.2f,1.2f);
            Box("Stainless",0,level+1.23f,15.8f,26,.06f,1.3f);
            if(floor>0) {
                Box("Glass",0,level+.65f,7.1f,10.7f,1.1f,.025f);
                Box("Stainless",0,level+1.22f,7.05f,10.7f,.045f,.045f);
                foreach(float u in new[]{-4.8f,4.8f})PottedPlant(u,level,6.95f);
            }
        }
        // Continuous central glazing and oversized timber soffit.
        Window(-5.45f,5.45f,5.92f,18.22f,-.06f);
        foreach(float y in new[]{8.1f,10.3f,12.5f,14.7f,16.9f})Box("Metal",0,y,-.14f,10.9f,.07f,.12f);
        Box("Wood",0,18.58f,2.0f,19.5f,.26f,8.5f);
        Box("Grey stone",0,18.77f,2.0f,19.7f,.13f,8.65f);
        for(int i=0;i<76;i++)Box("Bronze",-9.45f+i*.25f,18.435f,2,.018f,.025f,8.3f);
        foreach(float u in new[]{-3.2f,3.2f})Box("Lamp",u,18.41f,-1.1f,.065f,.018f,1.35f);
        Box("Grey stone",0,18.45f,13,14.4f,.3f,14);
        Box("Interior",0,18.29f,8,10.9f,.08f,16);
        RingLight(15.6f,2.6f,5.4f);RingLight(10.35f,1.85f,4.6f);
        // Framed doors, transom and pull handles remain aligned with the E entrance.
        Window(-5.45f,-2.7f,.78f,5.6f,.1f);Window(2.7f,5.45f,.78f,5.6f,.1f);
        Window(-2.7f,2.7f,4.0f,5.6f,.1f);
        Box("Glass",0,2.38f,.1f,5.4f,3.2f,.025f);
        for(int i=0;i<4;i++) {
            float u=(i-1.5f)*1.35f;
            foreach(float edge in new[]{-.65f,.65f})Box("Metal",u+edge,2.38f,.03f,.065f,3.2f,.12f);
            foreach(float y in new[]{.83f,4})Box("Metal",u,y,.03f,1.35f,.09f,.12f);
            Box("Stainless",u+(i%2==0?.42f:-.42f),2.1f,-.1f,.035f,.8f,.04f);
        }
        Box("Interior",0,4.46f,-.15f,9.5f,.72f,.05f);
        Box("Metal",0,5.8f,-1.85f,18.8f,.22f,4);
        Box("Wood",0,5.66f,-1.85f,18.45f,.055f,3.8f);
        for(float u=-7.5f;u<=7.5f;u+=2.5f)foreach(float d in new[]{-.5f,-1.7f,-2.9f})geometry["Lamp"].Disc(V(u,5.625f,d),Vector3.right,Vector3.forward,.065f,12);
        foreach(float u in new[]{-5.4f,5.4f})PottedPlant(u,.78f,-.7f);
        for(int step=0;step<6;step++) {
            float y=.18f+(step+1)*.095f,d=-1.8f+step*.3f;
            Box("Grey stone",0,(y+.14f)/2,(d+.05f)/2,13.9f,y-.14f,.05f-d);
            Box("Limestone",0,y+.012f,d+.025f,13.85f,.023f,.06f);
        }
        var a=V(7,.75f,-1.8f);var b=V(15,.18f,-1.8f);var c=V(15,.18f,-.1f);var d0=V(7,.75f,-.1f);
        geometry["Grey stone"].Quad(a,b,c,d0);
        foreach(float depth in new[]{-1.8f,-.1f}) {
            Beam("Stainless",V(7,1.65f,depth),V(15,1.08f,depth),.045f);
            for(int i=0;i<=8;i++){float y=.75f-i*.07125f;Beam("Stainless",V(7+i,y,depth),V(7+i,y+.9f,depth),.035f);}
        }
    }
    static void RingLight(float y,float radius,float d) {
        geometry["Lamp"].Ring(V(0,y,d),Vector3.right,Vector3.forward,radius,.09f,64);
        for(int i=0;i<64;i++){float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Beam("Bronze",V(Mathf.Cos(a)*radius,y+.065f,d+Mathf.Sin(a)*radius),V(Mathf.Cos(b)*radius,y+.065f,d+Mathf.Sin(b)*radius),.045f);}
        foreach(float a in new[]{0f,2.094f,4.189f})Beam("Metal",V(Mathf.Cos(a)*radius,y,d+Mathf.Sin(a)*radius),V(Mathf.Cos(a)*radius,18.3f,d+Mathf.Sin(a)*radius),.012f);
    }
    static void Leaf(Vector3 p,float size,int index,string material=null) {
        float a=index*2.399f;var u=new Vector3(Mathf.Cos(a),.2f,Mathf.Sin(a))*size;var v=new Vector3(-u.z,.05f,u.x)*.42f;
        geometry[material??(index%3==0?"Leaves light":"Leaves")].Quad(p-u,p+v,p+u,p-v);
    }
    static void PottedPlant(float u,float y,float d) {
        Box("Grey stone",u,y+.25f,d,.55f,.5f,.55f);
        Beam("Wood",V(u,y+.45f,d),V(u,y+1.7f,d),.035f);
        for(int i=0;i<55;i++){float a=i*2.399f;Leaf(V(u+Mathf.Cos(a)*.3f,y+.7f+(i%9)*.12f,d+Mathf.Sin(a)*.3f),.15f,i);}
    }
    static void Window(float left,float right,float low,float high,float d) {
        float width=right-left,mid=(left+right)/2;Box("Glass",mid,(low+high)/2,d,width,high-low,.025f);
        int n=Mathf.CeilToInt(width/1.4f);
        for(int i=0;i<=n;i++)Box("Metal",Mathf.Lerp(left,right,(float)i/n),(low+high)/2,d-.035f,.07f,high-low,.1f);
        foreach(float y in new[]{low,low+(high-low)*.32f,high})Box("Metal",mid,y,d-.035f,width,.065f,.1f);
        Box("Grey stone",mid,low-.05f,d-.12f,width+.08f,.1f,.33f);
    }
    static void Table(float u,float y,float d,bool occupied) {
        Box("Wood",u,y+.75f,d,1.7f,.06f,.78f);
        foreach(float side in new[]{-.58f,.58f})Box("Metal",u+side,y+.36f,d,.045f,.72f,.045f);
        foreach(float side in new[]{-.72f,.72f})foreach(float along in new[]{-.48f,.48f}) {
            float z=d+side,x=u+along;Box("Seat",x,y+.43f,z,.38f,.055f,.4f);
            Box("Seat",x,y+.7f,z+Mathf.Sign(side)*.17f,.38f,.45f,.045f);
            foreach(float a in new[]{-.13f,.13f})Box("Metal",x+a,y+.21f,z,.025f,.42f,.025f);
        }
        if(!occupied)return;
        string shirt=random.Next(2)==0?"Shirt":"Cream shirt";
        Box(shirt,u-.48f,y+.84f,d+.68f,.32f,.5f,.24f);
        Sphere("Skin",V(u-.48f,y+1.2f,d+.65f),new Vector3(.105f,.14f,.1f));
        Sphere("Hair",V(u-.48f,y+1.29f,d+.69f),new Vector3(.109f,.073f,.105f));
        Beam(shirt,V(u-.63f,y+1.0f,d+.64f),V(u-.68f,y+.79f,d+.25f),.085f);
        Beam(shirt,V(u-.33f,y+1.0f,d+.64f),V(u-.28f,y+.79f,d+.25f),.085f);
        Box("Interior",u-.48f,y+.8f,d+.1f,.28f,.025f,.22f);
    }
    static void Sphere(string m,Vector3 center,Vector3 scale) {
        Vector3 At(float latitude,float longitude)=>center+Vector3.Scale(new Vector3(Mathf.Cos(latitude)*Mathf.Cos(longitude),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(longitude)),scale);
        for(int y=0;y<6;y++)for(int x=0;x<10;x++) {
            float a=-Mathf.PI/2+y*Mathf.PI/6,b=a+Mathf.PI/6,c=x*Mathf.PI/5,d=c+Mathf.PI/5;
            geometry[m].Quad(At(a,c),At(a,d),At(b,d),At(b,c));
        }
    }
    static void Forecourt() {
        Box("Paving",0,.145f,-2.65f,40,.05f,5.1f);
        foreach(float u in new[]{-9.8f,9.8f})Box("Grey stone",u,.173f,-3.25f,.22f,.012f,3.8f);
        for(float u=-18;u<=18;u+=.9f)Box("Grey stone",u,.21f,-5.14f,.88f,.18f,.18f);
        // Leave the central arrival path open at the existing west-side road.
        for(float u=-7;u<=7;u+=.9f)Box("Paving",u,.178f,-5.14f,.9f,.018f,.22f);
        foreach(float u in new[]{-17f,17f}) {
            Box("Grey stone",u,.35f,-2.2f,2.4f,.4f,3.0f);
            for(int i=0;i<26;i++) {
                var p=V(u+((float)random.NextDouble()-.5f)*2,.53f,-2.2f+((float)random.NextDouble()-.5f)*2.6f);
                for(int leaf=0;leaf<28;leaf++) {
                    float angle=(float)random.NextDouble()*Mathf.PI*2;
                    var center=p+new Vector3(((float)random.NextDouble()-.5f)*.45f,(float)random.NextDouble()*.3f,((float)random.NextDouble()-.5f)*.45f);
                    var axis=new Vector3(Mathf.Cos(angle),.3f,Mathf.Sin(angle))*.09f;var across=new Vector3(-axis.z,.035f,axis.x)*.5f;
                    geometry[leaf%3==0?"Leaves light":"Leaves"].Quad(center-axis,center+across,center+axis,center-across);
                }
            }
        }
        // A row of bicycles, with real open wheels and thin frames.
        for(int i=0;i<5;i++)Bike(-11.7f+i*.75f,-3.15f,i);
        Sphere("Grey stone",V(15.25f,.95f,-4),new Vector3(.58f,.8f,1.15f));
        foreach(float u in new[]{-18.5f,18.5f}) {
            Box("Metal",u,2.1f,-2.5f,.075f,3.85f,.075f);
            Box("Metal",u,4.05f,-2.5f,.65f,.1f,.55f);Box("Lamp",u,3.91f,-2.5f,.24f,.19f,.24f);
            Box("Banner",u,2.65f,-2.56f,.65f,1.5f,.06f);
            Tree(u,-1.8f);
        }
        for(int i=0;i<4;i++)Bike(9.4f+i*.75f,-3.15f,i);
        foreach(float u in new[]{-16.5f,17.6f}) {
            for(int i=0;i<5;i++)Box("Wood",u,.52f,-4.0f+(i-2)*.09f,2.2f,.045f,.075f);
            foreach(float dx in new[]{-.8f,.8f})Box("Metal",u+dx,.32f,-4.0f,.055f,.4f,.5f);
            for(int i=0;i<45;i++){float a=i*2.399f;Leaf(V(u+Mathf.Cos(a)*.8f,.84f+(i%4)*.055f,-2.2f+Mathf.Sin(a)),.06f,i,"Flower");}
        }
        for(int i=0;i<10;i++)Box("Metal",6+i*.085f,.179f,-4.8f,.03f,.012f,.3f);
    }
    static void Tree(float u,float d) {
        var bottom=V(u,.2f,d);var top=V(u+.08f,5.4f,d);Beam("Wood",bottom,top,.15f);
        for(int branch=0;branch<8;branch++) {
            float a=branch*2.399f;var tip=top+new Vector3(Mathf.Cos(a)*1.4f,Mathf.Sin(branch)*.8f,Mathf.Sin(a)*1.4f);
            Beam("Wood",Vector3.Lerp(bottom,top,.55f),tip,.06f);
            for(int i=0;i<100;i++){var offset=new Vector3(((float)random.NextDouble()-.5f)*2,((float)random.NextDouble()-.5f)*1.6f,((float)random.NextDouble()-.5f)*2);Leaf(tip+offset,.2f,i);}
        }
    }
    static void Bike(float u,float d,int index) {
        var a=V(u,.52f,d-.57f);var b=V(u,.52f,d+.57f);
        foreach(var p in new[]{a,b}) {
            geometry["Rubber"].Ring(p,Vector3.right,Vector3.up,.34f,.035f,20);
            geometry["Stainless"].Ring(p,Vector3.right,Vector3.up,.3f,.015f,20);
            for(int spoke=0;spoke<8;spoke++){float angle=spoke*Mathf.PI/4;Beam("Stainless",p,p+new Vector3(Mathf.Cos(angle)*.29f,Mathf.Sin(angle)*.29f,0),.006f);}
        }
        var crank=V(u,.45f,d-.05f);var seat=V(u,.94f,d-.23f);var steer=V(u,1.04f,d+.42f);string m=index%2==0?"Metal":"Bronze";
        foreach(var pair in new[]{new[]{a,seat},new[]{a,crank},new[]{crank,seat},new[]{seat,steer},new[]{crank,steer},new[]{b,steer}})Beam(m,pair[0],pair[1],.03f);
        Box("Rubber",u,.99f,d-.25f,.19f,.05f,.24f);Box("Metal",u,1.08f,d+.4f,.5f,.025f,.025f);
    }
    static void Sign(Transform holder,CampusGame game,string text,float u,float y,float d,float size) {
        var obj=new GameObject(text.Replace('\n',' '));obj.transform.SetParent(holder,false);
        obj.transform.position=game.MapToWorld(V(u,y,d));obj.transform.rotation=Quaternion.LookRotation(Vector3.left,Vector3.up);
        // Compensate the imported root's negative X scale: text must read normally.
        obj.transform.localScale=new Vector3(-1,1,-1);
        var mesh=obj.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;
        mesh.characterSize=size/10;mesh.fontSize=96;mesh.fontStyle=FontStyle.Bold;mesh.color=text=="学 生 食 堂"||text=="Student Cafeteria"?new Color(.9f,.76f,.52f):text.Contains("\n行")?new Color(.88f,.88f,.8f):new Color(.19f,.23f,.25f);mesh.lineSpacing=1.1f;
        string path=Folder+"/Materials/Wall lettering.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material)material=Save(new Material(Shader.Find("Campus/WallLettering")){name="Wall lettering"},path);
        obj.AddComponent<CafeteriaSign>().surface=material;
    }
    static void UpdateMap(CampusData data,CampusGame game) {
        var feature=data.features.Single(f=>f.id==77943);feature.points=new[]{P(183,-10),P(203,-10),P(203,26),P(183,26)};
        var buildings=new HashSet<string>(data.features.Select(f=>f.name));
        data.obstacles=data.obstacles.Where(o=>!o.name.StartsWith("Cafeteria ")&&(buildings.Contains(o.name)||!InMask(o))).ToArray();
        foreach(var o in data.obstacles.Where(o=>o.name==feature.name)){o.points=feature.points;o.minX=183;o.maxX=203;o.minZ=-10;o.maxZ=26;}
        var extra=new List<CampusObstacle>{new CampusObstacle{name="Cafeteria entrance steps",minX=181.15f,maxX=183,minZ=1,maxZ=15},new CampusObstacle{name="Cafeteria display ramp",minX=181.15f,maxX=183,minZ=15,maxZ=23}};
        foreach(float u in new[]{-17f,17f})extra.Add(new CampusObstacle{name="Cafeteria planter",minX=179.3f,maxX=182.3f,minZ=Center+u-1.2f,maxZ=Center+u+1.2f});
        extra.Add(new CampusObstacle{name="Cafeteria bike rack",minX=178.9f,maxX=180.9f,minZ=-4.1f,maxZ=-.4f});
        extra.Add(new CampusObstacle{name="Cafeteria engraved stone",minX=178.8f,maxX=179.2f,minZ=22.1f,maxZ=24.4f});
        extra.Add(new CampusObstacle{name="Cafeteria east bike rack",minX=178.9f,maxX=180.9f,minZ=17,maxZ=20.6f});
        foreach(float u in new[]{-16.5f,17.6f})extra.Add(new CampusObstacle{name="Cafeteria bench",minX=178.75f,maxX=179.25f,minZ=Center+u-1.1f,maxZ=Center+u+1.1f});
        foreach(float u in new[]{-18.5f,18.5f}) {
            extra.Add(new CampusObstacle{name="Cafeteria tree",minX=181.05f,maxX=181.35f,minZ=Center+u-.15f,maxZ=Center+u+.15f});
            extra.Add(new CampusObstacle{name="Cafeteria lamp",minX=180.4f,maxX=180.6f,minZ=Center+u-.1f,maxZ=Center+u+.1f});
        }
        data.obstacles=data.obstacles.Concat(extra).ToArray();
        data.routes=data.routes.Where(r=>r.name!="cafeteria-approach").Concat(new[]{new CampusRoute{name="cafeteria-approach",points=new[]{P(174.5f,8),P(177,8),P(180.4f,8)}}}).ToArray();
        if(CampusCollision.Blocked(174.5f,8,data.obstacles))throw new Exception("Cafeteria arrival blocked");
        if(!CampusCollision.Blocked(193,8,data.obstacles))throw new Exception("Cafeteria building collision missing");
        foreach(var route in data.routes) {
            var p=new Vector3(route.points[0].x,.23f,route.points[0].z);
            for(int i=1;i<route.points.Length;i++) {
                var goal=new Vector3(route.points[i].x,.23f,route.points[i].z);
                for(int j=0;j<6000&&Vector3.Distance(p,goal)>.05f;j++)p=CampusCollision.Move(p,Vector3.ClampMagnitude(goal-p,.15f),data.obstacles);
                if(Vector3.Distance(p,goal)>.1f)throw new Exception("Cafeteria revision obstructs "+route.name+" at "+p);
            }
        }
        var root=GameObject.Find("Camera collision surfaces").transform;
        foreach(var collider in root.GetComponentsInChildren<BoxCollider>())if(collider.name==feature.name+" wall")UnityEngine.Object.DestroyImmediate(collider.gameObject);
        for(int i=0;i<4;i++) {
            var a=feature.points[i];var b=feature.points[(i+1)%4];var p=game.MapToWorld(new Vector3(a.x,Height/2,a.z));var q=game.MapToWorld(new Vector3(b.x,Height/2,b.z));
            var obj=new GameObject(feature.name+" wall"){layer=8};obj.transform.SetParent(root,false);obj.transform.position=(p+q)/2;obj.transform.rotation=Quaternion.LookRotation(q-p);
            obj.AddComponent<BoxCollider>().size=new Vector3(.25f,Height,Vector3.Distance(p,q));
        }
        string path=Folder+"/CampusMap.json";File.WriteAllText(path,JsonUtility.ToJson(data,true));AssetDatabase.ImportAsset(path);game.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(path);
    }
    static bool InMask(CampusObstacle o) {
        if(o.segment!=null&&o.segment.Length==2)return o.segment.All(p=>Mask.Contains(new Vector2(p.x,p.z)));
        if(o.points!=null&&o.points.Length>2)return o.points.All(p=>Mask.Contains(new Vector2(p.x,p.z)));
        return Mask.Contains(new Vector2((o.minX+o.maxX)/2,(o.minZ+o.maxZ)/2));
    }
    static void ReplaceOldGeometry(Transform root,Transform holder) {
        var suppressed=new List<Renderer>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) {
            var r=filter.GetComponent<MeshRenderer>();if(!r||!r.enabled||!filter.sharedMesh)continue;
            // New native dorms are far outside this area; retained imported batches
            // may contain both dorms and the old cafeteria and must also be clipped.
            var source=filter.sharedMesh;var b=source.bounds;
            if(b.max.x<Mask.xMin||b.min.x>Mask.xMax||b.max.z<Mask.yMin||b.min.z>Mask.yMax||b.max.y<.2f)continue;
            var path=AssetDatabase.GetAssetPath(source);
            if(!(path.StartsWith("Assets/LivingRevision/Meshes/")||path.Contains("Retained-")||path.Contains("Surroundings-")))continue;
            var mesh=Clip(source);if(!mesh)continue;
            suppressed.Add(r);r.enabled=false;if(mesh.vertexCount==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}
            string id=AssetDatabase.AssetPathToGUID(path);mesh=Save(mesh,Folder+"/Meshes/Preserved-"+id+".asset");
            var obj=new GameObject("Preserved surroundings "+id);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterials=r.sharedMaterials;renderer.shadowCastingMode=r.shadowCastingMode;
        }
        holder.gameObject.AddComponent<CourtyardRevision>().suppressedSources=suppressed.ToArray();
    }
    struct Vertex {public Vector3 p,n;public Vector2 uv;public Color c;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
    static float Distance(Vertex v,int plane)=>plane==0?v.p.x-Mask.xMin:plane==1?Mask.xMax-v.p.x:plane==2?v.p.z-Mask.yMin:Mask.yMax-v.p.z;
    static List<Vertex> Cut(List<Vertex> poly,int plane,bool inside) {
        var output=new List<Vertex>();for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];float da=Distance(a,plane),db=Distance(b,plane);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)output.Add(a);if(ia!=ib)output.Add(Vertex.Lerp(a,b,da/(da-db)));}return output;
    }
    static Mesh Clip(Mesh source) {
        var p=source.vertices;var n=source.normals;var uv=source.uv;var colors=source.colors;var tri=source.triangles;var output=new List<Vertex>();bool changed=false;
        Vertex Vtx(int i)=>new Vertex{p=p[i],n=n.Length==p.Length?n[i]:Vector3.up,uv=uv.Length==p.Length?uv[i]:Vector2.zero,c=colors.Length==p.Length?colors[i]:Color.white};
        void Append(List<Vertex> poly){for(int j=1;j<poly.Count-1;j++){output.Add(poly[0]);output.Add(poly[j]);output.Add(poly[j+1]);}}
        for(int i=0;i<tri.Length;i+=3) {
            var a=Vtx(tri[i]);var b=Vtx(tri[i+1]);var c=Vtx(tri[i+2]);
            if(Mathf.Max(a.p.y,b.p.y,c.p.y)<.2f||Mathf.Max(a.p.x,b.p.x,c.p.x)<Mask.xMin||Mathf.Min(a.p.x,b.p.x,c.p.x)>Mask.xMax||Mathf.Max(a.p.z,b.p.z,c.p.z)<Mask.yMin||Mathf.Min(a.p.z,b.p.z,c.p.z)>Mask.yMax){output.Add(a);output.Add(b);output.Add(c);continue;}
            var remain=new List<Vertex>{a,b,c};var outside=new List<Vertex>();
            for(int plane=0;plane<4&&remain.Count>0;plane++){var part=Cut(remain,plane,false);for(int j=1;j<part.Count-1;j++){outside.Add(part[0]);outside.Add(part[j]);outside.Add(part[j+1]);}remain=Cut(remain,plane,true);}
            if(remain.Count>=3){changed=true;output.AddRange(outside);}else Append(new List<Vertex>{a,b,c});
        }
        if(!changed)return null;
        var mesh=new Mesh{name="Cafeteria preserved surroundings",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.c).ToList());mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateBounds();return mesh;
    }
}
