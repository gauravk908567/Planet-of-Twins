using UnityEngine;

/// <summary>
/// Couch co-op (M3.2) — central config for JOINT abilities (Accord State, Soul Convergence,
/// Setsuna, Accord Spirits): powers that require BOTH players to co-activate their hold "together".
/// Holds the single designer tunable — the synchronized-start leniency window — so it lives in ONE
/// place rather than duplicated on four ability systems. The per-ability sync STATE lives in each
/// consumer's own <see cref="JointHoldSync"/> (generic, one per channel); this gate only supplies
/// <see cref="LeniencyWindow"/>.
///
/// Persistent R3 singleton (dup-destroy Awake guard, null Instance on OnDestroy, no DontDestroyOnLoad).
/// Consumers resolve it R4 in Start (<c>_gate ??= JointAbilityGate.Instance</c>). If absent, a joint
/// ability degrades to solo (single press) rather than bricking — this is an ADDITIVE co-op layer,
/// so a missing gate must never disable a core ability (LogWarning, fall back).
/// </summary>
[DisallowMultipleComponent]
public class JointAbilityGate : MonoBehaviour
{
    public static JointAbilityGate Instance { get; private set; }

    [Tooltip("Synchronized-start leniency (seconds, UNSCALED): the max gap between the two players' " +
             "presses that still counts as 'together'. Higher = more forgiving; 0 = frame-perfect. " +
             "This is the single knob for every joint ability's press-together feel.")]
    [SerializeField, Range(0f, 1.5f)] private float _leniencyWindow = 0.5f;

    /// <summary>Max real-world seconds between the two players' presses to count as a joint start.</summary>
    public float LeniencyWindow => _leniencyWindow;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
