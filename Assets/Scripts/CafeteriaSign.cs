using UnityEngine;

// Physical wall lettering. Dynamic OS glyphs are requested at runtime so Chinese
// remains readable without copying a proprietary font into the Git repository.
[RequireComponent(typeof(TextMesh))]
public class CafeteriaSign : MonoBehaviour
{
    static Font lettering;
    public Material surface;
    Material glyphs;
    void Awake() {
        if(!lettering)lettering=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Arial"},96);
        var text=GetComponent<TextMesh>();text.font=lettering;text.fontSize=96;
        glyphs=new Material(surface);RefreshAtlas(lettering);
        GetComponent<MeshRenderer>().sharedMaterial=glyphs;
    }
    void OnEnable(){Font.textureRebuilt+=RefreshAtlas;}
    void OnDisable(){Font.textureRebuilt-=RefreshAtlas;}
    void RefreshAtlas(Font font){if(font==lettering&&glyphs)glyphs.mainTexture=font.material.mainTexture;}
    void OnDestroy(){if(glyphs)Destroy(glyphs);}
}
