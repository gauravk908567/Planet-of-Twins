using System;
using UnityEngine;

public class AbilityController : MonoBehaviour
{
    private IAbility primaryAbility;   // E key
    private IAbility teleportAbility;  // T key

    private AbilityRadiusPreview radiusPreview;
    private TeleportMarkerPreview teleportMarkPreview;

    private void Awake()
    {
        radiusPreview = GetComponent<AbilityRadiusPreview>();
        teleportMarkPreview = GetComponent<TeleportMarkerPreview>();
    }
    public void SetPrimaryAbility(IAbility ability)
    {
        primaryAbility = ability;
        primaryAbility.Initialize(gameObject);
    }

    public void SetTeleportAbility(IAbility ability)
    {
        teleportAbility = ability;
        teleportAbility.Initialize(gameObject);
    }

    public void ActivatePrimary()
    {
        primaryAbility?.TryActivate();
    }

    public void ActivateTeleport()
    {
        teleportAbility?.TryActivate();
    }


    private void Update()
    {
        primaryAbility?.Tick();
        teleportAbility?.Tick();
    }

    public void ShowPrimaryPreview(float radius)
    {
        radiusPreview?.Show(radius);
    }

    public void ShowTeleportPreview()
    {
        teleportMarkPreview?.Show(GetTeleportPos());
    }

    public void HidePrimaryPreview()
    {
        radiusPreview?.Hide();
    }

    public void HideTeleportPreview()
    {
        teleportMarkPreview?.Hide();
    }

    public float GetPrimaryRange()
    {
        if (primaryAbility is AbilityBase baseAbility)
            return baseAbility.GetRange();

        return 0f;
    }

    public Transform GetTeleportPos()
    {
        return (teleportAbility as TeleportAbility).GetTeleportPos();
    }
}