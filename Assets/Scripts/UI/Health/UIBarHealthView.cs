using UnityEngine;

/// <summary>
/// An <see cref="IHealthBarView"/> backed by the new user-art bars (<see cref="UIBarView"/>)
/// instead of a UGUI Slider.
///
/// This is an ADAPTER, deliberately. Every presenter in the project already talks to
/// IHealthBarView and only ever calls SetFill / SetCriticalState, so the art can be replaced
/// wholesale without touching a single line of health logic. SharedHealthPresenter, the
/// per-twin bars and the enemy bars all keep working exactly as they do today.
///
/// Drives a LIST of bars because the shared-health emblem is two halves. Both halves receive the
/// SAME fill — the pool is one number, and one number must mean one thing. What differs between
/// the halves is their clan colour and their DRAIN (per-twin bond weakness), which is a separate
/// channel set through SetBondWeakness and never through fill.
/// </summary>
[DisallowMultipleComponent]
public class UIBarHealthView : MonoBehaviour, IHealthBarView
{
    [Header("Bars driven by this view")]
    [Tooltip("One entry for a normal bar; two for the shared emblem's left and right halves. " +
             "All entries receive the same fill.")]
    [SerializeField] private UIBarView[] _bars;

    private bool _critical;

    private void Awake()
    {
        if (_bars == null || _bars.Length == 0)
        {
            Debug.LogError($"{nameof(UIBarHealthView)} on '{name}' has no bars assigned — the " +
                           "health display will silently do nothing. Disabling.", this);
            enabled = false;
        }
    }

    /// <summary>0 = empty, 1 = full. The real pool value; never the bond/distance signal.</summary>
    public void SetFill(float normalised)
    {
        if (_bars == null) return;
        float v = Mathf.Clamp01(normalised);
        for (int i = 0; i < _bars.Length; i++)
            if (_bars[i] != null) _bars[i].SetValue(v);
    }

    /// <summary>
    /// Intentionally a NO-OP. UIBarView already drives the low-health flash itself, from the
    /// displayed value against its own `_flashThreshold` (0.30 by default), ramping the pulse as
    /// the value approaches zero — and the flash lives on the FRAME and clan symbol rather than
    /// as an interior wash, per the authored design.
    ///
    /// Forcing it from here as well would give one visual effect two owners, and they would fight
    /// whenever the presenter's idea of "critical" disagreed with the bar's threshold. If the
    /// presenter ever needs to override the bar's own judgement, that is a deliberate new
    /// override on UIBarView, not a second writer bolted on here.
    /// </summary>
    public void SetCriticalState(bool isCritical)
    {
        _critical = isCritical;
    }

    /// <summary>Last critical state the presenter reported. Display is driven by UIBarView.</summary>
    public bool IsCritical => _critical;

    /// <summary>
    /// Bond weakness for a specific half, 0 = bonded, 1 = fully stretched. Drains that half's
    /// clan colour toward grey WITHOUT moving the fill — grey reads as "weakened", never "dying".
    /// Index matches the bars array (0 = left/Lyra, 1 = right/Kai on the shared emblem).
    /// </summary>
    public void SetBondWeakness(int barIndex, float weakness01)
    {
        if (_bars == null || barIndex < 0 || barIndex >= _bars.Length) return;
        if (_bars[barIndex] != null) _bars[barIndex].SetDrain(Mathf.Clamp01(weakness01));
    }

    /// <summary>Applies the same bond weakness to every bar this view drives.</summary>
    public void SetBondWeakness(float weakness01)
    {
        if (_bars == null) return;
        float w = Mathf.Clamp01(weakness01);
        for (int i = 0; i < _bars.Length; i++)
            if (_bars[i] != null) _bars[i].SetDrain(w);
    }
}
