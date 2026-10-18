using UnityEngine;

// World-space labels belong to the player's floor, not the follow camera's height.
[DisallowMultipleComponent]
[RequireComponent(typeof(InteriorSceneRoot))]
public sealed class InteriorFloorTextVisibility : MonoBehaviour
{
    public float floorHeight = 4.3f;
    public int floorCount = 3;
    TextMesh[] labels;
    Renderer[] renderers;
    bool[] originalSuppression;
    InteriorSceneRoot interior;
    CampusGame game;
    bool filtering;

    void OnEnable()
    {
        interior = GetComponent<InteriorSceneRoot>();
        interior.onPlayerEntered.AddListener(Entered);
        interior.onPlayerLeaving.AddListener(Restore);
        RefreshLabels();
    }

    void Entered()
    {
        game = FindFirstObjectByType<CampusGame>();
        RefreshLabels();
        if (game && game.player) ApplyForHeight(game.player.position.y);
    }

    void LateUpdate()
    {
        if (!game) game = FindFirstObjectByType<CampusGame>();
        if (game && game.player && game.Interiors != null && game.Interiors.Interior == interior)
            ApplyForHeight(game.player.position.y);
        else if (filtering) Restore();
    }

    public void RefreshLabels()
    {
        Restore();
        labels = GetComponentsInChildren<TextMesh>(true);
        renderers = new Renderer[labels.Length];
        originalSuppression = new bool[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            renderers[i] = labels[i].GetComponent<Renderer>();
            originalSuppression[i] = renderers[i] && renderers[i].forceRenderingOff;
        }
    }

    public int FloorAtHeight(float worldY, float tolerance = 0)
        => Mathf.Clamp(Mathf.FloorToInt((worldY - transform.position.y + tolerance) / Mathf.Max(.1f, floorHeight)), 0, floorCount - 1);

    public void ApplyForHeight(float playerFeetY)
    {
        if (labels == null) RefreshLabels();
        int floor = FloorAtHeight(playerFeetY, .08f);
        for (int i = 0; i < labels.Length; i++)
        {
            if (!labels[i] || !renderers[i]) continue;
            // Read the current transform so labels on a moving elevator follow the cabin.
            bool hide = originalSuppression[i] || FloorAtHeight(labels[i].transform.position.y) != floor;
            if (renderers[i].forceRenderingOff != hide) renderers[i].forceRenderingOff = hide;
        }
        filtering = true;
    }

    public void Restore()
    {
        if (!filtering || renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i]) renderers[i].forceRenderingOff = originalSuppression[i];
        filtering = false;
    }

    void OnDisable()
    {
        Restore();
        if (!interior) return;
        interior.onPlayerEntered.RemoveListener(Entered);
        interior.onPlayerLeaving.RemoveListener(Restore);
    }
}
