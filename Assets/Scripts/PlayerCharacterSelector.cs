using System;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

public sealed class PlayerCharacterSelector : MonoBehaviour
{
    public const string PreferenceKey = "CampusUnityCharacterChoiceV1";

    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int DirectionParameter = Animator.StringToHash("Direction");

    [SerializeField] private PlayerCharacterCatalog catalog;
    [SerializeField] private DynamicCharacterAvatar avatar;
    [SerializeField] private GameObject fallbackVisual;

    private Animator activeAnimator;
    private bool subscribed;

    public PlayerCharacterChoice CurrentChoice { get; private set; }
    public SkinnedMeshRenderer PrimaryRenderer { get; private set; }
    public event Action VisualChanged;

    public void Configure(
        PlayerCharacterCatalog value,
        DynamicCharacterAvatar dynamicAvatar,
        GameObject fallback)
    {
        UnsubscribeAvatar();
        catalog = value;
        avatar = dynamicAvatar;
        fallbackVisual = fallback;
        SubscribeAvatar();
        RefreshGeneratedVisual();
    }

    public PlayerCharacterChoice ReadSavedChoice()
    {
        int value = PlayerPrefs.GetInt(PreferenceKey, 0);
        return value switch
        {
            1 => PlayerCharacterChoice.Male,
            2 => PlayerCharacterChoice.Female,
            _ => PlayerCharacterChoice.None
        };
    }

    public bool ApplyChoice(PlayerCharacterChoice choice, bool persist)
    {
        UMAPreset preset = catalog ? catalog.Resolve(choice) : null;
        if (!preset || !avatar)
            return UseFallback("UMA preset or avatar is missing for " + choice, choice != PlayerCharacterChoice.None);

        try
        {
            avatar.gameObject.SetActive(true);
            // Do not show the legacy backpack FBX while UMA is building.
            // UseFallback() re-enables it only if the UMA preset genuinely fails.
            if (fallbackVisual)
                fallbackVisual.SetActive(false);
            avatar.KeepAnimatorController = true;
            avatar.applyRootMotion = false;
            if (catalog.LocomotionController)
                avatar.animationController = catalog.LocomotionController;

            // In edit-mode tests/configuration we only load the recipe. Runtime
            // play performs the full asynchronous mesh build.
            avatar.ApplyPreset(preset, Application.isPlaying);
            CurrentChoice = choice;

            if (persist)
            {
                PlayerPrefs.SetInt(PreferenceKey, (int)choice);
                PlayerPrefs.Save();
            }

            if (!Application.isPlaying)
                RefreshGeneratedVisual();
            return true;
        }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            return UseFallback("UMA failed to apply " + choice, true);
        }
    }

    public bool PersistCurrentChoice()
    {
        if (CurrentChoice != PlayerCharacterChoice.Male &&
            CurrentChoice != PlayerCharacterChoice.Female)
            return false;

        PlayerPrefs.SetInt(PreferenceKey, (int)CurrentChoice);
        PlayerPrefs.Save();
        return true;
    }

    public void SetMotion(float planarSpeed, float signedForwardSpeed, bool grounded)
    {
        if (!activeAnimator)
            RefreshGeneratedVisual();
        if (!activeAnimator || !activeAnimator.runtimeAnimatorController)
            return;

        bool moving = planarSpeed > 0.05f;
        // UMA's Direction blend is lateral: -1=RunLeft, 0=Run, +1=RunRight.
        // The gameplay root already turns into the travel vector, so feeding
        // +1 for normal forward motion made the avatar visibly run sideways.
        float direction = 0f;
        _ = signedForwardSpeed;
        float delta = Application.isPlaying ? Time.deltaTime : 0f;
        activeAnimator.SetFloat(SpeedParameter, moving ? 1f : 0f, 0.12f, delta);
        activeAnimator.SetFloat(DirectionParameter, direction, 0.12f, delta);

        // The selected official UMA controller provides idle plus directional
        // locomotion. Playback rate differentiates walking and running.
        activeAnimator.speed = moving
            ? Mathf.Lerp(0.62f, 1.12f, Mathf.InverseLerp(2.5f, 9f, planarSpeed))
            : 1f;

        _ = grounded;
    }

    private void Awake()
    {
        RetireLegacyVisual();
        SubscribeAvatar();
        PlayerCharacterChoice saved = ReadSavedChoice();
        if (saved == PlayerCharacterChoice.Male || saved == PlayerCharacterChoice.Female)
            ApplyChoice(saved, false);
        else
            ActivateFallback();
    }

    private void RetireLegacyVisual()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (avatar && child == avatar.transform)
                continue;
            if (child.GetComponentInChildren<DynamicCharacterAvatar>(true))
                continue;
            if (child.GetComponentInChildren<Animation>(true) ||
                child.GetComponentInChildren<SkinnedMeshRenderer>(true))
                child.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        SubscribeAvatar();
    }

    private void OnDisable()
    {
        UnsubscribeAvatar();
    }

    private void SubscribeAvatar()
    {
        if (subscribed || !avatar)
            return;
        avatar.OnCharacterCreated += HandleCharacterBuilt;
        avatar.OnCharacterUpdated += HandleCharacterBuilt;
        subscribed = true;
    }

    private void UnsubscribeAvatar()
    {
        if (!subscribed || !avatar)
            return;
        avatar.OnCharacterCreated -= HandleCharacterBuilt;
        avatar.OnCharacterUpdated -= HandleCharacterBuilt;
        subscribed = false;
    }

    private void HandleCharacterBuilt(UMAData data)
    {
        RefreshGeneratedVisual();
        if (PrimaryRenderer && fallbackVisual)
            fallbackVisual.SetActive(false);
        VisualChanged?.Invoke();
    }

    private void RefreshGeneratedVisual()
    {
        if (!avatar)
            return;

        activeAnimator = avatar.GetComponentInChildren<Animator>(true);
        PrimaryRenderer = avatar.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (activeAnimator && catalog && catalog.LocomotionController)
        {
            activeAnimator.runtimeAnimatorController = catalog.LocomotionController;
            activeAnimator.applyRootMotion = false;
        }
    }

    private bool UseFallback(string message, bool logError)
    {
        CurrentChoice = PlayerCharacterChoice.None;
        if (avatar && avatar.gameObject != gameObject)
            avatar.gameObject.SetActive(false);
        ActivateFallback();
        if (logError)
            Debug.LogError(message, this);
        VisualChanged?.Invoke();
        return false;
    }

    private void ActivateFallback()
    {
        // The legacy CampusTraveler is retired permanently. If UMA is not
        // ready, leave the gameplay root invisible instead of showing the old
        // backpack character during editor pause or asynchronous generation.
        if (fallbackVisual)
            fallbackVisual.SetActive(false);
        PrimaryRenderer = null;
        activeAnimator = null;
    }
}






