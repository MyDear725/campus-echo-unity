using UnityEngine;

public enum CampusBuildingCategory { Library, Dormitory, Cafeteria, Teaching, Gymnasium }

// One independently editable asset per doorway. Exterior positions use the
// source campus map coordinates; indoor transforms always use Unity world space.
[CreateAssetMenu(menuName="Campus/Building Entrance",fileName="BuildingEntrance")]
public class BuildingEntranceDefinition : ScriptableObject
{
    [Tooltip("Stable building ID shared by all doorways that lead to the same interior.")]
    public string buildingId;
    [Tooltip("Unique doorway ID for buildings with multiple doors. Empty uses buildingId for existing entrances.")]
    public string entranceId;
    public string EntranceId=>string.IsNullOrEmpty(entranceId)?buildingId:entranceId;
    public string displayName;
    public CampusBuildingCategory category;
    public int mapFeatureId;
    [Tooltip("Reachable point immediately outside the entrance, in source-map coordinates.")]
    public Vector3 approachMapPosition;
    public Vector3 returnMapPosition;
    [Tooltip("World-space yaw pointing into the exterior door (campus root is mirrored).")]
    public float enterYaw;
    [Min(.5f)] public float interactionRadius=2.5f;
    [Tooltip("Full path, e.g. Assets/Interiors/Scenes/Library.unity. Include in Build Settings.")]
    public string interiorScenePath;
    public string spawnId="main";
}
