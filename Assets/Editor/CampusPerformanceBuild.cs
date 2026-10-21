using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class CampusPerformanceBuild
{
    public static void Build(){PlayerSettings.enableFrameTimingStats=true;CampusBuilder.Build();}
    public static void Optimize()
    {
        PlayerSettings.gpuSkinning=true;PlayerSettings.enableFrameTimingStats=true;
        QualitySettings.lodBias=1.1f;
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CampusURP.asset");
        pipeline.msaaSampleCount=2;pipeline.shadowDistance=85;pipeline.shadowCascadeCount=2;
        var so=new SerializedObject(pipeline);var quality=so.FindProperty("m_SoftShadowQuality");if(quality!=null)quality.intValue=1;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();
    }
    public static void OptimizeAndBuild(){Optimize();CampusBuilder.Build();}
}
