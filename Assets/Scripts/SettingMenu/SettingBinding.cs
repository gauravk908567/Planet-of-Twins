using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Marks one settings-row control with its stable <see cref="SettingsCatalog"/> Id and exposes the
/// UI control it wraps. Purely PASSIVE data: the SettingsScreenController discovers these by a child
/// sweep and binds each to its handler by Id. Because binding is by Id — never by a serialized
/// reference from the controller down to this object — moving the row to another tab, or having the
/// builder regenerate it, keeps the wiring intact as long as the Id survives. This is the single
/// mechanism that lets structure and visuals change without breaking function.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingBinding : MonoBehaviour
{
    [Tooltip("Stable id from SettingsCatalog (use its constants), e.g. \"video.resolution\".")]
    [SerializeField] private string _id;

    [Header("Wire the ONE control matching the definition's type")]
    [SerializeField] private TMP_Dropdown _dropdown;
    [SerializeField] private Slider _slider;
    [SerializeField] private Toggle _toggle;
    [SerializeField] private Button _button;

    [Header("Optional readout (render-scale value, API \"Current: …\")")]
    [SerializeField] private TMP_Text _valueLabel;

    public string Id => _id;
    public TMP_Dropdown Dropdown => _dropdown;
    public Slider Slider => _slider;
    public Toggle Toggle => _toggle;
    public Button Button => _button;
    public TMP_Text ValueLabel => _valueLabel;

#if UNITY_EDITOR
    /// <summary>Editor/builder hook: stamp the id + control refs when generating or repairing a row.</summary>
    public void EditorConfigure(string id, TMP_Dropdown dropdown, Slider slider, Toggle toggle,
        Button button, TMP_Text valueLabel)
    {
        _id = id;
        _dropdown = dropdown;
        _slider = slider;
        _toggle = toggle;
        _button = button;
        _valueLabel = valueLabel;
    }
#endif
}
