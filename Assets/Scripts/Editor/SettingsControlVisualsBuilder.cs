using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// One-shot editor tool that builds the MISSING visual children on the pause Settings
/// controls, IN PLACE, without recreating the control GameObjects.
///
/// Why this exists: the Settings controls were authored as bare stubs — each dropdown /
/// toggle / slider has only its Image + control component and NO template / checkmark /
/// fill children. So they render as blank coloured boxes and a TMP_Dropdown throws
/// "template not assigned" the moment it is clicked. Backend, wiring, layout, rows and the
/// RectMask2D viewport are all already correct — only the control internals are missing.
///
/// Rebuilding those internals by hand is hundreds of nested nodes per control; Unity's own
/// DefaultControls / TMP_DefaultControls builders make the standard hierarchies
/// deterministically. This tool builds a throwaway standard control, transplants its visual
/// children onto the EXISTING control, and re-points the control's own serialized slots at
/// the transplanted children. The control GameObject keeps its identity, so every serialized
/// reference on SettingsMenuController / GraphicsSettingsController stays intact.
///
/// Idempotent: a control that already has its template / checkmark / fill is skipped, so the
/// tool is safe to re-run. Scoped strictly to descendants of a GameObject named "SettingsPanel".
/// </summary>
public static class SettingsControlVisualsBuilder
{
    [MenuItem("Planet of Twins Tools/Settings/Build Control Visuals")]
    public static void Build()
    {
        var panels = FindByNameIncludingInactive("SettingsPanel");
        if (panels.Count == 0)
        {
            Debug.LogError("[SettingsControlVisualsBuilder] No 'SettingsPanel' in any loaded scene. Load Persistent additively first.");
            return;
        }

        int dropdowns = 0, toggles = 0, sliders = 0;
        Scene dirty = default;

        foreach (var panel in panels)
        {
            dirty = panel.scene;

            foreach (var dd in panel.GetComponentsInChildren<TMP_Dropdown>(true))
                if (dd.template == null) { BuildDropdown(dd); dropdowns++; }

            foreach (var tg in panel.GetComponentsInChildren<Toggle>(true))
                if (tg.graphic == null) { BuildToggleCheckmark(tg); toggles++; }

            foreach (var sl in panel.GetComponentsInChildren<Slider>(true))
                if (sl.fillRect == null || sl.handleRect == null) { BuildSlider(sl); sliders++; }
        }

        if (dirty.IsValid())
            EditorSceneManager.MarkSceneDirty(dirty);

        Debug.Log($"[SettingsControlVisualsBuilder] Built control visuals — dropdowns:{dropdowns} toggles:{toggles} sliders:{sliders}. Save the scene to persist.");
    }

    // TMP_Dropdown: transplant the standard Label + Arrow + (inactive) Template onto the real
    // dropdown and wire captionText / itemText / template. The real dropdown keeps its own Image
    // as the closed-state background (targetGraphic), unchanged.
    private static void BuildDropdown(TMP_Dropdown dd)
    {
        var temp = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        var tmpDd = temp.GetComponent<TMP_Dropdown>();

        var caption  = tmpDd.captionText;   // TMP_Text on "Label"
        var itemText = tmpDd.itemText;      // TMP_Text on Template/Viewport/Content/Item/Item Label
        var template = tmpDd.template;      // RectTransform on the inactive "Template"

        // Move Label, Arrow, Template (keep local layout — their anchors are relative).
        foreach (var child in Children(temp.transform))
            child.SetParent(dd.transform, worldPositionStays: false);

        dd.captionText = caption;
        dd.itemText    = itemText;
        dd.template    = template;

        Object.DestroyImmediate(temp);
        EditorUtility.SetDirty(dd);
    }

    // Toggle: the toggle GO already owns the box Image (targetGraphic). Add a centred inner
    // Checkmark image and set it as the toggle's graphic (shown when on, hidden when off).
    private static void BuildToggleCheckmark(Toggle tg)
    {
        var check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)check.transform;
        rt.SetParent(tg.transform, worldPositionStays: false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(28f, 28f);
        rt.anchoredPosition = Vector2.zero;

        var img = check.GetComponent<Image>();
        img.color = new Color(0.30f, 0.85f, 0.45f, 1f); // greybox "on" fill
        img.raycastTarget = false;

        var box = tg.GetComponent<Image>();
        if (box != null) tg.targetGraphic = box;
        tg.graphic = img;

        EditorUtility.SetDirty(tg);
    }

    // Slider: transplant the standard Background + Fill Area/Fill + Handle Slide Area/Handle
    // and wire fillRect / handleRect / targetGraphic. Disable the stub's root Image so the
    // transplanted track shows cleanly (the moved Background is the track).
    private static void BuildSlider(Slider sl)
    {
        var temp = DefaultControls.CreateSlider(new DefaultControls.Resources());
        var tmpSl = temp.GetComponent<Slider>();

        var fill   = tmpSl.fillRect;
        var handle = tmpSl.handleRect;

        foreach (var child in Children(temp.transform))
            child.SetParent(sl.transform, worldPositionStays: false);

        sl.fillRect   = fill;
        sl.handleRect = handle;
        sl.targetGraphic = handle != null ? handle.GetComponent<Image>() : null;
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = 0f;
        sl.maxValue = 1f;

        var rootImg = sl.GetComponent<Image>();
        if (rootImg != null) rootImg.enabled = false;

        Object.DestroyImmediate(temp);
        EditorUtility.SetDirty(sl);
    }

    private static List<Transform> Children(Transform t)
    {
        var list = new List<Transform>(t.childCount);
        foreach (Transform c in t) list.Add(c);
        return list;
    }

    private static List<GameObject> FindByNameIncludingInactive(string name)
    {
        var result = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) result.Add(t.gameObject);
        }
        return result;
    }
}
