using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Targeted changes to saved scenes, preserving travel contracts and gameplay anchors.
public static class CampusModernizationBuilder
{
    const string Output="Builds/Windows/QA/Modernization";
    public static void Audit()
    {
        Directory.CreateDirectory(Output);var report=new StringBuilder();
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game=Object.FindFirstObjectByType<CampusGame>();
        foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var r=f.GetComponent<MeshRenderer>();if(!r||!r.enabled||!r.gameObject.activeInHierarchy||!f.sharedMesh)continue;
            var name=f.name+" "+string.Join(" ",r.sharedMaterials.Where(m=>m).Select(m=>m.name));
            var mesh=f.sharedMesh;var v=mesh.vertices;int near=0,high=0;
            foreach(var p in v){var q=game.WorldToMap(f.transform.TransformPoint(p));if(q.x>87&&q.x<211&&q.z>6.7f&&q.z<13.3f){near++;if(q.y>4.9f)high++;}}
            if(high>0||near>0&&IsVegetation(name))report.AppendLine("BRIDGE "+Path(f.transform)+" | "+AssetDatabase.GetAssetPath(mesh)+" | near="+near+" high="+high+" | "+name+" | "+string.Join(",",r.sharedMaterials.Where(m=>m).Select(m=>m.shader.name)));
        }
        foreach(string id in new[]{"library","cafeteria"})
        {
            EditorSceneManager.OpenScene("Assets/Interiors/Scenes/"+id+".unity");
            var root=Object.FindFirstObjectByType<InteriorSceneRoot>();report.AppendLine("SCENE "+id);
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==root.transform||t.parent&&t.parent.parent==root.transform))report.AppendLine(Path(t)+" "+t.localPosition);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.Contains("Tread")||r.name=="Half_Landing"||r.name.Contains("Counter")||r.name.Contains("Table_Top")).Take(32))report.AppendLine("OBJECT "+Path(r.transform)+" local="+root.transform.InverseTransformPoint(r.bounds.center)+" size="+r.bounds.size);
        }
        File.WriteAllText(Output+"/audit.txt",report.ToString());EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");Debug.Log("MODERNIZATION_AUDIT_COMPLETE");
    }
    static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
    static bool IsVegetation(string name)=>new[]{"grass","leaf","foliage","shrub","flower","garden-plants","植被","杂草"}.Any(n=>name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0);
    const string Group="Modern campus interiors · 2026";
    const string Folder="Assets/CampusModernization";
    static InteriorRefinementPalette p;
    static Material[] accents;
    static Material Accent(string name,string color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Campus/RefinedInterior")){name=name};AssetDatabase.CreateAsset(m,path);}
        ColorUtility.TryParseHtmlString(color,out var c);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.22f);m.SetFloat("_Kind",3);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    [MenuItem("Campus/Interiors/Modernize Existing Rooms and Clear Bridge")]
    public static void Apply()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        p=InteriorRefinementPalette.Load();if(!p)throw new Exception("Missing existing interior palette");
        accents=new[]{Accent("Warm clay","#af8967"),Accent("Eucalyptus","#78998c"),Accent("Slate blue","#637e96"),Accent("Muted mulberry","#8e727b")};
        var report=new StringBuilder();
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        report.AppendLine(ClearBridgePassage(Object.FindFirstObjectByType<CampusGame>()));
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        foreach(var id in new[]{"library","cafeteria"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Interiors/Scenes/"+id+".unity");var root=Object.FindFirstObjectByType<InteriorSceneRoot>();
            var contract=JsonUtility.ToJson(root);int colliderCount=root.GetComponentsInChildren<Collider>(true).Length;
            var old=root.transform.Find(Group);if(old)Object.DestroyImmediate(old.gameObject);
            var kit=InteriorDetailKit.Create(root.transform,Group);kit.palette=p;
            if(id=="library"){report.AppendLine(RepairLibraryStairs(root));Library(root,kit);}else Cafeteria(root,kit);
            InteriorRefinementBuilder.PersistMeshes(kit,"modern-"+id);
            if(contract!=JsonUtility.ToJson(root)||colliderCount!=root.GetComponentsInChildren<Collider>(true).Length)throw new Exception("Travel contract or collider count changed: "+id);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            report.AppendLine(id+": all floors modernized; travel, existing seats, ordering and collision count retained.");
        }
        AssetDatabase.SaveAssets();File.WriteAllText(Output+"/apply-result.txt",report.ToString());
        LibraryWalkTest.Run();
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");Debug.Log("MODERNIZATION_APPLIED");
    }
    public static void ApplyAndBuild(){Apply();CampusBuilder.Build();}
    public static void FinishAndBuild()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Interiors/Scenes/library.unity");
        var root=Object.FindFirstObjectByType<InteriorSceneRoot>();
        var dome=root.transform.Find("Library_Interior/Round_Tower_Roof/Round_Dome");
        if(!dome)throw new Exception("Missing reading hall dome");
        // The full sphere previously hung 1.4m below the roof slab, hiding the
        // fourth-storey ceiling fittings. Keep its underside above the roof.
        var position=dome.localPosition;position.y=19f;dome.localPosition=position;
        HideFloatingFloorJoints(root);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");CampusBuilder.Build();
    }
    static string RepairLibraryStairs(InteriorSceneRoot root)
    {
        HideFloatingFloorJoints(root);
        int count=0;
        foreach(var tread in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("A_Tread_")||t.name.StartsWith("B_Tread_")))
        {
            var riser=tread.parent.Find(tread.name.Replace("Tread","Riser"));if(!riser)throw new Exception("Missing paired riser");
            // Absolute dimensions make the repair repeatable. Preserve the walking top.
            float height=4.3f/22-.031f;var pos=riser.localPosition;pos.y=tread.localPosition.y-.015f-.001f-height/2;riser.localPosition=pos;
            var scale=riser.localScale;scale.y=height;riser.localScale=scale;
            if(riser.GetComponent<Renderer>().bounds.max.y>=tread.GetComponent<Renderer>().bounds.min.y-.0005f)throw new Exception("Stair separation failed");count++;
        }
        if(count!=66)throw new Exception("Expected 66 library treads, found "+count);
        return "Library: all 66 tread/riser pairs now separated by 1 mm; tread top and landing levels preserved.";
    }
    static void HideFloatingFloorJoints(InteriorSceneRoot root)
    {
        // The old full-width seam strips crossed the real stair openings. The
        // metre-scaled floor shader already draws joints on the actual slabs.
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            if(r.name.StartsWith("Joint_X_")||r.name.StartsWith("Joint_Z_"))r.enabled=false;
    }
    static InteriorDetailKit At(InteriorDetailKit kit,string name,Vector3 at,Quaternion rotation)
    {var child=InteriorDetailKit.Create(kit.transform,name);child.palette=p;child.transform.localPosition=at;child.transform.localRotation=rotation;return child;}
    static void Library(InteriorSceneRoot root,InteriorDetailKit kit)
    {
        string[] titles={"综合服务","书海阅览","数字阅读","深度研读"};
        for(int floor=0;floor<4;floor++)
        {
            float y=floor*4.3f;var accent=accents[floor];
            // The west wall has a corridor door at Z=6 on 2F/3F; artwork stays south of it.
            kit.WallComposition(new Vector3(-19.73f,y+2.15f,-5.8f),Vector3.right,accent,4.6f);
            for(int i=0;i<20;i++)kit.Box("West acoustic oak slat",new Vector3(-19.73f,y+1.95f,-11.5f+i*.13f),new Vector3(.08f,3.3f,.045f),p.oak,.008f);
            kit.Box("West wall recessed light",new Vector3(-19.67f,y+3.60f,-7.8f),new Vector3(.04f,.025f,12.6f),p.light,.004f);
            kit.Box("North ceiling shadow line",new Vector3(-10,y+3.91f,15.42f),new Vector3(17,.07f,.14f),p.graphite);
            kit.Box("North cove diffuser",new Vector3(-10,y+3.85f,15.40f),new Vector3(16.8f,.014f,.05f),p.light,.003f);
            var board=At(kit,"Library floor identity",new Vector3(5.16f,y+2.55f,-10.0f),Quaternion.Euler(0,-90,0));
            board.Box("Floor marker",Vector3.zero,new Vector3(1.6f,1.25f,.05f),accent,.035f);
            board.Text(board.transform,(floor+1)+"F",new Vector3(-.34f,.12f,-.033f),.46f,Color.white);
            board.Text(board.transform,titles[floor],new Vector3(.25f,.10f,-.034f),.17f,Color.white);
            board.Text(board.transform,"LIBRARY · 阅读与思考",new Vector3(0,-.37f,-.034f),.085f,new Color(.91f,.92f,.90f));
            if(floor>0)
            {
                // Fluted crown over the ring workstations; no new floor objects in aisles.
                for(int i=0;i<40;i++)
                {
                    float a=i*Mathf.PI/20;var baffle=kit.Box("Radial acoustic ceiling baffle",new Vector3(34.5f+Mathf.Cos(a)*11,y+3.70f,Mathf.Sin(a)*11),new Vector3(.055f,.24f,1.75f),i%5==0?accent:p.oak,.008f);
                    baffle.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg+90,0);
                }
            }
        }
        // Existing table positions and chair/seat components remain authoritative.
        int index=0;
        foreach(var top in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Top"&&r.transform.parent.name.Contains("Round_Table")).ToArray())
        {
            int floor=Mathf.Clamp(Mathf.RoundToInt(root.transform.InverseTransformPoint(top.bounds.center).y/4.3f),1,3);
            var desk=At(kit,"Reading desktop details",root.transform.InverseTransformPoint(top.transform.position),Quaternion.Inverse(root.transform.rotation)*top.transform.rotation);
            desk.Box("Felt writing mat",new Vector3(-.48f,.029f,-.13f),new Vector3(.62f,.008f,.39f),accents[floor],.012f);
            desk.Box("Open reference page",new Vector3(-.48f,.036f,-.13f),new Vector3(.37f,.006f,.27f),p.linen,.002f);
            for(int line=0;line<5;line++)desk.Box("Page printed line",new Vector3(-.48f,.040f,-.21f+line*.035f),new Vector3(.27f,.001f,.003f),p.walnut,.0002f);
            if(index++%5==0)desk.Laptop(new Vector3(.5f,.030f,-.10f),accents[2]);
        }
        // Give the existing upholstered seats a subtle storey-specific identity.
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.Contains("Chair_Seat")||r.name.Contains("Chair_Back")))
        {int floor=Mathf.Clamp(Mathf.RoundToInt(root.transform.InverseTransformPoint(r.bounds.center).y/4.3f),0,3);r.sharedMaterial=accents[floor];}
    }
    static void Cafeteria(InteriorSceneRoot root,InteriorDetailKit kit)
    {
        string[] titles={"日常食光","风味餐厅","轻食与鲜果"};
        for(int floor=0;floor<3;floor++)
        {
            float y=floor*4.3f;
            kit.WallComposition(new Vector3(-17.70f,y+2.25f,6.7f),Vector3.right,accents[floor],3.5f);
            // Thin wall finishes leave the central queues and revised stairs untouched.
            for(int i=0;i<27;i++)kit.Box("Dining oak feature rib",new Vector3(-17.72f,y+2.15f,9.15f+i*.11f),new Vector3(.065f,2.35f,.035f),p.oak,.005f);
            kit.Box("Dining wall accent band",new Vector3(-17.70f,y+.72f,5.7f),new Vector3(.05f,1.4f,14.4f),accents[floor],.008f);
            kit.Box("Wall cap diffuser",new Vector3(-17.65f,y+1.46f,5.7f),new Vector3(.02f,.018f,14.4f),p.light,.003f);
            var sign=At(kit,"Dining floor identity",new Vector3(-17.61f,y+2.55f,.2f),Quaternion.Euler(0,-90,0));
            sign.Box("Dining identity board",Vector3.zero,new Vector3(3.2f,1.1f,.07f),p.graphite,.04f);
            sign.Box("Floor colour tab",new Vector3(-1.42f,0,-.04f),new Vector3(.12f,.95f,.012f),accents[floor],.006f);
            sign.Text(sign.transform,(floor+1)+"F  "+titles[floor],new Vector3(0,.18f,-.045f),.27f,Color.white);
            sign.Text(sign.transform,"用餐 · 交流 · 校园日常",new Vector3(0,-.27f,-.046f),.12f,new Color(.80f,.84f,.84f));
            for(int i=0;i<8;i++)kit.Box("Ceiling colour acoustic blade",new Vector3(-12.4f+i*1.65f,y+3.56f,7.3f),new Vector3(.06f,.28f,3.2f),accents[floor],.012f);
        }
        int index=0;
        foreach(var top in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Table_Top").ToArray())
        {
            var pos=root.transform.InverseTransformPoint(top.bounds.center);int floor=Mathf.Clamp(Mathf.FloorToInt(pos.y/4.3f),0,2);
            var desk=At(kit,"Dining tabletop ensemble",new Vector3(pos.x,pos.y+top.bounds.extents.y,pos.z),Quaternion.identity);
            if(index++%3==0)
            {
                desk.Box("Non-slip serving tray",new Vector3(0,.012f,0),new Vector3(.52f,.024f,.32f),accents[floor],.018f);
                desk.Disc("Ceramic dinner plate",new Vector3(-.04f,.025f,0),.115f,.014f,p.linen);
                desk.Disc("Plate shallow inset",new Vector3(-.04f,.040f,0),.086f,.002f,p.ceiling);
                desk.Disc("Water glass",new Vector3(.18f,.026f,.08f),.033f,.105f,p.glass);
            }
            else
            {
                desk.Box("Napkin dispenser",new Vector3(0,.07f,0),new Vector3(.19f,.14f,.10f),p.plaster,.02f);
                desk.Box("Napkin opening",new Vector3(0,.142f,0),new Vector3(.13f,.003f,.012f),p.graphite,.002f);
                desk.Box("Paper napkin",new Vector3(0,.162f,0),new Vector3(.10f,.035f,.008f),p.linen,.002f);
            }
        }
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.Contains("Chair_Seat")||r.name=="Seat"||r.name=="Back"))
        {int floor=Mathf.Clamp(Mathf.FloorToInt(root.transform.InverseTransformPoint(r.bounds.center).y/4.3f),0,2);r.sharedMaterial=accents[floor];}
    }

    struct Vertex
    {public Vector3 p,n;public Vector2 uv;public Color c;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
    static float Plane(Vector3 p,int side)=>side==0?p.x-88:side==1?211-p.x:side==2?p.z-6.65f:side==3?13.35f-p.z:side==4?p.y-.25f:10.5f-p.y;
    static List<Vertex> Clip(List<Vertex> poly,int side,bool inside,Func<Vertex,Vector3> map)
    {
        var result=new List<Vertex>();for(int i=0;i<poly.Count;i++)
        {var a=poly[i];var b=poly[(i+1)%poly.Count];float da=Plane(map(a),side),db=Plane(map(b),side);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)result.Add(a);if(ia!=ib)result.Add(Vertex.Lerp(a,b,da/(da-db)));}return result;
    }
    public static string ClearBridgePassage(CampusGame game)
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();int removed=0,assets=0;
        // Legacy combined scenery lost its original plant names. Clip only these
        // retained batches; the new covered bridge, street and all collision stay intact.
        foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.name=="Retained west-campus surroundings"||f.name=="Road clearance surroundings"))
        {
            var r=f.GetComponent<MeshRenderer>();if(!r||!r.enabled||!f.sharedMesh)continue;
            var source=f.sharedMesh;var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var colors=source.colors;
            var output=new List<Vertex>();var indices=new List<int>[source.subMeshCount];int trimmed=0;
            Vertex V(int i)=>new Vertex{p=vertices[i],n=normals.Length==vertices.Length?normals[i]:Vector3.up,uv=uv.Length==vertices.Length?uv[i]:Vector2.zero,c=colors.Length==vertices.Length?colors[i]:Color.white};
            Vector3 Map(Vertex v)=>game.WorldToMap(f.transform.TransformPoint(v.p));
            void Append(List<Vertex> poly,List<int> ids){for(int i=1;i<poly.Count-1;i++){int at=output.Count;output.Add(poly[0]);output.Add(poly[i]);output.Add(poly[i+1]);ids.AddRange(new[]{at,at+1,at+2});}}
            for(int s=0;s<source.subMeshCount;s++)
            {
                indices[s]=new List<int>();var tri=source.GetTriangles(s);
                for(int i=0;i<tri.Length;i+=3)
                {
                    var original=new List<Vertex>{V(tri[i]),V(tri[i+1]),V(tri[i+2])};var remain=original;var pieces=new List<List<Vertex>>();
                    for(int side=0;side<6&&remain.Count>0;side++){pieces.Add(Clip(remain,side,false,Map));remain=Clip(remain,side,true,Map);}
                    if(remain.Count>=3){trimmed++;foreach(var poly in pieces)Append(poly,indices[s]);}else Append(original,indices[s]);
                }
            }
            if(trimmed==0)continue;
            var mesh=new Mesh{name="Bridge passage clear · "+source.name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.c).ToList());mesh.subMeshCount=indices.Length;for(int s=0;s<indices.Length;s++)mesh.SetTriangles(indices[s],s);mesh.RecalculateBounds();
            string path=Folder+"/BridgeClear-"+source.GetInstanceID()+".asset";
            // Asset GUIDs, unlike instance IDs, remain stable on subsequent editor runs.
            path=Folder+"/BridgeClear-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".asset";
            AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;removed+=trimmed;assets++;
        }
        return "Bridge: clipped "+removed+" legacy scenery triangles from the pedestrian passage in "+assets+" batches; bridge structure, stairs and road untouched.";
    }
}
