using UnityEngine;

public class SharedHealthPresenter : MonoBehaviour
{
    [SerializeField] private SharedHealthPool sharedHealthPool;
    [SerializeField] private EmergencyTeleportMonitor emergencyMonitor;     // optional
    [SerializeField] private HealthBarView sharedHealthBarView;

    [Tooltip("Label or panel to show 'GAME OVER' warning. Optional.")]
    [SerializeField] private GameObject emergencyWarningPanel;

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

    private void HandleCombinedHealthChanged(float combined)
    {
        float normalised = combined / sharedHealthPool.MaxCombinedHealth;
        sharedHealthBarView.SetFill(normalised);
    }

    private void HandleSharedPoolEmpty()
    {
        sharedHealthBarView.SetFill(0f);
        sharedHealthBarView.SetCriticalState(true);
        // Game over logic lives in a separate GameOverManager � not here
    }

    private void HandleEmergencyStateChanged(bool isEmergency)
    {
        if (emergencyWarningPanel != null)
            emergencyWarningPanel.SetActive(isEmergency);
    }

    private void Refresh()
    {
        float normalised = sharedHealthPool.CombinedHealth / sharedHealthPool.MaxCombinedHealth;
        sharedHealthBarView.SetFill(normalised);
    }
}