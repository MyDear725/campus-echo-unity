using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Reference-inspired library exterior, with a separate unchanged gameplay interior.
// All dimensions are metres. Front is west; horizontal facade coordinate is +z.
public static class LibraryBuilder
{
    const string Folder="Assets/LibraryRevision",Group="图书馆 · 石材与通高玻璃大厅";
    const float Front=50,Center=92,Width=48,Depth=30,Height=22.4f;
    // Include the old canopy's projecting lip, not just the old wall footprint.
    static readonly Rect Mask=Rect.MinMaxRect(30,54,93,127.8f);
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
    [MenuItem("Campus/Apply Reference Library")]
    public static void Generate() {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        var root=GameObject.Find("开发区校区 · 校园环境").transform;
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        bool hasTeaching=root.Find(TeachingBuilder.Group);
        if(hasTeaching)TeachingBuilder.RestoreSources(root);
        bool hasGym=root.Find(GymnasiumBuilder.Group);
        if(hasGym)GymnasiumBuilder.RestoreSources(root);
        Apply(root,data,game);
        if(hasGym)GymnasiumBuilder.Apply(root,data,game);
        if(hasTeaching)TeachingBuilder.Apply(root,data,game);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("LIBRARY_GENERATION_COMPLETE");
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
        random=new System.Random(77917);geometry=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Limestone","#d6d3c8",.24f,1);Mat("Grey stone","#9c9f9b",.25f,1);
        Mat("Metal","#424d51",.65f);Mat("Bronze","#827464",.4f);Mat("Stainless","#b9c0bc",.85f);
        Mat("Interior","#ded4ba",.1f,0,0,.25f);Mat("Floor","#b8b3a4",.15f,1);
        Mat("Wood","#a88660",.2f,0,0,.07f);materials["Wood"].SetFloat("_Wood",1);Mat("Seat","#d1bb87",.2f);
        Mat("Glass","#93aebc",.8f,0,1);Mat("Lamp","#ffe2a6",.1f,0,0,1.35f);
        Mat("Skin","#be997a",.05f);Mat("Hair","#383a36",.05f);Mat("Shirt","#657989",.1f);Mat("Cream shirt","#c9c1ac",.1f);
        Mat("Rubber","#353936",.1f);Mat("Leaves","#60794a",.1f);Mat("Leaves light","#8a985d",.1f);
        Mat("Book blue","#496f83",.2f);Mat("Book red","#975d4f",.2f);Mat("Book gold","#b49c66",.2f);Mat("Soil","#665a43",.1f);Mat("Banner","#334d6d",.15f);
        var paving=new Material(Shader.Find("Campus/Courtyard")){name="Forecourt paving"};ColorUtility.TryParseHtmlString("#bbb8ad",out var pc);paving.SetColor("_BaseColor",pc);paving.SetFloat("_Surface",1);materials["Paving"]=Save(paving,Folder+"/Materials/Paving.mat");geometry["Paving"]=new DormitoryBuilder.Geometry();
        Building();Forecourt();RearEntrance();
        foreach(var pair in geometry.Where(p=>p.Value.vertices.Count>0)) {
            var mesh=Save(pair.Value.Mesh("Library "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");
            var obj=new GameObject(pair.Key);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];r.shadowCastingMode=pair.Key=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
        Sign(holder,game,"图 书 馆",0,11.3f,-.7f,1.2f);
        Sign(holder,game,"Library",0,10.25f,-.7f,.55f);
        Sign(holder,game,"阅读\n思考\n成长",-22.2f,15.7f,-.58f,.6f);
        Sign(holder,game,"A Brighter\nYou\nA Bigger\nWorld",-22.2f,12.5f,-.58f,.28f);
        Sign(holder,game,"书\n香\n致\n远",22.85f,15.7f,-.58f,.55f);
        Sign(holder,game,"知\n行\n合\n一",21.55f,13.5f,-.58f,.55f);
        Sign(holder,game,"知识让世界更美好",0,4.6f,.35f,.4f);
        Sign(holder,game,"博\n学\n笃\n行",-21f,3.1f,-13.12f,.2f);
        Sign(holder,game,"明\n德\n致\n远",21f,3.1f,-13.12f,.2f);
        Sign(holder,game,"图书馆 · 后门",0,5.85f,30.45f,.6f,true);
        UpdateMap(data,game);
        var oldDoor=GameObject.Find("图书馆 · 广场侧交互入口");if(oldDoor)UnityEngine.Object.DestroyImmediate(oldDoor);
        var entry=AssetDatabase.LoadAssetAtPath<BuildingEntranceDefinition>("Assets/Interiors/Entrances/library.asset");
        if(!entry)throw new Exception("Library entrance configuration missing");
        entry.approachMapPosition=new Vector3(46,.23f,92);entry.returnMapPosition=new Vector3(44.6f,.23f,92);entry.enterYaw=270;EditorUtility.SetDirty(entry);
        string rearPath="Assets/Interiors/Entrances/library-rear.asset";
        var rear=AssetDatabase.LoadAssetAtPath<BuildingEntranceDefinition>(rearPath);
        if(!rear){rear=ScriptableObject.CreateInstance<BuildingEntranceDefinition>();AssetDatabase.CreateAsset(rear,rearPath);}
        rear.buildingId="library";rear.entranceId="library-rear";rear.displayName="图书馆 · 后门";rear.category=CampusBuildingCategory.Library;rear.mapFeatureId=77917;
        rear.approachMapPosition=new Vector3(84,.23f,92);rear.returnMapPosition=new Vector3(85.4f,.23f,92);rear.enterYaw=90;
        rear.interiorScenePath=entry.interiorScenePath;rear.spawnId=entry.spawnId;EditorUtility.SetDirty(rear);
        var travel=game.GetComponent<BuildingInteriorTravel>();
        travel.entrances=travel.entrances.Where(e=>e&&e.EntranceId!="library-rear").Concat(new[]{rear}).ToArray();EditorUtility.SetDirty(travel);
        if(!game.GetComponent<LibraryPreview>())game.gameObject.AddComponent<LibraryPreview>();
        EditorUtility.SetDirty(game);Directory.CreateDirectory("QA");
        BuildingInteriorBuilder.ValidateConfiguration();
        File.WriteAllText("QA/library-generation.txt",$"PASS: library 77917, 48 x 30 m; three-storey stone wings, {Height} m glazed atrium.\nReference facade, window scenery, front steps, ramp, paving, planting and physical signage.\n{geometry.Count} material batches. Exterior routes and all entry points validated.\n");
    }
    static void Mat(string name,string hex,float smooth,float stone=0,float glass=0,float emission=0) {
        ColorUtility.TryParseHtmlString(hex,out var color);if(glass>0)color.a=.22f;
        var m=new Material(Shader.Find("Campus/Library")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Stone",stone);m.SetFloat("_Glass",glass);m.SetFloat("_Emission",emission);
        if(glass>0){m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static void Building() {
        // Three-storey wings flank an uninterrupted, double-height upper atrium.
        Box("Grey stone",0,.42f,15,48,.84f,30);
        // Leave a real opening in the rear wall for the second glazed entrance.
        foreach(float side in new[]{-1f,1f})Box("Limestone",side*14.7f,10.6f,30,18.6f,19.8f,.4f);
        Box("Limestone",0,13.35f,30,10.8f,14.3f,.4f);
        foreach(float side in new[]{-1f,1f}) {
            float u=side*16;
            foreach(float y in new[]{.9f,7.25f,13.65f,20f}) {
                Box("Floor",u,y,15,16,.2f,29.6f);
                Box("Limestone",u,y+.2f,0,16,.6f,.5f);
                Box("Limestone",side*23.9f,y+.3f,15,.3f,.85f,30);
            }
            Box("Grey stone",u,20.4f,15,16.2f,.5f,30.3f);
            Box("Limestone",u,20.75f,15,16.4f,.16f,30.5f);
            foreach(float d in new[]{.3f,6f,12f,18f,24f,29.8f})Box("Limestone",side*23.9f,10.3f,d,.5f,20,1.25f);
            for(int floor=0;floor<3;floor++)for(int bay=0;bay<5;bay++) {
                float y=4.1f+floor*6.4f,d=3.1f+bay*5.9f;
                Box("Glass",side*24,y,d,.03f,5.4f,4.5f);
                foreach(float t in new[]{-2.25f,0,2.25f})Box("Metal",side*24.04f,y,d+t,.1f,5.45f,.075f);
                foreach(float t in new[]{-2.7f,1.4f,2.7f})Box("Metal",side*24.04f,y+t,d,.1f,.07f,4.6f);
            }
            // Thick limestone outer piers and slim inner piers match the reference.
            Box("Limestone",side*22.25f,10.5f,-.16f,3.5f,20,.65f);
            Box("Limestone",side*8.65f,10.6f,-.25f,1.45f,20.2f,.8f);
            Box("Limestone",side*15.35f,7.35f,-.05f,12,.75f,.6f);
            Box("Metal",side*15.35f,13.7f,-.03f,12,.9f,.22f);
            for(int floor=0;floor<3;floor++) {
                float low=1.05f+floor*6.4f,high=6.95f+floor*6.4f;
                Window(side<0?-20.5f:9.4f,side<0?-9.4f:20.5f,low,high,.08f);
                float level=.98f+floor*6.4f;
                Box("Interior",side*16,high-.05f,14,15.3f,.1f,27);
                // Visible scenery only: the enterable interior remains a separate empty scene.
                for(int row=0;row<3;row++)for(int bay=0;bay<3;bay++)Shelf(side*(11+bay*4),level,4.5f+row*4.8f);
                for(int desk=0;desk<4;desk++)Table(side*(10.5f+desk*2.8f),level,1.9f,desk%3==0);
                foreach(float d in new[]{2.5f,8.5f,15f})Box("Lamp",side*15,high-.12f,d,11,.025f,.09f);
            }
            for(int i=0;i<6;i++)Box("Wood",side*15.3f+(i-2.5f)*.23f,13.5f,-.33f,.11f,12.25f,.35f);
            foreach(float u0 in new[]{10.8f,19.1f})Box("Limestone",side*u0,3.9f,-.19f,.45f,5.8f,.55f);
        }
        // Glazed central tower, roof overhang and vertical structural fins.
        Box("Floor",0,.94f,15,15.6f,.2f,30);
        Box("Interior",0,11.3f,23,15.5f,20.5f,.25f);
        Box("Wood",0,22.15f,3.5f,17.2f,.25f,10.7f);
        Box("Grey stone",0,22.34f,3.5f,17.4f,.13f,10.9f);
        Box("Grey stone",0,21.9f,19,15.8f,.3f,22);
        for(int i=0;i<65;i++)Box("Bronze",-8.4f+i*.26f,22.0f,3.5f,.018f,.018f,10.5f);
        foreach(float u in new[]{-7.6f,-4.6f,4.6f,7.6f})Box(Mathf.Abs(u)>7?"Wood":"Metal",u,11.65f,-.5f,.25f,20.9f,.42f);
        Window(-7.5f,7.5f,6.9f,21.85f,-.25f);
        for(float y=9.2f;y<21;y+=2.3f)Box("Metal",0,y,-.31f,15,.085f,.12f);
        Window(-7.5f,-2.8f,1.02f,6.65f,-.1f);Window(2.8f,7.5f,1.02f,6.65f,-.1f);
        Window(-2.8f,2.8f,4.1f,6.65f,-.1f);
        for(int door=0;door<4;door++) {
            float u=(door-1.5f)*1.4f;Box("Glass",u,2.55f,-.12f,1.36f,3.1f,.025f);
            foreach(float t in new[]{-.69f,.69f})Box("Metal",u+t,2.55f,-.19f,.075f,3.1f,.12f);
            foreach(float y in new[]{1.02f,4.1f})Box("Metal",u,y,-.19f,1.4f,.09f,.12f);
            Box("Stainless",u+(door%2==0?.46f:-.46f),2.5f,-.28f,.035f,.82f,.04f);
        }
        // Curved galleries seen through the glass create depth without designing the gameplay interior.
        foreach(float y in new[]{7.35f,13.75f}) {
            for(int k=0;k<32;k++) {
                float a=Mathf.PI+k*Mathf.PI/32,b=Mathf.PI+(k+1)*Mathf.PI/32;
                var p=V(Mathf.Cos(a)*6.4f,y,8+Mathf.Sin(a)*3.4f);var q=V(Mathf.Cos(b)*6.4f,y,8+Mathf.Sin(b)*3.4f);
                Beam("Limestone",p,q,.28f);Beam("Stainless",p+Vector3.up*1.1f,q+Vector3.up*1.1f,.045f);
                geometry["Glass"].Quad(p+Vector3.up*.12f,q+Vector3.up*.12f,q+Vector3.up*1.08f,p+Vector3.up*1.08f);
                if(k%3==0)Beam("Metal",p,p+Vector3.up*1.1f,.04f);
                Beam("Lamp",p+Vector3.down*.14f,q+Vector3.down*.14f,.035f);
            }
            Box("Floor",0,y,12.5f,15,.18f,8);
            foreach(float u in new[]{-5.8f,5.8f})Planter(u,y,6.2f,1,1,true);
        }
        foreach(float d in new[]{4f,10f,16f})Box("Lamp",0,21.7f,d,7,.04f,.16f);
        Box("Metal",0,6.6f,-1.85f,17.6f,.23f,4.3f);
        Box("Wood",0,6.44f,-1.85f,17.25f,.055f,4.0f);
        for(float u=-7;u<=7;u+=2.3f)foreach(float d in new[]{-.6f,-3.2f})geometry["Lamp"].Disc(V(u,6.4f,d),Vector3.right,Vector3.forward,.09f,12);
        foreach(float u in new[]{-8.2f,8.2f})Box("Metal",u,6.48f,-1.85f,.06f,.17f,4.1f);
        // Eight shallow steps, with a side ramp. Interaction is at the foot of the steps.
        for(int step=0;step<8;step++) {
            float d=-2.65f+step*.34f,y=.18f+(step+1)*.105f;
            Box("Grey stone",0,(y+.12f)/2,d/2,18,y-.12f,-d+.12f);
            Box("Limestone",0,y+.01f,d,17.9f,.022f,.07f);
        }
        foreach(float u in new[]{-9.4f,9.4f})Box("Limestone",u,.66f,-1.3f,.65f,1.05f,2.8f);
        geometry["Grey stone"].Quad(V(10,.99f,-2.5f),V(22,.18f,-2.5f),V(22,.18f,-.15f),V(10,.99f,-.15f));
        foreach(float d in new[]{-2.5f,-.15f}) {
            Beam("Stainless",V(10,1.95f,d),V(22,1.14f,d),.045f);
            for(int k=0;k<=12;k++)Beam("Stainless",V(10+k,.99f-k*.0675f,d),V(10+k,1.95f-k*.0675f,d),.035f);
        }
        foreach(float u in new[]{-6.6f,6.6f})Planter(u,1,-.9f,.7f,.7f,true);
    }
    static void Shelf(float u,float y,float d) {
        Box("Wood",u,y+1.4f,d,2.7f,2.8f,.42f);
        Box("Interior",u,y+1.43f,d-.23f,2.5f,2.6f,.035f);
        foreach(float h in new[]{.15f,.75f,1.4f,2.05f,2.7f}) {
            Box("Wood",u,y+h,d-.34f,2.65f,.065f,.5f);
            if(h>2.6f)continue;
            for(int i=0;i<15;i++){float height=.34f+(float)random.NextDouble()*.18f;Box(i%4==0?"Book red":i%3==0?"Book gold":"Book blue",u-1.17f+i*.165f,y+h+height/2+.035f,d-.37f,.12f,height,.21f);}
        }
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
        Box("Paving",0,.147f,-10.4f,51,.05f,20.2f);
        foreach(float u in new[]{-8f,8f})Box("Grey stone",u,.176f,-11,.3f,.014f,18);
        foreach(float d in new[]{-5f,-12f,-19f})Box("Grey stone",0,.176f,d,49,.014f,.3f);
        foreach(float side in new[]{-1f,1f}) {
            Planter(side*19,.18f,-7,5.5f,3.5f,true);
            Planter(side*20,.18f,-17,6,3.5f,false);
            Bench(side*20,-14.9f);
            for(int i=0;i<4;i++)Bike(side*(11.8f+i*.8f),-6,i);
            Lamp(side*21,-13);Lamp(side*11,-5);
            Box("Banner",side*21,3.1f,-13.05f,.8f,1.65f,.06f);
            foreach(float d in new[]{-7f,-17f})Tree(side*23,d);
        }
        // A shallow gutter beside the entrance, leaving the centre uninterrupted.
        foreach(float u in new[]{-11f,11f})for(int i=0;i<16;i++)Box("Metal",u+i*.06f,.18f,-3.6f,.028f,.012f,.25f);
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
            geometry["Rubber"].Ring(p,Vector3.right,Vector3.up,.34f,.035f,20);
            geometry["Stainless"].Ring(p,Vector3.right,Vector3.up,.3f,.015f,20);
            for(int spoke=0;spoke<8;spoke++){float angle=spoke*Mathf.PI/4;Beam("Stainless",p,p+new Vector3(Mathf.Cos(angle)*.29f,Mathf.Sin(angle)*.29f,0),.006f);}
        }
        var crank=V(u,.45f,d-.05f);var seat=V(u,.94f,d-.23f);var steer=V(u,1.04f,d+.42f);string m=index%2==0?"Metal":"Bronze";
        foreach(var pair in new[]{new[]{a,seat},new[]{a,crank},new[]{crank,seat},new[]{seat,steer},new[]{crank,steer},new[]{b,steer}})Beam(m,pair[0],pair[1],.03f);
        Box("Rubber",u,.99f,d-.25f,.19f,.05f,.24f);Box("Metal",u,1.08f,d+.4f,.5f,.025f,.025f);
    }
    static void RearEntrance() {
        // A smaller stone-and-glass portico on the east facade, matching the front.
        foreach(float side in new[]{-1f,1f})Box("Limestone",side*5.6f,3.15f,30.15f,.65f,5.7f,.75f);
        Box("Limestone",0,5.8f,30.12f,11.8f,.85f,.65f);
        Box("Glass",0,3.2f,30.18f,10.8f,4.4f,.025f);
        foreach(float u in new[]{-5.35f,-2.8f,-1.4f,0,1.4f,2.8f,5.35f})Box("Metal",u,3.2f,30.27f,.08f,4.4f,.12f);
        foreach(float y in new[]{1.02f,4.25f,5.4f})Box("Metal",0,y,30.27f,10.8f,.08f,.12f);
        foreach(float u in new[]{-2.8f,2.8f})Box("Metal",u,.99f,30.27f,5.4f,.1f,.12f);
        foreach(float u in new[]{-2.05f,-.75f,.75f,2.05f})Box("Stainless",u,2.55f,30.4f,.04f,.8f,.045f);
        Box("Metal",0,4.8f,31.7f,12.3f,.22f,3.8f);Box("Wood",0,4.66f,31.7f,12,.05f,3.55f);
        foreach(float u in new[]{-4.8f,-2.4f,0,2.4f,4.8f})geometry["Lamp"].Disc(V(u,4.62f,32),Vector3.right,Vector3.forward,.08f,12);
        foreach(float u in new[]{-5.65f,5.65f})Beam("Metal",V(u,4.92f,33.45f),V(u,6.25f,30.25f),.04f);
        // A shallow vestibule is scenery only; gameplay still uses the same empty interior.
        Box("Interior",0,3.5f,24,10.7f,5,.15f);Box("Floor",0,.94f,27,10.7f,.15f,6);
        Box("Interior",0,5.5f,27,10.7f,.1f,6);Box("Lamp",0,5.42f,27.5f,7,.025f,.15f);
        for(int step=0;step<8;step++) {
            float outer=32.7f-step*.34f,y=.18f+(step+1)*.105f;
            Box("Grey stone",0,(y+.12f)/2,(30+outer)/2,9.8f,y-.12f,outer-30+.1f);
            Box("Limestone",0,y+.01f,outer,9.75f,.022f,.07f);
        }
        foreach(float u in new[]{-5.2f,5.2f})Box("Limestone",u,.66f,31.35f,.55f,1.05f,2.8f);
        Box("Paving",0,.147f,35.15f,25,.05f,10.1f);
        foreach(float u in new[]{-6.5f,6.5f})Box("Grey stone",u,.176f,36,.2f,.014f,7.8f);
        foreach(float side in new[]{-1f,1f}) {
            Planter(side*8,.18f,31.8f,2.8f,2.5f,false);Lamp(side*11,38);
            // Connect both entrances around each side of the building.
            Box("Paving",side*26,.147f,15,2.5f,.05f,43);
        }
        Box("Paving",0,.147f,36,54.5f,.05f,2.5f);
        foreach(float side in new[]{-1f,1f})Box("Paving",side*25,.147f,-4,4.5f,.05f,2.5f);
    }
    static void Sign(Transform holder,CampusGame game,string text,float u,float y,float d,float size,bool rear=false) {
        var obj=new GameObject(text.Replace('\n',' '));obj.transform.SetParent(holder,false);
        obj.transform.position=game.MapToWorld(V(u,y,d));obj.transform.rotation=Quaternion.LookRotation(rear?Vector3.right:Vector3.left,Vector3.up);
        // Compensate the imported root's negative X scale: text must read normally.
        obj.transform.localScale=new Vector3(-1,1,-1);
        var mesh=obj.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;
        mesh.characterSize=size/10;mesh.fontSize=96;mesh.fontStyle=FontStyle.Bold;mesh.color=text=="图 书 馆"||text=="Library"?new Color(.86f,.69f,.42f):text.Contains("\n学")||text.Contains("\n德")?new Color(.9f,.89f,.8f):new Color(.2f,.22f,.23f);mesh.lineSpacing=1.1f;
        string path=Folder+"/Materials/Wall lettering.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material)material=Save(new Material(Shader.Find("Campus/WallLettering")){name="Wall lettering"},path);
        obj.AddComponent<CafeteriaSign>().surface=material;
    }
    static void UpdateMap(CampusData data,CampusGame game) {
        var feature=data.features.Single(f=>f.id==77917);feature.points=new[]{P(50,68),P(80,68),P(80,116),P(50,116)};
        var buildings=new HashSet<string>(data.features.Where(f=>f.kind=="building").Select(f=>f.name));
        data.obstacles=data.obstacles.Where(o=>!o.name.StartsWith("Library ")&&(buildings.Contains(o.name)||!InMask(o))).ToArray();
        foreach(var o in data.obstacles.Where(o=>o.name==feature.name)){o.points=feature.points;o.minX=50;o.maxX=80;o.minZ=68;o.maxZ=116;}
        var extra=new List<CampusObstacle>{new CampusObstacle{name="Library entrance steps",minX=47.25f,maxX=50,minZ=82.2f,maxZ=101.8f},new CampusObstacle{name="Library ramp",minX=47.4f,maxX=50,minZ=102,maxZ=114}};
        void Obstacle(string name,float u,float d,float w,float depth){extra.Add(new CampusObstacle{name="Library "+name,minX=Front+d-depth/2,maxX=Front+d+depth/2,minZ=Center+u-w/2,maxZ=Center+u+w/2});}
        foreach(float side in new[]{-1f,1f}) {
            Obstacle("planter",side*19,-7,5.5f,3.5f);Obstacle("planter",side*20,-17,6,3.5f);Obstacle("bench",side*20,-14.9f,2.8f,.7f);
            Obstacle("bike rack",side*13,-6,4.2f,1.9f);Obstacle("lamp",side*21,-13,.2f,.2f);Obstacle("lamp",side*11,-5,.2f,.2f);
            Obstacle("rear planter",side*8,31.8f,2.8f,2.5f);Obstacle("rear lamp",side*11,38,.2f,.2f);
        }
        Obstacle("rear steps",0,31.4f,11,2.8f);
        data.obstacles=data.obstacles.Concat(extra).ToArray();
        data.routes=data.routes.Where(r=>r.name!="library-approach"&&!r.name.StartsWith("library-rear-")).Concat(new[]{
            new CampusRoute{name="library-approach",points=new[]{P(38,104),P(42,98),P(44.6f,92),P(46,92)}},
            new CampusRoute{name="library-rear-south",points=new[]{P(46,92),P(46,118),P(86,118),P(86,92),P(84,92)}},
            new CampusRoute{name="library-rear-north",points=new[]{P(46,92),P(46,66),P(86,66),P(86,92),P(84,92)}}
        }).ToArray();
        if(!CampusCollision.Blocked(65,92,data.obstacles))throw new Exception("Library building collision missing");
        foreach(var route in data.routes) {
            var p=new Vector3(route.points[0].x,.23f,route.points[0].z);
            for(int i=1;i<route.points.Length;i++) {
                var goal=new Vector3(route.points[i].x,.23f,route.points[i].z);
                for(int j=0;j<6000&&Vector3.Distance(p,goal)>.05f;j++)p=CampusCollision.Move(p,Vector3.ClampMagnitude(goal-p,.15f),data.obstacles);
                if(Vector3.Distance(p,goal)>.1f)throw new Exception("Library revision obstructs "+route.name+" at "+p);
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
        var mesh=new Mesh{name="Library preserved surroundings",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.c).ToList());mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateBounds();return mesh;
    }
}
