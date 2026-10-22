using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Reference gym exterior. Metres; facade faces south (+map Z).
// The separately loaded gymnasium interior is deliberately left untouched.
public static class GymnasiumBuilder
{
    const string Folder="Assets/GymnasiumRevision";
    public const string Group="体育馆 · 曲面屋顶与玻璃大厅";
    const float Front=-9,Center=-158,Width=56,Depth=33,Height=24.6f;
    static readonly Rect Mask=Rect.MinMaxRect(-188.8f,-46,-120,8);
    static Dictionary<string,DormitoryBuilder.Geometry> geometry;
    static Dictionary<string,Material> materials;
    static System.Random random;
    static MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
    static Vector3 V(float u,float y,float d)=>new Vector3(Center+u,y,Front-d);
    static void Box(string m,float u,float y,float d,float w,float h,float depth)=>geometry[m].Box(V(u,y,d),Vector3.right,Vector3.up,Vector3.back,new Vector3(w,h,depth));
    static void Beam(string m,Vector3 a,Vector3 b,float width)=>geometry[m].Beam(a,b,width);
    static T Save<T>(T value,string path)where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    [MenuItem("Campus/Apply Reference Gymnasium")]
    public static void Generate() {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        var root=GameObject.Find("校园环境").transform;
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        bool hasTeaching=root.Find(TeachingBuilder.Group);
        if(hasTeaching)TeachingBuilder.RestoreSources(root);
        Apply(root,data,game);
        if(hasTeaching)TeachingBuilder.Apply(root,data,game);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("GYMNASIUM_GENERATION_COMPLETE");
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
        var holder=new GameObject(Group).transform;holder.SetParent(root,false);ReplaceOldGeometry(root,holder);
        random=new System.Random(77923);geometry=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Limestone","#d7d2c5",.23f,1);Mat("Grey stone","#969b99",.25f,1);
        Mat("Metal","#37464d",.65f);Mat("Bronze","#796048",.4f);Mat("Stainless","#b9c0bc",.8f);
        Mat("Interior","#e2d6bb",.12f,0,0,.2f);Mat("Floor","#bcb6a7",.16f,1);
        Mat("Wood","#a78055",.25f,0,0,.03f);materials["Wood"].SetFloat("_Wood",1);
        Mat("Glass","#9eb8c5",.85f,0,1);Mat("Lamp","#ffe4b1",.1f,0,0,1.3f);
        Mat("Rubber","#323b3c",.1f);Mat("Leaves","#607a48",.1f);Mat("Leaves light","#8e9d62",.1f);
        Mat("Soil","#665a43",.1f);Mat("Banner","#354e6b",.1f);Mat("Flower","#c7c1dc",.1f);
        var paving=new Material(Shader.Find("Campus/Courtyard")){name="Gym forecourt paving"};ColorUtility.TryParseHtmlString("#bcb9ae",out var pc);paving.SetColor("_BaseColor",pc);paving.SetFloat("_Surface",1);materials["Paving"]=Save(paving,Folder+"/Materials/Paving.mat");geometry["Paving"]=new DormitoryBuilder.Geometry();
        Building();Forecourt();
        foreach(var pair in geometry.Where(p=>p.Value.vertices.Count>0)) {
            var mesh=Save(pair.Value.Mesh("Gymnasium "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");
            var obj=new GameObject(pair.Key);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];r.shadowCastingMode=pair.Key=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
        Sign(holder,game,"体 育 馆",0,10.6f,-.48f,1.4f,true);
        Sign(holder,game,"Gymnasium",0,9.35f,-.48f,.62f,true);
        Sign(holder,game,"运动\n让青春更美好\n让未来更强大",-25.7f,16,-.57f,.42f);
        Sign(holder,game,"More Than Sports\nA Stronger You\nA Brighter Tomorrow",-25.7f,12.9f,-.57f,.23f);
        Sign(holder,game,"在运动中\n遇见更好的自己",25.7f,16,-.57f,.42f);
        Sign(holder,game,"Through Sports\nDiscover\nA Better You",25.7f,12.9f,-.57f,.25f);
        Sign(holder,game,"强健体魄   追求卓越",0,5.15f,-.17f,.46f);
        Sign(holder,game,"A Healthier Self   A Brighter Tomorrow",0,4.65f,-.17f,.23f);
        Sign(holder,game,"崇\n德\n笃\n行",-25,3.1f,-9.15f,.22f,true);
        Sign(holder,game,"强\n体\n励\n志",25,3.1f,-9.15f,.22f,true);
        Sign(holder,game,"运动点亮生活\n奋斗成就未来",22,1.2f,-13.85f,.25f);
        UpdateMap(data,game);
        var entry=AssetDatabase.LoadAssetAtPath<BuildingEntranceDefinition>("Assets/Interiors/Entrances/gymnasium.asset");
        if(!entry)throw new Exception("Gymnasium entrance configuration missing");
        entry.approachMapPosition=V(0,.23f,-3.8f);entry.returnMapPosition=V(0,.23f,-5.2f);entry.enterYaw=180;EditorUtility.SetDirty(entry);
        if(!game.GetComponent<GymnasiumPreview>())game.gameObject.AddComponent<GymnasiumPreview>();
        EditorUtility.SetDirty(game);BuildingInteriorBuilder.ValidateConfiguration();
        Directory.CreateDirectory("QA");File.WriteAllText("QA/gymnasium-generation.txt","PASS: gym 77923, 56 x 33 m. Curved roof, glazed atrium, limestone wings, timber fins, entrance forecourt. All walking routes and doorway configurations validated. Independent gym interior preserved.\n");
    }
    static void Mat(string name,string hex,float smooth,float stone=0,float glass=0,float emission=0) {
        ColorUtility.TryParseHtmlString(hex,out var color);if(glass>0)color.a=.22f;
        var m=new Material(Shader.Find("Campus/Cafeteria")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Stone",stone);m.SetFloat("_Glass",glass);m.SetFloat("_Emission",emission);
        if(glass>0){m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static void Building() {
        Box("Grey stone",0,.42f,16.5f,Width,.84f,Depth);
        Box("Limestone",0,10.15f,33,Width,19.5f,.45f);
        Box("Grey stone",0,19.9f,21,Width,.25f,24);
        foreach(float side in new[]{-1f,1f}) {
            float u=side*20;
            foreach(float y in new[]{.95f,7.25f,13.55f,19.85f}) {
                Box("Floor",u,y,16.5f,16,.2f,32.5f);Box("Limestone",u,y+.15f,0,16,.6f,.55f);
                Box("Limestone",side*27.9f,y+.2f,16.5f,.3f,.8f,33);
            }
            Box("Limestone",side*25.7f,10.2f,-.18f,4.6f,20,.7f);
            Box("Grey stone",side*25.7f,19.3f,-.55f,4.6f,.15f,.04f);
            Box("Limestone",side*12.5f,10.5f,-.2f,1.35f,20.6f,.8f);
            for(int floor=0;floor<3;floor++) {
                float low=1.05f+floor*6.3f,high=6.95f+floor*6.3f;
                Window(side<0?-23.3f:13.2f,side<0?-13.2f:23.3f,low,high,.12f);
                Box("Interior",u,high-.05f,12,15,.1f,23);
                for(int j=0;j<4;j++) {
                    float x=side*(14.7f+j*2.2f);
                    // Shallow display scenery behind the facade, not the playable interior.
                    Treadmill(x,low,3.8f);Bench(x,6.4f,low);
                }
                foreach(float d in new[]{2.8f,7f,12f,19f})Box("Lamp",u,high-.14f,d,12,.03f,.09f);
                for(int bay=0;bay<5;bay++) {
                    float d=3.5f+bay*6.2f;
                    Box("Glass",side*28,low+2.8f,d,.025f,5.3f,4.8f);
                    foreach(float t in new[]{-2.4f,0,2.4f})Box("Metal",side*28.03f,low+2.8f,d+t,.1f,5.4f,.07f);
                    foreach(float t in new[]{.15f,3.7f,5.45f})Box("Metal",side*28.03f,low+t,d,.1f,.07f,4.9f);
                }
            }
            foreach(float d in new[]{.5f,6.6f,12.8f,19f,25.2f,32.5f})Box("Limestone",side*27.9f,10.2f,d,.4f,19.5f,1.2f);
            for(int i=0;i<8;i++)Box("Wood",side*11.6f+(i-3.5f)*.19f,11.1f,-.52f,.095f,20.5f,.5f);
            for(int i=0;i<5;i++)Box("Wood",side*22.8f+(i-2)*.19f,13.4f,-.31f,.09f,12.2f,.36f);
        }
        Box("Floor",0,.95f,15,22.4f,.2f,30);
        Box("Interior",0,11.5f,20,22,21,.25f);
        Box("Grey stone",0,22.32f,21.5f,22.4f,.25f,23);
        foreach(float side in new[]{-1f,1f})Box("Limestone",side*11.1f,21.1f,21.5f,.3f,2.4f,23);
        Box("Metal",0,22.1f,-.2f,21.6f,.36f,.18f);
        Window(-10.8f,10.8f,6.7f,21.9f,-.22f);
        foreach(float y in new[]{9.1f,11.7f,14.3f,16.9f,19.5f})Box("Metal",0,y,-.3f,21.6f,.085f,.14f);
        Window(-10.8f,-3.6f,1.02f,6.6f,-.05f);Window(3.6f,10.8f,1.02f,6.6f,-.05f);Window(-3.6f,3.6f,4.15f,6.6f,-.05f);
        Box("Interior",0,4.97f,.03f,11.8f,1.3f,.12f);
        for(int i=0;i<4;i++) {
            float u=(i-1.5f)*1.8f;Box("Glass",u,2.58f,-.08f,1.76f,3.12f,.025f);
            foreach(float x in new[]{-.89f,.89f})Box("Metal",u+x,2.58f,-.14f,.08f,3.12f,.12f);
            foreach(float y in new[]{1.02f,4.15f})Box("Metal",u,y,-.14f,1.8f,.08f,.12f);
            Box("Stainless",u+(i%2==0?.65f:-.65f),2.5f,-.24f,.04f,.85f,.05f);
        }
        foreach(float y in new[]{7.25f,13.55f}) {
            Box("Floor",0,y,8.5f,21.5f,.22f,8);
            Box("Stainless",0,y+1.1f,4.4f,21.5f,.045f,.045f);
            Box("Glass",0,y+.6f,4.4f,21.5f,1,.025f);
            for(float u=-10;u<=10;u+=1.25f)Box("Metal",u,y+.6f,4.35f,.035f,1.1f,.04f);
            Box("Lamp",0,y-.15f,4.4f,21.3f,.035f,.08f);
            foreach(float u in new[]{-9.3f,9.3f})Planter(u,y,5.1f,.8f,.8f,true);
        }
        foreach(float d in new[]{3f,9f,15f})Box("Lamp",0,21.7f,d,17,.04f,.12f);
        Roof();Runner();
        Box("Metal",0,6.65f,-1.75f,26,.23f,4.5f);Box("Wood",0,6.49f,-1.75f,25.7f,.06f,4.2f);
        for(float u=-11;u<=11;u+=2.2f)foreach(float d in new[]{-.5f,-3.25f})geometry["Lamp"].Disc(V(u,6.45f,d),Vector3.right,Vector3.forward,.085f,12);
        for(int i=0;i<8;i++) {
            float d=-2.65f+i*.34f,y=.18f+(i+1)*.105f;
            Box("Grey stone",0,(y+.12f)/2,d/2,24,y-.12f,-d+.12f);Box("Limestone",0,y+.01f,d,23.95f,.022f,.07f);
        }
        foreach(float u in new[]{-12.4f,12.4f})Box("Limestone",u,.65f,-1.3f,.6f,1.05f,2.8f);
        foreach(float u in new[]{-9.8f,9.8f})Planter(u,1,-.7f,.8f,.8f,true);
    }
    static float RoofY(float u)=>22.3f+2.0f*Mathf.Pow(u/27.5f,2);
    static void Roof() {
        // Continuous parabolic sweep: raised tips, thin metal edge, warm timber soffit.
        for(int k=0;k<110;k++) {
            float a=-27.5f+k*.5f,b=a+.5f,ya=RoofY(a),yb=RoofY(b);
            geometry["Wood"].Quad(V(a,ya,-3.1f),V(b,yb,-3.1f),V(b,yb,10),V(a,ya,10));
            geometry["Metal"].Quad(V(a,ya+.23f,10),V(b,yb+.23f,10),V(b,yb+.23f,-3.1f),V(a,ya+.23f,-3.1f));
            geometry["Grey stone"].Quad(V(a,ya,-3.12f),V(a,ya+.24f,-3.12f),V(b,yb+.24f,-3.12f),V(b,yb,-3.12f));
            Beam("Bronze",V(a,ya-.018f,-3.08f),V(a,ya-.018f,9.95f),.025f);
            if(k%10==0)Beam("Lamp",V(a,ya-.035f,-1.8f),V(a,ya-.035f,1.2f),.065f);
        }
        foreach(float u in new[]{-27.5f,27.5f})Box("Metal",u,RoofY(u)+.1f,3.45f,.06f,.25f,13.1f);
        foreach(float u in new[]{-12.7f,12.7f})Box("Limestone",u,11.9f,.35f,.55f,22.4f,.65f);
    }
    static void Runner() {
        // Flat dark sports emblem mounted on the atrium glazing.
        var hip=V(-.4f,16.25f,-.4f);var chest=V(.6f,17.75f,-.4f);
        Beam("Banner",hip,chest,.48f);Sphere("Banner",V(1.05f,18.5f,-.42f),new Vector3(.36f,.4f,.055f));
        Beam("Banner",chest,V(1.7f,17.15f,-.4f),.26f);Beam("Banner",V(1.7f,17.15f,-.4f),V(2.8f,17.65f,-.4f),.24f);
        Beam("Banner",chest,V(-.6f,17.9f,-.4f),.26f);Beam("Banner",V(-.6f,17.9f,-.4f),V(-1.6f,17.15f,-.4f),.24f);
        Beam("Banner",hip,V(1.2f,15.7f,-.4f),.34f);Beam("Banner",V(1.2f,15.7f,-.4f),V(.5f,14.4f,-.4f),.28f);
        Beam("Banner",hip,V(-1.7f,15.3f,-.4f),.34f);Beam("Banner",V(-1.7f,15.3f,-.4f),V(-2.9f,14.5f,-.4f),.28f);
    }
    static void Treadmill(float u,float y,float d) {
        Box("Rubber",u,y+.15f,d,.85f,.16f,1.8f);
        foreach(float x in new[]{-.45f,.45f}) {Beam("Metal",V(u+x,y+.15f,d+.65f),V(u+x,y+1.15f,d+.8f),.055f);Box("Metal",u+x,y+.23f,d,.055f,.1f,1.85f);}
        Box("Metal",u,y+1.18f,d+.8f,1,.12f,.35f);Box("Banner",u,y+1.25f,d+.74f,.4f,.03f,.2f);
    }
    static void Bench(float u,float d,float y) {for(int i=0;i<4;i++)Box("Wood",u,y+.45f,d+(i-1.5f)*.11f,1.7f,.05f,.09f);foreach(float x in new[]{-.65f,.65f})Box("Metal",u+x,y+.23f,d,.06f,.46f,.4f);}
    static void Forecourt() {
        Box("Paving",0,.147f,-8.5f,59,.05f,17);
        foreach(float u in new[]{-9.2f,9.2f})Box("Grey stone",u,.176f,-9,.3f,.014f,14);
        foreach(float d in new[]{-5.3f,-10,-15.5f})Box("Grey stone",0,.176f,d,57,.014f,.26f);
        foreach(float side in new[]{-1f,1f}) {
            Planter(side*20,.18f,-3.5f,9,3,true);Planter(side*24,.18f,-13,6,3,false);
            Bench(side*24,-15.1f);Tree(side*26,-5);Tree(side*27,-13);
            for(int i=0;i<5;i++)Bike(side*(15+i*.8f),-7.5f,i);
            Lamp(side*25,-9);Box("Banner",side*25,3.1f,-9.06f,.8f,1.65f,.06f);
            for(int i=0;i<80;i++) {float a=i*2.399f;var p=V(side*20+Mathf.Cos(a)*3.8f,.9f+(i%3)*.05f,-3.5f+Mathf.Sin(a));geometry["Flower"].Disc(p,Vector3.right,Vector3.forward,.055f,5);}
        }
        Sphere("Grey stone",V(22,1.1f,-13),new Vector3(1.7f,1.1f,.75f));
        for(int i=0;i<100;i++)Box("Metal",-11+i*.22f,.18f,-3.1f,.035f,.012f,.25f);
    }
    static void Sign(Transform holder,CampusGame game,string text,float u,float y,float d,float size,bool gold=false) {
        var obj=new GameObject(text.Replace('\n',' '));obj.transform.SetParent(holder,false);
        obj.transform.position=game.MapToWorld(V(u,y,d));obj.transform.rotation=Quaternion.LookRotation(Vector3.back,Vector3.up);
        obj.transform.localScale=new Vector3(-1,1,1);
        var mesh=obj.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;
        mesh.characterSize=size/10;mesh.fontSize=96;mesh.fontStyle=FontStyle.Bold;mesh.color=gold?new Color(.9f,.77f,.55f):new Color(.21f,.24f,.25f);mesh.lineSpacing=1.1f;
        string path=Folder+"/Materials/Wall lettering.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material)material=Save(new Material(Shader.Find("Campus/WallLettering")){name="Wall lettering"},path);
        obj.AddComponent<CafeteriaSign>().surface=material;
    }
    static void Window(float left,float right,float low,float high,float d) {
        float width=right-left,mid=(left+right)/2;Box("Glass",mid,(low+high)/2,d,width,high-low,.025f);
        int n=Mathf.CeilToInt(width/1.4f);
        for(int i=0;i<=n;i++)Box("Metal",Mathf.Lerp(left,right,(float)i/n),(low+high)/2,d-.035f,.07f,high-low,.1f);
        foreach(float y in new[]{low,low+(high-low)*.32f,high})Box("Metal",mid,y,d-.035f,width,.065f,.1f);
        Box("Grey stone",mid,low-.05f,d-.12f,width+.08f,.1f,.33f);
    }
    static void Sphere(string m,Vector3 center,Vector3 scale) {
        Vector3 At(float latitude,float longitude)=>center+Vector3.Scale(new Vector3(Mathf.Cos(latitude)*Mathf.Cos(longitude),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(longitude)),scale);
        for(int y=0;y<6;y++)for(int x=0;x<10;x++) {
            float a=-Mathf.PI/2+y*Mathf.PI/6,b=a+Mathf.PI/6,c=x*Mathf.PI/5,d=c+Mathf.PI/5;
            geometry[m].Quad(At(a,c),At(a,d),At(b,d),At(b,c));
        }
    }
    static void Planter(float u,float y,float d,float w,float depth,bool tall) {
        Box("Limestone",u,y+.2f,d,w,.4f,depth);Box("Soil",u,y+.41f,d,w-.15f,.03f,depth-.15f);
        int count=(int)(w*depth*18);
        for(int i=0;i<count;i++) {
            var p=V(u+((float)random.NextDouble()-.5f)*(w-.2f),y+.5f+(float)random.NextDouble()*.2f,d+((float)random.NextDouble()-.5f)*(depth-.2f));
            for(int j=0;j<12;j++)Leaf(p,new Vector3(((float)random.NextDouble()-.5f)*.4f,(float)random.NextDouble()*.3f,((float)random.NextDouble()-.5f)*.4f),.12f,j);
        }
        if(tall&&w<2)for(int i=0;i<40;i++){float a=i*2.399f;var p=V(u+Mathf.Cos(a)*.25f,y+.8f+(i%6)*.1f,d+Mathf.Sin(a)*.25f);Leaf(p,Vector3.zero,.18f,i);}
    }
    static void Leaf(Vector3 p,Vector3 offset,float size,int i){float a=i*2.399f;var u=new Vector3(Mathf.Cos(a),.25f,Mathf.Sin(a))*size;var v=new Vector3(-u.z,.05f,u.x)*.42f;p+=offset;geometry[i%3==0?"Leaves light":"Leaves"].Quad(p-u,p+v,p+u,p-v);}
    static void Tree(float u,float d) {
        var bottom=V(u,.2f,d);var top=V(u+.1f,5.5f,d-.1f);Beam("Wood",bottom,top,.18f);
        for(int b=0;b<10;b++){float a=b*2.399f;var tip=top+new Vector3(Mathf.Cos(a)*1.7f,Mathf.Sin(b)*.8f,Mathf.Sin(a)*1.7f);Beam("Wood",Vector3.Lerp(bottom,top,.57f),tip,.065f);
            for(int j=0;j<110;j++)Leaf(tip,new Vector3(((float)random.NextDouble()-.5f)*2.6f,((float)random.NextDouble()-.5f)*2,((float)random.NextDouble()-.5f)*2.6f),.24f,j);
        }
    }
    static void Bench(float u,float d){for(int i=0;i<5;i++)Box("Wood",u,.65f,d+(i-2)*.11f,2.8f,.055f,.09f);for(int i=0;i<4;i++)Box("Wood",u,.9f+i*.11f,d+.3f,2.8f,.085f,.055f);foreach(float x in new[]{-1f,1f})Box("Metal",u+x,.42f,d,.065f,.6f,.52f);}
    static void Lamp(float u,float d){Box("Metal",u,2.3f,d,.085f,4.3f,.085f);Box("Metal",u,4.5f,d,.75f,.1f,.65f);Box("Lamp",u,4.35f,d,.32f,.22f,.32f);}
    static void Bike(float u,float d,int index) {
        var a=V(u,.52f,d-.57f);var b=V(u,.52f,d+.57f);
        foreach(var p in new[]{a,b}) {
            geometry["Rubber"].Ring(p,Vector3.back,Vector3.up,.34f,.035f,20);
            geometry["Stainless"].Ring(p,Vector3.back,Vector3.up,.3f,.015f,20);
            for(int spoke=0;spoke<8;spoke++){float angle=spoke*Mathf.PI/4;Beam("Stainless",p,p+new Vector3(0,Mathf.Sin(angle)*.29f,-Mathf.Cos(angle)*.29f),.006f);}
        }
        var crank=V(u,.45f,d-.05f);var seat=V(u,.94f,d-.23f);var steer=V(u,1.04f,d+.42f);string m=index%2==0?"Metal":"Bronze";
        foreach(var pair in new[]{new[]{a,seat},new[]{a,crank},new[]{crank,seat},new[]{seat,steer},new[]{crank,steer},new[]{b,steer}})Beam(m,pair[0],pair[1],.03f);
        Box("Rubber",u,.99f,d-.25f,.19f,.05f,.24f);Box("Metal",u,1.08f,d+.4f,.5f,.025f,.025f);
    }
    static void UpdateMap(CampusData data,CampusGame game) {
        var feature=data.features.Single(f=>f.id==77923);feature.points=new[]{P(-186,-42),P(-130,-42),P(-130,-9),P(-186,-9)};
        var buildings=new HashSet<string>(data.features.Select(f=>f.name));
        data.obstacles=data.obstacles.Where(o=>!o.name.StartsWith("Gymnasium ")&&(buildings.Contains(o.name)||!InMask(o))).ToArray();
        foreach(var o in data.obstacles.Where(o=>o.name==feature.name)){o.points=feature.points;o.minX=-186;o.maxX=-130;o.minZ=-42;o.maxZ=-9;}
        var extra=new List<CampusObstacle>();
        void Obstacle(string name,float u,float d,float w,float depth){extra.Add(new CampusObstacle{name="Gymnasium "+name,minX=Center+u-w/2,maxX=Center+u+w/2,minZ=Front-d-depth/2,maxZ=Front-d+depth/2});}
        Obstacle("entrance steps",0,-1.3f,25.4f,2.8f);
        foreach(float side in new[]{-1f,1f}) {
            Obstacle("planter",side*20,-3.5f,9,3);Obstacle("planter",side*24,-13,6,3);
            Obstacle("bench",side*24,-15.1f,2.8f,.7f);Obstacle("bikes",side*16.6f,-7.5f,4.3f,1.9f);
            Obstacle("lamp",side*25,-9,.2f,.2f);Obstacle("tree",side*26,-5,.3f,.3f);Obstacle("tree",side*27,-13,.3f,.3f);
        }
        data.obstacles=data.obstacles.Concat(extra).ToArray();
        data.routes=data.routes.Where(r=>r.name!="gymnasium-approach").Concat(new[]{new CampusRoute{name="gymnasium-approach",points=new[]{P(-158,7),P(-158,0),P(-158,-5.2f)}}}).ToArray();
        if(!CampusCollision.Blocked(-158,-20,data.obstacles))throw new Exception("Gymnasium building collision missing");
        foreach(var route in data.routes) {
            var p=new Vector3(route.points[0].x,.23f,route.points[0].z);
            for(int i=1;i<route.points.Length;i++) {
                var goal=new Vector3(route.points[i].x,.23f,route.points[i].z);
                for(int j=0;j<6000&&Vector3.Distance(p,goal)>.05f;j++)p=CampusCollision.Move(p,Vector3.ClampMagnitude(goal-p,.15f),data.obstacles);
                if(Vector3.Distance(p,goal)>.1f)throw new Exception("Gymnasium revision obstructs "+route.name+" at "+p);
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
            // may contain multiple buildings and must be clipped without deleting the batch.
            var source=filter.sharedMesh;var b=source.bounds;
            if(b.max.x<Mask.xMin||b.min.x>Mask.xMax||b.max.z<Mask.yMin||b.min.z>Mask.yMax||b.max.y<.2f)continue;
            var path=AssetDatabase.GetAssetPath(source);
            if(!(path.StartsWith("Assets/LivingRevision/Meshes/")||path.Contains("Retained-")||path.Contains("Surroundings-")||path.Contains("Preserved-")))continue;
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
        var mesh=new Mesh{name="Gymnasium preserved surroundings",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.c).ToList());mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateBounds();return mesh;
    }
}
