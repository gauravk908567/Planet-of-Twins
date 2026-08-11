using UnityEngine;

/// <summary>
/// Attach to a gate GameObject that has an Animator.
/// The Animator must have a bool parameter named "IsOpen".
///
/// Can be made permanent (prototype) or resettable via _isPermanent flag.
/// </summary>
public class GateActivatable : MonoBehaviour, IActivatable
{
    [SerializeField] private Animator gateAnimator;
    [SerializeField] private string openParameter = "IsOpen";

    [Tooltip("If true, Deactivate() is a no-op — gate stays open for the session.")]
    [SerializeField] private bool isPermanent = true;

    public bool IsActivated { get; private set; } = false;

    private void Awake()
    {
        if (gateAnimator == null)
            gateAnimator = GetComponent<Animator>();
    }

    public void Activate()
    {
        if (IsActivated) return;
        IsActivated = true;
        Debug.Log($"[GateActivatable] Activate called — animator={gateAnimator?.name ?? "NULL"}, param='{openParameter}', IsOpen={gateAnimator?.GetBool(openParameter)}");
        gateAnimator?.SetBool(openParameter, true);
        Debug.Log($"[GateActivatable] {name} opened.");
    }

    public void Deactivate()
    {
        if (isPermanent)
        {
            Debug.Log($"[GateActivatable] {name} is permanent — Deactivate ignored.");
            return;
        }
        IsActivated = false;
        gateAnimator?.SetBool(openParameter, false);
    }
}