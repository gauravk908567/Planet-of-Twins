using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// True while a text box has the keyboard (a focused <see cref="TMP_InputField"/>, e.g. the report screen's "What
/// happened?", game.md §27). While the player types, no key may act as a game or menu key: typing "hello" must not
/// toggle the control hints (H) or start the overview camera (B). <see cref="TwinInputReader"/>'s getters read
/// silent while this is true, except Pause and UI Cancel, which end the typing (through the scene's Back arbiter).
/// Checked once per frame; false whenever no text box is focused, which is nearly always.
/// </summary>
public static class UITextEntry
{
    private static int _frame = -1;
    private static bool _typing;

    public static bool IsTyping
    {
        get
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                var es = EventSystem.current;
                var selected = es != null ? es.currentSelectedGameObject : null;
                var field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
                _typing = field != null && field.isFocused;
            }
            return _typing;
        }
    }
}
