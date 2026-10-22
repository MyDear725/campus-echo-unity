using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Outdoor grading is isolated from the authored additive interiors.
public sealed class CampusEnvironmentPresentation : MonoBehaviour
{
    CampusGame game;
    Volume volume;
    VolumeProfile profile;

    public static Volume CreateProfile(GameObject owner)
    {
        var existing = owner.GetComponentInChildren<CampusEnvironmentPresentation>(true);
        if (existing) return existing.volume;
        var root = new GameObject("Campus · clear warm daylight");
        root.transform.SetParent(owner.transform, false);
        var presentation = root.AddComponent<CampusEnvironmentPresentation>();
        presentation.game = owner.GetComponent<CampusGame>();
        presentation.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        presentation.profile.name = "Campus outdoor atmosphere";
        presentation.volume = root.AddComponent<Volume>();
        presentation.volume.isGlobal = true;
        presentation.volume.priority = 5;
        presentation.volume.sharedProfile = presentation.profile;
        var tone = presentation.profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.ACES);
        var color = presentation.profile.Add<ColorAdjustments>();
        color.contrast.Override(8);
        color.saturation.Override(-2);
        color.postExposure.Override(.15f);
        var bloom = presentation.profile.Add<Bloom>();
        bloom.threshold.Override(1.2f);
        // Do not expand isolated HDR reflections into floating white screen glows.
        // Physical daylight, lamps and colour grading keep their authored settings.
        bloom.intensity.Override(0f);
        bloom.scatter.Override(.55f);
        return presentation.volume;
    }

    void LateUpdate()
    {
        if (game && volume) volume.weight = game.Interiors.Inside ? 0 : 1;
    }

    void OnDestroy()
    {
        if (!profile) return;
        foreach (var component in profile.components)
            if (component) { if (Application.isPlaying) Destroy(component); else DestroyImmediate(component); }
        if (Application.isPlaying) Destroy(profile); else DestroyImmediate(profile);
    }
}
