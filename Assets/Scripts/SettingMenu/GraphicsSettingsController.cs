using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GRAPHICS section of the settings menu (sibling of <see cref="SettingsMenuController"/>).
/// Lives in Persistent.unity so its settings apply on Start every boot and survive the
/// Restart→Bootstrap reload WITHOUT static state (PlayerPrefs "gfx_*" is the only persistence).
///
/// Options (backend call in parentheses):
///   1. Quality Preset   Low/Med/High/Custom  — master switch; sets 2–11 then applies.
///   2. VSync            (QualitySettings.vSyncCount 0/1)
///   3. FPS Cap          Off/60/120/144 (Application.targetFrameRate; only when VSync off)
///   4. Texture Quality  High/Med/Low (QualitySettings.globalTextureMipmapLimit 0/1/2)
///   5. Shadow Quality   Low/Med/High (URP shadowDistance + supportsSoftShadows + soft tier)
///   6. Anti-Aliasing    Off/SMAA (Main Camera UniversalAdditionalCameraData.antialiasing)
///   7. Ambient Occlusion(SSAO renderer feature .SetActive)
///   8. Volumetric Fog   Off/Low/High (PoTVolumetricFog feature .SetActive + fog mat _Steps 12/24)
///   9. Sun Shafts       (CoexistenceShafts feature .SetActive)
///  10. Terrain Quality  Low/Med/High (TerrainQualityService.Instance.SetTier)
///  11. Render Scale     0.7–1.0 (URP renderScale)
///
/// EDITOR-SAFETY: runtime mutation of the shared URP asset / renderer features / fog material
/// dirties those ASSETS in the editor and would persist across sessions. Awake snapshots every
/// mutated asset value; OnDestroy restores them — but ONLY under UNITY_EDITOR (a build has no
/// asset to protect and wants the last-applied values to stick for the session).
///
/// BUILD asset resolution: the URP pipeline asset, the PC_Renderer UniversalRendererData, and
/// the fog material are all SERIALIZED SLOTS on this component (wired in the Inspector). A build
/// has no AssetDatabase, so the serialized slot IS the resolution path — do not Resources.Load.
/// </summary>
public class GraphicsSettingsController : MonoBehaviour
{
    [Header("Pipeline assets (serialize for builds — do NOT duplicate)")]
    [Tooltip("PC_RPAsset — the active UniversalRenderPipelineAsset.")]
    [SerializeField] private UniversalRenderPipelineAsset _urpAsset;
    [Tooltip("PC_Renderer — the UniversalRendererData holding SSAO / fog / shafts features.")]
    [SerializeField] private ScriptableRendererData _rendererData;
    [Tooltip("M_PoTVolumetricFog material (its _Steps float is the fog quality knob).")]
    [SerializeField] private Material _fogMaterial;
    [Tooltip("Optional. Main Camera; falls back to Camera.main each apply (switch-proof).")]
    [SerializeField] private Camera _mainCamera;

    [Header("Preset")]
    [SerializeField] private TMP_Dropdown _qualityPresetDropdown;

    [Header("Performance")]
    [SerializeField] private Toggle _vsyncToggle;
    [SerializeField] private TMP_Dropdown _fpsCapDropdown;

    [Header("Quality")]
    [SerializeField] private TMP_Dropdown _textureQualityDropdown;
    [SerializeField] private TMP_Dropdown _shadowQualityDropdown;
    [SerializeField] private TMP_Dropdown _antiAliasingDropdown;
    [SerializeField] private Toggle _ambientOcclusionToggle;
    [SerializeField] private TMP_Dropdown _volumetricFogDropdown;
    [SerializeField] private Toggle _sunShaftsToggle;
    [SerializeField] private TMP_Dropdown _terrainQualityDropdown;

    [Header("Render Scale")]
    [SerializeField] private Slider _renderScaleSlider;   // range 0.7–1.0
    [SerializeField] private TMP_Text _renderScaleLabel;  // optional readout

    // ── Renderer-feature names (match PC_Renderer.asset) ──────────
    private const string SsaoFeatureName = "ScreenSpaceAmbientOcclusion";
    private const string FogFeatureName = "PoTVolumetricFog";
    private const string ShaftsFeatureName = "CoexistenceShafts";
    private static readonly int StepsID = Shader.PropertyToID("_Steps");

    // ── PlayerPrefs keys ──────────────────────────────────────────
    private const string K_Preset = "gfx_preset";
    private const string K_VSync = "gfx_vsync";
    private const string K_FpsCap = "gfx_fpscap";
    private const string K_Texture = "gfx_texture";
    private const string K_Shadow = "gfx_shadow";
    private const string K_AA = "gfx_aa";
    private const string K_AO = "gfx_ao";
    private const string K_Fog = "gfx_fog";
    private const string K_Shafts = "gfx_shafts";
    private const string K_Terrain = "gfx_terrain";
    private const string K_RenderScale = "gfx_renderscale";

    private static readonly int[] FpsCapValues = { -1, 60, 120, 144 };

    private ScriptableRendererFeature _ssaoFeature;
    private ScriptableRendererFeature _fogFeature;
    private ScriptableRendererFeature _shaftsFeature;

    // Suppress the "→ Custom" reaction while a preset or the loader drives the controls.
    private bool _suppressCustom;

    // ── Editor-safety snapshot ────────────────────────────────────
#if UNITY_EDITOR
    private struct AssetSnapshot
    {
        public float renderScale, shadowDistance, fogSteps;
        public bool softShadows, ssao, fog, shafts;
        public int softShadowQuality;   // -1 = field not found
        public int vSyncCount, mipmapLimit, targetFps;
        public bool valid;
    }
    private AssetSnapshot _snap;
#endif

    private void Awake()
    {
        // Resolve feature instances up front so both apply and snapshot use the same refs.
        _ssaoFeature = FindFeature(SsaoFeatureName);
        _fogFeature = FindFeature(FogFeatureName);
        _shaftsFeature = FindFeature(ShaftsFeatureName);

        if (_urpAsset == null)
            Debug.LogError("[GraphicsSettingsController] URP asset unwired — render scale / shadows dead.", this);
        if (_rendererData == null)
            Debug.LogError("[GraphicsSettingsController] Renderer data unwired — SSAO / fog / shafts dead.", this);

#if UNITY_EDITOR
        SnapshotAssets();
#endif
    }

    private void Start()
    {
        BuildDropdowns();
        LoadAndApplyAll();
        WireHandlers();
    }

    // ── Feature lookup ────────────────────────────────────────────
    private ScriptableRendererFeature FindFeature(string featureName)
    {
        if (_rendererData == null || _rendererData.rendererFeatures == null) return null;
        foreach (var f in _rendererData.rendererFeatures)
            if (f != null && f.name == featureName) return f;
        Debug.LogWarning($"[GraphicsSettingsController] Renderer feature '{featureName}' not found on PC_Renderer.", this);
        return null;
    }

    // ── Dropdown option labels ────────────────────────────────────
    private void BuildDropdowns()
    {
        SetOptions(_qualityPresetDropdown, "Low", "Medium", "High", "Custom");
        SetOptions(_fpsCapDropdown, "Off", "60", "120", "144");
        SetOptions(_textureQualityDropdown, "High", "Medium", "Low");
        SetOptions(_shadowQualityDropdown, "Low", "Medium", "High");
        SetOptions(_antiAliasingDropdown, "Off", "SMAA");
        SetOptions(_volumetricFogDropdown, "Off", "Low", "High");
        SetOptions(_terrainQualityDropdown, "Low", "Medium", "High");
        if (_renderScaleSlider != null)
        {
            _renderScaleSlider.minValue = 0.7f;
            _renderScaleSlider.maxValue = 1.0f;
        }
    }

    private static void SetOptions(TMP_Dropdown d, params string[] labels)
    {
        if (d == null) return;
        d.ClearOptions();
        d.AddOptions(new List<string>(labels));
    }

    // ── Load from prefs → controls → apply ────────────────────────
    private void LoadAndApplyAll()
    {
        _suppressCustom = true;

        if (_vsyncToggle) _vsyncToggle.isOn = PlayerPrefs.GetInt(K_VSync, 1) == 1;
        SetDropdown(_fpsCapDropdown, PlayerPrefs.GetInt(K_FpsCap, 0));
        SetDropdown(_textureQualityDropdown, PlayerPrefs.GetInt(K_Texture, 0));
        SetDropdown(_shadowQualityDropdown, PlayerPrefs.GetInt(K_Shadow, 2));
        SetDropdown(_antiAliasingDropdown, PlayerPrefs.GetInt(K_AA, 1));
        if (_ambientOcclusionToggle) _ambientOcclusionToggle.isOn = PlayerPrefs.GetInt(K_AO, 1) == 1;
        SetDropdown(_volumetricFogDropdown, PlayerPrefs.GetInt(K_Fog, 2));
        if (_sunShaftsToggle) _sunShaftsToggle.isOn = PlayerPrefs.GetInt(K_Shafts, 1) == 1;
        SetDropdown(_terrainQualityDropdown, PlayerPrefs.GetInt(K_Terrain, 2));
        if (_renderScaleSlider) _renderScaleSlider.value = PlayerPrefs.GetFloat(K_RenderScale, 1.0f);
        SetDropdown(_qualityPresetDropdown, PlayerPrefs.GetInt(K_Preset, 3));   // default Custom (i.e. per-control)

        ApplyAll();
        _suppressCustom = false;
    }

    private static void SetDropdown(TMP_Dropdown d, int value)
    {
        if (d == null) return;
        d.value = Mathf.Clamp(value, 0, Mathf.Max(0, d.options.Count - 1));
        d.RefreshShownValue();
    }

    private void WireHandlers()
    {
        _qualityPresetDropdown?.onValueChanged.AddListener(OnPresetChanged);
        _vsyncToggle?.onValueChanged.AddListener(OnVSyncChanged);
        _fpsCapDropdown?.onValueChanged.AddListener(OnFpsCapChanged);
        _textureQualityDropdown?.onValueChanged.AddListener(OnTextureChanged);
        _shadowQualityDropdown?.onValueChanged.AddListener(OnShadowChanged);
        _antiAliasingDropdown?.onValueChanged.AddListener(OnAntiAliasingChanged);
        _ambientOcclusionToggle?.onValueChanged.AddListener(OnAmbientOcclusionChanged);
        _volumetricFogDropdown?.onValueChanged.AddListener(OnVolumetricFogChanged);
        _sunShaftsToggle?.onValueChanged.AddListener(OnSunShaftsChanged);
        _terrainQualityDropdown?.onValueChanged.AddListener(OnTerrainChanged);
        _renderScaleSlider?.onValueChanged.AddListener(OnRenderScaleChanged);
    }

    // ── Per-control handlers: apply + save + mark Custom ──────────
    private void OnVSyncChanged(bool _)          { ApplyVSync(); ApplyFpsCap(); Save(); MarkCustom(); }
    private void OnFpsCapChanged(int _)          { ApplyFpsCap(); Save(); MarkCustom(); }
    private void OnTextureChanged(int _)         { ApplyTexture(); Save(); MarkCustom(); }
    private void OnShadowChanged(int _)          { ApplyShadow(); Save(); MarkCustom(); }
    private void OnAntiAliasingChanged(int _)    { ApplyAntiAliasing(); Save(); MarkCustom(); }
    private void OnAmbientOcclusionChanged(bool _){ ApplyAmbientOcclusion(); Save(); MarkCustom(); }
    private void OnVolumetricFogChanged(int _)   { ApplyVolumetricFog(); Save(); MarkCustom(); }
    private void OnSunShaftsChanged(bool _)      { ApplySunShafts(); Save(); MarkCustom(); }
    private void OnTerrainChanged(int _)         { ApplyTerrain(); Save(); MarkCustom(); }
    private void OnRenderScaleChanged(float _)   { ApplyRenderScale(); Save(); MarkCustom(); }

    private void MarkCustom()
    {
        if (_suppressCustom || _qualityPresetDropdown == null) return;
        _suppressCustom = true;
        _qualityPresetDropdown.value = 3;   // Custom
        _qualityPresetDropdown.RefreshShownValue();
        PlayerPrefs.SetInt(K_Preset, 3);
        PlayerPrefs.Save();
        _suppressCustom = false;
    }

    // ── Preset (master switch) ────────────────────────────────────
    private void OnPresetChanged(int index)
    {
        if (_suppressCustom) return;
        if (index >= 3) { Save(); return; }   // Custom — leave controls as-is

        _suppressCustom = true;
        // preset → per-control values. Higher index = higher quality on each control.
        //                   Low(0)  Medium(1)  High(2)
        bool vsync;
        int fps, tex, shadow, aa, fog, terrain; bool ao, shafts; float rs;
        switch (index)
        {
            case 0: // Low
                vsync = false; fps = 1 /*60*/; tex = 2 /*Low*/; shadow = 0; aa = 0;
                ao = false; fog = 0; shafts = false; terrain = 0; rs = 0.7f; break;
            case 1: // Medium
                vsync = true; fps = 0; tex = 1; shadow = 1; aa = 1;
                ao = true; fog = 1; shafts = true; terrain = 1; rs = 0.85f; break;
            default: // High
                vsync = true; fps = 0; tex = 0; shadow = 2; aa = 1;
                ao = true; fog = 2; shafts = true; terrain = 2; rs = 1.0f; break;
        }

        if (_vsyncToggle) _vsyncToggle.isOn = vsync;
        SetDropdown(_fpsCapDropdown, fps);
        SetDropdown(_textureQualityDropdown, tex);
        SetDropdown(_shadowQualityDropdown, shadow);
        SetDropdown(_antiAliasingDropdown, aa);
        if (_ambientOcclusionToggle) _ambientOcclusionToggle.isOn = ao;
        SetDropdown(_volumetricFogDropdown, fog);
        if (_sunShaftsToggle) _sunShaftsToggle.isOn = shafts;
        SetDropdown(_terrainQualityDropdown, terrain);
        if (_renderScaleSlider) _renderScaleSlider.value = rs;

        ApplyAll();
        Save();
        _suppressCustom = false;
    }

    // ── Apply-all (Start + preset) ────────────────────────────────
    private void ApplyAll()
    {
        ApplyVSync();
        ApplyFpsCap();
        ApplyTexture();
        ApplyShadow();
        ApplyAntiAliasing();
        ApplyAmbientOcclusion();
        ApplyVolumetricFog();
        ApplySunShafts();
        ApplyTerrain();
        ApplyRenderScale();
    }

    // ── Value sourcing ────────────────────────────────────────────
    // The UI controls are only an EDITING SURFACE over the persisted values. When a control
    // is unwired (e.g. UI not yet built), the applied value comes straight from PlayerPrefs —
    // so boot-time apply honours saved settings AND their defaults (AO/shafts ON, fog High)
    // instead of collapsing to a control's null-default.
    private int DropVal(TMP_Dropdown d, string key, int def) => d != null ? d.value : PlayerPrefs.GetInt(key, def);
    private bool TogVal(Toggle t, string key, bool def) => t != null ? t.isOn : PlayerPrefs.GetInt(key, def ? 1 : 0) == 1;
    private float SliderVal(Slider s, string key, float def) => s != null ? s.value : PlayerPrefs.GetFloat(key, def);

    // ── Individual apply methods ──────────────────────────────────
    private void ApplyVSync()
    {
        int on = TogVal(_vsyncToggle, K_VSync, true) ? 1 : 0;
        QualitySettings.vSyncCount = on;
        if (_fpsCapDropdown != null) _fpsCapDropdown.interactable = on == 0;   // FPS cap only when VSync off
    }

    private void ApplyFpsCap()
    {
        // VSync overrides targetFrameRate anyway; still set it so toggling VSync off is instant.
        int idx = DropVal(_fpsCapDropdown, K_FpsCap, 0);
        Application.targetFrameRate = FpsCapValues[Mathf.Clamp(idx, 0, FpsCapValues.Length - 1)];
    }

    private void ApplyTexture()
    {
        // 0 High → mip 0, 1 Medium → mip 1, 2 Low → mip 2
        int idx = DropVal(_textureQualityDropdown, K_Texture, 0);
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(idx, 0, 2);
    }

    private void ApplyShadow()
    {
        if (_urpAsset == null) return;
        int idx = DropVal(_shadowQualityDropdown, K_Shadow, 2);
        // Low / Medium / High → distance + soft-shadow tier
        switch (idx)
        {
            case 0: _urpAsset.shadowDistance = 20f; SetSoftShadows(false, 1); break;
            case 1: _urpAsset.shadowDistance = 35f; SetSoftShadows(true, 2); break;
            default: _urpAsset.shadowDistance = 50f; SetSoftShadows(true, 3); break;
        }
    }

    // supportsSoftShadows / softShadowQuality have no public setters; set the serialized
    // fields by reflection (best-effort — a URP API rename leaves shadowDistance still applied).
    private void SetSoftShadows(bool enabled, int tier)
    {
        SetPrivateField(_urpAsset, "m_SoftShadowsSupported", enabled);
        SetPrivateField(_urpAsset, "m_SoftShadowQuality", tier);
    }

    private static void SetPrivateField(object target, string field, object value)
    {
        if (target == null) return;
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return;
        try { f.SetValue(target, value); } catch { /* type shape changed — non-fatal */ }
    }

    private static int GetPrivateInt(object target, string field, int fallback)
    {
        if (target == null) return fallback;
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return fallback;
        try { return (int)f.GetValue(target); } catch { return fallback; }
    }

    private static bool GetPrivateBool(object target, string field, bool fallback)
    {
        if (target == null) return fallback;
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return fallback;
        try { return (bool)f.GetValue(target); } catch { return fallback; }
    }

    private void ApplyAntiAliasing()
    {
        var cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;
        int idx = DropVal(_antiAliasingDropdown, K_AA, 1);
        data.antialiasing = idx == 1
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
            : AntialiasingMode.None;
    }

    private void ApplyAmbientOcclusion()
    {
        if (_ssaoFeature == null) return;
        _ssaoFeature.SetActive(TogVal(_ambientOcclusionToggle, K_AO, true));
    }

    private void ApplyVolumetricFog()
    {
        int idx = DropVal(_volumetricFogDropdown, K_Fog, 2);   // Off/Low/High
        if (_fogFeature != null) _fogFeature.SetActive(idx > 0);
        if (_fogMaterial != null && idx > 0)
            _fogMaterial.SetFloat(StepsID, idx == 1 ? 12f : 24f);
    }

    private void ApplySunShafts()
    {
        if (_shaftsFeature == null) return;
        _shaftsFeature.SetActive(TogVal(_sunShaftsToggle, K_Shafts, true));
    }

    private void ApplyTerrain()
    {
        int idx = DropVal(_terrainQualityDropdown, K_Terrain, 2);
        TerrainQualityService.Instance?.SetTier(Mathf.Clamp(idx, 0, 2));
    }

    private void ApplyRenderScale()
    {
        if (_urpAsset == null) return;
        float rs = Mathf.Clamp(SliderVal(_renderScaleSlider, K_RenderScale, 1.0f), 0.7f, 1.0f);
        _urpAsset.renderScale = rs;
        if (_renderScaleLabel != null) _renderScaleLabel.text = rs.ToString("0.00");
    }

    // ── Persistence ───────────────────────────────────────────────
    private void Save()
    {
        if (_qualityPresetDropdown) PlayerPrefs.SetInt(K_Preset, _qualityPresetDropdown.value);
        if (_vsyncToggle) PlayerPrefs.SetInt(K_VSync, _vsyncToggle.isOn ? 1 : 0);
        if (_fpsCapDropdown) PlayerPrefs.SetInt(K_FpsCap, _fpsCapDropdown.value);
        if (_textureQualityDropdown) PlayerPrefs.SetInt(K_Texture, _textureQualityDropdown.value);
        if (_shadowQualityDropdown) PlayerPrefs.SetInt(K_Shadow, _shadowQualityDropdown.value);
        if (_antiAliasingDropdown) PlayerPrefs.SetInt(K_AA, _antiAliasingDropdown.value);
        if (_ambientOcclusionToggle) PlayerPrefs.SetInt(K_AO, _ambientOcclusionToggle.isOn ? 1 : 0);
        if (_volumetricFogDropdown) PlayerPrefs.SetInt(K_Fog, _volumetricFogDropdown.value);
        if (_sunShaftsToggle) PlayerPrefs.SetInt(K_Shafts, _sunShaftsToggle.isOn ? 1 : 0);
        if (_terrainQualityDropdown) PlayerPrefs.SetInt(K_Terrain, _terrainQualityDropdown.value);
        if (_renderScaleSlider) PlayerPrefs.SetFloat(K_RenderScale, _renderScaleSlider.value);
        PlayerPrefs.Save();
    }

    // ── Editor-safety: snapshot on Awake, restore on OnDestroy ────
#if UNITY_EDITOR
    private void SnapshotAssets()
    {
        _snap = new AssetSnapshot { valid = true };
        if (_urpAsset != null)
        {
            _snap.renderScale = _urpAsset.renderScale;
            _snap.shadowDistance = _urpAsset.shadowDistance;
            _snap.softShadows = GetPrivateBool(_urpAsset, "m_SoftShadowsSupported", _urpAsset.supportsSoftShadows);
            _snap.softShadowQuality = GetPrivateInt(_urpAsset, "m_SoftShadowQuality", -1);
        }
        _snap.ssao = _ssaoFeature != null && _ssaoFeature.isActive;
        _snap.fog = _fogFeature != null && _fogFeature.isActive;
        _snap.shafts = _shaftsFeature != null && _shaftsFeature.isActive;
        if (_fogMaterial != null) _snap.fogSteps = _fogMaterial.GetFloat(StepsID);
        _snap.vSyncCount = QualitySettings.vSyncCount;
        _snap.mipmapLimit = QualitySettings.globalTextureMipmapLimit;
        _snap.targetFps = Application.targetFrameRate;
    }

    private void OnDestroy()
    {
        if (!_snap.valid) return;
        if (_urpAsset != null)
        {
            _urpAsset.renderScale = _snap.renderScale;
            _urpAsset.shadowDistance = _snap.shadowDistance;
            SetPrivateField(_urpAsset, "m_SoftShadowsSupported", _snap.softShadows);
            if (_snap.softShadowQuality >= 0)
                SetPrivateField(_urpAsset, "m_SoftShadowQuality", _snap.softShadowQuality);
        }
        _ssaoFeature?.SetActive(_snap.ssao);
        _fogFeature?.SetActive(_snap.fog);
        _shaftsFeature?.SetActive(_snap.shafts);
        if (_fogMaterial != null) _fogMaterial.SetFloat(StepsID, _snap.fogSteps);
        QualitySettings.vSyncCount = _snap.vSyncCount;
        QualitySettings.globalTextureMipmapLimit = _snap.mipmapLimit;
        Application.targetFrameRate = _snap.targetFps;
    }
#endif
}
