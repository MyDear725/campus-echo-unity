using System;
using System.Linq;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerCharacterSelectionInstaller
{
    public const string CatalogPath = "Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset";
    public const string AvatarObjectName = "Selectable UMA Avatar";
    public const string ContextObjectName = "UMA_GLIB · Character Generation";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/CampusLiving.unity",
        "Assets/Scenes/Campus.unity",
        "Assets/Scenes/CampusPixel.unity"
    };

    [MenuItem("Campus/Characters/Install Male Female Selection")]
    public static void InstallAllScenes()
    {
        RebuildProjectIndexer();
        EnsureCatalog();
        foreach (string path in ScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CampusGame game = UnityEngine.Object.FindFirstObjectByType<CampusGame>();
            if (!game) throw new InvalidOperationException("CampusGame missing from " + path);
            ConfigureScene(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PLAYER_CHARACTER_SELECTION_INSTALLED");
    }

    [MenuItem("Campus/Characters/Rebuild Curated UMA Index")]
    public static void RebuildProjectIndexer()
    {
        UMAPathUtility.EnsureAssetFolder(UMAPathUtility.ProjectResourcesRoot);
        UMAAssetIndexer indexer = AssetDatabase.LoadAssetAtPath<UMAAssetIndexer>(
            UMAPathUtility.ProjectIndexerPath);
        if (!indexer)
        {
            indexer = ScriptableObject.CreateInstance<UMAAssetIndexer>();
            indexer.name = "AssetIndexerProject";
            AssetDatabase.CreateAsset(indexer, UMAPathUtility.ProjectIndexerPath);
        }

        UMAAssetIndexer.Unload();
        indexer = UMAAssetIndexer.Instance;
        if (!indexer)
            throw new InvalidOperationException("Unable to initialize the project-owned UMA asset index");
        // The official index can carry folder filters intended for the full UMA
        // package. They omit the curated wardrobe SlotDataAsset folders, which
        // makes the recipes resolve but silently produces a naked body at runtime.
        // This project owns the curated asset set, so index every UMA asset that
        // is actually present in Assets instead of inheriting those filters.
        foreach (var mapping in indexer.typeFolders.ToArray())
            foreach (var folder in mapping.Folders ?? Array.Empty<string>())
                indexer.RemoveSearchFolder(mapping.typeName, folder);
        indexer.RebuildLibrary();
        EditorUtility.SetDirty(indexer);
        AssetDatabase.SaveAssetIfDirty(indexer);
        Debug.Log("CURATED_UMA_INDEX_REBUILT");
    }

    public static void RebuildIndexerBatch()
    {
        try
        {
            RebuildProjectIndexer();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void RunBatch()
    {
        try
        {
            InstallAllScenes();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void ConfigureScene(CampusGame game)
    {
        if (!game || !game.player)
            throw new InvalidOperationException("CampusGame player reference is missing");

        PlayerCharacterCatalog catalog = EnsureCatalog();
        EnsureContext();

        Transform player = game.player;
        Animation legacyAnimation = player.GetComponentInChildren<Animation>(true);
        if (!legacyAnimation)
            throw new InvalidOperationException("Legacy fallback character is missing under " + player.name);
        GameObject fallback = legacyAnimation.gameObject;
        fallback.SetActive(true);

        Transform avatarTransform = player.Find(AvatarObjectName);
        GameObject avatarObject;
        if (avatarTransform)
            avatarObject = avatarTransform.gameObject;
        else
        {
            avatarObject = new GameObject(AvatarObjectName);
            avatarObject.transform.SetParent(player, false);
        }

        DynamicCharacterAvatar avatar = avatarObject.GetComponent<DynamicCharacterAvatar>();
        if (!avatar) avatar = avatarObject.AddComponent<DynamicCharacterAvatar>();
        avatar.KeepAnimatorController = true;
        avatar.applyRootMotion = false;
        avatar.animationController = catalog.LocomotionController;
        avatarObject.SetActive(false);

        PlayerCharacterSelector selector = player.GetComponent<PlayerCharacterSelector>();
        if (!selector) selector = player.gameObject.AddComponent<PlayerCharacterSelector>();
        selector.Configure(catalog, avatar, fallback);

        EditorUtility.SetDirty(avatar);
        EditorUtility.SetDirty(selector);
        EditorUtility.SetDirty(player.gameObject);
    }

    private static PlayerCharacterCatalog EnsureCatalog()
    {
        EnsureFolder("Assets/Characters", "PlayerSelection");
        PlayerCharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(CatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        UMAPreset male = AssetDatabase.LoadAssetAtPath<UMAPreset>("Assets/UMA/UMA3/Presets/Shane.asset");
        UMAPreset female = AssetDatabase.LoadAssetAtPath<UMAPreset>("Assets/UMA/UMA3/Presets/Sheila.asset");
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/UMA/UMA3/Animation/Locomotion.controller");
        if (!male || !female || !controller)
            throw new InvalidOperationException("Shane, Sheila, or UMA Locomotion asset is missing");

        catalog.Configure(male, female, controller);
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void EnsureContext()
    {
        UMAGeneratorBase generator = UnityEngine.Object.FindFirstObjectByType<UMAGeneratorBase>(FindObjectsInactive.Include);
        if (generator) return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UMA/Core/Defaults/UMA_GLIB.prefab");
        if (!prefab) throw new InvalidOperationException("UMA_GLIB prefab is missing");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = ContextObjectName;
        EditorUtility.SetDirty(instance);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}


