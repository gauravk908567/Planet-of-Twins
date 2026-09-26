using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Runtime brain of the unified pause/settings screen. Lives on an ALWAYS-ACTIVE Persistent object so
/// it can apply saved settings at boot (before the screen is ever opened) and snapshot/restore mutated
/// assets in the editor. Owns the setting handlers and the id→control registry: on Open() it sweeps
/// <see cref="SettingBinding"/> children, fills each control from its handler, and routes changes back.
/// No serialized field points at a specific control — binding is by id — so the screen can be rebuilt
/// or restyled by the builder without breaking apply.
/// </summary>
public sealed class SettingsScreenController : MonoBehaviour
{
    public static SettingsScreenController Instance { get; private set; }

    [Header("Screen")]
    [Tooltip("The toggled visual root (tab bar + panels). Inactive by default; activated on Open().")]
    [SerializeField] private GameObject _screenRoot;
    [SerializeField] private SettingsTabBar _tabBar;
    [SerializeField] private UIConfirmDialog _confirmDialog;
    [Tooltip("F6 Phase 3 — the CONTROLS tab's rebind view. Lets Back route per-player while a player is editing.")]
    [SerializeField] private ControlsRebindView _controls;
    [Tooltip("ON only for the copy on the MAIN MENU (FrontEnd): Resume reads \"Back\" and just closes the screen (no " +
             "pause flow there), Exit is hidden. OFF for the in-game (Persistent) copy.")]
    [SerializeField] private bool _menuContext;

    [Header("Backend assets (ASSET refs — R1, not control refs)")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private UniversalRenderPipelineAsset _urpAsset;
    [SerializeField] private ScriptableRendererData _rendererData;
    [SerializeField] private Material _fogMaterial;   // legacy PoT-fog slot (retained, unused)
    [Tooltip("The global FogVolume (Persistent) whose profile carries the CristianQiu Volumetric Fog the Fog row drives.")]
    [SerializeField] private Volume _fogVolume;
    [SerializeField] private Camera _mainCamera;

    private readonly List<ISettingHandler> _handlers = new List<ISettingHandler>();
    private AudioSettingsHandler _audio;
    private GraphicsSettingsHandler _graphics;

    private readonly Dictionary<string, SettingBinding> _bindings = new Dictionary<string, SettingBinding>();
    private bool _pushing;   // suppress control change events while we write values in

    // Deferred (resolution / window) revert state.
    private string _deferredId;
    private int _deferredPrevIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // The main-menu copy (FrontEnd) and the in-game copy (Persistent) never coexist in a real boot — the
            // bootstrapper unloads FrontEnd before Persistent loads. If they ever do (both opened by hand in the
            // editor), the in-game copy wins: never let the menu copy make the pause screen destroy itself.
            if (!(Instance._menuContext && !_menuContext)) { Destroy(gameObject); return; }
        }
        Instance = this;

        var config = new SettingsBackendConfig
        {
            AudioMixer = _audioMixer,
            UrpAsset = _urpAsset,
            RendererData = _rendererData,
            FogMaterial = _fogMaterial,
            FogProfile = _fogVolume != null ? _fogVolume.sharedProfile : null,
            MainCamera = _mainCamera,
        };

        _audio = new AudioSettingsHandler();
        _graphics = new GraphicsSettingsHandler();
        _handlers.Add(_audio);
        _handlers.Add(_graphics);
        _handlers.Add(new DisplaySettingsHandler());
        _handlers.Add(new ControlsSettingsHandler());
        foreach (var h in _handlers) h.Initialize(config);

        _graphics.SnapshotAssets();                    // editor-only body; no-op in builds
        if (_screenRoot != null) _screenRoot.SetActive(false);
    }

    private void Start()
    {
        // Apply saved settings at boot so they take effect before the screen is ever opened
        // (replaces the old always-on GraphicsSettings object + volume load path).
        _graphics.ApplyAll();
        _audio.ApplySaved();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _graphics?.RestoreAssets();                    // editor-only body
    }

    public bool IsOpen => _screenRoot != null && _screenRoot.activeSelf;

    /// <summary>Raised after the screen closes (the main menu uses it to take focus back).</summary>
    public event System.Action Closed;

    // ── Open / close ──────────────────────────────────────────────────
    public void Open()
    {
        if (_screenRoot == null) return;
        _screenRoot.SetActive(true);
        // Freeze device↔slot reassignment while the screen is open: a pad disconnecting mid-rebind must NOT reshuffle
        // which twin a device drives (or revert to solo, swapping P1 to keyboard). The CONTROLS view instead greys
        // the actual disconnected column. Reconciled on Close.
        CouchDeviceManager.Instance?.SetAutoAssignSuspended(true);
        BuildRegistry();
        RefreshOptionsAndValues();
        _tabBar?.SetMenuMode(_menuContext);   // before Initialise → bar navigation skips the hidden Exit
        _tabBar?.Initialise(this);
        var first = _tabBar != null ? _tabBar.FirstTabButton : null;
        if (first != null) UINavFocus.Focus(first);
    }

    public void Close()
    {
        _confirmDialog?.HideImmediate();
        if (_screenRoot != null) _screenRoot.SetActive(false);
        // Resume auto device assignment — this reconciles any connect/disconnect that happened while open.
        CouchDeviceManager.Instance?.SetAutoAssignSuspended(false);
        Closed?.Invoke();
    }

    // ── Back / Cancel (Esc or pad-B, routed by PauseMenuController's arbiter so ESC stays centralised) ──
    /// <summary>The Back button's state machine entry point (F6 Phase 3 — locked spec 2026-09-19). Per-player and
    /// edit-aware:
    ///   • if ANY player is in Controls edit mode → route Back to the CONTROLS view, which exits only the player(s)
    ///     who pressed Back THIS frame (his column returns to the idle read state), independent of the others, and
    ///     never resumes while anyone still edits (tab-switch / Resume / Exit stay locked);
    ///   • else (nobody editing) → Resume (leave settings → game), from any tab.
    /// It stays here (not in PauseMenuController) so the arbiter keeps owning ESC while the screen owns the logic.</summary>
    public void HandleBack()
    {
        if (_controls != null && _controls.AnyEditing) { _controls.HandleBackWhileEditing(); return; }
        RequestResume();
    }

    /// <summary>F6 Phase 3 — lock/unlock the top bar while a player is rebinding (called by the CONTROLS view when
    /// the first player enters edit mode / the last one leaves). Freezes tab-switch, Resume and Exit.</summary>
    public void SetEditLock(bool locked) => _tabBar?.SetLocked(locked);

    /// <summary>The top bar, so the CONTROLS two-cursor view can drive Resume/tabs/Exit for a player whose cursor
    /// has hopped up to the bar (zone-aware navigation).</summary>
    public SettingsTabBar TabBar => _tabBar;

    // ── Resume / exit (from the tab bar) ──────────────────────────────
    public void RequestResume()
    {
        // In-game the pause flow owns timescale/audio/cursor; delegate to it. On the main menu ("Back") — or a
        // standalone/test context with no PauseMenuController — just close.
        if (!_menuContext && PauseMenuController.Instance != null) PauseMenuController.Instance.Resume();
        else Close();
    }

    public void RequestExit()
    {
        if (_confirmDialog == null) { DoExit(); return; }
        _confirmDialog.ShowExit(SaveTimeTracker.Label(), DoExit);
    }

    private void DoExit()
    {
        if (PauseMenuController.Instance != null) { PauseMenuController.Instance.ExitGame(); return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Registry ──────────────────────────────────────────────────────
    private void BuildRegistry()
    {
        _bindings.Clear();
        if (_screenRoot == null) return;
        var found = _screenRoot.GetComponentsInChildren<SettingBinding>(true);
        foreach (var b in found)
        {
            if (b == null || string.IsNullOrEmpty(b.Id)) continue;
            _bindings[b.Id] = b;
            WireEvents(b);
        }
    }

    // Owned controls: clear then add our listener (idempotent across re-opens; sidesteps the
    // -=-a-lambda pitfall since we never need to remove a single specific listener).
    private void WireEvents(SettingBinding b)
    {
        string id = b.Id;
        if (b.Dropdown != null)
        {
            b.Dropdown.onValueChanged.RemoveAllListeners();
            b.Dropdown.onValueChanged.AddListener(v => OnDropdownChanged(id, v));
        }
        if (b.Slider != null)
        {
            b.Slider.onValueChanged.RemoveAllListeners();
            b.Slider.onValueChanged.AddListener(v => OnSliderChanged(id, v));
        }
        if (b.Toggle != null)
        {
            b.Toggle.onValueChanged.RemoveAllListeners();
            b.Toggle.onValueChanged.AddListener(v => OnToggleChanged(id, v));
        }
        if (b.Button != null)
        {
            b.Button.onClick.RemoveAllListeners();
            b.Button.onClick.AddListener(() => OnButtonClicked(id));
        }
    }

    // ── Populate controls from handlers ───────────────────────────────
    private void RefreshOptionsAndValues()
    {
        _pushing = true;
        foreach (var kv in _bindings)
        {
            var b = kv.Value;
            var def = FindDef(b.Id);
            var h = HandlerFor(b.Id);
            if (def == null || h == null) continue;

            if (def.Type == SettingControlType.Dropdown && b.Dropdown != null)
            {
                var dyn = h.BuildDynamicOptions(b.Id);
                b.Dropdown.ClearOptions();
                if (dyn != null) b.Dropdown.AddOptions(new List<string>(dyn));
                else if (def.Options != null) b.Dropdown.AddOptions(new List<string>(def.Options));
            }
            PushValue(b, def, h);
        }
        _pushing = false;
    }

    // Re-push current handler values into controls without firing change events (preset propagation,
    // deferred revert). Does not rebuild dropdown option lists.
    private void RefreshValues()
    {
        _pushing = true;
        foreach (var kv in _bindings)
        {
            var b = kv.Value;
            var def = FindDef(b.Id);
            var h = HandlerFor(b.Id);
            if (def != null && h != null) PushValue(b, def, h);
        }
        _pushing = false;
    }

    private void PushValue(SettingBinding b, SettingDefinition def, ISettingHandler h)
    {
        switch (def.Type)
        {
            case SettingControlType.Dropdown:
                if (b.Dropdown != null)
                {
                    int max = Mathf.Max(0, b.Dropdown.options.Count - 1);
                    b.Dropdown.SetValueWithoutNotify(Mathf.Clamp(h.GetInt(b.Id), 0, max));
                    b.Dropdown.RefreshShownValue();
                }
                break;
            case SettingControlType.Slider:
                if (b.Slider != null)
                {
                    b.Slider.minValue = def.Min;
                    b.Slider.maxValue = def.Max;
                    b.Slider.SetValueWithoutNotify(h.GetFloat(b.Id));
                }
                break;
            case SettingControlType.Toggle:
                if (b.Toggle != null) b.Toggle.SetIsOnWithoutNotify(h.GetBool(b.Id));
                break;
        }
        if (b.ValueLabel != null)
        {
            var readout = h.GetReadout(b.Id);
            if (readout != null) b.ValueLabel.text = readout;
        }
    }

    // ── Change routing ────────────────────────────────────────────────
    private void OnDropdownChanged(string id, int value)
    {
        if (_pushing) return;
        PoTLog.Crumb(PoTCrumb.Settings, $"{id} = option {value}");
        var def = FindDef(id);
        if (def != null && def.ApplyMode == SettingApplyMode.Deferred) { StageDeferred(id, value); return; }
        HandlerFor(id)?.SetInt(id, value);
        RefreshValues();   // propagate (preset -> other rows, or any row -> preset = Custom)
    }

    private void OnSliderChanged(string id, float value)
    {
        if (_pushing) return;
        HandlerFor(id)?.SetFloat(id, value);
        RefreshValues();
    }

    private void OnToggleChanged(string id, bool value)
    {
        if (_pushing) return;
        PoTLog.Crumb(PoTCrumb.Settings, $"{id} = {(value ? "on" : "off")}");
        HandlerFor(id)?.SetBool(id, value);
        RefreshValues();
    }

    private void OnButtonClicked(string id) => HandlerFor(id)?.Invoke(id);

    // ── Deferred (resolution / window / graphics API) ─────────────────
    private void StageDeferred(string id, int newIndex)
    {
        var h = HandlerFor(id);
        if (h == null) return;

        if (id == SettingsCatalog.GraphicsApi)
        {
            h.SetInt(id, newIndex);   // persist the choice; a relaunch actually applies it
            RefreshValues();
            _confirmDialog?.ShowRestart(() =>
                GraphicsApiPreference.ApplyNow((GraphicsApiPreference.ApiChoice)Mathf.Clamp(newIndex, 0, 2)));
            return;
        }

        // Resolution / window mode: apply now, then keep-or-revert (auto-reverts on the countdown).
        _deferredId = id;
        _deferredPrevIndex = h.GetInt(id);
        h.SetInt(id, newIndex);
        RefreshValues();
        _confirmDialog?.ShowKeepRevert(10f, RevertDeferred);
    }

    private void RevertDeferred()
    {
        var h = HandlerFor(_deferredId);
        if (h == null) return;
        h.SetInt(_deferredId, _deferredPrevIndex);
        RefreshValues();
    }

    // ── Lookups ───────────────────────────────────────────────────────
    private ISettingHandler HandlerFor(string id)
    {
        foreach (var h in _handlers) if (h.Owns(id)) return h;
        return null;
    }

    private static SettingDefinition FindDef(string id)
    {
        var defs = SettingsCatalog.Definitions;
        for (int i = 0; i < defs.Count; i++) if (defs[i].Id == id) return defs[i];
        return null;
    }
}
