using UnityEngine;
using System.Collections.Generic;

// A font atlas is rebuilt at runtime. Keep depth-tested lettering bound to that atlas
// without changing the font's shared GUI material (HUD text must stay on screen).
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
public sealed class InteriorWorldLabel : MonoBehaviour
{
    Material glyphs, original;
    Font font;
    MeshRenderer target;
    sealed class SharedGlyphs{public Material material;public int users;}
    static readonly Dictionary<Font,SharedGlyphs> cache=new Dictionary<Font,SharedGlyphs>();

    void OnEnable()
    {
        var text = GetComponent<TextMesh>();
        font = text.font;
        target = GetComponent<MeshRenderer>();
        var template = Resources.Load<Material>("InteriorWorldText");
        if (!font || !template) return;
        original = target.sharedMaterial;
        if(!cache.TryGetValue(font,out var shared))
        {shared=new SharedGlyphs{material=new Material(template){name="Shared depth-tested world lettering"}};cache.Add(font,shared);}
        shared.users++;glyphs=shared.material;
        font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
        glyphs.mainTexture = font.material.mainTexture;
        target.sharedMaterial = glyphs;
        Font.textureRebuilt += Refresh;
    }

    void Refresh(Font rebuilt)
    {
        if (rebuilt == font && glyphs) glyphs.mainTexture = font.material.mainTexture;
    }

    void OnDisable()
    {
        Font.textureRebuilt -= Refresh;
        if (target && target.sharedMaterial == glyphs) target.sharedMaterial = original;
        if(font&&glyphs&&cache.TryGetValue(font,out var shared)&&--shared.users==0)
        {cache.Remove(font);Destroy(shared.material);}
        glyphs = null;
    }
}
