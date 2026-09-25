using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Type-safe scene reference. Stores the scene name (must be in Build Settings)
/// as a serialized string, but shows a dropdown in the Inspector that lists
/// every scene registered in File > Build Settings — prevents typos.
///
/// Usage:
///   [SerializeField] private SceneReference myScene;
///   SceneManager.LoadSceneAsync(myScene.Name, LoadSceneMode.Additive);
/// </summary>
[Serializable]
public struct SceneReference
{
    [SerializeField] private string _name;

    public string Name => _name;
    public bool IsValid => !string.IsNullOrEmpty(_name);

    public static implicit operator string(SceneReference r) => r._name;

    public override string ToString() => _name ?? "(none)";
}

// ── Editor PropertyDrawer ─────────────────────────────────────────────────────
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SceneReference))]
public class SceneReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        var nameProp = prop.FindPropertyRelative("_name");

        string[] sceneNames = GetBuildSceneNames();
        if (sceneNames.Length == 0)
        {
            EditorGUI.HelpBox(pos, "No scenes in Build Settings.", MessageType.Warning);
            return;
        }

        string current = nameProp.stringValue;
        int currentIdx = Array.IndexOf(sceneNames, current);
        bool mismatch = currentIdx < 0;
        if (mismatch) currentIdx = 0;

        var prevBg = GUI.backgroundColor;
        if (mismatch) GUI.backgroundColor = new Color(1f, 0.55f, 0.55f); // red = stored name missing from Build Profiles

        EditorGUI.BeginChangeCheck();
        string displayLabel = mismatch ? $"{label.text}  ⚠ '{current}' missing" : label.text;
        int selected = EditorGUI.Popup(pos, displayLabel, currentIdx, sceneNames);
        GUI.backgroundColor = prevBg;

        // Only write on explicit user selection — never auto-overwrite with index-0 fallback.
        if (EditorGUI.EndChangeCheck())
        {
            nameProp.stringValue = sceneNames[selected];
            prop.serializedObject.ApplyModifiedProperties();
        }
    }

    private static string[] _cachedNames;
    private static int _cachedHash;

    private static string[] GetBuildSceneNames()
    {
        // Recompute only if Build Settings changed
        var scenes = EditorBuildSettings.scenes;
        int hash = scenes.Length;
        foreach (var s in scenes) hash = hash * 397 ^ (s.path?.GetHashCode() ?? 0);

        if (_cachedNames != null && _cachedHash == hash) return _cachedNames;

        _cachedHash = hash;
        var names = new System.Collections.Generic.List<string>();
        foreach (var s in scenes)
        {
            if (string.IsNullOrEmpty(s.path)) continue;
            names.Add(System.IO.Path.GetFileNameWithoutExtension(s.path));
        }
        _cachedNames = names.ToArray();
        return _cachedNames;
    }
}
#endif
