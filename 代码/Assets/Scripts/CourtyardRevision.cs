using UnityEngine;

// Records only the renderers suppressed by a garden/cafeteria replacement. An editor
// rebuild restores these before clipping again; imported meshes stay untouched.
public class CourtyardRevision : MonoBehaviour
{
    [HideInInspector] public Renderer[] suppressedSources;
}
