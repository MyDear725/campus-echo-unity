using System;
using System.Collections;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class GuideCharacterVisual : MonoBehaviour
{
    private DynamicCharacterAvatar avatar;
    private PlayerCharacterCatalog catalog;
    private Transform player;
    private PlayerCharacterSelector playerSelector;
    private Animator activeAnimator;
    private Vector3 previousPosition;
    private bool initialized;
    private bool legacyHidden;

    public void Initialize(PlayerCharacterCatalog catalog, Transform player)
    {
        var prefab=catalog?catalog.ResolveVisual(PlayerCharacterChoice.Female):null;
        if(!initialized&&prefab)
        {
            this.catalog=catalog;this.player=player;playerSelector=player?player.GetComponent<PlayerCharacterSelector>():null;
            previousPosition=transform.position;
            foreach(Transform child in transform)if(child.GetComponentInChildren<Animation>(true)||child.GetComponentInChildren<SkinnedMeshRenderer>(true))child.gameObject.SetActive(false);
            var imported=Instantiate(prefab,transform,false);imported.name="Xiaolan VRoid visual";
            activeAnimator=imported.GetComponentInChildren<Animator>();activeAnimator.runtimeAnimatorController=catalog.LocomotionController;activeAnimator.applyRootMotion=false;
            activeAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;initialized=true;return;
        }
        if (initialized || !catalog || !catalog.FemalePreset)
            return;

        this.catalog = catalog;
        this.player = player;
        playerSelector = player ? player.GetComponent<PlayerCharacterSelector>() : null;
        previousPosition = transform.position;
        var visual = new GameObject("Xiaolan UMA Visual");
        visual.transform.SetParent(transform, false);
        avatar = visual.AddComponent<DynamicCharacterAvatar>();
        avatar.KeepAnimatorController = true;
        avatar.applyRootMotion = false;
        if (catalog.LocomotionController)
            avatar.animationController = catalog.LocomotionController;

        avatar.OnCharacterCreated += HandleCharacterBuilt;
        avatar.OnCharacterUpdated += HandleCharacterBuilt;
        initialized = true;
        StartCoroutine(BuildAfterUmaReady(catalog.FemalePreset));
    }

    private IEnumerator BuildAfterUmaReady(UMAPreset preset)
    {
        yield return null;
        yield return null;
        try
        {
            avatar.InitializeAvatar();
            // Runtime-added avatars have no serialized empty slot array. UMA's
            // first race change reads it while importing the preset's colours.
            if (avatar.umaData.umaRecipe.slotDataList == null)
                avatar.umaData.umaRecipe.slotDataList = Array.Empty<SlotData>();
            avatar.ApplyPreset(preset, true);
            // UMA builds asynchronously. Bind the generated animator in its
            // completion callback rather than trusting preset/controller timing.
        }
        catch (Exception error)
        {
            Debug.LogWarning("Xiaolan UMA visual unavailable; retaining legacy guide visual. " + error, this);
            if (avatar != null)
                avatar.gameObject.SetActive(false);
            if (legacyHidden)
                RestoreLegacyVisualChildren();
        }
    }

    private void HandleCharacterBuilt(UMAData data)
    {
        activeAnimator = avatar.GetComponentInChildren<Animator>(true);
        if (activeAnimator && catalog.LocomotionController)
        {
            activeAnimator.runtimeAnimatorController = catalog.LocomotionController;
            activeAnimator.applyRootMotion = false;
            activeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.updateWhenOffscreen = true;
        DisableLegacyVisualChildren();
        legacyHidden = true;
        MatchPlayerHeight();
    }

    private void MatchPlayerHeight()
    {
        // Humanoid rig scale stays stable across walk/idle poses. Animated
        // renderer bounds would make the model grow/shrink every step; UMA's
        // optional collider height can remain at its default instead of stature.
        var playerAnimator = playerSelector && playerSelector.PrimaryRenderer
            ? playerSelector.PrimaryRenderer.GetComponentInParent<Animator>() : null;
        if (!activeAnimator || !activeAnimator.isHuman || !playerAnimator || !playerAnimator.isHuman)
            return;
        float current = activeAnimator.humanScale * Mathf.Abs(activeAnimator.transform.lossyScale.y);
        float target = playerAnimator.humanScale * Mathf.Abs(playerAnimator.transform.lossyScale.y);
        if (avatar && current > .1f && target > .1f && Mathf.Abs(current - target) > .001f)
            avatar.transform.localScale *= target / current;
    }

    private void LateUpdate()
    {
        MatchPlayerHeight();
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        delta.y = 0;
        // Regroup teleports must not produce a burst of running in place.
        float speed = Time.deltaTime > 0 && delta.magnitude < 2f ? delta.magnitude / Time.deltaTime : 0;
        if (!activeAnimator || !activeAnimator.runtimeAnimatorController)
            return;
        bool moving = speed > .05f;
        activeAnimator.SetFloat("Speed", moving ? 1f : 0f, .12f, Time.deltaTime);
        activeAnimator.SetFloat("Direction", 0f);
        activeAnimator.speed = moving ? Mathf.Lerp(.62f, 1.12f, Mathf.InverseLerp(2.5f, 9f, speed)) : 1f;
    }

    private void OnDestroy()
    {
        if (!avatar) return;
        avatar.OnCharacterCreated -= HandleCharacterBuilt;
        avatar.OnCharacterUpdated -= HandleCharacterBuilt;
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
