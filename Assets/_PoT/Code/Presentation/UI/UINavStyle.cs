using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Item 1 (controller nav) — makes a menu subtree controller-usable with ONE call, so every screen behaves
/// identically. For each <see cref="Selectable"/> under a root it:
///   1. sets Navigation = Automatic when it was None, so a pad can actually traverse the buttons;
///   2. gives it a clearly-visible Highlighted + Selected colour (via ColorTint) so the focused item always
///      reads on screen — the thing greybox buttons were missing.
///
/// <para>Idempotent and non-destructive: a Selectable that already uses a deliberate SpriteSwap/Animation
/// transition is left alone (its own highlight is respected); only None/ColorTint items get the accent. Call
/// once from a menu controller's Awake — no per-button scene tuning, no new scene objects.</para>
///
/// <para>The <b>primary</b> focus cue is now the glowing outline in <see cref="UINavHighlighter"/> (the shared
/// follower graphic that snaps onto the selected RectTransform — the replacement for the old flat-gold tint the
/// user flagged as cheap). The ColorTint set here is deliberately kept as only a GENTLE warm shift under that
/// glow; it no longer carries the highlight on its own.</para>
/// </summary>
public static class UINavStyle
{
    /// <summary>Bright gold accent — reads clearly on the dark greybox panels. Swap when final art lands.</summary>
    public static readonly Color DefaultAccent = new Color(1f, 0.82f, 0.30f, 1f);

    public static void Apply(GameObject root, Color? accent = null)
    {
        if (root == null) return;
        Color a = accent ?? DefaultAccent;

        foreach (var s in root.GetComponentsInChildren<Selectable>(includeInactive: true))
            Style(s, a);
    }

    /// <summary>Style a single <see cref="Selectable"/> (traversal + visible focus tint) — same rules as
    /// <see cref="Apply"/>, for callers that must pick specific items rather than sweep a whole subtree
    /// (e.g. skill nodes, whose click-only child buttons must NOT become stray navigation stops).</summary>
    public static void Style(Selectable s, Color? accent = null)
    {
        if (s == null) return;
        Color a = accent ?? DefaultAccent;

        // 1) Traversal — a pad can only move between Selectables whose navigation isn't None.
        var nav = s.navigation;
        if (nav.mode == Navigation.Mode.None)
        {
            nav.mode = Navigation.Mode.Automatic;
            s.navigation = nav;
        }

        // 2) Visible focus — only touch plain/ColorTint items; respect deliberate SpriteSwap/Animation.
        if (s.transition == Selectable.Transition.None)
            s.transition = Selectable.Transition.ColorTint;

        if (s.transition == Selectable.Transition.ColorTint)
        {
            // The glowing outline (UINavHighlighter) is now the primary focus cue, so this tint is only a
            // GENTLE warm shift — a light lerp toward the accent, not the old flat-gold recolor the user flagged
            // as cheap. Keeps a subtle bg change under the glow; the glow does the heavy lifting.
            Color soft = Color.Lerp(Color.white, a, 0.35f);
            var cb = s.colors;
            cb.highlightedColor = soft;                    // mouse hover
            cb.selectedColor = soft;                       // controller / keyboard focus
            cb.fadeDuration = Mathf.Min(cb.fadeDuration, 0.1f);
            cb.colorMultiplier = Mathf.Max(cb.colorMultiplier, 1f);
            s.colors = cb;
        }
    }

    /// <summary>Which screen axis the menu's items run along (drives which nav directions wrap).</summary>
    public enum WrapAxis { Vertical, Horizontal }

    // Reused scratch list — WireWrap runs on menu-open (never in a hot loop) but this keeps it alloc-free.
    private static readonly List<Selectable> _wrapBuffer = new List<Selectable>(16);

    /// <summary>
    /// P-C — explicit <b>wrap-around</b> navigation for a single-line menu. Unity's Automatic navigation
    /// stops dead at the ends of a list; a pad pressing Down on the last item (or Up on the first) does
    /// nothing. This collects the ACTIVE + interactable <see cref="Selectable"/>s under
    /// <paramref name="root"/> in hierarchy order (= visual order for a vertical/horizontal LayoutGroup) and
    /// links them head-to-tail along one axis, so focus recycles last↔first.
    ///
    /// <para>Call AFTER this showing's interactability is settled (e.g. a disabled Continue button is left
    /// out of the cycle) — it's cheap and re-callable every time the screen opens. The cross axis is left
    /// null: a vertical menu's Left/Right stay free for sliders/dropdowns to consume as value changes.
    /// Only up/down (or left/right) are wired, so this composes with <see cref="Apply"/> having run first.</para>
    /// </summary>
    public static void WireWrap(GameObject root, WrapAxis axis = WrapAxis.Vertical)
    {
        if (root == null) return;

        _wrapBuffer.Clear();
        foreach (var s in root.GetComponentsInChildren<Selectable>(includeInactive: false))
            if (s != null && s.IsInteractable() && s.navigation.mode != Navigation.Mode.None)
                _wrapBuffer.Add(s);

        int n = _wrapBuffer.Count;
        if (n == 0) return;   // nothing navigable this showing — leave as-is

        for (int i = 0; i < n; i++)
        {
            var s = _wrapBuffer[i];
            var prev = _wrapBuffer[(i - 1 + n) % n];   // wraps: first's prev = last
            var next = _wrapBuffer[(i + 1) % n];       // wraps: last's next = first

            var nav = s.navigation;
            nav.mode = Navigation.Mode.Explicit;
            if (axis == WrapAxis.Vertical)
            {
                nav.selectOnUp = prev;
                nav.selectOnDown = next;
                nav.selectOnLeft = null;   // free for horizontal sliders/dropdowns to eat as value change
                nav.selectOnRight = null;
            }
            else
            {
                nav.selectOnLeft = prev;
                nav.selectOnRight = next;
                nav.selectOnUp = null;
                nav.selectOnDown = null;
            }
            s.navigation = nav;
        }
    }
}
