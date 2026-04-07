using System.Collections;
using UnityEngine;

/// <summary>
/// SiphonGhost — pursues the soul during rescue events.
///
/// FLOW:
///   Spawn → 2.2s kill window (soul can E-attack ghost to kill it)
///   → kill window closes → ghost becomes immune to damage
///   → reaches soul → Bind (soul frozen, TTK paused, mash F to break free)
///   → mash success: soul freed, ghost backs away for stun duration, then retries
///   → bind timer expire: soul freed, ghost backs away for retry delay, then retries
///   → loop until ghost killed OR rescue resolves
///
/// SETUP:
///   Prefab: root GO + SiphonGhost + EnemyHealthComponent (HP=8). No NavMeshAgent.
///   Wire _ghostRenderer for bind colour feedback.
/// </summary>
[RequireComponent(typeof(EnemyHealthComponent))]
public class SiphonGhost : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Ghost pursuit speed in units/sec.")]
    [SerializeField] private float _pursuitSpeed = 8f;

    [Header("Bind Visual Feedback")]
    [SerializeField] private Renderer _ghostRenderer;
    [SerializeField] private Color _pursuitColour = new Color(0.5f, 0.5f, 1f, 1f);
    [SerializeField] private Color _bindColour = new Color(1f, 0.35f, 0f, 1f);

    // ── Runtime ───────────────────────────────────────────────
    private SoulPlayer _soul;
    private RescueEventController _rescueController;
    private SiphonEnemyData _data;
    private EnemyHealthComponent _health;

    private bool _binding;
    private bool _retreating;
    private bool _resolved;

    // ── Init ──────────────────────────────────────────────────
    public void Initialise(SoulPlayer soul, RescueEventController rescueController,
                            SiphonEnemyData data)
    {
        _soul = soul;
        _rescueController = rescueController;
        _data = data;
        _health = GetComponent<EnemyHealthComponent>();

        if (_health != null)
        {
            _health.SetMaxHealth(data?.ghostMaxHp ?? 8f);
            _health.OnDeath += OnGhostDied;
        }

        SetColour(_pursuitColour);

        if (_rescueController != null)
            _rescueController.OnRescueResolved += OnRescueResolved;

        StartCoroutine(PursuitLoop());
    }

    private void OnDestroy()
    {
        if (_rescueController != null)
            _rescueController.OnRescueResolved -= OnRescueResolved;
    }

    // ── Pursuit ───────────────────────────────────────────────
    private IEnumerator PursuitLoop()
    {
        // Kill window — soul can E-attack ghost during this time to kill it
        float killWindow = _data?.ghostKillWindowDuration ?? 2.2f;
        float killElapsed = 0f;
        bool killWindowOpen = true;

        while (!_resolved)
        {
            if (_soul == null || !_soul.gameObject.activeSelf)
            {
                yield return null;
                continue;
            }

            if (_retreating)
            {
                // Back away from soul — gives real breathing room before next bind attempt
                Vector3 fleeDir = (transform.position - _soul.transform.position).normalized;
                fleeDir.y = 0f;
                transform.position += fleeDir * _pursuitSpeed * Time.deltaTime;
            }
            else if (!_binding)
            {
                // Pursue soul directly
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    _soul.transform.position,
                    _pursuitSpeed * Time.deltaTime);

                // Track kill window
                if (killWindowOpen)
                {
                    killElapsed += Time.deltaTime;
                    if (killElapsed >= killWindow)
                    {
                        killWindowOpen = false;
                        if (_health != null) _health.enabled = false;
                        Debug.Log("[SiphonGhost] Kill window closed — ghost immune");
                    }
                }

                float dist = Vector3.Distance(transform.position, _soul.transform.position);
                if (dist <= 0.8f)
                    StartCoroutine(BindRoutine());
            }

            yield return null;
        }
    }

    // ── Chain Bind ────────────────────────────────────────────
    private IEnumerator BindRoutine()
    {
        _binding = true;
        SetColour(_bindColour);
        Debug.Log("[SiphonGhost] Bind started — soul mash E to break free");

        var activeTarget = _rescueController?.ActiveTarget;
        activeTarget?.PauseTTK();

        var soulMovement = _soul?.GetComponent<IMovementFreezable>();
        soulMovement?.SetFrozen(true);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(true);

        var input = _rescueController?.InputProvider;

        int mashCount = 0;
        int mashNeeded = _data?.ghostMashThreshold ?? 8;
        float bindDuration = _data?.ghostBindDuration ?? 2f;
        float elapsed = 0f;

        while (elapsed < bindDuration && !_resolved)
        {
            bool mashed = input != null
                ? input.GetStruggleMash()        // E key — consistent with all self-rescue
                : Input.GetKeyDown(KeyCode.E);

            if (mashed)
            {
                mashCount++;
                Debug.Log($"[SiphonGhost] Mash {mashCount}/{mashNeeded}");
                if (mashCount >= mashNeeded)
                {
                    // Mash success — free soul, ghost backs away then retries
                    activeTarget?.ResumeTTK();
                    soulMovement?.SetFrozen(false);
                    _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(false);
                    _binding = false;
                    _retreating = true;
                    SetColour(_pursuitColour);

                    float stunDuration = _data?.ghostStunOnBreakDuration ?? 1.25f;
                    yield return new WaitForSeconds(stunDuration);
                    _retreating = false;
                    yield break; // PursuitLoop resumes pursuit
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Bind timer expired — free soul, back away, retry
        activeTarget?.ResumeTTK();
        soulMovement?.SetFrozen(false);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(false);
        _binding = false;
        _retreating = true;
        SetColour(_pursuitColour);
        Debug.Log("[SiphonGhost] Bind expired — retreating before retry");

        float retryDelay = _data?.ghostRetryDelay ?? 0.75f;
        yield return new WaitForSeconds(retryDelay);
        _retreating = false;
    }

    // ── Death / Resolution ────────────────────────────────────
    private void OnGhostDied()
    {
        if (_resolved) return;
        DieGhost();
    }

    private void OnRescueResolved()
    {
        if (_resolved) return;
        DieGhost(); // full cleanup — unfreeze, resume TTK, unregister, destroy
    }

    private void DieGhost()
    {
        // Unconditional cleanup — ghost may die at any point in bind/retreat sequence
        _soul?.GetComponent<IMovementFreezable>()?.SetFrozen(false);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(false);
        _rescueController?.ActiveTarget?.ResumeTTK();
        _binding = false;
        _retreating = false;
        _resolved = true;
        _rescueController?.UnregisterGhost();
        Destroy(gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────
    private void SetColour(Color c)
    {
        if (_ghostRenderer != null)
            _ghostRenderer.material.color = c;
    }

    public void TakeDamage(float amount)
    {
        _health?.TakeDamage(new DamageData(amount, DamageType.Ability));
    }

    /// <summary>Called by SiphonEnemy when it dies — ghost has no purpose without owner.</summary>
    public void KillOnSiphonDeath()
    {
        if (_resolved) return;
        DieGhost();
    }

    public EnemyHealthComponent Health => _health;
}