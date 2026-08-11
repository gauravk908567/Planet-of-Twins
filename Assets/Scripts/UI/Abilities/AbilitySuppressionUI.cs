using UnityEngine;

/// <summary>
/// Wire both Lyra and Kai controllers on every slot.
/// _isQSlot: true = this is a Q ability slot (Possess/Stun)
///           false = this is a non-Q slot (Gate/SC/Coalesce/Empower)
///
/// LockPrimaryOnly bomb → only Q slots show suppressed
/// LockAbilities bomb   → ALL slots show suppressed
///
/// IMPORTANT: Subscribes in Awake/OnDestroy — NOT OnEnable/OnDisable.
/// OnEnable/OnDisable causes double-subscription if parent panel toggles,
/// which breaks the counter and leaves the overlay permanently stuck.
/// </summary>
public class AbilitySuppressionUI : MonoBehaviour
{
    [SerializeField] private AbilityController _lyraController;
    [SerializeField] private AbilityController _kaiController;
    [SerializeField] private GameObject _suppressedOverlay;
    [Tooltip("Is this a Q ability slot? True = Possess/Stun. False = Gate/SC/Coalesce/Empower.")]
    [SerializeField] private bool _isQSlot = true;

    private int _suppressionCount;

    private void Awake()
    {
        Subscribe(_lyraController);
        Subscribe(_kaiController);
        SyncState();
    }

    private void OnDestroy()
    {
        Unsubscribe(_lyraController);
        Unsubscribe(_kaiController);
    }

    private void Subscribe(AbilityController ctrl)
    {
        if (ctrl == null) return;
        ctrl.OnSuppressed += OnSuppressed;
        ctrl.OnSuppressionLifted += OnLifted;
        if (_isQSlot)
        {
            ctrl.OnPrimarySuppressed += OnSuppressed;
            ctrl.OnPrimarySuppressionLifted += OnLifted;
        }
    }

    private void Unsubscribe(AbilityController ctrl)
    {
        if (ctrl == null) return;
        ctrl.OnSuppressed -= OnSuppressed;
        ctrl.OnSuppressionLifted -= OnLifted;
        if (_isQSlot)
        {
            ctrl.OnPrimarySuppressed -= OnSuppressed;
            ctrl.OnPrimarySuppressionLifted -= OnLifted;
        }
    }

    /// <summary>
    /// Sync overlay to current suppression state on subscribe.
    /// Handles case where bomb already active when this component awakens.
    /// </summary>
    private void SyncState()
    {
        _suppressionCount = 0;

        if (_lyraController != null)
        {
            if (_lyraController.AbilitiesLocked) _suppressionCount++;
            if (_isQSlot && _lyraController.PrimaryLocked) _suppressionCount++;
        }

        if (_kaiController != null)
        {
            if (_kaiController.AbilitiesLocked) _suppressionCount++;
            if (_isQSlot && _kaiController.PrimaryLocked) _suppressionCount++;
        }

        _suppressedOverlay?.SetActive(_suppressionCount > 0);
    }

    private void OnSuppressed()
    {
        _suppressionCount++;
        _suppressedOverlay?.SetActive(true);
    }

    private void OnLifted()
    {
        _suppressionCount = Mathf.Max(0, _suppressionCount - 1);
        if (_suppressionCount == 0)
            _suppressedOverlay?.SetActive(false);
    }
}