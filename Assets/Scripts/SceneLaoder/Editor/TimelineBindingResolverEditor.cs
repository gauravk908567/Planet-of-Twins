#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Inspector for <see cref="TimelineBindingResolver"/> (instruction.md §16.1).
/// Each row's Track field is a dropdown populated from the director's actual tracks, so the
/// designer picks a real track by name and the underlying <see cref="TrackAsset"/> reference is
/// stored (rename-proof, no string match). Role is picked from the enum dropdown.
/// </summary>
[CustomEditor(typeof(TimelineBindingResolver))]
public class TimelineBindingResolverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var resolver = (TimelineBindingResolver)target;
        var director = resolver.GetComponent<PlayableDirector>();
        var timeline = director != null ? director.playableAsset as TimelineAsset : null;

        // List all non-group output tracks (Cinemachine is auto-bound by type but harmless to show).
        var tracks = new List<TrackAsset>();
        var names  = new List<string>();
        if (timeline != null)
        {
            foreach (var t in timeline.GetOutputTracks())
            {
                if (t == null || t is GroupTrack) continue;
                tracks.Add(t);
                names.Add($"{t.name}  ({t.GetType().Name})");
            }
        }

        EditorGUILayout.HelpBox(
            timeline == null
                ? "No TimelineAsset on this PlayableDirector — assign the Playable first."
                : "The lone CinemachineTrack is auto-bound by type (no row needed). Add one row " +
                  "per cross-scene track and pick which Persistent role it drives. " +
                  "Do NOT add Activation tracks that *deactivate* FadeCanvas/HUD here — use Signals.",
            timeline == null ? MessageType.Warning : MessageType.Info);

        var listProp = serializedObject.FindProperty("_trackBindings");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cross-scene Track Bindings", EditorStyles.boldLabel);

        int removeAt = -1;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem      = listProp.GetArrayElementAtIndex(i);
            var trackProp = elem.FindPropertyRelative("track");
            var roleProp  = elem.FindPropertyRelative("role");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Row {i}", GUILayout.Width(50));
            if (GUILayout.Button("Remove", GUILayout.Width(70))) removeAt = i;
            EditorGUILayout.EndHorizontal();

            if (tracks.Count > 0)
            {
                int cur = tracks.IndexOf(trackProp.objectReferenceValue as TrackAsset);
                int sel = EditorGUILayout.Popup("Track", cur < 0 ? 0 : cur, names.ToArray());
                if (sel >= 0 && sel < tracks.Count) trackProp.objectReferenceValue = tracks[sel];
            }
            else
            {
                EditorGUILayout.PropertyField(trackProp, new GUIContent("Track (no tracks found)"));
            }

            EditorGUILayout.PropertyField(roleProp, new GUIContent("Role"));
            EditorGUILayout.EndVertical();
        }

        if (removeAt >= 0) listProp.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("Add Cross-scene Track Binding"))
            listProp.InsertArrayElementAtIndex(listProp.arraySize);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
