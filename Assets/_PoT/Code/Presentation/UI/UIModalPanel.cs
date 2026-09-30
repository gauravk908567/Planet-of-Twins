using UnityEngine;

/// <summary>
/// A full-screen panel that must get every pointer event while it is open. The HUD panels that share its parent sit in
/// front of it in draw order (RescuePanel, SoulTimerPanel, AbilitiesHUD…) and would eat the clicks, so opening the modal
/// turns their raycast blocking off (adding a <see cref="CanvasGroup"/> where one is missing) and closing it turns it
/// back on.
///
/// <para>Plain class owned by the screen that shows the panel: no component, no scene wiring. First user: the game-over
/// screen (<c>GameOverController</c>). Any other full-screen panel on a shared HUD canvas uses this instead of its own
/// sibling loop.</para>
/// </summary>
public sealed class UIModalPanel
{
    private readonly GameObject _panel;

    public UIModalPanel(GameObject panel) => _panel = panel;

    /// <summary>Show the panel and stop its siblings from blocking pointer events.</summary>
    public void Open()
    {
        if (_panel == null) return;
        _panel.SetActive(true);
        SetSiblingsBlockRaycasts(false);
    }

    /// <summary>Let the siblings block pointer events again, then hide the panel.</summary>
    public void Close()
    {
        if (_panel == null) return;
        SetSiblingsBlockRaycasts(true);
        _panel.SetActive(false);
    }

    private void SetSiblingsBlockRaycasts(bool block)
    {
        Transform parent = _panel.transform.parent;
        if (parent == null) return;

        foreach (Transform sibling in parent)
        {
            if (sibling.gameObject == _panel) continue;
            var cg = sibling.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                if (block) continue;   // nothing was switched off on this sibling
                cg = sibling.gameObject.AddComponent<CanvasGroup>();
            }
            cg.blocksRaycasts = block;
        }
    }
}
