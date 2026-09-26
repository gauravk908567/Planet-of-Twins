using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor tools for the "Report a Problem" screen (game.md §27.3, phase 3). Greybox, in the same palette as the
/// unified settings screen; a theme pass restyles both later.
///
///   • <b>Build Report Screen Prefab</b> creates <c>ReportProblemScreen.prefab</c> (<see cref="PoTPaths.Create.UIPrefabs"/>).
///     It never overwrites an existing prefab: a rebuilt prefab gets new object ids, which would drop the scene
///     instances' overrides (Menu Context). Delete it first to rebuild, then place it again.
///   • <b>Place Report Screen in Loaded Scenes</b> puts one instance next to each loaded scene's UnifiedSettings screen
///     (last sibling, so it draws on top), copies that screen's Menu Context, wires FrontEndFlowController's slot,
///     and adds the Main Menu's Report button + crash notice. Idempotent. It does NOT save: review, then save.
/// </summary>
public static class ReportProblemScreenBuilder
{
    private const string PrefabFileName = "ReportProblemScreen.prefab";
    private const float CardWidth = 1180f;

    private static readonly Color CBackdrop = new Color(0.03f, 0.03f, 0.04f, 0.88f);
    private static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.12f, 1f);
    private static readonly Color CButton   = new Color(0.20f, 0.20f, 0.24f, 1f);
    private static readonly Color CControl  = new Color(0.16f, 0.16f, 0.19f, 1f);
    private static readonly Color CText     = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color CSubText  = new Color(0.65f, 0.68f, 0.78f, 1f);
    private static readonly Color CHint     = new Color(0.95f, 0.72f, 0.45f, 1f);
    private static readonly Color CChipOn   = new Color(1f, 0.82f, 0.30f, 0.55f);

    // ── Prefab ────────────────────────────────────────────────────────
    [MenuItem("Planet of Twins Tools/Diagnostics/Build Report Screen Prefab")]
    public static void BuildPrefab()
    {
        var existing = PoTAssetLookup.PathsOf<GameObject>(PoTPaths.Named.ReportScreenPrefab);
        if (existing.Count > 0)
        {
            Debug.LogWarning($"[ReportProblemScreenBuilder] {string.Join(", ", existing)} already exists; nothing built. " +
                             "Delete it first to rebuild (then run Place Report Screen in Loaded Scenes again).");
            return;
        }

        var root = new GameObject(PoTPaths.Named.ReportScreenPrefab, typeof(RectTransform));
        try
        {
            Stretch((RectTransform)root.transform);
            var screen = root.AddComponent<ReportProblemScreen>();

            var screenRoot = NewUI("ScreenRoot", root.transform); Stretch(screenRoot);
            var backdrop = NewUI("Backdrop", screenRoot); Stretch(backdrop); AddImage(backdrop.gameObject, CBackdrop);

            var card = NewUI("Card", screenRoot);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, 0f);
            AddImage(card.gameObject, CPanel);
            var cardLayout = VerticalGroup(card.gameObject, 12f, new RectOffset(44, 44, 36, 36));
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = AddLabel(card, "Report a Problem", 34f, TextAlignmentOptions.Left, CText);
            title.fontStyle = FontStyles.Bold;
            AddLabel(card, "Tell us what went wrong. The details we need to find it are attached for you.",
                     20f, TextAlignmentOptions.Left, CSubText);

            // ── Form ──
            var form = NewUI("Form", card);
            VerticalGroup(form.gameObject, 10f, new RectOffset(0, 0, 10, 0));

            AddLabel(form, "What happened?", 22f, TextAlignmentOptions.Left, CText).fontStyle = FontStyles.Bold;
            var description = MakeInputField(form, "Description",
                "Describe what went wrong and what you were doing. On a controller, pick a type below instead.",
                multiLine: true, height: 140f);

            AddLabel(form, "Or pick the type of problem", 20f, TextAlignmentOptions.Left, CSubText);
            var chipsRow = NewUI("Chips", form);
            HorizontalGroup(chipsRow.gameObject, 10f);
            SetHeight(chipsRow.gameObject, 50f);
            var group = chipsRow.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = true;
            var chips = new List<(string category, Toggle toggle)>();
            foreach (var category in ReportProblemScreen.Categories)
                chips.Add((category, MakeChip(chipsRow, category, group)));

            AddLabel(form, "Email for a reply (optional)", 20f, TextAlignmentOptions.Left, CSubText);
            var contact = MakeInputField(form, "Contact", "you@example.com", multiLine: false, height: 50f);
            contact.contentType = TMP_InputField.ContentType.EmailAddress;

            var included = AddLabel(form, "What's included", 18f, TextAlignmentOptions.TopLeft, CSubText);
            included.gameObject.name = "Included";
            included.margin = new Vector4(0f, 8f, 0f, 0f);

            var privacy = MakeButton(form, "PrivacyButton", "Privacy notice", 18f, out _);
            SetWidth(privacy.gameObject, 220f); SetHeight(privacy.gameObject, 40f);
            privacy.gameObject.SetActive(false);   // shown at runtime once a privacy URL is set

            var status = AddLabel(form, "", 18f, TextAlignmentOptions.Left, CHint);
            status.gameObject.name = "Status";
            SetHeight(status.gameObject, 26f);

            var formButtons = NewUI("Buttons", form);
            HorizontalGroup(formButtons.gameObject, 16f);
            SetHeight(formButtons.gameObject, 58f);
            var cancel = MakeButton(formButtons, "CancelButton", "Cancel", 22f, out _);
            var send = MakeButton(formButtons, "SendButton", "Send Report", 22f, out _);

            // ── Result ──
            var result = NewUI("Result", card);
            VerticalGroup(result.gameObject, 16f, new RectOffset(0, 0, 16, 0));
            var resultTitle = AddLabel(result, "Report saved", 30f, TextAlignmentOptions.Left, CText);
            resultTitle.fontStyle = FontStyles.Bold;
            var resultText = AddLabel(result, "", 22f, TextAlignmentOptions.TopLeft, CSubText);
            var resultButtons = NewUI("Buttons", result);
            HorizontalGroup(resultButtons.gameObject, 16f);
            SetHeight(resultButtons.gameObject, 58f);
            var openFolder = MakeButton(resultButtons, "OpenFolderButton", "Open Folder", 22f, out _);
            var done = MakeButton(resultButtons, "DoneButton", "Done", 22f, out _);
            result.gameObject.SetActive(false);

            var so = new SerializedObject(screen);
            so.FindProperty("_screenRoot").objectReferenceValue = screenRoot.gameObject;
            so.FindProperty("_formRoot").objectReferenceValue = form.gameObject;
            so.FindProperty("_resultRoot").objectReferenceValue = result.gameObject;
            so.FindProperty("_description").objectReferenceValue = description;
            so.FindProperty("_contact").objectReferenceValue = contact;
            so.FindProperty("_includedText").objectReferenceValue = included;
            so.FindProperty("_privacyButton").objectReferenceValue = privacy;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.FindProperty("_cancelButton").objectReferenceValue = cancel;
            so.FindProperty("_sendButton").objectReferenceValue = send;
            so.FindProperty("_resultTitle").objectReferenceValue = resultTitle;
            so.FindProperty("_resultText").objectReferenceValue = resultText;
            so.FindProperty("_openFolderButton").objectReferenceValue = openFolder;
            so.FindProperty("_doneButton").objectReferenceValue = done;
            var chipList = so.FindProperty("_chips");
            chipList.arraySize = chips.Count;
            for (int i = 0; i < chips.Count; i++)
            {
                var el = chipList.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("category").stringValue = chips[i].category;
                el.FindPropertyRelative("toggle").objectReferenceValue = chips[i].toggle;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            screenRoot.gameObject.SetActive(false);

            PoTAssetLookup.EnsureFolder(PoTPaths.Create.UIPrefabs);
            string path = $"{PoTPaths.Create.UIPrefabs}/{PrefabFileName}";
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            if (ok) Debug.Log($"[ReportProblemScreenBuilder] Built {path}. Next: load FrontEnd or Persistent and run " +
                              "Place Report Screen in Loaded Scenes.");
            else Debug.LogError($"[ReportProblemScreenBuilder] Could not save {path}.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ── Scene placement ───────────────────────────────────────────────
    [MenuItem("Planet of Twins Tools/Diagnostics/Place Report Screen in Loaded Scenes")]
    public static void PlaceInLoadedScenes()
    {
        var prefab = PoTAssetLookup.FindUnique<GameObject>(PoTPaths.Named.ReportScreenPrefab);
        if (prefab == null)
        {
            Debug.LogError("[ReportProblemScreenBuilder] No ReportProblemScreen prefab. Run Build Report Screen Prefab first.");
            return;
        }

        int placed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            if (PlaceInScene(scene, prefab)) placed++;
        }
        Debug.Log($"[ReportProblemScreenBuilder] Checked the loaded scenes; changed {placed}. Save the changed scenes " +
                  "to keep the result.");
    }

    private static bool PlaceInScene(Scene scene, GameObject prefab)
    {
        SettingsScreenController settings = null;
        ReportProblemScreen screen = null;
        FrontEndFlowController flow = null;
        MainMenuController menu = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            settings ??= go.GetComponentInChildren<SettingsScreenController>(true);
            screen ??= go.GetComponentInChildren<ReportProblemScreen>(true);
            flow ??= go.GetComponentInChildren<FrontEndFlowController>(true);
            menu ??= go.GetComponentInChildren<MainMenuController>(true);
        }
        if (settings == null) return false;   // not a scene with the settings screen (FrontEnd / Persistent)

        bool changed = false;
        bool menuContext = new SerializedObject(settings).FindProperty("_menuContext").boolValue;

        if (screen == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(settings.transform.parent, false);
            instance.transform.SetAsLastSibling();
            Stretch((RectTransform)instance.transform);
            screen = instance.GetComponent<ReportProblemScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_menuContext").boolValue = menuContext;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ReportProblemScreenBuilder] {scene.name}: placed the report screen (Menu Context " +
                      $"{(menuContext ? "ON" : "OFF")}).", instance);
            changed = true;
        }

        if (flow != null)
        {
            var so = new SerializedObject(flow);
            var slot = so.FindProperty("report");
            if (slot.objectReferenceValue == null)
            {
                slot.objectReferenceValue = screen;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[ReportProblemScreenBuilder] {scene.name}: wired FrontEndFlowController ▸ Report.", flow);
                changed = true;
            }
        }

        if (menu != null && AddMainMenuEntry(menu)) changed = true;

        if (changed) EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }

    // Report a Problem goes between Options and Exit (Exit moves down one step), with the crash notice to its right.
    private static bool AddMainMenuEntry(MainMenuController menu)
    {
        var so = new SerializedObject(menu);
        var reportProp = so.FindProperty("reportButton");
        if (reportProp.objectReferenceValue != null) return false;

        var options = so.FindProperty("optionsButton").objectReferenceValue as Button;
        var exit = so.FindProperty("exitButton").objectReferenceValue as Button;
        if (options == null || exit == null)
        {
            Debug.LogError("[ReportProblemScreenBuilder] MainMenuController has no Options/Exit button to copy; " +
                           "add the Report button by hand.", menu);
            return false;
        }

        var optionsRT = (RectTransform)options.transform;
        var exitRT = (RectTransform)exit.transform;
        float step = exitRT.anchoredPosition.y - optionsRT.anchoredPosition.y;

        var report = CloneButton(options, "ReportButton", "Report a Problem");
        var reportRT = (RectTransform)report.transform;
        reportRT.SetSiblingIndex(exitRT.GetSiblingIndex());   // before Exit: the pad cycle follows sibling order
        reportRT.anchoredPosition = exitRT.anchoredPosition;
        exitRT.anchoredPosition += new Vector2(0f, step);

        // Crash notice: one line + Not Now, to the right of the Report button. Hidden until a crash is pending.
        var notice = NewUI("CrashNotice", reportRT.parent);
        notice.SetSiblingIndex(reportRT.GetSiblingIndex() + 1);
        notice.anchorMin = notice.anchorMax = new Vector2(0.5f, 0.5f);
        notice.pivot = new Vector2(0f, 0.5f);
        notice.anchoredPosition = new Vector2(reportRT.sizeDelta.x * 0.5f + 32f, reportRT.anchoredPosition.y);
        notice.sizeDelta = new Vector2(660f, reportRT.sizeDelta.y);
        var hlg = HorizontalGroup(notice.gameObject, 16f);
        hlg.childForceExpandWidth = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(0, 0, 12, 12);
        var line = AddLabel(notice, "Something went wrong last time? Send us a report — it takes 10 seconds.",
                            20f, TextAlignmentOptions.Left, CHint);
        line.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var notNow = CloneButton(options, "NotNowButton", "Not Now");
        notNow.transform.SetParent(notice, false);
        SetWidth(notNow.gameObject, 150f);
        notice.gameObject.SetActive(false);

        reportProp.objectReferenceValue = report;
        so.FindProperty("crashNotice").objectReferenceValue = notice.gameObject;
        so.FindProperty("dismissCrashButton").objectReferenceValue = notNow;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[ReportProblemScreenBuilder] Main Menu: added Report a Problem + the crash notice.", menu);
        return true;
    }

    // A copy of an existing menu button (same art and font) with its own label and no inspector-wired click handlers.
    private static Button CloneButton(Button source, string name, string label)
    {
        var go = Object.Instantiate(source.gameObject, source.transform.parent);
        go.name = name;
        var button = go.GetComponent<Button>();
        var so = new SerializedObject(button);
        so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        var text = go.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        return button;
    }

    // ── Control factories ─────────────────────────────────────────────
    private static TMP_InputField MakeInputField(Transform parent, string name, string placeholder, bool multiLine, float height)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); if (image != null) image.color = CControl;
        var field = go.GetComponent<TMP_InputField>();
        field.lineType = multiLine ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        field.pointSize = 22f;
        var align = multiLine ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
        var wrap = multiLine ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        if (field.textComponent != null)
        {
            field.textComponent.color = CText;
            field.textComponent.alignment = align;
            field.textComponent.textWrappingMode = wrap;
        }
        if (field.placeholder is TMP_Text ph)
        {
            ph.text = placeholder;
            ph.color = new Color(CSubText.r, CSubText.g, CSubText.b, 0.6f);
            ph.fontStyle = FontStyles.Italic;
            ph.alignment = align;
            ph.textWrappingMode = wrap;
        }
        var textArea = field.textViewport;
        if (textArea != null) { textArea.offsetMin = new Vector2(14f, 10f); textArea.offsetMax = new Vector2(-14f, -10f); }
        SetHeight(go, height);
        return field;
    }

    private static Toggle MakeChip(Transform parent, string label, ToggleGroup group)
    {
        var rt = NewUI("Chip_" + label, parent);
        var bg = AddImage(rt.gameObject, CButton);
        var on = NewUI("On", rt); Stretch(on);
        var onImage = AddImage(on.gameObject, CChipOn); onImage.raycastTarget = false;
        var text = AddLabel(rt, label, 20f, TextAlignmentOptions.Center, CText);
        Stretch((RectTransform)text.transform);
        var toggle = rt.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = onImage;
        toggle.group = group;
        toggle.isOn = false;
        rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return toggle;
    }

    private static Button MakeButton(Transform parent, string name, string text, float size, out TextMeshProUGUI label)
    {
        var rt = NewUI(name, parent);
        var img = AddImage(rt.gameObject, CButton);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        label = AddLabel(rt, text, size, TextAlignmentOptions.Center, CText);
        Stretch((RectTransform)label.transform);
        return btn;
    }

    // ── Primitive helpers ─────────────────────────────────────────────
    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static TextMeshProUGUI AddLabel(Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        var rt = NewUI("Label", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private static Image AddImage(GameObject go, Color c) { var img = go.AddComponent<Image>(); img.color = c; return img; }

    private static VerticalLayoutGroup VerticalGroup(GameObject go, float spacing, RectOffset padding)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing; v.padding = padding;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        return v;
    }

    private static HorizontalLayoutGroup HorizontalGroup(GameObject go, float spacing)
    {
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = true; h.childForceExpandHeight = true;
        return h;
    }

    private static void SetHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>(); if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = h; le.preferredHeight = h;
    }

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
}
