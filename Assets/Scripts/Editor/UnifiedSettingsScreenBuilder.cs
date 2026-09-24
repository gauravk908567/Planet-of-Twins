using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor tool that GENERATES the unified pause/settings screen from <see cref="SettingsCatalog"/>:
/// a Valorant/Overwatch-style single page — top bar (Resume | GAMEPLAY · VIDEO · AUDIO · CONTROLS |
/// Exit), one scrolling panel per tab with section sub-headers + rows, and the confirm dialog. Each
/// row gets a <see cref="SettingBinding"/> stamped with its catalog id; the runtime binds controls to
/// handlers by that id, so this generated structure can be re-run, restyled, or rearranged without
/// breaking apply.
///
/// Deliberately greybox (flat panels, readable TMP) — styling is a later theme pass, NOT baked here,
/// so re-running this structural builder never wipes visual polish. Re-runnable: it deletes an
/// existing "UnifiedSettings" under the target canvas and rebuilds. It does NOT save the scene —
/// review, then save Persistent yourself.
///
/// Control internals (dropdown template, slider fill/handle, toggle checkmark) come from Unity's own
/// TMP_DefaultControls / DefaultControls builders, the same deterministic path SettingsControlVisualsBuilder uses.
/// </summary>
public static class UnifiedSettingsScreenBuilder
{
    private const string RootName = "UnifiedSettings";
    private const float BarHeight = 88f;
    private const float LegendHeight = 60f;

    // ── Greybox theme (colours only; a real theme pass replaces these later) ──
    private static readonly Color CBackdrop = new Color(0.05f, 0.05f, 0.06f, 0.92f);
    private static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.12f, 1f);
    private static readonly Color CBar      = new Color(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Color CButton   = new Color(0.20f, 0.20f, 0.24f, 1f);
    private static readonly Color CControl  = new Color(0.16f, 0.16f, 0.19f, 1f);
    private static readonly Color CText     = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color CSubText  = new Color(0.65f, 0.68f, 0.78f, 1f);

    [MenuItem("Planet of Twins Tools/Settings/Build Unified Settings Screen")]
    public static void Build()
    {
        var canvas = FindTargetCanvas();
        if (canvas == null)
        {
            Debug.LogError("[UnifiedSettingsScreenBuilder] No Canvas found. Load Persistent additively (or select PauseMenuCanvas) and re-run.");
            return;
        }

        var existing = canvas.transform.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Always-active root holds the controller (so it boot-applies + snapshots even when closed).
        var root = NewUI(RootName, canvas.transform); Stretch(root);
        var screenCtl = root.gameObject.AddComponent<SettingsScreenController>();

        // Toggled visual root.
        var screenRoot = NewUI("ScreenRoot", root); Stretch(screenRoot);
        var backdrop = NewUI("Backdrop", screenRoot); Stretch(backdrop); AddImage(backdrop.gameObject, CBackdrop);

        // ── Top bar: Resume | (tabs) | Exit ──
        var bar = NewUI("TopBar", screenRoot); AnchorTop(bar, BarHeight); AddImage(bar.gameObject, CBar);
        var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.childControlWidth = true; barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = false; barLayout.childForceExpandHeight = true;
        barLayout.padding = new RectOffset(16, 16, 12, 12); barLayout.spacing = 8;
        barLayout.childAlignment = TextAnchor.MiddleLeft;
        var tabBar = bar.gameObject.AddComponent<SettingsTabBar>();

        var resumeBtn = MakeButton(bar, "ResumeButton", "‹ Resume", out _); SetWidth(resumeBtn.gameObject, 170);

        var tabsHolder = NewUI("Tabs", bar);
        var tabsLayout = tabsHolder.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.childControlWidth = true; tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = false; tabsLayout.childForceExpandHeight = true;
        tabsLayout.spacing = 6; tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsHolder.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Overwatch-style shoulder hint: LB glyph to the LEFT of the tab group (first child), RB to the RIGHT
        // (added after the tab loop). Gamepad-gated + populated at runtime by SettingsTabBar; hidden by default.
        var tabHintLeft = MakeTabHint(tabsHolder, "TabHintLeft");

        // Content area between the top bar and the bottom legend bar.
        var content = NewUI("Content", screenRoot); AnchorContent(content, BarHeight, LegendHeight);

        var tabList = new List<(SettingTab tab, Button btn, GameObject panel)>();
        var tabs = new[] { SettingTab.Gameplay, SettingTab.Video, SettingTab.Audio, SettingTab.Controls };
        ControlsRebindView controlsView = null;
        for (int i = 0; i < tabs.Length; i++)
        {
            var tabBtn = MakeButton(tabsHolder, "Tab_" + tabs[i], tabs[i].ToString().ToUpper(), out _);
            SetWidth(tabBtn.gameObject, 150);
            var panel = BuildPanel(tabs[i], content);
            if (tabs[i] == SettingTab.Controls) controlsView = panel.GetComponent<ControlsRebindView>();
            panel.SetActive(i == 0);   // runtime bar re-applies; keep editor preview clean
            tabList.Add((tabs[i], tabBtn, panel));
        }

        var tabHintRight = MakeTabHint(tabsHolder, "TabHintRight");   // RB glyph, right of the tab group

        var exitBtn = MakeButton(bar, "ExitButton", "Exit ›", out _); SetWidth(exitBtn.gameObject, 170);

        // Bottom legend bar: device-aware nav hints + always-on "who last moved" P1/P2 tag.
        BuildLegendBar(screenRoot);

        // Confirm dialog (hidden) — built last so it renders above the legend when shown.
        var dialog = BuildConfirmDialog(screenRoot, out var dlgComp);

        WireTabBar(tabBar, resumeBtn, exitBtn, tabList, tabHintLeft, tabHintRight);
        WireController(screenCtl, screenRoot.gameObject, tabBar, dlgComp, controlsView);

        // Bake an initial layout pass for every panel so the built UI has valid geometry before the
        // first runtime rebuild (and so it inspects/screenshots correctly in the editor). Nested
        // ContentSizeFitter/LayoutGroup content built while inactive otherwise stays at zero size
        // until something forces a rebuild. Restore each panel's editor-preview active state after.
        Canvas.ForceUpdateCanvases();
        foreach (var entry in tabList)
        {
            bool wasActive = entry.panel.activeSelf;
            entry.panel.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)entry.panel.transform);
            entry.panel.SetActive(wasActive);
        }

        screenRoot.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log($"[UnifiedSettingsScreenBuilder] Built '{RootName}' under '{canvas.name}'. " +
                  "Check any unresolved asset refs on SettingsScreenController, then SAVE the scene to persist.");
    }

    // ── Panels ────────────────────────────────────────────────────────
    private static GameObject BuildPanel(SettingTab tab, RectTransform contentParent)
    {
        // CONTROLS is a bespoke two-column P1|P2 keybinding view (F6 Phase 2), not catalog rows.
        if (tab == SettingTab.Controls) return BuildControlsPanel(contentParent);

        var panelGO = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        panelGO.name = "Panel_" + tab;
        var prt = (RectTransform)panelGO.transform;
        prt.SetParent(contentParent, false);
        Stretch(prt);

        var scroll = panelGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        if (scroll.horizontalScrollbar != null) { Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject); scroll.horizontalScrollbar = null; }

        var panelImg = panelGO.GetComponent<Image>(); if (panelImg != null) panelImg.color = CPanel;

        var vcontent = scroll.content;
        vcontent.anchorMin = new Vector2(0f, 1f);
        vcontent.anchorMax = new Vector2(1f, 1f);
        vcontent.pivot = new Vector2(0.5f, 1f);
        var vlg = vcontent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.spacing = 6; vlg.padding = new RectOffset(28, 28, 20, 20);
        var fitter = vcontent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        string lastSection = null;
        foreach (var def in SettingsCatalog.Definitions)
        {
            if (def.Tab != tab) continue;
            if (def.Section != lastSection) { AddSectionHeader(vcontent, def.Section); lastSection = def.Section; }
            BuildRow(vcontent, def);
        }
        return panelGO;
    }

    // ── CONTROLS tab — two-column P1 | P2 keybinding view (F6 Phase 2) ──
    private static GameObject BuildControlsPanel(RectTransform contentParent)
    {
        var panel = NewUI("Panel_Controls", contentParent); Stretch(panel);
        AddImage(panel.gameObject, CPanel);
        var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 20, 24); vlg.spacing = 16;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        AddSectionHeader(panel, "Keybinds");

        var view = panel.gameObject.AddComponent<ControlsRebindView>();

        var columns = NewUI("Columns", panel);
        var chlg = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        chlg.spacing = 48; chlg.childControlWidth = true; chlg.childControlHeight = true;
        chlg.childForceExpandWidth = true; chlg.childForceExpandHeight = true;
        chlg.childAlignment = TextAnchor.UpperLeft;
        columns.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        BuildControlsColumn(columns, "PLAYER 1", out var rows1, out var dev1, out var edit1, out var restore1, out var grp1);
        BuildControlsColumn(columns, "PLAYER 2", out var rows2, out var dev2, out var edit2, out var restore2, out var grp2);

        // "Changes are saved live" legend at the bottom of the panel — hidden until a player is editing (F6 Phase 3).
        var savedLegend = AddLabel(panel, "Changes are saved as you make them — press Back to finish.",
                                   18f, TextAlignmentOptions.Center, CSubText);
        savedLegend.fontStyle = FontStyles.Italic; AddLE(savedLegend.gameObject, 26f);
        savedLegend.gameObject.SetActive(false);

        WireControlsView(view, grp1, grp2, rows1, rows2, dev1, dev2, restore1, restore2, edit1, edit2, savedLegend);
        return panel.gameObject;
    }

    private static void BuildControlsColumn(Transform parent, string title,
        out Transform rowsContent, out TMP_Text deviceLabel, out Button editBtn, out Button restoreBtn, out CanvasGroup group)
    {
        var col = NewUI("Column_" + title.Replace(" ", ""), parent);
        group = col.gameObject.AddComponent<CanvasGroup>();
        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var header = AddLabel(col, title, 24f, TextAlignmentOptions.Left, CText);
        header.fontStyle = FontStyles.Bold; AddLE(header.gameObject, 34f);

        deviceLabel = AddLabel(col, "—", 18f, TextAlignmentOptions.Left, CSubText);
        AddLE(deviceLabel.gameObject, 24f);

        // Edit + Restore sit at the TOP of the column (under the player header), above the keybind list.
        var buttons = NewUI("Buttons", col);
        var bhlg = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        bhlg.spacing = 10; bhlg.childControlWidth = true; bhlg.childControlHeight = true;
        bhlg.childForceExpandWidth = true; bhlg.childForceExpandHeight = true;
        AddLE(buttons.gameObject, 46f);

        editBtn = MakeButton(buttons, "EditButton", "Edit", out _);   // F6 Phase 3 — enters this column's edit mode (wired at runtime)
        restoreBtn = MakeButton(buttons, "RestoreButton", "Restore Defaults", out _);

        var rows = NewUI("Rows", col);
        var rvlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        rvlg.spacing = 4; rvlg.childControlWidth = true; rvlg.childControlHeight = true;
        rvlg.childForceExpandWidth = true; rvlg.childForceExpandHeight = false;
        rows.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        rowsContent = rows;
    }

    private static void WireControlsView(ControlsRebindView view, CanvasGroup g1, CanvasGroup g2,
        Transform rows1, Transform rows2, TMP_Text dev1, TMP_Text dev2,
        Button restore1, Button restore2, Button edit1, Button edit2, TMP_Text savedLegend)
    {
        var so = new SerializedObject(view);
        so.FindProperty("_columnP1Group").objectReferenceValue = g1;
        so.FindProperty("_columnP2Group").objectReferenceValue = g2;
        so.FindProperty("_rowsP1").objectReferenceValue = rows1;
        so.FindProperty("_rowsP2").objectReferenceValue = rows2;
        so.FindProperty("_deviceP1").objectReferenceValue = dev1;
        so.FindProperty("_deviceP2").objectReferenceValue = dev2;
        so.FindProperty("_restoreP1").objectReferenceValue = restore1;
        so.FindProperty("_restoreP2").objectReferenceValue = restore2;
        so.FindProperty("_editP1").objectReferenceValue = edit1;
        so.FindProperty("_editP2").objectReferenceValue = edit2;
        so.FindProperty("_savedLiveLegend").objectReferenceValue = savedLegend;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSectionHeader(Transform parent, string section)
    {
        var rt = NewUI("Section_" + section, parent);
        AddLE(rt.gameObject, 40f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = section.ToUpper(); t.fontSize = 20f; t.color = CSubText;
        t.alignment = TextAlignmentOptions.BottomLeft; t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 6f; t.raycastTarget = false;
    }

    private static void BuildRow(Transform parent, SettingDefinition def)
    {
        var row = NewUI("Row_" + def.Id, parent);
        AddLE(row.gameObject, 46f);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;

        TMP_Dropdown dd = null; Slider sl = null; Toggle tg = null; Button bt = null; TMP_Text valueLabel = null;

        if (def.Type == SettingControlType.Button)
        {
            // Button rows: the button spans the row and carries the text (no separate left label).
            bt = MakeButton(row, "Btn_" + def.Id, def.Label, out _);
            var btnLE = bt.gameObject.GetComponent<LayoutElement>();
            if (btnLE == null) btnLE = bt.gameObject.AddComponent<LayoutElement>();
            btnLE.flexibleWidth = 1;
        }
        else
        {
            var label = AddLabel(row, def.Label, 24f, TextAlignmentOptions.Left, CText);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            switch (def.Type)
            {
                case SettingControlType.Dropdown:
                    dd = MakeDropdown(row); SetWidth(dd.gameObject, 340);
                    break;
                case SettingControlType.Slider:
                    sl = MakeSlider(row); SetWidth(sl.gameObject, 300);
                    valueLabel = AddLabel(row, "—", 20f, TextAlignmentOptions.Right, CSubText);
                    SetWidth(valueLabel.gameObject, 70);
                    break;
                case SettingControlType.Toggle:
                    tg = MakeToggle(row); SetWidth(tg.gameObject, 46);
                    break;
            }
        }

        var binding = row.gameObject.AddComponent<SettingBinding>();
        binding.EditorConfigure(def.Id, dd, sl, tg, bt, valueLabel);
        EditorUtility.SetDirty(binding);
    }

    // ── Legend bar (Phase 1: device-aware nav hints + who-last-moved tag) ──
    private static UILegendBar BuildLegendBar(RectTransform screenRoot)
    {
        var bar = NewUI("LegendBar", screenRoot); AnchorBottom(bar, LegendHeight); AddImage(bar.gameObject, CBar);
        var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(24, 24, 8, 8); hlg.spacing = 26;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var comp = bar.gameObject.AddComponent<UILegendBar>();

        var items = new List<(UILegendBar.LegendAction action, Image icon, TMP_Text key, TMP_Text label)>();
        var order = new[]
        {
            UILegendBar.LegendAction.Move, UILegendBar.LegendAction.SwitchTab,
            UILegendBar.LegendAction.Select, UILegendBar.LegendAction.Back,
        };
        foreach (var a in order) items.Add(BuildLegendItem(bar, a));

        // Flexible spacer pushes the tag to the far right of the bar.
        var spacer = NewUI("Spacer", bar); spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        var tag = AddLabel(bar, "Keyboard & Mouse", 20f, TextAlignmentOptions.Right, CSubText);
        tag.fontStyle = FontStyles.Bold; tag.enableWordWrapping = false;
        var tagLE = tag.gameObject.AddComponent<LayoutElement>(); tagLE.minWidth = 220f; tagLE.preferredWidth = 260f;

        WireLegend(comp, items, tag);
        return comp;
    }

    private static (UILegendBar.LegendAction, Image, TMP_Text, TMP_Text) BuildLegendItem(
        Transform parent, UILegendBar.LegendAction action)
    {
        var group = NewUI("Legend_" + action, parent);
        var hlg = group.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleLeft;

        // Optional glyph slot (hidden until a sprite resolves at runtime).
        var iconRT = NewUI("Icon", group);
        var icon = AddImage(iconRT.gameObject, Color.white); icon.raycastTarget = false; icon.preserveAspect = true;
        var iconLE = iconRT.gameObject.AddComponent<LayoutElement>();
        iconLE.minWidth = 34f; iconLE.preferredWidth = 34f; iconLE.minHeight = 34f; iconLE.preferredHeight = 34f;
        iconRT.gameObject.SetActive(false);

        // Keycap text (device-flipped at runtime) then caption.
        var key = AddLabel(group, "—", 20f, TextAlignmentOptions.Center, CText); key.fontStyle = FontStyles.Bold;
        key.enableWordWrapping = false; key.gameObject.AddComponent<LayoutElement>().minWidth = 40f;

        var label = AddLabel(group, action.ToString(), 18f, TextAlignmentOptions.Left, CSubText);
        label.enableWordWrapping = false;

        return (action, icon, key, label);
    }

    private static void WireLegend(UILegendBar comp,
        List<(UILegendBar.LegendAction action, Image icon, TMP_Text key, TMP_Text label)> items, TMP_Text tag)
    {
        var so = new SerializedObject(comp);
        var arr = so.FindProperty("_items");
        arr.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("action").enumValueIndex = (int)items[i].action;
            el.FindPropertyRelative("icon").objectReferenceValue = items[i].icon;
            el.FindPropertyRelative("keyText").objectReferenceValue = items[i].key;
            el.FindPropertyRelative("label").objectReferenceValue = items[i].label;
        }
        so.FindProperty("_deviceTag").objectReferenceValue = tag;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Confirm dialog ────────────────────────────────────────────────
    private static GameObject BuildConfirmDialog(RectTransform parent, out UIConfirmDialog comp)
    {
        var host = NewUI("ConfirmDialog", parent); Stretch(host);
        comp = host.gameObject.AddComponent<UIConfirmDialog>();

        var dRoot = NewUI("DialogRoot", host); Stretch(dRoot);
        var dim = NewUI("Dim", dRoot); Stretch(dim); AddImage(dim.gameObject, new Color(0f, 0f, 0f, 0.6f));

        var box = NewUI("Box", dRoot); Center(box, 760f, 340f); AddImage(box.gameObject, CPanel);
        var vlg = box.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(36, 36, 30, 30); vlg.spacing = 18;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        var title = AddLabel(box, "Title", 30f, TextAlignmentOptions.Center, CText); title.fontStyle = FontStyles.Bold; AddLE(title.gameObject, 44f);
        var msg = AddLabel(box, "Message", 22f, TextAlignmentOptions.Center, CSubText); AddLE(msg.gameObject, 96f);

        var btnRow = NewUI("Buttons", box);
        var brl = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        brl.spacing = 16; brl.childControlWidth = true; brl.childControlHeight = true;
        brl.childForceExpandWidth = true; brl.childForceExpandHeight = true; brl.childAlignment = TextAnchor.MiddleCenter;
        AddLE(btnRow.gameObject, 58f);

        var cancelBtn = MakeButton(btnRow, "CancelButton", "Cancel", out var cancelLbl);
        var confirmBtn = MakeButton(btnRow, "ConfirmButton", "Confirm", out var confirmLbl);

        WireDialog(comp, dRoot.gameObject, title, msg, confirmBtn, confirmLbl, cancelBtn, cancelLbl);
        dRoot.gameObject.SetActive(false);
        return host.gameObject;
    }

    // ── Wiring (SerializedObject — sets the private [SerializeField] slots) ──
    private static void WireTabBar(SettingsTabBar bar, Button resume, Button exit,
        List<(SettingTab tab, Button btn, GameObject panel)> tabs, TMP_Text tabHintLeft, TMP_Text tabHintRight)
    {
        var so = new SerializedObject(bar);
        so.FindProperty("_resumeButton").objectReferenceValue = resume;
        so.FindProperty("_exitButton").objectReferenceValue = exit;
        so.FindProperty("_tabHintLeft").objectReferenceValue = tabHintLeft;
        so.FindProperty("_tabHintRight").objectReferenceValue = tabHintRight;
        var arr = so.FindProperty("_tabs");
        arr.arraySize = tabs.Count;
        for (int i = 0; i < tabs.Count; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("tab").enumValueIndex = (int)tabs[i].tab;
            el.FindPropertyRelative("button").objectReferenceValue = tabs[i].btn;
            el.FindPropertyRelative("panel").objectReferenceValue = tabs[i].panel;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireController(SettingsScreenController ctl, GameObject screenRoot,
        SettingsTabBar bar, UIConfirmDialog dlg, ControlsRebindView controls)
    {
        var so = new SerializedObject(ctl);
        so.FindProperty("_screenRoot").objectReferenceValue = screenRoot;
        so.FindProperty("_tabBar").objectReferenceValue = bar;
        so.FindProperty("_confirmDialog").objectReferenceValue = dlg;
        so.FindProperty("_controls").objectReferenceValue = controls;   // F6 Phase 3 — edit-aware Back

        // Backend ASSET refs. The old GraphicsSettings/SettingsMenu controllers that used to hold these are
        // retired, so load the project assets directly — but never clobber a ref an earlier build already set.
        SetAssetRefIfEmpty(so, "_audioMixer",   "Assets/Audio/GameAudioMixer.mixer");
        SetAssetRefIfEmpty(so, "_urpAsset",     "Assets/Settings/PC_RPAsset.asset");
        SetAssetRefIfEmpty(so, "_rendererData", "Assets/Settings/PC_Renderer.asset");

        // The Fog row drives the CristianQiu VolumetricFog on the FogVolume global Volume (a scene object).
        var fogProp = so.FindProperty("_fogVolume");
        if (fogProp != null && fogProp.objectReferenceValue == null)
            fogProp.objectReferenceValue = FindFogVolume();

        var camProp = so.FindProperty("_mainCamera");
        if (camProp.objectReferenceValue == null && Camera.main != null)
            camProp.objectReferenceValue = Camera.main;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Load a project asset by path into an empty serialized object-ref (leaves an already-wired ref alone).
    private static void SetAssetRefIfEmpty(SerializedObject so, string field, string assetPath)
    {
        var p = so.FindProperty(field);
        if (p == null || p.objectReferenceValue != null) return;
        var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        if (asset != null) p.objectReferenceValue = asset;
        else Debug.LogWarning($"[UnifiedSettingsScreenBuilder] Backend asset not found for {field}: {assetPath}");
    }

    // The global Volume whose profile carries the CristianQiu VolumetricFog (the live fog the Fog row drives).
    private static Volume FindFogVolume()
    {
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var v in volumes)
            if (v != null && v.sharedProfile != null && v.sharedProfile.Has<VolumetricFogVolumeComponent>())
                return v;
        Debug.LogWarning("[UnifiedSettingsScreenBuilder] No Volume with a VolumetricFogVolumeComponent found — _fogVolume left null.");
        return null;
    }

    private static void WireDialog(UIConfirmDialog dlg, GameObject root, TMP_Text title, TMP_Text msg,
        Button confirm, TMP_Text confirmLbl, Button cancel, TMP_Text cancelLbl)
    {
        var so = new SerializedObject(dlg);
        so.FindProperty("_root").objectReferenceValue = root;
        so.FindProperty("_titleText").objectReferenceValue = title;
        so.FindProperty("_messageText").objectReferenceValue = msg;
        so.FindProperty("_confirmButton").objectReferenceValue = confirm;
        so.FindProperty("_confirmLabel").objectReferenceValue = confirmLbl;
        so.FindProperty("_cancelButton").objectReferenceValue = cancel;
        so.FindProperty("_cancelLabel").objectReferenceValue = cancelLbl;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Control factories (reuse Unity's deterministic builders) ──────
    private static TMP_Dropdown MakeDropdown(Transform parent)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); if (img != null) img.color = CControl;
        return go.GetComponent<TMP_Dropdown>();
    }

    private static Slider MakeSlider(Transform parent)
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        return go.GetComponent<Slider>();
    }

    private static Toggle MakeToggle(Transform parent)
    {
        var go = DefaultControls.CreateToggle(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var label = go.transform.Find("Label"); if (label != null) Object.DestroyImmediate(label.gameObject);
        return go.GetComponent<Toggle>();
    }

    // ── Primitive helpers ─────────────────────────────────────────────
    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Button MakeButton(Transform parent, string name, string text, out TextMeshProUGUI label)
    {
        var rt = NewUI(name, parent);
        var img = AddImage(rt.gameObject, CButton);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        label = AddLabel(rt, text, 22f, TextAlignmentOptions.Center, CText);
        Stretch((RectTransform)label.transform);
        return btn;
    }

    // A shoulder tab-switch glyph label (LB/RB). Sized to content, gamepad-gated (starts inactive — SettingsTabBar
    // shows + populates it at runtime only on a pad). Its glyph sprite is resolved live via InputGlyphText.
    private static TMP_Text MakeTabHint(Transform parent, string name)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = string.Empty; t.fontSize = 26f; t.color = CText; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.enableWordWrapping = false;
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.minWidth = 34f;
        rt.gameObject.SetActive(false);
        return t;
    }

    private static TextMeshProUGUI AddLabel(Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        var rt = NewUI("Label", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        return t;
    }

    private static Image AddImage(GameObject go, Color c) { var img = go.AddComponent<Image>(); img.color = c; return img; }
    private static void AddLE(GameObject go, float h) { var le = go.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; }

    private static void SetWidth(GameObject go, float w)
    {
        var le = go.GetComponent<LayoutElement>(); if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = w; le.preferredWidth = w;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void AnchorTop(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -height); rt.offsetMax = Vector2.zero;
    }

    // Fill the screen between the top bar (topInset) and the bottom legend bar (bottomInset).
    private static void AnchorContent(RectTransform rt, float topInset, float bottomInset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, bottomInset); rt.offsetMax = new Vector2(0f, -topInset);
    }

    private static void AnchorBottom(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, height);
    }

    private static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(w, h);
    }

    private static Canvas FindTargetCanvas()
    {
        if (Selection.activeGameObject != null)
        {
            var c = Selection.activeGameObject.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var rootGo in scene.GetRootGameObjects())
                foreach (var t in rootGo.GetComponentsInChildren<Transform>(true))
                    if (t.name == "PauseMenuCanvas")
                    {
                        var c = t.GetComponent<Canvas>();
                        if (c != null) return c;
                    }
        }
        return Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
    }
}
