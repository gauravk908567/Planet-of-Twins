using UnityEngine;
using UnityEngine.Serialization;

public class SharedHealthPresenter : MonoBehaviour
{
    [SerializeField] private SharedHealthPool sharedHealthPool;
    [SerializeField] private EmergencyTeleportMonitor emergencyMonitor;     // optional

    // Typed as MonoBehaviour and cast to IHealthBarView — the project's standard same-scene DI
    // pattern (R1/SOLID). It used to be a concrete HealthBarView, which meant the health display
    // could only ever be a UGUI Slider. Any IHealthBarView now fits, so the art can be swapped
    // (UIBarHealthView drives the authored bar sprites) with zero change to this presenter.
    // EXISTING SCENES KEEP WORKING: the old HealthBarView still satisfies the interface.
    //
    // FormerlySerializedAs preserves the EXISTING scene wiring across this rename. Without it
    // Unity would drop the reference already assigned in Persistent and the shared-health bar
    // would silently stop updating — a renamed serialized field is a scene-data migration, not
    // just a code edit. The old value deserialises fine because HealthBarView is a MonoBehaviour.
    [FormerlySerializedAs("sharedHealthBarView")]
    [Tooltip("Any component implementing IHealthBarView — HealthBarView (slider) or " +
             "UIBarHealthView (authored bar art).")]
    [SerializeField] private MonoBehaviour sharedHealthBarViewSource;

    private IHealthBarView sharedHealthBarView;

    [Tooltip("Label or panel to show 'GAME OVER' warning. Optional.")]
    [SerializeField] private GameObject emergencyWarningPanel;

    private void Awake()
    {
        // Awake wires self (R8). Fail loud rather than silently showing a frozen bar.
        sharedHealthBarView = sharedHealthBarViewSource as IHealthBarView;
        if (sharedHealthBarViewSource != null && sharedHealthBarView == null)
        {
            Debug.LogError($"[SharedHealthPresenter] '{sharedHealthBarViewSource.GetType().Name}' " +
                           "does not implement IHealthBarView.", this);
        }
        else if (sharedHealthBarViewSource == null)
        {
            Debug.LogError("[SharedHealthPresenter] No health bar view assigned — the shared " +
                           "health display will not update.", this);
        }
    }

    private void Start()
    {
        sharedHealthPool ??= SharedHealthPool.Instance;
        if (sharedHealthPool == null)
        {
            Debug.LogError("[SharedHealthPresenter] SharedHealthPool unresolved — is Persistent loaded?", this);
            enabled = false;
            return;
        }

        // Re-subscribe in case OnEnable fired before sharedHealthPool was resolved.
        // ROLLBACK (BUG-091, 2026-08-05): bar FILL reverted to the pre-UI-shader (26a192e) masked
        // CombinedHealth to test the "survival-channel rework introduced the health bug" hypothesis
        // for the playtest. The survival channel (CombinedSurvival01/OnSurvivalChanged) and the
        // BondWeaknessPresenter grey-drain are left intact but unused — restore them for the proper
        // two-channel fix after the playtest.
        sharedHealthPool.OnCombinedHealthChanged -= HandleCombinedHealthChanged;
        sharedHealthPool.OnCombinedHealthChanged += HandleCombinedHealthChanged;
        sharedHealthPool.OnSharedPoolEmpty -= HandleSharedPoolEmpty;
        sharedHealthPool.OnSharedPoolEmpty += HandleSharedPoolEmpty;

        if (emergencyMonitor != null)
        {
            emergencyMonitor.OnEmergencyStateChanged -= HandleEmergencyStateChanged;
            emergencyMonitor.OnEmergencyStateChanged += HandleEmergencyStateChanged;
        }

        Refresh();
    }

    private void OnEnable()
    {
        if (sharedHealthPool == null) return;
        sharedHealthPool.OnCombinedHealthChanged += HandleCombinedHealthChanged;
        sharedHealthPool.OnSharedPoolEmpty += HandleSharedPoolEmpty;

        if (emergencyMonitor != null)
            emergencyMonitor.OnEmergencyStateChanged += HandleEmergencyStateChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (sharedHealthPool == null) return;
        sharedHealthPool.OnCombinedHealthChanged -= HandleCombinedHealthChanged;
        sharedHealthPool.OnSharedPoolEmpty -= HandleSharedPoolEmpty;

        if (emergencyMonitor != null)
            emergencyMonitor.OnEmergencyStateChanged -= HandleEmergencyStateChanged;
    }

    // ROLLBACK (BUG-091): FILL from the masked CombinedHealth (26a192e behaviour) — the bar shrinks
    // with distance again. Restore HandleSurvivalChanged + CombinedSurvival01 for the proper fix.
    private void HandleCombinedHealthChanged(float combined)
    {
        sharedHealthBarView?.SetFill(combined / sharedHealthPool.MaxCombinedHealth);
    }

    private void HandleSharedPoolEmpty()
    {
        sharedHealthBarView?.SetFill(0f);
        sharedHealthBarView?.SetCriticalState(true);
        // Game over logic lives in a separate GameOverManager � not here
    }

    private void HandleEmergencyStateChanged(bool isEmergency)
    {
        if (emergencyWarningPanel != null)
            emergencyWarningPanel.SetActive(isEmergency);
    }

    private void Refresh()
    {
        sharedHealthBarView?.SetFill(sharedHealthPool.CombinedHealth / sharedHealthPool.MaxCombinedHealth);
    }
}