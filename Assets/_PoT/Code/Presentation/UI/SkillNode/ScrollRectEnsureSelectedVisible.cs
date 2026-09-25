using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// P-B — keeps the EventSystem's currently-selected item visible inside a <see cref="ScrollRect"/>. A default
/// ScrollRect only responds to mouse drag/wheel, so controller/keyboard navigation moves the highlight off
/// screen without ever scrolling (the skill-tree "it only highlights the two on screen" bug). This drives the
/// content so that as the cursor moves past the visible window, the list scrolls to reveal the next item.
///
/// <para>Attached at runtime by <see cref="SkillTreeUI"/> to every ScrollRect under its panel, so it needs no
/// scene wiring and works whatever the exact hierarchy is. Only acts on a selection that is a descendant of
/// THIS ScrollRect's content, so multiple tab ScrollRects don't fight over one selection.</para>
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollRectEnsureSelectedVisible : MonoBehaviour
{
    [Tooltip("Margin (px, viewport space) kept between the selected item and the viewport edge.")]
    [SerializeField] private float _padding = 12f;

    private ScrollRect _sr;

    private void Awake() => _sr = GetComponent<ScrollRect>();

    private void Update()
    {
        if (_sr == null || _sr.content == null) return;
        RectTransform viewport = _sr.viewport != null ? _sr.viewport : (RectTransform)_sr.transform;

        var es = EventSystem.current;
        var selGO = es != null ? es.currentSelectedGameObject : null;
        if (selGO == null) return;

        var target = selGO.transform as RectTransform;
        if (target == null || !target.IsChildOf(_sr.content)) return;   // not our list — ignore

        EnsureVisible(viewport, target);
    }

    private void EnsureVisible(RectTransform viewport, RectTransform target)
    {
        // Target centre + half-extents expressed in the viewport's local space (viewport centre = origin).
        Vector3 targetLocal = viewport.InverseTransformPoint(target.position);
        Rect vRect = viewport.rect;
        Rect tRect = target.rect;
        float halfVW = vRect.width * 0.5f, halfVH = vRect.height * 0.5f;
        float halfTW = tRect.width * 0.5f, halfTH = tRect.height * 0.5f;

        Vector2 move = Vector2.zero;

        if (_sr.vertical)
        {
            float top = targetLocal.y + halfTH;
            float bottom = targetLocal.y - halfTH;
            if (top > halfVH - _padding) move.y = top - (halfVH - _padding);          // above view → scroll up
            else if (bottom < -halfVH + _padding) move.y = bottom - (-halfVH + _padding); // below view → scroll down
        }
        if (_sr.horizontal)
        {
            float right = targetLocal.x + halfTW;
            float left = targetLocal.x - halfTW;
            if (right > halfVW - _padding) move.x = right - (halfVW - _padding);
            else if (left < -halfVW + _padding) move.x = left - (-halfVW + _padding);
        }

        if (move.sqrMagnitude < 0.25f) return;

        // Content moves opposite to bring the target into the window. Standard ScrollRect content (anchored so
        // +y scrolls up) → subtract the viewport-space overflow. If a scene sets content up mirrored, flip here.
        Vector2 ap = _sr.content.anchoredPosition;
        if (_sr.vertical) ap.y -= move.y;
        if (_sr.horizontal) ap.x -= move.x;
        _sr.content.anchoredPosition = ap;
    }
}
