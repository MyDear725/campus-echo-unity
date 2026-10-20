using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Adds screen-space ambient occlusion to the campus URP renderer so furniture, shelves
// and wall corners get contact shadows instead of floating. Also keeps the colour
// grading neutral so nothing gets bloom-washed.
public static class LibraryPostFX
{
    const string RendererPath = "Assets/Settings/CampusRenderer.asset";

    [MenuItem("Campus/Rendering/Enable SSAO on Campus Renderer")]
    public static void Enable()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (!renderer) { Debug.LogError("CampusRenderer.asset not found"); return; }

        ScreenSpaceAmbientOcclusion existing = null;
        foreach (var f in renderer.rendererFeatures)
            if (f is ScreenSpaceAmbientOcclusion ssao) existing = ssao;

        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            existing.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(existing, renderer);
            renderer.rendererFeatures.Add(existing);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        // Modest settings: shape without dark halos.
        var so = new SerializedObject(existing);
        SetEnum(so, "m_Settings.AOMethod", 1);          // Blue Noise
        SetFloat(so, "m_Settings.Intensity", 0.85f);
        SetFloat(so, "m_Settings.Radius", 0.55f);
        SetEnum(so, "m_Settings.SampleCount", 1);       // Medium
        SetBool(so, "m_Settings.Downsample", true);
        SetBool(so, "m_Settings.AfterOpaque", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LIBRARY_SSAO_ENABLED");
    }

    static void SetFloat(SerializedObject so, string path, float v)
    {
        var p = so.FindProperty(path); if (p != null) p.floatValue = v;
    }
    static void SetBool(SerializedObject so, string path, bool v)
    {
        var p = so.FindProperty(path); if (p != null) p.boolValue = v;
    }
    static void SetEnum(SerializedObject so, string path, int v)
    {
        var p = so.FindProperty(path); if (p != null) p.enumValueIndex = v;
    }
}
