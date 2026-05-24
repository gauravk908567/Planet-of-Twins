#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

// ── Marker attributes ──────────────────────────────────────────────
/// <summary>
/// Mark an int field on a TutorialStepBase SO to show a dropdown
/// of checkpoint names from TutorialSceneContext.checkpoints[].
/// </summary>
public class TutorialCheckpointIndexAttribute : PropertyAttribute { }

/// <summary>
/// Mark an int field on a TutorialStepBase SO to show a dropdown
/// of activatable names from TutorialSceneContext.activatables[].
/// </summary>
public class TutorialActivatableIndexAttribute : PropertyAttribute { }

// ── Checkpoint index drawer ────────────────────────────────────────
[CustomPropertyDrawer(typeof(TutorialCheckpointIndexAttribute))]
public class TutorialCheckpointIndexDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        var names = GetCheckpointNames();

        if (names.Length == 0)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(pos, prop, label);
            if (EditorGUI.EndChangeCheck()) prop.serializedObject.ApplyModifiedProperties();
            return;
        }

        int current = Mathf.Clamp(prop.intValue, 0, names.Length - 1);
        int selected = EditorGUI.Popup(pos, label.text, current, names);

        if (selected != current)
        {
            prop.intValue = selected;
            prop.serializedObject.ApplyModifiedProperties();
        }
    }

    private static string[] GetCheckpointNames()
    {
        // Find TutorialDirector in scene — read context.checkpoints for names
        var director = Object.FindAnyObjectByType<TutorialDirector>();
        if (director == null) return new[] { "(no TutorialDirector in scene)" };

        var so = new SerializedObject(director);
        var ctx = so.FindProperty("context");
        if (ctx == null) return new[] { "(context not found)" };

        var cps = ctx.FindPropertyRelative("checkpoints");
        if (cps == null || cps.arraySize == 0) return new[] { "(no checkpoints)" };

        var names = new List<string>();
        for (int i = 0; i < cps.arraySize; i++)
        {
            var entry = cps.GetArrayElementAtIndex(i);
            var name = entry.FindPropertyRelative("name");
            names.Add($"[{i}] {(name != null ? name.stringValue : "Unnamed")}");
        }
        return names.ToArray();
    }
}

// ── Activatable index drawer ───────────────────────────────────────
[CustomPropertyDrawer(typeof(TutorialActivatableIndexAttribute))]
public class TutorialActivatableIndexDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        var names = GetActivatableNames();

        if (names.Length == 0)
        {
            EditorGUI.PropertyField(pos, prop, label);
            return;
        }

        int current = Mathf.Clamp(prop.intValue, 0, names.Length - 1);
        int selected = EditorGUI.Popup(pos, label.text, current, names);

        if (selected != current)
        {
            prop.intValue = selected;
            prop.serializedObject.ApplyModifiedProperties();
        }
    }

    private static string[] GetActivatableNames()
    {
        var director = Object.FindAnyObjectByType<TutorialDirector>();
        if (director == null) return new[] { "(no TutorialDirector in scene)" };

        var so = new SerializedObject(director);
        var ctx = so.FindProperty("context");
        if (ctx == null) return new[] { "(context not found)" };

        var acts = ctx.FindPropertyRelative("activatables");
        if (acts == null || acts.arraySize == 0) return new[] { "(no activatables)" };

        var names = new List<string>();
        for (int i = 0; i < acts.arraySize; i++)
        {
            var entry = acts.GetArrayElementAtIndex(i);
            string n = entry.objectReferenceValue != null
                ? entry.objectReferenceValue.name
                : "null";
            names.Add($"[{i}] {n}");
        }
        return names.ToArray();
    }
}
#endif