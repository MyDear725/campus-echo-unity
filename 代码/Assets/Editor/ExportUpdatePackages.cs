using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExportUpdatePackages
{
    const string OutDir = "Updates";

    // Explicit shared code dependencies are required: Unity does not discover C#
    // references between editor utilities when exporting a scene's asset graph.
    static readonly string[] SharedAssets = {
        "Assets/Scripts/CampusGame.cs",
        "Assets/Scripts/BuildingInteriorTravel.cs",
        "Assets/Scripts/InteriorSceneRoot.cs",
        "Assets/Scripts/InteriorPresentation.cs",
        "Assets/Scripts/InteriorFloorTextVisibility.cs",
        "Assets/Editor/InteriorGeometry.cs",
        "Assets/Editor/InteriorPresentationSetup.cs",
        "Assets/Editor/InteriorLabelStairFix.cs",
        "Assets/Editor/LibraryWalkTest.cs",
        "Assets/Settings/CampusURP.asset",
        "Assets/Settings/InteriorRenderer_Shared.asset",
        // CampusURP references this renderer even when only the cafeteria is imported.
        "Assets/Interiors/LibraryInterior/LibraryRenderer.asset"
    };

    static string[] WithDependencies(string[] buildingAssets)
    {
        return AssetDatabase.GetDependencies(buildingAssets.Concat(SharedAssets).ToArray(), true)
            .Where(p => p.StartsWith("Assets/")).Distinct().ToArray();
    }

    static readonly string[] CafeteriaAssets = new string[]
    {
        "Assets/Interiors/Scenes/cafeteria.unity",
        "Assets/Interiors/Entrances/cafeteria.asset",
        "Assets/Interiors/Materials/CafeteriaModern",
        "Assets/Interiors/CafeteriaInterior",
        "Assets/Interiors/Materials/Faculty_Dining_Facade.mat",
        "Assets/Interiors/Materials/Kitchen_Backdrop.mat",
        "Assets/Interiors/Textures/Faculty_Dining_Facade.png",
        "Assets/Interiors/Textures/Kitchen_Backdrop.png",
        "Assets/Scripts/CafeteriaIndoorInteractions.cs",
        "Assets/Editor/CafeteriaInteriorBuilder.cs",
        "Assets/Editor/CafeteriaScreenshotCapture.cs",
        "Assets/Resources/CampusFont.ttf"
    };

    static readonly string[] LibraryAssets = new string[]
    {
        "Assets/Interiors/Scenes/library.unity",
        "Assets/Interiors/Entrances/library.asset",
        "Assets/Interiors/Entrances/library-rear.asset",
        "Assets/Interiors/Materials/LibraryModern",
        "Assets/Interiors/LibraryInterior",
        "Assets/Interiors/Textures/Library_Granite_Tile.png",
        "Assets/Interiors/Textures/Library_Oak_Grain.png",
        "Assets/Interiors/Textures/Library_Stone_Dado.png",
        "Assets/Editor/LibraryBuilder.cs",
        "Assets/Editor/LibraryHallWindows.cs",
        "Assets/Editor/LibraryLightingBalance.cs",
        "Assets/Editor/LibraryUpperHallLandscape.cs",
        "Assets/Editor/LibraryPostFX.cs",
        "Assets/Editor/LibraryTextureGen.cs",
        "Assets/Interiors/Textures/Library_FloorPlan_Panel.png",
        "Assets/Interiors/Textures/Library_History_Panel.png",
        "Assets/Interiors/Textures/Library_Poster_Panel.png",
        "Assets/Scripts/LibraryIndoorInteractions.cs",
        "Assets/Editor/LibraryInteriorBuilder.cs",
        "Assets/Editor/LibraryScreenshotCapture.cs",
        "Assets/Editor/LibraryWalkTest.cs",
        "Assets/Resources/CampusFont.ttf"
    };

    [MenuItem("Campus/Updates/Export Cafeteria Interior Package (.unitypackage)")]
    public static void ExportCafeteria()
    {
        EnsureOutDir();
        string pkgPath = Path.Combine(OutDir, "Cafeteria_Interior_Update.unitypackage");
        AssetDatabase.ExportPackage(WithDependencies(CafeteriaAssets), pkgPath, ExportPackageOptions.Recurse);
        Debug.Log($"[ExportUpdatePackages] Successfully exported cafeteria package to {pkgPath}");
    }

    [MenuItem("Campus/Updates/Export Library Interior Package (.unitypackage)")]
    public static void ExportLibrary()
    {
        EnsureOutDir();
        string pkgPath = Path.Combine(OutDir, "Library_Interior_Update.unitypackage");
        AssetDatabase.ExportPackage(WithDependencies(LibraryAssets), pkgPath, ExportPackageOptions.Recurse);
        Debug.Log($"[ExportUpdatePackages] Successfully exported library package to {pkgPath}");
    }

    [MenuItem("Campus/Updates/Export All Interior Packages")]
    public static void ExportAll()
    {
        ExportCafeteria();
        ExportLibrary();
    }

    static void EnsureOutDir()
    {
        if (!Directory.Exists(OutDir))
        {
            Directory.CreateDirectory(OutDir);
        }
    }
}
