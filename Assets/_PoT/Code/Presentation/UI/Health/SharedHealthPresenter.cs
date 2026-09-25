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
        // BOND REFRAME (game.md §4.1, 2026-09-09): FILL reads the PURE combat pool via OnSurvivalChanged →
        // CombinedCombat01 (distance-INDEPENDENT), so the emblem never drains on separation — distance
        // drives ONLY the grey desaturation (BondWeaknessPresenter → PoT/BondBar _Drain). Game-over stays
        // on OnSharedPoolEmpty (masked pool incl. the over-max drain, so straying too far still kills).
        sharedHealthPool.OnSurvivalChanged -= HandleSurvivalChanged;
        sharedHealthPool.OnSurvivalChanged += HandleSurvivalChanged;
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
        sharedHealthPool.OnSurvivalChanged += HandleSurvivalChanged;
        sharedHealthPool.OnSharedPoolEmpty += HandleSharedPoolEmpty;

        if (emergencyMonitor != null)
            emergencyMonitor.OnEmergencyStateChanged += HandleEmergencyStateChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (sharedHealthPool == null) return;
        sharedHealthPool.OnSurvivalChanged -= HandleSurvivalChanged;
        sharedHealthPool.OnSharedPoolEmpty -= HandleSharedPoolEmpty;

        if (emergencyMonitor != null)
            emergencyMonitor.OnEmergencyStateChanged -= HandleEmergencyStateChanged;
    }

    // BOND REFRAME (game.md §4.1): FILL from the PURE combat pool — distance-independent, so the emblem
    // never drains on separation (distance only greys it via BondWeaknessPresenter). CombinedCombat01 is
    // already normalised 0..1 (mean of the twins' real combat-health fractions), so no divide by max.
    // NOT CombinedSurvival01 — that subtracts the over-max drain and would still empty the emblem when
    // the twins stray far (which also hides the grey, since grey only tints the FILLED region).
    private void HandleSurvivalChanged()
    {
        sharedHealthBarView?.SetFill(sharedHealthPool.CombinedCombat01);
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
        sharedHealthBarView?.SetFill(sharedHealthPool.CombinedCombat01);
    }
}