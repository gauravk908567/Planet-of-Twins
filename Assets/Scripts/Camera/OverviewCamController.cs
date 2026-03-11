using System;
using UnityEngine;

public class OverviewCamController : MonoBehaviour, IOverviewBroadcaster
{
    [SerializeField] private KeyCode overviewKey = KeyCode.B;
    [SerializeField] private float maxHoldDuration = 5f;
    [SerializeField] private float cooldownDuration = 4f;

    public event Action<bool> OnOverviewToggled;
    public bool IsOverviewActive { get; private set; }

    private float activeTimer = 0f;
    private float cooldownTimer = 0f;

    private void Update()
    {
        // 1. If we are on cooldown, tick it down and ignore all input
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        // 2. Check for Activation
        if (Input.GetKeyDown(overviewKey) && !IsOverviewActive)
        {
            StartOverview();
        }

        // 3. Handle Active State (Holding the key)
        if (IsOverviewActive)
        {
            activeTimer += Time.deltaTime;

            // Deactivate if they let go of 'B' OR if they hit the 5-second limit
            if (Input.GetKeyUp(overviewKey) || activeTimer >= maxHoldDuration)
            {
                EndOverview();
            }
        }
    }

    private void StartOverview()
    {
        IsOverviewActive = true;
        activeTimer = 0f;
        OnOverviewToggled?.Invoke(true); // Broadcast: "Overview Started!"
    }

    private void EndOverview()
    {
        IsOverviewActive = false;
        cooldownTimer = cooldownDuration; // Start the 3-second penalty
        OnOverviewToggled?.Invoke(false); // Broadcast: "Overview Ended!"
    }
}
