using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UMA;

public static class WelcomeCharacterAssetAudit
{
    public static void Run()
    {
        string folder="QA/WelcomeIntegration/character-assets";Directory.CreateDirectory(folder);
        var report=new StringBuilder();
        foreach(string guid in AssetDatabase.FindAssets("t:OverlayDataAsset",new[]{"Assets/UMA"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var overlay=AssetDatabase.LoadAssetAtPath<OverlayDataAsset>(path);
            if(!overlay)continue;
            var textures=overlay.textureList;int missing=0;
            report.AppendLine("OVERLAY "+path);
            if(textures!=null)for(int i=0;i<textures.Length;i++)
            {
                string expected=overlay.textureNames!=null&&i<overlay.textureNames.Length?overlay.textureNames[i]:"";
                report.AppendLine("  channel "+i+": "+(textures[i]?AssetDatabase.GetAssetPath(textures[i]):"MISSING")+" expected-name="+expected);
                if(!textures[i])missing++;
            }
            if(missing>0)InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[]{overlay},Path.Combine(folder,overlay.name+".yaml"),true);
        }
        File.WriteAllText(Path.Combine(folder,"overlay-textures.txt"),report.ToString());
        Debug.Log("WELCOME_CHARACTER_ASSET_AUDIT_COMPLETE");
        EditorApplication.Exit(0);
    }
}
