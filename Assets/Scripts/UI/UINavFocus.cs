using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Item 1 (controller menu navigation) — the ONE shared way a menu screen hands the controller its
/// initial focus. Every controller-navigable screen (Main Menu, Pause, Settings, Save-Slot, and later
/// the skill-tree modal) routes its first-selected through here so the pattern stays identical.
///
/// <para>Robust <c>null → target</c> set: the EventSystem reliably re-highlights the target even when it
/// was just activated the same frame (a bare <c>SetSelectedGameObject(target)</c> can no-op if the object
/// wasn't selectable yet). Fully null-safe — no EventSystem in the scene, or a null/inactive target, is a
/// silent no-op so mouse-only play is never disturbed.</para>
///
/// <para>Reads <see cref="EventSystem.current"/> (each scene owns one; FrontEnd and Persistent never
/// coexist — GameBootstrapper unloads FrontEnd before Persistent loads), so this is correct across the
/// front-end → gameplay handoff without any cross-scene reference (R2/R9).</para>
/// </summary>
public static class UINavFocus
{
    /// <summary>Give the current EventSystem's selection to <paramref name="target"/> (controller focus).</summary>
    public static void Focus(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return;
        var es = EventSystem.current;
        if (es == null) return;
        // P-C: ensure this EventSystem carries the focus-guardian (recovers a highlight lost to a mouse click on
        // empty space) AND the glow highlighter (the outline focus cue that follows the selection). Both are
        // auto-attached once — no scene wiring; they live and die with this scene's EventSystem.
        if (es.GetComponent<UINavFocusGuardian>() == null)
            es.gameObject.AddComponent<UINavFocusGuardian>();
        if (es.GetComponent<UINavHighlighter>() == null)
            es.gameObject.AddComponent<UINavHighlighter>();
        es.SetSelectedGameObject(null);
        es.SetSelectedGameObject(target);
    }

    /// <summary>Convenience overload — focus a Selectable/Button/Component by its GameObject.</summary>
    public static void Focus(Component target)
    {
        if (target != null) Focus(target.gameObject);
    }

    /// <summary>
    /// Re-assert focus if the screen is open but nothing is selected (e.g. a mouse click on empty space
    /// cleared the selection, then the player reaches for the pad). Call from a screen's Update while open.
    /// No-op while a valid selection already exists, so it never fights the pad's own navigation.
    /// </summary>
    public static void KeepFocus(GameObject fallback)
    {
        if (fallback == null) return;
        var es = EventSystem.current;
        if (es == null) return;
        var sel = es.currentSelectedGameObject;
        if (sel == null || !sel.activeInHierarchy)
            Focus(fallback);
    }
}
