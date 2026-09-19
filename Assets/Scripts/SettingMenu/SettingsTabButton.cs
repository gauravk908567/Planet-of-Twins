using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sits on a top-bar TAB button. When the button becomes selected — by mouse, stick/D-pad
/// navigation, or LB/RB — it tells the <see cref="SettingsTabBar"/> to show this tab's panel, the
/// Valorant-style "highlight a tab = show it" behaviour. Focus stays on the bar; the player drops
/// into the content with Down/Submit. Passive otherwise; the bar owns the tab list and index.
/// </summary>
public sealed class SettingsTabButton : MonoBehaviour, ISelectHandler
{
    private SettingsTabBar _bar;
    private int _index;

    public void Bind(SettingsTabBar bar, int index)
    {
        _bar = bar;
        _index = index;
    }

    public void OnSelect(BaseEventData eventData) => _bar?.Select(_index);
}
