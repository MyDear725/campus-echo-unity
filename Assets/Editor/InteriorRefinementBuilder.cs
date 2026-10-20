using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

// Augments the existing collaboration scenes; never rebuilds their gameplay layout.
public static class InteriorRefinementBuilder
{
    const string Folder="Assets/InteriorRefinement";
    static InteriorRefinementPalette palette;
    static int rendererIndex;
    static Material Material(string name,string color,int kind=0,float smooth=.25f,float metallic=0,float emission=0)
    {
        string path=Folder+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Campus/RefinedInterior")){name=name};AssetDatabase.CreateAsset(material,path);}
        ColorUtility.TryParseHtmlString(color,out var c);material.SetColor("_BaseColor",c);material.SetFloat("_Kind",kind);
        material.SetFloat("_Smoothness",smooth);material.SetFloat("_Metallic",metallic);material.SetFloat("_Emission",emission);
        material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
    }
    static void Palette()
    {
        Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory("Assets/Resources/InteriorRefinement");AssetDatabase.Refresh();
        const string path="Assets/Resources/InteriorRefinement/Palette.asset";
        palette=AssetDatabase.LoadAssetAtPath<InteriorRefinementPalette>(path);
        if(!palette){palette=ScriptableObject.CreateInstance<InteriorRefinementPalette>();AssetDatabase.CreateAsset(palette,path);}
        palette.floor=Material("Warm limestone","#c7c5bb",1,.28f);palette.terrazzo=Material("Fine terrazzo","#c9c5b9",5,.25f);
        palette.plaster=Material("Ivory plaster","#ddd9cd",0,.13f);palette.ceiling=Material("Warm white ceiling","#e6e3da",0,.08f);
        palette.oak=Material("Natural oak","#b18b62",2,.25f);palette.walnut=Material("Smoked oak","#76604b",2,.23f);
        palette.graphite=Material("Graphite metal","#35424a",4,.34f,.38f);palette.steel=Material("Brushed stainless","#a2a9a8",4,.45f,.68f);
        palette.brass=Material("Satin champagne","#b4a17a",4,.39f,.45f);palette.teal=Material("Campus petrol blue","#294c59",0,.18f);
        palette.sage=Material("Soft sage paint","#99a89b",0,.18f);palette.fabric=Material("Sage upholstery","#6e8b87",3,.12f);
        palette.linen=Material("Ivory linen","#d5cebc",3,.12f);palette.light=Material("Warm LED diffuser","#fff1d6",0,.2f,0,1.15f);
        palette.soil=Material("Planter soil","#38352c",0,.05f);palette.leaf=Material("Deep green foliage","#4d7352",0,.17f);
        string glassPath=Folder+"/Materials/Transparent balcony glass.mat";
        palette.glass=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
        if(!palette.glass){palette.glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Transparent balcony glass"};AssetDatabase.CreateAsset(palette.glass,glassPath);}
        var glass=palette.glass;glass.SetColor("_BaseColor",new Color(.76f,.86f,.90f,.13f));glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);
        glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);
        glass.SetFloat("_Smoothness",.68f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=3000;
        EditorUtility.SetDirty(glass);palette.font=Resources.Load<Font>("CampusFont");EditorUtility.SetDirty(palette);AssetDatabase.SaveAssets();
    }
    static int Renderer()
    {
        string path=Folder+"/InteriorRenderer.asset";
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if(!renderer)
        {
            var source=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(InteriorPresentationSetup.InteriorRendererPath);
            renderer=UnityEngine.Object.Instantiate(source);renderer.name="Refined campus interiors";AssetDatabase.CreateAsset(renderer,path);
        }
        var settings=new SerializedObject(renderer);settings.FindProperty("m_RenderingMode").intValue=(int)RenderingMode.ForwardPlus;settings.ApplyModifiedPropertiesWithoutUndo();renderer.SetDirty();
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CampusURP.asset");
        var so=new SerializedObject(pipeline);var list=so.FindProperty("m_RendererDataList");
        for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer)return i;
        int index=list.arraySize;list.InsertArrayElementAtIndex(index);list.GetArrayElementAtIndex(index).objectReferenceValue=renderer;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);return index;
    }
    static Material Replacement(Material m)
    {
        if(!m||m.shader.name.Contains("Unlit")||m.shader.name.Contains("Text")||m.name.Contains("Glass")||m.name.Contains("Food")||m.name.Contains("Fruit")||m.name.Contains("Book"))return null;
        string n=m.name;
        if(n.Contains("Floor_Joint"))return palette.walnut;
        if(n.Contains("Floor_Terrazzo"))return palette.floor;
        if(n.Contains("Floor_Carpet")||n.Contains("Rug"))return palette.fabric;
        if(n.Contains("DarkBasalt")||n.Contains("Wall_DarkCharcoal")||n.Contains("Sign_BlackBox"))return palette.teal;
        if(n.Contains("Ceiling"))return palette.ceiling;
        if(n.Contains("Microcement")||n.Contains("Marble")||n.Contains("Stone_Slate")||n.Contains("Plant_Pot"))return palette.plaster;
        if(n.Contains("AccentGreen"))return palette.teal;
        if(n.Contains("DarkWalnut"))return palette.walnut;
        if(n.Contains("Wood"))return palette.oak;
        if(n.Contains("Metal_Stainless"))return palette.steel;
        if(n.Contains("Metal_Brass"))return palette.brass;
        if(n.Contains("Metal"))return palette.graphite;
        if(n.Contains("Light_"))return palette.light;
        if(n.Contains("Chair_Cream"))return palette.linen;
        if(n.Contains("Chair")||n.Contains("Sofa_Seat"))return palette.fabric;
        if(n.Contains("Sofa_Orange"))return palette.walnut;
        return null;
    }
    static InteriorDetailKit StartDetails(InteriorSceneRoot contract)
    {
        var old=contract.transform.Find("Interior refinement · coordinated finishes");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var kit=InteriorDetailKit.Create(contract.transform,"Interior refinement · coordinated finishes");kit.palette=palette;return kit;
    }
    static void Materials(InteriorSceneRoot contract)
    {
        foreach(var text in contract.GetComponentsInChildren<TextMesh>(true))
            if(!text.GetComponent<CafeteriaSign>()&&!text.GetComponent<InteriorWorldLabel>())text.gameObject.AddComponent<InteriorWorldLabel>();
        foreach(var r in contract.GetComponentsInChildren<MeshRenderer>(true))
        {
            var materials=r.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++){var replacement=Replacement(materials[i]);if(replacement){materials[i]=replacement;changed=true;}}
            if(changed)r.sharedMaterials=materials;
            // Lamps used to share exactly the same plane with the ceiling finish.
            if((r.name.StartsWith("Light_1F_")||r.name.StartsWith("Light_2F_")||r.name.StartsWith("Light_3F_"))&&!r.GetComponent<TextMesh>()&&r.bounds.size.y<.09f)
            {var at=r.transform.localPosition;at.y=Mathf.Round(at.y/4.3f)*4.3f-.22f;r.transform.localPosition=at;}
        }
    }
    static void Presentation(InteriorSceneRoot root,bool library=false,bool dorm=false)
    {
        var presentation=root.GetComponent<InteriorPresentation>()??root.gameObject.AddComponent<InteriorPresentation>();
        presentation.rendererIndex=rendererIndex;presentation.interiorFarClip=library?120:75;presentation.interiorShadowDistance=library?55:36;
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.62f,.66f,.69f);RenderSettings.ambientEquatorColor=new Color(.42f,.44f,.44f);RenderSettings.ambientGroundColor=new Color(.24f,.25f,.26f);
        var profile=InteriorPresentationSetup.EnsureVolume(Folder+"/InteriorVolume.asset",.12f,7,-3,.035f,1.5f);
        var go=root.transform.Find("Interior refinement · exposure");
        if(!go){go=new GameObject("Interior refinement · exposure").transform;go.SetParent(root.transform,false);}
        var volume=go.GetComponent<Volume>()??go.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=40;volume.sharedProfile=profile;
        foreach(var light in root.GetComponentsInChildren<Light>(true))
        {
            if(light.type==LightType.Directional){light.intensity=light.name.Contains("bounce")?.12f:.55f;light.color=new Color(1,.97f,.91f);}
            else {light.color=new Color(1,.96f,.89f);light.intensity=dorm?3.6f:library?Mathf.Min(light.intensity,4.2f):3.5f;}
        }
    }
    static void Cafeteria(InteriorSceneRoot root,InteriorDetailKit kit)
    {
        for(int floor=0;floor<3;floor++)
        {
            float y=floor*4.3f;
            kit.Box("Dining acoustic canopy",new Vector3(-5,y+3.96f,1),new Vector3(15,.08f,16),palette.ceiling);
            for(int i=0;i<18;i++)kit.Box("Oak ceiling fin",new Vector3(-12+i*.8f,y+3.86f,1),new Vector3(.065f,.14f,16),palette.oak,.008f);
            foreach(float x in new[]{-10f,-5f,0f})foreach(float z in new[]{-4f,3f,8f})kit.Luminaire(new Vector3(x,y+3.76f,z),2.6f);
            foreach(float z in new[]{3f,9f})
            {
                kit.Box("West wall sage dado",new Vector3(-17.79f,y+.58f,z),new Vector3(.045f,1.16f,5.8f),palette.sage);
                kit.Box("West wall oak cap",new Vector3(-17.75f,y+1.19f,z),new Vector3(.06f,.055f,5.8f),palette.oak);
            }
            foreach(float x in new[]{-6f,4f})foreach(float z in new[]{-6f,4f})
            {
                kit.Box("Column oak pedestal",new Vector3(x,y+.55f,z),new Vector3(.85f,1.1f,.85f),palette.oak,.012f);
                kit.Box("Column champagne reveal",new Vector3(x,y+1.12f,z),new Vector3(.87f,.025f,.87f),palette.brass,.004f);
            }
            kit.Sign((floor+1)+"F  第六食堂",floor==0?"大众餐厅  /  DINING HALL":floor==1?"特色餐厅  /  SPECIALTIES":"自选餐厅 · 鲜果  /  MARKET",new Vector3(-17.72f,y+2.2f,-7),Vector3.right,3,.85f);
            kit.Sign("有序用餐 · 珍惜粮食","ORDER  →  COLLECT  →  DINE  →  RETURN",new Vector3(16.4f,y+2.35f,-15.77f),Vector3.forward,2.6f,.65f);
            kit.Plant(new Vector3(-16.8f,y,-5.2f),1);kit.Plant(new Vector3(16.8f,y,-14.6f),1);
        }
    }
    static void Library(InteriorSceneRoot root,InteriorDetailKit kit)
    {
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            if(renderer.name.Contains("Floor_R")&&renderer.transform.parent&&renderer.transform.parent.name.Contains("Round_Floor_Annulus"))renderer.enabled=false;
        for(int floor=0;floor<4;floor++)
        {
            float y=floor*4.3f;
            kit.Box("Lobby acoustic canopy",new Vector3(-2,y+3.97f,0),new Vector3(22,.065f,18),palette.ceiling);
            for(int i=0;i<12;i++)kit.Box("Library ceiling oak fin",new Vector3(-12+i*1.8f,y+3.87f,0),new Vector3(.075f,.14f,17),palette.oak,.01f);
            foreach(float x in new[]{-10f,-3f,4f})foreach(float z in new[]{-6f,1f,7f})kit.Luminaire(new Vector3(x,y+3.75f,z),2.2f);
            kit.Box("North wall oak wainscot",new Vector3(-9.5f,y+.60f,15.76f),new Vector3(13,1.2f,.065f),palette.oak);
            kit.Box("North wall stone cap",new Vector3(-9.5f,y+1.23f,15.71f),new Vector3(13,.05f,.09f),palette.brass,.005f);
            kit.Sign(floor==0?"图书馆  LIBRARY":(floor+1)+"F  静心阅读","READ · THINK · GROW  /  阅读 · 思考 · 成长",new Vector3(-10,y+2.25f,15.68f),Vector3.back,5,.95f);
            if(floor==0)continue;
            kit.Annulus(new Vector3(34.5f,y,0),4.5f,15,.40f,palette.floor);
            // Real segmented ring fittings echo the circular reading hall, outside the central void.
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                var p=new Vector3(34.5f+Mathf.Cos(a)*10.3f,y+3.96f,Mathf.Sin(a)*10.3f);
                var q=new Vector3(34.5f+Mathf.Cos(b)*10.3f,y+3.96f,Mathf.Sin(b)*10.3f);
                kit.Beam("Circular reading canopy",p,q,.10f,palette.walnut);
                kit.Beam("Circular reading light",p+Vector3.down*.066f,q+Vector3.down*.066f,.035f,palette.light);
            }
            kit.Sign("静音阅览区","QUIET READING  /  请将手机调至静音",new Vector3(19.6f,y+2.8f,-4.2f),Vector3.left,3,.6f);
        }
        // Refine the desk itself, keeping all service/return interactions in place.
        for(int i=0;i<32;i++)kit.Box("Reception fluted oak face",new Vector3(-12.73f+i*.14f,.57f,-5.48f),new Vector3(.055f,.92f,.035f),palette.oak,.008f);
        kit.Box("Reception toe-kick LED",new Vector3(-10.5f,.10f,-5.49f),new Vector3(4.5f,.025f,.025f),palette.light,.004f);
    }
    static void Seventh(InteriorSceneRoot root,InteriorDetailKit kit)
    {
        var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(primitive);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.GetComponent<TextMesh>())continue;
            r.sharedMaterial=r.name.Contains("floor")||r.name.Contains("landing")||r.name.StartsWith("Physical stair")?palette.floor:
                r.name=="Roof"?palette.ceiling:r.name.Contains("wall")||r.name.Contains("surround")||r.name.Contains("lintel")?palette.plaster:
                r.name.Contains("handrail")?palette.oak:palette.graphite;
            if(r.name=="Continuous handrail")
            {
                // Keep the original continuous rail envelope on every refinement run.
                r.GetComponent<MeshFilter>().sharedMesh=cube;r.transform.localScale=new Vector3(.07f,new Vector3(0,2.85f,-7.5f).magnitude,.07f);
                r.GetComponent<BoxCollider>().size=Vector3.one;
            }
        }
        foreach(float x in new[]{-7.82f,7.82f})
        {
            kit.Box("LG oak wall lining",new Vector3(x,.7f,5),new Vector3(.045f,1.4f,13),palette.oak);
            kit.Box("Upper oak wall lining",new Vector3(x,3.55f,-8.1f),new Vector3(.045f,1.4f,7.6f),palette.oak);
        }
        foreach(float z in new[]{-9f,-3f,4f,9f})kit.Luminaire(new Vector3(0,6.02f,z),3.4f);
        kit.Sign("7 舍 · LG 北门","NORTH ENTRANCE / 沿中央楼梯前往 1F 南门",new Vector3(-4.9f,2.6f,11.70f),Vector3.back,4.4f,.85f);
        kit.Sign("1F · 南门","SOUTH ENTRANCE / 下楼前往 LG 北门",new Vector3(4.9f,5.05f,-11.70f),Vector3.forward,4.4f,.85f);
        foreach(float x in new[]{-6.7f,6.7f}){kit.Plant(new Vector3(x,0,8.2f),1);kit.Plant(new Vector3(x,2.85f,-8.2f),1);}
    }
    static void PersistMeshes(InteriorDetailKit kit,string id)
    {
        // Batch leaf decorations by material. Signs retain their text children.
        var leaves=kit.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform.childCount==0&&!f.GetComponent<Collider>()&&f.GetComponent<MeshRenderer>()).ToArray();
        foreach(var group in leaves.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name=id+" · "+group.Key.name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=kit.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            var go=new GameObject(mesh.name);go.transform.SetParent(kit.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var leaf in leaves)UnityEngine.Object.DestroyImmediate(leaf.gameObject);
        int i=0;
        foreach(var filter in kit.GetComponentsInChildren<MeshFilter>())
        {
            if(!filter.sharedMesh||AssetDatabase.Contains(filter.sharedMesh))continue;
            string path=Folder+"/Meshes/"+id+"-"+(i++).ToString("D4")+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(filter.sharedMesh,old);filter.sharedMesh=old;EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(filter.sharedMesh,path);
        }
    }
    static void MountLabels(InteriorSceneRoot root)
    {
        var boards=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!r.GetComponent<TextMesh>()&&
            (r.sharedMaterial==palette.teal||r.name.Contains("Sign")||r.name.Contains("Plate"))).ToArray();
        foreach(var text in root.GetComponentsInChildren<TextMesh>(true))
        {
            if(!text.name.StartsWith("Text_",StringComparison.Ordinal))continue;
            Vector3 normal=-text.transform.forward,position=text.transform.position;
            var board=boards.Where(r=>
            {
                var bounds=r.bounds;bounds.Expand(.012f);
                float depth=Vector3.Dot(new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)),bounds.extents);
                return depth<.22f&&bounds.Contains(position);
            }).OrderBy(r=>Vector3.Distance(r.bounds.center,position)).FirstOrDefault();
            if(!board)continue;
            var b=board.bounds;var a=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
            float halfDepth=Vector3.Dot(a,b.extents);
            text.transform.position+=normal*(halfDepth-Vector3.Dot(position-b.center,normal)+.008f);
            int rows=text.text.Split('\n').Length,longest=text.text.Split('\n').Max(s=>s.Length);
            float width=Mathf.Abs(normal.x)>.7f?b.size.z:b.size.x;
            float target=Mathf.Min(.20f,b.size.y*.55f/rows,width/Mathf.Max(4,longest*.65f));
            float current=text.GetComponent<MeshRenderer>().bounds.size.y/rows;
            if(current>.005f&&target>.025f)text.characterSize*=target/current;
        }
    }
    [MenuItem("Campus/Interiors/Refine Existing Library Cafeteria and Dormitories")]
    public static void Apply()
    {
        Palette();rendererIndex=Renderer();
        var paths=Directory.GetFiles("Assets/Interiors/Scenes","*.unity").Where(p=>p.Contains("cafeteria")||p.Contains("library")||p.Contains("dormitory")).Concat(new[]{"Assets/CampusPlanRevision/Dormitory7.unity"});
        var report=new StringBuilder("Existing entrances, floor slabs, stairs, ordering and reservations preserved.\n");
        foreach(string path in paths)
        {
            var scene=EditorSceneManager.OpenScene(path);var root=UnityEngine.Object.FindFirstObjectByType<InteriorSceneRoot>();
            string before=JsonUtility.ToJson(root);int colliders=root.GetComponentsInChildren<Collider>(true).Length;
            var kit=StartDetails(root);
            if(root.buildingId=="dormitory-7")Seventh(root,kit);
            else if(root.buildingId=="library"||root.buildingId=="cafeteria")
            {Materials(root);if(root.buildingId=="library")Library(root,kit);else Cafeteria(root,kit);}
            Presentation(root,root.buildingId=="library",root.buildingId.StartsWith("dormitory"));
            if(before!=JsonUtility.ToJson(root))throw new Exception("Travel contract changed: "+path);
            if(colliders!=root.GetComponentsInChildren<Collider>(true).Length)throw new Exception("Existing collision envelope changed: "+path);
            MountLabels(root);PersistMeshes(kit,root.buildingId);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            report.AppendLine(root.buildingId+": contract and "+colliders+" colliders unchanged; palette, fixtures, details and presentation updated.");
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Builds/Windows/QA/InteriorRefinement");
        File.WriteAllText("Builds/Windows/QA/InteriorRefinement/editor-result.txt",report.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");Debug.Log("INTERIOR_REFINEMENT_APPLIED");
    }
    public static void ApplyAndBuild(){Apply();CampusBuilder.Build();}
    public static void ReviewAndBuild()
    {
        Apply();Directory.CreateDirectory("Builds/Windows/QA/InteriorRefinement/After");
        foreach(var id in new[]{"cafeteria","library","dormitory-7"})
        {
            var scene=EditorSceneManager.OpenScene(id=="dormitory-7"?"Assets/CampusPlanRevision/Dormitory7.unity":"Assets/Interiors/Scenes/"+id+".unity");
            var root=UnityEngine.Object.FindFirstObjectByType<InteriorSceneRoot>();
            if(id=="cafeteria")Capture(root,new Vector3(-13,1.8f,-2),new Vector3(1,1.7f,4),"After/cafeteria-1F");
            else if(id=="library")
            {Capture(root,new Vector3(0,1.8f,-10),new Vector3(-3,1.8f,8),"After/library-lobby");Capture(root,new Vector3(46,6.1f,-7),new Vector3(33,5.9f,3),"After/library-reading");}
            else Capture(root,new Vector3(-5,1.8f,9),new Vector3(0,2.6f,-6),"After/seventh-LG");
        }
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");CampusBuilder.Build();
    }
    public static void Audit()
    {
        Directory.CreateDirectory("Builds/Windows/QA/InteriorRefinement/Before");
        var report=new StringBuilder();
        foreach(var id in new[]{"cafeteria","library","dormitory-7"})
        {
            string path="Assets/Interiors/Scenes/"+id+".unity";
            if(!File.Exists(path))continue;
            var scene=EditorSceneManager.OpenScene(path);
            var contract=UnityEngine.Object.FindFirstObjectByType<InteriorSceneRoot>();
            report.AppendLine("SCENE "+path+" root="+contract.transform.position);
            foreach(var child in contract.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==contract.transform||t.parent&&t.parent.parent==contract.transform))
                report.AppendLine(child.name+" @ "+child.localPosition);
            foreach(var m in contract.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct())
                report.AppendLine("MAT "+m.name+" | "+m.shader.name+" | "+AssetDatabase.GetAssetPath(m));
            foreach(var spawn in contract.spawnPoints)report.AppendLine("SPAWN "+spawn.id+" "+spawn.anchor.position);
            if(id=="cafeteria")
            {
                Capture(contract,new Vector3(-13,1.8f,-2),new Vector3(1,1.7f,4),"Before/cafeteria-1F");
                Capture(contract,new Vector3(0,6,-6),new Vector3(-6,5.6f,5),"Before/cafeteria-2F");
            }
            if(id=="library")
            {
                Capture(contract,new Vector3(0,1.8f,-10),new Vector3(-3,1.8f,8),"Before/library-lobby");
                Capture(contract,new Vector3(30,6.1f,-8),new Vector3(36,5.9f,4),"Before/library-reading");
            }
        }
        File.WriteAllText("Builds/Windows/QA/InteriorRefinement/audit.txt",report.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        Debug.Log("INTERIOR_REFINEMENT_AUDIT_COMPLETE");
    }
    static void Capture(InteriorSceneRoot contract,Vector3 at,Vector3 target,string name)
    {
        var go=new GameObject("Interior audit camera");var cam=go.AddComponent<Camera>();
        cam.transform.position=contract.transform.TransformPoint(at);cam.transform.LookAt(contract.transform.TransformPoint(target));
        cam.fieldOfView=66;cam.nearClipPlane=.07f;cam.farClipPlane=140;
        var data=cam.GetUniversalAdditionalCameraData();var presentation=contract.GetComponent<InteriorPresentation>();
        data.SetRenderer(presentation?presentation.rendererIndex:-1);data.renderPostProcessing=true;
        var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;cam.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
        File.WriteAllBytes("Builds/Windows/QA/InteriorRefinement/"+name+".png",image.EncodeToPNG());
        cam.targetTexture=null;RenderTexture.active=old;rt.Release();
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
    }
}
