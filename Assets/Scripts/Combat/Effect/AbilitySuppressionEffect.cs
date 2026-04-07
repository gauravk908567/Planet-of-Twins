using System.Collections;
using UnityEngine;

/// <summary>
/// Timed ability suppression — wraps AbilityController.LockAbilities() /
/// LockPrimaryOnly() with automatic expiry.
///
/// CRITICAL: The coroutine runner must be a persistent MonoBehaviour.
/// Do NOT pass the bomb as runner — it is destroyed before the timer expires.
/// Pass the AbilityController itself (it lives on the player).
/// </summary>
public static class AbilitySuppressionEffect
{
    public static void Apply(
        AbilityController controller,
        float duration,
        bool suppressPrimaryOnly,
        MonoBehaviour runner) // runner param kept for API compat but ignored
    {
        if (controller == null || duration <= 0f) return;
        // Always run on the controller itself — it is persistent, bombs are not
        controller.StartCoroutine(SuppressionRoutine(controller, duration, suppressPrimaryOnly));
    }

    private static IEnumerator SuppressionRoutine(
        AbilityController controller,
        float duration,
        bool suppressPrimaryOnly)
    {
        if (suppressPrimaryOnly)
        {
            controller.LockPrimaryOnly();
            yield return new WaitForSeconds(duration);
            controller.UnlockPrimaryOnly();
        }
        else
        {
            controller.LockAbilities();
            yield return new WaitForSeconds(duration);
            controller.UnlockAbilities();
        }
    }
}