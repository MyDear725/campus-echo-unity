using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Reproducible views of the saved campus. Does not enter Play Mode or touch saves.
public static class CampusEnvironmentReview
{
    const string Output = "QA/EnvironmentQuality";
    public static void Baseline() => Capture("before", false);
    public static void Validate()
    {
        Improved();
        WelcomeIntegrationRunner.Run();
    }
    public static void Improved()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CampusURP.asset");
        pipeline.msaaSampleCount = 4;
        pipeline.shadowDistance = 100;
        pipeline.shadowCascadeCount = 4;
        pipeline.mainLightShadowmapResolution = 4096;
        var settings = new SerializedObject(pipeline);
        settings.FindProperty("m_SoftShadowQuality").intValue = 2;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        Capture("after", true);
    }

    static void Capture(string prefix, bool improved)
    {
        Directory.CreateDirectory(Output);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game = Object.FindFirstObjectByType<CampusGame>();
        var camera = game.followCamera;
        camera.enabled = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        Shader.SetGlobalFloat("_CampusNightAmount", 0);
        Shader.SetGlobalInt("_CampusLampCount", 0);
        if (improved)
        {
            CampusEnvironmentPresentation.CreateProfile(game.gameObject);
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.sharedMaterials.Any(m => m && m.HasProperty("_CampusOutdoorSurface"))) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetFloat("_CampusOutdoorSurface", 1);
                renderer.SetPropertyBlock(block);
            }
        }
        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
        sun.transform.rotation = Quaternion.Euler(60, -35, 0);
        sun.intensity = 1.15f;
        sun.color = new Color(1, .96f, .86f);
        Shot(camera, game, prefix + "-courtyard", new Vector3(251.5f, 13, -69), new Vector3(251.5f, 1, -105));
        Shot(camera, game, prefix + "-avenue", new Vector3(86, 2.1f, 10), new Vector3(35, 1.6f, 10));
        Shot(camera, game, prefix + "-lake", new Vector3(-38, 2.4f, 45), new Vector3(-48, 1, 12));
        foreach (var shader in new[]{"Campus/Surface", "Campus/Courtyard", "Campus/RoadLandscape", "Campus/DaySky", "Campus/Dormitory"})
        {
            var asset = Shader.Find(shader);
            if (!asset || ShaderUtil.ShaderHasError(asset)) throw new Exception("Shader failed: " + shader);
        }
        File.WriteAllText(Output + "/" + prefix + "-result.txt", "PASS: saved CampusLiving loaded; courtyard, avenue and lake rendered at 1600x900; five environment shaders compiled. Editor renders, no gameplay or performance claim.\n");
        Debug.Log("ENVIRONMENT_REVIEW_COMPLETE " + prefix);
    }

    static void Shot(Camera camera, CampusGame game, string name, Vector3 position, Vector3 target)
    {
        camera.transform.position = game.MapToWorld(position);
        camera.transform.LookAt(game.MapToWorld(target));
        camera.fieldOfView = 58;
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            camera.Render(); // Prime the SRP and LOD state after changing the pipeline.
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels32();
            if (pixels.Count(p => p.r > 15 || p.g > 15 || p.b > 15) < pixels.Length / 10)
                throw new Exception("Empty capture: " + name);
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }
}
