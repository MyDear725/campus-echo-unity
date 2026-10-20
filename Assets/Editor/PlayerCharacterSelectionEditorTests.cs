using System;
using System.IO;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerCharacterSelectionEditorTests
{
    private const string ResultPath = "QA/Characters/editor-result.txt";

    public static void Run()
    {
        Directory.CreateDirectory("QA/Characters");
        try
        {
            TestChoicePersistenceMapping();
            TestCatalogResolvesFixedPresets();
            TestInvalidChoiceFallsBackWithoutMovingRoot();
            TestCampusGameSelectionUiContract();
            TestInstallerExists();
            TestInstalledScenes();
            File.WriteAllText(ResultPath,
                "PASS: preference mapping; preset catalog; invalid-choice fallback; root transform preserved; selection UI contract; installer exists.\n");
            Debug.Log("PLAYER_CHARACTER_EDITOR_TESTS_PASS");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, "FAIL: " + error + "\n");
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    private static void TestChoicePersistenceMapping()
    {
        bool hadPreference=PlayerPrefs.HasKey(PlayerCharacterSelector.PreferenceKey);
        int savedPreference=PlayerPrefs.GetInt(PlayerCharacterSelector.PreferenceKey);
        PlayerPrefs.DeleteKey(PlayerCharacterSelector.PreferenceKey);
        var root = new GameObject("Selector Preference Test");
        try
        {
            var selector = root.AddComponent<PlayerCharacterSelector>();
            Require(selector.ReadSavedChoice() == PlayerCharacterChoice.None,
                "Missing preference must resolve to None");
            PlayerPrefs.SetInt(PlayerCharacterSelector.PreferenceKey, 1);
            Require(selector.ReadSavedChoice() == PlayerCharacterChoice.Male,
                "Stored value 1 must resolve to Male");
            PlayerPrefs.SetInt(PlayerCharacterSelector.PreferenceKey, 2);
            Require(selector.ReadSavedChoice() == PlayerCharacterChoice.Female,
                "Stored value 2 must resolve to Female");
            PlayerPrefs.SetInt(PlayerCharacterSelector.PreferenceKey, 99);
            Require(selector.ReadSavedChoice() == PlayerCharacterChoice.None,
                "Unknown stored values must resolve to None");
        }
        finally
        {
            if(hadPreference)PlayerPrefs.SetInt(PlayerCharacterSelector.PreferenceKey,savedPreference);
            else PlayerPrefs.DeleteKey(PlayerCharacterSelector.PreferenceKey);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestCatalogResolvesFixedPresets()
    {
        var male = ScriptableObject.CreateInstance<UMAPreset>();
        var female = ScriptableObject.CreateInstance<UMAPreset>();
        var catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
        try
        {
            catalog.Configure(male, female, null);
            Require(catalog.Resolve(PlayerCharacterChoice.Male) == male,
                "Male must resolve to the male preset");
            Require(catalog.Resolve(PlayerCharacterChoice.Female) == female,
                "Female must resolve to the female preset");
            Require(catalog.Resolve(PlayerCharacterChoice.None) == null,
                "None must not resolve to a preset");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(male);
            UnityEngine.Object.DestroyImmediate(female);
        }
    }

    private static void TestInvalidChoiceFallsBackWithoutMovingRoot()
    {
        var root = new GameObject("Selector Root Test");
        var fallback = new GameObject("Fallback Visual");
        fallback.transform.SetParent(root.transform, false);
        fallback.SetActive(false);
        var catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
        try
        {
            root.transform.SetPositionAndRotation(new Vector3(11f, 2f, -7f), Quaternion.Euler(0f, 37f, 0f));
            Vector3 position = root.transform.position;
            Quaternion rotation = root.transform.rotation;
            var selector = root.AddComponent<PlayerCharacterSelector>();
            selector.Configure(catalog, null, fallback);
            Require(!selector.ApplyChoice((PlayerCharacterChoice)99, false),
                "Invalid choice must fail");
            Require(!fallback.activeSelf, "Invalid choice must keep the retired legacy visual hidden");
            Require(root.transform.position == position && root.transform.rotation == rotation,
                "Visual selection must not move or rotate the player root");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    private static void TestCampusGameSelectionUiContract()
    {
        string source = File.ReadAllText("Assets/Scripts/CampusGame.cs");
        Require(source.Contains("PlayerCharacterSelector"),
            "CampusGame must delegate selected-character visuals to PlayerCharacterSelector");
        Require(source.Contains("更换角色"),
            "The pause UI must expose a change-character action");
        Require(source.Contains("PersistCurrentChoice"),
            "The first-start choice must be persisted only when Start is confirmed");
    }

    private static void TestInstallerExists()
    {
        Type installer = Type.GetType("PlayerCharacterSelectionInstaller, Assembly-CSharp-Editor");
        Require(installer != null,
            "PlayerCharacterSelectionInstaller must wire UMA into every playable scene");
    }

    private static void TestInstalledScenes()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(
            PlayerCharacterSelectionInstaller.CatalogPath);
        Require(catalog && catalog.MalePreset && catalog.FemalePreset && catalog.LocomotionController,
            "Installed catalog must reference Shane, Sheila, and Locomotion");

        foreach (string path in new[]
                 {
                     "Assets/Scenes/CampusLiving.unity",
                     "Assets/Scenes/Campus.unity",
                     "Assets/Scenes/CampusPixel.unity"
                 })
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CampusGame game = UnityEngine.Object.FindFirstObjectByType<CampusGame>();
            Require(game && game.player, "CampusGame/player missing from " + path);
            Require(game.player.GetComponents<PlayerCharacterSelector>().Length == 1,
                "Expected exactly one selector on the player in " + path);
            Require(game.player.GetComponentsInChildren<DynamicCharacterAvatar>(true).Length == 1,
                "Expected exactly one UMA avatar under the player in " + path);
            Require(UnityEngine.Object.FindObjectsByType<UMAGeneratorBase>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Expected exactly one UMA generator in " + path);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
