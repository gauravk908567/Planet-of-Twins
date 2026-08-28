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
/// <para><b>TODO (user 2026-08-27): replace the ColorTint highlight with a glowing OUTLINE/BORDER.</b> The
/// flat colour tint reads but looks cheap; the intended look is a gold glow border around the focused item.
/// Planned approach = a shared "follower" highlight graphic (a 9-sliced glow-outline sprite that snaps onto
/// the currently-selected RectTransform), which also removes the dependency on each button having a
/// targetGraphic. Interim tint stays until that lands.</para>
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
            var cb = s.colors;
            cb.highlightedColor = a;                       // mouse hover
            cb.selectedColor = a;                          // controller / keyboard focus
            cb.fadeDuration = Mathf.Min(cb.fadeDuration, 0.1f);
            cb.colorMultiplier = Mathf.Max(cb.colorMultiplier, 1f);
            s.colors = cb;
        }
    }
}
