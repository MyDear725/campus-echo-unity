using System;
using System.Collections;
using UMA.CharacterSystem;
using UnityEngine;

public sealed class GuideCharacterVisual : MonoBehaviour
{
    private DynamicCharacterAvatar avatar;
    private bool initialized;
    private bool legacyHidden;

    public void Initialize(PlayerCharacterCatalog catalog)
    {
        if (initialized || !catalog || !catalog.FemalePreset)
            return;

        var visual = new GameObject("Xiaolan UMA Visual");
        visual.transform.SetParent(transform, false);
        avatar = visual.AddComponent<DynamicCharacterAvatar>();
        avatar.KeepAnimatorController = true;
        avatar.applyRootMotion = false;
        if (catalog.LocomotionController)
            avatar.animationController = catalog.LocomotionController;

        initialized = true;
        StartCoroutine(BuildAfterUmaReady(catalog.FemalePreset));
    }

    private IEnumerator BuildAfterUmaReady(UMAPreset preset)
    {
        yield return null;
        yield return null;
        try
        {
            avatar.ApplyPreset(preset, true);
            DisableLegacyVisualChildren();
            legacyHidden = true;
        }
        catch (Exception error)
        {
            Debug.LogWarning("Xiaolan UMA visual unavailable; retaining legacy guide visual. " + error.Message, this);
            if (avatar != null)
                avatar.gameObject.SetActive(false);
            if (legacyHidden)
                RestoreLegacyVisualChildren();
        }
    }

    private void DisableLegacyVisualChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (avatar != null && child == avatar.transform)
                continue;
            if (child.GetComponentInChildren<DynamicCharacterAvatar>(true))
                continue;
            if (child.GetComponentInChildren<Animation>(true) || child.GetComponentInChildren<SkinnedMeshRenderer>(true))
                child.gameObject.SetActive(false);
        }
    }

    private void RestoreLegacyVisualChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (avatar != null && child == avatar.transform)
                continue;
            if (child.GetComponentInChildren<Animation>(true) != null || child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                child.gameObject.SetActive(true);
        }
    }
}