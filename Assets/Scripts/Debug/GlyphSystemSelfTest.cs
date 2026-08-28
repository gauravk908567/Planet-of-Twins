using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DEV proof for the button-glyph system (item 5, P2a). Drop on ANY GameObject in a sandbox scene (SampleScene)
/// and press Play: it builds a clean row (manual layout — no auto-layout mash) showing the resolved GLYPH sprite
/// for a set of actions, plus a live "device:" header, and re-resolves when you switch keyboard ↔ pad. Verifies
/// the whole pipeline — binding → control path → device kind → InputGlyphMap → sprite → render — with zero scene
/// wiring. Deliberately unstyled: it proves the engine, NOT the final look. Throwaway (delete after P2b wiring).
///
/// <para>Expected: button PICTURES for each action; header reads "Keyboard", flips to "Gamepad" (and glyphs to
/// Xbox A/B/X/Y) when you press a pad button, back on a key. An action with NO glyph shows empty + [its text].</para>
/// </summary>
public class GlyphSystemSelfTest : MonoBehaviour
{
    [Tooltip("Actions to preview (must exist in PlanetOfTwins.inputactions).")]
    [SerializeField]
    private string[] _actions = { "Interact", "Attack", "Ability", "Teleport", "Empower", "Pause", "SkillTree" };

    private const float Step = 150f;

    private readonly List<(string action, Image icon, TMP_Text label)> _rows = new();
    private TMP_Text _header;
    private TMP_Text _inlineLabel;   // P2b proof: <sprite …> inline in a real TMP string
    private IInputProvider _input;
    private InputDeviceKind? _lastLoggedKind;

    private void Start()
    {
        _input = PlayerInputRouter.SharedInput;
        if (_input == null)
        {
            Debug.LogError("[GlyphSelfTest] No shared IInputProvider — is Persistent loaded? (PersistentSceneAutoLoader)", this);
            enabled = false;
            return;
        }
        BuildUI();
        RefreshAll();
    }

    private void OnEnable()  => LastUsedDeviceTracker.OnLastUsedChanged += OnSwitch;
    private void OnDisable() => LastUsedDeviceTracker.OnLastUsedChanged -= OnSwitch;
    private void OnSwitch(InputDeviceKind kind) => RefreshAll();

    private void BuildUI()
    {
        var canvasGo = new GameObject("GlyphSelfTestCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        // ScreenSpaceCamera (not Overlay) so the MCP camera-based screenshot captures it; Overlay is excluded there.
        var cam = Camera.main;
        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = 5000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Background panel, bottom-centre.
        var panel = NewRect("Panel", canvasGo.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(_actions.Length * Step + 40f, 210f);
        panel.anchoredPosition = new Vector2(0, 30);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);

        // Header.
        _header = NewText("Header", panel, 22);
        var hrt = _header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(-20, 34);
        hrt.anchoredPosition = new Vector2(0, -8);
        _header.alignment = TextAlignmentOptions.Center;

        // Cells (manual X positions — deterministic, no layout-group mash).
        float startX = -(_actions.Length - 1) * Step / 2f;
        for (int i = 0; i < _actions.Length; i++)
        {
            float x = startX + i * Step;

            var iconGo = new GameObject(_actions[i] + "_Icon");
            iconGo.transform.SetParent(panel, false);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(96, 96);
            irt.anchoredPosition = new Vector2(x, 18);

            var label = NewText(_actions[i] + "_Label", panel, 16);
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.sizeDelta = new Vector2(Step - 8f, 44);
            lrt.anchoredPosition = new Vector2(x, -46);
            label.alignment = TextAlignmentOptions.Top;

            _rows.Add((_actions[i], icon, label));
        }

        // P2b: inline-glyph proof strip above the icon row — the SAME glyphs drawn inside a TMP sentence via
        // <sprite …> tags (InputGlyphText.Format), which is how the real surfaces (rescue/QTE/HUD) will show them.
        var strip = NewRect("InlineStrip", canvasGo.transform);
        strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 0f);
        strip.pivot = new Vector2(0.5f, 0f);
        strip.sizeDelta = new Vector2(_actions.Length * Step + 40f, 64f);
        strip.anchoredPosition = new Vector2(0, 250);
        var stripBg = strip.gameObject.AddComponent<Image>();
        stripBg.color = new Color(0f, 0f, 0f, 0.65f);
        _inlineLabel = NewText("InlineLabel", strip, 28);
        var ilrt = _inlineLabel.rectTransform;
        ilrt.anchorMin = Vector2.zero;
        ilrt.anchorMax = Vector2.one;
        ilrt.offsetMin = new Vector2(12, 6);
        ilrt.offsetMax = new Vector2(-12, -6);
        _inlineLabel.alignment = TextAlignmentOptions.Center;
    }

    private void RefreshAll()
    {
        if (_input == null) return;

        var kind = InputGlyphResolver.ResolveKind(_input);
        if (_header != null)
            _header.text = $"Glyph self-test — device: {kind}   (press a key / a pad button to switch)";

        if (_inlineLabel != null)
        {
            // Build "Interact {Interact}  Attack {Attack}  …" then Apply → inline sprites (or [key] fallback).
            var sb = new System.Text.StringBuilder("Inline: ");
            foreach (var (action, _, _) in _rows) sb.Append(action).Append(" {").Append(action).Append("}   ");
            InputGlyphText.Apply(_inlineLabel, sb.ToString(), _input);
        }

        bool logThisKind = _lastLoggedKind != kind;
        _lastLoggedKind = kind;
        var log = logThisKind ? new System.Text.StringBuilder($"[GlyphSelfTest] device={kind} → ") : null;

        foreach (var (action, icon, label) in _rows)
        {
            var sprite = InputGlyphResolver.ResolveSprite(_input, action, out var fallback);
            if (sprite != null)
            {
                icon.enabled = true;
                icon.sprite = sprite;
                label.text = action;
            }
            else
            {
                icon.enabled = false;
                label.text = $"{action}\n[{fallback}]";
            }

            if (log != null)
            {
                _input.TryGetBindingControlPath(action, kind, out var path, out _);
                log.Append($"{action}:'{path}'→{(sprite != null ? "GLYPH" : "text[" + fallback + "]")}  ");
            }
        }

        if (log != null) Debug.Log(log.ToString());
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static TMP_Text NewText(string name, Transform parent, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = Color.white;
        t.richText = true;   // parse <sprite …> tags (P2b inline proof)
        return t;
    }
}
