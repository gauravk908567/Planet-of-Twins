using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

// ── Marker attributes (RUNTIME — used on serialized fields of runtime SOs,
//    so they must compile into the player build; only the drawers are editor-only) ──
/// <summary>
/// Mark a string field on a TutorialStepBase SO to show a dropdown of checkpoint names from
/// TutorialStepContext.checkpoints[]. The field stores the picked entry's hidden stable id, never its name or position.
/// </summary>
public class TutorialCheckpointIdAttribute : PropertyAttribute { }

/// <summary>
/// Mark an int field on a TutorialStepBase SO to show a dropdown
/// of activatable names from TutorialSceneContext.activatables[].
/// </summary>
public class TutorialActivatableIndexAttribute : PropertyAttribute { }

#if UNITY_EDITOR
// ── Checkpoint id drawer ───────────────────────────────────────────
// Same dropdown as before ("[i] name" from the open scene's TutorialDirector), but the pick is stored as the entry's
// hidden stable id. Without a TutorialDirector in the open scenes (L1_Park closed) it shows the stored id read-only.
[CustomPropertyDrawer(typeof(TutorialCheckpointIdAttribute))]
public class TutorialCheckpointIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        if (!TryGetCheckpoints(out var labels, out var ids))
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.TextField(pos, label.text + " (open L1_Park to pick)", prop.stringValue);
            return;
        }

        // A stored id no entry carries (entry removed, or never picked) stays visible as the first option.
        int current = System.Array.IndexOf(ids, prop.stringValue);
        bool missing = current < 0;
        var options = new List<string>(labels.Length + 1);
        if (missing) options.Add(string.IsNullOrEmpty(prop.stringValue) ? "(none)" : "(missing checkpoint)");
        options.AddRange(labels);
        if (missing) current = 0;

        int selected = EditorGUI.Popup(pos, label.text, current, options.ToArray());
        if (selected == current) return;

        int index = missing ? selected - 1 : selected;
        if (index < 0 || string.IsNullOrEmpty(ids[index])) return;   // entry has no id yet: save L1_Park first
        prop.stringValue = ids[index];
        prop.serializedObject.ApplyModifiedProperties();
    }

    private static bool TryGetCheckpoints(out string[] labels, out string[] ids)
    {
        labels = ids = null;
        var director = Object.FindAnyObjectByType<TutorialDirector>();
        if (director == null) return false;

        var so = new SerializedObject(director);
        var cps = so.FindProperty("context")?.FindPropertyRelative("checkpoints");
        if (cps == null) return false;

        labels = new string[cps.arraySize];
        ids = new string[cps.arraySize];
        for (int i = 0; i < cps.arraySize; i++)
        {
            var entry = cps.GetArrayElementAtIndex(i);
            labels[i] = $"[{i}] {entry.FindPropertyRelative("name")?.stringValue ?? "Unnamed"}";
            ids[i] = entry.FindPropertyRelative("id")?.stringValue ?? "";
        }
        return true;
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