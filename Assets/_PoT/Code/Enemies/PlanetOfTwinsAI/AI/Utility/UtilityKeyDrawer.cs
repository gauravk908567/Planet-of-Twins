#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Custom property drawer for UtilityFactor.
/// Integrates blackboard key dropdown directly — no plain text field.
/// Shows live curve preview that updates with shape/steepness changes.
/// </summary>
[CustomPropertyDrawer(typeof(UtilityFactor))]
public class UtilityFactorDrawer : PropertyDrawer
{
    private const float PreviewHeight = 56f;
    private const float Spacing = 2f;
    private const int FieldCount = 7; // key + shape + steep + step + weight + invert + isBool

    // Cached key list shared across all instances
    private static string[] _keys;
    private static string[] _display;

    private static void EnsureKeys()
    {
        if (_keys != null) return;
        var keys = new List<string> { "" };
        var display = new List<string> { "(none)" };
        var fields = typeof(UtilityFactorKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var f in fields)
        {
            if (!f.IsLiteral || f.IsInitOnly || f.FieldType != typeof(string)) continue;
            string val = (string)f.GetRawConstantValue();
            keys.Add(val);
            display.Add(f.Name);
        }
        _keys = keys.ToArray();
        _display = display.ToArray();
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        if (!prop.isExpanded)
            return lineH;
        return lineH * (1 + FieldCount) + PreviewHeight + Spacing * (FieldCount + 3);
    }

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EnsureKeys();
        EditorGUI.BeginProperty(pos, label, prop);

        float lineH = EditorGUIUtility.singleLineHeight;
        float y = pos.y;

        // ── Foldout ──────────────────────────────────────────────
        prop.isExpanded = EditorGUI.Foldout(
            new Rect(pos.x, y, pos.width, lineH),
            prop.isExpanded, label, true);

        if (!prop.isExpanded) { EditorGUI.EndProperty(); return; }
        y += lineH + Spacing;

        // Helper — next field rect
        Rect NextLine()
        {
            var r = new Rect(pos.x, y, pos.width, lineH);
            y += lineH + Spacing;
            return r;
        }

        // ── Blackboard Key — dropdown ─────────────────────────────
        var keyProp = prop.FindPropertyRelative("blackboardKey");
        string current = keyProp.stringValue;
        int idx = System.Array.IndexOf(_keys, current);
        if (idx < 0) idx = 0;

        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(NextLine(), "Blackboard Key", idx, _display);
        if (EditorGUI.EndChangeCheck())
            keyProp.stringValue = _keys[selected];

        // ── Curve shape fields ────────────────────────────────────
        var shapeProp = prop.FindPropertyRelative("curveShape");
        EditorGUI.PropertyField(NextLine(), shapeProp, new GUIContent("Curve Shape"));

        var steepProp = prop.FindPropertyRelative("steepness");
        EditorGUI.PropertyField(NextLine(), steepProp, new GUIContent("Steepness"));

        var stepProp = prop.FindPropertyRelative("stepThreshold");
        EditorGUI.PropertyField(NextLine(), stepProp, new GUIContent("Step Threshold"));

        // ── Weight + modifiers ────────────────────────────────────
        EditorGUI.PropertyField(NextLine(), prop.FindPropertyRelative("weight"), new GUIContent("Weight"));
        EditorGUI.PropertyField(NextLine(), prop.FindPropertyRelative("invert"), new GUIContent("Invert"));
        EditorGUI.PropertyField(NextLine(), prop.FindPropertyRelative("isBool"), new GUIContent("Is Bool"));

        // ── Curve preview ─────────────────────────────────────────
        y += Spacing;
        var previewRect = new Rect(pos.x, y, pos.width, PreviewHeight);

        var shape = (CurveShape)shapeProp.enumValueIndex;
        float steep = steepProp.floatValue;
        float stepTh = stepProp.floatValue;
        bool inv = prop.FindPropertyRelative("invert").boolValue;
        bool isBool = prop.FindPropertyRelative("isBool").boolValue;

        AnimationCurve preview;
        if (isBool)
        {
            preview = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.49f, 0),
                                         new Keyframe(0.5f, 1), new Keyframe(1, 1));
        }
        else
        {
            preview = CurveGenerator.Generate(shape, steep, stepTh);
            if (inv)
            {
                var keys = preview.keys;
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = new Keyframe(keys[i].time, 1f - keys[i].value);
                preview = new AnimationCurve(keys);
            }
        }

        // Read-only curve display — ranges locked to 0-1
        EditorGUI.CurveField(previewRect, new GUIContent("Preview"), preview,
            Color.green, new Rect(0, 0, 1, 1));

        EditorGUI.EndProperty();
    }
}
#endif