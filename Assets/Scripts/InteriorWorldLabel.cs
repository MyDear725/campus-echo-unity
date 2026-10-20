using UnityEngine;

// Font glyphs must use depth-tested world lettering, rather than the GUI text shader.
[RequireComponent(typeof(TextMesh))]
public sealed class InteriorWorldLabel : MonoBehaviour
{
    Material glyphs;Font font;
    void Awake()
    {
        var palette=InteriorRefinementPalette.Load();var text=GetComponent<TextMesh>();font=text.font?text.font:palette.font;
        font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
        glyphs=new Material(Shader.Find("Campus/WallLettering"));glyphs.mainTexture=font.material.mainTexture;
        GetComponent<MeshRenderer>().sharedMaterial=glyphs;
    }
    void OnEnable(){Font.textureRebuilt+=Refresh;}
    void OnDisable(){Font.textureRebuilt-=Refresh;}
    void Refresh(Font rebuilt){if(rebuilt==font&&glyphs)glyphs.mainTexture=font.material.mainTexture;}
    void OnDestroy(){if(glyphs)Destroy(glyphs);}
}
