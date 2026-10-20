using UnityEngine;

// Seated pose for the campus traveller, which ships no Sit clip.
//
// The rig is a plain humanoid (Hips / Spine / Chest / Thigh_L,R / Shin_L,R / Foot_L,R)
// driven by an Animation component. The Animation system rewrites bone rotations every
// frame, so the fold is applied in LateUpdate, after animation has run, and is blended in
// by a weight that ramps to 1. Setting active=false hands the bones straight back to the
// normal clips, so standing up needs no explicit restore.
public class CafeteriaSitPose : MonoBehaviour
{
    public bool active;

    [Header("Bone angles (degrees around local X)")]
    public float thighAngle = -82f;   // thighs forward, horizontal
    public float shinAngle = 86f;     // shins back down, so feet land under the knees
    public float spineAngle = 6f;     // slight lean toward the table

    Transform thighL, thighR, shinL, shinR, spine;
    bool bound;
    float weight;

    void Awake() { Bind(); }

    void Bind()
    {
        if (bound) return;
        bound = true;
        thighL = Deep("Thigh_L");
        thighR = Deep("Thigh_R");
        shinL = Deep("Shin_L");
        shinR = Deep("Shin_R");
        spine = Deep("Spine");
    }

    Transform Deep(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    // True when the rig was found; the caller can fall back to a plain placement.
    public bool Ready { get { Bind(); return thighL && shinL; } }

    void LateUpdate()
    {
        Bind();

        // Ramp in/out so the pose does not pop.
        weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime * 6f);
        if (weight <= 0f) return;

        Fold(thighL, thighAngle);
        Fold(thighR, thighAngle);
        Fold(shinL, shinAngle);
        Fold(shinR, shinAngle);
        if (spine) Fold(spine, spineAngle);
    }

    // Read the animated rotation first, then ease toward the seated target. At weight 1
    // the target is applied exactly.
    void Fold(Transform bone, float degrees)
    {
        if (!bone) return;
        var animated = bone.localRotation;
        var target = Quaternion.Euler(degrees, 0f, 0f);
        bone.localRotation = Quaternion.Slerp(animated, target, weight);
    }
}
