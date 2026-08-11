using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Cue Book authoring panel — the container of NAMED effects you author
/// here. Each effect has a string id (what code plays it by) and an ordered element list; each element picks
/// a kind (Particle / Vfx / Sound) from a dropdown, shows only that kind's fields, carries its OWN audio list
/// (loop / one-shot / kill-with-visual per sound), then timing (start mode + delay), duration (default or
/// explicit), and an optional cut list that stops earlier elements at a scripted beat. No separate cue SOs —
/// drop prefabs/clips straight in.
/// </summary>
[CustomEditor(typeof(CueBookData))]
public class CueBookDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeMode"),
            new GUIContent("Sequence Time", "Domain the element list is sequenced on (delays/cuts). " +
                                            "Scaled = slows under Setsuna; Unscaled = pause-proof (UI books)."));
        EditorGUILayout.Space();

        // Author-time lint (flags only, never blocks) — shared with the Cue Id Verifier sweep. Reads the applied
        // asset state; updates one repaint after an edit (good enough for a hint).
        var findings = CueBookLinter.Analyze(target as CueBookData);

        var effects = serializedObject.FindProperty("effects");
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);

 // tier naming — shown on every book so the convention is in front of whoever authors ids.
        EditorGUILayout.HelpBox(
            "UPGRADE TIERS: name an id <baseId>_tN to make it the tier-N variant of <baseId> " +
            "(e.g. stun_cast_t2 plays instead of stun_cast once 2+ nodes of that ability's tree are " +
            "unlocked; falls back _tN → _t1 → base). An id WITHOUT _t[n] variants is used for ALL " +
            "tiers — tier only the ids you want changed; the rest keep their base effect.",
            MessageType.Info);

        int removeEffect = -1;
        for (int i = 0; i < effects.arraySize; i++)
        {
            var effect = effects.GetArrayElementAtIndex(i);
            var id = effect.FindPropertyRelative("id");
            var elements = effect.FindPropertyRelative("elements");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Id", GUILayout.Width(20));
                    id.stringValue = EditorGUILayout.TextField(id.stringValue);
                    GUILayout.Label($"{elements.arraySize} el", EditorStyles.miniLabel, GUILayout.Width(34));
                    if (GUILayout.Button("✕ Effect", GUILayout.Width(70))) removeEffect = i;
                }
                if (string.IsNullOrWhiteSpace(id.stringValue))
                    EditorGUILayout.HelpBox("This effect has no id — code can't call it.", MessageType.Warning);

                int removeEl = -1;
                for (int j = 0; j < elements.arraySize; j++)
                    if (DrawElement(elements.GetArrayElementAtIndex(j), j, id.stringValue, findings)) removeEl = j;
                if (removeEl >= 0) elements.DeleteArrayElementAtIndex(removeEl);

                if (GUILayout.Button("+ Add element")) elements.arraySize++;
            }
        }
        if (removeEffect >= 0) effects.DeleteArrayElementAtIndex(removeEffect);

        EditorGUILayout.Space();
        if (GUILayout.Button("+ Add effect")) effects.arraySize++;

        serializedObject.ApplyModifiedProperties();
    }

    // Draws one element; returns true if it requested removal.
    private static bool DrawElement(SerializedProperty el, int index, string effectId, List<CueBookLinter.Finding> findings)
    {
        var kind = el.FindPropertyRelative("kind");
        var kindVal = (CueElementKind)kind.enumValueIndex;
        bool remove = false;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"#{index}", EditorStyles.boldLabel, GUILayout.Width(28));
                EditorGUILayout.PropertyField(kind, GUIContent.none, GUILayout.Width(90));
                EditorGUILayout.PropertyField(el.FindPropertyRelative("label"), GUIContent.none);
                var isVariant = el.FindPropertyRelative("isVariant");
                isVariant.boolValue = GUILayout.Toggle(isVariant.boolValue,
                    new GUIContent("Variant", "Consecutive elements marked Variant = one group; each Play picks ONE at random."),
                    EditorStyles.miniButton, GUILayout.Width(56));
                if (GUILayout.Button("✕", GUILayout.Width(22))) remove = true;
            }

            switch (kindVal)
            {
                case CueElementKind.Particle:
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("particlePrefab"));
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("attachMode"));
                    DrawTransformOverrides(el);
                    break;
                case CueElementKind.Vfx:
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("vfxPrefab"));
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("attachMode"));
                    DrawTransformOverrides(el);
                    break;
                case CueElementKind.Sound:
                    break;   // pure audio — just the audio list below
                case CueElementKind.Manpu:
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("glyphSprite"), new GUIContent("Glyph Sprite"));
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("glyphColorA"), new GUIContent("Color A"));
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("glyphColorB"), new GUIContent("Color B"));
                    EditorGUILayout.HelpBox("Pulsed on the cue target's ManpuSlot (enemies). Transient; dropped if an " +
                                            "ability owns the slot. Timed by Start Mode / Delay below. (Held glyphs are the " +
 "ability ClosingSequence, — not a cue element.)", MessageType.None);
                    break;
            }

            DrawAudioList(el.FindPropertyRelative("audio"), kindVal);
            DrawCameraCue(el.FindPropertyRelative("camera"));

            // ── Timing ──
            EditorGUILayout.PropertyField(el.FindPropertyRelative("startMode"));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("startDelay"));

            var useDefault = el.FindPropertyRelative("useDefaultDuration");
            EditorGUILayout.PropertyField(useDefault, new GUIContent("Use Default Duration"));
            if (!useDefault.boolValue)
                EditorGUILayout.PropertyField(el.FindPropertyRelative("duration"));
            else if (kindVal == CueElementKind.Vfx)
                EditorGUILayout.HelpBox("A VFX graph has no natural end → held until stopped (a cut, or a gameplay Stop).",
                    MessageType.None);

            EditorGUILayout.PropertyField(el.FindPropertyRelative("timeMode"));

            // ── Cut (stop earlier elements) ──
            var canCut = el.FindPropertyRelative("canCut");
            EditorGUILayout.PropertyField(canCut, new GUIContent("Can Cut Earlier Elements"));
            if (canCut.boolValue)
            {
                EditorGUI.indentLevel++;
                if (index == 0)
                {
                    EditorGUILayout.HelpBox("Element #0 has nothing earlier to cut.", MessageType.Info);
                }
                else
                {
                    var cuts = el.FindPropertyRelative("cuts");
                    int removeCut = -1;
                    for (int c = 0; c < cuts.arraySize; c++)
                    {
                        var cut = cuts.GetArrayElementAtIndex(c);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var target = cut.FindPropertyRelative("targetIndex");
                            target.intValue = EarlierElementPopup(target.intValue, index);
                            EditorGUILayout.PropertyField(cut.FindPropertyRelative("afterSeconds"),
                                new GUIContent("after (s)"));
                            if (GUILayout.Button("✕", GUILayout.Width(22))) removeCut = c;
                        }
                    }
                    if (removeCut >= 0) cuts.DeleteArrayElementAtIndex(removeCut);
                    if (GUILayout.Button("+ Add cut")) cuts.arraySize++;
                }
                EditorGUI.indentLevel--;
            }

            // ── Author-time flags for this element (CueBookLinter) ──
            if (findings != null)
            {
                foreach (var f in findings)
                {
                    if (f.elementIndex != index || f.effectId != effectId) continue;
                    var type = f.severity == CueBookLinter.Severity.Warning ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox(f.message + "\nFix: " + f.fix, type);
                }
            }
        }
        return remove;
    }

    // Per-element audio: a small list of sounds, each loop/one-shot + (for a visual element) kill-with-visual.
    private static void DrawAudioList(SerializedProperty audio, CueElementKind kindVal)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Audio ({audio.arraySize})", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Sound", GUILayout.Width(70))) audio.arraySize++;
        }

        int removeAudio = -1;
        for (int a = 0; a < audio.arraySize; a++)
        {
            var au = audio.GetArrayElementAtIndex(a);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(au.FindPropertyRelative("sound"), GUIContent.none);
                    if (GUILayout.Button("✕", GUILayout.Width(22))) removeAudio = a;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    var loop = au.FindPropertyRelative("loop");
                    loop.boolValue = GUILayout.Toggle(loop.boolValue, "Loop", EditorStyles.miniButton, GUILayout.Width(60));
                    if (kindVal != CueElementKind.Sound)
                    {
                        var kill = au.FindPropertyRelative("killWithVisual");
                        kill.boolValue = GUILayout.Toggle(kill.boolValue, "Kill with visual", EditorStyles.miniButton, GUILayout.Width(120));
                    }
                    GUILayout.Label("delay", GUILayout.Width(40));
                    var delay = au.FindPropertyRelative("startDelay");
                    delay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(delay.floatValue, GUILayout.Width(50)));
                }
            }
        }
        if (removeAudio >= 0) audio.DeleteArrayElementAtIndex(removeAudio);
    }

    // Per-element transform overrides (offset / rotation / scale) — fix a prefab that spawns wrong.
    private static void DrawTransformOverrides(SerializedProperty el)
    {
        EditorGUILayout.PropertyField(el.FindPropertyRelative("localOffset"),   new GUIContent("Local Offset"));
        EditorGUILayout.PropertyField(el.FindPropertyRelative("localRotation"), new GUIContent("Local Rotation (°)"));
        EditorGUILayout.PropertyField(el.FindPropertyRelative("localScale"),    new GUIContent("Local Scale"));
        EditorGUILayout.PropertyField(el.FindPropertyRelative("drawOnTop"),
            new GUIContent("Draw On Top", "Ground telegraphs (cast circles, meteor decals): render over grass/props via the GroundVFX layer. Fog still veils it. Pool-safe."));
    }

    // Per-element camera feel — a + Camera block (mirrors + Sound). Channels toggle independently.
    private static void DrawCameraCue(SerializedProperty camera)
    {
        // A managed-reference/class field: present once allocated. We treat "has any channel on" as authored.
        var useShake = camera.FindPropertyRelative("useShake");
        var useDepth = camera.FindPropertyRelative("useDepth");
        bool authored = useShake.boolValue || useDepth.boolValue;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Camera", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (!authored)
            {
                if (GUILayout.Button("+ Camera", GUILayout.Width(80))) useShake.boolValue = true; // enabling a channel "adds" it
                return;
            }
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            { useShake.boolValue = useDepth.boolValue = false; return; }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // Shake (inline shape — driver stamps it on the shared source, then fires; R2-safe)
            useShake.boolValue = EditorGUILayout.ToggleLeft("Shake (Cinemachine Impulse)", useShake.boolValue);
            if (useShake.boolValue)
            {
                var shape = camera.FindPropertyRelative("shakeShape");
                EditorGUILayout.PropertyField(shape, new GUIContent("Shape"));
                if (shape.enumNames[shape.enumValueIndex] == "Custom")
                    EditorGUILayout.PropertyField(camera.FindPropertyRelative("shakeCustomShape"),
                        new GUIContent("Custom Shape", "Your own impulse curve (X 0..1 normalised time, Y amplitude)."));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("shakeAmplitude"), new GUIContent("Amplitude"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("shakeDuration"), new GUIContent("Duration (s)"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("shakeFrequency"), new GUIContent("Frequency"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("shakeDirection"), new GUIContent("Direction (0=default)"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("shakeRange"),
                    new GUIContent("Range (m, 0=uniform)", "0 = every camera shakes equally. > 0 = the shake fades out " +
                    "over this many metres from the cue position (distant cams feel less)."));
            }

            // Depth (post-process — also where a 'zoom' feel lives, via a Lens Distortion override in the profile)
            useDepth.boolValue = EditorGUILayout.ToggleLeft("Depth (post-process profile)", useDepth.boolValue);
            if (useDepth.boolValue)
            {
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("depthProfile"), new GUIContent("Profile"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("depthWeight"), new GUIContent("Weight"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("blendIn"), new GUIContent("Blend In (s)"));
                EditorGUILayout.PropertyField(camera.FindPropertyRelative("blendOut"), new GUIContent("Blend Out (s)"));
            }
        }
    }

    // Popup limited to the elements BEFORE this one (you can only cut something that started earlier).
    private static int EarlierElementPopup(int current, int selfIndex)
    {
        var options = new string[selfIndex];
        for (int i = 0; i < selfIndex; i++) options[i] = $"#{i}";
        int sel = Mathf.Clamp(current, 0, selfIndex - 1);
        return EditorGUILayout.Popup("Stop element", sel, options);
    }
}
