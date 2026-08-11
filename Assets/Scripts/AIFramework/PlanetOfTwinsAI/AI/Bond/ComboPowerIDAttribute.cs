using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Attribute to mark a string field as a ComboPowerID.
/// Shows a dropdown in Inspector populated from ComboPowerIDs constants.
/// Usage: [ComboPowerID] public string powerID;
/// </summary>
public class ComboPowerIDAttribute : PropertyAttribute { }

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(ComboPowerIDAttribute))]
public class ComboPowerIDDrawer : PropertyDrawer
{
    private static string[] _ids;
    private static string[] _display;

    private static void BuildIDs()
    {
        if (_ids != null) return;
        var ids = new List<string> { "" };
        var display = new List<string> { "(none)" };
        var fields = typeof(ComboPowerIDs)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var f in fields)
        {
            if (f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            {
                string val = (string)f.GetRawConstantValue();
                ids.Add(val);
                display.Add($"{f.Name}");
            }
        }
        _ids = ids.ToArray();
        _display = display.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        BuildIDs();
        if (property.propertyType != SerializedPropertyType.String)
        { EditorGUI.PropertyField(position, property, label); return; }

        string current = property.stringValue;
        int idx = System.Array.IndexOf(_ids, current);
        if (idx < 0) idx = 0;

        EditorGUI.BeginProperty(position, label, property);
        int selected = EditorGUI.Popup(position, label.text, idx, _display);
        if (selected != idx)
            property.stringValue = _ids[selected];
        EditorGUI.EndProperty();
    }
}
#endif