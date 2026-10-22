using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Shared setup used by the cafeteria and library builders: creates a dedicated
// interior renderer (copy of the campus one plus SSAO), an ACES tone-mapped volume and
// a reflection probe, and wires InteriorPresentation on the scene root.
//
// Everything is created as a real asset under the building folder / Settings so the
// result survives scene reload and a regeneration run.
public static class InteriorPresentationSetup
{
    public const string InteriorRendererPath = "Assets/Settings/InteriorRenderer_Shared.asset";

    // Builds (or refreshes) a renderer with URP SSAO tuned for interior contact shadows.
    public static int EnsureInteriorRenderer(float aoRadius, out UniversalRendererData renderer)
    {
        renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(InteriorRendererPath);
        if (!renderer)
        {
            var src = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/CampusRenderer.asset");
            renderer = Object.Instantiate(src);
            renderer.name = "Interior Renderer";
            renderer.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(renderer, InteriorRendererPath);
        }

        ScreenSpaceAmbientOcclusion ao = null;
        foreach (var f in renderer.rendererFeatures)
            if (f is ScreenSpaceAmbientOcclusion found) ao = found;

        if (!ao)
        {
            ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ao.name = "Interior contact shadows";
            AssetDatabase.AddObjectToAsset(ao, renderer);
            renderer.rendererFeatures.Add(ao);
        }

        var so = new SerializedObject(ao);
        var settingsProp = so.FindProperty("m_Settings");
        settingsProp.FindPropertyRelative("Source").enumValueIndex = 0;
        settingsProp.FindPropertyRelative("NormalSamples").enumValueIndex = 2;
        settingsProp.FindPropertyRelative("AOMethod").enumValueIndex = 1;
        settingsProp.FindPropertyRelative("Intensity").floatValue = 0.8f;
        settingsProp.FindPropertyRelative("Radius").floatValue = aoRadius;
        settingsProp.FindPropertyRelative("Falloff").floatValue = 55;
        settingsProp.FindPropertyRelative("Downsample").boolValue = false;
        settingsProp.FindPropertyRelative("AfterOpaque").boolValue = true;
        settingsProp.FindPropertyRelative("Samples").enumValueIndex = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        ao.SetActive(true);
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();

        return EnsureRendererIndex(renderer);
    }

    static int EnsureRendererIndex(UniversalRendererData renderer)
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CampusURP.asset");
        if (!pipeline) return 1;

        var so = new SerializedObject(pipeline);
        var list = so.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;

        if (index < 0)
        {
            index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }
        return index;
    }

    // ACES volume matching the teaching interiors: mild exposure lift, a little
    // contrast, slight desaturation, and a restrained bloom that only catches lamps.
    public static VolumeProfile EnsureVolume(string path, float postExposure, float contrast,
                                             float saturation, float bloomIntensity, float bloomThreshold)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        foreach (var c in profile.components) Object.DestroyImmediate(c, true);
        profile.components.Clear();

        var color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(contrast);
        color.postExposure.Override(postExposure);
        color.saturation.Override(saturation);

        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.7f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // Reflection probe covering the interior volume.
    public static ReflectionProbe EnsureProbe(Transform parent, string name, Vector3 center, Vector3 size,
                                              Color clearColor)
    {
        var existing = parent.Find(name);
        GameObject go = existing ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;

        var probe = go.GetComponent<ReflectionProbe>();
        if (!probe) probe = go.AddComponent<ReflectionProbe>();

        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;   // baked once at start
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.resolution = 128;
        probe.size = size;
        probe.boxProjection = true;
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = clearColor;
        probe.nearClipPlane = 0.15f;
        probe.farClipPlane = Mathf.Max(size.x, size.z);
        probe.intensity = 0.7f;
        probe.hdr = true;
        return probe;
    }

    // Removes legacy placeholder lights left over from the grey-box pass.
    public static int RemovePlaceholderLights(Scene scene)
    {
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.name.StartsWith("Placeholder lighting") || l.name.StartsWith("Placeholder Light"))
                {
                    Object.DestroyImmediate(l.gameObject);
                    removed++;
                }
            }
        }
        return removed;
    }
}
