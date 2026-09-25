using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for <c>ManpuVocabulary</c> — the Manpu authoring tool. It reflects over the enums
/// so EVERY <c>EnemyMood</c>, <c>EnemySearchState</c> and <c>ManpuAbility</c> shows up as a row
/// automatically (add a mood to the enum and a new row appears here — no manual sync). Each row has
/// drag-drop slots for a Sprite, an optional particle (ParticleSystem prefab) and an optional sound
/// (SoundCueData). Empty Sprite = no glyph for that trigger (suppressed) — that's the R3 curation.
/// </summary>
[CustomEditor(typeof(ManpuVocabulary))]
public class ManpuVocabularyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var vocab = (ManpuVocabulary)target;
        if (SyncEntries(vocab))
        {
            EditorUtility.SetDirty(vocab);
            serializedObject.Update();
        }
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Every mood / search-state / ability is listed automatically — add a value to the enum and a " +
            "new row appears here. Drag a Sprite (+ optional Particle + Sound) onto a row to give that " +
            "trigger a glyph. EMPTY SPRITE = no glyph (suppressed) — fill only the high-value rows (R3).",
            MessageType.Info);

        DrawAbilities(serializedObject.FindProperty("abilities"));
        DrawTriggerList("Moods (pulse on escalating entry)", serializedObject.FindProperty("moods"), "mood", true);
        DrawTriggerList("Perception (pulse — start set: Pursuing)", serializedObject.FindProperty("perception"), "state", false);

        serializedObject.ApplyModifiedProperties();
    }

    // Ensure exactly one entry exists per enum value (additive — never removes your data).
    private static bool SyncEntries(ManpuVocabulary v)
    {
        bool changed = false;
        foreach (ManpuAbility a in Enum.GetValues(typeof(ManpuAbility)))
            if (!v.abilities.Exists(e => e != null && e.ability == a))
            { v.abilities.Add(new ManpuVocabulary.AbilityEntry { ability = a }); changed = true; }
        foreach (ManpuMood m in Enum.GetValues(typeof(ManpuMood)))
            if (!v.moods.Exists(e => e != null && e.mood == m))
            { v.moods.Add(new ManpuVocabulary.MoodEntry { mood = m }); changed = true; }
        foreach (ManpuSearchState s in Enum.GetValues(typeof(ManpuSearchState)))
            if (!v.perception.Exists(e => e != null && e.state == s))
            { v.perception.Add(new ManpuVocabulary.PerceptionEntry { state = s }); changed = true; }
        return changed;
    }

    private static void DrawAbilities(SerializedProperty list)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Abilities (held → closing sequence)", EditorStyles.boldLabel);
        for (int i = 0; i < list.arraySize; i++)
        {
            var el = list.GetArrayElementAtIndex(i);
            var key = el.FindPropertyRelative("ability");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(EnumName(key), EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(el.FindPropertyRelative("held"), new GUIContent("Held glyph"), true);
                DrawClosingSequence(el.FindPropertyRelative("closingSequence"));
            }
        }
    }

 // an ordered list of closing beats (glyph + holdSeconds), capped at 4 (abilities never need more).
    private static void DrawClosingSequence(SerializedProperty seq)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Closing sequence ({seq.arraySize}/4)", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(seq.arraySize >= 4))
                if (GUILayout.Button("+ Beat", GUILayout.Width(60))) seq.arraySize++;
        }
        int remove = -1;
        for (int b = 0; b < seq.arraySize; b++)
        {
            var beat = seq.GetArrayElementAtIndex(b);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Beat {b + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(56));
                    EditorGUILayout.PropertyField(beat.FindPropertyRelative("holdSeconds"), new GUIContent("hold (s)"));
                    if (GUILayout.Button("✕", GUILayout.Width(22))) remove = b;
                }
                EditorGUILayout.PropertyField(beat.FindPropertyRelative("glyph"), new GUIContent("Glyph"), true);
            }
        }
        if (remove >= 0) seq.DeleteArrayElementAtIndex(remove);
    }

    private static void DrawTriggerList(string header, SerializedProperty list, string keyProp, bool isMood)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
        for (int i = 0; i < list.arraySize; i++)
        {
            var el = list.GetArrayElementAtIndex(i);
            var key = el.FindPropertyRelative(keyProp);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(EnumName(key), EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(el.FindPropertyRelative("glyph"), new GUIContent("Glyph"), true);
                if (isMood)
                {
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("escalatingOnly"));
                    // The P11 held-aura channel — was never drawn here, so it could not be authored (BUG-068).
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("loopPrefab"),
                        new GUIContent("Loop aura (held while mood active)"));
                }
            }
        }
    }

    private static string EnumName(SerializedProperty enumProp)
    {
        int idx = enumProp.enumValueIndex;
        var names = enumProp.enumDisplayNames;
        return idx >= 0 && idx < names.Length ? names[idx] : "(unknown)";
    }
}
