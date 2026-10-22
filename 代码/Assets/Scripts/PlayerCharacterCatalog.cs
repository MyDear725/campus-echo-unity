using UMA.CharacterSystem;
using UnityEngine;

public enum PlayerCharacterChoice
{
    None = 0,
    Male = 1,
    Female = 2
}

[CreateAssetMenu(menuName = "Campus/Player Character Catalog")]
public sealed class PlayerCharacterCatalog : ScriptableObject
{
    [SerializeField] private UMAPreset malePreset;
    [SerializeField] private UMAPreset femalePreset;
    [SerializeField] private RuntimeAnimatorController locomotionController;
    [SerializeField] private GameObject maleVisual;
    [SerializeField] private GameObject femaleVisual;

    public UMAPreset MalePreset => malePreset;
    public UMAPreset FemalePreset => femalePreset;
    public RuntimeAnimatorController LocomotionController => locomotionController;
    public GameObject ResolveVisual(PlayerCharacterChoice choice)=>choice==PlayerCharacterChoice.Female?femaleVisual:choice==PlayerCharacterChoice.Male?maleVisual:null;
    public void ConfigureVisuals(GameObject male,GameObject female){maleVisual=male;femaleVisual=female;}

    public void Configure(
        UMAPreset male,
        UMAPreset female,
        RuntimeAnimatorController controller)
    {
        malePreset = male;
        femalePreset = female;
        locomotionController = controller;
    }

    public UMAPreset Resolve(PlayerCharacterChoice choice)
    {
        return choice switch
        {
            PlayerCharacterChoice.Male => malePreset,
            PlayerCharacterChoice.Female => femalePreset,
            _ => null
        };
    }
}
