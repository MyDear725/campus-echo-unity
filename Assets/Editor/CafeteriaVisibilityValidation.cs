using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CafeteriaVisibilityValidation
{
    const string Output="output/食堂文字楼梯验证";
    public static void Run(Transform root)
    {
        // Treads and risers must not have coincident horizontal faces.
        var stairs=root.Find("Cafeteria_MultiFloor_Interior/00_Vertical_Circulation");
        foreach(Transform stair in stairs)
        foreach(string flight in new[]{"A","B"})
        for(int i=0;i<11;i++){
            var riser=stair.Find(flight+"_Riser_"+i).GetComponent<Renderer>().bounds;
            var tread=stair.Find(flight+"_Tread_"+i).GetComponent<Renderer>().bounds;
            if(riser.max.y>=tread.min.y-.0002f)throw new Exception("Coplanar stair faces: "+stair.name);
        }
        var material=Resources.Load<Material>("InteriorWorldText");
        foreach(var label in root.GetComponentsInChildren<TextMesh>(true))
            if(label.GetComponent<Renderer>().sharedMaterial!=material)throw new Exception("Non-depth-tested label "+label.name);
        TestOcclusion(material);
    }
    static void TestOcclusion(Material template)
    {
        var group=new GameObject("TemporaryTextOcclusionValidation");
        var camObject=new GameObject("ValidationCamera");camObject.transform.SetParent(group.transform);
        var cam=camObject.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
        cam.orthographic=true;cam.orthographicSize=1.5f;cam.nearClipPlane=.1f;cam.farClipPlane=10;
        cam.transform.position=new Vector3(100,1,-4);cam.transform.rotation=Quaternion.identity;
        var rt=new RenderTexture(640,400,24);cam.targetTexture=rt;
        var previous=RenderTexture.active;
        var textObject=new GameObject("VisibleTestText");textObject.transform.SetParent(group.transform);
        textObject.transform.position=new Vector3(100,1,0);
        var text=textObject.AddComponent<TextMesh>();text.font=Resources.Load<Font>("CampusFont");
        text.text="HHHH";text.fontSize=96;text.characterSize=.12f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
        text.font.RequestCharactersInTexture(text.text,text.fontSize);
        var glyphs=new Material(template);glyphs.mainTexture=text.font.material.mainTexture;text.GetComponent<Renderer>().sharedMaterial=glyphs;
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(group.transform);
        var opaque=new Material(Shader.Find("Universal Render Pipeline/Unlit"));opaque.SetColor("_BaseColor",Color.black);
        blocker.GetComponent<Renderer>().sharedMaterial=opaque;blocker.SetActive(false);
        try{
            int visible=Count(cam,rt,"text_visible");if(visible<100)throw new Exception("Occlusion test has no visible lettering");
            var bounds=text.GetComponent<Renderer>().bounds;
            blocker.SetActive(true);blocker.transform.position=new Vector3(bounds.min.x+bounds.size.x/4,1,-.3f);
            blocker.transform.localScale=new Vector3(bounds.size.x/2,3,.1f);
            int half=Count(cam,rt,"text_half_occluded");
            if(half<visible*.25f||half>visible*.75f)throw new Exception($"Partial occlusion failed: {half}/{visible}");
            blocker.transform.position=new Vector3(100,1,-.3f);blocker.transform.localScale=new Vector3(6,3,.1f);
            int hidden=Count(cam,rt,"text_hidden");if(hidden>visible*.01f)throw new Exception("Text visible through opaque blocker");
            foreach(float angle in new[]{-25f,-10f,10f,25f}){
                cam.transform.position=new Vector3(100,1,0)+Quaternion.Euler(0,angle,0)*Vector3.back*4;
                cam.transform.LookAt(new Vector3(100,1,0));
                if(Count(cam,rt,null)>visible*.01f)throw new Exception("Occlusion failed while rotating camera");
            }
            File.WriteAllText(Output+"/visibility.txt",$"PASS: 88 tread/riser pairs separated.\nPASS: visible={visible}, half occluded={half}, fully occluded={hidden}.\nPASS: opaque blocker hides text at -25, -10, 10, 25 degree camera angles.\n");
        }finally{
            RenderTexture.active=previous;cam.targetTexture=null;rt.Release();
            Object.DestroyImmediate(group);Object.DestroyImmediate(rt);Object.DestroyImmediate(glyphs);Object.DestroyImmediate(opaque);
        }
    }
    static int Count(Camera camera, RenderTexture rt, string filename)
    {
        camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
        int count=image.GetPixels32().Count(p=>p.r>180&&p.g>180&&p.b>180);
        if(filename!=null)File.WriteAllBytes(Output+"/"+filename+".png",image.EncodeToPNG());
        Object.DestroyImmediate(image);return count;
    }
}
