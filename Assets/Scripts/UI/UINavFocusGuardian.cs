using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// P-C — keeps a controller/keyboard highlight alive. The EventSystem drops its selection whenever the mouse
/// clicks empty space (or a non-selectable), after which pad/stick navigation does nothing until something is
/// selected again — the "I pressed a key but no highlight came back" problem. This remembers the last live
/// selection and, the moment a navigation intent arrives while nothing is selected, restores it.
///
/// <para>Auto-attached to the active EventSystem by <see cref="UINavFocus.Focus"/> — no scene wiring. If the
/// remembered item is gone (its screen closed), it does nothing; the newly-opened screen sets its own
/// first-selected via <see cref="UINavFocus"/>.</para>
/// </summary>
[DisallowMultipleComponent]
public class UINavFocusGuardian : MonoBehaviour
{
    private GameObject _lastValid;

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        var cur = es.currentSelectedGameObject;
        if (cur != null && cur.activeInHierarchy)
        {
            _lastValid = cur;   // remember the live selection
            return;
        }

        // Nothing selected — restore the last live item on any navigation intent (stick / dpad / WASD / arrows
        // past a deadzone), from EITHER device (the P-A shared aggregator).
        if (_lastValid == null || !_lastValid.activeInHierarchy) return;
        var input = PlayerInputRouter.SharedInput;
        if (input == null || input.GetMovementInput().sqrMagnitude < 0.04f) return;

        es.SetSelectedGameObject(_lastValid);
    }
}
