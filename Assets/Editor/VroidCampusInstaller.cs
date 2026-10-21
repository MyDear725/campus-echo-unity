using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UniGLTF;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRM;
using Object=UnityEngine.Object;

public static class VroidCampusInstaller
{
    const string Folder="Assets/Characters/VRoidCampus";
    public static void Install()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var catalog=AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(PlayerCharacterSelectionInstaller.CatalogPath);
        var male=Import("Male","D:/UnityProjectCaches/VRoidDownloads/male/Sakurada Fumiriya.vrm",catalog.LocomotionController,1.78f);
        var female=Import("Female","D:/UnityProjectCaches/VRoidDownloads/female/AvatarSample_D.vrm",catalog.LocomotionController,1.68f);
        catalog.ConfigureVisuals(male,female);EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Builds/Windows/QA/VRoid");
        File.WriteAllText("Builds/Windows/QA/VRoid/import.txt","Imported official CC0 models using UniVRM 0.131.4. Valid Humanoid Avatars; original textures, bone weights and facial blend shapes preserved. URP materials, normalized metres, existing gender selector/controller retained.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");Debug.Log("VROID_CAMPUS_INSTALLED");
    }
    static GameObject Import(string id,string source,RuntimeAnimatorController controller,float height)
    {
        string folder=Folder+"/"+id;Directory.CreateDirectory(folder+"/Textures");Directory.CreateDirectory(folder+"/Data");AssetDatabase.Refresh();
        string prefabPath=folder+"/Campus"+id+".prefab";
        if(File.Exists(prefabPath))return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        File.Copy(source,folder+"/Original.vrm.bytes",true);
        using(var data=new GlbFileParser(source).Parse())
        using(var context=new VRMImporterContext(new VRMData(data)))
        {
            var loaded=context.Load();loaded.ShowMeshes();loaded.EnableUpdateWhenOffscreen();
            var root=loaded.Root;root.name="Campus "+id+" · VRoid CC0";
            var animator=root.GetComponent<Animator>();
            if(!animator||!animator.avatar||!animator.avatar.isHuman||!animator.avatar.isValid)throw new Exception("Invalid VRoid humanoid "+id);
            var textures=new Dictionary<Texture,Texture>();int textureIndex=0;
            foreach(var texture in loaded.Textures.OfType<Texture2D>().Distinct())
            {
                string path=folder+"/Textures/"+(textureIndex++).ToString("D2")+".png";
                File.WriteAllBytes(path,texture.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=texture.isDataSRGB;importer.maxTextureSize=2048;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
                textures[texture]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            foreach(var material in loaded.Materials)
            {
                var texture=material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):material.mainTexture;
                var color=material.HasProperty("_Color")?material.GetColor("_Color"):Color.white;
                material.shader=Shader.Find("Campus/VRoid URP");material.shaderKeywords=Array.Empty<string>();
                material.SetTexture("_BaseMap",texture&&textures.TryGetValue(texture,out var saved)?saved:texture);
                material.SetColor("_BaseColor",color);material.SetFloat("_Cutoff",.45f);material.EnableKeyword("_ALPHATEST_ON");material.enableInstancing=true;
            }
            var meta=root.GetComponent<VRMMeta>();if(meta&&meta.Meta&&meta.Meta.Thumbnail&&textures.TryGetValue(meta.Meta.Thumbnail,out var thumbnail))meta.Meta.Thumbnail=(Texture2D)thumbnail;
            int assetIndex=0;var owned=loaded.RuntimeResources.ToArray();
            loaded.TransferOwnership((key,asset)=>
            {
                if(asset is Texture)return;
                if(AssetDatabase.Contains(asset))return;
                string path=folder+"/Data/"+(assetIndex++).ToString("D3")+"-"+asset.GetType().Name+".asset";
                AssetDatabase.CreateAsset(asset,path);
            });
            Object.DestroyImmediate(loaded);
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();var bounds=skins[0].bounds;foreach(var skin in skins)bounds.Encapsulate(skin.bounds);
            root.transform.localScale*=height/bounds.size.y;
            // The importer normalizes VRM facing and its humanoid T pose; scale only.
            root.AddComponent<VroidAvatarExpression>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);Object.DestroyImmediate(root);
            foreach(var item in owned)if(item.Item2 is Texture tex&&!AssetDatabase.Contains(tex))Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();Debug.Log("VROID_IMPORTED "+id+" height="+height+" vertices="+skins.Length+" renderers");return prefab;
        }
    }
    public static void InstallAndBuild(){Install();CampusBuilder.Build();}
}
